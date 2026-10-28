using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// How long each level takes (the scenario's economy needs it: gold per minute, the duel against the levels): every
    /// level played to its end is timed, kept per level on the phone (tries, wins, total seconds) and written to a line of
    /// level_times.csv beside the save, for the testers' phones to be read.
    /// </summary>
    public static class LevelTimes
    {
        private static string K(int level, string what) => "sb_lt_" + what + "_" + level;

        public static void Record(int level, float seconds, bool won)
        {
            if (level < 0 || seconds <= 0f) return;
            Prefs.SetInt(K(level, "n"), Prefs.GetInt(K(level, "n"), 0) + 1);
            if (won) Prefs.SetInt(K(level, "w"), Prefs.GetInt(K(level, "w"), 0) + 1);
            Prefs.SetInt(K(level, "s"), Prefs.GetInt(K(level, "s"), 0) + Mathf.RoundToInt(seconds));
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(Application.persistentDataPath, "level_times.csv"),
                    GameClock.Today + "," + (level + 1) + "," + Mathf.RoundToInt(seconds) + "," + (won ? 1 : 0) + "\n");
            }
            catch (System.Exception) { } // (only a note: never in the way of the game)
        }

    }
}
