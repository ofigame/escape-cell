using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Bombs from Bip's workshop (sold from level 31): once bought, foi carries three to every floor and throws them
    /// at the thickest knot of enemies near it. Three levels: a bigger bang each time, and at the top a wider blast.
    /// </summary>
    public static class Bombs
    {
        private const string Key = "sb_bombs";

        /// <summary>The level (0-based index) from which bombs are sold and carried.</summary>
        public const int FromLevel = 30;
        public const int PerFloor = 3;
        public const int MaxLevel = 3;
        /// <summary>How far a bomb is thrown (diagonals count as one step).</summary>
        public const int ThrowRange = 6;

        private static readonly int[] Prices = { 1200, 3200, 7000 };
        private static readonly int[] Damages = { 6, 9, 13 };

        /// <summary>0 = not bought; 1..3.</summary>
        public static int Level => Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, MaxLevel);
        public static bool Owned => Level > 0;
        public static bool Unlocked => Owned || SaveData.UnlockedLevel >= FromLevel;
        public static bool IsMaxed => Level >= MaxLevel;
        public static int NextPrice => IsMaxed ? 0 : Prices[Level];

        public static int Damage => Damages[Mathf.Max(1, Level) - 1];
        /// <summary>The blast's reach round where it lands: the 3x3 square, 5x5 at the top level.</summary>
        public static int Radius => Level >= 3 ? 2 : 1;

        public static bool TryBuy()
        {
            if (IsMaxed || !Unlocked || !Shop.Spend(NextPrice)) return false;
            PlayerPrefs.SetInt(Key, Level + 1);
            PlayerPrefs.Save();
            return true;
        }
    }
}
