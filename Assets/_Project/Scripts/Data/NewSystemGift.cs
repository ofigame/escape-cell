namespace SquashBot.Data
{
    /// <summary>
    /// The items' arrival (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 2.3): everyone gets, once, five common and one rare
    /// of each part item, a repair potion and a bronze chest; the parts' levels stay as they were. A cloud copy brought
    /// back carries the mark with it, so it is never given twice.
    /// </summary>
    public static class NewSystemGift
    {
        private const string Key = "sb_newsys_gift";

        public static bool Given => Prefs.GetInt(Key, 0) == 1;

        /// <summary>Gives the gift if it was not given yet (true: given now, Bip tells of it).</summary>
        public static bool GiveOnce()
        {
            if (Given) return false;
            Prefs.SetInt(Key, 1);
            var c = ItemCatalog.Current;
            foreach (var k in new[] { ItemKind.Body, ItemKind.Arms, ItemKind.Shell })
            {
                ItemBag.Add(k, ItemRarity.Common, c.giftCommonEach);
                ItemBag.Add(k, ItemRarity.Rare, c.giftRareEach);
            }
            ItemBag.Add(ItemKind.RepairSmall, ItemRarity.Common, c.giftRepairs);
            DuelChests.Add(ChestTier.Mid);
            Prefs.Save();
            return true;
        }
    }
}
