using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>Permanent upgrades bought with coins, each with a few levels.</summary>
    public enum Upgrade
    {
        /// <summary>Shields take more hits before they break (then last longer).</summary>
        Shield,
        /// <summary>Coins on nearby tiles fly to the robot.</summary>
        Magnet,
        /// <summary>The hover escape lasts longer.</summary>
        Hover,
        /// <summary>A bigger life tank.</summary>
        Lives
    }

    /// <summary>One-use items: bought ahead, picked before a level.</summary>
    public enum Boost
    {
        StartShield,
        ExtraRescue
    }

    /// <summary>
    /// The coin shop: what upgrades and boosts cost, what the player owns, and what each upgrade level does.
    /// Prices are tuned against roughly 10-25 coins per level, so a purchase is a goal worth a few levels.
    /// </summary>
    public static class Shop
    {
        public const int LifePrice = 150;
        public const int TunnelPrice = 250;

        private static readonly int[][] UpgradePrices =
        {
            new[] { 250, 500, 900, 1400 }, // Shield: 2 hits, 3 hits, +1.5 s, +3 s
            new[] { 400, 800, 1300 },      // Magnet: range 1, 2, 3
            new[] { 300, 600, 1000 },      // Hover: +0.5 s each
            new[] { 1200, 2500 },          // Lives: 6, 7
        };

        private static readonly int[] BoostPrices = { 80, 120 };

        public static int Level(Upgrade u) => PlayerPrefs.GetInt("sb_up_" + u, 0);
        public static int MaxLevel(Upgrade u) => UpgradePrices[(int)u].Length;
        public static bool IsMaxed(Upgrade u) => Level(u) >= MaxLevel(u);
        public static int NextPrice(Upgrade u) => IsMaxed(u) ? 0 : UpgradePrices[(int)u][Level(u)];

        public static bool TryBuy(Upgrade u)
        {
            if (IsMaxed(u) || !Spend(NextPrice(u))) return false;
            PlayerPrefs.SetInt("sb_up_" + u, Level(u) + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static int Owned(Boost b) => PlayerPrefs.GetInt("sb_boost_" + b, 0);
        public static int Price(Boost b) => BoostPrices[(int)b];

        public static bool TryBuy(Boost b)
        {
            if (!Spend(Price(b))) return false;
            PlayerPrefs.SetInt("sb_boost_" + b, Owned(b) + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static bool TryUse(Boost b)
        {
            if (Owned(b) <= 0) return false;
            PlayerPrefs.SetInt("sb_boost_" + b, Owned(b) - 1);
            PlayerPrefs.Save();
            return true;
        }

        public static bool Spend(int price)
        {
            if (price <= 0 || SaveData.Coins < price) return false;
            SaveData.Coins -= price;
            return true;
        }

        // ---------- What the upgrades do ----------

        /// <summary>Blocks a shield can take before it breaks.</summary>
        public static int ShieldHits => 1 + Mathf.Min(2, Level(Upgrade.Shield));

        /// <summary>Extra seconds on every shield (the last two shield levels).</summary>
        public static float ShieldBonusSeconds => Mathf.Max(0, Level(Upgrade.Shield) - 2) * 1.5f;

        /// <summary>Coins this many tiles away (or closer) are pulled in on every step.</summary>
        public static int MagnetRange => Level(Upgrade.Magnet);

        public static float HoverSeconds => 2f + 0.5f * Level(Upgrade.Hover);

        public static int MaxLives => 5 + Level(Upgrade.Lives);
    }
}
