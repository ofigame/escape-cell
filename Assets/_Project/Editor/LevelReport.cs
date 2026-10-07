using System.Globalization;
using System.IO;
using System.Text;
using SquashBot.Data;
using SquashBot.Gameplay;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>
    /// Writes every level's data (mission, floor shape, hazards, rules, design colours, road) as JSON, for the level
    /// design document. Batch: -executeMethod SquashBot.EditorTools.LevelReport.DumpBatch -reportPath &lt;file&gt;
    /// </summary>
    public static class LevelReport
    {
        public static void DumpBatch()
        {
            string path = "Builds/level_report.json";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-reportPath") path = args[i + 1];
            Loc.Set(Language.Turkish);
            File.WriteAllText(path, Build(), new UTF8Encoding(false));
            Debug.Log("[LevelReport] written " + path);
        }

        private static string Build()
        {
            var levels = LevelCatalog.CreateDefault();
            var sb = new StringBuilder();
            sb.Append("[\n");
            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                int world = LevelCatalog.WorldOf(i);
                var theme = WorldTheme.ForLevel(i);
                float d = i / (float)(LevelCatalog.LevelCount - 1);
                sb.Append("{");
                Num(sb, "i", i);
                Num(sb, "world", world);
                Num(sb, "variant", theme == WorldTheme.ForWorld(world) ? 0 : 1);
                Str(sb, "chapter", Loc.T("chapter." + Story.ChapterOf(world)));
                Str(sb, "floorName", Loc.T(theme.key));
                Str(sb, "scenery", theme.scenery.ToString());
                Str(sb, "weather", theme.weather.ToString());
                Str(sb, "block", theme.block.ToString());
                foreach (var (name, c) in new[] { ("bgTop", theme.bgTop), ("bgBottom", theme.bgBottom), ("bgGlow", theme.bgGlow), ("tile", theme.tileTop),
                             ("slab", theme.slab), ("pillar", theme.pillar), ("accent", theme.accent) })
                    Str(sb, name, "#" + ColorUtility.ToHtmlStringRGB(c));
                Str(sb, "mission", l.mission.ToString());
                Str(sb, "quest", l.quest.ToString());
                Str(sb, "shape", l.shapeName ?? "Square");
                Num(sb, "w", l.gridWidth);
                Num(sb, "h", l.gridHeight);
                Num(sb, "keys", l.keys);
                Num(sb, "coinTarget", l.coinTarget);
                Num(sb, "survive", l.surviveSeconds);
                Num(sb, "warning", l.warningTime);
                Num(sb, "interval", l.spawnInterval);
                Num(sb, "perWave", l.blocksPerWave);
                Num(sb, "aim", l.aimAtPlayerChance);
                Num(sb, "ramp", l.rampUp);
                Num(sb, "lines", l.lineWaveChance);
                Num(sb, "bombs", l.bombChance);
                Num(sb, "fire", l.fireChance);
                Num(sb, "linger", l.blockLinger);
                Bool(sb, "holes", l.breakTiles);
                Bool(sb, "collapse", l.collapseBehind);
                Num(sb, "chase", l.chaseSpeed);
                Bool(sb, "lowWalls", l.lowWalls);
                Str(sb, "event", l.levelEvent.ToString());
                Str(sb, "rules", l.rules == FloorRule.None ? "" : l.rules.ToString());
                Bool(sb, "guardsSpirit", l.guardsPrincess);
                Bool(sb, "phased", l.phased);
                Bool(sb, "marathon", l.marathon);
                Bool(sb, "final", l.final);
                Num(sb, "coinInterval", l.coinInterval);
                Num(sb, "powerUps", l.powerUpInterval);
                Num(sb, "score", l.score);
                Str(sb, "helper", l.helper.ToString());
                Num(sb, "timeLimit", l.timeLimit);
                Num(sb, "paintTarget", l.paintTarget);
                Bool(sb, "thiefRace", l.thiefRace);
                Num(sb, "escortSeconds", l.escortSeconds);
                Num(sb, "escortStops", l.escortStops);
                Num(sb, "cloneKnockouts", l.cloneKnockouts);
                Num(sb, "stages", l.stages);
                Num(sb, "hitsPerStage", l.hitsPerStage);
                Bool(sb, "allyKuzgun", l.allyKuzgun);
                Str(sb, "enemies", $"sw{l.sweepers} er{l.erasers} dr{l.drones} wo{l.sandworms} cr{l.crabs} tu{l.turrets} pe{l.penguins} sp{l.springbots}");
                Str(sb, "road", i < LevelCatalog.LevelCount - 1 || l.final ? DuctRunner.RoadTheme(i).ToString() : "");
                Num(sb, "roadLength", l.final ? 260f : 90f + i * 3f);
                Num(sb, "roadPatterns", Mathf.Clamp(3 + i / 3, 3, 18));
                Num(sb, "roadSpeedStart", Mathf.Lerp(4.4f, 6.2f, d));
                Num(sb, "roadSpeedEnd", Mathf.Lerp(6.2f, 9.6f, d));
                if (i % LevelCatalog.LevelsPerWorld == 0)
                {
                    Bool(sb, "opensChapter", Story.OpensChapter(world));
                    Str(sb, "story", Story.Line(world, 0));
                }
                sb.Append("\"layout\":");
                if (l.layout == null || l.layout.Length == 0) sb.Append("null");
                else
                {
                    sb.Append("[");
                    for (int r = 0; r < l.layout.Length; r++) sb.Append(r > 0 ? "," : "").Append('"').Append(l.layout[r]).Append('"');
                    sb.Append("]");
                }
                sb.Append(i < levels.Count - 1 ? "},\n" : "}\n");
            }
            sb.Append("]\n");
            return sb.ToString();
        }

        private static void Str(StringBuilder sb, string key, string value) =>
            sb.Append('"').Append(key).Append("\":\"").Append((value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",");

        private static void Num(StringBuilder sb, string key, float value) =>
            sb.Append('"').Append(key).Append("\":").Append(value.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');

        private static void Bool(StringBuilder sb, string key, bool value) => sb.Append('"').Append(key).Append("\":").Append(value ? "true" : "false").Append(',');
    }
}
