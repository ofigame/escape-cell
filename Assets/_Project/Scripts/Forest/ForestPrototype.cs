using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Gameplay;
using SquashBot.UI;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Forest
{
    /// <summary>
    /// The third-person journey prototype: the robot walks a forest path (left-thumb joystick, the camera always
    /// behind it), climbs stone stairs onto a raised deck full of falling crates and sweeping logs, goes down again,
    /// collects coins and fights two enemy robots in a clearing, and reaches the tunnel mouth.
    /// </summary>
    public partial class ForestPrototype : MonoBehaviour
    {
        private const float WalkSpeed = 3.6f, TurnSpeed = 240f, JumpSpeed = 5.6f, Gravity = 16f;
        private const float CamBack = 3.8f, CamUp = 1.55f, CamPitch = 9f;
        private const float HitDamage = 0.25f;

        public event Action Exited;
        /// <summary>The robot reached the tunnel mouth (the game runs the tunnel, builds the next land with <see cref="PrepareNext"/> and hands back with <see cref="SwitchWorld"/>).</summary>
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

        /// <summary>The next stretch of land, built at the passage's far end while the passage runs.</summary>
        private ForestWorld nextWorld;

        private bool suspended;

        private ForestWorld world;
        private Robot robot;
        private CameraRig rig;
        private FxSystem fx;

        // Walker state (local to the world).
        private Vector3 pos;
        private float yaw, camYaw, vy;
        private bool grounded = true;
        private float health = 1f, hurtLeft;
        private int coins;
        private Vector3 checkpoint;
        private bool finished;

        // Saved scene look, restored on exit.
        private Light sun;
        private Quaternion sunRot;
        private Color sunColor, ambientSky;
        private float sunIntensity;
        private LightShadows sunShadows;
        private bool fog;
        private Material skybox;
        private UnityEngine.Rendering.AmbientMode ambientMode;

        // Camera smoothing.
        private Vector3 camPos;
        private bool camInit;

        // HUD.
        private Canvas canvas;
        private RectTransform joyBase, joyKnob;
        private Image healthFill;
        private TextMeshProUGUI coinText, objectiveText;
        private CanvasGroup endPanel;
        private int joyFinger = -1;
        private Vector2 joyInput;
        private float yawVel, camYawVel;
        private bool jumpQueued, attackQueued;

        // Things in the world.
        private Transform guide, hammer;
        private float swingT = 1f;
        private readonly List<Transform> coinObjs = new List<Transform>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Crate> crates = new List<Crate>();
        private readonly List<Sweeper> sweepers = new List<Sweeper>();
        private float crateTimer = 1f, rollTimer = 2f;
        private Vector3 deckSafe = new Vector3(0f, ForestWorld.DeckHeight + 0.09f, ForestWorld.DeckStart + 1f);
        private readonly List<Transform> rollers = new List<Transform>();
        private Material warnMat, crateMat, goldMat;

        private class Crate
        {
            public Vector3 spot;
            public float t;
            public GameObject warn, box;
            public bool landed;
        }

        private class Sweeper
        {
            public Vector3 centre;
            public float angle, speed, length;
            public Transform beam;
        }

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
            world = ForestWorld.Build(SeedFor(Leg), ForestWorld.FirstOrigin, Leg, PassageStyle.None, StyleFor(Leg));
            SetLook(true);
            robot.EnterArena();
            robot.gameObject.SetActive(true);
            pos = checkpoint = StartPoint();
            yaw = camYaw = 0f;
            hammer = HammerModels.Held(robot.Visual, Weapons.Level);

            warnMat = MaterialFactory.CreateTransparent(new Color(1f, 0.25f, 0.2f, 0.45f), new Color(1.4f, 0.25f, 0.2f));
            crateMat = Resources.Load<Material>("Forest/Bark_Oak");
            goldMat = MaterialFactory.Create(new Color(1f, 0.78f, 0.25f), new Color(1.2f, 0.85f, 0.2f));
            BuildGuide();
            BuildCoins();
            BuildSweepers();
            BuildEnemies();
            BuildMission();
            BuildHud();
            BuildAutoBadge();
            AudioManager.PlayMusic(MusicTheme.Menu);
            BuildAmbience();
        }

        // ---------- Look: sun, sky, fog ----------

        private void SetLook(bool on)
        {
            if (on)
            {
                sun = FindAnyObjectByType<Light>();
                sunRot = sun.transform.rotation;
                sunColor = sun.color;
                sunIntensity = sun.intensity;
                sunShadows = sun.shadows;
                fog = RenderSettings.fog;
                skybox = RenderSettings.skybox;
                ambientMode = RenderSettings.ambientMode;
                ambientSky = RenderSettings.ambientLight;

                sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                sun.color = new Color(1f, 0.94f, 0.84f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.75f;
                QualitySettings.shadowDistance = 45f;
                RenderSettings.skybox = Resources.Load<Material>("Forest/Sky");
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.56f, 0.64f, 0.72f);
                RenderSettings.ambientEquatorColor = new Color(0.42f, 0.47f, 0.4f);
                RenderSettings.ambientGroundColor = new Color(0.24f, 0.23f, 0.19f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.74f, 0.8f, 0.84f);
                RenderSettings.fogStartDistance = 40f;
                RenderSettings.fogEndDistance = 190f;
                rig.SetOutdoor(true);
            }
            else
            {
                sun.transform.rotation = sunRot;
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.shadows = sunShadows;
                RenderSettings.fog = fog;
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientSky;
                rig.SetOutdoor(false);
                rig.EndChase();
            }
        }

        public void End()
        {
            SetLook(false);
            if (hammer != null) Destroy(hammer.gameObject);
            robot.ExitArena();
            Destroy(world.gameObject);
            if (nextWorld != null) Destroy(nextWorld.gameObject);
            if (canvas != null) Destroy(canvas.gameObject);
            foreach (var c in crates) { Destroy(c.warn); Destroy(c.box); }
            Destroy(gameObject);
        }

        // ---------- Build ----------

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

        private void BuildSweepers()
        {
            var bark = Resources.Load<Material>("Forest/Bark_Pine");
            // More logs on every leg's (longer, wider) deck, swinging faster.
            int count = Mathf.Min(2 + (Leg - 1), 6);
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(ForestWorld.DeckStart + 10f, world.DeckEnd - 8f, i / (float)(count - 1));
                float speed = (i % 2 == 0 ? 1f : -1f) * (80f + i * 6f) * (1f + (Leg - 1) * 0.12f);
                float x = count > 3 ? (i % 2 == 0 ? -1f : 1f) * world.DeckHalfWidth * 0.35f : 0f;
                var s = new Sweeper { centre = new Vector3(x, ForestWorld.DeckHeight, z), speed = speed, length = world.DeckHalfWidth - 0.5f - Mathf.Abs(x) * 0.3f, angle = z * 7f };
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(world.transform, false);
                post.transform.localPosition = s.centre + Vector3.up * 0.45f;
                post.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
                post.GetComponent<MeshRenderer>().sharedMaterial = bark;
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(beam.GetComponent<Collider>());
                beam.transform.SetParent(world.transform, false);
                beam.transform.localScale = new Vector3(0.32f, s.length, 0.32f);
                beam.GetComponent<MeshRenderer>().sharedMaterial = bark;
                s.beam = beam.transform;
                sweepers.Add(s);
            }
        }

        // ---------- Ambience ----------

        private ParticleSystem leaves;
        private float stepDust;

        /// <summary>Leaves and pollen drifting down through the light around the camera.</summary>
        private void BuildAmbience()
        {
            var go = new GameObject("DriftingLeaves");
            go.transform.SetParent(transform, false);
            leaves = go.AddComponent<ParticleSystem>();
            leaves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = leaves.main;
            main.startLifetime = 10f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.75f, 0.35f, 0.9f), new Color(0.55f, 0.75f, 0.3f, 0.9f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = leaves.emission;
            emission.rateOverTime = 14f;
            var shape = leaves.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 9f, 26f);
            var vel = leaves.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.45f, -0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            var noise = leaves.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.35f;
            var rot = leaves.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Resources.Load<Material>("SquashBot_ParticleAlpha");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            leaves.Play();
        }

        private void UpdateAmbience(float dt, bool walking)
        {
            if (leaves != null) leaves.transform.position = rig.Cam.transform.position + rig.Cam.transform.forward * 9f;
            if (!walking || !grounded) return;
            stepDust -= dt;
            if (stepDust > 0f) return;
            stepDust = 0.32f;
            bool deck = pos.y > ForestWorld.DeckHeight - 0.5f;
            fx.Dust(world.ToWorld(pos) + Vector3.up * 0.05f, deck ? new Color(0.6f, 0.62f, 0.55f) : new Color(0.45f, 0.36f, 0.26f), 3, 0.7f);
        }

        // ---------- HUD ----------

        private void BuildHud()
        {
            canvas = UiFactory.CreateCanvas("ForestHud", out var scaler);
            canvas.sortingOrder = 40;
            scaler.matchWidthOrHeight = Screen.width < Screen.height ? 0f : 1f;
            var t = canvas.transform;
            var hp = UiFactory.Pill("Health", t, new Vector2(0f, 1f), new Vector2(36f, -200f), new Vector2(340f, 70f), UiFactory.PillColor);
            UIController.HeartIcon(hp, new Vector2(40f, 0f), 46f);
            UiFactory.Bar(hp, new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(240f, 22f), new Color(0.4f, 0.95f, 0.5f), out healthFill);
            var coinPill = UiFactory.Pill("Coins", t, new Vector2(1f, 1f), new Vector2(-36f, -200f), new Vector2(240f, 90f), UiFactory.PillColor);
            UIController.CoinIcon(coinPill, new Vector2(52f, 0f));
            coinText = UiFactory.TextBox("Value", coinPill, new Vector2(0f, 0.5f), new Vector2(96f, 0f), new Vector2(130f, 80f), "0", 46f, Palette.UiGold, align: TextAlignmentOptions.Left);
            var obj = UiFactory.Pill("Objective", t, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(720f, 118f), new Color(0.06f, 0.1f, 0.08f, 0.6f));
            objectiveText = UiFactory.TextBox("Text", obj, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(690f, 110f), "", 34f, Color.white);
            objectiveText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.MakeButton(t, "X", UiFactory.ButtonKind.Icon, new Vector2(0f, 1f), new Vector2(36f, -36f), new Vector2(104f, 104f), () => Exited?.Invoke(), 52f);

            joyBase = UiFactory.Box("JoyBase", t, new Vector2(0f, 0f), Vector2.zero, new Vector2(240f, 240f));
            joyBase.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(joyBase, new Color(1f, 1f, 1f, 0.18f), UiSprites.Circle).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", joyBase), new Color(1f, 1f, 1f, 0.5f), UiSprites.Ring, 0.4f).raycastTarget = false;
            joyKnob = UiFactory.Box("Knob", joyBase, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
            joyKnob.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(joyKnob, new Color(1f, 1f, 1f, 0.7f), UiSprites.Circle).raycastTarget = false;
            joyBase.gameObject.SetActive(false);

            var attack = UiFactory.MakeButton(t, "", UiFactory.ButtonKind.Gold, new Vector2(1f, 0f), new Vector2(-60f, 220f), new Vector2(220f, 220f), () => attackQueued = true, 40f);
            var icon = UiFactory.Box("Icon", attack.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            icon.pivot = new Vector2(0.5f, 0.5f);
            var handle = UiFactory.Box("Handle", icon, new Vector2(0.5f, 0.5f), new Vector2(-8f, -14f), new Vector2(20f, 90f));
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.localRotation = Quaternion.Euler(0f, 0f, 35f);
            UiFactory.Fill(handle, UiFactory.TextDark, UiSprites.Rounded, 8f).raycastTarget = false;
            var head = UiFactory.Box("Head", icon, new Vector2(0.5f, 0.5f), new Vector2(14f, 22f), new Vector2(90f, 42f));
            head.pivot = new Vector2(0.5f, 0.5f);
            head.localRotation = Quaternion.Euler(0f, 0f, 35f);
            UiFactory.Fill(head, UiFactory.TextDark, UiSprites.Rounded, 8f).raycastTarget = false;
            UiFactory.MakeButton(t, "^", UiFactory.ButtonKind.Secondary, new Vector2(1f, 0f), new Vector2(-300f, 120f), new Vector2(150f, 150f), () => jumpQueued = true, 70f);

            var end = UiFactory.Stretch("End", t);
            UiFactory.Fill(end, new Color(0f, 0f, 0f, 0.85f));
            endPanel = end.gameObject.AddComponent<CanvasGroup>();
            UiFactory.TextBox("Title", end, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(900f, 120f), Loc.T("forest.end"), 64f, Palette.UiGold, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.MakeButton(end, Loc.T("forest.again"), UiFactory.ButtonKind.Primary, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(520f, 140f), Restart, 56f)
                .GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            UiFactory.MakeButton(end, Loc.T("btn.menu"), UiFactory.ButtonKind.Secondary, new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(520f, 120f), () => Exited?.Invoke(), 48f)
                .GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            endPanel.alpha = 0f;
            endPanel.blocksRaycasts = false;
        }

        private void Restart()
        {
            finished = false;
            endPanel.alpha = 0f;
            endPanel.blocksRaycasts = false;
            health = 1f;
            autoRun = false;
            pos = checkpoint = StartPoint();
            yaw = camYaw = 0f;
            camInit = false;
        }

        // ---------- Update ----------

        private void Update()
        {
            if (finished || suspended) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            ReadInput();
            Move(dt);
            UpdateAmbience(dt, joyInput.sqrMagnitude > 0.02f);
            UpdateSwing(dt);
            UpdateCoins();
            UpdateDeck(dt);
            UpdateEnemies(dt);
            UpdateMission(dt);
            UpdateGuide();
            if (hurtLeft > 0f) hurtLeft -= dt;
            UiFactory.SetBar(healthFill, health);
            healthFill.color = health > 0.6f ? new Color(0.4f, 0.95f, 0.5f) : health > 0.3f ? new Color(1f, 0.82f, 0.3f) : new Color(1f, 0.38f, 0.38f);
            coinText.text = coins.ToString();
            if (!nearSent && pos.z > world.TunnelZ - 28f) { nearSent = true; TunnelNear?.Invoke(); }
            if (pos.z >= world.BoardZ - 0.3f) EnterTunnel();
        }

        private bool InFight() =>
            (new Vector2(pos.x, pos.z) - world.ClearingCentre).magnitude < ForestWorld.ClearingRadius + 3f && enemies.Exists(e => !e.dead);

        private void Move(float dt)
        {
            if (Steer(dt, out float speed))
            {
                var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var next = pos + fwd * (speed * dt);
                next = Collide(next);
                CheckAutoBlocked(speed * dt, new Vector2(next.x - pos.x, next.z - pos.z).magnitude, dt);
                pos.x = next.x;
                pos.z = next.z;
                robot.ArenaBob(Mathf.Clamp01(speed / WalkSpeed));
            }
            // The camera swings round behind the robot, a little slower than it turns.
            camYaw = Mathf.SmoothDampAngle(camYaw, yaw, ref camYawVel, 0.5f, 140f, dt);

            float ground = GroundY(pos.x, pos.z, pos.y);
            if (jumpQueued && grounded) { vy = JumpSpeed; grounded = false; AudioManager.PlaySfx(Sfx.Hop, 0.6f, 1.1f); }
            jumpQueued = false;
            vy -= Gravity * dt;
            pos.y += vy * dt;
            if (pos.y <= ground)
            {
                pos.y = ground;
                if (!grounded && vy < -3f)
                {
                    // A landing: a puff of dust around the feet.
                    fx.Dust(world.ToWorld(pos) + Vector3.up * 0.05f, new Color(0.5f, 0.42f, 0.3f), 10, 2f);
                    AudioManager.PlaySfx(Sfx.Bump, 0.35f, 1.4f);
                }
                vy = 0f;
                grounded = true;
            }
            else if (pos.y - ground > 0.05f) grounded = false;
            robot.ArenaPlace(world.ToWorld(pos), Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
        }

        /// <summary>The ground under the walker: the stairs and the deck where they are, the terrain elsewhere.</summary>
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

        private bool InPassage(float z) =>
            z > world.TunnelZ - 0.3f || (world.Entry != PassageStyle.None && z < ForestWorld.EntryMouthZ + 0.3f);

        private bool InGap(float x, float z)
        {
            foreach (var g in world.Gaps)
                if (x > g.xMin + 0.15f && x < g.xMax - 0.15f && z > g.yMin + 0.15f && z < g.yMax - 0.15f) return true;
            return false;
        }

        private Vector3 StartPoint() => world.Entry == PassageStyle.None
            ? new Vector3(0f, world.TerrainY(0f, -6f), -6f)
            : new Vector3(0f, 0f, ForestWorld.ArriveZ + 0.5f);

        /// <summary>Keeps the walker out of trunks, off the deck's sides and edges, and near the way.</summary>
        private Vector3 Collide(Vector3 next)
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
            bool high = pos.y > deckTop - 0.8f;
            bool inDeck = Mathf.Abs(next.x) < world.DeckHalfWidth + 0.3f && next.z > ForestWorld.DeckStart - 0.3f && next.z < world.DeckEnd + 0.3f;
            bool stairs = Mathf.Abs(next.x) < ForestWorld.StairsHalfWidth - 0.25f;
            if (high)
            {
                // On the deck or the stairs: no stepping off the sides.
                bool onStairs = next.z < ForestWorld.DeckStart || next.z > world.DeckEnd;
                float limit = onStairs ? ForestWorld.StairsHalfWidth - 0.3f : world.DeckHalfWidth - 0.4f;
                next.x = Mathf.Clamp(next.x, -limit, limit);
            }
            else if (inDeck && !stairs)
            {
                next = new Vector3(pos.x, next.y, pos.z); // the deck's side is a wall
            }
            else if (!high && Mathf.Abs(next.x) < ForestWorld.StairsHalfWidth + 0.3f && next.z > ForestWorld.StairsUpStart && next.z < world.StairsDownEnd && !stairs)
            {
                next = new Vector3(pos.x, next.y, pos.z);
            }
            // Never wander far from the way.
            float px = world.PathX(next.z);
            float maxOff = (new Vector2(next.x, next.z) - world.ClearingCentre).magnitude < ForestWorld.ClearingRadius + 2f ? 14f : 7f;
            next.x = Mathf.Clamp(next.x, px - maxOff, px + maxOff);
            next.z = Mathf.Clamp(next.z, world.Entry == PassageStyle.None ? -12f : ForestWorld.ArriveZ, world.BoardZ + 0.5f);
            // The passage gate stays shut until the tasks are done.
            if (GateShut && next.z > world.TunnelZ - 0.9f) next.z = Mathf.Min(pos.z, world.TunnelZ - 0.9f);
            // The cliffs either side of a passage are solid: slide along them, never through.
            if (world.InRock(next.x, next.z))
            {
                if (!world.InRock(next.x, pos.z)) next.z = pos.z;
                else if (!world.InRock(pos.x, next.z)) next.x = pos.x;
                else { next.x = pos.x; next.z = pos.z; }
            }
            return next;
        }

        private void LateUpdate()
        {
            UpdateDaylight();
            if (suspended) return;
            var target = world.ToWorld(pos) + Vector3.up * 0.9f;
            var rot = Quaternion.Euler(CamPitch, camYaw, 0f);
            var want = target - rot * Vector3.forward * CamBack + Vector3.up * (CamUp - 0.9f);
            camPos = camInit ? Vector3.Lerp(camPos, want, 1f - Mathf.Exp(-Time.deltaTime * 10f)) : want;
            camInit = true;
            var look = Quaternion.LookRotation(target + rot * Vector3.forward * 5f + Vector3.up * 0.3f - camPos);
            rig.Chase(camPos, look, 60f);
        }

        // ---------- Combat, coins, hazards ----------

        private void UpdateSwing(float dt)
        {
            if (attackQueued && swingT >= 1f)
            {
                swingT = 0f;
                AudioManager.PlaySfx(Sfx.Hop, 0.5f, 0.7f);
                var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                foreach (var e in enemies)
                {
                    if (e.dead) continue;
                    var to = e.pos - pos;
                    to.y = 0f;
                    if (to.magnitude > 2.2f || Vector3.Angle(fwd, to) > 75f) continue;
                    HitEnemy(e, to);
                }
            }
            attackQueued = false;
            if (swingT < 1f)
            {
                swingT = Mathf.Min(1f, swingT + dt / 0.3f);
                float a = swingT < 0.35f ? Mathf.Lerp(0f, -60f, swingT / 0.35f) : Mathf.Lerp(-60f, 110f, (swingT - 0.35f) / 0.25f);
                if (swingT > 0.6f) a = Mathf.Lerp(110f, 0f, (swingT - 0.6f) / 0.4f);
                if (hammer != null) hammer.localRotation = Quaternion.Euler(20f + a, 0f, -15f);
            }
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
            if (hurtLeft > 0f) return;
            hurtLeft = 1f;
            health -= HitDamage;
            rig.Shake(0.8f);
            Haptics.Medium();
            AudioManager.PlaySfx(Sfx.Squash, 0.7f, 1.3f);
            fx.Burst(world.ToWorld(pos) + Vector3.up * 0.5f, new Color(1f, 0.35f, 0.35f), new Color(2.4f, 0.5f, 0.4f), 20, 5f);
            var push = pos - from;
            push.y = 0f;
            if (push.sqrMagnitude > 0.01f) pos = Collide(pos + push.normalized * 0.8f);
            if (health <= 0.01f)
            {
                // Back to the last checkpoint with full health.
                health = 1f;
                autoRun = false;
                pos = checkpoint;
                camInit = false;
            }
        }

        private void UpdateDeck(float dt)
        {
            bool onDeck = pos.y > ForestWorld.DeckHeight - 0.5f && pos.z > ForestWorld.DeckStart && pos.z < world.DeckEnd;
            if (pos.z > ForestWorld.StairsUpStart && checkpoint.z < ForestWorld.StairsUpStart) checkpoint = new Vector3(0f, 0f, ForestWorld.StairsUpStart - 2f);
            if (pos.z > world.StairsDownEnd && checkpoint.z < world.StairsDownEnd) checkpoint = new Vector3(0f, 0f, world.StairsDownEnd + 2f);
            checkpoint.y = world.TerrainY(checkpoint.x, checkpoint.z);

            // Holes in the deck: a fall costs health and puts the walker back where it last stood on the stone.
            bool overDeck = Mathf.Abs(pos.x) < world.DeckHalfWidth && pos.z > ForestWorld.DeckStart && pos.z < world.DeckEnd;
            if (onDeck && grounded && !InGap(pos.x, pos.z)) deckSafe = pos;
            if (overDeck && pos.y < ForestWorld.DeckHeight - 1.6f)
            {
                fx.Dust(world.ToWorld(pos), new Color(0.5f, 0.42f, 0.3f), 12, 2f);
                pos = deckSafe;
                vy = 0f;
                hurtLeft = 0f;
                Hurt(pos);
                pos = deckSafe;
                camInit = false;
            }
            UpdateRollers(dt, onDeck);
            UpdateSpikes(dt, onDeck);

            // Falling crates: a red mark, then a crate drops onto it.
            if (onDeck)
            {
                crateTimer -= dt;
                if (crateTimer <= 0f)
                {
                    crateTimer = UnityEngine.Random.Range(1.1f, 1.8f) / (1f + (Leg - 1) * 0.18f);
                    float edge = world.DeckHalfWidth - 0.7f;
                    var spot = new Vector3(Mathf.Clamp(pos.x + UnityEngine.Random.Range(-2.5f, 2.5f), -edge, edge), ForestWorld.DeckHeight + 0.1f,
                        Mathf.Clamp(pos.z + UnityEngine.Random.Range(1f, 6f), ForestWorld.DeckStart + 1f, world.DeckEnd - 1f));
                    var c = new Crate { spot = spot };
                    c.warn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(c.warn.GetComponent<Collider>());
                    c.warn.transform.SetParent(world.transform, false);
                    c.warn.transform.localPosition = spot + Vector3.up * 0.02f;
                    c.warn.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
                    c.warn.GetComponent<MeshRenderer>().sharedMaterial = warnMat;
                    c.box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(c.box.GetComponent<Collider>());
                    c.box.transform.SetParent(world.transform, false);
                    c.box.transform.localScale = Vector3.one * 1.1f;
                    c.box.GetComponent<MeshRenderer>().sharedMaterial = crateMat;
                    c.box.SetActive(false);
                    crates.Add(c);
                }
            }
            for (int i = crates.Count - 1; i >= 0; i--)
            {
                var c = crates[i];
                c.t += dt;
                const float warn = 1.1f;
                if (c.t < warn)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(c.t * 20f);
                    c.warn.transform.localScale = new Vector3(1.6f * pulse, 0.01f, 1.6f * pulse);
                    if (c.t > warn - 0.35f)
                    {
                        c.box.SetActive(true);
                        float k = (c.t - (warn - 0.35f)) / 0.35f;
                        c.box.transform.localPosition = c.spot + Vector3.up * Mathf.Lerp(9f, 0.55f, k * k);
                    }
                    continue;
                }
                if (!c.landed)
                {
                    c.landed = true;
                    c.box.transform.localPosition = c.spot + Vector3.up * 0.55f;
                    Destroy(c.warn);
                    fx.Dust(world.ToWorld(c.spot) + Vector3.up * 0.1f, new Color(0.7f, 0.65f, 0.55f), 16, 3f);
                    rig.Shake(0.3f);
                    AudioManager.PlaySfx(Sfx.Impact, 0.6f, 1f);
                    var d = new Vector2(pos.x - c.spot.x, pos.z - c.spot.z);
                    if (d.magnitude < 0.95f && pos.y < c.spot.y + 1.2f) Hurt(c.spot);
                }
                if (c.t > warn + 2.2f)
                {
                    Destroy(c.box);
                    crates.RemoveAt(i);
                }
            }

            // Sweeping logs at knee height: jump them or slip past behind.
            foreach (var s in sweepers)
            {
                s.angle += s.speed * dt;
                var dir = Quaternion.Euler(0f, s.angle, 0f) * Vector3.forward;
                s.beam.localPosition = s.centre + Vector3.up * 0.45f + dir * (s.length * 0.5f + 0.2f);
                s.beam.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
                if (!onDeck || pos.y > ForestWorld.DeckHeight + 0.75f) continue;
                var rel = pos - s.centre;
                rel.y = 0f;
                float along = Vector3.Dot(rel, dir);
                float side = (rel - dir * along).magnitude;
                if (along > 0.2f && along < s.length + 0.4f && side < 0.45f) Hurt(pos - Vector3.Cross(Vector3.up, dir) * Mathf.Sign(s.speed));
            }
        }

        /// <summary>From the third leg on, logs roll down the deck towards the walker: jump them.</summary>
        private void UpdateRollers(float dt, bool onDeck)
        {
            if (Leg >= 3 && onDeck)
            {
                rollTimer -= dt;
                if (rollTimer <= 0f && pos.z < world.DeckEnd - 10f)
                {
                    rollTimer = UnityEngine.Random.Range(3.2f, 4.6f) / (1f + (Leg - 3) * 0.15f);
                    var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(log.GetComponent<Collider>());
                    log.name = "RollingLog";
                    log.transform.SetParent(world.transform, false);
                    log.transform.localScale = new Vector3(0.75f, world.DeckHalfWidth - 0.4f, 0.75f);
                    log.transform.localPosition = new Vector3(0f, ForestWorld.DeckHeight + 0.46f, Mathf.Min(pos.z + 14f, world.DeckEnd - 0.5f));
                    log.GetComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("Forest/Bark_Pine");
                    rollers.Add(log.transform);
                    AudioManager.PlaySfx(Sfx.Bump, 0.5f, 0.6f);
                }
            }
            float speed = 4.2f + (Leg - 3) * 0.4f;
            for (int i = rollers.Count - 1; i >= 0; i--)
            {
                var r = rollers[i];
                var p = r.localPosition;
                p.z -= speed * dt;
                r.localPosition = p;
                r.localRotation = Quaternion.Euler(-p.z / 0.375f * Mathf.Rad2Deg, 0f, 90f);
                if (onDeck && Mathf.Abs(pos.z - p.z) < 0.55f && pos.y < ForestWorld.DeckHeight + 0.75f) Hurt(pos + Vector3.forward);
                if (p.z < ForestWorld.DeckStart)
                {
                    fx.Dust(world.ToWorld(p), new Color(0.5f, 0.42f, 0.3f), 10, 2f);
                    Destroy(r.gameObject);
                    rollers.RemoveAt(i);
                }
            }
        }

        private void UpdateGuide()
        {
            // The next waypoint not yet reached.
            Vector3 next = world.Waypoints[world.Waypoints.Count - 1];
            int stage = world.Waypoints.Count - 1;
            for (int i = 0; i < world.Waypoints.Count; i++)
                if (pos.z < world.Waypoints[i].z - 1f) { next = world.Waypoints[i]; stage = i; break; }
            bool fight = (new Vector2(pos.x, pos.z) - world.ClearingCentre).magnitude < ForestWorld.ClearingRadius + 3f && enemies.Exists(e => !e.dead);
            string key = fight ? "forest.fight"
                : pos.y > ForestWorld.DeckHeight - 0.5f && pos.z > ForestWorld.DeckStart - 1f ? "forest.deck"
                : stage <= 1 ? "forest.follow" : stage == 2 ? "forest.deck" : stage == 3 ? "forest.clearing" : "forest.tunnel";
            objectiveText.text = MissionText(Loc.T(key));

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

        /// <summary>
        /// The light follows the camera through the passages: a cave swallows the daylight a few metres in and gives
        /// it back at the far mouth; a gorge only shades it. The camera's place in the land it left and in the land
        /// ahead tells how deep in it is.
        /// </summary>
        private void UpdateDaylight()
        {
            if (sun == null || rig == null) return;
            var cam = rig.Cam.transform.position;
            float depth = 0f;
            var style = PassageStyle.None;
            if (world != null)
            {
                float zIn = cam.z - world.Origin.z;
                depth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(world.TunnelZ - 1f, world.TunnelZ + 9f, zIn));
                style = world.Exit;
                if (world.Entry != PassageStyle.None && !suspended)
                {
                    depth = Mathf.Max(depth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ForestWorld.EntryMouthZ + 1f, ForestWorld.EntryMouthZ - 9f, zIn)));
                    if (zIn < world.TunnelZ - 20f) style = world.Entry;
                }
            }
            if (nextWorld != null)
            {
                float zOut = cam.z - nextWorld.Origin.z;
                depth = Mathf.Min(depth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ForestWorld.EntryMouthZ + 1f, ForestWorld.EntryMouthZ - 9f, zOut)));
            }
            float dark = depth * (style == PassageStyle.Cave ? 0.93f : style == PassageStyle.Gorge ? 0.4f : 0f);
            sun.intensity = Mathf.Lerp(1.35f, 0.08f, dark);
            float a = Mathf.Lerp(1f, 0.16f, dark);
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.64f, 0.72f) * a;
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.47f, 0.4f) * a;
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.23f, 0.19f) * a;
            RenderSettings.fogColor = Color.Lerp(new Color(0.74f, 0.8f, 0.84f), new Color(0.05f, 0.05f, 0.06f), dark);
            if (leaves != null)
            {
                var em = leaves.emission;
                em.rateOverTime = 14f * (1f - depth);
            }
        }

        /// <summary>
        /// Onto the ride: the walker steps into the cart (or onto the raft) where it waits inside the passage, and the
        /// game runs the passage from here. Nothing is hidden: the land stays where it is, behind.
        /// </summary>
        private void EnterTunnel()
        {
            if (TunnelReached == null) { Finish(); return; }
            suspended = true;
            autoRun = false;
            canvas.gameObject.SetActive(false);
            guide.gameObject.SetActive(false);
            joyBase.gameObject.SetActive(false);
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
        /// Out of the passage: the walker carries on from exactly where the ride stopped, the camera from exactly where
        /// it was, in the land built ahead. The land left behind is gone.
        /// </summary>
        public void SwitchWorld(int rideCoins, Pose cameraPose)
        {
            if (nextWorld == null) return;
            Leg++;
            coins += rideCoins;
            foreach (var c in crates) { Destroy(c.warn); Destroy(c.box); }
            crates.Clear();
            foreach (var r in rollers) if (r != null) Destroy(r.gameObject);
            rollers.Clear();
            coinObjs.Clear();
            enemies.Clear();
            sweepers.Clear();
            Destroy(world.gameObject);
            world = nextWorld;
            nextWorld = null;
            nearSent = false;
            BuildGuide();
            BuildCoins();
            BuildSweepers();
            BuildEnemies();
            BuildMission();
            autoRun = false;
            robot.EnterArena();
            robot.gameObject.SetActive(true);
            if (hammer == null) hammer = HammerModels.Held(robot.Visual, Weapons.Level);
            hammer.gameObject.SetActive(true);

            var here = robot.transform.position - world.Origin;
            pos = checkpoint = new Vector3(here.x, 0f, here.z);
            deckSafe = new Vector3(0f, ForestWorld.DeckHeight + 0.09f, ForestWorld.DeckStart + 1f);
            var fwd = robot.Visual.forward;
            yaw = Mathf.Abs(fwd.x) + Mathf.Abs(fwd.z) > 0.01f ? Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg : 0f;
            camYaw = cameraPose.rotation.eulerAngles.y;
            camPos = cameraPose.position;
            camInit = true; // glide from the ride's camera into the walk's
            vy = 0f;
            grounded = true;
            rig.Chase(cameraPose.position, cameraPose.rotation, 60f);
            robot.ArenaPlace(world.ToWorld(pos), Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            canvas.gameObject.SetActive(true);
            suspended = false;
        }

        private void Finish()
        {
            finished = true;
            endPanel.alpha = 1f;
            endPanel.blocksRaycasts = true;
            joyBase.gameObject.SetActive(false);
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1f);
        }
    }
}
