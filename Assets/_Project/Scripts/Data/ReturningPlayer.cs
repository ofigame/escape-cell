using System;
using System.Globalization;

namespace SquashBot.Data
{
    /// <summary>
    /// A player back after three days or more (the design's §11.1): a "welcome back" with a bronze chest and ten core
    /// parts (once), the first three bot duels a step easier ("a little rusty"), and a short word on what is new (a new
    /// season began meanwhile).
    /// </summary>
    public static class ReturningPlayer
    {
        private const string LastKey = "sb_last_day", RustyKey = "sb_rusty", PendingKey = "sb_welcome_back", SeasonKey = "sb_season_seen";
        public const int AwayDays = 3, RustyDuels = 3, Cores = 10;
        private static bool checkedToday;

        /// <summary>Counts the days away (once a run): three or more, the welcome waits and the rust is on.</summary>
        public static void Check()
        {
            if (checkedToday) return;
            checkedToday = true;
            var today = DateTime.Now.Date;
            string last = Prefs.GetString(LastKey, "");
            if (DateTime.TryParseExact(last, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastDay) && (today - lastDay).TotalDays >= AwayDays)
            {
                Prefs.SetInt(PendingKey, 1);
                Prefs.SetInt(RustyKey, RustyDuels);
            }
            Prefs.SetString(LastKey, today.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            Prefs.Save();
        }

        /// <summary>The welcome waits to be shown.</summary>
        public static bool Pending => Prefs.GetInt(PendingKey, 0) == 1;

        /// <summary>The welcome taken: its gifts given, its words (the gifts, a new season if one began).</summary>
        public static string Take()
        {
            Prefs.SetInt(PendingKey, 0);
            DuelChests.Add(ChestTier.Mid);
            CoreParts.Add(Cores);
            string month = DateTime.Now.ToString("yyyyMM", CultureInfo.InvariantCulture);
            bool newSeason = Prefs.GetString(SeasonKey, month) != month;
            Prefs.SetString(SeasonKey, month);
            Prefs.Save();
            return Loc.F("back.text", Loc.T("chests.tier1"), Cores) + (newSeason ? "\n" + Loc.T("back.season") : "") + "\n" + Loc.T("back.rusty");
        }

        /// <summary>The first duels back: the bot a step easier.</summary>
        public static bool Rusty => Prefs.GetInt(RustyKey, 0) > 0;

        /// <summary>A bot duel played (one rusty duel fewer).</summary>
        public static void BotPlayed()
        {
            int n = Prefs.GetInt(RustyKey, 0);
            if (n <= 0) return;
            Prefs.SetInt(RustyKey, n - 1);
            Prefs.Save();
        }

        /// <summary>The season seen (noted each run, so a later return can tell a new one began).</summary>
        public static void NoteSeason()
        {
            if (Prefs.HasKey(SeasonKey)) return;
            Prefs.SetString(SeasonKey, DateTime.Now.ToString("yyyyMM", CultureInfo.InvariantCulture));
            Prefs.Save();
        }
    }
}
