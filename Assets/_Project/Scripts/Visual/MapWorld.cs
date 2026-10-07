using System;
using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    /// <summary>
    /// The level map as a real 3D place: a path of floating stepping stones winding up into the sky, one stone per
    /// level, with a themed island at the start of every world (its crystals, lamps and towers in the world's colours),
    /// glowing dots between the stones, stars and drifting clouds far behind, and the robot standing on the next level.
    /// It has its own camera that slides up and down the path; the map screen draws the numbers, stars and buttons on
    /// top and feeds it the drags.
    /// </summary>
    public class MapWorld : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(-6000f, 0f, 0f);
        private const float Step = 1.7f;        // height per level
        private const float WorldGap = 4.4f;    // extra height for each world's island
        private const float ViewDistance = 12.5f;

        public Camera Cam { get; private set; }

        private Transform root, path, robotHolder;
        private Func<Transform, GameObject> robotLook;
        private int levelCount;
        private readonly List<Transform> stones = new List<Transform>();
        private readonly List<Material> stoneTops = new List<Material>();
        private float height, velocity, minHeight, maxHeight;
        private int current = -1, hopFrom = -1;
        private float hopT = 1f;
        private float time;
        private bool dragging;

        public static MapWorld Create(int levelCount, Func<Transform, GameObject> robotLook)
        {
            var go = new GameObject("MapWorld");
            go.transform.position = Origin;
            var m = go.AddComponent<MapWorld>();
            m.levelCount = levelCount;
            m.robotLook = robotLook;
            m.BuildCamera();
            m.BuildBackdrop();
            go.SetActive(false);
            return m;
        }

        // ---------- Layout ----------

        /// <summary>Where level <paramref name="i"/>'s stone stands (its top centre).</summary>
        public static Vector3 NodePosition(int i)
        {
            int w = i / LevelCatalog.LevelsPerWorld;
            float y = i * Step + w * WorldGap + WorldGap;
            float x = Mathf.Sin(i * 0.8f) * 2.1f;
            float z = Mathf.Cos(i * 0.55f) * 1.1f;
            return Origin + new Vector3(x, y, z);
        }

        private static Vector3 IslandPosition(int world)
        {
            var first = NodePosition(world * LevelCatalog.LevelsPerWorld);
            // Set back behind the path (the camera looks from -z), so it never hides the stones in front of it.
            return new Vector3(Origin.x, first.y - WorldGap * 0.6f, Origin.z + 3.6f);
        }

        // ---------- Building ----------

        private void BuildCamera()
        {
            var camGo = new GameObject("MapCamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.06f, 0.05f, 0.16f);
            Cam.fieldOfView = 50f;
            Cam.nearClipPlane = 0.5f;
            Cam.farClipPlane = 80f;
            Cam.depth = 5f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = Application.isMobilePlatform ? AntialiasingMode.None : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        /// <summary>Far stars and soft clouds behind the path, all the way up.</summary>
        private void BuildBackdrop()
        {
            var back = new GameObject("Backdrop").transform;
            back.SetParent(transform, false);
            int worlds = Mathf.CeilToInt(levelCount / (float)LevelCatalog.LevelsPerWorld);
            float top = NodePosition(levelCount - 1).y - Origin.y + 20f;
            var rng = new System.Random(7);
            var star = MaterialFactory.Create(Color.white, new Color(1.6f, 1.6f, 2f));
            for (int i = 0; i < Mathf.Min(900, (int)(top * 2.5f)); i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 40f - 20f, (float)rng.NextDouble() * top - 10f, 14f + (float)rng.NextDouble() * 18f);
                float s = 0.05f + (float)rng.NextDouble() * 0.12f;
                Shapes.Primitive(PrimitiveType.Cube, "Star", back, p, Vector3.one * s, star);
            }
            for (int w = 0; w < worlds; w++)
            {
                var theme = WorldTheme.ForWorld(w);
                var tint = Color.Lerp(theme.accent, Color.white, 0.6f);
                var cloud = MaterialFactory.CreateTransparent(new Color(tint.r, tint.g, tint.b, 0.035f), tint * 0.06f);
                float baseY = IslandPosition(w).y - Origin.y;
                for (int c = 0; c < 5; c++)
                {
                    var p = new Vector3((float)rng.NextDouble() * 30f - 15f, baseY + (float)rng.NextDouble() * 16f, 18f + (float)rng.NextDouble() * 6f);
                    var size = new Vector3(9f + (float)rng.NextDouble() * 8f, 2.5f + (float)rng.NextDouble() * 2f, 3f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Cloud", back, p, size, cloud);
                }
            }
        }

        /// <summary>(Re)builds the stones, islands and dots for the current progress.</summary>
        public void Build(int unlocked, bool openAll)
        {
            if (path != null) Destroy(path.gameObject);
            stones.Clear();
            stoneTops.Clear();
            path = new GameObject("Path").transform;
            path.SetParent(transform, false);
            current = Mathf.Clamp(unlocked, 0, levelCount - 1);

            int worlds = Mathf.CeilToInt(levelCount / (float)LevelCatalog.LevelsPerWorld);
            for (int w = 0; w < worlds; w++) BuildIsland(w, w * LevelCatalog.LevelsPerWorld > unlocked && !openAll);

            var dotDone = MaterialFactory.Create(new Color(1f, 0.95f, 0.8f), new Color(1.6f, 1.4f, 0.9f));
            var dotLocked = MaterialFactory.Create(new Color(0.35f, 0.33f, 0.5f), Color.black);
            for (int i = 0; i < levelCount; i++)
            {
                stones.Add(BuildStone(i, unlocked, openAll));
                if (i == levelCount - 1) continue;
                var a = NodePosition(i);
                var b = NodePosition(i + 1);
                bool done = i + 1 <= unlocked;
                int dots = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.45f));
                for (int d = 1; d < dots; d++)
                {
                    var p = Vector3.Lerp(a, b, d / (float)dots) - Vector3.up * 0.05f - Origin;
                    Shapes.Primitive(PrimitiveType.Sphere, "Dot", path, p, Vector3.one * 0.13f, done ? dotDone : dotLocked);
                }
            }

            if (robotHolder == null)
            {
                robotHolder = new GameObject("Robot").transform;
                robotHolder.SetParent(transform, false);
            }
            for (int i = robotHolder.childCount - 1; i >= 0; i--) Destroy(robotHolder.GetChild(i).gameObject);
            robotLook?.Invoke(robotHolder);
            robotHolder.localScale = Vector3.one * 1.6f;

            minHeight = NodePosition(0).y - Origin.y - 1f;
            maxHeight = NodePosition(levelCount - 1).y - Origin.y;
        }

        private Transform BuildStone(int i, int unlocked, bool openAll)
        {
            var theme = WorldTheme.ForWorld(i / LevelCatalog.LevelsPerWorld);
            bool completed = i < unlocked;
            bool isCurrent = i == unlocked;
            bool locked = i > unlocked && !openAll;
            var stone = new GameObject("Stone " + (i + 1)).transform;
            stone.SetParent(path, false);
            stone.position = NodePosition(i);

            var rock = MaterialFactory.Create(locked ? new Color(0.22f, 0.2f, 0.32f) : Color.Lerp(theme.pillar, new Color(0.25f, 0.22f, 0.35f), 0.35f), Color.black);
            Color topColor = locked ? new Color(0.3f, 0.28f, 0.42f) : isCurrent ? Color.white : theme.accent;
            var top = MaterialFactory.Create(topColor, locked ? Color.black : topColor * (isCurrent ? 0.7f : 0.35f));
            var rim = MaterialFactory.Create(completed ? Palette.UiGold : locked ? new Color(0.4f, 0.38f, 0.55f) : Color.white,
                completed ? Palette.UiGold * 1.4f : locked ? Color.black : new Color(1.4f, 1.4f, 1.6f));
            // A rounded slab on top, a rock tapering away below (two stacked pieces read as a floating stone).
            Shapes.Primitive(PrimitiveType.Cylinder, "Top", stone, new Vector3(0f, -0.12f, 0f), new Vector3(1.25f, 0.12f, 1.25f), top);
            Shapes.Primitive(PrimitiveType.Cylinder, "Rim", stone, new Vector3(0f, -0.2f, 0f), new Vector3(1.38f, 0.06f, 1.38f), rim);
            Shapes.Primitive(PrimitiveType.Cylinder, "Rock", stone, new Vector3(0f, -0.48f, 0f), new Vector3(1.15f, 0.24f, 1.15f), rock);
            Shapes.Primitive(PrimitiveType.Sphere, "Root", stone, new Vector3(0f, -0.78f, 0f), new Vector3(0.85f, 0.7f, 0.85f), rock);
            stoneTops.Add(top);
            return stone;
        }

        /// <summary>The world's island: a big rock with its theme's landmarks, below the world's first stone.</summary>
        private void BuildIsland(int world, bool locked)
        {
            var theme = WorldTheme.ForWorld(world);
            var island = new GameObject("Island " + (world + 1)).transform;
            island.SetParent(path, false);
            island.position = IslandPosition(world);
            var rng = new System.Random(world * 31 + 5);
            float R() => (float)rng.NextDouble();

            var grass = MaterialFactory.Create(locked ? new Color(0.25f, 0.23f, 0.36f) : Color.Lerp(theme.tileTop, theme.accent, 0.35f), Color.black);
            var rock = MaterialFactory.Create(locked ? new Color(0.18f, 0.16f, 0.28f) : Color.Lerp(theme.pillar, Color.black, 0.25f), Color.black);
            var glow = MaterialFactory.Create(theme.accent, locked ? theme.accent * 0.2f : theme.accent * 1.6f);
            var light = MaterialFactory.Create(theme.slab, Color.black);

            Shapes.Rounded("Ground", island, new Vector3(0f, 0f, 0f), new Vector3(7.5f, 0.6f, 4.2f), 0.3f, grass);
            Shapes.Primitive(PrimitiveType.Sphere, "Underside", island, new Vector3(0f, -0.9f, 0f), new Vector3(7f, 2f, 3.9f), rock);
            Shapes.Primitive(PrimitiveType.Sphere, "Drip", island, new Vector3(-1.2f, -1.8f, 0.3f), new Vector3(2f, 1.6f, 1.6f), rock);
            Shapes.Primitive(PrimitiveType.Sphere, "Drip", island, new Vector3(1.6f, -1.6f, -0.2f), new Vector3(1.4f, 1.3f, 1.2f), rock);

            // Landmarks, a different mix on every world: crystals, lamp posts, a little tower.
            for (int k = 0; k < 4; k++)
            {
                float x = -3f + k * 2f + R() * 0.6f;
                float z = 0.6f + R() * 1.2f;
                switch ((world + k) % 3)
                {
                    case 0:
                        float h = 0.8f + R() * 0.9f;
                        Shapes.Rounded("Crystal", island, new Vector3(x, 0.3f + h * 0.5f, z), new Vector3(0.35f, h, 0.35f), 0.08f, glow).transform.localRotation = Quaternion.Euler(0f, 45f, R() * 20f - 10f);
                        break;
                    case 1:
                        Shapes.Rounded("Post", island, new Vector3(x, 0.9f, z), new Vector3(0.1f, 1.2f, 0.1f), 0.04f, rock);
                        Shapes.Primitive(PrimitiveType.Sphere, "Lamp", island, new Vector3(x, 1.6f, z), Vector3.one * 0.32f, glow);
                        break;
                    default:
                        Shapes.Rounded("Tower", island, new Vector3(x, 1f, z), new Vector3(0.7f, 1.4f, 0.7f), 0.12f, light);
                        Shapes.Rounded("Roof", island, new Vector3(x, 1.85f, z), new Vector3(0.85f, 0.3f, 0.85f), 0.1f, glow);
                        Shapes.Rounded("Window", island, new Vector3(x, 1.1f, z - 0.36f), new Vector3(0.22f, 0.3f, 0.04f), 0.04f, glow);
                        break;
                }
            }
        }

        // ---------- Showing and moving ----------

        /// <summary>Starts showing the map with the camera on <paramref name="focus"/>; a hop plays from <paramref name="from"/>.</summary>
        public void Open(int focus, int from)
        {
            gameObject.SetActive(true);
            height = Mathf.Clamp(NodePosition(Mathf.Clamp(focus, 0, levelCount - 1)).y - Origin.y + 1.5f, minHeight, maxHeight);
            velocity = 0f;
            hopFrom = from >= 0 && from < current ? from : -1;
            hopT = hopFrom >= 0 ? -0.4f : 1f;
            PlaceRobot();
            PlaceCamera();
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>A drag on the map by <paramref name="pixels"/> (screen pixels, up = positive).</summary>
        public void Drag(float pixels)
        {
            float unitsPerPixel = 2f * ViewDistance * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            float dy = -pixels * unitsPerPixel;
            height = Mathf.Clamp(height + dy, minHeight - 1f, maxHeight + 1f);
            velocity = dy / Mathf.Max(0.001f, Time.unscaledDeltaTime);
            dragging = true;
        }

        public void EndDrag() => dragging = false;

        /// <summary>Where level <paramref name="i"/>'s stone top is on screen (z &lt; 0 when behind the camera).</summary>
        public Vector3 ScreenPoint(int i) => Cam.WorldToScreenPoint(NodePosition(i));

        /// <summary>Screen point of a world's island sign.</summary>
        public Vector3 IslandScreenPoint(int world) => Cam.WorldToScreenPoint(IslandPosition(world) + new Vector3(0f, 0.3f, -2.4f));

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            time += dt;
            if (!dragging)
            {
                // Glide on after a flick, then settle back inside the path.
                height += velocity * dt;
                velocity = Mathf.MoveTowards(velocity, 0f, Mathf.Abs(velocity) * 4f * dt + 0.5f * dt);
                if (height < minHeight) height = Mathf.Lerp(height, minHeight, dt * 8f);
                if (height > maxHeight) height = Mathf.Lerp(height, maxHeight, dt * 8f);
            }
            if (hopT < 1f) hopT += dt / 0.7f;
            PlaceRobot();
            PlaceCamera();
            // The next level's stone glows in and out.
            if (current >= 0 && current < stoneTops.Count)
            {
                float k = 0.55f + 0.45f * Mathf.Sin(time * 4f);
                MaterialFactory.SetColors(stoneTops[current], Color.white, new Color(1f, 1f, 1.2f) * k);
                stones[current].localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(time * 4f));
            }
        }

        private void PlaceRobot()
        {
            if (robotHolder == null || current < 0) return;
            var to = NodePosition(current);
            Vector3 p;
            if (hopFrom >= 0 && hopT < 1f)
            {
                float t = Mathf.Clamp01(hopT);
                t = t * t * (3f - 2f * t);
                var from = NodePosition(hopFrom);
                p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.6f;
            }
            else p = to + Vector3.up * Mathf.Abs(Mathf.Sin(time * 3f)) * 0.12f;
            robotHolder.position = p;
            robotHolder.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -1f));
        }

        private void PlaceCamera()
        {
            // Narrow phones see as much of the path's width as a 9:16 screen does.
            Cam.fieldOfView = CameraFit.Fov(50f, Cam.aspect);
            var focus = Origin + new Vector3(0f, height, 0f);
            Cam.transform.position = focus + new Vector3(0f, 3.2f, -ViewDistance);
            Cam.transform.LookAt(focus + Vector3.up * 0.4f);
            // The sky shifts with the world the camera is in.
            int world = Mathf.Clamp(Mathf.FloorToInt((height - WorldGap) / (Step * LevelCatalog.LevelsPerWorld + WorldGap)), 0, Mathf.Max(0, (levelCount - 1) / LevelCatalog.LevelsPerWorld));
            var theme = WorldTheme.ForWorld(world);
            Cam.backgroundColor = Color.Lerp(Cam.backgroundColor, Color.Lerp(theme.bgBottom, theme.bgTop, 0.5f) * 0.55f, Time.unscaledDeltaTime * 2f);
        }
    }
}
