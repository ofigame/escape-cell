using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>The three bombs of the workshop.</summary>
    public enum BombKind
    {
        /// <summary>The blast: a bang that hits everything round where it lands (three levels).</summary>
        Blast,
        /// <summary>Ice: the enemies round where it lands freeze for a few seconds (and take a little damage).</summary>
        Ice,
        /// <summary>Fire: the tiles round where it lands burn for a few seconds, hurting the enemies on them.</summary>
        Fire
    }

    /// <summary>
    /// Bombs from Bip's workshop: once a kind is bought, foi carries three of it to every floor and throws one (the bomb
    /// button; the small button beside it picks the kind) at the thickest knot of enemies near it. The blast has three
    /// levels (a bigger bang each time, and at the top a wider one); ice and fire are bought once.
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
        public const int Kinds = 3;

        private static readonly int[] Prices = { 1200, 3200, 7000 };
        private static readonly int[] Damages = { 10, 15, 22 }; // (points: four health points each)

        /// <summary>The blast's level: 0 = not bought; 1..3.</summary>
        public static int Level => Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, MaxLevel);
        public static bool Owned => Level > 0;
        public static bool Unlocked => Owned || SaveData.UnlockedLevel >= FromLevel;
        public static bool IsMaxed => Level >= MaxLevel;
        public static int NextPrice => IsMaxed ? 0 : Prices[Level];

        /// <summary>The blast's damage (health points) at its level.</summary>
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

        // ---------- The three kinds ----------

        /// <summary>The level (0-based) a kind is sold from.</summary>
        public static int UnlockAt(BombKind k) => k == BombKind.Blast ? FromLevel : k == BombKind.Ice ? 40 : 60;

        public static bool KindUnlocked(BombKind k) => Has(k) || SaveData.UnlockedLevel >= UnlockAt(k);

        public static bool Has(BombKind k) => k == BombKind.Blast ? Owned : PlayerPrefs.GetInt(Key + "_" + k, 0) == 1;

        /// <summary>What the kind costs (the blast: its next level).</summary>
        public static int Price(BombKind k) => k == BombKind.Blast ? NextPrice : k == BombKind.Ice ? 2500 : 4000;

        public static bool TryBuy(BombKind k)
        {
            if (k == BombKind.Blast) return TryBuy();
            if (Has(k) || !KindUnlocked(k) || !Shop.Spend(Price(k))) return false;
            PlayerPrefs.SetInt(Key + "_" + k, 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>The kind's damage in points (four health points each) when it lands (fire: each second it burns).</summary>
        public static int DamageOf(BombKind k) => k == BombKind.Blast ? Damage : k == BombKind.Ice ? 3 : 5;

        public static int RadiusOf(BombKind k) => k == BombKind.Blast ? Radius : 2;

        /// <summary>Seconds the ice holds the enemies, and the fire burns.</summary>
        public const float FreezeSeconds = 4f, BurnSeconds = 4f;

        public static Color ColourOf(BombKind k) => k == BombKind.Blast ? new Color(0.95f, 0.45f, 0.25f) : k == BombKind.Ice ? new Color(0.45f, 0.82f, 1f) : new Color(1f, 0.6f, 0.15f);
    }
}
