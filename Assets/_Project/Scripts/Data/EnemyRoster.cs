using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The enemy robots of the 25 worlds: each world has its own two — a basic one from its first floor and an advanced
    /// one from its sixth — and the robots of every world before it keep coming too (a world's own make up about 30% of a
    /// floor's crowd, the rest are spread over the earlier worlds'). A robot has health points, a blow (health points off
    /// foi or a soldier) and armour (the share of a blow it takes off):
    /// the first world's basic robot 20 / 2 / 5%, its advanced one 30 / 3 / 10%; the last world's 120 / 9 / 30% and
    /// 160 / 12 / 35%, the worlds between in step. A robot keeps growing in the worlds after its own (the first world's is
    /// twice as strong by the last), and a little floor by floor within a world. vanG is every world's strongest: 100
    /// health points in the first world up to 1500 in the last, more on every floor of a world.
    /// </summary>
    public static class EnemyRoster
    {
        public struct Type
        {
            public int world;
            public bool advanced;
            /// <summary>The look's number (GuardBot style): world × 2, +1 the advanced one.</summary>
            public int Style => world * 2 + (advanced ? 1 : 0);
        }

        public struct Stats
        {
            public int hp, damage;
            public float armor;
        }

        public const int Worlds = 25;

        /// <summary>A robot's strength on a floor of world <paramref name="current"/> (its <paramref name="inWorld"/>th floor, 0..9).</summary>
        public static Stats For(Type t, int current, int inWorld)
        {
            float k = Mathf.Clamp01(t.world / (float)(Worlds - 1));
            float hp = t.advanced ? Mathf.Lerp(30f, 160f, k) : Mathf.Lerp(20f, 120f, k);
            float dmg = t.advanced ? Mathf.Lerp(3f, 12f, k) : Mathf.Lerp(2f, 9f, k);
            float armor = t.advanced ? Mathf.Lerp(0.1f, 0.35f, k) : Mathf.Lerp(0.05f, 0.3f, k);
            // Older worlds' robots grow on (the first world's twice as strong by the last world), and every floor a little.
            float older = 1f + Mathf.Clamp(current - t.world, 0, Worlds - 1) / (float)(Worlds - 1);
            float floor = 1f + 0.02f * Mathf.Clamp(inWorld, 0, 9);
            return new Stats
            {
                hp = Mathf.Max(1, Mathf.RoundToInt(hp * older * floor)),
                damage = Mathf.Max(1, Mathf.RoundToInt(dmg * older * floor)),
                armor = Mathf.Min(0.6f, armor * (1f + (older - 1f) * 0.5f)),
            };
        }

        /// <summary>
        /// A robot for a floor of world <paramref name="world"/>: about three in ten of the world's own (its advanced one only
        /// from the sixth floor), the rest from the earlier worlds (basic or advanced alike).
        /// </summary>
        public static Type Pick(System.Random rng, int world, int inWorld)
        {
            world = Mathf.Clamp(world, 0, Worlds - 1);
            bool own = world == 0 || rng.NextDouble() < 0.3;
            int w = own ? world : rng.Next(world);
            bool advanced = (w < world || inWorld >= 5) && rng.Next(2) == 0;
            return new Type { world = w, advanced = advanced };
        }

        /// <summary>vanG's health points: 180 in the first world up to 2700 in the last, five per cent more each floor of a world
        /// (a real fight: foi's powers and bombs are strong now).</summary>
        public static int VanGHp(int world, int inWorld) =>
            Mathf.RoundToInt(Mathf.Lerp(180f, 2700f, Mathf.Clamp01(world / (float)(Worlds - 1))) * (1f + 0.05f * Mathf.Clamp(inWorld, 0, 9)));

        /// <summary>vanG's blow: twice the world's hardest robot's.</summary>
        public static int VanGDamage(int world, int inWorld) => 2 * For(new Type { world = world, advanced = true }, world, inWorld).damage;

        /// <summary>The special ones (enforcers, warlords): half again an advanced robot of the world.</summary>
        public static Stats Special(int world, int inWorld, float times = 1.5f)
        {
            var s = For(new Type { world = Mathf.Clamp(world, 0, Worlds - 1), advanced = true }, world, inWorld);
            return new Stats { hp = Mathf.RoundToInt(s.hp * times), damage = Mathf.RoundToInt(s.damage * times), armor = Mathf.Min(0.6f, s.armor + 0.05f) };
        }

        /// <summary>A world's colour for its robots' plates.</summary>
        public static Color Plate(int world) => Color.HSVToRGB(Mathf.Repeat(world * 0.21f + 0.55f, 1f), 0.75f, 1f);
    }
}
