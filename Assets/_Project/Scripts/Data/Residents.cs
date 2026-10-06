namespace SquashBot.Data
{
    /// <summary>
    /// The cellmates: every WARDEN boss beaten for the first time (floors 2-20) frees one, who moves into the city.
    /// Each wears the colours of the floor it was freed on.
    /// </summary>
    public static class Residents
    {
        /// <summary>The cellmates, in the order their floors' bosses free them.</summary>
        public static readonly string[] Names =
        {
            "Bip", "Zip", "Tik", "Vin", "Pit", "Lop", "Fir", "Cit", "Dit", "Tin",
            "Bom", "Kivi", "Zap", "Pof", "Cik", "Fiu", "Nok", "Tuk", "Mavi",
        };

        public static int Count => Names.Length;

        /// <summary>The boss level (0-based) whose win frees resident <paramref name="i"/>.</summary>
        public static int BossLevel(int i) => (i + 2) * LevelCatalog.LevelsPerWorld - 1;

        /// <summary>The resident freed by beating this level, or -1 when it is not a boss level.</summary>
        public static int FreedBy(int levelIndex)
        {
            for (int i = 0; i < Names.Length; i++)
                if (BossLevel(i) == levelIndex) return i;
            return -1;
        }

        /// <summary>Freed: its boss has been beaten (test builds free everyone, so the city can be tried out).</summary>
        public static bool Rescued(int i) => SaveData.TestMode || Progress.Stars(BossLevel(i)) > 0;

        public static int RescuedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Names.Length; i++) if (Rescued(i)) n++;
                return n;
            }
        }

        /// <summary>The world look a resident wears (the floor it was freed on).</summary>
        public static int World(int i) => i + 1;
    }
}
