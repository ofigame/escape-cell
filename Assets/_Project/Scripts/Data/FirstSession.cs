using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The player's first session (the design's §8): for its first ten minutes no pop-up, announcement or offer opens by
    /// itself, so a new player gets straight to the duels. The game counts its starts.
    /// </summary>
    public static class FirstSession
    {
        private const string Key = "sb_launches";
        private const float QuietSeconds = 600f;
        private static bool counted;

        /// <summary>Counts this start (once a run).</summary>
        public static void Count()
        {
            if (counted) return;
            counted = true;
            Prefs.SetInt(Key, Prefs.GetInt(Key, 0) + 1);
            Prefs.Save();
        }

        /// <summary>The first start's first ten minutes: nothing pops up by itself.</summary>
        public static bool Quiet => Prefs.GetInt(Key, 0) <= 1 && Time.realtimeSinceStartup < QuietSeconds;

        /// <summary>The time from the game's start to its first duel this run (seconds; -1 until then): the
        /// <c>time_to_first_duel</c> measure, kept on the phone with the last few.</summary>
        public static float TimeToFirstDuel { get; private set; } = -1f;

        public static void DuelStarted()
        {
            if (TimeToFirstDuel >= 0f) return;
            TimeToFirstDuel = Time.realtimeSinceStartup;
            string log = Prefs.GetString("sb_ttfd", "");
            log = (log.Length > 0 ? log + "," : "") + Mathf.RoundToInt(TimeToFirstDuel);
            if (log.Length > 120) log = log.Substring(log.IndexOf(',', log.Length - 120) + 1);
            Prefs.SetString("sb_ttfd", log);
            Prefs.Save();
            Debug.Log("time_to_first_duel " + TimeToFirstDuel.ToString("0.0") + " s");
        }
    }
}
