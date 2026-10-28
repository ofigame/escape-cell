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
    public static partial class LevelCatalog
    {
        public const int LevelsPerWorld = 10;
        public const int WorldCount = 25;
        public const int LevelCount = LevelsPerWorld * WorldCount;

        /// <summary>Shield pickups start appearing from this level (1-based) on.</summary>
        private const int FirstPowerUpLevel = 4;

        public static int WorldOf(int levelIndex) => levelIndex / LevelsPerWorld;

        /// <summary>The Loc key of a world's name ("world.ocean").</summary>
        public static string WorldKey(int world) => Visual.WorldTheme.ForWorld(world).key;

        /// <summary>Localized "WORLD 2 · SUNSET CORAL" label for the world a level belongs to.</summary>
        public static string WorldName(int levelIndex)
        {
            int world = WorldOf(levelIndex);
            return Loc.F("world", world + 1, Loc.T(Visual.WorldTheme.ForWorld(world).key));
        }

        public static List<LevelData> CreateDefault()
        {
            // Every level is the same loop (crates, bugs and robots, then the monster), growing with the campaign;
            // the cards still give each level its number and helper (see LevelCatalogHunt).
            var levels = new List<LevelData>();
            foreach (var card in LevelScript.Cards) levels.Add(Hunt(card));
            return levels;
        }

        /// <summary>
        /// Difficulty 0..1, rising a little with every single level and never falling.
        /// Steeper than a plain line early on, so the middle of the campaign asks for a few tries per level.
        /// </summary>
        public static float Difficulty(int index) => Mathf.Pow(Mathf.Clamp01(index / (float)(LevelCount - 1)), 0.85f);

        // Mission rhythm inside a world: three story quests on big floors, two journeys to an exit, a monster fight,
        // arenas in between, coin rain, then the boss.
        private static readonly MissionType[] Rhythm =
        {
            MissionType.Quest, MissionType.Exit, MissionType.CollectCoins, MissionType.Quest,
            MissionType.Paint, MissionType.Exit, MissionType.Quest, MissionType.Monster,
            MissionType.CoinRain, MissionType.Boss,
        };

        /// <summary>The kinds of Exit level, and the level index (0-based) from which each can appear.</summary>
        private enum Journey { Arena, Corridor, Rooms, Collapse, Maze, Chase }

        private static readonly (Journey kind, int from)[] JourneyUnlocks =
        {
            (Journey.Arena, 0), (Journey.Corridor, 9), (Journey.Rooms, 23), (Journey.Collapse, 38), (Journey.Maze, 53), (Journey.Chase, 68),
        };

        /// <summary>
        /// From the Red Canyon (floor 6, level 51) each floor brings the rule its story tells of: mine barrels, the
        /// desert wind, the old teleport gates, sea currents, sticky seaweed and jellyfish, server lasers, ice, snow, thin ice (glass),
        /// crystal lasers, poison, cloud trampolines, storm blinks, hunting blocks and the darkness of the Star Road.
        /// vanG's Heart (the last floor) is dark and adds one more rule met on the way.
        /// </summary>
        private static readonly FloorRule[] Signature =
        {
            FloorRule.None, FloorRule.None, FloorRule.None, FloorRule.None, FloorRule.None, // Channel and Forest worlds
            FloorRule.Barrel, FloorRule.Wind, FloorRule.Teleport,                                // Red Canyon
            FloorRule.Current, FloorRule.Sticky | FloorRule.Barrel, FloorRule.Laser | FloorRule.Current, // Sea Floor (the barrels are jellyfish there)
            FloorRule.Ice, FloorRule.Ice | FloorRule.Sticky, FloorRule.Glass,                     // Snowy Mountain
            FloorRule.Laser | FloorRule.Teleport, FloorRule.Glass | FloorRule.Teleport, FloorRule.Poison, // Crystal Cave
            FloorRule.Trampoline, FloorRule.Trampoline | FloorRule.Wind, FloorRule.Wind | FloorRule.Blink,
            FloorRule.Hunter | FloorRule.Trampoline,                                             // Cloud Bridge
            FloorRule.Blink | FloorRule.Laser, FloorRule.Dark, FloorRule.Dark | FloorRule.Trampoline, // Star Road
            FloorRule.Dark,                                                                      // vanG's Heart (+ one more)
        };

        public const int FirstRuleWorld = 5;

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
            int pillars = Mathf.Clamp(Mathf.RoundToInt(level.gridWidth * level.gridWidth * 0.025f + d * 2f), 1, 8);
            level.layout = Layouts.Generate(level.gridWidth, shape, pillars, index);
            level.shapeName = shape.ToString();
        }

        /// <summary>All the numbers that follow from a difficulty value (0 = gentle, 1 = the hardest late levels).</summary>
        private static LevelData Base(MissionType mission, float d)
        {
            // Floors grow with the campaign (5x5 to 14x14); the camera follows the robot closely on the big ones and the
            // blocks gather around it, so the pressure is about the same per screen while the floor gives room to run.
            int size = Mathf.RoundToInt(Mathf.Lerp(5f, 20f, d));
            // Missions that ask for every tile or a race against the clock stay on manageable floors.
            if (mission == MissionType.Paint) size = Mathf.Min(size, 10);
            if (mission == MissionType.CoinRain) size = Mathf.Min(size, 12);
            float nearArea = Mathf.Min(size * size, 81f);
            var level = new LevelData
            {
                gridWidth = size,
                gridHeight = size,
                mission = mission,
                // Never less than a second of warning: hard, but always readable.
                warningTime = Mathf.Lerp(1.5f, 1.15f, d),
                spawnInterval = Mathf.Lerp(2.1f, 1.6f, d),
                blocksPerWave = Mathf.Clamp(Mathf.RoundToInt(nearArea * Mathf.Lerp(0.025f, 0.045f, d)), 1, 4),
                rampUp = Mathf.Lerp(0.15f, 0.3f, d),
                // Blocks fall around the robot, only sometimes right on it: pressure to move, not a trap.
                aimAtPlayerChance = Mathf.Lerp(0.3f, 0.2f, d),
                // The challenge grows in what the mission asks: more coins, longer to hold out, more keys to find.
                coinTarget = 8 + Mathf.RoundToInt(d * 16f),
                surviveSeconds = Mathf.Round(30f + d * 30f),
                keys = 2 + Mathf.RoundToInt(d * 3f),
            };

            if (mission == MissionType.CoinRain)
            {
                // Coins everywhere, the usual hazards for this point of the campaign, and a target to beat the clock.
                level.surviveSeconds = Mathf.Round(22f + d * 18f);
                level.coinTarget = 10 + Mathf.RoundToInt(d * 16f);
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
