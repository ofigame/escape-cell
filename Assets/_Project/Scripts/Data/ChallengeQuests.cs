using System;
using System.Globalization;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>What a challenge quest counts.</summary>
    public enum DuelGoal { Win, Play, SoloWin, LiveWin, Special, Ultimate, Combo5, Finish, Perfect, DodgeUlti }

    /// <summary>
    /// The challenge's own quests: three a day and three a week (bigger targets, bigger rewards), drawn the same for the
    /// whole day or week from the duel's deeds — winning, playing, winning foi against foi or a live rival, specials,
    /// ultimates, five-blow combos, finishes. Kept on the phone; each pays its coins once.
    /// </summary>
    public static class ChallengeQuests
    {
        public const int Count = 3;

        public struct Quest
        {
            public DuelGoal goal;
            public int target, progress, coins;
            public bool claimed;
            public bool Done => progress >= target;
        }

        private static readonly (DuelGoal goal, int min, int max, int coins)[] Pool =
        {
            (DuelGoal.Win, 2, 3, 200), (DuelGoal.Play, 3, 4, 150), (DuelGoal.SoloWin, 1, 2, 200), (DuelGoal.LiveWin, 1, 1, 250),
            (DuelGoal.Special, 3, 5, 150), (DuelGoal.Ultimate, 1, 2, 200), (DuelGoal.Combo5, 1, 2, 200), (DuelGoal.Finish, 1, 2, 200),
            (DuelGoal.Perfect, 3, 3, 200), (DuelGoal.DodgeUlti, 1, 1, 250), // (the defence: three perfect guards, an ultimate dodged)
        };

        private static string Today => DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        private static string ThisWeek
        {
            get
            {
                var now = DateTime.Now;
                int week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                return now.Year + "w" + week;
            }
        }

        /// <summary>A hash that is the same on every phone and every run (the draws must not differ).</summary>
        private static int Hash(string s) { unchecked { int h = 23; foreach (char c in s) h = h * 31 + c; return h; } }

        private static string Key(bool weekly, int i, string what) => "sb_cq_" + (weekly ? "w" : "d") + i + "_" + what;

        /// <summary>The day's (or week's) quests start afresh when the day (or week) has turned.</summary>
        private static void Roll(bool weekly)
        {
            string stamp = weekly ? ThisWeek : Today, key = weekly ? "sb_cq_week" : "sb_cq_day";
            if (Prefs.GetString(key, "") == stamp) return;
            Prefs.SetString(key, stamp);
            for (int i = 0; i < Count; i++) { Prefs.SetInt(Key(weekly, i, "p"), 0); Prefs.SetInt(Key(weekly, i, "c"), 0); }
            Prefs.Save();
        }

        public static Quest Get(bool weekly, int i)
        {
            Roll(weekly);
            string stamp = weekly ? ThisWeek : Today;
            // Three different goals, the same for everyone on the same day (or week).
            var rng = new System.Random(Hash(stamp) ^ (weekly ? 0x5f3759df : 0x2545f491));
            var picks = new int[Count];
            for (int k = 0; k < Count; k++)
            {
                int p;
                do p = rng.Next(Pool.Length); while (Array.IndexOf(picks, p, 0, k) >= 0);
                picks[k] = p;
            }
            var def = Pool[picks[Mathf.Clamp(i, 0, Count - 1)]];
            int target = def.min + new System.Random(Hash(stamp) + i).Next(def.max - def.min + 1);
            if (weekly) target *= 4;
            return new Quest
            {
                goal = def.goal,
                target = target,
                coins = weekly ? def.coins * 5 : def.coins,
                progress = Mathf.Min(target, Prefs.GetInt(Key(weekly, i, "p"), 0)),
                claimed = Prefs.GetInt(Key(weekly, i, "c"), 0) == 1,
            };
        }

        /// <summary>Something counting towards the quests happened (in the day's and the week's alike).</summary>
        public static void Report(DuelGoal goal, int amount = 1)
        {
            foreach (bool weekly in new[] { false, true })
                for (int i = 0; i < Count; i++)
                {
                    var q = Get(weekly, i);
                    if (q.goal != goal || q.claimed || q.Done) continue;
                    Prefs.SetInt(Key(weekly, i, "p"), Mathf.Min(q.target, q.progress + amount));
                }
        }

        /// <summary>Takes a finished quest's coins (0 if not finished or already taken).</summary>
        public static int Claim(bool weekly, int i)
        {
            var q = Get(weekly, i);
            if (!q.Done || q.claimed) return 0;
            Prefs.SetInt(Key(weekly, i, "c"), 1);
            SaveData.Coins += q.coins;
            ItemDrops.FromQuest(weekly, q.goal); // (a rare item a day, an epic a week)
            Prefs.Save();
            return q.coins;
        }

        /// <summary>Quests finished and not yet taken (for a badge).</summary>
        public static int Waiting
        {
            get
            {
                int n = 0;
                foreach (bool weekly in new[] { false, true })
                    for (int i = 0; i < Count; i++) { var q = Get(weekly, i); if (q.Done && !q.claimed) n++; }
                return n;
            }
        }

        public static string Text(Quest q) => Loc.F("cq." + q.goal, q.target);
    }
}
