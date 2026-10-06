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
        public static string ForCity(CityPiece p) => "city:" + p.id;

        /// <summary>The goal's name and price right now, or false when there is none (or it is owned / maxed).</summary>
        public static bool TryGet(out string name, out int price)
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
                case "city":
                    var piece = CityCatalog.Find(value);
                    if (piece == null || City.Count(piece.id) > 0) return false;
                    name = Loc.T("city." + piece.id);
                    price = piece.price;
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
    }
}
