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
        /// <summary>The leg sweep (4.3): down on one knee, the back leg sweeping a half circle at the ankles.</summary>
        Sweep,
        /// <summary>The uppercut (4.4): from the crouch, the legs springing, the fist rising from below.</summary>
        Uppercut,
        /// <summary>The flying headbutt (4.5): in the air, the body drawn back, then thrown forward head first.</summary>
        FlyingHeadbutt,
        /// <summary>The shoulder charge (4.6): the shoulder forward, a quick lunge.</summary>
        ShoulderCharge,
    }
}
