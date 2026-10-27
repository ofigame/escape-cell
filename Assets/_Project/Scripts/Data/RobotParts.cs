using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>foi's three parts, each levelled 0..20: the body (health), the arms (damage) and the shell (armour).</summary>
    public enum RobotPart { Body, Arms, Shell }

    /// <summary>
    /// foi's strength, part by part (the scenario's §3.3): the body adds 40 health a level (150 to start, 950 at 20), the
    /// arms 1% damage a level, the shell 1.25% armour a level. A level asks its part points first (items from the bag:
    /// <see cref="PartPoints"/>), then gold (from level 11 core parts too; the numbers in <see cref="ItemCatalog"/>). The
    /// look changes at levels 5, 10, 15 and 20 (the rival sees it too). Full strength on the floors; in a ranked duel the
    /// league ceiling cuts it down (see <see cref="DuelPower"/>).
    /// </summary>
    public static class RobotParts
    {
        public const int MaxLevel = 20;
        public const int BaseHp = 150, HpPerLevel = 40;
        public const float DamagePerLevel = 0.01f, ArmorPerLevel = 0.0125f;

        private static string Key(RobotPart p) => "sb_part_" + p;

        public static int Level(RobotPart p) => Mathf.Clamp(Prefs.GetInt(Key(p), 0), 0, MaxLevel);

        public static bool IsMaxed(RobotPart p) => Level(p) >= MaxLevel;

        /// <summary>The gold for reaching <paramref name="level"/> (1..20): 100 a level up to 10, then 1,250 + 250 a level
        /// (half the old prices: the items are needed too).</summary>
        public static int GoldFor(int level) => ItemCatalog.Current.GoldFor(level);

        /// <summary>The core parts for reaching <paramref name="level"/>: none up to 10, then 10 + 5 a level.</summary>
        public static int CoresFor(int level) => ItemCatalog.Current.CoresFor(level);

        public static int NextGold(RobotPart p) => IsMaxed(p) ? 0 : GoldFor(Level(p) + 1);
        public static int NextCores(RobotPart p) => IsMaxed(p) ? 0 : CoresFor(Level(p) + 1);

        /// <summary>The part's points bar is full and its gold and core parts are there.</summary>
        public static bool CanAfford(RobotPart p) => PartPoints.Full(p) && SaveData.Coins >= NextGold(p) && CoreParts.Count >= NextCores(p);

        /// <summary>Raises the part a level (true), taking its points and paying its gold and core parts; false when maxed,
        /// the bar is not full yet, or short of gold or core parts.</summary>
        public static bool TryUpgrade(RobotPart p)
        {
            if (!CanAfford(p)) return false;
            int gold = NextGold(p), cores = NextCores(p), points = PartPoints.Need(p);
            if (!Shop.Spend(gold)) return false;
            CoreParts.Spend(cores);
            PartPoints.Take(p, points);
            Prefs.SetInt(Key(p), Level(p) + 1);
            Prefs.Save();
            return true;
        }

        // ---------- What a level gives ----------

        public static int HpAt(int body) => BaseHp + HpPerLevel * Mathf.Clamp(body, 0, MaxLevel);
        public static float DamageAt(int arms) => DamagePerLevel * Mathf.Clamp(arms, 0, MaxLevel);
        public static float ArmorAt(int shell) => ArmorPerLevel * Mathf.Clamp(shell, 0, MaxLevel);

        /// <summary>foi's health points at full strength (the floors').</summary>
        public static int Hp => HpAt(Level(RobotPart.Body));
        /// <summary>The share the arms add to every blow (0..0.2).</summary>
        public static float DamageBonus => DamageAt(Level(RobotPart.Arms));
        /// <summary>The share of every blow taken the shell keeps off (0..0.25).</summary>
        public static float Armor => ArmorAt(Level(RobotPart.Shell));

        /// <summary>A part's look step (0..4): its look changes at levels 5, 10, 15 and 20.</summary>
        public static int LookStep(int level) => Mathf.Clamp(level / 5, 0, 4);

        /// <summary>What a part gives at a level, as shown ("590 health", "+12% damage", "15% armour").</summary>
        public static string Value(RobotPart p, int level) =>
            p == RobotPart.Body ? Loc.F("part.hp", HpAt(level))
            : p == RobotPart.Arms ? Loc.F("part.dmg", Mathf.RoundToInt(DamageAt(level) * 100f))
            : Loc.F("part.armor", (ArmorAt(level) * 100f).ToString("0.#"));
    }
}
