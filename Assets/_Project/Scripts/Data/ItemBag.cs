using System;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The bag (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, sections 1, 5 and 9): items gathered after duels, from chests and
    /// quests, kept without limit until the player USES them. A part item fills its part's points bar, a whetstone the
    /// carried weapon's mastery, an experience potion the duel level, a repair potion takes battle wear off. Never sold
    /// for money or ads. (The old <see cref="Backpack"/>, the gear's room, is a different thing.)
    /// </summary>
    public static class ItemBag
    {
        /// <summary>The bag changed (an item came or was used).</summary>
        public static event Action Changed;

        public static readonly ItemKind[] Kinds = { ItemKind.Body, ItemKind.Arms, ItemKind.Shell, ItemKind.Whet, ItemKind.Xp, ItemKind.RepairSmall, ItemKind.RepairLarge };
        public static readonly ItemRarity[] Rarities = { ItemRarity.Common, ItemRarity.Rare, ItemRarity.Epic };

        /// <summary>The item's id ("item_body", "item_repair_s"…).</summary>
        public static string Id(ItemKind k)
        {
            switch (k)
            {
                case ItemKind.Body: return "item_body";
                case ItemKind.Arms: return "item_arms";
                case ItemKind.Shell: return "item_shell";
                case ItemKind.Whet: return "item_whet";
                case ItemKind.Xp: return "item_xp";
                case ItemKind.RepairSmall: return "item_repair_s";
                default: return "item_repair_l";
            }
        }

        /// <summary>The repair potions have no rarity (kept as common).</summary>
        public static bool HasRarity(ItemKind k) => k != ItemKind.RepairSmall && k != ItemKind.RepairLarge;

        /// <summary>The part an item fills (null: not a part item).</summary>
        public static RobotPart? PartOf(ItemKind k) => k == ItemKind.Body ? RobotPart.Body : k == ItemKind.Arms ? RobotPart.Arms : k == ItemKind.Shell ? RobotPart.Shell : (RobotPart?)null;

        public static ItemKind ItemOf(RobotPart p) => p == RobotPart.Body ? ItemKind.Body : p == RobotPart.Arms ? ItemKind.Arms : ItemKind.Shell;

        private static string Key(ItemKind k, ItemRarity r) => "sb_inv_" + Id(k) + "_" + r.ToString().ToLowerInvariant();

        public static int Count(ItemKind k, ItemRarity r) => Mathf.Max(0, Prefs.GetInt(Key(k, HasRarity(k) ? r : ItemRarity.Common), 0));

        /// <summary>All of a kind, every rarity.</summary>
        public static int Count(ItemKind k)
        {
            if (!HasRarity(k)) return Count(k, ItemRarity.Common);
            int n = 0;
            foreach (var r in Rarities) n += Count(k, r);
            return n;
        }

        /// <summary>Everything in the bag (the menu's red count).</summary>
        public static int Total
        {
            get
            {
                int n = 0;
                foreach (var k in Kinds) n += Count(k);
                return n;
            }
        }

        public static void Add(ItemKind k, ItemRarity r, int n = 1)
        {
            if (n <= 0) return;
            if (!HasRarity(k)) r = ItemRarity.Common;
            Prefs.SetInt(Key(k, r), Count(k, r) + n);
            Prefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Why an item cannot be used now (null: it can): the part at its top, no weapon to whet, no wear to repair.</summary>
        public static string Blocked(ItemKind k)
        {
            var part = PartOf(k);
            if (part.HasValue) return RobotParts.IsMaxed(part.Value) ? "ibag.why.maxed" : null;
            if (k == ItemKind.Whet) { var w = Armory.Equipped; return w == null || w.kind == WeaponKind.Gun || Mastery.Level(w) >= Mastery.Max ? "ibag.why.whet" : null; }
            if (k == ItemKind.Xp) return null;
            return BattleWear.Level == 0 ? "ibag.why.shiny" : null;
        }

        /// <summary>Uses up to <paramref name="n"/> of the kind and rarity; how many were used.</summary>
        public static int Use(ItemKind k, ItemRarity r, int n = 1)
        {
            if (!HasRarity(k)) r = ItemRarity.Common;
            int used = 0;
            int value = ItemCatalog.Current.Value(k, r);
            while (used < n && Count(k, r) > 0 && Blocked(k) == null)
            {
                Prefs.SetInt(Key(k, r), Count(k, r) - 1);
                Apply(k, value);
                used++;
            }
            if (used > 0) { Prefs.Save(); Changed?.Invoke(); }
            return used;
        }

        private static void Apply(ItemKind k, int value)
        {
            var part = PartOf(k);
            if (part.HasValue) { PartPoints.Add(part.Value, value); return; }
            switch (k)
            {
                case ItemKind.Whet: Mastery.Add(Armory.Equipped, value); break;
                case ItemKind.Xp: DuelLevel.Add(value); break;
                case ItemKind.RepairSmall: BattleWear.Repair(1); break;
                case ItemKind.RepairLarge: BattleWear.Repair(BattleWear.MaxLevel); break;
            }
        }

        /// <summary>USE ALL: every part item, whetstone and experience potion (never the repair potions: the player picks
        /// when to repair). How many were used.</summary>
        public static int UseAll()
        {
            int used = 0;
            foreach (var k in Kinds)
            {
                if (!HasRarity(k)) continue;
                for (int i = Rarities.Length - 1; i >= 0; i--) used += Use(k, Rarities[i], int.MaxValue);
            }
            return used;
        }
    }
}
