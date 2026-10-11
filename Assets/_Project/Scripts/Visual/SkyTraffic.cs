using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Air traffic over the later fortresses: a few small craft (dark hulls, warm cockpit and engine glow) that cross the
    /// sky on their own, high above the floor, each pass starting near wherever the camera is looking so there is always
    /// one about. Decoration only.
    /// </summary>
    public class SkyTraffic : MonoBehaviour
    {
        private class Craft
        {
            public Transform t;
            public Vector3 from, to;
            public float time, duration, bob;
        }

        private readonly List<Craft> craft = new List<Craft>();
        private Func<Vector3> focus;
        private System.Random rng;

        public static SkyTraffic Create(Transform parent, Func<Vector3> focus, Color light, int count, int seed)
        {
            var go = new GameObject("SkyTraffic");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<SkyTraffic>();
            s.focus = focus;
            s.rng = new System.Random(seed);
            var hull = MaterialFactory.CreateSurface(new Color(0.22f, 0.22f, 0.27f), Color.black, 0.55f, 0.6f);
            var trim = MaterialFactory.CreateSurface(new Color(0.42f, 0.42f, 0.48f), Color.black, 0.6f, 0.7f);
            var glow = MaterialFactory.Create(light, light * 2.6f);
            for (int i = 0; i < count; i++)
            {
                var c = new Craft { t = BuildCraft(go.transform, hull, trim, glow, i % 2), bob = (float)s.rng.NextDouble() * 10f };
                s.craft.Add(c);
                s.NewPass(c, (float)s.rng.NextDouble());
            }
            return s;
        }

        private static Transform BuildCraft(Transform parent, Material hull, Material trim, Material glow, int kind)
        {
            var t = new GameObject("Craft").transform;
            t.SetParent(parent, false);
            Shapes.Rounded("Hull", t, Vector3.zero, new Vector3(0.7f, 0.32f, 1.6f), 0.15f, hull);
            Shapes.Rounded("Nose", t, new Vector3(0f, -0.02f, 0.85f), new Vector3(0.46f, 0.24f, 0.4f), 0.11f, trim);
            Shapes.Rounded("Cockpit", t, new Vector3(0f, 0.15f, 0.45f), new Vector3(0.36f, 0.12f, 0.42f), 0.06f, glow);
            foreach (float s in new[] { -1f, 1f })
            {
                if (kind == 0)
                {
                    Shapes.Rounded("Wing", t, new Vector3(s * 0.62f, -0.02f, -0.2f), new Vector3(0.6f, 0.06f, 0.6f), 0.03f, trim);
                    Shapes.Rounded("Pod", t, new Vector3(s * 0.88f, 0f, -0.25f), new Vector3(0.22f, 0.22f, 0.75f), 0.1f, hull);
                    Shapes.Rounded("PodGlow", t, new Vector3(s * 0.88f, 0f, -0.64f), new Vector3(0.16f, 0.16f, 0.04f), 0.03f, glow);
                }
                else
                {
                    Shapes.Rounded("Fin", t, new Vector3(s * 0.4f, 0.16f, -0.5f), new Vector3(0.06f, 0.36f, 0.4f), 0.02f, trim)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, -s * 25f);
                    Shapes.Rounded("Thruster", t, new Vector3(s * 0.22f, -0.04f, -0.82f), new Vector3(0.2f, 0.18f, 0.06f), 0.04f, glow);
                }
            }
            Shapes.Rounded("Engine", t, new Vector3(0f, 0f, -0.82f), new Vector3(0.3f, 0.16f, 0.04f), 0.03f, glow);
            t.localScale = Vector3.one * (kind == 0 ? 1.1f : 0.85f);
            return t;
        }

        /// <summary>A new straight pass across the sky near where the camera looks (or a delayed start, 0..1).</summary>
        private void NewPass(Craft c, float startAt = 0f)
        {
            var centre = focus != null ? focus() : Vector3.zero;
            float R() => (float)rng.NextDouble();
            float angle = R() * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var side = new Vector3(-dir.z, 0f, dir.x);
            // Mostly beyond the floor (north-east, where the camera looks), high up.
            var mid = centre + new Vector3(6f, 0f, 6f) * (0.6f + R() * 0.8f) + side * (R() * 10f - 5f);
            float height = 7f + R() * 6f, span = 26f + R() * 12f;
            c.from = mid - dir * span * 0.5f + Vector3.up * height;
            c.to = mid + dir * span * 0.5f + Vector3.up * (height + R() * 2f - 1f);
            c.duration = span / (2.2f + R() * 2.5f);
            c.time = startAt * c.duration;
            c.t.rotation = Quaternion.LookRotation(c.to - c.from);
        }

        private void Update()
        {
            foreach (var c in craft)
            {
                c.time += Time.deltaTime;
                float k = c.time / c.duration;
                if (k >= 1f) { NewPass(c); k = 0f; }
                c.t.position = Vector3.Lerp(c.from, c.to, k) + Vector3.up * Mathf.Sin(Time.time * 1.3f + c.bob) * 0.15f;
                c.t.rotation = Quaternion.LookRotation(c.to - c.from) * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 0.9f + c.bob) * 6f);
            }
        }
    }
}
