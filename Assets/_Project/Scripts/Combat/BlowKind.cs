namespace SquashBot.Combat
{
    /// <summary>
    /// What kind of blow meets a defence (it sets what blocking it costs and whether it can be guarded perfectly):
    /// a plain blow, a POWER HIT, a flying or sky blow (FLY, SKY STRIKE, a blow from a jump), a KNOCKDOWN, an ultimate
    /// (never guarded perfectly), another special, or none (a hurt nothing defends against).
    /// </summary>
    public enum BlowKind { Plain, PowerHit, Air, Knockdown, Ulti, Special, None }
}
