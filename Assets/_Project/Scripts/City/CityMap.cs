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
    /// The city in the scene: roads, pavements, neighbourhood lawns, the slots (coloured pads with their kind written on
    /// them, grey and numbered while locked) and the models standing on them. Units are studs, like the models.
    /// </summary>
    public sealed class CityMap
    {
        public const float Lawn = 0.4f;                      // top of the blocks (pavement) above the road
        public const float RoadTop = 0.1f;
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

        public CityMap(Transform parent)
        {
            root = new GameObject("City").transform;
            root.SetParent(parent, false);
            root.position = Center;
            BuildGround();
            BuildHomeLot();
            foreach (var a in CityLayout.Areas) BuildArea(a);
            people = new Townsfolk(root);
        }

        // ---------- static scenery ----------

        private void Add(string name, Mesh mesh, Material m, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private void BuildGround()
        {
            float x0 = CityLayout.MinX, x1 = CityLayout.MaxX, z0 = CityLayout.MinZ, zShore = CityLayout.ZOf(0) + CityLayout.Block / 2f;
            var asphalt = new MeshBuilder();
            asphalt.Box(new Vector3((x0 + x1) / 2f, RoadTop / 2f, (z0 + zShore) / 2f), new Vector3(x1 - x0, RoadTop, zShore - z0));
            Add("Roads", asphalt.ToMesh("roads"), Mats.Lit(new Color(0.33f, 0.35f, 0.38f), 0.1f));

            var lines = new MeshBuilder();
            float half = CityLayout.Block / 2f + CityLayout.Road / 2f;
            for (int c = 0; c <= CityLayout.Cols; c++)                       // north-south roads: dashed centre lines
            {
                float x = CityLayout.XOf(c) - half;
                for (float t = z0 + 3f; t < zShore - 4f; t += 8f) lines.Box(new Vector3(x, RoadTop + 0.02f, t + 2f), new Vector3(0.5f, 0.04f, 4f));
            }
            for (int r = 0; r < CityLayout.Rows; r++)                        // east-west roads: centre line and the parking-lane lines
            {
                float z = CityLayout.ZOf(r) - half;
                for (float t = x0 + 3f; t < x1 - 4f; t += 8f) lines.Box(new Vector3(t + 2f, RoadTop + 0.02f, z), new Vector3(4f, 0.04f, 0.5f));
                foreach (float side in new[] { -1f, 1f })
                    lines.Box(new Vector3(0, RoadTop + 0.02f, z + side * (CityLayout.Road / 2f - CityLayout.ParkingLane)), new Vector3(x1 - x0, 0.04f, 0.25f));
            }
            Add("RoadLines", lines.ToMesh("lines"), Mats.Lit(new Color(0.95f, 0.93f, 0.85f), 0.1f));

            // the stone quay along the lake and its railing
            var quay = new MeshBuilder();
            quay.Box(new Vector3(0, Lawn / 2f, zShore + CityLayout.Quay / 2f), new Vector3(x1 - x0, Lawn, CityLayout.Quay));
            Add("Quay", quay.ToMesh("quay"), Mats.Lit(new Color(0.72f, 0.7f, 0.66f), 0.1f));
            var rail = new MeshBuilder();
            float zr = CityLayout.MaxZ - 0.3f;
            for (float x = x0 + 1f; x < x1; x += 4f) rail.Box(new Vector3(x, Lawn + 0.5f, zr), new Vector3(0.25f, 1f, 0.25f));
            rail.Box(new Vector3(0, Lawn + 1.0f, zr), new Vector3(x1 - x0, 0.15f, 0.2f));
            Add("Railing", rail.ToMesh("rail"), Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.4f), true);
        }

        /// <summary>The player's lot: a raised block with a pavement, its name on the pavement by the road.</summary>
        private void BuildHomeLot()
        {
            var c = CityLayout.HomeLotCenter;
            var kerb = new MeshBuilder();
            kerb.Box(new Vector3(c.x, Lawn / 2f, c.y), new Vector3(CityLayout.HomeLot, Lawn, CityLayout.HomeLot));
            Add("Pavement Home", kerb.ToMesh("pavement"), Mats.Lit(new Color(0.8f, 0.79f, 0.76f), 0.1f));
            var lawn = new MeshBuilder();
            lawn.Box(new Vector3(c.x, Lawn + 0.03f, c.y), new Vector3(CityLayout.HomeLot - 2f * CityLayout.Pavement, 0.06f, CityLayout.HomeLot - 2f * CityLayout.Pavement));
            Add("Lawn Home", lawn.ToMesh("lawn"), Mats.Lit(new Color(0.58f, 0.8f, 0.5f), 0.05f));
            var sign = FlatText(root, new Vector3(c.x, Lawn + 0.08f, c.y + CityLayout.HomeLot / 2f - CityLayout.Pavement / 2f), "Evim", 1.9f, 20f);
            sign.color = new Color(0.25f, 0.27f, 0.3f);
        }

        private void BuildArea(Area a)
        {
            var kerb = new MeshBuilder();
            kerb.Box(new Vector3(a.center.x, Lawn / 2f, a.center.y), new Vector3(CityLayout.Block, Lawn, CityLayout.Block));
            Add("Pavement " + a.name, kerb.ToMesh("pavement"), Mats.Lit(new Color(0.8f, 0.79f, 0.76f), 0.1f));
            var lawn = new MeshBuilder();
            float inner = CityLayout.Block - 2f * CityLayout.Pavement;   // a pavement all round the block
            lawn.Box(new Vector3(a.center.x, Lawn + 0.03f, a.center.y), new Vector3(inner, 0.06f, inner));
            var go = new GameObject("Lawn " + a.name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = lawn.ToMesh("lawn");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mats.Lit(a.ground, 0.05f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            // the neighbourhood's name on the pavement along its front street
            var sign = FlatText(root, new Vector3(a.center.x, Lawn + 0.08f, a.center.y + CityLayout.Block / 2f - CityLayout.Pavement / 2f), a.name, 1.9f, 26f);
            sign.color = new Color(0.25f, 0.27f, 0.3f);
            areaViews[a] = (r, sign);
            foreach (var s in a.slots) BuildSlot(s);
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

        private void BuildSlot(Slot s)
        {
            var v = new SlotView { slot = s };
            v.go = new GameObject("Slot " + s.id);
            v.go.transform.SetParent(root, false);
            v.go.transform.localPosition = new Vector3(s.local.x, Base(s), s.local.y);
            v.go.transform.localRotation = Quaternion.Euler(0f, s.yaw, 0f);
            var pad = new MeshBuilder();
            pad.Box(new Vector3(0, s.onRoad ? 0.02f : 0.075f, 0), new Vector3(s.w - 0.4f, s.onRoad ? 0.04f : 0.15f, s.d - 0.4f));
            v.pad = new GameObject("Pad");
            v.pad.transform.SetParent(v.go.transform, false);
            v.pad.AddComponent<MeshFilter>().sharedMesh = pad.ToMesh("pad");
            v.padRenderer = v.pad.AddComponent<MeshRenderer>();
            v.padRenderer.shadowCastingMode = ShadowCastingMode.Off;
            v.label = FlatText(v.go.transform, new Vector3(0, 0.26f, 0), "", Mathf.Min(s.w, s.d) * 0.2f, s.WorldSize.x - 1.5f);
            v.hit = v.go.AddComponent<BoxCollider>();
            views[s] = v;
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
                v.model.transform.localPosition = new Vector3(0, s.onRoad ? 0.04f : 0.15f, 0);
                v.model.transform.localScale = Vector3.one * FitScale(m, s);
            }
            v.padRenderer.sharedMaterial = s == selected ? (padSelected ??= Mats.Lit(new Color(1f, 0.86f, 0.2f), 0.3f)) : open ? PadMat(s.kind) : (padLocked ??= Mats.Lit(new Color(0.7f, 0.7f, 0.7f), 0.1f));
            v.label.gameObject.SetActive(m == null);
            v.label.text = open ? CityLayout.KindShort(s.kind) : $"Sv {s.level}";
            v.label.color = open ? new Color(0.2f, 0.22f, 0.28f, 0.85f) : new Color(0.35f, 0.35f, 0.38f, 0.9f);
            float h = m != null ? Mathf.Max(2f, m.height * FitScale(m, s)) : 2f;
            v.hit.center = new Vector3(0, h / 2f, 0);
            v.hit.size = new Vector3(s.w, h, s.d);
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

        private static float Base(Slot s) => s.onRoad ? RoadTop : Lawn;

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

        public Vector3 WorldOf(Slot s) => root.TransformPoint(new Vector3(s.local.x, Base(s), s.local.y));

        // ---------- walking ----------

        /// <summary>Height of the walkable surface: road, the raised blocks and the quay, the slot pads on them.</summary>
        public float GroundAt(Vector3 world)
        {
            var l = world - Center;
            float y = RoadTop, half = CityLayout.Block / 2f;
            if (CityLayout.OnHomeLot(new Vector2(l.x, l.z))) return Center.y + Lawn + 0.03f;
            if (l.z > CityLayout.ZOf(0) + half) return Center.y + Lawn;           // the quay
            foreach (var a in CityLayout.Areas)
            {
                if (Mathf.Abs(l.x - a.center.x) > half || Mathf.Abs(l.z - a.center.y) > half) continue;
                y = Lawn + 0.03f;
                foreach (var s in a.slots)
                {
                    if (s.onRoad) continue;
                    var ws = s.WorldSize;
                    if (Mathf.Abs(l.x - s.local.x) <= ws.x / 2f - 0.2f && Mathf.Abs(l.z - s.local.y) <= ws.y / 2f - 0.2f) { y = Lawn + 0.15f; break; }
                }
                break;
            }
            return Center.y + y;
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
                float k = FitScale(m, s);
                var size = new Vector2(m.sx, m.sz) * k;
                if (Mathf.Abs(Mathf.Repeat(s.yaw, 180f) - 90f) < 1f) size = new Vector2(size.y, size.x);
                list.Add(new Obstacle
                {
                    center = new Vector2(Center.x + s.local.x, Center.z + s.local.y), half = size / 2f,
                    top = Center.y + Base(s) + 0.15f + m.height * k, model = m,
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

    /// <summary>Bird's-eye camera over the city: one finger pans, pinch / wheel zooms.</summary>
    public sealed class CityCam
    {
        public Vector3 target;
        public float distance = 120f, pitch = 55f;
        public const float MinDist = 25f, MaxDist = 420f;

        public void Focus(Vector3 at, float dist) { target = at; distance = Mathf.Clamp(dist, MinDist, MaxDist); }

        public void Pan(Camera cam, Vector2 deltaPx)
        {
            float k = distance * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            // the camera looks north, so screen right is world -x: the ground follows the finger
            target += new Vector3(deltaPx.x * k, 0f, deltaPx.y * k / Mathf.Sin(pitch * Mathf.Deg2Rad) * 0.85f);
            var c = CityMap.Center;
            target.x = Mathf.Clamp(target.x, c.x + CityLayout.MinX - CityLayout.HomeLot, c.x + CityLayout.MaxX);
            target.z = Mathf.Clamp(target.z, c.z + CityLayout.MinZ, c.z + CityLayout.MaxZ + 40f);
        }

        public void Zoom(float factor) => distance = Mathf.Clamp(distance * factor, MinDist, MaxDist);

        public void Apply(Camera cam)
        {
            var rot = Quaternion.Euler(pitch, 180f, 0f); // looking north (-z) from the south
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }
    }
}
