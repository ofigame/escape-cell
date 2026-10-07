using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using SquashBot.Core;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Turns the scenario document's level cards (<see cref="LevelScript"/>) into level data. Each card's difficulty
    /// score (1-100) sets the block tempo through the document's table (warning time, wave interval, blocks per wave,
    /// aiming, line and bomb chances), corrected per mission as the document asks; the map text gives the floor's
    /// shape and size; the goal text the numbers to reach; the hazard text the floor rules, the blocks and the moving
    /// enemies. Runs in the editor when the level set is built.
    /// </summary>
    public static partial class LevelCatalog
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        // The document's difficulty table: score → warning time, wave interval, blocks per wave, aiming, lines, bombs.
        private static readonly float[] ScoreKeys = { 5, 15, 25, 40, 55, 70, 85, 100 };
        private static readonly float[] WarnKeys = { 1.62f, 1.54f, 1.46f, 1.37f, 1.28f, 1.19f, 1.11f, 1.03f };
        private static readonly float[] IntervalKeys = { 2.1f, 1.96f, 1.82f, 1.66f, 1.52f, 1.39f, 1.27f, 1.16f };
        private static readonly float[] PerWaveKeys = { 1, 1, 2, 2, 3, 3, 4, 4 };
        private static readonly float[] AimKeys = { 0.18f, 0.22f, 0.25f, 0.28f, 0.31f, 0.34f, 0.37f, 0.4f };
        private static readonly float[] LineKeys = { 0f, 0.02f, 0.06f, 0.12f, 0.18f, 0.24f, 0.3f, 0.35f };
        private static readonly float[] BombKeys = { 0f, 0.03f, 0.06f, 0.09f, 0.12f, 0.15f, 0.18f, 0.22f };

        private static float Table(float[] values, float score)
        {
            if (score <= ScoreKeys[0]) return values[0];
            for (int i = 1; i < ScoreKeys.Length; i++)
                if (score <= ScoreKeys[i])
                    return Mathf.Lerp(values[i - 1], values[i], (score - ScoreKeys[i - 1]) / (ScoreKeys[i] - ScoreKeys[i - 1]));
            return values[values.Length - 1];
        }

        private static MissionType MissionOf(string name)
        {
            switch (name)
            {
                case "Altın yağmuru": return MissionType.CoinRain;
                case "Bip'i koru": return MissionType.Escort;
                case "Boyama": return MissionType.Paint;
                case "Canavar savaşı": return MissionType.Monster;
                case "Gölge Klonlar": return MissionType.Clone;
                case "Hayatta kal": return MissionType.Survive;
                case "Hikâye görevi":
                case "Kuzgun'la ortak görev": return MissionType.Quest;
                case "Kaçış yolculuğu": return MissionType.Exit;
                case "Maskeli Hırsız": return MissionType.Thief;
                case "vanG boss": return MissionType.Boss;
                default: return MissionType.CollectCoins;
            }
        }

        private static int Num(string text, string pattern, int fallback)
        {
            var m = Regex.Match(text, pattern);
            return m.Success && int.TryParse(m.Groups[1].Value, out int v) ? v : fallback;
        }

        private static bool Has(string text, params string[] words)
        {
            foreach (var w in words) if (text.Contains(w)) return true;
            return false;
        }

        /// <summary>A level built from its scenario card.</summary>
        public static LevelData FromCard(LevelScript.Card card)
        {
            int index = card.n - 1;
            var mission = MissionOf(card.mission);
            float score = card.difficulty;
            float d = Mathf.Clamp01(score / 100f);
            string map = card.map.ToLower(Tr), goal = card.goal.ToLower(Tr), hazards = card.hazards.ToLower(Tr);
            // The Thunder Hammer is the monster fights' weapon, not the lightning rule.
            hazards = hazards.Replace("şimşek çekici", "çekiç");
            goal = goal.Replace("şimşek çekici", "çekiç");
            string all = map + " | " + goal + " | " + hazards;

            var level = Base(mission, d);
            level.score = card.difficulty;
            level.number = card.n;
            level.helper = card.helper == "Lumi" ? Helper.Lumi : card.helper == "Kuzgun" ? Helper.Kuzgun : Helper.Bip;
            level.allyKuzgun = card.mission == "Kuzgun'la ortak görev";

            // ---- Block tempo from the difficulty table ----
            level.warningTime = Table(WarnKeys, score);
            level.spawnInterval = Table(IntervalKeys, score);
            level.blocksPerWave = Mathf.RoundToInt(Table(PerWaveKeys, score));
            level.aimAtPlayerChance = Table(AimKeys, score);
            level.rampUp = Mathf.Lerp(0.15f, 0.3f, d);
            float lines = Table(LineKeys, score), bombs = Table(BombKeys, score);

            bool rival = mission == MissionType.Monster || mission == MissionType.Boss || mission == MissionType.Thief
                         || mission == MissionType.Escort || mission == MissionType.Clone;
            if (rival) { level.spawnInterval *= 1.15f; level.aimAtPlayerChance *= 0.5f; }
            if (mission == MissionType.CoinRain) level.aimAtPlayerChance *= 0.5f;
            if (mission == MissionType.Quest) level.spawnInterval *= 1.1f;

            // ---- Hazards ----
            bool none = hazards.StartsWith("—") || hazards.StartsWith("blok: — | zemin: — | düşman: —");
            bool blocks = !none && Has(hazards, "düşen sandık", "düşen blok", "sandık", "seyrek düşen", "blok:  düşen", "dalga dalga", "kod blok", "meteor", "buz bloğu", "kar topu");
            if (Has(hazards, "blok: düşen", "blok: seyrek", "düşen blok")) blocks = true;
            bool bomb = Has(hazards, "bomba");
            bool line = Has(hazards, "sıra saldırısı");
            level.breakTiles = Has(hazards, "delik") || score >= 12;
            level.fireChance = Has(hazards, "ateş") ? Mathf.Lerp(0.25f, 0.4f, d) : 0f;
            level.bombChance = bomb ? (blocks || line ? Mathf.Max(bombs, 0.12f) : 1f) : 0f;
            level.lineWaveChance = line ? (blocks || bomb ? Mathf.Max(lines, 0.15f) : 1f) : 0f;
            if (!blocks && card.n <= 3)
            {
                // The very first levels teach one thing at a time.
                level.blocksPerWave = 0;
                if (!bomb && !line) level.spawnInterval = 999f;
            }
            else if (!blocks)
            {
                // Every other level has at least a light rain of blocks under whatever its card adds.
                level.blocksPerWave = Mathf.Max(1, level.blocksPerWave - 1);
                level.spawnInterval *= 1.2f;
            }
            if (Has(hazards, "çok seyrek")) level.spawnInterval *= 2.2f;
            else if (Has(hazards, "seyrek")) level.spawnInterval *= 1.5f;
            if (Has(hazards, "hızlı")) level.warningTime *= 0.88f;
            if (Has(hazards, "yavaş")) level.warningTime *= 1.15f;
            int together = Num(hazards, @"aynı anda (?:en fazla )?(\d+)", 0);
            if (together > 0 && blocks) level.blocksPerWave = together;
            // From the middle of the table on, bombs and sweeping lines mix into the rain even when the card is quiet.
            if (score >= 35f && level.blocksPerWave > 0)
            {
                level.bombChance = Mathf.Max(level.bombChance, bombs * 0.6f);
                level.lineWaveChance = Mathf.Max(level.lineWaveChance, lines * 0.6f);
            }
            if (mission == MissionType.CoinRain) { level.lineWaveChance = 0f; level.bombChance = 0f; }

            // ---- Floor rules ----
            var rules = FloorRule.None;
            void Rule(FloorRule r, params string[] words) { if (Has(all, words)) rules |= r; }
            Rule(FloorRule.Sticky, "yosun", "yapışkan");
            Rule(FloorRule.Dark, "karanlık");
            Rule(FloorRule.Barrel, "varil", "fıçı");
            Rule(FloorRule.Wind, "rüzgâr", "rüzgar");
            Rule(FloorRule.Teleport, "ışınlanma", "çift kapı", "kapı çifti");
            Rule(FloorRule.Current, "akıntı");
            Rule(FloorRule.Jellyfish, "deniz anası", "denizanası");
            Rule(FloorRule.Laser, "lazer");
            // Ice underfoot, not ice as a material ("buz kalıbı", "buz ışını", "buz kafes", "buz kalp"...).
            string iceText = Regex.Replace(map + " | " + hazards, "buz (kalıb|ışın|kafes|kalp|blo|köprü|kristal|küp)", "");
            if (Has(iceText, "buz", "buzlu")) rules |= FloorRule.Ice;
            Rule(FloorRule.Blizzard, "tipi");
            Rule(FloorRule.Glass, "cam", "ince buz");
            Rule(FloorRule.MirrorLaser, "ayna lazer", "lazer kaynağı");
            Rule(FloorRule.Poison, "zehir", "asit");
            Rule(FloorRule.Trampoline, "trambolin");
            Rule(FloorRule.MovingCloud, "kayan bulut", "hareketli bulut");
            Rule(FloorRule.Blink, "yanıp sönen", "pembe ve mavi", "pembe-mavi", "kalp atışı");
            Rule(FloorRule.Lightning, "şimşek");
            Rule(FloorRule.Hunter, "avcı blok");
            Rule(FloorRule.Glitch, "glitch");
            Rule(FloorRule.GravityWell, "kuyu");
            Rule(FloorRule.BlackHole, "kara delik");
            // A mirror laser is its own rule, not the plain one; "buz" in "buz kafes" or "buz pisti" is not always ice.
            if ((rules & FloorRule.MirrorLaser) != 0 && !Has(hazards, "lazer,", "lazer +", "zemin: lazer", "+ lazer")) rules &= ~FloorRule.Laser;
            if (Has(all, "buz kafes") && !Has(hazards, "buz")) rules &= ~FloorRule.Ice;
            if (Has(all, "cam karo") || Has(hazards, "cam")) rules |= FloorRule.Glass;
            level.rules = rules;
            if (mission == MissionType.Paint && (rules & FloorRule.Poison) != 0 && !Has(goal, "boya")) mission = level.mission = MissionType.CollectCoins;

            // ---- Moving enemies ----
            // Harder levels bring more of the same enemy (one more from score 55, two more from 80).
            int Extra = score >= 80f ? 2 : score >= 55f ? 1 : 0;
            int Count(string word)
            {
                if (!hazards.Contains(word)) return 0;
                var m = Regex.Match(hazards, Regex.Escape(word) + @"[^|·;+]*?(?:×\s*(\d+)|\((\d+)(?: adet)?\)|(\d+) adet)");
                if (m.Success)
                    for (int g = 1; g <= 3; g++)
                        if (m.Groups[g].Success && int.TryParse(m.Groups[g].Value, out int v)) return v + Extra;
                return 1 + Extra;
            }
            level.sweepers = Count("süpürgeç");
            level.erasers = Count("silgi");
            level.drones = Count("gözcü dron");
            level.sandworms = Count("kum solucan");
            level.crabs = Count("yengeç");
            level.turrets = Count("taret");
            level.penguins = Count("penguen-bot");
            level.springbots = Count("yay-bot");

            // ---- The floor ----
            BuildMap(level, map, mission, index, d);

            // ---- The goal ----
            ParseGoal(level, mission, goal, d);
            if (Has(hazards, "çöken yol") && !level.collapseBehind) level.collapseBehind = true;
            if (mission == MissionType.Exit && Has(hazards, "kovalamaca", "dalgası") && level.chaseSpeed <= 0f) level.chaseSpeed = Mathf.Lerp(0.9f, 1.5f, d);
            level.levelEvent = Has(all, "vagon") ? LevelEvent.GoldCart : Has(all, "alarm") ? LevelEvent.Alarm : LevelEvent.None;
            level.final = index == LevelCount - 1;
            return level;
        }

        private static void BuildMap(LevelData level, string map, MissionType mission, int seed, float d)
        {
            var dims = Regex.Match(map, @"(\d+)\s*[×x]\s*(\d+)");
            int w = dims.Success ? int.Parse(dims.Groups[1].Value) : level.gridWidth;
            int h = dims.Success ? int.Parse(dims.Groups[2].Value) : level.gridHeight;
            int pillars = Num(map, @"(\d+) (?:taş|sütun|kaya|konteyner|sağlam taş)", 0);
            if (Has(map, "dağınık taş", "dağınık kaya", "taş sütunlar")) pillars = Mathf.Max(pillars, Mathf.Max(w, h) / 2);
            level.gridWidth = Mathf.Max(3, Mathf.Min(w, h));
            level.gridHeight = Mathf.Max(3, Mathf.Max(w, h));

            if (Has(map, "kalp biçim"))
            {
                level.layout = Heart(Mathf.Max(w, h));
                level.shapeName = "Heart";
            }
            else if (Has(map, "çöken yol"))
            {
                level.layout = Journeys.Winding(Mathf.Clamp(Mathf.Max(w, h), 10, 28), seed);
                level.collapseBehind = true;
                level.shapeName = "Collapse";
            }
            else if (Has(map, "labirent"))
            {
                level.layout = Journeys.Maze(Mathf.Clamp((w + 1) / 2, 3, 7), Mathf.Clamp((h + 1) / 2, 3, 8), seed);
                level.lowWalls = true;
                level.shapeName = "Maze";
            }
            else if (Regex.IsMatch(map, @"\d+ (?:oda|sera)") || Has(map, "arka arkaya"))
            {
                int rooms = Num(map, @"(\d+) (?:oda|sera)", 3);
                int size = Num(map, @"her biri (\d+)", 4);
                level.layout = Journeys.Rooms(Mathf.Clamp(rooms, 2, 5), Mathf.Clamp(Mathf.Max(size, Num(map, @"her biri \d+\s*[×x]\s*(\d+)", size)), 3, 6), seed);
                level.shapeName = "Rooms";
            }
            else if (mission == MissionType.Exit || Mathf.Max(w, h) >= 2 * Mathf.Min(w, h) || Has(map, "koridor", "uzun-ince"))
            {
                int width = Mathf.Clamp(Mathf.Min(w, h), 3, 5), length = Mathf.Clamp(Mathf.Max(w, h), 8, 30);
                level.layout = Journeys.Corridor(width, length, Mathf.Clamp(pillars > 0 ? pillars : 1 + Mathf.RoundToInt(d * 4f), 0, 6), seed);
                level.shapeName = "Corridor";
                if (mission == MissionType.Exit && Has(map, "kovalamaca", "dalga")) level.chaseSpeed = Mathf.Lerp(0.9f, 1.5f, d);
            }
            else if (Has(map, "halka"))
            {
                level.layout = Layouts.Generate(level.gridWidth, PlatformShape.Ring, pillars, seed);
                level.shapeName = "Ring";
            }
            else if (w == h)
            {
                level.layout = Layouts.Generate(w, PlatformShape.Square, pillars, seed);
                level.shapeName = "Square";
            }
            else
            {
                level.layout = Rect(w, h, pillars, seed);
                level.shapeName = "Rect";
            }

            // Journey keys lie on the layout's own spots.
            if (mission == MissionType.Exit)
            {
                int keys = 0;
                foreach (var row in level.layout) foreach (char c in row) if (c == 'K') keys++;
                if (keys > 0) level.keys = keys;
            }
        }

        private static void ParseGoal(LevelData level, MissionType mission, string goal, float d)
        {
            int first = Num(goal, @"(\d+)", 0);
            int seconds = Num(goal, @"(\d+)\s*(?:sn|saniye)", 0);
            switch (mission)
            {
                case MissionType.CollectCoins:
                    level.coinTarget = Mathf.Max(1, Num(goal, @"(\d+) altın", first));
                    level.timeLimit = Has(goal, "içinde", "saniyede") ? seconds : 0f;
                    break;
                case MissionType.CoinRain:
                    level.surviveSeconds = seconds > 0 ? seconds : level.surviveSeconds;
                    level.coinTarget = Num(goal, @"(\d+) altın", level.coinTarget);
                    break;
                case MissionType.Survive:
                    level.surviveSeconds = seconds > 0 ? seconds : first;
                    if (Has(goal, "kontrol noktası"))
                    {
                        level.marathon = true;
                        level.rampUp = 0.4f;
                    }
                    break;
                case MissionType.Paint:
                    level.paintTarget = Num(goal, @"en az (\d+)", 0);
                    if (level.paintTarget == 0)
                    {
                        // "30 karoyu sarıya boya": a set number; "64 karonun hepsini boya": all of them.
                        int n = Num(goal, @"(\d+) (?:bulut )?karoyu", 0);
                        if (n > 0 && n < FloorTiles(level)) level.paintTarget = n;
                    }
                    level.timeLimit = Has(goal, "içinde") ? seconds : 0f;
                    break;
                case MissionType.Exit:
                    level.timeLimit = Has(goal, "içinde") ? seconds : 0f;
                    break;
                case MissionType.Quest:
                    level.keys = Mathf.Clamp(first > 0 ? first : 3, 1, 10);
                    level.quest = Has(goal, "lumi", "kafes", "kilit") ? QuestKind.Princess
                        : Has(goal, "tablet", "kayıt", "not") ? QuestKind.Treasure
                        : Has(goal, "fener", "mercek", "paratoner", "ayna", "bayrak", "yıldız") ? QuestKind.Lanterns
                        : Has(goal, "dost", "kafesteki") ? QuestKind.Cages
                        : QuestKind.Cores;
                    break;
                case MissionType.Monster:
                case MissionType.Boss:
                    level.stages = Mathf.Max(1, Num(goal, @"(\d+) aşama", mission == MissionType.Boss ? 3 : 1));
                    // "her aşamada 2 vuruş": per stage; "3 aşamada 9 vuruş", "3 kez", "3 kolunu": the whole fight.
                    int total = Num(goal, @"aşamada (\d+)", Num(goal, @"(\d+) (?:kez|kolunu|vuruş)", 0));
                    level.hitsPerStage = Has(goal, "her aşamada") ? Num(goal, @"her aşamada[^\d]*(\d+)", 2)
                        : total > 0 ? Mathf.Max(1, Mathf.RoundToInt(total / (float)level.stages))
                        : mission == MissionType.Boss ? 1 : 2;
                    level.keys = Mathf.Clamp(Has(goal, "her aşamada") || total <= 0 ? level.stages * level.hitsPerStage : total, 2, 12);
                    level.phased = mission == MissionType.Monster && level.score >= 40;
                    break;
                case MissionType.Thief:
                    level.thiefRace = Has(goal, "önce", "ulaşmadan");
                    level.keys = level.thiefRace ? Num(goal, @"(\d+) altın topla", Num(goal, @"(\d+) altın", 8)) : Mathf.Max(2, Num(goal, @"(\d+) kez", 3));
                    break;
                case MissionType.Escort:
                    level.escortSeconds = Has(goal, "sn", "saniye") ? seconds : 0f;
                    level.escortStops = level.escortSeconds > 0f ? 0 : Num(goal, @"(\d+) (?:şarj|tablet|vana|direğ|ateş böceği|nokta)", 0);
                    break;
                case MissionType.Clone:
                    if (Has(goal, "bayılt"))
                    {
                        int times = Num(goal, @"(\d+)(?:'şer|'er|'ar)? kez", 2);
                        level.cloneKnockouts = Has(goal, "'şer", "'er", "'ar") ? times * 2 : times;
                    }
                    else level.keys = Mathf.Max(3, first > 0 ? first : 4);
                    if (Has(goal, "aşama")) level.cloneKnockouts = Mathf.Max(3, Num(goal, @"(\d+) aşama", 3) * 2);
                    break;
            }
        }

        private static int FloorTiles(LevelData level)
        {
            if (level.layout == null) return level.gridWidth * level.gridHeight;
            int n = 0;
            foreach (var row in level.layout) foreach (char c in row) if (c != '.' && c != 'X') n++;
            return n;
        }

        /// <summary>A plain rectangle with a few stone pillars, never on the edge and never touching each other.</summary>
        private static string[] Rect(int w, int h, int pillars, int seed)
        {
            var grid = new char[h, w];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) grid[y, x] = '#';
            var rng = new System.Random(seed * 13 + 5);
            for (int attempt = 0; attempt < 200 && pillars > 0; attempt++)
            {
                int x = 1 + rng.Next(Mathf.Max(1, w - 2)), y = 1 + rng.Next(Mathf.Max(1, h - 2));
                bool free = true;
                for (int yy = y - 1; yy <= y + 1; yy++) for (int xx = x - 1; xx <= x + 1; xx++)
                    if (yy >= 0 && yy < h && xx >= 0 && xx < w && grid[yy, xx] == 'X') free = false;
                if (!free) continue;
                grid[y, x] = 'X';
                pillars--;
            }
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var chars = new char[w];
                for (int x = 0; x < w; x++) chars[x] = grid[y, x];
                rows[y] = new string(chars);
            }
            return rows;
        }

        /// <summary>The finale's floor: vanG's heart, a heart shape inside a <paramref name="size"/> square.</summary>
        private static string[] Heart(int size)
        {
            var rows = new string[size];
            for (int y = 0; y < size; y++)
            {
                var chars = new char[size];
                for (int x = 0; x < size; x++)
                {
                    // The classic heart curve (x² + y² − 1)³ − x²y³ ≤ 0, row 0 at the top (the far edge).
                    float fx = (x + 0.5f) / size * 2.6f - 1.3f, fy = 1.25f - (y + 0.5f) / size * 2.6f;
                    float a = fx * fx + fy * fy - 1f;
                    chars[x] = a * a * a - fx * fx * fy * fy * fy <= 0f ? '#' : '.';
                }
                rows[y] = new string(chars);
            }
            return rows;
        }
    }
}
