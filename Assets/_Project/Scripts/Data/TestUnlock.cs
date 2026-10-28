namespace SquashBot.Data
{
    /// <summary>
    /// TEMPORARY (the user's word, 2026-10-28): the iOS test build opens everything — every level on the map, every arena,
    /// every special, ultimate and passive — even as a store build. Set to false (or delete with its one use in
    /// <see cref="SaveData.TestMode"/>) before a real store release.
    /// </summary>
    public static class TestUnlock
    {
        public const bool All = true;
    }
}
