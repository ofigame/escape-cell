namespace SquashBot.Data
{
    /// <summary>Friend matches pay gold only for the first three of the day (the XP and mastery always come).</summary>
    public static class FriendMatchesPaid
    {
        public const int PerDay = 3;
        private const string DayKey = "sb_friend_day", CountKey = "sb_friend_n";

        public static int Used => Prefs.GetString(DayKey, "") == GameClock.Today ? Prefs.GetInt(CountKey, 0) : 0;

        /// <summary>Counts a friend match: true while it is one of the day's paid ones.</summary>
        public static bool Take()
        {
            int n = Used;
            Prefs.SetString(DayKey, GameClock.Today);
            Prefs.SetInt(CountKey, n + 1);
            Prefs.Save();
            return n < PerDay;
        }
    }
}
