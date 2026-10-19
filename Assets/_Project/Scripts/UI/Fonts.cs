using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BrickMuseum.UI
{
    /// <summary>
    /// Nunito (body) and Paytone One (titles), open-licence fonts in Resources/Fonts, turned into dynamic SDF fonts at
    /// startup so every Turkish letter is drawn on demand. Japanese and Korean come from Noto Sans KR, Arabic from Noto
    /// Sans Arabic (both open-licence), as fallbacks of the body font.
    /// </summary>
    public static class Fonts
    {
        private static TMP_FontAsset body, bold, title;

        public static TMP_FontAsset Body => body ??= WithScripts(Make("Fonts/Nunito-SemiBold", null));

        private static TMP_FontAsset WithScripts(TMP_FontAsset asset)
        {
            foreach (var path in new[] { "Fonts/NotoSansKR", "Fonts/NotoSansArabic" })
            {
                var f = Make(path, null);
                if (f != null && f != asset && f != TMP_Settings.defaultFontAsset) asset.fallbackFontAssetTable.Insert(0, f);
            }
            return asset;
        }
        public static TMP_FontAsset Bold => bold ??= Make("Fonts/Nunito-ExtraBold", Body);
        public static TMP_FontAsset Title => title ??= Make("Fonts/PaytoneOne-Regular", Bold);

        private static TMP_FontAsset Make(string path, TMP_FontAsset fallback)
        {
            var source = Resources.Load<Font>(path);
            TMP_FontAsset asset = null;
            if (source != null)
            {
                asset = TMP_FontAsset.CreateFontAsset(source, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (asset != null) asset.name = source.name;
            }
            var builtIn = TMP_Settings.defaultFontAsset;
            if (asset == null) return fallback ?? builtIn;
            asset.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (fallback != null) asset.fallbackFontAssetTable.Add(fallback);
            if (builtIn != null) asset.fallbackFontAssetTable.Add(builtIn);
            return asset;
        }
    }
}
