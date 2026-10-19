using System.Collections.Generic;
using BrickMuseum.Build;
using BrickMuseum.Data;
using UnityEngine;

namespace BrickMuseum.FreeBuild
{
    /// <summary>
    /// A lot of the player's own building, piece by piece on the stud grid (y in plates). Two uses:
    /// <list type="bullet">
    /// <item>editing (far from the city, with colliders): a tap on the top of something puts the piece on it, a tap on a
    /// side puts it next to it at the same height; pieces can't overlap; every piece comes out of (and goes back to) the
    /// material stock;</item>
    /// <item>standing in the city (no colliders): the walker's ground, walls, stairs and doors come from a grid of solid
    /// cells — doorways are open, stairs are steps — and the camera stops at walls.</item>
    /// </list>
    /// </summary>
    public sealed class BuildSite
    {
        public const int MaxY = 48, Storey = 10;
        private const float P = Piece.Plate;
        /// <summary>Where the editing lots are, far from the city (house lot, car chassis).</summary>
        public static readonly Vector3 HouseOrigin = new Vector3(-3000f, 1500f, 0f), CarOrigin = new Vector3(-3000f, 1500f, 400f), GardenOrigin = new Vector3(-3000f, 1500f, 800f);

        public sealed class Placed
        {
            public BuildPieceData data;
            public BuildKind kind;
            public GameObject go;
            public Transform leaf;           // doors: the swinging leaf
            public float open;
            public int Fw => data.r % 2 == 1 ? kind.d : kind.w;
            public int Fd => data.r % 2 == 1 ? kind.w : kind.d;
        }

        public readonly int lx, lz;
        public readonly Transform root;
        public readonly bool editable;
        public readonly List<Placed> pieces = new List<Placed>();
        private readonly List<BuildPieceData> data;
        private readonly Dictionary<int, Placed> cells = new Dictionary<int, Placed>();   // taken (for building)
        private readonly Dictionary<int, Placed> solid = new Dictionary<int, Placed>();   // in the way (for walking)
        private readonly Dictionary<Collider, Placed> byCollider = new Dictionary<Collider, Placed>();
        private readonly Collider baseCollider;
        private readonly Stack<(bool added, BuildPieceData data)> undo = new Stack<(bool, BuildPieceData)>();
        private GameObject ghost;
        private string ghostKey;
        public int view;      // 0 = everything, k = only the first k storeys
        public bool dirty;

        /// <param name="corner">World position of the lot's (0,0,0) corner; the lot turns round it by <paramref name="yaw"/>.</param>
        public BuildSite(Transform parent, List<BuildPieceData> data, int lx, int lz, Vector3 corner, float yaw, float scale, bool editable, string baseColour, bool baseplate = true)
        {
            this.data = data; this.lx = lx; this.lz = lz; this.editable = editable;
            root = new GameObject(editable ? "BuildSite" : "MyHouse").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(corner, Quaternion.Euler(0, yaw, 0));
            root.localScale = Vector3.one * scale;

            if (editable)
            {
                var lawn = new MeshBuilder();
                lawn.Box(new Vector3(lx / 2f, -P - 0.1f, lz / 2f), new Vector3(300f, 0.2f, 300f));
                var lg = new GameObject("Lawn");
                lg.transform.SetParent(root, false);
                lg.AddComponent<MeshFilter>().sharedMesh = lawn.ToMesh("lawn");
                lg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.55f, 0.76f, 0.45f), 0.1f);
            }
            GameObject plate = null;
            if (baseplate)
            {
                plate = PieceFactory.Create(Piece.Make("brick", baseColour, 0, 0, 0, lx, lz, 1), PieceLook.Solid, root);
                plate.name = "Baseplate";
                plate.transform.localPosition = new Vector3(lx / 2f, -P, lz / 2f);
            }
            if (editable && plate != null)
            {
                var bc = plate.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, P / 2f, 0);
                bc.size = new Vector3(lx, P, lz);
                baseCollider = bc;
            }
            foreach (var d in data) Spawn(d);
        }

        /// <summary>World corner for a lot centred on <paramref name="center"/> and turned by <paramref name="yaw"/>.</summary>
        public static Vector3 CornerFor(Vector3 center, int lx, int lz, float yaw, float scale = 1f) =>
            center - Quaternion.Euler(0, yaw, 0) * new Vector3(lx / 2f, 0, lz / 2f) * scale;

        private int Cell(int x, int y, int z) => x + lx * (z + lz * y);
        private bool Inside(int x, int y, int z) => x >= 0 && z >= 0 && y >= 0 && x < lx && z < lz && y < MaxY;

        private Placed Spawn(BuildPieceData d)
        {
            var kind = Stock.Find(d.kind);
            if (kind == null) return null;
            var pc = new Placed { data = d, kind = kind };
            var piece = kind.Make(d.c, d.x, d.z, d.y, d.r);
            pc.go = BuildParts.Create(piece, PieceLook.Solid, root);
            pc.go.transform.localRotation = PieceFactory.Rotation(piece.steps);
            pc.go.transform.localPosition = new Vector3(d.x + pc.Fw / 2f, d.y * P, d.z + pc.Fd / 2f);
            pc.leaf = pc.go.transform.Find("Leaf");
            if (editable)
            {
                var col = pc.go.AddComponent<BoxCollider>();
                col.center = new Vector3(0, kind.h * P / 2f, 0);
                col.size = new Vector3(kind.w, kind.h * P, kind.d);
                byCollider[col] = pc;
            }
            for (int x = 0; x < pc.Fw; x++) for (int z = 0; z < pc.Fd; z++) for (int y = 0; y < kind.h; y++) cells[Cell(d.x + x, d.y + y, d.z + z)] = pc;
            // the walking grid, in the piece's own frame: doorways stay open, stairs are steps
            var rot = Quaternion.Euler(0, 90f * d.r, 0);
            for (int i = 0; i < kind.w; i++)
                for (int j = 0; j < kind.d; j++)
                {
                    var v = rot * new Vector3(i + 0.5f - kind.w / 2f, 0, j + 0.5f - kind.d / 2f);
                    int fx = d.x + Mathf.FloorToInt(v.x + pc.Fw / 2f), fz = d.z + Mathf.FloorToInt(v.z + pc.Fd / 2f);
                    int height = kind.h, from = 0;
                    if (kind.t == "stairs") height = Mathf.RoundToInt((kind.w - i) * (float)kind.h / kind.w);       // high end at -x
                    if (kind.t == "door" && i > 0 && i < kind.w - 1) from = kind.h - 2;                              // only the lintel
                    for (int k = from; k < height; k++) if (Inside(fx, d.y + k, fz)) solid[Cell(fx, d.y + k, fz)] = pc;
                }
            pieces.Add(pc);
            ApplyView(pc);
            guidesDirty = true;
            return pc;
        }

        private void Despawn(Placed pc)
        {
            var d = pc.data;
            for (int x = 0; x < pc.Fw; x++) for (int z = 0; z < pc.Fd; z++) for (int y = 0; y < pc.kind.h; y++)
                    { int c = Cell(d.x + x, d.y + y, d.z + z); cells.Remove(c); if (solid.TryGetValue(c, out var o) && o == pc) solid.Remove(c); }
            var col = pc.go.GetComponent<BoxCollider>();
            if (col != null) byCollider.Remove(col);
            pieces.Remove(pc);
            Object.Destroy(pc.go);
            guidesDirty = true;
        }

        private bool Free(int x0, int y0, int z0, int fw, int h, int fd)
        {
            if (x0 < 0 || z0 < 0 || y0 < 0 || x0 + fw > lx || z0 + fd > lz || y0 + h > MaxY) return false;
            for (int x = 0; x < fw; x++) for (int z = 0; z < fd; z++) for (int y = 0; y < h; y++) if (cells.ContainsKey(Cell(x0 + x, y0 + y, z0 + z))) return false;
            return true;
        }

        public bool IsFree(int x, int y, int z) => Inside(x, y, z) && !cells.ContainsKey(Cell(x, y, z));

        private static readonly Placed Blocker = new Placed();

        /// <summary>Keeps cells free of pieces (a garden's house stands there).</summary>
        public void Block(int x0, int z0, int w, int d, int h)
        {
            for (int x = x0; x < x0 + w; x++) for (int z = z0; z < z0 + d; z++) for (int y = 0; y < h; y++) if (Inside(x, y, z)) cells[Cell(x, y, z)] = Blocker;
        }

        // ================= editing =================

        /// <summary>Where a piece of this kind would go for a screen ray (lot cells; y in plates). False: nowhere / blocked.</summary>
        public bool Target(Ray ray, BuildKind kind, int rot, out Vector3Int at, out bool hitSomething)
        {
            at = default;
            hitSomething = false;
            if (!editable || !Physics.Raycast(ray, out var hit, 600f)) return false;
            bool onBase = hit.collider == baseCollider;
            if (!onBase && !byCollider.ContainsKey(hit.collider)) return false;
            hitSomething = true;
            int bottom = 0, top = 0;
            if (!onBase) { var hp = byCollider[hit.collider]; bottom = hp.data.y; top = hp.data.y + hp.kind.h; }
            int fw = rot % 2 == 1 ? kind.d : kind.w, fd = rot % 2 == 1 ? kind.w : kind.d;
            var l = root.InverseTransformPoint(hit.point);
            var n = root.InverseTransformDirection(hit.normal);
            int x0, z0, y0;
            if (n.y > 0.5f || onBase)
            {
                int cx = Mathf.FloorToInt(l.x), cz = Mathf.FloorToInt(l.z);
                x0 = cx - (fw - 1) / 2; z0 = cz - (fd - 1) / 2; y0 = top;
            }
            else if (n.y < -0.5f)
            {
                int cx = Mathf.FloorToInt(l.x), cz = Mathf.FloorToInt(l.z);
                x0 = cx - (fw - 1) / 2; z0 = cz - (fd - 1) / 2; y0 = bottom - kind.h;
            }
            else
            {
                var q = l + n * 0.5f;
                int cx = Mathf.FloorToInt(q.x), cz = Mathf.FloorToInt(q.z);
                x0 = n.x > 0.5f ? cx : n.x < -0.5f ? cx - fw + 1 : cx - (fw - 1) / 2;
                z0 = n.z > 0.5f ? cz : n.z < -0.5f ? cz - fd + 1 : cz - (fd - 1) / 2;
                y0 = bottom;
            }
            x0 = Mathf.Clamp(x0, 0, lx - fw);
            z0 = Mathf.Clamp(z0, 0, lz - fd);
            at = new Vector3Int(x0, y0, z0);
            return Free(x0, y0, z0, fw, kind.h, fd);
        }

        /// <summary>Puts a piece where the ray points, if there is room and stock. Returns why not, or null.</summary>
        public string Place(Ray ray, BuildKind kind, string colour, int rot)
        {
            if (Stock.Count(kind.key) <= 0) return "Bu parçadan kalmadı";
            if (!Target(ray, kind, rot, out var at, out bool hitSomething)) return hitSomething ? "Oraya sığmıyor" : null;
            Add(kind, colour, at.x, at.y, at.z, rot);
            return null;
        }

        /// <summary>Puts a piece at a given cell (tests / scripted builds); false when it doesn't fit or none is left.</summary>
        public bool PlaceAt(string key, string colour, int x, int y, int z, int rot)
        {
            var kind = Stock.Find(key);
            if (kind == null || Stock.Count(key) <= 0) return false;
            int fw = rot % 2 == 1 ? kind.d : kind.w, fd = rot % 2 == 1 ? kind.w : kind.d;
            if (!Free(x, y, z, fw, kind.h, fd)) return false;
            Add(kind, colour, x, y, z, rot);
            return true;
        }

        private void Add(BuildKind kind, string colour, int x, int y, int z, int rot)
        {
            Stock.Take(kind.key);
            var d = new BuildPieceData { kind = kind.key, c = colour, x = x, y = y, z = z, r = rot };
            data.Add(d);
            Spawn(d);
            undo.Push((true, d));
            dirty = true;
        }

        /// <summary>Takes the piece under the ray back into the stock.</summary>
        public bool RemoveAt(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 600f) || !byCollider.TryGetValue(hit.collider, out var pc)) return false;
            Remove(pc);
            undo.Push((false, pc.data));
            return true;
        }

        private void Remove(Placed pc)
        {
            data.Remove(pc.data);
            Stock.Add(pc.kind.key, 1);
            Despawn(pc);
            dirty = true;
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            var (added, d) = undo.Pop();
            var k = Stock.Find(d.kind);
            if (added)
            {
                var pc = pieces.Find(p => p.data == d);
                if (pc != null) Remove(pc);
            }
            else if (k != null && Free(d.x, d.y, d.z, d.r % 2 == 1 ? k.d : k.w, k.h, d.r % 2 == 1 ? k.w : k.d) && Stock.Take(d.kind))
            {
                data.Add(d);
                Spawn(d);
            }
            dirty = true;
            return true;
        }

        public int CountOf(string t) { int n = 0; foreach (var pc in pieces) if (pc.kind.t == t) n++; return n; }

        // ================= what is shown =================

        public void SetView(int storeys) { view = storeys; foreach (var pc in pieces) ApplyView(pc); guidesDirty = true; }

        // ================= next-storey guides =================

        private GameObject guides;
        private bool guidesDirty = true;

        /// <summary>
        /// Once a storey's walls are roofed over by a floor (most of the walls' footprint covered), see-through bricks show
        /// where the walls of the storey above go: the same footprint, standing on the new floor, wherever it is still free.
        /// Taps go through them to the floor, so a piece lands right there.
        /// </summary>
        public void UpdateGuides()
        {
            if (!editable || !guidesDirty) return;
            guidesDirty = false;
            if (guides != null) Object.Destroy(guides);
            var b = new MeshBuilder();
            int count = 0;
            for (int k = 1; k * Storey + 3 <= MaxY; k++)
            {
                int floorY = k * Storey - 1, top = k * Storey;
                if (view != 0 && view < k + 1) break;
                var walls = new List<(int x, int z)>();
                int covered = 0;
                for (int x = 0; x < lx; x++)
                    for (int z = 0; z < lz; z++)
                    {
                        // a wall cell: something stands in the top course of the storey below (not the stairs or a railing)
                        bool wall = false;
                        for (int y = floorY - 3; y < floorY && !wall; y++)
                            if (y >= 0 && cells.TryGetValue(Cell(x, y, z), out var pc) && pc.kind != null && pc.kind.t != "stairs" && pc.kind.t != "fence") wall = true;
                        if (!wall) continue;
                        walls.Add((x, z));
                        if (cells.ContainsKey(Cell(x, floorY, z))) covered++;
                    }
                if (walls.Count < 8 || covered < walls.Count * 0.5f) continue;   // (a stairwell may stay open)
                foreach (var (x, z) in walls)
                {
                    if (!cells.ContainsKey(Cell(x, floorY, z))) continue;               // only where the floor carries it
                    if (!IsFree(x, top, z) || !IsFree(x, top + 1, z) || !IsFree(x, top + 2, z)) continue;
                    b.Box(new Vector3(x + 0.5f, (top + 1.5f) * P, z + 0.5f), new Vector3(0.9f, 3f * P - 0.06f, 0.9f));
                    count++;
                }
            }
            if (count == 0) return;
            guides = new GameObject("NextStoreyGuides");
            guides.transform.SetParent(root, false);
            guides.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("guides");
            var r = guides.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mats.Ghost("lblue");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void ApplyView(Placed pc) => pc.go.SetActive(view == 0 || pc.data.y < view * Storey - 1);

        /// <summary>A see-through copy of the piece where it would go, red when it doesn't fit.</summary>
        public void ShowGhost(Ray? ray, BuildKind kind, string colour, int rot)
        {
            bool ok = false;
            Vector3Int at = default;
            bool hitSomething = false;
            if (ray.HasValue && kind != null) ok = Target(ray.Value, kind, rot, out at, out hitSomething);
            if (!hitSomething) { HideGhost(); return; }
            string key = $"{kind.key}|{(ok ? colour : "red")}|{rot}";
            if (ghost == null || ghostKey != key)
            {
                if (ghost != null) Object.Destroy(ghost);
                var piece = kind.Make(ok ? colour : "red", 0, 0, 0, rot);
                ghost = BuildParts.Create(piece, PieceLook.Ghost, root);
                ghost.transform.localRotation = PieceFactory.Rotation(piece.steps);
                ghostKey = key;
            }
            ghost.SetActive(true);
            int fw = rot % 2 == 1 ? kind.d : kind.w, fd = rot % 2 == 1 ? kind.w : kind.d;
            ghost.transform.localPosition = new Vector3(at.x + fw / 2f, at.y * P, at.z + fd / 2f);
        }

        public void HideGhost() { if (ghost != null) ghost.SetActive(false); }

        /// <summary>Highest storey that has anything in it (1-based).</summary>
        public int Storeys
        {
            get
            {
                int top = 0;
                foreach (var pc in pieces) top = Mathf.Max(top, pc.data.y);
                return Mathf.Clamp(top / Storey + 1, 1, MaxY / Storey);
            }
        }

        // ================= walking in it =================

        private bool SolidAt(int x, int y, int z, bool visibleOnly = false) =>
            Inside(x, y, z) && solid.TryGetValue(Cell(x, y, z), out var pc) && (!visibleOnly || pc.go.activeSelf);

        /// <summary>True when the world point is over the lot.</summary>
        public bool Covers(Vector3 world, float margin = 0f)
        {
            var l = root.InverseTransformPoint(world);
            return l.x >= -margin && l.z >= -margin && l.x <= lx + margin && l.z <= lz + margin;
        }

        /// <summary>The walkable height under a walker (world y): the highest solid top no more than a step above the feet.</summary>
        public float Ground(Vector3 world, float step = 1.1f)
        {
            var l = root.InverseTransformPoint(world);
            int cx = Mathf.FloorToInt(l.x), cz = Mathf.FloorToInt(l.z);
            int kTop = Mathf.Min(MaxY, Mathf.FloorToInt((l.y + step) / P)) - 1;
            for (int k = kTop; k >= 0; k--) if (SolidAt(cx, k, cz)) return root.TransformPoint(new Vector3(0, (k + 1) * P, 0)).y;
            return root.position.y;   // the baseplate
        }

        /// <summary>Does a walker (a circle of this radius, this tall) standing here bump into a wall?</summary>
        public bool Blocked(Vector3 world, float radius, float height = 1.9f, float step = 1.1f)
        {
            var l = root.InverseTransformPoint(world);
            int k0 = Mathf.Max(0, Mathf.FloorToInt((l.y + step) / P)), k1 = Mathf.Min(MaxY - 1, Mathf.FloorToInt((l.y + height) / P));
            for (int a = 0; a < 8; a++)
            {
                float ang = a * Mathf.PI / 4f;
                int cx = Mathf.FloorToInt(l.x + Mathf.Cos(ang) * radius), cz = Mathf.FloorToInt(l.z + Mathf.Sin(ang) * radius);
                for (int k = k0; k <= k1; k++) if (SolidAt(cx, k, cz)) return true;
            }
            return false;
        }

        /// <summary>Pulls the camera in front of the first wall between it and what it looks at.</summary>
        public Vector3 CamClamp(Vector3 target, Vector3 camPos)
        {
            var a = root.InverseTransformPoint(target);
            var b = root.InverseTransformPoint(camPos);
            float len = Vector3.Distance(a, b);
            var last = a;
            for (float t = 0.3f; t <= len; t += 0.2f)
            {
                var p = Vector3.Lerp(a, b, t / len);
                if (SolidAt(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y / P), Mathf.FloorToInt(p.z), true)) return root.TransformPoint(last);
                last = p;
            }
            return camPos;
        }

        /// <summary>The first visible solid surface a ray meets from above (decorating): the floor point, world.</summary>
        public bool FloorUnder(Ray ray, out Vector3 world)
        {
            world = default;
            var o = root.InverseTransformPoint(ray.origin);
            var dir = root.InverseTransformDirection(ray.direction).normalized;
            var prev = o;
            for (float t = 0f; t < 300f; t += 0.1f)
            {
                var p = o + dir * t;
                if (p.y < 0f && p.x >= 0 && p.z >= 0 && p.x <= lx && p.z <= lz) { world = root.TransformPoint(new Vector3(p.x, 0, p.z)); return true; }
                int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y / P), cz = Mathf.FloorToInt(p.z);
                if (SolidAt(cx, cy, cz, true))
                {
                    // landed on a top: stand on it; hit a wall: drop to the floor in front of it
                    int px = Mathf.FloorToInt(prev.x), pz = Mathf.FloorToInt(prev.z);
                    int k = Mathf.FloorToInt(prev.y / P);
                    if (px == cx && pz == cz) { world = root.TransformPoint(new Vector3(prev.x, (cy + 1) * P, prev.z)); return true; }
                    while (k > 0 && !SolidAt(px, k - 1, pz, true)) k--;
                    world = root.TransformPoint(new Vector3(prev.x, k * P, prev.z));
                    return true;
                }
                prev = p;
            }
            return false;
        }

        /// <summary>Doors swing open for someone close by and close again behind them.</summary>
        public void Tick(Vector3 walker, float dt)
        {
            foreach (var pc in pieces)
            {
                if (pc.leaf == null) continue;
                var c = pc.go.transform.position + Vector3.up * 1.2f;
                bool near = (walker - c).sqrMagnitude < 2.4f * 2.4f;
                pc.open = Mathf.MoveTowards(pc.open, near ? 1f : 0f, dt * 2.5f);
                pc.leaf.localRotation = Quaternion.Euler(0, -95f * Mathf.SmoothStep(0, 1, pc.open), 0);
            }
        }
    }
}
