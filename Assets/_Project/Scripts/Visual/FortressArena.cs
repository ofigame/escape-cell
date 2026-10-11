using System;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A sample battlefield (after the user's reference art): a square fortress floor of grey stone tiles raised on a
    /// plinth, ramparts with merlons round its edge, four tall sci-fi spire towers on the corners (stone and steel with
    /// glowing blue windows and copper bands), flights of stairs on the sides, gun turrets and a domed bunker in front,
    /// lamps on posts, and in the middle a round pedestal ringed with orange light where foi stands in a burst of fire.
    /// A sunset city skyline fills the back. Built far away with its own camera; <see cref="Cam"/> renders it.
    /// </summary>
    public class FortressArena : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(-9000f, 0f, 9000f);
        private const int Half = 5;           // tiles from the middle to the edge (an 11x11 floor)

        public Camera Cam { get; private set; }

        private Transform flames, ring;
        private Material fireMat, ringMat;
        private float time;

        public static FortressArena Create(Func<Transform, GameObject> robotLook, WorldTheme sky)
        {
            var go = new GameObject("FortressArena");
            go.transform.position = Origin;
            var a = go.AddComponent<FortressArena>();
            a.Build(robotLook, sky);
            return a;
        }

        // ---------- Materials ----------

        private static Material M(float r, float g, float b, float glow = 0f) =>
            MaterialFactory.Create(new Color(r, g, b), new Color(r, g, b) * glow);

        private Material stone, stoneDark, stoneLight, steel, copper, dark, blue, warm;

        /// <summary>The materials are made when the arena is built (Unity allows no materials in field initialisers).</summary>
        private void MakeMaterials()
        {
            stone = M(0.42f, 0.42f, 0.48f); stoneDark = M(0.27f, 0.27f, 0.33f); stoneLight = M(0.53f, 0.53f, 0.59f);
            steel = M(0.36f, 0.38f, 0.44f); copper = M(0.72f, 0.42f, 0.24f); dark = M(0.16f, 0.17f, 0.21f);
            blue = MaterialFactory.Create(new Color(0.35f, 0.75f, 1f), new Color(0.6f, 1.6f, 2.6f));
            warm = MaterialFactory.Create(new Color(1f, 0.62f, 0.25f), new Color(2.4f, 1.2f, 0.35f));
        }

        private Transform Box(string n, Transform p, Vector3 at, Vector3 size, Material m, float r = 0.04f) =>
            Shapes.Rounded(n, p, at, size, Mathf.Min(r, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f), m).transform;

        private Transform Cyl(string n, Transform p, Vector3 at, Vector3 size, Material m) =>
            Shapes.Primitive(PrimitiveType.Cylinder, n, p, at, size, m).transform;

        private Transform Ball(string n, Transform p, Vector3 at, Vector3 size, Material m) =>
            Shapes.Primitive(PrimitiveType.Sphere, n, p, at, size, m).transform;

        // ---------- Building ----------

        private void Build(Func<Transform, GameObject> robotLook, WorldTheme sky)
        {
            MakeMaterials();
            var t = transform;
            float edge = Half + 0.5f;

            // The plinth and its stone floor: tiles with a darker frame, a few cracked and darker ones.
            Box("Plinth", t, new Vector3(0f, -0.55f, 0f), new Vector3(edge * 2f + 1.4f, 1f, edge * 2f + 1.4f), stoneDark, 0.1f);
            Box("PlinthTop", t, new Vector3(0f, -0.08f, 0f), new Vector3(edge * 2f + 1.6f, 0.12f, edge * 2f + 1.6f), stone, 0.05f);
            var rng = new System.Random(7);
            for (int x = -Half; x <= Half; x++)
                for (int z = -Half; z <= Half; z++)
                {
                    var tileMat = rng.Next(9) == 0 ? stoneDark : rng.Next(5) == 0 ? stoneLight : stone;
                    Box("Tile", t, new Vector3(x, 0.02f, z), new Vector3(0.94f, 0.1f, 0.94f), tileMat, 0.05f);
                    if (rng.Next(7) == 0) Box("Crack", t, new Vector3(x + 0.1f, 0.075f, z - 0.1f), new Vector3(0.4f, 0.005f, 0.03f), dark, 0.005f).localRotation = Quaternion.Euler(0f, rng.Next(180), 0f);
                }

            // Ramparts round the edge with merlons, broken by the stairs in the middle of each side and the front gate.
            foreach (var (sx, sz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                for (int i = -Half - 1; i <= Half + 1; i++)
                {
                    if (Mathf.Abs(i) <= 1) continue; // a gap for the stairs
                    var at = sx != 0 ? new Vector3(sx * (edge + 0.35f), 0.35f, i) : new Vector3(i, 0.35f, sz * (edge + 0.35f));
                    Box("Wall", t, at, sx != 0 ? new Vector3(0.6f, 0.7f, 1.02f) : new Vector3(1.02f, 0.7f, 0.6f), stoneDark, 0.05f);
                    if (i % 2 == 0) Box("Merlon", t, at + Vector3.up * 0.5f, new Vector3(0.5f, 0.32f, 0.5f), stone, 0.05f);
                }
                // Warm lights along the wall.
                foreach (int i in new[] { -4, 4 })
                {
                    var lp = sx != 0 ? new Vector3(sx * (edge + 0.05f), 0.55f, i) : new Vector3(i, 0.55f, sz * (edge + 0.05f));
                    Box("Torch", t, lp, new Vector3(0.12f, 0.16f, 0.12f), warm, 0.03f);
                }
                // Stairs down the side.
                for (int s = 0; s < 4; s++)
                {
                    float o = edge + 0.5f + s * 0.35f, y = -0.05f - s * 0.25f;
                    var at = sx != 0 ? new Vector3(sx * o, y, 0f) : new Vector3(0f, y, sz * o);
                    Box("Step", t, at, sx != 0 ? new Vector3(0.36f, 0.25f, 2.6f) : new Vector3(2.6f, 0.25f, 0.36f), stoneLight, 0.03f);
                }
            }

            // The four corner spires.
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                Spire(new Vector3(sx * (edge + 0.2f), 0f, sz * (edge + 0.2f)));

            // Lamps along the floor's inner edge.
            foreach (var (x, z) in new[] { (-4.6f, -1.6f), (1.6f, -4.6f), (-4.6f, 3f), (4.6f, -2.5f), (2.5f, 4.6f), (-2.5f, 4.6f), (4.6f, 2.5f) })
            {
                Box("LampPost", t, new Vector3(x, 0.45f, z), new Vector3(0.08f, 0.8f, 0.08f), dark);
                Ball("Lamp", t, new Vector3(x, 0.9f, z), Vector3.one * 0.18f, warm);
            }

            // Crates and a few barrels for cover.
            foreach (var (x, z) in new[] { (-3.5f, 1f), (3.6f, 3.4f), (-1.8f, -3.4f) })
            {
                Box("Crate", t, new Vector3(x, 0.3f, z), Vector3.one * 0.45f, copper, 0.04f);
                Box("CrateBand", t, new Vector3(x, 0.3f, z), new Vector3(0.47f, 0.08f, 0.47f), dark, 0.02f);
            }

            // Turrets and a domed bunker out in front, on the plinth's lower ledge.
            Turret(new Vector3(-edge - 1.2f, -0.6f, -edge + 0.6f), 30f);
            Turret(new Vector3(edge + 1.2f, -0.6f, -edge + 0.6f), -30f);
            var bunker = new GameObject("Bunker").transform;
            bunker.SetParent(t, false);
            bunker.localPosition = new Vector3(0f, -0.6f, -edge - 1.3f);
            Cyl("Base", bunker, new Vector3(0f, 0.2f, 0f), new Vector3(1.3f, 0.2f, 1.3f), stoneDark);
            Ball("Dome", bunker, new Vector3(0f, 0.45f, 0f), new Vector3(1.1f, 0.8f, 1.1f), steel);
            Box("Slit", bunker, new Vector3(0f, 0.55f, -0.52f), new Vector3(0.5f, 0.06f, 0.05f), blue, 0.02f);

            // The pedestal in the middle, ringed with orange light, foi on top in a burst of fire.
            Cyl("Pedestal", t, new Vector3(0f, 0.18f, 0f), new Vector3(2.2f, 0.16f, 2.2f), stoneDark);
            Cyl("PedestalRim", t, new Vector3(0f, 0.34f, 0f), new Vector3(2.3f, 0.03f, 2.3f), copper);
            ringMat = MaterialFactory.Create(new Color(1f, 0.6f, 0.25f), new Color(2.6f, 1.3f, 0.4f));
            ring = Cyl("Ring", t, new Vector3(0f, 0.37f, 0f), new Vector3(1.5f, 0.015f, 1.5f), ringMat);
            Cyl("Disc", t, new Vector3(0f, 0.38f, 0f), new Vector3(1.3f, 0.02f, 1.3f), MaterialFactory.Create(new Color(0.42f, 0.36f, 0.62f), new Color(0.15f, 0.1f, 0.3f)));
            fireMat = MaterialFactory.CreateTransparent(new Color(1f, 0.55f, 0.15f, 0.55f), new Color(3f, 1.4f, 0.3f));
            flames = new GameObject("Flames").transform;
            flames.SetParent(t, false);
            flames.localPosition = new Vector3(0f, 0.4f, 0f);
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f;
                var f = Ball("Flame", flames, new Vector3(Mathf.Cos(a) * 0.45f, 0.35f, Mathf.Sin(a) * 0.45f), new Vector3(0.22f, 0.7f, 0.22f), fireMat);
                f.localRotation = Quaternion.Euler(Mathf.Sin(a) * 15f, 0f, -Mathf.Cos(a) * 15f);
            }
            var holder = new GameObject("Foi").transform;
            holder.SetParent(t, false);
            holder.localPosition = new Vector3(0f, 0.4f, 0f);
            holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
            holder.localScale = Vector3.one * 2.1f;
            if (robotLook != null) robotLook(holder);

            // Light, camera and the sunset city behind.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(t, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.85f, 0.75f);
            sun.intensity = 1.05f;
            sun.transform.rotation = Quaternion.Euler(48f, 150f, 0f);
            Cam = new GameObject("FortressCamera").AddComponent<Camera>();
            Cam.transform.SetParent(t, false);
            Cam.orthographic = true;
            Cam.orthographicSize = 8.4f;
            Cam.transform.rotation = Quaternion.Euler(36f, 0f, 0f); // from the front, as in the art: the spires frame the floor left and right
            Cam.transform.position = transform.position + new Vector3(0f, 0.6f, 0f) - Cam.transform.forward * 40f;
            Cam.nearClipPlane = 1f;
            Cam.farClipPlane = 120f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.2f, 0.15f, 0.3f);
            Cam.depth = 50;
            var back = Shapes.Primitive(PrimitiveType.Quad, "Sky", Cam.transform, new Vector3(0f, 0f, 100f), Vector3.one, null).transform;
            float h = Cam.orthographicSize * 2f;
            back.localScale = new Vector3(h * 1.2f, h * 1.05f, 1f);
            back.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.CreateUnlit(BackgroundArt.Generate(1400, 1200, sky));
        }

        /// <summary>A corner spire: a stone base, a tall steel shaft with copper bands and blue windows, a crown and an antenna.</summary>
        private void Spire(Vector3 at)
        {
            var s = new GameObject("Spire").transform;
            s.SetParent(transform, false);
            s.localPosition = at;
            Box("Base", s, new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 1.2f, 1.9f), stoneDark, 0.1f);
            Box("BaseTrim", s, new Vector3(0f, 1.22f, 0f), new Vector3(2f, 0.1f, 2f), copper, 0.03f);
            Box("Door", s, new Vector3(0f, 0.5f, -0.96f), new Vector3(0.5f, 0.7f, 0.03f), blue, 0.08f);
            Cyl("Shaft", s, new Vector3(0f, 2.9f, 0f), new Vector3(1.25f, 1.7f, 1.25f), stone);
            foreach (float y in new[] { 1.6f, 2.9f, 4.2f }) Cyl("Band", s, new Vector3(0f, y, 0f), new Vector3(1.32f, 0.06f, 1.32f), copper);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                foreach (float y in new[] { 2.25f, 3.55f })
                    Box("Window", s, new Vector3(Mathf.Cos(a) * 0.62f, y, Mathf.Sin(a) * 0.62f), new Vector3(0.16f, 0.36f, 0.04f), blue, 0.03f)
                        .localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
            Cyl("Gallery", s, new Vector3(0f, 4.7f, 0f), new Vector3(1.75f, 0.14f, 1.75f), steel);
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; Box("Light", s, new Vector3(Mathf.Cos(a) * 0.86f, 4.7f, Mathf.Sin(a) * 0.86f), new Vector3(0.1f, 0.08f, 0.1f), warm, 0.02f); }
            Cyl("Crown", s, new Vector3(0f, 5.2f, 0f), new Vector3(0.95f, 0.45f, 0.95f), stoneLight);
            Ball("Cap", s, new Vector3(0f, 5.7f, 0f), new Vector3(0.85f, 0.55f, 0.85f), steel);
            Cyl("Antenna", s, new Vector3(0f, 6.5f, 0f), new Vector3(0.07f, 0.6f, 0.07f), dark);
            Ball("Tip", s, new Vector3(0f, 7.15f, 0f), Vector3.one * 0.18f, warm);
        }

        /// <summary>A heavy gun turret: a round armoured base and a barrel aimed out over the field.</summary>
        private void Turret(Vector3 at, float yaw)
        {
            var g = new GameObject("Turret").transform;
            g.SetParent(transform, false);
            g.localPosition = at;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Cyl("Base", g, new Vector3(0f, 0.25f, 0f), new Vector3(1.2f, 0.25f, 1.2f), steel);
            Cyl("Ring", g, new Vector3(0f, 0.52f, 0f), new Vector3(1.25f, 0.04f, 1.25f), copper);
            Ball("Body", g, new Vector3(0f, 0.65f, 0f), new Vector3(1f, 0.7f, 1f), stoneDark);
            Cyl("Barrel", g, new Vector3(0f, 0.75f, -0.75f), new Vector3(0.28f, 0.5f, 0.28f), dark).localRotation = Quaternion.Euler(90f, 0f, 0f);
            Cyl("Muzzle", g, new Vector3(0f, 0.75f, -1.25f), new Vector3(0.36f, 0.06f, 0.36f), copper).localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box("Light", g, new Vector3(0f, 0.95f, -0.42f), new Vector3(0.2f, 0.05f, 0.05f), blue, 0.02f);
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (flames != null)
            {
                flames.localRotation = Quaternion.Euler(0f, time * 40f, 0f);
                float s = 1f + Mathf.Sin(time * 9f) * 0.08f;
                flames.localScale = new Vector3(1f, s, 1f);
                MaterialFactory.SetColors(fireMat, new Color(1f, 0.55f, 0.15f, 0.5f + Mathf.Sin(time * 13f) * 0.08f), new Color(3f, 1.4f, 0.3f) * (0.9f + Mathf.Sin(time * 11f) * 0.15f));
            }
            if (ring != null) MaterialFactory.SetColors(ringMat, new Color(1f, 0.6f, 0.25f), new Color(2.6f, 1.3f, 0.4f) * (0.8f + Mathf.Sin(time * 4f) * 0.25f));
        }
    }
}
