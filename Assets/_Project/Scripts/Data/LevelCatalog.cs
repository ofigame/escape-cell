using System.Collections.Generic;
using SquashBot.Core;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The built-in 150 levels (15 worlds x 10). Edit the generated LevelSet asset to tune them without code.
    ///
    /// Two separate curves drive the campaign:
    ///   * Novelty is fast: a new mission type, mechanic, platform shape or hazard every few levels.
    ///   * Difficulty is slow and never goes down: every level is at least as hard as the one before it.
    ///     Breathers live outside the campaign, in the bonus rounds that stars unlock.
    /// </summary>
    public static class LevelCatalog
    {
        public const int LevelsPerWorld = 10;
        public const int WorldCount = 15;
        public const int LevelCount = LevelsPerWorld * WorldCount;

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
            for (int index = LevelsPerWorld; index < LevelCount; index++)
                levels.Add(Generated(index));

            for (int i = FirstPowerUpLevel - 1; i < levels.Count; i++)
                levels[i].powerUpInterval = 11f;
            return levels;
        }

        /// <summary>
        /// Difficulty 0..1, rising a little with every single level and never falling.
        /// Gentle early on (level 50 ≈ 0.27), the steep part only arrives in the last worlds.
        /// </summary>
        public static float Difficulty(int index) => Mathf.Pow(Mathf.Clamp01(index / (float)(LevelCount - 1)), 1.2f);

        // Mission rhythm inside a world: variety every level, coin rain on the 9th, an escape for the finale.
        private static readonly MissionType[] Rhythm =
        {
            MissionType.CollectCoins, MissionType.Exit, MissionType.Survive, MissionType.Paint,
            MissionType.CollectCoins, MissionType.Exit, MissionType.Paint, MissionType.Survive,
            MissionType.CoinRain, MissionType.Exit,
        };

        private static LevelData Generated(int index)
        {
            int world = WorldOf(index);
            int i = index % LevelsPerWorld;
            float d = Difficulty(index);
            // Shift the first eight missions per world so consecutive worlds don't open the same way.
            var mission = i >= 8 ? Rhythm[i] : Rhythm[(i + world) % 8];

            var level = Base(mission, d);
            // Hazards only ever get added: holes from level 7, bombs from world 2, lines and fire from world 3.
            level.breakTiles = true;
            level.bombChance = Mathf.Lerp(0.08f, 0.28f, d);
            level.lineWaveChance = world >= 2 ? Mathf.Lerp(0.04f, 0.22f, d) : 0f;
            level.fireChance = world >= 2 ? Mathf.Lerp(0.35f, 0.55f, d) : 0f;
            Shape(level, index, d);
            return level;
        }

        /// <summary>
        /// Gives a generated level its platform outline and stone pillars. Seeded by the level index, so a level
        /// always looks the same. More outlines unlock and pillars multiply as the campaign goes on.
        /// </summary>
        private static void Shape(LevelData level, int index, float d)
        {
            var rng = new System.Random(index * 7919 + 17);
            var pool = WorldOf(index) < 3
                ? new[] { PlatformShape.Square, PlatformShape.L, PlatformShape.Step, PlatformShape.T, PlatformShape.U }
                : new[] { PlatformShape.Square, PlatformShape.L, PlatformShape.Step, PlatformShape.T, PlatformShape.U, PlatformShape.Plus, PlatformShape.Ring };
            var shape = pool[rng.Next(pool.Length)];
            int pillars = Mathf.Clamp(Mathf.RoundToInt(d * 3f + 0.6f), 1, 3);
            level.layout = Layouts.Generate(level.gridWidth, shape, pillars, index);
        }

        /// <summary>All the numbers that follow from a difficulty value (0 = gentle, 1 = the hardest late levels).</summary>
        private static LevelData Base(MissionType mission, float d)
        {
            int size = d < 0.2f ? 4 : d < 0.55f ? 5 : 6;
            var level = new LevelData
            {
                gridWidth = size,
                gridHeight = size,
                mission = mission,
                warningTime = Mathf.Lerp(1.38f, 0.85f, d),
                spawnInterval = Mathf.Lerp(1.85f, 1.3f, d),
                blocksPerWave = Mathf.Clamp(1 + Mathf.RoundToInt(d * 3f), 1, 4),
                rampUp = Mathf.Lerp(0.2f, 0.5f, d),
                coinTarget = 6 + Mathf.RoundToInt(d * 6f),
                surviveSeconds = Mathf.Round(25f + d * 10f),
                keys = 1 + Mathf.RoundToInt(d * 2f),
            };

            if (mission == MissionType.CoinRain)
            {
                // Coins everywhere, the usual hazards for this point of the campaign, and a target to beat the clock.
                level.surviveSeconds = 20f;
                level.coinTarget = 10 + Mathf.RoundToInt(d * 8f);
                level.coinInterval = 0.45f;
                level.coinLifetime = 3.2f;
                level.maxCoins = 5;
            }
            else if (mission == MissionType.Survive)
            {
                level.coinInterval = 3f;
                level.maxCoins = 1;
            }
            return level;
        }

        /// <summary>World 1: one new thing every level or two, and every number a hair tougher than the last.</summary>
        private static List<LevelData> FirstWorld()
        {
            LevelData L(MissionType m, int size, float warning, float interval)
            {
                var level = Base(m, 0f);
                level.gridWidth = level.gridHeight = size;
                level.blocksPerWave = 1;
                level.warningTime = warning;
                level.spawnInterval = interval;
                level.rampUp = 0.2f;
                return level;
            }

            var levels = new List<LevelData>
            {
                L(MissionType.CollectCoins, 3, 1.5f, 2.2f),    // 1: move and grab coins
                L(MissionType.Survive, 3, 1.49f, 2.15f),       // 2: dodge
                L(MissionType.Paint, 3, 1.48f, 2.1f),          // 3: new mission: paint every tile (first L shape)
                L(MissionType.CollectCoins, 4, 1.46f, 2.05f),  // 4: bigger platform, first shield pickups
                L(MissionType.Exit, 4, 1.45f, 2.0f),           // 5: new mission: keys, then the door
                L(MissionType.Survive, 4, 1.43f, 1.97f),       // 6: first stone pillar
                L(MissionType.CollectCoins, 4, 1.42f, 1.94f),  // 7: holes appear: leap over them
                L(MissionType.Paint, 4, 1.41f, 1.91f),         // 8
                L(MissionType.CoinRain, 4, 1.4f, 1.88f),       // 9: coin rain against the clock
                L(MissionType.Exit, 4, 1.39f, 1.86f),          // 10: finale
            };

            levels[0].coinTarget = 5;
            levels[1].surviveSeconds = 20f;
            levels[3].coinTarget = 6;
            levels[5].surviveSeconds = 25f;
            levels[6].coinTarget = 6;
            for (int i = 6; i < levels.Count; i++) levels[i].breakTiles = true;

            // Shapes arrive gently: two plain squares, then a new outline every level or two, the first pillars late.
            var shapes = new[]
            {
                PlatformShape.Square, PlatformShape.Square, PlatformShape.L, PlatformShape.Square, PlatformShape.T,
                PlatformShape.L, PlatformShape.Step, PlatformShape.Square, PlatformShape.Square, PlatformShape.T,
            };
            int[] pillars = { 0, 0, 0, 0, 0, 1, 0, 1, 1, 1 };
            for (int i = 0; i < levels.Count; i++)
                if (shapes[i] != PlatformShape.Square || pillars[i] > 0)
                    levels[i].layout = Layouts.Generate(levels[i].gridWidth, shapes[i], pillars[i], 1000 + i);
            return levels;
        }

        /// <summary>
        /// A bonus treasure vault: a big platform raining coins for 25 seconds with only a few slow blocks.
        /// Free to play, nothing to lose; the shape changes every time.
        /// </summary>
        public static LevelData Treasure(int seed)
        {
            var rng = new System.Random(seed);
            var shapes = new[] { PlatformShape.Square, PlatformShape.Plus, PlatformShape.Ring, PlatformShape.T };
            var level = new LevelData
            {
                gridWidth = 5,
                gridHeight = 5,
                mission = MissionType.Treasure,
                surviveSeconds = 25f,
                warningTime = 1.5f,
                spawnInterval = 2.4f,
                blocksPerWave = 1,
                aimAtPlayerChance = 0.25f,
                coinInterval = 0.3f,
                coinLifetime = 3f,
                maxCoins = 7,
            };
            level.layout = Layouts.Generate(5, shapes[rng.Next(shapes.Length)], 0, seed);
            return level;
        }
    }
}
