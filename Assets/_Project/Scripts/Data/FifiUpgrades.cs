using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Fifi, foi's companion on the floors, grows with coins from the workshop: five levels, each zapping harder and
    /// more often and taking more before it goes down.
    /// </summary>
    public static class FifiUpgrades
    {
        private const string Key = "sb_fifi_level";

        public const int MaxLevel = 5;

        private static readonly int[] Damage = { 1, 1, 2, 3, 4 };
        private static readonly float[] Interval = { 1.6f, 1.3f, 1.1f, 0.9f, 0.75f };
        private static readonly int[] Health = { 10, 13, 16, 20, 25 };
        /// <summary>The price of reaching each level (level 1 comes with the game).</summary>
        private static readonly int[] Prices = { 0, 900, 2200, 4800, 9500 };

        public static int Level => Mathf.Clamp(PlayerPrefs.GetInt(Key, 1), 1, MaxLevel);
        public static bool IsMaxed => Level >= MaxLevel;
        public static int NextPrice => IsMaxed ? 0 : Prices[Level];

        /// <summary>Damage of one zap.</summary>
        public static int ZapDamage => Damage[Level - 1];
        /// <summary>Seconds between zaps.</summary>
        public static float ZapInterval => Interval[Level - 1];
        /// <summary>Health points (a robot's slam takes 2, an enforcer's 3, a crate or vanG 2, a tower bolt 1).</summary>
        public static int MaxHealth => Health[Level - 1];

        /// <summary>How far Fifi zaps (diagonals count as one step), the same reach as foi's blows.</summary>
        public const int Range = 2;

        public static bool TryUpgrade()
        {
            if (IsMaxed || !Shop.Spend(NextPrice)) return false;
            PlayerPrefs.SetInt(Key, Level + 1);
            PlayerPrefs.Save();
            return true;
        }
    }
}
