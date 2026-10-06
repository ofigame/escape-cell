using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>Procedurally generated 9-sliced sprites (rounded fills, rings, soft shadows) so the UI needs no art files.</summary>
    public static class UiSprites
    {
        private const int Size = 128;
        private const int Radius = 44;

        private static Sprite rounded, ring, shadow, circle, star;

        public static Sprite Rounded => rounded != null ? rounded : rounded = Make(FillAlpha, Radius);
        public static Sprite Ring => ring != null ? ring : ring = Make(RingAlpha, Radius);
        public static Sprite Shadow => shadow != null ? shadow : shadow = Make(ShadowAlpha, Radius + 20);
        public static Sprite Circle => circle != null ? circle : circle = Make(CircleAlpha, 0);
        public static Sprite Star => star != null ? star : star = MakeStar();

        private delegate float AlphaFn(float signedDistance);

        // Signed distance to a rounded rect inset by `pad` pixels (negative inside).
        private static float RoundedRectDistance(float x, float y, float pad, float r)
        {
            float half = Size * 0.5f - pad;
            float qx = Mathf.Abs(x - Size * 0.5f) - (half - r);
            float qy = Mathf.Abs(y - Size * 0.5f) - (half - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float FillAlpha(float d) => Mathf.Clamp01(0.5f - d);
        private static float RingAlpha(float d) => Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 4.5f);
        private static float ShadowAlpha(float d) => Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + 6f) / 26f)), 1.6f);
        private static float CircleAlpha(float d) => Mathf.Clamp01(0.5f - d);

        /// <summary>A chunky five-pointed star with slightly rounded look (4x4 supersampled edges).</summary>
        private static Sprite MakeStar()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0 ? 0.48f : 0.23f) * Size;
                points[i] = new Vector2(Size * 0.5f + Mathf.Cos(a) * r, Size * 0.53f + Mathf.Sin(a) * r);
            }

            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (InPolygon(points, new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))) inside++;
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 16));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool InPolygon(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        private static Sprite Make(AlphaFn fn, int border)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            bool isShadow = fn == ShadowAlpha;
            bool isCircle = fn == CircleAlpha;
            float pad = isShadow ? 22f : 1f;
            float r = isCircle ? Size * 0.5f - pad : (isShadow ? Radius : Radius - 1f);

            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, pad, r);
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)(fn(d) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);

            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
