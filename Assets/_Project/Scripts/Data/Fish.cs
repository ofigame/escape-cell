using System;
using System.Collections.Generic;
using System.Linq;

namespace BrickMuseum.Data
{
    /// <summary>One fish in the cooler.</summary>
    [Serializable]
    public class FishItem
    {
        public string kind;
        public int cm, price;
    }

    /// <summary>A kind of fish: where it lives, how big it grows, what it fetches and how hard it fights.</summary>
    public sealed class FishKind
    {
        public string id, name;
        public bool creek;               // the creek's fish (else the lake's)
        public int minCm, maxCm;
        public float liraPerCm, weight, strength;
    }

    /// <summary>The lake's and the creek's fish, and catching one at random (rarer ones less often).</summary>
    public static class Fish
    {
        public static readonly List<FishKind> Kinds = new List<FishKind>
        {
            new FishKind { id = "hamsi", name = "Hamsi", minCm = 9, maxCm = 14, liraPerCm = 0.5f, weight = 40, strength = 0.6f },
            new FishKind { id = "istavrit", name = "İstavrit", minCm = 12, maxCm = 20, liraPerCm = 0.6f, weight = 30, strength = 0.7f },
            new FishKind { id = "cipura", name = "Çipura", minCm = 20, maxCm = 35, liraPerCm = 0.8f, weight = 15, strength = 1.0f },
            new FishKind { id = "levrek", name = "Levrek", minCm = 25, maxCm = 50, liraPerCm = 0.9f, weight = 10, strength = 1.2f },
            new FishKind { id = "lufer", name = "Lüfer", minCm = 30, maxCm = 55, liraPerCm = 1.2f, weight = 5, strength = 1.5f },
            new FishKind { id = "alabalik", name = "Alabalık", creek = true, minCm = 18, maxCm = 40, liraPerCm = 0.9f, weight = 35, strength = 1.0f },
            new FishKind { id = "kefal", name = "Kefal", creek = true, minCm = 20, maxCm = 35, liraPerCm = 0.5f, weight = 30, strength = 0.8f },
            new FishKind { id = "sazan", name = "Sazan", creek = true, minCm = 30, maxCm = 60, liraPerCm = 0.6f, weight = 25, strength = 1.3f },
            new FishKind { id = "turna", name = "Turna", creek = true, minCm = 45, maxCm = 80, liraPerCm = 1.0f, weight = 10, strength = 1.7f },
        };

        public static FishKind Find(string id) => Kinds.FirstOrDefault(k => k.id == id);

        public static FishKind Pick(bool creek, Random r)
        {
            var pool = Kinds.Where(k => k.creek == creek).ToList();
            float total = pool.Sum(k => k.weight), roll = (float)r.NextDouble() * total;
            foreach (var k in pool) { roll -= k.weight; if (roll <= 0) return k; }
            return pool[pool.Count - 1];
        }

        /// <summary>A caught fish: a size (big ones rarer) and its price.</summary>
        public static FishItem Catch(FishKind k, Random r)
        {
            float t = (float)Math.Pow(r.NextDouble(), 1.6);
            int cm = k.minCm + (int)Math.Round(t * (k.maxCm - k.minCm));
            return new FishItem { kind = k.id, cm = cm, price = Math.Max(2, (int)Math.Round(cm * k.liraPerCm)) };
        }
    }
}
