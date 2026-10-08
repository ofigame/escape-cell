using SquashBot.Core;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The campaign's single loop: crates fall, bugs and guard robots roam the floor; squash and beat them all and
    /// the big monster comes. Everything only grows from level to level — the floor (tiny training squares first, then
    /// wide shapes up to 28 across), the crates' pace, the bugs, the robots and their toughness, the monster — so a
    /// later level is never easier than an earlier one. No tiles that push the robot around.
    /// </summary>
    public static partial class LevelCatalog
    {
        /// <summary>Difficulty along the campaign, 0..1, never falling.</summary>
        /// The first three floors are the same gentle start; from the fourth on it climbs in steps you can feel (a
        /// tenth of the way by level 10, a fifth by level 20) and keeps climbing to the end.
        private static float HuntDifficulty(int index) =>
            index < 3 ? 0f : 0.06f + 0.94f * Mathf.Pow(Mathf.Clamp01((index - 3) / (float)(LevelCount - 4)), 0.62f);

        /// <summary>
        /// The armour of the floor's guards and monster: 0 up to level 25, then 1 (a two-damage weapon gets through),
        /// 2 from level 71 (three damage: the mace) and 3 from level 171 (four: the star hammer). Each step comes some
        /// floors after the weapon that beats it is sold.
        /// </summary>
        public static int ArmorAt(int index) => index < 25 ? 0 : index < 70 ? 1 : index < 170 ? 2 : 3;

        public static LevelData Hunt(LevelScript.Card card)
        {
            int index = card.n - 1;
            float d = HuntDifficulty(index);

            // The floor: a few training squares, then growing wide shapes.
            int size = index switch
            {
                0 => 5, 1 => 5, 2 => 5, 3 => 6, 4 => 7, 5 => 7, 6 => 8, 7 => 8,
                _ => Mathf.RoundToInt(Mathf.Lerp(9f, 28f, Mathf.Pow(Mathf.InverseLerp(8f, LevelCount - 1, index), 0.8f))),
            };
            var level = new LevelData
            {
                mission = MissionType.Hunt,
                number = card.n,
                score = Mathf.RoundToInt(d * 100f),
                helper = card.helper == "Lumi" ? Helper.Lumi : card.helper == "Kuzgun" ? Helper.Kuzgun : Helper.Bip,
                cardGoal = false,
                final = index == LevelCount - 1,
            };
            level.layout = FloorShapes.For(index, size, out level.shapeName);
            level.gridWidth = level.layout[0].Length;
            level.gridHeight = level.layout.Length;

            // Crates: gentle at first, a little busier every level; now and then a storm (the hazard rhythm).
            level.warningTime = Mathf.Lerp(1.65f, 1.05f, d);
            level.spawnInterval = index < 3 ? 3.4f : Mathf.Lerp(2.6f, 1.3f, d);
            level.blocksPerWave = Mathf.Clamp(1 + Mathf.FloorToInt(d * 3.2f), 1, 4);
            level.aimAtPlayerChance = Mathf.Lerp(0.18f, 0.36f, d);
            level.rampUp = Mathf.Lerp(0.1f, 0.25f, d);
            level.lineWaveChance = d < 0.2f ? 0f : Mathf.Lerp(0.04f, 0.22f, (d - 0.2f) / 0.8f);
            level.bombChance = d < 0.12f ? 0f : Mathf.Lerp(0.05f, 0.18f, (d - 0.12f) / 0.88f);
            level.blockLinger = Mathf.Lerp(2.2f, 3.6f, d);
            level.breakTiles = index >= 15;
            level.tileRepairTime = Mathf.Lerp(7f, 4.5f, d);

            // The floor's crowd: bugs to squash, robots to beat (none on the very first floor).
            int floorTiles = size * size;
            level.bugs = Mathf.Clamp(2 + Mathf.RoundToInt(d * 12f), 2, Mathf.Max(2, floorTiles / 7));
            level.robots = index < 3 ? 0 : Mathf.Clamp(1 + Mathf.RoundToInt(d * 7f), 1, Mathf.Max(1, floorTiles / 12));
            // Now and then a tall humanoid enforcer joins them (from level 13), more often late on.
            level.brutes = index < 12 ? 0 : (index % 4 == 1 ? 1 : 0) + (d > 0.55f && index % 2 == 0 ? 1 : 0);
            level.robotHp = 2 + Mathf.RoundToInt(d * 3f);
            level.robotStep = Mathf.Lerp(1.05f, 0.5f, d);
            level.monsterHp = 6 + Mathf.RoundToInt(d * 26f);
            level.monsterAttack = Mathf.Lerp(2.3f, 0.95f, d); // seconds between the monster's attacks: it keeps the pressure on

            // Later floors are meant to need gear from the shop: the guards and the monster wear armour that only a
            // strong enough weapon (or the super skill's double blows) gets through, and crates hit harder (armour
            // upgrades soften them).
            level.armor = ArmorAt(index);
            level.crateShare = Mathf.Lerp(0.3f, 0.48f, Mathf.InverseLerp(0.25f, 1f, d));
            if (level.armor > 0)
            {
                level.robotHp += level.armor;
                level.monsterHp += 4 * level.armor;
            }

            level.coinInterval = 2.4f;
            level.maxCoins = 2;
            level.powerUpInterval = index < FirstPowerUpLevel - 1 ? 0f : Mathf.Lerp(11f, 6f, d);
            return level;
        }
    }
}
