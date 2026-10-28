using System;
using System.Globalization;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The season road (the scenario's §9.4): 30 steps a calendar month, 100 points each, from duels (a win 10, a draw 6,
    /// a loss 4) and quests (20–100). Most steps give gold or a plain / bronze chest; 5, 15 and 25 core parts (10 / 15 /
    /// 25); 10 the season's emote, 20 two epic items, 30 a gold chest and 3,000 gold. The free track only for now.
    /// </summary>
    public static class SeasonRoad
    {
        public const int Steps = 30, PerStep = 100;
        public const int Win = 10, Draw = 6, Loss = 4;
        public enum Kind { Gold, Basic, Bronze, Cores, Emote, Items, GoldChest }

        /// <summary>What a step (1..30) gives.</summary>
        public static (Kind kind, int amount) Gift(int step)
        {
            switch (step)
            {
                case 5: return (Kind.Cores, 10);
                case 15: return (Kind.Cores, 15);
                case 25: return (Kind.Cores, 25);
                case 10: return (Kind.Emote, 1);
                case 20: return (Kind.Items, 2); // (two epic items: the season's armour paint's place)
                case 30: return (Kind.GoldChest, 3000); // (a gold chest and the gold: the season's cape's place)
            }
            if (step % 4 == 0) return (Kind.Bronze, 1);
            if (step % 3 == 0) return (Kind.Basic, 1);
            return (Kind.Gold, 100 + 15 * step);
        }

        private static string Month => DateTime.Now.ToString("yyyyMM", CultureInfo.InvariantCulture);
        private const string MonthKey = "sb_sr_month", PointsKey = "sb_sr_points", TakenKey = "sb_sr_taken";

        /// <summary>A new month: the road starts again.</summary>
        private static void Roll()
        {
            if (Prefs.GetString(MonthKey, "") == Month) return;
            Prefs.SetString(MonthKey, Month);
            Prefs.SetInt(PointsKey, 0);
            Prefs.SetString(TakenKey, "");
            Prefs.Save();
        }

        public static int Points { get { Roll(); return Prefs.GetInt(PointsKey, 0); } }

        /// <summary>Steps reached (0..30).</summary>
        public static int Reached => Mathf.Min(Steps, Points / PerStep);

        public static void Add(int points)
        {
            if (points <= 0) return;
            Roll();
            Prefs.SetInt(PointsKey, Mathf.Min(Steps * PerStep, Prefs.GetInt(PointsKey, 0) + points));
            Prefs.Save();
        }

        public static bool Taken(int step) { Roll(); return Prefs.GetString(TakenKey, "").Contains("|" + step + "|"); }

        /// <summary>Steps reached and not yet taken (for a red dot).</summary>
        public static int Waiting
        {
            get
            {
                int n = 0;
                for (int s = 1; s <= Reached; s++) if (!Taken(s)) n++;
                return n;
            }
        }

        /// <summary>Takes a reached step's gift (true when given).</summary>
        public static bool Take(int step)
        {
            if (step < 1 || step > Reached || Taken(step)) return false;
            var g = Gift(step);
            ItemDrops.FromSeasonStep(step); // (the items beside the step's gift)
            switch (g.kind)
            {
                case Kind.Gold: SaveData.Coins += g.amount; break;
                case Kind.Basic: DuelChests.Add(ChestTier.Basic); break;
                case Kind.Bronze: DuelChests.Add(ChestTier.Mid); break;
                case Kind.Cores: CoreParts.Add(g.amount); break;
                case Kind.Emote: Prefs.SetInt("sb_emote_season_" + Month, 1); break; // (the season's emote, in the duel's emote row)
                case Kind.Items:
                {
                    var items = new System.Collections.Generic.List<ItemGrant>();
                    for (int i = 0; i < g.amount; i++) items.Add(ItemDrops.AnyItem(ItemRarity.Epic));
                    ItemDrops.Give("season", items);
                    break;
                }
                case Kind.GoldChest:
                    DuelChests.Add(ChestTier.Advanced);
                    SaveData.Coins += g.amount;
                    break;
            }
            Prefs.SetString(TakenKey, Prefs.GetString(TakenKey, "") + "|" + step + "|");
            Prefs.Save();
            return true;
        }

        // ---------- The paid track (the season pass) ----------

        /// <summary>The pass's gift at a step: more gold, a bronze chest every third step, a gold chest every tenth, 10 core-free.</summary>
        public static (Kind kind, int amount) PassGift(int step) =>
            step % 10 == 0 ? (Kind.GoldChest, 0) : step % 3 == 0 ? (Kind.Bronze, 1) : (Kind.Gold, 250 + 50 * step);

        public static bool PassTaken(int step) { Roll(); return Prefs.GetString(TakenKey, "").Contains("|p" + step + "|"); }

        /// <summary>Takes a reached step's pass gift (with the month's pass bought).</summary>
        public static bool TakePass(int step)
        {
            if (!Monetization.Store.HasPass || step < 1 || step > Reached || PassTaken(step)) return false;
            var g = PassGift(step);
            if (g.kind == Kind.Gold) SaveData.Coins += g.amount;
            else if (g.kind == Kind.Bronze) DuelChests.Add(ChestTier.Mid);
            else DuelChests.Add(ChestTier.Advanced);
            Prefs.SetString(TakenKey, Prefs.GetString(TakenKey, "") + "|p" + step + "|");
            Prefs.Save();
            return true;
        }

        /// <summary>Days left in the month's season.</summary>
        public static int DaysLeft => (int)Math.Ceiling((new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1) - DateTime.Now).TotalDays);
    }
}
