using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The battle arenas (main menu → BATTLE ARENAS): ten huge single-courtyard fortresses with tall towers and high
    /// ramparts, much harder than the levels around them, played again and again. Each opens at a level, looks like a
    /// world of its own, and pays for kill streaks with gifts. Best streak and wins are kept on the phone.
    /// </summary>
    public static class BattleArenas
    {
        public const int Count = 10;

        /// <summary>The level (0-based) the player must have opened for an arena: 20, 40 … 200.</summary>
        public static int UnlockAt(int i) => 19 + i * 20;
        /// <summary>Whose look (theme, enemies) it borrows: a level a little past where it opens.</summary>
        public static int ThemeLevel(int i) => Mathf.Min(249, UnlockAt(i) + 10);
        /// <summary>How hard, 1..5 stars for the card.</summary>
        public static int Stars(int i) => 1 + i / 2;
        /// <summary>The courtyard's side (odd, so the cell sits in the middle).</summary>
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

        /// <summary>How far each corner of the octagonal courtyard is cut back.</summary>
        public static int Cut(int i) => Size(i) / 4;

        /// <summary>
        /// The arena's floor: one huge octagonal courtyard (a square with its corners cut) and, in the later arenas,
        /// fire pits (2×2) round the middle. No cage, no tunnel. Rows top (north) to bottom; <paramref name="bounds"/> =
        /// x0, y0, x1, y1 of the square it is cut from.
        /// </summary>
        public static string[] Layout(int i, out int[] bounds)
        {
            int sz = Size(i), w = sz + 2, mid = w / 2, cut = Cut(i);
            var rows = new System.Collections.Generic.List<string>();
            int pits = i < 2 ? 0 : i < 6 ? 2 : 4;
            for (int r = 0; r < sz; r++)
            {
                var row = new string('.', w).ToCharArray();
                int inset = Mathf.Max(0, cut - Mathf.Min(r, sz - 1 - r));
                for (int x = 1 + inset; x <= sz - inset; x++) row[x] = '#';
                if (r == sz / 2) row[mid] = 'S';
                int q = sz / 4;
                if (pits > 0 && (r == sz / 2 - q || r == sz / 2 - q + 1 || (pits == 4 && (r == sz / 2 + q || r == sz / 2 + q + 1))))
                    foreach (int x in new[] { mid - q - 1, mid - q, mid + q, mid + q + 1 }) row[x] = 'F';
                rows.Add(new string(row));
            }
            rows.Add(new string('.', w)); // a margin row to the south
            int h = rows.Count;
            bounds = new[] { 1, h - sz, sz, h - 1 };
            return rows.ToArray();
        }
    }
}
