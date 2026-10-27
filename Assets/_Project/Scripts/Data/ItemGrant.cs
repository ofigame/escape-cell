namespace SquashBot.Data
{
    /// <summary>Items given at once: so many of a kind and rarity.</summary>
    [System.Serializable]
    public struct ItemGrant
    {
        public ItemKind kind;
        public ItemRarity rarity;
        public int count;

        public ItemGrant(ItemKind kind, ItemRarity rarity, int count = 1)
        {
            this.kind = kind;
            this.rarity = ItemBag.HasRarity(kind) ? rarity : ItemRarity.Common;
            this.count = count;
        }
    }
}
