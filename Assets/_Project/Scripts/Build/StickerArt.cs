using System.Collections.Generic;
using BrickMuseum.Data;
using UnityEngine;

namespace BrickMuseum.Build
{
    /// <summary>Sticker pictures drawn in code (clock face, eye, sign backgrounds); words are added as 3D text on top.</summary>
    public static class StickerArt
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Texture(string art)
        {
            if (cache.TryGetValue(art, out var t)) return t;
            int w = 256, h = Mathf.RoundToInt(256 * Stickers.Aspect(art));
            var px = new Color32[w * h];
            var c = new Canvas(px, w, h);
            switch (art)
            {
                case "clock": Clock(c); break;
                case "eye":
                    c.Circle(128, 128, 124, new Color32(34, 34, 34, 255));
                    c.Circle(128, 128, 112, new Color32(255, 255, 255, 255));
                    c.Circle(140, 120, 56, new Color32(27, 27, 27, 255));
                    c.Circle(118, 146, 18, new Color32(255, 255, 255, 255));
                    break;
                case "taxi":
                    c.Rect(0, 0, w, h, new Color32(246, 196, 69, 255));
                    for (int i = 0; i < w / 16; i++)
                    {
                        c.Rect(i * 16, 0, 16, 16, i % 2 == 0 ? new Color32(20, 20, 20, 255) : new Color32(255, 255, 255, 255));
                        c.Rect(i * 16, h - 16, 16, 16, i % 2 == 1 ? new Color32(20, 20, 20, 255) : new Color32(255, 255, 255, 255));
                    }
                    break;
                case "plate":
                    c.Rect(0, 0, w, h, new Color32(20, 20, 20, 255));
                    c.Rect(4, 4, w - 8, h - 8, new Color32(255, 255, 255, 255));
                    c.Rect(4, 4, 36, h - 8, new Color32(31, 79, 191, 255));
                    break;
                case "face": Face(c); break;
                case "bus":
                    c.Rect(0, 0, w, h, new Color32(17, 17, 17, 255));
                    break;
            }
            t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Sticker " + art, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            return cache[art] = t;
        }

        /// <summary>The brick person's face on a transparent sheet: round eyes with a shine, brows, a small smile.</summary>
        private static void Face(Canvas c)
        {
            var ink = new Color32(40, 30, 28, 255);
            foreach (float x in new[] { 84f, 172f })
            {
                c.Circle(x, 138, 17, ink);
                c.Circle(x + 5, 145, 5, new Color32(255, 255, 255, 255));
                c.Line(x - 20, 178, x + 18, 184, 6f, ink);
            }
            for (int i = 0; i < 12; i++)
            {
                float a0 = Mathf.Lerp(-0.85f, 0.85f, i / 12f), a1 = Mathf.Lerp(-0.85f, 0.85f, (i + 1) / 12f);
                c.Line(128 + Mathf.Sin(a0) * 46, 96 - Mathf.Cos(a0) * 24, 128 + Mathf.Sin(a1) * 46, 96 - Mathf.Cos(a1) * 24, 7f, ink);
            }
            c.Circle(62, 104, 16, new Color32(240, 140, 130, 110));
            c.Circle(194, 104, 16, new Color32(240, 140, 130, 110));
        }

        /// <summary>Gold rim, cream face, minute and hour ticks, hands at ten past ten.</summary>
        private static void Clock(Canvas c)
        {
            c.Circle(128, 128, 127, new Color32(201, 162, 39, 255));
            c.Circle(128, 128, 114, new Color32(253, 246, 227, 255));
            c.Circle(128, 128, 106, new Color32(255, 250, 240, 255));
            var ink = new Color32(34, 34, 34, 255);
            for (int i = 0; i < 60; i++)
            {
                float a = i / 60f * Mathf.PI * 2f;
                bool big = i % 5 == 0;
                float r1 = big ? 84 : 96;
                c.Line(128 + Mathf.Sin(a) * r1, 128 + Mathf.Cos(a) * r1, 128 + Mathf.Sin(a) * 104, 128 + Mathf.Cos(a) * 104, big ? 3.2f : 1.2f, ink);
            }
            for (int i = 0; i < 12; i += 3)
            {
                float a = i / 12f * Mathf.PI * 2f; // a dot inside each quarter mark
                c.Circle(128 + Mathf.Sin(a) * 70, 128 + Mathf.Cos(a) * 70, 6, ink);
            }
            float hour = 10f / 12f * Mathf.PI * 2f, minute = 2f / 12f * Mathf.PI * 2f;
            c.Line(128, 128, 128 + Mathf.Sin(hour) * 50, 128 + Mathf.Cos(hour) * 50, 5f, ink);
            c.Line(128, 128, 128 + Mathf.Sin(minute) * 78, 128 + Mathf.Cos(minute) * 78, 3.5f, ink);
            c.Circle(128, 128, 9, ink);
        }

        /// <summary>Tiny software rasteriser (y up, anti-aliased edges) for the sticker pictures.</summary>
        private sealed class Canvas
        {
            private readonly Color32[] px;
            private readonly int w, h;
            public Canvas(Color32[] px, int w, int h) { this.px = px; this.w = w; this.h = h; }

            private void Blend(int x, int y, Color32 c, float cover)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || cover <= 0f) return;
                int i = y * w + x;
                var d = px[i];
                float a = cover * c.a / 255f, da = d.a / 255f, oa = a + da * (1 - a);
                if (oa <= 0f) return;
                byte Mix(byte s, byte t) => (byte)Mathf.Clamp(Mathf.RoundToInt((s * a + t * da * (1 - a)) / oa), 0, 255);
                px[i] = new Color32(Mix(c.r, d.r), Mix(c.g, d.g), Mix(c.b, d.b), (byte)Mathf.RoundToInt(oa * 255));
            }

            public void Rect(int x, int y, int rw, int rh, Color32 c)
            {
                for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) Blend(i, j, c, 1f);
            }

            public void Circle(float cx, float cy, float r, Color32 c)
            {
                for (int j = (int)(cy - r - 2); j <= cy + r + 2; j++)
                    for (int i = (int)(cx - r - 2); i <= cx + r + 2; i++)
                    {
                        float d = Mathf.Sqrt((i + 0.5f - cx) * (i + 0.5f - cx) + (j + 0.5f - cy) * (j + 0.5f - cy));
                        Blend(i, j, c, Mathf.Clamp01(r - d + 0.5f));
                    }
            }

            public void Line(float x0, float y0, float x1, float y1, float width, Color32 c)
            {
                float minX = Mathf.Min(x0, x1) - width - 2, maxX = Mathf.Max(x0, x1) + width + 2;
                float minY = Mathf.Min(y0, y1) - width - 2, maxY = Mathf.Max(y0, y1) + width + 2;
                var a = new Vector2(x0, y0); var ab = new Vector2(x1, y1) - a;
                for (int j = (int)minY; j <= maxY; j++)
                    for (int i = (int)minX; i <= maxX; i++)
                    {
                        var p = new Vector2(i + 0.5f, j + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                        float d = (a + ab * t - p).magnitude;
                        Blend(i, j, c, Mathf.Clamp01(width / 2f - d + 0.5f));
                    }
            }
        }
    }
}
