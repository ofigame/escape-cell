using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>The six kinds of soldier group: three families (sword, spear, bow), each light (quick, frail) or heavy (slow, armoured).</summary>
    public enum GroupKind { LightSword, HeavySword, LightSpear, HeavySpear, LightBow, HeavyBow }

    /// <summary>
    /// foi's army for the challenges: six fixed groups of ten soldiers, one of each kind. Every floor won scores 0-100
    /// (its best score is kept); a world finished turns the sum of its ten floors' best scores into development points
    /// (sum / 10, up to 100 a world), which the player spreads over the six groups as they like — all on one, or across
    /// them. Replaying a floor for a better score adds the difference. Spread points can be taken back for gold.
    /// </summary>
    public static class Army
    {
        public const int Groups = 6;
        public const int Soldiers = 30;
        /// <summary>Power each development point adds to a group (health and attack).</summary>
        public const float PerPoint = 0.02f;
        /// <summary>A new look for a group every this many points.</summary>
        public const int TierStep = 25;

        private const string ScoreKey = "sb_lvscore_", GroupKey = "sb_army_", ResetCountKey = "sb_army_resets";

        public static GroupKind KindOf(int group) => (GroupKind)Mathf.Clamp(group, 0, Groups - 1);

        /// <summary>The family a kind belongs to: 0 sword, 1 spear, 2 bow.</summary>
        public static int Family(GroupKind k) => (int)k / 2;
        public static bool Heavy(GroupKind k) => (int)k % 2 == 1;

        /// <summary>Sword beats bow, spear beats sword, bow beats spear: +1 the attacker has the edge, -1 the defender, 0 even.</summary>
        public static int Edge(GroupKind a, GroupKind b)
        {
            int fa = Family(a), fb = Family(b);
            if (fa == fb) return 0;
            return (fa == 0 && fb == 2) || (fa == 1 && fb == 0) || (fa == 2 && fb == 1) ? 1 : -1;
        }

        // ---------- Floor scores ----------

        public static int BestScore(int level) => PlayerPrefs.GetInt(ScoreKey + level, 0);

        /// <summary>Keeps a floor's score if it beats the best; returns the points it added to the world's total.</summary>
        public static int RecordScore(int level, int score)
        {
            score = Mathf.Clamp(score, 0, 100);
            int best = BestScore(level);
            if (score <= best) return 0;
            PlayerPrefs.SetInt(ScoreKey + level, score);
            PlayerPrefs.Save();
            return score - best;
        }

        /// <summary>A world's floor scores summed (0-1000).</summary>
        public static int WorldScore(int world)
        {
            int sum = 0;
            for (int i = 0; i < LevelCatalog.LevelsPerWorld; i++) sum += BestScore(world * LevelCatalog.LevelsPerWorld + i);
            return sum;
        }

        /// <summary>Is the world finished (its tenth floor won)?</summary>
        public static bool WorldDone(int world) => SaveData.UnlockedLevel >= (world + 1) * LevelCatalog.LevelsPerWorld
                                                   || (world == LevelCatalog.WorldCount - 1 && BestScore(LevelCatalog.LevelCount - 1) > 0);

        /// <summary>Development points a world gives (only once finished): its score / 10, up to 100.</summary>
        public static int WorldPoints(int world) => WorldDone(world) ? Mathf.Min(100, WorldScore(world) / 10) : 0;

        /// <summary>All development points earned so far.</summary>
        public static int EarnedPoints
        {
            get
            {
                int sum = 0;
                for (int w = 0; w < LevelCatalog.WorldCount; w++) sum += WorldPoints(w);
                return sum;
            }
        }

        // ---------- Groups: armour and attack ----------

        /// <summary>
        /// A group's points: armour (health, and blows it takes off) and attack (its blows), spent separately. Points
        /// spent before the split are shared out once, half each.
        /// </summary>
        public static int ArmorPoints(int group) { Migrate(group); return PlayerPrefs.GetInt(ArmorKey + group, 0); }
        public static int AttackPoints(int group) { Migrate(group); return PlayerPrefs.GetInt(AttackKey + group, 0); }

        private const string ArmorKey = "sb_army_a_", AttackKey = "sb_army_h_";

        private static void Migrate(int group)
        {
            if (!PlayerPrefs.HasKey(GroupKey + group)) return;
            int old = PlayerPrefs.GetInt(GroupKey + group, 0);
            PlayerPrefs.DeleteKey(GroupKey + group);
            PlayerPrefs.SetInt(ArmorKey + group, PlayerPrefs.GetInt(ArmorKey + group, 0) + (old + 1) / 2);
            PlayerPrefs.SetInt(AttackKey + group, PlayerPrefs.GetInt(AttackKey + group, 0) + old / 2);
            PlayerPrefs.Save();
        }

        /// <summary>All the points in a group (armour and attack).</summary>
        public static int GroupPoints(int group) => ArmorPoints(group) + AttackPoints(group);

        public static int SpentPoints
        {
            get
            {
                int sum = 0;
                for (int g = 0; g < Groups; g++) sum += GroupPoints(g);
                return sum;
            }
        }

        public static int FreePoints => Mathf.Max(0, EarnedPoints - SpentPoints);

        /// <summary>Puts <paramref name="amount"/> free points into a group's armour (or attack).</summary>
        public static bool Spend(int group, int amount, bool armor)
        {
            amount = Mathf.Min(amount, FreePoints);
            if (amount <= 0) return false;
            string key = (armor ? ArmorKey : AttackKey) + group;
            Migrate(group);
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + amount);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Takes up to <paramref name="amount"/> points back off a group's armour (or attack): free to spend again.</summary>
        public static bool Take(int group, int amount, bool armor)
        {
            string key = (armor ? ArmorKey : AttackKey) + group;
            Migrate(group);
            int have = PlayerPrefs.GetInt(key, 0);
            if (have <= 0) return false;
            PlayerPrefs.SetInt(key, Mathf.Max(0, have - amount));
            PlayerPrefs.Save();
            return true;
        }

        public static int ResetPrice => 500 + 500 * PlayerPrefs.GetInt(ResetCountKey, 0);

        /// <summary>Takes every spent point back for gold.</summary>
        public static bool TryReset()
        {
            if (SpentPoints <= 0 || !Shop.Spend(ResetPrice)) return false;
            for (int g = 0; g < Groups; g++) { PlayerPrefs.DeleteKey(GroupKey + g); PlayerPrefs.DeleteKey(ArmorKey + g); PlayerPrefs.DeleteKey(AttackKey + g); }
            PlayerPrefs.SetInt(ResetCountKey, PlayerPrefs.GetInt(ResetCountKey, 0) + 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Strength each armour / attack point adds to its side of a group.</summary>
        public const float PerSidePoint = 0.03f;

        /// <summary>A group's toughness (health) and its blows' strength: their points, plus the army's rank strength.</summary>
        public static float ArmorPower(int group) => 1f + ArmorPoints(group) * PerSidePoint + Challenge.ArmyBonus;
        public static float AttackPower(int group) => 1f + AttackPoints(group) * PerSidePoint + Challenge.ArmyBonus;

        /// <summary>A group's overall strength (the average of the two; for matching and the rival's cards).</summary>
        public static float Power(int group) => (ArmorPower(group) + AttackPower(group)) * 0.5f;

        /// <summary>The look tier of a group (0 plain, one more every 25 points).</summary>
        public static int Tier(int group) => GroupPoints(group) / TierStep;

        /// <summary>The whole army's power, for matching: sixty soldiers at their groups' strength.</summary>
        public static int PowerScore
        {
            get
            {
                float sum = 0f;
                for (int g = 0; g < Groups; g++) sum += Soldiers * Power(g);
                return Mathf.RoundToInt(sum * 10f);
            }
        }
    }
}
