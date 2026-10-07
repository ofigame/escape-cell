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
            var levels = FirstWorld();
            var used = new HashSet<Journey>();
            int exits = 0;
            for (int index = LevelsPerWorld; index < LevelCount; index++)
                levels.Add(Generated(index, used, ref exits));

            for (int i = FirstPowerUpLevel - 1; i < levels.Count; i++)
                levels[i].powerUpInterval = Mathf.Lerp(11f, 5.5f, Difficulty(i)); // more skills on harder floors
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

        private static LevelData Generated(int index, HashSet<Journey> used, ref int exits)
        {
            int world = WorldOf(index);
            int i = index % LevelsPerWorld;
            float d = Difficulty(index);
            // Shift the first eight missions per world so consecutive worlds don't open the same way.
            var mission = i >= 8 ? Rhythm[i] : Rhythm[(i + world) % 8];
            // From the second world on, the middle quest of the world is a new kind of story: the Masked Thief on odd worlds,
            // guarding Bip to the door on even ones, and the Shadow Clones in the Crystal Cave and on the Star Road (where
            // they replace the thief; see CloneWorld). The other two quests stay classic stories.
            if (world >= 1 && i < 8 && (i + world) % 8 == 3) mission = CloneWorld(world) && (world % 2 == 1 || world <= 16) ? MissionType.Clone : world % 2 == 1 ? MissionType.Thief : MissionType.Escort;
            // From the second world on, the coin hunt of the world becomes a long haul: survive two to three minutes.
            bool marathon = world >= 1 && i < 8 && (i + world) % 8 == 2;
            if (marathon) mission = MissionType.Survive;
            var rules = Rules(world, index);
            // The finale (level 250): vanG's heart, painted gold, with no floor rule in the way.
            bool final = index == LevelCount - 1;
            if (final)
            {
                mission = MissionType.Paint;
                rules = FloorRule.None;
            }
            // Poison eats tiles for good, so a poisoned floor can never be fully painted.
            if (mission == MissionType.Paint && (rules & FloorRule.Poison) != 0) mission = MissionType.CollectCoins;

            var level = Base(mission, d);
            level.rules = rules;
            // Hazards only ever get added: holes from level 7, bombs from world 2, lines and fire from world 3.
            level.breakTiles = true;
            level.bombChance = Mathf.Lerp(0.05f, 0.16f, d);
            level.lineWaveChance = world >= 2 ? Mathf.Lerp(0.03f, 0.12f, d) : 0f;
            level.fireChance = world >= 2 ? Mathf.Lerp(0.25f, 0.4f, d) : 0f;

            level.levelEvent = EventFor(index, mission);

            if (final)
            {
                // An open round arena under vanG's face: lines of code blocks sweep it, and every tile must turn gold.
                level.final = true;
                level.layout = Journeys.Arena(9, 2, index);
                level.shapeName = "Arena";
                level.lineWaveChance = 0.3f;
                level.bombChance = 0.12f;
                level.fireChance = 0f;
                level.blocksPerWave = Mathf.Max(2, level.blocksPerWave);
                level.aimAtPlayerChance = 0.15f;
                level.levelEvent = LevelEvent.None;
            }
            else if (mission == MissionType.Boss)
            {
                // vanG's arena: wide open, lines of blocks sweep it often, three buttons to hit.
                level.layout = Journeys.Arena(Mathf.RoundToInt(Mathf.Lerp(7f, 14f, d)), world >= 6 ? 3 : 2, index);
                level.shapeName = "Arena";
                level.keys = 3;
                level.lineWaveChance = Mathf.Lerp(0.3f, 0.45f, d);
                level.bombChance = Mathf.Max(level.bombChance, 0.2f);
                level.blocksPerWave = Mathf.Min(5, level.blocksPerWave + 1);
            }
            else if (mission == MissionType.Monster)
            {
                // A monster to bring down with magic orbs: more hits on later floors, a roomy floor to run around on.
                level.keys = 3 + Mathf.RoundToInt(d * 5f);
                level.gridWidth = level.gridHeight = Mathf.Min(21, level.gridWidth + 1);
                level.blocksPerWave = Mathf.Max(1, level.blocksPerWave - 1);
                level.aimAtPlayerChance = 0.15f;
                // On every other floor the monster keeps Princess Lumi prisoner next to it.
                level.guardsPrincess = world % 2 == 1;
                // From world 5 the monsters that don't guard the princess shield themselves and leap after each hit.
                level.phased = world >= 4 && !level.guardsPrincess;
                Shape(level, index, d);
            }
            else if (mission == MissionType.Thief)
            {
                // A roomy floor for the chase, gentle blocks: the thief is the challenge.
                level.keys = 3 + Mathf.RoundToInt(d * 3f);
                level.gridWidth = level.gridHeight = Mathf.Min(16, level.gridWidth + 1);
                level.blocksPerWave = Mathf.Max(1, level.blocksPerWave - 1);
                level.aimAtPlayerChance = 0.1f;
                Shape(level, index, d);
            }
            else if (mission == MissionType.Clone)
            {
                // A roomy floor to lead the clones around, a few cores spread far apart, calm blocks to lure them under.
                level.keys = 4 + Mathf.RoundToInt(d * 3f);
                level.gridWidth = level.gridHeight = Mathf.Min(14, level.gridWidth + 1);
                level.blocksPerWave = Mathf.Max(1, level.blocksPerWave - 1);
                level.aimAtPlayerChance = 0.05f;
                Shape(level, index, d);
            }
            else if (mission == MissionType.Escort)
            {
                // A long way to lead Bip: the regular floor, a little calmer, the door on the far side.
                level.blocksPerWave = Mathf.Max(1, level.blocksPerWave - 1);
                level.spawnInterval *= 1.1f;
                level.aimAtPlayerChance = 0.1f;
                Shape(level, index, d);
            }
            else if (mission == MissionType.Quest)
            {
                // A story on a bigger floor than usual: room to explore, pieces to find, and gentler blocks, because
                // the challenge is the search, not surviving a storm of blocks.
                level.quest = (QuestKind)((world * 2 + i) % 5);
                level.gridWidth = level.gridHeight = Mathf.Min(22, level.gridWidth + 2);
                level.keys = 3 + Mathf.RoundToInt(d * 7f);
                level.blocksPerWave = Mathf.Max(1, level.blocksPerWave - 1);
                level.spawnInterval *= 1.15f;
                level.bombChance *= 0.6f;
                level.aimAtPlayerChance = 0.12f;
                level.lineWaveChance *= 0.5f;
                Shape(level, index, d);
            }
            else if (marathon)
            {
                // The long haul: a roomy floor, blocks that warn a little longer so they can always be seen and dodged, a little more often.
                level.marathon = true;
                level.surviveSeconds = Mathf.Round(120f + d * 60f);
                level.warningTime *= 1.25f;
                level.spawnInterval *= 0.95f;
                level.gridWidth = level.gridHeight = Mathf.Min(16, level.gridWidth + 1);
                Shape(level, index, d);
            }
            else if (mission == MissionType.Exit)
            {
                MakeJourney(level, PickJourney(index, used, exits++), index, d);
            }
            else
            {
                Shape(level, index, d);
            }
            return level;
        }

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
        /// The Masked Thief's levels that belong to the Shadow Clones instead: in the Crystal Cave, where vanG
        /// builds its hall of mirrors, and on the Star Road, after the thief has given the robot its last energy.
        /// </summary>
        public static bool CloneWorld(int world) => (world >= 14 && world <= 16) || world >= 21;

        private static FloorRule Rules(int world, int index)
        {
            if (world < FirstRuleWorld) return FloorRule.None;
            var rng = new System.Random(index * 131 + 7);

            // Every single rule met so far.
            var met = new List<FloorRule>();
            for (int w = FirstRuleWorld; w <= Mathf.Min(world, Signature.Length - 2); w++)
                foreach (FloorRule r in System.Enum.GetValues(typeof(FloorRule)))
                    if (r != FloorRule.None && (Signature[w] & r) != 0 && !met.Contains(r)) met.Add(r);

            if (world >= Signature.Length - 1)
            {
                // vanG's Heart: in the dark, with one more rule met on the way.
                var others = met.FindAll(r => r != FloorRule.Dark);
                return FloorRule.Dark | others[rng.Next(others.Count)];
            }

            var rules = Signature[world];
            // A floor's first two levels show its rule alone; later ones sometimes bring back an earlier floor's rule.
            int i = index % LevelsPerWorld;
            if (i >= 2 && rng.NextDouble() < 0.35)
            {
                var earlier = met.FindAll(r => (Signature[world] & r) == 0);
                if (earlier.Count > 0) rules |= earlier[rng.Next(earlier.Count)];
            }
            return rules;
        }

        /// <summary>
        /// About a third of the levels get one surprise: gold carts from level 14, WARDEN alarms from 25,
        /// supply crates from 35. Each one first shows up exactly where it unlocks.
        /// </summary>
        private static LevelEvent EventFor(int index, MissionType mission)
        {
            if (mission == MissionType.Boss || index < 13) return LevelEvent.None;
            if (index == 13) return LevelEvent.GoldCart;
            if (index == 24) return LevelEvent.Alarm;
            if (index == 34) return LevelEvent.SupplyCrate;
            var rng = new System.Random(index * 977 + 5);
            if (rng.NextDouble() >= 0.35) return LevelEvent.None;
            var pool = new List<LevelEvent> { LevelEvent.GoldCart };
            if (index >= 24) pool.Add(LevelEvent.Alarm);
            if (index >= 34) pool.Add(LevelEvent.SupplyCrate);
            return pool[rng.Next(pool.Count)];
        }

        /// <summary>A newly unlocked journey kind appears at the first chance; after that the kinds take turns.</summary>
        private static Journey PickJourney(int index, HashSet<Journey> used, int exits)
        {
            var available = new List<Journey>();
            foreach (var (kind, from) in JourneyUnlocks)
                if (index >= from) available.Add(kind);
            foreach (var kind in available)
                if (!used.Contains(kind) && kind != Journey.Arena)
                {
                    used.Add(kind);
                    return kind;
                }
            return available[exits % available.Count];
        }

        private static void MakeJourney(LevelData level, Journey kind, int index, float d)
        {
            level.shapeName = kind.ToString();
            switch (kind)
            {
                case Journey.Corridor:
                    level.layout = Journeys.Corridor(d > 0.55f ? 4 : 3, 10 + Mathf.RoundToInt(d * 14f), 1 + Mathf.RoundToInt(d * 4f), index);
                    break;
                case Journey.Rooms:
                    level.layout = Journeys.Rooms(d > 0.45f ? 4 : 3, d > 0.6f ? 4 : 3, index);
                    break;
                case Journey.Collapse:
                    level.layout = Journeys.Winding(14 + Mathf.RoundToInt(d * 10f), index);
                    level.collapseBehind = true;
                    break;
                case Journey.Maze:
                    level.layout = Journeys.Maze(d > 0.6f ? 5 : 4, 5 + Mathf.RoundToInt(d * 2f), index);
                    level.lowWalls = true;
                    break;
                case Journey.Chase:
                    level.layout = Journeys.Corridor(d > 0.6f ? 4 : 3, 16 + Mathf.RoundToInt(d * 8f), 2 + Mathf.RoundToInt(d * 3f), index);
                    level.chaseSpeed = Mathf.Lerp(0.9f, 1.5f, d);
                    break;
                default:
                    Shape(level, index, d);
                    return;
            }
            // Journey keys lie on the layout's own spots; count them for the star goals.
            int keys = 0;
            foreach (var row in level.layout)
                foreach (char c in row)
                    if (c == 'K') keys++;
            level.keys = keys;
            level.gridHeight = level.layout.Length;
            level.gridWidth = level.layout[0].Length;
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
            // Level 7 introduces the story quests: three keys to free Princess Lumi from the ice.
            levels[6].mission = MissionType.Quest;
            levels[6].quest = QuestKind.Princess;
            levels[6].keys = 3;
            levels[6].gridWidth = levels[6].gridHeight = 5;
            for (int i = 6; i < levels.Count; i++) levels[i].breakTiles = true;

            // Shapes arrive gently: two plain squares, then a new outline every level or two, the first pillars late.
            var shapes = new[]
            {
                PlatformShape.Square, PlatformShape.Square, PlatformShape.L, PlatformShape.Square, PlatformShape.T,
                PlatformShape.L, PlatformShape.Step, PlatformShape.Square, PlatformShape.Square, PlatformShape.T,
            };
            int[] pillars = { 0, 0, 0, 0, 0, 1, 0, 1, 1, 1 };
            for (int i = 0; i < levels.Count; i++)
            {
                levels[i].shapeName = shapes[i].ToString();
                if (shapes[i] != PlatformShape.Square || pillars[i] > 0)
                    levels[i].layout = Layouts.Generate(levels[i].gridWidth, shapes[i], pillars[i], 1000 + i);
            }

            // The finale is the first long road: key at the start, door at the far end.
            levels[9].layout = Journeys.Corridor(3, 10, 1, 1009);
            levels[9].shapeName = "Corridor";
            levels[9].keys = 1;
            levels[9].gridWidth = 3;
            levels[9].gridHeight = 10;
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
