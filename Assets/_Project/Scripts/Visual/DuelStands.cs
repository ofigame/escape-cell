using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SquashBot.Visual
{
    /// <summary>
    /// The duel's arena round the floor: a stand right against each end of the floor (the screen's left and right),
    /// one full of the player's fans in blue, the other of the rival's in red — never mixed — with flags waving, a
    /// roof in the side's colour and banners on the back wall. The camera's side stays open; across the floor, facing
    /// the camera, a backdrop in the stage's theme: a city skyline, a glacier, a volcano, deep space, a forest with a
    /// temple, or a desert with pyramids. The crowd is drawn instanced and cheers for its side; a murmur plays under it.
    /// </summary>
    public class DuelStands : MonoBehaviour
    {
        public enum Theme { City, Glacier, Volcano, Space, Forest, Desert }

        private const int Tiers = 7;
        private const float PodiumH = 0.55f, TierStep = 0.95f, TierRise = 0.55f;

        private Vector3 centre;
        private float hw, hd, floorY;
        private readonly List<Material> mats = new List<Material>();

        private class Batch { public Mesh mesh; public Material mat; public readonly List<Vector3> pos = new List<Vector3>(); public readonly List<float> phase = new List<float>(); public readonly List<int> side = new List<int>(); public readonly List<float> facing = new List<float>(); public Matrix4x4[] m; public int kind; }
        private readonly List<Batch> batches = new List<Batch>();
        private readonly float[] excite = new float[2];
        private bool wave;
        private float waveT;
        private AudioSource murmur;
        private readonly Matrix4x4[] chunk = new Matrix4x4[1023];

        /// <param name="playerSouth">The player's foi starts at the south end (the left of the screen): its fans fill that stand.</param>
        public static DuelStands Build(Transform parent, Vector3 centre, float halfWidth, float halfDepth, float floorY, int seed, bool playerSouth, Theme theme)
        {
            var go = new GameObject("DuelStands");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DuelStands>();
            d.centre = centre;
            d.hw = halfWidth;
            d.hd = halfDepth;
            d.floorY = floorY;
            var rng = new System.Random(seed);
            var decor = new GameObject("Decor").transform;
            decor.SetParent(go.transform, false);
            foreach (int s in new[] { -1, 1 })
            {
                int team = (s < 0) == playerSouth ? 0 : 1;
                d.Stand(decor, s, team);
                d.Crowd(rng, s, team);
            }
            d.Backdrop(decor, theme, rng);
            StaticMerge.Combine(decor.gameObject, t => false);
            foreach (var r in decor.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            foreach (var bt in d.batches) bt.m = new Matrix4x4[bt.pos.Count];
            d.MakeMurmur();
            return d;
        }

        /// <summary>A side's stand cheers (0 the player's, 1 the rival's, -1 both): 0..1, it settles back by itself.</summary>
        public void Cheer(int side, float strength)
        {
            if (side < 0) { excite[0] = Mathf.Max(excite[0], strength); excite[1] = Mathf.Max(excite[1], strength); }
            else excite[Mathf.Clamp(side, 0, 1)] = Mathf.Max(excite[Mathf.Clamp(side, 0, 1)], strength);
        }

        public void Wave(bool on) => wave = on;

        private Material M(Color c, float glow = 0f, bool instanced = false)
        {
            var m = MaterialFactory.Create(c, c * glow);
            m.enableInstancing = instanced;
            mats.Add(m);
            return m;
        }

        private Material Surface(Color c, float smooth) { var m = MaterialFactory.CreateSurface(c, Color.black, smooth, 0f); mats.Add(m); return m; }

        private static readonly Color Blue = new Color(0.2f, 0.45f, 0.95f), Red = new Color(0.9f, 0.2f, 0.18f);

        // ---------- The stands ----------

        /// <summary>One stand at an end of the floor (s: -1 south, 1 north): its podium against the floor, tiers rising
        /// away, a back wall with banners, a roof in the side's colour on posts, closed at both ends.</summary>
        private void Stand(Transform p, int s, int team)
        {
            var stone = Surface(new Color(0.8f, 0.78f, 0.74f), 0.15f);
            var riser = Surface(new Color(0.6f, 0.58f, 0.56f), 0.1f);
            var colour = team == 0 ? Blue : Red;
            var side = M(colour, 0.1f);
            var dark = M(new Color(0.16f, 0.16f, 0.2f));
            var white = M(new Color(0.95f, 0.95f, 0.97f), 0.1f);
            float x0 = centre.x - hw - 1.5f, x1 = centre.x + hw + 0.6f, len = x1 - x0, mx = (x0 + x1) * 0.5f;
            float z = centre.z + s * (hd + 0.4f);
            float y = floorY;
            // The podium right at the floor's edge, a stripe in the side's colour along its face.
            Shapes.Rounded("Podium", p, new Vector3(mx, y + PodiumH * 0.5f, z + s * 0.3f), new Vector3(len, PodiumH, 0.6f), 0.04f, riser);
            Shapes.Rounded("Stripe", p, new Vector3(mx, y + PodiumH * 0.6f, z - s * 0.02f), new Vector3(len, 0.12f, 0.04f), 0.01f, side);
            float off = 0.6f, top = y + PodiumH;
            for (int i = 0; i < Tiers; i++)
            {
                top += TierRise;
                float zc = z + s * (off + TierStep * 0.5f);
                Shapes.Rounded("Tier", p, new Vector3(mx, top - (top - y) * 0.5f, zc), new Vector3(len, top - y, TierStep), 0.03f, i % 2 == 0 ? stone : riser);
                // A colour band on every other step's front.
                if (i % 2 == 1) Shapes.Rounded("Band", p, new Vector3(mx, top - TierRise * 0.5f, z + s * off - s * 0.01f), new Vector3(len, 0.08f, 0.03f), 0.01f, side);
                off += TierStep;
            }
            float back = z + s * (off + 0.25f);
            Shapes.Rounded("BackWall", p, new Vector3(mx, (y + top + 2.4f) * 0.5f, back), new Vector3(len + 0.4f, top + 2.4f - y, 0.5f), 0.04f, dark);
            // Banners on the back wall, the side's colour with a white stripe.
            int banners = Mathf.RoundToInt(len / 3.2f);
            for (int i = 0; i < banners; i++)
            {
                float bx = x0 + (i + 0.5f) * len / banners;
                Shapes.Rounded("Banner", p, new Vector3(bx, top + 1.2f, back - s * 0.28f), new Vector3(1.1f, 1.8f, 0.04f), 0.01f, side);
                Shapes.Rounded("BannerStripe", p, new Vector3(bx, top + 1.2f, back - s * 0.31f), new Vector3(0.25f, 1.8f, 0.03f), 0.01f, white);
            }
            // Ends closed by walls, and the roof over the upper rows on posts.
            foreach (float ex in new[] { x0 - 0.25f, x1 + 0.25f })
                Shapes.Rounded("EndWall", p, new Vector3(ex, (y + top + 2.4f) * 0.5f, z + s * (off * 0.5f + 0.2f)), new Vector3(0.5f, top + 2.4f - y, off + 0.9f), 0.04f, dark);
            float roofY = top + 2.6f;
            Shapes.Rounded("Roof", p, new Vector3(mx, roofY, z + s * (off * 0.6f)), new Vector3(len + 1.2f, 0.25f, off * 0.85f), 0.05f, side).transform.localRotation = Quaternion.Euler(-s * 8f, 0f, 0f);
            for (int i = 0; i <= 4; i++)
                Shapes.Rounded("RoofPost", p, new Vector3(x0 + i * len / 4f, (top + roofY) * 0.5f, back - s * 0.1f), new Vector3(0.18f, roofY - top, 0.18f), 0.03f, white);
        }

        // ---------- The crowd ----------

        private void Crowd(System.Random rng, int s, int team)
        {
            if (batches.Count == 0) MakeBatches();
            float x0 = centre.x - hw - 1.2f, x1 = centre.x + hw + 0.3f;
            float z = centre.z + s * (hd + 0.4f);
            float off = 0.6f, y = floorY + PodiumH;
            float facing = s < 0 ? 0f : 180f; // looking at the floor
            for (int tier = 0; tier < Tiers; tier++)
            {
                y += TierRise;
                float seatZ = z + s * (off + TierStep * 0.45f);
                for (float x = x0 + (tier % 2) * 0.21f; x < x1; x += 0.42f)
                {
                    if (rng.NextDouble() < 0.05) continue; // an empty seat here and there
                    float phase = (float)rng.NextDouble() * 10f;
                    var body = batches[team * 4 + rng.Next(4)];
                    body.pos.Add(new Vector3(x, y + 0.18f, seatZ)); body.phase.Add(phase); body.side.Add(team); body.facing.Add(facing);
                    var head = batches[8 + rng.Next(4)];
                    head.pos.Add(new Vector3(x, y + 0.46f, seatZ)); head.phase.Add(phase); head.side.Add(team); head.facing.Add(facing);
                    // Some wave a flag in their side's colour.
                    if (rng.NextDouble() < 0.12)
                    {
                        var flag = batches[12 + team];
                        flag.pos.Add(new Vector3(x + 0.1f, y + 0.85f, seatZ)); flag.phase.Add(phase); flag.side.Add(team); flag.facing.Add(facing);
                    }
                }
                off += TierStep;
            }
        }

        /// <summary>The crowd's batches: 4 shades of each side's shirts, 4 skins, the two sides' flags.</summary>
        private void MakeBatches()
        {
            var blues = new[] { new Color(0.22f, 0.5f, 1f), new Color(0.35f, 0.62f, 1f), new Color(0.16f, 0.38f, 0.82f), new Color(0.55f, 0.75f, 1f) };
            var reds = new[] { new Color(1f, 0.32f, 0.3f), new Color(0.88f, 0.22f, 0.2f), new Color(1f, 0.5f, 0.42f), new Color(0.75f, 0.16f, 0.15f) };
            var skins = new[] { new Color(0.95f, 0.78f, 0.64f), new Color(0.85f, 0.64f, 0.48f), new Color(0.62f, 0.44f, 0.3f), new Color(0.4f, 0.28f, 0.2f) };
            var cube = Shapes.MeshOf(PrimitiveType.Cube);
            for (int i = 0; i < 4; i++) batches.Add(new Batch { mesh = cube, mat = M(blues[i], 0.08f, true), kind = 0 });
            for (int i = 0; i < 4; i++) batches.Add(new Batch { mesh = cube, mat = M(reds[i], 0.08f, true), kind = 0 });
            for (int i = 0; i < 4; i++) batches.Add(new Batch { mesh = cube, mat = M(skins[i], 0.05f, true), kind = 1 });
            batches.Add(new Batch { mesh = cube, mat = M(Blue, 0.2f, true), kind = 2 });
            batches.Add(new Batch { mesh = cube, mat = M(Red, 0.2f, true), kind = 2 });
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime, time = Time.time;
            for (int s = 0; s < 2; s++) excite[s] = Mathf.MoveTowards(excite[s], 0f, dt * 0.25f);
            if (wave) waveT += dt * 1.6f;
            var headScale = Vector3.one * 0.2f;
            var bodyScale = new Vector3(0.3f, 0.36f, 0.22f);
            var flagScale = new Vector3(0.34f, 0.22f, 0.02f);
            foreach (var b in batches)
            {
                if (b.m == null) continue;
                for (int i = 0; i < b.pos.Count; i++)
                {
                    float ex = excite[b.side[i]];
                    float jump = Mathf.Max(0f, Mathf.Sin(time * (6f + ex * 4f) + b.phase[i])) * (0.02f + ex * 0.22f);
                    if (wave) jump += Mathf.Pow(Mathf.Max(0f, Mathf.Cos(b.pos[i].x * 0.4f - waveT)), 8f) * 0.3f;
                    if (b.kind == 2)
                    {
                        // A flag swinging side to side, higher and faster when its side cheers.
                        float swing = Mathf.Sin(time * (3f + ex * 5f) + b.phase[i]) * (20f + ex * 25f);
                        b.m[i] = Matrix4x4.TRS(b.pos[i] + Vector3.up * jump, Quaternion.Euler(0f, b.facing[i], swing), flagScale);
                        continue;
                    }
                    var rot = Quaternion.Euler(0f, b.facing[i], Mathf.Sin(time * 2f + b.phase[i]) * (2f + ex * 8f));
                    b.m[i] = Matrix4x4.TRS(b.pos[i] + Vector3.up * jump, rot, b.kind == 1 ? headScale : bodyScale);
                }
                for (int k = 0; k < b.m.Length; k += 1023)
                {
                    int n = Mathf.Min(1023, b.m.Length - k);
                    System.Array.Copy(b.m, k, chunk, 0, n);
                    Graphics.DrawMeshInstanced(b.mesh, 0, b.mat, chunk, n, null, ShadowCastingMode.Off, true);
                }
            }
            if (murmur != null)
            {
                float target = (0.1f + Mathf.Max(excite[0], excite[1]) * 0.35f + (wave ? 0.12f : 0f)) * (Data.SaveData.Sound ? 1f : 0f);
                murmur.volume = Mathf.MoveTowards(murmur.volume, target, dt * 0.6f);
                murmur.pitch = 1f + Mathf.Max(excite[0], excite[1]) * 0.08f;
            }
        }

        // ---------- The backdrop across the floor (the west, facing the camera) ----------

        private static readonly Dictionary<int, Mesh> cones = new Dictionary<int, Mesh>();

        /// <summary>A cone (or a pyramid with 4 sides) 1 wide and 1 tall on its base.</summary>
        private static Mesh Cone(int sides)
        {
            if (cones.TryGetValue(sides, out var m) && m != null) return m;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            float tw = sides == 4 ? 0.5f : 0f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i + tw) / sides * Mathf.PI * 2f, a1 = (i + 1 + tw) / sides * Mathf.PI * 2f;
                var b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                var b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                var tip = new Vector3(0f, 1f, 0f);
                var normal = Vector3.Cross(tip - b0, b1 - b0).normalized;
                int k = v.Count;
                v.Add(b0); v.Add(tip); v.Add(b1);
                n.Add(normal); n.Add(normal); n.Add(normal);
                t.Add(k); t.Add(k + 1); t.Add(k + 2);
            }
            m = new Mesh { name = "Cone" + sides };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            cones[sides] = m;
            return m;
        }

        private static void Piece(Transform p, string name, Mesh mesh, Vector3 at, Vector3 scale, Material mat, float turn = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.position = at;
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(0f, turn, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private void Backdrop(Transform p, Theme theme, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float west = centre.x - hw - 2.5f; // just beyond the floor's far edge
            float y = floorY - 0.6f;
            Vector3 At(float back, float across, float up = 0f) => new Vector3(west - back, y + up, centre.z + across);
            // The ground beyond: one wide slab in the theme's colour, out to the horizon.
            Color groundC = theme switch
            {
                Theme.City => new Color(0.3f, 0.32f, 0.36f), Theme.Glacier => new Color(0.82f, 0.9f, 0.96f), Theme.Volcano => new Color(0.18f, 0.13f, 0.12f),
                Theme.Space => new Color(0.12f, 0.12f, 0.18f), Theme.Forest => new Color(0.24f, 0.42f, 0.2f), _ => new Color(0.9f, 0.72f, 0.45f),
            };
            if (theme != Theme.Space) Shapes.Rounded("Ground", p, At(48f, 0f, -0.3f), new Vector3(92f, 0.6f, 140f), 0.05f, Surface(groundC, theme == Theme.Glacier ? 0.7f : 0.1f));
            switch (theme)
            {
                case Theme.City:
                {
                    var walls = new[] { Surface(new Color(0.55f, 0.6f, 0.68f), 0.5f), Surface(new Color(0.42f, 0.46f, 0.55f), 0.5f), Surface(new Color(0.7f, 0.72f, 0.76f), 0.4f), Surface(new Color(0.3f, 0.34f, 0.44f), 0.6f) };
                    var window = M(new Color(1f, 0.86f, 0.55f), 1.4f);
                    var windowCool = M(new Color(0.6f, 0.85f, 1f), 1.2f);
                    for (int i = 0; i < 34; i++)
                    {
                        float back = R(13f, 60f), across = R(-46f, 46f);
                        float h = R(4f, 9f) + back * R(0.15f, 0.35f), w = R(3f, 6.5f), d = R(3f, 6f);
                        var at = At(back, across);
                        Shapes.Rounded("Tower", p, at + Vector3.up * h * 0.5f, new Vector3(d, h, w), 0.1f, walls[rng.Next(walls.Length)]);
                        // Rows of lit windows on the face towards the camera (east).
                        var lit = rng.NextDouble() < 0.6 ? window : windowCool;
                        for (float wy = 1.5f; wy < h - 1f; wy += 1.6f)
                            Shapes.Rounded("Windows", p, at + new Vector3(d * 0.5f + 0.02f, wy, 0f), new Vector3(0.05f, 0.5f, w * 0.8f), 0.01f, lit);
                        if (h > 22f) Shapes.Primitive(PrimitiveType.Cylinder, "Spire", p, at + Vector3.up * (h + 1.5f), new Vector3(0.25f, 1.5f, 0.25f), walls[3]);
                    }
                    break;
                }
                case Theme.Glacier:
                {
                    var ice = Surface(new Color(0.7f, 0.88f, 0.98f), 0.85f);
                    var iceDeep = Surface(new Color(0.45f, 0.7f, 0.9f), 0.85f);
                    var snow = Surface(new Color(0.96f, 0.98f, 1f), 0.3f);
                    var rock = Surface(new Color(0.45f, 0.48f, 0.55f), 0.2f);
                    for (int i = 0; i < 9; i++)
                    {
                        float back = R(28f, 60f), across = R(-50f, 50f), size = R(16f, 30f);
                        Piece(p, "Peak", Cone(5), At(back, across), new Vector3(size, size * R(0.8f, 1.2f), size), rock, R(0f, 360f));
                        Piece(p, "Snowcap", Cone(5), At(back, across, size * 0.55f), new Vector3(size * 0.5f, size * 0.5f, size * 0.5f), snow, R(0f, 360f));
                    }
                    for (int i = 0; i < 26; i++)
                    {
                        float back = R(4f, 24f), across = R(-40f, 40f), h = R(2f, 9f);
                        Shapes.Rounded("IceCliff", p, At(back, across, h * 0.5f), new Vector3(R(2f, 5f), h, R(2f, 6f)), 0.4f, i % 3 == 0 ? iceDeep : ice).transform.rotation = Quaternion.Euler(R(-6f, 6f), R(0f, 90f), R(-6f, 6f));
                    }
                    break;
                }
                case Theme.Volcano:
                {
                    var rock = Surface(new Color(0.22f, 0.17f, 0.16f), 0.15f);
                    var rockMid = Surface(new Color(0.32f, 0.24f, 0.2f), 0.1f);
                    var lava = M(new Color(1f, 0.42f, 0.08f), 2.4f);
                    Piece(p, "Volcano", Cone(9), At(46f, -6f), new Vector3(54f, 30f, 54f), rock, 12f);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Crater", p, At(46f, -6f, 29.4f), new Vector3(5.5f, 0.3f, 5.5f), lava);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = -40f + i * 20f;
                        var flow = Shapes.Rounded("LavaFlow", p, At(46f - 12f, -6f + a * 0.12f, 14f), new Vector3(0.9f, 0.3f, 30f), 0.1f, lava).transform;
                        flow.rotation = Quaternion.Euler(0f, 90f + a, -48f);
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        float back = R(20f, 58f), across = R(-50f, 50f), size = R(8f, 18f);
                        if (Mathf.Abs(across + 6f) < 20f && back > 30f) continue;
                        Piece(p, "Cone", Cone(7), At(back, across), new Vector3(size, size * 0.6f, size), rockMid, R(0f, 360f));
                    }
                    for (int i = 0; i < 18; i++)
                        Shapes.Rounded("LavaCrack", p, At(R(3f, 26f), R(-40f, 40f), 0.02f), new Vector3(R(3f, 9f), 0.08f, 0.3f), 0.03f, lava).transform.rotation = Quaternion.Euler(0f, R(0f, 180f), 0f);
                    break;
                }
                case Theme.Space:
                {
                    var planet = Surface(new Color(0.85f, 0.5f, 0.3f), 0.3f);
                    var planet2 = Surface(new Color(0.4f, 0.6f, 0.95f), 0.4f);
                    var ring = M(new Color(0.9f, 0.82f, 0.62f), 0.2f);
                    var star = M(Color.white, 2.5f);
                    var rock = Surface(new Color(0.45f, 0.42f, 0.42f), 0.1f);
                    var hull = Surface(new Color(0.7f, 0.74f, 0.8f), 0.6f);
                    var glow = M(new Color(0.4f, 0.9f, 1f), 2f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Planet", p, At(70f, 18f, 4f), Vector3.one * 34f, planet);
                    var rp = At(52f, -30f, 18f);
                    Shapes.Primitive(PrimitiveType.Sphere, "RingedPlanet", p, rp, Vector3.one * 12f, planet2);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Ring", p, rp, new Vector3(24f, 0.06f, 24f), ring).transform.rotation = Quaternion.Euler(18f, 0f, 14f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Moon", p, At(40f, 6f, 26f), Vector3.one * 4f, rock);
                    for (int i = 0; i < 160; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Star", p, At(R(30f, 90f), R(-80f, 80f), R(4f, 60f)), Vector3.one * R(0.15f, 0.4f), star);
                    for (int i = 0; i < 22; i++)
                        Shapes.Rounded("Asteroid", p, At(R(8f, 40f), R(-40f, 40f), R(-3f, 12f)), new Vector3(R(0.8f, 3f), R(0.6f, 2f), R(0.8f, 3f)), 0.4f, rock).transform.rotation = Quaternion.Euler(R(0f, 90f), R(0f, 90f), R(0f, 90f));
                    // A space station far off: a hub, two long arms, lit rings.
                    var st = At(34f, 22f, 14f);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Hub", p, st, new Vector3(3f, 1.2f, 3f), hull);
                    Shapes.Rounded("Arm", p, st, new Vector3(1f, 0.6f, 16f), 0.2f, hull);
                    foreach (int s in new[] { -1, 1 }) Shapes.Primitive(PrimitiveType.Cylinder, "Light", p, st + new Vector3(0f, 0f, s * 8f), new Vector3(1.6f, 0.3f, 1.6f), glow);
                    break;
                }
                case Theme.Forest:
                {
                    var trunk = Surface(new Color(0.4f, 0.28f, 0.18f), 0.1f);
                    var leaves = new[] { Surface(new Color(0.22f, 0.5f, 0.2f), 0.15f), Surface(new Color(0.3f, 0.58f, 0.24f), 0.15f), Surface(new Color(0.18f, 0.4f, 0.18f), 0.15f) };
                    var temple = Surface(new Color(0.62f, 0.6f, 0.5f), 0.1f);
                    var moss = Surface(new Color(0.36f, 0.48f, 0.28f), 0.1f);
                    // A stepped temple in the middle, the trees round it.
                    var tp = At(30f, 0f);
                    for (int i = 0; i < 6; i++)
                        Shapes.Rounded("TempleStep", p, tp + Vector3.up * (i * 2.2f + 1.1f), new Vector3(20f - i * 3f, 2.2f, 20f - i * 3f), 0.1f, i % 2 == 0 ? temple : moss);
                    Shapes.Rounded("Shrine", p, tp + Vector3.up * 14.6f, new Vector3(4f, 2.8f, 4f), 0.1f, temple);
                    for (int i = 0; i < 70; i++)
                    {
                        float back = R(4f, 56f), across = R(-50f, 50f);
                        if (Mathf.Abs(across) < 13f && back > 18f && back < 42f) continue;
                        float size = R(2.5f, 5f) + back * 0.06f;
                        var at = At(back, across);
                        Shapes.Primitive(PrimitiveType.Cylinder, "Trunk", p, at + Vector3.up * size * 0.5f, new Vector3(size * 0.15f, size * 0.5f, size * 0.15f), trunk);
                        Shapes.Primitive(PrimitiveType.Sphere, "Crown", p, at + Vector3.up * size * 1.3f, Vector3.one * size * 1.3f, leaves[rng.Next(leaves.Length)]);
                    }
                    break;
                }
                default:
                {
                    var sand = Surface(new Color(0.92f, 0.74f, 0.46f), 0.1f);
                    var sandDark = Surface(new Color(0.82f, 0.62f, 0.38f), 0.1f);
                    var stone = Surface(new Color(0.88f, 0.76f, 0.52f), 0.15f);
                    var trunk = Surface(new Color(0.5f, 0.36f, 0.22f), 0.1f);
                    var palm = Surface(new Color(0.3f, 0.55f, 0.22f), 0.15f);
                    var sun = M(new Color(1f, 0.85f, 0.5f), 2.2f);
                    for (int i = 0; i < 12; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Dune", p, At(R(10f, 60f), R(-55f, 55f), -1f), new Vector3(R(18f, 34f), R(3f, 7f), R(14f, 26f)), i % 2 == 0 ? sand : sandDark);
                    Piece(p, "Pyramid", Cone(4), At(40f, -10f), new Vector3(30f, 20f, 30f), stone, 45f);
                    Piece(p, "Pyramid", Cone(4), At(54f, 16f), new Vector3(22f, 15f, 22f), stone, 45f);
                    Piece(p, "Pyramid", Cone(4), At(30f, 24f), new Vector3(12f, 8f, 12f), stone, 45f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Sun", p, At(80f, -26f, 16f), Vector3.one * 9f, sun);
                    for (int i = 0; i < 14; i++)
                    {
                        var at = At(R(4f, 22f), R(-36f, 36f));
                        float h = R(3f, 5.5f);
                        Shapes.Primitive(PrimitiveType.Cylinder, "PalmTrunk", p, at + Vector3.up * h * 0.5f, new Vector3(0.3f, h * 0.5f, 0.3f), trunk);
                        for (int l = 0; l < 6; l++)
                            Shapes.Rounded("Frond", p, at + Vector3.up * h, new Vector3(2.6f, 0.08f, 0.5f), 0.04f, palm).transform.rotation = Quaternion.Euler(0f, l * 60f + R(0f, 20f), -18f);
                    }
                    break;
                }
            }
        }

        // ---------- The crowd's murmur ----------

        private void MakeMurmur()
        {
            const int rate = 22050;
            int len = rate * 6;
            var data = new float[len];
            var r = new System.Random(5);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < len; i++)
            {
                float white = (float)r.NextDouble() * 2f - 1f;
                lp1 += (white - lp1) * 0.08f;
                lp2 += (lp1 - lp2) * 0.2f;
                float t = i / (float)rate;
                float swell = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 2f / 6f) * Mathf.Sin(t * 1.3f + 1f);
                data[i] = lp2 * 2.2f * swell;
            }
            int fade = rate / 2;
            for (int i = 0; i < fade; i++) data[i] = Mathf.Lerp(data[len - fade + i], data[i], i / (float)fade);
            var clip = AudioClip.Create("CrowdMurmur", len - fade, 1, rate, false);
            var trimmed = new float[len - fade];
            System.Array.Copy(data, trimmed, len - fade);
            clip.SetData(trimmed, 0);
            murmur = gameObject.AddComponent<AudioSource>();
            murmur.clip = clip;
            murmur.loop = true;
            murmur.volume = 0f;
            murmur.spatialBlend = 0f;
            murmur.Play();
        }

        private void OnDestroy()
        {
            foreach (var m in mats) if (m != null) Destroy(m);
            if (murmur != null && murmur.clip != null) Destroy(murmur.clip);
        }
    }
}
