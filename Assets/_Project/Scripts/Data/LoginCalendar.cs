namespace SquashBot.Data
{
    /// <summary>
    /// The 7-day login calendar: one gift a day (local time), the days in order; a missed day is not lost, the calendar
    /// goes on from where it stopped, and after the 7th day it starts again from the first.
    /// </summary>
    public static class LoginCalendar
    {
        public const int Days = 7;
        public enum Kind { Gold, Chest, Cores }

        /// <summary>What each day brings: 300 gold, a plain chest, 500 gold, 10 core parts, a bronze chest, 800 gold, a gold chest.</summary>
        public static readonly (Kind kind, int amount, ChestTier tier)[] Gifts =
        {
            (Kind.Gold, 300, ChestTier.Basic),
            (Kind.Chest, 1, ChestTier.Basic),
            (Kind.Gold, 500, ChestTier.Basic),
            (Kind.Cores, 10, ChestTier.Basic),
            (Kind.Chest, 1, ChestTier.Mid),
            (Kind.Gold, 800, ChestTier.Basic),
            (Kind.Chest, 1, ChestTier.Advanced),
        };

        private const string DayKey = "sb_login_day", LastKey = "sb_login_last";

        /// <summary>The day whose gift is next (0-based).</summary>
        public static int Day => Prefs.GetInt(DayKey, 0) % Days;

        /// <summary>Today's gift is waiting (not taken yet today).</summary>
        /// <summary>A gift was ever taken.</summary>
        public static bool Started => Prefs.GetString(LastKey, "") != "";

        public static bool Ready => string.CompareOrdinal(GameClock.Today, Prefs.GetString(LastKey, "")) > 0;

        /// <summary>Gives today's gift and moves on to the next day; returns the day given (-1 if already taken today).</summary>
        public static int Claim()
        {
            if (!Ready) return -1;
            int day = Day;
            var g = Gifts[day];
            if (g.kind == Kind.Gold) SaveData.Coins += g.amount;
            else if (g.kind == Kind.Cores) CoreParts.Add(g.amount);
            else DuelChests.Add(g.tier);
            Prefs.SetInt(DayKey, (day + 1) % Days);
            Prefs.SetString(LastKey, GameClock.Today);
            Prefs.Save();
            return day;
        }
    }
}
