using System;
using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    /// <summary>
    /// The level map as a real 3D place: a path of floating stepping stones winding up into the sky, one stone per
    /// level, with a themed island at the start of every world (its crystals, lamps and towers in the world's colours),
    /// glowing dots between the stones, stars and drifting clouds far behind, and the robot standing on the next level.
    /// It has its own camera that slides up and down the path; the map screen draws the numbers, stars and buttons on
    /// top and feeds it the drags.
    /// </summary>
    public class MapWorld : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(-6000f, 0f, 0f);
        private const float Step = 1.7f;        // height per level
        private const float WorldGap = 4.4f;    // extra height for each world's island
        private const float ViewDistance = 12.5f;

        public Camera Cam { get; private set; }

        private Transform root, path, robotHolder;
        private Func<Transform, GameObject> robotLook;
        private int levelCount;
        private readonly List<Transform> stones = new List<Transform>();
        private readonly List<Material> stoneTops = new List<Material>();
        private float height, velocity, minHeight, maxHeight;
        private int current = -1, hopFrom = -1;
        private float hopT = 1f;
        private float time;
        private bool dragging;

        public static MapWorld Create(int levelCount, Func<Transform, GameObject> robotLook)
        {
            var go = new GameObject("MapWorld");
            go.transform.position = Origin;
            var m = go.AddComponent<MapWorld>();
            m.levelCount = levelCount;
            m.robotLook = robotLook;
            m.BuildCamera();
            m.BuildBackdrop();
            go.SetActive(false);
            return m;
        }

        // ---------- Layout ----------

        /// <summary>Where level <paramref name="i"/>'s stone stands (its top centre).</summary>
        public static Vector3 NodePosition(int i)
        {
            int w = i / LevelCatalog.LevelsPerWorld;
            float y = i * Step + w * WorldGap + WorldGap;
            float x = Mathf.Sin(i * 0.8f) * 2.1f;
            float z = Mathf.Cos(i * 0.55f) * 1.1f;
            return Origin + new Vector3(x, y, z);
        }

        private static Vector3 IslandPosition(int world)
        {
            var first = NodePosition(world * LevelCatalog.LevelsPerWorld);
            // Set back behind the path (the camera looks from -z), so it never hides the stones in front of it.
            return new Vector3(Origin.x, first.y - WorldGap * 0.6f, Origin.z + 3.6f);
        }

        // ---------- Building ----------

        private void BuildCamera()
        {
            var camGo = new GameObject("MapCamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.06f, 0.05f, 0.16f);
            Cam.fieldOfView = 50f;
            Cam.nearClipPlane = 0.5f;
            Cam.farClipPlane = 80f;
            Cam.depth = 5f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = false; // nothing on the map casts a shadow: no shadow pass
            data.antialiasing = Application.isMobilePlatform ? AntialiasingMode.None : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            // Deep space behind everything: the galaxy picture, hung far behind the path and moving with the camera.
            var sky = Shapes.Primitive(PrimitiveType.Quad, "Galaxy", camGo.transform, new Vector3(0f, 0f, 76f), new Vector3(80f, 100f, 1f), MaterialFactory.CreateUnlit(SquashBot.UI.UiSprites.Galaxy.texture)); // wide enough for a tablet
            sky.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Far stars and soft clouds behind the path, all the way up.</summary>
        private void BuildBackdrop()
        {
            var back = new GameObject("Backdrop").transform;
            back.SetParent(transform, false);
            int worlds = Mathf.CeilToInt(levelCount / (float)LevelCatalog.LevelsPerWorld);
            float top = NodePosition(levelCount - 1).y - Origin.y + 20f;
            var rng = new System.Random(7);
            var star = MaterialFactory.Create(Color.white, new Color(1.6f, 1.6f, 2f));
            // The stars: one mesh (hundreds of little cubes merged), one draw instead of hundreds of objects.
            var parts = new List<CombineInstance>();
            var cube = Shapes.MeshOf(PrimitiveType.Cube);
            for (int i = 0; i < Mathf.Min(900, (int)(top * 2.5f)); i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 40f - 20f, (float)rng.NextDouble() * top - 10f, 14f + (float)rng.NextDouble() * 18f);
                float s = 0.05f + (float)rng.NextDouble() * 0.12f;
                parts.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * s) });
            }
            MergedPart("Stars", back, parts, star);
            for (int w = 0; w < worlds; w++)
            {
                var theme = WorldTheme.ForWorld(w);
                var tint = Color.Lerp(theme.accent, Color.white, 0.6f);
                var cloud = MaterialFactory.CreateTransparent(new Color(tint.r, tint.g, tint.b, 0.05f), tint * 0.08f);
                float baseY = IslandPosition(w).y - Origin.y;
                for (int c = 0; c < 5; c++)
                {
                    var p = new Vector3((float)rng.NextDouble() * 30f - 15f, baseY + (float)rng.NextDouble() * 16f, 18f + (float)rng.NextDouble() * 6f);
                    var size = new Vector3(9f + (float)rng.NextDouble() * 8f, 2.5f + (float)rng.NextDouble() * 2f, 3f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Cloud", back, p, size, cloud);
                }
            }
        }

        /// <summary>(Re)builds the stones, islands and dots for the current progress.</summary>
        public void Build(int unlocked, bool openAll)
        {
            // The map casts no shadows (whatever the last floor set).
            bool shadows = Shapes.CastShadows;
            Shapes.CastShadows = false;
            try { BuildParts(unlocked, openAll); }
            finally { Shapes.CastShadows = shadows; }
        }

        private Transform[] islands;
        private bool[] islandLocked;
        private Transform islandRoot;
        private readonly Dictionary<string, Material> shared = new Dictionary<string, Material>();

        /// <summary>One material per look, shared by every stone wearing it (made on first use).</summary>
        private Material Shared(string key, Color colour, Color emission)
        {
            if (!shared.TryGetValue(key, out var m) || m == null) shared[key] = m = MaterialFactory.Create(colour, emission);
            return m;
        }

        private void BuildParts(int unlocked, bool openAll)
        {
            if (path != null) Destroy(path.gameObject);
            stones.Clear();
            stoneTops.Clear();
            path = new GameObject("Path").transform;
            path.SetParent(transform, false);
            current = Mathf.Clamp(unlocked, 0, levelCount - 1);

            // The islands are the heavy part: each is kept, and made again only when it locks or opens.
            int worlds = Mathf.CeilToInt(levelCount / (float)LevelCatalog.LevelsPerWorld);
            if (islandRoot == null)
            {
                islandRoot = new GameObject("Islands").transform;
                islandRoot.SetParent(transform, false);
                islands = new Transform[worlds];
                islandLocked = new bool[worlds];
            }
            for (int w = 0; w < worlds; w++)
            {
                bool locked = w * LevelCatalog.LevelsPerWorld > unlocked && !openAll;
                if (islands[w] != null && islandLocked[w] == locked) continue;
                if (islands[w] != null) Destroy(islands[w].gameObject);
                islands[w] = BuildIsland(w, locked);
                islandLocked[w] = locked;
            }

            // Beyond the last level: the Red Desert, the open world (dim until the escape is complete).
            bool desertOpen = unlocked >= levelCount || openAll || Progress.Stars(levelCount - 1) > 0;
            if (desert == null || desertWasOpen != desertOpen)
            {
                if (desert != null) Destroy(desert.gameObject);
                desert = BuildDesert(desertOpen);
                desertWasOpen = desertOpen;
            }

            // The dots between the stones: merged into two meshes (walked, still ahead), two draws for a thousand dots.
            var dotDone = Shared("dotDone", new Color(1f, 0.95f, 0.8f), new Color(1.6f, 1.4f, 0.9f));
            var dotLocked = Shared("dotLocked", new Color(0.35f, 0.33f, 0.5f), Color.black);
            var sphere = Shapes.MeshOf(PrimitiveType.Sphere);
            var doneParts = new List<CombineInstance>();
            var aheadParts = new List<CombineInstance>();
            for (int i = 0; i < levelCount; i++)
            {
                stones.Add(BuildStone(i, unlocked, openAll));
                if (i == levelCount - 1) continue;
                var a = NodePosition(i);
                var b = NodePosition(i + 1);
                bool done = i + 1 <= unlocked;
                int dots = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.45f));
                for (int d = 1; d < dots; d++)
                {
                    var p = Vector3.Lerp(a, b, d / (float)dots) - Vector3.up * 0.05f - Origin;
                    (done ? doneParts : aheadParts).Add(new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * 0.13f) });
                }
            }
            MergedPart("DotsDone", path, doneParts, dotDone);
            MergedPart("DotsAhead", path, aheadParts, dotLocked);

            if (robotHolder == null)
            {
                robotHolder = new GameObject("Robot").transform;
                robotHolder.SetParent(transform, false);
            }
            for (int i = robotHolder.childCount - 1; i >= 0; i--) Destroy(robotHolder.GetChild(i).gameObject);
            robotLook?.Invoke(robotHolder);
            robotHolder.localScale = Vector3.one * 1.6f;

            minHeight = NodePosition(0).y - Origin.y - 1f;
            maxHeight = NodePosition(levelCount - 1).y - Origin.y + 5.5f; // up to the Red Desert above the last level
        }

        /// <summary>Many small pieces of one material as a single mesh (destroyed with its object).</summary>
        private static void MergedPart(string name, Transform parent, List<CombineInstance> parts, Material material)
        {
            if (parts.Count == 0) return;
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<OwnedMesh>().mesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private Transform BuildStone(int i, int unlocked, bool openAll)
        {
            var theme = WorldTheme.ForWorld(i / LevelCatalog.LevelsPerWorld);
            bool completed = i < unlocked;
            bool isCurrent = i == unlocked;
            bool locked = i > unlocked && !openAll;
            var stone = new GameObject("Stone " + (i + 1)).transform;
            stone.SetParent(path, false);
            stone.position = NodePosition(i);

            // Shared per world and state (the pulsing current stone keeps a top of its own).
            int wd = i / LevelCatalog.LevelsPerWorld;
            string state = locked ? "L" : completed ? "C" : "O";
            // Purple pedestals edged in gold (the game look): a lit top in the world's colour, a deep purple body.
            var rock = locked ? Shared("rockL", new Color(0.18f, 0.16f, 0.26f), Color.black)
                : Shared("rockP", new Color(0.26f, 0.17f, 0.42f), new Color(0.04f, 0.02f, 0.08f));
            Color topColor = locked ? new Color(0.28f, 0.26f, 0.38f) : isCurrent ? Color.white : Color.Lerp(theme.accent, new Color(0.42f, 0.3f, 0.62f), 0.55f);
            // The stone the map pulses (the current one, or the last when all are done) has its own top: never a shared one.
            bool pulsed = i == Mathf.Clamp(unlocked, 0, levelCount - 1);
            var top = isCurrent ? MaterialFactory.Create(topColor, topColor * 0.7f)
                : pulsed ? MaterialFactory.Create(topColor, locked ? Color.black : topColor * 0.35f)
                : locked ? Shared("topL", topColor, Color.black) : Shared("top" + wd, topColor, topColor * 0.35f);
            var rim = Shared("rim" + state, locked ? new Color(0.42f, 0.38f, 0.5f) : new Color(1f, 0.78f, 0.36f),
                locked ? Color.black : completed ? new Color(1.2f, 0.85f, 0.3f) : new Color(1.6f, 1.2f, 0.5f));
            // A rounded slab on top, a rock tapering away below (two stacked pieces read as a floating stone).
            Shapes.Primitive(PrimitiveType.Cylinder, "Top", stone, new Vector3(0f, -0.12f, 0f), new Vector3(1.25f, 0.12f, 1.25f), top);
            Shapes.Primitive(PrimitiveType.Cylinder, "Rim", stone, new Vector3(0f, -0.2f, 0f), new Vector3(1.38f, 0.06f, 1.38f), rim);
            Shapes.Primitive(PrimitiveType.Cylinder, "Rock", stone, new Vector3(0f, -0.48f, 0f), new Vector3(1.15f, 0.24f, 1.15f), rock);
            Shapes.Primitive(PrimitiveType.Cylinder, "Band", stone, new Vector3(0f, -0.62f, 0f), new Vector3(1.05f, 0.025f, 1.05f), rim);
            Shapes.Primitive(PrimitiveType.Sphere, "Root", stone, new Vector3(0f, -0.78f, 0f), new Vector3(0.85f, 0.7f, 0.85f), rock);
            stoneTops.Add(top);
            return stone;
        }

        /// <summary>
        /// The world's island, a little diorama of what waits there: its realm floor (the same tiles and glowing seams
        /// as in play), and on it the world's own bugs, guard robot, enforcer, crate and end monster, in their real
        /// looks. Locked worlds show it too (a peek at what is ahead), under a dimmer light.
        /// </summary>
        private Transform desert;
        private bool desertWasOpen;

        /// <summary>Where the Red Desert lies: above the last level, set back behind the path like the islands.</summary>
        private Vector3 DesertPosition => new Vector3(Origin.x, NodePosition(levelCount - 1).y + 5f, Origin.z + 4.5f);

        /// <summary>
        /// The open world on the map: a wide plateau of red sand with scrap heaps and fallen robots in front, green farmland
        /// with red-roofed houses and trees behind, two moons over it. Dim and quiet until level 250 is won.
        /// </summary>
        private Transform BuildDesert(bool open)
        {
            var land = new GameObject("RedDesert").transform;
            land.SetParent(transform, false);
            land.position = DesertPosition;
            float dim = open ? 1f : 0.55f;
            Material Mat(string key, Color c, float glow = 0f) => Shared("desert_" + key + (open ? "" : "_dim"), c * dim, c * glow * dim);
            var sand = Mat("sand", new Color(0.86f, 0.42f, 0.26f));
            var rockM = Mat("rock", new Color(0.5f, 0.24f, 0.16f));
            var rust = Mat("rust", new Color(0.42f, 0.24f, 0.16f));
            var metal = Mat("metal", new Color(0.5f, 0.48f, 0.46f));
            var grass = Mat("grass", new Color(0.42f, 0.66f, 0.3f));
            var wall = Mat("wall", new Color(0.95f, 0.92f, 0.85f));
            var roof = Mat("roof", new Color(0.78f, 0.24f, 0.18f));
            var leaf = Mat("leaf", new Color(0.3f, 0.58f, 0.26f));
            var wood = Mat("wood", new Color(0.5f, 0.34f, 0.2f));
            var moon = Mat("moon", new Color(1f, 0.92f, 0.82f), open ? 1.4f : 0.3f);
            var rng = new System.Random(250);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // The plateau: red sand in front, green land behind, rock underneath.
            Shapes.Rounded("Sand", land, new Vector3(0f, -0.2f, -1.2f), new Vector3(15f, 0.5f, 5f), 0.2f, sand);
            Shapes.Rounded("Green", land, new Vector3(0f, -0.15f, 3.2f), new Vector3(15f, 0.5f, 4f), 0.2f, grass);
            Shapes.Primitive(PrimitiveType.Sphere, "Underside", land, new Vector3(0f, -1.4f, 0.8f), new Vector3(14f, 2.6f, 8.6f), rockM);
            for (int i = 0; i < 4; i++)
                Shapes.Primitive(PrimitiveType.Sphere, "Drip", land, new Vector3(R(-5f, 5f), R(-2.6f, -2f), R(-1f, 2f)), new Vector3(R(2f, 3.4f), R(1.4f, 2.2f), R(1.6f, 2.6f)), rockM);
            // Scrap heaps and a toppled robot on the sand.
            for (int i = 0; i < 9; i++)
            {
                var heap = new GameObject("Scrap").transform;
                heap.SetParent(land, false);
                heap.localPosition = new Vector3(R(-6.5f, 6.5f), 0.05f, R(-3.2f, 0.4f));
                for (int p = 0; p < 4; p++)
                    Shapes.Rounded("Part", heap, new Vector3(R(-0.3f, 0.3f), R(0.05f, 0.25f), R(-0.3f, 0.3f)), new Vector3(R(0.2f, 0.6f), R(0.15f, 0.4f), R(0.2f, 0.5f)), 0.03f, p % 2 == 0 ? rust : metal)
                        .transform.localRotation = Quaternion.Euler(R(-25f, 25f), R(0f, 90f), R(-25f, 25f));
            }
            var wreck = Shapes.Rounded("Wreck", land, new Vector3(3.6f, 0.35f, -0.6f), new Vector3(0.9f, 0.7f, 0.7f), 0.1f, rust).transform;
            wreck.localRotation = Quaternion.Euler(0f, 30f, 24f);
            Shapes.Rounded("WreckHead", wreck, new Vector3(0.1f, 0.7f, 0f), new Vector3(0.6f, 0.45f, 0.5f), 0.08f, metal);
            // Farms on the green: houses with red roofs, trees.
            foreach (float x in new[] { -5f, -1.5f, 2.5f, 5.6f })
            {
                var house = new GameObject("Farm").transform;
                house.SetParent(land, false);
                house.localPosition = new Vector3(x, 0.1f, R(2.4f, 4.2f));
                Shapes.Rounded("House", house, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.7f), 0.03f, wall);
                Shapes.Rounded("Roof", house, new Vector3(0f, 0.68f, 0f), new Vector3(1f, 0.18f, 0.8f), 0.03f, roof);
                for (int t = 0; t < 2; t++)
                {
                    var at = new Vector3(R(-1.4f, 1.4f), 0f, R(-0.6f, 0.8f));
                    Shapes.Primitive(PrimitiveType.Cylinder, "Trunk", house, at + new Vector3(0f, 0.2f, 0f), new Vector3(0.1f, 0.2f, 0.1f), wood);
                    Shapes.Primitive(PrimitiveType.Sphere, "Leaves", house, at + new Vector3(0f, 0.55f, 0f), Vector3.one * R(0.45f, 0.65f), leaf);
                }
            }
            // Two moons above.
            Shapes.Primitive(PrimitiveType.Sphere, "MoonA", land, new Vector3(-4f, 5.5f, 6f), Vector3.one * 2.4f, moon);
            Shapes.Primitive(PrimitiveType.Sphere, "MoonB", land, new Vector3(3.6f, 4.2f, 6.5f), Vector3.one * 1.1f, moon);
            return land;
        }

        private Transform BuildIsland(int world, bool locked)
        {
            var theme = WorldTheme.ForWorld(world).Realm(world);
            var island = new GameObject("Island " + (world + 1)).transform;
            island.SetParent(islandRoot, false);
            island.position = IslandPosition(world);
            var rng = new System.Random(world * 31 + 5);
            float R() => (float)rng.NextDouble();

            float dim = locked ? 0.6f : 1f;
            var rock = MaterialFactory.Create(Color.Lerp(theme.pillar, Color.black, 0.3f) * dim, Color.black);
            var slab = MaterialFactory.Create(theme.slab * dim, Color.black);
            var edge = MaterialFactory.Create(theme.slab * dim, theme.slabEdgeGlow * (locked ? 0.3f : 0.8f));
            var frame = MaterialFactory.Create(theme.tileTop * dim, theme.tileGlow * (locked ? 0.3f : 1f));
            var top = MaterialFactory.Create(theme.tileTop * dim, theme.tileSelfLight * dim);
            var glow = MaterialFactory.Create(theme.accent, locked ? theme.accent * 0.2f : theme.accent * 1.6f);

            // The floor: the world's slab with its glowing edge band and a grid of its tiles.
            Shapes.Rounded("Slab", island, new Vector3(0f, -0.15f, 0f), new Vector3(7.6f, 0.4f, 4.3f), 0.12f, slab);
            Shapes.Rounded("Edge", island, new Vector3(0f, -0.3f, 0f), new Vector3(7.7f, 0.06f, 4.4f), 0.03f, edge);
            Shapes.Primitive(PrimitiveType.Sphere, "Underside", island, new Vector3(0f, -1f, 0f), new Vector3(7f, 1.8f, 3.9f), rock);
            Shapes.Primitive(PrimitiveType.Sphere, "Drip", island, new Vector3(-1.2f, -1.8f, 0.3f), new Vector3(2f, 1.6f, 1.6f), rock);
            Shapes.Primitive(PrimitiveType.Sphere, "Drip", island, new Vector3(1.6f, -1.6f, -0.2f), new Vector3(1.4f, 1.3f, 1.2f), rock);
            const int cols = 8, rows = 4;
            const float tile = 0.9f;
            for (int cx = 0; cx < cols; cx++)
                for (int cz = 0; cz < rows; cz++)
                {
                    var at = new Vector3((cx - (cols - 1) * 0.5f) * tile, 0.06f, (cz - (rows - 1) * 0.5f) * tile);
                    Shapes.Rounded("Frame", island, at + new Vector3(0f, -0.03f, 0f), new Vector3(tile * 0.93f, 0.09f, tile * 0.93f), 0.04f, frame);
                    Shapes.Rounded("Top", island, at, new Vector3(tile * 0.78f, 0.09f, tile * 0.78f), 0.04f, top);
                }
            const float floorY = 0.11f;
            var facing = Quaternion.Euler(0f, 180f, 0f); // towards the map camera

            // The cast, back row over the front edge (where the world's name sits): bugs, a guard, the crate,
            // the enforcer (from the second world on) and the monster at the back right.
            var bugColour = Color.HSVToRGB(Mathf.Repeat(world * 0.137f + 0.1f, 1f), 0.7f, 1f);
            bool brightBugs = world >= 8 || theme.tileTop.grayscale < 0.55f;
            foreach (var (x, z, yaw) in new[] { (-3.1f, 0.15f, 150f), (-2.3f, 0.75f, 215f), (-2.75f, 1.3f, 175f) })
            {
                var bug = BugModel.Build(island, bugColour, world % BugModel.Kinds, brightBugs);
                bug.transform.localPosition = new Vector3(x, floorY, z);
                bug.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                bug.transform.localScale = Vector3.one * 1.8f;
            }

            var accent = Color.HSVToRGB(Mathf.Repeat(world * 0.21f + 0.55f, 1f), 0.75f, 1f);
            // The perfect city's worlds (level 101 on) show its pearl guards.
            var guard = world >= 10 ? GuardBot.Build(island, Color.HSVToRGB(Mathf.Repeat(world * 0.21f + 0.5f, 1f), 0.6f, 1f), world, utopian: true)
                : GuardBot.Build(island, Color.Lerp(accent, new Color(0.3f, 0.3f, 0.35f), 0.35f), world);
            guard.transform.localPosition = new Vector3(-1.1f, floorY, 0.9f);
            guard.transform.localRotation = facing * Quaternion.Euler(0f, 20f, 0f);
            guard.transform.localScale = Vector3.one * 1.05f;

            var crate = HazardVisuals.CrateOfTier(world / 5).transform;
            crate.SetParent(island, false);
            crate.localPosition = new Vector3(0.35f, floorY + 0.4f, 1.1f);
            crate.localRotation = Quaternion.Euler(0f, 18f + R() * 20f, 0f);
            crate.localScale = Vector3.one * 0.95f;

            // The world's guard tower (from the second world on, as on its floors).
            if (world >= 1)
            {
                var tower = TowerModel.Build(island, TowerModel.DesignFor(world, 0), accent);
                tower.transform.localPosition = new Vector3(-0.3f, floorY, 1.6f);
                tower.transform.localRotation = facing;
                tower.transform.localScale = Vector3.one * 0.72f;
                tower.enabled = false; // a still picture: no aiming, no glow pulses
            }

            if (world >= 1)
            {
                var brute = HumanoidBot.Build(island, accent);
                brute.transform.localPosition = new Vector3(1.5f, floorY, 0.55f);
                brute.transform.localRotation = facing * Quaternion.Euler(0f, -15f, 0f);
                brute.transform.localScale = Vector3.one * 1.05f;
            }

            MonsterLook(world, out var kind, out var tint);
            var monster = Monster.Create(kind, island.position, 1, tint, null);
            GuardianLooks.DressGuardian(monster.Body, world);
            monster.transform.SetParent(island, false);
            monster.transform.localPosition = new Vector3(2.8f, floorY, 1.15f);
            monster.transform.localRotation = facing * Quaternion.Euler(0f, -25f, 0f);
            monster.transform.localScale = Vector3.one * 0.8f;
            var bar = monster.transform.Find("HealthBar");
            if (bar != null) bar.gameObject.SetActive(false);

            // A lamp at each back corner in the world's accent.
            foreach (float x in new[] { -3.55f, 3.55f })
            {
                Shapes.Rounded("Post", island, new Vector3(x, 0.7f, 1.85f), new Vector3(0.09f, 1.2f, 0.09f), 0.03f, rock);
                Shapes.Primitive(PrimitiveType.Sphere, "Lamp", island, new Vector3(x, 1.35f, 1.85f), Vector3.one * 0.26f, glow);
            }
            return island;
        }

        /// <summary>The end monster of a world, as play dresses it (its guardian, else the world's kind and colour).</summary>
        private static void MonsterLook(int world, out Monster.Kind kind, out Color tint)
        {
            if (GuardianLooks.Guardian(world, out kind, out tint)) return;
            kind = (Monster.Kind)(world % 4);
            tint = Color.Lerp(WorldTheme.ForWorld(world).accent, new Color(0.55f, 0.85f, 0.4f), kind == Monster.Kind.Slime ? 0.5f : 0.15f);
        }

        // ---------- Showing and moving ----------

        /// <summary>Starts showing the map with the camera on <paramref name="focus"/>; a hop plays from <paramref name="from"/>.</summary>
        public void Open(int focus, int from)
        {
            gameObject.SetActive(true);
            height = Mathf.Clamp(NodePosition(Mathf.Clamp(focus, 0, levelCount - 1)).y - Origin.y + 1.5f, minHeight, maxHeight);
            velocity = 0f;
            hopFrom = from >= 0 && from < current ? from : -1;
            hopT = hopFrom >= 0 ? -0.4f : 1f;
            PlaceRobot();
            PlaceCamera();
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>A drag on the map by <paramref name="pixels"/> (screen pixels, up = positive).</summary>
        public void Drag(float pixels)
        {
            float unitsPerPixel = 2f * ViewDistance * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            float dy = -pixels * unitsPerPixel;
            height = Mathf.Clamp(height + dy, minHeight - 1f, maxHeight + 1f);
            velocity = dy / Mathf.Max(0.001f, Time.unscaledDeltaTime);
            dragging = true;
        }

        public void EndDrag() => dragging = false;

        /// <summary>Where level <paramref name="i"/>'s stone top is on screen (z &lt; 0 when behind the camera).</summary>
        public Vector3 ScreenPoint(int i) => Cam.WorldToScreenPoint(NodePosition(i));

        /// <summary>Screen point of a world's island sign.</summary>
        public Vector3 IslandScreenPoint(int world) => Cam.WorldToScreenPoint(IslandPosition(world) + new Vector3(0f, 0.3f, -2.4f));

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            time += dt;
            if (!dragging)
            {
                // Glide on after a flick, then settle back inside the path.
                height += velocity * dt;
                velocity = Mathf.MoveTowards(velocity, 0f, Mathf.Abs(velocity) * 4f * dt + 0.5f * dt);
                if (height < minHeight) height = Mathf.Lerp(height, minHeight, dt * 8f);
                if (height > maxHeight) height = Mathf.Lerp(height, maxHeight, dt * 8f);
            }
            if (hopT < 1f) hopT += dt / 0.7f;
            PlaceRobot();
            PlaceCamera();
            // Only the islands near the view are awake (their little robots animate; the rest are not drawn or run).
            if (islands != null)
                for (int w = 0; w < islands.Length; w++)
                {
                    if (islands[w] == null) continue;
                    bool near = Mathf.Abs(IslandPosition(w).y - Origin.y - height) < 16f;
                    if (islands[w].gameObject.activeSelf != near) islands[w].gameObject.SetActive(near);
                }
            // The next level's stone glows in and out.
            if (current >= 0 && current < stoneTops.Count)
            {
                float k = 0.55f + 0.45f * Mathf.Sin(time * 4f);
                MaterialFactory.SetColors(stoneTops[current], Color.white, new Color(1f, 1f, 1.2f) * k);
                stones[current].localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(time * 4f));
            }
        }

        private void PlaceRobot()
        {
            if (robotHolder == null || current < 0) return;
            var to = NodePosition(current);
            Vector3 p;
            if (hopFrom >= 0 && hopT < 1f)
            {
                float t = Mathf.Clamp01(hopT);
                t = t * t * (3f - 2f * t);
                var from = NodePosition(hopFrom);
                p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.6f;
            }
            else p = to + Vector3.up * Mathf.Abs(Mathf.Sin(time * 3f)) * 0.12f;
            robotHolder.position = p;
            robotHolder.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -1f));
        }

        private void PlaceCamera()
        {
            // Narrow phones see as much of the path's width as a 9:16 screen does.
            Cam.fieldOfView = CameraFit.Fov(50f, Cam.aspect);
            var focus = Origin + new Vector3(0f, height, 0f);
            Cam.transform.position = focus + new Vector3(0f, 3.2f, -ViewDistance);
            Cam.transform.LookAt(focus + Vector3.up * 0.4f);
            // The sky shifts with the world the camera is in.
            int world = Mathf.Clamp(Mathf.FloorToInt((height - WorldGap) / (Step * LevelCatalog.LevelsPerWorld + WorldGap)), 0, Mathf.Max(0, (levelCount - 1) / LevelCatalog.LevelsPerWorld));
            var theme = WorldTheme.ForWorld(world);
            Cam.backgroundColor = Color.Lerp(Cam.backgroundColor, height > NodePosition(levelCount - 1).y - Origin.y + 2.5f ? new Color(0.62f, 0.38f, 0.3f) : Color.Lerp(theme.bgBottom, theme.bgTop, 0.5f) * 0.55f, Time.unscaledDeltaTime * 2f); // (the Red Desert's dusky sky at the top)
        }
    }
}
