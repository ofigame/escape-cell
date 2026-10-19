using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BrickMuseum.Data
{
    /// <summary>One field of the village: bought or not, what grows on it and when it was sown / is ripe (UTC ticks).</summary>
    [Serializable]
    public class FieldSave
    {
        public int index;
        public bool owned;
        public string crop = "";
        public long sownAt, ripeAt;
    }

    /// <summary>A farm building of the village (the barn, the coop): bought or not, when its produce is ready.</summary>
    [Serializable]
    public class BarnSave
    {
        public string id;
        public bool owned;
        public long readyAt;
    }

    /// <summary>A crop: what its seed costs, how long it takes, what the harvest fetches, its colour when ripe.</summary>
    public sealed class Crop
    {
        public string id, name;
        public int seed, sell, minutes;
        public Color ripe;
    }

    /// <summary>A farm building: price, how often it gives, what, and how much.</summary>
    public sealed class Barn
    {
        public string id, name, product;
        public int price, minutes, yield;
    }

    /// <summary>
    /// The village's farming: fields bought one by one, a crop sown on each (a seed price), growing on a countdown
    /// through seed → sprout → young plant → ripe, harvested for lira; the barn (milk) and the coop (eggs) give lira on
    /// their own countdowns. An ad makes a field or a building ready at once.
    /// </summary>
    public static class Farming
    {
        public const int FieldCount = 8;

        public static readonly List<Crop> Crops = new List<Crop>
        {
            new Crop { id = "bugday", name = "Buğday", seed = 20, sell = 70, minutes = 30, ripe = new Color(0.93f, 0.76f, 0.3f) },
            new Crop { id = "misir", name = "Mısır", seed = 50, sell = 170, minutes = 60, ripe = new Color(0.98f, 0.84f, 0.2f) },
            new Crop { id = "domates", name = "Domates", seed = 80, sell = 290, minutes = 120, ripe = new Color(0.88f, 0.16f, 0.12f) },
            new Crop { id = "aycicegi", name = "Ayçiçeği", seed = 140, sell = 520, minutes = 240, ripe = new Color(1f, 0.82f, 0.1f) },
            new Crop { id = "cilek", name = "Çilek", seed = 200, sell = 800, minutes = 480, ripe = new Color(0.92f, 0.1f, 0.25f) },
        };

        public static readonly List<Barn> Barns = new List<Barn>
        {
            new Barn { id = "kumes", name = "Kümes", product = "Yumurta", price = 600, minutes = 60, yield = 90 },
            new Barn { id = "ahir", name = "Ahır", product = "Süt", price = 1500, minutes = 180, yield = 320 },
        };

        public static Crop CropOf(string id) => Crops.FirstOrDefault(c => c.id == id);
        public static Barn BarnOf(string id) => Barns.FirstOrDefault(b => b.id == id);

        private static SaveFile D => Progress.Data;

        // ---------- fields ----------

        public static FieldSave Field(int i)
        {
            var f = D.fields.FirstOrDefault(x => x.index == i);
            if (f == null) D.fields.Add(f = new FieldSave { index = i, owned = i < 2 });   // the first two fields come with the village
            return f;
        }

        public static int FieldPrice(int i) => i < 2 ? 0 : 150 + i * 50;

        public static bool BuyField(int i)
        {
            var f = Field(i);
            if (f.owned || !FreeBuild.Wallet.Spend(FieldPrice(i))) return false;
            f.owned = true;
            Progress.Save();
            return true;
        }

        public static bool Sow(int i, Crop c)
        {
            var f = Field(i);
            if (!f.owned || f.crop != "" || !FreeBuild.Wallet.Spend(c.seed)) return false;
            f.crop = c.id;
            f.sownAt = DateTime.UtcNow.Ticks;
            f.ripeAt = (DateTime.UtcNow + TimeSpan.FromMinutes(c.minutes)).Ticks;
            Progress.Save();
            return true;
        }

        /// <summary>How far the crop has grown, 0..1 (0 with nothing sown).</summary>
        public static float Grown(FieldSave f)
        {
            if (f.crop == "") return 0f;
            double all = f.ripeAt - f.sownAt, done = DateTime.UtcNow.Ticks - f.sownAt;
            return all <= 0 ? 1f : Mathf.Clamp01((float)(done / all));
        }

        public static bool Ripe(FieldSave f) => f.crop != "" && DateTime.UtcNow.Ticks >= f.ripeAt;

        public static TimeSpan Left(FieldSave f) { var t = new DateTime(f.ripeAt, DateTimeKind.Utc) - DateTime.UtcNow; return t > TimeSpan.Zero ? t : TimeSpan.Zero; }

        /// <summary>The growth stage: 0 nothing, 1 seed, 2 sprout, 3 young plant, 4 ripe.</summary>
        public static int Stage(FieldSave f)
        {
            if (f.crop == "") return 0;
            if (Ripe(f)) return 4;
            float g = Grown(f);
            return g < 0.15f ? 1 : g < 0.45f ? 2 : 3;
        }

        public static string StageName(int stage)
        {
            switch (stage)
            {
                case 1: return "Tohum";
                case 2: return "Filiz";
                case 3: return "Fide";
                case 4: return "Olgun";
                default: return "Boş";
            }
        }

        /// <summary>Harvests a ripe field: the lira it fetched (0: not ripe).</summary>
        public static int Harvest(int i)
        {
            var f = Field(i);
            if (!Ripe(f)) return 0;
            int got = CropOf(f.crop)?.sell ?? 0;
            f.crop = "";
            FreeBuild.Wallet.Earn(got);
            Progress.Save();
            return got;
        }

        public static void HurryField(int i) { var f = Field(i); if (f.crop != "") { f.ripeAt = DateTime.UtcNow.Ticks; Progress.Save(); } }

        public static int RipeCount => Enumerable.Range(0, FieldCount).Count(i => Ripe(Field(i)));

        // ---------- barn and coop ----------

        public static BarnSave BarnSave(string id)
        {
            var b = D.barns.FirstOrDefault(x => x.id == id);
            if (b == null) D.barns.Add(b = new BarnSave { id = id });
            return b;
        }

        public static bool BuyBarn(string id)
        {
            var b = BarnSave(id);
            var def = BarnOf(id);
            if (b.owned || def == null || !FreeBuild.Wallet.Spend(def.price)) return false;
            b.owned = true;
            b.readyAt = (DateTime.UtcNow + TimeSpan.FromMinutes(def.minutes)).Ticks;
            Progress.Save();
            return true;
        }

        public static bool BarnReady(string id) { var b = BarnSave(id); return b.owned && DateTime.UtcNow.Ticks >= b.readyAt; }

        public static TimeSpan BarnLeft(string id) { var t = new DateTime(BarnSave(id).readyAt, DateTimeKind.Utc) - DateTime.UtcNow; return t > TimeSpan.Zero ? t : TimeSpan.Zero; }

        public static int CollectBarn(string id)
        {
            if (!BarnReady(id)) return 0;
            var def = BarnOf(id);
            BarnSave(id).readyAt = (DateTime.UtcNow + TimeSpan.FromMinutes(def.minutes)).Ticks;
            FreeBuild.Wallet.Earn(def.yield);
            Progress.Save();
            return def.yield;
        }

        public static void HurryBarn(string id) { var b = BarnSave(id); if (b.owned) { b.readyAt = DateTime.UtcNow.Ticks; Progress.Save(); } }

        // ---------- for the phone's reminders ----------

        /// <summary>When the next field ripens (null: nothing growing).</summary>
        public static TimeSpan? NextCrop()
        {
            var growing = D.fields.Where(f => f.crop != "").ToList();
            if (growing.Count == 0) return null;
            return growing.Select(Left).Min();
        }

        /// <summary>When the next farm building has produce (null: none owned).</summary>
        public static TimeSpan? NextBarn()
        {
            var owned = D.barns.Where(b => b.owned).ToList();
            if (owned.Count == 0) return null;
            return owned.Select(b => BarnLeft(b.id)).Min();
        }
    }
}
