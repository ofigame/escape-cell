using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Gameplay;
using SquashBot.Journey;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Forest
{
    /// <summary>
    /// The third-person journey: the robot walks a stretch of land (forest path, stairs up to a raised deck full of
    /// hazards, down again, robots and cores in the open, a gated passage), rides the passage, and carries on in the
    /// next stretch. This class runs the land and its rules; the controls are separate pieces:
    /// <see cref="JourneyInput"/> (what the player asks) → <see cref="PlayerMotor"/> (the robot's position and facing,
    /// camera-relative), <see cref="TPSCamera"/> (the free orbit camera), <see cref="PlayerCombat"/> (swing and skills)
    /// and <see cref="PlayerAnimator"/> (the robot's parts). Each transform has exactly one owner.
    /// </summary>
    public partial class ForestPrototype : MonoBehaviour
    {
        private const float HitDamage = 0.25f;

        public event Action Exited;
        /// <summary>The robot reached the ride (the game runs the passage, builds the next land with <see cref="PrepareNext"/> and hands back with <see cref="SwitchWorld"/>).</summary>
        public event Action TunnelReached;
        /// <summary>The robot nears the passage: time to lay the passage and the land beyond, so the mouth shows a real way on.</summary>
        public event Action TunnelNear;
        private bool nearSent;

        /// <summary>Which leg of the journey this is (1 = the first forest).</summary>
        public int Leg { get; private set; } = 1;

        /// <summary>Where the ride (cart or raft) waits inside the passage (world).</summary>
        public Vector3 BoardPoint => world.ToWorld(new Vector3(0f, 0f, world.BoardZ));
        /// <summary>How this leg is left: through a cave by cart (odd legs) or down a gorge by raft (even legs).</summary>
        public PassageStyle ExitStyle => world.Exit;
        public static PassageStyle StyleFor(int leg) => leg % 2 == 1 ? PassageStyle.Cave : PassageStyle.Gorge;
        private static int SeedFor(int leg) => 7 + (leg - 1) * 101;

        /// <summary>Test hook: the robot walks the path by itself (screenshots of the whole leg).</summary>
        public static bool AutoWalk;

        /// <summary>The next stretch of land, built at the passage's far end while the passage runs.</summary>
        private ForestWorld nextWorld;
        private ForestWorld world;
        private Robot robot;
        private CameraRig rig;
        private FxSystem fx;
        private bool suspended, finished;

        // The controls.
        private JourneyTuning tuning;
        private JourneyInput input;
        private PlayerMotor motor;
        private TPSCamera cam;
        private PlayerCombat combat;
        private PlayerAnimator anim;
        private JourneyHud hud;

        /// <summary>The robot's place in the current land (the motor owns it).</summary>
        private Vector3 pos { get => motor.Position; set => motor.Position = value; }
        private float yaw => motor.Yaw;
        private bool grounded => motor.Grounded;

        private float health = 1f, hurtLeft;
        private int coins;
        private Vector3 checkpoint;

        // Things in the world.
        private Transform guide, hammer;
        private readonly List<Transform> coinObjs = new List<Transform>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private Material warnMat, crateMat, goldMat;

        public static ForestPrototype Begin(Robot robot, CameraRig rig, FxSystem fx)
        {
            var p = new GameObject("ForestPrototype").AddComponent<ForestPrototype>();
            p.robot = robot;
            p.rig = rig;
            p.fx = fx;
            p.Setup();
            return p;
        }

        private void Setup()
        {
            tuning = JourneyTuning.Get();
            input = new JourneyInput(tuning);
            motor = new PlayerMotor(tuning) { Collide = Collide, Ground = GroundY };
            motor.Landed += OnLanded;
            motor.Jumped += () => AudioManager.PlaySfx(Sfx.Hop, 0.6f, 1.1f);
            cam = new TPSCamera(tuning, rig) { GroundAt = p => world != null ? CameraGround(p) : 0f };
            combat = new PlayerCombat(tuning) { FindTarget = FindTarget, Strike = OnStrike, Slam = OnSlam, HammerPasses = OnHammerPasses };
            anim = new PlayerAnimator(robot);

            world = ForestWorld.Build(SeedFor(Leg), ForestWorld.FirstOrigin, Leg, PassageStyle.None, StyleFor(Leg));
            SetLook(true);
            robot.EnterArena();
            robot.gameObject.SetActive(true);
            hammer = HammerModels.Held(robot.Visual, Weapons.Level);
            anim.SetHammer(hammer);
            combat.Attach(world.transform, hammer);
            checkpoint = StartPoint();
            motor.Teleport(checkpoint, 0f);
            cam.Reset(0f, world.ToWorld(pos));

            warnMat = MaterialFactory.CreateTransparent(new Color(1f, 0.25f, 0.2f, 0.45f), new Color(1.4f, 0.25f, 0.2f));
            crateMat = Resources.Load<Material>("Forest/Bark_Oak");
            goldMat = MaterialFactory.Create(new Color(1f, 0.78f, 0.25f), new Color(1.2f, 0.85f, 0.2f));
            BuildLand();
            hud = JourneyHud.Build(input, tuning, () => Exited?.Invoke(), Restart);
            AudioManager.PlayMusic(MusicTheme.Menu);
            BuildAmbience();
            UpdateSkillLocks();
        }

        /// <summary>Everything the current land holds besides its ground: the guide, coins, deck hazards, robots, tasks.</summary>
        private void BuildLand()
        {
            BuildGuide();
            BuildCoins();
            BuildSweepers();
            BuildEnemies();
            BuildMission();
        }

        public void End()
        {
            SetLook(false);
            combat.Reset();
            if (hammer != null) Destroy(hammer.gameObject);
            robot.ExitArena();
            Destroy(world.gameObject);
            if (nextWorld != null) Destroy(nextWorld.gameObject);
            hud.Destroy();
            input.Dispose();
            ClearDeck();
            Destroy(gameObject);
        }

        private void Restart()
        {
            finished = false;
            hud.ShowEnd(false);
            health = 1f;
            input.Clear();
            checkpoint = StartPoint();
            motor.Teleport(checkpoint, 0f);
            cam.Reset(0f, world.ToWorld(pos));
        }

        private Vector3 StartPoint() => world.Entry == PassageStyle.None
            ? new Vector3(0f, world.TerrainY(0f, -6f), -6f)
            : new Vector3(0f, 0f, ForestWorld.ArriveZ + 0.5f);

        private void UpdateSkillLocks()
        {
            combat.SlamOpen = Leg >= tuning.slamFromLeg;
            combat.ThrowOpen = Leg >= tuning.throwFromLeg;
            hud.Slam.SetLocked(!combat.SlamOpen, Loc.F("forest.legShort", tuning.slamFromLeg));
            hud.Throw.SetLocked(!combat.ThrowOpen, Loc.F("forest.legShort", tuning.throwFromLeg));
        }

        // ---------- The frame ----------

        private void Update()
        {
            if (finished || suspended) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            input.Poll();
            if (AutoWalk) AutoSteer();

            // Camera first (look input is applied the frame it arrives), then the robot moves relative to it.
            cam.Turn(input.Look);
            if (input.JumpPressed) motor.Jump();
            combat.Tick(dt, input, motor, cam.FlatForward);
            motor.Tick(dt, input.Move, cam.Yaw, input.Sprint, combat.MoveMultiplier);
            robot.ArenaPlace(world.ToWorld(pos), motor.Forward, snapTurn: true); // the motor's facing, unsmoothed
            anim.Tick(motor, combat);

            UpdateAmbience(dt, motor.Speed01 > 0.1f);
            UpdateCoins();
            UpdateDeck(dt);
            UpdateEnemies(dt);
            UpdateMission(dt);
            UpdateGuide();
            if (hurtLeft > 0f) hurtLeft -= dt;
            hud.SetHealth(health);
            hud.CoinText.text = coins.ToString();
            hud.Dash.SetCooldown(combat.DashCooldown01);
            hud.Slam.SetCooldown(combat.SlamOpen ? combat.SlamCooldown01 : 0f);
            hud.Throw.SetCooldown(combat.ThrowOpen ? combat.ThrowCooldown01 : 0f);
            hud.Tick(dt);
            if (!nearSent && pos.z > world.TunnelZ - 28f) { nearSent = true; TunnelNear?.Invoke(); }
            if (pos.z >= world.BoardZ - 0.3f) EnterTunnel();
        }

        private void LateUpdate()
        {
            UpdateDaylight();
            if (suspended || finished) return;
            cam.Follow(world.ToWorld(pos), Time.deltaTime);
        }

        /// <summary>Test hook steering: along the path (or to the nearest task once past the clearing), jumping holes and logs, swinging at robots.</summary>
        private void AutoSteer()
        {
            Vector3 target;
            if (!MissionTarget(out target))
            {
                float aheadZ = pos.z + 4f;
                target = new Vector3(world.PathX(aheadZ), 0f, aheadZ);
            }
            var to = target - pos;
            to.y = 0f;
            float rel = Mathf.DeltaAngle(cam.Yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            input.ScriptedMove = new Vector2(Mathf.Sin(rel), Mathf.Cos(rel));
            // The camera swings round behind the way it walks (a player does this with the right thumb).
            cam.Turn(new Vector2(Mathf.DeltaAngle(cam.Yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg) * 0.08f, 0f));
            if (grounded && (InGap(pos.x, pos.z + 0.35f) || rollers.Exists(r => r.localPosition.z > pos.z && r.localPosition.z - pos.z < 1.6f))) motor.Jump();
            if (enemies.Exists(e => !e.dead && (e.pos - pos).magnitude < 2f)) { input.PressAttack(); input.ReleaseAttack(); }
        }

        private void OnLanded(float impact)
        {
            if (impact < 3f) return;
            // A landing: a puff of dust around the feet.
            fx.Dust(world.ToWorld(pos) + Vector3.up * 0.05f, new Color(0.5f, 0.42f, 0.3f), 10, 2f);
            AudioManager.PlaySfx(Sfx.Bump, 0.35f, 1.4f);
        }

        private bool InFight() =>
            (new Vector2(pos.x, pos.z) - world.ClearingCentre).magnitude < ForestWorld.ClearingRadius + 3f && enemies.Exists(e => !e.dead);

        // ---------- The land's rules for the motor ----------

        /// <summary>The ground under the walker: the stairs and the deck where they are, the passage floor, the terrain elsewhere.</summary>
        private float GroundY(float x, float z, float currentY)
        {
            float deckTop = ForestWorld.DeckHeight + 0.09f;
            bool onStairsX = Mathf.Abs(x) < ForestWorld.StairsHalfWidth;
            if (onStairsX && z > ForestWorld.StairsUpStart && z < ForestWorld.DeckStart)
                return Mathf.Lerp(0f, deckTop, (z - ForestWorld.StairsUpStart) / (ForestWorld.DeckStart - ForestWorld.StairsUpStart));
            if (onStairsX && z > world.DeckEnd && z < world.StairsDownEnd)
                return Mathf.Lerp(deckTop, 0f, (z - world.DeckEnd) / (world.StairsDownEnd - world.DeckEnd));
            if (Mathf.Abs(x) < world.DeckHalfWidth && z >= ForestWorld.DeckStart && z <= world.DeckEnd && currentY > deckTop - 0.8f && !InGap(x, z))
                return deckTop;
            if (InPassage(z)) return 0f; // the passage's rock floor (the land behind the cliff lies lower)
            return world.TerrainY(x, z);
        }

        /// <summary>Keeps the camera out of the ground and the deck (world point).</summary>
        private float CameraGround(Vector3 p)
        {
            var local = p - world.Origin;
            return GroundY(local.x, local.z, local.y);
        }

        private bool InPassage(float z) =>
            z > world.TunnelZ - 0.3f || (world.Entry != PassageStyle.None && z < ForestWorld.EntryMouthZ + 0.3f);

        private bool InGap(float x, float z)
        {
            foreach (var g in world.Gaps)
                if (x > g.xMin + 0.15f && x < g.xMax - 0.15f && z > g.yMin + 0.15f && z < g.yMax - 0.15f) return true;
            return false;
        }

        /// <summary>Keeps the walker out of trunks, off the deck's sides and edges, out of the rock and near the way.</summary>
        private Vector3 Collide(Vector3 from, Vector3 next)
        {
            foreach (var t in world.Trunks)
            {
                var d = new Vector2(next.x - t.x, next.z - t.y);
                float min = t.z + 0.3f;
                if (d.sqrMagnitude < min * min && d.sqrMagnitude > 0.0001f)
                {
                    var push = d.normalized * min;
                    next.x = t.x + push.x;
                    next.z = t.y + push.y;
                }
            }
            float deckTop = ForestWorld.DeckHeight;
            bool high = from.y > deckTop - 0.8f;
            bool inDeck = Mathf.Abs(next.x) < world.DeckHalfWidth + 0.3f && next.z > ForestWorld.DeckStart - 0.3f && next.z < world.DeckEnd + 0.3f;
            bool stairs = Mathf.Abs(next.x) < ForestWorld.StairsHalfWidth - 0.25f;
            if (high)
            {
                // On the deck or the stairs: no stepping off the sides.
                bool onStairs = next.z < ForestWorld.DeckStart || next.z > world.DeckEnd;
                float limit = onStairs ? ForestWorld.StairsHalfWidth - 0.3f : world.DeckHalfWidth - 0.4f;
                next.x = Mathf.Clamp(next.x, -limit, limit);
            }
            else if (inDeck && !stairs) next = SlideAlong(from, next, (x, z) => Mathf.Abs(x) < world.DeckHalfWidth + 0.3f && z > ForestWorld.DeckStart - 0.3f && z < world.DeckEnd + 0.3f && Mathf.Abs(x) >= ForestWorld.StairsHalfWidth - 0.25f);
            else if (Mathf.Abs(next.x) < ForestWorld.StairsHalfWidth + 0.3f && next.z > ForestWorld.StairsUpStart && next.z < world.StairsDownEnd && !stairs)
                next = new Vector3(from.x, next.y, from.z);
            // Never wander far from the way.
            float px = world.PathX(next.z);
            float maxOff = (new Vector2(next.x, next.z) - world.ClearingCentre).magnitude < ForestWorld.ClearingRadius + 2f ? 14f : 7f;
            next.x = Mathf.Clamp(next.x, px - maxOff, px + maxOff);
            next.z = Mathf.Clamp(next.z, world.Entry == PassageStyle.None ? -12f : ForestWorld.ArriveZ, world.BoardZ + 0.5f);
            // The passage gate stays shut until the tasks are done.
            if (GateShut && next.z > world.TunnelZ - 0.9f) next.z = Mathf.Min(from.z, world.TunnelZ - 0.9f);
            // The cliffs either side of a passage are solid: slide along them, never through.
            if (world.InRock(next.x, next.z)) next = SlideAlong(from, next, world.InRock);
            return next;
        }

        /// <summary>A step into something solid keeps whichever axis is free (sliding along walls instead of sticking).</summary>
        private static Vector3 SlideAlong(Vector3 from, Vector3 next, Func<float, float, bool> solid)
        {
            if (!solid(next.x, from.z)) return new Vector3(next.x, next.y, from.z);
            if (!solid(from.x, next.z)) return new Vector3(from.x, next.y, next.z);
            return new Vector3(from.x, next.y, from.z);
        }

        // ---------- Combat in this land ----------

        private Vector3? FindTarget(Vector3 from, Vector3 dir, float range, float arc)
        {
            Vector3? best = null;
            float bestScore = float.MaxValue;
            foreach (var e in enemies)
            {
                if (e.dead) continue;
                var to = e.pos - from;
                to.y = 0f;
                float d = to.magnitude;
                if (d > range || d < 0.01f) continue;
                float angle = Vector3.Angle(dir, to);
                if (angle > arc) continue;
                float score = d + angle * 0.05f;
                if (score < bestScore) { bestScore = score; best = e.pos; }
            }
            return best;
        }

        private void OnStrike(Vector3 from, Vector3 dir, float range, float arc, int damage)
        {
            foreach (var e in enemies)
            {
                if (e.dead) continue;
                var to = e.pos - from;
                to.y = 0f;
                if (to.magnitude > range || Vector3.Angle(dir, to) > arc) continue;
                HitEnemy(e, to, damage);
            }
        }

        private void OnSlam(Vector3 centre, float radius, int damage)
        {
            Shockwave.Create(world.ToWorld(centre), radius, new Color(1f, 0.6f, 0.3f));
            fx.Dust(world.ToWorld(centre), new Color(0.55f, 0.47f, 0.36f), 30, 5f);
            rig.Shake(0.9f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.6f);
            foreach (var e in enemies)
            {
                if (e.dead) continue;
                var to = e.pos - centre;
                to.y = 0f;
                if (to.magnitude > radius) continue;
                HitEnemy(e, to, damage);
            }
            // The slam also smashes crates and rolling logs close by.
            ClearDeckHazardsNear(centre, radius);
        }

        private void OnHammerPasses(Vector3 point, float radius, int throwId)
        {
            foreach (var e in enemies)
            {
                if (e.dead || e.lastThrow == throwId) continue;
                var to = e.pos + Vector3.up * 0.8f - point;
                if (to.magnitude > radius + 0.5f) continue;
                e.lastThrow = throwId;
                to.y = 0f;
                HitEnemy(e, to, 1);
            }
        }

        // ---------- Coins, harm ----------

        private void BuildGuide()
        {
            // A soft green chevron floating ahead along the way to go.
            guide = new GameObject("Guide").transform;
            guide.SetParent(world.transform, false);
            var m = MaterialFactory.CreateTransparent(new Color(0.55f, 1f, 0.6f, 0.75f), new Color(0.6f, 1.8f, 0.7f));
            foreach (float s in new[] { -1f, 1f })
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(guide, false);
                bar.transform.localPosition = new Vector3(s * 0.16f, 0f, 0f);
                bar.transform.localRotation = Quaternion.Euler(0f, s * -40f, 0f);
                bar.transform.localScale = new Vector3(0.09f, 0.03f, 0.45f);
                bar.GetComponent<MeshRenderer>().sharedMaterial = m;
            }
        }

        private void BuildCoins()
        {
            void Coin(Vector3 p)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(c.GetComponent<Collider>());
                c.name = "Coin";
                c.transform.SetParent(world.transform, false);
                c.transform.localPosition = p + Vector3.up * 0.55f;
                c.transform.localScale = new Vector3(0.36f, 0.03f, 0.36f);
                c.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                c.GetComponent<MeshRenderer>().sharedMaterial = goldMat;
                coinObjs.Add(c.transform);
            }
            // Lines along the path, a ring in the clearing, a few on the deck.
            foreach (float z0 in new[] { 10f, 28f, 58f, 145f, 185f })
                for (int i = 0; i < 6; i++)
                {
                    float z = z0 + i * 1.4f;
                    float x = world.PathX(z);
                    Coin(new Vector3(x, world.TerrainY(x, z), z));
                }
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                var c = world.ClearingCentre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f;
                Coin(new Vector3(c.x, world.TerrainY(c.x, c.y), c.y));
            }
            for (float z = ForestWorld.DeckStart + 4f; z < world.DeckEnd - 3f; z += 5f)
                Coin(new Vector3(UnityEngine.Random.Range(-world.DeckHalfWidth + 1.5f, world.DeckHalfWidth - 1.5f), ForestWorld.DeckHeight, z));
        }

        private void UpdateCoins()
        {
            float spin = Time.time * 180f;
            for (int i = coinObjs.Count - 1; i >= 0; i--)
            {
                var c = coinObjs[i];
                c.localRotation = Quaternion.Euler(90f, 0f, spin);
                var d = c.localPosition - (pos + Vector3.up * 0.55f);
                if (d.sqrMagnitude > 0.8f * 0.8f) continue;
                coins++;
                fx.Burst(c.position, Palette.Coin, Palette.CoinGlow, 10, 3f);
                AudioManager.PlaySfx(Sfx.Coin, 0.7f, 1.3f);
                Destroy(c.gameObject);
                coinObjs.RemoveAt(i);
            }
        }

        private void Hurt(Vector3 from)
        {
            if (hurtLeft > 0f || combat.Invulnerable) return;
            hurtLeft = 1f;
            health -= HitDamage;
            rig.Shake(0.8f);
            hud.FlashHurt();
            Haptics.Medium();
            AudioManager.PlaySfx(Sfx.Squash, 0.7f, 1.3f);
            fx.Burst(world.ToWorld(pos) + Vector3.up * 0.5f, new Color(1f, 0.35f, 0.35f), new Color(2.4f, 0.5f, 0.4f), 20, 5f);
            var push = pos - from;
            push.y = 0f;
            if (push.sqrMagnitude > 0.01f) pos = Collide(pos, pos + push.normalized * 0.8f);
            if (health <= 0.01f)
            {
                // Back to the last checkpoint with full health.
                health = 1f;
                input.Sprint = false;
                combat.Reset();
                motor.Teleport(checkpoint, 0f);
                cam.Reset(0f, world.ToWorld(pos));
            }
        }

        // ---------- Guide ----------

        private void UpdateGuide()
        {
            // The next waypoint not yet reached.
            Vector3 next = world.Waypoints[world.Waypoints.Count - 1];
            int stage = world.Waypoints.Count - 1;
            for (int i = 0; i < world.Waypoints.Count; i++)
                if (pos.z < world.Waypoints[i].z - 1f) { next = world.Waypoints[i]; stage = i; break; }
            bool fight = InFight();
            string key = fight ? "forest.fight"
                : pos.y > ForestWorld.DeckHeight - 0.5f && pos.z > ForestWorld.DeckStart - 1f ? "forest.deck"
                : stage <= 1 ? "forest.follow" : stage == 2 ? "forest.deck" : stage == 3 ? "forest.clearing" : "forest.tunnel";
            hud.ObjectiveText.text = MissionText(Loc.T(key));

            // While the gate is shut and the way is done, the chevron leads to the nearest robot or core left.
            if (!fight && MissionTarget(out var hunt))
            {
                var toward = hunt - pos;
                toward.y = 0f;
                var p = pos + Vector3.ClampMagnitude(toward, 2.5f);
                p.y = GroundY(p.x, p.z, pos.y) + 0.35f + Mathf.Sin(Time.time * 3f) * 0.08f;
                guide.localPosition = p;
                if (toward.sqrMagnitude > 0.01f) guide.localRotation = Quaternion.LookRotation(toward);
                guide.gameObject.SetActive(toward.magnitude > 3f);
                return;
            }
            // The chevron: on the path a few metres ahead, pointing on.
            float aheadZ = Mathf.Min(pos.z + 3.5f, next.z);
            float ax = Mathf.Abs(aheadZ - pos.z) < 0.5f ? next.x : world.PathX(aheadZ);
            var at = new Vector3(ax, 0f, aheadZ);
            at.y = GroundY(at.x, at.z, pos.y) + 0.35f + Mathf.Sin(Time.time * 3f) * 0.08f;
            guide.localPosition = at;
            var dir = new Vector3(world.PathX(aheadZ + 2f) - ax, 0f, 2f);
            guide.localRotation = Quaternion.LookRotation(dir);
            guide.gameObject.SetActive(!fight);
        }

        // ---------- Passages ----------

        /// <summary>
        /// Onto the ride: the walker steps into the cart (or onto the raft) where it waits inside the passage, and the
        /// game runs the passage from here. Nothing is hidden: the land stays where it is, behind.
        /// </summary>
        private void EnterTunnel()
        {
            if (TunnelReached == null) { Finish(); return; }
            suspended = true;
            input.Clear();
            combat.Reset();
            hud.SetVisible(false);
            guide.gameObject.SetActive(false);
            if (hammer != null) hammer.gameObject.SetActive(false);
            robot.ExitArena();
            TunnelReached.Invoke();
        }

        /// <summary>
        /// The next stretch of land, built where the passage comes out (<paramref name="arrival"/>: where the ride
        /// stops, facing on). Its own entry passage meets the ride's end, so the way runs on without a seam.
        /// </summary>
        public void PrepareNext(Vector3 arrival)
        {
            if (nextWorld != null) Destroy(nextWorld.gameObject);
            int leg = Leg + 1;
            nextWorld = ForestWorld.Build(SeedFor(leg), arrival - new Vector3(0f, 0f, ForestWorld.ArriveZ), leg, world.Exit, StyleFor(leg));
        }

        /// <summary>
        /// Out of the passage: the walker carries on from exactly where the ride stopped, the camera glides on from
        /// where it was, in the land built ahead. The land left behind is gone.
        /// </summary>
        public void SwitchWorld(int rideCoins, Pose cameraPose)
        {
            if (nextWorld == null) return;
            Leg++;
            coins += rideCoins;
            ClearDeck();
            coinObjs.Clear();
            enemies.Clear();
            Destroy(world.gameObject);
            world = nextWorld;
            nextWorld = null;
            nearSent = false;
            BuildLand();
            robot.EnterArena();
            robot.gameObject.SetActive(true);
            if (hammer == null) hammer = HammerModels.Held(robot.Visual, Weapons.Level);
            hammer.gameObject.SetActive(true);
            anim.SetHammer(hammer);
            combat.Attach(world.transform, hammer);

            var here = robot.transform.position - world.Origin;
            var fwd = robot.Visual.forward;
            float facing = Mathf.Abs(fwd.x) + Mathf.Abs(fwd.z) > 0.01f ? Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg : 0f;
            checkpoint = new Vector3(here.x, 0f, here.z);
            motor.Teleport(checkpoint, facing);
            deckSafe = new Vector3(0f, ForestWorld.DeckHeight + 0.09f, ForestWorld.DeckStart + 1f);
            cam.Snap(cameraPose, world.ToWorld(pos));
            rig.Chase(cameraPose.position, cameraPose.rotation, tuning.fieldOfView);
            robot.ArenaPlace(world.ToWorld(pos), motor.Forward, snapTurn: true);
            UpdateSkillLocks();
            hud.SetVisible(true);
            suspended = false;
        }

        private void Finish()
        {
            finished = true;
            input.Clear();
            hud.ShowEnd(true);
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1f);
        }
    }
}
