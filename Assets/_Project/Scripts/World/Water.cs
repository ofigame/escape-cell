using BrickMuseum.Build;
using UnityEngine;

namespace BrickMuseum.World
{
    /// <summary>
    /// Living water: a ripple texture drawn in code (light glints on blue) that drifts over the lake and flows down the
    /// creek toward it. Meshes using it need planar UVs (<see cref="PlanarUV"/>; the city's bent meshes have them).
    /// </summary>
    public static class Water
    {
        private static Texture2D ripples;
        private static Material lake, creek;

        private static Texture2D Ripples
        {
            get
            {
                if (ripples != null) return ripples;
                const int s = 128;
                ripples = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "ripples" };
                var px = new Color32[s * s];
                var deep = new Color(0.22f, 0.47f, 0.74f);
                var light = new Color(0.62f, 0.82f, 0.96f);
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = x / (float)s * Mathf.PI * 2f, v = y / (float)s * Mathf.PI * 2f;
                        // whole numbers of waves across the tile, so it repeats without seams
                        float w = Mathf.Sin(3f * u + 2f * Mathf.Sin(2f * v)) * 0.5f + Mathf.Sin(5f * v + 1.5f * Mathf.Sin(3f * u + 1f)) * 0.35f
                                  + Mathf.Sin(7f * u - 4f * v) * 0.15f;
                        float glint = Mathf.SmoothStep(0.55f, 0.95f, w * 0.5f + 0.5f);
                        var c = Color.Lerp(deep, light, glint);
                        px[y * s + x] = new Color(c.r, c.g, c.b, 0.9f);
                    }
                ripples.SetPixels32(px);
                ripples.Apply(true);
                return ripples;
            }
        }

        private static Material Make(float alpha)
        {
            // see-through, so the shallows and the deep bed show under it
            var m = Mats.LitTexture(Ripples, true);
            m.SetFloat("_Smoothness", 0.93f);
            m.SetColor("_BaseColor", new Color(0.28f, 0.56f, 0.95f, alpha));   // a blue tint over the light ripples
            return m;
        }

        public static Material Lake => lake ??= Make(0.78f);
        public static Material Creek => creek ??= Make(0.88f);

        /// <summary>The ripples move: the lake drifts slowly, the creek runs south into it.</summary>
        public static void Tick(float time)
        {
            if (lake != null) lake.SetTextureOffset("_BaseMap", new Vector2(Mathf.Sin(time * 0.07f) * 0.3f, time * 0.012f));
            if (creek != null) creek.SetTextureOffset("_BaseMap", new Vector2(Mathf.Sin(time * 0.3f) * 0.03f, -time * 0.12f));
        }

        /// <summary>Texture coordinates from x and z (one ripple tile every 1/scale units).</summary>
        public static void PlanarUV(Mesh m, float scale)
        {
            var v = m.vertices;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(v[i].x, v[i].z) * scale;
            m.uv = uv;
        }
    }
}
