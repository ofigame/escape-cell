using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    /// <summary>
    /// The main menu's showroom: the robot (in its current outfit) on a star dial (a navy disc in gold rings with
    /// constellations drawn on it), deep space and golden orbits behind, a brass globe to one side, sparks drifting up. It has its own
    /// camera and only draws while the menu is open, so the menu looks like a polished lobby rather than a paused level.
    /// </summary>
    public class LobbyStage : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(6000f, 0f, -6000f);

        public Camera Cam { get; private set; }

        private Transform robotHolder, ringA, ringB, sparks, globeSpin;
        private Func<Transform, GameObject> robotLook;
        private readonly List<Transform> sparkList = new List<Transform>();
        private float time;

        public static LobbyStage Create(Func<Transform, GameObject> robotLook)
        {
            var go = new GameObject("LobbyStage");
            go.transform.position = Origin;
            var s = go.AddComponent<LobbyStage>();
            s.robotLook = robotLook;
            s.Build();
            go.SetActive(false);
            return s;
        }

        private void Build()
        {
            var camGo = new GameObject("LobbyCamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.fieldOfView = 34f;
            Cam.nearClipPlane = 0.3f;
            Cam.farClipPlane = 60f;
            Cam.depth = 4f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = Application.isMobilePlatform ? AntialiasingMode.None : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.localPosition = new Vector3(0f, 6.2f, -11.8f);
            camGo.transform.LookAt(Origin + new Vector3(0f, 0.85f, 0f)); // aimed above foi: it stands lower, under the rank card, above the level card

            // Deep space behind everything: the galaxy picture far back, golden orbits sweeping round with a few planets.
            var sky = Shapes.Primitive(PrimitiveType.Quad, "Galaxy", transform, new Vector3(0f, 6f, 34f), new Vector3(46f, 100f, 1f), MaterialFactory.CreateUnlit(SquashBot.UI.UiSprites.Galaxy.texture)).transform;
            sky.localRotation = Quaternion.identity;
            var gold = MaterialFactory.Create(new Color(1f, 0.78f, 0.36f), new Color(0.55f, 0.38f, 0.12f));
            var goldDim = MaterialFactory.CreateTransparent(new Color(1f, 0.8f, 0.4f, 0.55f), new Color(0.8f, 0.55f, 0.2f));
            ringA = Orbit("OrbitA", 6.2f, 0.035f, goldDim, new Vector3(0f, 6.4f, 9f), 74f, 12f);
            ringB = Orbit("OrbitB", 8.4f, 0.03f, goldDim, new Vector3(0f, 6.8f, 11f), 78f, -8f);
            foreach (var (pos, size, colour) in new[] { (new Vector3(-5.6f, 7.8f, 9f), 0.9f, new Color(0.85f, 0.55f, 0.35f)), (new Vector3(6.4f, 6.2f, 10f), 0.6f, new Color(0.55f, 0.75f, 1f)), (new Vector3(3.2f, 8.6f, 12f), 0.45f, new Color(0.9f, 0.85f, 0.7f)) })
                Shapes.Primitive(PrimitiveType.Sphere, "Planet", transform, pos, Vector3.one * size, MaterialFactory.Create(colour, colour * 0.35f));

            // The star dial foi stands on: a navy disc in a double gold ring, gold spokes dividing it into eight fields, a
            // softly glowing centre, and a faint constellation drawn in each field.
            var dial = new GameObject("Dial").transform;
            dial.SetParent(transform, false);
            dial.localScale = new Vector3(0.62f, 1f, 0.62f); // under foi, whole in the view
            var navy = MaterialFactory.Create(new Color(0.07f, 0.09f, 0.22f), new Color(0.02f, 0.03f, 0.08f));
            var deep = MaterialFactory.Create(new Color(0.05f, 0.06f, 0.16f), Color.black);
            var lines = MaterialFactory.Create(new Color(0.6f, 0.88f, 1f), new Color(1.1f, 1.9f, 3f));
            var starGold = MaterialFactory.Create(new Color(1f, 0.85f, 0.45f), new Color(2.4f, 1.8f, 0.7f));
            var floor = MaterialFactory.Create(new Color(0.06f, 0.06f, 0.16f), new Color(0.02f, 0.02f, 0.06f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Floor", transform, new Vector3(0f, -0.42f, 0f), new Vector3(30f, 0.02f, 30f), floor);
            Shapes.Primitive(PrimitiveType.Cylinder, "Base", dial, new Vector3(0f, -0.2f, 0f), new Vector3(7.4f, 0.2f, 7.4f), deep);
            Shapes.Primitive(PrimitiveType.Cylinder, "RimOuter", dial, new Vector3(0f, 0.02f, 0f), new Vector3(7.2f, 0.05f, 7.2f), gold);
            Shapes.Primitive(PrimitiveType.Cylinder, "Dial", dial, new Vector3(0f, 0.05f, 0f), new Vector3(6.9f, 0.05f, 6.9f), navy);
            Shapes.Primitive(PrimitiveType.Cylinder, "RimInner", dial, new Vector3(0f, 0.075f, 0f), new Vector3(6.5f, 0.012f, 6.5f), gold);
            Shapes.Primitive(PrimitiveType.Cylinder, "DialInner", dial, new Vector3(0f, 0.08f, 0f), new Vector3(6.36f, 0.012f, 6.36f), navy);
            var core = MaterialFactory.Create(new Color(0.16f, 0.3f, 0.65f), new Color(0.2f, 0.45f, 1.1f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Core", dial, new Vector3(0f, 0.09f, 0f), new Vector3(2.1f, 0.012f, 2.1f), core);
            Shapes.Primitive(PrimitiveType.Cylinder, "CoreRim", dial, new Vector3(0f, 0.086f, 0f), new Vector3(2.3f, 0.012f, 2.3f), gold);
            Shapes.Primitive(PrimitiveType.Cylinder, "RimWall", dial, new Vector3(0f, -0.06f, 0f), new Vector3(7.25f, 0.14f, 7.25f), gold);
            var studs = MaterialFactory.Create(new Color(1f, 0.85f, 0.5f), new Color(0.9f, 0.65f, 0.25f));
            for (int i = 0; i < 32; i++)
            {
                float ang = (i + 0.5f) * Mathf.PI * 2f / 32f;
                Shapes.Primitive(PrimitiveType.Sphere, "Stud", dial, new Vector3(Mathf.Sin(ang) * 3.58f, 0.03f, Mathf.Cos(ang) * 3.58f), new Vector3(0.12f, 0.08f, 0.12f), studs);
            }
            // The medallion: rings round an eight-pointed gold star that glows.
            Shapes.Primitive(PrimitiveType.Cylinder, "MedalRing", dial, new Vector3(0f, 0.095f, 0f), new Vector3(1.5f, 0.01f, 1.5f), gold);
            Shapes.Primitive(PrimitiveType.Cylinder, "Medal", dial, new Vector3(0f, 0.1f, 0f), new Vector3(1.36f, 0.01f, 1.36f), core);
            for (int i = 0; i < 4; i++)
            {
                var ray = Shapes.Primitive(PrimitiveType.Cube, "Ray", dial, new Vector3(0f, 0.105f, 0f), new Vector3(0.09f, 0.01f, i % 2 == 0 ? 1.25f : 0.8f), starGold).transform;
                ray.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
            }
            // Tick marks round the rim, spokes between the fields.
            for (int i = 0; i < 48; i++)
            {
                float ang = i * Mathf.PI * 2f / 48f;
                var tick = Shapes.Primitive(PrimitiveType.Cube, "Tick", dial, new Vector3(Mathf.Sin(ang) * 3.33f, 0.085f, Mathf.Cos(ang) * 3.33f), new Vector3(0.025f, 0.01f, i % 6 == 0 ? 0.16f : 0.08f), gold).transform;
                tick.localRotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
            }
            var rng = new System.Random(7);
            for (int s = 0; s < 8; s++)
            {
                float ang = (s + 0.5f) * Mathf.PI * 2f / 8f;
                var spoke = Shapes.Primitive(PrimitiveType.Cube, "Spoke", dial, new Vector3(Mathf.Sin(ang) * 2.15f, 0.09f, Mathf.Cos(ang) * 2.15f), new Vector3(0.03f, 0.01f, 2.1f), gold).transform;
                spoke.localRotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
                // A constellation in the middle of the field: a few stars joined by thin lines.
                float mid = s * Mathf.PI * 2f / 8f;
                var centre = new Vector3(Mathf.Sin(mid) * 2.15f, 0.095f, Mathf.Cos(mid) * 2.15f);
                var stars = new List<Vector3>();
                int count = 4 + rng.Next(2);
                for (int k = 0; k < count; k++)
                    stars.Add(centre + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.9f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.9f));
                for (int k = 0; k < stars.Count; k++)
                {
                    Shapes.Primitive(PrimitiveType.Sphere, "Star", dial, stars[k], new Vector3(0.11f, 0.03f, 0.11f), k % 2 == 0 ? starGold : lines);
                    if (k == 0) continue;
                    var d = stars[k] - stars[k - 1];
                    var line = Shapes.Primitive(PrimitiveType.Cube, "Line", dial, stars[k - 1] + d * 0.5f, new Vector3(0.028f, 0.008f, d.magnitude), lines).transform;
                    line.localRotation = Quaternion.LookRotation(d);
                }
            }

            // Behind the dial: an observatory's colonnade on an arc (dark stone, gold capitals and bases), a gold arch
            // running over the columns, a low balustrade between them.
            var colonnade = new GameObject("Colonnade").transform;
            colonnade.SetParent(transform, false);
            var stone = MaterialFactory.Create(new Color(0.16f, 0.17f, 0.32f), new Color(0.03f, 0.03f, 0.08f));
            const int columns = 9;
            const float arcR = 7.2f;
            Vector3 prevTop = Vector3.zero;
            for (int i = 0; i < columns; i++)
            {
                float ang = Mathf.Lerp(-72f, 72f, i / (columns - 1f)) * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Sin(ang) * arcR, 0f, Mathf.Cos(ang) * arcR * 0.8f + 1.5f);
                Shapes.Rounded("Base", colonnade, at + new Vector3(0f, 0.1f, 0f), new Vector3(0.7f, 0.2f, 0.7f), 0.05f, gold);
                // Low columns with stone caps: nothing bright up at the top of the screen.
                Shapes.Rounded("Column", colonnade, at + new Vector3(0f, 1.35f, 0f), new Vector3(0.42f, 2.3f, 0.42f), 0.18f, stone);
                Shapes.Rounded("Cap", colonnade, at + new Vector3(0f, 2.55f, 0f), new Vector3(0.58f, 0.16f, 0.58f), 0.05f, stone);
                var top = at + new Vector3(0f, 2.6f, 0f);
                if (i > 0)
                {
                    var d = top - prevTop;
                    var rail = Shapes.Rounded("Rail", colonnade, prevTop + d * 0.5f - Vector3.up * 1.6f, new Vector3(0.16f, 0.12f, d.magnitude), 0.04f, stone).transform;
                    rail.localRotation = Quaternion.LookRotation(d);
                }
                prevTop = top;
            }

            // A brass globe on its stand to the left, half in the shadows.
            var globe = new GameObject("Globe").transform;
            globe.SetParent(transform, false);
            globe.localPosition = new Vector3(-4.3f, 0f, 1.8f);
            Shapes.Primitive(PrimitiveType.Cylinder, "Foot", globe, new Vector3(0f, 0.05f, 0f), new Vector3(0.7f, 0.05f, 0.7f), gold);
            Shapes.Primitive(PrimitiveType.Cylinder, "Stem", globe, new Vector3(0f, 0.5f, 0f), new Vector3(0.08f, 0.45f, 0.08f), gold);
            Shapes.Primitive(PrimitiveType.Sphere, "Ball", globe, new Vector3(0f, 1.3f, 0f), Vector3.one * 0.95f, MaterialFactory.Create(new Color(0.25f, 0.45f, 0.75f), new Color(0.15f, 0.3f, 0.6f)));
            var meridian = Shapes.Primitive(PrimitiveType.Cylinder, "Meridian", globe, new Vector3(0f, 1.3f, 0f), new Vector3(1.12f, 0.01f, 1.12f), gold).transform;
            meridian.localRotation = Quaternion.Euler(0f, 0f, 70f);
            globeSpin = globe.Find("Ball");

            StaticMerge.Combine(dial.gameObject, _ => false);
            StaticMerge.Combine(colonnade.gameObject, _ => false);
            StaticMerge.Combine(ringA.gameObject, _ => false);
            StaticMerge.Combine(ringB.gameObject, _ => false);

            // Sparks drifting up.
            sparks = new GameObject("Sparks").transform;
            sparks.SetParent(transform, false);
            var spark = MaterialFactory.Create(Color.white, new Color(1.6f, 1.6f, 2f));
            for (int i = 0; i < 18; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 9f - 4.5f, (float)rng.NextDouble() * 6f - 0.5f, (float)rng.NextDouble() * 6f - 1f);
                var sp = Shapes.Primitive(PrimitiveType.Cube, "Spark", sparks, p, Vector3.one * (0.03f + (float)rng.NextDouble() * 0.04f), spark).transform;
                sparkList.Add(sp);
            }

            robotHolder = new GameObject("Robot").transform;
            robotHolder.SetParent(transform, false);
            robotHolder.localPosition = new Vector3(0f, 0.16f, 0f);
            robotHolder.localScale = Vector3.one * 1.8f;
        }

        /// <summary>A thin golden orbit (a ring of short segments), tilted, round a centre high behind foi.</summary>
        private Transform Orbit(string name, float radius, float width, Material mat, Vector3 at, float tilt, float roll)
        {
            var ring = new GameObject(name).transform;
            ring.SetParent(transform, false);
            ring.localPosition = at;
            ring.localRotation = Quaternion.Euler(tilt, 0f, roll);
            const int parts = 72;
            float seg = 2f * Mathf.PI * radius / parts * 1.05f;
            for (int i = 0; i < parts; i++)
            {
                float a = i * Mathf.PI * 2f / parts;
                var p = Shapes.Primitive(PrimitiveType.Cube, "Arc", ring, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius), new Vector3(width, width, seg), mat).transform;
                p.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
            return ring;
        }

        /// <summary>Shows the stage with the robot's current look, tinted for the world the player is in.</summary>
        public void Open(WorldTheme theme)
        {
            gameObject.SetActive(true);
            Cam.fieldOfView = CameraFit.Fov(34f, Cam.aspect);
            for (int i = robotHolder.childCount - 1; i >= 0; i--) Destroy(robotHolder.GetChild(i).gameObject);
            robotLook?.Invoke(robotHolder);
            Cam.backgroundColor = new Color(0.03f, 0.03f, 0.09f);
        }

        public void Close() => gameObject.SetActive(false);

        public bool IsOpen => gameObject.activeSelf;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            time += dt;
            // The robot turns a little this way and that, as if showing off, with a slow breath.
            robotHolder.localRotation = Quaternion.Euler(0f, 180f + Mathf.Sin(time * 0.5f) * 28f, 0f); // the model faces +Z, the camera is on -Z
            robotHolder.localPosition = new Vector3(0f, 0.16f + Mathf.Abs(Mathf.Sin(time * 1.6f)) * 0.03f, 0f);
            ringA.localRotation = Quaternion.Euler(74f, 0f, 12f) * Quaternion.Euler(0f, time * 4f, 0f);
            ringB.localRotation = Quaternion.Euler(78f, 0f, -8f) * Quaternion.Euler(0f, -time * 3f, 0f);
            globeSpin.localRotation = Quaternion.Euler(0f, time * 20f, 0f);
            foreach (var s in sparkList)
            {
                var p = s.localPosition;
                p.y += dt * 0.35f;
                if (p.y > 5.5f) p.y = -0.4f;
                s.localPosition = p;
                s.localRotation = Quaternion.Euler(time * 40f, time * 60f, 0f);
            }
        }
    }
}
