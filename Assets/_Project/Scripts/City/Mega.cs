using System;
using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Data;

namespace BrickMuseum.City
{
    /// <summary>
    /// Gökkent: the big closed city at the end of the highway past the village — skyscrapers, huge hotels and golf
    /// courses, much bigger than the town. It opens for a big price once the village is open and the town's score is
    /// high; any model may be built there again (copies), and its hotels earn lira by the hour.
    /// </summary>
    public static class Mega
    {
        public const string Name = "Gökkent";
        public const int Price = 20000, ScoreNeeded = 250;

        public static bool IsOpen => Progress.Data.megaOpen;

        public static bool Ready => District.IsOpen && CityScore.Total >= ScoreNeeded;

        public static bool Open()
        {
            if (IsOpen || !Ready || !FreeBuild.Wallet.Spend(Price)) return false;
            Progress.Data.megaOpen = true;
            Progress.Save();
            StreetGraph.Reset();
            return true;
        }
    }

    /// <summary>One hotel's till: when its takings were last collected (UTC ticks).</summary>
    [Serializable]
    public class HotelSave
    {
        public string slot;
        public long last;
    }

    /// <summary>
    /// Hotels earn by the hour while you are away (up to half a day's takings); the till is emptied from the hotel's
    /// panel, an ad doubles what is in it.
    /// </summary>
    public static class Hotels
    {
        public const int CapHours = 12;

        /// <summary>Lira an hour for a hotel model.</summary>
        public static int Rate(ModelDef m) => m.id == "devotel" ? 140 : m.id == "luksotel" ? 90 : 60;

        private static HotelSave Till(string slot)
        {
            var h = Progress.Data.hotels.FirstOrDefault(x => x.slot == slot);
            if (h == null) { Progress.Data.hotels.Add(h = new HotelSave { slot = slot, last = DateTime.UtcNow.Ticks }); Progress.Save(); }
            return h;
        }

        /// <summary>What is in a hotel's till now.</summary>
        public static int InTill(string slot, ModelDef m)
        {
            double hours = (DateTime.UtcNow - new DateTime(Till(slot).last, DateTimeKind.Utc)).TotalHours;
            return (int)(Math.Min(CapHours, Math.Max(0, hours)) * Rate(m));
        }

        public static int Collect(string slot, ModelDef m, bool doubled)
        {
            int got = InTill(slot, m) * (doubled ? 2 : 1);
            Till(slot).last = DateTime.UtcNow.Ticks;
            if (got > 0) FreeBuild.Wallet.Earn(got);
            Progress.Save();
            return got;
        }

        /// <summary>The hotels standing in the city (for the score list and the reminders).</summary>
        public static List<Placement> All() => Progress.Data.placements.Where(p => ModelCatalog.Find(p.id)?.kind == "hotel").ToList();
    }
}
