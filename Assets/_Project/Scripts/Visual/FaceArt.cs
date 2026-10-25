using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The faces' drawings, made in code: for a head, a mouth and eyes (and a sweat drop) drawn in its face rectangle
    /// (the rectangle its face map points into), as a small picture the face's paint lays over its own — a grin with
    /// teeth, a sly smile, a mouth wide open, a pout, clenched teeth, a tongue out, a wobble, a cry; spiral, crossed or
    /// wide eyes, tears. Each drawn once per head and kept.
    /// </summary>
    public static class FaceArt
    {
        [System.Serializable] private class FaceInfo { public float x0 = -0.32f, x1 = 0.32f, z0 = 0.12f, z1 = 0.68f, mouthX, mouthZ = 0.26f, mouthW = 0.24f; }
        [System.Serializable] private class Eye { public float x, y, z, w, h; }
        [System.Serializable] private class HeadEyes { public Eye[] eyes; }

        private const int Size = 256;
        private static readonly Dictionary<string, Texture2D> drawn = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, (FaceInfo face, Eye[] eyes)> infos = new Dictionary<string, (FaceInfo, Eye[])>();
        private static readonly Dictionary<string, Texture2D> maps = new Dictionary<string, Texture2D>();

        private static readonly Color Dark = new Color(0.26f, 0.05f, 0.06f), Teeth = new Color(0.97f, 0.95f, 0.9f), Tongue = new Color(0.92f, 0.4f, 0.42f);
        private static readonly Color Lip = new Color(0.45f, 0.17f, 0.14f), Pink = new Color(0.86f, 0.52f, 0.5f), White = new Color(0.98f, 0.98f, 0.97f), Ink = new Color(0.12f, 0.1f, 0.12f), Tear = new Color(0.45f, 0.75f, 1f);

        /// <summary>The head's face map (where each texel lies in the face rectangle), or null if it has none.</summary>
        public static Texture2D Map(string headFile)
        {
            if (maps.TryGetValue(headFile, out var m)) return m;
            m = Resources.Load<Texture2D>("Heroes/Foi/" + headFile + "_facemap");
            maps[headFile] = m;
            return m;
        }

        private static (FaceInfo face, Eye[] eyes) Info(string headFile)
        {
            if (infos.TryGetValue(headFile, out var i)) return i;
            var f = Resources.Load<TextAsset>("Heroes/Foi/" + headFile + "_face");
            var h = Resources.Load<TextAsset>("Heroes/Foi/" + headFile);
            var face = f != null ? JsonUtility.FromJson<FaceInfo>(f.text) : new FaceInfo();
            var eyes = h != null ? JsonUtility.FromJson<HeadEyes>(h.text)?.eyes : null;
            i = (face, eyes ?? new Eye[0]);
            infos[headFile] = i;
            return i;
        }

        /// <summary>The drawing for a head: a mouth, eyes, a sweat drop (<paramref name="frame"/>: the chatter's second picture).</summary>
        public static Texture2D Get(string headFile, MouthShape mouth, EyeShape eyes, bool sweat, int frame = 0)
        {
            string key = headFile + mouth + eyes + sweat + frame;
            if (drawn.TryGetValue(key, out var t) && t != null) return t;
            var (face, eyeInfo) = Info(headFile);
            var px = new Color32[Size * Size];
            float sx = (face.x1 - face.x0) / Size, sz = (face.z1 - face.z0) / Size;
            float pix = Mathf.Max(sx, sz);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(face.x0 + (x + 0.5f) * sx, face.z0 + (y + 0.5f) * sz);
                    var c = new Color(0f, 0f, 0f, 0f);
                    if (mouth != MouthShape.None) Mouth(ref c, p, mouth, face, frame, pix);
                    if (eyes != EyeShape.None) foreach (var e in eyeInfo) Eyes(ref c, p, eyes, e, pix);
                    if (sweat) Sweat(ref c, p, face, pix);
                    px[y * Size + x] = c;
                }
            t = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Face_" + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(false, true);
            drawn[key] = t;
            return t;
        }

        // ---------- Painting by distance (negative inside) ----------

        private static void Over(ref Color c, Color col, float d, float pix)
        {
            float a = Mathf.Clamp01(0.5f - d / pix) * col.a;
            if (a <= 0f) return;
            float outA = a + c.a * (1f - a);
            c = new Color((col.r * a + c.r * c.a * (1f - a)) / Mathf.Max(1e-4f, outA), (col.g * a + c.g * c.a * (1f - a)) / Mathf.Max(1e-4f, outA), (col.b * a + c.b * c.a * (1f - a)) / Mathf.Max(1e-4f, outA), outA);
        }

        private static float Ellipse(Vector2 p, Vector2 c, float rx, float ry)
        {
            var q = new Vector2((p.x - c.x) / rx, (p.y - c.y) / ry);
            return (q.magnitude - 1f) * Mathf.Min(rx, ry);
        }

        /// <summary>A stroke along y = f(x) between x0 and x1, <paramref name="w"/> half thick.</summary>
        private static float Curve(Vector2 p, System.Func<float, float> f, float x0, float x1, float w)
        {
            float x = Mathf.Clamp(p.x, x0, x1);
            float dy = p.y - f(x);
            float dx = p.x - x;
            // the slope makes the vertical distance too big on steep parts: scale it back
            float slope = (f(x + 0.002f) - f(x - 0.002f)) / 0.004f;
            float d = Mathf.Abs(dy) / Mathf.Sqrt(1f + slope * slope);
            return Mathf.Sqrt(d * d + dx * dx) - w;
        }

        /// <summary>The region between two curves (top above bottom) from x0 to x1.</summary>
        private static float Between(Vector2 p, System.Func<float, float> top, System.Func<float, float> bottom, float x0, float x1)
        {
            float inX = Mathf.Max(x0 - p.x, p.x - x1);
            float inY = Mathf.Max(p.y - top(p.x), bottom(p.x) - p.y);
            return Mathf.Max(inX, inY);
        }

        private static void Mouth(ref Color c, Vector2 p, MouthShape shape, FaceInfo f, int frame, float pix)
        {
            float mx = f.mouthX, mz = f.mouthZ, hw = f.mouthW * 0.5f;
            float D(float x) => (x - mx) / hw; // -1 .. 1 across the mouth
            switch (shape)
            {
                case MouthShape.Grin:
                {
                    float w = hw * 1.05f;
                    System.Func<float, float> top = x => mz + 0.012f + 0.018f * D(x) * D(x);
                    System.Func<float, float> bot = x => mz - 0.05f + 0.06f * D(x) * D(x);
                    float d = Between(p, top, bot, mx - w, mx + w);
                    Over(ref c, Lip, d - 0.006f, pix);
                    Over(ref c, Dark, d, pix);
                    Over(ref c, Teeth, Mathf.Max(d, mz - 0.006f - p.y + 0.012f * D(p.x) * D(p.x)), pix);
                    Over(ref c, Tongue, Mathf.Max(d, Ellipse(p, new Vector2(mx, mz - 0.045f), hw * 0.45f, 0.022f)), pix);
                    break;
                }
                case MouthShape.Smile:
                    Over(ref c, Lip, Curve(p, x => mz - 0.006f + 0.03f * D(x) * D(x), mx - hw * 0.85f, mx + hw * 0.85f, 0.007f), pix);
                    break;
                case MouthShape.Sly:
                    Over(ref c, Lip, Curve(p, x => mz - 0.008f + 0.012f * D(x) + 0.03f * Mathf.Max(0f, D(x)) * Mathf.Max(0f, D(x)), mx - hw * 0.7f, mx + hw * 0.9f, 0.0075f), pix);
                    break;
                case MouthShape.Open:
                    Over(ref c, Lip, Ellipse(p, new Vector2(mx, mz - 0.015f), hw * 0.5f + 0.006f, 0.05f), pix);
                    Over(ref c, Dark, Ellipse(p, new Vector2(mx, mz - 0.015f), hw * 0.5f, 0.044f), pix);
                    Over(ref c, Tongue, Mathf.Max(Ellipse(p, new Vector2(mx, mz - 0.015f), hw * 0.5f, 0.044f), Ellipse(p, new Vector2(mx, mz - 0.05f), hw * 0.38f, 0.022f)), pix);
                    break;
                case MouthShape.Pout:
                    Over(ref c, Pink, Ellipse(p, new Vector2(mx, mz - 0.022f), hw * 0.38f, 0.014f), pix);
                    Over(ref c, Lip, Curve(p, x => mz + 0.004f - 0.026f * D(x) * D(x), mx - hw * 0.75f, mx + hw * 0.75f, 0.0085f), pix);
                    break;
                case MouthShape.Grit:
                case MouthShape.Chatter:
                {
                    float shift = shape == MouthShape.Chatter && frame == 1 ? 0.012f : 0f;
                    float w = hw * 0.78f, h = 0.022f;
                    var box = new Vector2(mx, mz - 0.012f + shift * 0.3f);
                    float d = RoundBox(p, box, w, h, 0.012f);
                    Over(ref c, Lip, d - 0.006f, pix);
                    Over(ref c, Teeth, d, pix);
                    float line = Mathf.Abs(p.y - box.y) - 0.0018f;
                    Over(ref c, new Color(0.55f, 0.5f, 0.5f), Mathf.Max(d, line), pix);
                    float gap = Mathf.Abs(Mathf.Repeat(p.x - mx + shift + 0.0125f, 0.025f) - 0.0125f) - 0.0012f;
                    Over(ref c, new Color(0.6f, 0.55f, 0.55f), Mathf.Max(d, gap), pix);
                    break;
                }
                case MouthShape.TongueOut:
                    Over(ref c, Lip, Ellipse(p, new Vector2(mx, mz - 0.01f), hw * 0.45f + 0.006f, 0.034f), pix);
                    Over(ref c, Dark, Ellipse(p, new Vector2(mx, mz - 0.01f), hw * 0.45f, 0.028f), pix);
                    Over(ref c, Tongue, Ellipse(p, new Vector2(mx + 0.008f, mz - 0.05f), hw * 0.3f, 0.045f), pix);
                    Over(ref c, new Color(0.75f, 0.25f, 0.3f), Mathf.Max(Ellipse(p, new Vector2(mx + 0.008f, mz - 0.05f), hw * 0.3f, 0.045f), Mathf.Abs(p.x - mx - 0.008f) - 0.002f), pix);
                    break;
                case MouthShape.Wavy:
                    Over(ref c, Lip, Curve(p, x => mz - 0.004f + 0.009f * Mathf.Sin(D(x) * 5.5f), mx - hw * 0.7f, mx + hw * 0.7f, 0.007f), pix);
                    break;
                case MouthShape.Cry:
                {
                    System.Func<float, float> top = x => mz + 0.008f - 0.03f * D(x) * D(x) + 0.004f * Mathf.Sin(D(x) * 9f);
                    System.Func<float, float> bot = x => mz - 0.035f - 0.01f * D(x) * D(x);
                    float d = Between(p, top, bot, mx - hw * 0.6f, mx + hw * 0.6f);
                    Over(ref c, Lip, d - 0.005f, pix);
                    Over(ref c, Dark, d, pix);
                    break;
                }
            }
        }

        private static float RoundBox(Vector2 p, Vector2 c, float hx, float hy, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x) - hx + r, Mathf.Abs(p.y - c.y) - hy + r);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        private static void Eyes(ref Color c, Vector2 p, EyeShape shape, Eye e, float pix)
        {
            var ec = new Vector2(e.x, e.z);
            float r = Mathf.Max(e.w, e.h) * 0.52f;
            switch (shape)
            {
                case EyeShape.Spiral:
                {
                    Over(ref c, White, Ellipse(p, ec, r, r), pix);
                    var q = p - ec;
                    float ang = Mathf.Atan2(q.y, q.x) * (e.x < 0f ? 1f : -1f);
                    float rr = q.magnitude;
                    float turn = r / 2.6f;
                    float k = (rr / turn - ang / (Mathf.PI * 2f)) % 1f;
                    if (k < 0f) k += 1f;
                    float d = (Mathf.Abs(k - 0.5f) - 0.2f) * turn;
                    Over(ref c, Ink, Mathf.Max(d, rr - r * 0.92f), pix);
                    break;
                }
                case EyeShape.Crossed:
                    Over(ref c, White, Ellipse(p, ec, r, r * 0.95f), pix);
                    Over(ref c, Ink, Ellipse(p, ec + new Vector2(-Mathf.Sign(e.x) * r * 0.45f, 0f), r * 0.32f, r * 0.32f), pix);
                    break;
                case EyeShape.Wide:
                    Over(ref c, White, Ellipse(p, ec + new Vector2(0f, r * 0.08f), r * 1.12f, r * 1.12f), pix);
                    Over(ref c, Ink, Ellipse(p, ec + new Vector2(0f, r * 0.08f), r * 0.24f, r * 0.24f), pix);
                    break;
                case EyeShape.Tears:
                    Over(ref c, Tear, Ellipse(p, ec + new Vector2(0f, -e.h * 0.95f), e.w * 0.16f, e.h * 0.55f), pix);
                    Over(ref c, Tear, Ellipse(p, ec + new Vector2(0f, -e.h * 1.7f), e.w * 0.13f, e.h * 0.2f), pix);
                    break;
            }
        }

        private static void Sweat(ref Color c, Vector2 p, FaceInfo f, float pix)
        {
            var at = new Vector2(f.x1 - 0.07f, f.z1 - 0.1f);
            float drop = Mathf.Min(Ellipse(p, at, 0.022f, 0.028f), Ellipse(p, at + new Vector2(0f, 0.025f), 0.009f, 0.02f));
            Over(ref c, new Color(0.55f, 0.82f, 1f, 0.95f), drop, pix);
            Over(ref c, new Color(1f, 1f, 1f, 0.9f), Ellipse(p, at + new Vector2(-0.008f, 0.004f), 0.005f, 0.007f), pix);
        }
    }
}
