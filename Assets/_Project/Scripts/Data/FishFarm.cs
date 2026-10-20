using System;
using System.Collections.Generic;

namespace BrickMuseum.Data
{
    /// <summary>
    /// The fish farm: floating pens in the lake by the quay. Bought once (from a higher level), it grows a batch of sea
    /// fish every 10 minutes (30, more and better ones with each upgrade); when they are ready they are collected into the
    /// cooler (and sold at the Fish Hall), and the next batch starts. No ad hurries it.
    /// </summary>
    public static class FishFarm
    {
        public const int Price = 1200, UnlockLevel = 6, MaxLevel = 3;
        public static readonly TimeSpan Grow = TimeSpan.FromMinutes(10);

        /// <summary>The farm's level (1 when bought): each upgrade gives more fish, and better ones.</summary>
        public static int Level => Math.Max(1, D.farmLevel);
        public static int Batch => 30 + 15 * (Level - 1);
        public static int UpgradePrice => Level == 1 ? 2500 : 6000;
        private static string[] Species => Level == 1 ? new[] { "hamsi", "istavrit" } : Level == 2 ? new[] { "hamsi", "istavrit", "cipura" } : new[] { "istavrit", "cipura", "levrek" };

        public static bool Upgrade()
        {
            if (!Owned || Level >= MaxLevel || !FreeBuild.Wallet.Spend(UpgradePrice)) return false;
            D.farmLevel = Level + 1;
            Progress.Save();
            return true;
        }

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
            D.farmLevel = 1;
            Restart();
            return true;
        }

        public static void Restart() { D.farmReady = (DateTime.UtcNow + Grow).Ticks; Progress.Save(); }

        /// <summary>This batch ready now (the screenshot tour only).</summary>
        public static void Hurry() { D.farmReady = DateTime.UtcNow.Ticks; Progress.Save(); }

        /// <summary>The grown fish into the cooler; the next batch starts.</summary>
        public static List<FishItem> Collect(Random r)
        {
            var got = new List<FishItem>();
            if (!Ready) return got;
            var species = Species;
            for (int i = 0; i < Batch; i++) got.Add(Fish.Catch(Fish.Find(species[r.Next(species.Length)]), r));
            D.fish.AddRange(got);
            Restart();
            return got;
        }
    }
}
