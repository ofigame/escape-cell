using System;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The cube-headed robot. Its logical position changes the instant a move starts,
    /// so swiping away from a tile just before impact always saves you.
    /// </summary>
    public class Robot : MonoBehaviour
    {
        private const float HopDuration = 0.14f; // snappy, but slow enough to see and follow each step
        /// <summary>Hops take this share of their usual time (the big robot of the core loop hops quicker, so it never feels sluggish).</summary>
        public static float HopScale = 1f;
        private const float HopHeight = 0.35f;
        private const int MaxJumpHoles = 2;
        private static readonly Quaternion FacingCamera = Quaternion.LookRotation(new Vector3(-1f, 0f, -1f));

        public GridPos Position { get; private set; }
        public bool IsAlive { get; private set; }
        public GridPos LastLeftTile { get; private set; }
        public float LastLeftTime { get; private set; } = -10f;

        public bool IsShielded => shieldLeft > 0f;
        public bool IsHopping => anim == Anim.Hop;

        /// <summary>Lifted into the air by the hover escape: blocks pass beneath it.</summary>
        public bool IsHovering => anim == Anim.Hover;

        /// <summary>Direction of the last move; a jump goes this way.</summary>
        public Direction Facing { get; private set; } = Direction.MinusY;

        /// <summary>Called when an armored robot hops into a landed block; returns true if the block was smashed.</summary>
        public Func<GridPos, bool> BlockSmasher;
        public float ShieldLeft => shieldLeft;

        /// <summary>Raised when a hop lands on its target tile.</summary>
        public event Action<GridPos> Arrived;
        /// <summary>The robot ran into something standing on this tile (a block, a pillar, a monster) and bounced off.</summary>
        public event Action<GridPos> BumpedInto;

        private Transform visual;
        private Material eyeMaterial;
        private Material bodyMaterial;
        private Material lightMaterial;
        private Transform accessories;
        private int lookWorld = -1;
        private Material shadowMaterial;
        private Transform shadow;
        private Transform bubble;
        private Material bubbleMaterial;
        private Transform eyeL, eyeR;
        private float shieldLeft;
        private int shieldHits, shieldMaxHits = 1;
        private float crackT = 1f;
        private float blinkTimer = 2f;
        private GridModel grid;

        private enum Anim { Idle, Hop, Bump, Squash, Fall, Cheer, Hover, Escape }
        private Anim anim;
        private float animTime;
        private Vector3 from, to;
        private Direction? bufferedMove;
        private float bufferedAt;
        private const float BufferLife = 0.25f; // an older swipe is dropped instead of surprising the player later
        private bool bufferedJump;
        private float hopDuration = HopDuration;
        private float hopHeight = HopHeight;
        private float flinchT = 1f;
        private Vector3 flinchDir;
        private Quaternion targetFacing = Quaternion.identity;

        public static Robot Create(Transform parent)
        {
            var root = new GameObject("Robot");
            root.transform.SetParent(parent, false);
            var robot = root.AddComponent<Robot>();
            robot.BuildVisual();
            return robot;
        }

        private void BuildVisual()
        {
            // Lift the model onto the tile surface; "visual" is what animations scale and rotate.
            var lift = new GameObject("Lift").transform;
            lift.SetParent(transform, false);
            lift.localPosition = new Vector3(0f, GridView.SurfaceY, 0f);
            visual = new GameObject("Visual").transform;
            visual.SetParent(lift, false);

            var body = bodyMaterial = MaterialFactory.Create(Palette.RobotBody, Color.black);
            var light = lightMaterial = MaterialFactory.Create(Palette.RobotLight, Color.black);
            var dark = MaterialFactory.Create(Palette.RobotDark, Color.black);
            eyeMaterial = MaterialFactory.Create(Palette.RobotEye, Palette.RobotEye);

            darkMaterial = dark;
            hero = Mathf.Clamp(Data.SaveData.Hero, 0, HeroModels.Count - 1);
            (eyeL, eyeR) = HeroModels.Build(visual, hero, body, light, dark, eyeMaterial);

            accessories = new GameObject("Accessories").transform;
            accessories.SetParent(visual, false);

            shadowMaterial = MaterialFactory.CreateTransparent(new Color(0.22f, 0.2f, 0.45f, 0.3f), Color.black);
            shadow = Shapes.Primitive(PrimitiveType.Cylinder, "Shadow", transform, Vector3.zero, new Vector3(0.48f, 0.004f, 0.48f), shadowMaterial).transform;

            bubbleMaterial = MaterialFactory.CreateTransparent(Palette.ShieldBubble, Palette.ShieldGlow);
            bubble = Shapes.Primitive(PrimitiveType.Sphere, "Shield", lift, new Vector3(0f, 0.4f, 0f), Vector3.one * 0.95f, bubbleMaterial).transform;
            bubble.gameObject.SetActive(false);
        }

        /// <summary>The animated model (scaled and rotated by animations); other modes may pose it directly.</summary>
        public Transform Visual => visual;

        /// <summary>Shield for <paramref name="seconds"/> that can take <paramref name="hits"/> blocks (upgrades add hits).</summary>
        public void GiveShield(float seconds, int hits = 1)
        {
            if (shieldLeft <= 0f) shieldHits = 0;
            shieldLeft = Mathf.Max(shieldLeft, seconds);
            shieldHits = Mathf.Max(shieldHits, hits);
            shieldMaxHits = Mathf.Max(1, shieldHits);
            crackT = 1f;
        }

        /// <summary>Blocks the shield can still take.</summary>
        public int ShieldHits => IsShielded ? shieldHits : 0;

        /// <summary>
        /// A block hit the shielded robot: the shield cracks (and changes colour) instead of vanishing, until its
        /// last hit. Returns true when that hit broke it.
        /// </summary>
        public bool AbsorbHit()
        {
            shieldHits--;
            crackT = 0f;
            if (shieldHits > 0) return false;
            shieldLeft = 0f;
            return true;
        }

        private int hero;
        private Material darkMaterial;
        public int Hero => hero;

        // The build's own colours, tinted a little by the world (the classic build takes the world's colours as before).
        private Color OwnBody(int world) => hero == 0 ? RobotLooks.BodyColor(world) : Color.Lerp(HeroModels.Colours(hero).body, RobotLooks.BodyColor(world), 0.2f);
        private Color OwnLight(int world) => hero == 0 ? RobotLooks.LightColor(world) : Color.Lerp(HeroModels.Colours(hero).light, RobotLooks.LightColor(world), 0.15f);
        private Color OwnEye(int world) => hero == 0 ? RobotLooks.EyeColor(world) : HeroModels.Colours(hero).eye;

        /// <summary>Switches to another of foi's four builds (rebuilds the body parts in place).</summary>
        public void SetHero(int newHero)
        {
            newHero = Mathf.Clamp(newHero, 0, HeroModels.Count - 1);
            if (newHero == hero) return;
            hero = newHero;
            for (int i = visual.childCount - 1; i >= 0; i--)
            {
                var c = visual.GetChild(i);
                if (HeroModels.IsPart(c.name)) DestroyImmediate(c.gameObject);
            }
            (eyeL, eyeR) = HeroModels.Build(visual, hero, bodyMaterial, lightMaterial, darkMaterial, eyeMaterial);
            int w = lookWorld;
            lookWorld = -1;
            ApplyWorld(Mathf.Max(0, w));
        }

        /// <summary>Dress the robot for a world: its colors and the gear it has earned so far.</summary>
        public void ApplyWorld(int world)
        {
            if (world == lookWorld) return;
            lookWorld = world;
            MaterialFactory.SetColors(bodyMaterial, OwnBody(world), Color.black);
            MaterialFactory.SetColors(lightMaterial, OwnLight(world), Color.black);
            var eye = OwnEye(world);
            MaterialFactory.SetColors(eyeMaterial, eye, eye);
            RobotLooks.Build(accessories, world);
        }

        private Transform cosmetics;
        private string dance = "dance.spin";

        /// <summary>Hop trail colour from the garage (null = none).</summary>
        public Color? TrailColor { get; private set; }

        /// <summary>
        /// Dress the robot in a garage outfit on top of its world look: paint, eye colour, a hat, back gear, a hop trail
        /// and a victory dance. A garage hat or back piece replaces the world's own gear.
        /// </summary>
        public void ApplyOutfit(System.Collections.Generic.Dictionary<Data.Slot, Data.Cosmetic> outfit)
        {
            int world = Mathf.Max(0, lookWorld);
            var paint = outfit[Data.Slot.Color];
            var bodyColor = paint.IsDefault ? OwnBody(world) : paint.color;
            var lightColor = paint.IsDefault ? OwnLight(world) : Color.Lerp(paint.color, Color.white, 0.35f);
            MaterialFactory.SetColors(bodyMaterial, bodyColor, Color.black);
            MaterialFactory.SetColors(lightMaterial, lightColor, Color.black);
            var eyes = outfit[Data.Slot.Eyes];
            var eye = eyes.IsDefault ? OwnEye(world) : eyes.color;
            MaterialFactory.SetColors(eyeMaterial, eye, eye);

            if (cosmetics == null)
            {
                cosmetics = new GameObject("Cosmetics").transform;
                cosmetics.SetParent(visual, false);
            }
            for (int i = cosmetics.childCount - 1; i >= 0; i--) Destroy(cosmetics.GetChild(i).gameObject);
            var hat = outfit[Data.Slot.Hat];
            var back = outfit[Data.Slot.Back];
            if (!hat.IsDefault) CosmeticModels.Hat(cosmetics, hat.id, hat.color);
            if (!back.IsDefault) CosmeticModels.Back(cosmetics, back.id, back.color);
            accessories.gameObject.SetActive(hat.IsDefault && back.IsDefault);

            // Arm and leg pieces replace the plain ones; shaped eyes replace the plain eye blocks.
            var arms = outfit[Data.Slot.Arms];
            var legs = outfit[Data.Slot.Legs];
            if (!arms.IsDefault) CosmeticModels.Arms(cosmetics, arms.id, arms.color);
            if (!legs.IsDefault) CosmeticModels.Legs(cosmetics, legs.id, legs.color);
            foreach (var part in new[] { "ArmL", "ArmR" }) SetPartShown(part, arms.IsDefault);
            foreach (var part in new[] { "LegL", "LegR", "FootL", "FootR" }) SetPartShown(part, legs.IsDefault);
            var badge = outfit[Data.Slot.Badge];
            if (!badge.IsDefault) CosmeticModels.Badge(cosmetics, badge.id, badge.color);

            bool shaped = Data.Cosmetics.ShapedEyes(eyes.id);
            foreach (var e in new[] { eyeL, eyeR })
            {
                var old = e.Find("Shape");
                if (old != null) Destroy(old.gameObject);
                e.GetComponent<MeshRenderer>().enabled = !shaped;
                if (shaped) CosmeticModels.EyeShape(e, eyes.id, eyeMaterial);
            }

            var step = outfit[Data.Slot.Step];
            StepId = step.IsDefault ? null : step.id;
            StepColor = step.color;
            var trail = outfit[Data.Slot.Trail];
            TrailColor = trail.IsDefault ? (Color?)null : trail.color;
            dance = outfit[Data.Slot.Dance].id;
        }

        /// <summary>Step mark from the paint workshop (null = none) and its colour.</summary>
        public string StepId { get; private set; }
        public Color StepColor { get; private set; }

        private void SetPartShown(string name, bool shown)
        {
            var part = visual.Find(name);
            if (part != null) part.gameObject.SetActive(shown);
        }

        private void UpdateShieldAndBlink()
        {
            if (shieldLeft > 0f && IsAlive) shieldLeft -= Dt;
            bool show = shieldLeft > 0f && (shieldLeft > 1.2f || Mathf.Repeat(shieldLeft, 0.2f) > 0.08f);
            bubble.gameObject.SetActive(show);
            if (show)
            {
                // Full shield: cyan. Each hit taken shifts it toward amber, then red; a hit makes it wobble.
                crackT = Mathf.Min(1f, crackT + Dt * 3f);
                float health = shieldMaxHits <= 1 ? 1f : (shieldHits - 1f) / (shieldMaxHits - 1f);
                var tint = health >= 1f ? Palette.ShieldBubble : Color.Lerp(new Color(1f, 0.35f, 0.35f, 0.45f), new Color(1f, 0.8f, 0.3f, 0.45f), health);
                var glow = health >= 1f ? Palette.ShieldGlow : Color.Lerp(new Color(2f, 0.3f, 0.3f), new Color(2f, 1.3f, 0.3f), health);
                MaterialFactory.SetColors(bubbleMaterial, tint, glow);
                float wobble = Mathf.Sin(crackT * Mathf.PI * 3f) * (1f - crackT) * 0.18f;
                bubble.localScale = Vector3.one * (0.95f + Mathf.Sin(Time.time * 6f) * 0.03f + wobble);
                bubble.Rotate(0f, 60f * Dt, 0f);
            }

            // Occasional eye blink keeps the robot feeling alive.
            blinkTimer -= Dt;
            float eyeY = blinkTimer < 0.12f ? 0.15f : 1f;
            if (blinkTimer < 0f) blinkTimer = UnityEngine.Random.Range(2f, 4.5f);
            eyeL.localScale = eyeR.localScale = new Vector3(1f, eyeY, 1f);
        }


        private void LateUpdate()
        {
            UpdateShieldAndBlink();

            // The shadow stays on the floor while the robot hops, and shrinks with height.
            bool grounded = anim != Anim.Fall;
            shadow.gameObject.SetActive(grounded);
            if (!grounded) return;

            var p = transform.position;
            float height = Mathf.Max(0f, p.y);
            shadow.position = new Vector3(p.x, GridView.SurfaceY + 0.004f, p.z);
            float s = 0.48f * (1f - Mathf.Clamp01(height) * 0.35f);
            shadow.localScale = new Vector3(s, 0.004f, s);
        }

        public void Spawn(GridModel model, GridPos start)
        {
            grid = model;
            Position = start;
            IsAlive = true;
            shieldLeft = 0f;
            bufferedMove = null;
            bufferedJump = false;
            Facing = Direction.MinusY;
            LastLeftTime = -10f;
            anim = Anim.Idle;
            flinchT = 1f;
            transform.position = GridView.ToWorld(start);
            visual.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            targetFacing = FacingCamera;
            visual.localRotation = targetFacing;
            gameObject.SetActive(true);
        }

        /// <summary>Arena fights (TPS): the arena moves the robot freely; grid moves and jumps are off meanwhile.</summary>
        public bool InArena { get; private set; }

        public void EnterArena()
        {
            InArena = true;
            bufferedMove = null;
            bufferedJump = false;
            anim = Anim.Idle;
            visual.localScale = Vector3.one;
        }

        public void ExitArena() => InArena = false;

        /// <summary>Places the robot (arena only) and turns it toward <paramref name="facing"/> (y ignored).</summary>
        public void ArenaPlace(Vector3 position, Vector3 facing, bool snapTurn = false)
        {
            transform.position = position;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f) return;
            targetFacing = Quaternion.LookRotation(facing);
            if (snapTurn) visual.localRotation = targetFacing;
        }

        /// <summary>A little squash and stretch while running in the arena.</summary>
        public void ArenaBob(float amount)
        {
            float s = 1f + Mathf.Sin(Time.time * 18f) * 0.06f * amount;
            visual.localScale = new Vector3(1f / s, s, 1f / s);
        }

        /// <summary>Turn to look straight at the camera (the garage shows the robot off from the front).</summary>
        public void FaceCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var back = -cam.transform.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f) return;
            targetFacing = Quaternion.LookRotation(back);
            visual.localRotation = targetFacing;
        }

        private const float HoverHeight = 1.15f;

        /// <summary>Hover escape: rise into the air and wait for a landing tile.</summary>
        public void StartHover()
        {
            if (!IsAlive || anim == Anim.Hop || anim == Anim.Bump || anim == Anim.Hover) return;
            bufferedMove = null;
            bufferedJump = false;
            targetFacing = FacingCamera;
            StartAnim(Anim.Hover, GridView.ToWorld(Position), GridView.ToWorld(Position));
        }

        /// <summary>Glide down from the hover onto <paramref name="target"/>.</summary>
        public void EndHover(GridPos target)
        {
            if (anim != Anim.Hover) return;
            var offset = new Vector3(target.x - Position.x, 0f, target.y - Position.y);
            if (offset.sqrMagnitude > 0.01f) targetFacing = Quaternion.LookRotation(offset);
            LastLeftTile = Position;
            LastLeftTime = Time.time;
            Position = target;
            hopDuration = 0.28f;
            hopHeight = 0.25f;
            StartAnim(Anim.Hop, transform.position, GridView.ToWorld(target));
        }

        /// <summary>A rescue charge pulled the robot out of a hole or fire: bounce back to a safe tile.</summary>
        public void RescueTo(GridPos safe)
        {
            bufferedMove = null;
            bufferedJump = false;
            Position = safe;
            visual.localScale = Vector3.one;
            hopDuration = 0.4f;
            hopHeight = 1.1f;
            StartAnim(Anim.Hop, transform.position, GridView.ToWorld(safe));
        }

        /// <summary>A still copy of the robot's look (body and outfit, no behaviour), for the mission briefing's stage.</summary>
        public GameObject BuildLookalike(Transform parent)
        {
            var copy = Instantiate(visual.gameObject, parent, false);
            copy.name = "RobotLook";
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>()) Destroy(b);
            return copy;
        }

        public void TryMove(Direction dir)
        {
            if (InArena) return;
            if (!IsAlive || anim == Anim.Hover) return;
            if (anim == Anim.Hop || anim == Anim.Bump)
            {
                bufferedMove = dir; // keeps fast swipes responsive
                bufferedAt = Time.time;
                bufferedJump = false;
                return;
            }

            // Floor rules can hold the robot back (sticky candy needs a second swipe).
            if (CanLeave != null && !CanLeave(dir))
            {
                var o = dir.ToOffset();
                StartAnim(Anim.Bump, transform.position, transform.position + new Vector3(o.x, 0f, o.y) * 0.12f);
                AudioManager.PlaySfx(Sfx.Bump, 0.4f, 0.7f);
                Haptics.Light();
                return;
            }

            var offset = dir.ToOffset();
            var target = Position + offset;
            Facing = dir;
            targetFacing = Quaternion.LookRotation(new Vector3(offset.x, 0f, offset.y));

            // Swiping toward a hole or fire leaps over it automatically, so jumping stays in the flow of play.
            if (grid.IsGap(target))
            {
                Jump(dir);
                return;
            }

            // With armor, hopping onto a landed block smashes it and the robot takes the tile.
            bool blocked = grid.InBounds(target) && grid.IsOccupied(target) && !(IsShielded && BlockSmasher != null && BlockSmasher(target));
            if (!grid.InBounds(target) || blocked)
            {
                if (blocked) BumpedInto?.Invoke(target);
                StartAnim(Anim.Bump, transform.position, transform.position + new Vector3(offset.x, 0f, offset.y) * 0.25f);
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                Haptics.Light();
                return;
            }

            HopTo(target, 1);
            AudioManager.PlaySfx(Sfx.Hop, 0.55f, 1f, 0.08f);
            Haptics.Light();
        }

        /// <summary>
        /// Double-tap jump in the facing direction (swipes toward a hole jump on their own, see TryMove).
        /// Leaps over one or two holes onto the first solid tile behind them.
        /// With nothing solid behind the holes, the robot drops into the first one.
        /// Without a hole ahead it is just a normal step.
        /// </summary>
        public void TryJump()
        {
            if (InArena) return;
            if (!IsAlive || anim == Anim.Hover) return;
            if (anim == Anim.Hop || anim == Anim.Bump)
            {
                bufferedJump = true;
                bufferedMove = null;
                return;
            }

            var next = Position + Facing.ToOffset();
            if (!grid.IsGap(next))
            {
                TryMove(Facing);
                return;
            }
            Jump(Facing);
        }

        private void Jump(Direction dir)
        {
            var offset = dir.ToOffset();
            var next = Position + offset;

            var landing = next;
            int holes = 0;
            while (grid.IsGap(landing) && holes < MaxJumpHoles)
            {
                landing += offset;
                holes++;
            }

            bool noLanding = !grid.InBounds(landing) || grid.IsGap(landing);
            if (noLanding && grid.Exists(next))
            {
                landing = next; // nothing to land on: fall short into the first hole
            }
            else if (noLanding || (grid.IsOccupied(landing) && !(IsShielded && BlockSmasher != null && BlockSmasher(landing))))
            {
                // A block or obstacle sits where we'd land, or it is the platform's edge: refuse the jump.
                StartAnim(Anim.Bump, transform.position, transform.position + new Vector3(offset.x, 0f, offset.y) * 0.25f);
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                Haptics.Light();
                return;
            }

            HopTo(landing, Mathf.Max(1, landing.Manhattan(Position)));
            AudioManager.PlaySfx(Sfx.Hop, 0.7f, 0.75f);
            Haptics.Medium();
        }

        /// <summary>Floor rules may veto a step (return false to keep the robot in place).</summary>
        public Func<Direction, bool> CanLeave;

        /// <summary>Speeds the robot up against a slowed world (the slow-motion tool keeps the robot at full speed).</summary>
        public float TimeBoost = 1f;
        private float Dt => Time.deltaTime * TimeBoost;

        /// <summary>
        /// Moves the robot without input: a slide on ice, a push by current or wind, a trampoline bounce.
        /// The caller checks the destination; <see cref="Arrived"/> fires on landing as usual.
        /// </summary>
        public void Shove(Direction dir, int distance, float height = 1f, float speed = 1f, bool turn = false)
        {
            if (!IsAlive) return;
            var o = dir.ToOffset();
            if (turn)
            {
                Facing = dir;
                targetFacing = Quaternion.LookRotation(new Vector3(o.x, 0f, o.y));
            }
            HopTo(new GridPos(Position.x + o.x * distance, Position.y + o.y * distance), distance);
            hopHeight *= height;
            hopDuration /= Mathf.Max(0.2f, speed);
        }

        private void HopTo(GridPos target, int distance)
        {
            LastLeftTile = Position;
            LastLeftTime = Time.time;
            Position = target;
            hopDuration = HopDuration * (distance == 1 ? 1f : 1.2f + 0.5f * distance);
            hopHeight = HopHeight * (distance == 1 ? 1f : 1.4f + 0.4f * distance);
            StartAnim(Anim.Hop, transform.position, GridView.ToWorld(target));
        }

        public void Squash()
        {
            IsAlive = false;
            StartAnim(Anim.Squash, transform.position, transform.position);
        }

        public void FallIntoHole()
        {
            IsAlive = false;
            StartAnim(Anim.Fall, transform.position, transform.position + Vector3.down * 12f);
        }

        /// <summary>A block slammed down next to us: lean away and squish for a moment.</summary>
        public void Flinch(Vector3 from)
        {
            if (!IsAlive || anim != Anim.Idle) return;
            var away = transform.position - from;
            away.y = 0f;
            flinchDir = away.sqrMagnitude > 0.001f ? away.normalized : Vector3.zero;
            flinchT = 0f;
        }

        /// <summary>A hammer blow at something on another tile: turn to it and lunge a little that way.</summary>
        public bool Strike(Vector3 target)
        {
            if (!IsAlive || anim != Anim.Idle) return false;
            var dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            targetFacing = Quaternion.LookRotation(dir);
            StartAnim(Anim.Bump, transform.position, transform.position + dir * 0.32f);
            return true;
        }

        /// <summary>Exit missions: spin, shrink and rise into the portal.</summary>
        public void EscapeInto()
        {
            IsAlive = false; // freezes input
            targetFacing = FacingCamera;
            StartAnim(Anim.Escape, transform.position, transform.position + Vector3.up * 1.2f);
        }

        public void Cheer()
        {
            IsAlive = false; // freezes input
            targetFacing = FacingCamera;
            StartAnim(Anim.Cheer, transform.position, transform.position);
        }

        private void StartAnim(Anim a, Vector3 start, Vector3 end)
        {
            anim = a;
            animTime = 0f;
            from = start;
            to = end;
        }

        private void Update()
        {
            animTime += Dt;
            visual.localRotation = Quaternion.Slerp(visual.localRotation, targetFacing, Dt * 20f);

            switch (anim)
            {
                case Anim.Idle:
                {
                    float breathe = 1f + Mathf.Sin(Time.time * 4f) * 0.025f;
                    visual.localScale = Vector3.Lerp(visual.localScale, new Vector3(1f, breathe, 1f), Dt * 12f);
                    break;
                }
                case Anim.Hop:
                {
                    float t = Mathf.Clamp01(animTime / (hopDuration * HopScale));
                    // A swipe already waiting takes over in the last stretch of a plain step, so a run of steps flows
                    // on without a stop on every tile.
                    bool flow = HopScale < 1f && bufferedMove.HasValue && t >= 0.8f && Vector3.Distance(from, to) < 1.1f;
                    if (flow) t = 1f;
                    transform.position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * hopHeight);
                    float stretch = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                    visual.localScale = new Vector3(1f / stretch, stretch, 1f / stretch);
                    if (t >= 1f)
                    {
                        visual.localScale = flow ? Vector3.one : new Vector3(1.15f, 0.85f, 1.15f);
                        anim = Anim.Idle;
                        Arrived?.Invoke(Position);
                        ConsumeBuffer();
                    }
                    break;
                }
                case Anim.Hover:
                {
                    // Rise quickly, then bob in the air with a little sway.
                    float up = 1f - Mathf.Pow(1f - Mathf.Clamp01(animTime / 0.22f), 3f);
                    float bob = Mathf.Sin(animTime * 6f) * 0.05f;
                    transform.position = from + Vector3.up * (HoverHeight * up + bob);
                    visual.localScale = Vector3.Lerp(visual.localScale, new Vector3(0.95f, 1.05f, 0.95f), Dt * 10f);
                    break;
                }
                case Anim.Bump:
                {
                    float t = Mathf.Clamp01(animTime / 0.12f);
                    transform.position = Vector3.Lerp(from, to, Mathf.Sin(t * Mathf.PI));
                    if (t >= 1f)
                    {
                        transform.position = from;
                        anim = Anim.Idle;
                        ConsumeBuffer();
                    }
                    break;
                }
                case Anim.Squash:
                {
                    float t = Mathf.Clamp01(animTime / 0.12f);
                    visual.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.5f, 0.12f, 1.5f), t);
                    break;
                }
                case Anim.Fall:
                {
                    float t = Mathf.Clamp01(animTime / 0.7f);
                    transform.position = Vector3.Lerp(from, to, t * t);
                    visual.localScale = Vector3.one * (1f - t * 0.6f);
                    visual.Rotate(0f, 0f, 400f * Dt);
                    break;
                }
                case Anim.Cheer:
                {
                    // Victory dance (the garage picks the style): spin, big jumps, a wobble or a backflip.
                    float fade = Mathf.Clamp01(1.6f - animTime);
                    float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(animTime / 1.1f), 3f);
                    float bounce = Mathf.Abs(Mathf.Sin(animTime * 7.5f)) * 0.45f * fade;
                    var pose = Quaternion.Euler(0f, 720f * ease, 0f);
                    if (dance == "dance.jump") { bounce = Mathf.Abs(Mathf.Sin(animTime * 5f)) * 0.8f * fade; pose = Quaternion.identity; }
                    else if (dance == "dance.wobble") pose = Quaternion.Euler(0f, Mathf.Sin(animTime * 9f) * 35f * fade, Mathf.Sin(animTime * 12f) * 20f * fade);
                    else if (dance == "dance.swim") { bounce = 0.25f + Mathf.Sin(animTime * 4f) * 0.12f * fade; pose = Quaternion.Euler(Mathf.Sin(animTime * 8f) * 20f * fade, Mathf.Sin(animTime * 4f) * 40f * fade, 0f); }
                    else if (dance == "dance.sugar") { bounce = Mathf.Abs(Mathf.Sin(animTime * 14f)) * 0.3f * fade; pose = Quaternion.Euler(0f, animTime * 540f, Mathf.Sin(animTime * 14f) * 12f * fade); }
                    else if (dance == "dance.flip") { bounce = Mathf.Sin(Mathf.Clamp01(animTime / 0.8f) * Mathf.PI) * 0.9f; pose = Quaternion.Euler(-360f * Mathf.Clamp01(animTime / 0.8f), 0f, 0f); }
                    transform.position = from + Vector3.up * bounce;
                    visual.localRotation = FacingCamera * pose;
                    float stretch = 1f + Mathf.Sin(animTime * 15f) * 0.08f * Mathf.Clamp01(1.6f - animTime);
                    visual.localScale = new Vector3(1f / stretch, stretch, 1f / stretch);
                    break;
                }
                case Anim.Escape:
                {
                    float t = Mathf.Clamp01(animTime / 0.65f);
                    transform.position = Vector3.Lerp(from, to, t * t);
                    visual.localRotation = FacingCamera * Quaternion.Euler(0f, 900f * t * t, 0f);
                    float s = t < 0.2f ? 1f + t * 1.5f : Mathf.Lerp(1.3f, 0f, (t - 0.2f) / 0.8f);
                    visual.localScale = new Vector3(s, s * (1f + t * 0.5f), s);
                    break;
                }
            }

            if (flinchT < 1f)
            {
                flinchT = Mathf.Min(1f, flinchT + Dt / 0.28f);
                float k = Mathf.Sin(flinchT * Mathf.PI);
                visual.localPosition = flinchDir * (0.09f * k);
                visual.localScale = Vector3.Scale(visual.localScale, new Vector3(1f + 0.12f * k, 1f - 0.15f * k, 1f + 0.12f * k));
                if (flinchT >= 1f) visual.localPosition = Vector3.zero;
            }
        }

        private void ConsumeBuffer()
        {
            if (bufferedJump)
            {
                bufferedJump = false;
                TryJump();
                return;
            }
            if (bufferedMove == null) return;
            var dir = bufferedMove.Value;
            bufferedMove = null;
            if (Time.time - bufferedAt > BufferLife) return;
            TryMove(dir);
        }
    }
}
