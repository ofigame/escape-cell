using System.Collections.Generic;
using SquashBot.Core;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The campaign's single loop: crates fall, bugs and guard robots roam the floor; squash and beat them all and
    /// the big monster comes. Everything only grows from level to level — the floor (tiny training squares first, then
    /// wide shapes up to 32 across), the crates' pace, the bugs, the robots and their toughness, the monster — so a
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
        /// The armour of the floor's guards and monster: 0 up to level 25, then 2 (a three-damage weapon gets through:
        /// the stone axe), 3 from level 71 (four: the mace) and 4 from level 171 (five: the battle axe). Each step comes
        /// some floors after the weapon that beats it is sold.
        /// </summary>
        public static int ArmorAt(int index) => index < 25 ? 0 : index < 70 ? 2 : index < 170 ? 3 : 4;

        /// <summary>
        /// Adds healing islands to a layout: a 2x2 patch of 'H' tiles one empty column off the east edge (and, for
        /// two, off the west edge too), next to two rows whose edge tiles are floor, so a swipe from there leaps over.
        /// </summary>
        private static string[] WithHealIslands(string[] layout, int count)
        {
            int h = layout.Length;
            bool Floor(char c) => c != '.' && c != 'X';
            // Rows are listed top first; pick the pair of neighbouring rows nearest the middle with floor at that edge.
            int PickRows(bool east)
            {
                int best = -1, bestDistance = int.MaxValue;
                for (int r = 0; r + 1 < h; r++)
                {
                    string a = layout[r], b = layout[r + 1];
                    bool ok = east ? Floor(a[a.Length - 1]) && Floor(b[b.Length - 1]) : Floor(a[0]) && Floor(b[0]);
                    int distance = Mathf.Abs(r - h / 2);
                    if (ok && distance < bestDistance) { bestDistance = distance; best = r; }
                }
                return best;
            }
            int eastRow = PickRows(true);
            int westRow = count >= 2 ? PickRows(false) : -1;
            if (eastRow < 0 && westRow < 0) return layout;
            var result = new string[h];
            for (int r = 0; r < h; r++)
            {
                string row = layout[r];
                if (eastRow >= 0) row += r == eastRow || r == eastRow + 1 ? ".HH" : "...";
                if (westRow >= 0) row = (r == westRow || r == westRow + 1 ? "HH." : "...") + row;
                result[r] = row;
            }
            return result;
        }

        /// <summary>Tiles of the bridge from vanG's cage to the tunnel.</summary>
        public const int BridgeLength = 4;

        /// <summary>
        /// vanG's cage and the bridge to the tunnel: a 3x2 cage ('C') in the middle of the top (north) rows, the row
        /// under it made floor so the cage always joins the main floor, and a one-tile bridge ('B') running north from
        /// the cage's middle over <see cref="BridgeLength"/> new rows.
        /// </summary>
        private static string[] WithCageAndBridge(string[] layout)
        {
            int w = layout[0].Length;
            int mid = w / 2;
            var rows = new List<char[]>();
            foreach (var r in layout) rows.Add(r.ToCharArray());
            for (int r = 0; r < 3 && r < rows.Count; r++)
                for (int x = mid - 1; x <= mid + 1; x++)
                {
                    if (x < 0 || x >= w) continue;
                    if (r < 2) rows[r][x] = 'C';
                    else if (rows[r][x] == '.' || rows[r][x] == 'X') rows[r][x] = '#';
                }
            var result = new List<string>();
            for (int i = 0; i < BridgeLength; i++)
            {
                var bridgeRow = new string('.', w).ToCharArray();
                bridgeRow[mid] = 'B';
                result.Add(new string(bridgeRow));
            }
            foreach (var r in rows) result.Add(new string(r));
            return result.ToArray();
        }

        public static LevelData Hunt(LevelScript.Card card)
        {
            int index = card.n - 1;
            float d = HuntDifficulty(index);

            // The floor: a few training squares, then growing wide shapes.
            int size = index switch
            {
                0 => 7, 1 => 7, 2 => 7, 3 => 8, 4 => 9, 5 => 9, 6 => 10, 7 => 10, // bigger from the very first floor
                _ => Mathf.RoundToInt(Mathf.Lerp(11f, 32f, Mathf.Pow(Mathf.InverseLerp(8f, LevelCount - 1, index), 0.8f))),
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
            // Each world's 4th level is a pyramid and its 8th terraces (both change from world to world, see FloorRelief).
            int inWorld = index % LevelsPerWorld;
            level.terrain = inWorld == 3 ? TerrainKind.Pyramid : inWorld == 7 ? TerrainKind.Terraces : TerrainKind.Flat;
            // Hard floors (from level 26: the 3rd, 6th and 10th of each world) get healing islands a leap off the side
            // edges: one, two from level 121.
            if (index >= 25 && (inWorld == 2 || inWorld == 5 || inWorld == 9))
                level.layout = WithHealIslands(level.layout, index >= 120 ? 2 : 1);
            // vanG's cage in the middle of the north edge, the bridge from it to the tunnel beyond.
            level.layout = WithCageAndBridge(level.layout);
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
                level.robotHp += level.armor - 1;
                level.monsterHp += 4 * (level.armor - 1);
            }

            level.coinInterval = 2.4f;
            level.maxCoins = 2;
            level.powerUpInterval = index < FirstPowerUpLevel - 1 ? 0f : Mathf.Lerp(11f, 6f, d);
            return level;
        }
    }
}
