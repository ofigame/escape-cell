using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Stars per level and the bonus meter they fill. Every <see cref="StarsPerBonus"/> newly earned stars
    /// (beating a level's best counts, replaying for the same stars does not) unlock a bonus round.
    /// </summary>
    public static class Progress
    {
        public const int StarsPerBonus = 8;

        private const string StarsKey = "sb_stars_";
        private const string MeterKey = "sb_bonus_meter";
        private const string TokensKey = "sb_bonus_tokens";

        public static int Stars(int level) => Prefs.GetInt(StarsKey + level, 0);

        private const string StarsV2Key = "sb_stars_v2", StarsNoteKey = "sb_stars_note";

        /// <summary>
        /// Once (and again after an older cloud copy comes back without the mark): the stars move to the floor score — a
        /// floor with a score kept gets its score's stars; an old three counts as two otherwise (the third is won again
        /// with a score of the top band). Never fewer than one for a floor won. A note is shown once when anything changed.
        /// </summary>
        public static void MigrateStarsToScore(int levelCount)
        {
            if (Prefs.GetInt(StarsV2Key, 0) == 1) return;
            var t = FloorTuning.Current;
            bool changed = false;
            for (int l = 0; l < levelCount; l++)
            {
                int old = Stars(l);
                if (old <= 0) continue;
                int score = BestScore(l);
                int fromScore = score > 0 ? (score >= t.starThree ? 3 : score >= t.starTwo ? 2 : 1) : 0;
                int now = Mathf.Max(1, Mathf.Max(fromScore, Mathf.Min(old, 2)));
                if (now == old) continue;
                Prefs.SetInt(StarsKey + l, now);
                changed = true;
            }
            Prefs.SetInt(StarsV2Key, 1);
            if (changed) Prefs.SetInt(StarsNoteKey, 1);
            Prefs.Save();
        }

        /// <summary>The move to score stars changed something and the player has not been told yet (true once).</summary>
        public static bool TakeStarsNote()
        {
            if (Prefs.GetInt(StarsNoteKey, 0) != 1) return false;
            Prefs.SetInt(StarsNoteKey, 0);
            Prefs.Save();
            return true;
        }

        private const string ScoreKey = "sb_lvscore_"; // (the floors' best scores: kept where the army kept them)

        /// <summary>A floor's best score (0..100; 0: none yet).</summary>
        public static int BestScore(int level) => Prefs.GetInt(ScoreKey + level, 0);

        /// <summary>Keeps a floor's score if it beats the best; returns how much it raised the best.</summary>
        public static int RecordScore(int level, int score)
        {
            score = Mathf.Clamp(score, 0, 100);
            int best = BestScore(level);
            if (score <= best) return 0;
            Prefs.SetInt(ScoreKey + level, score);
            Prefs.Save();
            return score - best;
        }

        public static int TotalStars(int levelCount)
        {
            int total = 0;
            for (int i = 0; i < levelCount; i++) total += Stars(i);
            return total;
        }

        /// <summary>Stars banked toward the next bonus round (0..StarsPerBonus-1).</summary>
        public static int Meter => Prefs.GetInt(MeterKey, 0);

        /// <summary>Bonus rounds waiting to be played.</summary>
        public static int BonusTokens
        {
            get => 0; // bonus rounds after levels are gone (user decision); the chest meter rewards instead
            set { }
        }

        /// <summary>Records a result. Returns how many bonus rounds it unlocked (usually 0 or 1).</summary>
        public static int Award(int level, int stars, out int newStars)
        {
            int best = Stars(level);
            newStars = Mathf.Max(0, stars - best);
            if (newStars == 0) return 0;

            Prefs.SetInt(StarsKey + level, stars);
            Prefs.Save();
            return 0;
        }
    }

    /// <summary>
    /// What it takes to earn the 2nd and 3rd star. Mostly coins (performance), except in coin-collecting levels,
    /// where every win has the same coins and speed counts instead.
    /// </summary>
    public static class StarRules
    {
        public struct Goals
        {
            /// <summary>True: the goals are seconds to beat; false: coins to collect.</summary>
            public bool timed;
            public float two, three;
        }

        public static Goals For(LevelData level, int floorCount)
        {
            switch (level.mission)
            {
                case MissionType.CollectCoins:
                {
                    // A new coin can only appear every coinInterval seconds, so par times follow from that.
                    float best = 0.6f + level.coinTarget * level.coinInterval;
                    return new Goals { timed = true, two = Mathf.Round(best * 1.45f), three = Mathf.Round(best * 1.15f) };
                }
                case MissionType.CoinRain:
                    return Coins(Mathf.Round(level.coinTarget * 1.4f), Mathf.Round(level.coinTarget * 1.8f));
                case MissionType.Survive:
                {
                    float possible = level.surviveSeconds / Mathf.Max(0.5f, level.coinInterval);
                    return Coins(Mathf.Max(2f, Mathf.Round(possible * 0.35f)), Mathf.Max(3f, Mathf.Round(possible * 0.6f)));
                }
                case MissionType.Exit:
                case MissionType.Quest:
                    return Coins(1 + level.keys, 2 + level.keys * 2);
                case MissionType.Boss:
                case MissionType.Monster:
                    return Coins(2, 5);
                case MissionType.Paint:
                    return Coins(Mathf.Ceil(floorCount / 8f), Mathf.Ceil(floorCount / 4.5f));
                default:
                    return Coins(1, 1);
            }

            Goals Coins(float two, float three) => new Goals { timed = false, two = two, three = Mathf.Max(two + 1f, three) };
        }

        public static int Evaluate(Goals goals, int coins, float seconds)
        {
            float value = goals.timed ? -seconds : coins;
            float two = goals.timed ? -goals.two : goals.two;
            float three = goals.timed ? -goals.three : goals.three;
            return value >= three ? 3 : value >= two ? 2 : 1;
        }
    }
}
