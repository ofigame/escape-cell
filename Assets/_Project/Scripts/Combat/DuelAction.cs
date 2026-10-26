namespace SquashBot.Combat
{
    /// <summary>
    /// What a fighter does in the duel, each with its own clip in <see cref="DuelAnimationMap"/> (the animation rules:
    /// one move, one clip). The stance and moving; defence and reactions; the plain chain's two blows; the combo moves;
    /// the specials; the ultimates.
    /// </summary>
    public enum DuelAction
    {
        Idle, Walk, Crouch, Block, Dodge,
        HitLight, HitHeavy, SweepFall, GetUpFaceUp, GetUpFaceDown, Knockout,
        ChainSlash, ChainStab,
        ComboHeavy, ComboKnockdown, ComboBreaker,
        SpecialFly, SpecialBackflip, SpecialSkyStrike, SpecialHook, SpecialShock, SpecialIceWall, SpecialPatch, SpecialTeleport,
        UltiCinematic, UltiWhirlwind, UltiQuake, UltiChainLightning, UltiMirror, UltiMeteor, UltiCoreBurst, UltiIronBody,
    }
}
