using BrickMuseum.Build;
using UnityEngine;

namespace BrickMuseum.World
{
    /// <summary>
    /// Lawn that looks like grass, not paint: a light texture drawn in code (patches of lighter and darker green, short
    /// blades), tinted by each lawn's own colour. Uses the plan UVs of the city's bent meshes.
    /// </summary>
    public static class Grass
    {
        private static Texture2D tex;

        public static Texture2D Texture
        {
            get
            {
                if (tex != null) return tex;
                const int s = 256;
                tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "grass", anisoLevel = 4 };
                var px = new Color[s * s];
                var rnd = new System.Random(11);
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        // soft patches (tileable: noise on a torus by summing shifted samples)
                        float u = x / (float)s, v = y / (float)s;
                        float n = Tile(u, v, 4f) * 0.6f + Tile(u, v, 11f) * 0.4f;
                        float l = Mathf.Lerp(0.78f, 1.0f, n);
                        px[y * s + x] = new Color(l * Mathf.Lerp(0.92f, 1f, n), l, l * 0.86f, 1f);
                    }
                // blades: short strokes, a little lighter or darker
                for (int b = 0; b < 2600; b++)
                {
                    int bx = rnd.Next(s), by = rnd.Next(s), len = 2 + rnd.Next(4);
                    float shade = rnd.NextDouble() < 0.5 ? 0.82f : 1.08f;
                    int lean = rnd.Next(3) - 1;
                    for (int k = 0; k < len; k++)
                    {
                        int px0 = (bx + (k * lean) / 2 + s) % s, py0 = (by + k) % s;
                        var c = px[py0 * s + px0];
                        px[py0 * s + px0] = new Color(Mathf.Clamp01(c.r * shade), Mathf.Clamp01(c.g * shade), Mathf.Clamp01(c.b * shade), 1f);
                    }
                }
                tex.SetPixels(px);
                tex.Apply(true);
                return tex;
            }
        }

        private static float Tile(float u, float v, float f)
        {
            // seamless noise: blend the four wrapped copies
            float a = Mathf.PerlinNoise(u * f, v * f), b = Mathf.PerlinNoise((u - 1f) * f + 40f, v * f);
            float c = Mathf.PerlinNoise(u * f, (v - 1f) * f + 40f), d = Mathf.PerlinNoise((u - 1f) * f + 40f, (v - 1f) * f + 40f);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>A lawn material: grass tinted with a colour (the tint can be changed later through _BaseColor).</summary>
        public static Material Lawn(Color tint)
        {
            var m = Mats.LitTexture(Texture, false);
            m.SetColor("_BaseColor", tint * 1.12f);
            m.SetFloat("_Smoothness", 0.05f);
            return m;
        }
    }
}
