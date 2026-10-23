using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>The three duel chests: a plain one (green strap), a middle one (bronze, yellow strap), a great one (gold, red strap).</summary>
    public enum ChestTier { Basic, Mid, Advanced }

    /// <summary>What came out of a chest: gold, a weapon (a shop one, or one only chests hold) or an outfit for foi.</summary>
    public class ChestReward
    {
        public int gold;
        public WeaponDef weapon;
        public Cosmetic outfit;
        public ShieldDef shield;
    }

    /// <summary>
    /// Duel chests: every duel won brings one, and the better the play the better the chest — a winning streak and
    /// health to spare. Each opens free; what came out can be swapped once for another draw after an ad. Inside: gold, a weapon from the
    /// shop, or (in the better chests, for winning streaks) a weapon no shop sells; now and then a surprise outfit for foi
    /// (one only chests hold, or one from the workshop not yet owned).
    /// </summary>
    public static class DuelChests
    {
        public const int Tiers = 3;
        public const int FreePerDay = 0; // (no daily count: every chest opens free; an ad only swaps what came out)
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
            var tier = (streak >= 7 && healthLeft >= 0.6f) || streak >= 12 ? ChestTier.Advanced
                : streak >= 3 || healthLeft >= 0.8f ? ChestTier.Mid
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

        /// <summary>Opens one chest of the kind: takes it and draws what is inside (not given yet: <see cref="Give"/>).</summary>
        public static ChestReward Open(ChestTier t)
        {
            if (Count(t) <= 0) return null;
            SetCount(t, Count(t) - 1);
            var r = Roll(t);
            Hold(r);
            return r;
        }

        /// <summary>A second draw from the same chest (after an ad), never the same thing again.</summary>
        public static ChestReward Redraw(ChestTier t, ChestReward was)
        {
            ChestReward r = null;
            for (int i = 0; i < 8; i++)
            {
                r = Roll(t);
                bool same = was != null && (r.weapon != null ? r.weapon == was.weapon : r.outfit != null ? r.outfit == was.outfit : r.shield != null ? r.shield == was.shield : was.weapon == null && was.outfit == null && was.shield == null);
                if (!same) break;
            }
            Hold(r);
            return r;
        }

        // What came out and is not given yet is kept on the phone: if the game closes while it is shown (or while an ad
        // swaps it), it is given the next time (the chest was already spent).
        private const string PendingKey = "sb_dchest_pending";

        private static void Hold(ChestReward r)
        {
            Prefs.SetString(PendingKey, r == null ? "" : r.weapon != null ? "w:" + r.weapon.id : r.shield != null ? "s:" + r.shield.id : r.outfit != null ? "o:" + r.outfit.id : "g:" + r.gold);
            Prefs.Save();
        }

        /// <summary>A gift left from a chest opened when the game closed: given now (true if there was one).</summary>
        public static bool GivePending()
        {
            string p = Prefs.GetString(PendingKey, "");
            if (p.Length < 3) return false;
            string v = p.Substring(2);
            var r = new ChestReward();
            if (p[0] == 'w') r.weapon = Armory.Get(v);
            else if (p[0] == 's') r.shield = Shields.Get(v);
            else if (p[0] == 'o') r.outfit = Cosmetics.Find(v);
            else int.TryParse(v, out r.gold);
            Give(r);
            return true;
        }

        /// <summary>Gives what came out to the player.</summary>
        public static void Give(ChestReward r)
        {
            Prefs.SetString(PendingKey, "");
            if (r == null) { Prefs.Save(); return; }
            if (r.weapon != null) Armory.Grant(r.weapon);
            else if (r.shield != null) Shields.Grant(r.shield);
            else if (r.outfit != null) Cosmetics.Give(r.outfit.id);
            else SaveData.Coins += r.gold;
            Prefs.Save();
        }

        /// <summary>
        /// The golden chest: an outfit 34%, a shield 4%, a chest sword 5% (the best rarest), a shop weapon 7%, else
        /// 2000 gold (at least half the time; more once there is nothing of a kind left to give).
        /// </summary>
        private static ChestReward RollGolden()
        {
            float roll = UnityEngine.Random.value;
            if (roll < 0.34f)
            {
                var own = Cosmetics.ChestOnly();
                var pool = own.Count > 0 ? own : Cosmetics.InSlot(Slot.Suit).FindAll(c => !c.IsDefault && !c.chest && !Cosmetics.Owns(c));
                if (pool.Count > 0) return new ChestReward { outfit = pool[UnityEngine.Random.Range(0, pool.Count)] };
            }
            else if (roll < 0.38f)
            {
                var left = Shields.ChestLeft();
                if (left.Count > 0) return new ChestReward { shield = left[UnityEngine.Random.Range(0, left.Count)] };
            }
            else if (roll < 0.43f)
            {
                var w = PickWeighted(Armory.All.FindAll(x => x.chest && !Armory.Owned(x)));
                if (w != null) return new ChestReward { weapon = w };
            }
            else if (roll < 0.5f)
            {
                var w = Pick(Armory.All.FindAll(x => !x.Special && !x.chest && x.price > 0 && !Armory.Owned(x) && x.tier >= 3 && x.tier <= 5));
                if (w != null) return new ChestReward { weapon = w };
            }
            return new ChestReward { gold = 2000 };
        }

        private static ChestReward Roll(ChestTier t)
        {
            if (t == ChestTier.Advanced) return RollGolden();
            // A surprise outfit first: the chests' own ones, or a suit from the workshop not owned yet.
            // the golden chest's own shields, now and then
            if (t == ChestTier.Advanced && UnityEngine.Random.value < 0.06f)
            {
                var left = Shields.ChestLeft();
                if (left.Count > 0) return new ChestReward { shield = left[UnityEngine.Random.Range(0, left.Count)] };
            }
            float suitChance = t == ChestTier.Advanced ? 0.12f : t == ChestTier.Mid ? 0.06f : 0.02f;
            if (UnityEngine.Random.value < suitChance)
            {
                var own = Cosmetics.ChestOnly();
                var pool = own.Count > 0 && (t != ChestTier.Basic || UnityEngine.Random.value < 0.3f) ? own
                    : Cosmetics.InSlot(Slot.Suit).FindAll(c => !c.IsDefault && !c.chest && !Cosmetics.Owns(c));
                if (pool.Count > 0) return new ChestReward { outfit = pool[UnityEngine.Random.Range(0, pool.Count)] };
            }
            float roll = UnityEngine.Random.value;
            // the chest swords rare (the golden chest only), and the best of them rarest
            bool wantChest = t == ChestTier.Advanced && roll < 0.06f;
            bool wantShop = !wantChest && roll < (t == ChestTier.Advanced ? 0.2f : t == ChestTier.Mid ? 0.16f : 0.12f);
            WeaponDef w = null;
            if (wantChest) w = PickWeighted(Armory.All.FindAll(x => x.chest && !Armory.Owned(x)));
            if (w == null && (wantChest || wantShop))
            {
                int lo = t == ChestTier.Advanced ? 3 : t == ChestTier.Mid ? 2 : 1;
                int hi = t == ChestTier.Advanced ? 5 : t == ChestTier.Mid ? 3 : 2;
                w = Pick(Armory.All.FindAll(x => !x.Special && !x.chest && x.price > 0 && !Armory.Owned(x) && x.tier >= lo && x.tier <= hi));
            }
            if (w != null) return new ChestReward { weapon = w };
            // Gold: the golden chest's always 2000; the others more of it when a weapon was due but none was left to give.
            if (t == ChestTier.Advanced) return new ChestReward { gold = 2000 };
            var range = t == ChestTier.Mid ? (200, 450) : (60, 160);
            float k = wantChest || wantShop ? 1.5f : 1f;
            int gold = Mathf.RoundToInt(UnityEngine.Random.Range(range.Item1, range.Item2) * k / 10f) * 10;
            return new ChestReward { gold = gold };
        }

        private static WeaponDef Pick(List<WeaponDef> list) => list.Count == 0 ? null : list[UnityEngine.Random.Range(0, list.Count)];

        /// <summary>One of the list, the stronger ones less likely (by damage: the best comes out least).</summary>
        private static WeaponDef PickWeighted(List<WeaponDef> list)
        {
            if (list.Count == 0) return null;
            float sum = 0f;
            foreach (var x in list) sum += 1f / (x.damage * x.damage);
            float pick = UnityEngine.Random.value * sum;
            foreach (var x in list)
            {
                pick -= 1f / (x.damage * x.damage);
                if (pick <= 0f) return x;
            }
            return list[list.Count - 1];
        }
    }
}
