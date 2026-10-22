using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A painted enchanted forest at night, made in code (one opaque picture): a violet sky with a soft light behind
    /// the trees, far mountains, layers of trees going into the mist, glowing purple and teal leaves, sunbeams falling
    /// through, hanging glowing wisteria, huge dark trunks framing the sides, a dark mossy ground with glowing plants,
    /// and fireflies everywhere. The same picture serves the menu (portrait) and the stages (wide, it wraps round).
    /// </summary>
    public static class ForestPainting
    {
        private static Texture2D portrait;

        /// <summary>The menu's picture (tall).</summary>
        public static Texture2D Portrait => portrait != null ? portrait : portrait = Resources.Load<Texture2D>("Forest/forest_portrait") ?? Paint(PortraitW, PortraitH, 7, false);


        public const int PortraitW = 768, PortraitH = 1536;

        /// <summary>Paints both afresh (to be saved as the shipped pictures).</summary>
        public static Texture2D PaintPortrait() => Paint(PortraitW, PortraitH, 7, false, true);

        private static float Fbm(float x, float y)
        {
            float s = 0f, a = 0.5f;
            for (int i = 0; i < 4; i++) { s += a * Mathf.PerlinNoise(x, y); x *= 2.03f; y *= 2.03f; a *= 0.5f; }
            return s;
        }

        private struct Tree { public float x, w, lean, wob, phase, top, canopy, cw; }

        private static Texture2D Paint(int W, int H, int seed, bool wrap, bool readable = false)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            float aspect = W / (float)H;
            // trees in three depths (more of them across a wide picture)
            int scale = wrap ? 4 : 1;
            float k = wrap ? 0.5f / aspect : 0.6f; // (widths across, as a share of the picture: the same trees tall or wide)
            var far = Trees(rng, 9 * scale, 0.012f, 0.03f, 0.55f, 0.75f, k);
            var mid = Trees(rng, (wrap ? 5 : 7) * scale, 0.03f, 0.06f, 0.72f, 0.95f, k);
            var near = new Tree[wrap ? 6 : 2];
            for (int i = 0; i < near.Length; i++)
            {
                float x = wrap ? (i + 0.5f) / near.Length + (R() - 0.5f) * 0.05f : (i == 0 ? 0.04f : 0.96f);
                near[i] = new Tree { x = x, w = (wrap ? 0.11f : 0.15f) * k * (0.85f + R() * 0.3f), lean = (R() - 0.5f) * 0.05f * k, wob = (0.02f + R() * 0.02f) * k, phase = R() * 6f, top = 1.2f, canopy = 0.3f, cw = 0.42f * k };
            }
            // wisteria strands hanging from the canopy, fireflies
            int strands = wrap ? 90 : 0; // (the menu has no hanging vines)
            var sx = new float[strands]; var sTop = new float[strands]; var sLen = new float[strands]; var sCol = new Color[strands];
            var palette = new[] { new Color(0.75f, 0.45f, 1f), new Color(1f, 0.55f, 0.85f), new Color(0.4f, 1f, 0.9f), new Color(0.6f, 0.75f, 1f) };
            for (int i = 0; i < strands; i++) { sx[i] = R(); sTop[i] = 0.82f + R() * 0.15f; sLen[i] = 0.08f + R() * 0.22f; sCol[i] = palette[rng.Next(palette.Length)]; }
            int flies = wrap ? 420 : 160;
            var fx = new float[flies]; var fy = new float[flies]; var fr = new float[flies]; var fc = new Color[flies];
            for (int i = 0; i < flies; i++)
            {
                fx[i] = R(); fy[i] = 0.08f + R() * 0.8f; fr[i] = (0.0015f + R() * R() * 0.006f);
                fc[i] = R() < 0.6f ? new Color(1f, 0.85f, 0.45f) : new Color(0.45f, 1f, 0.95f);
            }

            var px = new Color32[W * H];
            var skyTop = new Color(0.03f, 0.02f, 0.09f);
            var skyMid = new Color(0.13f, 0.07f, 0.27f);
            var light = new Color(0.35f, 0.62f, 0.7f);
            var warm = new Color(0.65f, 0.48f, 0.25f);
            var farCol = new Color(0.11f, 0.12f, 0.3f);
            var midCol = new Color(0.035f, 0.05f, 0.12f);
            var nearCol = new Color(0.012f, 0.018f, 0.035f);
            var purpleLeaf = new Color(0.45f, 0.2f, 0.65f);
            var tealLeaf = new Color(0.12f, 0.6f, 0.6f);
            var moss = new Color(0.1f, 0.55f, 0.45f);
            float lightX = 0.45f, lightY = 0.62f;
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    float nu = wrap ? u * aspect / 2f : u; // noise scale: the same look tall or wide
                    // sky: deep violet, a glow behind the trees
                    var c = Color.Lerp(skyMid, skyTop, Mathf.SmoothStep(0.5f, 1f, v));
                    float du = Wrapped(u - lightX, wrap) * (wrap ? aspect * 0.5f : 1f) * 1.6f, dv = (v - lightY) * 1.3f;
                    float glow = Mathf.Exp(-(du * du + dv * dv) * 5f);
                    c += light * glow * 0.95f + warm * Mathf.Exp(-(du * du + dv * dv) * 22f) * 0.5f;
                    if (v > 0.6f && Mathf.PerlinNoise(nu * 180f, v * 180f) > 0.84f) c += new Color(0.5f, 0.5f, 0.7f) * (v - 0.6f) * 1.5f;
                    // far mountains
                    float ridge = 0.36f + Fbm(nu * 4f + 3f, 1.3f) * 0.16f;
                    if (v < ridge) c = Color.Lerp(c, new Color(0.17f, 0.14f, 0.36f), 0.75f);
                    // mist rising from the ground
                    float mist = Mathf.Exp(-Mathf.Pow((v - 0.25f) / 0.12f, 2f)) * (0.6f + 0.4f * Fbm(nu * 6f, v * 3f + 9f));
                    // far trees in the mist
                    float fa = Cover(far, u, v, nu, wrap);
                    c = Color.Lerp(c, farCol + light * glow * 0.25f, fa * 0.8f);
                    c += new Color(0.2f, 0.32f, 0.42f) * mist * 0.45f;
                    // mid trees: dark trunks, glowing leaves
                    float leaves;
                    float ma = Cover(mid, u, v, nu, wrap, out leaves);
                    if (ma > 0f)
                    {
                        var leafCol = Color.Lerp(purpleLeaf, tealLeaf, Mathf.PerlinNoise(nu * 5f + 2f, v * 5f));
                        float sparkle = Mathf.Clamp01((Fbm(nu * 28f, v * 28f) - 0.52f) * 4f);
                        var tc = Color.Lerp(midCol, leafCol * (0.35f + sparkle * 0.9f), leaves);
                        c = Color.Lerp(c, tc, ma);
                    }
                    // sunbeams through the trees (from the glow, fanning down)
                    float ang = Mathf.Atan2(Wrapped(u - lightX, wrap) * (wrap ? aspect : 1f), lightY + 0.5f - v);
                    float beam = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(ang * 14f + 1.3f) * 0.5f + 0.5f), 6f) * Mathf.Clamp01((lightY + 0.3f - v) * 1.5f) * Mathf.Clamp01(v * 3f);
                    c += new Color(1f, 0.85f, 0.55f) * beam * 0.3f * (1f - ma * 0.5f);
                    // the near trunks framing it, a teal moss edge on their inner side, roots
                    float na = Cover(near, u, v, nu, wrap, out _);
                    if (na > 0f)
                    {
                        float edgeLight = Mathf.Clamp01(Mathf.PerlinNoise(nu * 30f, v * 12f) - 0.35f) * 0.6f;
                        c = Color.Lerp(c, nearCol + moss * edgeLight * 0.35f, na);
                    }
                    // the ground: dark moss, glowing plants, a pale path in the middle
                    float ground = 0.12f + Fbm(nu * 6f, 4.2f) * 0.05f;
                    if (v < ground)
                    {
                        var g = new Color(0.02f, 0.045f, 0.05f);
                        float plant = Mathf.Clamp01((Mathf.PerlinNoise(nu * 70f, v * 70f) - 0.7f) * 6f);
                        g += Color.Lerp(tealLeaf, purpleLeaf, Mathf.PerlinNoise(nu * 9f, 1f)) * plant * 0.8f;
                        float path = Mathf.Exp(-Mathf.Pow(Wrapped(u - 0.5f, wrap) * 6f, 2f)) * (1f - v / ground);
                        g += new Color(0.15f, 0.22f, 0.25f) * path * 0.6f;
                        c = Color.Lerp(c, g, Mathf.Clamp01((ground - v) * 60f));
                    }
                    px[y * W + x] = new Color(c.r, c.g, c.b, 1f);
                }
            }
            // wisteria strands and fireflies laid over it (each only over its own few pixels)
            void Add(int x, int y, Color c)
            {
                if (wrap) x = ((x % W) + W) % W; else if (x < 0 || x >= W) return;
                if (y < 0 || y >= H) return;
                var p = px[y * W + x];
                px[y * W + x] = new Color32((byte)Mathf.Min(255f, p.r + c.r * 255f), (byte)Mathf.Min(255f, p.g + c.g * 255f), (byte)Mathf.Min(255f, p.b + c.b * 255f), 255);
            }
            for (int i = 0; i < strands; i++)
            {
                int cx = Mathf.RoundToInt(sx[i] * (W - 1)), y0 = Mathf.RoundToInt((sTop[i] - sLen[i]) * (H - 1)), y1 = Mathf.RoundToInt(sTop[i] * (H - 1));
                for (int y = y0; y <= y1; y++)
                {
                    float along = (y1 - y) / (float)Mathf.Max(1, y1 - y0);
                    float bead = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(y * 0.35f)) * 1.2f);
                    for (int dx = -4; dx <= 4; dx++) Add(cx + dx + Mathf.RoundToInt(Mathf.Sin(y * 0.05f + i) * 2f), y, sCol[i] * Mathf.Clamp01(1f - Mathf.Abs(dx) / 4.5f) * (1f - along * 0.7f) * (0.3f + bead * 0.6f) * 0.7f);
                }
            }
            for (int i = 0; i < flies; i++)
            {
                float r = fr[i] * H; int cx = Mathf.RoundToInt(fx[i] * (W - 1)), cy = Mathf.RoundToInt(fy[i] * (H - 1)), reach = Mathf.CeilToInt(r * 3f);
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        float d2 = (dx * dx + dy * dy) / (r * r);
                        if (d2 < 9f) Add(cx + dx, cy + dy, fc[i] * Mathf.Exp(-d2) * 0.9f);
                    }
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = wrap ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.wrapModeV = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(true, !readable);
            return tex;
        }

        private static float Wrapped(float d, bool wrap) => wrap ? d - Mathf.Round(d) : d;

        private static Tree[] Trees(System.Random rng, int n, float wMin, float wMax, float topMin, float topMax, float k)
        {
            var t = new Tree[n];
            for (int i = 0; i < n; i++)
                t[i] = new Tree
                {
                    x = (i + (float)rng.NextDouble()) / n, w = (wMin + (float)rng.NextDouble() * (wMax - wMin)) * k, lean = ((float)rng.NextDouble() - 0.5f) * 0.08f * k,
                    wob = (0.005f + (float)rng.NextDouble() * 0.015f) * k, phase = (float)rng.NextDouble() * 6f,
                    top = topMin + (float)rng.NextDouble() * (topMax - topMin), canopy = 0.08f + (float)rng.NextDouble() * 0.1f,
                };
            for (int i = 0; i < n; i++) t[i].cw = t[i].canopy * 2.2f * k;
            return t;
        }

        private static float Cover(Tree[] trees, float u, float v, float nu, bool wrap) => Cover(trees, u, v, nu, wrap, out _);

        /// <summary>How much of this point the trees cover (soft edged), and how much of that is leaves.</summary>
        private static float Cover(Tree[] trees, float u, float v, float nu, bool wrap, out float leaves)
        {
            float a = 0f; leaves = 0f;
            foreach (var t in trees)
            {
                if (v > t.top + t.canopy * 1.4f) continue;
                // the trunk: wobbling, thinner going up, flaring into roots at the foot
                float cx = t.x + t.lean * v + Mathf.Sin(v * 9f + t.phase) * t.wob;
                float half = t.w * (1f - 0.45f * Mathf.Clamp01(v / Mathf.Max(0.1f, t.top))) + t.w * 1.6f * Mathf.Exp(-v * 22f);
                float sharp = 0.5f / Mathf.Max(0.0005f, t.w * 0.08f);
                float d = Mathf.Abs(Wrapped(u - cx, wrap));
                float trunk = v < t.top ? Mathf.Clamp01((half - d) * sharp) : 0f;
                // the crown: a cloudy blob round its top
                float cdx = Wrapped(u - (t.x + t.lean * t.top), wrap) / t.cw, cdy = (v - t.top) / t.canopy;
                float blob = 1f - (cdx * cdx + cdy * cdy) + (Fbm(nu * 18f + t.phase * 3f, v * 18f) - 0.5f) * 1.3f;
                float crown = Mathf.Clamp01(blob * 6f);
                float here = Mathf.Max(trunk, crown);
                if (here > a) { a = here; leaves = crown > trunk ? 1f : 0f; }
            }
            return a;
        }
    }
}
