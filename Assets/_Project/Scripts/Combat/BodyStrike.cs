namespace SquashBot.Combat
{
    /// <summary>
    /// What strikes in a blow (the choreography design's 4.2): the weapon, or the body itself with the weapon still in hand
    /// (the chain's third blow in every family).
    /// </summary>
    public enum BodyStrike
    {
        Weapon,
        /// <summary>A spinning heel kick (high, right to left): the sword's.</summary>
        HeelKick,
        /// <summary>A front kick (middle, straight): the spear's.</summary>
        FrontKick,
        /// <summary>A shoulder blow (middle, straight): the axe's.</summary>
        Shoulder,
        /// <summary>A headbutt (high, straight): the heavy weapon's.</summary>
        Headbutt,
    }
}
