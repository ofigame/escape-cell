namespace SquashBot.Gameplay
{
    /// <summary>
    /// The move a world form of vanG fights with: the one its world gives the player (the first world's form leaps like
    /// FLY). The specials and ultimates are its big attacks, each shown first (red tiles, a shell, a mark); the passives
    /// change how it fights.
    /// </summary>
    public enum FormMove
    {
        None,
        Leap, Backflip, Hook, Shock, IceWall, Patch, SkyStrike, Teleport,
        Quake, Whirlwind, IronBody, Mirror, Copy, ChainLightning, Meteor, CoreBurst,
        ComboMaster, HeartMaster, Piercer, FastCharge, Sturdy, Rage, Vampire, ShieldMaster, MasterWarrior,
    }
}
