using System.Collections.Generic;
using BrickMuseum.Build;
using BrickMuseum.Data;
using BrickMuseum.UI;
using BrickMuseum.World;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrickMuseum.City
{
    /// <summary>
    /// The city in the scene: winding streets over hills, pavements, neighbourhood lawns, the creek with its bridges, the
    /// park's footpaths, the slots (coloured pads on little plinths, their kind written on them, grey and numbered while
    /// locked) and the models standing on them. Everything is made on the straight plan and shaped by
    /// <see cref="CityShape"/>. Units are studs, like the models.
    /// </summary>
    public sealed class CityMap
    {
        public const float Lawn = 0.4f;                      // top of the blocks (pavement) above the road
        public const float RoadTop = 0.1f;
        public const float CreekBed = -2.4f, WaterTop = -1.9f;
        public static Vector3 Center => Landscape.CityCenter;
        public readonly Transform root;
        public readonly Townsfolk people;

        private sealed class SlotView
        {
            public Slot slot;
            public GameObject go, pad, model;
            public Renderer padRenderer;
            public TextMeshPro label;
            public BoxCollider hit;
        }

        private readonly Dictionary<Slot, SlotView> views = new Dictionary<Slot, SlotView>();
        private readonly Dictionary<Area, (Renderer lawn, TextMeshPro sign)> areaViews = new Dictionary<Area, (Renderer, TextMeshPro)>();
        private readonly Dictionary<string, Material> padMats = new Dictionary<string, Material>();
        private Material padLocked, padSelected;
        private Slot selected;
        private readonly List<(Transform t, float scale, float time)> pops = new List<(Transform, float, float)>();
        private static readonly Color PavementColour = new Color(0.8f, 0.79f, 0.76f);

        public CityMap(Transform parent)
        {
            root = new GameObject("City").transform;
            root.SetParent(parent, false);
            root.position = Center;
            BuildGround();
            BuildCreek();
            BuildHomeLot();
            foreach (var a in CityLayout.Areas) BuildArea(a);
            people = new Townsfolk(root);
        }

        // ---------- static scenery ----------

        /// <summary>A mesh made on the plan, bent onto the land.</summary>
        private Renderer Add(string name, MeshBuilder b, Material m, bool shadows = false, float maxEdge = 5f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = CityShape.Bend(b.ToMesh(name), maxEdge);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return r;
        }

        private void BuildGround()
        {
            float x0 = CityLayout.MinX, x1 = CityLayout.MaxX, z0 = CityLayout.MinZ, zShore = CityLayout.ZOf(0) + CityLayout.Block / 2f;
            float cx = CityLayout.CreekX, ch = CityLayout.Creek / 2f;
            // the streets: asphalt everywhere but in the creek valley (the blocks stand on it), bridges over the creek
            var asphalt = new MeshBuilder();
            asphalt.Box(new Vector3((x0 + cx - ch) / 2f, RoadTop / 2f, (z0 + zShore) / 2f), new Vector3(cx - ch - x0, RoadTop, zShore - z0));
            asphalt.Box(new Vector3((cx + ch + x1) / 2f, RoadTop / 2f, (z0 + zShore) / 2f), new Vector3(x1 - cx - ch, RoadTop, zShore - z0));
            foreach (float z in CityLayout.StreetZs())
                asphalt.Box(new Vector3(cx, RoadTop - 0.4f, z), new Vector3(CityLayout.Creek + 0.2f, 0.8f, CityLayout.Road));   // the bridge decks
            Add("Roads", asphalt, Mats.Lit(new Color(0.33f, 0.35f, 0.38f), 0.1f), false, 6f);

            var lines = new MeshBuilder();
            foreach (float x in CityLayout.StreetXs())                         // north-south streets: dashed centre lines
                for (float t = z0 + 3f; t < zShore - 4f; t += 8f)
                    if (!CityLayout.OnStreetZ(t + 2f)) lines.Box(new Vector3(x, RoadTop + 0.02f, t + 2f), new Vector3(0.5f, 0.04f, 4f));
            foreach (float z in CityLayout.StreetZs())                         // east-west streets: centre line and parking lanes
            {
                for (float t = x0 + 3f; t < x1 - 4f; t += 8f) lines.Box(new Vector3(t + 2f, RoadTop + 0.02f, z), new Vector3(4f, 0.04f, 0.5f));
                foreach (float side in new[] { -1f, 1f })
                    lines.Box(new Vector3(0, RoadTop + 0.02f, z + side * (CityLayout.Road / 2f - CityLayout.ParkingLane)), new Vector3(x1 - x0, 0.04f, 0.25f));
            }
            Add("RoadLines", lines, Mats.Lit(new Color(0.95f, 0.93f, 0.85f), 0.1f), false, 3f);

            // the bridges' railings
            var rail = new MeshBuilder();
            foreach (float z in CityLayout.StreetZs())
                foreach (float side in new[] { -1f, 1f })
                {
                    float rz = z + side * (CityLayout.Road / 2f - 0.15f);
                    for (float x = cx - ch; x <= cx + ch; x += 3f) rail.Box(new Vector3(x, RoadTop + 0.5f, rz), new Vector3(0.25f, 1f, 0.25f));
                    rail.Box(new Vector3(cx, RoadTop + 1.0f, rz), new Vector3(CityLayout.Creek, 0.15f, 0.2f));
                }
            // the stone quay along the lake and its railing
            var quay = new MeshBuilder();
            quay.Box(new Vector3(0, Lawn / 2f - 1f, zShore + CityLayout.Quay / 2f), new Vector3(x1 - x0, Lawn + 2f, CityLayout.Quay));
            Add("Quay", quay, Mats.Lit(new Color(0.72f, 0.7f, 0.66f), 0.1f));
            float zr = CityLayout.MaxZ - 0.3f;
            for (float x = x0 + 1f; x < x1; x += 4f) rail.Box(new Vector3(x, Lawn + 0.5f, zr), new Vector3(0.25f, 1f, 0.25f));
            rail.Box(new Vector3(0, Lawn + 1.0f, zr), new Vector3(x1 - x0, 0.15f, 0.2f));
            Add("Railing", rail, Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.4f), true, 4f);
        }

        /// <summary>The creek: grassy banks down to the water, from the forest to the lake (under the quay).</summary>
        private void BuildCreek()
        {
            float cx = CityLayout.CreekX, ch = CityLayout.Creek / 2f, w = CityLayout.CreekWater;
            float z0 = CityLayout.MinZ, z1 = CityLayout.MaxZ + 4f;
            var banks = new MeshBuilder();
            var profile = new List<Vector2>
            {
                new Vector2(cx - ch, RoadTop), new Vector2(cx - ch, -6f), new Vector2(cx + ch, -6f), new Vector2(cx + ch, RoadTop),
                new Vector2(cx + w + 1.5f, CreekBed + 0.3f), new Vector2(cx + w - 1f, CreekBed), new Vector2(cx - w + 1f, CreekBed), new Vector2(cx - w - 1.5f, CreekBed + 0.3f),
            };
            profile.Reverse();
            banks.Extrude(profile, z0, z1);
            Add("CreekBanks", banks, Mats.Lit(new Color(0.5f, 0.72f, 0.42f), 0.05f), false, 6f);
            var water = new MeshBuilder();
            water.Box(new Vector3(cx, WaterTop - 0.05f, (z0 + z1) / 2f), new Vector3(2f * w, 0.1f, z1 - z0));
            Add("CreekWater", water, Mats.Lit(new Color(0.3f, 0.56f, 0.8f), 0.92f), false, 6f);
            // stones along the water's edge
            var stones = new MeshBuilder();
            var rnd = new System.Random(7);
            for (float z = z0 + 4f; z < z1 - 6f; z += 7f)
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = cx + side * (w + 0.3f + (float)rnd.NextDouble() * 1.2f), r = 0.5f + (float)rnd.NextDouble() * 0.6f;
                    stones.Cylinder(new Vector3(x, CreekBed + 0.1f, z + (float)rnd.NextDouble() * 4f), r, r * 0.6f, r * 1.1f, 6);
                }
            Add("CreekStones", stones, Mats.Lit(new Color(0.6f, 0.6f, 0.58f), 0.1f), true, 50f);
        }

        /// <summary>The player's lot: a raised block with a pavement, its name on the pavement by the road.</summary>
        private void BuildHomeLot()
        {
            var c = CityLayout.HomeLotCenter;
            var kerb = new MeshBuilder();
            kerb.Box(new Vector3(c.x, Lawn / 2f - 1f, c.y), new Vector3(CityLayout.HomeLot, Lawn + 2f, CityLayout.HomeLot));
            Add("Pavement Home", kerb, Mats.Lit(PavementColour, 0.1f));
            var lawn = new MeshBuilder();
            lawn.Box(new Vector3(c.x, Lawn + 0.03f, c.y), new Vector3(CityLayout.HomeLot - 2f * CityLayout.Pavement, 0.06f, CityLayout.HomeLot - 2f * CityLayout.Pavement));
            Add("Lawn Home", lawn, Mats.Lit(new Color(0.58f, 0.8f, 0.5f), 0.05f));
            var sign = FlatText(root, c + new Vector2(0, CityLayout.HomeLot / 2f - CityLayout.Pavement / 2f), Lawn + 0.08f, "Evim", 1.9f, 20f);
            sign.color = new Color(0.25f, 0.27f, 0.3f);
        }

        private void BuildArea(Area a)
        {
            if (a.creek) { foreach (var s in a.slots) BuildSlot(s); return; }
            var kerb = new MeshBuilder();
            kerb.Box(new Vector3(a.center.x, Lawn / 2f - 1f, a.center.y), new Vector3(CityLayout.Block, Lawn + 2f, CityLayout.Block));   // a kerb on every side, also downhill
            Add("Pavement " + a.name, kerb, Mats.Lit(PavementColour, 0.1f));
            var lawn = new MeshBuilder();
            float inner = CityLayout.Block - 2f * CityLayout.Pavement;   // a pavement all round the block
            lawn.Box(new Vector3(a.center.x, Lawn + 0.03f, a.center.y), new Vector3(inner, 0.06f, inner));
            var r = Add("Lawn " + a.name, lawn, Mats.Lit(a.ground, 0.05f));
            // winding gravel footpaths (the park)
            if (a.paths.Count > 0)
            {
                var gravel = new MeshBuilder();
                foreach (var path in a.paths) Ribbon(gravel, path, 2.6f, Lawn + 0.07f);
                Add("Paths " + a.name, gravel, Mats.Lit(new Color(0.86f, 0.8f, 0.66f), 0.05f), false, 2.5f);
            }
            // the neighbourhood's name on the pavement along its front street
            var sign = FlatText(root, a.center + new Vector2(0, CityLayout.Block / 2f - CityLayout.Pavement / 2f), Lawn + 0.08f, a.name, 1.9f, 26f);
            sign.color = new Color(0.25f, 0.27f, 0.3f);
            areaViews[a] = (r, sign);
            foreach (var s in a.slots) BuildSlot(s);
        }

        /// <summary>A flat strip along a line of plan points (a footpath), as thin boxes between the points.</summary>
        private static void Ribbon(MeshBuilder b, Vector2[] pts, float width, float y)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i]; var c = pts[i + 1];
                var mid = (a + c) / 2f;
                float len = (c - a).magnitude + width * 0.5f;
                float yaw = Mathf.Atan2(c.x - a.x, c.y - a.y) * Mathf.Rad2Deg;
                var save = b.Xf;
                b.Xf = Matrix4x4.TRS(new Vector3(mid.x, y, mid.y), Quaternion.Euler(0, yaw, 0), Vector3.one);
                b.Box(Vector3.zero, new Vector3(width, 0.04f, len));
                b.Xf = save;
            }
        }

        /// <summary>Text lying on the ground at a plan point, turned with the street.</summary>
        private TextMeshPro FlatText(Transform parent, Vector2 plan, float y, string text, float size, float width)
        {
            var t = FlatText(parent, Vector3.zero, text, size, width);
            t.transform.localPosition = CityShape.Point(plan, y);
            t.transform.localRotation = Quaternion.Euler(0, CityShape.Angle(plan), 0) * Quaternion.Euler(90f, 180f, 0f);
            return t;
        }

        private static TextMeshPro FlatText(Transform parent, Vector3 localPos, string text, float size, float width)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.rotation = Quaternion.Euler(90f, 180f, 0f); // lying face up, reads upright for the camera looking north
            var t = go.AddComponent<TextMeshPro>();
            LocPreprocessor.Attach(t);
            t.font = Fonts.Bold;
            t.text = text;
            t.fontSize = size * 10f;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(width, size * 1.6f);
            t.enableAutoSizing = true; t.fontSizeMin = 1f; t.fontSizeMax = size * 10f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>
        /// A slot stands level on a little plinth (its top at the ground height under its middle, its foot below the
        /// lowest corner); parking spaces lie on the street's slope instead. Turned with the street.
        /// </summary>
        private void BuildSlot(Slot s)
        {
            var v = new SlotView { slot = s };
            v.go = new GameObject("Slot " + s.id);
            v.go.transform.SetParent(root, false);
            v.go.transform.localPosition = CityShape.Point(s.local, Base(s));
            var turn = Quaternion.Euler(0f, s.yaw + CityShape.Angle(s.local), 0f);
            v.go.transform.localRotation = s.onRoad ? Quaternion.FromToRotation(Vector3.up, CityShape.Normal(s.local)) * turn : turn;
            float drop = s.onRoad || s.only == "bridge" ? 0f : Plinth(s);
            var pad = new MeshBuilder();
            if (s.only == "bridge")
                pad.Box(new Vector3(0, 0.03f, 0), new Vector3(6f, 0.06f, 4f));                       // only a marker on the bridge deck
            else if (s.onRoad)
                pad.Box(new Vector3(0, 0.02f, 0), new Vector3(s.w - 0.4f, 0.04f, s.d - 0.4f));
            else
                pad.Box(new Vector3(0, (0.15f - drop) / 2f, 0), new Vector3(s.w - 0.4f, 0.15f + drop, s.d - 0.4f));
            v.pad = new GameObject("Pad");
            v.pad.transform.SetParent(v.go.transform, false);
            v.pad.AddComponent<MeshFilter>().sharedMesh = pad.ToMesh("pad");
            v.padRenderer = v.pad.AddComponent<MeshRenderer>();
            v.padRenderer.shadowCastingMode = ShadowCastingMode.Off;
            v.label = FlatText(v.go.transform, new Vector3(0, 0.26f, 0), "", Mathf.Min(s.w, s.d, 16f) * 0.2f, Mathf.Min(s.WorldSize.x, 30f) - 1.5f);
            v.hit = v.go.AddComponent<BoxCollider>();
            views[s] = v;
        }

        /// <summary>How far a slot's plinth reaches below its top: down past the lowest ground under its corners.</summary>
        private static float Plinth(Slot s)
        {
            float mid = CityShape.Height(s.local), low = mid;
            var ws = s.WorldSize / 2f;
            foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                low = Mathf.Min(low, CityShape.Height(s.local + new Vector2(x * ws.x, z * ws.y)));
            return mid - low + 0.4f;
        }

        // ---------- state ----------

        private Material PadMat(string kind)
        {
            if (padMats.TryGetValue(kind, out var m)) return m;
            Color c;
            switch (kind)
            {
                case "house": c = new Color(0.86f, 0.72f, 0.92f); break;
                case "big": c = new Color(0.72f, 0.7f, 0.94f); break;
                case "civic": c = new Color(0.62f, 0.8f, 0.94f); break;
                case "historic": c = new Color(0.9f, 0.78f, 0.6f); break;
                case "vehicle": c = new Color(0.42f, 0.45f, 0.5f); break;
                case "tree": c = new Color(0.45f, 0.62f, 0.32f); break;
                case "animal": c = new Color(0.86f, 0.74f, 0.5f); break;
                case "statue": c = new Color(0.88f, 0.86f, 0.8f); break;
                case "landmark": c = new Color(0.96f, 0.84f, 0.45f); break;
                default: c = new Color(0.55f, 0.84f, 0.84f); break;
            }
            return padMats[kind] = Mats.Lit(c, 0.2f);
        }

        /// <summary>Redraws every slot and lawn for the player's level and what stands where.</summary>
        public void RefreshAll()
        {
            int level = Progress.Level;
            foreach (var kv in areaViews)
            {
                var a = kv.Key;
                bool open = level >= a.level;
                kv.Value.lawn.sharedMaterial.SetColor("_BaseColor", open ? a.ground : Color.Lerp(a.ground, new Color(0.6f, 0.6f, 0.6f), 0.75f));
                kv.Value.sign.text = open ? a.name : $"{a.name}  ·  Seviye {a.level}";
            }
            foreach (var v in views.Values) Refresh(v, level);
            people.Sync();
        }

        /// <summary>The model standing on a slot (null when empty).</summary>
        public GameObject ModelOn(Slot s) => views.TryGetValue(s, out var v) ? v.model : null;

        public void Refresh(Slot s) { if (views.TryGetValue(s, out var v)) Refresh(v, Progress.Level); people.Sync(); }

        /// <summary>Height of the bridge model's road deck above its foot (the model's third brick layer), unscaled.</summary>
        private const float BridgeDeck = 9 * Piece.Plate, StudHeight = 0.135f;   // (its studs go just under the asphalt deck)

        private void Refresh(SlotView v, int level)
        {
            var s = v.slot;
            bool open = level >= s.level;
            string id = Progress.At(s.id);
            var m = id != null ? ModelCatalog.Find(id) : null;
            string shown = v.model != null ? v.model.name : null;
            if (m == null && v.model != null) { Object.Destroy(v.model); v.model = null; }
            if (m != null && shown != m.name)
            {
                if (v.model != null) Object.Destroy(v.model);
                v.model = PieceFactory.BuildModel(m);
                v.model.transform.SetParent(v.go.transform, false);
                float k = FitScale(m, s);
                // the big bridge stands in the creek with its deck just under the street's deck; the rest stand on their pads
                v.model.transform.localPosition = new Vector3(0, s.only == "bridge" ? -0.05f - (BridgeDeck + StudHeight) * k : s.onRoad ? 0.04f : 0.15f, 0);
                v.model.transform.localScale = Vector3.one * k;
            }
            v.padRenderer.sharedMaterial = s == selected ? (padSelected ??= Mats.Lit(new Color(1f, 0.86f, 0.2f), 0.3f)) : open ? PadMat(s.kind) : (padLocked ??= Mats.Lit(new Color(0.7f, 0.7f, 0.7f), 0.1f));
            v.pad.SetActive(m == null || s.only != "bridge" || s == selected);
            v.label.gameObject.SetActive(m == null);
            v.label.text = open ? CityLayout.SlotShort(s) : $"Sv {s.level}";
            v.label.color = open ? new Color(0.2f, 0.22f, 0.28f, 0.85f) : new Color(0.35f, 0.35f, 0.38f, 0.9f);
            float h = m != null ? Mathf.Max(2f, m.height * FitScale(m, s)) : 2f;
            v.hit.center = new Vector3(0, h / 2f, 0);
            v.hit.size = new Vector3(s.only == "bridge" ? 30f : s.w, h, s.d);
        }

        /// <summary>Largest scale at which the model fits its slot (a little room left round it).</summary>
        public static float SlotFit(ModelDef m, Slot s)
        {
            float margin = s.kind == "vehicle" ? 0.4f : s.kind == "landmark" ? 0.6f : 1.2f;
            return Mathf.Min((s.w - margin) / Mathf.Max(1, m.sx), (s.d - margin) / Mathf.Max(1, m.sz));
        }

        /// <summary>
        /// The scale a model stands at: its real-world size (ModelDef.scale), shrunk only when the slot is too small.
        /// Trees may spread over the pavement and are never shrunk.
        /// </summary>
        public static float FitScale(ModelDef m, Slot s) => s.kind == "tree" ? m.scale : Mathf.Min(m.scale, SlotFit(m, s));

        private static float Base(Slot s) => s.onRoad || s.only == "bridge" ? RoadTop : Lawn;

        public void Select(Slot s)
        {
            var old = selected;
            selected = s;
            if (old != null) Refresh(old);
            if (s != null) Refresh(s);
        }

        public Slot Hit(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 5000f)) return null;
            foreach (var v in views.Values) if (v.hit == hit.collider) return v.slot;
            return null;
        }

        public Vector3 WorldOf(Slot s) => root.TransformPoint(CityShape.Point(s.local, Base(s)));

        /// <summary>A plan point (relative to the city centre) at height y above the ground, in the world.</summary>
        public Vector3 WorldAt(Vector2 plan, float y) => root.TransformPoint(CityShape.Point(plan, y));

        /// <summary>The plan point under a world position.</summary>
        public static Vector2 PlanOf(Vector3 world) => CityShape.ToPlan(new Vector2(world.x - Center.x, world.z - Center.z));

        // ---------- walking and driving ----------

        /// <summary>Height of the walkable surface: streets, bridges, the raised blocks, the slot pads, the creek's banks, the quay.</summary>
        public float GroundAt(Vector3 world)
        {
            var p = PlanOf(world);
            return Center.y + CityShape.Height(p) + SurfaceAt(p);
        }

        /// <summary>The surface's height above the bare ground at a plan point.</summary>
        private float SurfaceAt(Vector2 p)
        {
            float half = CityLayout.Block / 2f;
            if (CityLayout.OnHomeLot(p)) return Lawn + 0.03f;
            if (p.y > CityLayout.ZOf(0) + half) return Lawn;                                     // the quay
            if (CityLayout.InCreek(p))
            {
                if (CityLayout.OnStreetZ(p.y)) return RoadTop;                                  // a bridge
                float d = Mathf.Abs(p.x - CityLayout.CreekX), w = CityLayout.CreekWater + 1.5f, ch = CityLayout.Creek / 2f;
                return d <= w ? WaterTop : Mathf.Lerp(CreekBed + 0.3f, RoadTop, (d - w) / (ch - w));   // wading, or on the bank
            }
            foreach (var a in CityLayout.Areas)
            {
                if (a.creek || Mathf.Abs(p.x - a.center.x) > half || Mathf.Abs(p.y - a.center.y) > half) continue;
                foreach (var s in a.slots)
                {
                    if (s.onRoad) continue;
                    var ws = s.WorldSize;
                    if (Mathf.Abs(p.x - s.local.x) <= ws.x / 2f - 0.2f && Mathf.Abs(p.y - s.local.y) <= ws.y / 2f - 0.2f)
                        return CityShape.Height(s.local) - CityShape.Height(p) + Lawn + 0.15f;   // a level pad
                }
                return Lawn + 0.03f;
            }
            return RoadTop;
        }

        /// <summary>Is this world point on a street or a bridge (where a car may drive)?</summary>
        public bool IsRoad(Vector3 world)
        {
            var p = PlanOf(world);
            if (p.x < CityLayout.MinX || p.x > CityLayout.MaxX || p.y < CityLayout.MinZ || p.y > CityLayout.ZOf(0) + CityLayout.Block / 2f) return false;
            if (CityLayout.InCreek(p)) return CityLayout.OnStreetZ(p.y);
            return Mathf.Abs(SurfaceAt(p) - RoadTop) < 0.01f;
        }

        /// <summary>A placed model as a box the walker can't enter (world x/z centre and half size, top height).</summary>
        public struct Obstacle
        {
            public Vector2 center, half;
            public float top;
            public float bottom;      // 0 = standing on the ground; else only in the way at this height and above
            public ModelDef model;
        }

        public List<Obstacle> Obstacles()
        {
            var list = new List<Obstacle>();
            foreach (var v in views.Values)
            {
                string id = Progress.At(v.slot.id);
                var m = id != null ? ModelCatalog.Find(id) : null;
                if (m == null) continue;
                var s = v.slot;
                if (s.only == "bridge") continue;              // you walk and drive over the bridge
                float k = FitScale(m, s);
                var size = new Vector2(m.sx, m.sz) * k;
                if (Mathf.Abs(Mathf.Repeat(s.yaw + CityShape.Angle(s.local), 180f) - 90f) < 45f) size = new Vector2(size.y, size.x);
                var at = WorldOf(s);
                list.Add(new Obstacle
                {
                    center = new Vector2(at.x, at.z), half = size / 2f,
                    top = at.y + 0.15f + m.height * k, model = m,
                });
            }
            return list;
        }

        /// <summary>The new model bounces once when it lands.</summary>
        public void Pop(Slot s)
        {
            if (views.TryGetValue(s, out var v) && v.model != null) pops.Add((v.model.transform, v.model.transform.localScale.x, 0f));
        }

        public void Tick(float dt)
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (t, scale, time) = pops[i];
                if (t == null) { pops.RemoveAt(i); continue; }
                time += dt;
                float k = Mathf.Min(1f, time / 0.5f);
                t.localScale = Vector3.one * scale * (1f + 0.3f * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                if (k >= 1f) { t.localScale = Vector3.one * scale; pops.RemoveAt(i); } else pops[i] = (t, scale, time);
            }
        }
    }

    /// <summary>Bird's-eye camera over the city: one finger pans, pinch / wheel zooms (far enough to see the whole town on a phone).</summary>
    public sealed class CityCam
    {
        public Vector3 target;
        public float distance = 120f, pitch = 55f;
        public const float MinDist = 25f, MaxDist = 1000f;

        public void Focus(Vector3 at, float dist) { target = at; distance = Mathf.Clamp(dist, MinDist, MaxDist); }

        public void Pan(Camera cam, Vector2 deltaPx)
        {
            float k = distance * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            // the camera looks north, so screen right is world -x: the ground follows the finger
            target += new Vector3(deltaPx.x * k, 0f, deltaPx.y * k / Mathf.Sin(pitch * Mathf.Deg2Rad) * 0.85f);
            var c = CityMap.Center;
            target.x = Mathf.Clamp(target.x, c.x + CityLayout.MinX - CityLayout.HomeLot - 20f, c.x + CityLayout.MaxX + 20f);
            target.z = Mathf.Clamp(target.z, c.z + CityLayout.MinZ - 20f, c.z + CityLayout.MaxZ + 60f);
        }

        public void Zoom(float factor) => distance = Mathf.Clamp(distance * factor, MinDist, MaxDist);

        public void Apply(Camera cam)
        {
            var rot = Quaternion.Euler(pitch, 180f, 0f); // looking north (-z) from the south
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }
    }
}
