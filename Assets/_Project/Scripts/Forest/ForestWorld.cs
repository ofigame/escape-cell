using System.Collections.Generic;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Forest
{
    /// <summary>
    /// The forest leg of the prototype, built at runtime far from the stage: a hilly terrain painted with photo
    /// textures, a winding dirt path (painted into the terrain), trees, shrubs, ferns, rocks and grass along it,
    /// stone stairs up to a raised stone deck and down again, a clearing and a rocky tunnel mouth at the end.
    /// Everything is placed along the path: z grows forward, x is to the side.
    /// </summary>
    public class ForestWorld : MonoBehaviour
    {
        /// <summary>The first leg's place: far from the level stage so nothing of it is ever in view.</summary>
        public static readonly Vector3 FirstOrigin = new Vector3(5000f, 0f, 0f);

        /// <summary>This leg's place in the world (its local origin).</summary>
        public Vector3 Origin { get; private set; }
        public int Leg { get; private set; }
        /// <summary>How this leg is entered (none for the first) and left.</summary>
        public PassageStyle Entry { get; private set; }
        public PassageStyle Exit { get; private set; }

        // The course (local coordinates, metres). The deck grows longer and wider with every leg; what follows it
        // shifts along.
        public const float DeckHeight = 3.2f;
        public const float StairsUpStart = 80f, DeckStart = 88f, StairsHalfWidth = 1.6f, ClearingRadius = 11f;
        public float DeckEnd { get; private set; }
        public float StairsDownEnd => DeckEnd + 8f;
        public float DeckHalfWidth { get; private set; }
        public Vector2 ClearingCentre => new Vector2(0f, 160f + Shift);
        public float TunnelZ => 212f + Shift;
        /// <summary>The cart (cave) or raft (gorge) waits here, inside the passage.</summary>
        public float BoardZ => TunnelZ + 8f;
        /// <summary>Where the walker comes out of the entry passage.</summary>
        public const float EntryMouthZ = -14f, ArriveZ = -21f;
        /// <summary>Holes in the deck to jump over (local x/z rectangles), from the second leg on.</summary>
        public readonly List<Rect> Gaps = new List<Rect>();
        private float Shift => DeckEnd - 128f;

        private const float TerrainWidth = 170f, TerrainLength = 340f, TerrainHeight = 24f, TerrainBase = 6f;
        private static readonly Vector3 TerrainCorner = new Vector3(-85f, -TerrainBase, -35f);

        public Terrain Terrain { get; private set; }
        /// <summary>Dense points along the path's centre line (local).</summary>
        public readonly List<Vector3> PathPoints = new List<Vector3>();
        /// <summary>Tree trunks the walker can't pass through (local x, z, radius).</summary>
        public readonly List<Vector3> Trunks = new List<Vector3>();
        /// <summary>Where the signposts guide the walker, in order (local).</summary>
        public readonly List<Vector3> Waypoints = new List<Vector3>();

        private System.Random rng;
        private float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public static ForestWorld Build(int seed, Vector3 origin, int leg, PassageStyle entry, PassageStyle exit)
        {
            var go = new GameObject("ForestWorld " + leg);
            go.transform.position = origin;
            var w = go.AddComponent<ForestWorld>();
            w.Origin = origin;
            w.Leg = leg;
            w.Entry = entry;
            w.Exit = exit;
            int grow = Mathf.Min(leg - 1, 4);
            w.DeckEnd = 128f + grow * 14f;
            w.DeckHalfWidth = 4.5f + grow * 0.75f;
            w.rng = new System.Random(seed);
            w.BuildPath();
            w.BuildTerrain();
            w.BuildStructures();
            w.Scatter();
            return w;
        }

        public Vector3 ToWorld(Vector3 local) => Origin + local;

        // ---------- The path ----------

        private void BuildPath()
        {
            var ctrl = new List<Vector3>
            {
                new Vector3(0f, 0f, -40f), new Vector3(0f, 0f, -26f), new Vector3(0f, 0f, -12f), new Vector3(0f, 0f, 0f), new Vector3(3.5f, 0f, 16f), new Vector3(-5f, 0f, 33f),
                new Vector3(-2f, 0f, 50f), new Vector3(3f, 0f, 65f), new Vector3(0f, 0f, 76f), new Vector3(0f, 0f, StairsUpStart),
                new Vector3(0f, 0f, StairsDownEnd), new Vector3(0f, 0f, 140f + Shift), new Vector3(3f, 0f, 150f + Shift), new Vector3(0f, 0f, 160f + Shift),
                new Vector3(-3f, 0f, 176f + Shift), new Vector3(2.5f, 0f, 192f + Shift), new Vector3(0f, 0f, 205f + Shift), new Vector3(0f, 0f, TunnelZ + 4f), new Vector3(0f, 0f, TunnelZ + 12f),
            };
            for (int i = 1; i < ctrl.Count - 2; i++)
            {
                var p0 = ctrl[i - 1]; var p1 = ctrl[i]; var p2 = ctrl[i + 1]; var p3 = ctrl[i + 2];
                int steps = Mathf.CeilToInt(Vector3.Distance(p1, p2) / 0.6f);
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    var p = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                    PathPoints.Add(p);
                }
            }
            Waypoints.Add(new Vector3(0f, 0f, 40f));
            Waypoints.Add(new Vector3(0f, 0f, StairsUpStart - 1f));
            Waypoints.Add(new Vector3(0f, DeckHeight, DeckEnd - 1f));
            Waypoints.Add(new Vector3(ClearingCentre.x, 0f, ClearingCentre.y));
            Waypoints.Add(new Vector3(0f, 0f, TunnelZ - 2f));
        }

        /// <summary>Distance (in x/z) from a local point to the path's centre line.</summary>
        public float PathDistance(float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 0; i < PathPoints.Count; i += 2)
            {
                float dx = PathPoints[i].x - x, dz = PathPoints[i].z - z;
                float d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>The path's centre x at a given z (for the guide arrow and signposts).</summary>
        public float PathX(float z)
        {
            var best = PathPoints[0];
            foreach (var p in PathPoints) if (Mathf.Abs(p.z - z) < Mathf.Abs(best.z - z)) best = p;
            return best.x;
        }

        private bool InStructure(float x, float z, float margin) =>
            Mathf.Abs(x) < DeckHalfWidth + margin && z > StairsUpStart - margin && z < StairsDownEnd + margin;

        /// <summary>Past a passage mouth (inside the cliff, or the lowered land behind it): nothing grows there.</summary>
        private bool BeyondMouth(float z, float margin) =>
            z > TunnelZ - margin || (Entry != PassageStyle.None && z < EntryMouthZ + margin);

        private bool InClearing(float x, float z, float margin) =>
            (new Vector2(x, z) - ClearingCentre).magnitude < ClearingRadius + margin;

        // ---------- Terrain ----------

        private void BuildTerrain()
        {
            const int res = 257, splat = 256;
            var heights = new float[res, res];
            float seedX = R(0f, 100f), seedZ = R(0f, 100f);
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float x = TerrainCorner.x + ix / (float)(res - 1) * TerrainWidth;
                    float z = TerrainCorner.z + iz / (float)(res - 1) * TerrainLength;
                    // Rolling hills that grow away from the path; flat (y = 0) on and near it.
                    float n = Mathf.PerlinNoise(seedX + x * 0.025f, seedZ + z * 0.025f) * 6f
                              + Mathf.PerlinNoise(seedX + x * 0.09f, seedZ + z * 0.09f) * 1.2f;
                    float far = Mathf.Max(PathDistance(x, z) - 5f, 0f);
                    if (InClearing(x, z, 3f)) far = 0f;
                    float rise = Mathf.Clamp01(far / 18f);
                    float y = (n - 2.5f) * rise * rise + Mathf.PerlinNoise(seedX + x * 0.3f, seedZ + z * 0.3f) * 0.25f;
                    // Behind the passage mouths the land drops away: the cliff hides the dip, and the passage's rock is
                    // never cut by a hill.
                    y = Mathf.Lerp(y, -3f, Lowered(z));
                    heights[iz, ix] = Mathf.Clamp01((y + TerrainBase) / TerrainHeight);
                }

            var maps = new float[splat, splat, 3];
            for (int iz = 0; iz < splat; iz++)
                for (int ix = 0; ix < splat; ix++)
                {
                    float x = TerrainCorner.x + (ix + 0.5f) / splat * TerrainWidth;
                    float z = TerrainCorner.z + (iz + 0.5f) / splat * TerrainLength;
                    float d = PathDistance(x, z);
                    float path = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.1f, 2.3f, d));
                    if (InClearing(x, z, 0f)) path = Mathf.Max(path, 0.35f * Mathf.PerlinNoise(x * 0.3f, z * 0.3f));
                    float grass = Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(seedX + x * 0.08f, seedZ + z * 0.08f) * 1.5f + 0.15f);
                    grass *= 1f - path;
                    maps[iz, ix, 2] = path;
                    maps[iz, ix, 1] = grass;
                    maps[iz, ix, 0] = Mathf.Max(0f, 1f - path - grass);
                }

            var data = new TerrainData { heightmapResolution = res, alphamapResolution = splat };
            data.size = new Vector3(TerrainWidth, TerrainHeight, TerrainLength);
            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[]
            {
                Resources.Load<TerrainLayer>("Forest/Layer_Ground"),
                Resources.Load<TerrainLayer>("Forest/Layer_Grass"),
                Resources.Load<TerrainLayer>("Forest/Layer_Path"),
            };
            data.SetAlphamaps(0, 0, maps);
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = TerrainCorner;
            Terrain = go.GetComponent<Terrain>();
            Terrain.materialTemplate = Resources.Load<Material>("Forest/Terrain");
            Terrain.heightmapPixelError = 6f;
            Terrain.basemapDistance = 80f;
            Terrain.drawInstanced = false; // the instanced terrain variant is stripped from builds (the ground went flat)
            Terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = go.GetComponent<TerrainCollider>();
            if (col != null) Destroy(col);
        }

        /// <summary>0 on open land, 1 behind a passage mouth (beyond the exit cliff, before the entry cliff).</summary>
        private float Lowered(float z)
        {
            float l = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TunnelZ + 1f, TunnelZ + 4f, z));
            if (Entry != PassageStyle.None) l = Mathf.Max(l, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(EntryMouthZ + 1f, EntryMouthZ - 2f, z)));
            return l;
        }

        /// <summary>The ground height at a local point (terrain only).</summary>
        public float TerrainY(float x, float z) => Terrain.SampleHeight(ToWorld(new Vector3(x, 0f, z))) + Terrain.transform.position.y - Origin.y;

        // ---------- Stairs, deck, signposts, tunnel ----------

        private Material Mat(string name) => Resources.Load<Material>("Forest/" + name);

        private void BuildStructures()
        {
            var stone = Mat("Rock_Mossy");
            var wood = Mat("Bark_Oak");
            int steps = 8;
            float stepDepth = (DeckStart - StairsUpStart) / steps;
            for (int i = 0; i < steps; i++)
            {
                float h = DeckHeight * (i + 1) / steps;
                Block("StepUp", new Vector3(0f, h * 0.5f, StairsUpStart + stepDepth * (i + 0.5f)), new Vector3(StairsHalfWidth * 2f, h, stepDepth), stone, 1.2f);
                Block("StepDown", new Vector3(0f, h * 0.5f, StairsDownEnd - stepDepth * (i + 0.5f)), new Vector3(StairsHalfWidth * 2f, h, stepDepth), stone, 1.2f);
            }
            // The deck: stone flags on a rim slab and arched piers, two metres a cell. From the second leg on, holes
            // open in it (jump them or go round).
            int cellsX = Mathf.FloorToInt(DeckHalfWidth);
            int cellsZ = Mathf.FloorToInt((DeckEnd - DeckStart) / 2f);
            var holes = new HashSet<(int, int)>();
            int gapCount = Mathf.Min((Leg - 1) * 2, 7);
            for (int g = 0, tries = 0; g < gapCount && tries < 50; tries++)
            {
                int cz = rng.Next(2, cellsZ - 2), cx = rng.Next(0, cellsX - 1);
                if (holes.Contains((cx, cz)) || holes.Contains((cx, cz - 1)) || holes.Contains((cx, cz + 1))) continue;
                holes.Add((cx, cz));
                holes.Add((cx + 1, cz));
                Gaps.Add(new Rect(-cellsX + cx * 2f, DeckStart + cz * 2f, 4f, 2f));
                g++;
            }
            for (int cz = 0; cz < cellsZ; cz++)
                for (int cx = 0; cx < cellsX; cx++)
                {
                    if (holes.Contains((cx, cz))) continue;
                    float x = -cellsX + 1f + cx * 2f, z = DeckStart + 1f + cz * 2f;
                    Block("Rim", new Vector3(x, DeckHeight - 0.35f, z), new Vector3(2.02f, 0.7f, 2.02f), stone, 1.2f);
                    var flag = Block("Flag", new Vector3(x, DeckHeight + 0.04f, z), new Vector3(1.92f, 0.1f, 1.92f), stone, 0.7f);
                    flag.transform.localRotation = Quaternion.Euler(0f, R(-1.5f, 1.5f), 0f);
                }
            DeckHalfWidth = cellsX; // the walkable edge is where the flags end
            for (float z = DeckStart + 3f; z < DeckEnd - 1f; z += 6f)
                foreach (float x in new[] { -DeckHalfWidth + 0.5f, DeckHalfWidth - 0.5f })
                    Block("Pier", new Vector3(x, (DeckHeight - 0.7f) * 0.5f, z), new Vector3(1f, DeckHeight - 0.7f, 1.4f), stone, 1.4f);
            // Low parapet stones along the deck's edges, with gaps.
            for (float z = DeckStart + 2f; z < DeckEnd - 1f; z += 4f)
                foreach (float x in new[] { -DeckHalfWidth + 0.15f, DeckHalfWidth - 0.15f })
                    Block("Parapet", new Vector3(x, DeckHeight + 0.35f, z), new Vector3(0.3f, 0.6f, 2.4f), stone, 0.8f);

            // Signposts at the turns.
            Signpost(new Vector3(1.8f, 0f, 6f), 0f);
            Signpost(new Vector3(-2.6f, 0f, 46f), 0f);
            Signpost(new Vector3(2.4f, 0f, StairsUpStart - 3f), 0f);
            Signpost(new Vector3(2.6f, 0f, 145f + Shift), 0f);
            Signpost(new Vector3(-2.8f, 0f, 196f + Shift), 0f);

            BuildMouth(Exit, TunnelZ, 1f);
            if (Entry != PassageStyle.None) BuildMouth(Entry, EntryMouthZ, -1f);
        }

        private GameObject Block(string name, Vector3 local, Vector3 size, Material mat, float tile)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            // Tile the texture by size so stones keep their scale.
            var mpb = new MaterialPropertyBlock();
            mpb.SetVector("_BaseMap_ST", new Vector4(Mathf.Max(size.x, size.z) / (tile * 2f), size.y / (tile * 2f) + 0.2f, 0f, 0f));
            r.SetPropertyBlock(mpb);
            return go;
        }

        /// <summary>A weathered wooden post with an arrow board pointing along the path.</summary>
        private void Signpost(Vector3 local, float yaw)
        {
            local.y = TerrainY(local.x, local.z);
            var wood = Mat("Bark_Oak");
            var root = new GameObject("Signpost").transform;
            root.SetParent(transform, false);
            root.localPosition = local;
            float ahead = PathX(local.z + 6f) - PathX(local.z);
            root.localRotation = Quaternion.Euler(0f, Mathf.Atan2(ahead, 6f) * Mathf.Rad2Deg, 0f);
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(post.GetComponent<Collider>());
            post.transform.SetParent(root, false);
            post.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            post.transform.localScale = new Vector3(0.12f, 0.75f, 0.12f);
            post.GetComponent<MeshRenderer>().sharedMaterial = wood;
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(board.GetComponent<Collider>());
            board.transform.SetParent(root, false);
            board.transform.localPosition = new Vector3(0f, 1.25f, 0.25f);
            board.transform.localScale = new Vector3(0.08f, 0.26f, 0.8f);
            board.GetComponent<MeshRenderer>().sharedMaterial = wood;
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(tip.GetComponent<Collider>());
            tip.transform.SetParent(root, false);
            tip.transform.localPosition = new Vector3(0f, 1.25f, 0.68f);
            tip.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            tip.transform.localScale = new Vector3(0.08f, 0.26f, 0.26f);
            tip.GetComponent<MeshRenderer>().sharedMaterial = wood;
            Trunks.Add(new Vector3(local.x, local.z, 0.25f));
        }

        /// <summary>Half the passage's inside width and its ceiling (the same as the runner's realistic shell).</summary>
        public const float PassageHalf = 2.1f, PassageCeiling = 3.3f, MouthDepth = 12f;

        /// <summary>
        /// A passage mouth in a wide rock cliff at <paramref name="z"/>, the passage running on in direction
        /// <paramref name="dir"/> (+1: the way on, -1: the way the walker came in). A cave is a dark opening under a
        /// lintel with torches either side; a gorge is a tall cleft open to the sky. The cliff is deep enough that its
        /// inner faces are the passage's walls until the runner's own walls take over.
        /// </summary>
        private void BuildMouth(PassageStyle style, float z, float dir)
        {
            if (style == PassageStyle.None) return;
            bool cave = style == PassageStyle.Cave;
            var stone = Mat("Rock_Mossy");
            float mid = z + dir * MouthDepth * 0.5f;
            float noise = R(0f, 50f);
            // Columns of rock out to both sides, the innermost ones flush with the passage.
            foreach (float side in new[] { -1f, 1f })
            {
                float x = PassageHalf + 0.06f; // just behind the ride's own walls where they overlap
                for (int i = 0; x < 62f; i++)
                {
                    float w = i == 0 ? 2.4f : R(3f, 4.6f);
                    float h = (cave ? 9f : 15f) + Mathf.PerlinNoise(noise + x * 0.12f, side * 3f) * (cave ? 6f : 7f) - Mathf.Max(0f, x - 30f) * 0.12f;
                    float lean = i == 0 ? 0f : R(-0.6f, 0.6f);
                    var col = Block("Cliff", new Vector3(side * (x + w * 0.5f), h * 0.5f - 4f, mid + lean), new Vector3(w, h + 4f, MouthDepth + R(0f, 2f) * (i == 0 ? 0f : 1f)), stone, 1.6f);
                    if (i > 0) col.transform.localRotation = Quaternion.Euler(R(-2f, 2f), R(-5f, 5f), R(-3f, 3f));
                    if (!cave && i == 0)
                    {
                        // A gorge's walls step back as they rise: open to the sky.
                        float up = R(8f, 11f);
                        Block("CliffUpper", new Vector3(side * (x + 0.9f + w * 0.5f), h + up * 0.5f - 1f, mid), new Vector3(w, up, MouthDepth), stone, 1.6f);
                    }
                    x += w * 0.92f;
                }
            }
            if (cave)
            {
                float top = 11f + R(0f, 3f);
                Block("Lintel", new Vector3(0f, PassageCeiling + (top - PassageCeiling) * 0.5f, mid), new Vector3(PassageHalf * 2f + 0.6f, top - PassageCeiling, MouthDepth), stone, 1.6f);
            }
            // Loose mossy boulders round the opening and along the cliff's foot hide the blocks' straight edges.
            var rocks = Resources.Load<GameObject>("Forest/Models/rock_moss_set_01_1k");
            var rockMat = Mat("Rock_Set");
            float face = z - dir * 0.6f;
            if (rocks != null)
            {
                int part = 0;
                foreach (float side in new[] { -1f, 1f })
                {
                    PlaceModelPart(rocks, rockMat, part++, new Vector3(side * (PassageHalf + 2.8f), -0.2f, face - dir * 0.4f), R(1.8f, 2.3f), R(0f, 360f));
                    if (cave) PlaceModelPart(rocks, rockMat, part++, new Vector3(side * 1.6f, PassageCeiling + 0.5f, face + dir * 0.3f), R(1.1f, 1.4f), R(0f, 360f));
                    for (int k = 0; k < 9; k++)
                    {
                        float x = side * R(6f, 40f);
                        PlaceModelPart(rocks, rockMat, part++, new Vector3(x, TerrainY(x, face - dir * 1.5f) - 0.3f, face - dir * R(0.5f, 2f)), R(1.8f, 3.6f), R(0f, 360f));
                        // Ledges higher up the face break its flat front.
                        if (k % 2 == 0) PlaceModelPart(rocks, rockMat, part++, new Vector3(x * 0.9f, R(2.5f, 7f), face + dir * 0.1f), R(1.6f, 2.8f), R(0f, 360f));
                    }
                }
            }
            // The passage floor (bare rock, level with the path).
            Block("PassageFloor", new Vector3(0f, -0.2f, mid), new Vector3(PassageHalf * 2f, 0.4f, MouthDepth), stone, 1.2f);
            if (cave)
            {
                // Torches: a pair at the mouth, a pair inside, each a warm flickering light.
                foreach (float side in new[] { -1f, 1f })
                {
                    Torch(new Vector3(side * (PassageHalf + 0.35f), 2.1f, face - dir * 0.15f));
                    Torch(new Vector3(side * (PassageHalf - 0.12f), 2.1f, z + dir * 6f));
                }
            }
            else
            {
                // Ferns and grass cling to a gorge's ledges.
                var fern = Resources.Load<GameObject>("Forest/Models/fern_02_1k");
                if (fern != null)
                    for (int k = 0; k < 8; k++)
                    {
                        float side = k % 2 == 0 ? -1f : 1f;
                        PlaceModelPart(fern, Mat("Fern"), k, new Vector3(side * (PassageHalf + R(1.2f, 2.4f)), R(13f, 17f), z + dir * R(0.5f, MouthDepth - 1f)), R(1.6f, 2.4f), R(0f, 360f));
                    }
            }
        }

        private Material torchMat;

        private void Torch(Vector3 local)
        {
            if (torchMat == null) torchMat = MaterialFactory.Create(new Color(1f, 0.7f, 0.35f), new Color(4f, 2.1f, 0.6f));
            var root = new GameObject("Torch").transform;
            root.SetParent(transform, false);
            root.localPosition = local;
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stick.GetComponent<Collider>());
            stick.transform.SetParent(root, false);
            stick.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            stick.transform.localScale = new Vector3(0.07f, 0.25f, 0.07f);
            stick.GetComponent<MeshRenderer>().sharedMaterial = Mat("Bark_Oak");
            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(flame.GetComponent<Collider>());
            flame.transform.SetParent(root, false);
            flame.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            flame.transform.localScale = new Vector3(0.16f, 0.24f, 0.16f);
            var fr = flame.GetComponent<MeshRenderer>();
            fr.sharedMaterial = torchMat;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = root.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = 7f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
            root.gameObject.AddComponent<TorchFlicker>();
        }

        /// <summary>True where solid rock stands (a cliff beside a passage), for the walker's collisions.</summary>
        public bool InRock(float x, float z)
        {
            if (Exit != PassageStyle.None && z > TunnelZ - 0.5f && z < TunnelZ + MouthDepth + 0.5f && Mathf.Abs(x) > PassageHalf - 0.5f) return true;
            if (Entry != PassageStyle.None && z < EntryMouthZ + 0.5f && Mathf.Abs(x) > PassageHalf - 0.5f) return true;
            return false;
        }

        // ---------- Plants and props ----------

        private void Scatter()
        {
            var fir = Mat("Bark_Pine");
            var twig = Mat("Fir_Twig");
            var oak = Mat("Bark_Oak");
            var leaves = Mat("Leaves");
            int placed = 0;
            for (int tries = 0; tries < 2000 && placed < 150; tries++)
            {
                float z = R(-30f, TunnelZ + 30f);
                float x = R(-55f, 55f);
                float d = PathDistance(x, z);
                if (d < 4.2f || InStructure(x, z, 2.5f) || InClearing(x, z, 1.5f) || BeyondMouth(z, 1f)) continue;
                // Denser near the path (where it is seen), thinning out into the hills.
                if (rng.NextDouble() > Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(4f, 40f, d))) continue;
                if (TooClose(x, z, 3.2f)) continue;
                bool conifer = rng.NextDouble() < 0.68f;
                var mesh = conifer ? ForestTrees.Fir(rng.Next(3)) : ForestTrees.Broadleaf(rng.Next(3));
                var go = new GameObject(conifer ? "Fir" : "Broadleaf");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(x, TerrainY(x, z) - 0.15f, z);
                go.transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * R(0.8f, 1.35f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = conifer ? new[] { fir, twig } : new[] { oak, leaves };
                go.AddComponent<TreeSway>();
                Trunks.Add(new Vector3(x, z, 0.45f * go.transform.localScale.x));
                placed++;
            }

            // Undergrowth from the photo-scanned models, each variant of a set used on its own.
            var grass = Resources.Load<GameObject>("Forest/Models/grass_medium_01_1k");
            var fern = Resources.Load<GameObject>("Forest/Models/fern_02_1k");
            var shrub3 = Resources.Load<GameObject>("Forest/Models/shrub_03_1k");
            var shrub4 = Resources.Load<GameObject>("Forest/Models/shrub_04_1k");
            var rock = Resources.Load<GameObject>("Forest/Models/rock_moss_set_01_1k");
            var stump = Resources.Load<GameObject>("Forest/Models/tree_stump_01_1k");
            Undergrowth(grass, Mat("Grass"), 420, 1.4f, 11f, 2.8f, 4.2f);
            Undergrowth(fern, Mat("Fern"), 70, 2.2f, 12f, 1.4f, 2.2f);
            Undergrowth(shrub3, Mat("Shrub_03"), 40, 2.8f, 14f, 3f, 5f);
            Undergrowth(shrub4, Mat("Shrub_04"), 30, 3f, 14f, 3.5f, 6f);
            Undergrowth(rock, Mat("Rock_Set"), 16, 3.5f, 20f, 0.35f, 0.8f);
            Undergrowth(stump, Mat("Stump"), 3, 4f, 12f, 0.8f, 1f);
        }

        private bool TooClose(float x, float z, float min)
        {
            foreach (var t in Trunks)
                if ((t.x - x) * (t.x - x) + (t.y - z) * (t.y - z) < min * min) return true;
            return false;
        }

        private void Undergrowth(GameObject model, Material mat, int count, float minD, float maxD, float minScale, float maxScale)
        {
            if (model == null) return;
            int parts = model.GetComponentsInChildren<MeshFilter>().Length;
            for (int i = 0, tries = 0; i < count && tries < count * 20; tries++)
            {
                float z = R(-20f, TunnelZ + 10f);
                float side = rng.NextDouble() < 0.5f ? -1f : 1f;
                float x = PathX(z) + side * R(minD, maxD);
                if (InStructure(x, z, 0.8f) || PathDistance(x, z) < minD * 0.9f || BeyondMouth(z, 0.6f)) continue;
                PlaceModelPart(model, mat, rng.Next(parts), new Vector3(x, TerrainY(x, z), z), R(minScale, maxScale), R(0f, 360f));
                i++;
            }
        }

        /// <summary>
        /// One mesh of a multi-part model, standing on <paramref name="local"/> (its lowest point on the ground),
        /// keeping the model's own orientation (Blender's axis conversion) and turned by <paramref name="yaw"/>.
        /// </summary>
        private void PlaceModelPart(GameObject model, Material mat, int part, Vector3 local, float scale, float yaw)
        {
            var filters = model.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) return;
            var src = filters[part % filters.Length];
            var go = new GameObject(model.name + "_" + part);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = src.sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The part's own rotation and scale inside the model, then the yaw on top.
            var rel = Quaternion.Inverse(model.transform.rotation) * src.transform.rotation;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * rel;
            go.transform.localScale = Vector3.Scale(src.transform.lossyScale, model.transform.localScale.Reciprocal()) * scale;
            go.transform.localPosition = Vector3.zero;
            // Re-centre: the mesh's footprint centre over the spot, its lowest point on the ground.
            var b = mr.bounds;
            var offset = go.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.localPosition = local + offset;
        }
    }

    /// <summary>A torch flame's flicker: its light wavers in brightness and its flame in size.</summary>
    public class TorchFlicker : MonoBehaviour
    {
        private Light glow;
        private Transform flame;
        private float phase;

        private void Start()
        {
            glow = GetComponent<Light>();
            flame = transform.childCount > 1 ? transform.GetChild(1) : null;
            phase = Random.Range(0f, 10f);
        }

        private void Update()
        {
            float t = Time.time * 9f + phase;
            float f = 0.85f + Mathf.PerlinNoise(t, phase) * 0.3f;
            if (glow != null) glow.intensity = 2.2f * f;
            if (flame != null) flame.localScale = new Vector3(0.16f, 0.24f * f, 0.16f);
        }
    }

    /// <summary>A gentle sway in the wind (the whole tree leans a little, each at its own pace).</summary>
    public class TreeSway : MonoBehaviour
    {
        private Quaternion rest;
        private float phase, speed;

        private void Start()
        {
            rest = transform.localRotation;
            phase = Random.Range(0f, 10f);
            speed = Random.Range(0.6f, 1f);
        }

        private void Update()
        {
            float t = Time.time * speed + phase;
            transform.localRotation = rest * Quaternion.Euler(Mathf.Sin(t) * 0.7f, 0f, Mathf.Sin(t * 0.73f + 1.3f) * 0.6f);
        }
    }

    internal static class VectorExtensions
    {
        public static Vector3 Reciprocal(this Vector3 v) => new Vector3(1f / v.x, 1f / v.y, 1f / v.z);
    }
}
