using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The duel level (§3.7): foi's experience in the arena. Every duel brings XP — a win 100, a draw 60, a loss 40 (so a
    /// lost duel is never wasted), the day's first win 100 more, the match's own challenge 50, a comeback win 50. Level n
    /// needs 300 + 50 n. Each level brings 50 + 10 × level gold, every fifth a bronze chest; levels 10, 20, 30, 40 and 50
    /// a title; level 5 opens the passive slot.
    /// </summary>
    public static class DuelLevel
    {
        public const int Max = 50;
        public const int Win = 100, Draw = 60, Loss = 40, FirstWin = 100, Challenge = 50, Comeback = 50;
        private const string LevelKey = "sb_dlv", XpKey = "sb_dxp";

        public static int Level => Mathf.Clamp(Prefs.GetInt(LevelKey, 1), 1, Max);

        /// <summary>XP gathered toward the next level.</summary>
        public static int Xp => Prefs.GetInt(XpKey, 0);

        public static int Need(int level) => 300 + 50 * level;

        /// <summary>The titles of levels 10..50 (keys).</summary>
        public static string TitleKey(int level) => level >= 50 ? "title.d50" : level >= 40 ? "title.d40" : level >= 30 ? "title.d30" : level >= 20 ? "title.d20" : level >= 10 ? "title.d10" : null;

        /// <summary>What a level up gave, for the reward screen.</summary>
        public struct LevelUp
        {
            public int level, gold;
            public bool chest;
            public string title, move;
        }

        /// <summary>Adds XP: returns the level ups it made (their gifts already given).</summary>
        public static System.Collections.Generic.List<LevelUp> Add(int xp)
        {
            var ups = new System.Collections.Generic.List<LevelUp>();
            int level = Level, have = Xp + Mathf.Max(0, xp);
            while (level < Max && have >= Need(level))
            {
                have -= Need(level);
                level++;
                var up = new LevelUp { level = level, gold = 50 + 10 * level, chest = level % 5 == 0, title = level % 10 == 0 ? TitleKey(level) : null };
                if (level % 10 == 0) up.move = MoveSet.GiftNext(); // (a duel-only player is not left behind)
                SaveData.Coins += up.gold;
                if (up.chest) DuelChests.Add(ChestTier.Mid);
                ups.Add(up);
            }
            if (level >= Max) have = 0;
            Prefs.SetInt(LevelKey, level);
            Prefs.SetInt(XpKey, have);
            Prefs.Save();
            return ups;
        }
    }

    /// <summary>
    /// Weapon mastery (§3.8): every duel with a weapon adds to its mastery — a win 3, a draw 2, a loss 1. Ten levels
    /// (10, 25, 50, 80, 120, 170, 230, 300, 380, 470 points), each with its gift: gold, core parts, the badge on the rival
    /// card from level 7, the golden look and the family's master title at 10.
    /// </summary>
    public static class Mastery
    {
        public const int Max = 10;
        private static readonly int[] Needs = { 10, 25, 50, 80, 120, 170, 230, 300, 380, 470 };

        private static string Key(WeaponDef w) => "sb_mast_" + w.id;

        public static int Points(WeaponDef w) => Prefs.GetInt(Key(w), 0);

        public static int LevelOf(int points)
        {
            int l = 0;
            while (l < Max && points >= Needs[l]) l++;
            return l;
        }

        public static int Level(WeaponDef w) => LevelOf(Points(w));

        /// <summary>Points for the next level (0 at the top).</summary>
        public static int NextAt(int level) => level >= Max ? 0 : Needs[level];

        /// <summary>A level's gift: (gold, core parts).</summary>
        public static (int gold, int cores) GiftOf(int level) =>
            level == 2 ? (100, 0) : level == 4 ? (250, 0) : level == 6 ? (0, 15) : level == 8 ? (500, 10) : level == 9 ? (750, 15) : level == 10 ? (1250, 25) : (0, 0);

        /// <summary>Adds a duel's points to the weapon: returns the levels reached (their gifts already given).</summary>
        public static System.Collections.Generic.List<int> Add(WeaponDef w, int points)
        {
            var ups = new System.Collections.Generic.List<int>();
            if (w == null || w.kind == WeaponKind.Gun) return ups;
            int before = Level(w);
            Prefs.SetInt(Key(w), Points(w) + Mathf.Max(0, points));
            int after = Level(w);
            for (int l = before + 1; l <= after; l++)
            {
                var g = GiftOf(l);
                SaveData.Coins += g.gold;
                CoreParts.Add(g.cores);
                ups.Add(l);
            }
            Prefs.Save();
            return ups;
        }

        /// <summary>The master title of a family ("Sword Master"), won with a weapon of it at level 10.</summary>
        public static string TitleKey(WeaponDef w) => "title.master." + Armory.Family(w);
    }
}
