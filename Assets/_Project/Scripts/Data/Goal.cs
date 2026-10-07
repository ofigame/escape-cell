using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The one item the player is saving up for, picked in the shop or garage. The result card shows how close
    /// the coins are ("140 more coins for the Crown"), which makes "one more level" a concrete step.
    /// Stored as "up:Shield", "boost:StartShield" or "cos:hat.crown".
    /// </summary>
    public static class Goal
    {
        private const string Key = "sb_goal";

        public static string Id => PlayerPrefs.GetString(Key, "");

        public static bool Is(string id) => Id == id;

        public static void Toggle(string id)
        {
            PlayerPrefs.SetString(Key, Is(id) ? "" : id);
            PlayerPrefs.Save();
        }

        public static string ForUpgrade(Upgrade u) => "up:" + u;
        public static string ForBoost(Boost b) => "boost:" + b;
        public static string ForCosmetic(Cosmetic c) => "cos:" + c.id;

        /// <summary>The goal's name and price right now: the one picked, else the next thing within reach (false when nothing is left).</summary>
        public static bool TryGet(out string name, out int price) => Picked(out name, out price) || Auto(out name, out price);

        private static bool Picked(out string name, out int price)
        {
            name = null;
            price = 0;
            string id = Id;
            if (string.IsNullOrEmpty(id)) return false;
            int colon = id.IndexOf(':');
            if (colon < 0) return false;
            string kind = id.Substring(0, colon), value = id.Substring(colon + 1);

            switch (kind)
            {
                case "up":
                    if (!System.Enum.TryParse(value, out Upgrade u) || Shop.IsMaxed(u)) return false;
                    name = Loc.T("shop." + u);
                    price = Shop.NextPrice(u);
                    return true;
                case "boost":
                    if (!System.Enum.TryParse(value, out Boost b)) return false;
                    name = Loc.T("shop." + b);
                    price = Shop.Price(b);
                    return true;
                case "tool":
                    if (!System.Enum.TryParse(value, out Tool t) || Tools.Owned(t)) return false;
                    name = Loc.T("tool." + t);
                    price = Tools.NextPrice(t);
                    return true;
                case "cos":
                    var c = Cosmetics.Find(value);
                    if (c == null || Cosmetics.Owns(c)) return false;
                    name = Loc.T("cos." + c.id);
                    price = c.price;
                    return true;
            }
            return false;
        }

        /// <summary>
        /// No goal picked (or it is done): the next thing within reach is the goal by itself, the cheapest upgrade or tool
        /// the coins can't buy yet, so the result card always says what the next few levels are saving up for.
        /// </summary>
        private static bool Auto(out string name, out int price)
        {
            name = null;
            price = int.MaxValue;
            int coins = SaveData.Coins;
            foreach (Upgrade u in System.Enum.GetValues(typeof(Upgrade)))
            {
                int p = Shop.NextPrice(u);
                if (!Shop.IsMaxed(u) && Shop.Unlocked(u) && p > coins && p < price) { price = p; name = Loc.T("shop." + u); }
            }
            foreach (var t in Tools.All)
            {
                int p = Tools.NextPrice(t);
                if (!Tools.IsMaxed(t) && Tools.Unlocked(t) && p > coins && p < price) { price = p; name = Loc.T("tool." + t); }
            }
            if (name != null) return true;
            price = 0;
            return false;
        }
    }
}
