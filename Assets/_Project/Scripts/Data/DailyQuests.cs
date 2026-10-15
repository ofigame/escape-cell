using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    public enum DailyGoal { Robots, Bugs, Towers, Wins, Bombs, Streak, Shields }

    /// <summary>
    /// Three quests a day (new ones at local midnight): beat so many robots, squash so many bugs, knock down towers,
    /// win floors, throw bombs, make a five-kill streak, break energy shields — only the ones the player has met so
    /// far. Each pays coins (more later in the campaign) once it is done and claimed from the menu.
    /// </summary>
    public static class DailyQuests
    {
        public const int Count = 3;
        private const string DayKey = "sb_dq_day";

        public struct Quest
        {
            public DailyGoal goal;
            public int target, progress, reward;
            public bool claimed;
            public bool Done => progress >= target;
        }

        private static string K(string what, int i) => "sb_dq_" + what + "_" + i;

        /// <summary>Today's quests (rolled on the first look of the day).</summary>
        public static Quest Get(int i)
        {
            Roll();
            return new Quest
            {
                goal = (DailyGoal)Prefs.GetInt(K("goal", i), 0),
                target = Prefs.GetInt(K("target", i), 1),
                progress = Prefs.GetInt(K("prog", i), 0),
                reward = Prefs.GetInt(K("reward", i), 100),
                claimed = Prefs.GetInt(K("claim", i), 0) == 1,
            };
        }

        /// <summary>Quests done and waiting to be claimed.</summary>
        public static int Claimable
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++) { var q = Get(i); if (q.Done && !q.claimed) n++; }
                return n;
            }
        }

        private static void Roll()
        {
            string today = GameClock.Today;
            if (Prefs.GetString(DayKey, "") == today) return;
            Prefs.SetString(DayKey, today);
            int reached = SaveData.UnlockedLevel;
            var pool = new List<DailyGoal> { DailyGoal.Robots, DailyGoal.Bugs, DailyGoal.Wins };
            if (reached >= 15) pool.Add(DailyGoal.Towers);
            if (reached >= 8) pool.Add(DailyGoal.Streak);
            if (Bombs.Owned) pool.Add(DailyGoal.Bombs);
            if (reached >= 100) pool.Add(DailyGoal.Shields);
            var rng = new System.Random(today.GetHashCode());
            // The robot quest is always one of the three: it is the heart of the game.
            var picks = new List<DailyGoal> { DailyGoal.Robots };
            pool.Remove(DailyGoal.Robots);
            while (picks.Count < Count && pool.Count > 0)
            {
                int k = rng.Next(pool.Count);
                picks.Add(pool[k]);
                pool.RemoveAt(k);
            }
            int baseReward = 100 + 4 * Mathf.Min(reached, 200);
            for (int i = 0; i < Count; i++)
            {
                var g = picks[i % picks.Count];
                int target = g switch
                {
                    DailyGoal.Robots => Mathf.Min(150, 40 + reached / 3),
                    DailyGoal.Bugs => Mathf.Min(80, 15 + reached / 6),
                    DailyGoal.Towers => Mathf.Min(20, 3 + reached / 30),
                    DailyGoal.Wins => 3,
                    DailyGoal.Bombs => 4,
                    DailyGoal.Streak => 1,
                    _ => 12,
                };
                float weight = g == DailyGoal.Robots || g == DailyGoal.Wins ? 1.5f : 1f;
                Prefs.SetInt(K("goal", i), (int)g);
                Prefs.SetInt(K("target", i), target);
                Prefs.SetInt(K("prog", i), 0);
                Prefs.SetInt(K("reward", i), Mathf.RoundToInt(baseReward * weight / 10f) * 10);
                Prefs.SetInt(K("claim", i), 0);
            }
            Prefs.Save();
        }

        /// <summary>Something counting towards today's quests happened; true when it finished one just now.</summary>
        public static bool Report(DailyGoal goal, int amount = 1)
        {
            bool finished = false, changed = false;
            for (int i = 0; i < Count; i++)
            {
                var q = Get(i);
                if (q.goal != goal || q.claimed || q.Done) continue;
                int p = Mathf.Min(q.target, q.progress + amount);
                Prefs.SetInt(K("prog", i), p);
                if (p >= q.target) finished = true;
                changed = true;
            }
            if (changed) Prefs.Save();
            return finished;
        }

        /// <summary>Takes a finished quest's coins.</summary>
        public static bool Claim(int i)
        {
            var q = Get(i);
            if (!q.Done || q.claimed) return false;
            Prefs.SetInt(K("claim", i), 1);
            SaveData.Coins += q.reward;
            Prefs.Save();
            return true;
        }
    }
}
