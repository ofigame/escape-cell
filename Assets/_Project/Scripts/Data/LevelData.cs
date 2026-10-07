using System;
using UnityEngine;

namespace SquashBot.Data
{
    public enum MissionType
    {
        /// <summary>Pick up a number of coins.</summary>
        CollectCoins,
        /// <summary>Stay alive for a number of seconds.</summary>
        Survive,
        /// <summary>Collect the keys that appear far from the robot; then the door unlocks; reach it to escape.</summary>
        Exit,
        /// <summary>Step on every tile to paint the whole platform.</summary>
        Paint,
        /// <summary>Coins rain down: grab the target number before the clock runs out.</summary>
        CoinRain,
        /// <summary>Bonus treasure vault: grab as many coins as you can; nothing to lose.</summary>
        Treasure,
        /// <summary>Bonus escape tunnel: a third-person run down an air duct (see DuctRunner).</summary>
        Tunnel,
        /// <summary>World finale: dodge WARDEN's attacks and hit the buttons that light up, three times.</summary>
        Boss,
        /// <summary>A little story on a big floor: find every piece (keys, cores, cages, lanterns, gems), then reach the goal.</summary>
        Quest,
        /// <summary>A monster on a fixed tile: pick up the Thunder Hammer, run into it, repeat until its health bar is empty.</summary>
        Monster,
        /// <summary>A coin thief runs from the robot: corner it and bump into it a number of times.</summary>
        Thief,
        /// <summary>Lead Bip, a little lost robot that follows a tile behind, through the open door.</summary>
        Escort,
        /// <summary>Two Shadow Clones copy every hop mirrored: collect the energy cores without touching them.</summary>
        Clone
    }

    /// <summary>The signature rules of the upper floors (several can be combined).</summary>
    [Flags]
    public enum FloorRule
    {
        None = 0,
        Current = 1,
        Sticky = 2,
        Poison = 4,
        Dark = 8,
        Ice = 16,
        Wind = 32,
        Laser = 64,
        Teleport = 128,
        Trampoline = 256,
        Glass = 512,
        Blink = 1024,
        Hunter = 2048,
        Barrel = 4096,
        /// <summary>Electric jellyfish drifting along a row or column (the barrel rule, at sea).</summary>
        Jellyfish = 8192,
        /// <summary>A blizzard: the view closes in around the robot, the warnings still glow.</summary>
        Blizzard = 16384,
        /// <summary>Lasers bounced off crystals, their path drawn first.</summary>
        MirrorLaser = 32768,
        /// <summary>Cloud platforms that slide a tile every few seconds, carrying the robot.</summary>
        MovingCloud = 65536,
        /// <summary>Lightning: a yellow ring on a tile, then a strike.</summary>
        Lightning = 131072,
        /// <summary>Glitch tiles: two tiles pixelate, then swap places (with whatever stands on them).</summary>
        Glitch = 262144,
        /// <summary>Gravity wells pulling the robot a tile towards their centre now and then.</summary>
        GravityWell = 524288,
        /// <summary>A black hole in the middle of the floor that grows and shrinks.</summary>
        BlackHole = 1048576
    }

    /// <summary>A surprise that happens once during a level.</summary>
    public enum LevelEvent
    {
        None,
        GoldCart,
        Alarm,
        SupplyCrate
    }

    [Serializable]
    public class LevelData
    {
        [Header("Platform")]
        [Range(3, 6)] public int gridWidth = 3;
        [Range(3, 6)] public int gridHeight = 3;
        [Tooltip("Optional shape: one row per line (row 0 = far edge), '#' floor, '.' no tile, 'X' obstacle. Empty = full rectangle.")]
        public string[] layout;
        [Tooltip("The kind of floor the layout was made from (Square, L, Ring, Corridor, Rooms, Maze, Arena...), for reports.")]
        public string shapeName;

        [Header("Mission")]
        public MissionType mission = MissionType.CollectCoins;
        public int coinTarget = 5;
        public float surviveSeconds = 30f;
        [Tooltip("Exit missions: keys to collect (one at a time) before the door unlocks.")]
        public int keys = 1;

        [Header("Journey")]
        [Tooltip("Tiles crumble a moment after the robot steps off them (they mend after a while).")]
        public bool collapseBehind;
        [Tooltip("A wave that swallows the platform row by row from the start (tiles per second, 0 = none).")]
        public float chaseSpeed;
        [Tooltip("Obstacles are low hedges (mazes), so the robot never hides behind them.")]
        public bool lowWalls;
        [Tooltip("A surprise during the level: a gold cart, a WARDEN alarm or a supply crate (at most one).")]
        public LevelEvent levelEvent;
        [Tooltip("Floor rules: currents, candy, poison, darkness, ice, wind, lasers, teleports, trampolines, glass, blinking tiles, hunting blocks, barrels.")]
        public FloorRule rules;

        /// <summary>Quest levels: which story (and so which pieces and which goal).</summary>
        public QuestKind quest;

        /// <summary>Monster levels: the monster guards Princess Lumi, who is freed when it falls.</summary>
        public bool guardsPrincess;
        [Tooltip("Monster levels: after each hit the monster shields itself and leaps to another tile.")]
        public bool phased;
        [Tooltip("A long level (2-3 minutes): slower-warning blocks, short heavy waves now and then, and a helicopter bringing a super power every 30 seconds.")]
        public bool marathon;
        [Tooltip("The very last level: vanG's heart, painted gold; then the last road, chasing vanG's core.")]
        public bool final;

        [Header("Scenario card")]
        [Tooltip("The level's number in the scenario (1-based); its texts are \"lvl.{number}.*\" in Loc.")]
        public int number;
        [Tooltip("Difficulty score of the level card (1-100).")]
        public int score;
        [Tooltip("Who gives the tip at the start of the level.")]
        public Helper helper;
        [Tooltip("Seconds to finish the goal in (0 = no limit): coin hunts, roads, paint.")]
        public float timeLimit;
        [Tooltip("Paint levels: tiles to paint (0 = every tile).")]
        public int paintTarget;
        [Tooltip("Coin thief levels: race Kuzgun to the coins instead of catching him.")]
        public bool thiefRace;
        [Tooltip("Guard Bip: keep it safe for this many seconds (0 = lead it to the door).")]
        public float escortSeconds;
        [Tooltip("Guard Bip: lead it to this many spots in turn (0 = to the door).")]
        public int escortStops;
        [Tooltip("Shadow Clones: knock them out this many times (0 = collect the cores).")]
        public int cloneKnockouts;
        [Tooltip("Boss and monster fights: stages, each with hitsPerStage hits.")]
        public int stages;
        public int hitsPerStage;
        [Tooltip("Kuzgun runs alongside the robot as an ally.")]
        public bool allyKuzgun;
        [Tooltip("Moving enemies on the floor (counts).")]
        public int sweepers, erasers, drones, sandworms, crabs, turrets, penguins, springbots;

        [Header("Blocks")]
        [Tooltip("Seconds between the red warning appearing and the block hitting the tile.")]
        public float warningTime = 1.4f;
        [Tooltip("Seconds between two waves of blocks.")]
        public float spawnInterval = 2f;
        public int blocksPerWave = 1;
        [Tooltip("How long a landed block blocks the tile.")]
        public float blockLinger = 1.5f;
        [Range(0f, 1f)] public float aimAtPlayerChance = 0.5f;
        [Tooltip("0 = constant pace. 0.5 = waves come 50% faster by the end of the mission.")]
        public float rampUp = 0f;

        [Tooltip("Chance that a wave is a whole row or column (always leaving a gap to escape through).")]
        [Range(0f, 1f)] public float lineWaveChance = 0f;
        [Tooltip("Chance that a wave is a bomb whose blast covers a plus shape.")]
        [Range(0f, 1f)] public float bombChance = 0f;

        [Header("Breaking tiles")]
        public bool breakTiles;
        public float tileRepairTime = 6f;
        [Tooltip("Chance that a breaking tile catches fire (temporary) instead of becoming a hole.")]
        [Range(0f, 1f)] public float fireChance = 0f;
        public float fireDuration = 3.5f;

        [Header("Coins")]
        public float coinInterval = 2.2f;
        public float coinLifetime = 5f;
        public int maxCoins = 2;

        [Header("Power-ups")]
        [Tooltip("Seconds between power-up spawns. 0 = no power-ups in this level.")]
        public float powerUpInterval = 0f;
        public float powerUpLifetime = 5f;
        public float shieldDuration = 5f;

        public LevelData Clone()
        {
            var copy = (LevelData)MemberwiseClone();
            copy.layout = layout == null ? null : (string[])layout.Clone();
            return copy;
        }
    }
}
