using System;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Five lives. Starting a level costs one; winning refills all five, so only losses (or quitting) cost lives.
    /// A missing life comes back every <see cref="RegenMinutes"/> minutes: from empty, a full refill takes one hour.
    /// Uses wall-clock UTC time, so regeneration continues while the game is closed.
    /// </summary>
    public static class Lives
    {
        public static int Max => Shop.MaxLives;
        public const int RegenMinutes = 12;

        private const string CountKey = "sb_lives";
        private const string TimeKey = "sb_lives_since"; // UTC ticks when the next regen countdown started

        private static readonly TimeSpan Regen = TimeSpan.FromMinutes(RegenMinutes);

        public static int Count
        {
            get
            {
                Update();
                return PlayerPrefs.GetInt(CountKey, Max);
            }
        }

        public static bool IsFull => Count >= Max;

        /// <summary>Time until the next life arrives (zero when full).</summary>
        public static TimeSpan UntilNext
        {
            get
            {
                Update();
                if (PlayerPrefs.GetInt(CountKey, Max) >= Max) return TimeSpan.Zero;
                var left = Since + Regen - DateTime.UtcNow;
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        public static string UntilNextText
        {
            get
            {
                var t = UntilNext;
                return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
            }
        }

        /// <summary>Spend a life to start a level. False when there are none left.</summary>
        public static bool TryConsume()
        {
            int count = Count;
            if (count <= 0) return false;
            if (count >= Max) Since = DateTime.UtcNow; // the regen clock starts with the first missing life
            Set(count - 1);
            return true;
        }

        /// <summary>Grant extra lives (e.g. a rewarded ad was watched).</summary>
        public static void Add(int amount = 1)
        {
            int count = Mathf.Min(Max, Count + amount);
            Set(count);
            if (count >= Max) Since = DateTime.UtcNow;
        }

        /// <summary>Back to all five lives (a level was won).</summary>
        public static void Refill()
        {
            Set(Max);
            Since = DateTime.UtcNow;
        }

        private static void Update()
        {
            int count = PlayerPrefs.GetInt(CountKey, Max);
            if (count >= Max) return;

            var now = DateTime.UtcNow;
            var since = Since;
            if (since > now) since = now; // clock moved backwards: restart the countdown instead of granting lives

            int gained = (int)((now - since).Ticks / Regen.Ticks);
            if (gained <= 0)
            {
                Since = since;
                return;
            }

            count = Mathf.Min(Max, count + gained);
            Set(count);
            Since = count >= Max ? now : since + TimeSpan.FromTicks(Regen.Ticks * gained);
        }

        private static DateTime Since
        {
            get
            {
                string raw = PlayerPrefs.GetString(TimeKey, "");
                return long.TryParse(raw, out long ticks) ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.UtcNow;
            }
            set
            {
                PlayerPrefs.SetString(TimeKey, value.Ticks.ToString());
                PlayerPrefs.Save();
            }
        }

        private static void Set(int count)
        {
            PlayerPrefs.SetInt(CountKey, Mathf.Clamp(count, 0, Max));
            PlayerPrefs.Save();
        }
    }
}
