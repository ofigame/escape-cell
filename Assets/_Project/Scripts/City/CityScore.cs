using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Data;

namespace BrickMuseum.City
{
    /// <summary>
    /// The city score: how well the town is looked after. Police, street cleaners (municipal workers), municipal staff,
    /// rubbish bins, municipal vehicles and public buildings count; the people come with the buildings and vehicles
    /// that bring them and with the level-up bonuses. The score opens the gardens of houses and of bigger buildings.
    /// </summary>
    public static class CityScore
    {
        public const int HouseGardens = 40, BuildingGardens = 120;

        private static readonly HashSet<string> MunicipalVehicles = new HashSet<string> { "cop", "itfaiye", "polis", "ambulans", "kepce", "bus" };

        /// <summary>One line of the score: what, how many, points each.</summary>
        public sealed class Part
        {
            public string name, hint, pic;   // pic: a model that shows it
            public int count, each;
            public int Points => count * each;
        }

        public static List<Part> Parts()
        {
            var placed = Progress.Data.placements.Select(p => ModelCatalog.Find(p.id)).Where(m => m != null).ToList();
            var roles = placed.SelectMany(Townsfolk.RolesOf).ToList();
            var d = Progress.Data;
            return new List<Part>
            {
                new Part { pic = "polis", name = "Polis", hint = "Polis arabası, belediye binası, bonus polis gücü", count = roles.Count(r => r == "police") + d.bonusPolice, each = 10 },
                new Part { pic = "cop", name = "Belediye işçisi", hint = "Çöp kamyonu, belediye binası, bonus işçiler", count = roles.Count(r => r == "cleaner") + d.bonusWorkers, each = 8 },
                new Part { pic = "belediye", name = "Belediye personeli", hint = "Kamu binaları, iş merkezleri, bonus personel", count = roles.Count(r => r == "office") + d.bonusStaff, each = 6 },
                new Part { pic = "konteyner", name = "Çöp Konteyneri", hint = "Dekor yerlerine konteyner koy", count = placed.Count(m => m.id == "konteyner"), each = 6 },
                new Part { pic = "itfaiye", name = "Belediye araçları", hint = "Çöp kamyonu, itfaiye, polis arabası, ambulans, kepçe, otobüs", count = placed.Count(m => MunicipalVehicles.Contains(m.id)), each = 12 },
                new Part { pic = "okul", name = "Kamu binaları", hint = "Okul, hastane, belediye, itfaiye, kütüphane", count = placed.Count(m => m.kind == "civic"), each = 15 },
            };
        }

        public static int Total => Parts().Sum(p => p.Points);

        /// <summary>The score a slot's building needs before its garden can be built.</summary>
        public static int NeededFor(Slot s) => s.kind == "house" ? HouseGardens : BuildingGardens;

        public static bool CanUpgrade(Slot s) => s.kind == "house" || s.kind == "big" || s.kind == "civic" || s.kind == "historic";
    }
}
