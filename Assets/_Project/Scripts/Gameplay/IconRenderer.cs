using System;
using System.Collections;
using System.IO;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Development tool: launched with "-renderIcon &lt;folder&gt;", the player renders the app icon with the real game
    /// assets (the fully evolved robot on a neon platform, a falling block, a coin) and quits.
    /// Writes icon.png (tight framing for iOS / legacy icons) and icon_adaptive.png (wider, for Android adaptive icons).
    /// </summary>
    public class IconRenderer : MonoBehaviour
    {
        private const int Size = 1024;
        private const int RobotWorld = 14; // the most evolved look: golden body, star crown, orbiting planet

        public static bool TryRun()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-renderIcon");
            if (i < 0 || i + 1 >= args.Length) return false;
            var go = new GameObject("IconRenderer");
            var renderer = go.AddComponent<IconRenderer>();
            renderer.StartCoroutine(renderer.Render(args[i + 1]));
            return true;
        }

        private IEnumerator Render(string folder)
        {
            Directory.CreateDirectory(folder);
            WorldTheme.SetCurrent(0); // the signature lavender world

            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, 20f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Palette.Ambient * 0.85f;

            // The game area: a 3x3 neon platform.
            var fx = new GameObject("FX").AddComponent<FxSystem>();
            var grid = new GridModel(3, 3);
            var gridView = new GameObject("Grid").AddComponent<GridView>();
            gridView.Build(grid, fx);

            var robot = Robot.Create(null);
            robot.Spawn(grid, new GridPos(1, 1));
            robot.ApplyWorld(RobotWorld);

            // A block plunging toward the back tile (with its red warning) and a coin up front.
            var block = HazardVisuals.Block(0);
            block.transform.position = new Vector3(0.95f, 1.2f, 1.3f);
            block.transform.localScale = Vector3.one * 0.5f;
            block.transform.rotation = Quaternion.Euler(8f, 20f, -6f);
            var coinMat = MaterialFactory.Create(Palette.Coin, Palette.CoinGlow);
            var rimMat = MaterialFactory.Create(Palette.CoinRim, Palette.CoinGlow * 0.4f);
            var coin = new GameObject("Coin").transform;
            coin.position = new Vector3(1.75f, 0.62f, 0.85f);
            coin.localScale = Vector3.one * 0.75f;
            Shapes.Primitive(PrimitiveType.Cylinder, "Rim", coin, Vector3.zero, new Vector3(0.46f, 0.035f, 0.46f), rimMat);
            Shapes.Primitive(PrimitiveType.Cylinder, "Face", coin, Vector3.zero, new Vector3(0.36f, 0.045f, 0.36f), coinMat);

            var cam = CreateCamera(out var backdrop);
            coin.rotation = Quaternion.FromToRotation(Vector3.up, -cam.transform.forward);

            // Let animations, the bloom history and the robot's idle settle.
            for (int f = 0; f < 30; f++)
            {
                gridView.SetWarning(new GridPos(1, 2), 0.9f);
                yield return null;
            }

            var focus = robot.transform.position + new Vector3(0f, 0.5f, 0f);
            yield return Capture(cam, backdrop, focus, 0.92f, Path.Combine(folder, "icon.png"), gridView);
            // Adaptive icons are masked to roughly the middle two thirds, so frame wider.
            yield return Capture(cam, backdrop, focus, 1.4f, Path.Combine(folder, "icon_adaptive.png"), gridView);

            Debug.Log("[EscapeCell] Icons written to " + folder);
            Application.Quit();
        }

        private static Camera CreateCamera(out Transform backdrop)
        {
            var cam = new GameObject("IconCamera").AddComponent<Camera>();
            cam.orthographic = true; // the game's own isometric look
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.BgBottom;
            cam.allowMSAA = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(1.2f);
            bloom.scatter.Override(0.6f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.CreateUnlit(BackgroundArt.Generate(Size, Size));
            backdrop = quad.transform;
            backdrop.SetParent(cam.transform, false);
            return cam;
        }

        /// <summary>
        /// Captures what the camera puts on screen (HDR, bloom, tonemapping: exactly the in-game look),
        /// supersampled from the 512x512 window to a 1024x1024 image.
        /// </summary>
        private static IEnumerator Capture(Camera cam, Transform backdrop, Vector3 focus, float halfHeight, string path, GridView gridView)
        {
            cam.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            cam.transform.position = focus - cam.transform.forward * 30f;
            cam.orthographicSize = halfHeight;

            float depth = 100f;
            float h = 2f * halfHeight;
            backdrop.localPosition = new Vector3(0f, 0f, depth);
            backdrop.localScale = new Vector3(h * cam.aspect, h, 1f);

            for (int f = 0; f < 20; f++)
            {
                gridView.SetWarning(new GridPos(1, 2), 0.9f);
                yield return null;
            }
            yield return new WaitForEndOfFrame();

            int superSize = Mathf.Max(1, Mathf.RoundToInt(Size / (float)Screen.height));
            var shot = ScreenCapture.CaptureScreenshotAsTexture(superSize);
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Destroy(shot);
        }
    }
}
