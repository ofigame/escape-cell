namespace SquashBot.Visual
{
    /// <summary>
    /// The moments foi's face answers in a duel (the design's §2.1), and the face taunts (§7). Higher in the list is not
    /// higher in priority: the face controller keeps each one's priority.
    /// </summary>
    public enum FaceMoment
    {
        Idle, LightHit, HeavyHit, Launched, Shocked, Frozen, LowHealth, PerfectGuard, UltiStart, KnockedOut, Victory, Defeat, Draw,
        TongueOut, BrowRaise, Wink, SlyGrin, FakeCry, Surprise,
    }
}
