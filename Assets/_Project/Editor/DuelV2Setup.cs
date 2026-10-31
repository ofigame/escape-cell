using System;
using System.IO;
using SquashBot.DuelV2;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SquashBot.EditorTools
{
    /// <summary>
    /// The duel fight core v2's prototype (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 9): its scene (the game's main scene's copy
    /// with the prototype on it) and its Windows build (Builds/DuelPrototypeV2, then zipped by Tools/build-duelv2.sh).
    /// </summary>
    public static class DuelV2Setup
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dev/DuelPrototypeV2.unity";
        private const string MainScene = "Assets/_Project/Scenes/Main.unity";

        [MenuItem("Squash Bot/Duel V2/Make Prototype Scene")]
        public static void MakeScene()
        {
            Directory.CreateDirectory("Assets/_Project/Scenes/Dev");
            if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset(MainScene, ScenePath);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (UnityEngine.Object.FindAnyObjectByType<DuelPrototypeV2>() == null)
                new GameObject("DuelPrototypeV2").AddComponent<DuelPrototypeV2>();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DuelV2] scene " + ScenePath);
        }

        public static void MakeSceneBatch()
        {
            try { MakeScene(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>The bot-against-bot balance report (Faz 8), by the rules alone: -benchMatches n -benchOut file.</summary>
        public static void BenchBatch()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                int mi = Array.IndexOf(args, "-benchMatches"), oi = Array.IndexOf(args, "-benchOut");
                int n = mi >= 0 ? int.Parse(args[mi + 1]) : 500;
                string outFile = oi >= 0 ? args[oi + 1] : "Builds/DuelV2Bench.md";
                var t0 = DateTime.Now;
                string report = DuelBenchV2.Run(n, Math.Max(40, n / 5), 3, 12345);
                File.WriteAllText(outFile, report + "\n(" + (DateTime.Now - t0).TotalSeconds.ToString("0.0") + " s)\n");
                Debug.Log("[DuelV2] bench written " + outFile);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>The prototype as a Windows exe: windowed, upright (the game's desktop window), its scene only.</summary>
        public static void BuildBatch()
        {
            try
            {
                SquashBotSetup.Setup();
                MakeScene();
                string name = PlayerSettings.productName;
                PlayerSettings.productName = "foi Duel V2";
                BuildReport report;
                try
                {
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { ScenePath },
                        locationPathName = "Builds/DuelPrototypeV2/foiDuelV2.exe",
                        target = BuildTarget.StandaloneWindows64,
                        options = BuildOptions.None,
                    });
                }
                finally
                {
                    PlayerSettings.productName = name;
                    AssetDatabase.SaveAssets();
                }
                Debug.Log($"[DuelV2] Windows build: {report.summary.result}");
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
