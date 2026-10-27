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
        /// <summary>Core parts beside the gold (the bronze and gold chests).</summary>
        public int cores;
        public WeaponDef weapon;
        public Cosmetic outfit;
        public ShieldDef shield;
        /// <summary>The items beside it (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 4): in the bag already as the chest
        /// opens; an ad's second draw keeps them (never redrawn for an ad).</summary>
        public System.Collections.Generic.List<ItemGrant> items = new System.Collections.Generic.List<ItemGrant>();
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

        /// <summary>A chest given outright (the login calendar, quests).</summary>
        public static void Add(ChestTier t)
        {
            SetCount(t, Count(t) + 1);
            Prefs.Save();
        }

        // The chances (the odds shown on the chests' screen are worked out from these same numbers).
        // (the scenario's §4.5: plain 3% outfit, 10% shop weapon of tier 1–2; bronze 8%, 14% of tier 2–3, core parts 2–5;
        // gold 30% outfit, 8% chest sword, 4% chest shield, 8% shop weapon of tier 3–5, else 1,500 gold and 10–20 core parts)
        private static readonly float[] SuitChance = { 0.03f, 0.08f }, ShopChance = { 0.10f, 0.14f }; // (plain, bronze)
        private const float GoldOutfit = 0.30f, GoldShield = 0.04f, GoldChestWeapon = 0.08f, GoldShopWeapon = 0.08f;
        private const int GoldChestGold = 1500;
        /// <summary>The shop weapons' tiers a chest draws from, and its gold and core parts when gold comes out (plain, bronze, gold).</summary>
        private static readonly (int lo, int hi)[] ShopTiers = { (1, 2), (2, 3), (3, 5) };
        private static readonly (int lo, int hi)[] GoldRange = { (60, 160), (200, 450), (GoldChestGold, GoldChestGold) };
        private static readonly (int lo, int hi)[] CoreRange = { (0, 0), (2, 5), (10, 20) };

        /// <summary>The shop weapons' tiers this chest draws from (the contents screen lists the same).</summary>
        public static (int lo, int hi) ShopTierRange(ChestTier t) => ShopTiers[(int)t];

        /// <summary>The chest's ranges, worked out from the same numbers as its draw, e.g. "Weapons tier 2–3 · Gold
        /// 200–450 · Core parts 2–5", and the note that gold comes instead of a kind there is nothing left of.</summary>
        public static string DetailText(ChestTier t)
        {
            var (lo, hi) = ShopTiers[(int)t];
            var g = GoldRange[(int)t];
            var c = CoreRange[(int)t];
            string text = Loc.F("odds.tiers", lo, hi) + " · " + Loc.F("odds.goldRange", Span(g.lo, g.hi));
            if (c.hi > 0) text += " · " + Loc.F("odds.coreRange", Span(c.lo, c.hi));
            return text + "\n" + Loc.T("odds.fallback") + "\n" + ItemDrops.ChestText(t);
        }

        private static string Span(int lo, int hi) => lo == hi ? lo.ToString() : lo + "–" + hi;

        /// <summary>What may come out of a chest and how likely, e.g. "Gold 86% · Weapon 12% · Outfit 2%".</summary>
        public static string OddsText(ChestTier t)
        {
            var parts = new List<(string key, float p)>();
            if (t == ChestTier.Advanced)
            {
                parts.Add(("odds.outfit", GoldOutfit));
                parts.Add(("odds.chestWeapon", GoldChestWeapon));
                parts.Add(("odds.weapon", GoldShopWeapon));
                parts.Add(("odds.shield", GoldShield));
            }
            else
            {
                int i = (int)t;
                parts.Add(("odds.weapon", (1f - SuitChance[i]) * ShopChance[i]));
                parts.Add(("odds.outfit", SuitChance[i]));
            }
            float rest = 1f;
            foreach (var x in parts) rest -= x.p;
            parts.Insert(0, ("odds.gold", rest));
            var sb = new System.Text.StringBuilder();
            foreach (var x in parts)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(Loc.T(x.key)).Append(' ').Append(Loc.F("fmt.pct", Mathf.RoundToInt(x.p * 100f)));
            }
            sb.Append(" · +").Append(Loc.F("drop.chestItems", ItemCatalog.Current.chestItems[(int)t])); // (the items beside the draw)
            return sb.ToString();
        }

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
            // gold: every 7th win in a row with 60% health left (7, 14, 21..), not every win after the 7th
            var tier = streak % 7 == 0 && healthLeft >= 0.6f ? ChestTier.Advanced
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
            r.items = ItemDrops.FromChest(t);
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
            if (was != null) r.items = was.items; // (the items stay: an ad never draws them again)
            Hold(r);
            return r;
        }

        // What came out and is not given yet is kept on the phone: if the game closes while it is shown (or while an ad
        // swaps it), it is given the next time (the chest was already spent).
        private const string PendingKey = "sb_dchest_pending";

        private static void Hold(ChestReward r)
        {
            Prefs.SetString(PendingKey, r == null ? "" : r.weapon != null ? "w:" + r.weapon.id : r.shield != null ? "s:" + r.shield.id : r.outfit != null ? "o:" + r.outfit.id : "g:" + r.gold + ":" + r.cores);
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
            else
            {
                var gc = v.Split(':');
                int.TryParse(gc[0], out r.gold);
                if (gc.Length > 1) int.TryParse(gc[1], out r.cores);
            }
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
            else { SaveData.Coins += r.gold; CoreParts.Add(r.cores); }
            Prefs.Save();
        }

        /// <summary>
        /// The golden chest: an outfit 30%, a shield 4%, a chest sword 8% (the best rarest), a shop weapon 8% (tier 3–5),
        /// else 1,500 gold and 10–20 core parts (about half the time; more once there is nothing of a kind left to give).
        /// </summary>
        private static ChestReward RollGolden()
        {
            float roll = UnityEngine.Random.value;
            if (roll < GoldOutfit)
            {
                var own = Cosmetics.ChestOnly();
                var pool = own.Count > 0 ? own : Cosmetics.InSlot(Slot.Suit).FindAll(c => !c.IsDefault && !c.chest && !Cosmetics.Owns(c));
                if (pool.Count > 0) return new ChestReward { outfit = pool[UnityEngine.Random.Range(0, pool.Count)] };
            }
            else if (roll < GoldOutfit + GoldShield)
            {
                var left = Shields.ChestLeft();
                if (left.Count > 0) return new ChestReward { shield = left[UnityEngine.Random.Range(0, left.Count)] };
            }
            else if (roll < GoldOutfit + GoldShield + GoldChestWeapon)
            {
                var w = PickWeighted(Armory.All.FindAll(x => x.chest && !Armory.Owned(x)));
                if (w != null) return new ChestReward { weapon = w };
            }
            else if (roll < GoldOutfit + GoldShield + GoldChestWeapon + GoldShopWeapon)
            {
                var w = Pick(Armory.All.FindAll(x => !x.Special && !x.chest && x.kind != WeaponKind.Gun && x.price > 0 && !Armory.Owned(x) && x.tier >= ShopTiers[2].lo && x.tier <= ShopTiers[2].hi));
                if (w != null) return new ChestReward { weapon = w };
            }
            return new ChestReward { gold = GoldRange[2].lo, cores = UnityEngine.Random.Range(CoreRange[2].lo, CoreRange[2].hi + 1) };
        }

        private static ChestReward Roll(ChestTier t)
        {
            if (t == ChestTier.Advanced) return RollGolden();
            // A surprise outfit first: the chests' own ones, or a suit from the workshop not owned yet.
            if (UnityEngine.Random.value < SuitChance[(int)t])
            {
                var own = Cosmetics.ChestOnly();
                var pool = own.Count > 0 && (t != ChestTier.Basic || UnityEngine.Random.value < 0.3f) ? own
                    : Cosmetics.InSlot(Slot.Suit).FindAll(c => !c.IsDefault && !c.chest && !Cosmetics.Owns(c));
                if (pool.Count > 0) return new ChestReward { outfit = pool[UnityEngine.Random.Range(0, pool.Count)] };
            }
            float roll = UnityEngine.Random.value;
            bool wantChest = false; // (the chest swords: the golden chest only)
            bool wantShop = roll < ShopChance[(int)t];
            WeaponDef w = null;
            if (wantChest) w = PickWeighted(Armory.All.FindAll(x => x.chest && !Armory.Owned(x)));
            if (w == null && (wantChest || wantShop))
            {
                var (lo, hi) = ShopTiers[(int)t];
                w = Pick(Armory.All.FindAll(x => !x.Special && !x.chest && x.kind != WeaponKind.Gun && x.price > 0 && !Armory.Owned(x) && x.tier >= lo && x.tier <= hi));
            }
            if (w != null) return new ChestReward { weapon = w };
            // Gold (more of it when a weapon was due but none was left to give); the bronze chest's with 2–5 core parts.
            var range = GoldRange[(int)t];
            float k = wantChest || wantShop ? 1.5f : 1f;
            int gold = Mathf.RoundToInt(UnityEngine.Random.Range(range.lo, range.hi) * k / 10f) * 10;
            var cores = CoreRange[(int)t];
            return new ChestReward { gold = gold, cores = cores.hi > 0 ? UnityEngine.Random.Range(cores.lo, cores.hi + 1) : 0 };
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
