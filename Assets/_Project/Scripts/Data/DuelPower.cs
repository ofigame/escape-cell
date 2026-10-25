using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>The four weapon families: each feels different in the hand, but two weapons of one tier do the same damage a second.</summary>
    public enum WeaponFamily { Sword, Spear, Axe, Heavy }

    /// <summary>
    /// The league ceiling (the scenario's §3.2): every rank tier caps the parts' levels, health, damage bonus, armour and
    /// the weapon's tier in ranked duels — strength is real, but at the ceiling skill decides. Friend matches and the weekly
    /// rule mode are "equal power": both sides at the third tier's ceiling. As the server's Rules.DuelStats.
    /// </summary>
    public static class DuelPower
    {
        public const int Tiers = 5;

        /// <summary>The tier of a rank (1..5): Er–Onbaşı, Çavuş–Başçavuş, Asteğmen–Üsteğmen, Yüzbaşı–Yarbay, Albay–Mareşal.</summary>
        public static int TierOf(int rank) => rank <= 1 ? 1 : rank <= 3 ? 2 : rank <= 6 ? 3 : rank <= 9 ? 4 : 5;

        /// <summary>The first rank of a tier (for "rise to Çavuş" lines).</summary>
        public static int FirstRankOf(int tier) => tier <= 1 ? 0 : tier == 2 ? 2 : tier == 3 ? 4 : tier == 4 ? 7 : 10;

        /// <summary>The part level cap of a tier (4, 8, 12, 16, 20).</summary>
        public static int PartCap(int tier) => 4 * Mathf.Clamp(tier, 1, Tiers);

        /// <summary>The weapon tier cap of a tier (2..6).</summary>
        public static int WeaponCap(int tier) => Mathf.Clamp(tier, 1, Tiers) + 1;

        /// <summary>The tier "equal power" matches are fought at.</summary>
        public const int EqualTier = 3;

        /// <summary>One side's duel strength: health (the duel's: twice the body's), damage bonus, armour and weapon tier.</summary>
        public struct Stats
        {
            public int hp;
            public float damage, armor;
            public int weaponTier;
        }

        /// <summary>Strength from part levels under a tier's ceiling (equal: exactly the ceiling).</summary>
        public static Stats For(int body, int arms, int shell, int weaponTier, int tier, bool equal)
        {
            int cap = PartCap(tier);
            if (equal) body = arms = shell = cap;
            return new Stats
            {
                hp = RobotParts.HpAt(Mathf.Min(body, cap)) * 2,
                damage = RobotParts.DamageAt(Mathf.Min(arms, cap)),
                armor = RobotParts.ArmorAt(Mathf.Min(shell, cap)),
                weaponTier = equal ? WeaponCap(tier) : Mathf.Min(weaponTier, WeaponCap(tier)),
            };
        }

        /// <summary>The player's own duel strength: ranked under the rank's ceiling, else equal power.</summary>
        public static Stats Mine(bool ranked, WeaponDef w) =>
            For(RobotParts.Level(RobotPart.Body), RobotParts.Level(RobotPart.Arms), RobotParts.Level(RobotPart.Shell), w.tier,
                ranked ? TierOf(Challenge.Rank) : EqualTier, !ranked);

        /// <summary>The player is at the ceiling of the rank's tier (every part at its cap): shown as an achievement.</summary>
        public static bool AtCeiling
        {
            get
            {
                int cap = PartCap(TierOf(Challenge.Rank));
                return TierOf(Challenge.Rank) < Tiers && RobotParts.Level(RobotPart.Body) >= cap && RobotParts.Level(RobotPart.Arms) >= cap && RobotParts.Level(RobotPart.Shell) >= cap;
            }
        }

        // ---------- The weapons' duel blows ----------

        // (the numbers: the duel's table, DuelBalanceTable, the same file the server reads)

        /// <summary>Seconds between blows of a family (sword 0.12, spear 0.18, axe 0.28, heavy 0.30 in the table).</summary>
        public static float Cooldown(WeaponFamily f) => DuelBalanceTable.Current.Cooldown(f);

        /// <summary>One duel blow of a weapon at a tier (its own, or the ceiling's when above it): the same damage a second
        /// within a tier for every family.</summary>
        public static int Blow(WeaponDef w, int tier) => DuelBalanceTable.Current.Blow(Armory.Family(w), tier);

        /// <summary>The power hit's and knockdown's factor by family (the axe's a fifth harder).</summary>
        public static float ComboTimes(WeaponDef w) => DuelBalanceTable.Current.ComboTimes(Armory.Family(w));

        /// <summary>How long a blow of this weapon staggers the one it hits (the heavy ones').</summary>
        public static float Stagger(WeaponDef w) => DuelBalanceTable.Current.Stagger(Armory.Family(w));
    }
}
