using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// An aged dressed-stone pattern drawn in code (grey, so a material's colour tints it): two courses of blocks in a
    /// running bond, darker mortar lines, every block its own shade, weathering blotches, chipped corners and a few
    /// hairline cracks. Made once and kept; the ramparts tile it by their height.
    /// </summary>
    public static class StoneTexture
    {
        private static Texture2D aged;

        public static Texture2D Aged
        {
            get
            {
                if (aged != null) return aged;
                const int n = 256;
                aged = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "AgedStone", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
                var px = new Color32[n * n];
                var rng = new System.Random(11);
                int course = n / 2, block = n / 2, mortar = 5;
                // A shade per block (two courses × three blocks, the offset row wrapping round).
                var shade = new float[2, 3];
                for (int r = 0; r < 2; r++) for (int b = 0; b < 3; b++) shade[r, b] = 0.88f + (float)rng.NextDouble() * 0.16f;
                for (int y = 0; y < n; y++)
                {
                    int row = y / course, yy = y % course;
                    int offset = row == 1 ? block / 2 : 0;
                    for (int x = 0; x < n; x++)
                    {
                        int xs = (x + offset) % n, bi = xs / block, xx = xs % block;
                        bool joint = yy < mortar || xx < mortar;
                        // Weathering: soft blotches and fine grain.
                        float blotch = Mathf.PerlinNoise(x * 0.025f + 3f, y * 0.025f + 7f);
                        float grain = Mathf.PerlinNoise(x * 0.22f, y * 0.22f) * 0.5f + Mathf.PerlinNoise(x * 0.6f + 9f, y * 0.6f) * 0.5f;
                        float v = shade[row, Mathf.Min(bi, 2)] * (0.86f + grain * 0.2f) * Mathf.Lerp(0.82f, 1.02f, blotch);
                        // Worn, rounded block edges: a little darker towards the joints.
                        int edge = Mathf.Min(Mathf.Min(xx - mortar, block - 1 - xx), Mathf.Min(yy - mortar, course - 1 - yy));
                        if (!joint && edge < 6) v *= 0.9f + edge * 0.016f;
                        if (joint) v = 0.56f + grain * 0.08f;
                        byte c = (byte)Mathf.Clamp(v * 255f, 0f, 255f);
                        px[y * n + x] = new Color32(c, c, c, 255);
                    }
                }
                // A few hairline cracks and chipped corners.
                for (int k = 0; k < 7; k++)
                {
                    float x = rng.Next(n), y = rng.Next(n), a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    int len = 18 + rng.Next(40);
                    for (int s = 0; s < len; s++)
                    {
                        a += ((float)rng.NextDouble() - 0.5f) * 0.6f;
                        x += Mathf.Cos(a); y += Mathf.Sin(a);
                        int ix = ((int)x % n + n) % n, iy = ((int)y % n + n) % n;
                        var p = px[iy * n + ix];
                        byte c = (byte)(p.r * 0.72f);
                        px[iy * n + ix] = new Color32(c, c, c, 255);
                    }
                }
                aged.SetPixels32(px);
                aged.Apply(true, true);
                return aged;
            }
        }
    }
}
