namespace SquashBot.Visual
{
    /// <summary>
    /// A special move's or ultimate's body choreography (the choreography design's section 5): what the fighter's own body
    /// does, the effects only dressing it. Played by <see cref="SkinnedHero.Perform"/>.
    /// </summary>
    public enum BodyMove
    {
        None,
        /// <summary>WHIRLWIND: spinning on one foot, the weapon arm straight out.</summary>
        Whirl,
        /// <summary>QUAKE: the weapon over its head in both hands, a jump, down and the weapon into the ground, the knees giving.</summary>
        Quake,
        /// <summary>CHAIN LIGHTNING: the weapon raised to the sky, brought down at the other with each bolt, the arm kicking back.</summary>
        SkyRaise,
        /// <summary>METEOR: points at the other, then both arms pull something down out of the sky.</summary>
        PullDown,
        /// <summary>CORE BURST: a fist on its chest, the arms flung open as it rises, down onto one knee.</summary>
        CoreBurst,
        /// <summary>IRON BODY: the chest beaten twice, the muscles swelling.</summary>
        IronFlex,
        /// <summary>MIRROR: the arms crossed in front, the palms open.</summary>
        MirrorGuard,
        /// <summary>HOOK: the arm flung forward (the hook flies), then both hands hauling on the chain, leaning back.</summary>
        HookHaul,
        /// <summary>SHOCK: the palm pushed out, the recoil a step back.</summary>
        ShockPush,
        /// <summary>ICE WALL: a fist punched into the ground.</summary>
        GroundPunch,
        /// <summary>PATCH: a hand at its chest turning a part in, the shoulders shrugging.</summary>
        ChestPatch,
        /// <summary>TELEPORT: arriving at once with a shoulder push.</summary>
        ShoulderPush,
    }
}
