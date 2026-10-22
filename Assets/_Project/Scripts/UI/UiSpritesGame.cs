using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>
    /// The game look's sprites, drawn in code like the rest: cut-corner plates and bevelled faces (tint them), a double
    /// gold rim with small diamonds at the corners, a gold ring for round buttons, a ribbon for titles and a starry
    /// galaxy for the backgrounds.
    /// </summary>
    public static partial class UiSprites
    {
        /// <summary>Corner cut of the plates and rims (texture pixels).</summary>
        private const float Chamfer = 15f;
        private const float FramePad = 3f;
        private const int FrameBorder = 40;
        /// <summary>The frames are drawn at twice the old size (crisp edges on sharp screens), the same size on screen.</summary>
        private const int FS = 256;
        private const float K = FS / 128f;

        private static Sprite plate, bevel, goldRim, goldRing, galaxy, ribbon;

        /// <summary>A cut-corner plate, a touch brighter at the top (tint it: panels, cards, chips).</summary>
        public static Sprite Plate => plate != null ? plate : plate = MakeFrame(FrameKind.Plate);
        /// <summary>A cut-corner bevelled face: a light top edge, a dark bottom edge (tint it: buttons).</summary>
        public static Sprite Bevel => bevel != null ? bevel : bevel = MakeFrame(FrameKind.Bevel);
        /// <summary>The double gold border of a plate or face, in its own colours (draw it white over the plate).</summary>
        public static Sprite GoldRim => goldRim != null ? goldRim : goldRim = MakeFrame(FrameKind.Rim);
        /// <summary>A double gold ring for round buttons and orbs (unsliced).</summary>
        public static Sprite GoldRing => goldRing != null ? goldRing : goldRing = MakeGoldRing();
        /// <summary>A deep space backdrop: nebula clouds, stars and faint constellations (portrait, unsliced).</summary>
        public static Sprite Galaxy => galaxy != null ? galaxy : galaxy = MakeGalaxy();
        /// <summary>A banner ribbon with folded tails (tint it: screen titles). Sliced left and right.</summary>
        public static Sprite Ribbon => ribbon != null ? ribbon : ribbon = MakeRibbon();

        private static Sprite parchment;

        /// <summary>Old parchment in its own colours: warm paper, faint mottling, burnt edges (sliced).</summary>
        public static Sprite Parchment => parchment != null ? parchment : parchment = MakeParchment();

        private static Sprite MakeParchment()
        {
            var tex = new Texture2D(FS, FS, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[FS * FS];
            var paper = new Color(0.95f, 0.87f, 0.69f);
            var burnt = new Color(0.62f, 0.43f, 0.22f);
            for (int y = 0; y < FS; y++)
                for (int x = 0; x < FS; x++)
                {
                    float fx = (x + 0.5f) / K, fy = (y + 0.5f) / K; // (drawn twice as fine, measured in the old 128 units)
                    // a slightly ragged outline, darker toward it
                    float rag = (Mathf.PerlinNoise(fx * 0.21f, fy * 0.21f) - 0.5f) * 2.4f;
                    float d = ChamferDistance(fx, fy) + rag;
                    float burn = Mathf.Clamp01(1f + d / 13f);
                    float mottle = (Mathf.PerlinNoise(fx * 0.06f + 5f, fy * 0.06f + 9f) - 0.5f) * 0.07f;
                    var c = Color.Lerp(paper, burnt, burn * burn) + new Color(mottle, mottle, mottle * 0.8f, 0f);
                    c.a = Mathf.Clamp01(0.5f - d * K);
                    px[y * FS + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, FS, FS), new Vector2(0.5f, 0.5f), 100f * K, 0, SpriteMeshType.FullRect,
                new Vector4(FrameBorder * K, FrameBorder * K, FrameBorder * K, FrameBorder * K));
        }

        private enum FrameKind { Plate, Bevel, Rim }

        /// <summary>Corner radius of the new look's rounded plates and frames (old 128 units).</summary>
        private const float Round = 28f;

        /// <summary>Signed distance to the rounded rectangle inset by <see cref="FramePad"/> (negative inside).</summary>
        private static float ChamferDistance(float x, float y)
        {
            float half = Size * 0.5f - FramePad;
            float qx = Mathf.Abs(x - Size * 0.5f) - (half - Round), qy = Mathf.Abs(y - Size * 0.5f) - (half - Round);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - Round;
        }

        /// <summary>Brushed steel with a blue cast: dark at the bottom, pale at the top, a bright streak where <paramref name="shine"/> is high.</summary>
        private static Color SteelAt(float t, float shine)
        {
            var dark = new Color(0.36f, 0.42f, 0.52f);
            var light = new Color(0.86f, 0.9f, 0.96f);
            var c = Color.Lerp(dark, light, Mathf.SmoothStep(0f, 1f, t));
            return Color.Lerp(c, Color.white, shine * 0.5f);
        }

        private static readonly Color Neon = new Color(0.35f, 0.92f, 1f);

        /// <summary>How much of a band of distances [from, to] covers this pixel (soft edges).</summary>
        /// <summary>The same in the fine drawing: distances in the old units, soft edges one fine pixel wide.</summary>
        private static float BandK(float d, float from, float to) => Mathf.Clamp01((d - from) * K + 0.5f) * Mathf.Clamp01((to - d) * K + 0.5f);

        private static float Band(float d, float from, float to) => Mathf.Clamp01(d - from + 0.5f) * Mathf.Clamp01(to - d + 0.5f);

        /// <summary>Polished gold: deep at the bottom, pale at the top, with a bright streak where <paramref name="shine"/> is high.</summary>
        private static Color GoldAt(float t, float shine)
        {
            var dark = new Color(0.52f, 0.33f, 0.12f);
            var light = new Color(1f, 0.87f, 0.55f);
            var c = Color.Lerp(dark, light, Mathf.SmoothStep(0f, 1f, t));
            return Color.Lerp(c, new Color(1f, 0.96f, 0.82f), shine * 0.55f);
        }

        private static Sprite MakeFrame(FrameKind kind)
        {
            var tex = new Texture2D(FS, FS, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[FS * FS];
            float half = Size * 0.5f - FramePad;
            for (int y = 0; y < FS; y++)
                for (int x = 0; x < FS; x++)
                {
                    float fx = (x + 0.5f) / K, fy = (y + 0.5f) / K; // (drawn twice as fine, measured in the old 128 units)
                    float d = ChamferDistance(fx, fy);
                    float t = y / (float)(FS - 1);
                    Color c;
                    if (kind == FrameKind.Plate)
                    {
                        // the new look's panels: a soft top-to-bottom gradient, a gloss along the top, a shade at the foot
                        float g = Mathf.Lerp(0.78f, 1f, Mathf.SmoothStep(0f, 1f, t));
                        if (t > 0.8f && d < -6f) g = Mathf.Min(1.0f, g + 0.06f * Mathf.SmoothStep(0.8f, 0.95f, t));
                        g = Mathf.Lerp(g, 0.7f, BandK(d, -6f, 0f) * (t < 0.5f ? 0.5f : 0f));
                        c = new Color(g, g, g, Mathf.Clamp01(0.5f - d * K));
                    }
                    else if (kind == FrameKind.Bevel)
                    {
                        // A raised face: the body over a gradient, a lit band along the upper inner edge and a shaded
                        // band along the lower one, and a faint sheen over the upper half.
                        // the new look's glossy candy face: deeper toward the foot, a broad pale gloss over the top half,
                        // a darker band along the lower edge, and a small white glint near the top-left corner
                        float g = Mathf.Lerp(0.55f, 0.92f, Mathf.SmoothStep(0f, 1f, t));
                        float edge = BandK(d, -8f, 0f);
                        g = Mathf.Lerp(g, t > 0.5f ? 1f : 0.38f, edge * 0.85f);
                        float gloss = t > 0.54f && d < -7f ? Mathf.SmoothStep(0.54f, 0.68f, t) * 0.3f : 0f;
                        float gx = fx - 26f, gy = fy - (Size - 22f);
                        float glint = Mathf.Clamp01(1.2f - Mathf.Sqrt(gx * gx / 64f + gy * gy / 16f));
                        float w = Mathf.Min(1f, g + gloss + glint * 0.6f);
                        c = new Color(w, w, w, Mathf.Clamp01(0.5f - d * K));
                    }
                    else
                    {
                        // the new look's frame: a dark navy outline, a band of brushed steel, a fine neon-cyan line inside
                        float outline = BandK(d, 0f, 2.2f) * 0.9f;
                        float outer = BandK(d, -6.8f, 0f);
                        float inner = BandK(d, -10f, -8.2f);
                        float shine = Mathf.Clamp01(1f - Mathf.Abs((d + 3.4f) / 3.4f));
                        if (outer > 0f) { var g = SteelAt(t, shine); c = new Color(g.r, g.g, g.b, outer); }
                        else if (inner > 0f) c = new Color(Neon.r, Neon.g, Neon.b, inner * 0.8f);
                        else c = new Color(0.03f, 0.05f, 0.14f, outline);
                    }
                    px[y * FS + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, FS, FS), new Vector2(0.5f, 0.5f), 100f * K, 0, SpriteMeshType.FullRect,
                new Vector4(FrameBorder * K, FrameBorder * K, FrameBorder * K, FrameBorder * K));
        }

        private static Sprite MakeGoldRing()
        {
            const int S = Big;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color[S * S];
            float R = S * 0.5f - 3f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - S * 0.5f, dy = y + 0.5f - S * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - R; // negative inside
                    // the new look: a dark outline, a thick band of brushed steel, a fine neon-cyan line inside it,
                    // and a white glint at the top left of the ball
                    float outer = Band(d, -12f, 0f), inner = Band(d, -16f, -13.5f), outline = Band(d, 0f, 3f) * 0.85f;
                    float t = y / (float)(S - 1);
                    float shine = Mathf.Clamp01(1f - Mathf.Abs((d + 5f) / 5f));
                    float gx = dx + S * 0.2f, gy = dy - S * 0.24f;
                    float glint = Mathf.Clamp01(1.3f - Mathf.Sqrt(gx * gx / 380f + gy * gy / 120f)) * (d < -16f ? 1f : 0f);
                    Color g; float a;
                    if (outer > 0f) { g = SteelAt(t, shine); a = outer; }
                    else if (inner > 0f) { g = Neon; a = inner * 0.85f; }
                    else if (glint > 0f) { g = Color.white; a = glint * 0.7f; }
                    else { g = new Color(0.03f, 0.05f, 0.14f); a = outline; }
                    px[y * S + x] = new Color(g.r, g.g, g.b, a);
                }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 200f);
        }

        private static Sprite MakeRibbon()
        {
            // A wide band with notched tails tucked behind its ends: 256 x 64, sliced left and right.
            const int W = 256, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int band = 0, tail = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float u = x + (sx + 0.5f) / 4f, v = y + (sy + 0.5f) / 4f;
                            float e = Mathf.Min(u, W - u); // in from the nearer end
                            if (e >= 22f && v >= 10f && v <= H - 2f) band++;
                            else if (e < 30f && v >= 0f && v <= H - 14f && e > 2f + Mathf.Abs(v - (H - 14f) * 0.5f) * 0.4f) tail++;
                        }
                    float a = (band + tail) / 16f;
                    float t = y / (float)(H - 1);
                    float g = band >= tail ? Mathf.Lerp(0.72f, 1f, t) : 0.5f;
                    if (band > 0 && (y == 12 || y == H - 5)) g = Mathf.Min(1f, g + 0.25f);
                    px[y * W + x] = new Color(g, g, g, a);
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(40, 0, 40, 0));
        }

        private static float Fbm(float x, float y)
        {
            float s = 0f, a = 0.5f;
            for (int i = 0; i < 4; i++) { s += a * Mathf.PerlinNoise(x, y); x *= 2.03f; y *= 2.03f; a *= 0.5f; }
            return s;
        }

        private static Sprite MakeGalaxy()
        {
            const int W = 720, H = 1560; // sharp stars on a phone
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[W * H]; // (a quarter of the memory a Color array takes)
            var top = new Color(0.035f, 0.045f, 0.13f);
            var bottom = new Color(0.015f, 0.015f, 0.055f);
            var purple = new Color(0.42f, 0.14f, 0.55f);
            var blue = new Color(0.1f, 0.3f, 0.62f);
            // The clouds are soft: worked out on a grid four pixels apart and blended between (a sixteenth of the noise).
            const int Step = 4;
            int gw = W / Step + 2, gh = H / Step + 2;
            var cloudP = new float[gw * gh];
            var cloudB = new float[gw * gh];
            for (int gy = 0; gy < gh; gy++)
                for (int gx = 0; gx < gw; gx++)
                {
                    float u = gx * Step / (float)W, v = gy * Step / (float)H;
                    // a band of cloud running corner to corner: purple, with blue drifts through it
                    float diag = Mathf.Abs((u - 0.5f) * 0.9f + (v - 0.5f) * 0.55f);
                    float band = Mathf.Clamp01(1f - diag * 2.4f);
                    float n1 = Fbm(u * 3.2f + 11f, v * 6.4f + 3f), n2 = Fbm(u * 2.6f + 41f, v * 5.2f + 17f);
                    cloudP[gy * gw + gx] = Mathf.Pow(Mathf.Clamp01(n1 * 1.6f - 0.45f), 1.6f) * (0.35f + band) * 0.55f;
                    cloudB[gy * gw + gx] = Mathf.Pow(Mathf.Clamp01(n2 * 1.6f - 0.5f), 1.6f) * (0.25f + band * 0.8f) * 0.5f;
                }
            for (int y = 0; y < H; y++)
            {
                int gy = y / Step;
                float fy = (y % Step) / (float)Step;
                var sky = Color.Lerp(bottom, top, y / (float)H);
                for (int x = 0; x < W; x++)
                {
                    int gx = x / Step;
                    float fx = (x % Step) / (float)Step;
                    int i00 = gy * gw + gx;
                    float p = Mathf.Lerp(Mathf.Lerp(cloudP[i00], cloudP[i00 + 1], fx), Mathf.Lerp(cloudP[i00 + gw], cloudP[i00 + gw + 1], fx), fy);
                    float bl = Mathf.Lerp(Mathf.Lerp(cloudB[i00], cloudB[i00 + 1], fx), Mathf.Lerp(cloudB[i00 + gw], cloudB[i00 + gw + 1], fx), fy);
                    var c = sky + purple * p + blue * bl;
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            }

            var rng = new System.Random(20261017);
            void Add(int x, int y, Color k)
            {
                var o = px[y * W + x];
                px[y * W + x] = new Color32((byte)Mathf.Min(255, o.r + (int)(k.r * 255f)), (byte)Mathf.Min(255, o.g + (int)(k.g * 255f)), (byte)Mathf.Min(255, o.b + (int)(k.b * 255f)), 255);
            }
            void Dot(float cx, float cy, float radius, float bright, Color tint)
            {
                int r = Mathf.CeilToInt(radius * 3f);
                for (int y = (int)cy - r; y <= (int)cy + r; y++)
                    for (int x = (int)cx - r; x <= (int)cx + r; x++)
                    {
                        if (x < 0 || y < 0 || x >= W || y >= H) continue;
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        Add(x, y, tint * (Mathf.Exp(-(dx * dx + dy * dy) / (radius * radius)) * bright));
                    }
            }
            for (int i = 0; i < 1100; i++)
            {
                float b = (float)rng.NextDouble();
                b = b * b * b;
                var tint = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.9f, 0.75f), (float)rng.NextDouble());
                Dot((float)rng.NextDouble() * W, (float)rng.NextDouble() * H, 0.8f + b * 1.7f, 0.35f + b * 0.9f, tint);
            }
            // faint constellations: a few bright stars joined by thin lines
            for (int g = 0; g < 5; g++)
            {
                float gx = 60f + (float)rng.NextDouble() * (W - 120f), gy = 80f + (float)rng.NextDouble() * (H - 160f);
                var pts = new Vector2[4 + rng.Next(3)];
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = new Vector2(gx + ((float)rng.NextDouble() - 0.5f) * 225f, gy + ((float)rng.NextDouble() - 0.5f) * 225f);
                for (int i = 1; i < pts.Length; i++)
                {
                    int steps = Mathf.CeilToInt(Vector2.Distance(pts[i - 1], pts[i]));
                    for (int s = 0; s <= steps; s++)
                    {
                        var q = Vector2.Lerp(pts[i - 1], pts[i], s / (float)steps);
                        int qx = (int)q.x, qy = (int)q.y;
                        if (qx >= 0 && qy >= 0 && qx < W && qy < H) Add(qx, qy, new Color(0.07f, 0.084f, 0.126f));
                    }
                }
                foreach (var q in pts) Dot(q.x, q.y, 1.8f, 1.1f, new Color(0.85f, 0.9f, 1f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

    }
}
