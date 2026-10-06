using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>Paints the soft space backdrop (gradient, glow, grid lines, planet, stars) into a texture at runtime.</summary>
    public static class BackgroundArt
    {
        public static Texture2D Generate(int width, int height) => Generate(width, height, WorldTheme.Current);

        public static Texture2D Generate(int width, int height, WorldTheme theme)
        {
            var px = new Color[width * height];
            float cx = width * 0.5f, cy = height * 0.48f;

            for (int y = 0; y < height; y++)
            {
                var row = Color.Lerp(theme.bgBottom, theme.bgTop, (float)y / (height - 1));
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - cx) / height, dy = (y - cy) / height;
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 0.6f);
                    px[y * width + x] = Color.Lerp(row, theme.bgGlow, glow * glow * 0.7f);
                }
            }

            // Faint "cyber grid" on the upper-left, fading out toward the center.
            float fadeEnd = width * 0.5f;
            foreach (float fx in new[] { 0.05f, 0.17f, 0.3f })
                Line(px, width, height, fx * width, height * 0.38f, fx * width, height, 1.6f, 0.32f, fadeEnd, theme.bgLines);
            foreach (float fy in new[] { 0.5f, 0.66f, 0.82f })
                Line(px, width, height, 0f, fy * height, width * 0.5f, (fy + 0.22f) * height, 1.6f, 0.32f, fadeEnd, theme.bgLines);

            // Planet, top right.
            float minSide = Mathf.Min(width, height);
            Disc(px, width, height, width * 0.92f, height * 0.86f, minSide * 0.2f, theme.bgPlanet, 0.55f);
            Disc(px, width, height, width * 0.86f, height * 0.9f, minSide * 0.05f, theme.bgTop, 0.25f);
            Disc(px, width, height, width * 0.95f, height * 0.8f, minSide * 0.035f, theme.bgTop, 0.2f);

            // Stars.
            var rng = new System.Random(7);
            for (int i = 0; i < 55; i++)
            {
                float sx = (float)rng.NextDouble() * width;
                float sy = (float)rng.NextDouble() * height;
                float r = 0.8f + (float)rng.NextDouble() * 1.8f;
                Disc(px, width, height, sx, sy, r, theme.bgStar, 0.35f + (float)rng.NextDouble() * 0.5f);
            }
            Sparkle(px, width, height, width * 0.94f, height * 0.06f, minSide * 0.035f, theme.bgStar);

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Background",
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        private static void Blend(Color[] px, int w, int h, int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || a <= 0f) return;
            int i = y * w + x;
            px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(a));
        }

        private static void Line(Color[] px, int w, int h, float x0, float y0, float x1, float y1, float thickness, float alpha, float fadeEndX, Color color)
        {
            int minX = Mathf.FloorToInt(Mathf.Min(x0, x1) - thickness - 1), maxX = Mathf.CeilToInt(Mathf.Max(x0, x1) + thickness + 1);
            int minY = Mathf.FloorToInt(Mathf.Min(y0, y1) - thickness - 1), maxY = Mathf.CeilToInt(Mathf.Max(y0, y1) + thickness + 1);
            var a = new Vector2(x0, y0);
            var ab = new Vector2(x1 - x0, y1 - y0);
            float len2 = Mathf.Max(ab.sqrMagnitude, 0.0001f);

            for (int y = Mathf.Max(0, minY); y <= Mathf.Min(h - 1, maxY); y++)
                for (int x = Mathf.Max(0, minX); x <= Mathf.Min(w - 1, maxX); x++)
                {
                    var p = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    float cover = Mathf.Clamp01(thickness - d);
                    float fade = Mathf.Clamp01(1f - x / fadeEndX);
                    Blend(px, w, h, x, y, color, cover * alpha * fade);
                }
        }

        private static void Disc(Color[] px, int w, int h, float cx, float cy, float r, Color c, float alpha)
        {
            for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
                for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    Blend(px, w, h, x, y, c, Mathf.Clamp01(r - d + 0.5f) * alpha);
                }
        }

        private static void Sparkle(Color[] px, int w, int h, float cx, float cy, float size, Color color)
        {
            for (int y = Mathf.FloorToInt(cy - size); y <= Mathf.CeilToInt(cy + size); y++)
                for (int x = Mathf.FloorToInt(cx - size); x <= Mathf.CeilToInt(cx + size); x++)
                {
                    float dx = Mathf.Abs(x - cx) / size, dy = Mathf.Abs(y - cy) / size;
                    // four-point star: a thin diamond along each axis
                    float a = Mathf.Max(1f - (dx + dy * 4f), 1f - (dy + dx * 4f));
                    Blend(px, w, h, x, y, color, Mathf.Clamp01(a * 2f) * 0.85f);
                }
        }
    }
}
