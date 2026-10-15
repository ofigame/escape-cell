using System.Collections.Generic;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.OpenWorld
{
    /// <summary>
    /// The Red Desert's land: 7 × 7 regions of 48 units, desert at the middle (where foi came out: scrap heaps, fallen
    /// robots) turning to dry grass and then green farmland further out (trees, bushes, neighbours' farms). Only the
    /// regions round foi are built (3 × 3); each is one ground mesh coloured by a small texture (no seams between them)
    /// and its props, merged for speed, and destroyed when foi walks away.
    /// </summary>
    public class OwWorld : MonoBehaviour
    {
        public const int Regions = 7;
        public const float RegionSize = 48f;
        public const float Size = Regions * RegionSize;
        /// <summary>Far from the game's own scene.</summary>
        public static readonly Vector3 Origin = new Vector3(20000f, 0f, 20000f);
        public static readonly Vector2 Centre = new Vector2(Size * 0.5f, Size * 0.5f);

        /// <summary>The player's farm (its land's middle) and the co-op, in map units.</summary>
        /// <summary>The safe spots a farm can stand on (north, east, west, south of the middle, out on the green).</summary>
        public static readonly Vector2[] Sites =
        {
            new Vector2(Size * 0.5f, Size * 0.5f + 66f), new Vector2(Size * 0.5f + 66f, Size * 0.5f),
            new Vector2(Size * 0.5f - 66f, Size * 0.5f), new Vector2(Size * 0.5f, Size * 0.5f - 68f),
        };

        /// <summary>The farm's site now (set from the save before the world is built).</summary>
        public static int Site;
        public static Vector2 FarmCentre => Sites[Mathf.Clamp(Site, 0, Sites.Length - 1)];
        public static readonly Vector2 CoopAt = new Vector2(Size * 0.5f, Size * 0.5f + 14f);
        public static readonly Vector2 Arrival = new Vector2(Size * 0.5f, Size * 0.5f - 6f);

        /// <summary>The neighbours' farms (the safe green regions round the middle).</summary>
        public static readonly Vector2[] Neighbours =
        {
            new Vector2(Size * 0.5f - 58f, Size * 0.5f + 52f), new Vector2(Size * 0.5f + 60f, Size * 0.5f + 50f),
            new Vector2(Size * 0.5f - 62f, Size * 0.5f - 50f), new Vector2(Size * 0.5f + 56f, Size * 0.5f - 56f),
        };

        private readonly Dictionary<Vector2Int, Transform> loaded = new Dictionary<Vector2Int, Transform>();
        private readonly List<Material> mats = new List<Material>();
        private Material sand, rock, rust, metal, dark, wood, leaf, leafDark, wall, roof, barnRed, soil, crop, wheat, glass;

        /// <summary>Round obstacles foi walks round (map units: x, z, radius), for the regions built now.</summary>
        public readonly List<Vector3> Blockers = new List<Vector3>();
        private readonly Dictionary<Vector2Int, List<Vector3>> regionBlockers = new Dictionary<Vector2Int, List<Vector3>>();

        /// <summary>The land under the farm and the co-op is levelled (how far round each).</summary>
        public static float FarmFlat = 26f, CoopFlat = 16f;

        public static OwWorld Create(Transform parent)
        {
            var go = new GameObject("OwWorld");
            go.transform.SetParent(parent, false);
            go.transform.position = Origin;
            var w = go.AddComponent<OwWorld>();
            w.Materials();
            return w;
        }

        private Material M(Color c, float glow = 0f) { var m = MaterialFactory.Create(c, c * glow); mats.Add(m); return m; }

        private void Materials()
        {
            sand = M(new Color(0.86f, 0.42f, 0.26f));
            rock = M(new Color(0.55f, 0.27f, 0.18f));
            rust = M(new Color(0.42f, 0.24f, 0.16f));
            metal = M(new Color(0.5f, 0.48f, 0.46f));
            dark = M(new Color(0.22f, 0.18f, 0.18f));
            wood = M(new Color(0.5f, 0.34f, 0.2f));
            leaf = M(new Color(0.3f, 0.58f, 0.26f));
            leafDark = M(new Color(0.22f, 0.45f, 0.22f));
            wall = M(new Color(0.95f, 0.92f, 0.85f));
            roof = M(new Color(0.78f, 0.24f, 0.18f));
            barnRed = M(new Color(0.7f, 0.18f, 0.14f));
            soil = M(new Color(0.46f, 0.3f, 0.18f));
            crop = M(new Color(0.55f, 0.8f, 0.3f));
            wheat = M(new Color(0.92f, 0.78f, 0.36f));
            glass = M(new Color(0.55f, 0.75f, 0.9f), 0.3f);
        }

        private void OnDestroy()
        {
            foreach (var m in mats) if (m != null) Destroy(m);
        }

        // ---------- The land ----------

        /// <summary>0 in the desert, 1 on the green (with a ragged edge).</summary>
        public static float Green(float x, float z)
        {
            float d = Vector2.Distance(new Vector2(x, z), Centre);
            d += (Mathf.PerlinNoise(x * 0.03f + 11f, z * 0.03f + 5f) - 0.5f) * 18f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(34f, 64f, d));
        }

        private static float Raw(float x, float z)
        {
            float g = Green(x, z);
            float dunes = Mathf.PerlinNoise(x * 0.045f + 3.1f, z * 0.045f + 7.7f) * 2.4f + Mathf.PerlinNoise(x * 0.14f, z * 0.14f) * 0.3f;
            float hills = Mathf.PerlinNoise(x * 0.02f + 40f, z * 0.02f + 9f) * 2.2f;
            return Mathf.Lerp(dunes, hills, g) - 1.2f;
        }

        /// <summary>The ground's height at a map point (levelled round the farm and the co-op).</summary>
        public static float Height(float x, float z)
        {
            float h = Raw(x, z);
            h = Flatten(h, x, z, FarmCentre, FarmFlat);
            h = Flatten(h, x, z, CoopAt, CoopFlat);
            foreach (var n in Neighbours) h = Flatten(h, x, z, n, 14f);
            return h;
        }

        private static float Flatten(float h, float x, float z, Vector2 at, float r)
        {
            float d = Vector2.Distance(new Vector2(x, z), at);
            if (d >= r + 8f) return h;
            float flat = Raw(at.x, at.y);
            return Mathf.Lerp(flat, h, Mathf.SmoothStep(0f, 1f, (d - r) / 8f));
        }

        public static Vector3 World(float x, float z) => Origin + new Vector3(x, Height(x, z), z);
        public static Vector3 World(Vector2 p) => World(p.x, p.y);

        private static Color GroundColour(float x, float z)
        {
            float g = Green(x, z);
            var desert = new Color(0.86f, 0.42f, 0.26f);
            var dry = new Color(0.66f, 0.58f, 0.3f);
            var green = new Color(0.42f, 0.66f, 0.3f);
            float n = (Mathf.PerlinNoise(x * 0.2f, z * 0.2f) - 0.5f) * 0.06f;
            var c = g < 0.5f ? Color.Lerp(desert, dry, g * 2f) : Color.Lerp(dry, green, (g - 0.5f) * 2f);
            return new Color(c.r + n, c.g + n, c.b + n);
        }

        // ---------- Streaming ----------

        /// <summary>Drops every region (the farm moved: the land is levelled somewhere else now).</summary>
        public void Reload()
        {
            foreach (var kv in loaded) if (kv.Value != null) Destroy(kv.Value.gameObject);
            loaded.Clear();
            regionBlockers.Clear();
            Blockers.Clear();
        }

        /// <summary>Builds the regions round a map point and drops the far ones.</summary>
        public void Around(Vector2 p)
        {
            int rx = Mathf.Clamp(Mathf.FloorToInt(p.x / RegionSize), 0, Regions - 1);
            int rz = Mathf.Clamp(Mathf.FloorToInt(p.y / RegionSize), 0, Regions - 1);
            bool changed = false;
            var keep = new HashSet<Vector2Int>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var r = new Vector2Int(rx + dx, rz + dz);
                    if (r.x < 0 || r.y < 0 || r.x >= Regions || r.y >= Regions) continue;
                    keep.Add(r);
                    if (!loaded.ContainsKey(r)) { loaded[r] = BuildRegion(r); changed = true; }
                }
            var drop = new List<Vector2Int>();
            foreach (var kv in loaded) if (!keep.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var r in drop) { Destroy(loaded[r].gameObject); loaded.Remove(r); regionBlockers.Remove(r); changed = true; }
            if (!changed) return;
            Blockers.Clear();
            foreach (var kv in regionBlockers) Blockers.AddRange(kv.Value);
        }

        private Transform BuildRegion(Vector2Int r)
        {
            var root = new GameObject("Region " + r.x + "," + r.y).transform;
            root.SetParent(transform, false);
            float x0 = r.x * RegionSize, z0 = r.y * RegionSize;
            var blockers = new List<Vector3>();
            regionBlockers[r] = blockers;

            // The ground: 25 × 25 points, its colour from a 32 × 32 texture over it.
            const int n = 25;
            float step = RegionSize / (n - 1);
            var verts = new Vector3[n * n];
            var uv = new Vector2[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float wx = x0 + x * step, wz = z0 + z * step;
                    verts[z * n + x] = new Vector3(wx, Height(wx, wz), wz);
                    uv[z * n + x] = new Vector2(x / (float)(n - 1), z / (float)(n - 1));
                }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    tris[t++] = i; tris[t++] = i + n; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + n; tris[t++] = i + n + 1;
                }
            var mesh = new Mesh { vertices = verts, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            const int tex = 32;
            var colours = new Color[tex * tex];
            for (int z = 0; z < tex; z++)
                for (int x = 0; x < tex; x++)
                    colours[z * tex + x] = GroundColour(x0 + (x + 0.5f) / tex * RegionSize, z0 + (z + 0.5f) / tex * RegionSize);
            var texture = new Texture2D(tex, tex, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels(colours);
            texture.Apply(false, true);
            var groundMat = MaterialFactory.Create(Color.white, Color.black);
            groundMat.SetTexture("_BaseMap", texture);
            groundMat.mainTexture = texture;
            var ground = new GameObject("Ground");
            ground.transform.SetParent(root, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = groundMat;
            ground.AddComponent<OwnedMesh>().mesh = mesh;
            ground.AddComponent<RegionOwned>().Set(groundMat, texture);

            // Its props: scrap and wrecks in the desert, trees and bushes on the green.
            var rng = new System.Random(r.x * 97 + r.y * 13 + 7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var props = new GameObject("Props").transform;
            props.SetParent(root, false);
            for (int i = 0; i < 70; i++)
            {
                float x = x0 + R(1f, RegionSize - 1f), z = z0 + R(1f, RegionSize - 1f);
                if (Reserved(x, z)) continue;
                float g = Green(x, z);
                var at = new Vector3(x, Height(x, z), z);
                if (g < 0.4f)
                {
                    if (rng.NextDouble() < 0.6) Scrap(props, at, rng, blockers);
                    else Rock(props, at, rng, R(1f, 3f), blockers);
                }
                else if (g > 0.6f)
                {
                    if (rng.NextDouble() < 0.7) Tree(props, at, R(1.6f, 2.6f), blockers);
                    else Shapes.Primitive(PrimitiveType.Sphere, "Bush", props, at + Vector3.up * 0.3f, new Vector3(R(1f, 1.8f), R(0.7f, 1.1f), R(1f, 1.8f)), leafDark);
                }
                else if (rng.NextDouble() < 0.5) Rock(props, at, rng, R(0.6f, 1.4f), blockers);
            }
            // A few fallen robots in the desert regions.
            if (Green(x0 + RegionSize * 0.5f, z0 + RegionSize * 0.5f) < 0.3f)
                for (int i = 0; i < 3; i++)
                {
                    float x = x0 + R(4f, RegionSize - 4f), z = z0 + R(4f, RegionSize - 4f);
                    if (Reserved(x, z)) continue;
                    var bot = GuardBot.Build(props, new Color(0.45f, 0.28f, 0.2f), rng.Next(5), false, rng.Next(50));
                    bot.transform.localPosition = new Vector3(x, Height(x, z) - 0.3f, z);
                    bot.transform.localRotation = Quaternion.Euler(R(60f, 100f), R(0f, 360f), R(-20f, 20f));
                    bot.transform.localScale = Vector3.one * R(2.4f, 3.6f);
                    bot.enabled = false;
                    blockers.Add(new Vector3(x, z, 1.6f));
                }
            // The co-op in the middle, the neighbours' farms.
            if (Inside(CoopAt, x0, z0)) Coop(root, blockers);
            foreach (var nb in Neighbours) if (Inside(nb, x0, z0)) NeighbourFarm(root, nb, rng, blockers);
            StaticBatchingUtility.Combine(props.gameObject);
            return root;
        }

        private static bool Inside(Vector2 p, float x0, float z0) => p.x >= x0 && p.x < x0 + RegionSize && p.y >= z0 && p.y < z0 + RegionSize;

        /// <summary>Kept clear: the farm, the co-op, the neighbours, the arrival spot.</summary>
        private static bool Reserved(float x, float z)
        {
            var p = new Vector2(x, z);
            if (Vector2.Distance(p, FarmCentre) < FarmFlat + 4f) return true;
            if (Vector2.Distance(p, CoopAt) < CoopFlat + 2f) return true;
            if (Vector2.Distance(p, Arrival) < 5f) return true;
            var reg = OwRegions.Of(p);
            if (reg != OwRegions.Middle && Vector2.Distance(p, OwRegions.CampAt(reg)) < (reg == OwRegions.Fortress ? 18f : 14f)) return true; // a bandit camp's ground
            foreach (var n in Neighbours) if (Vector2.Distance(p, n) < 16f) return true;
            return false;
        }

        private void Scrap(Transform parent, Vector3 at, System.Random rng, List<Vector3> blockers)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var heap = new GameObject("Scrap").transform;
            heap.SetParent(parent, false);
            heap.localPosition = at;
            heap.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            int parts = rng.Next(4, 9);
            for (int p = 0; p < parts; p++)
            {
                var size = new Vector3(R(0.8f, 2.6f), R(0.5f, 1.8f), R(0.8f, 2.4f));
                Shapes.Rounded("Part", heap, new Vector3(R(-1.2f, 1.2f), size.y * 0.35f, R(-1.2f, 1.2f)), size, 0.08f, p % 3 == 0 ? metal : p % 2 == 0 ? dark : rust)
                    .transform.localRotation = Quaternion.Euler(R(-25f, 25f), R(0f, 90f), R(-25f, 25f));
            }
            blockers.Add(new Vector3(at.x, at.z, 1.8f));
        }

        private void Rock(Transform parent, Vector3 at, System.Random rng, float size, List<Vector3> blockers)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Shapes.Rounded("Rock", parent, at + Vector3.up * size * 0.3f, new Vector3(size * R(0.8f, 1.4f), size * R(0.5f, 1f), size * R(0.8f, 1.4f)), Mathf.Min(0.4f, size * 0.2f), rock)
                .transform.localRotation = Quaternion.Euler(R(-10f, 10f), R(0f, 90f), R(-10f, 10f));
            if (size > 1.2f) blockers.Add(new Vector3(at.x, at.z, size * 0.6f));
        }

        private void Tree(Transform parent, Vector3 at, float size, List<Vector3> blockers)
        {
            Shapes.Primitive(PrimitiveType.Cylinder, "Trunk", parent, at + new Vector3(0f, size * 0.4f, 0f), new Vector3(size * 0.16f, size * 0.4f, size * 0.16f), wood);
            Shapes.Primitive(PrimitiveType.Sphere, "Leaves", parent, at + new Vector3(0f, size * 1.05f, 0f), Vector3.one * size, leaf);
            blockers.Add(new Vector3(at.x, at.z, size * 0.25f));
        }

        private void Coop(Transform root, List<Vector3> blockers)
        {
            var at = new Vector3(CoopAt.x, Height(CoopAt.x, CoopAt.y), CoopAt.y);
            var c = new GameObject("Coop").transform;
            c.SetParent(root, false);
            c.localPosition = at;
            // A long market hall: wooden posts, a striped awning, crates and baskets of produce, a big sign.
            Shapes.Rounded("Floor", c, new Vector3(0f, 0.1f, 0f), new Vector3(14f, 0.2f, 8f), 0.05f, wood);
            Shapes.Rounded("Back", c, new Vector3(0f, 2f, 3.6f), new Vector3(14f, 4f, 0.6f), 0.05f, wall);
            for (int i = -3; i <= 3; i++)
                Shapes.Rounded("Post", c, new Vector3(i * 2.2f, 1.6f, -3.4f), new Vector3(0.3f, 3.2f, 0.3f), 0.05f, wood);
            for (int i = 0; i < 7; i++)
                Shapes.Rounded("Awning", c, new Vector3(-6f + i * 2f, 3.4f, -0.2f), new Vector3(2f, 0.2f, 7.6f), 0.05f, i % 2 == 0 ? roof : wall)
                    .transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Shapes.Rounded("Sign", c, new Vector3(0f, 4.6f, -3.3f), new Vector3(6f, 1.2f, 0.2f), 0.05f, barnRed);
            for (int i = -2; i <= 2; i++)
            {
                Shapes.Rounded("Crate", c, new Vector3(i * 2.4f, 0.6f, 1.6f), new Vector3(1.4f, 0.8f, 1f), 0.05f, wood);
                Shapes.Primitive(PrimitiveType.Sphere, "Goods", c, new Vector3(i * 2.4f, 1.05f, 1.6f), new Vector3(1.2f, 0.4f, 0.8f), i % 2 == 0 ? wheat : crop);
            }
            blockers.Add(new Vector3(CoopAt.x, CoopAt.y + 2.6f, 4f));
            blockers.Add(new Vector3(CoopAt.x - 4.5f, CoopAt.y + 2.6f, 3f));
            blockers.Add(new Vector3(CoopAt.x + 4.5f, CoopAt.y + 2.6f, 3f));
        }

        private void NeighbourFarm(Transform root, Vector2 p, System.Random rng, List<Vector3> blockers)
        {
            var f = new GameObject("NeighbourFarm").transform;
            f.SetParent(root, false);
            f.localPosition = new Vector3(p.x, Height(p.x, p.y), p.y);
            f.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            HouseModel(f, new Vector3(0f, 0f, 4f), 1.4f, wall, roof);
            Shapes.Rounded("Barn", f, new Vector3(7f, 2f, 3f), new Vector3(4.4f, 4f, 5.4f), 0.05f, barnRed);
            Shapes.Rounded("BarnRoof", f, new Vector3(7f, 4.2f, 3f), new Vector3(4.8f, 0.5f, 5.8f), 0.05f, wood);
            for (int r = 0; r < 8; r++)
            {
                Shapes.Rounded("Soil", f, new Vector3(-4f + r * 1.4f, 0.06f, -4f), new Vector3(1.1f, 0.14f, 6f), 0.03f, soil);
                Shapes.Rounded("Crop", f, new Vector3(-4f + r * 1.4f, 0.35f, -4f), new Vector3(0.7f, 0.5f, 5.6f), 0.15f, r % 3 == 0 ? wheat : crop);
            }
            for (int i = 0; i < 12; i++)
                Shapes.Rounded("Post", f, new Vector3(-5.5f + i * 1.2f, 0.5f, -7.6f), new Vector3(0.16f, 1f, 0.16f), 0.03f, wood);
            Shapes.Rounded("Rail", f, new Vector3(1f, 0.75f, -7.6f), new Vector3(13.4f, 0.12f, 0.12f), 0.03f, wood);
            var w = f.TransformPoint(new Vector3(0f, 0f, 4f)) - Origin;
            blockers.Add(new Vector3(w.x, w.z, 3f));
            var b = f.TransformPoint(new Vector3(7f, 0f, 3f)) - Origin;
            blockers.Add(new Vector3(b.x, b.z, 3f));
        }

        /// <summary>A farmhouse: white walls, a pitched red roof, a door, windows, a chimney (scaled).</summary>
        public void HouseModel(Transform parent, Vector3 at, float s, Material walls, Material roofMat)
        {
            var h = new GameObject("House").transform;
            h.SetParent(parent, false);
            h.localPosition = at;
            h.localScale = Vector3.one * s;
            Shapes.Rounded("Walls", h, new Vector3(0f, 1.2f, 0f), new Vector3(4f, 2.4f, 3.2f), 0.05f, walls);
            foreach (float side in new[] { -1f, 1f })
                Shapes.Rounded("Roof", h, new Vector3(0f, 2.9f, side * 0.85f), new Vector3(4.4f, 0.25f, 2.2f), 0.05f, roofMat).transform.localRotation = Quaternion.Euler(side * 32f, 0f, 0f);
            Shapes.Rounded("Door", h, new Vector3(0f, 0.7f, -1.62f), new Vector3(0.8f, 1.4f, 0.06f), 0.03f, wood);
            foreach (float side in new[] { -1.2f, 1.2f })
                Shapes.Rounded("Window", h, new Vector3(side, 1.5f, -1.62f), new Vector3(0.7f, 0.6f, 0.05f), 0.03f, glass);
            Shapes.Rounded("Chimney", h, new Vector3(1.2f, 3.4f, 0.4f), new Vector3(0.4f, 1f, 0.4f), 0.05f, dark);
        }

        public Material Wall => wall;
        public Material Roof => roof;
        public Material Wood => wood;
        public Material Soil => soil;
        public Material Leaf => leaf;
        public Material BarnRed => barnRed;
        public Material Metal => metal;
        public Material Dark => dark;

        /// <summary>A region's own ground material and texture (destroyed with it).</summary>
        private class RegionOwned : MonoBehaviour
        {
            private Material mat;
            private Texture2D tex;
            public void Set(Material m, Texture2D t) { mat = m; tex = t; }
            private void OnDestroy() { if (mat != null) Destroy(mat); if (tex != null) Destroy(tex); }
        }
    }
}
