using UnityEngine;
using Random = System.Random;

namespace SquashBot.Visual
{
    /// <summary>
    /// The perfect city's skylines (level 101 on): a pale far skyline, slender pearl towers with glass bands along
    /// the sides (rounded tops, needle spires or terraces of green), sky bridges between them, floating islands,
    /// flying cars trailing light and hologram rings. At night the towers go dark and the windows and rings glow.
    /// </summary>
    public static partial class BackgroundArt
    {
        private enum CityStyle { Day, Spires, Garden, Night }

        private static void UtopiaCity(Canvas c, Canvas s, Random rng, CityStyle style)
        {
            bool night = style == CityStyle.Night;
            var pearl = night ? Color.Lerp(c.near, c.t.bgBottom, 0.3f) : Color.Lerp(Color.white, c.t.bgGlow, 0.25f);
            var shade = night ? Color.Lerp(c.near, Color.black, 0.2f) : Color.Lerp(pearl, c.t.bgTop, 0.25f);
            var glass = night ? c.t.accent : Color.Lerp(c.t.accent, c.t.bgTop, 0.35f);
            var gold = new Color(1f, 0.85f, 0.5f);
            var green = new Color(0.45f, 0.8f, 0.5f);

            // Sky: the sun or the stars, floating islands high up, flying cars.
            if (night) Stars(s, rng, 70, 0.5f);
            else Sun(s, 0.82f, 0.86f, 0.065f);
            for (int i = 0; i < 4; i++)
            {
                float x = c.w * (i % 2 == 0 ? 0.06f + (float)rng.NextDouble() * 0.22f : 0.72f + (float)rng.NextDouble() * 0.22f);
                float y = c.h * (0.66f + (float)rng.NextDouble() * 0.2f), r = c.m * (0.035f + (float)rng.NextDouble() * 0.03f);
                Tri(s, x - r, y, x + r, y, x, y - r * 1.4f, shade, 0.75f);
                Ellipse(s, x, y, r * 1.1f, r * 0.28f, pearl, 0.9f);
                if (style == CityStyle.Garden || rng.Next(2) == 0) Ellipse(s, x - r * 0.3f, y + r * 0.35f, r * 0.45f, r * 0.35f, green, 0.85f);
                Ellipse(s, x + r * 0.35f, y + r * 0.25f, r * 0.3f, r * 0.3f, glass, 0.7f);
                Dot(s, x, y - r * 1.3f, r * 0.3f, c.t.accent, night ? 0.5f : 0.25f);
            }
            for (int i = 0; i < 8; i++)
            {
                float x = c.w * (float)rng.NextDouble(), y = c.h * (0.55f + (float)rng.NextDouble() * 0.35f), u = c.m * 0.009f;
                float dir = rng.Next(2) == 0 ? -1f : 1f;
                Stroke(s, x, y, x - dir * u * 9f, y - u * 0.6f, 1.4f, c.t.accent, night ? 0.6f : 0.4f);
                Ellipse(s, x, y, u * 1.8f, u * 0.7f, night ? c.t.bgStar : pearl, 0.95f);
                Dot(s, x + dir * u * 1.2f, y, u * 0.4f, c.t.accent, 0.9f);
            }

            // The far skyline across the whole width.
            float far = c.h * 0.02f;
            for (float x = 0f; x < c.w;)
            {
                float tw = c.w * (0.018f + (float)rng.NextDouble() * 0.025f), th = c.h * (0.08f + (float)rng.NextDouble() * 0.22f);
                Rect(c, x, far, x + tw, far + th, c.far, night ? 0.55f : 0.4f);
                if (rng.Next(3) == 0) Tri(c, x, far + th, x + tw, far + th, x + tw * 0.5f, far + th + tw * 1.4f, c.far, night ? 0.55f : 0.4f);
                if (night) for (float y = far + c.h * 0.02f; y < far + th; y += c.h * 0.025f) if (rng.Next(3) == 0) Rect(c, x + tw * 0.3f, y, x + tw * 0.7f, y + 2f, c.t.accent, 0.35f);
                x += tw + c.w * (0.004f + (float)rng.NextDouble() * 0.01f);
            }

            // The near towers along both sides.
            var tops = new System.Collections.Generic.List<(float x, float w, float top)>();
            for (int i = 0; i < 10; i++)
            {
                bool left = i < 5;
                float x = c.w * (left ? 0.005f + (i % 5) * 0.058f : 0.71f + (i % 5) * 0.058f) + (float)rng.NextDouble() * c.w * 0.01f;
                float tw = c.w * (0.034f + (float)rng.NextDouble() * 0.014f);
                float th = c.h * (0.2f + (float)rng.NextDouble() * 0.32f);
                Rect(c, x, 0f, x + tw, th, pearl, 0.95f);
                Rect(c, x + tw * 0.78f, 0f, x + tw, th, shade, 0.6f); // the shaded side
                // Glass bands and windows.
                for (float y = c.h * 0.03f; y < th - c.h * 0.02f; y += c.h * 0.034f)
                {
                    Rect(c, x + tw * 0.12f, y, x + tw * 0.78f, y + c.h * 0.012f, glass, night ? 0.75f : 0.5f);
                    if (night && rng.Next(2) == 0) Rect(c, x + tw * (0.15f + (float)rng.NextDouble() * 0.45f), y + 1f, x + tw * 0.8f, y + 3f, c.t.bgStar, 0.6f);
                }
                Rect(c, x + tw * 0.48f, 0f, x + tw * 0.52f, th, night ? c.t.accent : gold, night ? 0.6f : 0.5f); // a seam of light
                switch (style)
                {
                    case CityStyle.Spires:
                        Tri(c, x, th, x + tw, th, x + tw * 0.5f, th + tw * 2.6f, pearl, 0.95f);
                        Stroke(c, x + tw * 0.5f, th + tw * 2.6f, x + tw * 0.5f, th + tw * 3.3f, 1.4f, gold, 0.8f);
                        Dot(c, x + tw * 0.5f, th + tw * 3.3f, tw * 0.12f, c.t.accent, 0.9f);
                        break;
                    case CityStyle.Garden:
                        Ellipse(c, x + tw * 0.5f, th, tw * 0.62f, tw * 0.22f, pearl, 0.95f);
                        for (int k = 0; k < 3; k++)
                            Ellipse(c, x + tw * (0.2f + k * 0.3f), th + tw * 0.25f, tw * 0.26f, tw * 0.3f, green, 0.9f);
                        for (float y = c.h * 0.08f; y < th; y += c.h * 0.09f)
                            Ellipse(c, x + (rng.Next(2) == 0 ? 0f : tw), y, tw * 0.22f, tw * 0.14f, green, 0.85f);
                        break;
                    default:
                        Ellipse(c, x + tw * 0.5f, th, tw * 0.5f, tw * 0.5f, pearl, 0.95f);
                        Ellipse(c, x + tw * 0.5f, th + tw * 0.05f, tw * 0.3f, tw * 0.22f, glass, 0.8f);
                        break;
                }
                tops.Add((x, tw, th));
            }
            // Sky bridges between neighbouring towers, and hologram rings over a few.
            for (int i = 0; i + 1 < tops.Count; i++)
            {
                if (i == 4 || rng.Next(3) == 0) continue;
                var a = tops[i];
                var b = tops[i + 1];
                float y = Mathf.Min(a.top, b.top) * (0.55f + (float)rng.NextDouble() * 0.3f);
                Rect(c, a.x + a.w, y, b.x, y + c.h * 0.008f, pearl, 0.95f);
                Rect(c, a.x + a.w, y + c.h * 0.008f, b.x, y + c.h * 0.011f, glass, 0.7f);
            }
            for (int i = 0; i < tops.Count; i++)
            {
                if (rng.Next(3) != 0) continue;
                var tw = tops[i];
                float cx = tw.x + tw.w * 0.5f, cy = tw.top + tw.w * (style == CityStyle.Spires ? 3.8f : 1.4f);
                Ring(c, cx, cy, tw.w * 0.9f, 1.4f, c.t.accent, night ? 0.6f : 0.35f);
                Ring(c, cx, cy, tw.w * 0.55f, 1f, c.t.accent, night ? 0.5f : 0.3f);
                Dot(c, cx, cy, tw.w * 0.3f, c.t.accent, night ? 0.25f : 0.12f);
            }
        }
    }
}
