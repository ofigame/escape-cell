using System;
using System.Collections.Generic;

namespace SquashBot.Core
{
    public enum PlatformShape
    {
        Square,
        L,
        Step,
        T,
        Plus,
        U,
        Ring
    }

    /// <summary>
    /// Builds platform layouts: a square of cells with parts cut away (L, step, T, plus, U, ring) and optional
    /// fixed obstacles, always keeping the walkable tiles connected. Seeded, so a level always gets the same shape.
    /// Output rows use '#' floor, '.' no tile, 'X' obstacle (row 0 = far edge), the format GridModel reads.
    /// </summary>
    public static class Layouts
    {
        public static string[] Generate(int size, PlatformShape shape, int obstacles, int seed)
        {
            var rng = new Random(seed);
            var floor = new bool[size, size];
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    floor[x, y] = true;

            Cut(floor, size, shape);
            floor = Orient(floor, size, rng.Next(8));

            var walls = new bool[size, size];
            PlaceObstacles(floor, walls, size, obstacles, rng);

            var rows = new string[size];
            for (int row = 0; row < size; row++)
            {
                var chars = new char[size];
                int y = size - 1 - row;
                for (int x = 0; x < size; x++)
                    chars[x] = !floor[x, y] ? '.' : walls[x, y] ? 'X' : '#';
                rows[row] = new string(chars);
            }
            return rows;
        }

        private static void Clear(bool[,] floor, int x0, int y0, int w, int h)
        {
            for (int x = x0; x < x0 + w; x++)
                for (int y = y0; y < y0 + h; y++)
                    floor[x, y] = false;
        }

        private static void Cut(bool[,] f, int n, PlatformShape shape)
        {
            int half = n / 2;
            int third = Math.Max(1, n / 3);
            switch (shape)
            {
                case PlatformShape.L:
                    Clear(f, n - half, n - half, half, half);
                    break;
                case PlatformShape.Step:
                    // A long rectangle with a step cut out of one end, like a staircase.
                    Clear(f, n - half, n - third, half, third);
                    Clear(f, 0, 0, third, third);
                    break;
                case PlatformShape.T:
                    Clear(f, 0, 0, third, half);
                    Clear(f, n - third, 0, third, half);
                    break;
                case PlatformShape.Plus:
                    Clear(f, 0, 0, third, third);
                    Clear(f, n - third, 0, third, third);
                    Clear(f, 0, n - third, third, third);
                    Clear(f, n - third, n - third, third, third);
                    break;
                case PlatformShape.U:
                    Clear(f, third, n - half, n - 2 * third, half);
                    break;
                case PlatformShape.Ring:
                {
                    int hole = n % 2 == 0 ? 2 : 1;
                    int start = (n - hole) / 2;
                    Clear(f, start, start, hole, hole);
                    break;
                }
            }
        }

        /// <summary>One of the 8 rotations / mirrors, so each shape appears in many orientations.</summary>
        private static bool[,] Orient(bool[,] f, int n, int variant)
        {
            var o = new bool[n, n];
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                {
                    int sx = x, sy = y;
                    for (int r = 0; r < (variant & 3); r++)
                    {
                        int t = sx;
                        sx = sy;
                        sy = n - 1 - t;
                    }
                    if ((variant & 4) != 0) sx = n - 1 - sx;
                    o[x, y] = f[sx, sy];
                }
            return o;
        }

        private static void PlaceObstacles(bool[,] floor, bool[,] walls, int n, int count, Random rng)
        {
            int floorCount = 0;
            foreach (bool b in floor) if (b) floorCount++;

            var center = (n / 2, n / 2);
            for (int attempt = 0; attempt < 60 && count > 0; attempt++)
            {
                int x = rng.Next(n), y = rng.Next(n);
                if (!floor[x, y] || walls[x, y]) continue;
                if (Math.Abs(x - center.Item1) + Math.Abs(y - center.Item2) <= 1) continue; // keep the start area open
                if (NeighbourWall(walls, n, x, y)) continue;                               // spread them out
                if (floorCount - 1 < n * n * 0.55f) break;                                  // never cramp the platform

                walls[x, y] = true;
                if (!Connected(floor, walls, n))
                {
                    walls[x, y] = false;
                    continue;
                }
                floorCount--;
                count--;
            }
        }

        private static bool NeighbourWall(bool[,] walls, int n, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < n && ny < n && walls[nx, ny]) return true;
                }
            return false;
        }

        /// <summary>Every walkable tile can reach every other one (obstacles must never split the platform).</summary>
        private static bool Connected(bool[,] floor, bool[,] walls, int n)
        {
            var seen = new bool[n, n];
            var stack = new Stack<(int, int)>();
            int total = 0, reached = 0;
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                    if (floor[x, y] && !walls[x, y])
                    {
                        total++;
                        if (stack.Count == 0) { stack.Push((x, y)); seen[x, y] = true; }
                    }

            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Pop();
                reached++;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= n || ny >= n || seen[nx, ny] || !floor[nx, ny] || walls[nx, ny]) continue;
                    seen[nx, ny] = true;
                    stack.Push((nx, ny));
                }
            }
            return reached == total;
        }
    }
}
