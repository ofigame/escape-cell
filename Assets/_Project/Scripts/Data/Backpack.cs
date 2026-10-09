using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// foi's backpack, which has levels: everything bought from the armory and the tool shelf takes room in it (a
    /// weapon by its weight, a tool by its level), and a purchase only goes through when it still fits. Bigger, later
    /// weapons and tools weigh more, so before the strongest gear can be bought the backpack has to be upgraded. The
    /// backpack's level also sets how many of each skill orb it holds.
    /// Gear owned before the backpack came in is kept even if it is over the limit; only new purchases are checked.
    /// </summary>
    public static class Backpack
    {
        private const string LevelKey = "sb_pack_level";

        /// <summary>Room at each level (1..8).</summary>
        private static readonly int[] Capacities = { 4, 9, 16, 26, 40, 56, 72, 96 };
        /// <summary>The price of reaching each level (level 1 is where everyone starts).</summary>
        private static readonly int[] Prices = { 0, 400, 1200, 2800, 5500, 9500, 15000, 22000 };
        /// <summary>Skill orbs of one kind the backpack holds at each level.</summary>
        private static readonly int[] OrbRoom = { 3, 4, 5, 6, 7, 8, 9, 9 };

        public static int MaxLevel => Capacities.Length;

        public static int Level => Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, 1), 1, MaxLevel);

        public static bool IsMaxed => Level >= MaxLevel;

        public static int Capacity => Capacities[Level - 1];

        public static int CapacityAt(int level) => Capacities[Mathf.Clamp(level, 1, MaxLevel) - 1];

        public static int NextPrice => IsMaxed ? 0 : Prices[Level];

        /// <summary>How many orbs of one skill fit.</summary>
        public static int OrbCapacity => OrbRoom[Level - 1];

        public static int Weight(WeaponDef w)
        {
            switch (w.id)
            {
                case "mallet": return 1;
                case "ironSword": return 2;
                case "stoneAxe": return 2;
                case "steelHammer": return 3;
                case "spear": return 3;
                case "mace": return 4;
                case "crystalSword": return 5;
                case "battleAxe": return 6;
                case "stormSpear": return 6;
                case "starHammer": return 8;
                case "flameSword": return 9;
                case "plasmaBlade": return 10;
                case "novaMace": return 12;
                default: return w.tier + 1;
            }
        }

        /// <summary>Room one level of a tool takes (the late freeze and blast are bulkier).</summary>
        public static int Weight(Tool t) => t == Tool.Freeze || t == Tool.Blast ? 2 : 1;

        /// <summary>Room taken by everything owned.</summary>
        public static int Used
        {
            get
            {
                int used = 0;
                foreach (var w in Armory.All) if (Armory.Owned(w)) used += Weight(w);
                foreach (var t in Tools.All) used += Tools.Level(t) * Weight(t);
                return used;
            }
        }

        public static int Free => Capacity - Used;

        public static bool Fits(int weight) => Used + weight <= Capacity;

        /// <summary>The lowest backpack level at which this much more would fit (beyond the top level: the top level).</summary>
        public static int LevelNeeded(int weight)
        {
            int used = Used;
            for (int l = 1; l <= MaxLevel; l++) if (used + weight <= Capacities[l - 1]) return l;
            return MaxLevel + 1;
        }

        public static bool TryUpgrade()
        {
            if (IsMaxed || !Shop.Spend(NextPrice)) return false;
            PlayerPrefs.SetInt(LevelKey, Level + 1);
            PlayerPrefs.Save();
            return true;
        }
    }
}
