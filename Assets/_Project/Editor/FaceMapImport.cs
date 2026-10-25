using UnityEditor;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>
    /// The heads' face maps (*_facemap.png: where each texel lies in the face's rectangle) are data, not colour: no sRGB,
    /// no compression, no mipmaps, clamped — the face's drawn expression is read through them exactly.
    /// </summary>
    public class FaceMapImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.EndsWith("_facemap.png")) return;
            var t = (TextureImporter)assetImporter;
            t.sRGBTexture = false;
            t.mipmapEnabled = false;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.alphaIsTransparency = false;
            foreach (var platform in new[] { "Android", "iPhone", "Standalone" })
            {
                var s = t.GetPlatformTextureSettings(platform);
                s.overridden = true;
                s.format = TextureImporterFormat.RGBA32;
                s.textureCompression = TextureImporterCompression.Uncompressed;
                t.SetPlatformTextureSettings(s);
            }
        }
    }
}
