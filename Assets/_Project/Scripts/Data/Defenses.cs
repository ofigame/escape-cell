using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>The player's own defences, set up before a level from the workshop's packs.</summary>
    public enum DefenseKind { Archer, Cannon, Tower }

    /// <summary>
    /// The player's defences: archers on the fortress's corner towers, cannons on its ramparts, towers on its floor.
    /// They come in packs of three from the workshop; each placed one is used up only when its level is won (a lost try
    /// keeps them where they stand), and one placed in a level is held there until it is won or taken back. Their
    /// strength is bought for good: level 1 from level 30 on (simple archers before that), level 2 from level 100,
    /// level 3 from level 200. Everything is kept on the phone (PlayerPrefs).
    /// </summary>
    public static class Defenses
    {
        public const int PackSize = 3;
        public static readonly DefenseKind[] All = { DefenseKind.Archer, DefenseKind.Cannon, DefenseKind.Tower };

        private const string StockKey = "sb_def_stock_", LevelKey = "sb_def_lv_", PlaceKey = "sb_def_place_", LevelsKey = "sb_def_levels", FreeKey = "sb_def_free";

        /// <summary>Level index (0-based) the kind first comes in the workshop: simple archers from the start, the rest at 30.</summary>
        public static int UnlockAt(DefenseKind k) => k == DefenseKind.Archer ? 0 : 29;
        /// <summary>Level index from which each strength step can be bought: 1 at 30, 2 at 100, 3 at 200.</summary>
        public static int StepFrom(int step) => step <= 1 ? 29 : step == 2 ? 99 : 199;
        public const int MaxStep = 3;

        public static int PackPrice(DefenseKind k) => k == DefenseKind.Archer ? 300 : k == DefenseKind.Cannon ? 900 : 1500;
        private static readonly int[,] StepPrices = { { 4000, 10000 }, { 6000, 15000 }, { 8000, 20000 } };

        /// <summary>The highest level the player has opened (the map's frontier).</summary>
        private static int Frontier => SaveData.UnlockedLevel;

        public static bool Unlocked(DefenseKind k) => Frontier >= UnlockAt(k);

        // ---------- Strength ----------

        /// <summary>The strength bought for good (1..3; step 1 comes free once the kind opens).</summary>
        public static int Step(DefenseKind k) => Mathf.Clamp(Prefs.GetInt(LevelKey + k, 1), 1, MaxStep);

        /// <summary>How strong it fights in a level: simple archers (0) before level 30, the bought step after.</summary>
        public static int Tier(DefenseKind k, int levelIndex) => levelIndex < 29 ? 0 : Step(k);

        public static bool IsMaxed(DefenseKind k) => Step(k) >= MaxStep;
        public static bool NextStepOpen(DefenseKind k) => !IsMaxed(k) && Frontier >= StepFrom(Step(k) + 1);
        public static int NextStepPrice(DefenseKind k) => IsMaxed(k) ? 0 : StepPrices[(int)k, Step(k) - 1];

        public static bool TryUpgrade(DefenseKind k)
        {
            if (!NextStepOpen(k) || !Shop.Spend(NextStepPrice(k))) return false;
            Prefs.SetInt(LevelKey + k, Step(k) + 1);
            Prefs.Save();
            return true;
        }

        // ---------- Stock ----------

        /// <summary>All of this kind the player has (placed ones included).</summary>
        public static int Stock(DefenseKind k) => Prefs.GetInt(StockKey + k, 0);
        /// <summary>Ready to place: the stock minus the ones waiting in levels.</summary>
        public static int Available(DefenseKind k) => Mathf.Max(0, Stock(k) - Reserved(k));

        public static bool TryBuyPack(DefenseKind k)
        {
            if (!Unlocked(k) || !Shop.Spend(PackPrice(k))) return false;
            AddStock(k, PackSize);
            return true;
        }

        public static void AddStock(DefenseKind k, int n)
        {
            Prefs.SetInt(StockKey + k, Mathf.Max(0, Stock(k) + n));
            Prefs.Save();
        }

        /// <summary>The first archers are a gift (once), so placing them is learned on level 1.</summary>
        public static bool GrantStarterOnce()
        {
            if (Prefs.GetInt(FreeKey, 0) == 1) return false;
            Prefs.SetInt(FreeKey, 1);
            AddStock(DefenseKind.Archer, PackSize);
            return true;
        }

        // ---------- Placements ----------

        /// <summary>One defence set up in a level: what, on which tile (a corner tower's corner tile, the floor tile a
        /// cannon's rampart runs along, or a tower's own tile) and, for a cannon, which way its rampart lies.</summary>
        public struct Placement
        {
            public DefenseKind kind;
            public int x, y, dx, dy;
            /// <summary>Already spent: the level was started with it standing (it stays for the retries, free).</summary>
            public bool paid;
            public Placement(DefenseKind kind, int x, int y, int dx = 0, int dy = 0) { this.kind = kind; this.x = x; this.y = y; this.dx = dx; this.dy = dy; paid = false; }
            public bool SameSpot(Placement o) => kind == o.kind && x == o.x && y == o.y && dx == o.dx && dy == o.dy;
        }

        public static List<Placement> Placed(int levelIndex)
        {
            var list = new List<Placement>();
            foreach (var part in Prefs.GetString(PlaceKey + levelIndex, "").Split(';'))
            {
                var f = part.Split(':');
                if (f.Length < 3 || !int.TryParse(f[0], out int k) || !int.TryParse(f[1], out int x) || !int.TryParse(f[2], out int y)) continue;
                int dx = f.Length > 3 && int.TryParse(f[3], out int a) ? a : 0, dy = f.Length > 4 && int.TryParse(f[4], out int b) ? b : 0;
                list.Add(new Placement((DefenseKind)Mathf.Clamp(k, 0, 2), x, y, dx, dy) { paid = f.Length > 5 && f[5] == "1" });
            }
            return list;
        }

        public static void Save(int levelIndex, List<Placement> list)
        {
            var parts = new List<string>();
            foreach (var p in list) parts.Add($"{(int)p.kind}:{p.x}:{p.y}:{p.dx}:{p.dy}:{(p.paid ? 1 : 0)}");
            Prefs.SetString(PlaceKey + levelIndex, string.Join(";", parts));
            var levels = Levels();
            if (list.Count > 0) levels.Add(levelIndex); else levels.Remove(levelIndex);
            Prefs.SetString(LevelsKey, string.Join(",", levels));
            Prefs.Save();
        }

        /// <summary>
        /// The level starts with these standing: each one not yet spent is spent now (placing and leaving without
        /// playing costs nothing; tries after this one are free until the level is won).
        /// </summary>
        public static void PayPlaced(int levelIndex)
        {
            var list = Placed(levelIndex);
            bool changed = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].paid) continue;
                var p = list[i];
                AddStock(p.kind, -1);
                p.paid = true;
                list[i] = p;
                changed = true;
            }
            if (changed) Save(levelIndex, list);
        }

        /// <summary>Taken back in set-up: one already spent comes back to the stock.</summary>
        public static void Refund(Placement p)
        {
            if (p.paid) AddStock(p.kind, 1);
        }

        /// <summary>The level was won: what stood in it is used up (anything not yet spent is spent too).</summary>
        public static void ConsumeOnWin(int levelIndex)
        {
            var list = Placed(levelIndex);
            if (list.Count == 0) return;
            foreach (var p in list) if (!p.paid) AddStock(p.kind, -1);
            Save(levelIndex, new List<Placement>());
        }

        /// <summary>How many of a kind wait in levels (all levels with placements).</summary>
        public static int Reserved(DefenseKind k)
        {
            int n = 0;
            foreach (int level in Levels())
                foreach (var p in Placed(level)) if (p.kind == k && !p.paid) n++; // spent ones are out of the stock already
            return n;
        }

        private static HashSet<int> Levels()
        {
            var set = new HashSet<int>();
            foreach (var s in Prefs.GetString(LevelsKey, "").Split(',')) if (int.TryParse(s, out int v)) set.Add(v);
            return set;
        }

        /// <summary>How many of each may stand in one fortress stage.</summary>
        public static int PerStage(DefenseKind k) => k == DefenseKind.Archer ? 4 : k == DefenseKind.Cannon ? 2 : 1;

        /// <summary>Extra robots a defence brings to its stage on the early floors (below level 30): three per archer.</summary>
        public static int ExtraEnemies(DefenseKind k, int levelIndex) => levelIndex < 29 ? 3 : 0;
    }
}
