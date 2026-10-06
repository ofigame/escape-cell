using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Bonus mode "escape tunnel": the robot sprints down a winding course seen from behind (third person).
    /// Swipe left / right to change lanes; tap to hop, swipe up and keep holding to jump higher and further;
    /// swipe down to slide under bars. Dodge dropping and lane-switching blocks, clear hurdles, ride jump pads,
    /// grab coins, magnets and shields, and reach the exit at the end.
    ///
    /// The course is planned in "track space" (lane offset x, height y, distance z along the course) and bent onto
    /// a curve with turns and hills only when it is drawn, so all the running rules stay simple. Rows are built
    /// a little ahead and removed behind.
    /// </summary>
    public partial class DuctRunner : MonoBehaviour
    {
        public const float TrackLength = 320f;
        private const int Lanes = 3;
        private const float LaneWidth = 1.1f;
        private const float SpeedStart = 5.4f;
        private const float SpeedEnd = 9.2f;
        // Jumping: a tap is a hop that clears a two-row hole or a hurdle; holding the finger down after a swipe up
        // (or holding space) softens gravity for a moment, so the jump goes higher and further.
        private const float JumpVelocity = 5.8f;
        private const float Gravity = 17f;
        private const float HoldGravity = 0.36f;
        private const float MaxHold = 0.34f;
        private const float PadVelocity = 10.5f;
        private const float SlideTime = 0.75f;
        private const float LaneSpeed = 10f;
        private const int BuildAhead = 46;
        private const float StartDelay = 1.3f;
        private const float BlockDropDistance = 10f;
        private const float CoyoteDepth = -0.3f;
        private const float FallDepth = -0.6f;
        private const float WallX = 1.5f * LaneWidth + 0.3f;
        private const float MagnetTime = 8f;

        /// <summary>A coin was grabbed at this world position.</summary>
        public event Action<Vector3> CoinCollected;
        /// <summary>The run ended: whether the robot got out, and the exit bonus it earned (safe gate +10, risky route +30).</summary>
        public event Action<bool, int> Finished;
        /// <summary>The robot took the risky gate (for a heads-up).</summary>
        public event Action RiskTaken;
        /// <summary>Something worth a floating word happened (Loc key, world position): magnet, shield, saved by the shield.</summary>
        public event Action<string, Vector3> Notice;

        /// <summary>The flavours of bonus tunnel: same running and lanes, different world and rules.</summary>
        public enum Kind { Duct, Surf, Mine, Neon, Laser, Crystal, Void }

        public const int SafeBonus = 10, RiskBonus = 30;
        private const int RiskLength = 40;
        private Kind kind;
        private bool risky;
        private Transform ride, bubble;
        private readonly HashSet<(int, int)> ramps = new HashSet<(int, int)>();
        private int GateRow => Mathf.CeilToInt(length);

        // A road between two levels (instead of a bonus tunnel): it starts at the edge of the platform just beaten, gets
        // longer and harder with the level, cannot be lost (a bump costs coins, a fall puts the robot back on the road)
        // and ends on the next floor's landing.
        private bool roadMode;
        private float length = TrackLength, difficulty, invulnerable;
        private int nextWorld, roadLevel;
        private float speedStart = SpeedStart, speedEnd = SpeedEnd, startDelay = StartDelay;

        /// <summary>The course is built (it can be seen) but the robot is not on it yet.</summary>
        public bool Prepared { get; private set; }
        /// <summary>The road was walked to the next floor's landing.</summary>
        public event Action Arrived;

        // Seamless hand-over from the platform view: the camera glides from where it was into the chase view.
        private float blend = 1f, blendFov = 62f;
        private Pose blendFrom;

        /// <summary>The tunnel owns the robot, camera and input (from Begin until Stop).</summary>
        public bool Active { get; private set; }
        public int Coins { get; private set; }
        /// <summary>Test hooks: nothing ends the run (screenshots of the whole course).</summary>
        public static bool TestInvulnerable;
        public float Progress => Mathf.Clamp01(z / length);

        private Robot robot;
        private CameraRig rig;
        private InputReader input;
        private FxSystem fx;
        private Transform visual, shadow;
        private readonly List<(Transform t, Vector3 pos, Quaternion rot)> limbs = new List<(Transform, Vector3, Quaternion)>();
        private Transform legL, legR, armL, armR;

        private enum ObstacleKind
        {
            /// <summary>A block that drops into its lane (a buoy on the surf channel, floating from the start).</summary>
            Drop,
            /// <summary>A low barrier across every lane: jump it.</summary>
            Hurdle,
            /// <summary>A bar across every lane at head height: slide under it.</summary>
            Bar,
            /// <summary>A block sliding from lane to lane.</summary>
            Mover,
            /// <summary>A pad in one lane that throws the robot high over a wide gap.</summary>
            Pad,
            Magnet,
            Shield
        }

        private class Obstacle
        {
            public ObstacleKind kind;
            public int lane, row;
            public float y, vy, phase;
            public bool landed, done;
            public Transform go, ring;
            public Material ringMaterial;
            public float X(float time) => kind == ObstacleKind.Mover ? Mathf.Sin(time * 1.6f + phase) * LaneWidth : LaneX(lane);
        }

        private class Coin
        {
            public int lane;
            public float z, y;
            public Transform go;
        }

        private bool[,] floorPlan;
        private int totalRows;
        private readonly List<(ObstacleKind kind, int lane, int row)> obstaclePlan = new List<(ObstacleKind, int, int)>();
        private readonly List<(int lane, float z, float y)> coinPlan = new List<(int, float, float)>();
        private readonly Queue<(int row, Transform root)> rows = new Queue<(int, Transform)>();
        private readonly List<Obstacle> obstacles = new List<Obstacle>();
        private readonly List<Coin> coins = new List<Coin>();
        private int builtRow;
        private Transform gate, exitRing;

        // The bent course: per row, its centre in the world, its heading and its slope.
        private Vector3[] centers;
        private float[] headings, slopes;
        private readonly HashSet<int> curveRows = new HashSet<int>();

        private Material frameMat, topMat, slabMat, wallGlowMat, archMat, blockMat, coinMat, rimMat, railMat, foamMat, poolMat;
        private Material hurdleMat, stripeMat, barMat, padMat, magnetMat, shieldMat, chevronMat;

        // Robot state (track space)
        private float x, y, z, vy;
        private int lane = 1;
        private bool grounded, falling, crashed, ended, escaped, shielded;
        private float startTimer, runPhase, time, endTimer, squash, holdTime, slideLeft, magnetLeft;
        private Vector3 camPos;
        private Quaternion camRot;

        public void Init(Robot robotRef, CameraRig rigRef, InputReader inputRef, FxSystem fxRef)
        {
            robot = robotRef;
            rig = rigRef;
            input = inputRef;
            fx = fxRef;
        }

        // ---------- Start / stop ----------

        public void Begin(int seed, Kind tunnel = Kind.Duct)
        {
            Prepare(seed, tunnel, Vector3.zero, 0f);
            TakeOver();
        }

        /// <summary>
        /// Builds a road between two levels from the platform edge at <paramref name="origin"/> heading out at
        /// <paramref name="heading"/> degrees: it is seen at once, and <see cref="TakeOver"/> puts the robot on it when
        /// it steps onto the edge tile. <paramref name="level"/> (0-based) makes it longer and harder.
        /// </summary>
        public void PrepareRoad(int seed, Vector3 origin, float heading, int level, int toWorld)
        {
            // Every level adds to the road: 40 rows after level 1, about 2 more each level (about 400 after level 200).
            // Its difficulty follows the level just as closely: speed, spacing and the kinds of obstacles.
            float d = Mathf.Clamp01(level / 199f);
            roadLevel = level;
            Prepare(seed, RoadTheme(level), origin, heading, road: true, roadLength: 40f + level * 1.8f, roadDifficulty: d, toWorld: toWorld);
        }

        private void Prepare(int seed, Kind tunnel, Vector3 origin, float heading, bool road = false, float roadLength = TrackLength, float roadDifficulty = 1f, int toWorld = 0)
        {
            Stop();
            Prepared = true;
            kind = tunnel;
            roadMode = road;
            length = roadLength;
            difficulty = roadDifficulty;
            nextWorld = toWorld;
            speedStart = road ? Mathf.Lerp(4.4f, 6.2f, roadDifficulty) : SpeedStart;
            speedEnd = road ? Mathf.Lerp(6.2f, 9.6f, roadDifficulty) : SpeedEnd;
            startDelay = road ? 0f : StartDelay;
            transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, heading, 0f));
            risky = false;
            Coins = 0;
            x = 0f; y = 0f; z = road ? -1f : 0f; vy = 0f;
            lane = 1;
            grounded = true;
            falling = crashed = ended = escaped = shielded = false;
            startTimer = endTimer = squash = time = holdTime = slideLeft = magnetLeft = invulnerable = 0f;

            CreateMaterials();
            var rng = new System.Random(seed);
            Plan(rng);
            BendCourse(rng);
            builtRow = 0;
            while (builtRow < Mathf.Min(totalRows, BuildAhead)) BuildRow(builtRow++);
            if (!roadMode) BuildGate();
        }

        /// <summary>The robot, camera and input belong to the course from now on; the camera glides into the chase view.</summary>
        public void TakeOver()
        {
            Active = true;
            var cam = rig.Cam;
            blendFrom = rig.CurrentPose;
            // A narrow perspective from where the flat (orthographic) camera stood frames the same picture, so the switch is invisible.
            blendFov = cam.orthographic ? Mathf.Max(1f, 2f * Mathf.Atan(cam.orthographicSize / Mathf.Max(1f, (blendFrom.position - robot.transform.position).magnitude)) * Mathf.Rad2Deg) : cam.fieldOfView;
            blend = roadMode ? 0f : 1f;

            // Take over the robot: its own grid animations pause while the tunnel poses it.
            robot.gameObject.SetActive(true);
            robot.enabled = false;
            visual = robot.Visual;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            shadow = robot.transform.Find("Shadow");
            limbs.Clear();
            foreach (var name in new[] { "LegL", "LegR", "FootL", "FootR", "ArmL", "ArmR" })
            {
                var t = visual.Find(name);
                if (t != null) limbs.Add((t, t.localPosition, t.localRotation));
            }
            legL = visual.Find("LegL");
            legR = visual.Find("LegR");
            armL = visual.Find("ArmL");
            armR = visual.Find("ArmR");

            BuildRide();
            StartThemeFx();
            input.ScreenMode = true;
            camPos = World(0f, 2.6f, -4.6f);
            PoseRobot(0f);
            UpdateCamera(1f);
        }

        /// <summary>Removes the tunnel and gives the robot, camera and input back to the platform game.</summary>
        public void Stop()
        {
            if (!Active && !Prepared) return;
            bool hadRobot = Active;
            Active = false;
            Prepared = false;
            while (rows.Count > 0) Destroy(rows.Dequeue().root.gameObject);
            foreach (var o in obstacles) { Destroy(o.go.gameObject); if (o.ring != null) Destroy(o.ring.gameObject); }
            foreach (var c in coins) Destroy(c.go.gameObject);
            obstacles.Clear();
            coins.Clear();
            if (gate != null) Destroy(gate.gameObject);
            if (ride != null) Destroy(ride.gameObject);
            if (bubble != null) Destroy(bubble.gameObject);
            ride = bubble = null;
            StopThemeFx();
            ramps.Clear();
            curveRows.Clear();
            if (!hadRobot) return;

            foreach (var (t, pos, rot) in limbs)
            {
                t.localPosition = pos;
                t.localRotation = rot;
            }
            limbs.Clear();
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                visual.localScale = Vector3.one;
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
            }
            robot.transform.rotation = Quaternion.identity;
            robot.enabled = true;
            rig.EndChase();
            input.ScreenMode = false;
        }

        private void CreateMaterials()
        {
            frameMat = MaterialFactory.Create(Palette.TileTop, Palette.TileGlow * 0.55f);
            topMat = MaterialFactory.Create(Palette.TileTop * 0.92f, Palette.TileSelfLight * 0.5f);
            slabMat = MaterialFactory.Create(Color.Lerp(Palette.Slab, Palette.Pillar, 0.5f), Color.black);
            wallGlowMat = MaterialFactory.Create(Palette.Slab, Palette.SlabEdgeGlow);
            var accent = WorldTheme.Current.accent;
            archMat = MaterialFactory.Create(accent, accent * 0.9f);
            blockMat = MaterialFactory.Create(Palette.Block, Palette.BlockGlow * 0.6f);
            hurdleMat = MaterialFactory.Create(new Color(1f, 0.85f, 0.3f), new Color(0.9f, 0.6f, 0.1f));
            stripeMat = MaterialFactory.Create(new Color(0.18f, 0.16f, 0.3f), Color.black);
            barMat = MaterialFactory.Create(new Color(1f, 0.35f, 0.4f), new Color(2f, 0.35f, 0.4f));
            padMat = MaterialFactory.Create(new Color(0.4f, 1f, 0.6f), new Color(0.4f, 2.2f, 0.8f));
            magnetMat = MaterialFactory.Create(new Color(1f, 0.3f, 0.35f), new Color(1.2f, 0.2f, 0.25f));
            shieldMat = MaterialFactory.Create(Palette.ShieldPickup, Palette.ShieldPickupGlow);
            chevronMat = MaterialFactory.Create(new Color(1f, 0.6f, 0.2f), new Color(2.2f, 1f, 0.2f));
            switch (kind)
            {
                case Kind.Surf:
                    // A water channel between sandy banks; buoys instead of blocks, driftwood hurdles.
                    frameMat = MaterialFactory.Create(new Color(0.35f, 0.75f, 0.95f), new Color(0.2f, 0.7f, 1.1f));
                    topMat = MaterialFactory.Create(new Color(0.3f, 0.65f, 0.95f), new Color(0.05f, 0.25f, 0.45f));
                    slabMat = MaterialFactory.Create(new Color(0.95f, 0.85f, 0.62f), Color.black);
                    wallGlowMat = MaterialFactory.Create(new Color(0.4f, 0.8f, 0.5f), new Color(0.2f, 0.6f, 0.3f));
                    archMat = MaterialFactory.Create(new Color(1f, 0.6f, 0.3f), new Color(0.9f, 0.4f, 0.1f));
                    blockMat = MaterialFactory.Create(new Color(1f, 0.3f, 0.3f), new Color(0.6f, 0.1f, 0.1f));
                    hurdleMat = MaterialFactory.Create(new Color(0.65f, 0.45f, 0.28f), Color.black);
                    foamMat = MaterialFactory.Create(Color.white, new Color(0.8f, 1f, 1.1f));
                    poolMat = MaterialFactory.Create(new Color(0.08f, 0.2f, 0.4f), new Color(0.05f, 0.25f, 0.6f));
                    break;
                case Kind.Mine:
                    // Plank tracks with rails, rock walls, lamps on timber posts, falling rocks, low timber beams.
                    frameMat = MaterialFactory.Create(new Color(0.45f, 0.3f, 0.2f), Color.black);
                    topMat = MaterialFactory.Create(new Color(0.62f, 0.44f, 0.28f), new Color(0.08f, 0.05f, 0.02f));
                    slabMat = MaterialFactory.Create(new Color(0.42f, 0.36f, 0.34f), Color.black);
                    wallGlowMat = MaterialFactory.Create(new Color(1f, 0.7f, 0.3f), new Color(1.8f, 0.9f, 0.3f));
                    archMat = MaterialFactory.Create(new Color(0.55f, 0.38f, 0.22f), Color.black);
                    blockMat = MaterialFactory.Create(new Color(0.5f, 0.42f, 0.38f), new Color(0.15f, 0.08f, 0.05f));
                    hurdleMat = MaterialFactory.Create(new Color(0.55f, 0.38f, 0.22f), Color.black);
                    barMat = MaterialFactory.Create(new Color(0.6f, 0.42f, 0.25f), new Color(0.3f, 0.12f, 0.02f));
                    railMat = MaterialFactory.Create(new Color(0.75f, 0.75f, 0.82f), new Color(0.2f, 0.2f, 0.25f));
                    break;
            }
            coinMat = MaterialFactory.Create(Palette.Coin, Palette.CoinGlow);
            rimMat = MaterialFactory.Create(Palette.CoinRim, Palette.CoinGlow * 0.4f);
            ThemeMaterials();
        }

        // ---------- Course planning ----------

        /// <summary>
        /// Lays out the whole course: calm stretches between patterns (coin lines, holes, full-width gaps, dropping
        /// and sliding blocks, hurdles, bars, jump pads, pickups). Every pattern leaves a way through, and later
        /// patterns come closer together and include the harder ones.
        /// </summary>
        private void Plan(System.Random rng)
        {
            totalRows = roadMode ? Mathf.CeilToInt(length) + 14 : GateRow + RiskLength + 30;
            ramps.Clear();
            floorPlan = new bool[totalRows, Lanes];
            for (int r = 0; r < totalRows; r++)
                for (int l = 0; l < Lanes; l++)
                    floorPlan[r, l] = true;
            obstaclePlan.Clear();
            coinPlan.Clear();
            int magnets = 0, shields = 0;

            CoinLine(1, 5, 6);
            int row = 16;
            while (row < length - (roadMode ? 6 : 14))
            {
                float p = row / length;
                // Early on only the gentle patterns; the full set from a fifth of the way in.
                // On roads a new kind of obstacle joins every 3 levels: coins, holes and blocks first; hurdles from level 4,
                // full gaps from 7, bars from 10, sliding blocks 13, zigzags 16, jump pads 19, pickups 22.
                int kinds = roadMode ? Mathf.Clamp(3 + roadLevel / 3, 3, 10) : (p < 0.1f ? 4 : p < 0.2f ? 7 : 10);
                int pattern = rng.Next(kinds);
                switch (pattern)
                {
                    case 0: // a line of coins in one lane
                        CoinLine(rng.Next(Lanes), row, 6);
                        row += 6;
                        break;

                    case 1: // holes in one or two lanes: switch lanes or jump them
                    {
                        int holes = p > 0.3f && rng.Next(2) == 0 ? 2 : 1;
                        int keep = rng.Next(Lanes);
                        for (int l = 0; l < holes; l++)
                        {
                            int hl = (keep + 1 + l) % Lanes;
                            Hole(hl, row, 2);
                            if (l == 0) CoinArc(hl, row, 1.25f);
                        }
                        row += 2;
                        break;
                    }

                    case 2: // one or two dropping blocks, coins in the free lane
                    {
                        int free = rng.Next(Lanes);
                        int count = p > 0.25f && rng.Next(2) == 0 ? 2 : 1;
                        for (int l = 0; l < count; l++) obstaclePlan.Add((ObstacleKind.Drop, (free + 1 + l) % Lanes, row));
                        CoinLine(free, row - 2, 5);
                        row += 3;
                        break;
                    }

                    case 3: // a hurdle across every lane, coins floating over it: jump!
                        obstaclePlan.Add((ObstacleKind.Hurdle, 1, row));
                        CoinArc(rng.Next(Lanes), row, 1.3f);
                        row += 2;
                        break;

                    case 4: // the floor drops away across all lanes: jump (surf: a wave throws the board over)
                        for (int l = 0; l < Lanes; l++) Hole(l, row, 2);
                        if (kind == Kind.Surf) for (int l = 0; l < Lanes; l++) ramps.Add((l, row - 1));
                        CoinArc(rng.Next(Lanes), row, 1.25f);
                        row += 2;
                        break;

                    case 5: // a bar at head height across the course: slide under, coins low beneath it
                        obstaclePlan.Add((ObstacleKind.Bar, 1, row));
                        for (int i = -1; i <= 1; i++) coinPlan.Add((rng.Next(Lanes), row + i * 0.6f, 0.3f));
                        row += 2;
                        break;

                    case 6: // a block sliding from lane to lane
                        obstaclePlan.Add((ObstacleKind.Mover, 1, row));
                        CoinLine(rng.Next(Lanes), row + 3, 4);
                        row += 6;
                        break;

                    case 7: // zigzag: blocks in alternating lanes
                    {
                        int a = rng.Next(Lanes);
                        int b = (a + 1 + rng.Next(Lanes - 1)) % Lanes;
                        obstaclePlan.Add((ObstacleKind.Drop, a, row));
                        obstaclePlan.Add((ObstacleKind.Drop, b, row + 5));
                        row += 6;
                        break;
                    }

                    case 8: // a jump pad before a wide gap: a big flight through a high arc of coins
                    {
                        int pl = rng.Next(Lanes);
                        obstaclePlan.Add((ObstacleKind.Pad, pl, row));
                        for (int l = 0; l < Lanes; l++) Hole(l, row + 2, 4);
                        for (int i = 0; i < 6; i++)
                        {
                            float t = i / 5f;
                            coinPlan.Add((pl, row + 1f + t * 5f, 0.8f + Mathf.Sin(t * Mathf.PI) * 2.2f));
                        }
                        row += 6;
                        break;
                    }

                    default: // a pickup: magnet or shield (a couple of each per run), else a coin line
                    {
                        int pl = rng.Next(Lanes);
                        if (magnets < 2 && rng.Next(2) == 0) { obstaclePlan.Add((ObstacleKind.Magnet, pl, row)); magnets++; }
                        else if (shields < 2) { obstaclePlan.Add((ObstacleKind.Shield, pl, row)); shields++; }
                        else CoinLine(pl, row, 5);
                        row += 3;
                        break;
                    }
                }
                row += Mathf.RoundToInt(roadMode ? Mathf.Lerp(9.5f, 4f, difficulty * 0.8f + p * 0.2f) : Mathf.Lerp(7f, 4f, p)) + rng.Next(2);
            }

            if (roadMode) return; // a road ends on the next floor's landing, no gates

            // The risky route past the right-hand gate: short, dense and fast.
            int r2 = GateRow + 4;
            while (r2 < GateRow + RiskLength - 4)
            {
                switch (rng.Next(4))
                {
                    case 0:
                    {
                        int a = rng.Next(Lanes);
                        obstaclePlan.Add((ObstacleKind.Drop, a, r2));
                        obstaclePlan.Add((ObstacleKind.Drop, (a + 1 + rng.Next(Lanes - 1)) % Lanes, r2 + 3));
                        r2 += 4;
                        break;
                    }
                    case 1:
                    {
                        int keep = rng.Next(Lanes);
                        for (int l = 0; l < Lanes; l++) if (l != keep) Hole(l, r2, 2);
                        CoinLine(keep, r2 - 1, 4);
                        r2 += 3;
                        break;
                    }
                    case 2:
                        obstaclePlan.Add((ObstacleKind.Hurdle, 1, r2));
                        CoinArc(1, r2, 1.3f);
                        r2 += 2;
                        break;
                    default:
                        obstaclePlan.Add((ObstacleKind.Bar, 1, r2));
                        CoinLine(rng.Next(Lanes), r2 - 1, 3);
                        r2 += 2;
                        break;
                }
                r2 += 3;
            }
            // The right-hand lane leads into the risky route; a block splits it from the safe gate.
            obstaclePlan.Add((ObstacleKind.Drop, 1, GateRow + 1));
        }

        /// <summary>
        /// Bends the straight plan into a course: straight runs, sweeping left and right turns and gentle hills.
        /// The start and the gates stay straight and level.
        /// </summary>
        private void BendCourse(System.Random rng)
        {
            int n = totalRows + 1;
            var turn = new float[n];
            var height = new float[n];
            int straightFrom = roadMode ? Mathf.CeilToInt(length) - 4 : GateRow - 16;
            int r = roadMode ? 8 : 22;
            while (r < straightFrom)
            {
                if (rng.Next(3) > 0)
                {
                    // A turn: the turning rate eases in and out, total 40-100 degrees.
                    int len = 16 + rng.Next(14);
                    float total = (40f + rng.Next(60)) * (rng.Next(2) == 0 ? -1f : 1f) * Mathf.Deg2Rad;
                    for (int i = 0; i < len && r + i < straightFrom; i++)
                    {
                        float s = Mathf.Sin((i + 0.5f) / len * Mathf.PI);
                        turn[r + i] = total * s * (Mathf.PI / 2f) / len;
                        curveRows.Add(r + i);
                    }
                    r += len;
                }
                if (rng.Next(2) == 0)
                {
                    // A hill (up then down) under whatever comes next.
                    int len = 18 + rng.Next(16);
                    float amp = 0.6f + (float)rng.NextDouble() * 1.4f;
                    for (int i = 0; i < len && r + i < straightFrom; i++)
                        height[r + i] = amp * (1f - Mathf.Cos(i / (float)len * Mathf.PI * 2f)) * 0.5f;
                }
                r += 8 + rng.Next(16);
            }

            centers = new Vector3[n];
            headings = new float[n];
            slopes = new float[n];
            for (int i = 1; i < n; i++)
            {
                headings[i] = headings[i - 1] + turn[i - 1];
                float mid = (headings[i] + headings[i - 1]) * 0.5f;
                centers[i] = centers[i - 1] + new Vector3(Mathf.Sin(mid), 0f, Mathf.Cos(mid));
                centers[i].y = height[i];
            }
            for (int i = 0; i < n; i++)
            {
                float dy = (i + 1 < n ? centers[i + 1].y : centers[i].y) - (i > 0 ? centers[i - 1].y : centers[i].y);
                slopes[i] = Mathf.Atan2(dy, 2f) * Mathf.Rad2Deg;
            }
        }

        private void Hole(int l, int row, int length)
        {
            for (int r = row; r < row + length && r < totalRows; r++) floorPlan[r, l] = false;
        }

        private void CoinLine(int l, int row, int count)
        {
            for (int i = 0; i < count; i++) coinPlan.Add((l, row + i, 0.45f));
        }

        /// <summary>Coins following a jump: only a jump collects them.</summary>
        private void CoinArc(int l, int row, float top)
        {
            coinPlan.Add((l, row - 0.7f, top - 0.3f));
            coinPlan.Add((l, row + 0.5f, top));
            coinPlan.Add((l, row + 1.7f, top - 0.3f));
        }

        private static float LaneX(int l) => (l - 1) * LaneWidth;

        // ---------- Track space to world ----------

        // The course is laid out in the runner's own space; its transform puts it in the world (a road starts at a platform edge).
        private Vector3 Center(float zf) => transform.TransformPoint(LocalCenter(zf));
        private Quaternion Rotation(float zf) => transform.rotation * LocalRotation(zf);

        private Vector3 LocalCenter(float zf)
        {
            if (zf <= 0f) return centers[0] + new Vector3(0f, 0f, zf);
            int last = centers.Length - 1;
            if (zf >= last) return centers[last] + Quaternion.Euler(0f, headings[last] * Mathf.Rad2Deg, 0f) * Vector3.forward * (zf - last);
            int r = (int)zf;
            return Vector3.Lerp(centers[r], centers[r + 1], zf - r);
        }

        private Quaternion LocalRotation(float zf)
        {
            int last = headings.Length - 1;
            float c = Mathf.Clamp(zf, 0f, last);
            int r = Mathf.Min((int)c, last - 1);
            float t = c - r;
            float heading = Mathf.Lerp(headings[r], headings[r + 1], t) * Mathf.Rad2Deg;
            float slope = Mathf.Lerp(slopes[r], slopes[r + 1], t);
            return Quaternion.Euler(-slope, heading, 0f);
        }

        /// <summary>A point in track space (lane offset, height, distance) on the bent course.</summary>
        private Vector3 World(float lx, float ly, float zf) => Center(zf) + Rotation(zf) * new Vector3(lx, ly, 0f);

        // ---------- Building ----------

        private void BuildRow(int r)
        {
            if (roadMode && r > length) { BuildLanding(r); return; }
            var root = new GameObject("Row " + r).transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(Center(r), Rotation(r));

            for (int l = 0; l < Lanes; l++)
            {
                float lx = LaneX(l);
                if (!floorPlan[r, l])
                {
                    // Surf: a hole in the channel is a whirlpool.
                    if (kind == Kind.Surf)
                    {
                        Shapes.Primitive(PrimitiveType.Cylinder, "Whirl", root, new Vector3(lx, -0.06f, 0f), new Vector3(LaneWidth * 0.9f, 0.01f, 0.95f), poolMat);
                        Shapes.Primitive(PrimitiveType.Cylinder, "Eye", root, new Vector3(lx, -0.05f, 0f), new Vector3(0.3f, 0.012f, 0.3f), foamMat);
                    }
                    continue;
                }
                Shapes.Rounded("Frame", root, new Vector3(lx, -0.03f, 0f), new Vector3(LaneWidth * 0.93f, 0.1f, 1.0f), 0.045f, frameMat);
                Shapes.Rounded("Top", root, new Vector3(lx, 0f, 0f), new Vector3(LaneWidth * 0.78f, 0.1f, 0.8f), 0.045f, topMat);
                Shapes.Rounded("Slab", root, new Vector3(lx, -0.24f, 0f), new Vector3(LaneWidth * 1.02f, 0.36f, 1.12f), 0.05f, slabMat);
                if (kind == Kind.Mine)
                    foreach (float rx in new[] { -0.28f, 0.28f })
                        Shapes.Rounded("Rail", root, new Vector3(lx + rx, 0.07f, 0f), new Vector3(0.05f, 0.04f, 1.08f), 0.012f, railMat);
                if (kind == Kind.Surf && (r + l) % 4 == 0)
                    Shapes.Rounded("Foam", root, new Vector3(lx - 0.15f, 0.06f, 0.1f), new Vector3(0.36f, 0.01f, 0.05f), 0.01f, foamMat);
                if (ramps.Contains((l, r)))
                {
                    var wedge = Shapes.Rounded("Wave", root, new Vector3(lx, 0.12f, 0.1f), new Vector3(LaneWidth * 0.85f, 0.08f, 0.6f), 0.04f, foamMat);
                    wedge.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
                }
            }

            if (IsThemed) BuildThemeRow(root, r);
            else
            {
            // Walls with a glowing strip; a glowing pylon on each side every few rows for a sense of speed.
            // Nothing spans the course overhead except the bars you slide under, which stay below the camera.
            foreach (float side in new[] { -1f, 1f })
            {
                Shapes.Rounded("Wall", root, new Vector3(side * WallX, 0.35f, 0f), new Vector3(0.32f, 1.3f, 1.18f), 0.06f, slabMat);
                Shapes.Rounded("Strip", root, new Vector3(side * (WallX - 0.17f), 0.1f, 0f), new Vector3(0.04f, 0.07f, 1.18f), 0.015f, wallGlowMat);
            }
            if (curveRows.Contains(r) && r % 3 == 0)
            {
                // Chevrons on the outside of a bend point the way round.
                float dir = Mathf.Sign(headings[Mathf.Min(r + 1, headings.Length - 1)] - headings[r]);
                Shapes.Rounded("Chevron", root, new Vector3(-dir * (WallX + 0.05f), 1.15f, 0f), new Vector3(0.08f, 0.5f, 0.6f), 0.04f, chevronMat);
                foreach (float s in new[] { -1f, 1f })
                    Shapes.Rounded("Arrow", root, new Vector3(-dir * (WallX - 0.02f), 1.15f + s * 0.1f, 0f), new Vector3(0.04f, 0.06f, 0.36f), 0.02f, stripeMat)
                        .transform.localRotation = Quaternion.Euler(0f, -dir * s * 35f, 0f);
            }
            else if (r % 6 == 0 && kind != Kind.Surf)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Shapes.Rounded("Post", root, new Vector3(side * (WallX + 0.05f), 1.25f, 0f), new Vector3(0.2f, 0.9f, 0.2f), 0.05f, archMat);
                    Shapes.Rounded("Cap", root, new Vector3(side * (WallX + 0.05f), 1.78f, 0f), new Vector3(0.26f, 0.16f, 0.26f), 0.07f, wallGlowMat);
                }
            }
            else if (kind == Kind.Surf && r % 9 == 0)
            {
                // Palm trees on the banks.
                foreach (float side in new[] { -1f, 1f })
                {
                    var trunk = Shapes.Rounded("Palm", root, new Vector3(side * (WallX + 0.3f), 1.2f, 0f), new Vector3(0.14f, 1.8f, 0.14f), 0.05f, archMat).transform;
                    trunk.localRotation = Quaternion.Euler(0f, 0f, side * -8f);
                    for (int k = 0; k < 4; k++)
                        Shapes.Rounded("Leaf", root, new Vector3(side * (WallX + 0.15f), 2.1f, 0f), new Vector3(0.7f, 0.05f, 0.2f), 0.03f, wallGlowMat)
                            .transform.localRotation = Quaternion.Euler(0f, k * 45f, side * 15f - 10f);
                }
            }
            }
            rows.Enqueue((r, root));

            foreach (var (k, ol, orow) in obstaclePlan)
                if (orow == r) CreateObstacle(k, ol, orow);
            foreach (var (cl, cz, cy) in coinPlan)
                if (Mathf.RoundToInt(cz) == r) CreateCoin(cl, cz, cy);
        }

        private Material landingFrame, landingTop, landingGlow;

        /// <summary>The end of a road: a wide landing in the next floor's colours under a glowing arch.</summary>
        private void BuildLanding(int r)
        {
            var theme = WorldTheme.ForWorld(nextWorld);
            if (landingFrame == null)
            {
                landingFrame = MaterialFactory.Create(theme.tileTop, theme.tileGlow * 0.55f);
                landingTop = MaterialFactory.Create(theme.tileTop * 0.95f, theme.tileSelfLight * 0.5f);
                landingGlow = MaterialFactory.Create(theme.accent, theme.accent * 1.6f);
            }
            var root = new GameObject("Landing " + r).transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(Center(r), Rotation(r));
            for (int i = -3; i <= 3; i++)
            {
                Shapes.Rounded("Frame", root, new Vector3(i * 1.0f, -0.03f, 0f), new Vector3(0.94f, 0.1f, 0.94f), 0.045f, landingFrame);
                Shapes.Rounded("Top", root, new Vector3(i * 1.0f, 0f, 0f), new Vector3(0.78f, 0.1f, 0.78f), 0.045f, landingTop);
                Shapes.Rounded("Slab", root, new Vector3(i * 1.0f, -0.3f, 0f), new Vector3(1.02f, 0.5f, 1.02f), 0.05f, MaterialFactory.Create(theme.slab, Color.black));
            }
            if (r == Mathf.CeilToInt(length) + 2)
            {
                foreach (float side in new[] { -1f, 1f })
                    Shapes.Rounded("Post", root, new Vector3(side * 2.6f, 1.7f, 0f), new Vector3(0.22f, 3.4f, 0.22f), 0.06f, landingGlow);
                Shapes.Rounded("Top", root, new Vector3(0f, 3.45f, 0f), new Vector3(5.4f, 0.24f, 0.24f), 0.06f, landingGlow);
            }
            rows.Enqueue((r, root));
        }

        private void CreateObstacle(ObstacleKind k, int l, int r)
        {
            var go = new GameObject(k.ToString()).transform;
            go.SetParent(transform, false);
            var o = new Obstacle { kind = k, lane = l, row = r, go = go, phase = r * 0.7f, landed = true };
            float w = LaneWidth * Lanes;
            if (IsThemed && ThemedObstacle(o, go, k, l, r, w))
            {
                obstacles.Add(o);
                PlaceObstacle(o);
                return;
            }
            switch (k)
            {
                case ObstacleKind.Drop:
                    if (kind == Kind.Surf)
                    {
                        // A bobbing buoy, floating in the lane from the start.
                        Shapes.Primitive(PrimitiveType.Sphere, "Buoy", go, Vector3.zero, new Vector3(0.75f, 0.7f, 0.75f), blockMat);
                        Shapes.Primitive(PrimitiveType.Cylinder, "Stripe", go, Vector3.zero, new Vector3(0.78f, 0.08f, 0.78f), foamMat);
                        break;
                    }
                    if (kind == Kind.Mine) Shapes.Rounded("Rock", go, Vector3.zero, new Vector3(0.88f, 0.8f, 0.86f), 0.3f, blockMat).transform.localRotation = Quaternion.Euler(12f, 30f, 8f);
                    else Shapes.Rounded("Cube", go, Vector3.zero, new Vector3(0.9f, 0.9f, 0.9f), 0.12f, blockMat);
                    o.y = 7f;
                    o.landed = false;
                    o.ringMaterial = MaterialFactory.CreateTransparent(new Color(1f, 0.3f, 0.35f, 0.5f), Palette.TileWarningGlow * 0.5f);
                    o.ring = Shapes.Primitive(PrimitiveType.Cylinder, "Warning", transform, Vector3.zero, new Vector3(0.85f, 0.004f, 0.85f), o.ringMaterial).transform;
                    o.ring.SetPositionAndRotation(World(LaneX(l), 0.07f, r), Rotation(r));
                    go.gameObject.SetActive(false); // appears only when it starts to drop; until then only the warning shows
                    break;
                case ObstacleKind.Hurdle:
                    // Low striped boards on short legs across every lane.
                    for (int i = 0; i < 6; i++)
                        Shapes.Rounded("Board", go, new Vector3(-w * 0.5f + (i + 0.5f) * w / 6f, 0.32f, 0f), new Vector3(w / 6f - 0.02f, 0.18f, 0.12f), 0.04f, i % 2 == 0 ? hurdleMat : stripeMat);
                    foreach (float s in new[] { -1f, 0f, 1f })
                        Shapes.Rounded("Leg", go, new Vector3(s * LaneWidth, 0.12f, 0f), new Vector3(0.08f, 0.24f, 0.08f), 0.03f, stripeMat);
                    break;
                case ObstacleKind.Bar:
                    // A glowing bar at head height between two posts: only a slide gets under it.
                    Shapes.Rounded("Bar", go, new Vector3(0f, 1.05f, 0f), new Vector3(w + 0.3f, 0.7f, 0.16f), 0.06f, barMat);
                    for (int i = 0; i < 5; i++)
                        Shapes.Rounded("Stripe", go, new Vector3(-w * 0.4f + i * w * 0.2f, 1.05f, -0.09f), new Vector3(0.14f, 0.5f, 0.02f), 0.02f, stripeMat).transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Post", go, new Vector3(s * (w * 0.5f + 0.2f), 0.7f, 0f), new Vector3(0.14f, 1.4f, 0.14f), 0.04f, stripeMat);
                    break;
                case ObstacleKind.Mover:
                    Shapes.Rounded("Cube", go, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f), 0.12f, blockMat);
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Arrow", go, new Vector3(s * 0.5f, 0.45f, 0f), new Vector3(0.12f, 0.3f, 0.3f), 0.05f, barMat);
                    break;
                case ObstacleKind.Pad:
                    Shapes.Rounded("Pad", go, new Vector3(0f, 0.05f, 0f), new Vector3(LaneWidth * 0.8f, 0.1f, 0.8f), 0.04f, padMat);
                    for (int i = 0; i < 3; i++)
                        Shapes.Rounded("Chevron", go, new Vector3(0f, 0.12f, -0.25f + i * 0.25f), new Vector3(0.5f, 0.03f, 0.08f), 0.015f, stripeMat);
                    break;
                case ObstacleKind.Magnet:
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Shapes.Rounded("Arm", go, new Vector3(s * 0.15f, 0.55f, 0f), new Vector3(0.12f, 0.36f, 0.12f), 0.05f, magnetMat);
                        Shapes.Rounded("Tip", go, new Vector3(s * 0.15f, 0.36f, 0f), new Vector3(0.13f, 0.1f, 0.13f), 0.04f, MaterialFactory.Create(Color.white, new Color(0.8f, 0.8f, 0.9f)));
                    }
                    Shapes.Rounded("Bow", go, new Vector3(0f, 0.74f, 0f), new Vector3(0.42f, 0.12f, 0.12f), 0.05f, magnetMat);
                    break;
                case ObstacleKind.Shield:
                    Shapes.Primitive(PrimitiveType.Sphere, "Orb", go, new Vector3(0f, 0.55f, 0f), Vector3.one * 0.45f, shieldMat);
                    break;
            }
            obstacles.Add(o);
            PlaceObstacle(o);
        }

        private void PlaceObstacle(Obstacle o)
        {
            float ox = o.kind == ObstacleKind.Hurdle || o.kind == ObstacleKind.Bar ? 0f : o.X(time);
            float oy = 0f;
            if (o.kind == ObstacleKind.Drop) oy = kind == Kind.Surf ? 0.4f + Mathf.Sin(time * 2f + o.phase) * 0.05f : 0.5f + o.y;
            if (o.kind == ObstacleKind.Magnet || o.kind == ObstacleKind.Shield) oy = Mathf.Sin(time * 3f + o.phase) * 0.08f;
            o.go.SetPositionAndRotation(World(ox, oy, o.row), Rotation(o.row));
            if (o.kind == ObstacleKind.Magnet || o.kind == ObstacleKind.Shield) o.go.Rotate(0f, time * 120f, 0f, Space.Self);
        }

        private void CreateCoin(int l, float cz, float cy)
        {
            var go = new GameObject("Coin").transform;
            go.SetParent(transform, false);
            go.position = World(LaneX(l), cy, cz);
            Shapes.Primitive(PrimitiveType.Cylinder, "Rim", go, Vector3.zero, new Vector3(0.42f, 0.03f, 0.42f), rimMat);
            Shapes.Primitive(PrimitiveType.Cylinder, "Face", go, Vector3.zero, new Vector3(0.33f, 0.04f, 0.33f), coinMat);
            coins.Add(new Coin { lane = l, z = cz, y = cy, go = go });
        }

        /// <summary>
        /// The end of the course: a green gate over the left two lanes (safe, +10) and a red one over the right
        /// lane, leading into a short, hard extra stretch (+30) that ends in the glowing exit ring.
        /// </summary>
        private void BuildGate()
        {
            gate = new GameObject("Gates").transform;
            gate.SetParent(transform, false);
            gate.SetPositionAndRotation(Center(GateRow), Rotation(GateRow));
            Arch(gate, (LaneX(0) + LaneX(1)) * 0.5f, LaneWidth * 2.1f, new Color(0.4f, 1f, 0.6f), new Color(0.4f, 2f, 0.7f));
            Arch(gate, LaneX(2), LaneWidth * 1.05f, new Color(1f, 0.4f, 0.45f), new Color(2.2f, 0.35f, 0.4f));

            var ring = new GameObject("ExitRing").transform;
            ring.SetParent(gate, false);
            ring.localPosition = new Vector3(0f, 1.3f, RiskLength);
            var glow = MaterialFactory.Create(new Color(0.6f, 1f, 1f), new Color(0.6f, 1.9f, 2.4f));
            const int pieces = 20;
            for (int i = 0; i < pieces; i++)
            {
                float a = i * Mathf.PI * 2f / pieces;
                var piece = Shapes.Rounded("Ring", ring, new Vector3(Mathf.Cos(a) * 1.7f, Mathf.Sin(a) * 1.7f, 0f), new Vector3(0.5f, 0.24f, 0.24f), 0.08f, glow);
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
            }
            exitRing = ring;
        }

        private static void Arch(Transform parent, float x, float width, Color color, Color glow)
        {
            var m = MaterialFactory.Create(color, glow);
            foreach (float side in new[] { -0.5f, 0.5f })
                Shapes.Rounded("Post", parent, new Vector3(x + side * width, 1.6f, 0f), new Vector3(0.16f, 3.2f, 0.16f), 0.05f, m);
            Shapes.Rounded("Top", parent, new Vector3(x, 3.25f, 0f), new Vector3(width + 0.16f, 0.2f, 0.16f), 0.05f, m);
            Shapes.Rounded("Panel", parent, new Vector3(x, 2.95f, 0f), new Vector3(width * 0.6f, 0.36f, 0.06f), 0.05f, m);
        }

        /// <summary>What the robot rides: an orange-lit cell for surfing, a cart in the mine; plus its shield bubble.</summary>
        private void BuildRide()
        {
            if (kind == Kind.Surf)
            {
                ride = new GameObject("Board").transform;
                ride.SetParent(robot.transform, false);
                var glow = MaterialFactory.Create(new Color(1f, 0.45f, 0.1f), new Color(1.45f, 0.3f, 0.02f));
                var top = MaterialFactory.Create(Palette.TileTop, Palette.TileSelfLight);
                Shapes.Rounded("Frame", ride, new Vector3(0f, 0.02f, 0f), new Vector3(0.62f, 0.07f, 0.8f), 0.03f, glow);
                Shapes.Rounded("Top", ride, new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.05f, 0.66f), 0.02f, top);
            }
            else if (kind == Kind.Mine)
            {
                ride = new GameObject("Cart").transform;
                ride.SetParent(robot.transform, false);
                var wood = MaterialFactory.Create(new Color(0.55f, 0.35f, 0.22f), Color.black);
                var metal = MaterialFactory.Create(new Color(0.35f, 0.33f, 0.42f), Color.black);
                Shapes.Rounded("Tub", ride, new Vector3(0f, 0.18f, 0f), new Vector3(0.62f, 0.26f, 0.62f), 0.06f, wood);
                Shapes.Rounded("Rim", ride, new Vector3(0f, 0.32f, 0f), new Vector3(0.66f, 0.04f, 0.66f), 0.02f, metal);
                foreach (var w in new[] { new Vector2(-0.22f, -0.22f), new Vector2(0.22f, -0.22f), new Vector2(-0.22f, 0.22f), new Vector2(0.22f, 0.22f) })
                    Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", ride, new Vector3(w.x, 0.07f, w.y), new Vector3(0.14f, 0.03f, 0.14f), metal)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            bubble = Shapes.Primitive(PrimitiveType.Sphere, "ShieldBubble", robot.transform, new Vector3(0f, 0.42f, 0f), Vector3.one * 1.05f,
                MaterialFactory.CreateTransparent(Palette.ShieldBubble, Palette.ShieldGlow)).transform;
            bubble.gameObject.SetActive(false);
        }

        // ---------- Play ----------

        private void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // paused
            time += dt;

            // A short grace moment after stepping off the edge still allows the jump (coyote time).
            if (!ended && !crashed && (!falling || y > CoyoteDepth)) HandleInput();
            Move(dt);
            UpdateRows();
            UpdateObstacles(dt);
            UpdateCoins();
            PoseRobot(dt);
            UpdateCamera(dt);

            if (exitRing != null) exitRing.localRotation = Quaternion.Euler(0f, 0f, time * 40f);
            UpdateThemeFx(dt);
        }

        private void HandleInput()
        {
            var cmd = input.Poll(robot.transform.position);
            if (startTimer < startDelay) return;
            if (cmd.jump) Jump();
            if (!cmd.move.HasValue) return;
            switch (cmd.move.Value)
            {
                case Direction.PlusX: ChangeLane(1); break;
                case Direction.MinusX: ChangeLane(-1); break;
                case Direction.PlusY: Jump(); break;
                case Direction.MinusY:
                    if (!grounded) vy = Mathf.Min(vy, -9f); // slam down, then slide on landing
                    slideLeft = SlideTime;
                    if (grounded) AudioManager.PlaySfx(Sfx.Hop, 0.5f, 0.6f);
                    break;
            }
        }

        private void ChangeLane(int dir)
        {
            int target = Mathf.Clamp(lane + dir, 0, Lanes - 1);
            if (target == lane)
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.4f);
                return;
            }
            lane = target;
            AudioManager.PlaySfx(Sfx.Hop, 0.35f, 1.3f, 0.05f);
            Haptics.Light();
        }

        private void Jump()
        {
            if (!grounded && !(falling && y > CoyoteDepth)) return;
            Launch(JumpVelocity);
            AudioManager.PlaySfx(Sfx.Hop, 0.7f, 0.8f);
            Haptics.Light();
        }

        private void Launch(float velocity)
        {
            grounded = false;
            falling = false;
            slideLeft = 0f;
            holdTime = 0f;
            y = Mathf.Max(y, 0f);
            vy = velocity;
            squash = -0.25f;
        }

        private void Move(float dt)
        {
            startTimer += dt;
            float speed = startTimer < startDelay ? 0f : Mathf.Lerp(speedStart, speedEnd, Progress);
            if (crashed) speed = 0f;
            if (ended && escaped) speed *= Mathf.Clamp01(1f - endTimer * 0.6f);
            if (falling) speed *= 0.6f;

            // Holding on after the jump: lighter gravity for a moment, and a little extra push forward.
            bool held = !grounded && !falling && vy > 0f && holdTime < MaxHold && InputReader.PointerHeld;
            if (held) speed *= 1.1f;
            z += speed * dt;
            runPhase += speed * dt * 2.2f;
            x = Mathf.MoveTowards(x, LaneX(lane), LaneSpeed * LaneScale * dt);
            if (grounded) slideLeft = Mathf.Max(0f, slideLeft - dt);

            int row = Mathf.RoundToInt(z);
            int under = Mathf.Clamp(Mathf.RoundToInt(x / LaneWidth) + 1, 0, Lanes - 1);
            bool floor = row < 0 || row >= totalRows || floorPlan[row, under];

            if (grounded && !floor && !crashed)
            {
                grounded = false;
                falling = true;
                vy = 0f;
            }
            if (!grounded)
            {
                float g = Gravity * GravityScale;
                if (held) { g *= HoldGravity; holdTime += dt; }
                else if (vy > 0f) holdTime = MaxHold; // let go: no second boost in the same jump
                vy -= g * dt;
                y += vy * dt;
                if (y <= 0f && !falling)
                {
                    if (floor)
                    {
                        y = 0f;
                        vy = 0f;
                        grounded = true;
                        squash = 0.3f;
                        fx.Dust(World(x, 0.08f, z), Palette.TileTop, 6, 1.2f);
                    }
                    else falling = true;
                }
            }

            if (falling && !ended && y < FallDepth)
            {
                if (shielded || TestInvulnerable) SaveFromFall();
                else if (roadMode) { Stumble(); SaveFromFall(); }
                else
                {
                    AudioManager.PlaySfx(Sfx.Fall);
                    Haptics.Death();
                    End(false, 0);
                }
            }

            // Surf: waves launch the board over the whirlpools.
            if (grounded && !ended && ramps.Contains((under, row))) { Launch(JumpVelocity * 1.2f); AudioManager.PlaySfx(Sfx.Hop, 0.7f, 1.1f); }

            if (falling && y < -8f) visual.gameObject.SetActive(false);

            // A road ends on the next floor's landing.
            if (roadMode && !ended && z >= length + 4f) Arrive();
            invulnerable = Mathf.Max(0f, invulnerable - dt);

            // The gates: the left two lanes are the safe exit, the right lane takes the risky route.
            if (!roadMode && !ended && !risky && z >= GateRow)
            {
                if (lane == 2)
                {
                    risky = true;
                    RiskTaken?.Invoke();
                    AudioManager.PlaySfx(Sfx.Warning, 0.9f, 1.2f);
                    rig.Punch(0.6f);
                }
                else Escape(SafeBonus);
            }
            if (!roadMode && !ended && risky && z >= GateRow + RiskLength) Escape(RiskBonus);
            if (ended) endTimer += dt;
            squash = Mathf.MoveTowards(squash, 0f, dt * 2f);
            magnetLeft = Mathf.Max(0f, magnetLeft - dt);
        }

        /// <summary>The shield pops instead of the robot falling: it bounces back up and over the gap.</summary>
        private void SaveFromFall()
        {
            UseShield();
            int laneUnder = Mathf.Clamp(Mathf.RoundToInt(x / LaneWidth) + 1, 0, Lanes - 1);
            while (z < totalRows - 1 && !floorPlan[Mathf.Clamp(Mathf.RoundToInt(z), 0, totalRows - 1), laneUnder]) z += 1f;
            y = 0f;
            Launch(JumpVelocity);
            visual.gameObject.SetActive(true);
        }

        private void UseShield()
        {
            if (!shielded) return;
            shielded = false;
            bubble.gameObject.SetActive(false);
            var at = robot.transform.position + Vector3.up * 0.4f;
            fx.Burst(at, Palette.ShieldPickup, Palette.ShieldPickupGlow, 30, 5f);
            AudioManager.PlaySfx(Sfx.Shield, 1f, 0.9f);
            Haptics.Medium();
            rig.Shake(0.6f);
            Notice?.Invoke("float.shieldSaved", at);
        }

        /// <summary>A bump on a road: a few coins lost and a moment of blinking safety.</summary>
        private void Stumble()
        {
            if (invulnerable > 0f) return;
            invulnerable = 1.4f;
            int lost = Mathf.Min(3, Coins);
            Coins -= lost;
            fx.Burst(robot.transform.position + Vector3.up * 0.4f, Palette.Coin, Palette.CoinGlow, 6 + lost * 4, 4f);
            AudioManager.PlaySfx(Sfx.Bump, 0.9f, 0.8f);
            Haptics.Medium();
            rig.Shake(0.7f);
        }

        /// <summary>The robot steps onto the next floor's landing: a happy spin, then the game shows the result.</summary>
        private void Arrive()
        {
            ended = true;
            escaped = true;
            fx.Burst(robot.transform.position + Vector3.up, WorldTheme.ForWorld(nextWorld).accent, Palette.ShieldPickupGlow, 40, 6f);
            AudioManager.PlaySfx(Sfx.Win);
            Haptics.Medium();
            rig.Punch(0.8f);
            Arrived?.Invoke();
        }

        private void Escape(int bonus)
        {
            escaped = true;
            fx.Burst(robot.transform.position + Vector3.up, Palette.UiCyan, Palette.ShieldPickupGlow, 40, 6f);
            AudioManager.PlaySfx(Sfx.Win);
            Haptics.Medium();
            rig.Punch(1f);
            End(true, bonus);
        }

        private void End(bool reachedExit, int bonus)
        {
            ended = true;
            Finished?.Invoke(reachedExit, bonus);
        }

        private void UpdateRows()
        {
            while (builtRow < Mathf.Min(totalRows, z + BuildAhead)) BuildRow(builtRow++);
            while (rows.Count > 0 && rows.Peek().row < z - 8f) Destroy(rows.Dequeue().root.gameObject);

            // The far rows rise into place, so the course seems to assemble itself ahead of the robot.
            float riseStart = z + BuildAhead - 10f;
            foreach (var (r, root) in rows)
            {
                float k = Mathf.Clamp01((r - riseStart) / 10f);
                root.position = Center(r) + Vector3.down * (k * k * 4f);
            }
        }

        private void UpdateObstacles(float dt)
        {
            bool sliding = slideLeft > 0f && grounded;
            float robotTop = y + (sliding ? 0.42f : 0.8f);
            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var o = obstacles[i];
                if (o.row < z - 8f || o.done)
                {
                    Destroy(o.go.gameObject);
                    if (o.ring != null) Destroy(o.ring.gameObject);
                    obstacles.RemoveAt(i);
                    continue;
                }

                if (o.kind == ObstacleKind.Drop && !o.landed && o.row - z < BlockDropDistance)
                {
                    o.go.gameObject.SetActive(true);
                    o.vy += 30f * dt;
                    o.y -= o.vy * dt;
                    if (o.y <= 0f)
                    {
                        o.y = 0f;
                        o.landed = true;
                        o.ring.gameObject.SetActive(false);
                        fx.Dust(World(LaneX(o.lane), 0.1f, o.row), Palette.Block, 10, 2f);
                        AudioManager.PlaySfx(Sfx.Impact, 0.5f);
                        rig.Shake(0.25f);
                    }
                }
                if (o.kind == ObstacleKind.Drop && !o.landed)
                {
                    float a = 0.35f + 0.25f * Mathf.Sin(time * 12f);
                    MaterialFactory.SetColors(o.ringMaterial, new Color(1f, 0.3f, 0.35f, a), Palette.TileWarningGlow * a);
                }
                PlaceObstacle(o);

                if (ended || crashed) continue;
                float dz = Mathf.Abs(o.row - z);
                float dx = Mathf.Abs(o.X(time) - x);
                switch (o.kind)
                {
                    case ObstacleKind.Drop:
                    case ObstacleKind.Mover:
                        if (dz < 0.6f && dx < 0.62f && y < o.y + 0.85f && o.y < 0.9f) Hit(o);
                        break;
                    case ObstacleKind.Hurdle:
                        if (dz < 0.4f && y < 0.48f) Hit(o);
                        break;
                    case ObstacleKind.Bar:
                        if (dz < 0.4f && robotTop > 0.7f && y < 1.4f) Hit(o);
                        break;
                    case ObstacleKind.Pad:
                        if (dz < 0.5f && dx < 0.55f && y < 0.2f && vy <= 0.01f)
                        {
                            Launch(PadVelocity);
                            fovKick = 1f;
                            holdTime = MaxHold;
                            fx.Burst(World(o.X(time), 0.2f, o.row), new Color(0.4f, 1f, 0.6f), new Color(0.4f, 2.2f, 0.8f), 24, 5f);
                            AudioManager.PlaySfx(Sfx.Shield, 0.9f, 1.4f);
                            Haptics.Medium();
                            rig.Punch(0.5f);
                        }
                        break;
                    case ObstacleKind.Magnet:
                    case ObstacleKind.Shield:
                        if (dz < 0.7f && dx < 0.7f && y < 1.3f)
                        {
                            var at = o.go.position + Vector3.up * 0.5f;
                            fx.Burst(at, o.kind == ObstacleKind.Magnet ? new Color(1f, 0.4f, 0.45f) : Palette.ShieldPickup, Palette.ShieldPickupGlow, 20, 4f);
                            AudioManager.PlaySfx(Sfx.Shield, 0.9f, 1.2f);
                            Haptics.Medium();
                            if (o.kind == ObstacleKind.Magnet) { magnetLeft = MagnetTime; Notice?.Invoke("float.magnet", at); }
                            else { shielded = true; bubble.gameObject.SetActive(true); Notice?.Invoke("float.shield", at); }
                            o.done = true;
                        }
                        break;
                }
            }
        }

        /// <summary>The robot ran into something: the shield takes it (the obstacle bursts), otherwise the run ends.</summary>
        private void Hit(Obstacle o)
        {
            if (TestInvulnerable) return;
            if (roadMode)
            {
                // Roads cannot be lost: the block bursts, a few coins go flying, and the robot runs on.
                if (invulnerable > 0f) return;
                fx.Burst(o.go.position + Vector3.up * 0.4f, Palette.Block, Palette.BlockGlow, 16, 4f);
                o.done = true;
                Stumble();
                return;
            }
            if (shielded)
            {
                UseShield();
                fx.Burst(o.go.position + Vector3.up * 0.4f, Palette.Block, Palette.BlockGlow, 16, 4f);
                o.done = true;
                return;
            }
            crashed = true;
            squash = 0f;
            fx.Burst(robot.transform.position + Vector3.up * 0.3f, Palette.RobotBody, Palette.RobotEye, 24, 5f);
            AudioManager.PlaySfx(Sfx.Squash);
            Haptics.Death();
            rig.Shake(1.2f);
            End(false, 0);
        }

        private void UpdateCoins()
        {
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                var c = coins[i];
                if (c.z < z - 8f)
                {
                    Destroy(c.go.gameObject);
                    coins.RemoveAt(i);
                    continue;
                }
                c.go.rotation = Rotation(c.z) * Quaternion.Euler(0f, time * 200f + c.z * 20f, 0f) * Quaternion.Euler(90f, 0f, 0f);
                if (ended || crashed) continue;
                bool mine = Mathf.Abs(c.z - z) < 0.6f && Mathf.Abs(LaneX(c.lane) - x) < 0.6f && Mathf.Abs(c.y - (y + 0.45f)) < 0.6f;
                // The magnet pulls in every coin just ahead, whatever its lane or height.
                bool pulled = magnetLeft > 0f && c.z - z < 2.5f && c.z - z > -0.5f;
                if (!mine && !pulled) continue;
                Coins++;
                var at = c.go.position;
                fx.Burst(at, Palette.Coin, Palette.CoinGlow, 8, 3f);
                AudioManager.PlaySfx(Sfx.Coin, 0.7f, 1f, 0.04f);
                Haptics.Pulse(18, 0.4f);
                CoinCollected?.Invoke(at);
                Destroy(c.go.gameObject);
                coins.RemoveAt(i);
            }
        }

        // ---------- Robot and camera ----------

        private void PoseRobot(float dt)
        {
            robot.transform.SetPositionAndRotation(World(x, y, z), Rotation(z));

            if (crashed)
            {
                visual.localScale = Vector3.Lerp(visual.localScale, new Vector3(1.5f, 0.12f, 1.5f), dt * 14f);
                return;
            }

            // Lean into lane changes and forward while sprinting; squash on landing, stretch on take-off; low when sliding.
            float lean = (LaneX(lane) - x) * 18f;
            float forward = startTimer < startDelay ? 0f : 12f;
            bool sliding = slideLeft > 0f && grounded;
            visual.localRotation = Quaternion.Euler(sliding ? -25f : forward, lean * 0.6f, -lean);
            float s = 1f + squash;
            float sx = 1f / Mathf.Sqrt(Mathf.Max(0.3f, s));
            float sy = Mathf.Max(0.3f, 1f - squash * 0.6f) * (sliding ? 0.5f : 1f);
            var targetScale = new Vector3(sx * (sliding ? 1.15f : 1f), sy, sx);
            visual.localScale = dt >= 1f ? targetScale : Vector3.Lerp(visual.localScale, targetScale, Mathf.Clamp01(dt * 18f));
            if (ended && escaped) visual.localRotation = Quaternion.Euler(0f, endTimer * 540f, 0f);
            // Blinking while a road bump's safety lasts.
            if (invulnerable > 0f) visual.gameObject.SetActive(Mathf.Repeat(time, 0.2f) > 0.08f);
            else if (!visual.gameObject.activeSelf && !falling) visual.gameObject.SetActive(true);
            if (bubble != null && bubble.gameObject.activeSelf) bubble.localScale = Vector3.one * (1.05f + Mathf.Sin(time * 6f) * 0.04f);

            // Run cycle: legs pump and arms swing (tucked while in the air, back while sliding).
            bool running = grounded && startTimer >= startDelay && !ended && !sliding;
            float swing = running ? Mathf.Sin(runPhase * Mathf.PI) : 0f;
            if (legL != null)
            {
                legL.localRotation = Quaternion.Euler(swing * 40f, 0f, 0f);
                legR.localRotation = Quaternion.Euler(-swing * 40f, 0f, 0f);
            }
            if (armL != null)
            {
                float air = grounded ? (sliding ? 60f : 0f) : -70f;
                armL.localRotation = Quaternion.Euler(-swing * 45f + air, 0f, 0f);
                armR.localRotation = Quaternion.Euler(swing * 45f + air, 0f, 0f);
            }
            visual.localPosition = new Vector3(0f, running ? Mathf.Abs(Mathf.Sin(runPhase * Mathf.PI)) * 0.06f : 0f, 0f);

            if (shadow != null)
            {
                int row = Mathf.RoundToInt(z);
                int under = Mathf.Clamp(Mathf.RoundToInt(x / LaneWidth) + 1, 0, Lanes - 1);
                bool floor = row < 0 || row >= totalRows || floorPlan[row, under];
                shadow.gameObject.SetActive(floor && !falling);
                shadow.SetPositionAndRotation(World(x, GridView.SurfaceY + 0.004f, z), Rotation(z));
                float k = 0.48f * (1f - Mathf.Clamp01(y) * 0.4f);
                shadow.localScale = new Vector3(k, 0.004f, k);
            }
        }

        private void UpdateCamera(float dt)
        {
            // Behind and above the robot along the course, so the camera swings round the bends with it.
            float lift = 2.1f + Mathf.Max(y, -1f) * 0.35f;
            if (falling) lift = Mathf.Max(1.4f, lift);
            var target = World(x * 0.55f, lift, z - 3.7f);
            camPos = dt >= 1f ? target : Vector3.Lerp(camPos, target, 1f - Mathf.Exp(-dt * 8f));
            var look = World(x * 0.75f, 0.55f + Mathf.Max(y, -0.5f) * 0.3f, z + 4.5f);
            var rot = Quaternion.LookRotation(look - camPos);
            camRot = dt >= 1f ? rot : Quaternion.Slerp(camRot, rot, 1f - Mathf.Exp(-dt * 10f));
            if (blend < 1f)
            {
                // The hand-over from the platform view: position, angle and lens glide together.
                if (dt < 1f) blend = Mathf.Min(1f, blend + dt / 1.1f);
                float k = blend * blend * (3f - 2f * blend);
                float fov = Mathf.Exp(Mathf.Lerp(Mathf.Log(blendFov), Mathf.Log(CameraFov), k));
                rig.Chase(Vector3.Lerp(blendFrom.position, camPos, k), Quaternion.Slerp(blendFrom.rotation, camRot, k), fov);
                return;
            }
            rig.Chase(camPos, camRot, CameraFov);
        }
    }
}
