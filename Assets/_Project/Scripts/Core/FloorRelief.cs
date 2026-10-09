using UnityEngine;

namespace SquashBot.Core
{
    /// <summary>The floor's relief in a level: flat, a pyramid (each world's 4th level) or terraces (its 8th).</summary>
    public enum TerrainKind { Flat, Pyramid, Terraces }

    /// <summary>
    /// The heights of the floor being played: whole steps per tile (0 = the base). Everything placed with
    /// GridView.ToWorld stands on its tile's step. A walker climbs at most one step at a time and drops at most two,
    /// the same rule for foi, the robots and the bugs.
    ///
    /// The pyramids and terraces change with the worlds, so each world's pair is new:
    /// pyramids — a low two-step hill, then taller and steeper ones, twin pyramids, a pyramid with a crater on top,
    /// and finally a cluster of three of different heights; terraces — two levels in straight bands, then three
    /// levels running diagonally, then a cross of high plateaus with low corners.
    /// </summary>
    public static class FloorRelief
    {
        /// <summary>Height of one step in world units.</summary>
        public const float Step = 0.55f;
        public const int MaxClimb = 1, MaxDrop = 2;

        private static int[,] heights;

        public static bool IsFlat => heights == null;

        public static int HeightAt(GridPos p) =>
            heights != null && p.x >= 0 && p.y >= 0 && p.x < heights.GetLength(0) && p.y < heights.GetLength(1) ? heights[p.x, p.y] : 0;

        public static int MaxHeight
        {
            get
            {
                if (heights == null) return 0;
                int m = 0;
                foreach (int h in heights) m = Mathf.Max(m, h);
                return m;
            }
        }

        /// <summary>Can a walker step from one tile onto its neighbour (one step up at most, two down)?</summary>
        public static bool StepOk(GridPos from, GridPos to)
        {
            int d = HeightAt(to) - HeightAt(from);
            return d <= MaxClimb && -d <= MaxDrop;
        }

        public static void Clear() => heights = null;

        /// <summary>Raises the floor for a level (flat leaves it level) and moves the start onto low ground.</summary>
        public static void Apply(GridModel grid, TerrainKind kind, int world)
        {
            heights = null;
            if (kind == TerrainKind.Flat) return;
            var h = new int[grid.Width, grid.Height];
            if (kind == TerrainKind.Pyramid) Pyramids(grid, h, world);
            else Terraces(grid, h, world);
            // vanG's cage and the bridge to the tunnel stay at ground level.
            foreach (var p in grid.CageTiles) h[p.x, p.y] = 0;
            foreach (var p in grid.BridgeTiles) h[p.x, p.y] = 0;
            Smooth(h); // whatever the shape, every slope can be climbed a step at a time
            heights = h;
            // Start low, near the front edge (the side the camera looks from): the climb is ahead.
            GridPos best = grid.CenterFloor();
            int bestScore = int.MaxValue;
            var front = new GridPos(grid.Width / 2, grid.Height / 2);
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsFloor(p) || grid.IsSafe(p) || grid.IsCage(p) || grid.IsBridge(p)) continue;
                int score = h[p.x, p.y] * 1000 + p.x + p.y + Mathf.Abs((p.x - p.y) - (front.x - front.y)) * 2;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            if (grid.StartSpot == null || h[grid.StartSpot.Value.x, grid.StartSpot.Value.y] > 0) grid.StartSpot = best;
        }

        // ---------- Pyramids (each world's 4th level) ----------

        private static void Pyramids(GridModel grid, int[,] h, int world)
        {
            int w = grid.Width, d = grid.Height;
            int small = Mathf.Min(w, d);
            var centre = new Vector2((w - 1) * 0.5f, (d - 1) * 0.5f);
            if (world < 5)
            {
                // One hill: two steps on the first worlds, three a little later.
                Pyramid(h, centre, world < 2 ? 2 : 3, Mathf.Max(1, small / 7));
            }
            else if (world < 10)
            {
                // One tall, steep pyramid: a step on every ring.
                Pyramid(h, centre, Mathf.Clamp(small / 3, 3, 5), 1);
            }
            else if (world < 15)
            {
                // Twin pyramids along the long side, a valley between them.
                bool wide = w >= d;
                var a = wide ? new Vector2(w * 0.27f, centre.y) : new Vector2(centre.x, d * 0.27f);
                var b = wide ? new Vector2(w * 0.73f, centre.y) : new Vector2(centre.x, d * 0.73f);
                int tiers = Mathf.Clamp(small / 4, 3, 4);
                Pyramid(h, a, tiers, 1);
                Pyramid(h, b, tiers, 1);
            }
            else if (world < 20)
            {
                // A big pyramid with a crater on top: climb the rim, drop into the hollow (where the monster waits).
                int tiers = Mathf.Clamp(small / 3, 3, 5);
                Pyramid(h, centre, tiers, 1);
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < d; z++)
                        if (Cheb(x, z, centre) <= 1.01f && h[x, z] >= tiers) h[x, z] = tiers - 1;
            }
            else
            {
                // A cluster of three, each a different height.
                Pyramid(h, new Vector2(w * 0.3f, d * 0.3f), 3, 1);
                Pyramid(h, new Vector2(w * 0.7f, d * 0.35f), 4, 1);
                Pyramid(h, new Vector2(w * 0.45f, d * 0.75f), 5, 1);
            }
        }

        /// <summary>A stepped pyramid: <paramref name="tiers"/> steps up to its top, each ring <paramref name="ring"/> tiles wide.</summary>
        private static void Pyramid(int[,] h, Vector2 centre, int tiers, int ring)
        {
            for (int x = 0; x < h.GetLength(0); x++)
                for (int z = 0; z < h.GetLength(1); z++)
                {
                    int level = tiers - Mathf.FloorToInt(Cheb(x, z, centre) / ring);
                    h[x, z] = Mathf.Max(h[x, z], Mathf.Clamp(level, 0, tiers));
                }
        }

        private static float Cheb(int x, int z, Vector2 c) => Mathf.Max(Mathf.Abs(x - c.x), Mathf.Abs(z - c.y));

        // ---------- Terraces (each world's 8th level) ----------

        private static void Terraces(GridModel grid, int[,] h, int world)
        {
            int w = grid.Width, d = grid.Height;
            if (world < 8)
            {
                // Two levels in straight bands across the floor: low, high, low, high...
                int band = Mathf.Max(2, w / 5);
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < d; z++)
                        h[x, z] = Zigzag(x / band, 2);
            }
            else if (world < 16)
            {
                // Three levels running diagonally, up and back down like a staircase wave.
                int band = Mathf.Max(2, (w + d) / 10);
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < d; z++)
                        h[x, z] = Zigzag((x + z) / band, 3);
            }
            else
            {
                // A cross of high plateaus over low corners, the middle highest: four levels.
                float cx = (w - 1) * 0.5f, cz = (d - 1) * 0.5f;
                int arm = Mathf.Max(1, Mathf.Min(w, d) / 6);
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < d; z++)
                    {
                        float ax = Mathf.Abs(x - cx), az = Mathf.Abs(z - cz);
                        int across = Mathf.FloorToInt(Mathf.Min(ax, az) / arm);   // distance from the cross's arms
                        int along = Mathf.FloorToInt(Mathf.Max(ax, az) / (arm * 2f)); // distance from the middle
                        h[x, z] = Mathf.Clamp(3 - across - (along > 1 ? 1 : 0), 0, 3);
                    }
                Smooth(h);
            }
        }

        /// <summary>0, 1, .. top, .. 1, 0, 1 .. for band index i: neighbouring bands are always one step apart.</summary>
        private static int Zigzag(int i, int levels)
        {
            int period = (levels - 1) * 2;
            int k = i % period;
            return k < levels ? k : period - k;
        }

        /// <summary>Lowers any tile more than one step above a neighbour, so every slope can be climbed.</summary>
        private static void Smooth(int[,] h)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int x = 0; x < h.GetLength(0); x++)
                    for (int z = 0; z < h.GetLength(1); z++)
                    {
                        int low = h[x, z];
                        if (x > 0) low = Mathf.Min(low, h[x - 1, z]);
                        if (z > 0) low = Mathf.Min(low, h[x, z - 1]);
                        if (x < h.GetLength(0) - 1) low = Mathf.Min(low, h[x + 1, z]);
                        if (z < h.GetLength(1) - 1) low = Mathf.Min(low, h[x, z + 1]);
                        if (h[x, z] > low + 1) { h[x, z] = low + 1; changed = true; }
                    }
            }
        }
    }
}
