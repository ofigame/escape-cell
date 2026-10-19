using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Build;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrickMuseum.Core
{
    /// <summary>
    /// The village's farm in the city: eight fields of furrowed soil in two field blocks (each with 36 plants that go
    /// from seed to sprout to young plant to ripe as the countdown runs), and in the farmyard a red barn with cows and a
    /// coop with hens. Tapping a field or a building opens its panel: buy, sow (a list of crops), the countdown, the ad
    /// that hurries it, harvest / collect for lira.
    /// </summary>
    public sealed partial class Game
    {
        private sealed class FieldView
        {
            public int index;
            public Transform root;
            public BoxCollider hit;
            public MeshFilter[] plants;
            public MeshRenderer[] plantR;
            public TMPro.TextMeshPro sign;
            public string shown;
        }

        private sealed class BarnView
        {
            public string id;
            public Transform root, animals;
            public BoxCollider hit;
            public TMPro.TextMeshPro sign;
            public List<(Transform t, float r, float speed, float phase)> walkers = new List<(Transform, float, float, float)>();
        }

        private readonly List<FieldView> fieldViews = new List<FieldView>();
        private readonly List<BarnView> barnViews = new List<BarnView>();
        private readonly Dictionary<string, Mesh> plantMeshes = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, Material> plantMats = new Dictionary<string, Material>();
        private int fieldPanel = -1;
        private string barnPanel;
        private float fieldsTick;
        private const float FieldSize = 22f;

        // ---------- building the village's farm ----------

        private void BuildFields()
        {
            int i = 0;
            foreach (var a in CityLayout.Areas.Where(x => x.fields))
                foreach (var off in new[] { new Vector2(-14, 14), new Vector2(14, 14), new Vector2(-14, -14), new Vector2(14, -14) })
                    fieldViews.Add(BuildField(i++, a.center + off));
            var yard = CityLayout.Areas.FirstOrDefault(x => x.farmyard);
            if (yard != null)
            {
                barnViews.Add(BuildBarn("ahir", yard.center + new Vector2(-10, 4)));
                barnViews.Add(BuildBarn("kumes", yard.center + new Vector2(17, -12)));
            }
            RefreshFields(true);
        }

        private Transform Spot(string name, Vector2 plan, float y)
        {
            var t = new GameObject(name).transform;
            t.SetParent(city.root, false);
            t.localPosition = CityShape.Point(plan, y);
            t.localRotation = Quaternion.Euler(0, CityShape.Angle(plan), 0);
            return t;
        }

        private static void Mesh(Transform parent, string name, MeshBuilder b, Material m, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private FieldView BuildField(int index, Vector2 plan)
        {
            var v = new FieldView { index = index, root = Spot("Field " + index, plan, CityMap.Lawn) };
            // dark soil with six ridges, a low wooden fence round it with a gap at the front
            var soil = new MeshBuilder();
            soil.Box(new Vector3(0, 0.08f, 0), new Vector3(FieldSize, 0.16f, FieldSize));
            var ridges = new MeshBuilder();
            for (int k = 0; k < 6; k++) ridges.Box(new Vector3(-FieldSize / 2f + (k + 0.5f) * FieldSize / 6f, 0.25f, 0), new Vector3(1.8f, 0.2f, FieldSize - 2f));
            Mesh(v.root, "Soil", soil, Mats.Lit(new Color(0.36f, 0.24f, 0.15f), 0.05f), false);
            Mesh(v.root, "Ridges", ridges, Mats.Lit(new Color(0.44f, 0.3f, 0.19f), 0.05f), false);
            var fence = new MeshBuilder();
            float h = FieldSize / 2f + 0.6f;
            for (float t = -h; t <= h; t += 2.2f)
            {
                fence.Box(new Vector3(t, 0.5f, -h), new Vector3(0.2f, 1f, 0.2f));
                fence.Box(new Vector3(-h, 0.5f, t), new Vector3(0.2f, 1f, 0.2f));
                fence.Box(new Vector3(h, 0.5f, t), new Vector3(0.2f, 1f, 0.2f));
                if (Mathf.Abs(t) > 2.5f) fence.Box(new Vector3(t, 0.5f, h), new Vector3(0.2f, 1f, 0.2f));
            }
            fence.Box(new Vector3(0, 0.8f, -h), new Vector3(2 * h, 0.12f, 0.12f));
            fence.Box(new Vector3(-h, 0.8f, 0), new Vector3(0.12f, 0.12f, 2 * h));
            fence.Box(new Vector3(h, 0.8f, 0), new Vector3(0.12f, 0.12f, 2 * h));
            fence.Box(new Vector3(-h / 2f - 1.25f, 0.8f, h), new Vector3(h - 2.5f, 0.12f, 0.12f));
            fence.Box(new Vector3(h / 2f + 1.25f, 0.8f, h), new Vector3(h - 2.5f, 0.12f, 0.12f));
            Mesh(v.root, "Fence", fence, Mats.Lit(new Color(0.62f, 0.45f, 0.28f), 0.1f));
            // 6 × 6 plants on the ridges
            v.plants = new MeshFilter[36];
            v.plantR = new MeshRenderer[36];
            for (int k = 0; k < 36; k++)
            {
                var go = new GameObject("Plant");
                go.transform.SetParent(v.root, false);
                go.transform.localPosition = new Vector3(-FieldSize / 2f + (k % 6 + 0.5f) * FieldSize / 6f, 0.34f, -FieldSize / 2f + (k / 6 + 0.5f) * FieldSize / 6f);
                go.transform.localRotation = Quaternion.Euler(0, (k * 47) % 360, 0);
                v.plants[k] = go.AddComponent<MeshFilter>();
                v.plantR[k] = go.AddComponent<MeshRenderer>();
            }
            // a sign at the gate
            v.sign = Board(v.root, new Vector3(4.2f, 0, h + 0.4f), 0f);
            v.hit = v.root.gameObject.AddComponent<BoxCollider>();
            v.hit.center = new Vector3(0, 1f, 0);
            v.hit.size = new Vector3(FieldSize + 1.5f, 2f, FieldSize + 1.5f);
            return v;
        }

        /// <summary>A small white board on a post with a line of text (both faces).</summary>
        private TMPro.TextMeshPro Board(Transform parent, Vector3 at, float yaw)
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0, 0.7f, 0), new Vector3(0.18f, 1.4f, 0.18f));
            b.Box(new Vector3(0, 1.55f, 0), new Vector3(3.6f, 0.9f, 0.12f));
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            Mesh(go.transform, "Board", b, Mats.Lit(new Color(0.96f, 0.94f, 0.88f), 0.2f));
            TMPro.TextMeshPro first = null;
            foreach (float face in new[] { 0f, 180f })
            {
                var t = new GameObject("Text").AddComponent<TMPro.TextMeshPro>();
                t.transform.SetParent(go.transform, false);
                t.transform.localPosition = Quaternion.Euler(0, face, 0) * new Vector3(0, 1.55f, 0.08f);
                t.transform.localRotation = Quaternion.Euler(0, 180f + face, 0);
                LocPreprocessor.Attach(t);
                t.font = Fonts.Bold;
                t.alignment = TMPro.TextAlignmentOptions.Center;
                t.rectTransform.sizeDelta = new Vector2(3.4f, 0.8f);
                t.enableAutoSizing = true; t.fontSizeMin = 0.5f; t.fontSizeMax = 4f;
                t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                t.color = new Color(0.25f, 0.2f, 0.15f);
                if (first == null) first = t; else first.gameObject.AddComponent<TwinText>().twin = t;
            }
            return first;
        }

        private BarnView BuildBarn(string id, Vector2 plan)
        {
            var v = new BarnView { id = id, root = Spot("Barn " + id, plan, CityMap.Lawn) };
            bool barn = id == "ahir";
            float w = barn ? 16f : 7f, d = barn ? 11f : 5f, wall = barn ? 6f : 2.6f;
            var walls = new MeshBuilder();
            walls.Box(new Vector3(0, wall / 2f, 0), new Vector3(w, wall, d));
            Mesh(v.root, "Walls", walls, Mats.Lit(barn ? new Color(0.72f, 0.16f, 0.12f) : new Color(0.78f, 0.6f, 0.36f), 0.1f));
            // a pitched roof: two slanted slabs
            var roof = new MeshBuilder();
            float half = d / 2f + 0.6f, rise = barn ? 3.4f : 1.6f, ang = Mathf.Atan2(rise, half) * Mathf.Rad2Deg, len = Mathf.Sqrt(half * half + rise * rise);
            foreach (float side in new[] { -1f, 1f })
            {
                roof.Xf = Matrix4x4.TRS(new Vector3(0, wall + rise / 2f, side * half / 2f), Quaternion.Euler(side * ang, 0, 0), Vector3.one);
                roof.Box(Vector3.zero, new Vector3(w + 1f, 0.3f, len));
            }
            roof.Xf = Matrix4x4.identity;
            Mesh(v.root, "Roof", roof, Mats.Lit(barn ? new Color(0.25f, 0.25f, 0.28f) : new Color(0.55f, 0.3f, 0.18f), 0.1f));
            // white trim and a big door at the front (+z)
            var trim = new MeshBuilder();
            trim.Box(new Vector3(0, wall * 0.42f, d / 2f + 0.06f), new Vector3(barn ? 4.4f : 1.6f, wall * 0.84f, 0.12f));
            if (barn)
            {
                trim.Box(new Vector3(0, wall * 0.42f, d / 2f + 0.14f), new Vector3(4.4f, 0.25f, 0.1f));
                trim.Box(new Vector3(0, wall * 0.42f, d / 2f + 0.14f), new Vector3(0.25f, wall * 0.84f, 0.1f));
            }
            Mesh(v.root, "Door", trim, Mats.Lit(barn ? Color.white : new Color(0.45f, 0.3f, 0.2f), 0.2f));
            // the paddock in front with the animals
            v.animals = new GameObject("Animals").transform;
            v.animals.SetParent(v.root, false);
            v.animals.localPosition = new Vector3(0, 0, d / 2f + (barn ? 6f : 3.5f));
            var pen = new MeshBuilder();
            float pr = barn ? 8f : 4f, pd = barn ? 4.5f : 2.5f;
            for (float t = -pr; t <= pr; t += 2f) { pen.Box(new Vector3(t, 0.45f, pd), new Vector3(0.18f, 0.9f, 0.18f)); }
            for (float t = -pd; t <= pd; t += 2f) { pen.Box(new Vector3(-pr, 0.45f, t), new Vector3(0.18f, 0.9f, 0.18f)); pen.Box(new Vector3(pr, 0.45f, t), new Vector3(0.18f, 0.9f, 0.18f)); }
            pen.Box(new Vector3(0, 0.75f, pd), new Vector3(2 * pr, 0.12f, 0.12f));
            pen.Box(new Vector3(-pr, 0.75f, 0), new Vector3(0.12f, 0.12f, 2 * pd));
            pen.Box(new Vector3(pr, 0.75f, 0), new Vector3(0.12f, 0.12f, 2 * pd));
            Mesh(v.animals, "Pen", pen, Mats.Lit(new Color(0.95f, 0.95f, 0.92f), 0.2f));
            var rnd = new System.Random(id.Length * 7);
            int n = barn ? 4 : 7;
            for (int k = 0; k < n; k++)
            {
                var a = new GameObject(barn ? "Cow" : "Hen").transform;
                a.SetParent(v.animals, false);
                Mesh(a, "Body", barn ? CowMesh() : HenMesh(), barn ? Mats.Lit(Color.white, 0.2f) : Mats.Lit(new Color(0.98f, 0.97f, 0.93f), 0.2f));
                Mesh(a, "Marks", barn ? CowSpots() : HenComb(), barn ? Mats.Lit(new Color(0.12f, 0.12f, 0.12f), 0.2f) : Mats.Lit(new Color(0.9f, 0.15f, 0.1f), 0.2f));
                v.walkers.Add((a, (float)(0.6 + rnd.NextDouble()) * (barn ? 4.5f : 2f), (float)(0.15 + rnd.NextDouble() * 0.2) * (rnd.Next(2) == 0 ? 1 : -1), (float)rnd.NextDouble() * 10f));
            }
            v.sign = Board(v.root, new Vector3(w / 2f + 1.5f, 0, d / 2f + 1f), 0f);
            v.hit = v.root.gameObject.AddComponent<BoxCollider>();
            v.hit.center = new Vector3(0, wall / 2f, 2f);
            v.hit.size = new Vector3(w + 4f, wall + rise, d + 10f);
            return v;
        }

        private static MeshBuilder CowMesh()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0, 1.1f, 0), new Vector3(0.9f, 0.8f, 1.8f));          // body
            b.Box(new Vector3(0, 1.4f, 1.15f), new Vector3(0.55f, 0.55f, 0.6f));    // head
            foreach (var (x, z) in new[] { (-0.3f, -0.65f), (0.3f, -0.65f), (-0.3f, 0.65f), (0.3f, 0.65f) }) b.Box(new Vector3(x, 0.35f, z), new Vector3(0.2f, 0.7f, 0.2f));
            return b;
        }

        private static MeshBuilder CowSpots()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0.46f, 1.2f, -0.2f), new Vector3(0.04f, 0.4f, 0.5f));
            b.Box(new Vector3(-0.46f, 1.0f, 0.3f), new Vector3(0.04f, 0.35f, 0.6f));
            b.Box(new Vector3(0, 1.51f, -0.4f), new Vector3(0.5f, 0.04f, 0.5f));
            b.Box(new Vector3(0, 1.45f, 1.46f), new Vector3(0.4f, 0.2f, 0.04f));      // the muzzle
            return b;
        }

        private static MeshBuilder HenMesh()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0, 0.35f, 0), new Vector3(0.35f, 0.35f, 0.5f));
            b.Box(new Vector3(0, 0.62f, 0.2f), new Vector3(0.22f, 0.25f, 0.22f));
            b.Box(new Vector3(0, 0.1f, 0), new Vector3(0.08f, 0.2f, 0.08f));
            return b;
        }

        private static MeshBuilder HenComb()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0, 0.8f, 0.2f), new Vector3(0.06f, 0.12f, 0.16f));
            b.Box(new Vector3(0, 0.58f, 0.35f), new Vector3(0.08f, 0.06f, 0.1f));   // the beak (red here, it reads)
            return b;
        }

        // ---------- the plants: one mesh per crop and stage ----------

        private Mesh PlantMesh(string crop, int stage)
        {
            string key = crop + stage;
            if (plantMeshes.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            switch (stage)
            {
                case 1: b.Dome(Vector3.zero, 0.35f, 0.15f, 8); break;                                                    // a mound with the seed
                case 2: b.Cylinder(Vector3.zero, 0.22f, 0f, 0.55f, 5, false, true); b.Box(new Vector3(0, 0.4f, 0), new Vector3(0.5f, 0.05f, 0.14f)); break; // a sprout, two leaves
                case 3:
                    if (crop == "misir" || crop == "aycicegi") { b.Box(new Vector3(0, 0.8f, 0), new Vector3(0.14f, 1.6f, 0.14f)); b.Box(new Vector3(0, 0.9f, 0), new Vector3(0.9f, 0.06f, 0.2f)); }
                    else if (crop == "bugday") for (int i = 0; i < 5; i++) b.Box(new Vector3((i - 2) * 0.16f, 0.45f, (i % 2) * 0.14f), new Vector3(0.07f, 0.9f, 0.07f));
                    else b.Dome(Vector3.zero, 0.6f, 0.6f, 8);
                    break;
                default:
                    if (crop == "misir") { b.Box(new Vector3(0, 1.1f, 0), new Vector3(0.16f, 2.2f, 0.16f)); b.Box(new Vector3(0, 1.0f, 0), new Vector3(1.1f, 0.07f, 0.25f)); b.Box(new Vector3(0, 1.6f, 0), new Vector3(0.25f, 0.07f, 1.0f)); }
                    else if (crop == "aycicegi") { b.Box(new Vector3(0, 1.1f, 0), new Vector3(0.14f, 2.2f, 0.14f)); b.Box(new Vector3(0, 1.2f, 0), new Vector3(0.9f, 0.06f, 0.2f)); }
                    else if (crop == "bugday") for (int i = 0; i < 7; i++) b.Box(new Vector3((i - 3) * 0.14f, 0.6f, (i % 2) * 0.14f), new Vector3(0.07f, 1.2f, 0.07f));
                    else b.Dome(Vector3.zero, crop == "cilek" ? 0.55f : 0.7f, crop == "cilek" ? 0.4f : 0.8f, 8);
                    break;
            }
            return plantMeshes[key] = b.ToMesh(key);
        }

        /// <summary>The fruit on a ripe plant (corn cobs, a sunflower head, tomatoes, strawberries, golden ears).</summary>
        private Mesh FruitMesh(string crop)
        {
            string key = crop + "fruit";
            if (plantMeshes.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            switch (crop)
            {
                case "misir": b.Box(new Vector3(0.18f, 1.3f, 0), new Vector3(0.22f, 0.6f, 0.22f)); b.Box(new Vector3(-0.18f, 1.7f, 0), new Vector3(0.2f, 0.5f, 0.2f)); break;
                case "aycicegi": b.Cylinder(new Vector3(0, 2.2f, -0.05f), 0.7f, 0.7f, 0.12f, 12); break;
                case "domates": foreach (var p in new[] { new Vector3(0.45f, 0.5f, 0.2f), new Vector3(-0.4f, 0.6f, -0.2f), new Vector3(0.1f, 0.75f, -0.45f), new Vector3(-0.2f, 0.3f, 0.5f) }) b.Dome(p, 0.18f, 0.3f, 6); break;
                case "cilek": foreach (var p in new[] { new Vector3(0.35f, 0.2f, 0.2f), new Vector3(-0.3f, 0.25f, -0.2f), new Vector3(0.05f, 0.35f, -0.3f) }) b.Dome(p, 0.13f, 0.22f, 6); break;
                default: for (int i = 0; i < 7; i++) b.Box(new Vector3((i - 3) * 0.14f, 1.25f, (i % 2) * 0.14f), new Vector3(0.12f, 0.3f, 0.12f)); break;
            }
            return plantMeshes[key] = b.ToMesh(key);
        }

        private Material PlantMat(string key, Color c)
        {
            if (!plantMats.TryGetValue(key, out var m)) plantMats[key] = m = Mats.Lit(c, 0.15f);
            return m;
        }

        /// <summary>Redraws what grows on each field (only when its crop or stage changes) and the boards.</summary>
        private void RefreshFields(bool force = false)
        {
            bool open = District.IsOpen;
            foreach (var v in fieldViews)
            {
                var f = Farming.Field(v.index);
                int stage = Farming.Stage(f);
                string want = $"{open}{f.owned}{f.crop}{stage}";
                v.sign.text = !open ? "İmara kapalı" : !f.owned ? $"SATILIK · {Farming.FieldPrice(v.index)} lira" : f.crop == "" ? "Boş tarla" :
                    stage == 4 ? $"{Farming.CropOf(f.crop)?.name} · hasat!" : $"{Farming.CropOf(f.crop)?.name} · {Progress.LockText(Farming.Left(f))}";
                if (want == v.shown && !force) continue;
                v.shown = want;
                var crop = Farming.CropOf(f.crop);
                for (int k = 0; k < 36; k++)
                {
                    var go = v.plants[k].gameObject;
                    // the fruit is a second child, made the first time it is needed
                    var fruit = go.transform.childCount > 0 ? go.transform.GetChild(0).gameObject : null;
                    if (crop == null || stage == 0) { go.SetActive(false); continue; }
                    go.SetActive(true);
                    v.plants[k].sharedMesh = PlantMesh(crop.id, stage);
                    Color leaf = stage == 1 ? new Color(0.3f, 0.2f, 0.12f) : crop.id == "bugday" && stage == 4 ? crop.ripe : new Color(0.3f, 0.66f, 0.24f);
                    v.plantR[k].sharedMaterial = PlantMat($"leaf{leaf}", leaf);
                    if (stage == 4)
                    {
                        if (fruit == null)
                        {
                            fruit = new GameObject("Fruit");
                            fruit.transform.SetParent(go.transform, false);
                            fruit.AddComponent<MeshFilter>();
                            fruit.AddComponent<MeshRenderer>();
                        }
                        fruit.SetActive(true);
                        fruit.GetComponent<MeshFilter>().sharedMesh = FruitMesh(crop.id);
                        fruit.GetComponent<MeshRenderer>().sharedMaterial = PlantMat("fruit" + crop.id, crop.id == "bugday" ? new Color(0.85f, 0.66f, 0.22f) : crop.ripe);
                    }
                    else if (fruit != null) fruit.SetActive(false);
                }
            }
            foreach (var v in barnViews)
            {
                var def = Farming.BarnOf(v.id);
                var b = Farming.BarnSave(v.id);
                v.sign.text = !open ? "İmara kapalı" : !b.owned ? $"{def.name} · {def.price} lira" : Farming.BarnReady(v.id) ? $"{def.product} hazır!" : $"{def.product} · {Progress.LockText(Farming.BarnLeft(v.id))}";
                v.animals.gameObject.SetActive(open && b.owned);
            }
        }

        /// <summary>Every frame in the city: the plants grow a little within their stage and sway; animals wander.</summary>
        private void FieldsTick(float dt)
        {
            if (fieldViews.Count == 0) return;
            foreach (var v in fieldViews)
            {
                var f = Farming.Field(v.index);
                if (f.crop == "") continue;
                int stage = Farming.Stage(f);
                float g = Farming.Grown(f);
                float within = stage == 1 ? g / 0.15f : stage == 2 ? (g - 0.15f) / 0.3f : stage == 3 ? (g - 0.45f) / 0.55f : 1f;
                float s = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(within));
                for (int k = 0; k < 36; k++)
                {
                    var t = v.plants[k].transform;
                    if (!t.gameObject.activeSelf) continue;
                    t.localScale = Vector3.one * s;
                    t.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.3f + k) * (stage >= 3 ? 3f : 0f), (k * 47) % 360, 0);
                }
            }
            foreach (var v in barnViews)
            {
                if (!v.animals.gameObject.activeSelf) continue;
                foreach (var (t, r, speed, phase) in v.walkers)
                {
                    float a = phase + time * speed;
                    t.localPosition = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r * 0.45f);
                    t.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + (speed > 0 ? 0f : 180f), 0);
                }
            }
            fieldsTick -= dt;
            if (fieldsTick > 0f) return;
            fieldsTick = 1f;
            RefreshFields();
            int ripe = District.IsOpen ? Farming.RipeCount + Farming.Barns.Count(b => Farming.BarnReady(b.id)) : 0;
            cu.harvestBtn.gameObject.SetActive(ripe > 0 && !cu.panel.activeSelf && !cu.list.activeSelf);
            if (ripe > 0) cu.harvestBtn.SetLabel($"Hasat hazır ({ripe})");
            if (cu.panel.activeSelf && fieldPanel >= 0 && cu.panelTitle.text == FieldTitle(fieldPanel)) ShowField(fieldPanel);
            else if (cu.panel.activeSelf && barnPanel != null && cu.panelTitle.text == Farming.BarnOf(barnPanel).name) ShowBarn(barnPanel);
        }

        // ---------- tapping ----------

        /// <summary>A field or a farm building under the tap: opens its panel (true).</summary>
        private bool FarmTap(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 5000f)) return false;
            var fv = fieldViews.FirstOrDefault(v => v.hit == hit.collider);
            if (fv != null) { OpenField(fv.index); return true; }
            var bv = barnViews.FirstOrDefault(v => v.hit == hit.collider);
            if (bv != null) { OpenBarn(bv.id); return true; }
            return false;
        }

        private static string FieldTitle(int i) => $"Tarla {i + 1}";

        private void OpenField(int i)
        {
            if (!District.IsOpen) { ShowDistrict(); return; }
            panelSlot = null; city.Select(null);
            Sfx.Play("pick", 0.6f);
            cu.help.transform.parent.gameObject.SetActive(false);
            barnPanel = null;
            fieldPanel = i;
            ShowField(i);
        }

        private void ShowField(int i)
        {
            var f = Farming.Field(i);
            var crop = Farming.CropOf(f.crop);
            if (!f.owned)
                cu.ShowPanel(FieldTitle(i), $"Satılık tarla · {Farming.FieldPrice(i)} lira\nParan: {FreeBuild.Wallet.Money} lira", null,
                    ($"Satın al · {Farming.FieldPrice(i)} lira", Ui.Yellow, () => BuyField(i)), ("Kapat", Neutral, ClosePanel));
            else if (crop == null)
                cu.ShowPanel(FieldTitle(i), "Boş tarla\nTohum ek, büyüyünce hasat et ve sat", null,
                    ("Tohum ek", Ui.Yellow, () => ChooseCrop(i)), ("Kapat", Neutral, ClosePanel));
            else if (Farming.Ripe(f))
                cu.ShowPanel(FieldTitle(i), $"{crop.name} olgunlaştı!\nHasat et: +{crop.sell} lira", null,
                    ("Hasat et", Ui.Yellow, () => HarvestField(i)), ("Kapat", Neutral, ClosePanel));
            else
                cu.ShowPanel(FieldTitle(i), $"{crop.name} · {Farming.StageName(Farming.Stage(f))}\nHasat: {Progress.LockText(Farming.Left(f))} sonra\nSatış: {crop.sell} lira", null,
                    ("Reklam izle: hemen hazır", Ui.Yellow, () => StartCoroutine(HurryField(i))), ("Kapat", Neutral, ClosePanel));
        }

        private void BuyField(int i)
        {
            if (FreeBuild.Wallet.Money < Farming.FieldPrice(i)) { Toast("Yeterli para yok"); return; }
            if (!Farming.BuyField(i)) return;
            Sfx.Play("win", 0.6f);
            RefreshFields(true);
            ShowField(i);
        }

        /// <summary>The crops to sow: seed price, time, what the harvest fetches.</summary>
        private void ChooseCrop(int i)
        {
            ClosePanel();
            cu.ClearRows();
            cu.listTitle.text = "Ne ekelim?";
            foreach (var c in Farming.Crops)
            {
                var crop = c;
                string time = c.minutes >= 60 ? $"{c.minutes / 60} saat" : $"{c.minutes} dakika";
                cu.AddRow(CropPicture(c), c.name, $"Tohum {c.seed} lira · {time}\nHasat {c.sell} lira", "Ek", () => Sow(i, crop));
            }
            cu.list.SetActive(true);
        }

        private readonly Dictionary<string, Texture2D> cropPics = new Dictionary<string, Texture2D>();

        /// <summary>A small round picture in the crop's ripe colour on green (for the list).</summary>
        private Texture2D CropPicture(Crop c)
        {
            if (cropPics.TryGetValue(c.id, out var t)) return t;
            const int s = 64;
            t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f));
                    Color col = d < 20f ? c.ripe : d < 30f ? new Color(0.36f, 0.66f, 0.28f) : new Color(0.93f, 0.95f, 0.9f);
                    px[y * s + x] = col;
                }
            t.SetPixels32(px);
            t.Apply();
            return cropPics[c.id] = t;
        }

        private void Sow(int i, Crop c)
        {
            if (FreeBuild.Wallet.Money < c.seed) { Toast("Yeterli para yok"); return; }
            if (!Farming.Sow(i, c)) return;
            cu.list.SetActive(false);
            Sfx.Play("place", 0.7f);
            RefreshFields(true);
            Toast($"{c.name} ekildi · hasat {Progress.LockText(Farming.Left(Farming.Field(i)))} sonra", 2.2f);
            OpenField(i);
        }

        private IEnumerator HurryField(int i)
        {
            ClosePanel();
            yield return TestAd();
            Farming.HurryField(i);
            RefreshFields(true);
            OpenField(i);
        }

        private void HarvestField(int i)
        {
            string name = Farming.CropOf(Farming.Field(i).crop)?.name;
            int got = Farming.Harvest(i);
            if (got <= 0) return;
            Sfx.Play("win");
            RefreshFields(true);
            UpdateCityHud();
            Toast($"{name} hasat edildi · +{got} lira", 2.2f);
            ShowField(i);
        }

        private void OpenBarn(string id)
        {
            if (!District.IsOpen) { ShowDistrict(); return; }
            panelSlot = null; city.Select(null);
            Sfx.Play("pick", 0.6f);
            cu.help.transform.parent.gameObject.SetActive(false);
            fieldPanel = -1;
            barnPanel = id;
            ShowBarn(id);
        }

        private void ShowBarn(string id)
        {
            var def = Farming.BarnOf(id);
            var b = Farming.BarnSave(id);
            string every = def.minutes >= 60 ? $"{def.minutes / 60} saatte" : $"{def.minutes} dakikada";
            if (!b.owned)
                cu.ShowPanel(def.name, $"Her {every} {def.yield} lira {def.product.ToLowerInvariant()}\nParan: {FreeBuild.Wallet.Money} lira", null,
                    ($"Satın al · {def.price} lira", Ui.Yellow, () => BuyBarn(id)), ("Kapat", Neutral, ClosePanel));
            else if (Farming.BarnReady(id))
                cu.ShowPanel(def.name, $"{def.product} hazır!\nTopla: +{def.yield} lira", null, ("Topla", Ui.Yellow, () => CollectBarn(id)), ("Kapat", Neutral, ClosePanel));
            else
                cu.ShowPanel(def.name, $"{def.product}: {Progress.LockText(Farming.BarnLeft(id))} sonra\nHer {every} {def.yield} lira", null,
                    ("Reklam izle: hemen hazır", Ui.Yellow, () => StartCoroutine(HurryBarn(id))), ("Kapat", Neutral, ClosePanel));
        }

        private void BuyBarn(string id)
        {
            var def = Farming.BarnOf(id);
            if (FreeBuild.Wallet.Money < def.price) { Toast("Yeterli para yok"); return; }
            if (!Farming.BuyBarn(id)) return;
            Sfx.Play("win");
            RefreshFields(true);
            city.people.Sync();
            ShowBarn(id);
        }

        private void CollectBarn(string id)
        {
            int got = Farming.CollectBarn(id);
            if (got <= 0) return;
            Sfx.Play("win");
            RefreshFields(true);
            Toast($"{Farming.BarnOf(id).product} satıldı · +{got} lira", 2.2f);
            ShowBarn(id);
        }

        private IEnumerator HurryBarn(string id)
        {
            ClosePanel();
            yield return TestAd();
            Farming.HurryBarn(id);
            RefreshFields(true);
            OpenBarn(id);
        }

        /// <summary>The HUD's "Hasat hazır": to the first ripe field or building.</summary>
        private void GoToHarvest()
        {
            var fv = fieldViews.FirstOrDefault(v => Farming.Ripe(Farming.Field(v.index)));
            if (fv != null) { cityCam.Focus(fv.root.position, 70f); OpenField(fv.index); return; }
            var bv = barnViews.FirstOrDefault(v => Farming.BarnReady(v.id));
            if (bv != null) { cityCam.Focus(bv.root.position, 70f); OpenBarn(bv.id); }
        }
    }
}
