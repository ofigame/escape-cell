using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The cups the player has won, for the profile's trophy case: a season cup for the rank each season's reward was
    /// claimed at (that league's trophy), and the weekly cup's four steps (bronze, silver, gold, diamond), each counted.
    /// Kept with the game's other values (so the cloud copy carries them).
    /// </summary>
    public static class TrophyCase
    {
        public const int WeekSteps = 4;

        /// <summary>The league trophy each weekly step is shown with (bronze, silver, gold, diamond).</summary>
        public static readonly int[] WeekArt = { 3, 6, 9, 12 };

        private static string SeasonKey(int rank) => "sb_cup_season_" + Mathf.Clamp(rank, 0, Challenge.RankAt.Length - 1);
        private static string WeekKey(int step) => "sb_cup_week_" + Mathf.Clamp(step, 0, WeekSteps - 1);

        public static int Season(int rank) => Prefs.GetInt(SeasonKey(rank), 0);
        public static int Week(int step) => Prefs.GetInt(WeekKey(step), 0);

        public static void AddSeason(int rank) { Prefs.SetInt(SeasonKey(rank), Season(rank) + 1); Prefs.Save(); }
        public static void AddWeek(int step) { Prefs.SetInt(WeekKey(step), Week(step) + 1); Prefs.Save(); }

        public static int Total
        {
            get
            {
                int n = 0;
                for (int r = 0; r < Challenge.RankAt.Length; r++) n += Season(r);
                for (int s = 0; s < WeekSteps; s++) n += Week(s);
                return n;
            }
        }
    }
}
