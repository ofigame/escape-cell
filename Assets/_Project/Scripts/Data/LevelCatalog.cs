using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The built-in 150 levels (15 worlds x 10). Edit the generated LevelSet asset to tune them without code.
    ///
    /// Two separate curves drive the campaign:
    ///   * Novelty is fast: a new mission type, mechanic or hazard every few levels.
    ///   * Difficulty is slow and saw-toothed: it climbs a little through each world, the 9th level is a relaxed
    ///     coin-rain bonus, and the next world starts easier than the previous world ended.
    /// Level 50 plays roughly like the old level 12-15; the peak only arrives around level 120.
    /// </summary>
    public static class LevelCatalog
    {
        public const int LevelsPerWorld = 10;
        public const int WorldCount = 15;

        /// <summary>Shield pickups start appearing from this level (1-based) on.</summary>
        private const int FirstPowerUpLevel = 4;

        public static int WorldOf(int levelIndex) => levelIndex / LevelsPerWorld;

        /// <summary>Localized "WORLD 2 · SUNSET CORAL" label for the world a level belongs to.</summary>
        public static string WorldName(int levelIndex)
        {
            int world = WorldOf(levelIndex);
            return Loc.F("world", world + 1, Loc.T(Visual.WorldTheme.ForWorld(world).key));
        }

        public static List<LevelData> CreateDefault()
        {
            var levels = FirstWorld();
            for (int world = 1; world < WorldCount; world++)
                for (int i = 0; i < LevelsPerWorld; i++)
                    levels.Add(Generated(world, i));

            for (int i = FirstPowerUpLevel - 1; i < levels.Count; i++)
                if (levels[i].mission != MissionType.CoinRain) levels[i].powerUpInterval = 11f;
            return levels;
        }

        /// <summary>
        /// Difficulty 0..1. The world term rises slowly (and keeps rising until world 12),
        /// the level term adds a small climb inside each world, which resets at the next world.
        /// </summary>
        public static float Difficulty(int world, int i)
        {
            float w = Mathf.Clamp01(world / 12f);
            float worldPart = Mathf.Pow(w, 1.35f) * 0.78f;
            float levelPart = Mathf.Clamp01(i / 8f) * 0.22f;
            return Mathf.Clamp01(worldPart + levelPart);
        }

        // Mission rhythm inside a world: variety every level, a breather on the 9th, an escape for the finale.
        private static readonly MissionType[] Rhythm =
        {
            MissionType.CollectCoins, MissionType.Exit, MissionType.Survive, MissionType.Paint,
            MissionType.CollectCoins, MissionType.Exit, MissionType.Paint, MissionType.Survive,
            MissionType.CoinRain, MissionType.Exit,
        };

        private static LevelData Generated(int world, int i)
        {
            float d = Difficulty(world, i);
            // Shift the first eight missions per world so consecutive worlds don't open the same way.
            var mission = i >= 8 ? Rhythm[i] : Rhythm[(i + world) % 8];

            var level = Base(mission, d);
            if (i == 9)
            {
                // The world finale: a notch harder than the rest of the world.
                level.warningTime = Mathf.Max(0.85f, level.warningTime - 0.05f);
                level.blocksPerWave = Mathf.Min(4, level.blocksPerWave + 1);
            }

            level.breakTiles = mission != MissionType.Paint && mission != MissionType.CoinRain;
            level.bombChance = mission == MissionType.CoinRain ? 0f : Mathf.Lerp(0.08f, 0.28f, d);
            level.lineWaveChance = world >= 2 && mission != MissionType.CoinRain ? Mathf.Lerp(0.04f, 0.22f, d) : 0f;
            level.fireChance = world >= 2 ? Mathf.Lerp(0.35f, 0.55f, d) : 0f;
            return level;
        }

        /// <summary>All the numbers that follow from a difficulty value (0 = gentle, 1 = the hardest late levels).</summary>
        private static LevelData Base(MissionType mission, float d)
        {
            int size = d < 0.25f ? 4 : d < 0.65f ? 5 : 6;
            var level = new LevelData
            {
                gridWidth = size,
                gridHeight = size,
                mission = mission,
                warningTime = Mathf.Lerp(1.35f, 0.85f, d),
                spawnInterval = Mathf.Lerp(2.0f, 1.35f, d),
                blocksPerWave = Mathf.Clamp(1 + Mathf.RoundToInt(d * 3f), 1, 4),
                rampUp = Mathf.Lerp(0.2f, 0.5f, d),
                coinTarget = 6 + Mathf.RoundToInt(d * 6f),
                surviveSeconds = Mathf.Round(25f + d * 10f),
                exitDelay = Mathf.Round(7f + d * 7f),
            };

            if (mission == MissionType.CoinRain)
            {
                // A relaxed bonus: coins everywhere, a single slow block now and then.
                level.surviveSeconds = 18f;
                level.blocksPerWave = 1;
                level.spawnInterval = 2.6f;
                level.warningTime = 1.4f;
                level.rampUp = 0f;
                level.coinInterval = 0.45f;
                level.coinLifetime = 3.2f;
                level.maxCoins = 6;
            }
            else if (mission == MissionType.Survive)
            {
                level.coinInterval = 3f;
                level.maxCoins = 1;
            }
            return level;
        }

        /// <summary>World 1: one new thing every level or two, very gentle difficulty.</summary>
        private static List<LevelData> FirstWorld()
        {
            LevelData L(MissionType m, int size, int blocks, float warning, float interval)
            {
                var level = Base(m, 0f);
                level.gridWidth = level.gridHeight = size;
                level.blocksPerWave = blocks;
                level.warningTime = warning;
                level.spawnInterval = interval;
                return level;
            }

            var levels = new List<LevelData>
            {
                L(MissionType.CollectCoins, 3, 1, 1.5f, 2.2f),   // 1: move and grab coins
                L(MissionType.Survive, 3, 1, 1.45f, 2.0f),       // 2: dodge
                L(MissionType.Paint, 3, 1, 1.45f, 2.1f),         // 3: new mission: paint every tile
                L(MissionType.CollectCoins, 4, 1, 1.4f, 2.0f),   // 4: bigger platform, first shield pickups
                L(MissionType.Exit, 4, 1, 1.4f, 2.0f),           // 5: new mission: reach the exit door
                L(MissionType.Survive, 4, 2, 1.45f, 2.3f),       // 6: two blocks at once, but slow
                L(MissionType.CollectCoins, 4, 1, 1.35f, 1.9f),  // 7: holes appear: leap over them
                L(MissionType.Paint, 4, 2, 1.4f, 2.2f),          // 8
                L(MissionType.CoinRain, 4, 1, 1.4f, 2.6f),       // 9: bonus coin rain
                L(MissionType.Exit, 4, 2, 1.35f, 2.0f),          // 10: finale
            };

            levels[0].coinTarget = 5;
            levels[1].surviveSeconds = 20f;
            levels[3].coinTarget = 6;
            levels[4].exitDelay = 6f;
            levels[5].surviveSeconds = 25f;
            levels[6].breakTiles = true;
            levels[6].coinTarget = 7;
            levels[9].breakTiles = true;
            levels[9].exitDelay = 8f;
            return levels;
        }
    }
}
