using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>The built-in 150 levels (15 worlds x 10). Edit the generated LevelSet asset to tune them without code.</summary>
    public static class LevelCatalog
    {
        /// <summary>Shield pickups start appearing from this level (1-based) on.</summary>
        private const int FirstPowerUpLevel = 4;

        public const int LevelsPerWorld = 10;
        public const int WorldCount = 15;

        public static int WorldOf(int levelIndex) => levelIndex / LevelsPerWorld;

        /// <summary>Localized "WORLD 2 · SUNSET CORAL" label for the world a level belongs to.</summary>
        public static string WorldName(int levelIndex)
        {
            int world = WorldOf(levelIndex);
            return Loc.F("world", world + 1, Loc.T(Visual.WorldTheme.ForWorld(world).key));
        }

        public static List<LevelData> CreateDefault()
        {
            var levels = CreateBase();
            for (int world = 1; world < WorldCount; world++)
                for (int i = 0; i < LevelsPerWorld; i++)
                    levels.Add(Generated(world, i));

            for (int i = FirstPowerUpLevel - 1; i < levels.Count; i++)
                levels[i].powerUpInterval = 11f;
            return levels;
        }

        /// <summary>
        /// Worlds 2-15. The pressure rises gently and then levels off, so late worlds stay tough but fair:
        /// warnings never drop below 0.8 s, at most 4 blocks fall at once, and bombs / row waves stay occasional.
        /// Variety (fire, bombs, row waves) grows faster than raw speed, so progress feels new rather than just harder.
        /// Within a world the difficulty still climbs from the first to the tenth level.
        /// </summary>
        private static LevelData Generated(int world, int i)
        {
            float t = i / (float)(LevelsPerWorld - 1);
            float w = Mathf.Clamp01(world / 10f); // 0 → 1 over the first ten worlds, then flat
            int size = world == 1 ? 4 : world < 5 ? 5 : (i >= 5 || world >= 10 ? 6 : 5);
            int blocks = Mathf.Min(4, Mathf.Min(size * size / 6, 2 + world / 4 + (i >= 8 ? 1 : 0)));
            float warning = Mathf.Max(0.8f, Mathf.Lerp(1.2f, 1.05f, t) - w * 0.25f);
            float interval = Mathf.Max(1.35f, Mathf.Lerp(1.9f, 1.6f, t) - w * 0.25f);
            float ramp = Mathf.Lerp(0.35f, 0.6f, w);

            var level = i % 2 == 0
                ? Collect(size, 10 + world / 2 + i / 2, warning, interval, blocks, breakTiles: true)
                : Survive(size, 30f + Mathf.Min(world * 2f, 20f) + i, warning, interval, blocks, ramp, breakTiles: true);
            level.maxCoins = i % 2 == 0 ? 2 : 1;
            level.lineWaveChance = world >= 2 ? Mathf.Min(0.25f, 0.06f * (world - 1)) : 0f;
            level.bombChance = Mathf.Min(0.3f, 0.1f + 0.02f * world); // bombs from world 2 on
            level.fireChance = world >= 2 ? Mathf.Min(0.6f, 0.35f + 0.03f * world) : 0f; // fire from world 3 on
            return level;
        }
        private static List<LevelData> CreateBase()
        {
            return new List<LevelData>
            {
                // 1-3: 3x3 tutorial
                Collect(3, 5, warning: 1.5f, interval: 2.2f, blocks: 1),
                Survive(3, 20f, warning: 1.4f, interval: 1.9f, blocks: 1, ramp: 0.3f),
                Collect(3, 8, warning: 1.25f, interval: 1.6f, blocks: 1),

                // 4-6: 4x4, two blocks at once
                Survive(4, 30f, warning: 1.25f, interval: 1.5f, blocks: 1, ramp: 0.4f),
                Collect(4, 10, warning: 1.2f, interval: 1.9f, blocks: 2),
                Survive(4, 40f, warning: 1.05f, interval: 1.8f, blocks: 2, ramp: 0.4f),

                // 7-9: breaking tiles
                Collect(4, 12, warning: 1.15f, interval: 1.7f, blocks: 1, breakTiles: true),
                Survive(4, 45f, warning: 1.05f, interval: 1.8f, blocks: 2, ramp: 0.5f, breakTiles: true),
                Collect(4, 15, warning: 0.95f, interval: 1.7f, blocks: 2, breakTiles: true),

                // 10: 5x5 finale
                Survive(5, 60f, warning: 0.9f, interval: 1.7f, blocks: 3, ramp: 0.6f, breakTiles: true),
            };
        }

        private static LevelData Collect(int size, int coins, float warning, float interval, int blocks, bool breakTiles = false)
        {
            return new LevelData
            {
                gridWidth = size,
                gridHeight = size,
                mission = MissionType.CollectCoins,
                coinTarget = coins,
                warningTime = warning,
                spawnInterval = interval,
                blocksPerWave = blocks,
                breakTiles = breakTiles,
            };
        }

        private static LevelData Survive(int size, float seconds, float warning, float interval, int blocks, float ramp, bool breakTiles = false)
        {
            return new LevelData
            {
                gridWidth = size,
                gridHeight = size,
                mission = MissionType.Survive,
                surviveSeconds = seconds,
                warningTime = warning,
                spawnInterval = interval,
                blocksPerWave = blocks,
                rampUp = ramp,
                breakTiles = breakTiles,
                coinInterval = 3f,
                maxCoins = 1,
            };
        }
    }
}
