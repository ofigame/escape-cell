using System;
using System.Collections.Generic;

namespace SquashBot.Core
{
    /// <summary>
    /// Layouts for "journey" levels, the long platforms that take the robot from a start to a far exit:
    /// a long corridor, rooms joined by bridges, a winding path, a maze and a wide arena for the boss.
    /// Besides '#', '.' and 'X' they mark spots: 'S' start, 'K' key, 'D' door, read by GridModel.
    /// Every layout runs along +y (up the screen in the isometric view), so the camera follows the robot upward.
    /// </summary>
    public static class Journeys
    {
        private class Canvas
        {
            public readonly int w, h;
            public readonly char[,] c;

            public Canvas(int width, int height, char fill = '.')
            {
                w = width;
                h = height;
                c = new char[width, height];
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        c[x, y] = fill;
            }

            public bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;

            public void Set(int x, int y, char ch)
            {
                if (In(x, y)) c[x, y] = ch;
            }

            public void Fill(int x0, int y0, int fw, int fh, char ch)
            {
                for (int x = x0; x < x0 + fw; x++)
                    for (int y = y0; y < y0 + fh; y++)
                        Set(x, y, ch);
            }

            public string[] Rows()
            {
                var rows = new string[h];
                for (int row = 0; row < h; row++)
                {
                    var line = new char[w];
                    for (int x = 0; x < w; x++) line[x] = c[x, h - 1 - row];
                    rows[row] = new string(line);
                }
                return rows;
            }
        }

        /// <summary>
        /// A long straight floor: start and key at one end, door at the far end, a few pillars in between
        /// (never two side by side, so the way stays open).
        /// </summary>
        public static string[] Corridor(int width, int length, int pillars, int seed)
        {
            var rng = new Random(seed);
            var cv = new Canvas(width, length, '#');
            int mid = width / 2;
            cv.Set(mid, 0, 'S');
            cv.Set(mid, 1, 'K');
            cv.Set(mid, length - 1, 'D');
            ScatterPillars(cv, pillars, 3, length - 3, rng);
            return cv.Rows();
        }

        /// <summary>
        /// Square rooms joined by one-tile bridges, zigzagging upward (neighbouring rooms share one column,
        /// where the bridge runs). A key waits in every room but the last, which holds the door.
        /// </summary>
        public static string[] Rooms(int rooms, int roomSize, int seed)
        {
            var rng = new Random(seed);
            const int bridge = 2;
            int s = roomSize, off = roomSize - 1;
            var cv = new Canvas(s + off, rooms * s + (rooms - 1) * bridge);
            bool left = rng.Next(2) == 0;
            for (int r = 0; r < rooms; r++)
            {
                int x0 = left ? 0 : off;
                int y0 = r * (s + bridge);
                cv.Fill(x0, y0, s, s, '#');
                if (r == 0)
                {
                    cv.Set(x0 + s / 2, y0, 'S');
                    cv.Set(left ? x0 : x0 + s - 1, y0 + s - 1, 'K');
                }
                else if (r == rooms - 1)
                {
                    cv.Set(x0 + s / 2, y0 + s - 1, 'D');
                }
                else
                {
                    cv.Set(rng.Next(2) == 0 ? x0 : x0 + s - 1, y0 + s / 2, 'K');
                }
                if (r < rooms - 1) cv.Fill(off, y0 + s, 1, bridge, '#');
                left = !left;
            }
            return cv.Rows();
        }

        /// <summary>
        /// A two-tile-wide path that winds left and right as it climbs. Used for the collapsing-floor levels:
        /// key at the start, door at the end, no way back.
        /// </summary>
        public static string[] Winding(int length, int seed)
        {
            var rng = new Random(seed);
            const int width = 6;
            var cv = new Canvas(width, length);
            int x = 2;
            int runLeft = 2 + rng.Next(2);
            for (int y = 0; y < length; y++)
            {
                cv.Set(x, y, '#');
                cv.Set(x + 1, y, '#');
                if (--runLeft <= 0 && y < length - 3)
                {
                    // Step sideways by two, filling the corner so the path stays connected.
                    int dir = x <= 0 ? 1 : x >= width - 2 ? -1 : (rng.Next(2) == 0 ? -1 : 1);
                    int nx = Math.Max(0, Math.Min(width - 2, x + dir * 2));
                    int lo = Math.Min(x, nx), hi = Math.Max(x, nx) + 1;
                    for (int fx = lo; fx <= hi; fx++) cv.Set(fx, y, '#');
                    x = nx;
                    runLeft = 2 + rng.Next(3);
                }
            }
            // Start and key on the first row, door on the last.
            int sx = 0;
            while (sx < width && cv.c[sx, 0] != '#') sx++;
            cv.Set(sx, 0, 'S');
            cv.Set(sx + 1, 0, 'K');
            int dx = 0;
            while (dx < width && cv.c[dx, length - 1] != '#') dx++;
            cv.Set(dx, length - 1, 'D');
            return cv.Rows();
        }

        /// <summary>
        /// A braided maze (some walls knocked out, so there are loops to dodge through). Walls are low hedges.
        /// The door sits on the farthest cell; the key in a dead end well away from both.
        /// </summary>
        public static string[] Maze(int cellsX, int cellsY, int seed)
        {
            var rng = new Random(seed);
            int w = cellsX * 2 - 1, h = cellsY * 2 - 1;
            var cv = new Canvas(w, h, 'X');
            var seen = new bool[cellsX, cellsY];
            var stack = new Stack<(int, int)>();
            stack.Push((0, 0));
            seen[0, 0] = true;
            cv.Set(0, 0, '#');
            var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Peek();
                var options = new List<(int, int)>();
                foreach (var (dx, dy) in dirs)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx >= 0 && ny >= 0 && nx < cellsX && ny < cellsY && !seen[nx, ny]) options.Add((nx, ny));
                }
                if (options.Count == 0)
                {
                    stack.Pop();
                    continue;
                }
                var (ox, oy) = options[rng.Next(options.Count)];
                seen[ox, oy] = true;
                cv.Set(cx * 2 + (ox - cx), cy * 2 + (oy - cy), '#');
                cv.Set(ox * 2, oy * 2, '#');
                stack.Push((ox, oy));
            }

            // Braid: open a share of the remaining walls between two cells.
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    bool between = (x % 2 == 1) != (y % 2 == 1);
                    if (between && cv.c[x, y] == 'X' && rng.NextDouble() < 0.28) cv.Set(x, y, '#');
                }

            var dist = Distances(cv, 0, 0);
            var (doorX, doorY) = Farthest(cv, dist);
            var fromDoor = Distances(cv, doorX, doorY);

            // Key: the dead end (or failing that, any cell) that is far from both start and door.
            int best = -1, kx = 0, ky = 0;
            for (int x = 0; x < w; x += 2)
                for (int y = 0; y < h; y += 2)
                {
                    if (cv.c[x, y] != '#' || dist[x, y] < 0 || fromDoor[x, y] < 0) continue;
                    int open = 0;
                    foreach (var (dx, dy) in dirs)
                        if (cv.In(x + dx, y + dy) && cv.c[x + dx, y + dy] != 'X') open++;
                    int score = Math.Min(dist[x, y], fromDoor[x, y]) + (open == 1 ? 4 : 0);
                    if (score > best && !(x == doorX && y == doorY) && !(x == 0 && y == 0))
                    {
                        best = score;
                        kx = x;
                        ky = y;
                    }
                }

            cv.Set(0, 0, 'S');
            cv.Set(kx, ky, 'K');
            cv.Set(doorX, doorY, 'D');
            return cv.Rows();
        }

        /// <summary>An open square arena with a few pillars to hide behind: the boss fight against WARDEN.</summary>
        public static string[] Arena(int size, int pillars, int seed)
        {
            var rng = new Random(seed);
            var cv = new Canvas(size, size, '#');
            cv.Set(size / 2, 0, 'S');
            ScatterPillars(cv, pillars, 2, size - 1, rng);
            return cv.Rows();
        }

        private static void ScatterPillars(Canvas cv, int count, int yMin, int yMax, Random rng)
        {
            for (int attempt = 0; attempt < 80 && count > 0; attempt++)
            {
                int x = rng.Next(cv.w), y = yMin + rng.Next(Math.Max(1, yMax - yMin));
                if (cv.c[x, y] != '#') continue;
                bool crowded = false;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (cv.In(x + dx, y + dy) && cv.c[x + dx, y + dy] == 'X') crowded = true;
                if (crowded) continue;
                cv.Set(x, y, 'X');
                count--;
            }
        }

        private static int[,] Distances(Canvas cv, int sx, int sy)
        {
            var d = new int[cv.w, cv.h];
            for (int x = 0; x < cv.w; x++)
                for (int y = 0; y < cv.h; y++)
                    d[x, y] = -1;
            var queue = new Queue<(int, int)>();
            d[sx, sy] = 0;
            queue.Enqueue((sx, sy));
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (!cv.In(nx, ny) || cv.c[nx, ny] == 'X' || cv.c[nx, ny] == '.' || d[nx, ny] >= 0) continue;
                    d[nx, ny] = d[x, y] + 1;
                    queue.Enqueue((nx, ny));
                }
            }
            return d;
        }

        private static (int, int) Farthest(Canvas cv, int[,] d)
        {
            int best = -1, bx = 0, by = 0;
            for (int x = 0; x < cv.w; x++)
                for (int y = 0; y < cv.h; y++)
                    if (d[x, y] > best)
                    {
                        best = d[x, y];
                        bx = x;
                        by = y;
                    }
            return (bx, by);
        }
    }
}
