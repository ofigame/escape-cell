using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Core
{
    /// <summary>
    /// The wide floors of the campaign: big open shapes (square, ring, diamond, oval, plus, horseshoe, islands,
    /// heart, star, gear, castle, moat island, crescent, rooms...), each unlocked from a level on, so early floors
    /// are plain small squares and the shapes vary more and more. Layout rows use '#' floor, '.' none, 'X' pillar
    /// (row 0 = far edge), the format <see cref="GridModel"/> reads. Every result is checked to be one connected
    /// floor; a shape that falls apart at a small size gives way to a square.
    /// </summary>
    public static class FloorShapes
    {
        private delegate char Cell(float u, float v, int x, int y, int w, int h);

        /// <summary>A shape: its name, the first level index it may appear on, and its cell rule (u, v from -1 to 1).</summary>
        private static readonly (string name, int from, float aspect, Cell cell)[] Shapes =
        {
            ("Square", 0, 1f, (u, v, x, y, w, h) => '#'),
            ("Plaza", 8, 0.85f, (u, v, x, y, w, h) => x % 6 == 3 && y % 5 == 2 && x < w - 2 && y < h - 2 ? 'X' : '#'),
            ("Pit", 12, 1f, (u, v, x, y, w, h) => Mathf.Abs(u) < 0.28f && Mathf.Abs(v) < 0.28f ? '.' : '#'),
            ("Oval", 16, 0.72f, (u, v, x, y, w, h) => u * u + v * v <= 1.02f ? '#' : '.'),
            ("WideL", 20, 0.92f, (u, v, x, y, w, h) => u < 0f || v < -0.1f ? '#' : '.'),
            ("Plus", 26, 1f, (u, v, x, y, w, h) => Mathf.Abs(u) < 0.42f || Mathf.Abs(v) < 0.42f ? '#' : '.'),
            ("Ring", 32, 1f, (u, v, x, y, w, h) => { float r = u * u + v * v; return r <= 1.02f && r > 0.18f ? '#' : '.'; }),
            ("Horseshoe", 38, 0.92f, (u, v, x, y, w, h) => Mathf.Abs(u) < 0.36f && v > -0.15f ? '.' : '#'),
            ("Diamond", 44, 1f, (u, v, x, y, w, h) => Mathf.Abs(u) + Mathf.Abs(v) <= 1.04f ? '#' : '.'),
            ("Holes", 50, 0.9f, (u, v, x, y, w, h) => (x / 2 + y / 2) % 3 == 0 && x % 2 == 0 && y % 2 == 0 && x > 1 && y > 1 && x < w - 2 && y < h - 2 ? '.' : '#'),
            ("Butterfly", 58, 0.7f, (u, v, x, y, w, h) => Sq((u + 0.5f) / 0.52f) + Sq(v / 0.95f) <= 1f || Sq((u - 0.5f) / 0.52f) + Sq(v / 0.95f) <= 1f || Mathf.Abs(v) < 0.3f ? '#' : '.'),
            ("Castle", 66, 1f, (u, v, x, y, w, h) =>
            {
                bool body = Mathf.Abs(u) < 0.82f && Mathf.Abs(v) < 0.82f;
                bool tower = Sq(Mathf.Abs(u) - 0.78f) + Sq(Mathf.Abs(v) - 0.78f) <= 0.07f;
                if (!body && !tower) return '.';
                return (Mathf.Abs(u) - 0.38f) * (Mathf.Abs(u) - 0.38f) < 0.003f && (Mathf.Abs(v) - 0.38f) * (Mathf.Abs(v) - 0.38f) < 0.003f ? 'X' : '#';
            }),
            ("Gear", 74, 1f, (u, v, x, y, w, h) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                bool tooth = Mathf.Repeat(a * 8f / Mathf.PI, 2f) < 1f;
                return (r <= 0.76f || (tooth && r <= 1.02f)) && r > 0.26f ? '#' : '.';
            }),
            ("Moat", 82, 1f, (u, v, x, y, w, h) =>
            {
                float c = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                bool moat = c > 0.42f && c < 0.64f;
                bool bridge = Mathf.Abs(u) < 0.13f || Mathf.Abs(v) < 0.13f;
                return moat && !bridge ? '.' : '#';
            }),
            ("Islands", 90, 1f, (u, v, x, y, w, h) =>
            {
                bool island = Mathf.Abs(u) > 0.24f && Mathf.Abs(v) > 0.24f;
                bool centre = Mathf.Abs(u) < 0.3f && Mathf.Abs(v) < 0.3f;
                bool bridge = (Mathf.Abs(u) < 0.12f || Mathf.Abs(v) < 0.12f) && Mathf.Abs(u) < 0.62f && Mathf.Abs(v) < 0.62f;
                bool spoke = Mathf.Abs(Mathf.Abs(u) - Mathf.Abs(v)) < 0.12f;
                return island || centre || bridge || spoke ? '#' : '.';
            }),
            ("Star", 100, 1f, (u, v, x, y, w, h) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                return r <= 0.58f + 0.44f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 1.5f) ? '#' : '.';
            }),
            ("Zigzag", 110, 0.66f, (u, v, x, y, w, h) => Mathf.Abs(v - 0.55f * Mathf.Sin(u * Mathf.PI)) < 0.6f ? '#' : '.'),
            ("Crescent", 120, 0.85f, (u, v, x, y, w, h) => u * u + v * v <= 1.02f && Sq(u - 0.45f) + Sq(v - 0.25f) > 0.36f ? '#' : '.'),
            ("Heart", 130, 0.92f, (u, v, x, y, w, h) =>
            {
                float a = u * u + Sq(-v * 1.1f + 0.15f) - 0.75f;
                return a * a * a - u * u * Mathf.Pow(-v * 1.1f + 0.15f, 3f) * 0.8f <= 0f ? '#' : '.';
            }),
            ("Rooms", 140, 0.92f, (u, v, x, y, w, h) =>
            {
                bool wallX = x == w / 2, wallY = y == h / 2;
                bool door = (wallX && Mathf.Abs(Mathf.Abs(v) - 0.5f) < 0.16f) || (wallY && Mathf.Abs(Mathf.Abs(u) - 0.5f) < 0.16f);
                return (wallX || wallY) && !door ? 'X' : '#';
            }),
            ("Broken", 150, 0.9f, (u, v, x, y, w, h) => Mathf.PerlinNoise(x * 0.35f + 3.1f, y * 0.35f + 7.7f) < 0.27f ? '.' : '#'),
        };

        private static float Sq(float v) => v * v;

        /// <summary>
        /// The floor of level <paramref name="index"/>: <paramref name="size"/> tiles across, a shape picked among those
        /// unlocked by then (seeded by the level, so it never changes), the newest ones more often.
        /// </summary>
        public static string[] For(int index, int size, out string name)
        {
            var open = new List<int>();
            for (int i = 0; i < Shapes.Length; i++) if (index >= Shapes[i].from) open.Add(i);
            var rng = new System.Random(index * 7919 + 17);
            int pick = open[open.Count - 1 - Mathf.Min(open.Count - 1, (int)(Math.Pow(rng.NextDouble(), 1.6) * open.Count))];
            // A brand-new shape is shown on the level it unlocks.
            foreach (int i in open) if (Shapes[i].from == index) pick = i;
            var s = Shapes[pick];
            int w = size, h = Mathf.Max(5, Mathf.RoundToInt(size * s.aspect));
            if ((rng.Next(2) == 0) && s.aspect < 0.99f) (w, h) = (h, w); // stand some of the wide ones up
            var rows = Build(s.cell, w, h);
            if (size < 9 && s.name != "Square" || !Connected(rows, out int floor) || floor < size * size * 0.35f)
            {
                rows = Build(Shapes[0].cell, size, size);
                name = Shapes[0].name;
                return rows;
            }
            name = s.name;
            return rows;
        }

        private static string[] Build(Cell cell, int w, int h)
        {
            var rows = new string[h];
            for (int r = 0; r < h; r++)
            {
                int y = h - 1 - r;
                var chars = new char[w];
                for (int x = 0; x < w; x++)
                {
                    float u = w > 1 ? x / (float)(w - 1) * 2f - 1f : 0f;
                    float v = h > 1 ? y / (float)(h - 1) * 2f - 1f : 0f;
                    chars[x] = cell(u, v, x, y, w, h);
                }
                rows[r] = new string(chars);
            }
            return rows;
        }

        /// <summary>True when every floor tile ('#') reaches every other (pillars don't count as floor).</summary>
        private static bool Connected(string[] rows, out int floorCount)
        {
            int h = rows.Length, w = rows[0].Length;
            floorCount = 0;
            int sx = -1, sy = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (rows[y][x] == '#') { floorCount++; if (sx < 0) { sx = x; sy = y; } }
            if (sx < 0) return false;
            var seen = new bool[w, h];
            var stack = new Stack<(int, int)>();
            stack.Push((sx, sy));
            seen[sx, sy] = true;
            int reached = 0;
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                reached++;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[nx, ny] || rows[ny][nx] != '#') continue;
                    seen[nx, ny] = true;
                    stack.Push((nx, ny));
                }
            }
            return reached == floorCount;
        }
    }
}
