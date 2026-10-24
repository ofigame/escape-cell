using System;
using System.Globalization;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The weekly rule mode (the scenario's §4.1, Monday to Thursday): equal power, and a rule that changes every week —
    /// one weapon for all, quick ultimate, no hearts, sudden death, giant foi, combo frenzy, shield war, random sets. No
    /// rank points: its own wins and gifts (3, 6 and 10 wins).
    /// </summary>
    public static class WeeklyMode
    {
        public enum Rule { OneWeapon, QuickUlti, Heartless, SuddenDeath, GiantFoi, ComboFrenzy, ShieldWar, RandomSet }

        private static int WeekIndex
        {
            get
            {
                var now = DateTime.Now;
                int back = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                return (int)(now.Date.AddDays(-back) - new DateTime(2026, 1, 5)).TotalDays / 7;
            }
        }

        /// <summary>Monday to Thursday.</summary>
        public static bool Open { get { var d = DateTime.Now.DayOfWeek; return d >= DayOfWeek.Monday && d <= DayOfWeek.Thursday; } }

        public static Rule ThisWeek => (Rule)(((WeekIndex % 8) + 8) % 8);

        /// <summary>This week's one weapon (the one-weapon rule): a shop weapon of the third tier, a family a week.</summary>
        public static WeaponDef Weapon
        {
            get
            {
                var pool = Armory.All.FindAll(w => w.kind != WeaponKind.Gun && !w.chest && !w.Special && w.tier == 3);
                return pool.Count > 0 ? pool[((WeekIndex % pool.Count) + pool.Count) % pool.Count] : Armory.All[0];
            }
        }

        public static readonly int[] Steps = { 3, 6, 10 };

        private const string WeekKey = "sb_wk_week", WinsKey = "sb_wk_wins", TakenKey = "sb_wk_taken";

        private static void Roll()
        {
            if (Prefs.GetInt(WeekKey, -1) == WeekIndex) return;
            Prefs.SetInt(WeekKey, WeekIndex);
            Prefs.SetInt(WinsKey, 0);
            Prefs.SetInt(TakenKey, 0);
            Prefs.Save();
        }

        public static int Wins { get { Roll(); return Prefs.GetInt(WinsKey, 0); } }

        /// <summary>A weekly duel won: counted; returns the step reached now (its gift given), or -1.</summary>
        public static int RecordWin()
        {
            Roll();
            int wins = Prefs.GetInt(WinsKey, 0) + 1;
            Prefs.SetInt(WinsKey, wins);
            int taken = Prefs.GetInt(TakenKey, 0);
            for (int s = 0; s < Steps.Length; s++)
            {
                if (wins < Steps[s] || (taken & (1 << s)) != 0) continue;
                Prefs.SetInt(TakenKey, taken | (1 << s));
                if (s == 0) SaveData.Coins += 500;
                else if (s == 1) CoreParts.Add(15);
                else DuelChests.Add(ChestTier.Mid);
                Prefs.Save();
                return s;
            }
            Prefs.Save();
            return -1;
        }
    }

    /// <summary>
    /// The weekend tournament (§4.1, Saturday and Sunday): an eight-player knockout, three rounds of 2-minute duels, the
    /// empty places filled with bots. A first-round win brings 300 gold, the final a bronze chest, the title a gold chest
    /// and the "Champion" title. One bracket a weekend.
    /// </summary>
    public static class Tournament
    {
        public const int Rounds = 3;

        /// <summary>Saturday or Sunday.</summary>
        public static bool Open { get { var d = DateTime.Now.DayOfWeek; return d == DayOfWeek.Saturday || d == DayOfWeek.Sunday; } }

        private static int Weekend
        {
            get
            {
                var now = DateTime.Now;
                int back = ((int)now.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
                return (int)(now.Date.AddDays(-back) - new DateTime(2026, 1, 3)).TotalDays / 7;
            }
        }

        private const string KeyWeekend = "sb_tn_weekend", KeyRound = "sb_tn_round", KeyOut = "sb_tn_out";

        private static void Roll()
        {
            if (Prefs.GetInt(KeyWeekend, -1) == Weekend) return;
            Prefs.SetInt(KeyWeekend, Weekend);
            Prefs.SetInt(KeyRound, 0);
            Prefs.SetInt(KeyOut, 0);
            Prefs.Save();
        }

        /// <summary>The round to play next (0 quarter-final, 1 semi-final, 2 final; 3: champion).</summary>
        public static int Round { get { Roll(); return Prefs.GetInt(KeyRound, 0); } }

        /// <summary>Knocked out this weekend.</summary>
        public static bool Out { get { Roll(); return Prefs.GetInt(KeyOut, 0) == 1; } }

        public static bool Champion => Round >= Rounds;

        /// <summary>This round's rival's seed (the same all weekend for the same round).</summary>
        public static int SeedFor(int round) => Weekend * 977 + round * 131 + 17;

        /// <summary>A tournament duel ended: on to the next round (its gift given) or out. Returns the gift's key (or null).</summary>
        public static string Record(bool won)
        {
            Roll();
            if (!won) { Prefs.SetInt(KeyOut, 1); Prefs.Save(); return null; }
            int round = Prefs.GetInt(KeyRound, 0);
            Prefs.SetInt(KeyRound, round + 1);
            string gift = null;
            if (round == 0) { SaveData.Coins += 300; gift = "tn.gift0"; }
            else if (round == 1) { DuelChests.Add(ChestTier.Mid); gift = "tn.gift1"; }
            else if (round == 2) { DuelChests.Add(ChestTier.Advanced); Prefs.SetInt("sb_title_champion", 1); gift = "tn.gift2"; }
            Prefs.Save();
            return gift;
        }
    }
}
