using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The battle arenas (main menu → ARENAS): ten huge courtyard fortresses, each a design of its own — its shape
    /// (square, hexagon, octagon, round, diamond, cross), its marble floor and its ramparts (copper, ice blocks, fire
    /// crowned stone, sandstone, pearl, glowing crystal, sky white, obsidian, star-base metal, vanG's black stone), all of
    /// them very high and thick, bigger with every arena. Much harder than the levels around them, played again and
    /// again; kill streaks bring gifts. Best streak and wins are kept on the phone.
    /// </summary>
    public static class BattleArenas
    {
        /// <summary>One arena for every world, opening with it.</summary>
        public const int Count = LevelCatalog.WorldCount;

        public enum Shape { Square, Hexagon, Octagon, Round, Diamond, Cross }

        /// <summary>How an arena looks: its shape, floor and wall colours, what crowns the walls (FortressFloor.WallFx:
        /// 0 none, 1 flames, 2 ice, 3 glow), its towers' accent and trim colours.</summary>
        public struct Look
        {
            public Shape shape;
            public Color floor, wall, accent, trim;
            public int fx;
        }

        private static readonly Look[] Looks =
        {
            new Look { shape = Shape.Square,  floor = new Color(0.86f, 0.8f, 0.7f),  wall = new Color(0.62f, 0.42f, 0.3f),  accent = new Color(1f, 0.55f, 0.2f),  trim = new Color(0.2f, 0.42f, 0.88f), fx = 0 }, // Copper Keep
            new Look { shape = Shape.Hexagon, floor = new Color(0.78f, 0.9f, 1f),   wall = new Color(0.72f, 0.9f, 1f),    accent = new Color(0.45f, 0.9f, 1f),  trim = new Color(0.85f, 0.95f, 1f), fx = 2 },  // Frost Citadel
            new Look { shape = Shape.Octagon, floor = new Color(0.62f, 0.57f, 0.54f), wall = new Color(0.42f, 0.24f, 0.2f), accent = new Color(1f, 0.45f, 0.1f),  trim = new Color(1f, 0.25f, 0.1f), fx = 1 },   // Ember Bastion
            new Look { shape = Shape.Round,   floor = new Color(0.92f, 0.85f, 0.68f), wall = new Color(0.86f, 0.72f, 0.5f), accent = new Color(0.95f, 0.6f, 0.25f), trim = new Color(0.25f, 0.6f, 0.65f), fx = 0 }, // Sandstone Fort
            new Look { shape = Shape.Hexagon, floor = new Color(0.95f, 0.94f, 0.92f), wall = new Color(0.93f, 0.91f, 0.96f), accent = new Color(1f, 0.8f, 0.45f), trim = new Color(0.7f, 0.85f, 1f), fx = 3 }, // Pearl Ramparts
            new Look { shape = Shape.Diamond, floor = new Color(0.86f, 0.8f, 0.95f), wall = new Color(0.62f, 0.45f, 0.95f), accent = new Color(0.8f, 0.5f, 1f),  trim = new Color(0.45f, 0.95f, 1f), fx = 3 },  // Crystal Stronghold
            new Look { shape = Shape.Cross,   floor = new Color(0.9f, 0.93f, 0.98f),  wall = new Color(0.95f, 0.96f, 1f),   accent = new Color(0.45f, 0.8f, 1f),  trim = new Color(1f, 0.75f, 0.35f), fx = 3 },  // Sky Fortress
            new Look { shape = Shape.Octagon, floor = new Color(0.5f, 0.5f, 0.56f),  wall = new Color(0.2f, 0.18f, 0.25f),  accent = new Color(0.7f, 0.4f, 1f),   trim = new Color(0.35f, 0.25f, 0.6f), fx = 3 }, // Night Bastion
            new Look { shape = Shape.Hexagon, floor = new Color(0.72f, 0.74f, 0.78f), wall = new Color(0.32f, 0.35f, 0.4f),  accent = new Color(0.3f, 0.95f, 1f),  trim = new Color(1f, 0.55f, 0.2f), fx = 3 },  // Star Base
            new Look { shape = Shape.Octagon, floor = new Color(0.4f, 0.3f, 0.3f),   wall = new Color(0.16f, 0.11f, 0.11f), accent = new Color(1f, 0.25f, 0.1f),  trim = new Color(0.9f, 0.7f, 0.2f), fx = 1 },   // vanG's Throne
        };

        /// <summary>The first ten have their own designs; the later worlds' arenas take their world's colours, each with
        /// its own shape and crown.</summary>
        public static Look Style(int i)
        {
            i = Mathf.Clamp(i, 0, Count - 1);
            if (i < Looks.Length) return Looks[i];
            var theme = SquashBot.Visual.WorldTheme.ForLevel(i * LevelCatalog.LevelsPerWorld);
            Color.RGBToHSV(theme.accent, out float h, out _, out _);
            return new Look
            {
                shape = (Shape)((i * 7 + 3) % 6),
                floor = Color.HSVToRGB(h, 0.12f, 0.93f),
                wall = Color.HSVToRGB(h, 0.4f, 0.5f),
                accent = theme.accent,
                trim = Color.HSVToRGB(Mathf.Repeat(h + 0.5f, 1f), 0.6f, 1f),
                fx = i % 5 == 0 ? 1 : i % 3 == 0 ? 0 : 3,
            };
        }

        /// <summary>The arena's name: its own for the first ten, its world's name after.</summary>
        public static string NameKey(int i) => i < Looks.Length ? "arena." + i : SquashBot.Visual.WorldTheme.ForLevel(i * LevelCatalog.LevelsPerWorld).key;

        /// <summary>The level (0-based) that opens an arena: its world's first level.</summary>
        public static int UnlockAt(int i) => i * LevelCatalog.LevelsPerWorld;
        /// <summary>Whose enemies it borrows: the middle of its world.</summary>
        public static int ThemeLevel(int i) => Mathf.Min(LevelCatalog.LevelCount - 1, i * LevelCatalog.LevelsPerWorld + 5);
        /// <summary>How hard, 1..5 stars for the card.</summary>
        public static int Stars(int i) => 1 + i / 5;
        /// <summary>The courtyard's side (odd, so the cell sits in the middle): huge even in the first arena, bigger every other one.</summary>
        public static int Size(int i) => 45 + 2 * (i / 2); // 45 … 69 tiles a side: a real city to cross
        /// <summary>The narrowest the courtyard's first and last rows may be: room on the north and south ramparts for
        /// the gun posts' stairs (round and diamond courtyards lose their tips).</summary>
        private const int PostRowWidth = 13;

        public static bool Unlocked(int i) => SaveData.UnlockedLevel >= UnlockAt(i) || SaveData.TestMode; // test builds: every arena

        public static int BestStreak(int i) => Prefs.GetInt("sb_arena_best_" + i, 0);
        public static int Wins(int i) => Prefs.GetInt("sb_arena_wins_" + i, 0);

        public static void Record(int i, int streak, bool won)
        {
            if (streak > BestStreak(i)) Prefs.SetInt("sb_arena_best_" + i, streak);
            if (won) Prefs.SetInt("sb_arena_wins_" + i, Wins(i) + 1);
            Prefs.Save();
        }

        /// <summary>Coins for clearing an arena (the first win pays triple).</summary>
        public static int WinCoins(int i) => (300 + i * 150) * (Wins(i) == 0 ? 3 : 1);

        /// <summary>How far in from the square's edge a row of the courtyard starts, for its shape.</summary>
        public static int Inset(Shape shape, int sz, int r)
        {
            float c = (sz - 1) * 0.5f, d = Mathf.Abs(r - c);
            switch (shape)
            {
                case Shape.Hexagon: return Mathf.RoundToInt(d * 0.55f);
                case Shape.Octagon: return Mathf.Max(0, sz / 4 - Mathf.Min(r, sz - 1 - r));
                case Shape.Round: return Mathf.RoundToInt(c - Mathf.Sqrt(Mathf.Max(0f, c * c - d * d)));
                case Shape.Diamond: return Mathf.Min(Mathf.RoundToInt(d * 0.85f), Mathf.RoundToInt(c) - 3);
                case Shape.Cross: return d <= sz / 6f ? 0 : sz / 3;
                default: return 0;
            }
        }

        /// <summary>A building of an arena city: its tiles (grid coordinates, inclusive) and how tall it stands.</summary>
        public struct Building
        {
            public int x0, y0, x1, y1;
            public float height;
        }

        /// <summary>A gun post's stair: its obstacle tiles (two rows deep against the rampart), the column of its foot
        /// tiles, the column the gun stands over, and which rampart it is on (grid coordinates).</summary>
        public struct Post
        {
            public int x0, x1, y0, y1, footX, topX;
            /// <summary>+1: the north rampart, -1: the south one.</summary>
            public int outward;
        }

        /// <summary>The buildings and gun posts of the arena laid out last (grid coordinates).</summary>
        public static readonly System.Collections.Generic.List<Building> LastBuildings = new System.Collections.Generic.List<Building>();
        public static readonly System.Collections.Generic.List<Post> LastPosts = new System.Collections.Generic.List<Post>();

        /// <summary>
        /// The arena's floor: squares joined by roads, like a city's. The first arena is the hand-drawn plan (a
        /// crossroads in the middle, a big square to the west and the east, one up the road to the north and a wide one
        /// to the south narrowing into a pass); the later ones grow from it — more squares, more and wider roads, a
        /// bigger city. The buildings stand right along the edge of the squares and roads (they are the city's walls;
        /// ramparts fill only the gaps between them), and the gun posts stand on the north edge of the northernmost
        /// square and the south edge of the southernmost one, against a rampart. No cage, no tunnel. Rows top (north) to
        /// bottom; <paramref name="bounds"/> = x0, y0, x1, y1 of the city's box.
        /// </summary>
        public static string[] Layout(int i, out int[] bounds)
        {
            var rng = new System.Random(4100 + i * 37);
            // The plan in tiles: squares (and road strips) as rectangles, rows growing southwards.
            var areas = new System.Collections.Generic.List<(int x0, int r0, int x1, int r1)>();
            var squares = new System.Collections.Generic.List<(int x0, int r0, int x1, int r1)>();
            (int x, int r) start;
            // Extra rows narrowing a square's south side towards a pass: (square index, rows, pass half width).
            var tapers = new System.Collections.Generic.List<(int sq, int rows, int half)>();
            if (i == 0)
            {
                // The hand-drawn first arena.
                squares.Add((18, 0, 39, 13));   // north
                squares.Add((2, 14, 13, 35));   // west
                squares.Add((41, 14, 57, 36));  // east
                squares.Add((12, 42, 44, 51));  // south (narrowing below into the pass)
                areas.AddRange(squares);
                areas.Add((24, 14, 31, 41));    // the road north–south
                areas.Add((14, 21, 40, 24));    // the road west–east
                tapers.Add((3, 8, 5));
                start = (28, 22);
            }
            else
            {
                // A network of squares on a jittered grid, joined by roads (a tree plus a few loops).
                int n = 5 + i / 4;
                int cols = Mathf.CeilToInt(Mathf.Sqrt(n)), rowsN = Mathf.CeilToInt(n / (float)cols);
                int cell = 26 + (i % 3) * 2 + i / 5;
                int road = 4 + (i % 3) + i / 10;
                var centres = new System.Collections.Generic.List<(int x, int r)>();
                for (int k = 0; k < n; k++)
                {
                    int cx = (k % cols) * cell + cell / 2 + rng.Next(-cell / 6, cell / 6 + 1);
                    int cy = (k / cols) * cell + cell / 2 + rng.Next(-cell / 6, cell / 6 + 1);
                    int hw = 6 + rng.Next(3 + i / 6), hh = 5 + rng.Next(3 + i / 6);
                    squares.Add((cx - hw, cy - hh, cx + hw, cy + hh));
                    centres.Add((cx, cy));
                }
                areas.AddRange(squares);
                // Roads: every square joined to its nearest already-joined one, then a few extra links.
                var joined = new System.Collections.Generic.List<int> { 0 };
                void Road(int a, int b)
                {
                    var (ax, ay) = centres[a];
                    var (bx, by) = centres[b];
                    int h = road / 2;
                    areas.Add((Mathf.Min(ax, bx) - h, ay - h, Mathf.Max(ax, bx) + h, ay - h + road - 1)); // east–west leg
                    areas.Add((bx - h, Mathf.Min(ay, by) - h, bx - h + road - 1, Mathf.Max(ay, by) + h)); // north–south leg
                }
                for (int k = 1; k < n; k++)
                {
                    int best = 0, bestD = int.MaxValue;
                    foreach (int j in joined)
                    {
                        int d = Mathf.Abs(centres[k].x - centres[j].x) + Mathf.Abs(centres[k].r - centres[j].r);
                        if (d < bestD) { bestD = d; best = j; }
                    }
                    Road(best, k);
                    joined.Add(k);
                }
                for (int e = 0; e < 1 + i / 4; e++) Road(rng.Next(n), rng.Next(n));
                // Now and then a square narrows into a pass on its south side.
                for (int k = 0; k < n; k++) if (rng.Next(3) == 0) tapers.Add((k, 4 + rng.Next(4), 3 + rng.Next(3)));
                // foi starts in the square nearest the middle.
                int mx = cols * cell / 2, my = rowsN * cell / 2, pick = 0, pd = int.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    int d = Mathf.Abs(centres[k].x - mx) + Mathf.Abs(centres[k].r - my);
                    if (d < pd) { pd = d; pick = k; }
                }
                start = centres[pick];
            }

            // Paint the plan into a grid with a margin all round for the buildings.
            const int margin = 6;
            int minX = int.MaxValue, minR = int.MaxValue, maxX = int.MinValue, maxR = int.MinValue;
            foreach (var a in areas) { minX = Mathf.Min(minX, a.x0); minR = Mathf.Min(minR, a.r0); maxX = Mathf.Max(maxX, a.x1); maxR = Mathf.Max(maxR, a.r1); }
            foreach (var t in tapers) maxR = Mathf.Max(maxR, squares[t.sq].r1 + t.rows + 3);
            int ox = margin - minX, or = margin - minR;
            int w = maxX - minX + 1 + margin * 2, h = maxR - minR + 1 + margin * 2;
            var g = new char[h][];
            for (int r = 0; r < h; r++) g[r] = new string('.', w).ToCharArray();
            void Fill(int x0, int r0, int x1, int r1)
            {
                for (int r = Mathf.Max(1, r0 + or); r <= Mathf.Min(h - 2, r1 + or); r++)
                    for (int x = Mathf.Max(1, x0 + ox); x <= Mathf.Min(w - 2, x1 + ox); x++) g[r][x] = '#';
            }
            foreach (var a in areas) Fill(a.x0, a.r0, a.x1, a.r1);
            foreach (var (sq, rows, half) in tapers)
            {
                var s = squares[sq];
                int cx = (s.x0 + s.x1) / 2;
                for (int k = 1; k <= rows; k++)
                {
                    float f = k / (float)rows;
                    int l = Mathf.RoundToInt(Mathf.Lerp(s.x0, cx - half, f)), rr = Mathf.RoundToInt(Mathf.Lerp(s.x1, cx + half, f));
                    Fill(l, s.r1 + k, rr, s.r1 + k);
                }
                Fill(cx - half, s.r1 + rows + 1, cx + half, s.r1 + rows + 3); // the pass
            }
            int sx = start.x + ox, sr = start.r + or;
            g[sr][sx] = 'S';

            // Fire pits in a few squares of the later arenas (never foi's).
            int pits = i < 2 ? 0 : 1 + i / 6;
            for (int k = 0, tries = 0; k < pits && tries < 40; tries++)
            {
                var s = squares[rng.Next(squares.Count)];
                int px = rng.Next(s.x0 + 3, Mathf.Max(s.x0 + 4, s.x1 - 3)) + ox, pr = rng.Next(s.r0 + 3, Mathf.Max(s.r0 + 4, s.r1 - 3)) + or;
                if (Mathf.Abs(px - sx) < 6 && Mathf.Abs(pr - sr) < 6) continue;
                bool ok = true;
                for (int dy = -1; dy <= 2 && ok; dy++) for (int dx = -1; dx <= 2 && ok; dx++) if (g[pr + dy][px + dx] != '#') ok = false;
                if (!ok) continue;
                for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) g[pr + dy][px + dx] = 'F';
                k++;
            }

            // The gun posts: a stretch of the north edge of the top-most floor and of the south edge of the bottom-most,
            // six tiles wide with nothing beyond it, gets a two-tile-deep stair against the rampart.
            LastPosts.Clear();
            int hh2 = h;
            bool Floor(int r, int x) => r >= 0 && r < h && x >= 0 && x < w && g[r][x] == '#';
            bool Empty(int r, int x) => r < 0 || r >= h || x < 0 || x >= w || g[r][x] == '.';
            for (int side = 0; side < 2; side++)
            {
                bool north = side == 0;
                bool placed = false;
                for (int step = 0; step < h && !placed; step++)
                {
                    int r = north ? step : h - 1 - step;
                    for (int x = 2; x < w - 7 && !placed; x++)
                    {
                        bool fits = true;
                        for (int dx = 0; dx < 6 && fits; dx++)
                        {
                            int xx = x + dx;
                            int inner = north ? r + 1 : r - 1, beyond = north ? r - 1 : r + 1;
                            fits = Floor(r, xx) && Floor(inner, xx) && Floor(north ? r + 2 : r - 2, xx) && Empty(beyond, xx);
                        }
                        if (!fits) continue;
                        // Stair tiles on the middle four of the six, the foot on the side.
                        int rIn = north ? r + 1 : r - 1;
                        for (int dx = north ? 1 : 1; dx <= 4; dx++) { g[r][x + dx] = 'X'; g[rIn][x + dx] = 'X'; }
                        var p = new Post { x0 = x + 1, x1 = x + 4, outward = north ? 1 : -1 };
                        p.footX = north ? x : x + 5;
                        p.topX = north ? x + 4 : x + 1;
                        int ya = hh2 - 1 - r, yb = hh2 - 1 - rIn;
                        p.y0 = Mathf.Min(ya, yb);
                        p.y1 = Mathf.Max(ya, yb);
                        LastPosts.Add(p);
                        placed = true;
                    }
                }
            }

            // The buildings: blocks packed along the edge of the floor, outside it (touching it, so they stand as the
            // city's walls), up to three tiles deep, a tile of gap between blocks; never round the gun posts' ramparts.
            var near = new int[h, w];
            for (int r = 0; r < h; r++)
                for (int x = 0; x < w; x++)
                {
                    near[r, x] = 99;
                    if (g[r][x] != '.') { near[r, x] = 0; continue; }
                }
            for (int pass = 0; pass < 4; pass++)
                for (int r = 0; r < h; r++)
                    for (int x = 0; x < w; x++)
                    {
                        if (near[r, x] == 0) continue;
                        int best = near[r, x];
                        for (int dr = -1; dr <= 1; dr++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int rr = r + dr, xx = x + dx;
                                if (rr < 0 || rr >= h || xx < 0 || xx >= w) continue;
                                best = Mathf.Min(best, near[rr, xx] + 1);
                            }
                        near[r, x] = best;
                    }
            var used = new bool[h, w];
            foreach (var p in LastPosts)
            {
                // Keep the rampart behind each stair (and a little round it) free of buildings.
                for (int y = p.y0 - 3; y <= p.y1 + 3; y++)
                    for (int x = p.x0 - 3; x <= p.x1 + 3; x++)
                    {
                        int r = hh2 - 1 - y;
                        if (r >= 0 && r < h && x >= 0 && x < w) used[r, x] = true;
                    }
            }
            float k01 = i / (float)(Count - 1);
            LastBuildings.Clear();
            // First the blocks right on the edge (shuffled), then the deeper ones filling the blocks between the roads.
            var order = new System.Collections.Generic.List<(int r, int x)>();
            for (int depth = 1; depth <= 5; depth++)
            {
                var ring = new System.Collections.Generic.List<(int r, int x)>();
                for (int r = 1; r < h - 1; r++) for (int x = 1; x < w - 1; x++) if (near[r, x] == depth) ring.Add((r, x));
                for (int a = ring.Count - 1; a > 0; a--) { int b = rng.Next(a + 1); (ring[a], ring[b]) = (ring[b], ring[a]); }
                order.AddRange(ring);
            }
            foreach (var (r, x) in order)
            {
                if (used[r, x]) continue;
                int bw = 2 + rng.Next(3), bh = 2 + rng.Next(2);
                // Grow away from the floor: try the four ways the block can sit with (r, x) as its floor-side corner.
                bool done = false;
                foreach (var (sxn, syn) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
                {
                    if (done) break;
                    foreach (var (ww, hh) in new[] { (bw, bh), (bh, bw), (2, 2) })
                    {
                        int xa = sxn > 0 ? x : x - ww + 1, ra = syn > 0 ? r : r - hh + 1, xb = xa + ww - 1, rb = ra + hh - 1;
                        if (xa < 1 || ra < 1 || xb >= w - 1 || rb >= h - 1) continue;
                        bool ok = true;
                        for (int rr = ra; rr <= rb && ok; rr++) for (int xx = xa; xx <= xb && ok; xx++) if (g[rr][xx] != '.' || used[rr, xx] || near[rr, xx] > 5) ok = false;
                        if (!ok) continue;
                        for (int rr = ra - 1; rr <= rb + 1; rr++) for (int xx = xa - 1; xx <= xb + 1; xx++) if (rr >= 0 && rr < h && xx >= 0 && xx < w) used[rr, xx] = true;
                        float tall = Mathf.Lerp(3.2f, 5f, k01) + (float)rng.NextDouble() * Mathf.Lerp(2.5f, 5f, k01);
                        LastBuildings.Add(new Building { x0 = xa, x1 = xb, y0 = hh2 - 1 - rb, y1 = hh2 - 1 - ra, height = tall });
                        done = true;
                        break;
                    }
                }
            }

            var rowsOut = new System.Collections.Generic.List<string>();
            foreach (var row in g) rowsOut.Add(new string(row));
            bounds = new[] { margin, margin, w - 1 - margin, h - 1 - margin };
            return rowsOut.ToArray();
        }

        /// <summary>Is a tile (grid coordinates) under one of the arena's buildings?</summary>
        public static bool BuildingAt(int x, int y)
        {
            foreach (var b in LastBuildings) if (x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1) return true;
            return false;
        }
    }
}
