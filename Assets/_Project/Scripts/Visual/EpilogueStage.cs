using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The closing scene behind the ending story: vanG is gone and nature wakes up. A bright green hill under a real sun,
    /// trees set free, a waterfall pouring from a cliff, broken grey tunnel walls with grass growing through them, the
    /// System Spirits rising as lights into the sky, and on the hilltop the Observer, its paint tank set down beside
    /// it, with Bip at its side. The camera slowly rises and pulls back. Built far away with its own camera, drawn over
    /// the game while the story's lines play.
    /// </summary>
    public class EpilogueStage : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(-6000f, 0f, -6000f);
        private const float CameraTravel = 26f;

        private Camera cam;
        private Transform robotRoot, water;
        private Material waterMat;
        private readonly List<Transform> spirits = new List<Transform>();
        private readonly List<Renderer> eyes = new List<Renderer>();
        private float t;

        public static EpilogueStage Create(Func<Transform, GameObject> robotLook)
        {
            var stage = new GameObject("Epilogue").AddComponent<EpilogueStage>();
            stage.Build(robotLook);
            return stage;
        }

        private static Material M(string hex, Color emission = default)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return MaterialFactory.Create(c, emission);
        }

        private Transform Part(PrimitiveType type, Vector3 at, Vector3 scale, Material m) =>
            Shapes.Primitive(type, "Part", transform, at, scale, m).transform;

        private void Build(Func<Transform, GameObject> robotLook)
        {
            transform.position = Origin;
            cam = new GameObject("EpilogueCamera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.depth = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            ColorUtility.TryParseHtmlString("#8FD0F2", out var sky);
            cam.backgroundColor = sky;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;

            // The sun, its halo, and soft clouds.
            Part(PrimitiveType.Sphere, new Vector3(0f, 34f, 150f), Vector3.one * 22f, M("#FFF2C0", new Color(2.4f, 2.1f, 1.3f)));
            Part(PrimitiveType.Sphere, new Vector3(0f, 34f, 152f), Vector3.one * 40f, MaterialFactory.CreateTransparent(new Color(1f, 0.95f, 0.75f, 0.25f), new Color(1.2f, 1f, 0.6f)));
            var cloud = M("#FFFFFF", new Color(0.5f, 0.5f, 0.55f));
            foreach (var (x, y, z, s) in new[] { (-50f, 40f, 140f, 14f), (60f, 46f, 160f, 18f), (-20f, 55f, 180f, 12f), (35f, 30f, 120f, 10f) })
                for (int i = 0; i < 3; i++)
                    Part(PrimitiveType.Sphere, new Vector3(x + i * s * 0.8f, y + (i == 1 ? s * 0.3f : 0f), z), new Vector3(s * 1.4f, s, s), cloud);

            // Hills: the bright one under the robot, softer ones behind fading into the haze.
            Part(PrimitiveType.Sphere, new Vector3(0f, -4f, 0f), new Vector3(46f, 9f, 46f), M("#7CCB6A", new Color(0.05f, 0.12f, 0.03f)));
            Part(PrimitiveType.Sphere, new Vector3(-40f, -6f, 60f), new Vector3(70f, 18f, 50f), M("#6BB86A"));
            Part(PrimitiveType.Sphere, new Vector3(45f, -8f, 80f), new Vector3(80f, 22f, 60f), M("#8CC9A0"));
            Part(PrimitiveType.Sphere, new Vector3(0f, -10f, 130f), new Vector3(160f, 24f, 60f), M("#A8D6C8"));

            // Trees set free, flowers in the grass.
            var trunk = M("#7A5236");
            var leaves = new[] { M("#4FA65A"), M("#66BE5E"), M("#3E8F55") };
            var rng = new System.Random(5);
            for (int i = 0; i < 16; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 9f + (float)rng.NextDouble() * 14f;
                var at = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r + 6f);
                if (at.z < 2f && Mathf.Abs(at.x) < 6f) continue; // keep the view of the robot open
                at.y = HillY(at) - 0.2f;
                float h = 1.6f + (float)rng.NextDouble() * 1.4f;
                Part(PrimitiveType.Cylinder, at + Vector3.up * h * 0.5f, new Vector3(0.35f, h * 0.5f, 0.35f), trunk);
                var leaf = leaves[rng.Next(leaves.Length)];
                Part(PrimitiveType.Sphere, at + Vector3.up * (h + 0.6f), Vector3.one * (2f + (float)rng.NextDouble()), leaf);
                Part(PrimitiveType.Sphere, at + new Vector3(0.6f, h + 1.3f, 0.2f), Vector3.one * 1.4f, leaf);
            }
            var petals = new[] { M("#FFD1E8", new Color(0.3f, 0.2f, 0.25f)), M("#FFF3A8", new Color(0.3f, 0.3f, 0.1f)), M("#FFFFFF"), M("#C8B8FF") };
            for (int i = 0; i < 70; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 1.5f + (float)rng.NextDouble() * 10f;
                var at = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                at.y = HillY(at) + 0.05f;
                Part(PrimitiveType.Sphere, at, Vector3.one * 0.22f, petals[rng.Next(petals.Length)]);
            }

            // The waterfall pouring from a green-topped cliff, foaming into a pool.
            Part(PrimitiveType.Cube, new Vector3(-16f, 5f, 22f), new Vector3(9f, 16f, 5f), M("#8A7A6E"));
            Part(PrimitiveType.Cube, new Vector3(-16f, 13.2f, 22f), new Vector3(9.4f, 0.8f, 5.4f), M("#6BB86A"));
            waterMat = MaterialFactory.CreateTransparent(new Color(0.6f, 0.85f, 1f, 0.8f), new Color(0.4f, 0.8f, 1.2f));
            water = Part(PrimitiveType.Cube, new Vector3(-14f, 6f, 19.4f), new Vector3(2.6f, 14f, 0.3f), waterMat);
            Part(PrimitiveType.Cylinder, new Vector3(-14f, -0.6f, 17f), new Vector3(7f, 0.1f, 4f), waterMat);
            var foam = M("#FFFFFF", new Color(0.6f, 0.7f, 0.8f));
            for (int i = 0; i < 6; i++) Part(PrimitiveType.Sphere, new Vector3(-15.2f + i * 0.5f, -0.3f, 18.6f), Vector3.one * 0.9f, foam);

            // What is left of vanG's grey tunnels: broken slabs, grass growing through.
            var slab = M("#9AA0B0");
            foreach (var (x, z, rot) in new[] { (9f, 10f, 25f), (12f, 4f, -40f), (-9f, 6f, 60f) })
            {
                var at = new Vector3(x, 0f, z);
                at.y = HillY(at);
                Part(PrimitiveType.Cube, at + Vector3.up * 0.6f, new Vector3(2.6f, 1.6f, 0.3f), slab).localRotation = Quaternion.Euler(12f, rot, 20f);
                Part(PrimitiveType.Sphere, at + new Vector3(0.4f, 0.2f, 0.3f), new Vector3(1.2f, 0.5f, 1.2f), leaves[0]);
            }

            // The Observer on the hilltop, its paint tank set down, Bip beside it.
            robotRoot = new GameObject("Observer").transform;
            robotRoot.SetParent(transform, false);
            robotRoot.localPosition = new Vector3(0f, HillY(Vector3.zero), 0f);
            robotRoot.localScale = Vector3.one * 1.3f;
            robotLook?.Invoke(robotRoot);
            foreach (var r in robotRoot.GetComponentsInChildren<Renderer>())
                if (r.name.StartsWith("Eye")) eyes.Add(r);
            Part(PrimitiveType.Cylinder, robotRoot.localPosition + new Vector3(-0.8f, 0.3f, 0.1f), new Vector3(0.45f, 0.3f, 0.45f), M("#C9CFE0"));
            Part(PrimitiveType.Cylinder, robotRoot.localPosition + new Vector3(-0.8f, 0.42f, 0.1f), new Vector3(0.47f, 0.05f, 0.47f), M("#FFC94A", new Color(1.6f, 1.1f, 0.3f)));
            var bip = Buddy.Create(transform.TransformPoint(robotRoot.localPosition + new Vector3(0.9f, 0f, -0.1f)));
            bip.transform.SetParent(transform, true);
            bip.transform.localScale = Vector3.one * 1.3f;
            bip.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var glow = bip.transform.Find("Glow");
            if (glow != null) glow.gameObject.SetActive(false);

            // The System Spirits, freed, rising as lights.
            var spiritMat = MaterialFactory.Create(new Color(1f, 0.95f, 0.8f), new Color(2.2f, 2f, 1.4f));
            for (int i = 0; i < 14; i++)
            {
                var s = Part(PrimitiveType.Sphere, new Vector3(-20f + i * 3f, 0f, 10f + (i % 3) * 8f), Vector3.one * 0.35f, spiritMat);
                spirits.Add(s);
            }

            UpdateCamera(0f);
            Data.Story.LineShown += OnLine;
        }

        /// <summary>The height of the main hill at a point (its top is the robot's spot).</summary>
        private static float HillY(Vector3 p)
        {
            float k = (p.x * p.x + p.z * p.z) / (23f * 23f);
            return k >= 1f ? -4f : -4f + 4.5f * Mathf.Sqrt(1f - k);
        }

        private void OnLine(int scene, int line)
        {
            // The last line: the cold blue of the Observer's eyes turns a warm green.
            if (scene != Data.Story.Ending || line != Data.Story.LineCount(scene) - 1) return;
            var green = MaterialFactory.Create(new Color(0.5f, 1f, 0.55f), new Color(0.8f, 2.4f, 0.9f));
            foreach (var r in eyes) if (r != null) r.sharedMaterial = green;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            UpdateCamera(t);
            for (int i = 0; i < spirits.Count; i++)
            {
                var s = spirits[i];
                float y = Mathf.Repeat(t * (1.5f + (i % 4) * 0.4f) + i * 3f, 40f);
                var p = s.localPosition;
                s.localPosition = new Vector3(p.x + Mathf.Sin(t + i) * dt * 0.5f, y, p.z);
            }
            if (waterMat != null)
                MaterialFactory.SetColors(waterMat, new Color(0.6f, 0.85f, 1f, 0.75f + 0.1f * Mathf.Sin(t * 9f)), new Color(0.4f, 0.8f, 1.2f) * (0.9f + 0.2f * Mathf.Sin(t * 13f)));
        }

        /// <summary>Close behind the robot looking at the sun, then slowly up and back to take in the whole valley.</summary>
        private void UpdateCamera(float time)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / CameraTravel));
            var top = robotRoot != null ? robotRoot.localPosition : Vector3.zero;
            var pos = Vector3.Lerp(top + new Vector3(1.2f, 2.4f, -6.5f), top + new Vector3(0f, 9f, -18f), k);
            var look = Vector3.Lerp(top + new Vector3(0f, 1.2f, 6f), new Vector3(0f, 6f, 60f), k);
            cam.transform.localPosition = pos;
            cam.transform.localRotation = Quaternion.LookRotation(look - pos);
        }

        public void Close() => Destroy(gameObject);

        private void OnDestroy() => Data.Story.LineShown -= OnLine;
    }
}
