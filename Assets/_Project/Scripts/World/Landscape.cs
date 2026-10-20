using System.Collections.Generic;
using BrickMuseum.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrickMuseum.World
{
    /// <summary>
    /// The scenery around the workbench and the city: sky, sun, fog, a green valley with a lake, pine woods, rolling hills
    /// and snowy mountains in rings, clouds. Units are studs (the workbench scale).
    /// </summary>
    public static class Landscape
    {
        public const float Ground = -18f;
        /// <summary>Middle of the city, north of the workbench deck.</summary>
        public static readonly Vector3 CityCenter = new Vector3(0f, Ground, -300f);
        /// <summary>The lake between the workbench (south shore) and the city's quay (north shore), in world z.</summary>
        public static float LakeNorth => CityCenter.z + City.CityLayout.MaxZ;
        public const float LakeSouth = -56f, LakeHalfWidth = 380f;
        private static float CityWest => CityCenter.x + City.CityLayout.MinX;
        private static float CityEast => CityCenter.x + City.CityLayout.MaxX;
        private static float CityNorth => CityCenter.z + City.CityLayout.MinZ;

        public static Light Build(Transform root)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(48f, 28f, 0f);
            sun.cullingMask = ~(1 << Previews.PictureLayer);
            RenderSettings.sun = sun;

            var sky = Resources.Load<Material>("Mats/Sky");
            if (sky == null) sky = new Material(Shader.Find("Skybox/Procedural"));
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.66f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.34f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.76f, 0.84f, 0.92f);
            RenderSettings.fogStartDistance = 260f;
            RenderSettings.fogEndDistance = 3200f;

            var land = new GameObject("Landscape").transform;
            land.SetParent(root, false);

            // the valley floor, with an opening along the town's creek: the creek lies below the ground's level, and the
            // town's own streets and the creek's banks cover the opening (without it the grass hid the water)
            var ground = new MeshBuilder();
            float hx0 = CityCenter.x + City.CityLayout.CreekX - City.CityLayout.Creek / 2f - 20f, hx1 = hx0 + City.CityLayout.Creek + 40f;
            float hz0 = CityCenter.z + City.CityLayout.MinZ + 16f, hz1 = LakeNorth;
            const float R = 4500f, T = 0.5f;
            void Slab(float x0, float x1, float z0, float z1) => ground.Box(new Vector3((x0 + x1) / 2f, Ground - T / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, T, z1 - z0));
            Slab(-R, hx0, -R, R);
            Slab(hx1, R, -R, R);
            Slab(hx0, hx1, -R, hz0);
            Slab(hx0, hx1, hz1, R);
            Add(land, "Ground", ground.ToMesh("ground"), Mats.Lit(new Color(0.42f, 0.62f, 0.32f), 0.05f), false, true);

            BuildLake(land);

            var middle = new Vector3(-220f, 0f, -460f);   // between the deck, Gökkent (west) and the village (north)
            Ring(land, "Hills", 1150f, 260f, 30f, 110f, 3.1f, new Color(0.33f, 0.5f, 0.3f), 0f, middle);
            Ring(land, "Mountains", 1750f, 420f, 160f, 420f, 7.7f, new Color(0.45f, 0.52f, 0.6f), 300f, middle);
            Ring(land, "FarMountains", 2450f, 500f, 260f, 650f, 1.3f, new Color(0.58f, 0.64f, 0.74f), 470f, middle);
            Trees(land);
            Clouds(land);
            return sun;
        }

        private static void Add(Transform parent, string name, Mesh mesh, Material mat, bool castShadows, bool receive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receive;
        }

        /// <summary>A ring of ridges around the valley; peaks above snowLine get white caps.</summary>
        private static void Ring(Transform parent, string name, float radius, float depth, float minH, float maxH, float seed, Color color, float snowLine, Vector3 middle)
        {
            const int segs = 180;
            var body = new MeshBuilder();
            var snow = new MeshBuilder();
            var ridge = new Vector3[segs + 1];
            for (int i = 0; i <= segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                float n = Mathf.PerlinNoise(seed + Mathf.Cos(a) * 2.2f + 10f, seed + Mathf.Sin(a) * 2.2f + 10f) * 0.7f
                          + Mathf.PerlinNoise(seed * 2 + Mathf.Cos(a) * 7f + 30f, Mathf.Sin(a) * 7f + 30f) * 0.3f;
                float h = Mathf.Lerp(minH, maxH, Mathf.SmoothStep(0f, 1f, n));
                float r = radius + (Mathf.PerlinNoise(seed + i * 0.13f, 3f) - 0.5f) * depth * 0.5f;
                ridge[i] = new Vector3(Mathf.Cos(a) * r, Ground + h, Mathf.Sin(a) * r) + middle;
            }
            for (int i = 0; i < segs; i++)
            {
                Vector3 r0 = ridge[i], r1 = ridge[i + 1];
                Vector3 in0 = Flat(r0) * (1f - depth * 0.5f / Flat(r0).magnitude), in1 = Flat(r1) * (1f - depth * 0.5f / Flat(r1).magnitude);
                Vector3 out0 = Flat(r0) * (1f + depth * 0.5f / Flat(r0).magnitude), out1 = Flat(r1) * (1f + depth * 0.5f / Flat(r1).magnitude);
                in0.y = in1.y = out0.y = out1.y = Ground;
                var inward = -(r0 + r1).normalized + Vector3.up;
                body.Face(in0, r0, r1, inward); body.Face(in0, r1, in1, inward);
                var outward = (Flat(r0) + Flat(r1)).normalized + Vector3.up;
                body.Face(out0, r1, r0, outward); body.Face(out0, out1, r1, outward);
                if (snowLine > 0 && (r0.y - Ground > snowLine || r1.y - Ground > snowLine))
                {
                    // a cap sitting just above the slope: top 18% of the height, a hair in front of the rock
                    Vector3 s0 = Vector3.Lerp(r0, in0, 0.18f), s1 = Vector3.Lerp(r1, in1, 0.18f);
                    var lift = inward.normalized * 1.5f;
                    snow.Face(s0 + lift, r0 + lift, r1 + lift, inward); snow.Face(s0 + lift, r1 + lift, s1 + lift, inward);
                }
            }
            Add(parent, name, body.ToMesh(name), Mats.Lit(color, 0.05f), false, false);
            if (snow.VertexCount > 0) Add(parent, name + "Snow", snow.ToMesh(name + " snow"), Mats.Lit(new Color(0.95f, 0.96f, 0.98f), 0.3f), false, false);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>A long lake with rounded ends, a sandy beach round it and a few boats.</summary>
        private static void BuildLake(Transform parent)
        {
            float north = LakeNorth, south = LakeSouth, mid = (north + south) / 2f, half = (south - north) / 2f;
            var sand = new MeshBuilder();
            sand.Box(new Vector3(0, Ground + 0.02f, mid), new Vector3(LakeHalfWidth * 2f, 0.04f, (south - north) + 10f));
            foreach (float sx in new[] { -1f, 1f }) sand.Cylinder(new Vector3(sx * LakeHalfWidth, Ground, mid), half + 5f, half + 5f, 0.04f, 32, true, false);
            Add(parent, "Beach", sand.ToMesh("beach"), Mats.Lit(new Color(0.86f, 0.8f, 0.62f), 0.05f), false, true);
            var water = new MeshBuilder();
            water.Box(new Vector3(0, Ground + 0.06f, mid), new Vector3(LakeHalfWidth * 2f, 0.04f, south - north));
            foreach (float sx in new[] { -1f, 1f }) water.Cylinder(new Vector3(sx * LakeHalfWidth, Ground + 0.04f, mid), half, half, 0.04f, 32, true, false);
            // under the water: a pale sandy shelf along the town's quay, then the deep dark bed
            var shelf = new MeshBuilder();
            shelf.Box(new Vector3(0, Ground - 0.9f, north + 7f), new Vector3(LakeHalfWidth * 2f, 0.2f, 14f));
            Add(parent, "LakeShelf", shelf.ToMesh("shelf"), Mats.Lit(new Color(0.62f, 0.62f, 0.46f), 0.05f), false, true);
            var bed = new MeshBuilder();
            bed.Box(new Vector3(0, Ground - 3.2f, mid + 6f), new Vector3(LakeHalfWidth * 2f + half * 2f, 0.2f, south - north));
            foreach (float sx in new[] { -1f, 1f }) bed.Cylinder(new Vector3(sx * LakeHalfWidth, Ground - 3.3f, mid), half, half, 0.2f, 32, true, false);
            Add(parent, "LakeBed", bed.ToMesh("bed"), Mats.Lit(new Color(0.1f, 0.24f, 0.32f), 0.05f), false, true);
            var lakeMesh = water.ToMesh("lake");
            Water.PlanarUV(lakeMesh, 0.04f);
            Add(parent, "Lake", lakeMesh, Water.Lake, false, true);

            var hulls = new MeshBuilder();
            var cabins = new MeshBuilder();
            foreach (var (x, z, len) in new[] { (-150f, mid - 6f, 7f), (60f, mid + 8f, 5f), (210f, mid - 10f, 9f), (-260f, mid + 4f, 6f) })
            {
                hulls.Box(new Vector3(x, Ground + 0.5f, z), new Vector3(len, 0.9f, len * 0.36f));
                cabins.Box(new Vector3(x - len * 0.1f, Ground + 1.3f, z), new Vector3(len * 0.35f, 0.8f, len * 0.26f));
                cabins.Box(new Vector3(x + len * 0.05f, Ground + 3.5f, z), new Vector3(0.15f, 5f, 0.15f));   // mast
            }
            Add(parent, "Boats", hulls.ToMesh("boats"), Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.4f), true, true);
            Add(parent, "BoatCabins", cabins.ToMesh("cabins"), Mats.Lit(new Color(0.55f, 0.36f, 0.22f), 0.3f), true, true);
        }

        private static bool InLake(Vector3 p) => p.z > LakeNorth - 8f && p.z < LakeSouth + 12f && Mathf.Abs(p.x) < LakeHalfWidth + (LakeSouth - LakeNorth) / 2f + 8f;
        private static bool InCity(Vector3 p)
        {
            var plan = new Vector2(p.x - CityCenter.x, p.z - CityCenter.z);
            if (City.CityLayout.InFar(plan, 30f) || City.CityLayout.OnCountryRoad(plan, 18f)) return true;   // the far district and its road
            if (City.CityLayout.InMega(plan, 40f) || City.CityLayout.OnHighway(plan, 22f)) return true;      // Gökkent and the highway
            return p.x > CityWest - City.CityLayout.HomeLot - 30f && p.x < CityEast + 30f && p.z > CityNorth - 30f && p.z < LakeNorth + 4f;
        }

        /// <summary>Pine woods across the valley and a thick forest north of the town, kept off the deck, the lake and the city.</summary>
        private static void Trees(Transform parent)
        {
            var leaves = new MeshBuilder();
            var trunks = new MeshBuilder();
            var rnd = new System.Random(11);
            int placed = 0;
            for (int tries = 0; tries < 4000 && placed < 340; tries++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = Mathf.Lerp(70f, 820f, Mathf.Sqrt((float)rnd.NextDouble()));
                var p = new Vector3(Mathf.Cos(a) * r, Ground, Mathf.Sin(a) * r);
                if (Mathf.Abs(p.x) < 60f && p.z > -60f && p.z < 80f) continue;                              // the deck
                if (InLake(p) || InCity(p)) continue;
                float s = Mathf.Lerp(0.7f, 1.5f, (float)rnd.NextDouble());
                trunks.Cylinder(p, 1.6f * s, 1.2f * s, 7f * s, 6, false, false);
                leaves.Cylinder(p + Vector3.up * 5f * s, 9f * s, 0f, 16f * s, 7, false, true);
                leaves.Cylinder(p + Vector3.up * 13f * s, 7f * s, 0f, 14f * s, 7, false, true);
                placed++;
            }
            // the forest the town grows into: dense pines north of the last street
            for (int tries = 0; tries < 6000 && placed < 1100; tries++)
            {
                var p = new Vector3(Mathf.Lerp(CityWest - 140f, CityEast + 140f, (float)rnd.NextDouble()), Ground, CityNorth - 18f - Mathf.Pow((float)rnd.NextDouble(), 1.4f) * 420f);
                if (InCity(p)) continue;
                float s = Mathf.Lerp(0.8f, 1.7f, (float)rnd.NextDouble());
                trunks.Cylinder(p, 1.6f * s, 1.2f * s, 7f * s, 6, false, false);
                leaves.Cylinder(p + Vector3.up * 5f * s, 9f * s, 0f, 16f * s, 7, false, true);
                leaves.Cylinder(p + Vector3.up * 13f * s, 7f * s, 0f, 14f * s, 7, false, true);
                placed++;
            }
            Add(parent, "Pines", leaves.ToMesh("pines"), Mats.Lit(new Color(0.18f, 0.42f, 0.26f), 0.05f), true, true);
            Add(parent, "Trunks", trunks.ToMesh("trunks"), Mats.Lit(new Color(0.42f, 0.28f, 0.18f), 0.05f), true, true);

        }

        private static void Clouds(Transform parent)
        {
            var b = new MeshBuilder();
            var rnd = new System.Random(5);
            for (int i = 0; i < 18; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = Mathf.Lerp(700f, 1900f, (float)rnd.NextDouble());
                var c = new Vector3(Mathf.Cos(a) * r, Ground + Mathf.Lerp(380f, 620f, (float)rnd.NextDouble()), Mathf.Sin(a) * r);
                int puffs = 3 + rnd.Next(3);
                for (int k = 0; k < puffs; k++)
                {
                    var off = new Vector3((k - puffs / 2f) * 55f, (float)rnd.NextDouble() * 12f, (float)(rnd.NextDouble() - 0.5) * 40f);
                    float s = Mathf.Lerp(45f, 80f, (float)rnd.NextDouble());
                    b.Dome(c + off, s, s * 0.55f, 14);
                }
            }
            var m = Mats.Unlit(new Color(1f, 1f, 1f, 0.92f));
            Add(parent, "Clouds", b.ToMesh("clouds"), m, false, false);
        }
    }
}
