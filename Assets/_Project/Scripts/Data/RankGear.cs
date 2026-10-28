using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The player's rank gear: the tier of the highest rank ever reached (it stays when trophies drop), and the tier
    /// they chose to wear (a lower one they opened, or the newest).
    /// </summary>
    public static class RankGear
    {
        private const string WornKey = "sb_rankgear_worn";
        private const string SeenKey = "sb_rankgear_seen";

        /// <summary>The highest tier opened (1..6).</summary>
        public static int Opened => RankGearData.Current.TierOf(Challenge.BestRank);

        /// <summary>The tier worn: the chosen one when opened, else the highest.</summary>
        public static int Worn
        {
            get
            {
                int w = Prefs.GetInt(WornKey, 0);
                return w >= 1 && w <= Opened ? w : Opened;
            }
            set => Prefs.SetInt(WornKey, value >= Opened ? 0 : Mathf.Max(1, value)); // (0: always the newest)
        }

        /// <summary>The tier of a rival of rank <paramref name="rank"/> (bots too).</summary>
        public static int ForRank(int rank) => RankGearData.Current.TierOf(Mathf.Max(0, rank));

        /// <summary>A new tier was opened since the last ceremony: its number (0 if none), and it is marked seen.</summary>
        public static int TakeNewTier()
        {
            int seen = Prefs.GetInt(SeenKey, 0), now = Opened;
            if (seen == 0) { Prefs.SetInt(SeenKey, now); return 0; } // (the first look: no ceremony for what they already had)
            if (now <= seen) return 0;
            Prefs.SetInt(SeenKey, now);
            return now;
        }
    }
}
