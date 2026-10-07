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
    /// Designs: calm, sunny, hero, candy, neon, and the character-first escape, shield, crown, warden, dodge.
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
            public Vector3? burstAt;     // ... or at this point
            public bool noFloor;         // hide the platform (the robot stands on light)
            public float bloom = 1.2f;
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

            if (design.noFloor) gridView.gameObject.SetActive(false);

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
                // ----- Character-first designs: the robot fills the icon so it reads at home-screen size. -----
                case "escape": // arms up with the key, a block falling behind, on a hot orange sunburst
                    return new Design
                    {
                        theme = 0, robotWorld = 13, pitch = 10f, halfHeight = 0.46f, focusOffset = new Vector3(0f, 0.44f, 0f),
                        background = BackgroundArt.Sunburst(Size, Hex("#FFF1C9"), Hex("#FF5A36"), Hex("#FFB347"), Hex("#FF8A3D"), 16, new Vector2(0.5f, 0.45f)),
                        dress = (robot, props) =>
                        {
                            Bold(robot);
                            Arms(robot, 140f, 140f);
                            Coin(props, new Vector3(1.26f, 0.62f, 0.74f), 0.36f);
                            Coin(props, new Vector3(1.3f, 0.32f, 0.7f), 0.26f);
                            Block(props, 0, new Vector3(0.76f, 0.8f, 1.24f), new Vector3(15f, 30f, -18f), 0.22f);
                            SpeedLines(props, new Vector3(0.76f, 0.64f, 1.24f), vertical: true);
                        },
                    };
                case "shield": // safe in a glowing shield while a block shatters on top of it
                    return new Design
                    {
                        theme = 3, robotWorld = 13, pitch = 12f, halfHeight = 0.53f, focusOffset = new Vector3(0f, 0.5f, 0f),
                        noFloor = true, bloom = 2.2f,
                        background = BackgroundArt.Radial(Size, Hex("#46E0FF"), Hex("#0E0A4A"), new Vector2(0.5f, 0.52f), 60, Color.white),
                        dress = (robot, props) =>
                        {
                            Bold(robot);
                            Arms(robot, 70f, 70f);
                            GlassShield(props, new Vector3(1f, GridView.SurfaceY + 0.4f, 1f), 0.5f, 12f);
                            // A block shatters against the top of the shield.
                            Block(props, 0, new Vector3(1.2f, 0.92f, 0.8f), new Vector3(20f, 35f, -25f), 0.2f);
                            Block(props, 0, new Vector3(1.0f, 0.99f, 1.0f), new Vector3(-30f, 10f, 40f), 0.07f);
                            Block(props, 0, new Vector3(1.37f, 0.98f, 0.63f), new Vector3(45f, -20f, 10f), 0.06f);
                            Star(props, new Vector3(0.62f, 0.82f, 1.38f), 0.07f);
                            Star(props, new Vector3(1.42f, 0.3f, 0.58f), 0.06f);
                        },
                    };
                case "crown": // the champion: crown, golden eyes, a victory jump in a shower of coins
                    return new Design
                    {
                        theme = 0, robotWorld = 13, pitch = 12f, halfHeight = 0.54f, focusOffset = new Vector3(0f, 0.6f, 0f),
                        background = BackgroundArt.Sunburst(Size, Hex("#E6FFF6"), Hex("#0F8F7A"), Hex("#4CE0C0"), Hex("#25B89E"), 18, new Vector2(0.5f, 0.5f)),
                        dress = (robot, props) =>
                        {
                            Bold(robot);
                            var outfit = Data.Cosmetics.Outfit(Data.Cosmetics.Find("hat.crown"));
                            outfit[Data.Slot.Color] = new Data.Cosmetic { id = "icon", slot = Data.Slot.Color, price = 1, color = Hex("#2FD3B7") };
                            outfit[Data.Slot.Eyes] = Data.Cosmetics.Find("eyes.gold");
                            robot.ApplyOutfit(outfit);
                            Jump(robot, 0.12f);
                            Arms(robot, 150f, 30f);
                            Coin(props, new Vector3(1.4f, 0.7f, 0.6f), 0.42f);
                            Coin(props, new Vector3(0.6f, 0.95f, 1.4f), 0.34f);
                            Coin(props, new Vector3(1.35f, 1.05f, 0.75f), 0.26f);
                            Star(props, new Vector3(0.55f, 0.55f, 1.45f), 0.1f);
                        },
                    };
                case "warden": // the robot in front, WARDEN's huge red eye glaring behind it
                    return new Design
                    {
                        theme = 3, robotWorld = 13, pitch = 10f, halfHeight = 0.55f, focusOffset = new Vector3(0f, 0.55f, 0f),
                        background = BackgroundArt.Radial(Size, Hex("#7A1838"), Hex("#12081E"), new Vector2(0.5f, 0.62f), 30, Hex("#FF8FA0")),
                        dress = (robot, props) =>
                        {
                            Bold(robot);
                            Arms(robot, 40f, 40f);
                            var view = Quaternion.Euler(10f, 45f, 0f) * Vector3.forward;
                            var right = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;
                            var boss = WardenBoss.Create(new Vector3(1f, 0.98f, 1f) + view * 3f + right * 0.16f, 3, null);
                            boss.transform.SetParent(props, true);
                            boss.transform.localScale = Vector3.one * 0.5f;
                            boss.transform.rotation = Quaternion.LookRotation(view, Vector3.up);
                        },
                    };
                case "dodge": // big and bold: a last-second dodge as a block slams down, on bright yellow
                    return new Design
                    {
                        theme = 0, robotWorld = 13, pitch = 16f, halfHeight = 0.5f, focusOffset = new Vector3(0.12f, 0.5f, -0.12f),
                        background = BackgroundArt.Sunburst(Size, Hex("#FFFBE0"), Hex("#FFB800"), Hex("#FFE15C"), Hex("#FFC929"), 14, new Vector2(0.4f, 0.5f)),
                        burstAt = new Vector3(0.66f, 0.15f, 1.5f),
                        dress = (robot, props) =>
                        {
                            Bold(robot);
                            robot.transform.position += new Vector3(0.2f, 0.18f, -0.2f);
                            robot.transform.rotation = Quaternion.AngleAxis(-14f, new Vector3(1f, 0f, 1f).normalized);
                            Arms(robot, 120f, 20f);
                            SpeedLines(props, new Vector3(0.9f, 0.6f, 1.1f), vertical: false);
                            Block(props, 0, new Vector3(0.66f, 0.48f, 1.5f), Vector3.zero, 1f);
                        },
                    };
                // ----- "Escape Cell": the robot surfs out on one of the game's own glowing cells. -----
                case "surfshield": // the chosen glass shield, now riding a cell through the night sky
                    return new Design
                    {
                        theme = 3, robotWorld = 13, pitch = 12f, halfHeight = 0.7f, focusOffset = new Vector3(0.09f, 0.4f, -0.09f),
                        noFloor = true, bloom = 2.2f,
                        background = BackgroundArt.Radial(Size, Hex("#46E0FF"), Hex("#0E0A4A"), new Vector2(0.5f, 0.5f), 60, Color.white),
                        dress = (robot, props) =>
                        {
                            Bold(robot);
                            Arms(robot, 95f, 125f);
                            var board = Surf(robot, props, new Vector3(1f, 0.16f, 1f), 20f, 3, new Color(1.45f, 0.3f, 0.02f));
                            GlassShield(props, board.position + Vector3.up * 0.33f, 0.56f, 12f);
                            Block(props, 0, new Vector3(1.28f, 1.05f, 0.72f), new Vector3(20f, 35f, -25f), 0.17f);
                            Block(props, 0, new Vector3(1.46f, 0.86f, 0.54f), new Vector3(45f, -20f, 10f), 0.06f);
                            Star(props, new Vector3(0.55f, 0.9f, 1.45f), 0.07f);
                        },
                    };
                case "surf": // riding a cell on a sunset sunburst, coins flying off the trail
                    return new Design
                    {
                        theme = 0, robotWorld = 13, pitch = 12f, halfHeight = 0.66f, focusOffset = new Vector3(0.09f, 0.36f, -0.09f),
                        noFloor = true, bloom = 1.8f,
                        background = BackgroundArt.Sunburst(Size, Hex("#FFF1C9"), Hex("#FF4F6E"), Hex("#FFB347"), Hex("#FF7A4D"), 16, new Vector2(0.55f, 0.42f)),
                        dress = (robot, props) =>
                        {
                            Bold(robot);
                            Arms(robot, 90f, 135f);
                            Surf(robot, props, new Vector3(1f, 0.14f, 1f), 22f, 4);
                            Coin(props, new Vector3(0.62f, 0.32f, 1.38f), 0.3f);
                            Coin(props, new Vector3(0.5f, 0.12f, 1.5f), 0.22f);
                            Coin(props, new Vector3(1.42f, 0.9f, 0.58f), 0.28f);
                        },
                    };
                case "surfescape": // surfing away as blocks rain down behind
                    return new Design
                    {
                        theme = 3, robotWorld = 13, pitch = 12f, halfHeight = 0.68f, focusOffset = new Vector3(0.06f, 0.38f, -0.06f),
                        noFloor = true, bloom = 1.8f,
                        background = BackgroundArt.Radial(Size, Hex("#B06BFF"), Hex("#1A0B45"), new Vector2(0.55f, 0.5f), 50, Hex("#FFD6FF")),
                        dress = (robot, props) =>
                        {
                            Bold(robot);
                            Arms(robot, 100f, 130f);
                            Surf(robot, props, new Vector3(1.05f, 0.15f, 0.95f), 22f, 4);
                            Block(props, 0, new Vector3(0.6f, 0.95f, 1.4f), new Vector3(15f, 30f, -18f), 0.22f);
                            Block(props, 0, new Vector3(0.48f, 0.42f, 1.52f), new Vector3(-10f, 20f, 25f), 0.16f);
                            Block(props, 0, new Vector3(0.78f, 1.18f, 1.22f), new Vector3(30f, -15f, 10f), 0.12f);
                            SpeedLines(props, new Vector3(0.6f, 0.75f, 1.4f), vertical: true);
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

        /// <summary>Bright teal paint and no world gear: a clean, bold silhouette that pops at icon size.</summary>
        private static void Bold(Robot robot)
        {
            var outfit = Data.Cosmetics.Outfit();
            outfit[Data.Slot.Color] = new Data.Cosmetic { id = "icon", slot = Data.Slot.Color, price = 1, color = Hex("#2FD3B7") };
            outfit[Data.Slot.Eyes] = new Data.Cosmetic { id = "icon", slot = Data.Slot.Eyes, price = 1, color = new Color(0.5f, 2.2f, 2.4f) };
            robot.ApplyOutfit(outfit);
            var gear = robot.transform.Find("Lift/Visual/Accessories");
            if (gear != null) gear.gameObject.SetActive(false);
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
            // Mirror the angle for the left arm, so positive always means raised outward.
            degrees *= side;
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
            var block = HazardVisuals.Block().transform;
            block.SetParent(root, false);
            block.position = position;
            block.rotation = Quaternion.Euler(euler);
            block.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// Makes the shield bubble read as a 3D glass sphere: a bright rim that is strongest toward the light
        /// (top left), a softer inner rim, a big glossy highlight with a small one beside it, and a glow at the base.
        /// Everything is laid out in the camera's view plane, so it frames the bubble exactly from the icon's angle.
        /// </summary>
        private static void GlassShield(Transform root, Vector3 center, float radius, float pitch)
        {
            var view = Quaternion.Euler(pitch, 45f, 0f);
            var right = view * Vector3.right;
            var up = view * Vector3.up;
            var toward = -(view * Vector3.forward);
            var front = center + toward * radius;

            // Glow inside the sphere, behind the robot (so the robot itself stays crisp): brighter toward the middle.
            foreach (var (scale, alpha) in new[] { (2f, 0.22f), (1.6f, 0.2f), (1.15f, 0.18f) })
            {
                var glow = MaterialFactory.CreateTransparent(new Color(0.45f, 0.95f, 1f, alpha), new Color(0.2f, 0.7f, 1f) * alpha * 3f);
                var disc = Shapes.Primitive(PrimitiveType.Sphere, "Glow", root, Vector3.zero, new Vector3(radius * scale, radius * scale, 0.002f), glow).transform;
                disc.position = center - toward * 0.35f;
                disc.rotation = view;
            }

            // The rim: continuous, thick and bright toward the light (top left), thin and dim on the far side.
            const int pieces = 140;
            float step = Mathf.PI * 2f * radius / pieces;
            for (int i = 0; i < pieces; i++)
            {
                float a = i * Mathf.PI * 2f / pieces;
                float lit = 0.5f + 0.5f * Mathf.Cos(a - Mathf.PI * 0.75f);
                var dir = right * Mathf.Cos(a) + up * Mathf.Sin(a);
                var spin = view * Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
                var rim = MaterialFactory.Create(new Color(0.45f, 0.95f, 1f), new Color(0.35f, 1.3f, 1.9f) * Mathf.Lerp(1f, 2.6f, lit));
                var piece = Shapes.Rounded("Rim", root, Vector3.zero, new Vector3(step * 1.6f, Mathf.Lerp(0.016f, 0.05f, lit), 0.02f), 0.008f, rim);
                piece.transform.position = center + dir * radius + toward * 0.05f;
                piece.transform.rotation = spin;

                // A white crescent just inside the rim on the lit side: light wrapping round a curved surface.
                if (lit > 0.55f)
                {
                    float k = (lit - 0.55f) / 0.45f;
                    var shine = MaterialFactory.Create(Color.white, new Color(1.6f, 2f, 2.1f) * k);
                    var crescent = Shapes.Rounded("Crescent", root, Vector3.zero, new Vector3(step * 1.6f, 0.022f * k + 0.004f, 0.01f), 0.005f, shine);
                    crescent.transform.position = center + dir * (radius * 0.9f) + toward * 0.06f;
                    crescent.transform.rotation = spin;
                }
            }

            // Glossy highlights on the glass, kept clear of the robot's face.
            var gloss = MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.85f), new Color(1.8f, 2f, 2.1f));
            var at = front + (right * Mathf.Cos(Mathf.PI * 0.75f) + up * Mathf.Sin(Mathf.PI * 0.75f)) * (radius * 0.72f) + toward * 0.02f;
            var big = Shapes.Primitive(PrimitiveType.Sphere, "Gloss", root, Vector3.zero, new Vector3(0.15f, 0.055f, 0.01f), gloss).transform;
            big.position = at;
            big.rotation = view * Quaternion.Euler(0f, 0f, 45f);
            var dot = Shapes.Primitive(PrimitiveType.Sphere, "GlossDot", root, Vector3.zero, new Vector3(0.04f, 0.04f, 0.01f), gloss).transform;
            dot.position = at + right * 0.1f + up * 0.06f;
            dot.rotation = view;
            var faint = MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.4f), new Color(0.8f, 1.1f, 1.2f));
            var low = Shapes.Primitive(PrimitiveType.Sphere, "Reflection", root, Vector3.zero, new Vector3(0.12f, 0.03f, 0.01f), faint).transform;
            low.position = front + (right * Mathf.Cos(-Mathf.PI * 0.25f) + up * Mathf.Sin(-Mathf.PI * 0.25f)) * (radius * 0.76f) + toward * 0.02f;
            low.rotation = view * Quaternion.Euler(0f, 0f, 45f);

            // A soft cyan glow pooled under the bubble.
            var pool = MaterialFactory.CreateTransparent(new Color(0.5f, 1f, 1f, 0.4f), new Color(0.4f, 1.4f, 1.8f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Pool", root, center + Vector3.down * 0.38f, new Vector3(1.05f, 0.004f, 1.05f), pool);
            var core = MaterialFactory.CreateTransparent(new Color(0.8f, 1f, 1f, 0.5f), new Color(0.8f, 2f, 2.4f));
            Shapes.Primitive(PrimitiveType.Cylinder, "PoolCore", root, center + Vector3.down * 0.379f, new Vector3(0.55f, 0.004f, 0.55f), core);
        }

        /// <summary>
        /// Puts the robot on a surfboard made of a game cell (the glowing tile from the platform), tilted like a
        /// board carving a wave, sliding toward the top right of the icon with fading copies of the cell as a trail.
        /// Returns the board.
        /// </summary>
        private const float BoardScale = 0.66f;

        private static Transform Surf(Robot robot, Transform props, Vector3 at, float roll, int ghosts, Color? glow = null)
        {
            var view = Quaternion.Euler(12f, 45f, 0f);
            var right = view * Vector3.right;
            var up = view * Vector3.up;
            var forward = view * Vector3.forward;
            // The board banks around the view axis and lifts its nose toward the direction of travel.
            var tilt = Quaternion.AngleAxis(-roll, forward) * Quaternion.AngleAxis(-14f, right);

            var board = new GameObject("Board").transform;
            board.SetParent(props, false);
            board.position = at;
            board.rotation = tilt;
            Cell(board, 1f, BoardScale, glow);

            robot.transform.SetParent(board, true);
            robot.transform.position = at;
            robot.transform.rotation = tilt;

            // Ghost cells behind it: the trail of a board in motion.
            var travel = (right * 0.85f + up * 0.35f).normalized;
            for (int i = 1; i <= ghosts; i++)
            {
                var ghost = new GameObject("Ghost").transform;
                ghost.SetParent(props, false);
                ghost.position = at - travel * (0.17f * i) - up * (0.015f * i);
                ghost.rotation = tilt;
                Cell(ghost, 0.42f / i, BoardScale * (1f - i * 0.07f), glow);
            }

            // Spray and streaks off the trailing edge.
            var streak = MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.6f), new Color(1f, 1.4f, 1.6f));
            for (int i = 0; i < 3; i++)
            {
                var line = Shapes.Rounded("Streak", props, Vector3.zero, new Vector3(0.28f - i * 0.06f, 0.018f, 0.01f), 0.01f, streak).transform;
                line.position = at - travel * (0.42f + i * 0.06f) + up * (0.2f - i * 0.1f) - forward * 0.3f;
                line.rotation = view * Quaternion.Euler(0f, 0f, Mathf.Atan2(0.35f, 0.85f) * Mathf.Rad2Deg);
            }
            return board;
        }

        /// <summary>A platform cell like the game's: lavender top in a glowing frame on a slab.</summary>
        private static void Cell(Transform root, float alpha, float scale, Color? glow = null)
        {
            bool solid = alpha >= 0.99f;
            var light = glow ?? Palette.TileGlow;
            var tint = glow.HasValue ? new Color(1f, 0.5f, 0.15f, alpha) : new Color(0.75f, 0.95f, 1f, alpha);
            var frame = solid ? MaterialFactory.Create(glow.HasValue ? new Color(1f, 0.4f, 0.08f) : Palette.TileTop, light * 1.4f)
                : MaterialFactory.CreateTransparent(tint, light * alpha);
            var top = solid ? MaterialFactory.Create(Palette.TileTop, Palette.TileSelfLight)
                : MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, alpha * 0.6f), Palette.TileSelfLight * alpha);
            float s = scale;
            Shapes.Rounded("Frame", root, new Vector3(0f, -0.03f, 0f), new Vector3(0.93f * s, 0.1f, 0.93f * s), 0.045f, frame);
            Shapes.Rounded("Top", root, Vector3.zero, new Vector3(0.78f * s, 0.1f, 0.78f * s), 0.045f, top);
            if (solid)
            {
                Shapes.Rounded("Slab", root, new Vector3(0f, -0.11f, 0f), new Vector3(1.0f * s, 0.1f, 1.0f * s), 0.04f, MaterialFactory.Create(Palette.Slab, Color.black));
                Shapes.Rounded("Glow", root, new Vector3(0f, -0.17f, 0f), new Vector3(1.04f * s, 0.035f, 1.04f * s), 0.015f, MaterialFactory.Create(Palette.Slab, (glow ?? Palette.SlabEdgeGlow) * 1.5f));
            }
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
            bloom.intensity.Override(design.bloom);
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
            if (design.burst || design.burstAt.HasValue)
            {
                fx.Burst(design.burstAt ?? new Vector3(0.66f, 0.15f, 1.5f), Palette.Block, Palette.BlockGlow, 40, 3.5f);
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
