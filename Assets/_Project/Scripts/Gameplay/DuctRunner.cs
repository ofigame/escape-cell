using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Bonus mode "escape tunnel": the robot sprints down an air duct seen from behind (third person).
    /// Swipe left / right to change lanes, tap or swipe up to jump over holes, dodge the blocks that drop
    /// ahead, grab coins, and reach the exit vent at the end. Built from code like the rest of the game:
    /// the whole course is planned up front (seeded), and rows are built a little ahead and removed behind.
    /// </summary>
    public class DuctRunner : MonoBehaviour
    {
        public const float TrackLength = 190f;
        private const int Lanes = 3;
        private const float LaneWidth = 1.1f;
        private const float SpeedStart = 5.2f;
        private const float SpeedEnd = 8.2f;
        private const float JumpVelocity = 6.2f;
        private const float Gravity = 17f;
        private const float LaneSpeed = 10f;
        private const int BuildAhead = 42;
        private const float StartDelay = 1.3f;
        private const float BlockDropDistance = 9f;
        private const float CoyoteDepth = -0.3f;
        private const float FallDepth = -0.6f;
        private const float WallX = 1.5f * LaneWidth + 0.3f;

        /// <summary>A coin was grabbed at this world position.</summary>
        public event Action<Vector3> CoinCollected;
        /// <summary>The run ended: whether the robot got out, and the exit bonus it earned (safe gate +10, risky route +30).</summary>
        public event Action<bool, int> Finished;
        /// <summary>The robot took the risky gate (for a heads-up).</summary>
        public event Action RiskTaken;

        /// <summary>The flavours of bonus tunnel: same running and lanes, different world and rules.</summary>
        public enum Kind { Duct, Surf, Mine, Chase }

        public const int SafeBonus = 10, RiskBonus = 30;
        private const int RiskLength = 34;
        private Kind kind;
        private bool risky;
        private Transform ride, chaserRoot;
        private WardenBoss chaser;
        private float chaserGap = 3.2f, invulnerable, drainTimer;
        private readonly HashSet<(int, int)> ramps = new HashSet<(int, int)>();
        private int GateRow => Mathf.CeilToInt(TrackLength);

        /// <summary>The tunnel owns the robot, camera and input (from Begin until Stop).</summary>
        public bool Active { get; private set; }
        public int Coins { get; private set; }
        public float Progress => Mathf.Clamp01(z / TrackLength);
        /// <summary>The chase: how close WARDEN is (0 = right behind, 1 = far).</summary>
        public float ChaserDistance => Mathf.Clamp01(chaserGap / 3.2f);

        private Robot robot;
        private CameraRig rig;
        private InputReader input;
        private FxSystem fx;
        private Transform visual, shadow;
        private readonly List<(Transform t, Vector3 pos, Quaternion rot)> limbs = new List<(Transform, Vector3, Quaternion)>();
        private Transform legL, legR, armL, armR;

        private class Block
        {
            public int lane, row;
            public float y = 7f, vy;
            public bool landed;
            public Transform go, ring;
            public Material ringMaterial;
        }

        private class Coin
        {
            public int lane;
            public float z, y;
            public Transform go;
        }

        private bool[,] floorPlan;
        private int totalRows;
        private readonly List<(int lane, int row)> blockPlan = new List<(int, int)>();
        private readonly List<(int lane, float z, float y)> coinPlan = new List<(int, float, float)>();
        private readonly Queue<(int row, Transform root)> rows = new Queue<(int, Transform)>();
        private readonly List<Block> blocks = new List<Block>();
        private readonly List<Coin> coins = new List<Coin>();
        private int builtRow;
        private Transform gate;

        private Material frameMat, topMat, slabMat, wallGlowMat, archMat, blockMat, coinMat, rimMat;

        // Robot state
        private float x, y, z, vy;
        private int lane = 1;
        private bool grounded, falling, crashed, ended, escaped;
        private float startTimer, runPhase, time, endTimer, squash;
        private Vector3 camPos;

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
            Stop();
            Active = true;
            kind = tunnel;
            risky = false;
            chaserGap = 3.2f;
            invulnerable = 0f;
            Coins = 0;
            x = 0f; y = 0f; z = 0f; vy = 0f;
            lane = 1;
            grounded = true;
            falling = crashed = ended = escaped = false;
            startTimer = 0f;
            endTimer = 0f;
            squash = 0f;
            time = 0f;

            CreateMaterials();
            Plan(new System.Random(seed));
            builtRow = 0;
            while (builtRow < Mathf.Min(totalRows, BuildAhead)) BuildRow(builtRow++);
            BuildGate();

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
            input.ScreenMode = true;
            camPos = new Vector3(0f, 2.6f, -4.6f);
            PoseRobot(0f);
            UpdateCamera(1f);
        }

        /// <summary>Removes the tunnel and gives the robot, camera and input back to the platform game.</summary>
        public void Stop()
        {
            if (!Active) return;
            Active = false;
            while (rows.Count > 0) Destroy(rows.Dequeue().root.gameObject);
            foreach (var b in blocks) Destroy(b.go.gameObject);
            foreach (var c in coins) Destroy(c.go.gameObject);
            blocks.Clear();
            coins.Clear();
            if (gate != null) Destroy(gate.gameObject);
            if (ride != null) Destroy(ride.gameObject);
            if (chaserRoot != null) Destroy(chaserRoot.gameObject);
            ride = chaserRoot = null;
            chaser = null;
            ramps.Clear();

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
            }
            robot.enabled = true;
            rig.EndChase();
            input.ScreenMode = false;
        }

        private Material railMat, foamMat, poolMat;

        private void CreateMaterials()
        {
            frameMat = MaterialFactory.Create(Palette.TileTop, Palette.TileGlow * 0.55f);
            topMat = MaterialFactory.Create(Palette.TileTop * 0.92f, Palette.TileSelfLight * 0.5f);
            slabMat = MaterialFactory.Create(Color.Lerp(Palette.Slab, Palette.Pillar, 0.5f), Color.black);
            wallGlowMat = MaterialFactory.Create(Palette.Slab, Palette.SlabEdgeGlow);
            var accent = WorldTheme.Current.accent;
            archMat = MaterialFactory.Create(accent, accent * 0.9f);
            blockMat = MaterialFactory.Create(Palette.Block, Palette.BlockGlow * 0.6f);
            switch (kind)
            {
                case Kind.Surf:
                    // A water channel between sandy banks; buoys instead of blocks.
                    frameMat = MaterialFactory.Create(new Color(0.35f, 0.75f, 0.95f), new Color(0.2f, 0.7f, 1.1f));
                    topMat = MaterialFactory.Create(new Color(0.3f, 0.65f, 0.95f), new Color(0.05f, 0.25f, 0.45f));
                    slabMat = MaterialFactory.Create(new Color(0.95f, 0.85f, 0.62f), Color.black);
                    wallGlowMat = MaterialFactory.Create(new Color(0.4f, 0.8f, 0.5f), new Color(0.2f, 0.6f, 0.3f));
                    archMat = MaterialFactory.Create(new Color(1f, 0.6f, 0.3f), new Color(0.9f, 0.4f, 0.1f));
                    blockMat = MaterialFactory.Create(new Color(1f, 0.3f, 0.3f), new Color(0.6f, 0.1f, 0.1f));
                    foamMat = MaterialFactory.Create(Color.white, new Color(0.8f, 1f, 1.1f));
                    poolMat = MaterialFactory.Create(new Color(0.08f, 0.2f, 0.4f), new Color(0.05f, 0.25f, 0.6f));
                    break;
                case Kind.Mine:
                    // Plank tracks with rails, rock walls, timber beams with lamps, falling rocks.
                    frameMat = MaterialFactory.Create(new Color(0.45f, 0.3f, 0.2f), Color.black);
                    topMat = MaterialFactory.Create(new Color(0.62f, 0.44f, 0.28f), new Color(0.08f, 0.05f, 0.02f));
                    slabMat = MaterialFactory.Create(new Color(0.42f, 0.36f, 0.34f), Color.black);
                    wallGlowMat = MaterialFactory.Create(new Color(1f, 0.7f, 0.3f), new Color(1.8f, 0.9f, 0.3f));
                    archMat = MaterialFactory.Create(new Color(0.55f, 0.38f, 0.22f), Color.black);
                    blockMat = MaterialFactory.Create(new Color(0.5f, 0.42f, 0.38f), new Color(0.15f, 0.08f, 0.05f));
                    railMat = MaterialFactory.Create(new Color(0.75f, 0.75f, 0.82f), new Color(0.2f, 0.2f, 0.25f));
                    break;
                case Kind.Chase:
                    // WARDEN's red alert corridor.
                    archMat = MaterialFactory.Create(new Color(1f, 0.35f, 0.4f), new Color(1.8f, 0.3f, 0.35f));
                    wallGlowMat = MaterialFactory.Create(new Color(1f, 0.4f, 0.45f), new Color(2f, 0.3f, 0.35f));
                    break;
            }
            coinMat = MaterialFactory.Create(Palette.Coin, Palette.CoinGlow);
            rimMat = MaterialFactory.Create(Palette.CoinRim, Palette.CoinGlow * 0.4f);
        }

        // ---------- Course planning ----------

        /// <summary>
        /// Lays out the whole course: calm stretches between patterns (coin lines, holes in some lanes,
        /// full-width gaps, dropping blocks, zigzags). Every pattern leaves a way through.
        /// </summary>
        private void Plan(System.Random rng)
        {
            totalRows = GateRow + RiskLength + 30;
            ramps.Clear();
            floorPlan = new bool[totalRows, Lanes];
            for (int r = 0; r < totalRows; r++)
                for (int l = 0; l < Lanes; l++)
                    floorPlan[r, l] = true;
            blockPlan.Clear();
            coinPlan.Clear();

            CoinLine(1, 5, 6);
            int row = 14;
            while (row < TrackLength - 12)
            {
                float p = row / TrackLength;
                int pattern = rng.Next(p < 0.12f ? 3 : 5);
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
                        int filled = 0;
                        for (int l = 0; l < Lanes && filled < holes; l++)
                        {
                            int hl = (keep + 1 + l) % Lanes;
                            Hole(hl, row, 2);
                            filled++;
                            if (filled == 1) CoinArc(hl, row);
                        }
                        row += 2;
                        break;
                    }

                    case 2: // one or two dropping blocks, coins in the free lane
                    {
                        int free = rng.Next(Lanes);
                        int count = p > 0.25f && rng.Next(2) == 0 ? 2 : 1;
                        int placed = 0;
                        for (int l = 0; l < Lanes && placed < count; l++)
                        {
                            int bl = (free + 1 + l) % Lanes;
                            blockPlan.Add((bl, row));
                            placed++;
                        }
                        CoinLine(free, row - 2, 5);
                        row += 3;
                        break;
                    }

                    case 3: // the floor drops away across all lanes: jump!
                        for (int l = 0; l < Lanes; l++) Hole(l, row, 2);
                        if (this.kind == Kind.Surf) for (int l = 0; l < Lanes; l++) ramps.Add((l, row - 1)); // a wave launches the board over
                        CoinArc(rng.Next(Lanes), row);
                        row += 2;
                        break;

                    default: // zigzag: blocks in alternating lanes
                    {
                        int a = rng.Next(Lanes);
                        int b = (a + 1 + rng.Next(Lanes - 1)) % Lanes;
                        blockPlan.Add((a, row));
                        blockPlan.Add((b, row + 5));
                        row += 6;
                        break;
                    }
                }
                row += Mathf.RoundToInt(Mathf.Lerp(7f, 4.5f, p)) + rng.Next(2);
            }

            // The risky route past the right-hand gate: short, dense and fast.
            int r2 = GateRow + 4;
            while (r2 < GateRow + RiskLength - 4)
            {
                if (rng.Next(2) == 0)
                {
                    int a = rng.Next(Lanes);
                    blockPlan.Add((a, r2));
                    blockPlan.Add(((a + 1 + rng.Next(Lanes - 1)) % Lanes, r2 + 3));
                    r2 += 4;
                }
                else
                {
                    int keep = rng.Next(Lanes);
                    for (int l = 0; l < Lanes; l++) if (l != keep) Hole(l, r2, 2);
                    CoinLine(keep, r2 - 1, 4);
                    r2 += 3;
                }
                r2 += 3;
            }
            // The right-hand lane leads into the risky route; a wall splits it from the safe gate.
            blockPlan.Add((1, GateRow + 1));
        }

        private void Hole(int l, int row, int length)
        {
            for (int r = row; r < row + length && r < totalRows; r++) floorPlan[r, l] = false;
        }

        private void CoinLine(int l, int row, int count)
        {
            for (int i = 0; i < count; i++) coinPlan.Add((l, row + i, 0.45f));
        }

        /// <summary>Coins following the jump over a two-row hole: only a jump collects them.</summary>
        private void CoinArc(int l, int row)
        {
            coinPlan.Add((l, row - 0.7f, 0.95f));
            coinPlan.Add((l, row + 0.5f, 1.25f));
            coinPlan.Add((l, row + 1.7f, 0.95f));
        }

        private static float LaneX(int l) => (l - 1) * LaneWidth;

        // ---------- Building ----------

        private void BuildRow(int r)
        {
            var root = new GameObject("Row " + r).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 0f, r);

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
                Shapes.Rounded("Frame", root, new Vector3(lx, -0.03f, 0f), new Vector3(LaneWidth * 0.93f, 0.1f, 0.95f), 0.045f, frameMat);
                Shapes.Rounded("Top", root, new Vector3(lx, 0f, 0f), new Vector3(LaneWidth * 0.78f, 0.1f, 0.8f), 0.045f, topMat);
                Shapes.Rounded("Slab", root, new Vector3(lx, -0.24f, 0f), new Vector3(LaneWidth * 1.02f, 0.36f, 1.02f), 0.05f, slabMat);
                if (kind == Kind.Mine)
                    foreach (float rx in new[] { -0.28f, 0.28f })
                        Shapes.Rounded("Rail", root, new Vector3(lx + rx, 0.07f, 0f), new Vector3(0.05f, 0.04f, 1.02f), 0.012f, railMat);
                if (kind == Kind.Surf && (r + l) % 4 == 0)
                    Shapes.Rounded("Foam", root, new Vector3(lx - 0.15f, 0.06f, 0.1f), new Vector3(0.36f, 0.01f, 0.05f), 0.01f, foamMat);
                if (ramps.Contains((l, r)))
                {
                    var wedge = Shapes.Rounded("Wave", root, new Vector3(lx, 0.12f, 0.1f), new Vector3(LaneWidth * 0.85f, 0.08f, 0.6f), 0.04f, foamMat);
                    wedge.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
                }
            }

            // Duct walls with a glowing strip, and a neon arch every few rows for a sense of speed.
            foreach (float side in new[] { -1f, 1f })
            {
                Shapes.Rounded("Wall", root, new Vector3(side * WallX, 0.35f, 0f), new Vector3(0.32f, 1.3f, 1.03f), 0.06f, slabMat);
                Shapes.Rounded("Strip", root, new Vector3(side * (WallX - 0.17f), 0.1f, 0f), new Vector3(0.04f, 0.07f, 1.04f), 0.015f, wallGlowMat);
            }
            if (r % 6 == 0 && kind != Kind.Surf)
            {
                foreach (float side in new[] { -1f, 1f })
                    Shapes.Rounded("Post", root, new Vector3(side * WallX, 1.9f, 0f), new Vector3(0.2f, 2.0f, 0.2f), 0.05f, archMat);
                Shapes.Rounded("Beam", root, new Vector3(0f, 2.95f, 0f), new Vector3(WallX * 2f + 0.2f, 0.18f, 0.2f), 0.05f, archMat);
                if (kind == Kind.Mine) Shapes.Rounded("Lamp", root, new Vector3(0f, 2.78f, 0f), new Vector3(0.18f, 0.18f, 0.18f), 0.06f, wallGlowMat);
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
            rows.Enqueue((r, root));

            foreach (var (bl, br) in blockPlan)
                if (br == r) CreateBlock(bl, br);
            foreach (var (cl, cz, cy) in coinPlan)
                if (Mathf.RoundToInt(cz) == r) CreateCoin(cl, cz, cy);
        }

        private void CreateBlock(int l, int r)
        {
            var go = new GameObject("Block").transform;
            go.SetParent(transform, false);
            if (kind == Kind.Surf)
            {
                // A bobbing buoy, floating in the lane from the start.
                Shapes.Primitive(PrimitiveType.Sphere, "Buoy", go, Vector3.zero, new Vector3(0.75f, 0.7f, 0.75f), blockMat);
                Shapes.Primitive(PrimitiveType.Cylinder, "Stripe", go, Vector3.zero, new Vector3(0.78f, 0.08f, 0.78f), foamMat);
            }
            else if (kind == Kind.Mine)
            {
                Shapes.Rounded("Rock", go, Vector3.zero, new Vector3(0.88f, 0.8f, 0.86f), 0.3f, blockMat).transform.localRotation = Quaternion.Euler(12f, 30f, 8f);
            }
            else
            {
                Shapes.Rounded("Cube", go, Vector3.zero, new Vector3(0.9f, 0.9f, 0.9f), 0.12f, blockMat);
            }
            var ringMaterial = MaterialFactory.CreateTransparent(new Color(1f, 0.3f, 0.35f, 0.5f), Palette.TileWarningGlow * 0.5f);
            var ring = Shapes.Primitive(PrimitiveType.Cylinder, "Warning", transform, new Vector3(LaneX(l), 0.07f, r),
                new Vector3(0.85f, 0.004f, 0.85f), ringMaterial).transform;
            var block = new Block { lane = l, row = r, go = go, ring = ring, ringMaterial = ringMaterial };
            go.localPosition = new Vector3(LaneX(l), 0.5f + block.y, r);
            go.gameObject.SetActive(false); // appears only when it starts to drop; until then only the warning shows
            blocks.Add(block);
            if (kind == Kind.Surf)
            {
                block.y = 0f;
                block.landed = true;
                ring.gameObject.SetActive(false);
                go.gameObject.SetActive(true);
                go.localPosition = new Vector3(LaneX(l), 0.4f, r);
            }
        }

        private void CreateCoin(int l, float cz, float cy)
        {
            var go = new GameObject("Coin").transform;
            go.SetParent(transform, false);
            go.localPosition = new Vector3(LaneX(l), cy, cz);
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
            gate.localPosition = new Vector3(0f, 0f, GateRow);
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

        private Transform exitRing;

        private static void Arch(Transform parent, float x, float width, Color color, Color glow)
        {
            var m = MaterialFactory.Create(color, glow);
            foreach (float side in new[] { -0.5f, 0.5f })
                Shapes.Rounded("Post", parent, new Vector3(x + side * width, 1.1f, 0f), new Vector3(0.16f, 2.2f, 0.16f), 0.05f, m);
            Shapes.Rounded("Top", parent, new Vector3(x, 2.25f, 0f), new Vector3(width + 0.16f, 0.2f, 0.16f), 0.05f, m);
            Shapes.Rounded("Panel", parent, new Vector3(x, 1.9f, 0f), new Vector3(width * 0.6f, 0.36f, 0.06f), 0.05f, m);
        }

        /// <summary>What the robot rides: an orange-lit cell for surfing, a cart in the mine; and WARDEN on the chase.</summary>
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
            else if (kind == Kind.Chase)
            {
                chaserRoot = new GameObject("Chaser").transform;
                chaserRoot.SetParent(transform, false);
                chaser = WardenBoss.Create(new Vector3(0f, 1.8f, -3f), 3, robot.transform);
                chaser.transform.SetParent(chaserRoot, true);
                chaser.transform.localScale = Vector3.one * 0.9f;
            }
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
            UpdateBlocks(dt);
            UpdateCoins();
            PoseRobot(dt);
            UpdateCamera(dt);

            if (exitRing != null)
                exitRing.localRotation = Quaternion.Euler(0f, 0f, time * 40f);
        }

        private void HandleInput()
        {
            var cmd = input.Poll(robot.transform.position);
            if (startTimer < StartDelay) return;
            if (cmd.jump) Jump();
            if (!cmd.move.HasValue) return;
            switch (cmd.move.Value)
            {
                case Direction.PlusX: ChangeLane(1); break;
                case Direction.MinusX: ChangeLane(-1); break;
                case Direction.PlusY: Jump(); break;
                case Direction.MinusY:
                    if (!grounded) vy = Mathf.Min(vy, -9f); // slam down
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
            grounded = false;
            falling = false;
            y = Mathf.Max(y, 0f);
            vy = JumpVelocity;
            squash = -0.25f;
            AudioManager.PlaySfx(Sfx.Hop, 0.7f, 0.8f);
            Haptics.Light();
        }

        private void Move(float dt)
        {
            startTimer += dt;
            float speed = startTimer < StartDelay ? 0f : Mathf.Lerp(SpeedStart, SpeedEnd, Progress);
            if (crashed) speed = 0f;
            if (ended && escaped) speed *= Mathf.Clamp01(1f - endTimer * 0.6f);
            if (falling) speed *= 0.6f;
            z += speed * dt;
            runPhase += speed * dt * 2.2f;
            x = Mathf.MoveTowards(x, LaneX(lane), LaneSpeed * dt);

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
                vy -= Gravity * dt;
                y += vy * dt;
                if (y <= 0f && !falling)
                {
                    if (floor)
                    {
                        y = 0f;
                        vy = 0f;
                        grounded = true;
                        squash = 0.3f;
                        fx.Dust(new Vector3(x, 0.08f, z), Palette.TileTop, 6, 1.2f);
                    }
                    else
                    {
                        falling = true;
                    }
                }
            }

            if (falling && !ended && y < FallDepth)
            {
                if (kind == Kind.Chase)
                {
                    // No falling out of the chase: the robot scrambles back up, but WARDEN gains on it.
                    falling = false;
                    grounded = true;
                    y = 0f;
                    vy = 0f;
                    z += 2.4f;
                    Stumble();
                }
                else
                {
                    AudioManager.PlaySfx(Sfx.Fall);
                    Haptics.Death();
                    End(false, 0);
                }
            }

            // Surf: waves launch the board over the whirlpools.
            if (grounded && !ended && ramps.Contains((under, row))) { Jump(); vy = JumpVelocity * 1.15f; }

            if (kind == Kind.Chase && !ended) UpdateChaser(dt);
            if (falling && y < -8f) visual.gameObject.SetActive(false);

            // The gates: the left two lanes are the safe exit, the right lane takes the risky route.
            if (!ended && !risky && z >= GateRow)
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
            if (!ended && risky && z >= GateRow + RiskLength) Escape(RiskBonus);
            if (ended) endTimer += dt;
            squash = Mathf.MoveTowards(squash, 0f, dt * 2f);
        }

        private void Escape(int bonus)
        {
            escaped = true;
            fx.Burst(new Vector3(x, 1f, z + 0.5f), Palette.UiCyan, Palette.ShieldPickupGlow, 40, 6f);
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

        /// <summary>WARDEN chase: a hit costs coins and lets WARDEN close in; it falls back slowly over time.</summary>
        private void Stumble()
        {
            if (invulnerable > 0f) return;
            invulnerable = 1.2f;
            int lost = Mathf.Min(3, Coins);
            Coins -= lost;
            chaserGap = Mathf.Max(0.4f, chaserGap - 1.1f);
            fx.Burst(robot.transform.position + Vector3.up * 0.4f, Palette.Coin, Palette.CoinGlow, 6 + lost * 4, 4f);
            AudioManager.PlaySfx(Sfx.Bump, 0.9f, 0.8f);
            Haptics.Medium();
            rig.Shake(0.7f);
        }

        private void UpdateChaser(float dt)
        {
            invulnerable = Mathf.Max(0f, invulnerable - dt);
            visual.gameObject.SetActive(invulnerable <= 0f || Mathf.Repeat(time, 0.2f) > 0.08f);
            chaserGap = Mathf.Min(3.2f, chaserGap + dt * 0.12f);
            // Too close: WARDEN snatches a coin every second.
            if (chaserGap < 1.2f && Coins > 0)
            {
                drainTimer += dt;
                if (drainTimer >= 1f)
                {
                    drainTimer = 0f;
                    Coins--;
                    AudioManager.PlaySfx(Sfx.Coin, 0.5f, 0.6f);
                }
            }
            // WARDEN hovers behind and to the side, closing in as the gap shrinks.
            if (chaser != null) chaser.MoveTo(new Vector3(x * 0.3f + 1.7f, 2.3f, z - chaserGap * 0.6f - 0.3f));
        }

        private void UpdateRows()
        {
            while (builtRow < Mathf.Min(totalRows, z + BuildAhead)) BuildRow(builtRow++);
            while (rows.Count > 0 && rows.Peek().row < z - 6f) Destroy(rows.Dequeue().root.gameObject);

            // The far rows rise into place, so the duct seems to assemble itself ahead of the robot.
            float riseStart = z + BuildAhead - 10f;
            foreach (var (r, root) in rows)
            {
                float k = Mathf.Clamp01((r - riseStart) / 10f);
                root.localPosition = new Vector3(0f, -k * k * 4f, r);
            }
        }

        private void UpdateBlocks(float dt)
        {
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                var b = blocks[i];
                if (b.row < z - 6f)
                {
                    Destroy(b.go.gameObject);
                    Destroy(b.ring.gameObject);
                    blocks.RemoveAt(i);
                    continue;
                }

                if (!b.landed && b.row - z < BlockDropDistance)
                {
                    b.go.gameObject.SetActive(true);
                    b.vy += 30f * dt;
                    b.y -= b.vy * dt;
                    if (b.y <= 0f)
                    {
                        b.y = 0f;
                        b.landed = true;
                        b.ring.gameObject.SetActive(false);
                        fx.Dust(new Vector3(LaneX(b.lane), 0.1f, b.row), Palette.Block, 10, 2f);
                        AudioManager.PlaySfx(Sfx.Impact, 0.5f);
                        rig.Shake(0.25f);
                    }
                }
                b.go.localPosition = new Vector3(LaneX(b.lane), 0.5f + b.y, b.row);
                if (!b.landed)
                {
                    float a = 0.35f + 0.25f * Mathf.Sin(time * 12f);
                    MaterialFactory.SetColors(b.ringMaterial, new Color(1f, 0.3f, 0.35f, a), Palette.TileWarningGlow * a);
                }

                // Crash: the robot runs into a block (or one lands on it) without being high enough to clear it.
                if (!ended && !crashed && Mathf.Abs(b.row - z) < 0.6f && Mathf.Abs(LaneX(b.lane) - x) < 0.62f && y < b.y + 0.85f && b.y < 0.9f)
                {
                    if (kind == Kind.Chase)
                    {
                        Stumble();
                        fx.Burst(b.go.position, Palette.Block, Palette.BlockGlow, 14, 4f);
                        Destroy(b.go.gameObject);
                        Destroy(b.ring.gameObject);
                        blocks.RemoveAt(i);
                        continue;
                    }
                    Crash();
                }
            }
        }

        private void Crash()
        {
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
                if (c.z < z - 6f)
                {
                    Destroy(c.go.gameObject);
                    coins.RemoveAt(i);
                    continue;
                }
                c.go.localRotation = Quaternion.Euler(0f, time * 200f + c.z * 20f, 0f) * Quaternion.Euler(90f, 0f, 0f);
                if (ended || crashed) continue;
                if (Mathf.Abs(c.z - z) < 0.6f && Mathf.Abs(LaneX(c.lane) - x) < 0.6f && Mathf.Abs(c.y - (y + 0.45f)) < 0.6f)
                {
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
        }

        // ---------- Robot and camera ----------

        private void PoseRobot(float dt)
        {
            robot.transform.position = new Vector3(x, y, z);
            robot.transform.rotation = Quaternion.identity;

            if (crashed)
            {
                visual.localScale = Vector3.Lerp(visual.localScale, new Vector3(1.5f, 0.12f, 1.5f), dt * 14f);
                return;
            }

            // Lean into lane changes and forward while sprinting; squash on landing, stretch on take-off.
            float lean = (LaneX(lane) - x) * 18f;
            float forward = startTimer < StartDelay ? 0f : 12f;
            visual.localRotation = Quaternion.Euler(forward, lean * 0.6f, -lean);
            float s = 1f + squash;
            visual.localScale = new Vector3(1f / Mathf.Sqrt(Mathf.Max(0.3f, s)), Mathf.Max(0.3f, 1f - squash * 0.6f), 1f / Mathf.Sqrt(Mathf.Max(0.3f, s)));
            if (ended && escaped) visual.localRotation = Quaternion.Euler(0f, endTimer * 540f, 0f);

            // Run cycle: legs pump and arms swing (tucked while in the air).
            bool running = grounded && startTimer >= StartDelay && !ended;
            float swing = running ? Mathf.Sin(runPhase * Mathf.PI) : 0f;
            if (legL != null)
            {
                legL.localRotation = Quaternion.Euler(swing * 40f, 0f, 0f);
                legR.localRotation = Quaternion.Euler(-swing * 40f, 0f, 0f);
            }
            if (armL != null)
            {
                float air = grounded ? 0f : -70f;
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
                shadow.position = new Vector3(x, GridView.SurfaceY + 0.004f, z);
                float k = 0.48f * (1f - Mathf.Clamp01(y) * 0.4f);
                shadow.localScale = new Vector3(k, 0.004f, k);
            }
        }

        private void UpdateCamera(float dt)
        {
            // Behind and above the robot, following the lane softly, looking down the duct.
            // The chase looks down more steeply, so WARDEN shows behind the robot.
            if (kind == Kind.Chase)
            {
                var high = new Vector3(x * 0.4f, 4.6f, z - 5.2f);
                camPos = Vector3.Lerp(camPos, high, 1f - Mathf.Exp(-dt * 6f));
                rig.Chase(camPos, Quaternion.LookRotation(new Vector3(x * 0.5f, 0.3f, z + 3f) - camPos), 64f);
                return;
            }
            var target = new Vector3(x * 0.55f, 2.1f + Mathf.Max(y, -1f) * 0.35f, z - 3.7f);
            if (falling) target.y = Mathf.Max(1.4f, target.y);
            camPos = Vector3.Lerp(camPos, target, 1f - Mathf.Exp(-dt * 8f));
            var look = new Vector3(x * 0.75f, 0.55f + Mathf.Max(y, -0.5f) * 0.3f, z + 4.5f);
            rig.Chase(camPos, Quaternion.LookRotation(look - camPos), 62f);
        }
    }
}
