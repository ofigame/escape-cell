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
        private static readonly int[] Prices = { 0, 300, 600, 1000, 1500, 8000, 10000, 12000, 15000 };

        public static int Count => CityLayout.LotCount;

        private static LotSave Save(int i)
        {
            var l = Progress.Data.lots;
            while (l.Count < Count) l.Add(new LotSave());
            return l[i];
        }

        public static bool Owned(int i) => i == 0 || Save(i).owned;
        public static int Price(int i) => Prices[System.Math.Min(i, Prices.Length - 1)];
        public static string Name(int i) => i == 0 ? "Evim" : CityLayout.IsVilla(i) ? $"Villa {i - CityLayout.TownLots + 1}" : $"Ev {i + 1}";

        /// <summary>Can this lot be bought now (the villas only once Gökkent is open)?</summary>
        public static bool ForSale(int i) => !CityLayout.IsVilla(i) || Mega.IsOpen;

        /// <summary>The house's pieces on that lot (the first lot keeps the original save fields).</summary>
        public static List<BuildPieceData> Pieces(int i) => i == 0 ? Progress.Data.build : Save(i).pieces;
        public static List<HomeItem> Items(int i) => i == 0 ? Progress.Data.home.items : Save(i).items;

        public static int OwnedCount { get { int n = 0; for (int i = 0; i < Count; i++) if (Owned(i)) n++; return n; } }

        /// <summary>Buys a lot; false when there is not enough money.</summary>
        public static bool Buy(int i)
        {
            if (Owned(i)) return true;
            if (!ForSale(i) || !Wallet.Spend(Price(i))) return false;
            Save(i).owned = true;
            Save(i).rentLast = System.DateTime.UtcNow.Ticks;
            Progress.Save();
            return true;
        }

        // ---------- rent: every house you own but don't live in (not "Evim") is let ----------

        public const int RentCapHours = 24;

        /// <summary>Lira an hour a let house brings: more for a bigger house, more for a villa.</summary>
        public static int RentRate(int i)
        {
            int n = Pieces(i).Count;
            if (i == 0 || n < 20) return 0;
            return (CityLayout.IsVilla(i) ? 40 : 10) + n / (CityLayout.IsVilla(i) ? 8 : 15);
        }

        public static int RentDue(int i)
        {
            int rate = RentRate(i);
            if (rate == 0 || !Owned(i)) return 0;
            var s = Save(i);
            if (s.rentLast == 0) { s.rentLast = System.DateTime.UtcNow.Ticks; Progress.Save(); }
            double h = (System.DateTime.UtcNow - new System.DateTime(s.rentLast, System.DateTimeKind.Utc)).TotalHours;
            return (int)(System.Math.Min(RentCapHours, System.Math.Max(0, h)) * rate);
        }

        public static int CollectRent(int i)
        {
            int got = RentDue(i);
            Save(i).rentLast = System.DateTime.UtcNow.Ticks;
            if (got > 0) Wallet.Earn(got);
            Progress.Save();
            return got;
        }

        /// <summary>The rent waiting on all the let houses.</summary>
        public static int RentWaiting { get { int n = 0; for (int i = 1; i < Count; i++) n += RentDue(i); return n; } }

        /// <summary>The next owned lot after this one (round).</summary>
        public static int NextOwned(int i)
        {
            for (int k = 1; k <= Count; k++) { int j = (i + k) % Count; if (Owned(j)) return j; }
            return 0;
        }
    }
}
