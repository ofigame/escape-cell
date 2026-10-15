using System;
using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.OpenWorld
{
    [Serializable] public class PlotState { public int x, z; public int crop = -1; public long planted; }
    [Serializable] public class TreeState { public int x, z; public int kind; public long planted; public long picked; }
    [Serializable] public class BuildingState { public int x, z; public int kind; public int animals; public long fed; }
    [Serializable] public class Order { public int item, count, item2, count2, reward; public bool done; }
    [Serializable] public class DefenceState { public int kind; public float x, z, rot; public int level = 1; public int hp; }

    /// <summary>
    /// The open world's save: money, the house, every plot, tree and building on the farm, the barn and the co-op's
    /// orders of the day. Kept as JSON in the player prefs (a server copy comes with phase D). Times are UTC seconds, so
    /// crops grow and animals produce while the game is closed.
    /// </summary>
    [Serializable]
    public class OwSave
    {
        private const string Key = "sb_ow";

        public long money;
        /// <summary>The escape's gold has been turned into money (once).</summary>
        public bool converted;
        public int house;
        public int barn;
        public List<PlotState> plots = new List<PlotState>();
        public List<TreeState> trees = new List<TreeState>();
        public List<BuildingState> buildings = new List<BuildingState>();
        public int[] items = new int[Enum.GetValues(typeof(Item)).Length];
        public int orderDay;
        public List<Order> orders = new List<Order>();
        public int[] soldToday = new int[Enum.GetValues(typeof(Item)).Length];
        /// <summary>The camera the player last used (true: TPS, sideways).</summary>
        public bool tps;
        /// <summary>Where foi stood when it left (world units; 0,0 = never been).</summary>
        public float px, pz;

        /// <summary>Defences raised round the farm.</summary>
        public List<DefenceState> defences = new List<DefenceState>();
        /// <summary>Soldiers of each army group stationed at the farm (bought with money), and the last day they were fed.</summary>
        public int[] soldiers = new int[6];
        public int fedDay;
        public bool hungry;
        /// <summary>The next raid (UTC seconds; 0: none planned), safe until, raids today.</summary>
        public long raidAt, protectedUntil, firstEntry;
        public int raidDay, raidsToday;
        /// <summary>Which of the farm sites the farm stands on (moving after selling up changes it).</summary>
        public int site;
        /// <summary>The last raid's report, shown once.</summary>
        public string report;

        private static OwSave current;

        public static OwSave Current
        {
            get
            {
                if (current != null) return current;
                var json = PlayerPrefs.GetString(Key, "");
                current = string.IsNullOrEmpty(json) ? new OwSave() : JsonUtility.FromJson<OwSave>(json) ?? new OwSave();
                current.Fix();
                return current;
            }
        }

        /// <summary>Arrays sized for the current item list (a save from an older version gets the new items at 0).</summary>
        private void Fix()
        {
            int n = Enum.GetValues(typeof(Item)).Length;
            if (items == null || items.Length != n) { var a = new int[n]; if (items != null) Array.Copy(items, a, Mathf.Min(n, items.Length)); items = a; }
            if (soldToday == null || soldToday.Length != n) soldToday = new int[n];
            plots ??= new List<PlotState>();
            trees ??= new List<TreeState>();
            buildings ??= new List<BuildingState>();
            orders ??= new List<Order>();
            defences ??= new List<DefenceState>();
            if (soldiers == null || soldiers.Length != 6) soldiers = new int[6];
        }

        /// <summary>Goes up with every save: the server keeps the highest (a new phone takes it).</summary>
        public long version;

        /// <summary>Saved since the server last had a copy.</summary>
        public static bool Dirty;

        public void Save()
        {
            version++;
            Dirty = true;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public string Json => JsonUtility.ToJson(this);

        /// <summary>Takes the server's copy in place of this phone's (it was newer).</summary>
        public static void Replace(string json)
        {
            var s = JsonUtility.FromJson<OwSave>(json);
            if (s == null) return;
            s.Fix();
            current = s;
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
            Dirty = false;
        }

        // ---------- The clock ----------

        private const string OffsetKey = "sb_ow_offset";
        private static long lastNow;

        /// <summary>The server's clock minus the phone's (seconds), learnt when the open world opens; kept for offline play.</summary>
        public static long Offset
        {
            get => long.TryParse(PlayerPrefs.GetString(OffsetKey, "0"), out var v) ? v : 0;
            set { PlayerPrefs.SetString(OffsetKey, value.ToString()); PlayerPrefs.Save(); }
        }

        private static long offsetCache = long.MinValue;

        /// <summary>The open world's time (UTC seconds): the server's clock, never going backwards — setting the phone's clock
        /// on does not ripen crops or fast-forward the animals.</summary>
        public static long Now
        {
            get
            {
                if (offsetCache == long.MinValue) offsetCache = Offset;
                long n = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + offsetCache;
                if (n < lastNow) n = lastNow;
                lastNow = n;
                return n;
            }
        }

        /// <summary>The server said what time it is.</summary>
        public static void SetServerTime(long serverNow)
        {
            if (serverNow <= 0) return;
            offsetCache = serverNow - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Offset = offsetCache;
            lastNow = 0;
        }

        // ---------- Money and the barn ----------

        public bool Spend(long amount)
        {
            if (amount < 0 || money < amount) return false;
            money -= amount;
            return true;
        }

        public int BarnSize => OwData.BarnSizes[Mathf.Clamp(barn, 0, OwData.BarnSizes.Length - 1)];

        public int Stored
        {
            get { int n = 0; foreach (var c in items) n += c; return n; }
        }

        public int Room => Mathf.Max(0, BarnSize - Stored);

        /// <summary>Puts items into the barn (as many as fit). Returns how many went in.</summary>
        public int Add(Item i, int n)
        {
            int k = Mathf.Clamp(n, 0, Room);
            items[(int)i] += k;
            return k;
        }

        public bool Take(Item i, int n)
        {
            if (n <= 0) return true;
            if (items[(int)i] < n) return false;
            items[(int)i] -= n;
            return true;
        }

        public int Count(Item i) => items[(int)i];

        public HouseDef House => OwData.Houses[Mathf.Clamp(house, 0, OwData.Houses.Length - 1)];

        // ---------- Gold → money ----------

        /// <summary>The first entry: all the escape's gold becomes money (10 to 1). Returns (gold, money) moved.</summary>
        public (int gold, long money) ConvertFirst()
        {
            if (converted) return (0, 0);
            converted = true;
            int gold = SaveData.Coins;
            long m = gold / OwData.FirstRate;
            money += m + 50; // and a little to start with
            SaveData.Coins = gold - (int)(m * OwData.FirstRate);
            Save();
            return (gold, m + 50);
        }

        /// <summary>Gold earned since (levels again, duels, arenas) carried over at a quarter of the value (40 to 1).</summary>
        public (int gold, long money) CarryGold()
        {
            int gold = SaveData.Coins;
            long m = gold / OwData.LaterRate;
            if (m <= 0) return (0, 0);
            money += m;
            SaveData.Coins = gold - (int)(m * OwData.LaterRate);
            Save();
            return ((int)(m * OwData.LaterRate), m);
        }

        // ---------- Soldiers ----------

        public int SoldierCap => 30 + house * 10;

        public int SoldierCount { get { int n = 0; foreach (var s in soldiers) n += s; return n; } }

        /// <summary>Each day every soldier eats a wheat and an egg or milk; short of food they fight at 60%.</summary>
        public void FeedSoldiers()
        {
            int today = Today;
            if (fedDay == today) return;
            int days = 1;
            if (fedDay != 0)
                days = Mathf.Clamp((int)(DateTime.ParseExact(today.ToString(), "yyyyMMdd", null) - DateTime.ParseExact(fedDay.ToString(), "yyyyMMdd", null)).TotalDays, 1, 7);
            fedDay = today;
            int need = SoldierCount * days;
            if (need == 0) { hungry = false; return; }
            int wheat = Mathf.Min(need, items[(int)Item.Wheat]);
            items[(int)Item.Wheat] -= wheat;
            int other = need;
            foreach (var i in new[] { Item.Egg, Item.Milk, Item.GoatMilk })
            {
                int k = Mathf.Min(other, items[(int)i]);
                items[(int)i] -= k;
                other -= k;
            }
            hungry = wheat < need || other > 0;
            Save();
        }

        // ---------- Raids ----------

        /// <summary>Plans the next raid if none is: a few hours on, never inside the protection, at most two a day; the
        /// first one only half a day after foi first came.</summary>
        public void PlanRaid()
        {
            long now = Now;
            if (firstEntry == 0) firstEntry = now;
            if (raidAt > now) return;
            if (raidDay != Today) { raidDay = Today; raidsToday = 0; }
            var rng = new System.Random((int)(now % 100000));
            long at = now + 3 * 3600 + rng.Next(6 * 3600);
            at = Math.Max(at, protectedUntil);
            at = Math.Max(at, firstEntry + 12 * 3600);
            if (raidsToday >= OwBattleData.RaidsPerDay) at = Math.Max(at, new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1)).ToUnixTimeSeconds() + rng.Next(4 * 3600));
            raidAt = at;
            Save();
        }

        public void RaidDone()
        {
            if (raidDay != Today) { raidDay = Today; raidsToday = 0; }
            raidsToday++;
            protectedUntil = Now + (long)(OwBattleData.ProtectHours * 3600);
            raidAt = 0;
            PlanRaid();
        }

        public float WarningHours => OwBattleData.WarningHours + (defences.Exists(d => d.kind == (int)DefenceKind.Watchtower) ? OwBattleData.WatchtowerExtra : 0f);

        /// <summary>The bandits take their share: part of the barn, the crops in the ground, and wear on the defences.</summary>
        public (int items, int crops) Loot(System.Random rng)
        {
            int taken = 0, crops = 0;
            for (int i = 0; i < items.Length; i++)
            {
                int k = Mathf.FloorToInt(items[i] * OwBattleData.LootShare);
                items[i] -= k;
                taken += k;
            }
            foreach (var p in plots) if (p.crop >= 0 && rng.NextDouble() < 0.6) { p.crop = -1; crops++; }
            foreach (var d in defences) d.hp = Mathf.Max(1, d.hp - Mathf.RoundToInt(OwBattleData.Of((DefenceKind)d.kind).hp * 0.3f));
            return (taken, crops);
        }

        /// <summary>What everything on the farm is worth (the house, its buildings and animals, plots, trees, defences, the
        /// barn and what is in it).</summary>
        public long FarmValue()
        {
            long v = 0;
            for (int h = 1; h <= house; h++) v += OwData.Houses[h].price;
            for (int b = 1; b <= barn; b++) v += OwData.BarnPrices[b];
            foreach (var b in buildings) { var d = OwData.Of((BuildingKind)b.kind); v += d.price + d.animalPrice * b.animals; }
            v += plots.Count * 10;
            foreach (var t in trees) v += OwData.Of((TreeKind)t.kind).sapling;
            foreach (var d in defences) v += (long)OwBattleData.Of((DefenceKind)d.kind).price * d.level;
            for (int i = 0; i < items.Length; i++) v += (long)items[i] * OwData.Price((Item)i);
            return v;
        }

        /// <summary>Sells up at half the value and starts again in another safe spot (the soldiers come along).</summary>
        public long SellAndMove()
        {
            long got = FarmValue() / 2;
            money += got;
            house = 0; barn = 0;
            plots.Clear(); trees.Clear(); buildings.Clear(); defences.Clear();
            for (int i = 0; i < items.Length; i++) items[i] = 0;
            site = (site + 1) % OwWorld.Sites.Length;
            px = 0f; pz = 0f;
            raidAt = 0;
            protectedUntil = Now + 12 * 3600;
            PlanRaid();
            Save();
            return got;
        }

        // ---------- The co-op ----------

        public static int Today => int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));

        /// <summary>Today's three orders (made new each day from what the farm can grow now) and a fresh price list.</summary>
        public void RefreshDay()
        {
            if (orderDay == Today && orders.Count > 0) return;
            orderDay = Today;
            for (int i = 0; i < soldToday.Length; i++) soldToday[i] = 0;
            orders.Clear();
            var rng = new System.Random(Today * 7 + house);
            var can = new List<Item>();
            foreach (var c in OwData.Crops) if (c.house <= house) can.Add(c.item);
            foreach (var t in OwData.Trees) if (t.house <= house) can.Add(t.item);
            foreach (var b in OwData.Buildings) if (b.house <= house) can.Add(b.product);
            for (int k = 0; k < 3; k++)
            {
                var a = can[rng.Next(can.Count)];
                var b = can[rng.Next(can.Count)];
                int ca = 4 + rng.Next(8 + house * 4), cb = a == b ? 0 : 2 + rng.Next(5 + house * 2);
                int value = ca * OwData.Price(a) + cb * OwData.Price(b);
                orders.Add(new Order { item = (int)a, count = ca, item2 = (int)b, count2 = cb, reward = Mathf.RoundToInt(value * 1.5f) + 10 });
            }
            Save();
        }

        /// <summary>The co-op's price for one more of an item today: it falls (to 80%) as the same item is sold a lot.</summary>
        public int PriceToday(Item i)
        {
            float drop = Mathf.Clamp01(soldToday[(int)i] / 200f) * 0.2f;
            return Mathf.Max(1, Mathf.RoundToInt(OwData.Price(i) * (1f - drop)));
        }

        /// <summary>Sells up to n of an item at today's price. Returns the money made.</summary>
        public long Sell(Item i, int n)
        {
            long made = 0;
            n = Mathf.Min(n, items[(int)i]);
            for (int k = 0; k < n; k++) { made += PriceToday(i); soldToday[(int)i]++; }
            items[(int)i] -= n;
            money += made;
            return made;
        }

        public bool CanFill(Order o) => !o.done && items[o.item] >= o.count && (o.count2 <= 0 || items[o.item2] >= o.count2);

        public bool Fill(Order o)
        {
            if (!CanFill(o)) return false;
            items[o.item] -= o.count;
            if (o.count2 > 0) items[o.item2] -= o.count2;
            money += o.reward;
            o.done = true;
            return true;
        }
    }
}
