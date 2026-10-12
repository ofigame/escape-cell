using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The open world's roads: from one battle arena to the next a long mountain road, winding north in S bends between
    /// the peaks, five tiles wide, opening every so often into a clearing. Some clearings hold a band of robots, some a
    /// chest guarded by poison-spitting monsters; the road ends at the next arena's gate. Each road is laid out the
    /// same every time (seeded by the arena it leads to); later roads are longer and busier.
    /// </summary>
    public static class TrailMap
    {
        public enum ClearingKind { Robots, Chest }

        public struct Clearing
        {
            public int x, y, radius;
            public ClearingKind kind;
        }

        /// <summary>The road laid out last (grid coordinates): where foi starts, the gate tile, the clearings.</summary>
        public static GridPoint Start, Gate;
        public static readonly List<Clearing> Clearings = new List<Clearing>();

        public struct GridPoint { public int x, y; public GridPoint(int x, int y) { this.x = x; this.y = y; } }

        /// <summary>Rows top (north) to bottom; <paramref name="bounds"/> = x0, y0, x1, y1 of the road's box.</summary>
        public static string[] Layout(int target, out int[] bounds)
        {
            var rng = new System.Random(7700 + target * 53);
            float k = target / (float)(BattleArenas.Count - 1);
            int bends = 7 + Mathf.RoundToInt(k * 5f);          // 7 … 12 bends
            int step = 13;                                       // rows between bends
            int w = 64, h = bends * step + 26;
            var g = new char[h][];
            for (int r = 0; r < h; r++) g[r] = new string('.', w).ToCharArray();
            void Disc(float cx, float cr, float radius)
            {
                for (int r = Mathf.FloorToInt(cr - radius); r <= Mathf.CeilToInt(cr + radius); r++)
                    for (int x = Mathf.FloorToInt(cx - radius); x <= Mathf.CeilToInt(cx + radius); x++)
                    {
                        if (r < 2 || r >= h - 2 || x < 2 || x >= w - 2) continue;
                        float dx = x - cx, dr = r - cr;
                        if (dx * dx + dr * dr <= radius * radius) g[r][x] = '#';
                    }
            }
            // The waypoints: from the bottom (south) up, swinging left and right.
            var points = new List<Vector2>();
            int mid = w / 2;
            for (int i = 0; i <= bends; i++)
            {
                float swing = (i % 2 == 0 ? -1f : 1f) * (10f + (float)rng.NextDouble() * 9f);
                if (i == 0 || i == bends) swing *= 0.3f;
                points.Add(new Vector2(mid + swing, h - 8 - i * step));
            }
            // The road between them: a smooth curve brushed five tiles wide.
            for (int i = 0; i + 1 < points.Count; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var c1 = new Vector2(a.x, a.y - step * 0.5f);
                var c2 = new Vector2(b.x, b.y + step * 0.5f);
                for (float t = 0f; t <= 1f; t += 0.02f)
                {
                    float u = 1f - t;
                    var p = u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b;
                    Disc(p.x, p.y, 2.6f);
                }
            }
            // Clearings at some bends: robots and chests in turn (the first stays quiet, the last is the gate's square).
            Clearings.Clear();
            for (int i = 1; i < points.Count - 1; i++)
            {
                if (i % 2 == 0 && rng.Next(3) == 0) continue;
                var p = points[i];
                int radius = 5 + rng.Next(2);
                Disc(p.x, p.y, radius);
                var kind = (Clearings.Count % 3 == 1) ? ClearingKind.Chest : ClearingKind.Robots;
                Clearings.Add(new Clearing { x = Mathf.RoundToInt(p.x), y = h - 1 - Mathf.RoundToInt(p.y), radius = radius, kind = kind });
            }
            // The gate's square at the top, the gate in its north edge; foi starts at the bottom.
            var top = points[points.Count - 1];
            Disc(top.x, top.y, 5f);
            int gx = Mathf.RoundToInt(top.x), gr = Mathf.RoundToInt(top.y) - 5;
            while (gr < h - 1 && g[gr][gx] != '#') gr++;
            var bottom = points[0];
            Disc(bottom.x, bottom.y, 3.5f);
            int sx = Mathf.RoundToInt(bottom.x), sr = Mathf.RoundToInt(bottom.y);
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
