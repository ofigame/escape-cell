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

        public static int BestScore(int level) => Prefs.GetInt(ScoreKey + level, 0);

        /// <summary>Keeps a floor's score if it beats the best; returns the points it added to the world's total.</summary>
        public static int RecordScore(int level, int score)
        {
            score = Mathf.Clamp(score, 0, 100);
            int best = BestScore(level);
            if (score <= best) return 0;
            Prefs.SetInt(ScoreKey + level, score);
            Prefs.Save();
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

        // ---------- Groups: power, hit and armour ----------

        /// <summary>The three things a group's development points go into.</summary>
        public enum Track { Power, Hit, Armor }

        /// <summary>
        /// Each kind's soldier (the user's table): power (health points) from → to, its blow from → to, the share of a
        /// blow its armour takes off from → to. A development point adds 1 power, 0.1 to the blow or half a per cent of armour.
        /// </summary>
        private static readonly float[,] Table =
        {
            // power, max, hit, max, armour, max
            { 20f, 110f, 2f, 8f, 0.15f, 0.40f },   // light swords
            { 25f, 140f, 3f, 10f, 0.15f, 0.40f },  // heavy swords
            { 18f, 100f, 1.5f, 7f, 0.15f, 0.40f }, // light spears
            { 22f, 130f, 2.5f, 9f, 0.15f, 0.40f }, // heavy spears
            { 15f, 90f, 1f, 6f, 0.10f, 0.30f },    // light bows
            { 20f, 120f, 2f, 8f, 0.10f, 0.30f },   // heavy bows
        };

        private static readonly float[] PerPointOf = { 1f, 0.1f, 0.005f };
        private const string PowerKey = "sb_army_p_", HitKey = "sb_army_h_", ArmorKey = "sb_army_a_", VersionKey = "sb_army_v3";

        private static string KeyOf(Track t) => t == Track.Power ? PowerKey : t == Track.Hit ? HitKey : ArmorKey;

        /// <summary>The most points a group's track takes (from its start to its top).</summary>
        public static int MaxPoints(int group, Track t)
        {
            int c = (int)t * 2;
            return Mathf.RoundToInt((Table[group, c + 1] - Table[group, c]) / PerPointOf[(int)t]);
        }

        /// <summary>Points in a group's track (never more than it takes).</summary>
        public static int Points(int group, Track t)
        {
            Migrate();
            return Mathf.Clamp(Prefs.GetInt(KeyOf(t) + group, 0), 0, MaxPoints(group, t));
        }

        /// <summary>
        /// Points spent before the three tracks: the old single points shared out three ways, the old armour (health)
        /// points into power and the old attack points into the blow.
        /// </summary>
        private static bool migrated;
        private static void Migrate()
        {
            if (migrated) return;
            migrated = true;
            if (Prefs.GetInt(VersionKey, 0) == 3) return;
            for (int g = 0; g < Groups; g++)
            {
                int old = Prefs.GetInt(GroupKey + g, 0);
                int oldArmor = Prefs.GetInt(ArmorKey + g, 0), oldAttack = Prefs.GetInt(HitKey + g, 0);
                Prefs.DeleteKey(GroupKey + g);
                Prefs.SetInt(PowerKey + g, oldArmor + old / 3);
                Prefs.SetInt(HitKey + g, oldAttack + old / 3);
                Prefs.SetInt(ArmorKey + g, old - 2 * (old / 3));
            }
            Prefs.SetInt(VersionKey, 3);
            Prefs.Save();
        }

        /// <summary>All the points in a group.</summary>
        public static int GroupPoints(int group) => Points(group, Track.Power) + Points(group, Track.Hit) + Points(group, Track.Armor);

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

        /// <summary>Puts up to <paramref name="amount"/> free points into a group's track (never past its top).</summary>
        public static bool Spend(int group, int amount, Track t)
        {
            int have = Points(group, t);
            amount = Mathf.Min(amount, Mathf.Min(FreePoints, MaxPoints(group, t) - have));
            if (amount <= 0) return false;
            Prefs.SetInt(KeyOf(t) + group, have + amount);
            Prefs.Save();
            return true;
        }

        /// <summary>Takes up to <paramref name="amount"/> points back off a group's track (free to spend again).</summary>
        public static bool Take(int group, int amount, Track t)
        {
            int have = Points(group, t);
            if (have <= 0) return false;
            Prefs.SetInt(KeyOf(t) + group, Mathf.Max(0, have - amount));
            Prefs.Save();
            return true;
        }

        public static int ResetPrice => 500 + 500 * Prefs.GetInt(ResetCountKey, 0);

        /// <summary>Takes every spent point back for gold.</summary>
        public static bool TryReset()
        {
            if (SpentPoints <= 0 || !Shop.Spend(ResetPrice)) return false;
            for (int g = 0; g < Groups; g++) foreach (var k in new[] { GroupKey, PowerKey, HitKey, ArmorKey }) Prefs.DeleteKey(k + g);
            Prefs.SetInt(ResetCountKey, Prefs.GetInt(ResetCountKey, 0) + 1);
            Prefs.Save();
            return true;
        }

        /// <summary>A soldier's strengths: health points, blow (health points off a foe) and armour (share taken off a blow).</summary>
        public struct Stats
        {
            public int hp;
            public float hit, armor;
        }

        /// <summary>A soldier of a group with these points in its three tracks.</summary>
        public static Stats StatsFor(int group, int power, int hit, int armor)
        {
            group = Mathf.Clamp(group, 0, Groups - 1);
            return new Stats
            {
                hp = Mathf.RoundToInt(Mathf.Min(Table[group, 1], Table[group, 0] + Mathf.Max(0, power) * PerPointOf[0])),
                hit = Mathf.Min(Table[group, 3], Table[group, 2] + Mathf.Max(0, hit) * PerPointOf[1]),
                armor = Mathf.Min(Table[group, 5], Table[group, 4] + Mathf.Max(0, armor) * PerPointOf[2]),
            };
        }

        /// <summary>The player's soldier of a group.</summary>
        public static Stats StatsOf(int group) => StatsFor(group, Points(group, Track.Power), Points(group, Track.Hit), Points(group, Track.Armor));

        /// <summary>A rival's soldier from its group's points (shared out over the three tracks, a third each).</summary>
        public static Stats StatsFromTotal(int group, int total) => StatsFor(group, total / 3, total / 3, total - 2 * (total / 3));

        /// <summary>A group's strength as a multiplier of its plain soldier's power (for the rival cards and matching).</summary>
        public static float Power(int group) => StatsOf(group).hp / Table[Mathf.Clamp(group, 0, Groups - 1), 0];

        /// <summary>How far a group's soldiers have grown (0 untrained … 1 every track full): the three tracks' shares, averaged.
        /// Their models grow with it.</summary>
        public static float Growth(int group, Stats s)
        {
            int g = Mathf.Clamp(group, 0, Groups - 1);
            float Share(float v, int c) => Mathf.Clamp01((v - Table[g, c]) / Mathf.Max(0.0001f, Table[g, c + 1] - Table[g, c]));
            return (Share(s.hp, 0) + Share(s.hit, 2) + Share(s.armor, 4)) / 3f;
        }

        /// <summary>A soldier's model size for its growth: up to half as big again when fully trained.</summary>
        public static float SizeFor(int group, Stats s) => 1f + 0.5f * Growth(group, s);

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
