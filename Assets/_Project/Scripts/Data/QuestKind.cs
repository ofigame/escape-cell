namespace SquashBot.Data
{
    /// <summary>
    /// The little stories of quest levels: pieces lie scattered over a big floor, and once all are found the goal
    /// waits somewhere on the far side. The danger keeps the player moving; it is not there to end the run quickly.
    /// </summary>
    public enum QuestKind
    {
        /// <summary>Princess Lumi is frozen in a block of ice: gather the keys, then thaw her free.</summary>
        Princess,
        /// <summary>The escape lift has no power: collect the energy cores, then switch on the generator.</summary>
        Cores,
        /// <summary>Friends are locked in cages: open every cage, then reach the rescue pad together.</summary>
        Cages,
        /// <summary>The floor has gone dark: light every lantern, then fire up the great beacon.</summary>
        Lanterns,
        /// <summary>A lost treasure: find the gems scattered around and bring them to the old chest.</summary>
        Treasure
    }
}
