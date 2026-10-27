using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Where the items come from (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, sections 3 and 4): after a duel by its result and
    /// mode, the chests beside their old draw, the quests, the login calendar, the season road, the weekly mode, the
    /// tournament, a new rank and a world's monster. Every draw from <see cref="ItemCatalog"/>; never for money or ads.
    /// What a source gives goes into the bag at once and is returned to be shown.
    /// </summary>
    public static class ItemDrops
    {
        private const string PityKey = "sb_pity_epic", LossRowKey = "sb_item_lossrow", FriendDayKey = "sb_item_friend_day", FriendCountKey = "sb_item_friend_n";

        /// <summary>Items came from somewhere (the source, for the analytics and the reward screens).</summary>
        public static event Action<string, List<ItemGrant>> Dropped;

        private static readonly ItemKind[] Drawn = { ItemKind.Body, ItemKind.Arms, ItemKind.Shell, ItemKind.Whet, ItemKind.Xp };

        /// <summary>Ranked duels since the last epic.</summary>
        public static int Pity => Prefs.GetInt(PityKey, 0);

        /// <summary>Puts the items in the bag and tells of them (empty: nothing).</summary>
        public static List<ItemGrant> Give(string source, List<ItemGrant> items)
        {
            foreach (var g in items) ItemBag.Add(g.kind, g.rarity, g.count);
            if (items.Count > 0) Dropped?.Invoke(source, items);
            return items;
        }

        // ---------- the draws ----------

        private static ItemRarity RollRarity(float common, float rare, float epic)
        {
            float v = UnityEngine.Random.value * (common + rare + epic);
            return v < common ? ItemRarity.Common : v < common + rare ? ItemRarity.Rare : ItemRarity.Epic;
        }

        /// <summary>A kind by the weights (body, arms, shell, whetstone, experience), the lowest part's raised.</summary>
        private static ItemKind RollKind(int[] weights, bool balance)
        {
            var w = new int[Drawn.Length];
            for (int i = 0; i < w.Length; i++) w[i] = i < weights.Length ? Mathf.Max(0, weights[i]) : 0;
            if (balance)
            {
                int low = Mathf.Min(RobotParts.Level(RobotPart.Body), Mathf.Min(RobotParts.Level(RobotPart.Arms), RobotParts.Level(RobotPart.Shell)));
                for (int i = 0; i < 3; i++) if (RobotParts.Level((RobotPart)i) == low) w[i] += ItemCatalog.Current.lowestPartBonus;
            }
            int total = 0;
            foreach (var x in w) total += x;
            int v = UnityEngine.Random.Range(0, Mathf.Max(1, total));
            for (int i = 0; i < w.Length; i++) { if (v < w[i]) return Drawn[i]; v -= w[i]; }
            return ItemKind.Body;
        }

        /// <summary>A part item of a rarity (the lowest part likelier).</summary>
        public static ItemGrant PartItem(ItemRarity r) => new ItemGrant(RollKind(new[] { 1, 1, 1, 0, 0 }, true), r);

        /// <summary>Any drawn item of a rarity.</summary>
        public static ItemGrant AnyItem(ItemRarity r) => new ItemGrant(RollKind(ItemCatalog.Current.kindWeights, true), r);

        // ---------- after a duel (3.1, 3.2) ----------

        /// <summary>The duel's mode for its items.</summary>
        public enum DuelMode { Ranked, Friend, Event, Practice }

        /// <summary>A duel's items, given (section 3.1–3.2). <paramref name="firstWin"/>: the day's first win.</summary>
        public static List<ItemGrant> AfterDuel(DuelMode mode, bool won, bool draw, bool firstWin)
        {
            var c = ItemCatalog.Current;
            var items = new List<ItemGrant>();
            if (mode == DuelMode.Practice || mode == DuelMode.Event) return items; // (the events give their own gifts)
            if (mode == DuelMode.Friend)
            {
                string today = GameClock.Today;
                if (Prefs.GetString(FriendDayKey, "") != today) { Prefs.SetString(FriendDayKey, today); Prefs.SetInt(FriendCountKey, 0); }
                int n = Prefs.GetInt(FriendCountKey, 0);
                if (n < c.friendDaily) { Prefs.SetInt(FriendCountKey, n + 1); items.Add(AnyItem(ItemRarity.Common)); }
                return Give("friend", items);
            }
            int count = won ? c.winItems : draw ? c.drawItems : c.lossItems;
            bool pity = Pity >= c.pityMatches;
            for (int i = 0; i < count; i++)
            {
                var r = pity && i == 0 ? ItemRarity.Epic : RollRarity(c.matchRarity[0], c.matchRarity[1], c.matchRarity[2]);
                items.Add(AnyItem(r));
            }
            if (won && firstWin) items.Add(AnyItem(ItemRarity.Rare));
            bool epic = items.Exists(g => g.rarity == ItemRarity.Epic);
            Prefs.SetInt(PityKey, epic ? 0 : Pity + 1);
            // the repairs: a win with battle wear on, the third loss in a row
            if (won && BattleWear.Level >= 1) items.Add(new ItemGrant(ItemKind.RepairSmall, ItemRarity.Common));
            int row = won || draw ? 0 : Prefs.GetInt(LossRowKey, 0) + 1;
            if (row >= c.lossesForRepair) { items.Add(new ItemGrant(ItemKind.RepairSmall, ItemRarity.Common)); row = 0; }
            Prefs.SetInt(LossRowKey, row);
            return Give("duel", items);
        }

        // ---------- chests (4) ----------

        /// <summary>A chest's items beside its old draw, the least rare first (the rarest is the last moment); given.</summary>
        public static List<ItemGrant> FromChest(ChestTier t)
        {
            var c = ItemCatalog.Current;
            int i = (int)t;
            var weights = t == ChestTier.Advanced ? c.goldChestWeights : c.kindWeights;
            var rarities = new List<ItemRarity>();
            for (int n = 0; n < c.chestItems[i]; n++) rarities.Add(RollRarity(c.chestCommon[i], c.chestRare[i], c.chestEpic[i]));
            // the guarantee: at least one of its rarity (the best drawn raised if none reached it)
            if (c.chestAtLeast[i] > 0 && rarities.Count > 0 && !rarities.Exists(r => (int)r >= c.chestAtLeast[i]))
                rarities[rarities.Count - 1] = (ItemRarity)c.chestAtLeast[i];
            rarities.Sort();
            var items = new List<ItemGrant>();
            foreach (var r in rarities) items.Add(new ItemGrant(RollKind(weights, true), r));
            if (UnityEngine.Random.value < c.chestRepair[i]) items.Insert(0, new ItemGrant(ItemKind.RepairSmall, ItemRarity.Common));
            if (UnityEngine.Random.value < c.chestGreatRepair[i]) items.Insert(0, new ItemGrant(ItemKind.RepairLarge, ItemRarity.Common));
            return Give("chest" + i, items);
        }

        /// <summary>The chest's items as its contents screen shows them ("4 items · Common 70% · Rare 27% · Epic 3% · at
        /// least 1 Rare · Repair Potion"), from the same table as the draw.</summary>
        public static string ChestText(ChestTier t)
        {
            var c = ItemCatalog.Current;
            int i = (int)t;
            string Pct(float p) => Loc.F("fmt.pct", Mathf.RoundToInt(p * 100f));
            var s = Loc.F("drop.chestItems", c.chestItems[i]) + " · " + Loc.T("rarity.Common") + " " + Pct(c.chestCommon[i]) + " · " + Loc.T("rarity.Rare") + " " + Pct(c.chestRare[i]);
            if (c.chestEpic[i] > 0f) s += " · " + Loc.T("rarity.Epic") + " " + Pct(c.chestEpic[i]);
            if (c.chestAtLeast[i] > 0) s += " · " + Loc.F("drop.atLeast", Loc.T("rarity." + (ItemRarity)c.chestAtLeast[i]));
            if (c.chestRepair[i] > 0f) s += " · " + Loc.T("item.RepairSmall") + (c.chestRepair[i] >= 1f ? "" : " " + Pct(c.chestRepair[i]));
            if (c.chestGreatRepair[i] > 0f) s += " · " + Loc.T("item.RepairLarge") + (c.chestGreatRepair[i] >= 1f ? "" : " " + Pct(c.chestGreatRepair[i]));
            return s;
        }

        // ---------- the other sources (3.3) ----------

        /// <summary>A quest's item: a rare one a day (an ultimate quest an experience potion, a special or combo one a
        /// whetstone, else a part item), an epic part item a week.</summary>
        public static List<ItemGrant> FromQuest(bool weekly, DuelGoal goal)
        {
            var items = new List<ItemGrant>();
            var r = weekly ? ItemRarity.Epic : ItemRarity.Rare;
            if (goal == DuelGoal.Ultimate || goal == DuelGoal.DodgeUlti) items.Add(new ItemGrant(ItemKind.Xp, r));
            else if (goal == DuelGoal.Special || goal == DuelGoal.Combo5) items.Add(new ItemGrant(ItemKind.Whet, r));
            else items.Add(PartItem(r));
            return Give(weekly ? "quest_week" : "quest_day", items);
        }

        /// <summary>The login calendar's items beside its gift (day 0-based): day 1 three common, day 3 a rare and a repair
        /// potion, day 5 a bronze chest, day 7 a gold chest.</summary>
        public static List<ItemGrant> FromLogin(int day)
        {
            var items = new List<ItemGrant>();
            if (day == 0) for (int i = 0; i < 3; i++) items.Add(AnyItem(ItemRarity.Common));
            else if (day == 2) { items.Add(AnyItem(ItemRarity.Rare)); items.Add(new ItemGrant(ItemKind.RepairSmall, ItemRarity.Common)); }
            else if (day == 4) DuelChests.Add(ChestTier.Mid);
            else if (day == 6) DuelChests.Add(ChestTier.Advanced);
            return Give("login", items);
        }

        /// <summary>The season road's items with a step: steps 10, 20 and 30 an epic; every second gold step two rares.</summary>
        public static List<ItemGrant> FromSeasonStep(int step)
        {
            var items = new List<ItemGrant>();
            if (step == 10 || step == 20 || step == 30) items.Add(AnyItem(ItemRarity.Epic));
            else if (SeasonRoad.Gift(step).kind == SeasonRoad.Kind.Gold)
            {
                int nth = 0; // (which gold step this is)
                for (int s = 1; s <= step; s++) if (SeasonRoad.Gift(s).kind == SeasonRoad.Kind.Gold) nth++;
                if (nth % 2 == 0) { items.Add(AnyItem(ItemRarity.Rare)); items.Add(AnyItem(ItemRarity.Rare)); }
            }
            return Give("season", items);
        }

        /// <summary>The weekly mode's step (0, 1, 2 at 3, 6, 10 wins): three common, two rare, an epic.</summary>
        public static List<ItemGrant> FromWeekly(int step)
        {
            var items = new List<ItemGrant>();
            if (step == 0) for (int i = 0; i < 3; i++) items.Add(AnyItem(ItemRarity.Common));
            else if (step == 1) for (int i = 0; i < 2; i++) items.Add(AnyItem(ItemRarity.Rare));
            else if (step == 2) items.Add(AnyItem(ItemRarity.Epic));
            return Give("weekly", items);
        }

        /// <summary>The tournament's round won (0 quarter, 1 semi, 2 final): two rare, an epic, two epics.</summary>
        public static List<ItemGrant> FromTournament(int round)
        {
            var items = new List<ItemGrant>();
            if (round == 0) for (int i = 0; i < 2; i++) items.Add(AnyItem(ItemRarity.Rare));
            else if (round == 1) items.Add(AnyItem(ItemRarity.Epic));
            else if (round == 2) for (int i = 0; i < 2; i++) items.Add(AnyItem(ItemRarity.Epic));
            return Give("tournament", items);
        }

        /// <summary>A new rank: two epic part items.</summary>
        public static List<ItemGrant> FromRankUp() => Give("rank", new List<ItemGrant> { PartItem(ItemRarity.Epic), PartItem(ItemRarity.Epic) });

        /// <summary>A world's monster beaten the first time: three rare part items.</summary>
        public static List<ItemGrant> FromWorldMonster() => Give("world", new List<ItemGrant> { PartItem(ItemRarity.Rare), PartItem(ItemRarity.Rare), PartItem(ItemRarity.Rare) });

        /// <summary>The items as a line ("Body Plate ×2 (Rare) · Repair Potion").</summary>
        public static string Text(List<ItemGrant> items)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var g in items)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(Loc.T("item." + g.kind));
                if (g.count > 1) sb.Append(" ×").Append(g.count);
                if (ItemBag.HasRarity(g.kind) && g.rarity != ItemRarity.Common) sb.Append(" (").Append(Loc.T("rarity." + g.rarity)).Append(')');
            }
            return sb.ToString();
        }
    }
}
