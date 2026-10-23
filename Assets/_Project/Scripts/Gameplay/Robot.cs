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
        /// <summary>Standing (not walking, hopping or in a blow).</summary>
        public bool IsIdle => anim == Anim.Idle;

        /// <summary>Turns smoothly toward <paramref name="target"/> (flat), as a turn on the spot does.</summary>
        public void TurnTo(Vector3 target)
        {
            var dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) targetFacing = Quaternion.LookRotation(dir.normalized);
        }

        /// <summary>Lifted into the air by the hover escape: blocks pass beneath it.</summary>
        public bool IsHovering => anim == Anim.Hover;

        /// <summary>Direction of the last move; a jump goes this way.</summary>
        public Direction Facing { get; private set; } = Direction.MinusY;

        /// <summary>Called when an armored robot hops into a landed block; returns true if the block was smashed.</summary>
        public Func<GridPos, bool> BlockSmasher;
        /// <summary>Tiles foi may not reach by a step or a leap (a fortress stage behind a shut gate).</summary>
        public Func<GridPos, bool> Forbidden;
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
        private float bufferedPace = -1f;
        /// <summary>How long this step takes, from how fast the finger moved (1 = the normal step).</summary>
        private float paceScale = 1f;
        /// <summary>The battle arenas only: foi walks and runs on the ground instead of hopping tile to tile.</summary>
        public bool WalkStyle;
        /// <summary>How long one tile takes when walking (stick part-way) or running (stick pushed right out).</summary>
        private float walkStep = 0.3f;
        /// <summary>A short victory dance that leaves foi free to move (the arenas' gift hunt) rather than freezing it.</summary>
        private bool cheerBrief;
        /// <summary>The way the last step was going when a turn follows straight on: the new step curves out of it.</summary>
        private Vector3 carry, hopCarry;
        private float carryAt = -10f;
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
            var lift = this.lift = new GameObject("Lift").transform;
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
            accessories.localPosition = new Vector3(0f, HeroModels.TorsoExtra(hero), 0f); // the worlds' gear rides with the head

            shadowMaterial = MaterialFactory.CreateTransparent(new Color(0.22f, 0.2f, 0.45f, 0.3f), Color.black);
            shadow = Shapes.Primitive(PrimitiveType.Cylinder, "Shadow", transform, Vector3.zero, new Vector3(0.48f, 0.004f, 0.48f), shadowMaterial).transform;

            bubbleMaterial = MaterialFactory.CreateTransparent(Palette.ShieldBubble, Palette.ShieldGlow);
            bubble = Shapes.Primitive(PrimitiveType.Sphere, "Shield", lift, new Vector3(0f, 0.4f, 0f), Vector3.one * 0.95f, bubbleMaterial).transform;
            bubble.gameObject.SetActive(false);
        }

        private Transform lift;

        /// <summary>foi's size in this mode (the floors show it a little bigger; 1 elsewhere).</summary>
        public void SetSize(float size, float lunge = 0.32f) { if (lift != null) lift.localScale = Vector3.one * size; this.lunge = lunge; }

        /// <summary>How far a blow lunges towards its target.</summary>
        private float lunge = 0.32f;

        /// <summary>The animated model (scaled and rotated by animations); other modes may pose it directly.</summary>
        public Transform Visual => visual;

        /// <summary>Plays one of foi's own moves (a dance, the ultimate's cast or kick); false if there is none.</summary>
        public bool PlayMove(string name, float rate = 1f) => Skin != null && Skin.PlayMove(name, rate);

        /// <summary>Standing again at once (out of any knock-down or reel).</summary>
        public void StandUp() { if (Skin != null) Skin.StandUp(); }

        /// <summary>A rigged build's clip player (null for the builds made of parts).</summary>
        private SkinnedHero Skin => HeroModels.IsSkinned(hero) ? visual.GetComponentInChildren<SkinnedHero>() : null;


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
        private Color OwnBody(int world) => hero == 0 ? HeroModels.Colours(0).body : Color.Lerp(HeroModels.Colours(hero).body, RobotLooks.BodyColor(world), 0.2f);
        private Color OwnLight(int world) => hero == 0 ? HeroModels.Colours(0).light : Color.Lerp(HeroModels.Colours(hero).light, RobotLooks.LightColor(world), 0.15f);
        private Color OwnEye(int world) => hero == 0 ? HeroModels.Colours(0).eye : HeroModels.Colours(hero).eye;

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
            legLT = footLT = null;
            accessories.localPosition = new Vector3(0f, HeroModels.TorsoExtra(hero), 0f);
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
            var step = outfit[Data.Slot.Step];
            StepId = step.IsDefault ? null : step.id;
            StepColor = step.color;
            var trail = outfit[Data.Slot.Trail];
            TrailColor = trail.IsDefault ? (Color?)null : trail.color;
            dance = outfit[Data.Slot.Dance].id;
            var skin = Skin;
            if (skin != null)
            {
                StepId = null; TrailColor = null; // (the workshop has none of these now)
                DressSkin(skin, outfit);
                ShieldHold.Attach(visual); // the shield carried, on the left arm
                return;
            }

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
            for (int i = cosmetics.childCount - 1; i >= 0; i--) DestroyImmediate(cosmetics.GetChild(i).gameObject); // at once: a lookalike may be copied right after
            var hat = outfit[Data.Slot.Hat];
            var back = outfit[Data.Slot.Back];
            // A build with a longer body wears its gear that much higher (head and arms all of it, back and badge half).
            float extra = HeroModels.TorsoExtra(hero);
            if (!hat.IsDefault) CosmeticModels.Hat(Raised(extra), hat.id, hat.color);
            float lift = HeroModels.LegLift(hero); // (the longer legs lift the body: its gear with it)
            if (!back.IsDefault) CosmeticModels.Back(Raised(extra * 0.6f + lift), back.id, back.color);
            accessories.gameObject.SetActive(hat.IsDefault && back.IsDefault);
            // Arm and leg pieces replace the plain ones; shaped eyes replace the plain eye blocks.
            var arms = outfit[Data.Slot.Arms];
            var legs = outfit[Data.Slot.Legs];
            if (!arms.IsDefault) CosmeticModels.Arms(Raised(extra + lift), arms.id, arms.color);
            if (!legs.IsDefault)
            {
                var legHolder = Raised(0.0001f);
                legHolder.localScale = new Vector3(1f, HeroModels.LegStretch(hero), 1f); // as long as the legs they replace
                CosmeticModels.Legs(legHolder, legs.id, legs.color);
            }
            foreach (var part in new[] { "ArmL", "ArmR" }) SetPartShown(part, arms.IsDefault);
            foreach (var part in new[] { "LegL", "LegR", "FootL", "FootR" }) SetPartShown(part, legs.IsDefault);
            var badge = outfit[Data.Slot.Badge];
            if (!badge.IsDefault) CosmeticModels.Badge(Raised(extra * 0.5f + lift), badge.id, badge.color);

            bool shaped = Data.Cosmetics.ShapedEyes(eyes.id);
            foreach (var e in new[] { eyeL, eyeR })
            {
                var old = e.Find("Shape");
                if (old != null) DestroyImmediate(old.gameObject);
                e.GetComponent<MeshRenderer>().enabled = !shaped;
                if (shaped) CosmeticModels.EyeShape(e, eyes.id, eyeMaterial);
            }
        }

        /// <summary>
        /// foi (the rigged build) in a workshop outfit: its head, hair and skin colour, its armour's paint, a hat on its head,
        /// a cape as cloth or another back piece on its back, arm and leg gear on its bones, a badge on its chest.
        /// </summary>
        private void DressSkin(SkinnedHero skin, System.Collections.Generic.Dictionary<Data.Slot, Data.Cosmetic> outfit)
        {
            accessories.gameObject.SetActive(false);
            if (cosmetics != null) cosmetics.gameObject.SetActive(false);
            var suit = outfit[Data.Slot.Suit];
            var headPiece = outfit[Data.Slot.Head];
            var hair = outfit[Data.Slot.Hair];
            var skinPiece = outfit[Data.Slot.Skin];
            SkinnedHero.Dress(skin.transform, new SkinnedHero.Look
            {
                head = SkinnedHero.HeadNumber(headPiece.id),
                hair = hair.IsDefault ? (Color?)null : hair.color,
                skin = skinPiece.color,
                eyes = outfit.TryGetValue(Data.Slot.Iris, out var iris) && iris != null ? iris.color : (Color?)null,
                suit = suit.IsDefault ? null : suit.id.Replace("suit.", ""),
            });
            foreach (var mount in new[] { skin.HatMount, skin.BackMount, skin.BadgeMount })
                if (mount != null)
                    for (int i = mount.childCount - 1; i >= 0; i--) DestroyImmediate(mount.GetChild(i).gameObject); // at once: a lookalike may be copied right after
            var hat = outfit[Data.Slot.Hat];
            bool hood = hat.id.StartsWith("hat.hood"); // a hooded cloak: over the head, not on it
            SkinnedHero.Hood(skin.transform, hood ? hat.color : (Color?)null);
            if (!hat.IsDefault && !hood && skin.HatMount != null) CosmeticModels.Hat(skin.HatMount, hat.id, hat.color);
            var oldCape = skin.transform.Find("Hero_Cape");
            if (oldCape != null) DestroyImmediate(oldCape.gameObject);
            var back = outfit[Data.Slot.Back];
            if (back.id.StartsWith("back.cape")) SoftCape.Build(skin.transform, back.id, back.color); // a cape is cloth on this body
            // (other back pieces belonged to the old robot: foi wears capes only)
            var arms = outfit[Data.Slot.Arms];
            var legs = outfit[Data.Slot.Legs];
            SkinnedHero.Gear(skin.transform, arms.IsDefault ? null : arms.id, arms.color, null, legs.color);
        }

        /// <summary>A holder for garage gear, <paramref name="y"/> higher than the gear was made for.</summary>
        private Transform Raised(float y)
        {
            if (y == 0f) return cosmetics;
            var t = new GameObject("Raised").transform;
            t.SetParent(cosmetics, false);
            t.localPosition = new Vector3(0f, y, 0f);
            return t;
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
            float ground = GridView.ToWorld(new GridPos(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z))).y; // the step under it
            float height = Mathf.Max(0f, p.y - ground);
            shadow.position = new Vector3(p.x, ground + GridView.SurfaceY + 0.004f, p.z);
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

        /// <summary>Turn at once to face a way (a shot in the arenas), whatever it is doing.</summary>
        public void ArenaFace(Vector3 facing)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f) return;
            targetFacing = Quaternion.LookRotation(facing);
            visual.localRotation = targetFacing;
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
        /// <returns>False when it can't rise now (mid-hop or mid-bump): the hover has not started.</returns>
        public bool StartHover()
        {
            if (!IsAlive || anim == Anim.Hop || anim == Anim.Bump || anim == Anim.Hover) return false;
            bufferedMove = null;
            bufferedJump = false;
            targetFacing = FacingCamera;
            StartAnim(Anim.Hover, GridView.ToWorld(Position), GridView.ToWorld(Position));
            return true;
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
            paceScale = 1f;
            hopCarry = Vector3.zero;
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
            paceScale = 1f;
            hopCarry = Vector3.zero;
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
            foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>()) if (!(b is CapeWave || b is SkinnedHero || b is SoftCape || b is ShieldHold)) Destroy(b); // (a cape still waves, a rigged build still moves, its shield stays in hand)
            if (HeroModels.IsSkinned(hero))
                foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.enabled = true; // (a stand-in may have hidden its pieces at the moment it was copied)
            return copy;
        }

        /// <param name="pace">How fast the finger moved, 0 (a slow drag) .. 1 (a quick flick); negative = a normal step.</param>
        public void TryMove(Direction dir, float pace = -1f)
        {
            if (InArena) return;
            if (!IsAlive || anim == Anim.Hover) return;
            if (anim == Anim.Hop || anim == Anim.Bump)
            {
                bufferedMove = dir; // keeps fast swipes responsive
                bufferedPace = pace;
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

            // Swiping toward a hole or fire leaps over it automatically, so jumping stays in the flow of play; the fortress's
            // burning stairs lead off the floor, so there foi steps down into the flames instead.
            if (grid.IsLava(target))
            {
                HopTo(target, 1);
                AudioManager.PlaySfx(Sfx.Hop, 0.55f);
                return;
            }
            if (grid.IsGap(target))
            {
                Jump(dir);
                return;
            }

            // A wall of the floor's relief too high to climb (more than one step up, or a drop of more than two): bump.
            if (grid.Exists(target) && !FloorRelief.StepOk(Position, target))
            {
                StartAnim(Anim.Bump, transform.position, transform.position + new Vector3(offset.x, 0f, offset.y) * 0.2f);
                AudioManager.PlaySfx(Sfx.Bump, 0.5f, 0.8f);
                Haptics.Light();
                return;
            }

            // With armor, hopping onto a landed block smashes it and the robot takes the tile.
            bool blocked = grid.InBounds(target) && ((grid.IsOccupied(target) && !(IsShielded && BlockSmasher != null && BlockSmasher(target))) || (Forbidden != null && Forbidden(target)));
            if (!grid.InBounds(target) || blocked)
            {
                if (blocked) BumpedInto?.Invoke(target);
                StartAnim(Anim.Bump, transform.position, transform.position + new Vector3(offset.x, 0f, offset.y) * 0.25f);
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                Haptics.Light();
                return;
            }

            // The finger sets the pace: a quick flick hops quicker, a slow drag a little slower, within a steady range.
            paceScale = pace < 0f ? 1f : Mathf.Lerp(1.3f, 0.75f, Mathf.Clamp01(pace));
            // Walking or running in the arenas: a steady stride, a little quicker when the stick is pushed all the way.
            walkStep = Mathf.Lerp(0.27f, 0.155f, Mathf.InverseLerp(0.6f, 0.95f, pace < 0f ? 0.5f : pace));
            HopTo(target, 1);
            if (WalkStyle) AudioManager.PlaySfx(Sfx.Hop, 0.12f, 1.5f, 0.1f); // soft footsteps, no buzz on every tile
            else
            {
                AudioManager.PlaySfx(Sfx.Hop, 0.55f, 1f, 0.08f);
                Haptics.Light();
            }
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
            else if (noLanding || (Forbidden != null && Forbidden(landing)) || (grid.IsOccupied(landing) && !(IsShielded && BlockSmasher != null && BlockSmasher(landing))))
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
            if (distance != 1) paceScale = 1f;
            // Straight on from a step the other way: curve out of it instead of snapping round the corner.
            var dirNow = GridView.ToWorld(target) - GridView.ToWorld(Position);
            hopCarry = distance == 1 && Time.time - carryAt < 0.3f && carry.sqrMagnitude > 0.01f && Vector3.Dot(carry.normalized, dirNow.normalized) < 0.5f ? carry : Vector3.zero;
            carry = Vector3.zero;
            LastLeftTile = Position;
            LastLeftTime = Time.time;
            Position = target;
            hopDuration = HopDuration * (distance == 1 ? 1f : 1.2f + 0.5f * distance);
            hopHeight = HopHeight * (distance == 1 ? 1f : 1.4f + 0.4f * distance);
            // Climbing a step: a higher arc, so it looks like a hop up rather than a slide.
            float climb = (GridView.ToWorld(target).y - transform.position.y) / FloorRelief.Step;
            if (climb > 0.1f) hopHeight += 0.25f * climb;
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
            Skin?.Anger();
        }

        /// <summary>A hammer blow at something on another tile: turn to it and lunge a little that way.</summary>
        /// <summary>Blows swung so far (a live duel tells the other game, which swings the rival's weapon).</summary>
        public int Swings { get; private set; }

        /// <summary>Turns to face <paramref name="target"/> at once (no blow of its own: the ultimates' moves play instead).</summary>
        public void Face(Vector3 target)
        {
            var dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            targetFacing = Quaternion.LookRotation(dir.normalized);
            visual.localRotation = targetFacing;
        }

        /// <summary>The skinned hero's move player (null for the old builds).</summary>
        public SkinnedHero Moves => Skin;

        public bool Strike(Vector3 target)
        {
            if (!IsAlive || anim != Anim.Idle) return false;
            var dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            targetFacing = Quaternion.LookRotation(dir);
            visual.localRotation = targetFacing; // face the target at once, even one behind: no slow turn before a blow
            StartAnim(Anim.Bump, transform.position, transform.position + dir * lunge);
            Skin?.Punch();
            Swings++;
            return true;
        }

        /// <summary>Exit missions: spin, shrink and rise into the portal.</summary>
        public void EscapeInto()
        {
            IsAlive = false; // freezes input
            targetFacing = FacingCamera;
            var from = GroundedPosition;
            StartAnim(Anim.Escape, from, from + Vector3.up * 1.2f);
        }

        private Vector3 freeVel;
        private float stridePhase, lastBob;
        private bool stepParity;
        private Transform legLT, legRT, footLT, footRT;
        private Vector3 legLB, legRB, footLB, footRB;
        private Quaternion legLR, legRR;

        /// <summary>
        /// The legs walking: at <paramref name="phase"/> one foot reaches forward and lifts while the other pushes back,
        /// the legs swinging with them; <paramref name="amount"/> 0 stands them straight again.
        /// </summary>
        private void Stride(float phase, float amount)
        {
            if (legLT == null || footLT == null)
            {
                legLT = visual.Find("LegL"); legRT = visual.Find("LegR"); footLT = visual.Find("FootL"); footRT = visual.Find("FootR");
                if (legLT == null || legRT == null || footLT == null || footRT == null) { legLT = footLT = null; return; }
                legLB = legLT.localPosition; legRB = legRT.localPosition; footLB = footLT.localPosition; footRB = footRT.localPosition;
                legLR = legLT.localRotation; legRR = legRT.localRotation;
            }
            float s = Mathf.Sin(phase) * amount;
            float liftL = Mathf.Max(0f, s), liftR = Mathf.Max(0f, -s);
            footLT.localPosition = footLB + new Vector3(0f, liftL * 0.04f, s * 0.07f);
            footRT.localPosition = footRB + new Vector3(0f, liftR * 0.04f, -s * 0.07f);
            legLT.localPosition = legLB + new Vector3(0f, liftL * 0.02f, s * 0.035f);
            legRT.localPosition = legRB + new Vector3(0f, liftR * 0.02f, -s * 0.035f);
            legLT.localRotation = legLR * Quaternion.Euler(-s * 22f, 0f, 0f);
            legRT.localRotation = legRR * Quaternion.Euler(s * 22f, 0f, 0f);
        }

        /// <summary>
        /// The battle arenas: walks foi freely (any direction, any speed) instead of tile to tile. It eases into the
        /// wanted velocity, keeps a body's width from anything it may not stand on (sliding along walls rather than
        /// stopping dead), turns to the way it goes with a light stride bob, and when it crosses into another tile that
        /// tile becomes its grid position (and the game hears it arrive there, as after a step).
        /// </summary>
        public void FreeStep(Vector3 wantVel, float dt, Func<GridPos, bool> walkable)
        {
            if (!IsAlive || InArena || grid == null) { freeVel = Vector3.zero; return; }
            // The short victory dance gives way the moment the stick is pushed.
            if (anim == Anim.Cheer && cheerBrief && wantVel.sqrMagnitude > 0.01f) { cheerBrief = false; transform.position = from; visual.localScale = Vector3.one; anim = Anim.Idle; }
            if (anim != Anim.Idle) { freeVel = Vector3.zero; return; } // a blow or a spin in progress
            freeVel = Vector3.MoveTowards(freeVel, wantVel, dt * 28f);
            var pos = transform.position - Vector3.up * lastBob;
            lastBob = 0f;
            float speed = freeVel.magnitude;
            if (speed > 0.05f)
            {
                bool Free(Vector3 p)
                {
                    const float r = 0.3f;
                    foreach (var o in new[] { Vector3.zero, new Vector3(r, 0f, 0f), new Vector3(-r, 0f, 0f), new Vector3(0f, 0f, r), new Vector3(0f, 0f, -r) })
                    {
                        var t = new GridPos(Mathf.RoundToInt(p.x + o.x), Mathf.RoundToInt(p.z + o.z));
                        if (t != Position && !walkable(t)) return false;
                    }
                    return true;
                }
                var step = freeVel * dt;
                var next = pos + step;
                if (!Free(next))
                {
                    // Slide along whatever is in the way.
                    var alongX = pos + new Vector3(step.x, 0f, 0f);
                    var alongZ = pos + new Vector3(0f, 0f, step.z);
                    if (Mathf.Abs(step.x) > 0.0001f && Free(alongX)) next = alongX;
                    else if (Mathf.Abs(step.z) > 0.0001f && Free(alongZ)) next = alongZ;
                    else { next = pos; freeVel *= 0.5f; }
                }
                var tile = new GridPos(Mathf.RoundToInt(next.x), Mathf.RoundToInt(next.z));
                // Up and down the floor's steps smoothly (no pop at the tile edge).
                next.y = Mathf.MoveTowards(pos.y, GridView.ToWorld(tile).y, dt * 4f);
                targetFacing = Quaternion.LookRotation(new Vector3(freeVel.x, 0f, freeVel.z));
                Facing = Mathf.Abs(freeVel.x) >= Mathf.Abs(freeVel.z) ? (freeVel.x > 0f ? Direction.PlusX : Direction.MinusX) : (freeVel.z > 0f ? Direction.PlusY : Direction.MinusY);
                // A light stride: a bob and a squash in step with the pace.
                stridePhase += dt * (6f + speed * 2.2f);
                float bob = Mathf.Abs(Mathf.Sin(stridePhase)) * Mathf.Min(0.07f, speed * 0.015f);
                transform.position = next + Vector3.up * bob;
                lastBob = bob;
                float s = 1f + Mathf.Sin(stridePhase * 2f) * 0.025f;
                visual.localScale = new Vector3(1f / s, s, 1f / s);
                Stride(stridePhase, Mathf.Clamp01(speed / 3f));
                if (tile != Position && grid.InBounds(tile))
                {
                    LastLeftTile = Position;
                    LastLeftTime = Time.time;
                    Position = tile;
                    Arrived?.Invoke(Position);
                }
            }
            else
            {
                freeVel = Vector3.zero;
                transform.position = new Vector3(pos.x, Mathf.MoveTowards(pos.y, GridView.ToWorld(Position).y, dt * 4f), pos.z);
                Stride(0f, 0f);
            }
        }

        /// <summary>A quick victory dance that does not stop foi (it can walk off at once; a step ends the dance).</summary>
        public void CheerBrief()
        {
            cheerBrief = true;
            Skin?.Celebrate(dance);
            StartAnim(Anim.Cheer, transform.position, transform.position);
        }

        public void Cheer()
        {
            cheerBrief = false;
            Skin?.Celebrate(dance);
            IsAlive = false; // freezes input
            targetFacing = FacingCamera;
            var from = GroundedPosition;
            StartAnim(Anim.Cheer, from, from);
        }

        /// <summary>Where foi stands: mid-hop that is its tile (not the arc), else where it is.</summary>
        private Vector3 GroundedPosition => anim == Anim.Hop ? GridView.ToWorld(Position) : transform.position;

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
            // The body swings round quickly (a quarter turn in about a tenth of a second) but never snaps.
            visual.localRotation = Quaternion.Slerp(visual.localRotation, targetFacing, Dt * 18f);

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
                    bool walk = WalkStyle && Vector3.Distance(from, to) < 1.1f;
                    float t = Mathf.Clamp01(animTime / (walk ? walkStep : hopDuration * HopScale * paceScale));
                    // A swipe already waiting takes over in the last stretch of a plain step, so a run of steps flows
                    // on without a stop on every tile.
                    bool flow = HopScale < 1f && bufferedMove.HasValue && t >= 0.8f && Vector3.Distance(from, to) < 1.1f;
                    if (flow) t = 1f;
                    var ground = Vector3.Lerp(from, to, t);
                    if (hopCarry != Vector3.zero)
                    {
                        // A turn taken on the run: the path leaves along the old heading and bends into the new one
                        // (a Hermite curve), so the corner is a smooth swing rather than a sharp click.
                        float t2 = t * t, t3 = t2 * t;
                        var span = to - from;
                        ground = (2f * t3 - 3f * t2 + 1f) * from + (t3 - 2f * t2 + t) * hopCarry * 0.8f
                                 + (-2f * t3 + 3f * t2) * to + (t3 - t2) * span;
                    }
                    // Hopping: an arc. Walking: feet on the ground, just a light stride bob.
                    transform.position = ground + Vector3.up * (walk ? Mathf.Abs(Mathf.Sin(t * Mathf.PI)) * 0.05f : Mathf.Sin(t * Mathf.PI) * hopHeight);
                    if (t >= 1f) { carry = new Vector3(to.x - from.x, 0f, to.z - from.z); carryAt = Time.time; } // a turn soon after curves out of this step
                    float stretch = 1f + Mathf.Sin(t * Mathf.PI) * (walk ? 0.03f : 0.15f);
                    visual.localScale = new Vector3(1f / stretch, stretch, 1f / stretch);
                    Stride((stepParity ? Mathf.PI : 0f) + t * Mathf.PI, t >= 1f ? 0f : 1f);
                    if (t >= 1f) stepParity = !stepParity;
                    if (t >= 1f)
                    {
                        visual.localScale = flow || walk ? Vector3.one : new Vector3(1.15f, 0.85f, 1.15f);
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
                    if (dance == "dance.jump") { bounce = Mathf.Abs(Mathf.Sin(animTime * 5f)) * 0.38f * fade; pose = Quaternion.identity; }
                    else if (dance == "dance.wobble") pose = Quaternion.Euler(0f, Mathf.Sin(animTime * 9f) * 35f * fade, Mathf.Sin(animTime * 12f) * 20f * fade);
                    else if (dance == "dance.swim") { bounce = 0.25f + Mathf.Sin(animTime * 4f) * 0.12f * fade; pose = Quaternion.Euler(Mathf.Sin(animTime * 8f) * 20f * fade, Mathf.Sin(animTime * 4f) * 40f * fade, 0f); }
                    else if (dance == "dance.sugar") { bounce = Mathf.Abs(Mathf.Sin(animTime * 14f)) * 0.3f * fade; pose = Quaternion.Euler(0f, animTime * 540f, Mathf.Sin(animTime * 14f) * 12f * fade); }
                    else if (dance == "dance.flip") { bounce = Mathf.Sin(Mathf.Clamp01(animTime / 0.8f) * Mathf.PI) * 0.5f; pose = Quaternion.Euler(-360f * Mathf.Clamp01(animTime / 0.8f), 0f, 0f); }
                    transform.position = from + Vector3.up * bounce;
                    visual.localRotation = FacingCamera * pose;
                    if (cheerBrief && animTime >= 1.6f)
                    {
                        cheerBrief = false;
                        transform.position = from;
                        visual.localScale = Vector3.one;
                        anim = Anim.Idle;
                        break;
                    }
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
            TryMove(dir, bufferedPace);
        }
    }
}
