using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Data;

namespace BrickMuseum.City
{
    /// <summary>
    /// The village (Orman Köyü): four blocks far north of the town in the forest, at the end of a country road. It is closed to building
    /// ("imara kapalı") until the town has a school, a hospital and a police station, and then it is opened for a high
    /// price. Any model of a plot's kind may be built there again (a second copy), so the town can keep growing.
    /// </summary>
    public static class District
    {
        public const int Price = 5000;
        public static readonly string[] Needed = { "okul", "hastane", "karakol" };

        public static bool IsOpen => Progress.Data.farOpen;

        /// <summary>The public buildings still missing from the town.</summary>
        public static List<ModelDef> Missing() =>
            Needed.Where(id => !Progress.Data.placements.Any(p => p.id == id && !(CityLayout.Find(p.slot)?.area.far ?? false)))
                .Select(ModelCatalog.Find).Where(m => m != null).ToList();

        public static bool Ready => Missing().Count == 0;

        /// <summary>Pays and opens the district (false: not ready, already open, or not enough money).</summary>
        public static bool Open()
        {
            if (IsOpen || !Ready || !FreeBuild.Wallet.Spend(Price)) return false;
            Progress.Data.farOpen = true;
            Progress.Save();
            StreetGraph.Reset();
            return true;
        }

        /// <summary>Is this slot (or block) open for building at this level?</summary>
        public static bool Opened(Slot s, int level) => level >= s.level && (!s.area.far || IsOpen) && (!s.area.mega || Mega.IsOpen);
        public static bool Opened(Area a, int level) => level >= a.level && (!a.far || IsOpen) && (!a.mega || Mega.IsOpen);

        /// <summary>Closed to building (the village or Gökkent not opened yet)?</summary>
        public static bool Closed(Area a) => a.far && !IsOpen || a.mega && !Mega.IsOpen;
    }
}
