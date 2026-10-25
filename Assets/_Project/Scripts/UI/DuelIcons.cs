using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>
    /// The duel buttons' icons, drawn in code (no picture files): each is a shape described by its distance field
    /// (negative inside), painted white with a soft dark rim and shadow so it reads on any button colour. Made once per
    /// icon and kept.
    /// </summary>
    public static class DuelIcons
    {
        private const int Size = 160;
        private static readonly Dictionary<DuelIcon, Sprite> made = new Dictionary<DuelIcon, Sprite>();

        public static Sprite Get(DuelIcon icon)
        {
            if (made.TryGetValue(icon, out var s) && s != null) return s;
            s = Draw(Shape(icon));
            s.name = "DuelIcon_" + icon;
            made[icon] = s;
            return s;
        }

        private static Sprite ring;

        /// <summary>A plain round ring (filled radially round a button: its power, durability or time left).</summary>
        public static Sprite Ring
        {
            get
            {
                if (ring != null) return ring;
                const int N = 256;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float r = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f).magnitude;
                        float d = Mathf.Abs(r - 0.92f) - 0.07f; // a band near the edge
                        float a = Mathf.Clamp01(0.5f - d * N * 0.5f);
                        px[y * N + x] = new Color(1f, 1f, 1f, a);
                    }
                tex.SetPixels32(px);
                tex.Apply(true, true);
                ring = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
                return ring;
            }
        }

        /// <summary>The icon of a special move.</summary>
        public static DuelIcon ForSpecial(Data.SpecialKind k) => k switch
        {
            Data.SpecialKind.Backflip => DuelIcon.SpecialBackflip,
            Data.SpecialKind.Hook => DuelIcon.SpecialHook,
            Data.SpecialKind.Shock => DuelIcon.SpecialShock,
            Data.SpecialKind.IceWall => DuelIcon.SpecialIceWall,
            Data.SpecialKind.Patch => DuelIcon.SpecialPatch,
            Data.SpecialKind.SkyStrike => DuelIcon.SpecialSkyStrike,
            Data.SpecialKind.Teleport => DuelIcon.SpecialTeleport,
            _ => DuelIcon.SpecialFly,
        };

        private static Sprite Draw(Func<Vector2, float> sdf)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[Size * Size];
            float pix = 2f / Size; // one pixel in the shape's units
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                    float d = sdf(p);
                    float shadow = sdf(p + new Vector2(-0.035f, 0.05f)); // a drop shadow below and to the right
                    float glyph = Mathf.Clamp01(0.5f - d / pix);
                    float rim = Mathf.Clamp01(0.5f - (d - 0.055f) / pix);
                    float shade = Mathf.Clamp01(0.5f - (shadow - 0.07f) / (pix * 6f)) * 0.45f;
                    // white glyph, a dark rim round it, a soft shadow under that
                    float a = Mathf.Max(glyph, Mathf.Max(rim * 0.85f, shade));
                    float white = a > 0f ? glyph / a : 0f;
                    var c = Color.Lerp(new Color(0.08f, 0.06f, 0.14f), Color.white, white);
                    px[y * Size + x] = new Color(c.r, c.g, c.b, a);
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---------- The shapes ----------

        private static Func<Vector2, float> Shape(DuelIcon icon)
        {
            switch (icon)
            {
                case DuelIcon.Shield:
                    return p =>
                    {
                        float outer = Poly(p, ShieldPts, 0.72f) - 0.05f;
                        float inner = Poly(p, ShieldPts, 0.46f) - 0.04f;
                        float boss = Circle(p - new Vector2(0f, 0.06f), 0.13f);
                        return Mathf.Min(Mathf.Max(outer, -inner), boss); // a rim and a boss in the middle
                    };
                case DuelIcon.Ulti:
                    return p => Mathf.Min(Star(p, 8, 0.82f, 0.38f) - 0.03f, Circle(p, 0.3f));
                case DuelIcon.Heart:
                    return p => Heart(p * 1.15f + new Vector2(0f, 0.12f)) / 1.15f;
                case DuelIcon.Heavy:
                    return p =>
                    {
                        float burst = Star(p, 10, 0.85f, 0.55f) - 0.02f;
                        float hole = Star(p, 10, 0.55f, 0.36f);
                        float fist = Box(p, new Vector2(0.22f, 0.22f), 0.08f);
                        return Mathf.Min(Mathf.Max(burst, -hole), fist);
                    };
                case DuelIcon.Upper:
                    return p =>
                    {
                        float head = Poly(p, new[] { new Vector2(-0.55f, 0.05f), new Vector2(0f, 0.75f), new Vector2(0.55f, 0.05f) }, 1f) - 0.04f;
                        float shaft = Box(p - new Vector2(0f, -0.3f), new Vector2(0.18f, 0.4f), 0.05f);
                        float lines = Mathf.Min(Seg(p, new Vector2(-0.6f, -0.35f), new Vector2(-0.6f, -0.75f)), Seg(p, new Vector2(0.6f, -0.35f), new Vector2(0.6f, -0.75f))) - 0.06f;
                        return Mathf.Min(Mathf.Min(head, shaft), lines);
                    };
                case DuelIcon.Breaker:
                case DuelIcon.SpecialShock:
                    return p => Poly(p, BoltPts, 1f) - 0.03f;
                case DuelIcon.Finish:
                    return p =>
                    {
                        float big = Star(p - new Vector2(-0.12f, -0.1f), 4, 0.72f, 0.17f) - 0.02f;
                        float small = Star(p - new Vector2(0.5f, 0.5f), 4, 0.3f, 0.08f) - 0.01f;
                        return Mathf.Min(big, small);
                    };
                case DuelIcon.Jump:
                    return p => Mathf.Min(Chevron(p - new Vector2(0f, 0.25f)), Chevron(p - new Vector2(0f, -0.25f)));
                case DuelIcon.Pause:
                    return p => Mathf.Min(Box(p - new Vector2(-0.28f, 0f), new Vector2(0.15f, 0.55f), 0.07f), Box(p - new Vector2(0.28f, 0f), new Vector2(0.15f, 0.55f), 0.07f));
                case DuelIcon.Emote:
                    return p =>
                    {
                        float bubble = Mathf.Min(Box(p - new Vector2(0f, 0.12f), new Vector2(0.7f, 0.46f), 0.28f),
                            Poly(p, new[] { new Vector2(-0.4f, -0.2f), new Vector2(-0.55f, -0.72f), new Vector2(-0.05f, -0.25f) }, 1f));
                        float dots = Mathf.Min(Circle(p - new Vector2(-0.32f, 0.12f), 0.1f), Mathf.Min(Circle(p - new Vector2(0f, 0.12f), 0.1f), Circle(p - new Vector2(0.32f, 0.12f), 0.1f)));
                        return Mathf.Max(bubble, -dots);
                    };
                case DuelIcon.Taunt:
                    return p =>
                    {
                        float face = Circle(p, 0.75f);
                        float eyes = Mathf.Min(Circle(p - new Vector2(-0.26f, 0.2f), 0.1f), Circle(p - new Vector2(0.26f, 0.2f), 0.1f));
                        float grin = Arc(p - new Vector2(0f, 0.05f), 0.42f, 0.08f, -150f, -30f);
                        float tongue = Circle(p - new Vector2(0.1f, -0.42f), 0.13f);
                        return Mathf.Max(face, -Mathf.Min(Mathf.Min(eyes, grin), tongue));
                    };
                case DuelIcon.SpecialBackflip:
                    return p =>
                    {
                        float loop = Arc(p, 0.55f, 0.13f, -40f, 250f);
                        var tip = Rot(new Vector2(0.55f, 0f), -40f);
                        float head = Poly(p - tip, new[] { new Vector2(-0.3f, 0.12f), new Vector2(0.3f, 0.12f), new Vector2(0f, -0.32f) }, 1f) - 0.02f;
                        return Mathf.Min(loop, head);
                    };
                case DuelIcon.SpecialHook:
                    return p =>
                    {
                        float shank = Seg(p, new Vector2(0.25f, 0.75f), new Vector2(0.25f, -0.2f)) - 0.12f;
                        float bend = Arc(p - new Vector2(-0.05f, -0.2f), 0.3f, 0.12f, 180f, 360f);
                        float barb = Poly(p, new[] { new Vector2(-0.5f, -0.05f), new Vector2(-0.18f, -0.1f), new Vector2(-0.38f, 0.25f) }, 1f) - 0.02f;
                        float eye = Mathf.Abs(Circle(p - new Vector2(0.25f, 0.75f), 0.13f)) - 0.06f;
                        return Mathf.Min(Mathf.Min(shank, bend), Mathf.Min(barb, eye));
                    };
                case DuelIcon.SpecialIceWall:
                    return p =>
                    {
                        float d = 9f;
                        for (int i = 0; i < 3; i++)
                        {
                            var a = Rot(new Vector2(0f, 0.78f), i * 60f);
                            d = Mathf.Min(d, Seg(p, a, -a) - 0.08f);
                            foreach (float s in new[] { 1f, -1f })
                            {
                                var mid = a * 0.55f * s;
                                d = Mathf.Min(d, Seg(p, mid, mid + Rot(a * 0.3f * s, 40f)) - 0.06f);
                                d = Mathf.Min(d, Seg(p, mid, mid + Rot(a * 0.3f * s, -40f)) - 0.06f);
                            }
                        }
                        return d;
                    };
                case DuelIcon.SpecialPatch:
                    return p => Mathf.Max(Mathf.Min(Box(p, new Vector2(0.22f, 0.7f), 0.08f), Box(p, new Vector2(0.7f, 0.22f), 0.08f)), -Circle(p, 0.09f));
                case DuelIcon.SpecialSkyStrike:
                    return p =>
                    {
                        float blade = Poly(p, new[] { new Vector2(-0.12f, 0.45f), new Vector2(0.12f, 0.45f), new Vector2(0.12f, -0.45f), new Vector2(0f, -0.8f), new Vector2(-0.12f, -0.45f) }, 1f) - 0.02f;
                        float guard = Box(p - new Vector2(0f, 0.5f), new Vector2(0.38f, 0.07f), 0.04f);
                        float grip = Box(p - new Vector2(0f, 0.7f), new Vector2(0.08f, 0.16f), 0.04f);
                        float speed = Mathf.Min(Seg(p, new Vector2(-0.5f, 0.15f), new Vector2(-0.5f, -0.5f)), Seg(p, new Vector2(0.5f, 0.15f), new Vector2(0.5f, -0.5f))) - 0.05f;
                        return Mathf.Min(Mathf.Min(blade, guard), Mathf.Min(grip, speed));
                    };
                case DuelIcon.SpecialTeleport:
                    return p => Mathf.Min(Arc(p, 0.62f, 0.1f, 20f, 160f), Mathf.Min(Arc(p, 0.62f, 0.1f, 200f, 340f), Mathf.Min(Arc(p, 0.32f, 0.09f, 100f, 260f), Circle(p, 0.12f))));
                default: // SpecialFly: a rocket
                    return p =>
                    {
                        var q = Rot(p, -35f);
                        float body = Box(q - new Vector2(0f, 0.05f), new Vector2(0.2f, 0.42f), 0.18f);
                        float nose = Poly(q, new[] { new Vector2(-0.2f, 0.4f), new Vector2(0f, 0.8f), new Vector2(0.2f, 0.4f) }, 1f);
                        float fins = Poly(q, new[] { new Vector2(-0.45f, -0.5f), new Vector2(0.45f, -0.5f), new Vector2(0.2f, -0.1f), new Vector2(-0.2f, -0.1f) }, 1f) - 0.02f;
                        float flame = Circle(q - new Vector2(0f, -0.68f), 0.13f);
                        float window = Circle(q - new Vector2(0f, 0.15f), 0.09f);
                        return Mathf.Max(Mathf.Min(Mathf.Min(body, nose), Mathf.Min(fins, flame)), -window);
                    };
            }
        }

        private static readonly Vector2[] ShieldPts =
        {
            new Vector2(-0.82f, 0.86f), new Vector2(0f, 1.02f), new Vector2(0.82f, 0.86f), new Vector2(0.82f, 0.05f),
            new Vector2(0.5f, -0.55f), new Vector2(0f, -1.05f), new Vector2(-0.5f, -0.55f), new Vector2(-0.82f, 0.05f),
        };

        private static readonly Vector2[] BoltPts =
        {
            new Vector2(0.15f, 0.85f), new Vector2(-0.45f, 0f), new Vector2(-0.05f, 0f), new Vector2(-0.25f, -0.85f),
            new Vector2(0.45f, 0.12f), new Vector2(0.05f, 0.12f),
        };

        // ---------- Distance helpers (negative inside) ----------

        private static float Circle(Vector2 p, float r) => p.magnitude - r;

        private static float Box(Vector2 p, Vector2 half, float round)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * round;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - round;
        }

        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-6f, Vector2.Dot(ba, ba)));
            return (pa - ba * h).magnitude;
        }

        /// <summary>A ring's piece between two angles (degrees, counter-clockwise from the right), <paramref name="w"/> half thick.</summary>
        private static float Arc(Vector2 p, float r, float w, float from, float to)
        {
            float a = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            float mid = (from + to) * 0.5f, half = (to - from) * 0.5f;
            float off = Mathf.Abs(Mathf.DeltaAngle(mid, a));
            if (off <= half) return Mathf.Abs(p.magnitude - r) - w;
            var e1 = Rot(new Vector2(r, 0f), from); var e2 = Rot(new Vector2(r, 0f), to);
            return Mathf.Min((p - e1).magnitude, (p - e2).magnitude) - w;
        }

        private static Vector2 Rot(Vector2 v, float deg)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static float Chevron(Vector2 p) => Mathf.Min(Seg(p, new Vector2(-0.55f, -0.22f), new Vector2(0f, 0.3f)), Seg(p, new Vector2(0.55f, -0.22f), new Vector2(0f, 0.3f))) - 0.13f;

        private static float Star(Vector2 p, int points, float outer, float inner)
        {
            var pts = new Vector2[points * 2];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = Rot(new Vector2(0f, i % 2 == 0 ? outer : inner), -i * 180f / points);
            return Poly(p, pts, 1f);
        }

        /// <summary>Inigo Quilez's heart.</summary>
        private static float Heart(Vector2 p)
        {
            p.x = Mathf.Abs(p.x);
            p.y += 0.6f;
            p *= 0.85f;
            if (p.y + p.x > 1f) return Mathf.Sqrt(Vector2.Dot(p - new Vector2(0.25f, 0.75f), p - new Vector2(0.25f, 0.75f))) - Mathf.Sqrt(2f) / 4f;
            var a = p - new Vector2(0f, 1f);
            float h = 0.5f * Mathf.Max(p.x + p.y, 0f);
            var b = p - new Vector2(h, h);
            return Mathf.Sqrt(Mathf.Min(Vector2.Dot(a, a), Vector2.Dot(b, b))) * Mathf.Sign(p.x - p.y);
        }

        /// <summary>Any polygon (scaled by <paramref name="scale"/>), Inigo Quilez's winding test.</summary>
        private static float Poly(Vector2 p, Vector2[] v, float scale)
        {
            p /= scale;
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d) * scale;
        }
    }
}
