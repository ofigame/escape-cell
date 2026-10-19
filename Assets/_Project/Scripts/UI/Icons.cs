using UnityEngine;

namespace BrickMuseum.UI
{
    /// <summary>Icons drawn in code (the fonts have no emoji/heart/star glyphs): heart, star, bag, circle.</summary>
    public static class Icons
    {
        private static Sprite heart, star, circle;

        public static Sprite Heart => heart ??= Draw(128, (x, y) =>
        {
            // two circles on top of a downward triangle, in a 0..1 box (y up)
            float c1 = Dist(x, y, 0.3f, 0.64f) - 0.22f, c2 = Dist(x, y, 0.7f, 0.64f) - 0.22f;
            float tri = Mathf.Max(Mathf.Abs(x - 0.5f) * 1.25f + (0.62f - y) * 1.08f - 0.5f, 0.12f - y);
            return Mathf.Min(Mathf.Min(c1, c2), tri);
        });

        public static Sprite Star => star ??= Draw(128, StarSdf);

        private static Vector2[] starPts;

        /// <summary>Signed distance to a five-pointed star polygon (outer radius 0.48, inner 0.2).</summary>
        private static float StarSdf(float x, float y)
        {
            if (starPts == null)
            {
                starPts = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 0.48f : 0.2f;
                    starPts[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.48f + Mathf.Sin(a) * r);
                }
            }
            var p = new Vector2(x, y);
            float d = float.MaxValue; bool inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                Vector2 a = starPts[j], b = starPts[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                d = Mathf.Min(d, (a + ab * t - p).magnitude);
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -d : d;
        }

        public static Sprite Circle => circle ??= Draw(128, (x, y) => Dist(x, y, 0.5f, 0.5f) - 0.48f);

        private static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

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
