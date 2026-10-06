namespace SquashBot.Data
{
    /// <summary>What a piece does, which decides whether and how often it needs care.</summary>
    public enum CityRole
    {
        /// <summary>Decorations never need care: nobody is punished for decorating.</summary>
        Decor,
        /// <summary>Homes: care every 3 days for 10% of the price.</summary>
        Home,
        /// <summary>Production buildings: every 2 days for 15%.</summary>
        Producer,
        /// <summary>Special (star) pieces: every 4 days for 8%.</summary>
        Special
    }
}
