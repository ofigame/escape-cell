using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Paints the level map: the Escape Cell prison tower rising out of a bright utopian city by the sea.
    /// One texture per floor (world), stacked bottom to top; together they show the climb from the shore at
    /// dawn, past green skyscrapers and floating gardens, through the clouds at sunset, up to the roof under
    /// the stars where WARDEN keeps watch. Textures depend only on the floor, so they are cached.
    /// </summary>
    public static class UtopiaArt
    {
        public const float TowerLeft = 0.2f, TowerRight = 0.8f;

        private static readonly Dictionary<int, Texture2D> Cache = new Dictionary<int, Texture2D>();
        private static List<Building> city;

        private class Building
        {
            public float x, w, top; // x, width in texture units; top in floors (global altitude)
            public Color body, roof;
            public bool gardens;
        }

        /// <summary>The painting for one floor. <paramref name="floors"/> is the tower's total height.</summary>
        public static Texture2D Floor(int world, int floors, int width, int height)
        {
            if (Cache.TryGetValue(world, out var cached) && cached != null) return cached;
            var tex = Paint(world, floors, width, height);
            Cache[world] = tex;
            return tex;
        }

        private static Texture2D Paint(int world, int floors, int W, int H)
        {
            var px = new Color[W * H];
            var theme = WorldTheme.ForWorld(world);
            var rng = new System.Random(1000 + world);

            // Sky: dawn at the bottom of the tower, noon in the middle, sunset and then night at the top.
            for (int y = 0; y < H; y++)
            {
                float a = (world + y / (float)H) / floors;
                var sky = Sky(a);
                for (int x = 0; x < W; x++) px[y * W + x] = sky;
            }

            // Stars once the sky goes dark.
            for (int i = 0; i < 70; i++)
            {
                float sy = (float)rng.NextDouble() * H;
                float a = (world + sy / H) / floors;
                float alpha = Mathf.Clamp01((a - 0.82f) / 0.12f);
                if (alpha > 0f) Disc(px, W, H, (float)rng.NextDouble() * W, sy, 0.8f + (float)rng.NextDouble() * 1.4f, Color.white, alpha * 0.8f);
            }

            if (world == 0)
            {
                // The shore: sun low over distant mountains, the sea, a strip of beach.
                Disc(px, W, H, W * 0.86f, H * 0.42f, 34f, new Color(1f, 0.85f, 0.5f), 0.35f);
                Disc(px, W, H, W * 0.86f, H * 0.42f, 22f, new Color(1f, 0.86f, 0.5f), 1f);
                Mountains(px, W, H, H * 0.14f, H * 0.36f, new Color(0.73f, 0.83f, 0.89f), rng);
                Mountains(px, W, H, H * 0.14f, H * 0.27f, new Color(0.62f, 0.78f, 0.84f), rng);
                Rect(px, W, H, 0, 0, W, H * 0.1f, new Color(0.42f, 0.76f, 0.84f), 1f);
                for (int i = 0; i < 9; i++)
                {
                    float wy = (float)rng.NextDouble() * H * 0.09f;
                    float wx = (float)rng.NextDouble() * W;
                    Rect(px, W, H, wx, wy, wx + 14f + (float)rng.NextDouble() * 18f, wy + 1.5f, new Color(0.66f, 0.88f, 0.92f), 0.9f);
                }
                Rect(px, W, H, 0, H * 0.1f, W, H * 0.13f, new Color(0.98f, 0.93f, 0.82f), 1f);
            }

            Clouds(px, W, H, world, floors, rng, behind: true);
            City(px, W, H, world, floors);
            if (world >= 4 && world <= 10) Gardens(px, W, H, rng);
            Vehicles(px, W, H, world, floors, rng);
            Tower(px, W, H, world, floors, theme);
            Clouds(px, W, H, world, floors, rng, behind: false);

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Utopia " + world,
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        // ---------- Sky ----------

        private static readonly (float at, Color color)[] SkyKeys =
        {
            (0.00f, new Color(0.99f, 0.89f, 0.78f)),
            (0.10f, new Color(0.75f, 0.9f, 0.96f)),
            (0.40f, new Color(0.56f, 0.82f, 0.95f)),
            (0.62f, new Color(0.45f, 0.67f, 0.92f)),
            (0.76f, new Color(0.96f, 0.66f, 0.62f)),
            (0.88f, new Color(0.55f, 0.46f, 0.78f)),
            (1.00f, new Color(0.17f, 0.16f, 0.36f)),
        };

        private static Color Sky(float a)
        {
            a = Mathf.Clamp01(a);
            for (int i = 1; i < SkyKeys.Length; i++)
                if (a <= SkyKeys[i].at)
                    return Color.Lerp(SkyKeys[i - 1].color, SkyKeys[i].color, Mathf.InverseLerp(SkyKeys[i - 1].at, SkyKeys[i].at, a));
            return SkyKeys[SkyKeys.Length - 1].color;
        }

        // ---------- City ----------

        /// <summary>Skyscrapers with green roofs and sky gardens, shared by all floors (each paints its own slice).</summary>
        private static List<Building> Skyline(int W)
        {
            if (city != null) return city;
            city = new List<Building>();
            var rng = new System.Random(77);
            var bodies = new[] { new Color(0.96f, 0.97f, 0.97f), new Color(0.9f, 0.96f, 0.94f), new Color(0.87f, 0.93f, 0.97f) };
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -10f : W * TowerRight + 4f;
                float end = side == 0 ? W * TowerLeft + 30f : W + 10f;
                while (x < end)
                {
                    float w = 16f + (float)rng.NextDouble() * 22f;
                    var b = new Building
                    {
                        x = x,
                        w = w,
                        top = 0.25f + (float)rng.NextDouble() * (rng.NextDouble() < 0.35 ? 5.5f : 2.2f),
                        body = bodies[rng.Next(bodies.Length)],
                        roof = new Color(0.42f + (float)rng.NextDouble() * 0.1f, 0.74f, 0.5f),
                        gardens = rng.NextDouble() < 0.5,
                    };
                    city.Add(b);
                    x += w + 3f + (float)rng.NextDouble() * 6f;
                }
            }
            return city;
        }

        private static void City(Color[] px, int W, int H, int world, int floors)
        {
            float ground = 0.13f; // in floors
            foreach (var b in Skyline(W))
            {
                // This floor's slice of the building, in texture rows.
                float y0 = (ground - world) * H, y1 = (b.top - world) * H;
                if (y1 < 0f || y0 > H) continue;
                Rect(px, W, H, b.x, y0, b.x + b.w, y1, b.body, 1f);
                // Window rows.
                for (float wy = Mathf.Max(y0 + 8f, -8f); wy < y1 - 10f && wy < H; wy += 11f)
                    Rect(px, W, H, b.x + 3f, wy, b.x + b.w - 3f, wy + 4f, new Color(0.62f, 0.85f, 0.95f), 0.85f);
                // Sky gardens every so often on the tall ones, and a green roof with trees on top.
                if (b.gardens)
                    for (float gy = y0 + H * 0.45f; gy < y1 - 20f; gy += H * 0.6f)
                    {
                        Ellipse(px, W, H, b.x + b.w * 0.5f, gy, b.w * 0.85f, 5f, b.roof, 1f);
                        Disc(px, W, H, b.x + b.w * 0.3f, gy + 6f, 5f, b.roof * 0.85f, 1f);
                        Disc(px, W, H, b.x + b.w * 0.7f, gy + 7f, 6f, b.roof * 0.85f, 1f);
                    }
                Ellipse(px, W, H, b.x + b.w * 0.5f, y1, b.w * 0.62f, 6f, b.roof, 1f);
                Disc(px, W, H, b.x + b.w * 0.35f, y1 + 5f, 5f, b.roof * 0.85f, 1f);
                Disc(px, W, H, b.x + b.w * 0.65f, y1 + 6f, 6f, b.roof * 0.85f, 1f);
            }

            if (world == 0)
            {
                // Wind turbines and a glass dome on the shore.
                foreach (float tx in new[] { W * 0.06f, W * 0.94f })
                {
                    float baseY = H * 0.13f, hubY = H * 0.3f;
                    Rect(px, W, H, tx - 1.5f, baseY, tx + 1.5f, hubY, Color.white, 1f);
                    for (int k = 0; k < 3; k++)
                    {
                        float ang = k * Mathf.PI * 2f / 3f + 0.4f;
                        Line(px, W, H, tx, hubY, tx + Mathf.Cos(ang) * 22f, hubY + Mathf.Sin(ang) * 22f, 1.6f, Color.white);
                    }
                }
            }
            if (world == 1)
            {
                // The maglev line sweeping between towers.
                for (int x = 0; x < W; x++)
                {
                    float y = H * 0.35f + Mathf.Sin(x / (float)W * Mathf.PI) * 18f;
                    Rect(px, W, H, x, y, x + 1, y + 2.5f, Color.white, 0.9f);
                }
            }
        }

        private static void Gardens(Color[] px, int W, int H, System.Random rng)
        {
            // Floating islands with trees, drifting beside the tower.
            for (int i = 0; i < 2; i++)
            {
                bool left = i == 0;
                float cx = left ? W * 0.1f : W * 0.9f;
                float cy = H * (0.3f + (float)rng.NextDouble() * 0.45f);
                Ellipse(px, W, H, cx, cy - 6f, 26f, 10f, new Color(0.62f, 0.52f, 0.45f), 1f);
                Ellipse(px, W, H, cx, cy, 30f, 6f, new Color(0.5f, 0.78f, 0.52f), 1f);
                Disc(px, W, H, cx - 10f, cy + 8f, 7f, new Color(0.36f, 0.66f, 0.45f), 1f);
                Disc(px, W, H, cx + 8f, cy + 10f, 9f, new Color(0.4f, 0.7f, 0.48f), 1f);
            }
        }

        private static void Vehicles(Color[] px, int W, int H, int world, int floors, System.Random rng)
        {
            if (world >= floors - 2) return;
            for (int i = 0; i < 2; i++)
            {
                bool left = rng.NextDouble() < 0.5;
                float cx = left ? W * (0.04f + (float)rng.NextDouble() * 0.1f) : W * (0.86f + (float)rng.NextDouble() * 0.1f);
                float cy = H * (0.2f + (float)rng.NextDouble() * 0.6f);
                // Dashed light trail, then the pod.
                for (int d = 1; d < 4; d++)
                    Rect(px, W, H, cx - d * 9f * (left ? -1 : 1) - 3f, cy - 0.8f, cx - d * 9f * (left ? -1 : 1) + 3f, cy + 0.8f, Color.white, 0.7f);
                Ellipse(px, W, H, cx, cy, 9f, 4f, Color.white, 1f);
                Rect(px, W, H, cx - 3f, cy, cx + 3f, cy + 2.5f, new Color(0.55f, 0.82f, 0.95f), 1f);
            }
        }

        private static void Clouds(Color[] px, int W, int H, int world, int floors, System.Random rng, bool behind)
        {
            int count = world >= 3 && world <= 12 ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                float cy = H * (float)rng.NextDouble();
                float a = (world + cy / H) / floors;
                var tint = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.8f), Mathf.Clamp01((a - 0.7f) / 0.1f));
                float alpha = behind ? 0.55f : 0.35f;
                bool left = rng.NextDouble() < 0.5;
                float cx = behind ? W * (float)rng.NextDouble() : (left ? W * 0.12f : W * 0.88f);
                float s = 0.8f + (float)rng.NextDouble() * 0.6f;
                Ellipse(px, W, H, cx, cy, 34f * s, 9f * s, tint, alpha);
                Ellipse(px, W, H, cx - 12f * s, cy + 6f * s, 18f * s, 9f * s, tint, alpha);
                Ellipse(px, W, H, cx + 10f * s, cy + 8f * s, 20f * s, 10f * s, tint, alpha);
            }
        }

        // ---------- The tower ----------

        private static void Tower(Color[] px, int W, int H, int world, int floors, WorldTheme theme)
        {
            float x0 = W * TowerLeft, x1 = W * TowerRight;
            bool top = world == floors - 1;
            float yTop = top ? H * 0.9f : H;
            float yBottom = world == 0 ? H * 0.1f : 0f;

            var steel = Color.Lerp(new Color(0.24f, 0.2f, 0.45f), theme.accent, 0.22f);
            var edge = Color.Lerp(steel, Color.white, 0.25f);
            Rect(px, W, H, x0, yBottom, x1, yTop, steel, 1f);
            // Lit edges and a glowing strip in the floor's colour on each side.
            Rect(px, W, H, x0, yBottom, x0 + 3f, yTop, edge, 1f);
            Rect(px, W, H, x1 - 3f, yBottom, x1, yTop, edge, 1f);
            Rect(px, W, H, x0 + 8f, yBottom, x0 + 11f, yTop, theme.accent, 0.8f);
            Rect(px, W, H, x1 - 11f, yBottom, x1 - 8f, yTop, theme.accent, 0.8f);

            // Barred windows along both sides of the path.
            var rng = new System.Random(500 + world);
            for (float wy = yBottom + 30f; wy < yTop - 40f; wy += 46f)
                foreach (float side in new[] { 0.09f, 0.82f })
                {
                    if (rng.NextDouble() < 0.35) continue;
                    float wx = x0 + (x1 - x0) * side;
                    Rect(px, W, H, wx, wy, wx + 18f, wy + 24f, Color.Lerp(steel, Color.black, 0.45f), 1f);
                    Rect(px, W, H, wx + 2f, wy + 2f, wx + 16f, wy + 22f, Sky((world + wy / H) / floors) * 0.8f, 0.6f);
                    for (int bar = 0; bar < 3; bar++)
                        Rect(px, W, H, wx + 4f + bar * 5f, wy, wx + 5.5f + bar * 5f, wy + 24f, new Color(0.55f, 0.55f, 0.6f), 1f);
                }

            // The floor slab at the bottom of every floor.
            Rect(px, W, H, x0 - 6f, yBottom, x1 + 6f, yBottom + 9f, Color.Lerp(steel, Color.black, 0.3f), 1f);
            Rect(px, W, H, x0 - 6f, yBottom + 9f, x1 + 6f, yBottom + 11f, theme.accent, 0.9f);

            if (top)
            {
                // The roof: a parapet, an antenna and WARDEN's red eye watching the city.
                Rect(px, W, H, x0 - 8f, yTop - 6f, x1 + 8f, yTop + 4f, Color.Lerp(steel, Color.black, 0.3f), 1f);
                float cx = W * 0.5f;
                Rect(px, W, H, cx - 26f, yTop + 4f, cx + 26f, yTop + 34f, new Color(0.15f, 0.13f, 0.28f), 1f);
                Rect(px, W, H, cx - 22f, yTop + 8f, cx + 22f, yTop + 30f, new Color(0.35f, 0.06f, 0.12f), 1f);
                Disc(px, W, H, cx, yTop + 19f, 8f, new Color(1f, 0.3f, 0.35f), 1f);
                Disc(px, W, H, cx, yTop + 19f, 3f, Color.white, 1f);
                Rect(px, W, H, x1 - 30f, yTop + 4f, x1 - 27f, H - 4f, new Color(0.7f, 0.7f, 0.78f), 1f);
                Disc(px, W, H, x1 - 28.5f, H - 6f, 4f, new Color(1f, 0.35f, 0.4f), 1f);
            }
        }

        // ---------- Drawing helpers ----------

        private static void Blend(Color[] px, int W, int H, int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || a <= 0f) return;
            int i = y * W + x;
            px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(a));
        }

        private static void Rect(Color[] px, int W, int H, float x0, float y0, float x1, float y1, Color c, float a)
        {
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0)), ix1 = Mathf.Min(W - 1, Mathf.CeilToInt(x1) - 1);
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(y0)), iy1 = Mathf.Min(H - 1, Mathf.CeilToInt(y1) - 1);
            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                    Blend(px, W, H, x, y, c, a);
        }

        private static void Disc(Color[] px, int W, int H, float cx, float cy, float r, Color c, float a) =>
            Ellipse(px, W, H, cx, cy, r, r, c, a);

        private static void Ellipse(Color[] px, int W, int H, float cx, float cy, float rx, float ry, Color c, float a)
        {
            int x0 = Mathf.FloorToInt(cx - rx - 1), x1 = Mathf.CeilToInt(cx + rx + 1);
            int y0 = Mathf.FloorToInt(cy - ry - 1), y1 = Mathf.CeilToInt(cy + ry + 1);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(H - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(W - 1, x1); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry)); // soft 1px edge
                    Blend(px, W, H, x, y, c, a * edge);
                }
        }

        private static void Line(Color[] px, int W, int H, float xa, float ya, float xb, float yb, float width, Color c)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(xa, ya), new Vector2(xb, yb)));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                Disc(px, W, H, Mathf.Lerp(xa, xb, t), Mathf.Lerp(ya, yb, t), width, c, 1f);
            }
        }

        private static void Mountains(Color[] px, int W, int H, float baseY, float peakY, Color c, System.Random rng)
        {
            float phase = (float)rng.NextDouble() * 10f;
            for (int x = 0; x < W; x++)
            {
                float t = x / (float)W;
                float h = Mathf.Lerp(baseY, peakY, 0.5f + 0.5f * Mathf.Sin(t * 9f + phase) * Mathf.Sin(t * 3.3f + phase * 0.7f));
                for (int y = Mathf.FloorToInt(baseY); y < h && y < H; y++) Blend(px, W, H, x, y, c, 1f);
            }
        }
    }
}
