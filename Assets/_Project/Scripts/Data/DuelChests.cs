using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>The three duel chests: a plain one (green strap), a middle one (bronze, yellow strap), a great one (gold, red strap).</summary>
    public enum ChestTier { Basic, Mid, Advanced }

    /// <summary>What came out of a chest: gold, or a weapon (a shop one, or one only chests hold).</summary>
    public class ChestReward
    {
        public int gold;
        public WeaponDef weapon;
    }

    /// <summary>
    /// Duel chests: every duel won brings one, and the better the play the better the chest — a winning streak and
    /// health to spare. Two may be opened free each day; after that an ad opens one. Inside: gold, a weapon from the
    /// shop, or (in the better chests, for winning streaks) a weapon no shop sells.
    /// </summary>
    public static class DuelChests
    {
        public const int Tiers = 3;
        public const int FreePerDay = 2;
        private const int Cap = 30;

        public static int Count(ChestTier t) => Prefs.GetInt("sb_dchest_" + (int)t, 0);

        public static int Total => Count(ChestTier.Basic) + Count(ChestTier.Mid) + Count(ChestTier.Advanced);

        private static void SetCount(ChestTier t, int n) => Prefs.SetInt("sb_dchest_" + (int)t, Mathf.Clamp(n, 0, Cap));

        /// <summary>Duels won in a row (a loss ends the streak; a draw keeps it).</summary>
        public static int Streak => Prefs.GetInt("sb_dchest_streak", 0);

        /// <summary>
        /// A duel ended: a win brings a chest (returned), its kind by the streak and the health left; a practice duel
        /// (no rival online) only the plain one.
        /// </summary>
        public static ChestTier? OnDuelEnd(bool won, bool draw, bool ranked, float healthLeft)
        {
            if (!won)
            {
                if (!draw) Prefs.SetInt("sb_dchest_streak", 0);
                Prefs.Save();
                return null;
            }
            int streak = Streak + 1;
            Prefs.SetInt("sb_dchest_streak", streak);
            var tier = (streak >= 5 && healthLeft >= 0.5f) || streak >= 8 ? ChestTier.Advanced
                : streak >= 3 || healthLeft >= 0.7f ? ChestTier.Mid
                : ChestTier.Basic;
            if (!ranked) tier = ChestTier.Basic;
            SetCount(tier, Count(tier) + 1);
            Prefs.Save();
            return tier;
        }

        private static string Today => DateTime.Now.ToString("yyyyMMdd");

        /// <summary>Free openings left today.</summary>
        public static int FreeLeft => Prefs.GetString("sb_dchest_day", "") == Today ? Mathf.Max(0, FreePerDay - Prefs.GetInt("sb_dchest_used", 0)) : FreePerDay;

        public static void UseFree()
        {
            if (Prefs.GetString("sb_dchest_day", "") != Today)
            {
                Prefs.SetString("sb_dchest_day", Today);
                Prefs.SetInt("sb_dchest_used", 0);
            }
            Prefs.SetInt("sb_dchest_used", Prefs.GetInt("sb_dchest_used", 0) + 1);
            Prefs.Save();
        }

        /// <summary>Opens one chest of the kind: takes it, draws what is inside and gives it to the player.</summary>
        public static ChestReward Open(ChestTier t)
        {
            if (Count(t) <= 0) return null;
            SetCount(t, Count(t) - 1);
            var r = Roll(t);
            if (r.weapon != null) Armory.Grant(r.weapon);
            else SaveData.Coins += r.gold;
            Prefs.Save();
            return r;
        }

        private static ChestReward Roll(ChestTier t)
        {
            float roll = UnityEngine.Random.value;
            bool wantChest = t == ChestTier.Advanced ? roll < 0.32f : t == ChestTier.Mid && Streak >= 3 && roll < 0.08f;
            bool wantShop = !wantChest && roll < (t == ChestTier.Advanced ? 0.62f : t == ChestTier.Mid ? 0.4f : 0.25f);
            WeaponDef w = null;
            if (wantChest) w = Pick(Armory.All.FindAll(x => x.chest && !Armory.Owned(x)));
            if (w == null && (wantChest || wantShop))
            {
                int lo = t == ChestTier.Advanced ? 4 : t == ChestTier.Mid ? 3 : 1;
                int hi = t == ChestTier.Advanced ? 6 : t == ChestTier.Mid ? 4 : 2;
                w = Pick(Armory.All.FindAll(x => !x.Special && !x.chest && x.price > 0 && !Armory.Owned(x) && x.tier >= lo && x.tier <= hi));
            }
            if (w != null) return new ChestReward { weapon = w };
            // Gold (more of it when a weapon was due but none was left to give).
            var range = t == ChestTier.Advanced ? (500, 1100) : t == ChestTier.Mid ? (200, 450) : (60, 160);
            float k = wantChest || wantShop ? 1.5f : 1f;
            int gold = Mathf.RoundToInt(UnityEngine.Random.Range(range.Item1, range.Item2) * k / 10f) * 10;
            return new ChestReward { gold = gold };
        }

        private static WeaponDef Pick(List<WeaponDef> list) => list.Count == 0 ? null : list[UnityEngine.Random.Range(0, list.Count)];
    }
}
