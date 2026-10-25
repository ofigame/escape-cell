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
    public partial class DuelStands : MonoBehaviour
    {
        public enum Theme { City, Glacier, Volcano, Space, Forest, Desert, Grove, Frost, Mars, Cappadocia, Asia, Europe, Arabia, Istanbul }

        /// <summary>A stage built of made models (the platform, its stands, the land round it), not of the board's own tiles.</summary>
        public static bool IsMade(Theme t) => t >= Theme.Grove;

        /// <summary>What the shoulder camera must not go into on a made stage: its stands (their own shapes).</summary>
        private static readonly List<Collider> Blockers = new List<Collider>();

        /// <summary>How far from <paramref name="from"/> towards <paramref name="to"/> the view is clear of the stands.</summary>
        public static float ClearTo(Vector3 from, Vector3 to)
        {
            var d = to - from;
            float best = d.magnitude;
            var ray = new Ray(from, d.normalized);
            foreach (var c in Blockers)
                if (c != null && c.Raycast(ray, out var hit, best)) best = hit.distance;
            return best;
        }

        private const int Tiers = 7;
        private const float PodiumH = 0.5f, TierRise = 0.5f;

        private Vector3 centre;
        private float hw, hd, floorY;
        private readonly List<Material> mats = new List<Material>();
        private readonly List<Texture2D> marbles = new List<Texture2D>();

        private class Batch { public Mesh mesh; public Material mat; public readonly List<Vector3> pos = new List<Vector3>(); public readonly List<float> phase = new List<float>(); public readonly List<int> side = new List<int>(); public readonly List<float> facing = new List<float>(); public Matrix4x4[] m; public int kind; }
        private readonly List<Batch> batches = new List<Batch>();
        /// <summary>How big the fans are (the made stages' stands: bigger, fewer of them).</summary>
        private float crowdSize = 1f;

        /// <summary>Each side's seats taken and flags waving (0: the player's, filled by its rescued foi; 1: the rival's).</summary>
        public static float[] Fill = { 0.88f, 0.88f }, FlagShare = { 0.1f, 0.1f };
        private const float MadeCrowd = 2.2f;
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
            if (IsMade(theme)) d.MadeStage(decor, floor, origin, playerSouth, rng, LookOf(theme));
            else
            {
                d.Terraces(decor, floor, origin, playerSouth, rng);
                d.Backdrop(decor, theme, rng);
            }
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

        /// <summary>A rough, unshining surface (moss, earth, grass) with a little of its own light: no grey sheen in the dark.</summary>
        private Material Matte(Color c, float glow) { var m = MaterialFactory.CreateSurface(c, c * glow, 0.04f, 0f); mats.Add(m); return m; }

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
                        if (rng.NextDouble() > Fill[team]) continue; // the seats its rescued foi fill
                        var at = new Vector3(c.x + (k % 2 == 0 ? -0.25f : 0.25f), top, c.z + (k < 2 ? -0.25f : 0.25f));
                        float phase = (float)rng.NextDouble() * 10f;
                        var body = batches[team * 4 + rng.Next(4)];
                        body.pos.Add(at + Vector3.up * 0.18f); body.phase.Add(phase); body.side.Add(team); body.facing.Add(facing);
                        var head = batches[8 + rng.Next(4)];
                        head.pos.Add(at + Vector3.up * 0.46f); head.phase.Add(phase); head.side.Add(team); head.facing.Add(facing);
                        if (rng.NextDouble() < FlagShare[team])
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
            var headScale = Vector3.one * 0.2f * crowdSize;
            var bodyScale = new Vector3(0.3f, 0.36f, 0.22f) * crowdSize;
            var flagScale = new Vector3(0.34f, 0.22f, 0.02f) * crowdSize;
            DrawMadeFans(time);
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

        // ---------- The made stages (the blue grove, the glacier peak): a rune platform, two stands, the land round about ----------

        /// <summary>What a made stage is made of and how it looks.</summary>
        private class Look
        {
            /// <summary>Its models' folder and names' start (platform, stand, tree_1.., rock).</summary>
            public string folder;
            /// <summary>How far out the plain marble middle reaches, of the floor's half width (over the emblem, short of the rim).</summary>
            public float cover = 1.22f;
            public int trees;
            public float treeMin, treeMax;
            public bool rocks, grass, lanterns, snow, glints, dust;
            /// <summary>A few of its own made thing beside the platform (Mars: a rover), how many.</summary>
            public int props;
            /// <summary>The platform's height to its width (as made: 1), and how far in its rim is from its outline.</summary>
            public float thick, rim;
            public Color glow, selfLight, ground, mound, bank, grassColour, crystal, mist, lamp;
            public float lampPower;
            /// <summary>How high above the floor the stands stand (on their banks).</summary>
            public float standRise = 1.2f;
            /// <summary>Glowing crystal clusters on the ground (not in Cappadocia), and the colour of the dust in the air.</summary>
            public bool crystals = true;
            public Color dustColour = new Color(0.95f, 0.6f, 0.4f, 0.55f);
            /// <summary>What falls from the sky (snow, cherry petals): its colour and how fast.</summary>
            public Color snowColour = new Color(1f, 1f, 1f, 0.9f);
            public float snowFall = 1.5f;
            /// <summary>A tree right behind the platform, between the stands (none where the sky picture has its castle there).</summary>
            public bool backTree = true;
            /// <summary>Trees behind each stand too (the knights' court).</summary>
            public bool standTrees;
            /// <summary>The grass in front of the platform (none: alpha 0) and the pebbles' colour there.</summary>
            public Color frontGrass, pebbles = new Color(0.4f, 0.4f, 0.42f);
            /// <summary>A city street instead of stands and land (Galata): the fans along its pavements, houses, its landmark.</summary>
            public bool street;
            /// <summary>The ground's tiled picture (Resources), or none: the land's own colour.</summary>
            public string paving;
            /// <summary>Its sky picture behind it all (Resources; its landscape as designed), or none.</summary>
            public string sky;
            /// <summary>The colour its sky picture fades into at its foot: a haze of it where the land ends, so the land and
            /// the picture meet softly (none: no haze).</summary>
            public Color haze;
            /// <summary>Its sky's colours if it had no picture: high up and at the horizon (none: the grove's night).</summary>
            public Color skyTop, skyLow;
            /// <summary>Its light: the sun's colour and strength, the ambient sky, horizon and ground.</summary>
            public Color sun = Color.white, ambSky = Color.grey, ambHorizon = Color.grey, ambGround = Color.black;
            public float sunPower = 1f;
        }

        private static readonly Look GroveLook = new Look
        {
            frontGrass = new Color(0.05f, 0.11f, 0.08f), pebbles = new Color(0.22f, 0.26f, 0.3f),
            folder = "Duel/Grove/grove_", cover = 1.32f, trees = 3, treeMin = 12f, treeMax = 16f, grass = true, lanterns = true, thick = 0.6f, rim = 1.03f,
            glow = new Color(0.35f, 0.85f, 1f), selfLight = new Color(0.16f, 0.2f, 0.26f),
            ground = new Color(0.07f, 0.12f, 0.1f), mound = new Color(0.1f, 0.17f, 0.13f), bank = new Color(0.05f, 0.09f, 0.08f), grassColour = new Color(0.05f, 0.11f, 0.08f),
            crystal = new Color(0.45f, 0.85f, 1f), mist = new Color(0.55f, 0.8f, 0.95f, 0.17f), lamp = new Color(0.35f, 0.85f, 1f), lampPower = 1f,
            sun = new Color(0.6f, 0.75f, 1f), sunPower = 0.95f, ambSky = new Color(0.2f, 0.32f, 0.5f), ambHorizon = new Color(0.12f, 0.26f, 0.36f), ambGround = new Color(0.03f, 0.05f, 0.07f),
        };

        private static readonly Look FrostLook = new Look
        {
            pebbles = new Color(0.78f, 0.88f, 0.98f),
            folder = "Duel/Ice/ice_", trees = 1, treeMin = 7f, treeMax = 10.5f, rocks = true, snow = true, glints = true, thick = 1f, rim = 1.03f,
            glow = new Color(0.55f, 0.9f, 1f), selfLight = new Color(0.42f, 0.48f, 0.56f),
            ground = new Color(0.8f, 0.87f, 0.95f), mound = new Color(0.9f, 0.94f, 1f), bank = new Color(0.72f, 0.8f, 0.9f), grassColour = Color.white,
            crystal = new Color(0.55f, 0.88f, 1f), mist = new Color(0.92f, 0.96f, 1f, 0.16f), lamp = new Color(0.7f, 0.9f, 1f), lampPower = 0.45f,
            sky = "Duel/Ice/ice_sky", haze = new Color(0.561f, 0.729f, 0.855f), skyTop = new Color(0.5f, 0.68f, 0.88f), skyLow = new Color(0.86f, 0.92f, 0.98f), sun = new Color(0.95f, 0.97f, 1f), sunPower = 1.15f, ambSky = new Color(0.62f, 0.72f, 0.86f), ambHorizon = new Color(0.55f, 0.66f, 0.78f), ambGround = new Color(0.35f, 0.4f, 0.48f),
        };

        private static readonly Look MarsLook = new Look
        {
            pebbles = new Color(0.42f, 0.18f, 0.12f),
            folder = "Duel/Mars/mars_", trees = 1, treeMin = 9f, treeMax = 15f, rocks = true, dust = true, props = 2, thick = 0.8f, rim = 1.03f, standRise = 0.15f,
            glow = new Color(1f, 0.6f, 0.25f), selfLight = new Color(0.34f, 0.24f, 0.2f),
            ground = new Color(0.58f, 0.27f, 0.16f), mound = new Color(0.66f, 0.32f, 0.19f), bank = new Color(0.46f, 0.2f, 0.12f), grassColour = Color.white,
            crystal = new Color(1f, 0.6f, 0.22f), mist = new Color(0.9f, 0.55f, 0.35f, 0.15f), lamp = new Color(1f, 0.62f, 0.3f), lampPower = 0.6f,
            sky = "Duel/Mars/mars_sky", haze = new Color(0.671f, 0.314f, 0.247f), skyTop = new Color(0.42f, 0.18f, 0.14f), skyLow = new Color(0.96f, 0.6f, 0.4f), sun = new Color(1f, 0.84f, 0.68f), sunPower = 1.1f, ambSky = new Color(0.72f, 0.48f, 0.36f), ambHorizon = new Color(0.62f, 0.4f, 0.3f), ambGround = new Color(0.32f, 0.17f, 0.1f),
        };

        private static readonly Look CappadociaLook = new Look
        {
            frontGrass = new Color(0.62f, 0.5f, 0.28f), pebbles = new Color(0.74f, 0.6f, 0.46f),
            folder = "Duel/Kapa/kapa_", trees = 2, treeMin = 9f, treeMax = 14f, rocks = true, dust = true, crystals = false, thick = 1f, rim = 1.03f, standRise = 0.8f,
            glow = new Color(1f, 0.82f, 0.45f), selfLight = new Color(0.36f, 0.31f, 0.27f),
            ground = new Color(0.8f, 0.62f, 0.44f), mound = new Color(0.86f, 0.68f, 0.48f), bank = new Color(0.74f, 0.56f, 0.4f), grassColour = Color.white,
            crystal = Color.white, mist = new Color(1f, 0.8f, 0.6f, 0.12f), lamp = new Color(1f, 0.72f, 0.45f), lampPower = 0.5f,
            dustColour = new Color(1f, 0.85f, 0.6f, 0.45f),
            sky = "Duel/Kapa/kapa_sky", haze = new Color(0.553f, 0.341f, 0.22f), skyTop = new Color(0.38f, 0.3f, 0.56f), skyLow = new Color(1f, 0.74f, 0.5f), sun = new Color(1f, 0.86f, 0.66f), sunPower = 1.12f, ambSky = new Color(0.76f, 0.58f, 0.52f), ambHorizon = new Color(0.7f, 0.52f, 0.4f), ambGround = new Color(0.36f, 0.26f, 0.18f),
        };

        private static readonly Look AsiaLook = new Look
        {
            frontGrass = new Color(0.3f, 0.48f, 0.24f), pebbles = new Color(0.45f, 0.47f, 0.48f),
            folder = "Duel/Asia/asia_", trees = 2, treeMin = 9f, treeMax = 13f, rocks = true, grass = true, snow = true, crystals = false, props = 2,
            thick = 1f, rim = 0.97f, standRise = 0.8f,
            glow = new Color(1f, 0.8f, 0.4f), selfLight = new Color(0.34f, 0.3f, 0.3f),
            ground = new Color(0.36f, 0.5f, 0.3f), mound = new Color(0.42f, 0.56f, 0.34f), bank = new Color(0.32f, 0.44f, 0.28f), grassColour = new Color(0.3f, 0.48f, 0.24f),
            crystal = Color.white, mist = new Color(1f, 0.9f, 0.95f, 0.16f), lamp = new Color(1f, 0.75f, 0.5f), lampPower = 0.5f,
            snowColour = new Color(1f, 0.7f, 0.82f, 0.95f), snowFall = 0.7f,
            sky = "Duel/Asia/asia_sky", haze = new Color(0.546f, 0.553f, 0.557f), skyTop = new Color(0.62f, 0.5f, 0.7f), skyLow = new Color(1f, 0.84f, 0.8f), sun = new Color(1f, 0.9f, 0.82f), sunPower = 1.12f, ambSky = new Color(0.78f, 0.68f, 0.72f), ambHorizon = new Color(0.68f, 0.62f, 0.6f), ambGround = new Color(0.3f, 0.32f, 0.26f),
        };

        private static readonly Look EuropeLook = new Look
        {
            frontGrass = new Color(0.3f, 0.5f, 0.21f), pebbles = new Color(0.5f, 0.5f, 0.48f), standTrees = true,
            folder = "Duel/Euro/euro_", trees = 1, treeMin = 10f, treeMax = 14f, rocks = true, grass = true, snow = true, crystals = false, props = 2, backTree = false,
            thick = 1f, rim = 1.03f, standRise = 0.8f,
            glow = new Color(1f, 0.82f, 0.4f), selfLight = new Color(0.34f, 0.32f, 0.3f),
            ground = new Color(0.36f, 0.54f, 0.27f), mound = new Color(0.42f, 0.6f, 0.3f), bank = new Color(0.33f, 0.48f, 0.25f), grassColour = new Color(0.3f, 0.5f, 0.21f),
            crystal = Color.white, mist = new Color(1f, 1f, 0.95f, 0.1f), lamp = new Color(1f, 0.85f, 0.6f), lampPower = 0.35f,
            snowColour = new Color(0.62f, 0.78f, 0.3f, 0.9f), snowFall = 0.6f, // (leaves drifting down from the oaks)
            sky = "Duel/Euro/euro_sky", haze = new Color(0.443f, 0.435f, 0.318f), skyTop = new Color(0.34f, 0.6f, 0.92f), skyLow = new Color(0.8f, 0.9f, 0.98f), sun = new Color(1f, 0.96f, 0.88f), sunPower = 1.2f, ambSky = new Color(0.7f, 0.78f, 0.9f), ambHorizon = new Color(0.62f, 0.68f, 0.62f), ambGround = new Color(0.32f, 0.36f, 0.26f),
        };

        private static readonly Look ArabiaLook = new Look
        {
            frontGrass = new Color(0.6f, 0.52f, 0.3f), pebbles = new Color(0.78f, 0.6f, 0.42f),
            folder = "Duel/Arab/arab_", trees = 1, treeMin = 8f, treeMax = 12f, rocks = true, dust = true, crystals = false, props = 2,
            thick = 1f, rim = 1.03f, standRise = 0.8f,
            glow = new Color(0.4f, 0.9f, 1f), selfLight = new Color(0.36f, 0.3f, 0.28f),
            ground = new Color(0.84f, 0.64f, 0.44f), mound = new Color(0.9f, 0.7f, 0.48f), bank = new Color(0.78f, 0.58f, 0.4f), grassColour = Color.white,
            crystal = Color.white, mist = new Color(1f, 0.8f, 0.7f, 0.12f), lamp = new Color(1f, 0.72f, 0.42f), lampPower = 0.55f,
            dustColour = new Color(1f, 0.82f, 0.58f, 0.45f),
            sky = "Duel/Arab/arab_sky", haze = new Color(0.694f, 0.431f, 0.333f), skyTop = new Color(0.36f, 0.24f, 0.5f), skyLow = new Color(1f, 0.7f, 0.48f), sun = new Color(1f, 0.8f, 0.64f), sunPower = 1.1f, ambSky = new Color(0.74f, 0.56f, 0.62f), ambHorizon = new Color(0.76f, 0.56f, 0.46f), ambGround = new Color(0.38f, 0.27f, 0.2f),
        };

        private static Look LookOf(Theme t) => t switch
        {
            Theme.Frost => FrostLook, Theme.Mars => MarsLook, Theme.Cappadocia => CappadociaLook, Theme.Asia => AsiaLook, Theme.Europe => EuropeLook,
            Theme.Arabia => ArabiaLook, Theme.Istanbul => GalataLook, _ => GroveLook,
        };

        /// <summary>A made stage's light: the sun's colour and strength, the ambient sky, horizon and ground.</summary>
        public static (Color sun, float power, Color sky, Color horizon, Color ground) LightOf(Theme t)
        {
            var l = LookOf(t);
            return (l.sun, l.sunPower, l.ambSky, l.ambHorizon, l.ambGround);
        }

        /// <summary>
        /// A made stage: the rune platform under the floor (the floor's own tiles are hidden, see GridView.Ghost), a stand at
        /// each back corner against its rim with each side's fans on its steps, trees (and rocks) behind and round about, lamps.
        /// </summary>
        private void MadeStage(Transform p, bool[,] floor, Vector3 origin, bool playerSouth, System.Random rng, Look look)
        {
            Blockers.Clear();
            int w = floor.GetLength(0), h = floor.GetLength(1);
            int fx0 = w, fx1 = -1, fy0 = h, fy1 = -1;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor[x, y]) { fx0 = Mathf.Min(fx0, x); fx1 = Mathf.Max(fx1, x); fy0 = Mathf.Min(fy0, y); fy1 = Mathf.Max(fy1, y); }
            if (fx1 < 0) return;
            centre = origin + new Vector3((fx0 + fx1) * 0.5f, 0f, (fy0 + fy1) * 0.5f);
            hw = (fx1 - fx0 + 1) * 0.5f; // (along the view)
            hd = (fy1 - fy0 + 1) * 0.5f; // (across it: the stands' ends)
            if (batches.Count == 0) MakeBatches();
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // The platform: round (as made), its stone ring a little wider than the floor, its top where the floor is (a
            // street has none: the fight is on its cobbles; the same room is kept round the floor).
            Bounds pb;
            if (look.street) pb = new Bounds(new Vector3(centre.x, floorY, centre.z), new Vector3(Mathf.Max(hw, hd) * 2.56f, 0f, Mathf.Max(hw, hd) * 2.56f));
            else
            {
                var plat = MadeModel(p, look, "platform", true, true);
                pb = BoundsOf(plat);
                float kp = Mathf.Max(hw, hd) * 2f * 1.28f / Mathf.Max(pb.size.x, pb.size.z);
                plat.localScale = new Vector3(kp, kp * look.thick, kp);
                pb = BoundsOf(plat);
                plat.position += new Vector3(centre.x - pb.center.x, 0f, centre.z - pb.center.z);
                // its walking surface: the highest of several points across it (an inlaid emblem in the middle may sit lower)
                float top = float.MinValue;
                for (int i = 0; i <= 8; i++)
                {
                    float a = i * Mathf.PI / 4f, rr = i == 8 ? 0f : Mathf.Min(hw, hd) * 0.55f;
                    top = Mathf.Max(top, SurfaceAt(plat, new Vector3(centre.x + Mathf.Cos(a) * rr, pb.max.y + 1f, centre.z + Mathf.Sin(a) * rr), float.MinValue));
                }
                if (top == float.MinValue) top = pb.max.y;
                plat.position += Vector3.up * (floorY - top);
                if (look.cover > 0f) CoverEmblem(p, plat, look, centre, floorY, Mathf.Max(hw, hd) * look.cover); // (the middle plain: marble of the platform's own stone)
            }
            // The forest floor a little under the platform's top: it stands on the land, sunk in it (not up in the air).
            float ground = look.street ? floorY - 0.02f : floorY - 1.1f;
            var moss = Matte(look.ground, 0.2f); // (a little of its own light: the land reads in the dark)
            if (look.paving != null) PavedGround(p, look, ground);
            else Shapes.Rounded("Ground", p, new Vector3(centre.x - 10f, ground - 0.3f, centre.z), new Vector3(110f, 0.6f, 110f), 0.05f, moss);
            // mossy mounds and patches here and there (the ground is not a flat board)
            var mossLight = Matte(look.mound, 0.2f);
            var mossDark = Matte(look.bank, 0.2f);
            for (int i = 0; i < (look.street ? 0 : 34); i++)
            {
                float ang = R(0f, Mathf.PI * 2f), dist = R(Mathf.Max(hw, hd) * 1.15f, 30f);
                if (Mathf.Cos(ang) > 0.25f) ang += Mathf.PI * 0.5f * Mathf.Sign(Mathf.Sin(ang) + 1e-4f); // (none in front of the camera: flat slabs there)
                var at = new Vector3(centre.x + Mathf.Cos(ang) * dist, ground, centre.z + Mathf.Sin(ang) * dist);
                float sx = R(2.5f, 7f), sz = R(2.5f, 7f);
                Shapes.Rounded("Mound", p, at, new Vector3(sx, R(0.3f, 1.1f), sz), Mathf.Min(sx, sz) * 0.45f, i % 2 == 0 ? mossLight : mossDark).transform.rotation = Quaternion.Euler(0f, R(0f, 180f), 0f);
            }

            // Long dark grass all round the platform and out under the trees, in tufts.
            if (look.grass)
            {
                var grass = new GameObject("Grass");
                grass.transform.SetParent(p, false);
                grassMesh = GroveGrass(new Vector3(centre.x, ground, centre.z), Mathf.Max(hw, hd) * 1.22f, 32f, 420, rng);
                grass.AddComponent<MeshFilter>().sharedMesh = grassMesh;
                grass.AddComponent<MeshRenderer>().sharedMaterial = Matte(look.grassColour, 0.35f);
            }

            // A stand at each end of the floor (the screen's left and right), its steps towards the floor, up on a bank:
            // the floor lies down in the hollow between them.
            float bank = floorY + look.standRise;
            float platR = Mathf.Max(pb.size.x, pb.size.z) * 0.5f; // (the stands stand clear of the platform's rim)
            platRadius = platR * look.rim;
            int southTeam = playerSouth ? 0 : 1;
            if (look.street) GalataStreet(p, look, ground, platR, southTeam, rng);
            for (int s = 0; s < (look.street ? 0 : 2); s++)
            {
                float dir = s == 0 ? -1f : 1f; // south (-z), north (+z)
                int team = s == 0 ? southTeam : 1 - southTeam;
                var stand = MadeModel(p, look, "stand", true);
                var sb = BoundsOf(stand);
                float k = hw * 2f * 0.95f / Mathf.Max(sb.size.x, sb.size.z);
                stand.localScale = Vector3.one * k;
                // its long side along the view (x), its seats facing the floor (which way the made model faces is found by looking)
                sb = BoundsOf(stand);
                if (sb.size.z > sb.size.x) stand.rotation = Quaternion.Euler(0f, 90f, 0f) * stand.rotation;
                for (int tryTurn = 0; tryTurn < 2; tryTurn++)
                {
                    sb = BoundsOf(stand);
                    stand.position += new Vector3(centre.x - sb.center.x, bank - sb.min.y, centre.z + dir * (platR - 0.3f + sb.extents.z) - sb.center.z);
                    if (tryTurn == 0 && !SeatsFaceFloor(stand, dir)) { stand.rotation = Quaternion.Euler(0f, 180f, 0f) * stand.rotation; continue; }
                    break;
                }
                // the bank of earth it stands on (the hollow's side), mossy, from the forest floor up: under it and behind it, never over the platform
                sb = BoundsOf(stand);
                var bankGo = Shapes.Rounded("Bank", p, new Vector3(sb.center.x, (ground + bank) * 0.5f - 0.25f, sb.center.z + dir * 1.2f), new Vector3(sb.size.x + 1f, bank - ground + 0.5f, sb.size.z + 1f), 0.8f, mossDark);
                // turned back round the floor (with its bank) to its back corner: against the rim, diagonally behind it
                var pivot = new Vector3(centre.x, ground, centre.z);
                stand.RotateAround(pivot, Vector3.up, -dir * 45f);
                bankGo.transform.RotateAround(pivot, Vector3.up, -dir * 45f);
                // in against the rim: the middle of its front row (its curve leaves the middle further out than its ends) just
                // outside the platform's edge
                var outward = BoundsOf(stand).center - pivot; outward.y = 0f; outward.Normalize();
                float shift = platR * look.rim + 0.6f - FrontDistance(stand, pivot, outward); // (just off the rim, a little room)
                stand.position += outward * shift;
                bankGo.transform.position += outward * shift;
                LevelEnds(stand, pivot, outward);
                BendToRing(stand, pivot, outward, platR * look.rim);
                SeatCrowd(stand, team, rng);
                Blockers.AddRange(Probe(stand)); // (its colliders stay: the camera keeps out of it)
            }

            // Giant trees: between the stands behind the platform, one at each side of it (a little to the front), and the
            // forest beyond them all round (never in front of the camera).
            var spots = new List<Vector3>();
            Vector3 Around(float degrees, float dist) => new Vector3(centre.x + Mathf.Cos(degrees * Mathf.Deg2Rad) * dist, 0f, centre.z + Mathf.Sin(degrees * Mathf.Deg2Rad) * dist);
            if (look.backTree) spots.Add(Around(180f, platR + 8.5f)); // (not where it would hide the sky picture's castle)
            if (look.backTree) { spots.Add(Around(160f, platR + 14f)); spots.Add(Around(200f, platR + 14f)); }
            if (look.backTree) spots.Add(Around(180f, platR + 20f));
            spots.Add(Around(-64f, platR + 6.5f)); // the left side (low on the screen: clear of the stands)
            spots.Add(Around(64f, platR + 6.5f)); // the right side
            for (int i = 0; i < 8; i++)
            {
                float deg = R(118f, 242f); // (behind: never between the camera and the stands)
                if (!look.backTree && deg > 140f && deg < 220f) deg += deg < 180f ? -70f : 70f; // (the view behind the platform kept clear)
                spots.Add(Around(deg, platR + R(17f, 26f)));
            }
            if (look.standTrees)
            {
                for (int s = -1; s <= 1; s += 2)
                    for (int k = 0; k < 3; k++)
                        spots.Add(Around(s * (128f + k * 11f + R(-3f, 3f)), platR + R(11f, 15f)));
                // and behind the middle, between the two stands (big ones: over the stands' roofs)
                foreach (var deg in new[] { 165f, 180f, 195f }) spots.Add(Around(deg + R(-3f, 3f), platR + R(10f, 13f)));
            }
            if (look.street) spots.Clear(); // (a street: its houses stand where the trees would)
            foreach (var spot in spots)
            {
                var tree = MadeModel(p, look, "tree_" + (1 + rng.Next(look.trees)), false);
                var tb = BoundsOf(tree);
                tree.localScale = Vector3.one * (R(look.treeMin, look.treeMax) / tb.size.y);
                tree.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f) * tree.rotation;
                tb = BoundsOf(tree);
                tree.position += new Vector3(spot.x - tb.center.x, ground - tb.min.y - 0.2f, spot.z - tb.center.z);
            }

            // Where the land ends: a haze of the sky picture's own horizon colour, thick at the land and thinning upwards.
            if (look.haze.a > 0f) Haze(look.haze, ground);

            // Icy rocks here and there between the trees (the glacier).
            if (look.rocks)
                for (int i = 0; i < 7; i++)
                {
                    var boulder = MadeModel(p, look, "rock", true); // (its crystals glow, if it has a glow picture)
                    var rb = BoundsOf(boulder);
                    boulder.localScale = Vector3.one * (R(2.2f, 4.2f) / Mathf.Max(rb.size.x, rb.size.z));
                    boulder.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f) * boulder.rotation;
                    rb = BoundsOf(boulder);
                    var at = Around(i < 2 ? (i == 0 ? -100f : 100f) : R(110f, 250f), platR + (i < 2 ? R(2.5f, 4f) : R(5f, 14f)));
                    boulder.position += new Vector3(at.x - rb.center.x, ground - rb.min.y - 0.15f, at.z - rb.center.z);
                }

            // The land in front of the platform (the bottom of the screen): tufts of the stage's grass, low rocks of its
            // own and pebbles, so it reads as land and never a bare floor.
            Foreground(p, look, ground, platR, rng);

            // Its own things beside the platform (Mars: rovers), facing every which way.
            for (int i = 0; i < look.props; i++)
            {
                var thing = MadeModel(p, look, "prop", false);
                var tb = BoundsOf(thing);
                thing.localScale = Vector3.one * (R(3.2f, 3.8f) / Mathf.Max(tb.size.x, tb.size.z));
                thing.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f) * thing.rotation;
                tb = BoundsOf(thing);
                var at = Around(i == 0 ? -95f : 95f, platR + R(4.5f, 6f));
                thing.position += new Vector3(at.x - tb.center.x, ground - tb.min.y, at.z - tb.center.z);
            }

            // Lamps: either side of the floor and among the trees behind.
            void Lamp(Vector3 at, float intensity, float range)
            {
                var l = new GameObject("GroveLamp").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.transform.position = at;
                l.type = LightType.Point; l.color = look.lamp; l.intensity = intensity * look.lampPower; l.range = range; l.shadows = LightShadows.None;
            }
            Lamp(new Vector3(centre.x, floorY + 2.5f, centre.z - hd - 1f), 2.2f, 11f);
            Lamp(new Vector3(centre.x, floorY + 2.5f, centre.z + hd + 1f), 2.2f, 11f);
            Lamp(new Vector3(centre.x - hw - 3f, floorY + 4f, centre.z), 3f, 16f);
            Lamp(new Vector3(centre.x + hw + 4f, floorY + 2f, centre.z), 2.4f, 15f); // (the near side: the land in front of the floor)

            // Crystal clusters on the forest floor round the platform and among the trees, and mossy rocks.
            var crystal = M(look.crystal, 1.5f);
            var rock = Surface(new Color(0.3f, 0.36f, 0.42f), 0.2f);
            var cone = Cone(6);
            for (int i = 0; i < (look.crystals ? 16 : 0); i++)
            {
                float ang = R(0f, Mathf.PI * 2f), dist = R(Mathf.Max(hw, hd) * 1.25f, 22f);
                if (i < 4) { ang = (i % 2 == 0 ? -1f : 1f) * R(1.35f, 1.65f); dist = R(11f, 14f); } // the near sides (not in front of the floor)
                else if (Mathf.Cos(ang) > 0.3f) ang += Mathf.PI * 0.6f; // (none in front of the camera)
                var at = new Vector3(centre.x + Mathf.Cos(ang) * dist, ground, centre.z + Mathf.Sin(ang) * dist);
                int n = 3 + rng.Next(3);
                for (int k = 0; k < n; k++)
                {
                    float hgt = R(0.9f, 2.6f) * (k == 0 ? 1.3f : 1f);
                    var piece = new GameObject("Crystal");
                    piece.transform.SetParent(p, false);
                    piece.transform.position = at + new Vector3(R(-0.6f, 0.6f), -0.1f, R(-0.6f, 0.6f));
                    piece.transform.rotation = Quaternion.Euler(R(-28f, 28f), R(0f, 360f), R(-28f, 28f));
                    piece.transform.localScale = new Vector3(hgt * 0.32f, hgt, hgt * 0.32f);
                    piece.AddComponent<MeshFilter>().sharedMesh = cone;
                    piece.AddComponent<MeshRenderer>().sharedMaterial = crystal;
                }
                Shapes.Rounded("Rock", p, at + new Vector3(R(-1.4f, 1.4f), 0.15f, R(-1.4f, 1.4f)), new Vector3(R(0.8f, 1.6f), R(0.5f, 0.9f), R(0.8f, 1.4f)), 0.25f, rock);
            }

            // Lanterns hanging from the trees round the back and sides: a dark frame round a glowing blue core, on a chain.
            var iron = Surface(new Color(0.1f, 0.12f, 0.15f), 0.4f);
            var flame = M(new Color(0.5f, 0.9f, 1f), 2.4f);
            for (int i = 0; i < (look.lanterns ? 9 : 0); i++)
            {
                float ang = Mathf.Lerp(Mathf.PI * 0.55f, Mathf.PI * 1.45f, i / 8f) + R(-0.12f, 0.12f); // (the far half of the ring)
                float dist = Mathf.Max(hw, hd) * R(1.35f, 1.9f);
                var at = new Vector3(centre.x + Mathf.Cos(ang) * dist, floorY + R(4f, 6.5f), centre.z + Mathf.Sin(ang) * dist);
                Shapes.Rounded("Chain", p, at + Vector3.up * 3.4f, new Vector3(0.05f, 6f, 0.05f), 0.02f, iron);
                Shapes.Rounded("LanternCap", p, at + Vector3.up * 0.48f, new Vector3(0.62f, 0.14f, 0.62f), 0.05f, iron);
                Shapes.Rounded("LanternFrame", p, at, new Vector3(0.5f, 0.78f, 0.5f), 0.06f, iron);
                Shapes.Rounded("LanternLight", p, at, new Vector3(0.56f, 0.6f, 0.56f), 0.1f, flame);
                Shapes.Rounded("LanternBase", p, at + Vector3.down * 0.44f, new Vector3(0.6f, 0.1f, 0.6f), 0.04f, iron);
            }

            // Fireflies drifting over the floor and among the trees (or snow falling), and a low mist on the ground.
            if (look.dust)
                GroveParticles("Dust", Weather.AlphaMaterial, new Vector3(centre.x - 3f, floorY + 2.5f, centre.z), new Vector3(40f, 5f, 40f),
                    300, 40f, 6f, look.dustColour, 0.06f, 0.13f, 1.4f, 0.15f);
            else if (look.snow)
                GroveParticles("Snow", Weather.AlphaMaterial, new Vector3(centre.x - 3f, floorY + 11f, centre.z), new Vector3(40f, 1f, 40f),
                    600, 70f, 8f, look.snowColour, 0.08f, 0.17f, look.snowFall > 1f ? 0.4f : 1.1f, look.snowFall); // (petals drift more)
            else if (!look.street)
                GroveParticles("Fireflies", Weather.AddMaterial, new Vector3(centre.x - 3f, floorY + 2.2f, centre.z), new Vector3(34f, 4.5f, 34f),
                    90, 26f, 7f, new Color(1.7f, 1.9f, 0.8f), 0.14f, 0.24f, 0.9f);
            GroveParticles("Mist", Weather.AlphaMaterial, new Vector3(centre.x - 6f, ground + 0.7f, centre.z), new Vector3(48f, 0.6f, 48f),
                70, 4f, 16f, look.mist, 7f, 11f, 0.25f);
            // The ice glinting: small sparkles coming and going over the platform's top and over the stands.
            if (look.glints)
            {
                GroveParticles("Glints", Weather.AddMaterial, new Vector3(centre.x, floorY + 0.25f, centre.z), new Vector3(platR * 1.7f, 0.3f, platR * 1.7f),
                    120, 40f, 1.4f, new Color(2f, 2.5f, 3f), 0.14f, 0.28f, 0.05f);
                for (int s = 0; s < 2; s++)
                {
                    var at = Around(s == 0 ? -135f : 135f, platR + 4f);
                    GroveParticles("StandGlints", Weather.AddMaterial, new Vector3(at.x, floorY + 3.5f, at.z), new Vector3(11f, 4f, 11f),
                        90, 30f, 1.4f, new Color(2f, 2.5f, 3f), 0.16f, 0.32f, 0.05f);
                }
            }
        }

        private Mesh grassMesh, frontMesh;

        /// <summary>
        /// In front of the platform (towards the camera): tufts of the stage's grass, low rocks of its own (or round
        /// stones), pebbles. Kept low, so the platform and the two foi are never covered.
        /// </summary>
        private void Foreground(Transform p, Look look, float ground, float platR, System.Random rng)
        {
            if (look.street) return; // (the street's paving in front: nothing strewn on it)
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 Front(float degrees, float dist) => new Vector3(centre.x + Mathf.Cos(degrees * Mathf.Deg2Rad) * dist, ground, centre.z + Mathf.Sin(degrees * Mathf.Deg2Rad) * dist);
            if (look.frontGrass.a > 0f)
            {
                var go = new GameObject("FrontGrass");
                go.transform.SetParent(p, false);
                frontMesh = Tufts(Front, platR * 1.02f, platR + 20f, -85f, 85f, 420, rng);
                go.AddComponent<MeshFilter>().sharedMesh = frontMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Matte(look.frontGrass, 0.3f);
            }
            // low rocks: the stage's own rock model, or round stones
            var stone = Matte(look.pebbles * 0.85f, 0.15f);
            // patches of the ground a shade lighter and darker all round (off the platform; the land is never one flat colour)
            var lighter = Matte(look.mound, 0.2f); var darker = Matte(look.bank, 0.2f);
            for (int i = 0; i < 70; i++)
            {
                var at = Front(R(0f, 360f), platR + 0.5f + Mathf.Sqrt((float)rng.NextDouble()) * 42f);
                float sx = R(2f, 5.5f), sz = R(1.5f, 4f);
                Shapes.Rounded("Patch", p, at + Vector3.up * 0.01f, new Vector3(sx, 0.04f, sz), 0.015f, i % 2 == 0 ? lighter : darker)
                    .transform.rotation = Quaternion.Euler(0f, R(0f, 180f), 0f);
            }
            for (int i = 0; i < 12; i++)
            {
                float deg = Mathf.Lerp(-75f, 75f, i / 11f) + R(-6f, 6f);
                var at = Front(deg, platR + R(2f, 16f));
                if (look.rocks)
                {
                    var boulder = MadeModel(p, look, "rock", true);
                    var rb = BoundsOf(boulder);
                    boulder.localScale = Vector3.one * (R(0.8f, 1.6f) / Mathf.Max(0.01f, rb.size.y));
                    boulder.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f) * boulder.rotation;
                    rb = BoundsOf(boulder);
                    boulder.position += new Vector3(at.x - rb.center.x, ground - rb.min.y - 0.1f, at.z - rb.center.z);
                }
                else
                {
                    float s = R(0.6f, 1.2f);
                    Shapes.Rounded("Stone", p, at + Vector3.up * s * 0.25f, new Vector3(s * R(1f, 1.6f), s * 0.55f, s * R(0.9f, 1.4f)), s * 0.25f, stone)
                        .transform.rotation = Quaternion.Euler(0f, R(0f, 180f), 0f);
                }
            }
            // pebbles scattered over the ground in front
            var pebble = Matte(look.pebbles, 0.15f);
            var pebbleDark = Matte(look.pebbles * 0.7f, 0.15f);
            for (int i = 0; i < 70; i++)
            {
                var at = Front(R(-85f, 85f), platR + R(0.8f, 20f));
                float s = R(0.12f, 0.38f);
                Shapes.Rounded("Pebble", p, at + Vector3.up * s * 0.15f, new Vector3(s * R(1f, 1.7f), s * 0.4f, s), s * 0.18f, i % 3 == 0 ? pebbleDark : pebble)
                    .transform.rotation = Quaternion.Euler(0f, R(0f, 180f), 0f);
            }
        }

        /// <summary>Tufts of grass in an arc (from <paramref name="a0"/> to <paramref name="a1"/> degrees, between two distances), short and leaning; one mesh.</summary>
        private static Mesh Tufts(System.Func<float, float, Vector3> at, float inner, float outer, float a0, float a1, int tufts, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < tufts; i++)
            {
                var tc = at(R(a0, a1), R(inner, outer));
                int blades = 6 + rng.Next(6);
                float tall = R(0.45f, 1.25f);
                for (int b = 0; b < blades; b++)
                {
                    var bp = tc + new Vector3(R(-0.35f, 0.35f), -0.05f, R(-0.35f, 0.35f));
                    float h = tall * R(0.6f, 1.15f), w = R(0.05f, 0.1f), turn = R(0f, Mathf.PI);
                    var side = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * w;
                    var tip = bp + Vector3.up * h + new Vector3(R(-0.4f, 0.4f), 0f, R(-0.4f, 0.4f)) * h;
                    var normal = (Vector3.Cross(side, tip - bp).normalized + Vector3.up * 0.8f).normalized;
                    var back = new Vector3(-normal.x, normal.y, -normal.z);
                    int k = v.Count;
                    v.Add(bp - side); v.Add(bp + side); v.Add(tip); v.Add(bp - side); v.Add(bp + side); v.Add(tip);
                    n.Add(normal); n.Add(normal); n.Add(Vector3.up); n.Add(back); n.Add(back); n.Add(Vector3.up);
                    t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k + 3); t.Add(k + 4); t.Add(k + 5);
                }
            }
            var m = new Mesh { name = "FrontGrass", indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Tufts of long grass in a ring round <paramref name="c"/> (from <paramref name="inner"/> out to <paramref name="outer"/>):
        /// each blade a thin leaning triangle, both faces, taller further out; one mesh for all of it.
        /// </summary>
        private static Mesh GroveGrass(Vector3 c, float inner, float outer, int tufts, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < tufts; i++)
            {
                float ang = R(0f, Mathf.PI * 2f);
                float d = Mathf.Sqrt(R(inner * inner, outer * outer));
                bool near = Mathf.Cos(ang) > 0.5f && d < inner * 1.6f; // (in front of the camera: shorter, the floor stays in view)
                var tc = c + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d);
                int blades = 7 + rng.Next(6);
                float tall = Mathf.Lerp(0.9f, 2.1f, Mathf.InverseLerp(inner, outer * 0.6f, d)) * (near ? 0.55f : 1f);
                for (int b = 0; b < blades; b++)
                {
                    var at = tc + new Vector3(R(-0.45f, 0.45f), -0.05f, R(-0.45f, 0.45f));
                    float h = tall * R(0.6f, 1.15f), w = R(0.06f, 0.12f), turn = R(0f, Mathf.PI);
                    var side = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * w;
                    var lean = new Vector3(R(-0.45f, 0.45f), 0f, R(-0.45f, 0.45f)) * h;
                    var tip = at + Vector3.up * h + lean;
                    var normal = Vector3.Cross(side, tip - at).normalized;
                    normal = (normal + Vector3.up * 0.8f).normalized;
                    int k = v.Count;
                    v.Add(at - side); v.Add(at + side); v.Add(tip);
                    v.Add(at - side); v.Add(at + side); v.Add(tip);
                    n.Add(normal); n.Add(normal); n.Add(Vector3.up);
                    var back = new Vector3(-normal.x, normal.y, -normal.z);
                    n.Add(back); n.Add(back); n.Add(Vector3.up);
                    t.Add(k); t.Add(k + 2); t.Add(k + 1);
                    t.Add(k + 3); t.Add(k + 4); t.Add(k + 5);
                }
            }
            var m = new Mesh { name = "GroveGrass", indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A soft cloud of particles in a box (the grove's fireflies, its mist), drifting where the noise takes them.</summary>
        private void GroveParticles(string name, Material mat, Vector3 at, Vector3 box, int max, float rate, float life, Color color, float sizeMin, float sizeMax, float drift, float fall = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = drift;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.2f;
            if (fall > 0f)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = new ParticleSystem.MinMaxCurve(-fall * 1.2f, -fall * 0.8f);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }


        private Mesh hazeMesh;
        private static Texture2D hazeFade;

        /// <summary>
        /// A ring of haze round the stage at the land's far edge, facing in: its colour the sky picture's horizon, solid at
        /// the bottom and clear at the top, so the land fades into the picture with no seam whichever way the camera looks.
        /// </summary>
        private void Haze(Color colour, float ground)
        {
            if (hazeFade == null)
            {
                const int N = 64;
                hazeFade = new Texture2D(2, N, TextureFormat.RGBA32, false) { name = "HazeFade", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[2 * N];
                for (int y = 0; y < N; y++)
                {
                    float v = y / (float)(N - 1);
                    var a = (byte)(255f * Mathf.Pow(1f - v, 1.4f));
                    px[y * 2] = px[y * 2 + 1] = new Color32(255, 255, 255, a);
                }
                hazeFade.SetPixels32(px);
                hazeFade.Apply(false, true);
            }
            const int Segments = 48;
            const float Radius = 54f, Height = 26f;
            var c = new Vector3(centre.x - 10f, ground - 1.5f, centre.z); // (the land's own middle: its square reaches 55 out)
            var v3 = new List<Vector3>(); var uv = new List<Vector2>(); var cols = new List<Color>(); var tris = new List<int>();
            for (int i = 0; i <= Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Radius;
                v3.Add(c + d); v3.Add(c + d + Vector3.up * Height);
                uv.Add(new Vector2(i / (float)Segments, 0f)); uv.Add(new Vector2(i / (float)Segments, 1f));
                cols.Add(Color.white); cols.Add(Color.white);
                if (i == Segments) break;
                int k = i * 2;
                tris.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 }); // (both faces: whatever the material culls)
                tris.AddRange(new[] { k, k + 3, k + 1, k, k + 2, k + 3 });
            }
            hazeMesh = new Mesh { name = "Haze" };
            hazeMesh.SetVertices(v3); hazeMesh.SetUVs(0, uv); hazeMesh.SetColors(cols); hazeMesh.SetTriangles(tris, 0);
            hazeMesh.RecalculateBounds();
            var m = new Material(Weather.AlphaMaterial);
            m.SetTexture("_BaseMap", hazeFade); m.SetTexture("_MainTex", hazeFade);
            m.SetColor("_BaseColor", colour);
            m.renderQueue = 2990;
            mats.Add(m);
            var go = new GameObject("Haze");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = hazeMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private float platRadius;

        /// <summary>
        /// A post with a crossbar at the platform's back corner on one side (in front of its stand) (<paramref name="south"/>: the south end's),
        /// tall enough for <paramref name="length"/> of cape; returns the hook a cape's top hangs from.
        /// </summary>
        public Vector3 CapeHook(bool south, float length)
        {
            float r = platRadius > 0f ? platRadius : Mathf.Max(hw, hd);
            float a = (south ? -1f : 1f) * 128f * Mathf.Deg2Rad; // (the back corner, in front of that side's stand)
            var foot = new Vector3(centre.x + Mathf.Cos(a) * r * 0.9f, floorY, centre.z + Mathf.Sin(a) * r * 0.9f); // (on the platform's edge)
            float h = length + 0.45f;
            var wood = Surface(new Color(0.32f, 0.2f, 0.12f), 0.3f);
            var brass = Surface(new Color(0.85f, 0.65f, 0.3f), 0.7f);
            Shapes.Rounded("CapePost", transform, foot + Vector3.up * h * 0.5f, new Vector3(0.14f, h, 0.14f), 0.04f, wood);
            Shapes.Rounded("CapeBar", transform, foot + Vector3.up * h, new Vector3(0.12f, 0.1f, 0.9f), 0.03f, wood);
            Shapes.Rounded("CapeKnob", transform, foot + Vector3.up * (h + 0.1f), new Vector3(0.2f, 0.16f, 0.2f), 0.08f, brass);
            return foot + Vector3.up * (h - 0.05f) + new Vector3(0.1f, 0f, 0f);
        }

        /// <summary>The middle of the platform (on the floor).</summary>
        public Vector3 Middle => new Vector3(centre.x, floorY, centre.z);

        /// <summary>A stand turned about its own middle until both its ends are as far from the platform's middle.</summary>
        private void LevelEnds(Transform stand, Vector3 pivot, Vector3 outward)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var side = Vector3.Cross(Vector3.up, outward);
                float dl = EndDistance(stand, pivot, side, -1f), dr = EndDistance(stand, pivot, side, 1f);
                if (float.IsNaN(dl) || float.IsNaN(dr)) return;
                var b = BoundsOf(stand);
                float half = Mathf.Max(0.5f, Mathf.Max(b.extents.x, b.extents.z));
                float turn = Mathf.Atan2(dl - dr, half * 1.6f) * Mathf.Rad2Deg; // (the nearer end swung out)
                if (Mathf.Abs(turn) < 0.5f) return;
                stand.RotateAround(b.center, Vector3.up, turn);
            }
        }

        /// <summary>How far from the platform's middle a stand's end (<paramref name="sign"/>: which side) comes nearest.</summary>
        private static float EndDistance(Transform stand, Vector3 pivot, Vector3 side, float sign)
        {
            var b = BoundsOf(stand);
            float half = Mathf.Max(b.extents.x, b.extents.z);
            float best = float.MaxValue;
            foreach (var mf in stand.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var m = mf.transform.localToWorldMatrix;
                var c = b.center;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = m.MultiplyPoint3x4(v);
                    float across = Vector3.Dot(w - c, side) * sign;
                    if (across < half * 0.6f) continue; // (its outer end only)
                    var flat = w - pivot; flat.y = 0f;
                    best = Mathf.Min(best, flat.magnitude);
                }
            }
            return best == float.MaxValue ? float.NaN : best;
        }

        private readonly List<Mesh> bentMeshes = new List<Mesh>();

        /// <summary>
        /// A stand bent round the platform's ring: its front follows the ring (the middle against the rim, the ends coming
        /// round with it), whichever way the made model curves.
        /// </summary>
        private void BendToRing(Transform stand, Vector3 pivot, Vector3 outward, float ring)
        {
            var side = Vector3.Cross(Vector3.up, outward);
            var b = BoundsOf(stand);
            float half = Mathf.Max(b.extents.x, b.extents.z);
            float mid = FrontDistance(stand, pivot, outward);
            float end = FrontDistance(stand, pivot, outward, 0.6f, 0.95f);
            float xe = half * 0.8f;
            float want = ring - Mathf.Sqrt(Mathf.Max(0f, ring * ring - xe * xe)); // how much nearer the floor's middle the ring is there
            float delta = (end - mid) + want;
            if (Mathf.Abs(delta) < 0.05f) return;
            float k = delta / (xe * xe);
            var c = b.center;
            foreach (var mf in stand.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var mesh = Instantiate(mf.sharedMesh);
                bentMeshes.Add(mesh);
                var v = mesh.vertices;
                var toWorld = mf.transform.localToWorldMatrix;
                var toLocal = mf.transform.worldToLocalMatrix;
                for (int i = 0; i < v.Length; i++)
                {
                    var w = toWorld.MultiplyPoint3x4(v[i]);
                    float x = Vector3.Dot(w - c, side);
                    v[i] = toLocal.MultiplyPoint3x4(w - outward * (k * x * x));
                }
                mesh.vertices = v;
                mesh.RecalculateBounds();
                mf.sharedMesh = mesh;
            }
        }

        /// <summary>One of a made stage's models in its own colour picture (the rune pieces with their runes and crystals glowing).</summary>
        /// <summary>
        /// The emblem in the platform's middle (a dragon, runes, a crest) covered by a plain disc of marble in the stone's
        /// own colour (sampled from the platform's top just outside the disc), lit as the platform is.
        /// </summary>
        private void CoverEmblem(Transform p, Transform plat, Look look, Vector3 centre, float y, float radius)
        {
            var colour = RingColour(plat, look, centre, y, radius) ?? new Color(0.62f, 0.6f, 0.58f);
            var tex = Marble(colour);
            var m = Surface(Color.white, 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", look.selfLight);
            var disc = Shapes.Primitive(PrimitiveType.Cylinder, "PlainMiddle", p, Vector3.zero, new Vector3(radius * 2f, 0.02f, radius * 2f), m).transform;
            disc.position = new Vector3(centre.x, y + 0.025f - 0.02f, centre.z); // (its top just over the walking surface; the foot rings over it)
            disc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>The platform top's colour in a ring just outside <paramref name="radius"/>: its picture sampled where its
        /// upward faces lie there (null: none found).</summary>
        private static Color? RingColour(Transform plat, Look look, Vector3 centre, float y, float radius)
        {
            var src = Resources.Load<Texture2D>(look.folder + "platform_tex");
            if (src == null) return null;
            // a small readable copy of the picture (the original stays unreadable: no doubled memory)
            var rt = RenderTexture.GetTemporary(128, 128, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            small.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            Color sum = Color.black; int n = 0;
            foreach (var mf in plat.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var v = mesh.vertices; var uv = mesh.uv; var nr = mesh.normals;
                if (uv.Length != v.Length) continue;
                for (int i = 0; i < v.Length; i++)
                {
                    var w = mf.transform.TransformPoint(v[i]);
                    if (Mathf.Abs(w.y - y) > 0.25f) continue;
                    if (nr.Length == v.Length && mf.transform.TransformDirection(nr[i]).y < 0.7f) continue;
                    float r = new Vector2(w.x - centre.x, w.z - centre.z).magnitude;
                    if (r < radius * 1.0f || r > radius * 1.14f) continue;
                    sum += small.GetPixelBilinear(uv[i].x, uv[i].y); n++;
                }
            }
            Object.Destroy(small);
            if (n < 8) return null;
            var c = sum / n; c.a = 1f;
            return c;
        }

        /// <summary>A marble picture in <paramref name="c"/>: soft clouds and thin veins a little lighter and darker.</summary>
        private Texture2D Marble(Color c)
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Marble" };
            var px = new Color[N * N];
            float seed = c.r * 37f + c.g * 11f;
            for (int yy = 0; yy < N; yy++)
                for (int xx = 0; xx < N; xx++)
                {
                    float u = xx / (float)N, v = yy / (float)N;
                    float cloud = Mathf.PerlinNoise(u * 4f + seed, v * 4f) * 0.6f + Mathf.PerlinNoise(u * 11f, v * 11f + seed) * 0.4f;
                    float turb = Mathf.PerlinNoise(u * 6f + 3.1f, v * 6f + seed) * 5f;
                    float vein = Mathf.Pow(1f - Mathf.Abs(Mathf.Sin((u * 3f + v * 2f) * Mathf.PI * 2f + turb)), 14f);
                    float k = 0.92f + (cloud - 0.5f) * 0.16f;
                    var col = c * k;
                    col = Color.Lerp(col, Color.Lerp(c, Color.white, 0.45f), vein * 0.55f);
                    col.a = 1f;
                    px[yy * N + xx] = col;
                }
            tex.SetPixels(px);
            tex.Apply(true, true);
            tex.anisoLevel = 2;
            marbles.Add(tex);
            return tex;
        }

        private Transform MadeModel(Transform p, Look look, string name, bool runes, bool matte = false)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(p, false);
            var prefab = Resources.Load<GameObject>(look.folder + name);
            if (prefab == null) return holder;
            Instantiate(prefab, holder, false);
            var m = Surface(Color.white, matte ? 0f : 0.12f);
            if (matte)
            {
                // no highlights, no reflections: the stone reads by its picture alone
                m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            var tex = Resources.Load<Texture2D>(look.folder + name + "_tex");
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            var glow = runes ? Resources.Load<Texture2D>(look.folder + name + "_glow") : null;
            // (a little of its own colour too: the grove is dark, but nothing in it goes black)
            m.SetTexture("_EmissionMap", glow != null ? glow : tex);
            m.SetColor("_EmissionColor", glow != null ? look.glow * (matte ? 0.55f : 1.4f) : look.selfLight); // (the platform's runes show, never glare)
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return holder;
        }

        /// <summary>
        /// How far out from <paramref name="pivot"/> (along <paramref name="outward"/>) a stand's front is, measured on its middle
        /// (the third of it straight out from the pivot).
        /// </summary>
        private static float FrontDistance(Transform stand, Vector3 pivot, Vector3 outward, float from = 0f, float to = 0.33f)
        {
            var side = Vector3.Cross(Vector3.up, outward);
            var b = BoundsOf(stand);
            float halfWidth = Mathf.Max(b.extents.x, b.extents.z);
            float best = float.MaxValue;
            foreach (var mf in stand.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = m.MultiplyPoint3x4(v) - pivot;
                    float across = Mathf.Abs(Vector3.Dot(w, side)) / halfWidth;
                    if (across < from || across > to) continue;
                    best = Mathf.Min(best, Vector3.Dot(w, outward));
                }
            }
            return best == float.MaxValue ? Vector3.Dot(b.center - pivot, outward) - halfWidth * 0.5f : best;
        }

        private static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Colliders on a model's meshes for a moment (to find its surfaces), taken off again by <see cref="Unprobe"/>.</summary>
        private static List<MeshCollider> Probe(Transform t)
        {
            var list = new List<MeshCollider>();
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.isReadable) { var c = mf.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = mf.sharedMesh; list.Add(c); }
            Physics.SyncTransforms();
            return list;
        }

        private static void Unprobe(List<MeshCollider> list) { foreach (var c in list) if (c != null) DestroyImmediate(c); }

        /// <summary>The height of a model's surface under a point (from above), or <paramref name="otherwise"/>.</summary>
        private static float SurfaceAt(Transform t, Vector3 from, float otherwise)
        {
            var probe = Probe(t);
            bool found = false;
            float y = otherwise;
            foreach (var c in probe)
                if (c.Raycast(new Ray(from, Vector3.down), out var hit, 200f)) { y = found ? Mathf.Max(y, hit.point.y) : hit.point.y; found = true; }
            Unprobe(probe);
            return y;
        }

        /// <summary>The seats a stand's steps make: the highest upward-facing step under its roof, on a grid.</summary>
        private List<Vector3> Seats(Transform stand, float spacing = 0.55f)
        {
            var seats = new List<Vector3>();
            var b = BoundsOf(stand);
            var probe = Probe(stand);
            float roof = b.min.y + b.size.y * 0.8f, ground = b.min.y;
            var ups = new List<float>();
            for (float x = b.min.x + 0.3f; x < b.max.x - 0.3f; x += spacing)
                for (float z = b.min.z + 0.3f; z < b.max.z - 0.3f; z += spacing)
                {
                    // down the column surface by surface: every upward face, highest first
                    ups.Clear();
                    foreach (var c in probe)
                    {
                        var from = new Vector3(x, b.max.y + 1f, z);
                        for (int guard = 0; guard < 12 && c.Raycast(new Ray(from, Vector3.down), out var hit, 200f); guard++)
                        {
                            if (hit.normal.y > 0.6f && hit.point.y > ground + 0.3f) ups.Add(hit.point.y);
                            from = hit.point + Vector3.down * 0.05f;
                        }
                    }
                    if (ups.Count == 0) continue;
                    ups.Sort((p, q) => q.CompareTo(p));
                    // the top one with something under it is a roof (or its eaves): the seat is the step under it; a step open to
                    // the sky only low down (a roof's eave with nothing under it is not a seat)
                    bool roofed = ups.Count >= 2 && ups[0] - ups[1] > 1f; // (room to sit between them: a roof over a step)
                    float seat = roofed ? ups[1] : ups[0];
                    if (!roofed && ups[0] > b.min.y + b.size.y * 0.5f) continue;
                    if (seat >= roof) continue;
                    seats.Add(new Vector3(x, seat, z));
                }
            Unprobe(probe);
            return seats;
        }

        /// <summary>Whether a stand's steps rise away from the floor (its low front towards it).</summary>
        private bool SeatsFaceFloor(Transform stand, float dir)
        {
            var seats = Seats(stand);
            if (seats.Count < 8) return true;
            float near = 0f, far = 0f; int nn = 0, nf = 0;
            float mid = BoundsOf(stand).center.z;
            foreach (var s in seats)
            {
                if ((s.z - mid) * dir < 0f) { near += s.y; nn++; } else { far += s.y; nf++; }
            }
            return nn == 0 || nf == 0 || near / nn <= far / nf;
        }

        /// <summary>A side's fans on a stand's steps, each looking at the floor.</summary>
        private void SeatCrowd(Transform stand, int team, System.Random rng)
        {
            crowdSize = MadeCrowd;
            float k = crowdSize;
            var seats = Seats(stand, (FanModels ? 0.85f : 0.55f) * k); // (bigger fans: further apart, fewer to a step; the made people bigger still)
            foreach (var at in seats) AddFan(at, team, rng);
        }

        /// <summary>One fan standing (or sitting) at <paramref name="at"/>, looking at the floor (a seat its rescued foi fill, or none).</summary>
        private void AddFan(Vector3 at, int team, System.Random rng)
        {
            float k = crowdSize;
            if (rng.NextDouble() > Fill[team]) return; // the seats its rescued foi fill
            float facing = Mathf.Atan2(centre.x - at.x, centre.z - at.z) * Mathf.Rad2Deg;
            float phase = (float)rng.NextDouble() * 10f;
            if (AddMadeFan(at, team, facing, phase, rng)) return; // (the made people, where their models are)
            var body = batches[team * 4 + rng.Next(4)];
            body.pos.Add(at + Vector3.up * 0.18f * k); body.phase.Add(phase); body.side.Add(team); body.facing.Add(facing);
            var head = batches[8 + rng.Next(4)];
            head.pos.Add(at + Vector3.up * 0.46f * k); head.phase.Add(phase); head.side.Add(team); head.facing.Add(facing);
            if (rng.NextDouble() < FlagShare[team])
            {
                var flag = batches[12 + team];
                flag.pos.Add(at + new Vector3(0.1f, 0.85f, 0f) * k); flag.phase.Add(phase); flag.side.Add(team); flag.facing.Add(facing);
            }
        }

        private static Texture2D groveSky;

        /// <summary>A made stage's sky: the glacier's mountains, or the grove's night.</summary>
        public static Texture SkyOf(Theme theme)
        {
            var l = LookOf(theme);
            if (l.sky != null) { var picture = Resources.Load<Texture2D>(l.sky); if (picture != null) return picture; }
            if (l.skyTop.a <= 0f) return GroveSky();
            if (skies.TryGetValue(theme, out var made) && made != null) return made;
            return skies[theme] = Gradient(l.skyTop, l.skyLow, theme.ToString());
        }

        private static readonly Dictionary<Theme, Texture2D> skies = new Dictionary<Theme, Texture2D>();

        /// <summary>A sky from one colour high up to another low down, softly (a glow at the horizon).</summary>
        private static Texture2D Gradient(Color top, Color low, string name)
        {
            const int H = 256;
            var tex = new Texture2D(4, H, TextureFormat.RGBA32, false) { name = "Sky " + name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[4 * H];
            for (int y = 0; y < H; y++)
            {
                float k = y / (float)(H - 1); // 0 the bottom .. 1 the top
                var c = Color.Lerp(low, top, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.35f) / 0.65f)));
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>The grove's sky: deep night blue above, a soft blue glow low between the trees.</summary>
        public static Texture GroveSky()
        {
            if (groveSky != null) return groveSky;
            const int H = 256;
            groveSky = new Texture2D(4, H, TextureFormat.RGBA32, false) { name = "GroveSky", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[4 * H];
            for (int y = 0; y < H; y++)
            {
                float k = y / (float)(H - 1); // 0 the bottom .. 1 the top
                var c = Color.Lerp(new Color(0.16f, 0.42f, 0.55f), new Color(0.02f, 0.05f, 0.12f), Mathf.SmoothStep(0f, 1f, Mathf.Abs(k - 0.42f) / 0.58f));
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            groveSky.SetPixels32(px);
            groveSky.Apply(false, true);
            return groveSky;
        }

        private void OnDestroy()
        {
            foreach (var m in mats) if (m != null) Destroy(m);
            Blockers.Clear();
            foreach (var mt in marbles) if (mt != null) Destroy(mt);
            if (ownedMesh != null) Destroy(ownedMesh);
            if (grassMesh != null) Destroy(grassMesh);
            if (frontMesh != null) Destroy(frontMesh);
            foreach (var bm in bentMeshes) if (bm != null) Destroy(bm);
            if (hazeMesh != null) Destroy(hazeMesh);
            if (murmur != null && murmur.clip != null) Destroy(murmur.clip);
        }
    }
}
