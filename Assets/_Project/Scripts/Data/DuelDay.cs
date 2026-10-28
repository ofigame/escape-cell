namespace SquashBot.Data
{
    /// <summary>The duel's daily moments: the day's first win (double gold, 10 core parts and a bronze chest).</summary>
    public static class DuelDay
    {
        private const string FirstWinKey = "sb_duel_firstwin";

        /// <summary>The day's first win is still waiting.</summary>
        public static bool FirstWinWaiting => Prefs.GetString(FirstWinKey, "") != GameClock.Today;

        /// <summary>Counts a win: true when it is the day's first.</summary>
        public static bool TakeFirstWin()
        {
            if (!FirstWinWaiting) return false;
            Prefs.SetString(FirstWinKey, GameClock.Today);
            Prefs.Save();
            return true;
        }
    }
}
