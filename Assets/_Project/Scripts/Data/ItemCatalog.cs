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

        [Header("After a duel (section 3.1): rarity chances (common, rare, epic) and the kinds' weights")]
        public float[] matchRarity = { 0.80f, 0.18f, 0.02f };
        /// <summary>The kinds' weights: body, arms, shell, whetstone, experience (the repair potions come by their own rules).</summary>
        public int[] kindWeights = { 25, 25, 25, 12, 13 };
        /// <summary>The player's lowest part's weight raised by this (the parts kept close).</summary>
        public int lowestPartBonus = 10;
        /// <summary>This many ranked duels with no epic: the next one's reward brings one.</summary>
        public int pityMatches = 25;
        /// <summary>Items a ranked win, draw and loss bring; the friends' matches: a common one for the day's first few.</summary>
        public int winItems = 2, drawItems = 1, lossItems = 1, friendDaily = 3;
        /// <summary>A loss streak this long brings a repair potion.</summary>
        public int lossesForRepair = 3;

        [Header("Chests (section 4): plain, bronze, gold")]
        public int[] chestItems = { 2, 4, 8 };
        public float[] chestCommon = { 0.90f, 0.70f, 0.35f };
        public float[] chestRare = { 0.10f, 0.27f, 0.45f };
        public float[] chestEpic = { 0f, 0.03f, 0.20f }; // (the gold chest's epic 15% → 20%: the outfits' share, Docs/RUTBE_KUSAMI_TAC_SAC_BIRLESIK.md 1.3)
        /// <summary>At least one of this rarity (0: none, 1: rare, 2: epic).</summary>
        public int[] chestAtLeast = { 0, 1, 2 };
        /// <summary>The repair potion's chance (1: always), and the great one's.</summary>
        public float[] chestRepair = { 0.30f, 1f, 0f };
        public float[] chestGreatRepair = { 0f, 0.10f, 1f };
        /// <summary>The share the outfits had, now extra items: their chance, how many, and the first one's rarity at least
        /// (plain 3%: one common, bronze 8%: one rare, gold 30%: two, one of them rare at least).</summary>
        public float[] chestBonus = { 0.03f, 0.08f, 0.30f };
        public int[] chestBonusItems = { 1, 1, 2 };
        public int[] chestBonusRarity = { 0, 1, 1 };
        /// <summary>The gold chest's kinds' weights (richer in whetstones and experience).</summary>
        public int[] goldChestWeights = { 20, 20, 20, 20, 20 };

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
