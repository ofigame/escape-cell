using System.Collections.Generic;
using BrickMuseum.City;
using BrickMuseum.Data;

namespace BrickMuseum.FreeBuild
{
    /// <summary>
    /// The player's lots along the west road: the first (the home) is theirs from the start, the others are for sale,
    /// each dearer than the last. Every lot owned holds a free-built house with its own furniture.
    /// </summary>
    public static class Lots
    {
        private static readonly int[] Prices = { 0, 300, 600, 1000, 1500 };

        public static int Count => CityLayout.LotCount;

        private static LotSave Save(int i)
        {
            var l = Progress.Data.lots;
            while (l.Count < Count) l.Add(new LotSave());
            return l[i];
        }

        public static bool Owned(int i) => i == 0 || Save(i).owned;
        public static int Price(int i) => Prices[System.Math.Min(i, Prices.Length - 1)];
        public static string Name(int i) => i == 0 ? "Evim" : $"Ev {i + 1}";

        /// <summary>The house's pieces on that lot (the first lot keeps the original save fields).</summary>
        public static List<BuildPieceData> Pieces(int i) => i == 0 ? Progress.Data.build : Save(i).pieces;
        public static List<HomeItem> Items(int i) => i == 0 ? Progress.Data.home.items : Save(i).items;

        public static int OwnedCount { get { int n = 0; for (int i = 0; i < Count; i++) if (Owned(i)) n++; return n; } }

        /// <summary>Buys a lot; false when there is not enough money.</summary>
        public static bool Buy(int i)
        {
            if (Owned(i)) return true;
            if (!Wallet.Spend(Price(i))) return false;
            Save(i).owned = true;
            Progress.Save();
            return true;
        }

        /// <summary>The next owned lot after this one (round).</summary>
        public static int NextOwned(int i)
        {
            for (int k = 1; k <= Count; k++) { int j = (i + k) % Count; if (Owned(j)) return j; }
            return 0;
        }
    }
}
