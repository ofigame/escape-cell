namespace SquashBot.DuelV2
{
    /// <summary>
    /// The duel fight core v2's switches. The game's challenge duels are fought by it (<see cref="InGame"/>); the
    /// prototype scene (Scenes/Dev/DuelPrototypeV2) runs it with its tools. While it fights, the game's own duel (its
    /// input, rival, clock, camera and HUD) stands still.
    /// </summary>
    public static class DuelCoreV2
    {
        /// <summary>The v2 core is fighting (the game's own duel waits).</summary>
        public static bool Running;

        /// <summary>The fight is the game's own (a challenge duel), not the prototype's: the game keeps its back button and pause.</summary>
        public static bool InGame;

        /// <summary>The game's duels are fought by the v2 core (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md, Faz 10).</summary>
        public const bool Enabled = true;

        /// <summary>The prototype scene: it sets duels up the old way and takes them over itself (the game's own takeover off).</summary>
        public static bool PrototypeScene;
    }
}
