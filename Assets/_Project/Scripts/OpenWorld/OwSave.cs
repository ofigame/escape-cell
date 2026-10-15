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
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
