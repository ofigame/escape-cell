using System.Collections.Generic;
using UnityEngine;

namespace BrickMuseum.UI
{
    /// <summary>
    /// Icons drawn in code from signed distance shapes (the fonts have no emoji): white, anti-aliased, tinted by the UI.
    /// <see cref="ForLabel"/> picks a button's icon from its (Turkish) label, so every button gets its icon wherever it is made.
    /// </summary>
    public static class Icons
    {
        private static readonly Dictionary<string, Sprite> made = new Dictionary<string, Sprite>();

        private static Sprite Get(string name, System.Func<float, float, float> sdf) => made.TryGetValue(name, out var s) ? s : made[name] = Draw(128, sdf);

        // ---------- shapes (0..1 box, y up; negative = inside) ----------

        private static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        private static float Circ(float x, float y, float cx, float cy, float r) => Dist(x, y, cx, cy) - r;
        private static float Ring(float x, float y, float cx, float cy, float r, float w) => Mathf.Abs(Dist(x, y, cx, cy) - r) - w;

        private static float Box(float x, float y, float cx, float cy, float hx, float hy, float r = 0f)
        {
            float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        private static float Seg(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            var p = new Vector2(x - ax, y - ay);
            var ab = new Vector2(bx - ax, by - ay);
            float t = Mathf.Clamp01(Vector2.Dot(p, ab) / ab.sqrMagnitude);
            return (p - ab * t).magnitude - r;
        }

        private static float Poly(float x, float y, params float[] xy)
        {
            int n = xy.Length / 2;
            var p = new Vector2(x, y);
            float d = float.MaxValue; bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = new Vector2(xy[j * 2], xy[j * 2 + 1]), b = new Vector2(xy[i * 2], xy[i * 2 + 1]), ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                d = Mathf.Min(d, (a + ab * t - p).magnitude);
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -d : d;
        }

        private static float U(params float[] d) { float m = d[0]; for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]); return m; }
        private static float Cut(float a, float b) => Mathf.Max(a, -b);

        // ---------- the icons ----------

        public static Sprite Heart => Get("heart", (x, y) =>
        {
            float c1 = Circ(x, y, 0.3f, 0.64f, 0.22f), c2 = Circ(x, y, 0.7f, 0.64f, 0.22f);
            float tri = Mathf.Max(Mathf.Abs(x - 0.5f) * 1.25f + (0.62f - y) * 1.08f - 0.5f, 0.12f - y);
            return U(c1, c2, tri);
        });

        public static Sprite Star => Get("star", (x, y) =>
        {
            var pts = new float[20];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 0.48f : 0.2f;
                pts[i * 2] = 0.5f + Mathf.Cos(a) * r; pts[i * 2 + 1] = 0.48f + Mathf.Sin(a) * r;
            }
            return Poly(x, y, pts);
        });

        public static Sprite Circle => Get("circle", (x, y) => Circ(x, y, 0.5f, 0.5f, 0.48f));

        public static Sprite Grid => Get("grid", (x, y) => U(Box(x, y, 0.29f, 0.29f, 0.16f, 0.16f, 0.05f), Box(x, y, 0.71f, 0.29f, 0.16f, 0.16f, 0.05f),
            Box(x, y, 0.29f, 0.71f, 0.16f, 0.16f, 0.05f), Box(x, y, 0.71f, 0.71f, 0.16f, 0.16f, 0.05f)));

        public static Sprite Crate => Get("crate", (x, y) => Cut(U(Box(x, y, 0.5f, 0.4f, 0.33f, 0.27f, 0.05f), Box(x, y, 0.5f, 0.76f, 0.4f, 0.09f, 0.04f)),
            Box(x, y, 0.5f, 0.55f, 0.11f, 0.045f, 0.02f)));

        public static Sprite Walker => Get("walker", (x, y) => U(Circ(x, y, 0.56f, 0.85f, 0.1f),
            Seg(x, y, 0.53f, 0.68f, 0.46f, 0.42f, 0.075f),
            Seg(x, y, 0.46f, 0.42f, 0.3f, 0.1f, 0.065f), Seg(x, y, 0.46f, 0.42f, 0.64f, 0.1f, 0.065f),
            Seg(x, y, 0.51f, 0.63f, 0.32f, 0.48f, 0.055f), Seg(x, y, 0.51f, 0.63f, 0.7f, 0.52f, 0.055f)));

        public static Sprite Person => Get("person", (x, y) => U(Circ(x, y, 0.5f, 0.68f, 0.19f), Mathf.Max(Circ(x, y, 0.5f, 0.1f, 0.38f), 0.1f - y)));

        public static Sprite Globe => Get("globe", (x, y) =>
        {
            float ex = (x - 0.5f) / 0.19f, ey = (y - 0.5f) / 0.42f;
            float ellipse = Mathf.Abs(Mathf.Sqrt(ex * ex + ey * ey) - 1f) * 0.19f - 0.035f;
            float inside = Circ(x, y, 0.5f, 0.5f, 0.42f);
            return U(Ring(x, y, 0.5f, 0.5f, 0.42f, 0.05f), Mathf.Max(U(ellipse, Seg(x, y, 0.08f, 0.5f, 0.92f, 0.5f, 0.035f), Seg(x, y, 0.5f, 0.08f, 0.5f, 0.92f, 0.035f)), inside));
        });

        public static Sprite House => Get("house", (x, y) => Cut(U(Poly(x, y, 0.08f, 0.55f, 0.5f, 0.93f, 0.92f, 0.55f), Box(x, y, 0.5f, 0.32f, 0.3f, 0.26f, 0.03f)),
            Box(x, y, 0.5f, 0.2f, 0.08f, 0.14f, 0.02f)));

        public static Sprite Car => Get("car", (x, y) => Cut(U(Box(x, y, 0.5f, 0.42f, 0.44f, 0.13f, 0.07f), Box(x, y, 0.47f, 0.6f, 0.25f, 0.13f, 0.07f),
            Circ(x, y, 0.27f, 0.27f, 0.11f), Circ(x, y, 0.73f, 0.27f, 0.11f)), Box(x, y, 0.47f, 0.62f, 0.17f, 0.06f, 0.02f)));

        public static Sprite City => Get("city", (x, y) => Cut(U(Box(x, y, 0.2f, 0.33f, 0.13f, 0.25f, 0.02f), Box(x, y, 0.5f, 0.46f, 0.14f, 0.38f, 0.02f), Box(x, y, 0.8f, 0.28f, 0.13f, 0.2f, 0.02f)),
            U(Box(x, y, 0.5f, 0.66f, 0.05f, 0.05f), Box(x, y, 0.5f, 0.46f, 0.05f, 0.05f), Box(x, y, 0.2f, 0.4f, 0.05f, 0.05f), Box(x, y, 0.8f, 0.32f, 0.05f, 0.05f))));

        public static Sprite Close => Get("close", (x, y) => U(Seg(x, y, 0.24f, 0.24f, 0.76f, 0.76f, 0.08f), Seg(x, y, 0.24f, 0.76f, 0.76f, 0.24f, 0.08f)));

        public static Sprite Bulb => Get("bulb", (x, y) => U(Circ(x, y, 0.5f, 0.6f, 0.28f), Box(x, y, 0.5f, 0.28f, 0.13f, 0.09f, 0.03f), Box(x, y, 0.5f, 0.12f, 0.09f, 0.04f, 0.03f)));

        public static Sprite Spread => Get("spread", (x, y) => U(Circ(x, y, 0.24f, 0.3f, 0.11f), Circ(x, y, 0.72f, 0.24f, 0.1f), Circ(x, y, 0.5f, 0.55f, 0.12f),
            Circ(x, y, 0.22f, 0.76f, 0.09f), Circ(x, y, 0.78f, 0.74f, 0.11f)));

        public static Sprite Play => Get("play", (x, y) => Poly(x, y, 0.28f, 0.16f, 0.28f, 0.84f, 0.84f, 0.5f));

        public static Sprite Video => Get("video", (x, y) => Cut(Box(x, y, 0.5f, 0.5f, 0.43f, 0.31f, 0.09f), Poly(x, y, 0.42f, 0.35f, 0.42f, 0.65f, 0.66f, 0.5f)));

        public static Sprite Plus => Get("plus", (x, y) => U(Seg(x, y, 0.2f, 0.5f, 0.8f, 0.5f, 0.09f), Seg(x, y, 0.5f, 0.2f, 0.5f, 0.8f, 0.09f)));

        public static Sprite Undo => Get("undo", (x, y) => U(Mathf.Max(Ring(x, y, 0.55f, 0.42f, 0.28f, 0.07f), 0.42f - y), Poly(x, y, 0.12f, 0.45f, 0.42f, 0.45f, 0.27f, 0.22f)));

        public static Sprite Rotate => Get("rotate", (x, y) => U(Mathf.Max(Ring(x, y, 0.5f, 0.46f, 0.31f, 0.07f), -Mathf.Min(0.5f - x, 0.46f - y)),
            Poly(x, y, 0.47f, 0.95f, 0.47f, 0.59f, 0.72f, 0.77f)));

        public static Sprite Trash => Get("trash", (x, y) => Cut(U(Box(x, y, 0.5f, 0.38f, 0.25f, 0.3f, 0.04f), Box(x, y, 0.5f, 0.76f, 0.34f, 0.045f, 0.02f), Box(x, y, 0.5f, 0.84f, 0.1f, 0.05f, 0.02f)),
            U(Box(x, y, 0.41f, 0.38f, 0.03f, 0.2f), Box(x, y, 0.59f, 0.38f, 0.03f, 0.2f))));

        public static Sprite Layers => Get("layers", (x, y) =>
        {
            float R(float cy) => (Mathf.Abs(x - 0.5f) / 0.42f + Mathf.Abs(y - cy) / 0.15f - 1f) * 0.13f;
            return U(Mathf.Abs(R(0.28f)) - 0.025f, Mathf.Abs(R(0.5f)) - 0.025f, R(0.72f));
        });

        public static Sprite Check => Get("check", (x, y) => U(Seg(x, y, 0.16f, 0.5f, 0.41f, 0.25f, 0.085f), Seg(x, y, 0.41f, 0.25f, 0.85f, 0.75f, 0.085f)));

        public static Sprite Dice => Get("dice", (x, y) => Cut(Box(x, y, 0.5f, 0.5f, 0.4f, 0.4f, 0.11f),
            U(Circ(x, y, 0.5f, 0.5f, 0.075f), Circ(x, y, 0.31f, 0.31f, 0.075f), Circ(x, y, 0.69f, 0.69f, 0.075f), Circ(x, y, 0.31f, 0.69f, 0.075f), Circ(x, y, 0.69f, 0.31f, 0.075f))));

        public static Sprite Up => Get("up", (x, y) => U(Poly(x, y, 0.5f, 0.9f, 0.14f, 0.5f, 0.86f, 0.5f), Seg(x, y, 0.5f, 0.55f, 0.5f, 0.12f, 0.1f)));

        public static Sprite Down => Get("down", (x, y) => U(Poly(x, y, 0.5f, 0.1f, 0.14f, 0.5f, 0.86f, 0.5f), Seg(x, y, 0.5f, 0.45f, 0.5f, 0.88f, 0.1f)));

        public static Sprite Door => Get("door", (x, y) => Cut(Box(x, y, 0.5f, 0.48f, 0.25f, 0.42f, 0.04f), Circ(x, y, 0.63f, 0.46f, 0.05f)));

        public static Sprite Exit => Get("exit", (x, y) => U(Cut(Box(x, y, 0.36f, 0.5f, 0.24f, 0.4f, 0.04f), Box(x, y, 0.36f, 0.5f, 0.16f, 0.32f, 0.02f)),
            Seg(x, y, 0.42f, 0.5f, 0.74f, 0.5f, 0.07f), Poly(x, y, 0.7f, 0.68f, 0.7f, 0.32f, 0.95f, 0.5f)));

        public static Sprite Sofa => Get("sofa", (x, y) => U(Box(x, y, 0.5f, 0.36f, 0.4f, 0.12f, 0.05f), Box(x, y, 0.5f, 0.58f, 0.31f, 0.15f, 0.06f),
            Box(x, y, 0.13f, 0.46f, 0.08f, 0.22f, 0.05f), Box(x, y, 0.87f, 0.46f, 0.08f, 0.22f, 0.05f), Box(x, y, 0.2f, 0.17f, 0.04f, 0.07f), Box(x, y, 0.8f, 0.17f, 0.04f, 0.07f)));

        public static Sprite Palette => Get("palette", (x, y) => Cut(Circ(x, y, 0.5f, 0.5f, 0.42f),
            U(Circ(x, y, 0.32f, 0.6f, 0.07f), Circ(x, y, 0.52f, 0.72f, 0.07f), Circ(x, y, 0.72f, 0.58f, 0.07f), Circ(x, y, 0.42f, 0.3f, 0.1f))));

        public static Sprite Bag => Get("bag", (x, y) => U(Box(x, y, 0.5f, 0.38f, 0.36f, 0.3f, 0.09f), Mathf.Max(Ring(x, y, 0.5f, 0.68f, 0.17f, 0.05f), 0.66f - y)));

        /// <summary>The icon for a button with this (Turkish source) label; null when it has none.</summary>
        public static Sprite ForLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            string l = label.Trim();
            if (l == "Koleksiyon") return Grid;
            if (l.StartsWith("Envanter")) return Crate;
            if (l == "Şehri gez") return Walker;
            if (l == "Karakter" || l == "MÜŞTERİ") return Person;
            if (l == "İPTAL") return Close;
            if (l == "Sonraki araba") return Car;
            if (l == "Yeni araba") return Plus;
            if (l == "Dil") return Globe;
            if (l == "Evimi yap" || l == "Evime geç" || l == "GİR") return l == "GİR" ? Door : House;
            if (l == "Arabamı yap" || l == "Arabama geç" || l == "Arabam olsun" || l == "BİN") return Car;
            if (l == "İN") return Exit;
            if (l == "Şehre dön" || l == "Şehir" || l == "Şehre yerleştir") return City;
            if (l == "Kapat") return Close;
            if (l == "İpucu") return Bulb;
            if (l == "Dağıt") return Spread;
            if (l == "Başla" || l == "Devam" || l == "İnşaya devam et") return Play;
            if (l.StartsWith("Reklam izle")) return Video;
            if (l == "Yeni yap" || l == "Koy") return Plus;
            if (l == "Geri al") return Undo;
            if (l == "Döndür") return Rotate;
            if (l == "Sil" || l == "Kaldır") return Trash;
            if (l == "Renk") return Palette;
            if (l == "Tüm katlar" || l.EndsWith("kata kadar")) return Layers;
            if (l == "Bitti" || l == "Tamam" || l == "Seç") return Check;
            if (l == "Rastgele") return Dice;
            if (l == "ZIPLA" || l == "Üst kat") return Up;
            if (l == "Alt kat") return Down;
            if (l == "Dekore et") return Sofa;
            if (l == "ÇANTA") return Bag;
            return null;
        }

        /// <summary>White shape from a signed distance function (negative = inside), anti-aliased.</summary>
        private static Sprite Draw(int s, System.Func<float, float, float> sdf)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = sdf((x + 0.5f) / s, (y + 0.5f) / s) * s;
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255));
                }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
