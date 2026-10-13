using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The journey's roads: from one floor to the next (or to a world's battle arena) a road, five tiles wide, winding
    /// north between the peaks and opening every so often into a clearing. Some clearings hold a band of robots, some a
    /// chest guarded by robots, poison spitters and a vanG. Every road has a shape of its own — a snake of tight bends, a
    /// gentle sway, one wide C, sharp zigzags, a big S, a spiral that winds round on itself, or a fork round an island —
    /// and is laid out the same every time (seeded). It ends at a staircase up to the next floor or at an arena's gate.
    /// </summary>
    public static class TrailMap
    {
        public enum ClearingKind { Robots, Chest }

        public enum Shape { Snake, Gentle, WideC, ZigZag, BigS, Spiral, Fork }

        public struct Clearing
        {
            public int x, y, radius;
            public ClearingKind kind;
        }

        /// <summary>The road laid out last (grid coordinates): where foi starts, the end tile (stairs or gate), the clearings.</summary>
        public static GridPoint Start, Gate;
        public static readonly List<Clearing> Clearings = new List<Clearing>();
        /// <summary>The shape of the road laid out last.</summary>
        public static Shape LastShape;

        /// <summary>Chests on a battle-arena road (the old arena mode); the journey's roads have one.</summary>
        public const int ChestCount = 5;

        public struct GridPoint { public int x, y; public GridPoint(int x, int y) { this.x = x; this.y = y; } }

        /// <summary>The old arena mode's road to arena <paramref name="target"/> (five chests, a snake of bends).</summary>
        public static string[] Layout(int target, out int[] bounds)
        {
            float k = target / (float)(BattleArenas.Count - 1);
            return Layout(7700 + target * 53, Shape.Snake, 8 + Mathf.RoundToInt(k * 5f), ChestCount, out bounds);
        }

        /// <summary>The shape of the journey's road before floor <paramref name="level"/>: never the same twice in a row.</summary>
        public static Shape ShapeFor(int level)
        {
            int n = System.Enum.GetValues(typeof(Shape)).Length;
            int prev = level > 0 ? RawShape(level - 1, n) : -1;
            int s = RawShape(level, n);
            if (s == prev) s = (s + 1 + new System.Random(level * 31 + 5).Next(n - 1)) % n;
            return (Shape)s;
        }

        private static int RawShape(int level, int n) => new System.Random(4100 + level * 97).Next(n);

        /// <summary>
        /// A road of the given shape. <paramref name="size"/> sets its length (about the number of bends); the chests are
        /// spread along it. Rows top (north) to bottom; <paramref name="bounds"/> = x0, y0, x1, y1 of the road's box.
        /// </summary>
        public static string[] Layout(int seed, Shape shape, int size, int chests, out int[] bounds)
        {
            var rng = new System.Random(seed);
            LastShape = shape;
            size = Mathf.Max(3, size);
            // The waypoints from the bottom (south) up; their y counts up from the bottom (a grid y).
            var pts = new List<Vector2>();
            int w = 64;
            float mid = w / 2f;
            bool curved = true;
            List<Vector2> branch = null; // the fork's second way
            switch (shape)
            {
                case Shape.Snake:
                    for (int i = 0; i <= size; i++)
                        pts.Add(new Vector2(mid + (i % 2 == 0 ? -1f : 1f) * (i == 0 || i == size ? 4f : 10f + (float)rng.NextDouble() * 9f), 8 + i * 12));
                    break;
                case Shape.Gentle:
                    for (int i = 0; i <= size; i++)
                        pts.Add(new Vector2(mid + Mathf.Sin(i * 0.9f + (float)rng.NextDouble()) * 7f, 8 + i * 14));
                    break;
                case Shape.WideC:
                {
                    // One wide sweep round to the right and back up to the end.
                    curved = false;
                    w = 84; mid = w / 2f;
                    float r = 24f + size;
                    int steps = 16 + size * 2;
                    for (int i = 0; i <= steps; i++)
                    {
                        float a = Mathf.Lerp(-Mathf.PI * 0.6f, Mathf.PI * 0.75f, i / (float)steps);
                        pts.Add(new Vector2(mid - 6f + Mathf.Cos(a) * r * 0.95f, 10f + r + Mathf.Sin(a) * r));
                    }
                    break;
                }
                case Shape.ZigZag:
                    curved = false;
                    for (int i = 0; i <= size + 2; i++)
                        pts.Add(new Vector2(mid + (i % 2 == 0 ? -1f : 1f) * (i == 0 ? 2f : 18f), 8 + i * 13));
                    break;
                case Shape.BigS:
                {
                    curved = false;
                    w = 72; mid = w / 2f;
                    int steps = 20 + size * 3;
                    float len = 30f + size * 12f;
                    for (int i = 0; i <= steps; i++)
                    {
                        float t = i / (float)steps;
                        pts.Add(new Vector2(mid + Mathf.Sin(t * Mathf.PI * 2f) * 24f, 8f + t * len));
                    }
                    break;
                }
                case Shape.Spiral:
                {
                    // Round and round, widening out from the middle: the end is on the outside.
                    curved = false;
                    w = 88; mid = w / 2f;
                    float turns = 1.5f + size * 0.12f;
                    int steps = Mathf.RoundToInt(turns * 24f);
                    float cy = 48f;
                    for (int i = 0; i <= steps; i++)
                    {
                        float t = i / (float)steps;
                        float a = -Mathf.PI * 0.5f + t * turns * Mathf.PI * 2f;
                        float rr = Mathf.Lerp(8f, 38f, t);
                        pts.Add(new Vector2(mid + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr));
                    }
                    break;
                }
                default: // Fork: a straight run, then two ways round an island that meet again, then on.
                {
                    curved = false;
                    w = 72; mid = w / 2f;
                    int len = 24 + size * 8;
                    pts.Add(new Vector2(mid, 8));
                    pts.Add(new Vector2(mid, 20));
                    branch = new List<Vector2> { new Vector2(mid, 20) };
                    for (int i = 1; i <= 12; i++)
                    {
                        float t = i / 13f;
                        float off = Mathf.Sin(t * Mathf.PI) * 18f;
                        pts.Add(new Vector2(mid - off, 20 + t * len));
                        branch.Add(new Vector2(mid + off, 20 + t * len));
                    }
                    pts.Add(new Vector2(mid, 20 + len));
                    branch.Add(new Vector2(mid, 20 + len));
                    pts.Add(new Vector2(mid + 4f, 30 + len));
                    pts.Add(new Vector2(mid, 40 + len));
                    break;
                }
            }
            float top = 0f;
            foreach (var p in pts) top = Mathf.Max(top, p.y);
            if (branch != null) foreach (var p in branch) top = Mathf.Max(top, p.y);
            int h = Mathf.CeilToInt(top) + 16;
            var g = new char[h][];
            for (int r = 0; r < h; r++) g[r] = new string('.', w).ToCharArray();
            // Grid rows run top-down: a point's row is h - 1 - y.
            void Disc(float cx, float cy, float radius)
            {
                float cr = h - 1 - cy;
                for (int r = Mathf.FloorToInt(cr - radius); r <= Mathf.CeilToInt(cr + radius); r++)
                    for (int x = Mathf.FloorToInt(cx - radius); x <= Mathf.CeilToInt(cx + radius); x++)
                    {
                        if (r < 2 || r >= h - 2 || x < 2 || x >= w - 2) continue;
                        float dx = x - cx, dr = r - cr;
                        if (dx * dx + dr * dr <= radius * radius) g[r][x] = '#';
                    }
            }
            void Brush(List<Vector2> way)
            {
                for (int i = 0; i + 1 < way.Count; i++)
                {
                    var a = way[i];
                    var b = way[i + 1];
                    if (!curved)
                    {
                        float len = Mathf.Max(1f, (b - a).magnitude);
                        for (float t = 0f; t <= 1f; t += 0.5f / len) { var p = Vector2.Lerp(a, b, t); Disc(p.x, p.y, 2.6f); }
                        continue;
                    }
                    var c1 = new Vector2(a.x, a.y + (b.y - a.y) * 0.5f);
                    var c2 = new Vector2(b.x, b.y - (b.y - a.y) * 0.5f);
                    for (float t = 0f; t <= 1f; t += 0.02f)
                    {
                        float u = 1f - t;
                        var p = u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b;
                        Disc(p.x, p.y, 2.6f);
                    }
                }
            }
            Brush(pts);
            if (branch != null) Brush(branch);
            // Clearings spread along the way (not the very start or end).
            Clearings.Clear();
            int spots = Mathf.Clamp(size - 1, 2, 9);
            for (int i = 1; i <= spots; i++)
            {
                float t = i / (float)(spots + 1);
                var p = pts[Mathf.Clamp(Mathf.RoundToInt(t * (pts.Count - 1)), 1, pts.Count - 2)];
                int radius = 5 + rng.Next(2);
                Disc(p.x, p.y, radius);
                Clearings.Add(new Clearing { x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y), radius = radius, kind = ClearingKind.Robots });
            }
            int n = Clearings.Count;
            int chestsHere = Mathf.Min(chests, n);
            for (int j = 0; j < chestsHere; j++)
            {
                // One chest: two thirds of the way along; several: spread out evenly.
                int at = chestsHere == 1 ? Mathf.Clamp(Mathf.RoundToInt(n * 0.66f), 0, n - 1) : Mathf.Min(n - 1, Mathf.FloorToInt((j + 0.5f) * n / chestsHere));
                var c = Clearings[at];
                c.kind = ClearingKind.Chest;
                Clearings[at] = c;
            }
            // The end's square at the last point, the end tile in its north edge; foi starts at the first point.
            var last = pts[pts.Count - 1];
            Disc(last.x, last.y, 5f);
            int gx = Mathf.Clamp(Mathf.RoundToInt(last.x), 2, w - 3);
            int gr = Mathf.Clamp(h - 1 - Mathf.RoundToInt(last.y) - 5, 2, h - 3);
            while (gr < h - 1 && g[gr][gx] != '#') gr++;
            var first = pts[0];
            Disc(first.x, first.y, 3.5f);
            int sx = Mathf.RoundToInt(first.x), sr = h - 1 - Mathf.RoundToInt(first.y);
            g[sr][sx] = 'S';
            var rows = new List<string>();
            foreach (var row in g) rows.Add(new string(row));
            Start = new GridPoint(sx, h - 1 - sr);
            Gate = new GridPoint(gx, h - 1 - gr);
            bounds = new[] { 2, 2, w - 3, h - 3 };
            BattleArenas.LastBuildings.Clear(); // no city on a road
            BattleArenas.LastPosts.Clear();
            return rows.ToArray();
        }
    }
}
