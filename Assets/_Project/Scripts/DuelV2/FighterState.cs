namespace SquashBot.DuelV2
{
    /// <summary>What a v2 fighter is doing (its rules: what it may do next, whether it can be struck).</summary>
    public enum FighterState
    {
        Idle, Walk, Blocking,
        Windup, Active, Recovery,
        HitStun, BlockStun, Staggered,
        Dodging, Jumping,
        Airborne, Down, GettingUp,
        Casting,
        KO,
    }
}
