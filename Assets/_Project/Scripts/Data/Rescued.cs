namespace SquashBot.Data
{
    /// <summary>
    /// The rescued foi (the scenario's §3.9): 40 for every world's monster beaten, 1,000 in all. They fill the player's
    /// blue stand in the duel — half empty below 100, filling from 100, flags waving at 250, a wave at 500, packed at
    /// 1,000. A show, not a power: the rival sees the player's stand too.
    /// </summary>
    public static class Rescued
    {
        public const int PerWorld = 40;

        public static int Count
        {
            get
            {
                int n = 0;
                for (int w = 1; w <= LevelCatalog.WorldCount; w++) if (MoveSet.WorldBeaten(w)) n += PerWorld;
                return n;
            }
        }

        /// <summary>The share of a stand's seats taken for this many rescued foi.</summary>
        public static float Fill(int count) => count >= 1000 ? 0.98f : count >= 500 ? 0.9f : count >= 250 ? 0.8f : count >= 100 ? 0.65f : 0.45f;

        /// <summary>The share of the fans waving a flag.</summary>
        public static float Flags(int count) => count >= 250 ? 0.25f : 0.08f;
    }
}
