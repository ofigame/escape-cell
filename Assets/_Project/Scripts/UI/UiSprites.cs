using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>Procedurally generated 9-sliced sprites (rounded fills, rings, soft shadows) so the UI needs no art files.</summary>
    public static class UiSprites
    {
        private const int Size = 128;
        private const int Radius = 44;

        private static Sprite rounded, ring, shadow, circle;

        public static Sprite Rounded => rounded != null ? rounded : rounded = Make(FillAlpha, Radius);
        public static Sprite Ring => ring != null ? ring : ring = Make(RingAlpha, Radius);
        public static Sprite Shadow => shadow != null ? shadow : shadow = Make(ShadowAlpha, Radius + 20);
        public static Sprite Circle => circle != null ? circle : circle = Make(CircleAlpha, 0);

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
