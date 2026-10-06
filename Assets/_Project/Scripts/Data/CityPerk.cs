namespace SquashBot.Data
{
    /// <summary>
    /// The small conveniences production buildings give the main game. They never make a level easier;
    /// they only work while the building is cared for (fresh or dusty).
    /// </summary>
    public enum CityPerk
    {
        None,
        /// <summary>Gold mine: 5 coins an hour, up to 60 waiting to be picked up.</summary>
        Mine,
        /// <summary>Workshop: tool charges come back 10% more often.</summary>
        Workshop,
        /// <summary>Training ground: the daily bonus game can be any tunnel type you have seen.</summary>
        Training,
        /// <summary>Town square: +20% coins from the daily chest.</summary>
        Square,
        /// <summary>Repair shop: care costs 25% less.</summary>
        RepairShop
    }
}
