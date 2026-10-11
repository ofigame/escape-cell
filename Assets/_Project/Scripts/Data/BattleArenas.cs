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
        public const int Count = 10;

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

        public static Look Style(int i) => Looks[Mathf.Clamp(i, 0, Count - 1)];

        /// <summary>The level (0-based) the player must have opened for an arena: 20, 40 … 200.</summary>
        public static int UnlockAt(int i) => 19 + i * 20;
        /// <summary>Whose enemies it borrows: a level a little past where it opens.</summary>
        public static int ThemeLevel(int i) => Mathf.Min(249, UnlockAt(i) + 10);
        /// <summary>How hard, 1..5 stars for the card.</summary>
        public static int Stars(int i) => 1 + i / 2;
        /// <summary>The courtyard's side (odd, so the cell sits in the middle): bigger with every arena.</summary>
        public static int Size(int i) => 23 + i * 2;

        public static bool Unlocked(int i) => SaveData.UnlockedLevel >= UnlockAt(i);

        public static int BestStreak(int i) => PlayerPrefs.GetInt("sb_arena_best_" + i, 0);
        public static int Wins(int i) => PlayerPrefs.GetInt("sb_arena_wins_" + i, 0);

        public static void Record(int i, int streak, bool won)
        {
            if (streak > BestStreak(i)) PlayerPrefs.SetInt("sb_arena_best_" + i, streak);
            if (won) PlayerPrefs.SetInt("sb_arena_wins_" + i, Wins(i) + 1);
            PlayerPrefs.Save();
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

        /// <summary>
        /// The arena's floor: one huge courtyard in its shape and, in the later arenas, fire pits (2×2) round the middle.
        /// No cage, no tunnel. Rows top (north) to bottom; <paramref name="bounds"/> = x0, y0, x1, y1 of its square.
        /// </summary>
        public static string[] Layout(int i, out int[] bounds)
        {
            int sz = Size(i), w = sz + 2, mid = w / 2;
            var shape = Style(i).shape;
            var rows = new System.Collections.Generic.List<string>();
            int pits = i < 2 ? 0 : i < 6 ? 2 : 4;
            for (int r = 0; r < sz; r++)
            {
                var row = new string('.', w).ToCharArray();
                int inset = Inset(shape, sz, r);
                for (int x = 1 + inset; x <= sz - inset; x++) row[x] = '#';
                if (r == sz / 2) row[mid] = 'S';
                int q = sz / 5;
                if (pits > 0 && (r == sz / 2 - q || r == sz / 2 - q + 1 || (pits == 4 && (r == sz / 2 + q || r == sz / 2 + q + 1))))
                    foreach (int x in new[] { mid - q - 1, mid - q, mid + q, mid + q + 1 })
                        if (row[x] == '#') row[x] = 'F';
                rows.Add(new string(row));
            }
            rows.Add(new string('.', w)); // a margin row to the south
            int h = rows.Count;
            bounds = new[] { 1, h - sz, sz, h - 1 };
            return rows.ToArray();
        }
    }
}
