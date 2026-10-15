using System.Collections.Generic;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.OpenWorld
{
    /// <summary>
    /// The Red Desert's land: 7 × 7 regions of 108 units, desert at the middle (where foi came out: scrap heaps, fallen
    /// robots) turning to dry grass and then rolling green hills further out. A small town stands at the desert's edge
    /// round the co-op (two streets, a square with a fountain, a clock tower); dirt roads run from it out to the farm
    /// sites and round a ring road lined with the neighbours' farms, their fields in rows. Only the regions round foi
    /// are built (3 × 3, one more each moment); each is one ground mesh coloured by its own texture (no seams), tufts
    /// of grass, and its props, merged for speed, and destroyed when foi walks away.
    /// </summary>
    public class OwWorld : MonoBehaviour
    {
        public const int Regions = 7;
        public const float RegionSize = 108f;
        public const float Size = Regions * RegionSize;
        /// <summary>Far from the game's own scene.</summary>
        public static readonly Vector3 Origin = new Vector3(20000f, 0f, 20000f);
        public static readonly Vector2 Centre = new Vector2(Size * 0.5f, Size * 0.5f);

        /// <summary>The safe spots a farm can stand on (east, west, south-east, south-west of the middle, on the green).</summary>
        public static readonly Vector2[] Sites =
        {
            Centre + new Vector2(148f, 4f), Centre + new Vector2(-148f, 4f),
            Centre + new Vector2(100f, -106f), Centre + new Vector2(-100f, -106f),
        };

        /// <summary>The farm's site now (set from the save before the world is built).</summary>
        public static int Site;
        public static Vector2 FarmCentre => Sites[Mathf.Clamp(Site, 0, Sites.Length - 1)];
        /// <summary>The co-op's hall at the head of the town's square; the square; foi's arrival in the desert.</summary>
        public static readonly Vector2 CoopAt = Centre + new Vector2(0f, 126f);
        public static readonly Vector2 Plaza = CoopAt - new Vector2(0f, 14f);
        public static readonly Vector2 Arrival = Centre - new Vector2(0f, 6f);
        private static readonly Vector2 TownAt = Plaza - new Vector2(0f, 22f);
        private const float TownFlat = 64f, RingRoad = 192f, RoadHalf = 2.4f;

        /// <summary>A neighbour's farm: where, which way its road lies, which crops its fields grow.</summary>
        public struct Farmstead { public Vector2 at, toRoad, side; public int seed; }
        public static readonly List<Farmstead> Neighbours = new List<Farmstead>();
        /// <summary>The town's houses: where, the way their doors face, how tall.</summary>
        private struct TownHouse { public Vector2 at, door; public bool tall; public int look; }
        private static readonly List<TownHouse> Houses = new List<TownHouse>();
        private static readonly List<Vector2> Lamps = new List<Vector2>();
        private static readonly List<(Vector2 a, Vector2 b)> Roads = new List<(Vector2, Vector2)>();
        /// <summary>Ground levelled flat (where, how far): the town, the neighbours, the bandits' camps.</summary>
        private static readonly List<(Vector2 at, float r, float edge)> Flats = new List<(Vector2, float, float)>();

        static OwWorld()
        {
            // Roads: the desert track into town and on through its square, from town and from the desert to each site
            // (stopping short of the farm), the town's cross street.
            Roads.Add((Arrival, Plaza));
            Roads.Add((Plaza - new Vector2(66f, 0f), Plaza + new Vector2(66f, 0f)));
            void ToSite(Vector2 from, Vector2 site) { var d = (site - from).normalized; Roads.Add((from, site - d * 40f)); }
            ToSite(Plaza + new Vector2(66f, 0f), Sites[0]);
            ToSite(Plaza - new Vector2(66f, 0f), Sites[1]);
            ToSite(Arrival, Sites[2]);
            ToSite(Arrival, Sites[3]);

            // The town: houses down the main street and along the cross street, doors to the road; lamps between.
            int look = 0;
            for (int i = 0; i < 5; i++)
                foreach (float s in new[] { -1f, 1f })
                    Houses.Add(new TownHouse { at = Plaza + new Vector2(s * 10f, -22f - i * 11f), door = new Vector2(-s, 0f), tall = (i + (s > 0 ? 1 : 0)) % 2 == 0, look = look++ });
            for (int i = 0; i < 5; i++)
                foreach (float s in new[] { -1f, 1f })
                    foreach (float side in new[] { -1f, 1f })
                        Houses.Add(new TownHouse { at = Plaza + new Vector2(s * (22f + i * 10.5f), side * 10f), door = new Vector2(0f, -side), tall = (i + (side > 0 ? 0 : 1)) % 2 == 1, look = look++ });
            for (int i = 0; i < 6; i++) Lamps.Add(Plaza + new Vector2(i % 2 == 0 ? 3.6f : -3.6f, -16f - i * 11f));
            for (int i = 0; i < 6; i++) { Lamps.Add(Plaza + new Vector2(17f + i * 9f, i % 2 == 0 ? 3.6f : -3.6f)); Lamps.Add(Plaza + new Vector2(-17f - i * 9f, i % 2 == 0 ? -3.6f : 3.6f)); }

            // The neighbours' farms in two rows along the ring road (inside facing out, outside facing in), kept off the
            // farm sites, the town, the roads and the bandits' camps.
            int seed = 0;
            bool Farm(Vector2 at, Vector2 toRoad, int road)
            {
                bool ok = Vector2.Distance(at, TownAt) > 88f && Green(at.x, at.y) > 0.5f && at.x > 30f && at.y > 30f && at.x < Size - 30f && at.y < Size - 30f;
                foreach (var s in Sites) ok &= Vector2.Distance(at, s) > 58f;
                for (int i = 0; i < Roads.Count; i++) if (i != road) ok &= SegDist(at, Roads[i].a, Roads[i].b) > 24f;
                if (road >= 0) ok &= Mathf.Abs(Vector2.Distance(at, Centre) - RingRoad) > 26f;
                var reg = OwRegions.Of(at);
                ok &= reg == OwRegions.Middle || Vector2.Distance(at, OwRegions.CampAt(reg)) > 40f;
                foreach (var o in Neighbours) ok &= Vector2.Distance(at, o.at) > 46f;
                if (!ok) return false;
                Neighbours.Add(new Farmstead { at = at, toRoad = toRoad, side = new Vector2(-toRoad.y, toRoad.x), seed = seed++ });
                return true;
            }
            void Ring(float radius, int count, float offset, bool inside)
            {
                for (int i = 0; i < count; i++)
                {
                    float a = (offset + i * 360f / count) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var at = Centre + dir * radius;
                    Farm(at, inside ? dir : -dir, -1);
                }
            }
            Ring(RingRoad - 25f, 22, 8f, true);
            Ring(RingRoad + 26f, 26, 0f, false);
            // Rows along the roads to the sites, both sides, facing the road.
            for (int i = 2; i < Roads.Count; i++)
            {
                var (a, b) = Roads[i];
                var along = (b - a).normalized;
                var across = new Vector2(-along.y, along.x);
                for (float d = 30f; d < (b - a).magnitude; d += 48f)
                    foreach (float s in new[] { -1f, 1f })
                        Farm(a + along * d + across * s * 24f, -across * s, i);
            }

            Flats.Add((CoopAt, 16f, 8f));
            Flats.Add((TownAt, TownFlat, 14f));
            foreach (var n in Neighbours) Flats.Add((n.at + n.toRoad * 2f, 22f, 10f));
            for (int x = 0; x < Regions; x++)
                for (int y = 0; y < Regions; y++)
                {
                    var r = new Vector2Int(x, y);
                    if (r == OwRegions.Middle) continue;
                    Flats.Add((OwRegions.CampAt(r), r == OwRegions.Fortress ? 19f : 15f, 9f));
                }
        }

        private readonly Dictionary<Vector2Int, Transform> loaded = new Dictionary<Vector2Int, Transform>();
        private readonly List<Material> mats = new List<Material>();
        private Material sand, rock, rust, metal, dark, wood, leaf, leafDark, leafAutumn, wall, roof, barnRed, soil, crop, wheat, corn, glass, stone, water, lampGlow, hay;
        private Material grassDry, grassLight, grassDark;
        private Material[] walls, roofs;

        /// <summary>Round obstacles foi walks round (map units: x, z, radius), for the regions built now.</summary>
        public readonly List<Vector3> Blockers = new List<Vector3>();
        private readonly Dictionary<Vector2Int, List<Vector3>> regionBlockers = new Dictionary<Vector2Int, List<Vector3>>();

        /// <summary>The land under the farm is levelled (how far round it).</summary>
        public static float FarmFlat = 30f;

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
            leafDark = M(new Color(0.2f, 0.42f, 0.2f));
            leafAutumn = M(new Color(0.62f, 0.6f, 0.22f));
            wall = M(new Color(0.95f, 0.92f, 0.85f));
            roof = M(new Color(0.78f, 0.24f, 0.18f));
            barnRed = M(new Color(0.7f, 0.18f, 0.14f));
            soil = M(new Color(0.46f, 0.3f, 0.18f));
            crop = M(new Color(0.55f, 0.8f, 0.3f));
            wheat = M(new Color(0.92f, 0.78f, 0.36f));
            corn = M(new Color(0.42f, 0.66f, 0.22f));
            glass = M(new Color(0.55f, 0.75f, 0.9f), 0.3f);
            stone = M(new Color(0.72f, 0.68f, 0.6f));
            water = M(new Color(0.35f, 0.6f, 0.85f), 0.25f);
            lampGlow = M(new Color(1f, 0.85f, 0.55f), 1.2f);
            hay = M(new Color(0.85f, 0.72f, 0.4f));
            grassDry = M(new Color(0.62f, 0.62f, 0.3f));
            grassLight = M(new Color(0.44f, 0.7f, 0.28f));
            grassDark = M(new Color(0.27f, 0.5f, 0.2f));
            walls = new[] { wall, M(new Color(0.96f, 0.86f, 0.62f)), M(new Color(0.78f, 0.86f, 0.92f)), M(new Color(0.94f, 0.78f, 0.74f)), M(new Color(0.84f, 0.8f, 0.72f)) };
            roofs = new[] { roof, M(new Color(0.48f, 0.3f, 0.22f)), M(new Color(0.3f, 0.33f, 0.4f)), M(new Color(0.85f, 0.45f, 0.25f)) };
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
            d += (Mathf.PerlinNoise(x * 0.014f + 11f, z * 0.014f + 5f) - 0.5f) * 40f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(58f, 108f, d));
        }

        private static float Raw(float x, float z)
        {
            float g = Green(x, z);
            float dunes = Mathf.PerlinNoise(x * 0.03f + 3.1f, z * 0.03f + 7.7f) * 3f + Mathf.PerlinNoise(x * 0.11f, z * 0.11f) * 0.4f;
            float hills = Mathf.PerlinNoise(x * 0.0085f + 40f, z * 0.0085f + 9f) * 12f + Mathf.PerlinNoise(x * 0.028f + 5f, z * 0.028f + 70f) * 2.6f;
            return Mathf.Lerp(dunes, hills, g) - 1.5f;
        }

        /// <summary>The ground's height at a map point (levelled under the farm, the town, the neighbours and the camps).</summary>
        public static float Height(float x, float z)
        {
            float h = Raw(x, z);
            h = Flatten(h, x, z, FarmCentre, FarmFlat, 10f);
            foreach (var f in Flats) h = Flatten(h, x, z, f.at, f.r, f.edge);
            return h;
        }

        private static float Flatten(float h, float x, float z, Vector2 at, float r, float edge)
        {
            float dx = x - at.x, dz = z - at.y, far = r + edge;
            if (dx * dx + dz * dz >= far * far) return h;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float flat = Raw(at.x, at.y);
            return Mathf.Lerp(flat, h, Mathf.SmoothStep(0f, 1f, (d - r) / edge));
        }

        public static Vector3 World(float x, float z) => Origin + new Vector3(x, Height(x, z), z);
        public static Vector3 World(Vector2 p) => World(p.x, p.y);

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>How far a point is from the nearest road.</summary>
        public static float RoadDist(Vector2 p) => RoadDist(p, All);

        private static float RoadDist(Vector2 p, Near near)
        {
            float d = Mathf.Abs(Vector2.Distance(p, Centre) - RingRoad);
            foreach (var r in near.roads) d = Mathf.Min(d, SegDist(p, r.a, r.b));
            return d;
        }

        /// <summary>The roads and neighbours' farms that reach into a stretch of land (a region's ground is coloured pixel by
        /// pixel: each pixel asks only these, not all of them).</summary>
        private class Near
        {
            public readonly List<(Vector2 a, Vector2 b)> roads = new List<(Vector2, Vector2)>();
            public readonly List<Farmstead> farms = new List<Farmstead>();
        }

        private static Near all;
        private static Near All
        {
            get
            {
                if (all != null) return all;
                all = new Near();
                all.roads.AddRange(Roads);
                all.farms.AddRange(Neighbours);
                return all;
            }
        }

        private static Near NearOf(float x0, float z0, float size)
        {
            var n = new Near();
            var mid = new Vector2(x0 + size * 0.5f, z0 + size * 0.5f);
            float reach = size * 0.75f + 40f;
            foreach (var r in Roads) if (SegDist(mid, r.a, r.b) < reach) n.roads.Add(r);
            foreach (var f in Neighbours) if (Vector2.Distance(mid, f.at) < reach) n.farms.Add(f);
            return n;
        }

        /// <summary>Where a point lies on a neighbour's farm: 0 not on one, 1 its yard, 2 its fields (crop = which).</summary>
        private static int OnFarmstead(Vector2 p, out float u, out float v, out int crop) => OnFarmstead(p, All, out u, out v, out crop);

        private static int OnFarmstead(Vector2 p, Near near, out float u, out float v, out int crop)
        {
            u = v = 0f; crop = 0;
            foreach (var n in near.farms)
            {
                var d = p - n.at;
                if (d.sqrMagnitude > 34f * 34f) continue;
                u = Vector2.Dot(d, n.side);
                v = -Vector2.Dot(d, n.toRoad);
                if (Mathf.Abs(u) > 14f) continue;
                if (v >= 3f && v <= 27f) { crop = (n.seed * 7 + (u < -4.6f ? 0 : u < 4.6f ? 1 : 2) * 3) % 4; return 2; }
                if (v > -15f && v < 3f) return 1;
            }
            return 0;
        }

        private static float Hash(float x, float z)
        {
            float h = Mathf.Sin(x * 12.9898f + z * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private static readonly Color Desert = new Color(0.86f, 0.42f, 0.26f), Dry = new Color(0.66f, 0.58f, 0.3f),
            Lush = new Color(0.36f, 0.6f, 0.24f), Meadow = new Color(0.5f, 0.64f, 0.28f), Clover = new Color(0.26f, 0.48f, 0.2f),
            Dirt = new Color(0.56f, 0.44f, 0.3f), Track = new Color(0.7f, 0.36f, 0.22f), Cobble = new Color(0.66f, 0.62f, 0.55f);
        private static readonly Color[] Crops = { new Color(0.86f, 0.74f, 0.34f), new Color(0.42f, 0.66f, 0.24f), new Color(0.58f, 0.78f, 0.32f), new Color(0.66f, 0.5f, 0.62f) };

        /// <summary>The ground's colour: sand with ripples, grass in patches (meadow, lush, clover) with a fine grain,
        /// dirt roads, the town's cobbles, the neighbours' fields in rows.</summary>
        public static Color GroundColour(float x, float z) => GroundColour(x, z, All);

        private static Color GroundColour(float x, float z, Near near)
        {
            var p = new Vector2(x, z);
            float g = Green(x, z);
            float grain = (Hash(Mathf.Floor(x * 2f), Mathf.Floor(z * 2f)) - 0.5f) * 0.07f;
            Color c;
            if (g < 0.5f)
            {
                float ripple = Mathf.Sin(x * 0.9f + Mathf.PerlinNoise(x * 0.05f, z * 0.05f) * 8f) * 0.025f;
                float patch = (Mathf.PerlinNoise(x * 0.04f + 50f, z * 0.04f + 20f) - 0.5f) * 0.12f;
                c = Color.Lerp(Desert, Dry, g * 2f) * (1f + ripple + patch);
            }
            else
            {
                float big = Mathf.PerlinNoise(x * 0.012f + 7f, z * 0.012f + 3f);
                var grass = Color.Lerp(Lush, Meadow, Mathf.SmoothStep(0.3f, 0.75f, big));
                float mid = (Mathf.PerlinNoise(x * 0.09f + 13f, z * 0.09f + 4f) - 0.5f) * 0.16f;
                grass *= 1f + mid;
                if (Mathf.PerlinNoise(x * 0.22f + 1f, z * 0.22f + 9f) > 0.68f) grass = Color.Lerp(grass, Clover, 0.55f);
                c = Color.Lerp(Dry, grass, (g - 0.5f) * 2f);
            }
            c = new Color(c.r + grain, c.g + grain, c.b + grain * 0.6f);

            // The neighbours' fields in rows, their trodden yards.
            int on = OnFarmstead(p, near, out float u, out float v, out int cropKind);
            if (on == 2)
            {
                float row = Mathf.SmoothStep(0f, 1f, 0.5f + 0.9f * Mathf.Cos(v / 3.2f * Mathf.PI * 2f));
                var field = Color.Lerp(new Color(0.44f, 0.3f, 0.18f), Crops[cropKind], row);
                if (Mathf.Abs(Mathf.Repeat(u + 4.6f, 9.2f) - 4.6f) > 4.2f) field = new Color(0.4f, 0.5f, 0.24f); // grass between fields
                c = field * (1f + grain);
            }
            else if (on == 1) c = Color.Lerp(c, Dirt, 0.45f);

            // Roads, the square.
            float rd = RoadDist(p, near);
            bool inTown = Vector2.Distance(p, TownAt) < TownFlat + 4f;
            float plaza = Vector2.Distance(p, Plaza);
            if (inTown && plaza < 12f) rd = 0f;
            if (rd < RoadHalf + 0.8f)
            {
                var road = inTown ? Cobble : g < 0.4f ? Track : Dirt;
                if (inTown && Hash(Mathf.Floor(x * 1.4f), Mathf.Floor(z * 1.4f)) > 0.5f) road *= 0.93f;
                float wheel = !inTown && Mathf.Abs(rd - 1.1f) < 0.35f ? 0.9f : 1f;
                c = Color.Lerp(road * wheel * (1f + grain), c, Mathf.InverseLerp(RoadHalf, RoadHalf + 0.8f, rd));
            }
            return c;
        }

        // ---------- Streaming ----------

        /// <summary>Drops every region (the farm moved: the land is levelled somewhere else now).</summary>
        public void Reload()
        {
            if (building != null) { StopCoroutine(building); building = null; }
            foreach (var kv in loaded) if (kv.Value != null) Destroy(kv.Value.gameObject);
            loaded.Clear();
            regionBlockers.Clear();
            Blockers.Clear();
        }

        /// <summary>Builds the regions round a map point and drops the far ones. foi's own region (and all of them when
        /// asked: entering, moving the farm) is built at once; the others one at a time, a few milliseconds a frame, so
        /// walking never stutters.</summary>
        public void Around(Vector2 p, bool all = false)
        {
            var at = OwRegions.Of(p);
            bool changed = false;
            var keep = new HashSet<Vector2Int>();
            // foi's own region at once (one half built goes on being built: foi stands on the land's height, not its mesh).
            if (!loaded.ContainsKey(at)) { BuildNow(at); changed = true; }
            if (all) warmUntil = Time.unscaledTime + 5f;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var r = new Vector2Int(at.x + dx, at.y + dz);
                    if (r.x < 0 || r.y < 0 || r.x >= Regions || r.y >= Regions) continue;
                    keep.Add(r);
                    if (loaded.ContainsKey(r)) continue;
                    // Entering: the regions foi could see at once are built at once too; the far ones a slice a frame.
                    if (all && NearRect(p, r) < 45f) { if (building != null && buildingAt == r) StopBuilding(); BuildNow(r); changed = true; }
                    else if (building == null) { buildingAt = r; building = StartCoroutine(BuildLater(r)); }
                }
            var drop = new List<Vector2Int>();
            foreach (var kv in loaded) if (!keep.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var r in drop)
            {
                if (building != null && buildingAt == r) StopBuilding();
                else { if (loaded[r] != null) Destroy(loaded[r].gameObject); loaded.Remove(r); regionBlockers.Remove(r); }
                changed = true;
            }
            if (changed || blockersDirty) RefreshBlockers();
        }

        private Coroutine building;
        private Vector2Int buildingAt;
        private bool blockersDirty, buildingNow;
        private readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        private const double SliceMs = 4.0;

        /// <summary>A region built a slice a frame has used its time this frame.</summary>
        private bool Over() => !buildingNow && clock.Elapsed.TotalMilliseconds > (Time.unscaledTime < warmUntil ? 12.0 : SliceMs);
        private float warmUntil;

        private static float NearRect(Vector2 p, Vector2Int r)
        {
            float x0 = r.x * RegionSize, z0 = r.y * RegionSize;
            float dx = Mathf.Max(0f, Mathf.Max(x0 - p.x, p.x - (x0 + RegionSize)));
            float dz = Mathf.Max(0f, Mathf.Max(z0 - p.y, p.y - (z0 + RegionSize)));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void RefreshBlockers()
        {
            blockersDirty = false;
            Blockers.Clear();
            foreach (var kv in regionBlockers) Blockers.AddRange(kv.Value);
        }

        /// <summary>Stops the region being built bit by bit and drops what it had made.</summary>
        private void StopBuilding()
        {
            if (building == null) return;
            StopCoroutine(building);
            building = null;
            if (loaded.TryGetValue(buildingAt, out var root) && root != null) Destroy(root.gameObject);
            loaded.Remove(buildingAt);
            regionBlockers.Remove(buildingAt);
        }

        private void BuildNow(Vector2Int r)
        {
            buildingNow = true;
            var stack = new Stack<System.Collections.IEnumerator>();
            stack.Push(BuildRegion(r));
            while (stack.Count > 0)
            {
                var e = stack.Peek();
                if (!e.MoveNext()) stack.Pop();
                else if (e.Current is System.Collections.IEnumerator inner) stack.Push(inner);
            }
            buildingNow = false;
        }

        private System.Collections.IEnumerator BuildLater(Vector2Int r)
        {
            var stack = new Stack<System.Collections.IEnumerator>();
            stack.Push(BuildRegion(r));
            clock.Restart();
            while (stack.Count > 0)
            {
                var e = stack.Peek();
                if (!e.MoveNext()) { stack.Pop(); continue; }
                if (e.Current is System.Collections.IEnumerator inner) { stack.Push(inner); continue; }
                yield return null;
                clock.Restart();
            }
            building = null;
            blockersDirty = true;
        }

        private System.Collections.IEnumerator BuildRegion(Vector2Int r)
        {
            var root = new GameObject("Region " + r.x + "," + r.y).transform;
            root.SetParent(transform, false);
            loaded[r] = root;
            float x0 = r.x * RegionSize, z0 = r.y * RegionSize;
            var blockers = new List<Vector3>();
            regionBlockers[r] = blockers;

            // The ground: 55 × 55 points (2 units apart), its colour from its own texture over it (256 × 256; 192 on the low tier).
            const int n = 55;
            float step = RegionSize / (n - 1);
            var verts = new Vector3[n * n];
            var uv = new Vector2[n * n];
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    float wx = x0 + x * step, wz = z0 + z * step;
                    verts[z * n + x] = new Vector3(wx, Height(wx, wz), wz);
                    uv[z * n + x] = new Vector2(x / (float)(n - 1), z / (float)(n - 1));
                }
                if (Over()) yield return null;
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
            int tex = SquashBot.Data.GraphicsQuality.Current == SquashBot.Data.GraphicsTier.High ? 256 : 192;
            var near = NearOf(x0, z0, RegionSize);
            var colours = new Color[tex * tex];
            for (int z = 0; z < tex; z++)
            {
                for (int x = 0; x < tex; x++)
                    colours[z * tex + x] = GroundColour(x0 + (x + 0.5f) / tex * RegionSize, z0 + (z + 0.5f) / tex * RegionSize, near);
                if (Over()) yield return null;
            }
            var texture = new Texture2D(tex, tex, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            texture.SetPixels(colours);
            texture.Apply(true, true);
            var groundMat = MaterialFactory.Create(Color.white, Color.black);
            groundMat.SetTexture("_BaseMap", texture);
            groundMat.mainTexture = texture;
            var ground = new GameObject("Ground");
            ground.transform.SetParent(root, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var groundRenderer = ground.AddComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = groundMat;
            groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // (it takes shadows; it never casts one anyone sees)
            ground.AddComponent<OwnedMesh>().mesh = mesh;
            ground.AddComponent<RegionOwned>().Set(groundMat, texture);

            var rng = new System.Random(r.x * 97 + r.y * 13 + 7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            yield return Grass(root, x0, z0, rng);

            // Its props: scrap and rocks in the desert, woods (thicker where the land says so), lone trees and bushes on
            // the green.
            // (In groups, each merged on its own: one merge of a whole region is a long frame.)
            var groups = new List<Transform>();
            Transform Group(string name) { var g = new GameObject(name).transform; g.SetParent(root, false); groups.Add(g); return g; }
            var props = Group("Props");
            for (int i = 0; i < 260; i++)
            {
                if (i % 65 == 64) props = Group("Props");
                if (Over()) yield return null;
                float x = x0 + R(1f, RegionSize - 1f), z = z0 + R(1f, RegionSize - 1f);
                if (Reserved(x, z)) continue;
                float g = Green(x, z);
                var at = new Vector3(x, Height(x, z), z);
                if (g < 0.4f)
                {
                    double roll = rng.NextDouble();
                    if (roll < 0.35) Scrap(props, at, rng, blockers);
                    else if (roll < 0.65) Rock(props, at, rng, R(1f, 3f), blockers);
                }
                else if (g > 0.6f)
                {
                    float wood = Mathf.PerlinNoise(x * 0.02f + 90f, z * 0.02f + 12f);
                    double roll = rng.NextDouble();
                    if (wood > 0.56f && roll < 0.9) Tree(props, at, rng, R(1.8f, 3.2f), blockers);
                    else if (roll < 0.1) Tree(props, at, rng, R(1.6f, 2.8f), blockers);
                    else if (roll < 0.22) Shapes.Primitive(PrimitiveType.Sphere, "Bush", props, at + Vector3.up * 0.3f, new Vector3(R(1f, 1.8f), R(0.7f, 1.1f), R(1f, 1.8f)), leafDark);
                    else if (roll < 0.27) Rock(props, at, rng, R(0.5f, 1.2f), blockers);
                }
                else if (rng.NextDouble() < 0.35) Rock(props, at, rng, R(0.6f, 1.4f), blockers);
            }
            // A few fallen robots in the desert regions.
            if (Green(x0 + RegionSize * 0.5f, z0 + RegionSize * 0.5f) < 0.3f)
                for (int i = 0; i < 6; i++)
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
            // The co-op, the town, the neighbours' farms.
            props = Group("Town");
            if (Inside(CoopAt, x0, z0)) Coop(root, blockers);
            if (Inside(Plaza, x0, z0)) Square(props, blockers);
            foreach (var h in Houses) if (Inside(h.at, x0, z0)) TownHouseModel(props, h, blockers);
            foreach (var l in Lamps) if (Inside(l, x0, z0)) Lamp(props, l);
            foreach (var nb in Neighbours) if (Inside(nb.at, x0, z0)) { NeighbourFarm(Group("Farm"), nb, blockers); if (Over()) yield return null; }
            foreach (var g in groups)
            {
                if (g.childCount > 0) StaticBatchingUtility.Combine(g.gameObject);
                if (Over()) yield return null;
            }
            blockersDirty = true;
        }

        private static bool Low => SquashBot.Data.GraphicsQuality.Current == SquashBot.Data.GraphicsTier.Low;

        private static bool Inside(Vector2 p, float x0, float z0) => p.x >= x0 && p.x < x0 + RegionSize && p.y >= z0 && p.y < z0 + RegionSize;

        /// <summary>Kept clear: the farm, the town, the neighbours, the roads, the arrival spot, the camps.</summary>
        private static bool Reserved(float x, float z)
        {
            var p = new Vector2(x, z);
            if (Vector2.Distance(p, FarmCentre) < FarmFlat + 6f) return true;
            if (Vector2.Distance(p, TownAt) < TownFlat + 4f || Vector2.Distance(p, CoopAt) < 18f) return true;
            if (Vector2.Distance(p, Arrival) < 6f) return true;
            var reg = OwRegions.Of(p);
            if (reg != OwRegions.Middle && Vector2.Distance(p, OwRegions.CampAt(reg)) < (reg == OwRegions.Fortress ? 20f : 16f)) return true; // a bandit camp's ground
            foreach (var n in Neighbours) if ((p - n.at).sqrMagnitude < 31f * 31f) return true;
            return RoadDist(p) < RoadHalf + 1.5f;
        }

        /// <summary>Tufts of grass over the green (three blades each, lit as the ground is), in dry, light and dark greens;
        /// none on the roads, the fields, the farm.</summary>
        private System.Collections.IEnumerator Grass(Transform root, float x0, float z0, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            if (Green(x0 + RegionSize * 0.5f, z0 + RegionSize * 0.5f) < 0.2f && Green(x0, z0) < 0.3f && Green(x0 + RegionSize, z0 + RegionSize) < 0.3f) yield break;
            var lists = new[] { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
            var nearGrass = NearOf(x0, z0, RegionSize);
            int tufts = Low ? 1400 : SquashBot.Data.GraphicsQuality.Current == SquashBot.Data.GraphicsTier.Medium ? 2400 : 3400;
            for (int i = 0; i < tufts; i++)
            {
                if (i % 200 == 199 && Over()) yield return null;
                float x = x0 + R(0f, RegionSize), z = z0 + R(0f, RegionSize);
                float g = Green(x, z);
                if (g < 0.5f + (float)rng.NextDouble() * 0.15f) continue;
                var p = new Vector2(x, z);
                if (Vector2.Distance(p, FarmCentre) < 18f || RoadDist(p, nearGrass) < RoadHalf + 0.4f || OnFarmstead(p, nearGrass, out _, out _, out _) == 2) continue;
                if (Vector2.Distance(p, Plaza) < 13f) continue;
                int kind = g < 0.72f ? 0 : rng.NextDouble() < 0.5 ? 1 : 2;
                var list = lists[kind];
                float h = Height(x, z) - 0.03f;
                float tall = R(0.28f, 0.6f) * (0.7f + 0.3f * g);
                for (int b = 0; b < 3; b++)
                {
                    float a = R(0f, Mathf.PI);
                    var across = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.08f;
                    var lean = new Vector3(R(-0.15f, 0.15f), 0f, R(-0.15f, 0.15f));
                    var c = new Vector3(x + R(-0.2f, 0.2f), h, z + R(-0.2f, 0.2f));
                    list.Add(c - across); list.Add(c + across); list.Add(c + lean + Vector3.up * tall);
                }
            }
            var matsFor = new[] { grassDry, grassLight, grassDark };
            for (int k = 0; k < 3; k++)
            {
                var v = lists[k];
                if (v.Count == 0) continue;
                var tris = new int[v.Count * 2];
                var normals = new Vector3[v.Count];
                for (int i = 0; i < v.Count; i += 3)
                {
                    // Both faces, so a blade shows from either side.
                    int o = i * 2;
                    tris[o] = i; tris[o + 1] = i + 1; tris[o + 2] = i + 2;
                    tris[o + 3] = i; tris[o + 4] = i + 2; tris[o + 5] = i + 1;
                    normals[i] = normals[i + 1] = normals[i + 2] = Vector3.up;
                }
                var mesh = new Mesh { name = "Grass" };
                mesh.SetVertices(v);
                mesh.SetNormals(normals);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                var go = new GameObject("Grass");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = matsFor[k];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<OwnedMesh>().mesh = mesh;
            }
        }

        private static float Step(float v, float step) => Mathf.Max(step, Mathf.Round(v / step) * step);

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
                // (Sizes in steps: a rounded box is made once per size and kept, so endless random sizes would pile up meshes.)
                var size = new Vector3(Step(R(0.8f, 2.6f), 0.45f), Step(R(0.5f, 1.8f), 0.4f), Step(R(0.8f, 2.4f), 0.45f));
                Shapes.Rounded("Part", heap, new Vector3(R(-1.2f, 1.2f), size.y * 0.35f, R(-1.2f, 1.2f)), size, 0.08f, p % 3 == 0 ? metal : p % 2 == 0 ? dark : rust)
                    .transform.localRotation = Quaternion.Euler(R(-25f, 25f), R(0f, 90f), R(-25f, 25f));
            }
            blockers.Add(new Vector3(at.x, at.z, 1.8f));
        }

        private void Rock(Transform parent, Vector3 at, System.Random rng, float size, List<Vector3> blockers)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            size = Step(size, 0.5f);
            Shapes.Rounded("Rock", parent, at + Vector3.up * size * 0.3f, new Vector3(Step(size * R(0.8f, 1.4f), 0.4f), Step(size * R(0.5f, 1f), 0.3f), Step(size * R(0.8f, 1.4f), 0.4f)), Mathf.Min(0.4f, size * 0.2f), rock)
                .transform.localRotation = Quaternion.Euler(R(-10f, 10f), R(0f, 90f), R(-10f, 10f));
            if (size > 1.2f) blockers.Add(new Vector3(at.x, at.z, size * 0.6f));
        }

        /// <summary>A tree: round and leafy, tall and dark like a poplar, or one turning yellow.</summary>
        private void Tree(Transform parent, Vector3 at, System.Random rng, float size, List<Vector3> blockers)
        {
            int kind = rng.Next(10);
            if (kind < 3)
            {
                Shapes.Primitive(PrimitiveType.Cylinder, "Trunk", parent, at + new Vector3(0f, size * 0.5f, 0f), new Vector3(size * 0.14f, size * 0.5f, size * 0.14f), wood);
                Shapes.Primitive(PrimitiveType.Sphere, "Leaves", parent, at + new Vector3(0f, size * 1.6f, 0f), new Vector3(size * 0.8f, size * 2.2f, size * 0.8f), leafDark);
            }
            else
            {
                Shapes.Primitive(PrimitiveType.Cylinder, "Trunk", parent, at + new Vector3(0f, size * 0.4f, 0f), new Vector3(size * 0.16f, size * 0.4f, size * 0.16f), wood);
                var m = kind == 9 ? leafAutumn : leaf;
                Shapes.Primitive(PrimitiveType.Sphere, "Leaves", parent, at + new Vector3(0f, size * 1.05f, 0f), Vector3.one * size, m);
                Shapes.Primitive(PrimitiveType.Sphere, "Leaves", parent, at + new Vector3(size * 0.3f, size * 1.4f, size * 0.2f), Vector3.one * size * 0.6f, m);
            }
            blockers.Add(new Vector3(at.x, at.z, size * 0.25f));
        }

        /// <summary>The co-op's long market hall (in the world, and for its picture on the map).</summary>
        public void CoopModel(Transform c)
        {
            // Wooden posts, a striped awning, crates and baskets of produce, a big sign.
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
        }

        private void Coop(Transform root, List<Vector3> blockers)
        {
            var c = new GameObject("Coop").transform;
            c.SetParent(root, false);
            c.localPosition = new Vector3(CoopAt.x, Height(CoopAt.x, CoopAt.y), CoopAt.y);
            CoopModel(c);
            blockers.Add(new Vector3(CoopAt.x, CoopAt.y + 2.6f, 4f));
            blockers.Add(new Vector3(CoopAt.x - 4.5f, CoopAt.y + 2.6f, 3f));
            blockers.Add(new Vector3(CoopAt.x + 4.5f, CoopAt.y + 2.6f, 3f));
        }

        /// <summary>The town square: a fountain in the middle, a clock tower on the corner, benches.</summary>
        private void Square(Transform parent, List<Vector3> blockers)
        {
            var at = new Vector3(Plaza.x, Height(Plaza.x, Plaza.y), Plaza.y);
            Shapes.Primitive(PrimitiveType.Cylinder, "Basin", parent, at + Vector3.up * 0.35f, new Vector3(6f, 0.35f, 6f), stone);
            Shapes.Primitive(PrimitiveType.Cylinder, "Water", parent, at + Vector3.up * 0.62f, new Vector3(5.2f, 0.05f, 5.2f), water);
            Shapes.Primitive(PrimitiveType.Cylinder, "Pillar", parent, at + Vector3.up * 1.4f, new Vector3(0.7f, 1.1f, 0.7f), stone);
            Shapes.Primitive(PrimitiveType.Cylinder, "Bowl", parent, at + Vector3.up * 2.5f, new Vector3(2.2f, 0.18f, 2.2f), stone);
            Shapes.Primitive(PrimitiveType.Sphere, "Spout", parent, at + Vector3.up * 2.9f, new Vector3(0.9f, 0.7f, 0.9f), water);
            blockers.Add(new Vector3(Plaza.x, Plaza.y, 3.2f));
            var tw = Plaza + new Vector2(-16f, -16f);
            var t = new Vector3(tw.x, Height(tw.x, tw.y), tw.y);
            Shapes.Rounded("Tower", parent, t + Vector3.up * 6f, new Vector3(4f, 12f, 4f), 0.1f, walls[4]);
            Shapes.Rounded("TowerTop", parent, t + Vector3.up * 12.4f, new Vector3(4.8f, 0.8f, 4.8f), 0.1f, stone);
            Shapes.Rounded("Spire", parent, t + Vector3.up * 14f, new Vector3(3f, 2.6f, 3f), 0.6f, roofs[2]);
            foreach (var face in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                var clock = Shapes.Primitive(PrimitiveType.Cylinder, "Clock", parent, t + Vector3.up * 10.4f + face * 2.02f, new Vector3(2f, 0.04f, 2f), wall);
                clock.transform.localRotation = Quaternion.FromToRotation(Vector3.up, face);
                Shapes.Rounded("Hand", parent, t + Vector3.up * 10.7f + face * 2.08f, new Vector3(face.x != 0f ? 0.05f : 0.12f, 0.7f, face.z != 0f ? 0.05f : 0.12f), 0.02f, dark);
            }
            blockers.Add(new Vector3(tw.x, tw.y, 2.8f));
            foreach (var d in new[] { new Vector2(7f, 7f), new Vector2(-7f, 7f), new Vector2(7f, -7f), new Vector2(-7f, -7f) })
            {
                var b = Plaza + d;
                var bp = new Vector3(b.x, Height(b.x, b.y), b.y);
                Shapes.Rounded("Bench", parent, bp + Vector3.up * 0.45f, new Vector3(2.2f, 0.15f, 0.7f), 0.04f, wood).transform.localRotation = Quaternion.Euler(0f, d.x * d.y > 0 ? 45f : -45f, 0f);
            }
        }

        private void Lamp(Transform parent, Vector2 p)
        {
            var at = new Vector3(p.x, Height(p.x, p.y), p.y);
            Shapes.Rounded("LampPost", parent, at + Vector3.up * 1.6f, new Vector3(0.18f, 3.2f, 0.18f), 0.05f, dark);
            Shapes.Primitive(PrimitiveType.Sphere, "Lamp", parent, at + Vector3.up * 3.35f, Vector3.one * 0.5f, lampGlow);
        }

        /// <summary>A town house facing its street: one storey or two, its own wall and roof colours, a chimney.</summary>
        private void TownHouseModel(Transform parent, TownHouse th, List<Vector3> blockers)
        {
            var h = new GameObject("TownHouse").transform;
            h.SetParent(parent, false);
            h.localPosition = new Vector3(th.at.x, Height(th.at.x, th.at.y), th.at.y);
            // The model's door is on its -z side.
            h.localRotation = Quaternion.LookRotation(new Vector3(-th.door.x, 0f, -th.door.y));
            var w = walls[th.look % walls.Length];
            var rf = roofs[(th.look * 3 + 1) % roofs.Length];
            if (!th.tall) { HouseModel(h, Vector3.zero, 1.45f, w, rf); blockers.Add(new Vector3(th.at.x, th.at.y, 3.2f)); return; }
            Shapes.Rounded("Walls", h, new Vector3(0f, 2.9f, 0f), new Vector3(6.4f, 5.8f, 5.2f), 0.06f, w);
            foreach (float side in new[] { -1f, 1f })
                Shapes.Rounded("Roof", h, new Vector3(0f, 6.6f, side * 1.4f), new Vector3(7f, 0.3f, 3.5f), 0.06f, rf).transform.localRotation = Quaternion.Euler(side * 32f, 0f, 0f);
            Shapes.Rounded("Door", h, new Vector3(0f, 1f, -2.62f), new Vector3(1.1f, 2f, 0.08f), 0.03f, wood);
            foreach (float y in new[] { 1.6f, 4.2f })
                foreach (float x in new[] { -2f, 2f })
                    Shapes.Rounded("Window", h, new Vector3(x, y, -2.62f), new Vector3(1f, 0.9f, 0.06f), 0.03f, glass);
            Shapes.Rounded("Window", h, new Vector3(0f, 4.2f, -2.62f), new Vector3(1f, 0.9f, 0.06f), 0.03f, glass);
            Shapes.Rounded("Chimney", h, new Vector3(2f, 7.4f, 0.6f), new Vector3(0.6f, 1.6f, 0.6f), 0.05f, dark);
            blockers.Add(new Vector3(th.at.x, th.at.y, 3.6f));
        }

        /// <summary>A neighbour's farm: the house and barn by the road, a silo, hay bales, its fields fenced in behind
        /// (the rows are in the ground; one field of tall corn stands up).</summary>
        private void NeighbourFarm(Transform parent, Farmstead nb, List<Vector3> blockers)
        {
            var f = new GameObject("NeighbourFarm").transform;
            f.SetParent(parent, false);
            f.localPosition = new Vector3(nb.at.x, Height(nb.at.x, nb.at.y), nb.at.y);
            // Its -z faces the road; the fields lie along +z.
            f.localRotation = Quaternion.LookRotation(new Vector3(-nb.toRoad.x, 0f, -nb.toRoad.y));
            var w = walls[nb.seed % walls.Length];
            var rf = roofs[(nb.seed + 2) % roofs.Length];
            HouseModel(f, new Vector3(-3f, 0f, -8f), 1.5f, w, rf);
            Shapes.Rounded("Barn", f, new Vector3(8f, 2.3f, -6f), new Vector3(5.4f, 4.6f, 6.4f), 0.06f, barnRed);
            foreach (float side in new[] { -1f, 1f })
                Shapes.Rounded("BarnRoof", f, new Vector3(8f + side * 1.5f, 5.2f, -6f), new Vector3(3.4f, 0.3f, 6.9f), 0.05f, dark).transform.localRotation = Quaternion.Euler(0f, 0f, -side * 30f);
            Shapes.Rounded("BarnDoor", f, new Vector3(8f, 1.4f, -9.24f), new Vector3(2.4f, 2.8f, 0.08f), 0.03f, wood);
            Shapes.Primitive(PrimitiveType.Cylinder, "Silo", f, new Vector3(-11f, 3.4f, -5f), new Vector3(2.4f, 3.4f, 2.4f), metal);
            Shapes.Primitive(PrimitiveType.Sphere, "SiloTop", f, new Vector3(-11f, 6.8f, -5f), new Vector3(2.4f, 1.4f, 2.4f), metal);
            for (int i = 0; i < 3; i++)
                Shapes.Primitive(PrimitiveType.Cylinder, "Hay", f, new Vector3(2f + i * 1.5f, 0.6f, -1f), new Vector3(1.2f, 0.6f, 1.2f), hay).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // The fence round the fields.
            const float u0 = -14f, u1 = 14f, v0 = 2.4f, v1 = 27.6f;
            Shapes.Rounded("Rail", f, new Vector3(0f, 0.75f, v0), new Vector3(u1 - u0, 0.12f, 0.12f), 0.03f, wood);
            Shapes.Rounded("Rail", f, new Vector3(0f, 0.75f, v1), new Vector3(u1 - u0, 0.12f, 0.12f), 0.03f, wood);
            Shapes.Rounded("Rail", f, new Vector3(u0, 0.75f, (v0 + v1) * 0.5f), new Vector3(0.12f, 0.12f, v1 - v0), 0.03f, wood);
            Shapes.Rounded("Rail", f, new Vector3(u1, 0.75f, (v0 + v1) * 0.5f), new Vector3(0.12f, 0.12f, v1 - v0), 0.03f, wood);
            for (int i = 0; i <= 4; i++)
                foreach (float u in new[] { u0, u1 })
                    Shapes.Rounded("Post", f, new Vector3(u, 0.5f, Mathf.Lerp(v0, v1, i / 4f)), new Vector3(0.18f, 1f, 0.18f), 0.03f, wood);
            for (int i = 1; i < 4; i++)
                foreach (float v in new[] { v0, v1 })
                    Shapes.Rounded("Post", f, new Vector3(Mathf.Lerp(u0, u1, i / 4f), 0.5f, v), new Vector3(0.18f, 1f, 0.18f), 0.03f, wood);
            // One field of corn standing up, in its rows.
            float cu = ((nb.seed % 3) - 1) * 9.2f;
            for (int row = 0; row < 8; row++)
                Shapes.Rounded("Corn", f, new Vector3(cu, 0.6f,  4.2f + row * 3.2f), new Vector3(8f, 1.3f, 1.4f), 0.3f, row % 2 == 0 ? corn : crop);
            // Two trees by the house.
            var rng = new System.Random(nb.seed + 99);
            foreach (var p in new[] { new Vector3(-9f, 0f, -12f), new Vector3(3f, 0f, -13f) })
                Tree(f, p, rng, 2.4f, new List<Vector3>());
            void Block(Vector3 local, float r) { var wp = f.TransformPoint(local) - Origin; blockers.Add(new Vector3(wp.x, wp.z, r)); }
            Block(new Vector3(-3f, 0f, -8f), 3.4f);
            Block(new Vector3(8f, 0f, -6f), 3.8f);
            Block(new Vector3(-11f, 0f, -5f), 2.4f);
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

        /// <summary>The co-op's picture (for the map).</summary>
        public Texture CoopIcon() => OwIcons.Get("coop", CoopModel);

        // ---------- The map's picture ----------

        private static Texture2D map;
        private static Color[] mapPixels;
        private static float[] mapHeights;
        private static int mapRow;
        private const int MapPx = 300;

        /// <summary>Draws the map's picture a little each frame (a couple of milliseconds) after entering, so it is ready when asked for.</summary>
        public static System.Collections.IEnumerator PrepareMap()
        {
            yield return new WaitForSecondsRealtime(2f);
            var sw = new System.Diagnostics.Stopwatch();
            while (map == null)
            {
                sw.Restart();
                while (map == null && sw.Elapsed.TotalMilliseconds < 2.5) MapRows(1);
                yield return null;
            }
        }

        /// <summary>The whole land seen from above: the ground's colours shaded by its hills, the woods, scrap in the
        /// desert, the town's and the neighbours' roofs (finished now if not yet).</summary>
        public static Texture2D MapTexture()
        {
            while (map == null) MapRows(MapPx);
            return map;
        }

        private static void MapRows(int rows)
        {
            if (map != null) return;
            const int px = MapPx;
            float unit = Size / px;
            if (mapPixels == null)
            {
                mapPixels = new Color[px * px];
                mapHeights = new float[(px + 1) * (px + 1)];
                mapRow = -1;
            }
            var c = mapPixels;
            // The heights at the pixels' corners, a row ahead of the colours (shading from them, not three lookups a pixel).
            void HeightRow(int j) { for (int i = 0; i <= px; i++) mapHeights[j * (px + 1) + i] = Height(i * unit, j * unit); }
            if (mapRow < 0) { HeightRow(0); mapRow = 0; }
            var light = new Vector3(-0.6f, 0.75f, 0.5f).normalized;
            for (int end = Mathf.Min(px, mapRow + rows); mapRow < end; mapRow++)
            {
                int j = mapRow;
                HeightRow(j + 1);
                for (int i = 0; i < px; i++)
                {
                    float x = (i + 0.5f) * unit, z = (j + 0.5f) * unit;
                    float h00 = mapHeights[j * (px + 1) + i], h10 = mapHeights[j * (px + 1) + i + 1], h01 = mapHeights[(j + 1) * (px + 1) + i];
                    var normal = new Vector3(-(h10 - h00), unit, -(h01 - h00)).normalized;
                    float shade = Mathf.Lerp(0.7f, 1.2f, Mathf.Clamp01(Vector3.Dot(normal, light)));
                    var col = GroundColour(x, z) * shade;
                    float g = Green(x, z);
                    if (g > 0.6f)
                    {
                        float wood = Mathf.PerlinNoise(x * 0.02f + 90f, z * 0.02f + 12f);
                        float spot = Hash(Mathf.Floor(x / 3f), Mathf.Floor(z / 3f));
                        if (((wood > 0.56f && spot < 0.55f) || spot < 0.04f) && !Reserved(x, z)) col = Color.Lerp(col, new Color(0.17f, 0.36f, 0.16f), 0.75f);
                    }
                    else if (g < 0.4f && Hash(Mathf.Floor(x / 3f), Mathf.Floor(z / 3f)) < 0.05f && !Reserved(x, z)) col = new Color(0.32f, 0.25f, 0.22f);
                    col.a = 1f;
                    c[j * px + i] = col;
                }
            }
            if (mapRow < px) return;
            void Roof(Vector2 at, float half, Color colour)
            {
                int ci = Mathf.FloorToInt(at.x / unit), cj = Mathf.FloorToInt(at.y / unit), r = Mathf.Max(1, Mathf.RoundToInt(half / unit));
                for (int j = cj - r; j <= cj + r; j++)
                    for (int i = ci - r; i <= ci + r; i++)
                        if (i >= 0 && j >= 0 && i < px && j < px) c[j * px + i] = (i == ci + r || j == cj - r) ? colour * 0.7f : colour;
            }
            foreach (var h in Houses) Roof(h.at, 2.8f, new Color(0.78f, 0.3f, 0.2f));
            foreach (var n in Neighbours)
            {
                Roof(n.at - n.toRoad * 8f - n.side * 3f, 2.8f, new Color(0.8f, 0.32f, 0.22f));
                Roof(n.at - n.toRoad * 6f + n.side * 8f, 3f, new Color(0.62f, 0.16f, 0.12f));
            }
            Roof(CoopAt, 4f, new Color(0.9f, 0.85f, 0.75f));
            Roof(Plaza + new Vector2(-16f, -16f), 2f, new Color(0.35f, 0.38f, 0.45f));
            map = new Texture2D(px, px, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "OwMap" };
            map.SetPixels(c);
            map.Apply(false, false);
            mapPixels = null;
            mapHeights = null;
        }

        /// <summary>Lets the map's picture go (leaving the open world).</summary>
        public static void ForgetMap()
        {
            if (map != null) Destroy(map);
            map = null;
            mapPixels = null;
            mapHeights = null;
        }

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
