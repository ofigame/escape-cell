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
        private const float PodiumH = 0.5f, TierRise = 0.5f;

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

        /// <param name="floor">The duel's floor, cell by cell (x across the view, y from the south end to the north).</param>
        /// <param name="origin">Where cell (0, 0) stands (its middle).</param>
        /// <param name="playerSouth">The player's foi starts at the south end (the left of the screen): its fans fill that side.</param>
        public static DuelStands Build(Transform parent, bool[,] floor, Vector3 origin, float floorY, int seed, bool playerSouth, Theme theme)
        {
            var go = new GameObject("DuelStands");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DuelStands>();
            d.floorY = floorY;
            var rng = new System.Random(seed);
            var decor = new GameObject("Decor").transform;
            decor.SetParent(go.transform, false);
            d.Terraces(decor, floor, origin, playerSouth, rng);
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

        // ---------- The stands: terraces round the floor, whatever its shape ----------

        /// <summary>
        /// Every cell round the floor (within the floor's own length along the view, out to seven cells past its ends) is
        /// a step of the stand, as high as it is far from the floor: the stands wrap the floor's outline wherever it goes,
        /// right from its edge. The south half is one side's, the north the other's; the outermost ring is a wall in the
        /// side's colour. The crowd sits on the steps (four to a cell), each looking at the nearest bit of floor.
        /// </summary>
        private void Terraces(Transform p, bool[,] floor, Vector3 origin, bool playerSouth, System.Random rng)
        {
            int w = floor.GetLength(0), h = floor.GetLength(1);
            int fx0 = w, fx1 = -1, fy0 = h, fy1 = -1;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor[x, y]) { fx0 = Mathf.Min(fx0, x); fx1 = Mathf.Max(fx1, x); fy0 = Mathf.Min(fy0, y); fy1 = Mathf.Max(fy1, y); }
            if (fx1 < 0) return;
            // The middle and the size (for the backdrop across the floor).
            centre = origin + new Vector3((fx0 + fx1) * 0.5f, 0f, (fy0 + fy1) * 0.5f);
            hw = (fx1 - fx0 + 1) * 0.5f;
            hd = (fy1 - fy0 + 1) * 0.5f;
            // Distances from the floor (a step a cell, in eight directions), and the nearest floor cell, by a flood out.
            int ey0 = fy0 - Tiers, ew = fx1 - fx0 + 1, eh = fy1 - fy0 + 1 + Tiers * 2;
            var dist = new int[ew, eh];
            var near = new Vector2Int[ew, eh];
            var queue = new Queue<Vector2Int>();
            for (int x = 0; x < ew; x++)
                for (int y = 0; y < eh; y++)
                {
                    int gx = x + fx0, gy = y + ey0;
                    bool f = gy >= 0 && gy < h && floor[gx, gy];
                    dist[x, y] = f ? 0 : int.MaxValue;
                    if (f) { near[x, y] = new Vector2Int(gx, gy); queue.Enqueue(new Vector2Int(x, y)); }
                }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = c.x + dx, ny = c.y + dy;
                        if (nx < 0 || ny < 0 || nx >= ew || ny >= eh || dist[nx, ny] <= dist[c.x, c.y] + 1) continue;
                        dist[nx, ny] = dist[c.x, c.y] + 1;
                        near[nx, ny] = near[c.x, c.y];
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
            }
            if (batches.Count == 0) MakeBatches();
            var stone = Surface(new Color(0.8f, 0.78f, 0.74f), 0.15f);
            var riser = Surface(new Color(0.62f, 0.6f, 0.58f), 0.1f);
            var sides = new[] { M(Blue, 0.1f), M(Red, 0.1f) };
            int southTeam = playerSouth ? 0 : 1;
            float midY = (fy0 + fy1) * 0.5f;
            var verts = new List<Vector3>(); var normals = new List<Vector3>();
            var tris = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() }; // stone, riser, blue, red
            for (int x = 0; x < ew; x++)
                for (int y = 0; y < eh; y++)
                {
                    int dd = dist[x, y];
                    if (dd < 1 || dd > Tiers) continue;
                    int gx = x + fx0, gy = y + ey0;
                    int team = gy <= midY ? southTeam : 1 - southTeam;
                    bool wall = dd == Tiers;
                    float top = floorY + PodiumH + (dd - 1) * TierRise + (wall ? 1.4f : 0f);
                    var c = origin + new Vector3(gx, 0f, gy);
                    int sub = wall ? 2 + team : dd % 2 == 0 ? 0 : 1;
                    AddBox(verts, normals, tris[sub], new Vector3(c.x - 0.5f, floorY - 0.4f, c.z - 0.5f), new Vector3(c.x + 0.5f, top, c.z + 0.5f));
                    if (wall || dd == 1) continue; // the front step a low wall, the back a coloured one: no seats there
                    // Four to a cell, looking at the nearest floor.
                    var lookAt = origin + new Vector3(near[x, y].x, 0f, near[x, y].y);
                    float facing = Mathf.Atan2(lookAt.x - c.x, lookAt.z - c.z) * Mathf.Rad2Deg;
                    for (int k = 0; k < 4; k++)
                    {
                        if (rng.NextDouble() < 0.06) continue; // an empty seat here and there
                        var at = new Vector3(c.x + (k % 2 == 0 ? -0.25f : 0.25f), top, c.z + (k < 2 ? -0.25f : 0.25f));
                        float phase = (float)rng.NextDouble() * 10f;
                        var body = batches[team * 4 + rng.Next(4)];
                        body.pos.Add(at + Vector3.up * 0.18f); body.phase.Add(phase); body.side.Add(team); body.facing.Add(facing);
                        var head = batches[8 + rng.Next(4)];
                        head.pos.Add(at + Vector3.up * 0.46f); head.phase.Add(phase); head.side.Add(team); head.facing.Add(facing);
                        if (rng.NextDouble() < 0.1)
                        {
                            var flag = batches[12 + team];
                            flag.pos.Add(at + new Vector3(0.1f, 0.85f, 0f)); flag.phase.Add(phase); flag.side.Add(team); flag.facing.Add(facing);
                        }
                    }
                }
            var mesh = new Mesh { name = "DuelTerraces", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 4;
            for (int s = 0; s < 4; s++) mesh.SetTriangles(tris[s], s);
            mesh.RecalculateBounds();
            ownedMesh = mesh;
            var go = new GameObject("Terraces");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { stone, riser, sides[0], sides[1] };
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        private Mesh ownedMesh;

        /// <summary>A box's top and four sides (no bottom) into the lists, flat-shaded.</summary>
        private static void AddBox(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b)
        {
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
            {
                int s = v.Count;
                v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
                n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
                t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
            }
            Quad(new Vector3(a.x, b.y, a.z), new Vector3(a.x, b.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, b.y, a.z), Vector3.up);
            Quad(new Vector3(a.x, a.y, a.z), new Vector3(a.x, b.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, a.y, a.z), Vector3.back);
            Quad(new Vector3(b.x, a.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, a.y, b.z), Vector3.forward);
            Quad(new Vector3(a.x, a.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, b.y, a.z), new Vector3(a.x, a.y, a.z), Vector3.left);
            Quad(new Vector3(b.x, a.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, a.y, b.z), Vector3.right);
        }

        // ---------- The crowd ----------

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
        internal static Mesh Cone(int sides)
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
            if (ownedMesh != null) Destroy(ownedMesh);
            if (murmur != null && murmur.clip != null) Destroy(murmur.clip);
        }
    }
}
