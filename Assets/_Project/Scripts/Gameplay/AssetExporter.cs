using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Developer tool: writes every model the game builds from code — foi's builds and outfits, Fifi, the bugs,
    /// guard robots, enforcers, monsters and guardians, the other characters, weapons, hammers, crates and falling
    /// blocks, pickups, garage cosmetics and the realm floor tiles — into a folder tree sorted by kind, each as a
    /// transparent PNG picture and an OBJ + MTL model (with its colours). Started with the command line
    /// <c>-sbExport &lt;folder&gt;</c>; the game is not touched otherwise. An index.html shows them all.
    /// </summary>
    public static class AssetExporter
    {
        private const int Size = 1024;
        private const int Layer = 31;
        private static readonly Vector3 StageAt = new Vector3(0f, -900f, 0f);

        private static string root;
        private static Camera cam;
        private static RenderTexture rt;
        private static Texture2D readback;
        private static readonly List<(string folder, string name)> done = new List<(string, string)>();

        /// <summary>The folder from the command line, or null when no export was asked for.</summary>
        public static string RequestedFolder()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-sbExport") return args[i + 1];
            return null;
        }

        public static IEnumerator Run(string folder, Robot robot)
        {
            root = folder;
            Directory.CreateDirectory(root);
            yield return null;
            Setup();

            // ---------- Characters ----------
            for (int h = 0; h < HeroModels.Count; h++)
            {
                robot.SetHero(h);
                robot.ApplyWorld(0);
                Export("01_Karakterler/foi", $"foi_{h + 1}_{Loc.T("hero." + h)}", t => robot.BuildLookalike(t));
            }
            robot.SetHero(0);
            for (int w = 0; w < LevelCatalog.WorldCount; w++)
            {
                robot.ApplyWorld(w);
                Export("01_Karakterler/foi_dunya_kiyafetleri", $"foi_dunya_{w + 1:00}", t => robot.BuildLookalike(t));
            }
            robot.SetHero(SaveData.Hero);
            robot.ApplyWorld(0);
            Export("01_Karakterler/Fifi", "Fifi", t => Adopt(Buddy.Create(t.position).gameObject, t));
            Export("01_Karakterler/Diger", "Prenses_Mira", t => PrincessMira.Build(t).gameObject);
            Export("01_Karakterler/Diger", "Prenses_Mira_kafeste", t =>
            {
                var holder = new GameObject("Caged").transform;
                holder.SetParent(t, false);
                PrincessMira.Build(holder);
                PrincessMira.BuildCage(holder, MaterialFactory.Create(new Color(0.85f, 0.75f, 0.45f), new Color(0.6f, 0.45f, 0.1f)));
                return holder.gameObject;
            });
            Export("01_Karakterler/Diger", "Hirsiz", t => Adopt(Thief.Create(t.position, new Color(0.6f, 0.6f, 0.68f)).gameObject, t));
            Export("01_Karakterler/Diger", "Ozgur_Mira", t => Adopt(FreedMira.Create(t.position, t.position + Vector3.back).gameObject, t));

            // ---------- Enemies ----------
            for (int k = 0; k < BugModel.Kinds; k++)
            {
                int world = k;
                var colour = Color.HSVToRGB(Mathf.Repeat(world * 0.137f + 0.1f, 1f), 0.7f, 1f);
                Export("02_Dusmanlar/Bocekler", $"bocek_{k + 1}", t => BugModel.Build(t, colour, k).gameObject);
                Export("02_Dusmanlar/Bocekler", $"bocek_{k + 1}_isikli", t => BugModel.Build(t, colour, k, true).gameObject);
            }
            for (int w = 0; w < LevelCatalog.WorldCount; w++)
            {
                int world = w;
                var accent = Color.HSVToRGB(Mathf.Repeat(world * 0.21f + 0.55f, 1f), 0.75f, 1f);
                Export("02_Dusmanlar/Bekci_Robotlar", $"bekci_dunya_{w + 1:00}", t => GuardBot.Build(t, Color.Lerp(accent, new Color(0.3f, 0.3f, 0.35f), 0.35f), world).gameObject);
                Export("02_Dusmanlar/Iri_Robotlar", $"iri_robot_dunya_{w + 1:00}", t => HumanoidBot.Build(t, accent).gameObject);
            }

            // ---------- Monsters and bosses ----------
            foreach (Monster.Kind kind in Enum.GetValues(typeof(Monster.Kind)))
                Export("03_Canavarlar/Turler", "canavar_" + kind, t => MonsterAt(t, kind, new Color(0.55f, 0.85f, 0.4f), -1));
            for (int w = 0; w < LevelCatalog.WorldCount; w++)
            {
                int world = w;
                if (!GuardianLooks.Guardian(world, out var gk, out var gt))
                {
                    gk = (Monster.Kind)(world % 4);
                    gt = Color.Lerp(WorldTheme.ForWorld(world).accent, new Color(0.55f, 0.85f, 0.4f), gk == Monster.Kind.Slime ? 0.5f : 0.15f);
                }
                Export("03_Canavarlar/Dunya_Canavarlari", $"canavar_dunya_{w + 1:00}", t => MonsterAt(t, gk, gt, world));
            }
            Export("03_Canavarlar/Bosslar", "WARDEN", t => Adopt(WardenBoss.Create(t.position, 10, null).gameObject, t));

            // ---------- Weapons ----------
            foreach (var w in Armory.All)
                Export("04_Silahlar/Silahlar", $"{Armory.All.IndexOf(w) + 1:00}_{Loc.T("weapon." + w.id)}", t => WeaponModels.Build(t, w).gameObject);
            for (int l = 1; l <= 5; l++)
                Export("04_Silahlar/Cekicler", $"cekic_seviye_{l}", t => HammerModels.Build(t, l).gameObject);
            Export("04_Silahlar/Ozel", "Gok_Gurultusu_Cekici", t => ThunderHammer.BuildModel(t).gameObject);

            // ---------- Crates and falling blocks ----------
            for (int tier = 0; tier < 5; tier++)
                Export("05_Kasalar_ve_Engeller/Kasalar", $"kasa_seviye_{tier + 1}", t => Adopt(HazardVisuals.CrateOfTier(tier), t));
            HazardVisuals.CrateTier = 0;
            Export("05_Kasalar_ve_Engeller/Kasalar", "TNT_kasasi", t => Adopt(HazardVisuals.Bomb(), t));
            HazardVisuals.CrateTier = -1;
            var styles = new HashSet<BlockStyle>();
            var previous = WorldTheme.Current;
            foreach (var first in WorldTheme.All)
                foreach (var theme in new[] { first, first.variant })
                {
                    if (theme == null || !styles.Add(theme.block)) continue;
                    WorldTheme.SetCurrent(theme);
                    Export("05_Kasalar_ve_Engeller/Dusen_Bloklar", "blok_" + theme.block, t => Adopt(HazardVisuals.Block(), t));
                }
            WorldTheme.SetCurrent(previous);
            Export("05_Kasalar_ve_Engeller/Dusen_Bloklar", "bomba", t => Adopt(HazardVisuals.Bomb(), t));

            // ---------- Pickups and props ----------
            Export("06_Toplanabilir_ve_Nesneler", "anahtar", t =>
            {
                var key = KeyPickup.Create(t.position);
                var body = key.transform.Find("Body");
                if (body != null) { body.localScale = Vector3.one; body.localPosition = new Vector3(0f, 0.35f, 0f); } // it pops in from nothing in play
                return Adopt(key.gameObject, t);
            });
            Export("06_Toplanabilir_ve_Nesneler", "super_kasa", t => SuperCrate.BuildModel(t).gameObject);
            Export("06_Toplanabilir_ve_Nesneler", "cikis_portali", t => Adopt(ExitPortal.Create(t.position).gameObject, t));
            foreach (QuestKind q in Enum.GetValues(typeof(QuestKind)))
                Export("06_Toplanabilir_ve_Nesneler/Gorev_Esyalari", "gorev_" + q, t => Adopt(QuestItem.Create(q, t.position).gameObject, t));

            // ---------- Garage cosmetics ----------
            foreach (var c in Cosmetics.All)
            {
                if (c.IsDefault) continue;
                string sub = c.slot == Slot.Hat ? "Sapkalar" : c.slot == Slot.Back ? "Sirt" : c.slot == Slot.Arms ? "Kollar" : c.slot == Slot.Legs ? "Bacaklar" : c.slot == Slot.Badge ? "Rozetler" : null;
                if (sub == null) continue;
                var item = c;
                Export("07_Kozmetik/" + sub, item.id.Replace('.', '_'), t =>
                {
                    var holder = new GameObject(item.id).transform;
                    holder.SetParent(t, false);
                    if (item.slot == Slot.Hat) CosmeticModels.Hat(holder, item.id, item.color);
                    else if (item.slot == Slot.Back) CosmeticModels.Back(holder, item.id, item.color);
                    else if (item.slot == Slot.Arms) CosmeticModels.Arms(holder, item.id, item.color);
                    else if (item.slot == Slot.Legs) CosmeticModels.Legs(holder, item.id, item.color);
                    else CosmeticModels.Badge(holder, item.id, item.color);
                    return holder.childCount > 0 ? holder.gameObject : null;
                });
            }

            // ---------- Realm floors ----------
            for (int w = 0; w < LevelCatalog.WorldCount; w++)
            {
                var theme = WorldTheme.ForWorld(w).Realm(w);
                Export("08_Zeminler", $"zemin_dunya_{w + 1:00}", t => FloorPatch(t, theme));
            }

            WriteIndex();
            Debug.Log($"[AssetExporter] {done.Count} models written to {root}");
            Application.Quit();
        }

        // ---------- Building helpers ----------

        private static GameObject Adopt(GameObject go, Transform holder)
        {
            go.transform.SetParent(holder, false);
            go.transform.localPosition = Vector3.zero;
            return go;
        }

        private static GameObject MonsterAt(Transform t, Monster.Kind kind, Color tint, int world)
        {
            var m = Monster.Create(kind, t.position, 1, tint, null);
            if (world >= 0) GuardianLooks.DressGuardian(m.Body, world);
            var bar = m.transform.Find("HealthBar");
            if (bar != null) UnityEngine.Object.DestroyImmediate(bar.gameObject);
            return Adopt(m.gameObject, t);
        }

        private static GameObject FloorPatch(Transform t, WorldTheme theme)
        {
            var patch = new GameObject("Floor").transform;
            patch.SetParent(t, false);
            var slab = MaterialFactory.Create(theme.slab, Color.black);
            var edge = MaterialFactory.Create(theme.slab, theme.slabEdgeGlow);
            var frame = MaterialFactory.Create(theme.tileTop, theme.tileGlow);
            var top = MaterialFactory.Create(theme.tileTop, theme.tileSelfLight);
            for (int x = 0; x < 3; x++)
                for (int z = 0; z < 3; z++)
                {
                    var at = new Vector3(x - 1, 0f, z - 1);
                    Shapes.Rounded("SlabTop", patch, at + new Vector3(0f, -0.19f, 0f), new Vector3(1.16f, 0.3f, 1.16f), 0.06f, slab);
                    Shapes.Rounded("EdgeGlow", patch, at + new Vector3(0f, -0.36f, 0f), new Vector3(1.2f, 0.05f, 1.2f), 0.025f, edge);
                    Shapes.Rounded("Frame", patch, at + new Vector3(0f, -0.03f, 0f), new Vector3(0.93f, 0.1f, 0.93f), 0.045f, frame);
                    Shapes.Rounded("Top", patch, at, new Vector3(0.78f, 0.1f, 0.78f), 0.045f, top);
                }
            return patch.gameObject;
        }

        // ---------- Export ----------

        private static void Setup()
        {
            var camGo = new GameObject("ExportCamera");
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << Layer;
            cam.enabled = false;
            cam.allowHDR = false;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            rt.Create();
            readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var key = new GameObject("ExportKey").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.cullingMask = 1 << Layer;
            key.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
        }

        /// <summary>Builds one model on the stage, photographs it and writes its PNG, OBJ and MTL.</summary>
        private static void Export(string folder, string name, Func<Transform, GameObject> build)
        {
            name = Clean(name);
            var stage = new GameObject("ExportStage").transform;
            stage.position = StageAt;
            GameObject model = null;
            try { model = build(stage); }
            catch (Exception e) { Debug.LogWarning($"[AssetExporter] {name}: {e.Message}"); }
            if (model == null || stage.GetComponentsInChildren<MeshFilter>().Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(stage.gameObject);
                return;
            }
            foreach (var p in stage.GetComponentsInChildren<ParticleSystem>()) UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;

            string dir = Path.Combine(root, folder);
            Directory.CreateDirectory(dir);
            Photograph(stage, Path.Combine(dir, name + ".png"));
            WriteObj(stage, dir, name);
            done.Add((folder, name));
            UnityEngine.Object.DestroyImmediate(stage.gameObject);
        }

        private static void Photograph(Transform stage, string file)
        {
            var renderers = stage.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            // A three-quarter view from the front and a little above (the models face +z).
            var rotation = Quaternion.Euler(20f, 215f, 0f);
            cam.transform.rotation = rotation;
            cam.transform.position = bounds.center - rotation * Vector3.forward * (bounds.extents.magnitude * 4f + 2f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = bounds.extents.magnitude * 10f + 10f;
            float half = 0f;
            var c = bounds.center;
            var e = bounds.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = cam.transform.InverseTransformPoint(corner) - cam.transform.InverseTransformPoint(c);
                half = Mathf.Max(half, Mathf.Abs(local.x), Mathf.Abs(local.y));
            }
            cam.orthographicSize = half * 1.08f;
            cam.targetTexture = rt;
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            File.WriteAllBytes(file, readback.EncodeToPNG());
        }

        /// <summary>
        /// The model as a Wavefront OBJ (right-handed: x is mirrored and the winding turned round) in metres, with one
        /// MTL material per colour (diffuse, emission and opacity).
        /// </summary>
        private static void WriteObj(Transform stage, string dir, string name)
        {
            var inv = CultureInfo.InvariantCulture;
            var obj = new StringBuilder();
            var mtl = new StringBuilder();
            var materials = new Dictionary<string, string>();
            obj.Append("# foi cell - ").Append(name).Append('\n').Append("mtllib ").Append(name).Append(".mtl\n");
            int baseIndex = 1;
            var toStage = stage.worldToLocalMatrix;
            foreach (var mf in stage.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                var renderer = mf.GetComponent<Renderer>();
                if (mesh == null || renderer == null || !renderer.enabled || !mf.gameObject.activeInHierarchy) continue;
                var m = toStage * mf.transform.localToWorldMatrix;
                var normalMatrix = m.inverse.transpose;
                var mat = renderer.sharedMaterial;
                Color colour = mat != null && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                Color glow = mat != null && mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                string key = string.Format(inv, "{0:0.000}_{1:0.000}_{2:0.000}_{3:0.00}_{4:0.00}_{5:0.00}_{6:0.00}", colour.r, colour.g, colour.b, colour.a, glow.r, glow.g, glow.b);
                if (!materials.TryGetValue(key, out var matName))
                {
                    matName = "mat" + materials.Count;
                    materials[key] = matName;
                    mtl.Append("newmtl ").Append(matName).Append('\n');
                    mtl.AppendFormat(inv, "Kd {0:0.####} {1:0.####} {2:0.####}\n", colour.r, colour.g, colour.b);
                    mtl.AppendFormat(inv, "Ka {0:0.####} {1:0.####} {2:0.####}\n", colour.r * 0.2f, colour.g * 0.2f, colour.b * 0.2f);
                    mtl.AppendFormat(inv, "Ke {0:0.####} {1:0.####} {2:0.####}\n", Mathf.Clamp01(glow.r), Mathf.Clamp01(glow.g), Mathf.Clamp01(glow.b));
                    mtl.AppendFormat(inv, "d {0:0.###}\nillum 2\n\n", colour.a);
                }
                obj.Append("g ").Append(Clean(mf.name)).Append('\n').Append("usemtl ").Append(matName).Append('\n');
                var verts = mesh.vertices;
                var normals = mesh.normals;
                foreach (var v in verts)
                {
                    var p = m.MultiplyPoint3x4(v);
                    obj.AppendFormat(inv, "v {0:0.#####} {1:0.#####} {2:0.#####}\n", -p.x, p.y, p.z);
                }
                bool hasNormals = normals != null && normals.Length == verts.Length;
                if (hasNormals)
                    foreach (var n in normals)
                    {
                        var q = normalMatrix.MultiplyVector(n).normalized;
                        obj.AppendFormat(inv, "vn {0:0.####} {1:0.####} {2:0.####}\n", -q.x, q.y, q.z);
                    }
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                    var tris = mesh.GetTriangles(s);
                    for (int i = 0; i + 2 < tris.Length; i += 3)
                    {
                        int a = tris[i] + baseIndex, b = tris[i + 2] + baseIndex, c = tris[i + 1] + baseIndex; // winding turned round
                        if (hasNormals) obj.AppendFormat(inv, "f {0}//{0} {1}//{1} {2}//{2}\n", a, b, c);
                        else obj.AppendFormat(inv, "f {0} {1} {2}\n", a, b, c);
                    }
                }
                baseIndex += verts.Length;
            }
            File.WriteAllText(Path.Combine(dir, name + ".obj"), obj.ToString());
            File.WriteAllText(Path.Combine(dir, name + ".mtl"), mtl.ToString());
        }

        private static string Clean(string s)
        {
            var b = new StringBuilder();
            foreach (char ch in s)
            {
                char c = ch switch { 'ç' => 'c', 'Ç' => 'C', 'ğ' => 'g', 'Ğ' => 'G', 'ı' => 'i', 'İ' => 'I', 'ö' => 'o', 'Ö' => 'O', 'ş' => 's', 'Ş' => 'S', 'ü' => 'u', 'Ü' => 'U', _ => ch };
                b.Append(char.IsLetterOrDigit(c) && c < 128 || c == '_' || c == '-' ? c : '_');
            }
            return b.ToString().Trim('_');
        }

        /// <summary>An index.html in the export folder: every picture, grouped by folder, with links to its model.</summary>
        private static void WriteIndex()
        {
            var html = new StringBuilder();
            html.Append("<!doctype html><meta charset=\"utf-8\"><title>foi cell - modeller</title><style>")
                .Append("body{font-family:system-ui,sans-serif;background:#14121f;color:#eee;margin:24px}h2{margin:32px 0 8px;color:#9fe3ff;font-weight:600}")
                .Append(".g{display:grid;grid-template-columns:repeat(auto-fill,minmax(170px,1fr));gap:12px}")
                .Append(".c{background:#211e33;border-radius:12px;padding:8px;text-align:center;font-size:12px}")
                .Append(".c img{width:100%;aspect-ratio:1;object-fit:contain;background:repeating-conic-gradient(#2a2740 0 25%,#232036 0 50%) 0 0/20px 20px;border-radius:8px}")
                .Append("a{color:#ffd27a}</style><h1>foi cell - oyun modelleri (")
                .Append(done.Count).Append(")</h1>");
            string current = null;
            foreach (var (folder, name) in done)
            {
                if (folder != current)
                {
                    if (current != null) html.Append("</div>");
                    html.Append("<h2>").Append(folder.Replace('/', ' ').Replace('_', ' ')).Append("</h2><div class=\"g\">");
                    current = folder;
                }
                string p = folder + "/" + name;
                html.Append("<div class=\"c\"><img loading=\"lazy\" src=\"").Append(p).Append(".png\"><div>").Append(name)
                    .Append("</div><a href=\"").Append(p).Append(".obj\">obj</a> · <a href=\"").Append(p).Append(".mtl\">mtl</a></div>");
            }
            if (current != null) html.Append("</div>");
            File.WriteAllText(Path.Combine(root, "index.html"), html.ToString());
        }
    }
}
