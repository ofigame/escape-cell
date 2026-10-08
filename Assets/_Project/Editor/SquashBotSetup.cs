using System;
using System.IO;
using SquashBot.Data;
using SquashBot.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SquashBot.EditorTools
{
    /// <summary>
    /// One-click project setup: URP + bloom-ready pipeline, base material, level asset, main scene, player settings.
    /// Safe to run again; existing assets are reused.
    /// </summary>
    public static class SquashBotSetup
    {
        private const string Root = "Assets/_Project";
        private const string SettingsDir = Root + "/Settings";
        private const string ResourcesDir = Root + "/Resources";
        private const string ScenesDir = Root + "/Scenes";
        private const string ScenePath = ScenesDir + "/Main.unity";

        [MenuItem("Squash Bot/Setup Project")]
        public static void Setup()
        {
            EnsureFolders();
            ImportTextMeshProEssentials();
            SetupRenderPipeline();
            CreateBaseMaterial();
            ForestAssetBuilder.Build();
            var levelSet = CreateLevelSet();
            CreateMainScene(levelSet);
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[SquashBot] Setup complete. Open Assets/_Project/Scenes/Main.unity and press Play.");
        }

        /// <summary>Entry point for: Unity -batchmode -executeMethod SquashBot.EditorTools.SquashBotSetup.SetupBatch</summary>
        public static void SetupBatch()
        {
            try
            {
                Setup();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Squash Bot/Build Windows Test")]
        public static void BuildWindowsTest()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/EscapeCell.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Debug.Log($"[SquashBot] Windows build: {report.summary.result}");
        }

        public static void SetupAndBuildBatch()
        {
            try
            {
                Setup();
                BuildWindowsTest();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Squash Bot/Build Android APK (test)")]
        public static void BuildAndroidApk()
        {
            ConfigureAndroidTools();

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP); // the only backend this Unity install ships for Android
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64; // 32-bit too: many tablets (e.g. Huawei) run 32-bit Android
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.bundleVersion = "0.2";
            EditorUserBuildSettings.buildAppBundle = false;
            ResolveAndroidDependencies();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/EscapeCell.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Debug.Log($"[SquashBot] Android build: {report.summary.result} ({report.summary.totalErrors} errors)");
        }

        public static void BuildAndroidBatch()
        {
            try
            {
                Setup();
                BuildAndroidApk();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Lets the External Dependency Manager write the ad SDKs (Google Mobile Ads and the mediation adapters) into the
        /// custom Gradle templates under Assets/Plugins/Android, so Gradle fetches them during the build. Reflection keeps
        /// this file compiling without the package.
        /// </summary>
        private static void ResolveAndroidDependencies()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var resolver = asm.GetType("GooglePlayServices.PlayServicesResolver");
                if (resolver == null) continue;
                var resolve = resolver.GetMethod("ResolveSync", new[] { typeof(bool) });
                if (resolve == null) break;
                bool ok = (bool)resolve.Invoke(null, new object[] { true });
                Debug.Log("[SquashBot] Android dependencies resolved: " + ok);
                return;
            }
            Debug.LogWarning("[SquashBot] External Dependency Manager not found: ad SDKs are not resolved.");
        }

        /// <summary>
        /// Points Unity at an existing Android Studio install (SDK, NDK, bundled JDK) when the Hub's own
        /// Android tools are not installed. Uses reflection so this file still compiles without the Android module.
        /// </summary>
        private static void ConfigureAndroidTools()
        {
            var settings = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
            if (settings == null) throw new Exception("Android Build Support module is not installed.");

            string sdk = Environment.GetEnvironmentVariable("ANDROID_HOME");
            if (string.IsNullOrEmpty(sdk)) sdk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk");
            // A JDK 17 kept next to the project (E:\OFIGAME\Tools\jdk-17), since Unity rejects Android Studio's JDK 21.
            string jdk = Path.GetFullPath(Path.Combine("..", "Tools", "jdk-17"));

            void Set(string property, string value)
            {
                var p = settings.GetProperty(property);
                if (p == null || string.IsNullOrEmpty(value) || !Directory.Exists(value)) return;
                try
                {
                    p.SetValue(null, value);
                    Debug.Log($"[SquashBot] Android {property}: {p.GetValue(null)}");
                }
                catch (Exception e)
                {
                    // e.g. an NDK version Unity does not accept; Mono builds can go ahead without it.
                    Debug.LogWarning($"[SquashBot] Android {property} not set: {(e.InnerException ?? e).Message}");
                }
            }

            Set("sdkRootPath", sdk);
            Set("jdkRootPath", jdk);
            // Unity requires exactly NDK r27c; it is kept next to the project as well.
            Set("ndkRootPath", Path.GetFullPath(Path.Combine("..", "Tools", "android-ndk-r27c")));
        }

        private const string IconDir = Root + "/Icon";

        /// <summary>
        /// Imports the icons rendered by the player ("-renderIcon Builds/Icon") and assigns them:
        /// a default icon for every platform (iOS scales it) and Android legacy, round and adaptive icons.
        /// </summary>
        [MenuItem("Squash Bot/Apply App Icon")]
        public static void ApplyIcons()
        {
            Directory.CreateDirectory(IconDir);
            File.Copy("Builds/Icon/icon.png", IconDir + "/AppIcon.png", true);
            File.Copy("Builds/Icon/icon_adaptive.png", IconDir + "/AppIconAdaptiveBackground.png", true);

            // Adaptive icons need a foreground layer; ours is baked into the background, so it stays empty.
            var clear = new Texture2D(432, 432, TextureFormat.RGBA32, false);
            clear.SetPixels32(new Color32[432 * 432]);
            File.WriteAllBytes(IconDir + "/AppIconAdaptiveForeground.png", clear.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(clear);
            AssetDatabase.Refresh();

            Texture2D Import(string name)
            {
                string path = IconDir + "/" + name;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = name.Contains("Foreground");
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            var icon = Import("AppIcon.png");
            var background = Import("AppIconAdaptiveBackground.png");
            var foreground = Import("AppIconAdaptiveForeground.png");

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            // Android icon kinds live in the Android module; look them up by name so this compiles without it.
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                bool adaptive = kind.ToString().Contains("Adaptive");
                foreach (var platformIcon in icons)
                {
                    if (adaptive) platformIcon.SetTextures(background, foreground);
                    else platformIcon.SetTexture(icon);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[EscapeCell] App icon applied.");
        }

        public static void ApplyIconsBatch()
        {
            try
            {
                ApplyIcons();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Squash Bot/Reset Levels To Default")]
        public static void ResetLevels()
        {
            AssetDatabase.DeleteAsset(ResourcesDir + "/LevelSet.asset");
            CreateLevelSet(); // the scene's reference becomes empty, so GameManager loads this one from Resources
            AssetDatabase.SaveAssets();
            Debug.Log("[SquashBot] Levels reset to the built-in defaults.");
        }

        /// <summary>TextMeshPro needs its default font and shaders imported once into the project.</summary>
        private static void ImportTextMeshProEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) return;
            var ugui = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.ugui");
            if (ugui == null) throw new Exception("com.unity.ugui package not found; TextMeshPro resources cannot be imported.");
            string package = Path.Combine(ugui.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();
        }

        [MenuItem("Squash Bot/Reset Save Data")]
        public static void ResetSave()
        {
            PlayerPrefs.DeleteAll();
            Debug.Log("[SquashBot] Save data cleared.");
        }

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { SettingsDir, ResourcesDir, ScenesDir })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        private static void SetupRenderPipeline()
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                // URP templates assign the pipeline per quality level (PC / Mobile); reuse that.
                urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
                if (urp != null) GraphicsSettings.defaultRenderPipeline = urp;
            }
            if (urp == null)
            {
                string rendererPath = SettingsDir + "/SquashBot_Renderer.asset";
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    if (renderer.postProcessData == null)
                        renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                }

                string urpPath = SettingsDir + "/SquashBot_URP.asset";
                urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
                if (urp == null)
                {
                    urp = UniversalRenderPipelineAsset.Create(renderer);
                    AssetDatabase.CreateAsset(urp, urpPath);
                }

                GraphicsSettings.defaultRenderPipeline = urp;
            }

            // HDR is needed for emission values > 1 to bloom. Keep each quality level's own asset (PC / Mobile).
            EnableHdr(urp);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                EnableHdr(QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset);
        }

        private static void EnableHdr(UniversalRenderPipelineAsset asset)
        {
            if (asset == null) return;
            asset.supportsHDR = true;      // emission > 1 blooms
            // Smooth edges on the rounded meshes; phones get 2x so older devices keep a steady frame rate.
            asset.msaaSampleCount = asset.name.Contains("Mobile") ? 2 : 4;
            asset.renderScale = 1f;        // always render at native resolution
            EditorUtility.SetDirty(asset);
        }

        private static void CreateBaseMaterial()
        {
            string path = ResourcesDir + "/SquashBot_BaseLit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            // URP drops the _EMISSION keyword when the emission color is black, which would also strip
            // the emissive shader variant from builds. A non-black color keeps it; runtime copies recolor it.
            material.SetColor("_EmissionColor", Color.white);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetFloat("_Smoothness", 0.15f); // soft, matte pastel look
            EditorUtility.SetDirty(material);

            // Unlit base for the painted background (kept in Resources so the shader ships in builds).
            string transparentPath = ResourcesDir + "/SquashBot_BaseTransparent.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(transparentPath) == null)
            {
                var transparent = new Material(material);
                SquashBot.Visual.MaterialFactory.ConfigureTransparent(transparent);
                AssetDatabase.CreateAsset(transparent, transparentPath);
            }

            string unlitPath = ResourcesDir + "/SquashBot_BaseUnlit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(unlitPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Unlit")), unlitPath);

            // Particle materials (weather, speed lines, sparks): additive glow and soft alpha, kept in Resources so the
            // particle shader and these variants ship in builds.
            CreateParticleMaterial(ResourcesDir + "/SquashBot_ParticleAdd.mat", additive: true);
            CreateParticleMaterial(ResourcesDir + "/SquashBot_ParticleAlpha.mat", additive: false);
        }

        private static void CreateParticleMaterial(string path, bool additive)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
        }

        private static LevelSet CreateLevelSet()
        {
            string path = ResourcesDir + "/LevelSet.asset";
            var set = AssetDatabase.LoadAssetAtPath<LevelSet>(path);
            if (set != null) return set;

            set = ScriptableObject.CreateInstance<LevelSet>();
            set.levels = LevelCatalog.CreateDefault();
            AssetDatabase.CreateAsset(set, path);
            return set;
        }

        private static void CreateMainScene(LevelSet levelSet)
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var manager = new GameObject("SquashBot").AddComponent<GameManager>();
                var so = new SerializedObject(manager);
                so.FindProperty("levelSet").objectReferenceValue = levelSet;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "OFIGAME";
            PlayerSettings.productName = "Escape Cell";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ofigame.escapecell");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.ofigame.escapecell");
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.appleEnableAutomaticSigning = false; // the cloud build compiles unsigned; signing happens at install time
            // Less code for IL2CPP to convert: keeps the cloud runner's memory in check (exit code 137 = out of memory).
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.iOS, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // White ice from the very first frame: the native launch screen (no Unity logo) shows the same ice as the game's
            // own splash, where the OFIGAME logo then fades in and stays until the menu is ready.
            var iceSprite = SplashSprite(ResourcesDir + "/IceBackground.png");
            PlayerSettings.SplashScreen.show = iceSprite != null;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.blurBackgroundImage = false;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.DarkOnLight;
            PlayerSettings.SplashScreen.backgroundColor = Color.white;
            PlayerSettings.SplashScreen.background = iceSprite;
            PlayerSettings.SplashScreen.backgroundPortrait = iceSprite;
            // The native screen shows the ice only: the logo appears once, in the game's own splash, so it never jumps.
            PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[0];
        }

        /// <summary>Imports a texture as an uncompressed single sprite (the splash logo and background) and returns it.</summary>
        private static Sprite SplashSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
