namespace SquashBot.Gameplay
{
    /// <summary>
    /// What a training challenge's step asks (the design's §10.2): a chain of blows unbroken, a combo ending in a POWER
    /// HIT or a KNOCKDOWN, a blow in the air, a POWER HIT through a block; blocks in a row (or without the shield
    /// breaking), perfect guards, a perfect guard answered by a blow, a dodged ultimate.
    /// </summary>
    public enum ComboStepKind { Chain, PowerHit, Knockdown, AirHit, PowerThroughBlock, BlockInRow, BlockNoBreak, Perfects, PerfectCounter, DodgeUlti }
}
