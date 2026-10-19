using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Data;

namespace BrickMuseum.FreeBuild
{
    /// <summary>
    /// The player's garage: any number of free-built cars (the first one comes from the old single car). A car can be
    /// driven once it has four wheels, and it is a taxi when a taxi sign is on it.
    /// </summary>
    public static class Garage
    {
        public static List<CarSave> Cars
        {
            get
            {
                var d = Progress.Data;
                if (d.cars.Count == 0 && d.carBuild.Count > 0)
                {
                    d.cars.Add(new CarSave { name = "Araba 1", pieces = d.carBuild });
                    d.carBuild = new List<BuildPieceData>();
                    Progress.Save();
                }
                return d.cars;
            }
        }

        /// <summary>The car being built (a first, empty one is made when there is none).</summary>
        public static CarSave Active
        {
            get
            {
                var cars = Cars;
                if (cars.Count == 0) { cars.Add(new CarSave { name = "Araba 1" }); Progress.Save(); }
                var d = Progress.Data;
                d.activeCar = System.Math.Max(0, System.Math.Min(d.activeCar, cars.Count - 1));
                return cars[d.activeCar];
            }
        }

        public static CarSave AddNew()
        {
            var c = new CarSave { name = $"Araba {Cars.Count + 1}" };
            Cars.Add(c);
            Progress.Data.activeCar = Cars.Count - 1;
            Progress.Save();
            return c;
        }

        public static void Next()
        {
            if (Cars.Count == 0) return;
            Progress.Data.activeCar = (Progress.Data.activeCar + 1) % Cars.Count;
            Progress.Save();
        }

        public static bool Drivable(CarSave c) => c.pieces.Count(p => p.kind.StartsWith("wheel")) >= 4;
        public static bool IsTaxi(CarSave c) => c.pieces.Any(p => p.kind.StartsWith("taxisign"));
        public static bool AnyDrivable => Cars.Any(Drivable);
    }
}
