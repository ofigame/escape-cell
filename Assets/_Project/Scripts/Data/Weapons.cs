using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The robot's hammer for the arena fights: it starts as a plain wooden mallet and is upgraded in Bip's workshop,
    /// level by level, into a star hammer. Every level hits harder, holds more blows (ammo) and looks different.
    /// </summary>
    public static class Weapons
    {
        public const int MaxLevel = 5;
        private const string Key = "sb_hammer";

        /// <summary>What the next level costs (index = current level).</summary>
        private static readonly int[] Prices = { 0, 500, 1200, 2500, 4500 };

        /// <summary>Damage per blow, in monster health points (a monster has 10 per "hit" on its card).</summary>
        private static readonly int[] Damage = { 10, 14, 19, 25, 32 };

        /// <summary>How many hammer blows can be carried at once.</summary>
        private static readonly int[] Ammo = { 3, 4, 5, 6, 7 };

        /// <summary>The level (0-based index) from which each hammer can be bought.</summary>
        private static readonly int[] UnlockAt = { 0, 10, 40, 90, 150 };

        public static int Level => Mathf.Clamp(PlayerPrefs.GetInt(Key, 1), 1, MaxLevel);
        public static bool IsMaxed => Level >= MaxLevel;
        public static int NextPrice => IsMaxed ? 0 : Prices[Level];
        public static int NextUnlockLevel => IsMaxed ? 0 : UnlockAt[Level];
        public static bool NextUnlocked => !IsMaxed && SaveData.UnlockedLevel >= NextUnlockLevel;

        public static int HitDamage => Damage[Level - 1];
        public static int MaxAmmo => Ammo[Level - 1];
        public static int DamageAt(int level) => Damage[Mathf.Clamp(level, 1, MaxLevel) - 1];
        public static int AmmoAt(int level) => Ammo[Mathf.Clamp(level, 1, MaxLevel) - 1];

        public static bool TryUpgrade()
        {
            if (IsMaxed || !NextUnlocked || !Shop.Spend(NextPrice)) return false;
            PlayerPrefs.SetInt(Key, Level + 1);
            PlayerPrefs.Save();
            return true;
        }
    }
}
