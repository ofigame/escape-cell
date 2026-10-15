using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The soldiers foi brings onto a floor: how many of each group (up to five a group and the world's cap in all, from what the pool has at home) and the
    /// stage each group joins foi in. The plan is kept for the next floors (cut to what is at home each time); a soldier
    /// who falls on a floor is gone from the pool for an hour, like in the duels.
    /// </summary>
    public static class LevelSquad
    {
        public const int MaxPerGroup = 5;
        private const string Key = "sb_lsquad";

        private static readonly int[] count = new int[Army.Groups];
        private static readonly int[] stage = new int[Army.Groups];
        private static bool loaded;

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            var p = PlayerPrefs.GetString(Key, "").Split(',');
            for (int g = 0; g < Army.Groups; g++)
            {
                if (p.Length > g * 2 + 1 && int.TryParse(p[g * 2], out int c) && int.TryParse(p[g * 2 + 1], out int s)) { count[g] = c; stage[g] = s; }
            }
        }

        private static void Save()
        {
            var parts = new string[Army.Groups * 2];
            for (int g = 0; g < Army.Groups; g++) { parts[g * 2] = count[g].ToString(); parts[g * 2 + 1] = stage[g].ToString(); }
            PlayerPrefs.SetString(Key, string.Join(",", parts));
            PlayerPrefs.Save();
        }

        /// <summary>How many soldiers foi may bring in a world (0-based): four in the first, two more in each next one.</summary>
        public static int LimitFor(int world) => 4 + 2 * Mathf.Max(0, world);

        /// <summary>The cap for the floor at hand (set by the game from its world before the card and the floor).</summary>
        public static int Limit = 4;

        /// <summary>Soldiers of a group brought along (never more than are at home, and the groups before it take the
        /// world's cap first).</summary>
        public static int Count(int group)
        {
            Load();
            int left = Limit;
            for (int g = 0; g < group; g++) left -= Raw(g);
            return Mathf.Clamp(Raw(group), 0, Mathf.Max(0, left));
        }

        private static int Raw(int group) => Mathf.Clamp(count[group], 0, Mathf.Min(MaxPerGroup, SoldierPool.Available(group)));

        /// <summary>How many more fit under the cap.</summary>
        public static int Room => Mathf.Max(0, Limit - Total);

        /// <summary>The stage (0-based) a group joins foi in, on a floor of <paramref name="stages"/> stages.</summary>
        public static int Stage(int group, int stages) { Load(); return Mathf.Clamp(stage[group], 0, Mathf.Max(0, stages - 1)); }

        public static void SetCount(int group, int value) { Load(); count[group] = Mathf.Clamp(value, 0, MaxPerGroup); Save(); }
        public static void SetStage(int group, int value) { Load(); stage[group] = Mathf.Max(0, value); Save(); }

        public static int Total
        {
            get { int n = 0; for (int g = 0; g < Army.Groups; g++) n += Count(g); return n; }
        }
    }
}
