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
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[Size * Size];
            var paper = new Color(0.95f, 0.87f, 0.69f);
            var burnt = new Color(0.62f, 0.43f, 0.22f);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    // a slightly ragged outline, darker toward it
                    float rag = (Mathf.PerlinNoise(fx * 0.21f, fy * 0.21f) - 0.5f) * 2.4f;
                    float d = ChamferDistance(fx, fy) + rag;
                    float burn = Mathf.Clamp01(1f + d / 13f);
                    float mottle = (Mathf.PerlinNoise(fx * 0.06f + 5f, fy * 0.06f + 9f) - 0.5f) * 0.07f;
                    var c = Color.Lerp(paper, burnt, burn * burn) + new Color(mottle, mottle, mottle * 0.8f, 0f);
                    c.a = Mathf.Clamp01(0.5f - d);
                    px[y * Size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder));
        }

        private enum FrameKind { Plate, Bevel, Rim }

        /// <summary>Signed distance to the cut-corner rectangle inset by <see cref="FramePad"/> (negative inside).</summary>
        private static float ChamferDistance(float x, float y)
        {
            float half = Size * 0.5f - FramePad;
            float px = Mathf.Abs(x - Size * 0.5f), py = Mathf.Abs(y - Size * 0.5f);
            return Mathf.Max(Mathf.Max(px - half, py - half), (px + py - (2f * half - Chamfer)) * 0.70710678f);
        }

        /// <summary>How much of a band of distances [from, to] covers this pixel (soft edges).</summary>
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
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[Size * Size];
            float half = Size * 0.5f - FramePad;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float d = ChamferDistance(fx, fy);
                    float t = y / (float)(Size - 1);
                    Color c;
                    if (kind == FrameKind.Plate)
                    {
                        float g = Mathf.Lerp(0.82f, 1f, Mathf.SmoothStep(0f, 1f, t));
                        c = new Color(g, g, g, Mathf.Clamp01(0.5f - d));
                    }
                    else if (kind == FrameKind.Bevel)
                    {
                        // A raised face: the body over a gradient, a lit band along the upper inner edge and a shaded
                        // band along the lower one, and a faint sheen over the upper half.
                        float g = Mathf.Lerp(0.66f, 0.9f, Mathf.SmoothStep(0f, 1f, t));
                        float edge = Band(d, -7f, 0f);
                        g = Mathf.Lerp(g, t > 0.5f ? 1f : 0.48f, edge * 0.85f);
                        if (t > 0.55f && d < -7f) g = Mathf.Min(1f, g + 0.08f);
                        c = new Color(g, g, g, Mathf.Clamp01(0.5f - d));
                    }
                    else
                    {
                        // a dark outline, a broad gold line, a gap, a fine gold line; diamonds at the four inner corners
                        float outline = Band(d, 0f, 1.6f) * 0.75f;
                        float outer = Band(d, -4.6f, 0f);
                        float inner = Band(d, -9.6f, -7.6f);
                        float shine = Mathf.Clamp01(1f - Mathf.Abs((d + 2.3f) / 2.3f));
                        float ax = Mathf.Abs(fx - Size * 0.5f), ay = Mathf.Abs(fy - Size * 0.5f);
                        float dia = Mathf.Abs(ax - (half - 12f)) + Mathf.Abs(ay - (half - 12f));
                        float diamond = Mathf.Clamp01(5.2f - dia);
                        float a = Mathf.Max(outer, Mathf.Max(inner * 0.9f, diamond));
                        if (a > 0f)
                        {
                            var g = outer >= inner || diamond > inner ? GoldAt(t, shine) : GoldAt(t, 0f) * 0.92f;
                            c = new Color(g.r, g.g, g.b, a);
                        }
                        else c = new Color(0.06f, 0.035f, 0.01f, outline);
                    }
                    px[y * Size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder));
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
                    float outer = Band(d, -9f, 0f), inner = Band(d, -18f, -14.5f), outline = Band(d, 0f, 2.5f) * 0.75f;
                    float t = y / (float)(S - 1);
                    float shine = Mathf.Clamp01(1f - Mathf.Abs((d + 4.5f) / 4.5f));
                    float a = Mathf.Max(outer, inner * 0.9f);
                    var g = a > 0f ? (inner > outer ? GoldAt(t, 0f) * 0.92f : GoldAt(t, shine)) : new Color(0.06f, 0.035f, 0.01f);
                    px[y * S + x] = new Color(g.r, g.g, g.b, a > 0f ? a : outline);
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
            const int W = 540, H = 1170;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[W * H];
            var top = new Color(0.035f, 0.045f, 0.13f);
            var bottom = new Color(0.015f, 0.015f, 0.055f);
            var purple = new Color(0.42f, 0.14f, 0.55f);
            var blue = new Color(0.1f, 0.3f, 0.62f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    var c = Color.Lerp(bottom, top, v);
                    // a band of cloud running corner to corner: purple, with blue drifts through it
                    float diag = Mathf.Abs((u - 0.5f) * 0.9f + (v - 0.5f) * 0.55f);
                    float band = Mathf.Clamp01(1f - diag * 2.4f);
                    float n1 = Fbm(u * 3.2f + 11f, v * 6.4f + 3f), n2 = Fbm(u * 2.6f + 41f, v * 5.2f + 17f);
                    float p = Mathf.Pow(Mathf.Clamp01(n1 * 1.6f - 0.45f), 1.6f) * (0.35f + band);
                    float b = Mathf.Pow(Mathf.Clamp01(n2 * 1.6f - 0.5f), 1.6f) * (0.25f + band * 0.8f);
                    c += purple * p * 0.55f + blue * b * 0.5f;
                    c.a = 1f;
                    px[y * W + x] = c;
                }

            var rng = new System.Random(20261017);
            void Dot(float cx, float cy, float radius, float bright, Color tint)
            {
                int r = Mathf.CeilToInt(radius * 3f);
                for (int y = (int)cy - r; y <= (int)cy + r; y++)
                    for (int x = (int)cx - r; x <= (int)cx + r; x++)
                    {
                        if (x < 0 || y < 0 || x >= W || y >= H) continue;
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        var k = tint * (Mathf.Exp(-(dx * dx + dy * dy) / (radius * radius)) * bright);
                        k.a = 0f;
                        px[y * W + x] += k;
                    }
            }
            for (int i = 0; i < 1100; i++)
            {
                float b = (float)rng.NextDouble();
                b = b * b * b;
                var tint = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.9f, 0.75f), (float)rng.NextDouble());
                Dot((float)rng.NextDouble() * W, (float)rng.NextDouble() * H, 0.6f + b * 1.3f, 0.35f + b * 0.9f, tint);
            }
            // faint constellations: a few bright stars joined by thin lines
            for (int g = 0; g < 5; g++)
            {
                float gx = 60f + (float)rng.NextDouble() * (W - 120f), gy = 80f + (float)rng.NextDouble() * (H - 160f);
                var pts = new Vector2[4 + rng.Next(3)];
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = new Vector2(gx + ((float)rng.NextDouble() - 0.5f) * 170f, gy + ((float)rng.NextDouble() - 0.5f) * 170f);
                for (int i = 1; i < pts.Length; i++)
                {
                    int steps = Mathf.CeilToInt(Vector2.Distance(pts[i - 1], pts[i]));
                    for (int s = 0; s <= steps; s++)
                    {
                        var q = Vector2.Lerp(pts[i - 1], pts[i], s / (float)steps);
                        int qx = (int)q.x, qy = (int)q.y;
                        if (qx >= 0 && qy >= 0 && qx < W && qy < H) px[qy * W + qx] += new Color(0.07f, 0.084f, 0.126f, 0f);
                    }
                }
                foreach (var q in pts) Dot(q.x, q.y, 1.8f, 1.1f, new Color(0.85f, 0.9f, 1f));
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
