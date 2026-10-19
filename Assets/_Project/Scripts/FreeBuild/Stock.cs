using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Data;

namespace BrickMuseum.FreeBuild
{
    /// <summary>A kind of piece the player can build freely with (colour is free, the stock counts kinds).</summary>
    public sealed class BuildKind
    {
        public string key, t, name, group;
        public int w, d, h;
        /// <summary>Special materials are bought with lira: this many pieces for this price (0 = only earned by building the city).</summary>
        public int price, pack;
        /// <summary>A piece of this kind in its canonical orientation (for pictures and building).</summary>
        public Piece Make(string colour, int x = 0, int z = 0, int y = 0, int rot = 0) =>
            Piece.Make(t, colour, x, z, y, w, d, h, Dirs[rot & 3]);
        private static readonly string[] Dirs = { "E", "N", "W", "S" };
    }

    /// <summary>
    /// The materials for free building. A starter kit, then every model finished in the city puts part of its pieces into
    /// the box (more with more stars): building the town is how the house grows. Doors, windows, stairs and railings
    /// come from houses, public buildings and landmarks.
    /// </summary>
    public static class Stock
    {
        private static List<BuildKind> kinds;

        public static List<BuildKind> Kinds => kinds ??= MakeKinds();

        public static BuildKind Find(string key) => Kinds.FirstOrDefault(k => k.key == key);

        private static List<BuildKind> MakeKinds()
        {
            var l = new List<BuildKind>();
            void K(string group, string t, int w, int d, int h, string name, int price = 0, int pack = 0) =>
                l.Add(new BuildKind { key = $"{t}:{w}x{d}x{h}", t = t, w = w, d = d, h = h, name = name, group = group, price = price, pack = pack });
            foreach (var (w, d) in new[] { (1, 1), (2, 1), (3, 1), (4, 1), (6, 1), (8, 1), (2, 2), (3, 2), (4, 2), (6, 2), (8, 2) })
                K("Tuğla", "brick", w, d, 3, $"{d}×{w} tuğla");
            foreach (var (w, d) in new[] { (2, 1), (4, 1), (2, 2), (4, 2), (6, 2), (8, 2), (4, 4), (8, 4), (8, 8) })
                K("Plaka", "brick", w, d, 1, $"{d}×{w} plaka");
            K("Özel", "door", 3, 1, 9, "Kapı", 40, 1);
            K("Özel", "window", 2, 1, 6, "Pencere", 40, 2);
            K("Özel", "window", 4, 1, 6, "Geniş pencere", 50, 1);
            K("Özel", "stairs", 10, 2, 10, "Merdiven", 80, 1);
            K("Özel", "fence", 2, 1, 3, "Korkuluk", 20, 2);
            K("Özel", "fence", 4, 1, 3, "Uzun korkuluk", 25, 1);
            K("Çatı", "slope", 2, 1, 3, "Eğimli 1×2");
            K("Çatı", "slope", 2, 2, 3, "Eğimli 2×2");
            K("Çatı", "slope", 2, 4, 3, "Eğimli 2×4");
            K("Çatı", "curve", 2, 2, 3, "Kavisli 2×2");
            K("Süs", "tile", 2, 2, 1, "Düz plaka 2×2", 20, 4);
            K("Süs", "tile", 4, 1, 1, "Düz plaka 1×4", 20, 4);
            K("Süs", "round", 1, 1, 3, "Yuvarlak 1×1", 20, 4);
            K("Süs", "round", 2, 2, 3, "Yuvarlak 2×2", 25, 2);
            K("Süs", "cone", 1, 1, 3, "Koni", 20, 4);
            K("Süs", "dome", 2, 2, 2, "Kubbe", 25, 2);
            K("Araba", "wheel", 2, 1, 3, "Tekerlek", 60, 4);
            K("Araba", "taxisign", 2, 1, 3, "Taksi tabelası", 100, 1);
            K("Bahçe", "picket", 2, 1, 3, "Çit", 15, 2);
            K("Bahçe", "hedge", 2, 1, 3, "Çalı", 15, 2);
            K("Bahçe", "pool", 4, 4, 1, "Havuz", 120, 1);
            K("Bahçe", "gtree", 2, 2, 8, "Süs ağacı", 40, 1);
            K("Bahçe", "flowers", 1, 1, 1, "Çiçek", 10, 4);
            K("Bahçe", "stones", 2, 2, 1, "Taş yol", 10, 4);
            K("Bahçe", "glamp", 1, 1, 6, "Bahçe lambası", 30, 1);
            return l;
        }

        // ---------- counts ----------

        public static int Count(string key) => Progress.Data.stock.FirstOrDefault(s => s.kind == key)?.count ?? 0;

        public static int Total => Progress.Data.stock.Sum(s => s.count);

        public static void Add(string key, int n)
        {
            if (n == 0 || Find(key) == null) return;
            var s = Progress.Data.stock.FirstOrDefault(i => i.kind == key);
            if (s == null) Progress.Data.stock.Add(s = new StockItem { kind = key });
            s.count = System.Math.Max(0, s.count + n);
        }

        public static bool Take(string key) { if (Count(key) <= 0) return false; Add(key, -1); return true; }

        /// <summary>Buys one pack of a special material; false when it is not for sale or there is not enough money.</summary>
        public static bool Buy(BuildKind k)
        {
            if (k.price <= 0 || !Wallet.Spend(k.price)) return false;
            Add(k.key, k.pack);
            Progress.Save();
            return true;
        }

        // ---------- earning ----------

        /// <summary>The first time: the starter kit, plus materials for every model already finished.</summary>
        public static void EnsureStarted()
        {
            var data = Progress.Data;
            if (data.stockInit) return;
            foreach (var (key, n) in new[]
            {
                ("brick:2x1x3", 60), ("brick:4x1x3", 50), ("brick:2x2x3", 30), ("brick:4x2x3", 40), ("brick:1x1x3", 30), ("brick:6x1x3", 20),
                ("brick:4x2x1", 20), ("brick:8x4x1", 12), ("brick:8x8x1", 4),
                ("door:3x1x9", 2), ("window:2x1x6", 4), ("stairs:10x2x10", 1), ("fence:2x1x3", 4),
                ("slope:2x2x3", 16), ("slope:2x4x3", 10), ("wheel:2x1x3", 4), ("taxisign:2x1x3", 1),   // enough for a first taxi
            })
                Add(key, n);
            foreach (var st in data.models)
            {
                var m = ModelCatalog.Find(st.id);
                if (m != null && st.stars > 0) foreach (var kv in Reward(m, st.stars)) Add(kv.Key, kv.Value);
            }
            data.stockInit = true;
            Wallet.EnsureStarted();
            Progress.Save();
        }

        /// <summary>What a finished model puts into the box: 30 / 45 / 60 % of its rough pieces (bricks, plates, roof pieces) by stars.</summary>
        public static Dictionary<string, int> Reward(ModelDef m, int stars)
        {
            float share = stars >= 3 ? 0.6f : stars == 2 ? 0.45f : 0.3f;
            var raw = new Dictionary<string, float>();
            void Put(string key, float n) { raw.TryGetValue(key, out var v); raw[key] = v + n; }
            foreach (var p in m.pieces)
            {
                int a = System.Math.Max(p.w, p.d), b = System.Math.Min(p.w, p.d);
                switch (p.t)
                {
                    case "brick":
                        string key = p.h == 1 ? $"brick:{a}x{b}x1" : $"brick:{a}x{b}x3";
                        if (Find(key) != null) Put(key, 1);
                        else if (p.h == 1) Put("brick:4x2x1", System.Math.Max(1, a * b / 8f));
                        else Put("brick:2x1x3", System.Math.Max(1, a * b * p.h / 6f));
                        break;
                    case "slope": Put(a >= 4 ? "slope:2x4x3" : b >= 2 ? "slope:2x2x3" : "slope:2x1x3", 1); break;
                    case "curve": Put("curve:2x2x3", 1); break;
                }
            }
            var gift = new Dictionary<string, int>();
            foreach (var kv in raw)
            {
                int n = (int)System.Math.Round(kv.Value * share);
                if (n > 0) gift[kv.Key] = n;
            }
            return gift;
        }

        /// <summary>Puts a finished model's materials into the box; returns how many pieces it gave.</summary>
        public static int Earn(ModelDef m, int stars)
        {
            EnsureStarted();
            int total = 0;
            foreach (var kv in Reward(m, stars)) { Add(kv.Key, kv.Value); total += kv.Value; }
            Progress.Save();
            return total;
        }
    }
}

namespace BrickMuseum.FreeBuild
{
    /// <summary>The player's lira (150 to start with).</summary>
    public static class Wallet
    {
        public static int Money => Progress.Data.money;

        public static void EnsureStarted()
        {
            var d = Progress.Data;
            if (d.moneyInit) return;
            d.moneyInit = true;
            d.money += 150;
            Progress.Save();
        }

        public static void Earn(int n) { EnsureStarted(); Progress.Data.money += n; Progress.Save(); }

        public static bool Spend(int n)
        {
            EnsureStarted();
            if (Progress.Data.money < n) return false;
            Progress.Data.money -= n;
            Progress.Save();
            return true;
        }
    }
}
