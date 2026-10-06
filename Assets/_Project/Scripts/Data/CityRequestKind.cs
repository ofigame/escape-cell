namespace SquashBot.Data
{
    /// <summary>What a resident asks for.</summary>
    public enum CityRequestKind
    {
        /// <summary>"Build me an igloo."</summary>
        Build,
        /// <summary>"I'd like a tree next to a house."</summary>
        Near,
        /// <summary>"My mine broke, can you fix it?"</summary>
        Repair,
        /// <summary>"Beat 3 levels of the fire floor with 3 stars."</summary>
        MainMode
    }
}
