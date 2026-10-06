namespace SquashBot.Data
{
    /// <summary>How well a building is looked after. A building never falls apart; at worst it stops helping.</summary>
    public enum CareStage
    {
        /// <summary>Shining, full contribution.</summary>
        Fresh,
        /// <summary>Faded and dusty: still a full contribution, a "care soon" warning.</summary>
        Dusty,
        /// <summary>A wrench spins over it and its contribution stops (it gets no worse).</summary>
        NeedsCare
    }
}
