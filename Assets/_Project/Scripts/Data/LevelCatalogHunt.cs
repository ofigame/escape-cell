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

        /// <summary>Tiles of the bridge from vanG's cage to the tunnel.</summary>
        public const int BridgeLength = 4;

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
                for (int x = mid - 1; x <= mid + 1; x++) if (x >= 0 && x < w) bridgeRow[x] = 'B'; // three tiles wide, like the cage
                result.Add(new string(bridgeRow));
            }
            foreach (var r in rows) result.Add(new string(r));
            return result.ToArray();
        }

        /// <summary>How many fortress stages a floor has: one up to level 20, then two, three (61), four (111), five (161) and six from level 201.</summary>
        public static int StageCount(int index) => index < 20 ? 1 : index < 60 ? 2 : index < 110 ? 3 : index < 160 ? 4 : index < 200 ? 5 : 6;

        /// <summary>The hard floors: from level 91 on, and before that every world's last floor from level 31.</summary>
        public static bool IsHard(int index) => index >= 90 || (index >= 30 && index % LevelsPerWorld == LevelsPerWorld - 1);

        /// <summary>
        /// A floor of fortress stages, stacked from south (stage 1, where foi's cell is) to north (the last, with vanG's
        /// cage and the tunnel bridge beyond): stages growing from a small first yard to a huge last field and all growing
        /// with the campaign, joined by three-tile bridges whose middle tile is a gate ('G', shut until the stage before
        /// is cleared). Burning stairs ('F') stand off the corners of every stage and off the south edge of the first;
        /// on hard floors the last stage also has two fire pits in it. <paramref name="bounds"/> gets x0, y0, x1, y1 of
        /// each stage (grid coordinates, y up from the bottom row), first stage first.
        /// </summary>
        /// <summary>The bridges between fortress stages: rows long (each climbing a stair up to the next, higher stage),
        /// and the row of the gate counted from the lower stage's side.</summary>
        public const int BridgeRows = 8, BridgeGateRow = 3;
        /// <summary>How many steps each stage stands above the one before it.</summary>
        public const int StageRise = 3;

        private static string[] StageLayout(int index, float d, bool hard, out int[] bounds, out int cells)
        {
            int n = StageCount(index);
            // One stage: the floor growing a little every level through the first twenty. From level 21 the stages
            // are twice the area they used to be (about 1.4 times wider and longer), each next one clearly bigger.
            int early = index >= 1 && index < 20 ? index / 5 : -1;
            int first = index < 20 ? (n == 1 ? 9 + index / 2 : 11 + index / 3)
                : Mathf.RoundToInt(Mathf.Lerp(13f, 15f, Mathf.InverseLerp(20f, LevelCount - 1, index)) * 1.41f);
            float grow = index < 20 ? 1f : 1.41f;
            var sizes = new int[n];   // widths (odd: the bridges sit on the middle column)
            var lengths = new int[n]; // rows, south to north
            for (int s = 0; s < n; s++)
            {
                int extra = n == 6 && s == 5 ? Mathf.RoundToInt(6 * grow) : 0;
                sizes[s] = (first + Mathf.RoundToInt(4 * grow) * s + extra) | 1;
                lengths[s] = first + Mathf.RoundToInt(3 * grow) * s + extra;
            }
            int w = sizes[n - 1] + 2; // a column each side for the corner stairs
            int mid = w / 2;
            var rows = new List<string>(); // built top (north, the last stage) down
            var tops = new int[n];
            cells = 0;
            for (int s = n - 1; s >= 0; s--)
            {
                int sz = sizes[s], len = lengths[s], x0 = mid - sz / 2, x1 = mid + sz / 2;
                // Not every stage is a square (user asked): octagons, hexagons, circles, diamonds and crosses, in turn.
                int shape = early >= 0 ? new[] { 0, 3, 2, 1 }[early] : (index * 3 + s * 2) % 6;
                tops[s] = rows.Count;
                for (int r = 0; r < len; r++)
                {
                    var row = new string('.', w).ToCharArray();
                    float dc = Mathf.Abs(r - (len - 1) * 0.5f);
                    int inset = shape == 1 ? Mathf.Max(0, Mathf.Min(sz, len) / 4 - Mathf.Min(r, len - 1 - r))
                        : shape == 2 ? Mathf.Min(Mathf.RoundToInt(dc * 0.5f), sz / 2 - 2)
                        : shape == 3 ? Mathf.Clamp(sz / 2 - Mathf.RoundToInt((sz / 2 + 0.5f) * Mathf.Sqrt(Mathf.Max(0f, 1f - (dc / ((len - 1) * 0.5f + 0.5f)) * (dc / ((len - 1) * 0.5f + 0.5f))))), 0, sz / 2 - 2) // round
                        : shape == 4 ? Mathf.Min(Mathf.RoundToInt(dc * 0.8f), sz / 2 - 3) // diamond
                        : shape == 5 ? (dc <= len / 6f ? 0 : sz / 3) // cross
                        : 0;
                    for (int x = x0 + inset; x <= x1 - inset; x++) row[x] = '#';
                    if (r == 0 || r == len - 1) { row[x0 + inset - 1] = 'F'; row[x1 - inset + 1] = 'F'; } // burning stairs off the corners
                    if (r == len / 2 && s == 0) row[mid] = 'S';                          // foi's cell
                    if (hard && s == n - 1 && n > 1 && (r == len / 2 + 1 || r == len / 2 + 2))
                        foreach (int x in new[] { mid - sz / 4 - 2, mid - sz / 4 - 1, mid + sz / 4 + 1, mid + sz / 4 + 2 }) if (row[x] == '#') row[x] = 'F'; // two fire pits, 2x2 each (inside the floor)
                    rows.Add(new string(row));
                    cells += sz;
                }
                if (s > 0)
                    for (int b = 0; b < BridgeRows; b++)
                    {
                        // Two tiles wide; the gate across it a few rows up from the lower stage.
                        var row = new string('.', w).ToCharArray();
                        char c = BridgeRows - 1 - b == BridgeGateRow ? 'G' : '#';
                        row[mid - 1] = c;
                        row[mid] = c;
                        rows.Add(new string(row));
                    }
            }
            // The first stage's south stairs.
            var south = new string('.', w).ToCharArray();
            for (int x = mid - 1; x <= mid + 1; x++) south[x] = 'F';
            rows.Add(new string(south));
            int h = rows.Count;
            bounds = new int[n * 4];
            for (int s = 0; s < n; s++)
            {
                int sz = sizes[s];
                bounds[s * 4] = mid - sz / 2;
                bounds[s * 4 + 2] = mid + sz / 2;
                bounds[s * 4 + 3] = h - 1 - tops[s];
                bounds[s * 4 + 1] = h - 1 - (tops[s] + lengths[s] - 1);
            }
            return rows.ToArray();
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
            // Level 1 is the fortress: an 11x11 stone floor with a flight of stairs in the middle of each side. vanG's
            // cage stands on the north stairs (the bridge to the tunnel beyond it); the others burn ('F').
            if (index == 0)
            {
                size = 11;
                level.fortress = true;
                level.shapeName = "fortress";
                var rows = new List<string> { ".....###.....", ".....###....." };
                for (int r = 0; r < 11; r++) rows.Add(r == 5 ? "F###########F" : ".###########.");
                rows.Add(".....FFF.....");
                level.layout = rows.ToArray();
            }
            int inWorld = index % LevelsPerWorld;
            level.hard = IsHard(index);
            if (index > 0)
            {
                // Every other floor: a chain of fortress stages joined by gated bridges (see StageLayout).
                level.layout = StageLayout(index, d, level.hard, out level.stageBounds, out int cells);
                level.fortressStages = level.stageBounds.Length / 4;
                level.shapeName = "stages" + level.fortressStages;
                size = Mathf.Max(9, Mathf.RoundToInt(Mathf.Sqrt(cells)));
            }
            else level.fortressStages = 1;
            // The tall pyramids and terraces are switched off (they looked odd in places); gentle mounds instead: a few
            // low plateaus a short step up, on every third floor from level 9.
            level.terrain = TerrainKind.Flat; // no raised areas inside a stage (user decision)
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
            level.bugs = Mathf.Clamp(2 + Mathf.RoundToInt(d * 12f) + (level.fortressStages - 1) * 3, 2, Mathf.Max(2, floorTiles / 7));
            // Plenty of robots on every floor (20 on the very first, up to 45 late on), but only a few at a time: when
            // one falls another drops in from the sky somewhere off, so the floor stays busy without being overwhelming.
            level.robots = 24 + Mathf.RoundToInt(d * 34f) + (level.fortressStages - 1) * 12; // the bigger stages hold more
            // From level 30 (when cannons and towers open) the crowds are bigger still: defences are there to help.
            if (index >= 29) level.robots = Mathf.RoundToInt(level.robots * 1.25f);
            // A tenth of the crowd are vanG's warlords (from level 6): very strong, as strong as the level is far in.
            level.elites = index < 5 ? 0 : Mathf.Max(1, Mathf.RoundToInt(level.robots * 0.1f));
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
