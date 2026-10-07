using System;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The supply helicopter of the long levels: a chubby yellow chopper flies in, hovers over a tile, lowers the
    /// super-power crate on a rope, lets it go and flies off the other way. <see cref="Dropped"/> fires when the crate
    /// lands; the helicopter removes itself once it is out of sight.
    /// </summary>
    public class Helicopter : MonoBehaviour
    {
        public event Action Dropped;

        private const float FlyIn = 1.6f, Lower = 0.9f, FlyOut = 1.6f;
        private const float HoverHeight = 4.2f;

        private Transform body, rotor, tailRotor, rope, crate;
        private Vector3 from, hover, to, ground;
        private float t;
        private bool dropped;

        public static Helicopter Create(Vector3 groundTarget, Vector3 approachDir)
        {
            var go = new GameObject("Helicopter");
            var h = go.AddComponent<Helicopter>();
            h.ground = groundTarget;
            var dir = new Vector3(approachDir.x, 0f, approachDir.z).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = new Vector3(1f, 0f, 1f).normalized;
            h.hover = groundTarget + Vector3.up * HoverHeight;
            h.from = h.hover - dir * 14f + Vector3.up * 3f;
            h.to = h.hover + dir * 16f + Vector3.up * 4f;
            go.transform.position = h.from;
            h.Build();
            return h;
        }

        private void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            var yellow = MaterialFactory.Create(new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0.02f));
            var white = MaterialFactory.Create(new Color(0.95f, 0.96f, 1f), Color.black);
            var dark = MaterialFactory.Create(new Color(0.18f, 0.18f, 0.24f), Color.black);
            var glass = MaterialFactory.CreateTransparent(new Color(0.55f, 0.85f, 1f, 0.6f), new Color(0.3f, 0.5f, 0.7f));
            var red = MaterialFactory.Create(new Color(1f, 0.3f, 0.3f), new Color(1.6f, 0.2f, 0.2f));

            Shapes.Rounded("Cabin", body, Vector3.zero, new Vector3(1.1f, 0.9f, 1.6f), 0.42f, yellow);
            Shapes.Rounded("Stripe", body, new Vector3(0f, -0.05f, 0f), new Vector3(1.12f, 0.16f, 1.5f), 0.08f, white);
            Shapes.Rounded("Window", body, new Vector3(0f, 0.12f, 0.55f), new Vector3(0.9f, 0.5f, 0.55f), 0.22f, glass);
            Shapes.Rounded("Boom", body, new Vector3(0f, 0.15f, -1.3f), new Vector3(0.24f, 0.24f, 1.4f), 0.1f, yellow);
            Shapes.Rounded("Fin", body, new Vector3(0f, 0.45f, -1.95f), new Vector3(0.08f, 0.55f, 0.32f), 0.04f, yellow);
            Shapes.Primitive(PrimitiveType.Sphere, "Beacon", body, new Vector3(0f, 0.72f, -1.95f), Vector3.one * 0.1f, red);
            foreach (float s in new[] { -0.45f, 0.45f })
            {
                Shapes.Rounded("Skid", body, new Vector3(s, -0.62f, 0.05f), new Vector3(0.07f, 0.07f, 1.5f), 0.03f, dark);
                Shapes.Rounded("Strut", body, new Vector3(s * 0.9f, -0.5f, 0.35f), new Vector3(0.05f, 0.28f, 0.05f), 0.02f, dark);
                Shapes.Rounded("Strut", body, new Vector3(s * 0.9f, -0.5f, -0.3f), new Vector3(0.05f, 0.28f, 0.05f), 0.02f, dark);
            }
            Shapes.Rounded("Mast", body, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 0.25f, 0.12f), 0.04f, dark);
            rotor = new GameObject("Rotor").transform;
            rotor.SetParent(body, false);
            rotor.localPosition = new Vector3(0f, 0.7f, 0f);
            Shapes.Rounded("Blade", rotor, Vector3.zero, new Vector3(3.2f, 0.03f, 0.16f), 0.02f, dark);
            Shapes.Rounded("Blade", rotor, Vector3.zero, new Vector3(0.16f, 0.03f, 3.2f), 0.02f, dark);
            tailRotor = new GameObject("TailRotor").transform;
            tailRotor.SetParent(body, false);
            tailRotor.localPosition = new Vector3(0.14f, 0.35f, -1.95f);
            Shapes.Rounded("Blade", tailRotor, Vector3.zero, new Vector3(0.02f, 0.6f, 0.08f), 0.01f, dark);

            rope = Shapes.Rounded("Rope", transform, Vector3.zero, new Vector3(0.03f, 1f, 0.03f), 0.01f, dark).transform;
            crate = SuperCrate.BuildModel(transform);
            crate.localPosition = new Vector3(0f, -0.9f, 0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            rotor.localRotation = Quaternion.Euler(0f, t * 1400f, 0f);
            tailRotor.localRotation = Quaternion.Euler(t * 2000f, 0f, 0f);
            Vector3 pos;
            float lowered = 0f;
            if (t < FlyIn)
            {
                float k = Ease(t / FlyIn);
                pos = Vector3.Lerp(from, hover, k);
            }
            else if (t < FlyIn + Lower)
            {
                pos = hover + Vector3.up * Mathf.Sin((t - FlyIn) * 6f) * 0.05f;
                lowered = (t - FlyIn) / Lower;
            }
            else
            {
                if (!dropped)
                {
                    dropped = true;
                    crate.gameObject.SetActive(false);
                    rope.gameObject.SetActive(false);
                    Dropped?.Invoke();
                }
                float k = Mathf.Clamp01((t - FlyIn - Lower) / FlyOut);
                pos = Vector3.Lerp(hover, to, k * k);
                if (k >= 1f) Destroy(gameObject);
            }
            transform.position = pos;
            // Nose into the flight, tilted forward while moving, level when hovering.
            var dir = to - from;
            dir.y = 0f;
            float tilt = t < FlyIn ? Mathf.Lerp(18f, 0f, t / FlyIn) : t > FlyIn + Lower ? 20f : 0f;
            transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(tilt, 0f, Mathf.Sin(t * 2f) * 3f);
            if (!dropped)
            {
                // The crate swings below on its rope and is lowered towards the tile.
                float drop = Mathf.Lerp(0.9f, HoverHeight - 0.6f, lowered);
                var crateWorld = transform.position + Vector3.down * drop;
                crate.position = crateWorld;
                crate.rotation = Quaternion.Euler(0f, t * 40f, 0f);
                rope.position = transform.position + Vector3.down * (drop * 0.5f + 0.2f);
                rope.rotation = Quaternion.identity;
                rope.localScale = new Vector3(0.03f, Mathf.Max(0.1f, drop - 0.3f), 0.03f);
            }
        }

        private static float Ease(float x) => 1f - (1f - x) * (1f - x);
    }
}
