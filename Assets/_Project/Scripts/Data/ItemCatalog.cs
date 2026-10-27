using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The items' numbers (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, sections 1 and 2): what each rarity gives, and what a part
    /// level asks (its points, gold and core parts). Resources/Items/ItemCatalog (made by the editor; without it, these).
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Items/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        [Header("What an item gives (common, rare, epic)")]
        /// <summary>A part item's points on its part.</summary>
        public int[] partPoints = { 10, 50, 200 };
        /// <summary>A whetstone's mastery points on the carried weapon.</summary>
        public int[] whetPoints = { 3, 15, 60 };
        /// <summary>An experience potion's duel XP.</summary>
        public int[] xpPoints = { 100, 500, 2000 };

        [Header("A part's level L (1..20)")]
        /// <summary>The part points a level asks: base + perLevel × L (30 … 220).</summary>
        public int pointsBase = 20, pointsPerLevel = 10;
        /// <summary>Gold for levels 1–10: perLevel × L (100 … 1,000).</summary>
        public int goldPerLevelLow = 100;
        /// <summary>Gold for levels 11–20: base + perLevel × (L − 11) (1,250 … 3,500).</summary>
        public int goldBaseHigh = 1250, goldPerLevelHigh = 250;
        /// <summary>Core parts for levels 11–20: base + perLevel × (L − 11) (10 … 55); none below.</summary>
        public int coresBaseHigh = 10, coresPerLevelHigh = 5;

        [Header("The new system's one-off gift (section 2.3)")]
        public int giftCommonEach = 5, giftRareEach = 1, giftRepairs = 1;

        private static ItemCatalog current;

        public static ItemCatalog Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<ItemCatalog>("Items/ItemCatalog");
                if (current == null) current = CreateInstance<ItemCatalog>();
                return current;
            }
        }

        /// <summary>What one item of the kind and rarity gives (points, XP; a repair potion: its steps, the large one 3).</summary>
        public int Value(ItemKind kind, ItemRarity rarity)
        {
            int r = (int)rarity;
            switch (kind)
            {
                case ItemKind.Body:
                case ItemKind.Arms:
                case ItemKind.Shell: return partPoints[r];
                case ItemKind.Whet: return whetPoints[r];
                case ItemKind.Xp: return xpPoints[r];
                case ItemKind.RepairSmall: return 1;
                default: return 3;
            }
        }

        /// <summary>The part points level <paramref name="level"/> asks.</summary>
        public int PointsFor(int level) => pointsBase + pointsPerLevel * level;

        public int GoldFor(int level) => level <= 10 ? goldPerLevelLow * level : goldBaseHigh + goldPerLevelHigh * (level - 11);

        public int CoresFor(int level) => level <= 10 ? 0 : coresBaseHigh + coresPerLevelHigh * (level - 11);
    }
}
