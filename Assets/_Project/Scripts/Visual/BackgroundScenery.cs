using System;
using UnityEngine;
using Random = System.Random;

namespace SquashBot.Visual
{
    /// <summary>
    /// The landscapes painted into the backdrop: one per floor design (<see cref="Scenery"/>). They stay soft and low
    /// (along the bottom and the sides, around the platform), in the theme's own colours, so the play area keeps
    /// reading clearly.
    /// </summary>
    public static partial class BackgroundArt
    {
        /// <summary>The horizon, as a share of the screen height from the bottom (the platform covers what is below).</summary>
        private const float Horizon = 0.45f;

        private struct Canvas
        {
            public Color[] px;
            public int w, h;
            public float m;
            public WorldTheme t;
            public Color far, mid, near;
            /// <summary>Where the land starts: the landscape stands on a horizon behind the platform, not under it.</summary>
            public float g;
        }

        private static void PaintScenery(Color[] px, int w, int h, WorldTheme theme)
        {
            var c = new Canvas
            {
                px = px, w = w, h = h, m = Mathf.Min(w, h), t = theme,
                far = Color.Lerp(theme.bgGlow, theme.bgTop, 0.45f),
                mid = Color.Lerp(theme.bgBottom, theme.bgGlow, 0.5f),
                near = Color.Lerp(theme.bgBottom, Color.black, 0.18f),
                g = h * Horizon,
            };
            // Sky things (sun, moon, stars, lights high up) are drawn without the horizon offset.
            var s = c;
            s.g = 0f;
            var rng = new Random((int)theme.scenery * 97 + 13);
            switch (theme.scenery)
            {
                case Scenery.Pipes: Pipes(c, rng, false); break;
                case Scenery.PipesFire: Pipes(c, rng, true); break;
                case Scenery.Factory: Factory(c, rng, 0.75f); break;
                case Scenery.Gate: Factory(c, rng, 0.35f); Gate(c); break;
                case Scenery.Trees: Sun(s, 0.82f, 0.82f, 0.09f); Hills(c, rng); Trees(c, rng, false); break;
                case Scenery.CubeTrees: Hills(c, rng); Trees(c, rng, true); break;
                case Scenery.Flowers: Sun(s, 0.15f, 0.84f, 0.08f); Hills(c, rng); Flowers(c, rng); break;
                case Scenery.Ruins: Hills(c, rng); Ruins(c, rng); break;
                case Scenery.NightTrees: Stars(s, rng, 70, 0.35f); Moon(s); Pines(c, rng, 1f, false); break;
                case Scenery.Mushrooms: Stars(s, rng, 60, 0.4f); Hills(c, rng); Mushrooms(c, rng); break;
                case Scenery.Mesas: Sun(s, 0.8f, 0.8f, 0.08f); Mesas(c, rng, 1f); break;
                case Scenery.MesasStorm: Mesas(c, rng, 0.55f); Streaks(s, rng, c.t.bgLines, 0.16f, -0.25f, 140); break;
                case Scenery.Dunes: Sun(s, 0.85f, 0.85f, 0.07f); Dunes(c, 1f); break;
                case Scenery.DunesSun: Sun(s, 0.68f, 0.4f, 0.24f); Dunes(c, 1.1f); break;
                case Scenery.Arches: Arches(c, rng); break;
                case Scenery.Tablets: Tablets(c, rng); break;
                case Scenery.Harbor: Stars(s, rng, 30, 0.6f); Harbor(c, rng); break;
                case Scenery.HarborFog: Harbor(c, rng); Fog(s, c.t.bgGlow, 0.55f, 0.6f); break;
                case Scenery.Corals: Rays(s, rng); Seabed(c, rng); Corals(c, rng); Bubbles(s, rng); break;
                case Scenery.Jellyfish: Seabed(c, rng); Jellyfish(s, rng); Bubbles(s, rng); break;
                case Scenery.Server:
                case Scenery.ServerRed: Server(c, rng); Bubbles(s, rng); break;
                case Scenery.SnowHills: Sun(s, 0.8f, 0.82f, 0.07f); SnowHills(c, rng); break;
                case Scenery.Aurora: Stars(s, rng, 80, 0.3f); AuroraBands(s); SnowHills(c, rng); break;
                case Scenery.Pines: Mountains(c, rng); Pines(c, rng, 1f, true); break;
                case Scenery.PinesBlizzard: Mountains(c, rng); Pines(c, rng, 0.6f, true); Streaks(s, rng, Color.white, 0.22f, -0.5f, 220); Fog(s, c.t.bgGlow, 0.4f, 0.5f); break;
                case Scenery.Icicles: Icicles(s, rng); Shards(c, rng, c.t.bgLines, 0.5f); break;
                case Scenery.Towers: Towers(c, rng); Pines(c, rng, 0.5f, true); break;
                case Scenery.Shards: Shards(c, rng, c.t.accent, 0.75f); Glints(s, rng); break;
                case Scenery.Prisms: PrismRays(s); Shards(c, rng, c.t.bgLines, 0.7f); Glints(s, rng); break;
                case Scenery.Mirrors: Mirrors(c, rng, false); break;
                case Scenery.MirrorsDark: Mirrors(c, rng, true); break;
                case Scenery.AcidShards: Shards(c, rng, c.t.accent, 0.7f); Drips(c, rng); break;
                case Scenery.AcidPools: Pools(c, rng); Shards(c, rng, c.t.bgLines, 0.45f); break;
                case Scenery.Clouds: Sun(s, 0.82f, 0.84f, 0.07f); CloudBank(c, rng, 1f); break;
                case Scenery.Islands: CloudBank(c, rng, 0.7f); Islands(s, rng); break;
                case Scenery.SunsetClouds: Sun(s, 0.5f, 0.3f, 0.2f); CloudBank(c, rng, 0.8f); break;
                case Scenery.Balloons: CloudBank(c, rng, 0.8f); Balloons(s, rng); break;
                case Scenery.StormClouds: StormClouds(s, rng); CloudBank(c, rng, 0.5f); break;
                case Scenery.Vortex: Vortex(s, rng); CloudBank(c, rng, 0.5f); break;
                case Scenery.Castle: CloudBank(c, rng, 0.7f); Castle(c, 0.5f, 0.16f, 1f); break;
                case Scenery.CastleGate: Sun(s, 0.5f, 0.42f, 0.16f); Castle(c, 0.5f, 0.3f, 0.5f); Gate(c); break;
                case Scenery.CyberCity: GridFloor(c); Columns(c, rng); break;
                case Scenery.DataStreams: GridFloor(c); DataStreams(s, rng); break;
                case Scenery.Meteors: Stars(s, rng, 90, 0f); MeteorShower(s, rng); break;
                case Scenery.Spiral: Stars(s, rng, 90, 0f); Spiral(s, rng); break;
                case Scenery.BlackHole: Stars(s, rng, 110, 0f); BlackHole(s); break;
                case Scenery.CoreRings: Stars(s, rng, 50, 0f); CoreRings(s); break;
                case Scenery.VanGFace: Stars(s, rng, 40, 0f); VanGFace(s, rng); break;
                case Scenery.Utopia: UtopiaCity(c, s, rng, CityStyle.Day); break;
                case Scenery.UtopiaSpires: UtopiaCity(c, s, rng, CityStyle.Spires); break;
                case Scenery.UtopiaGarden: UtopiaCity(c, s, rng, CityStyle.Garden); break;
                case Scenery.UtopiaNight: UtopiaCity(c, s, rng, CityStyle.Night); break;
                default: PlanetsAndGrid(s); break;
            }
        }

        // ---------- The original space backdrop ----------

        private static void PlanetsAndGrid(Canvas c)
        {
            int width = c.w, height = c.h;
            var theme = c.t;
            float fadeEnd = width * 0.5f;
            foreach (float fx in new[] { 0.05f, 0.17f, 0.3f })
                Line(c.px, width, height, fx * width, height * 0.38f, fx * width, height, 1.6f, 0.32f, fadeEnd, theme.bgLines);
            foreach (float fy in new[] { 0.5f, 0.66f, 0.82f })
                Line(c.px, width, height, 0f, fy * height, width * 0.5f, (fy + 0.22f) * height, 1.6f, 0.32f, fadeEnd, theme.bgLines);
            Disc(c.px, width, height, width * 0.92f, height * 0.86f, c.m * 0.2f, theme.bgPlanet, 0.55f);
            Disc(c.px, width, height, width * 0.86f, height * 0.9f, c.m * 0.05f, theme.bgTop, 0.25f);
            Disc(c.px, width, height, width * 0.95f, height * 0.8f, c.m * 0.035f, theme.bgTop, 0.2f);
            Stars(c, new Random(7), 55, 0f);
            Sparkle(c.px, width, height, width * 0.94f, height * 0.06f, c.m * 0.035f, theme.bgStar);
        }

        // ---------- Sky ----------

        private static void Stars(Canvas c, Random rng, int count, float minY)
        {
            for (int i = 0; i < count; i++)
            {
                float sx = (float)rng.NextDouble() * c.w;
                float sy = (minY + (float)rng.NextDouble() * (1f - minY)) * c.h;
                Dot(c, sx, sy, 0.8f + (float)rng.NextDouble() * 1.6f, c.t.bgStar, 0.3f + (float)rng.NextDouble() * 0.5f);
            }
        }

        private static void Sun(Canvas c, float x, float y, float r)
        {
            Dot(c, c.w * x, c.h * y, c.m * r * 1.8f, c.t.bgPlanet, 0.18f);
            Dot(c, c.w * x, c.h * y, c.m * r, c.t.bgPlanet, 0.75f);
        }

        private static void Moon(Canvas c)
        {
            Dot(c, c.w * 0.82f, c.h * 0.84f, c.m * 0.1f, c.t.bgPlanet, 0.8f);
            Dot(c, c.w * 0.85f, c.h * 0.86f, c.m * 0.09f, c.t.bgTop, 0.8f);
        }

        private static void Fog(Canvas c, Color color, float alpha, float upTo)
        {
            for (int y = 0; y < c.h; y++)
            {
                float a = alpha * Mathf.Clamp01(1f - y / (c.h * upTo));
                if (a <= 0f) continue;
                for (int x = 0; x < c.w; x++) Blend(c.px, c.w, c.h, x, y, color, a);
            }
        }

        private static void Streaks(Canvas c, Random rng, Color color, float alpha, float slope, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = (float)rng.NextDouble() * c.w, y = (float)rng.NextDouble() * c.h;
                float len = c.m * (0.03f + (float)rng.NextDouble() * 0.06f);
                Stroke(c, x, y, x + len, y + len * slope, 1.2f, color, alpha * (0.5f + (float)rng.NextDouble() * 0.5f));
            }
        }

        // ---------- Channel World ----------

        private static void Pipes(Canvas c, Random rng, bool fire)
        {
            float t = c.m * 0.026f;
            for (int i = 0; i < 4; i++)
            {
                float y = c.h * (0.06f + 0.075f * i);
                var col = Color.Lerp(c.near, c.mid, i / 3f);
                Rect(c, 0f, y - t, c.w, y + t, col, 0.7f);
                Rect(c, 0f, y + t * 0.3f, c.w, y + t * 0.6f, c.t.bgLines, 0.15f);
                for (float x = (i % 2) * c.w / 12f; x < c.w; x += c.w / 6f)
                    Rect(c, x - t * 0.6f, y - t * 1.3f, x + t * 0.6f, y + t * 1.3f, c.near, 0.6f);
                if (fire)
                    for (int k = 0; k < 2; k++)
                    {
                        float fx = (float)rng.NextDouble() * c.w;
                        Ellipse(c, fx, y + t * 2.6f, t * 1.1f, t * 2.2f, c.t.accent, 0.55f);
                        Ellipse(c, fx, y + t * 2.2f, t * 0.5f, t * 1.2f, c.t.bgStar, 0.55f);
                    }
            }
            foreach (float x in new[] { 0.05f, 0.95f })
            {
                Rect(c, c.w * x - t * 1.2f, 0f, c.w * x + t * 1.2f, c.h * 0.72f, c.mid, 0.6f);
                for (float y = c.h * 0.1f; y < c.h * 0.72f; y += c.h * 0.12f)
                    Rect(c, c.w * x - t * 1.6f, y - t * 0.5f, c.w * x + t * 1.6f, y + t * 0.5f, c.near, 0.6f);
            }
            Stars(c, rng, 25, 0.55f);
        }

        private static void Factory(Canvas c, Random rng, float alpha)
        {
            float x = 0f;
            while (x < c.w)
            {
                float bw = c.w * (0.06f + (float)rng.NextDouble() * 0.08f);
                float bh = c.h * (0.1f + (float)rng.NextDouble() * 0.18f);
                Rect(c, x, 0f, x + bw, bh, c.near, alpha);
                for (float wy = c.h * 0.04f; wy < bh - c.h * 0.03f; wy += c.h * 0.045f)
                    Rect(c, x + bw * 0.2f, wy, x + bw * 0.4f, wy + c.h * 0.015f, c.t.accent, alpha * 0.4f);
                if (rng.Next(3) == 0)
                {
                    float cx = x + bw * 0.6f, ch = bh + c.h * (0.08f + (float)rng.NextDouble() * 0.12f);
                    Rect(c, cx - c.m * 0.012f, bh, cx + c.m * 0.012f, ch, c.near, alpha);
                    for (int s = 0; s < 4; s++)
                        Dot(c, cx + s * c.m * 0.02f, ch + s * c.m * 0.035f, c.m * (0.02f + s * 0.012f), c.t.bgLines, alpha * 0.22f);
                }
                x += bw + c.w * 0.01f;
            }
        }

        /// <summary>A great data gate with the first green light glowing through it.</summary>
        private static void Gate(Canvas c)
        {
            float cx = c.w * 0.5f, pw = c.m * 0.07f;
            Ellipse(c, cx, c.h * 0.28f, c.w * 0.3f, c.h * 0.24f, c.t.accent, 0.35f);
            foreach (float s in new[] { -1f, 1f })
                Rect(c, cx + s * c.w * 0.36f - pw, 0f, cx + s * c.w * 0.36f + pw, c.h * 0.56f, c.near, 0.8f);
            Rect(c, cx - c.w * 0.36f - pw * 1.4f, c.h * 0.54f, cx + c.w * 0.36f + pw * 1.4f, c.h * 0.6f, c.near, 0.85f);
            Stroke(c, cx - c.w * 0.1f, c.h * 0.6f, cx - c.w * 0.04f, c.h * 0.54f, 2f, c.t.accent, 0.6f);
            Stroke(c, cx + c.w * 0.15f, c.h * 0.6f, cx + c.w * 0.2f, c.h * 0.55f, 2f, c.t.accent, 0.6f);
        }

        // ---------- Forest World ----------

        private static float HillY(Canvas c, float x01, float baseY, float amp, float phase) =>
            c.h * (baseY + amp * Mathf.Sin(x01 * 5.3f + phase) + amp * 0.5f * Mathf.Sin(x01 * 13.1f + phase * 2f));

        private static void Hills(Canvas c, Random rng)
        {
            float p = (float)rng.NextDouble() * 6f;
            Ground(c, x => HillY(c, x, 0.2f, 0.04f, p), c.far, 0.55f);
            Ground(c, x => HillY(c, x, 0.11f, 0.035f, p + 2f), c.mid, 0.7f);
        }

        private static void Trees(Canvas c, Random rng, bool cubes)
        {
            for (int i = 0; i < 14; i++)
            {
                float x = (float)rng.NextDouble() * c.w;
                if (x > c.w * 0.3f && x < c.w * 0.7f && rng.Next(3) > 0) continue; // keep the middle open
                float baseY = HillY(c, x / c.w, 0.11f, 0.035f, 0f) - c.h * 0.02f;
                float th = c.h * (0.05f + (float)rng.NextDouble() * 0.05f), r = c.m * (0.04f + (float)rng.NextDouble() * 0.035f);
                Rect(c, x - r * 0.15f, baseY, x + r * 0.15f, baseY + th, c.near, 0.75f);
                var leaf = Color.Lerp(c.mid, c.t.accent, 0.25f);
                if (cubes)
                {
                    Rect(c, x - r, baseY + th - r * 0.4f, x + r, baseY + th + r * 1.4f, leaf, 0.85f);
                    Rect(c, x - r, baseY + th + r * 0.5f - 1f, x + r, baseY + th + r * 0.5f + 1f, c.t.bgLines, 0.5f);
                    Rect(c, x - 1f, baseY + th - r * 0.4f, x + 1f, baseY + th + r * 1.4f, c.t.bgLines, 0.5f);
                }
                else
                {
                    Ellipse(c, x, baseY + th + r * 0.4f, r, r * 0.9f, leaf, 0.85f);
                    Ellipse(c, x - r * 0.3f, baseY + th + r * 0.7f, r * 0.45f, r * 0.35f, c.t.bgLines, 0.25f);
                }
            }
        }

        private static void Flowers(Canvas c, Random rng)
        {
            Trees(c, rng, false);
            var colors = new[] { c.t.accent, c.t.bgStar, c.t.bgPlanet };
            for (int i = 0; i < 160; i++)
            {
                float x = (float)rng.NextDouble() * c.w;
                float y = (float)rng.NextDouble() * HillY(c, x / c.w, 0.11f, 0.035f, 0f);
                Dot(c, x, y, c.m * (0.004f + (float)rng.NextDouble() * 0.005f), colors[rng.Next(colors.Length)], 0.75f);
            }
        }

        private static void Ruins(Canvas c, Random rng)
        {
            for (int i = 0; i < 9; i++)
            {
                float x = c.w * (i < 5 ? 0.03f + i * 0.06f : 0.7f + (i - 5) * 0.07f);
                float top = c.h * (0.15f + (float)rng.NextDouble() * 0.25f), pw = c.m * 0.025f;
                Rect(c, x - pw, 0f, x + pw, top, c.near, 0.7f);
                Rect(c, x - pw * 1.4f, top - c.m * 0.01f, x + pw * 1.4f, top + c.m * 0.012f, c.near, 0.7f);
                float vx = x;
                for (float y = top; y > c.h * 0.05f; y -= c.h * 0.02f)
                {
                    float nx = x + Mathf.Sin(y * 0.05f + i) * pw * 1.2f;
                    Stroke(c, vx, y, nx, y - c.h * 0.02f, 1.6f, c.t.accent, 0.55f);
                    if (rng.Next(3) == 0) Dot(c, nx, y, c.m * 0.006f, c.t.accent, 0.6f);
                    vx = nx;
                }
            }
        }

        private static void Pines(Canvas c, Random rng, float alpha, bool snowy)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                var col = layer == 0 ? c.mid : c.near;
                for (int i = 0; i < 16; i++)
                {
                    float x = (float)rng.NextDouble() * c.w;
                    if (x > c.w * 0.32f && x < c.w * 0.68f && rng.Next(4) > 0) continue;
                    float baseY = c.h * (layer == 0 ? 0.12f : 0.02f), ht = c.h * (0.12f + (float)rng.NextDouble() * 0.12f) * (layer == 0 ? 0.8f : 1f);
                    float bw = ht * 0.32f;
                    Tri(c, x - bw, baseY, x + bw, baseY, x, baseY + ht, col, alpha * 0.85f);
                    if (snowy) Tri(c, x - bw * 0.3f, baseY + ht * 0.7f, x + bw * 0.3f, baseY + ht * 0.7f, x, baseY + ht, c.t.bgStar, alpha * 0.7f);
                }
            }
        }

        private static void Mushrooms(Canvas c, Random rng)
        {
            for (int i = 0; i < 10; i++)
            {
                float x = c.w * (i < 5 ? 0.04f + i * 0.06f : 0.7f + (i - 5) * 0.06f);
                float baseY = c.h * 0.08f, ht = c.h * (0.04f + (float)rng.NextDouble() * 0.08f), r = c.m * (0.03f + (float)rng.NextDouble() * 0.03f);
                Rect(c, x - r * 0.18f, baseY, x + r * 0.18f, baseY + ht, c.t.bgLines, 0.6f);
                Ellipse(c, x, baseY + ht, r * 1.6f, r * 3f, c.t.accent, 0.12f);
                Ellipse(c, x, baseY + ht, r, r * 0.55f, c.t.accent, 0.8f);
                for (int s = 0; s < 3; s++)
                    Dot(c, x + (s - 1) * r * 0.45f, baseY + ht + r * 0.2f, r * 0.12f, c.t.bgStar, 0.8f);
            }
        }

        // ---------- Red Canyon ----------

        private static void Mesas(Canvas c, Random rng, float alpha)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                var col = layer == 0 ? c.far : c.mid;
                float x = -c.w * 0.05f;
                while (x < c.w)
                {
                    float mw = c.w * (0.1f + (float)rng.NextDouble() * 0.15f), top = c.h * (layer == 0 ? 0.25f : 0.15f) * (0.7f + (float)rng.NextDouble() * 0.6f);
                    if (rng.Next(3) > 0)
                    {
                        Rect(c, x + mw * 0.1f, 0f, x + mw * 0.9f, top, col, alpha * 0.85f);
                        Tri(c, x, 0f, x + mw * 0.1f, 0f, x + mw * 0.1f, top, col, alpha * 0.85f);
                        Tri(c, x + mw * 0.9f, 0f, x + mw, 0f, x + mw * 0.9f, top, col, alpha * 0.85f);
                        for (float sy = top * 0.3f; sy < top; sy += top * 0.25f)
                            Rect(c, x + mw * 0.1f, sy, x + mw * 0.9f, sy + 1.5f, c.t.bgLines, alpha * 0.25f);
                    }
                    x += mw;
                }
            }
            Ground(c, x => c.h * 0.05f, c.near, alpha * 0.6f);
        }

        private static void Dunes(Canvas c, float alpha)
        {
            Ground(c, x => c.h * (0.2f + 0.05f * Mathf.Sin(x * 4f + 1f)), c.far, 0.5f * alpha);
            Ground(c, x => c.h * (0.13f + 0.04f * Mathf.Sin(x * 6f + 3f)), c.mid, 0.65f * alpha);
            Ground(c, x => c.h * (0.06f + 0.03f * Mathf.Sin(x * 9f)), c.near, 0.6f * alpha);
        }

        private static void Arches(Canvas c, Random rng)
        {
            float cx = c.w * 0.5f, cy = c.h * 0.1f;
            for (int i = 0; i < 4; i++)
            {
                float r = c.w * (0.62f - i * 0.12f);
                Ring(c, cx, cy, r, c.m * (0.03f - i * 0.004f), c.near, 0.75f - i * 0.12f);
            }
            foreach (float s in new[] { -1f, 1f })
                Stroke(c, cx + s * c.w * 0.5f, 0f, cx + s * c.w * 0.08f, c.h * 0.3f, 3f, c.t.bgLines, 0.4f);
            for (int i = 0; i < 6; i++)
            {
                float ly = c.h * (0.25f + 0.08f * i), lx = i % 2 == 0 ? c.w * 0.08f : c.w * 0.92f;
                Dot(c, lx, ly, c.m * 0.03f, c.t.accent, 0.18f);
                Dot(c, lx, ly, c.m * 0.008f, c.t.bgStar, 0.8f);
            }
            Streaks(c, rng, c.t.bgLines, 0.08f, 0.1f, 60);
        }

        private static void Tablets(Canvas c, Random rng)
        {
            for (int i = 0; i < 8; i++)
            {
                float x = c.w * (i < 4 ? 0.06f + i * 0.08f : 0.68f + (i - 4) * 0.08f);
                float tw = c.m * 0.04f, th = c.h * (0.14f + (float)rng.NextDouble() * 0.12f);
                Rect(c, x - tw, 0f, x + tw, th, c.near, 0.75f);
                Ellipse(c, x, th, tw, tw * 0.6f, c.near, 0.75f);
                for (float gy = th * 0.3f; gy < th * 0.9f; gy += th * 0.12f)
                    Rect(c, x - tw * 0.6f, gy, x - tw * 0.6f + tw * (0.4f + (float)rng.NextDouble() * 0.8f), gy + 2f, c.t.accent, 0.7f);
            }
            Glints(c, rng);
        }

        // ---------- Sea Floor ----------

        private static void Harbor(Canvas c, Random rng)
        {
            Rect(c, 0f, 0f, c.w, c.h * 0.16f, c.mid, 0.7f);
            for (int i = 0; i < 40; i++)
            {
                float x = (float)rng.NextDouble() * c.w, y = (float)rng.NextDouble() * c.h * 0.15f;
                Stroke(c, x, y, x + c.m * 0.04f, y, 1.2f, c.t.bgLines, 0.35f);
            }
            float lx = c.w * 0.12f, lb = c.h * 0.12f, lt = c.h * 0.48f, lw = c.m * 0.045f;
            for (int y = (int)lb; y < (int)lt; y++)
            {
                float k = (y - lb) / (lt - lb), half = Mathf.Lerp(lw, lw * 0.55f, k);
                bool stripe = Mathf.FloorToInt(k * 6f) % 2 == 0;
                for (int x = (int)(lx - half); x <= (int)(lx + half); x++) Px(c, x, y, stripe ? c.t.bgStar : c.near, 0.75f);
            }
            Dot(c, lx, lt + lw * 0.6f, lw * 2.4f, c.t.accent, 0.25f);
            Dot(c, lx, lt + lw * 0.6f, lw * 0.6f, c.t.accent, 0.9f);
            Tri(c, lx, lt + lw * 0.6f, c.w * 0.55f, lt + c.h * 0.08f, c.w * 0.55f, lt - c.h * 0.02f, c.t.accent, 0.1f);
            foreach (float mx in new[] { 0.78f, 0.88f })
            {
                float x = c.w * mx;
                Rect(c, x - c.m * 0.06f, c.h * 0.13f, x + c.m * 0.06f, c.h * 0.17f, c.near, 0.75f);
                Stroke(c, x, c.h * 0.17f, x, c.h * 0.42f, 2f, c.near, 0.75f);
                Tri(c, x + 2f, c.h * 0.2f, x + c.m * 0.07f, c.h * 0.2f, x + 2f, c.h * 0.4f, c.t.bgStar, 0.5f);
            }
        }

        private static void Rays(Canvas c, Random rng)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = c.w * (0.1f + 0.16f * i + (float)rng.NextDouble() * 0.05f);
                Tri(c, x - c.w * 0.03f, c.h, x + c.w * 0.03f, c.h, x + c.w * 0.12f, c.h * 0.2f, c.t.bgStar, 0.06f);
            }
        }

        private static void Seabed(Canvas c, Random rng)
        {
            float p = (float)rng.NextDouble() * 6f;
            Ground(c, x => HillY(c, x, 0.08f, 0.025f, p), c.mid, 0.75f);
        }

        private static void Corals(Canvas c, Random rng)
        {
            var colors = new[] { c.t.accent, c.t.bgPlanet, c.t.bgLines };
            for (int i = 0; i < 12; i++)
            {
                float x = c.w * (i < 6 ? 0.03f + i * 0.05f : 0.7f + (i - 6) * 0.05f);
                Branch(c, x, c.h * 0.06f, 90f, c.h * (0.06f + (float)rng.NextDouble() * 0.04f), 3, colors[rng.Next(colors.Length)], rng);
            }
            for (int i = 0; i < 10; i++)
            {
                float x = (float)rng.NextDouble() * c.w, top = c.h * (0.12f + (float)rng.NextDouble() * 0.15f);
                float px0 = x;
                for (float y = 0f; y < top; y += c.h * 0.015f)
                {
                    float nx = x + Mathf.Sin(y * 0.06f + i) * c.m * 0.012f;
                    Stroke(c, px0, y, nx, y + c.h * 0.015f, 2.2f, Color.Lerp(c.mid, c.t.accent, 0.3f), 0.55f);
                    px0 = nx;
                }
            }
        }

        private static void Branch(Canvas c, float x, float y, float angle, float len, int depth, Color color, Random rng)
        {
            float a = angle * Mathf.Deg2Rad;
            float ex = x + Mathf.Cos(a) * len, ey = y + Mathf.Sin(a) * len;
            Stroke(c, x, y, ex, ey, 1f + depth * 1.2f, color, 0.65f);
            if (depth <= 0)
            {
                Dot(c, ex, ey, 2.5f, c.t.bgStar, 0.6f);
                return;
            }
            Branch(c, ex, ey, angle - 20f - rng.Next(15), len * 0.7f, depth - 1, color, rng);
            Branch(c, ex, ey, angle + 20f + rng.Next(15), len * 0.7f, depth - 1, color, rng);
        }

        private static void Bubbles(Canvas c, Random rng)
        {
            for (int i = 0; i < 40; i++)
            {
                float x = (float)rng.NextDouble() * c.w, y = (float)rng.NextDouble() * c.h;
                Ring(c, x, y, c.m * (0.004f + (float)rng.NextDouble() * 0.008f), 1.2f, c.t.bgStar, 0.35f);
            }
        }

        private static void Jellyfish(Canvas c, Random rng)
        {
            for (int i = 0; i < 7; i++)
            {
                float x = c.w * (i < 4 ? 0.05f + i * 0.08f : 0.72f + (i - 4) * 0.09f), y = c.h * (0.3f + (float)rng.NextDouble() * 0.55f);
                float r = c.m * (0.025f + (float)rng.NextDouble() * 0.025f);
                Dot(c, x, y, r * 2.4f, c.t.accent, 0.1f);
                for (int yy = (int)y; yy < (int)(y + r); yy++)
                {
                    float half = Mathf.Sqrt(Mathf.Max(0f, r * r - (yy - y) * (yy - y)));
                    for (int xx = (int)(x - half); xx <= (int)(x + half); xx++) Px(c, xx, yy, c.t.accent, 0.6f);
                }
                for (int k = 0; k < 4; k++)
                {
                    float tx = x + (k - 1.5f) * r * 0.45f, py = y;
                    for (int s = 0; s < 6; s++)
                    {
                        float ny = py - r * 0.4f, nx = tx + Mathf.Sin(s + k) * r * 0.15f;
                        Stroke(c, tx, py, nx, ny, 1.2f, c.t.bgLines, 0.45f);
                        tx = nx;
                        py = ny;
                    }
                }
            }
        }

        private static void Server(Canvas c, Random rng)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = c.w * (i < 3 ? 0.02f + i * 0.09f : 0.73f + (i - 3) * 0.09f), rw = c.w * 0.07f, rh = c.h * (0.35f + (float)rng.NextDouble() * 0.25f);
                Rect(c, x, 0f, x + rw, rh, c.near, 0.75f);
                for (float y = c.h * 0.03f; y < rh - c.h * 0.02f; y += c.h * 0.028f)
                {
                    Rect(c, x + rw * 0.1f, y, x + rw * 0.9f, y + c.h * 0.016f, c.mid, 0.5f);
                    if (rng.Next(2) == 0) Dot(c, x + rw * 0.8f, y + c.h * 0.008f, 2.4f, c.t.accent, 0.9f);
                }
            }
            for (int i = 0; i < 5; i++)
            {
                float y = c.h * (0.4f + i * 0.08f);
                Stroke(c, c.w * 0.27f, y, c.w * 0.73f, y + c.h * 0.05f, 2f, c.mid, 0.3f);
            }
        }

        // ---------- Snowy Mountain ----------

        private static void SnowHills(Canvas c, Random rng)
        {
            float p = (float)rng.NextDouble() * 6f;
            var snow = Color.Lerp(c.t.bgStar, c.far, 0.35f);
            Ground(c, x => HillY(c, x, 0.2f, 0.035f, p), Color.Lerp(snow, c.far, 0.4f), 0.6f);
            Ground(c, x => HillY(c, x, 0.1f, 0.03f, p + 2f), snow, 0.75f);
            for (int i = 0; i < 8; i++)
            {
                float x = c.w * (i < 4 ? 0.04f + i * 0.07f : 0.72f + (i - 4) * 0.07f), b = HillY(c, x / c.w, 0.1f, 0.03f, p + 2f) - 4f, ht = c.h * 0.07f;
                Tri(c, x - ht * 0.3f, b, x + ht * 0.3f, b, x, b + ht, c.near, 0.6f);
            }
        }

        private static void AuroraBands(Canvas c)
        {
            for (int band = 0; band < 3; band++)
            {
                var col = band == 1 ? c.t.bgLines : c.t.accent;
                for (int x = 0; x < c.w; x += 2)
                {
                    float x01 = x / (float)c.w;
                    float y = c.h * (0.62f + band * 0.07f + 0.06f * Mathf.Sin(x01 * 4f + band * 1.7f));
                    Stroke(c, x, y, x, y + c.h * 0.1f, 1.2f, col, 0.12f + 0.08f * Mathf.Sin(x01 * 20f + band));
                }
            }
        }

        private static void Mountains(Canvas c, Random rng)
        {
            for (int i = 0; i < 7; i++)
            {
                float x = c.w * (i / 6f) + (float)rng.NextDouble() * c.w * 0.05f, ht = c.h * (0.25f + (float)rng.NextDouble() * 0.18f), bw = ht * 0.9f;
                Tri(c, x - bw, 0f, x + bw, 0f, x, ht, c.far, 0.6f);
                Tri(c, x - bw * 0.25f, ht * 0.75f, x + bw * 0.25f, ht * 0.75f, x, ht, c.t.bgStar, 0.6f);
            }
        }

        private static void Icicles(Canvas c, Random rng)
        {
            for (float x = 0f; x < c.w; x += c.w * 0.03f)
            {
                float len = c.h * (0.04f + (float)rng.NextDouble() * 0.12f);
                if (x > c.w * 0.3f && x < c.w * 0.7f) len *= 0.4f;
                Tri(c, x - c.w * 0.014f, c.h, x + c.w * 0.014f, c.h, x, c.h - len, c.t.bgLines, 0.5f);
            }
            Rect(c, 0f, c.h * 0.97f, c.w, c.h, c.t.bgLines, 0.5f);
        }

        private static void Towers(Canvas c, Random rng)
        {
            foreach (float tx in new[] { 0.12f, 0.88f, 0.26f })
            {
                float cx = c.w * tx, baseW = c.m * 0.11f, ht = c.h * 0.42f;
                for (int y = 0; y < (int)ht; y++)
                {
                    float k = y / ht, half = baseW * (1f - 0.35f * Mathf.Sin(k * Mathf.PI)) * (1f - k * 0.2f);
                    for (int x = (int)(cx - half); x <= (int)(cx + half); x++) Px(c, x, y, c.mid, 0.7f);
                }
                for (int s = 0; s < 5; s++)
                    Dot(c, cx + s * c.m * 0.02f, ht + s * c.m * 0.04f, c.m * (0.04f + s * 0.015f), c.t.bgStar, 0.25f);
                Rect(c, cx - baseW * 0.8f, ht - c.h * 0.01f, cx + baseW * 0.8f, ht, c.t.bgStar, 0.6f);
            }
        }

        // ---------- Crystal Cave ----------

        private static void Shards(Canvas c, Random rng, Color color, float alpha)
        {
            for (int i = 0; i < 16; i++)
            {
                float x = c.w * (i < 8 ? (float)rng.NextDouble() * 0.3f : 0.7f + (float)rng.NextDouble() * 0.3f);
                float ht = c.h * (0.08f + (float)rng.NextDouble() * 0.22f), bw = ht * (0.15f + (float)rng.NextDouble() * 0.1f);
                float lean = ((float)rng.NextDouble() - 0.5f) * bw * 2f;
                var col = Color.Lerp(color, c.mid, (float)rng.NextDouble() * 0.4f);
                Tri(c, x - bw, 0f, x + bw, 0f, x + lean, ht, col, alpha);
                Stroke(c, x - bw * 0.3f, 0f, x + lean, ht, 1.2f, c.t.bgStar, alpha * 0.5f);
            }
        }

        private static void Glints(Canvas c, Random rng)
        {
            for (int i = 0; i < 18; i++)
                Sparkle(c.px, c.w, c.h, (float)rng.NextDouble() * c.w, (float)rng.NextDouble() * c.h, c.m * (0.01f + (float)rng.NextDouble() * 0.015f), c.t.bgStar);
        }

        private static void PrismRays(Canvas c)
        {
            float ox = c.w * 0.1f, oy = c.h * 0.9f;
            for (int i = 0; i < 7; i++)
            {
                var col = Color.HSVToRGB(i / 7f, 0.45f, 1f);
                float a = (-20f - i * 6f) * Mathf.Deg2Rad;
                Stroke(c, ox, oy, ox + Mathf.Cos(a) * c.w * 1.2f, oy + Mathf.Sin(a) * c.w * 1.2f, c.m * 0.008f, col, 0.12f);
            }
        }

        private static void Mirrors(Canvas c, Random rng, bool clones)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = c.w * (i < 3 ? 0.03f + i * 0.1f : 0.67f + (i - 3) * 0.1f), y = c.h * (0.12f + (i % 2) * 0.1f);
                float fw = c.w * 0.08f, fh = c.h * 0.28f;
                Rect(c, x, y, x + fw, y + fh, c.t.bgGlow, clones ? 0.2f : 0.3f);
                RectOutline(c, x, y, x + fw, y + fh, c.m * 0.008f, c.t.bgLines, 0.75f);
                Stroke(c, x + fw * 0.2f, y + fh * 0.3f, x + fw * 0.6f, y + fh * 0.75f, 2f, c.t.bgStar, 0.3f);
                if (clones)
                {
                    float cx = x + fw * 0.5f, cy = y + fh * 0.35f, s = fw * 0.3f;
                    Rect(c, cx - s, cy - s * 0.8f, cx + s, cy + s * 0.8f, c.near, 0.8f);
                    Dot(c, cx - s * 0.4f, cy, s * 0.18f, c.t.accent, 0.9f);
                    Dot(c, cx + s * 0.4f, cy, s * 0.18f, c.t.accent, 0.9f);
                }
            }
            Rect(c, 0f, 0f, c.w, c.h * 0.08f, c.mid, 0.5f);
        }

        private static void Drips(Canvas c, Random rng)
        {
            for (int i = 0; i < 20; i++)
            {
                float x = c.w * (i < 10 ? (float)rng.NextDouble() * 0.3f : 0.7f + (float)rng.NextDouble() * 0.3f), y = c.h * (0.15f + (float)rng.NextDouble() * 0.3f);
                for (int k = 0; k < 3; k++) Dot(c, x, y - k * c.m * 0.02f, c.m * (0.006f - k * 0.0015f), c.t.accent, 0.6f);
            }
            Ellipse(c, c.w * 0.1f, c.h * 0.03f, c.w * 0.12f, c.h * 0.025f, c.t.accent, 0.4f);
            Ellipse(c, c.w * 0.9f, c.h * 0.03f, c.w * 0.12f, c.h * 0.025f, c.t.accent, 0.4f);
        }

        private static void Pools(Canvas c, Random rng)
        {
            for (int i = 0; i < 5; i++)
            {
                float x = c.w * (i < 3 ? 0.08f + i * 0.1f : 0.78f + (i - 3) * 0.12f), y = c.h * (0.04f + (float)rng.NextDouble() * 0.08f);
                Ellipse(c, x, y, c.w * 0.07f, c.h * 0.02f, c.t.accent, 0.5f);
                for (int b = 0; b < 4; b++)
                    Ring(c, x + ((float)rng.NextDouble() - 0.5f) * c.w * 0.08f, y + c.h * (0.01f + b * 0.02f), c.m * 0.006f, 1.2f, c.t.accent, 0.5f);
            }
        }

        // ---------- Cloud Bridge ----------

        private static void Cloud(Canvas c, float x, float y, float s, float alpha, Color color)
        {
            Ellipse(c, x, y - s * 0.15f, s * 1.6f, s * 0.45f, Color.Lerp(color, c.mid, 0.4f), alpha * 0.6f);
            Ellipse(c, x - s * 0.7f, y, s * 0.7f, s * 0.5f, color, alpha);
            Ellipse(c, x, y + s * 0.2f, s * 0.9f, s * 0.7f, color, alpha);
            Ellipse(c, x + s * 0.8f, y, s * 0.65f, s * 0.45f, color, alpha);
        }

        private static void CloudBank(Canvas c, Random rng, float alpha)
        {
            for (int i = 0; i < 9; i++)
            {
                float x = c.w * (i / 8f), y = c.h * (0.05f + (float)rng.NextDouble() * 0.08f);
                Cloud(c, x, y, c.m * (0.1f + (float)rng.NextDouble() * 0.06f), 0.8f * alpha, c.t.bgStar);
            }
            Cloud(c, c.w * 0.08f, c.h * 0.6f, c.m * 0.07f, 0.55f * alpha, c.t.bgStar);
            Cloud(c, c.w * 0.92f, c.h * 0.7f, c.m * 0.06f, 0.55f * alpha, c.t.bgStar);
        }

        private static void Islands(Canvas c, Random rng)
        {
            foreach (var (x, y, s) in new[] { (0.12f, 0.42f, 1f), (0.88f, 0.55f, 0.8f), (0.2f, 0.75f, 0.6f), (0.82f, 0.28f, 0.7f) })
            {
                float cx = c.w * x, cy = c.h * y, r = c.m * 0.08f * s;
                Tri(c, cx - r, cy, cx + r, cy, cx + r * 0.1f, cy - r * 1.4f, c.near, 0.75f);
                Ellipse(c, cx, cy, r * 1.05f, r * 0.3f, Color.Lerp(c.t.accent, new Color(0.5f, 0.8f, 0.4f), 0.6f), 0.85f);
                Ellipse(c, cx - r * 0.3f, cy + r * 0.45f, r * 0.25f, r * 0.25f, new Color(0.4f, 0.7f, 0.35f), 0.8f);
            }
        }

        private static void Balloons(Canvas c, Random rng)
        {
            for (int i = 0; i < 7; i++)
            {
                float x = c.w * (i < 4 ? 0.05f + i * 0.08f : 0.72f + (i - 4) * 0.09f), y = c.h * (0.35f + (float)rng.NextDouble() * 0.5f), r = c.m * (0.025f + (float)rng.NextDouble() * 0.02f);
                var col = Color.HSVToRGB((float)rng.NextDouble(), 0.45f, 1f);
                Ellipse(c, x, y, r, r * 1.2f, col, 0.8f);
                Ellipse(c, x - r * 0.35f, y + r * 0.4f, r * 0.25f, r * 0.3f, Color.white, 0.4f);
                Stroke(c, x, y - r * 1.2f, x + r * 0.2f, y - r * 3f, 1f, c.t.bgStar, 0.5f);
            }
        }

        private static void StormClouds(Canvas c, Random rng)
        {
            for (int i = 0; i < 9; i++)
                Cloud(c, c.w * (i / 8f), c.h * (0.9f + (float)rng.NextDouble() * 0.06f), c.m * 0.12f, 0.75f, c.near);
            for (int b = 0; b < 3; b++)
            {
                float x = c.w * (b == 0 ? 0.12f : b == 1 ? 0.85f : 0.3f), y = c.h * 0.85f;
                for (int s = 0; s < 6; s++)
                {
                    float nx = x + (s % 2 == 0 ? 1f : -1f) * c.m * 0.025f, ny = y - c.h * 0.05f;
                    Stroke(c, x, y, nx, ny, 2.2f, c.t.accent, 0.8f);
                    x = nx;
                    y = ny;
                }
            }
            Streaks(c, rng, c.t.bgLines, 0.18f, -3f, 160);
        }

        private static void Vortex(Canvas c, Random rng)
        {
            float cx = c.w * 0.5f, cy = c.h * 0.72f;
            for (int arm = 0; arm < 3; arm++)
            {
                float px0 = cx, py0 = cy;
                for (float t = 0f; t < 9f; t += 0.08f)
                {
                    float r = c.m * 0.02f * t * t * 0.18f + c.m * 0.01f * t, a = t + arm * 2.09f;
                    float x = cx + Mathf.Cos(a) * r * 1.4f, y = cy + Mathf.Sin(a) * r * 0.5f;
                    Stroke(c, px0, py0, x, y, 2f, c.t.bgLines, 0.25f);
                    px0 = x;
                    py0 = y;
                }
            }
            for (int i = 0; i < 30; i++)
                Dot(c, (float)rng.NextDouble() * c.w, (float)rng.NextDouble() * c.h, c.m * 0.004f, c.t.bgStar, 0.5f);
        }

        private static void Castle(Canvas c, float x, float baseY, float scale)
        {
            float cx = c.w * x, b = c.h * baseY, u = c.m * 0.05f * scale;
            var col = c.near;
            Rect(c, cx - u * 2.5f, b, cx + u * 2.5f, b + u * 3f, col, 0.8f);
            foreach (float s in new[] { -1f, 1f, -0.4f, 0.4f })
            {
                float tx = cx + s * u * 3f, th = b + u * (Mathf.Abs(s) > 0.5f ? 5f : 6.5f);
                Rect(c, tx - u * 0.6f, b, tx + u * 0.6f, th, col, 0.85f);
                Tri(c, tx - u * 0.8f, th, tx + u * 0.8f, th, tx, th + u * 1.6f, c.t.accent, 0.8f);
                Dot(c, tx, th - u * 1.2f, u * 0.2f, c.t.bgStar, 0.8f);
                Stroke(c, tx, th + u * 1.6f, tx, th + u * 2.4f, 1.4f, col, 0.8f);
                Tri(c, tx, th + u * 2.4f, tx, th + u * 2f, tx + u * 0.6f, th + u * 2.2f, c.t.accent, 0.8f);
            }
            Ellipse(c, cx, b + u * 0.9f, u * 0.8f, u * 1.1f, c.t.bgGlow, 0.7f);
        }

        // ---------- Star Road ----------

        private static void GridFloor(Canvas c)
        {
            float horizon = c.h * 0.3f, cx = c.w * 0.5f;
            for (int i = -10; i <= 10; i++)
                Stroke(c, cx + i * c.w * 0.03f, horizon, cx + i * c.w * 0.18f, 0f, 1.4f, c.t.bgLines, 0.3f);
            for (int i = 1; i < 8; i++)
            {
                float y = horizon * (1f - Mathf.Pow(i / 8f, 0.6f));
                Stroke(c, 0f, y, c.w, y, 1.2f, c.t.bgLines, 0.25f);
            }
        }

        private static void Columns(Canvas c, Random rng)
        {
            for (int i = 0; i < 10; i++)
            {
                float x = c.w * (i < 5 ? 0.01f + i * 0.06f : 0.71f + (i - 5) * 0.06f), cw = c.w * 0.035f, ch = c.h * (0.25f + (float)rng.NextDouble() * 0.4f);
                Rect(c, x, c.h * 0.25f, x + cw, ch + c.h * 0.25f, c.near, 0.6f);
                Rect(c, x, c.h * 0.25f, x + 2f, ch + c.h * 0.25f, c.t.accent, 0.7f);
                for (float y = c.h * 0.28f; y < ch + c.h * 0.24f; y += c.h * 0.03f)
                    if (rng.Next(3) == 0) Rect(c, x + cw * 0.3f, y, x + cw * 0.7f, y + 3f, c.t.accent, 0.6f);
            }
        }

        private static void DataStreams(Canvas c, Random rng)
        {
            for (int i = 0; i < 26; i++)
            {
                float x = c.w * (i < 13 ? (float)rng.NextDouble() * 0.3f : 0.7f + (float)rng.NextDouble() * 0.3f);
                float y0 = c.h * (0.3f + (float)rng.NextDouble() * 0.6f), s = c.m * 0.008f;
                for (int k = 0; k < 10; k++)
                    Rect(c, x, y0 - k * s * 2.4f, x + s * 1.5f, y0 - k * s * 2.4f + s * 1.6f, k == 0 ? c.t.bgStar : c.t.accent, 0.7f * (1f - k / 10f));
            }
        }

        private static void MeteorShower(Canvas c, Random rng)
        {
            for (int i = 0; i < 9; i++)
            {
                float x = (float)rng.NextDouble() * c.w, y = c.h * (0.4f + (float)rng.NextDouble() * 0.55f), len = c.m * (0.08f + (float)rng.NextDouble() * 0.1f);
                for (int k = 0; k < 6; k++)
                    Stroke(c, x + len * k / 6f, y + len * 0.5f * k / 6f, x + len * (k + 1) / 6f, y + len * 0.5f * (k + 1) / 6f, 2f - k * 0.2f, c.t.accent, 0.6f * (1f - k / 6f));
                Dot(c, x, y, c.m * 0.006f, c.t.bgStar, 0.95f);
            }
            Dot(c, c.w * 0.88f, c.h * 0.84f, c.m * 0.08f, c.t.bgPlanet, 0.6f);
        }

        private static void Spiral(Canvas c, Random rng)
        {
            float cx = c.w * 0.8f, cy = c.h * 0.78f;
            Dot(c, cx, cy, c.m * 0.1f, c.t.bgGlow, 0.35f);
            Dot(c, cx, cy, c.m * 0.03f, c.t.bgStar, 0.8f);
            for (int arm = 0; arm < 2; arm++)
                for (float t = 0.2f; t < 5f; t += 0.03f)
                {
                    float r = c.m * 0.03f * Mathf.Exp(t * 0.45f), a = t * 1.2f + arm * Mathf.PI;
                    float x = cx + Mathf.Cos(a) * r + ((float)rng.NextDouble() - 0.5f) * c.m * 0.02f, y = cy + Mathf.Sin(a) * r * 0.55f + ((float)rng.NextDouble() - 0.5f) * c.m * 0.02f;
                    Dot(c, x, y, 1.2f + (float)rng.NextDouble() * 1.4f, rng.Next(2) == 0 ? c.t.bgLines : c.t.bgStar, 0.55f);
                }
        }

        private static void BlackHole(Canvas c)
        {
            float cx = c.w * 0.78f, cy = c.h * 0.8f, r = c.m * 0.16f;
            for (float a = 0f; a < Mathf.PI * 2f; a += 0.01f)
            {
                float x = cx + Mathf.Cos(a) * r * 1.6f, y = cy + Mathf.Sin(a) * r * 0.35f;
                Dot(c, x, y, c.m * 0.012f, c.t.bgPlanet, 0.18f);
            }
            Ring(c, cx, cy, r * 0.62f, c.m * 0.012f, c.t.bgPlanet, 0.7f);
            Dot(c, cx, cy, r * 0.58f, Color.black, 0.9f);
        }

        private static void CoreRings(Canvas c)
        {
            float cx = c.w * 0.5f, cy = c.h * 0.72f;
            for (int i = 0; i < 6; i++) Ring(c, cx, cy, c.m * (0.08f + i * 0.07f), c.m * 0.005f, c.t.accent, 0.35f - i * 0.04f);
            Dot(c, cx, cy, c.m * 0.07f, c.t.bgGlow, 0.6f);
            Dot(c, cx, cy, c.m * 0.03f, c.t.bgStar, 0.8f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Stroke(c, cx + Mathf.Cos(a) * c.m * 0.45f, cy + Mathf.Sin(a) * c.m * 0.45f, cx + Mathf.Cos(a) * c.m * 0.7f, cy + Mathf.Sin(a) * c.m * 0.7f, 1.4f, c.t.bgLines, 0.25f);
            }
        }

        /// <summary>vanG's face over the whole sky: one great eye under an angry brow, pixels drifting off it.</summary>
        private static void VanGFace(Canvas c, Random rng)
        {
            float cx = c.w * 0.5f, cy = c.h * 0.74f, r = c.m * 0.3f;
            Ring(c, cx, cy, r, c.m * 0.012f, c.t.bgLines, 0.45f);
            Ellipse(c, cx, cy, r * 0.55f, r * 0.32f, c.t.bgPlanet, 0.55f);
            Dot(c, cx, cy, r * 0.18f, c.t.bgStar, 0.85f);
            Stroke(c, cx - r * 0.6f, cy + r * 0.55f, cx + r * 0.6f, cy + r * 0.4f, c.m * 0.012f, c.t.bgPlanet, 0.6f);
            Stroke(c, cx - r * 0.35f, cy - r * 0.62f, cx + r * 0.35f, cy - r * 0.62f, c.m * 0.008f, c.t.bgLines, 0.5f);
            for (int i = 0; i < 70; i++)
            {
                float x = (float)rng.NextDouble() * c.w, y = (float)rng.NextDouble() * c.h, s = c.m * (0.004f + (float)rng.NextDouble() * 0.008f);
                Rect(c, x, y, x + s, y + s, c.t.accent, 0.35f + (float)rng.NextDouble() * 0.35f);
            }
        }

        // ---------- Drawing helpers ----------

        private static void Rect(Canvas c, float x0, float y0, float x1, float y1, Color color, float alpha)
        {
            // Things standing on the ground reach all the way down; everything else sits on the horizon.
            y0 = y0 <= 0f ? 0f : y0 + c.g;
            y1 += c.g;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(y0)); y < Mathf.Min(c.h, Mathf.CeilToInt(y1)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(x0)); x < Mathf.Min(c.w, Mathf.CeilToInt(x1)); x++)
                    Blend(c.px, c.w, c.h, x, y, color, alpha);
        }

        private static void RectOutline(Canvas c, float x0, float y0, float x1, float y1, float t, Color color, float alpha)
        {
            Rect(c, x0, y0, x1, y0 + t, color, alpha);
            Rect(c, x0, y1 - t, x1, y1, color, alpha);
            Rect(c, x0, y0 + t, x0 + t, y1 - t, color, alpha);
            Rect(c, x1 - t, y0 + t, x1, y1 - t, color, alpha);
        }

        private static void Ellipse(Canvas c, float cx, float cy, float rx, float ry, Color color, float alpha)
        {
            cy += c.g;
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= Mathf.CeilToInt(cy + ry + 1); y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= Mathf.CeilToInt(cx + rx + 1); x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    float d = (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
                    Blend(c.px, c.w, c.h, x, y, color, Mathf.Clamp01(0.5f - d) * alpha);
                }
        }

        private static void Ring(Canvas c, float cx, float cy, float r, float thickness, Color color, float alpha)
        {
            cy += c.g;
            for (int y = Mathf.FloorToInt(cy - r - thickness - 1); y <= Mathf.CeilToInt(cy + r + thickness + 1); y++)
                for (int x = Mathf.FloorToInt(cx - r - thickness - 1); x <= Mathf.CeilToInt(cx + r + thickness + 1); x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);
                    Blend(c.px, c.w, c.h, x, y, color, Mathf.Clamp01(thickness - d + 0.5f) * alpha);
                }
        }

        private static void Tri(Canvas c, float ax, float ay, float bx, float by, float cx, float cy, Color color, float alpha)
        {
            ay += c.g;
            by += c.g;
            cy += c.g;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, Mathf.Min(bx, cx)))), maxX = Mathf.Min(c.w - 1, Mathf.CeilToInt(Mathf.Max(ax, Mathf.Max(bx, cx))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, Mathf.Min(by, cy)))), maxY = Mathf.Min(c.h - 1, Mathf.CeilToInt(Mathf.Max(ay, Mathf.Max(by, cy))));
            float area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay);
            if (Mathf.Abs(area) < 0.01f) return;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float w0 = ((bx - x) * (cy - y) - (cx - x) * (by - y)) / area;
                    float w1 = ((cx - x) * (ay - y) - (ax - x) * (cy - y)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 >= 0f && w1 >= 0f && w2 >= 0f) Blend(c.px, c.w, c.h, x, y, color, alpha);
                }
        }

        /// <summary>Fills every column from the bottom up to <paramref name="top"/>(x in 0..1), in pixels.</summary>
        private static void Ground(Canvas c, Func<float, float> top, Color color, float alpha)
        {
            for (int x = 0; x < c.w; x++)
            {
                float t = top(x / (float)c.w) + c.g;
                int max = Mathf.Min(c.h, Mathf.CeilToInt(t));
                for (int y = 0; y < max; y++) Blend(c.px, c.w, c.h, x, y, color, alpha * Mathf.Clamp01(t - y));
            }
        }

        private static void Stroke(Canvas c, float x0, float y0, float x1, float y1, float thickness, Color color, float alpha) =>
            Line(c.px, c.w, c.h, x0, y0 + c.g, x1, y1 + c.g, thickness, alpha, float.MaxValue, color);

        private static void Dot(Canvas c, float x, float y, float r, Color color, float alpha) => Disc(c.px, c.w, c.h, x, y + c.g, r, color, alpha);

        /// <summary>One pixel of a shape drawn row by row, on the horizon.</summary>
        private static void Px(Canvas c, int x, int y, Color color, float alpha) => Blend(c.px, c.w, c.h, x, y + Mathf.RoundToInt(c.g), color, alpha);
    }
}
