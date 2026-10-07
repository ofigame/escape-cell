using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>Permanent upgrades bought with coins, each with a few levels.</summary>
    public enum Upgrade
    {
        /// <summary>Kalkan: shields take more hits and last longer (Bip's old brush cap).</summary>
        Shield,
        /// <summary>Mıknatıs: coins on nearby tiles fly to the robot (the harbour crane's magnet, from level 91).</summary>
        Magnet,
        /// <summary>The hover escape lasts longer.</summary>
        Hover,
        /// <summary>A bigger life tank.</summary>
        Lives,
        /// <summary>Kurtarma hakkı: more rescue charges can be banked in a level (the spare battery, from level 11).</summary>
        Rescue
    }

    /// <summary>One-use items from Bip's daily counter: bought ahead, picked before a level.</summary>
    public enum Boost
    {
        StartShield,
        ExtraRescue,
        /// <summary>A monster level starts with the Thunder Hammer already in hand.</summary>
        StartHammer,
        /// <summary>Every coin of the level counts twice.</summary>
        DoubleCoins,
        /// <summary>Coins fly to the robot for the whole level.</summary>
        CoinMagnet,
        /// <summary>The first crash on a road costs no life (used up by itself).</summary>
        TunnelBoost
    }

    /// <summary>
    /// Bip's workshop: what upgrades and counter items cost, what the player owns, and what each upgrade level does.
    /// Prices follow the workshop document (about 25 coins a level): a skill level costs 150, 450 and 1200.
    /// </summary>
    public static class Shop
    {
        public const int LifePrice = 150;
        public const int TunnelPrice = 250;
        /// <summary>Counter items: at most this many of each are carried.</summary>
        public const int MaxCarried = 1;
        /// <summary>How many items the daily counter offers.</summary>
        public const int CounterSize = 3;

        private static readonly int[][] UpgradePrices =
        {
            new[] { 150, 450, 1200 },      // Shield: 2 hits +1 s, 3 hits +2 s, 3 hits +3 s
            new[] { 150, 450, 1200 },      // Magnet: range 2, 3, 4
            new[] { 300, 600, 1000 },      // Hover: +0.5 s each
            new[] { 1200, 2500 },          // Lives: 6, 7
            new[] { 150, 450, 1200 },      // Rescue: 4, 5, 6 charges
        };

        private static readonly int[] BoostPrices = { 60, 80, 100, 120, 70, 90 };

        public static int Level(Upgrade u) => Mathf.Min(PlayerPrefs.GetInt("sb_up_" + u, 0), MaxLevel(u));
        public static int MaxLevel(Upgrade u) => UpgradePrices[(int)u].Length;
        public static bool IsMaxed(Upgrade u) => Level(u) >= MaxLevel(u);
        public static int NextPrice(Upgrade u) => IsMaxed(u) ? 0 : UpgradePrices[(int)u][Level(u)];

        /// <summary>The level (0-based index) from which the story unlocks the upgrade.</summary>
        public static int UnlockLevel(Upgrade u) => u == Upgrade.Magnet ? 90 : u == Upgrade.Rescue ? Tools.FromLevel : 0;

        public static bool Unlocked(Upgrade u) => Level(u) > 0 || SaveData.UnlockedLevel >= UnlockLevel(u);

        public static bool TryBuy(Upgrade u)
        {
            if (IsMaxed(u) || !Unlocked(u) || !Spend(NextPrice(u))) return false;
            PlayerPrefs.SetInt("sb_up_" + u, Level(u) + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static int Owned(Boost b) => PlayerPrefs.GetInt("sb_boost_" + b, 0);
        public static int Price(Boost b) => BoostPrices[(int)b];
        public static bool Full(Boost b) => Owned(b) >= MaxCarried;

        public static bool TryBuy(Boost b)
        {
            if (Full(b) || !Spend(Price(b))) return false;
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

        /// <summary>Hard moments are not bought through: no counter items on boss and marathon levels.</summary>
        public static bool BoostsAllowed(LevelData level) => level.mission != MissionType.Boss && !level.marathon;

        /// <summary>Today's counter: a few of the items, a different mix every day.</summary>
        public static Boost[] Counter()
        {
            var all = (Boost[])System.Enum.GetValues(typeof(Boost));
            int day = (int)(System.DateTime.Today - new System.DateTime(2026, 1, 1)).TotalDays;
            var pick = new Boost[CounterSize];
            int start = ((day % all.Length) + all.Length) % all.Length;
            int step = day % 2 == 0 ? 1 : 2;
            for (int i = 0; i < CounterSize; i++) pick[i] = all[(start + i * step) % all.Length];
            return pick;
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

        /// <summary>Extra seconds on every shield (one more per level).</summary>
        public static float ShieldBonusSeconds => Level(Upgrade.Shield);

        /// <summary>Coins this many tiles away (or closer) are pulled in on every step.</summary>
        public static int MagnetRange => Level(Upgrade.Magnet) > 0 ? Level(Upgrade.Magnet) + 1 : 0;

        public static float HoverSeconds => 2f + 0.5f * Level(Upgrade.Hover);

        public static int MaxLives => 5 + Level(Upgrade.Lives);

        /// <summary>Rescue charges a level can bank at once.</summary>
        public static int MaxRescues => 3 + Level(Upgrade.Rescue);
    }
}
