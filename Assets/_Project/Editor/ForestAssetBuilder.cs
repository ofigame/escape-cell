using System.IO;
using UnityEditor;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>
    /// Prepares the forest prototype's assets (Poly Haven, CC0): copies the downloaded files into the project, packs
    /// colour + alpha into cutout textures for leaves and plants, and writes the materials, terrain layers, terrain
    /// material and sky as assets. Being assets (in Resources) is what keeps their shader variants (normal maps, alpha
    /// clipping, terrain) in phone builds.
    /// </summary>
    public static class ForestAssetBuilder
    {
        private const string SourceDir = "../Tools/assets/polyhaven";
        private const string TexDir = "Assets/_Project/Forest/Textures";
        private const string ModelDir = "Assets/_Project/Resources/Forest/Models";
        private const string OutDir = "Assets/_Project/Resources/Forest";

        [MenuItem("SquashBot/Build forest assets")]
        public static void Build()
        {
            Directory.CreateDirectory(TexDir);
            Directory.CreateDirectory(ModelDir);
            CopySources();
            AssetDatabase.Refresh();

            // Leaves, needles and plants: colour with the alpha map packed in.
            Pack("fir_tree_01_twig_diff_1k", "fir_tree_01_twig_alpha_1k", "fir_twig_cutout");
            Pack("tree_small_02_leaves_diff_1k", "tree_small_02_leaves_alpha_1k", "leaves_cutout");
            Pack("shrub_03_diff_1k", "shrub_03_alpha_1k", "shrub_03_cutout");
            Pack("shrub_04_diff_1k", "shrub_04_alpha_1k", "shrub_04_cutout");
            Pack("fern_02_diff_1k", "fern_02_alpha_1k", "fern_02_cutout");
            Pack("grass_medium_01_diff_1k", "grass_medium_01_alpha_1k", "grass_01_cutout");
            AssetDatabase.Refresh();

            Lit("Bark_Pine", "pine_bark_diff_1k", "pine_bark_nor_gl_1k", 0.12f, false);
            Lit("Bark_Oak", "bark_brown_02_diff_1k", "bark_brown_02_nor_gl_1k", 0.12f, false);
            Lit("Rock_Mossy", "mossy_rock_diff_1k", "mossy_rock_nor_gl_1k", 0.18f, false);
            Lit("Rock_Set", "rock_moss_set_01_diff_1k", "rock_moss_set_01_nor_gl_1k", 0.2f, false);
            Lit("Stump", "tree_stump_01_diff_1k", "tree_stump_01_nor_gl_1k", 0.12f, false);
            Lit("Path", "stony_dirt_path_diff_1k", "stony_dirt_path_nor_gl_1k", 0.1f, false);
            Lit("Fir_Twig", "fir_twig_cutout", "fir_tree_01_twig_nor_gl_1k", 0.1f, true);
            Lit("Leaves", "leaves_cutout", "tree_small_02_leaves_nor_gl_1k", 0.2f, true);
            Lit("Shrub_03", "shrub_03_cutout", "shrub_03_nor_gl_1k", 0.15f, true);
            Lit("Shrub_04", "shrub_04_cutout", "shrub_04_nor_gl_1k", 0.15f, true);
            Lit("Fern", "fern_02_cutout", "fern_02_nor_gl_1k", 0.15f, true);
            Lit("Grass", "grass_01_cutout", "grass_medium_01_nor_gl_1k", 0.1f, true);

            Layer("Layer_Ground", "forrest_ground_01_diff_1k", "forrest_ground_01_nor_gl_1k", 3.5f);
            Layer("Layer_Grass", "leafy_grass_diff_1k", "leafy_grass_nor_gl_1k", 3f);
            Layer("Layer_Path", "stony_dirt_path_diff_1k", "stony_dirt_path_nor_gl_1k", 2.5f);
            TerrainMaterial();
            Sky();
            AssetDatabase.SaveAssets();
            Debug.Log("[SquashBot] Forest assets built.");
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var b = new Bounds();
                bool first = true;
                int tris = 0;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    var rb = mf.sharedMesh.bounds;
                    var wb = new Bounds(mf.transform.TransformPoint(rb.center), Vector3.Scale(rb.size, mf.transform.lossyScale));
                    if (first) { b = wb; first = false; } else b.Encapsulate(wb);
                    tris += mf.sharedMesh.triangles.Length / 3;
                }
                Debug.Log($"[Forest] {go.name} parts {go.GetComponentsInChildren<MeshFilter>().Length} size {b.size} tris {tris}");
            }
        }

        private static void CopySources()
        {
            if (!Directory.Exists(SourceDir)) return; // a checkout without the downloads: the copies in the project are used
            foreach (var file in Directory.GetFiles(SourceDir))
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(name.EndsWith(".fbx") ? ModelDir : TexDir, name);
                if (!File.Exists(dest)) File.Copy(file, dest);
            }
        }

        private static string Find(string baseName)
        {
            foreach (var ext in new[] { ".png", ".jpg", ".exr" })
            {
                string p = TexDir + "/" + baseName + ext;
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static Texture2D Tex(string baseName)
        {
            string p = Find(baseName);
            return p == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        /// <summary>Writes colour (RGB) + alpha map (as A) into one PNG for alpha-clipped foliage.</summary>
        private static void Pack(string diff, string alpha, string outName)
        {
            string outPath = TexDir + "/" + outName + ".png";
            if (File.Exists(outPath)) return;
            var d = Tex(diff);
            var a = Tex(alpha);
            if (d == null || a == null)
            {
                Debug.LogWarning("[SquashBot] Missing textures for " + outName);
                return;
            }
            int w = d.width, h = d.height;
            var dc = d.GetPixels();
            var ac = a.width == w && a.height == h ? a.GetPixels() : Resize(a, w, h);
            var res = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int i = 0; i < dc.Length; i++)
            {
                var c = dc[i];
                c.a = ac[i].r;
                dc[i] = c;
            }
            res.SetPixels(dc);
            res.Apply();
            File.WriteAllBytes(outPath, res.EncodeToPNG());
            Object.DestroyImmediate(res);
        }

        private static Color[] Resize(Texture2D t, int w, int h)
        {
            var result = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    result[y * w + x] = t.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
            return result;
        }

        private static void Lit(string name, string albedo, string normal, float smoothness, bool cutout)
        {
            string path = OutDir + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", Tex(albedo));
            m.SetColor("_BaseColor", Color.white);
            var n = Tex(normal);
            if (n != null)
            {
                m.SetTexture("_BumpMap", n);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_EnvironmentReflections", 0f);
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f); // leaves are seen from both sides
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        private static void Layer(string name, string albedo, string normal, float tile)
        {
            string path = OutDir + "/" + name + ".terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (l == null)
            {
                l = new TerrainLayer();
                AssetDatabase.CreateAsset(l, path);
            }
            l.diffuseTexture = Tex(albedo);
            l.normalMapTexture = Tex(normal);
            l.tileSize = new Vector2(tile, tile);
            l.normalScale = 1f;
            l.smoothness = 0.05f;
            EditorUtility.SetDirty(l);
        }

        private static void TerrainMaterial()
        {
            string path = OutDir + "/Terrain.mat";
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null) { Debug.LogWarning("[SquashBot] URP terrain shader not found."); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(m);
        }

        private static void Sky()
        {
            string path = OutDir + "/Sky.mat";
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_SunSize", 0.035f);
            m.SetFloat("_SunSizeConvergence", 6f);
            m.SetFloat("_AtmosphereThickness", 0.9f);
            m.SetColor("_SkyTint", new Color(0.55f, 0.68f, 0.85f));
            m.SetColor("_GroundColor", new Color(0.32f, 0.36f, 0.3f));
            m.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(m);
        }
    }
}
