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
        /// <summary>An exit door opens after a while; reach it to escape.</summary>
        Exit,
        /// <summary>Step on every tile to paint the whole platform.</summary>
        Paint,
        /// <summary>Bonus round: coins rain down, few hazards, the clock always wins.</summary>
        CoinRain
    }

    [Serializable]
    public class LevelData
    {
        [Header("Platform")]
        [Range(3, 6)] public int gridWidth = 3;
        [Range(3, 6)] public int gridHeight = 3;

        [Header("Mission")]
        public MissionType mission = MissionType.CollectCoins;
        public int coinTarget = 5;
        public float surviveSeconds = 30f;
        [Tooltip("Exit missions: seconds until the door opens.")]
        public float exitDelay = 8f;

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

        public LevelData Clone() => (LevelData)MemberwiseClone();
    }
}
