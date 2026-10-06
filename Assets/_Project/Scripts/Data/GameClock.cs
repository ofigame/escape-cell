using System;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Wall-clock time for the daily bonus, guarded against the simplest cheat: when the device clock
    /// goes backwards, time stands still at the latest moment seen instead of running again.
    /// </summary>
    public static class GameClock
    {
        private const string Key = "sb_clock_max";
        private static long max = -1;

        /// <summary>Unix seconds, never smaller than the largest value returned before.</summary>
        public static long Now
        {
            get
            {
                if (max < 0) long.TryParse(PlayerPrefs.GetString(Key, "0"), out max);
                long real = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (real > max)
                {
                    max = real;
                    PlayerPrefs.SetString(Key, max.ToString());
                }
                return max;
            }
        }

        /// <summary>The local calendar day as yyyyMMdd (compares as a string).</summary>
        public static string Today => DateTime.Now.ToString("yyyyMMdd");

        /// <summary>Seconds until local midnight.</summary>
        public static int SecondsToMidnight => (int)(DateTime.Today.AddDays(1) - DateTime.Now).TotalSeconds;
    }
}
