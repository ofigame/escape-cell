using System;
using System.Collections.Generic;

namespace SquashBot.Data
{
    /// <summary>
    /// The duel's emotes: six from the start, seven more opened by the duel level (3, 7, 12, 18, 25, 33, 42; the scenario's
    /// §3.7) and the season's own from the season road (step 10, that month).
    /// </summary>
    public static class Emotes
    {
        public const int Count = 14, Season = 13;
        public static readonly int[] Levels = { 3, 7, 12, 18, 25, 33, 42 };

        public static bool Owned(int e)
        {
            if (e < 6) return true;
            if (e < Season) return DuelLevel.Level >= Levels[e - 6];
            return Prefs.GetInt("sb_emote_season_" + DateTime.Now.ToString("yyyyMM"), 0) == 1;
        }

        public static List<int> All
        {
            get
            {
                var list = new List<int>();
                for (int e = 0; e < Count; e++) if (Owned(e)) list.Add(e);
                return list;
            }
        }

        /// <summary>The emote a duel level opens (-1: none).</summary>
        public static int ForLevel(int level) { int i = Array.IndexOf(Levels, level); return i < 0 ? -1 : 6 + i; }
    }
}
