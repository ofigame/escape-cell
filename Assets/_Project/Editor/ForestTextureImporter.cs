using UnityEditor;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>
    /// Import rules for the photo-scanned forest assets (Poly Haven, CC0): normal maps as normal maps, colour and
    /// alpha maps readable (the builder packs them into cutout textures), models without their own materials (the
    /// game assigns the prepared ones) and in metres.
    /// </summary>
    public class ForestTextureImporter : AssetPostprocessor
    {
        private const string Root = "Assets/_Project/Forest/";
        private const string Models = "Assets/_Project/Resources/Forest/Models/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.anisoLevel = 4;
            if (name.Contains("_nor_"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
            }
            else if (name.Contains("_alpha_") || name.Contains("_rough_") || name.Contains("_mask_"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.isReadable = true;
            }
            else if (name.EndsWith("_cutout"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.mipMapsPreserveCoverage = true;
                ti.alphaTestReferenceValue = 0.5f;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.isReadable = name.Contains("_diff_");
            }
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Models)) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.useFileScale = true;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
        }
    }
}
