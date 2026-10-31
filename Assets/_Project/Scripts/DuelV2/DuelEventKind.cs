namespace SquashBot.DuelV2
{
    /// <summary>What happened in a v2 tick (the pictures, the camera, the sounds and the log read these).</summary>
    public enum DuelEventKind
    {
        Swing, Result, ChainOpen, ChainBroken, Series, PowerHitOffered, KnockdownOffered, BreakUsed,
        Dodge, Jump, Landed, GotUp, ShieldBroke, Heart, Special, UltiStart, UltiHit,
        PowerEarned, PowerLost, RainStart, RainDrop, RainEnd,
        RoundIntro, RoundFight, RoundOver, TimeUp, GoldenRound, MatchOver, KO,
    }
}
