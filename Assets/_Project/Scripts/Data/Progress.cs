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

        public static int Stars(int level) => PlayerPrefs.GetInt(StarsKey + level, 0);

        public static int TotalStars(int levelCount)
        {
            int total = 0;
            for (int i = 0; i < levelCount; i++) total += Stars(i);
            return total;
        }

        /// <summary>Stars banked toward the next bonus round (0..StarsPerBonus-1).</summary>
        public static int Meter => PlayerPrefs.GetInt(MeterKey, 0);

        /// <summary>Bonus rounds waiting to be played.</summary>
        public static int BonusTokens
        {
            get => PlayerPrefs.GetInt(TokensKey, 0);
            set { PlayerPrefs.SetInt(TokensKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>Records a result. Returns how many bonus rounds it unlocked (usually 0 or 1).</summary>
        public static int Award(int level, int stars, out int newStars)
        {
            int best = Stars(level);
            newStars = Mathf.Max(0, stars - best);
            if (newStars == 0) return 0;

            PlayerPrefs.SetInt(StarsKey + level, stars);
            int meter = Meter + newStars;
            int unlocked = meter / StarsPerBonus;
            PlayerPrefs.SetInt(MeterKey, meter % StarsPerBonus);
            PlayerPrefs.SetInt(TokensKey, BonusTokens + unlocked);
            PlayerPrefs.Save();
            return unlocked;
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
