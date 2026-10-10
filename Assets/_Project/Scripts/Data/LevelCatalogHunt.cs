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
        public static int ArmorAt(int index) => index < 25 ? 0 : index < 70 ? 2 : index < 170 ? 3 : index < 215 ? 4 : 5;

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
            // On shapes open in the middle of the north edge (a U, a ring...) the cage would float over a gap: the
            // three columns under it are filled down until they meet the floor, so it is always walked to.
            for (int x = mid - 1; x <= mid + 1; x++)
            {
                if (x < 0 || x >= w) continue;
                int floorAt = -1;
                for (int r = 3; r < rows.Count && floorAt < 0; r++)
                    if (rows[r][x] != '.' && rows[r][x] != 'X') floorAt = r;
                if (floorAt < 0) continue;
                for (int r = 3; r < floorAt; r++) rows[r][x] = '#';
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
                0 => 9, 1 => 9, 2 => 10, 3 => 10, 4 => 11, 5 => 11, 6 => 12, 7 => 12, // bigger from the very first floor
                // Wide floors that keep growing; past level 100 the utopian worlds open up even more.
                _ => index < 100
                    ? Mathf.RoundToInt(Mathf.Lerp(12f, 30f, Mathf.Pow(Mathf.InverseLerp(8f, 99f, index), 0.85f)))
                    : Mathf.RoundToInt(Mathf.Lerp(31f, 40f, Mathf.InverseLerp(100f, LevelCount - 1, index))),
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
            int inWorld = index % LevelsPerWorld;
            // The tall pyramids and terraces are switched off (they looked odd in places); gentle mounds instead: a few
            // low plateaus a short step up, on every third floor from level 9.
            level.terrain = index >= 8 && inWorld % 3 == 1 ? TerrainKind.Mounds : TerrainKind.Flat;
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
            // Plenty of robots on every floor (20 on the very first, up to 45 late on), but only a few at a time: when
            // one falls another drops in from the sky somewhere off, so the floor stays busy without being overwhelming.
            level.robots = 20 + Mathf.RoundToInt(d * 25f);
            level.robotsAtOnce = Mathf.Clamp(3 + Mathf.RoundToInt(d * 5f), 3, Mathf.Max(3, floorTiles / 14));
            // Now and then a tall humanoid enforcer joins them (from level 13), more often late on.
            level.brutes = index < 12 ? 0 : (index % 4 == 1 ? 1 : 0) + (d > 0.55f && index % 2 == 0 ? 1 : 0);
            level.robotHp = index < 5 ? 1 : 2 + Mathf.RoundToInt(d * 3f); // the first floors: one blow each, easy but many
            // Guard towers from level 16 (one at first, up to six on the late, wide floors), sturdier as they go.
            level.towers = index < 15 ? 0 : Mathf.Clamp(1 + Mathf.FloorToInt(d * 4f) + (index >= 100 ? 1 : 0), 1, Mathf.Max(1, floorTiles / 60));
            level.towerHp = 4 + Mathf.RoundToInt(d * 10f);
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
                level.towerHp += level.armor - 1;
            }

            // The perfect city (level 101 on): pearl guards, a third of them (half from level 171) behind energy shields,
            // repair drones mending the towers (from level 111), a reactor core as the boss of every tenth floor, and
            // the city's own floor rules: teleport pads on every floor, lasers and energy panels taking turns.
            if (index >= 100)
            {
                level.utopia = true;
                level.shieldEvery = index >= 170 ? 2 : 3;
                level.shieldHits = index >= 200 ? 3 : 2;
                level.repairDrones = index >= 110;
                level.reactor = inWorld == 9;
                var rules = FloorRule.Teleport;
                if (inWorld % 3 == 1 || (index >= 150 && inWorld % 3 == 0 && inWorld != 9)) rules |= FloorRule.Laser;
                if (inWorld % 3 == 2) rules |= FloorRule.Blink;
                level.rules = rules;
            }

            level.coinInterval = 2.4f;
            level.maxCoins = 2;
            level.powerUpInterval = index < FirstPowerUpLevel - 1 ? 0f : Mathf.Lerp(11f, 6f, d);
            return level;
        }
    }
}
