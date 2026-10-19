using System;
using System.Collections.Generic;

namespace BrickMuseum.Data
{
    /// <summary>
    /// The fish farm: floating pens in the lake by the quay. Bought once (from a higher level), it grows a batch of sea
    /// bream and sea bass on a countdown; when they are ready they are collected into the cooler (and sold at the Fish
    /// Hall), and the next batch starts. An ad makes a batch ready at once. At this level it earns more than the rod.
    /// </summary>
    public static class FishFarm
    {
        public const int Price = 1200, UnlockLevel = 6, Batch = 10;
        public static readonly TimeSpan Grow = TimeSpan.FromHours(2);
        private static readonly string[] Species = { "cipura", "levrek", "cipura" };

        private static SaveFile D => Progress.Data;

        public static bool Owned => D.farmOwned;
        public static bool Unlocked => Progress.Level >= UnlockLevel;
        public static DateTime ReadyAt => new DateTime(D.farmReady, DateTimeKind.Utc);

        /// <summary>Time until the batch is ready (zero when it is).</summary>
        public static TimeSpan Left
        {
            get
            {
                var left = ReadyAt - DateTime.UtcNow;
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        public static bool Ready => Owned && Left == TimeSpan.Zero;

        /// <summary>How far the batch has grown, 0..1.</summary>
        public static float Grown => Owned ? 1f - (float)(Left.TotalSeconds / Grow.TotalSeconds) : 0f;

        public static bool Buy()
        {
            if (Owned || !Unlocked || !FreeBuild.Wallet.Spend(Price)) return false;
            D.farmOwned = true;
            Restart();
            return true;
        }

        public static void Restart() { D.farmReady = (DateTime.UtcNow + Grow).Ticks; Progress.Save(); }

        /// <summary>The ad: this batch is ready now.</summary>
        public static void Hurry() { D.farmReady = DateTime.UtcNow.Ticks; Progress.Save(); }

        /// <summary>The grown fish into the cooler; the next batch starts.</summary>
        public static List<FishItem> Collect(Random r)
        {
            var got = new List<FishItem>();
            if (!Ready) return got;
            for (int i = 0; i < Batch; i++) got.Add(Fish.Catch(Fish.Find(Species[r.Next(Species.Length)]), r));
            D.fish.AddRange(got);
            Restart();
            return got;
        }
    }
}
