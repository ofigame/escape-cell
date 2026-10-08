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
        /// <summary>Far from the level stage so nothing of it is ever in view.</summary>
        public static readonly Vector3 Origin = new Vector3(5000f, 0f, 0f);

        // The course (local coordinates, metres).
        public const float DeckHeight = 3.2f;
        public const float StairsUpStart = 80f, DeckStart = 88f, DeckEnd = 128f, StairsDownEnd = 136f;
        public const float DeckHalfWidth = 4.5f, StairsHalfWidth = 1.6f;
        public static readonly Vector2 ClearingCentre = new Vector2(0f, 160f);
        public const float ClearingRadius = 11f;
        public const float TunnelZ = 212f;

        private const float TerrainWidth = 170f, TerrainLength = 270f, TerrainHeight = 24f, TerrainBase = 6f;
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

        public static ForestWorld Build(int seed)
        {
            var go = new GameObject("ForestWorld");
            go.transform.position = Origin;
            var w = go.AddComponent<ForestWorld>();
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
                new Vector3(0f, 0f, StairsDownEnd), new Vector3(0f, 0f, 140f), new Vector3(3f, 0f, 150f), new Vector3(0f, 0f, 160f),
                new Vector3(-3f, 0f, 176f), new Vector3(2.5f, 0f, 192f), new Vector3(0f, 0f, 205f), new Vector3(0f, 0f, TunnelZ + 4f),
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
            // The deck: a slab of stone flags on arched piers.
            float len = DeckEnd - DeckStart;
            Block("DeckRim", new Vector3(0f, DeckHeight - 0.35f, (DeckStart + DeckEnd) * 0.5f), new Vector3(DeckHalfWidth * 2f + 0.3f, 0.7f, len), stone, 3f);
            for (float z = DeckStart + 1f; z < DeckEnd; z += 2f)
                for (float x = -DeckHalfWidth + 1f; x < DeckHalfWidth; x += 2f)
                {
                    var flag = Block("Flag", new Vector3(x, DeckHeight + 0.04f, z), new Vector3(1.92f, 0.1f, 1.92f), stone, 0.7f);
                    flag.transform.localRotation = Quaternion.Euler(0f, R(-1.5f, 1.5f), 0f);
                }
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
            Signpost(new Vector3(2.6f, 0f, 145f), 0f);
            Signpost(new Vector3(-2.8f, 0f, 196f), 0f);

            BuildTunnelMouth();
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

        /// <summary>The way on: a rocky outcrop with a dark arch, where the tunnel begins.</summary>
        private void BuildTunnelMouth()
        {
            var rocks = Resources.Load<GameObject>("Forest/Models/rock_moss_set_01_1k");
            var mat = Mat("Rock_Set");
            float y = TerrainY(0f, TunnelZ);
            var placements = new[]
            {
                (new Vector3(-4.2f, 0f, TunnelZ + 1f), 3.2f, 20f), (new Vector3(4.2f, 0f, TunnelZ + 1.2f), 3.4f, -30f),
                (new Vector3(0f, 3.4f, TunnelZ + 2f), 3.6f, 90f), (new Vector3(-7f, 0f, TunnelZ + 4f), 3.8f, 140f),
                (new Vector3(7f, 0f, TunnelZ + 4f), 4f, 200f), (new Vector3(0f, 1f, TunnelZ + 7f), 5f, 10f),
            };
            int part = 0;
            foreach (var (pos, scale, yaw) in placements)
                PlaceModelPart(rocks, mat, part++, new Vector3(pos.x, y + pos.y, pos.z), scale, yaw);
            // The dark opening.
            var hole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(hole.GetComponent<Collider>());
            hole.name = "TunnelDark";
            hole.transform.SetParent(transform, false);
            hole.transform.localPosition = new Vector3(0f, y + 1.6f, TunnelZ + 2.2f);
            hole.transform.localScale = new Vector3(3.6f, 3.2f, 1f);
            hole.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.Create(new Color(0.02f, 0.02f, 0.03f), Color.black);
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
                if (d < 4.2f || InStructure(x, z, 2.5f) || InClearing(x, z, 1.5f)) continue;
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
                if (InStructure(x, z, 0.8f) || PathDistance(x, z) < minD * 0.9f) continue;
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
