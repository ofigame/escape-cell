using System;
using System.IO;
using BrickMuseum.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BrickMuseum.EditorTools
{
    /// <summary>
    /// One-click setup (safe to run again): URP settings, base materials in Resources (so their shader variants ship),
    /// the procedural sky, the single scene with the Game object, player settings; plus the test builds.
    /// </summary>
    public static class BrickSetup
    {
        private const string Root = "Assets/_Project";
        private const string MatsDir = Root + "/Resources/Mats";
        private const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("Brickville/Setup Project")]
        public static void Setup()
        {
            Directory.CreateDirectory(MatsDir);
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();
            SetupRenderPipeline();
            CreateMaterials();
            CreateScene();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[BrickMuseum] Setup complete.");
        }

        public static void SetupAndBuildBatch()
        {
            try { Setup(); BuildWindows(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Brickville/Build Windows (test)")]
        public static void BuildWindows()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/Brickville.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Debug.Log($"[BrickMuseum] Windows build: {report.summary.result}");
        }

        public static void BuildAndroidBatch()
        {
            try
            {
                Setup();
                var target = NamedBuildTarget.Android;
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.bundleVersion = "0.1";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = "Builds/Android/Brickville.apk",
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                });
                Debug.Log($"[BrickMuseum] Android build: {report.summary.result}");
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void SetupRenderPipeline()
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
                if (urp != null) GraphicsSettings.defaultRenderPipeline = urp;
            }
            void Tune(UniversalRenderPipelineAsset a)
            {
                if (a == null) return;
                a.supportsHDR = true;
                a.msaaSampleCount = a.name.Contains("Mobile") ? 2 : 4;
                a.renderScale = 1f;
                a.shadowDistance = 160f;
                EditorUtility.SetDirty(a);
            }
            Tune(urp);
            for (int i = 0; i < QualitySettings.names.Length; i++) Tune(QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset);
        }

        private static void CreateMaterials()
        {
            Material Make(string name, string shader, Action<Material> configure)
            {
                string path = $"{MatsDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
                configure(m);
                EditorUtility.SetDirty(m);
                return m;
            }
            // URP drops _EMISSION when the colour is black, which would strip the variant: keep white in the base.
            Make("Lit", "Universal Render Pipeline/Lit", m => { m.SetColor("_EmissionColor", Color.white); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; });
            Make("LitTransparent", "Universal Render Pipeline/Lit", m => { Build.Mats.MakeTransparent(m); m.SetColor("_EmissionColor", Color.white); m.EnableKeyword("_EMISSION"); });
            Make("Unlit", "Universal Render Pipeline/Unlit", m => { });
            Make("UnlitTransparent", "Universal Render Pipeline/Unlit", m => Build.Mats.MakeTransparent(m));
            Make("Sky", "Skybox/Procedural", m =>
            {
                m.SetFloat("_SunDisk", 2f);
                m.SetFloat("_SunSize", 0.035f);
                m.SetFloat("_AtmosphereThickness", 0.85f);
                m.SetColor("_SkyTint", new Color(0.45f, 0.58f, 0.78f));
                m.SetColor("_GroundColor", new Color(0.55f, 0.62f, 0.55f));
                m.SetFloat("_Exposure", 1.25f);
            });
        }

        private static void CreateScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Game").AddComponent<Game>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "OFIGAME";
            PlayerSettings.productName = "Brickville";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ofigame.brickville");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.ofigame.brickville");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.ofigame.brickville");
            // portrait for building; the game turns the screen to landscape for walking (TPS), so both must be allowed
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
        }
    }
}
