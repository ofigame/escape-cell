using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    /// <summary>
    /// The main menu's showroom: the robot (in its current outfit) on a lit pedestal under a soft beam of light, rings of
    /// glowing beads slowly orbiting behind it, sparks drifting up and a halo in the world's colour. It has its own
    /// camera and only draws while the menu is open, so the menu looks like a polished lobby rather than a paused level.
    /// </summary>
    public class LobbyStage : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(6000f, 0f, -6000f);

        public Camera Cam { get; private set; }

        private Transform robotHolder, ringA, ringB, sparks, beam;
        private Func<Transform, GameObject> robotLook;
        private readonly List<Transform> sparkList = new List<Transform>();
        private readonly List<Material> accentMats = new List<Material>();
        private Material halo, beamMat;
        private Color accent = Color.cyan;
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
            camGo.transform.localPosition = new Vector3(0f, 2.6f, -13f);
            camGo.transform.LookAt(Origin + new Vector3(0f, -0.08f, 0f)); // the pedestal sits above the level card

            // Floor and a halo behind everything.
            var floor = MaterialFactory.Create(new Color(0.09f, 0.08f, 0.2f), new Color(0.03f, 0.025f, 0.08f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Floor", transform, new Vector3(0f, -0.32f, 0f), new Vector3(26f, 0.02f, 26f), floor);
            halo = MaterialFactory.CreateTransparent(new Color(0.5f, 0.8f, 1f, 0.16f), new Color(0.4f, 0.7f, 1f) * 0.6f);
            Shapes.Primitive(PrimitiveType.Sphere, "Halo", transform, new Vector3(0f, 1.6f, 6f), new Vector3(11f, 11f, 0.2f), halo);

            // The pedestal: a dark base, a glowing rim, a bright top and an inner ring.
            var dark = MaterialFactory.Create(new Color(0.14f, 0.12f, 0.3f), Color.black);
            var top = MaterialFactory.Create(new Color(0.85f, 0.88f, 1f), new Color(0.25f, 0.27f, 0.35f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Base", transform, new Vector3(0f, -0.15f, 0f), new Vector3(3.4f, 0.17f, 3.4f), dark);
            accentMats.Add(MaterialFactory.Create(accent, accent * 1.6f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Rim", transform, new Vector3(0f, 0.04f, 0f), new Vector3(3.55f, 0.04f, 3.55f), accentMats[0]);
            Shapes.Primitive(PrimitiveType.Cylinder, "Top", transform, new Vector3(0f, 0.09f, 0f), new Vector3(3.1f, 0.05f, 3.1f), top);
            accentMats.Add(MaterialFactory.Create(accent, accent * 1.2f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Inner", transform, new Vector3(0f, 0.14f, 0f), new Vector3(2.2f, 0.01f, 2.2f), accentMats[1]);
            Shapes.Primitive(PrimitiveType.Cylinder, "InnerTop", transform, new Vector3(0f, 0.15f, 0f), new Vector3(2.05f, 0.01f, 2.05f), top);

            // A soft beam of light from above: stacked see-through rings, wider towards the floor.
            beam = new GameObject("Beam").transform;
            beam.SetParent(transform, false);
            beamMat = MaterialFactory.CreateTransparent(new Color(0.8f, 0.9f, 1f, 0.05f), new Color(0.6f, 0.7f, 1f) * 0.25f);
            for (int i = 0; i < 5; i++)
            {
                float r = 1.2f + i * 0.35f;
                Shapes.Primitive(PrimitiveType.Cylinder, "Shaft", beam, new Vector3(0f, 4.2f - i * 0.15f, 0f), new Vector3(r, 4.2f, r), beamMat);
            }

            // Two tilted rings of glowing beads behind the robot.
            ringA = Ring("RingA", 3.1f, 40, 0.09f, 18f);
            ringB = Ring("RingB", 3.9f, 52, 0.07f, -12f);

            // Sparks drifting up.
            sparks = new GameObject("Sparks").transform;
            sparks.SetParent(transform, false);
            var spark = MaterialFactory.Create(Color.white, new Color(1.6f, 1.6f, 2f));
            var rng = new System.Random(3);
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 9f - 4.5f, (float)rng.NextDouble() * 6f - 0.5f, (float)rng.NextDouble() * 6f - 1f);
                var t = Shapes.Primitive(PrimitiveType.Cube, "Spark", sparks, p, Vector3.one * (0.04f + (float)rng.NextDouble() * 0.05f), spark).transform;
                sparkList.Add(t);
            }

            robotHolder = new GameObject("Robot").transform;
            robotHolder.SetParent(transform, false);
            robotHolder.localPosition = new Vector3(0f, 0.16f, 0f);
            robotHolder.localScale = Vector3.one * 1.8f;
        }

        private Transform Ring(string name, float radius, int beads, float size, float tilt)
        {
            var ring = new GameObject(name).transform;
            ring.SetParent(transform, false);
            ring.localPosition = new Vector3(0f, 1.3f, 1.2f);
            ring.localRotation = Quaternion.Euler(72f, 0f, tilt);
            var mat = MaterialFactory.Create(accent, accent * 1.8f);
            accentMats.Add(mat);
            for (int i = 0; i < beads; i++)
            {
                float a = i * Mathf.PI * 2f / beads;
                Shapes.Primitive(PrimitiveType.Sphere, "Bead", ring, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius), Vector3.one * size * (i % 5 == 0 ? 1.8f : 1f), mat);
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
            accent = theme.accent;
            foreach (var m in accentMats) MaterialFactory.SetColors(m, accent, accent * 1.6f);
            MaterialFactory.SetColors(halo, new Color(accent.r, accent.g, accent.b, 0.14f), accent * 0.5f);
            Cam.backgroundColor = Color.Lerp(theme.bgTop, theme.bgBottom, 0.35f) * 0.55f;
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
            ringA.localRotation = Quaternion.Euler(72f, 0f, 18f) * Quaternion.Euler(0f, time * 9f, 0f);
            ringB.localRotation = Quaternion.Euler(72f, 0f, -12f) * Quaternion.Euler(0f, -time * 6f, 0f);
            foreach (var s in sparkList)
            {
                var p = s.localPosition;
                p.y += dt * 0.35f;
                if (p.y > 5.5f) p.y = -0.4f;
                s.localPosition = p;
                s.localRotation = Quaternion.Euler(time * 40f, time * 60f, 0f);
            }
            MaterialFactory.SetColors(beamMat, new Color(0.8f, 0.9f, 1f, 0.04f + 0.015f * Mathf.Sin(time * 2f)), new Color(0.6f, 0.7f, 1f) * 0.25f);
        }
    }
}
