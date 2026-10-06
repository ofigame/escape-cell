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
    /// Development tool: renders the app icon with the real game assets, then quits.
    ///   EscapeCell.exe -renderIcon &lt;folder&gt; [design]          → icon.png + icon_adaptive.png
    ///   EscapeCell.exe -renderIcon &lt;folder&gt; [design] -preview → &lt;design&gt;.png only (for comparing ideas)
    /// Designs: calm, sunny, hero, candy, neon.
    /// </summary>
    public class IconRenderer : MonoBehaviour
    {
        private const int Size = 1024;

        private class Design
        {
            public int theme;            // world theme for the platform
            public int robotWorld;       // robot look
            public Texture2D background; // null = flat color
            public Color flat;
            public float pitch = 20f;
            public float halfHeight = 0.62f;
            public Vector3 focusOffset = new Vector3(0f, 0.42f, 0f);
            public Action<Robot, Transform> dress; // pose the robot and add props (props go under the given root)
            public bool burst;           // fire an impact burst right before the capture
        }

        public static bool TryRun()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-renderIcon");
            if (i < 0 || i + 1 >= args.Length) return false;
            string design = i + 2 < args.Length && !args[i + 2].StartsWith("-") ? args[i + 2] : "calm";
            bool preview = Array.IndexOf(args, "-preview") >= 0;

            var go = new GameObject("IconRenderer");
            var renderer = go.AddComponent<IconRenderer>();
            renderer.StartCoroutine(renderer.Render(args[i + 1], design, preview));
            return true;
        }

        private IEnumerator Render(string folder, string designName, bool preview)
        {
            Directory.CreateDirectory(folder);
            var design = Make(designName);
            WorldTheme.SetCurrent(design.theme);

            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(45f, 25f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Palette.Ambient * 0.9f;

            var fx = new GameObject("FX").AddComponent<FxSystem>();
            var grid = new GridModel(3, 3);
            var gridView = new GameObject("Grid").AddComponent<GridView>();
            gridView.Build(grid, fx);

            var robot = Robot.Create(null);
            robot.Spawn(grid, new GridPos(1, 1));
            robot.ApplyWorld(design.robotWorld);
            CuteFace(robot);

            var props = new GameObject("Props").transform;
            design.dress?.Invoke(robot, props);

            var cam = CreateCamera(design, out var backdrop);

            // Let animations, the bloom history and the robot's idle settle.
            for (int f = 0; f < 30; f++) yield return null;

            var focus = new Vector3(1f, 0f, 1f) + design.focusOffset;
            if (preview)
            {
                yield return Capture(cam, backdrop, design, focus, design.halfHeight, Path.Combine(folder, designName + ".png"), fx);
            }
            else
            {
                yield return Capture(cam, backdrop, design, focus, design.halfHeight, Path.Combine(folder, "icon.png"), fx);
                // Adaptive icons are masked to roughly the middle two thirds, so frame wider.
                yield return Capture(cam, backdrop, design, focus, design.halfHeight * 1.5f, Path.Combine(folder, "icon_adaptive.png"), fx);
            }

            Debug.Log("[EscapeCell] Icon '" + designName + "' written to " + folder);
            Application.Quit();
        }

        // ---------- Designs ----------

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        private static Design Make(string name)
        {
            switch (name)
            {
                case "sunny": // a happy jump on a sunburst: bright, warm, full of motion
                    return new Design
                    {
                        theme = 0, robotWorld = 0, pitch = 16f, halfHeight = 0.68f, focusOffset = new Vector3(0f, 0.58f, 0f),
                        background = BackgroundArt.Sunburst(Size, Hex("#FFF6C8"), Hex("#FF8A3D"), Hex("#FFD35C"), Hex("#FFB03B"), 14, new Vector2(0.5f, 0.55f)),
                        dress = (robot, props) =>
                        {
                            Jump(robot, 0.32f);
                            SpeedLines(props, new Vector3(1f, 0.12f, 1f), vertical: true);
                            Block(props, 0, new Vector3(0.68f, 1.12f, 1.32f), new Vector3(10f, 25f, -12f), 0.38f);
                            Coin(props, new Vector3(1.38f, 0.78f, 0.62f), 0.5f);
                            Coin(props, new Vector3(1.3f, 1.12f, 0.95f), 0.36f);
                        },
                    };
                case "hero": // a big friendly golden face with sparkles
                    return new Design
                    {
                        theme = 0, robotWorld = 14, pitch = 12f, halfHeight = 0.5f, focusOffset = new Vector3(0f, 0.5f, 0f),
                        background = BackgroundArt.Radial(Size, Hex("#9BFFF0"), Hex("#1A7FA0"), new Vector2(0.5f, 0.55f), 40, Color.white),
                        dress = (robot, props) =>
                        {
                            Star(props, new Vector3(0.45f, 1.0f, 1.35f), 0.16f);
                            Star(props, new Vector3(1.55f, 0.95f, 0.6f), 0.12f);
                            Star(props, new Vector3(1.6f, 0.45f, 0.75f), 0.08f);
                        },
                    };
                case "candy": // pink candy land, waving hello, gumdrops raining
                    return new Design
                    {
                        theme = 8, robotWorld = 8, pitch = 18f, halfHeight = 0.72f, focusOffset = new Vector3(0f, 0.55f, 0f),
                        background = BackgroundArt.Sunburst(Size, Hex("#FFE6F6"), Hex("#B84FD8"), Hex("#FF8FD6"), Hex("#FF6EC2"), 18, new Vector2(0.5f, 0.5f)),
                        dress = (robot, props) =>
                        {
                            Block(props, 8, new Vector3(0.68f, 1.12f, 1.32f), new Vector3(-8f, 20f, 15f), 0.34f);
                            Block(props, 8, new Vector3(1.36f, 0.95f, 0.66f), new Vector3(12f, -10f, -20f), 0.26f);
                            Star(props, new Vector3(1.5f, 0.6f, 0.45f), 0.1f);
                            Coin(props, new Vector3(0.66f, 0.62f, 1.34f), 0.38f);
                        },
                    };
                case "neon": // a last-second dodge as a block slams down beside the robot
                    return new Design
                    {
                        theme = 3, robotWorld = 13, pitch = 22f, halfHeight = 0.58f, focusOffset = new Vector3(0.18f, 0.66f, -0.22f),
                        background = BackgroundArt.Radial(Size, Hex("#5B3FD0"), Hex("#120A35"), new Vector2(0.5f, 0.5f), 60, Hex("#FFB8F5")),
                        burst = true,
                        dress = (robot, props) =>
                        {
                            robot.transform.position += new Vector3(0.25f, 0.25f, -0.25f);
                            robot.transform.rotation = Quaternion.AngleAxis(-16f, new Vector3(1f, 0f, 1f).normalized); // lean into the dodge
                            SpeedLines(props, new Vector3(0.95f, 0.66f, 1.05f), vertical: false);
                            Block(props, 0, new Vector3(0.66f, 0.48f, 1.5f), Vector3.zero, 1f);
                        },
                    };
                default: // calm: the current icon
                    return new Design { theme = 0, robotWorld = 14, flat = Palette.BgTop };
            }
        }

        // ---------- Robot styling ----------

        /// <summary>A wide, confident grin and big eyes: friendly and lively without looking cutesy.</summary>
        private static void CuteFace(Robot robot)
        {
            var visual = robot.transform.Find("Lift/Visual");
            var mouth = MaterialFactory.Create(Hex("#232842"), Color.black);
            Shapes.Rounded("Grin", visual, new Vector3(0f, 0.33f, 0.226f), new Vector3(0.12f, 0.026f, 0.012f), 0.01f, mouth);
            Shapes.Rounded("GrinL", visual, new Vector3(-0.072f, 0.344f, 0.226f), new Vector3(0.045f, 0.026f, 0.012f), 0.01f, mouth)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -38f);
            Shapes.Rounded("GrinR", visual, new Vector3(0.072f, 0.344f, 0.226f), new Vector3(0.045f, 0.026f, 0.012f), 0.01f, mouth)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 38f);

            foreach (var eye in new[] { "EyeL", "EyeR" })
            {
                var t = visual.Find(eye);
                if (t != null) t.localScale = new Vector3(1.3f, 1.45f, 1f);
            }
        }

        private static void Jump(Robot robot, float height) => robot.transform.position += Vector3.up * height;

        /// <summary>Rotate the arms around the shoulders (degrees, positive = raised outward).</summary>
        private static void Arms(Robot robot, float left, float right)
        {
            var visual = robot.transform.Find("Lift/Visual");
            Arm(visual.Find("ArmL"), -1f, left);
            Arm(visual.Find("ArmR"), 1f, right);
        }

        private static void Arm(Transform arm, float side, float degrees)
        {
            if (arm == null || Mathf.Approximately(degrees, 0f)) return;
            // Pivot around the shoulder (top of the arm) instead of the arm's centre.
            var shoulder = new Vector3(side * 0.18f, 0.27f, 0f);
            float rad = degrees * Mathf.Deg2Rad;
            var hang = new Vector3(0f, -0.07f, 0f);
            var rotated = new Vector3(hang.x * Mathf.Cos(rad) - hang.y * Mathf.Sin(rad), hang.x * Mathf.Sin(rad) + hang.y * Mathf.Cos(rad), 0f);
            arm.localPosition = shoulder + rotated + new Vector3(side * 0.02f, 0f, 0f);
            arm.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        // ---------- Props ----------

        private static void Coin(Transform root, Vector3 position, float scale)
        {
            var coin = new GameObject("Coin").transform;
            coin.SetParent(root, false);
            coin.position = position;
            coin.localScale = Vector3.one * scale;
            coin.rotation = Quaternion.Euler(70f, 45f, 20f);
            Shapes.Primitive(PrimitiveType.Cylinder, "Rim", coin, Vector3.zero, new Vector3(0.46f, 0.035f, 0.46f), MaterialFactory.Create(Palette.CoinRim, Palette.CoinGlow * 0.4f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Face", coin, Vector3.zero, new Vector3(0.36f, 0.045f, 0.36f), MaterialFactory.Create(Palette.Coin, Palette.CoinGlow));
        }

        private static void Block(Transform root, int world, Vector3 position, Vector3 euler, float scale)
        {
            var block = HazardVisuals.Block(world).transform;
            block.SetParent(root, false);
            block.position = position;
            block.rotation = Quaternion.Euler(euler);
            block.localScale = Vector3.one * scale;
        }

        /// <summary>A four-pointed sparkle star.</summary>
        private static void Star(Transform root, Vector3 position, float size)
        {
            var material = MaterialFactory.Create(Hex("#FFF2A8"), new Color(2.6f, 2.2f, 0.9f));
            var star = new GameObject("Star").transform;
            star.SetParent(root, false);
            star.position = position;
            star.rotation = Quaternion.Euler(20f, 45f, 0f);
            Shapes.Rounded("A", star, Vector3.zero, new Vector3(size * 0.25f, size * 1.4f, size * 0.25f), size * 0.1f, material);
            Shapes.Rounded("B", star, Vector3.zero, new Vector3(size * 1.4f, size * 0.25f, size * 0.25f), size * 0.1f, material)
                .transform.localRotation = Quaternion.identity;
        }

        /// <summary>White motion streaks behind a moving robot.</summary>
        private static void SpeedLines(Transform root, Vector3 center, bool vertical)
        {
            var material = MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.55f), new Color(0.6f, 0.6f, 0.6f));
            for (int i = -1; i <= 1; i++)
            {
                var size = vertical ? new Vector3(0.03f, 0.26f - Mathf.Abs(i) * 0.06f, 0.03f) : new Vector3(0.3f - Mathf.Abs(i) * 0.08f, 0.03f, 0.03f);
                var offset = vertical ? new Vector3(i * 0.12f, -Mathf.Abs(i) * 0.04f, -i * 0.12f) : new Vector3(-i * 0.1f, i * 0.12f, -i * 0.1f);
                var line = Shapes.Rounded("Speed", root, Vector3.zero, size, 0.015f, material);
                line.transform.position = center + offset;
                line.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            }
        }

        // ---------- Camera & capture ----------

        private static Camera CreateCamera(Design design, out Transform backdrop)
        {
            var cam = new GameObject("IconCamera").AddComponent<Camera>();
            cam.orthographic = true; // the game's own isometric look
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = design.background == null ? design.flat : Color.black;
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
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(design.background != null ? 12f : 0f); // a little extra punch on the colorful designs

            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            backdrop = quad.transform;
            backdrop.SetParent(cam.transform, false);
            if (design.background != null) quad.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.CreateUnlit(design.background);
            else quad.SetActive(false); // flat color only
            return cam;
        }

        /// <summary>
        /// Captures what the camera puts on screen (HDR, bloom, tonemapping: exactly the in-game look),
        /// supersampled from the 512x512 window to a 1024x1024 image.
        /// </summary>
        private static IEnumerator Capture(Camera cam, Transform backdrop, Design design, Vector3 focus, float halfHeight, string path, FxSystem fx)
        {
            cam.transform.rotation = Quaternion.Euler(design.pitch, 45f, 0f);
            cam.transform.position = focus - cam.transform.forward * 30f;
            cam.orthographicSize = halfHeight;

            float h = 2f * halfHeight;
            backdrop.localPosition = new Vector3(0f, 0f, 100f);
            backdrop.localScale = new Vector3(h * cam.aspect, h, 1f);

            for (int f = 0; f < 14; f++) yield return null;
            if (design.burst)
            {
                fx.Burst(new Vector3(0.66f, 0.15f, 1.5f), Palette.Block, Palette.BlockGlow, 40, 3.5f);
                for (int f = 0; f < 6; f++) yield return null;
            }
            yield return new WaitForEndOfFrame();

            int superSize = Mathf.Max(1, Mathf.RoundToInt(Size / (float)Screen.height));
            var shot = ScreenCapture.CaptureScreenshotAsTexture(superSize);
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Destroy(shot);
        }
    }
}
