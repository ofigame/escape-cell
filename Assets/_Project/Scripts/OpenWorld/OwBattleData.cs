using UnityEngine;

namespace SquashBot.OpenWorld
{
    /// <summary>What foi can raise round its farm: fences, walls, a gate, towers, cannons, a watchtower.</summary>
    public enum DefenceKind { Fence, Wall, Gate, ArcherTower, FireTower, Cannon, HeavyCannon, Watchtower }

    public enum BanditKind { Raider, Archer, Bomber, SiegeCart, Chief }

    public struct DefenceDef
    {
        public DefenceKind kind;
        public int price, hp, damage, house;
        public float range, cooldown, splash, radius;
        /// <summary>Stops bandits (they must break it) — walls, fences, the gate.</summary>
        public bool blocks;
    }

    public struct BanditDef
    {
        public BanditKind kind;
        public int hp, damage, reward;
        public float speed, range, cooldown, splash;
    }

    /// <summary>The defences' and the bandits' numbers (Docs/AcikDunya_Tasarim.md §7–8).</summary>
    public static class OwBattleData
    {
        public static readonly DefenceDef[] Defences =
        {
            new DefenceDef { kind = DefenceKind.Fence, price = 5, hp = 60, radius = 0.9f, blocks = true, house = 0 },
            new DefenceDef { kind = DefenceKind.Wall, price = 20, hp = 300, radius = 1f, blocks = true, house = 1 },
            new DefenceDef { kind = DefenceKind.Gate, price = 80, hp = 400, radius = 1.2f, blocks = true, house = 1 },
            new DefenceDef { kind = DefenceKind.ArcherTower, price = 400, hp = 250, damage = 12, range = 7f, cooldown = 1f, radius = 1.2f, house = 0 },
            new DefenceDef { kind = DefenceKind.FireTower, price = 1200, hp = 300, damage = 9, range = 5f, cooldown = 1.2f, splash = 1.6f, radius = 1.2f, house = 2 },
            new DefenceDef { kind = DefenceKind.Cannon, price = 900, hp = 220, damage = 40, range = 12f, cooldown = 3f, splash = 2f, radius = 1.1f, house = 1 },
            new DefenceDef { kind = DefenceKind.HeavyCannon, price = 3000, hp = 350, damage = 80, range = 15f, cooldown = 4f, splash = 2.6f, radius = 1.4f, house = 3 },
            new DefenceDef { kind = DefenceKind.Watchtower, price = 600, hp = 200, radius = 1f, house = 1 },
        };

        public static readonly BanditDef[] Bandits =
        {
            new BanditDef { kind = BanditKind.Raider, hp = 60, damage = 6, speed = 3.2f, range = 1.3f, cooldown = 0.9f, reward = 12 },
            new BanditDef { kind = BanditKind.Archer, hp = 45, damage = 8, speed = 2.6f, range = 7f, cooldown = 1.4f, reward = 14 },
            new BanditDef { kind = BanditKind.Bomber, hp = 40, damage = 120, speed = 3.6f, range = 1.1f, cooldown = 1f, splash = 2.2f, reward = 16 },
            new BanditDef { kind = BanditKind.SiegeCart, hp = 400, damage = 50, speed = 1.3f, range = 1.8f, cooldown = 2.5f, reward = 40 },
            new BanditDef { kind = BanditKind.Chief, hp = 800, damage = 22, speed = 2.4f, range = 1.8f, cooldown = 1.2f, splash = 1.5f, reward = 200 },
        };

        public static DefenceDef Of(DefenceKind k) => Defences[(int)k];
        public static BanditDef Of(BanditKind k) => Bandits[(int)k];

        public const int MaxLevel = 3;

        /// <summary>A level adds two fifths to damage and health.</summary>
        public static float LevelScale(int level) => 1f + 0.4f * (Mathf.Clamp(level, 1, MaxLevel) - 1);

        public static int UpgradePrice(DefenceDef d, int level) => d.price * level;

        /// <summary>How far round the farm's middle defences may stand.</summary>
        public static float Reach(int land) => land * 0.5f + 16f;

        // ---------- Raids ----------

        public const float WarningHours = 2f, WatchtowerExtra = 0.5f, ProtectHours = 6f;
        public const int RaidsPerDay = 2;
        /// <summary>A live raid lasts at most this long; bandits still standing then loot and go.</summary>
        public const float RaidSeconds = 180f;
        /// <summary>What a lost raid takes: a share of the barn, the crops in the ground.</summary>
        public const float LootShare = 0.3f;
    }
}
