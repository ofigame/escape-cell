namespace SquashBot.DuelV2
{
    /// <summary>
    /// The duel fight core v2's switch. Off everywhere but the prototype scene (Scenes/Dev/DuelPrototypeV2): while it
    /// runs, the game's own duel (its input, rival, clock, camera and HUD) stands still and the v2 core fights.
    /// </summary>
    public static class DuelCoreV2
    {
        /// <summary>The v2 core is fighting (the game's own duel waits).</summary>
        public static bool Running;
    }
}
