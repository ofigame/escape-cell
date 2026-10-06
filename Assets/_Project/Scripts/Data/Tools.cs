using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>Active tools the robot carries into a level and fires with a button.</summary>
    public enum Tool
    {
        /// <summary>Everything but the robot slows down for a few seconds.</summary>
        SlowMo,
        /// <summary>Mends the holes and fire around the robot and keeps those tiles from breaking for a while.</summary>
        Bridge,
        /// <summary>Wipes every block in the air and on the ground (and, upgraded, lasers and barrels too).</summary>
        Emp
    }

    /// <summary>
    /// The tool bag: tools are unlocked and upgraded with coins (level 1-3), and up to two ride along in the bag.
    /// Each level starts with one charge per tool (two at level 3); close calls and combos refill them.
    /// Every tool can be tried once for free before buying it.
    /// </summary>
    public static class Tools
    {
        public const int MaxLevel = 3;
        public const int SecondSlotPrice = 1500;
        /// <summary>Tools (and trials) show up from this level (0-based index) on.</summary>
        public const int FromLevel = 10;

        private static readonly int[] UnlockPrices = { 600, 800, 1200 };
        private static readonly int[] UpgradePrices = { 400, 800 };

        public static Tool[] All => (Tool[])System.Enum.GetValues(typeof(Tool));

        public static int Level(Tool t) => PlayerPrefs.GetInt("sb_tool_" + t, 0);
        public static bool Owned(Tool t) => Level(t) > 0;
        public static bool IsMaxed(Tool t) => Level(t) >= MaxLevel;

        public static int NextPrice(Tool t)
        {
            int level = Level(t);
            if (level == 0) return UnlockPrices[(int)t];
            return level >= MaxLevel ? 0 : UpgradePrices[level - 1];
        }

        public static bool TryBuy(Tool t)
        {
            if (IsMaxed(t) || !Shop.Spend(NextPrice(t))) return false;
            bool first = !Owned(t);
            PlayerPrefs.SetInt("sb_tool_" + t, Level(t) + 1);
            if (first && Equipped(0) == null) SetSlot(0, t); // a first tool goes straight into the bag
            PlayerPrefs.Save();
            return true;
        }

        // ---------- Bag ----------

        public static int Slots => PlayerPrefs.GetInt("sb_tool_slot2", 0) == 1 ? 2 : 1;

        public static bool TryBuySecondSlot()
        {
            if (Slots >= 2 || !Shop.Spend(SecondSlotPrice)) return false;
            PlayerPrefs.SetInt("sb_tool_slot2", 1);
            PlayerPrefs.Save();
            return true;
        }

        public static Tool? Equipped(int slot)
        {
            if (slot >= Slots) return null;
            int v = PlayerPrefs.GetInt("sb_tool_bag" + slot, -1);
            return v >= 0 && Owned((Tool)v) ? (Tool?)(Tool)v : null;
        }

        private static void SetSlot(int slot, Tool? t)
        {
            PlayerPrefs.SetInt("sb_tool_bag" + slot, t.HasValue ? (int)t.Value : -1);
            PlayerPrefs.Save();
        }

        public static bool IsEquipped(Tool t) => Equipped(0) == t || Equipped(1) == t;

        /// <summary>Puts the tool in the bag (first free slot, else the first slot), or takes it out again.</summary>
        public static void ToggleEquip(Tool t)
        {
            if (!Owned(t)) return;
            for (int s = 0; s < 2; s++)
                if (Equipped(s) == t) { SetSlot(s, null); return; }
            for (int s = 0; s < Slots; s++)
                if (Equipped(s) == null) { SetSlot(s, t); return; }
            SetSlot(0, t);
        }

        // ---------- Trials ----------

        public static bool TrialUsed(Tool t) => PlayerPrefs.GetInt("sb_tool_trial_" + t, 0) == 1;

        public static void MarkTrial(Tool t)
        {
            PlayerPrefs.SetInt("sb_tool_trial_" + t, 1);
            PlayerPrefs.Save();
        }

        /// <summary>A tool the player has neither bought nor tried yet, to offer as a free trial.</summary>
        public static Tool? NextTrial()
        {
            foreach (var t in All)
                if (!Owned(t) && !TrialUsed(t)) return t;
            return null;
        }

        // ---------- What each level does ----------

        public static int Charges(int level) => level >= 3 ? 2 : 1;

        public static float SlowSeconds(int level) => level >= 2 ? 4.5f : 3f;
        public const float SlowScale = 0.45f;

        public static float BridgeSeconds(int level) => level >= 2 ? 8f : 5f;
        public static int BridgeRadius(int level) => level >= 2 ? 3 : 2;

        /// <summary>Upgraded EMP also switches off lasers and barrels.</summary>
        public static bool EmpClearsRules(int level) => level >= 2;
    }
}
