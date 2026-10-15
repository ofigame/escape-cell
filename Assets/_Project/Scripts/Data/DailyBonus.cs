using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Daily bonus games: 3 free plays a day (renewed at local midnight, not carried over), plus one more for watching
    /// an ad once a day. Separate from the bonus rounds levels unlock. Each of the day's plays is a different bonus game,
    /// chosen from the ones the player has already met in the main game.
    /// </summary>
    public static class DailyBonus
    {
        public const int FreePlays = 3;
        private const string DayKey = "sb_db_day", UsedKey = "sb_db_used", AdKey = "sb_db_ad", ExtraKey = "sb_db_extra";
        private const string SeenKey = "sb_seen_bonus_";

        /// <summary>Starts a new day when the date moved forward. A clock turned back gets no new plays.</summary>
        private static void Roll()
        {
            string today = GameClock.Today;
            string day = Prefs.GetString(DayKey, "");
            // A day stored in another shape (an old non-Gregorian calendar's date) is not a "future" day: roll.
            bool valid = day.Length == 8 && long.TryParse(day, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long d) && d >= 20250101;
            if (valid && string.CompareOrdinal(today, day) <= 0) return;
            Prefs.SetString(DayKey, today);
            Prefs.SetInt(UsedKey, 0);
            Prefs.SetInt(AdKey, 0);
            Prefs.SetInt(ExtraKey, 0);
            Prefs.Save();
        }

        public static int Used { get { Roll(); return Prefs.GetInt(UsedKey, 0); } }

        /// <summary>Free plays left today (the ad play is counted separately).</summary>
        public static int FreeLeft => Mathf.Max(0, FreePlays - Used);

        public static bool AdPlayWaiting { get { Roll(); return Prefs.GetInt(ExtraKey, 0) > 0; } }

        public static int Left => FreeLeft + (AdPlayWaiting ? 1 : 0);

        /// <summary>The ad button shows once the free plays are gone, and only once a day.</summary>
        public static bool CanWatchAd { get { Roll(); return FreeLeft == 0 && !AdPlayWaiting && Prefs.GetInt(AdKey, 0) == 0; } }

        public static bool AdUsedToday { get { Roll(); return Prefs.GetInt(AdKey, 0) == 1; } }

        /// <summary>The ad was watched to the end: one extra play today.</summary>
        public static void GrantAdPlay()
        {
            Roll();
            Prefs.SetInt(AdKey, 1);
            Prefs.SetInt(ExtraKey, 1);
            Prefs.Save();
        }

        /// <summary>Spends a play; false when none is left.</summary>
        public static bool Use()
        {
            Roll();
            if (FreeLeft > 0) Prefs.SetInt(UsedKey, Used + 1);
            else if (AdPlayWaiting) Prefs.SetInt(ExtraKey, 0);
            else return false;
            Prefs.Save();
            return true;
        }

        // ---------- Which games ----------

        public static void MarkSeen(BonusGame g) => Prefs.SetInt(SeenKey + g, 1);

        public static bool Seen(BonusGame g) => SaveData.TestMode || Prefs.GetInt(SeenKey + g, 0) == 1;

        /// <summary>The bonus games met so far; a fresh player starts with the air duct and the treasure room.</summary>
        public static List<BonusGame> SeenGames()
        {
            var list = new List<BonusGame>();
            foreach (BonusGame g in System.Enum.GetValues(typeof(BonusGame))) if (Seen(g)) list.Add(g);
            if (list.Count == 0) { list.Add(BonusGame.Duct); list.Add(BonusGame.Treasure); }
            return list;
        }

        /// <summary>Today's three games, the same all day (seeded by the date), each different where possible.</summary>
        public static BonusGame[] TodaysGames()
        {
            var pool = SeenGames();
            var rng = new System.Random(int.Parse(GameClock.Today, System.Globalization.CultureInfo.InvariantCulture));
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            var games = new BonusGame[FreePlays];
            for (int i = 0; i < FreePlays; i++) games[i] = pool[i % pool.Count];
            return games;
        }

        /// <summary>The game the next play gets: the day's list in order, then a random one for the ad play.</summary>
        public static BonusGame NextGame()
        {
            int used = Used;
            if (used < FreePlays) return TodaysGames()[used];
            var pool = SeenGames();
            return pool[Random.Range(0, pool.Count)];
        }
    }
}
