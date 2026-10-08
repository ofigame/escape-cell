using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A floor bug, a different kind every few worlds: a domed beetle with a glowing back, a three-segment ant, a
    /// hairy round spider on eight legs, a red ladybird with black spots and a scorpion with claws and a raised sting.
    /// Built with real volume (domes, segments, jointed legs) so it reads as 3D from the play camera. Scuttles while
    /// it moves; under a foot or a hammer it flattens into the floor (it never grows). About 0.5 units long, facing +z.
    /// </summary>
    public class BugModel : MonoBehaviour
    {
        public const int Kinds = 5;

        private readonly List<Transform> legs = new List<Transform>();
        private readonly List<float> legYaw = new List<float>();
        private Transform body, sting;
        private float moving, squash = -1f;

        /// <summary>The colour its insides are (for the splat when it is squashed).</summary>
        public Color Blood { get; private set; } = new Color(0.55f, 0.04f, 0.04f);

        public static BugModel Build(Transform parent, Color accent, int kind = 0)
        {
            var root = new GameObject("Bug").transform;
            root.SetParent(parent, false);
            var b = root.gameObject.AddComponent<BugModel>();
            b.Make(root, accent, ((kind % Kinds) + Kinds) % Kinds);
            return b;
        }

        private void Make(Transform root, Color accent, int kind)
        {
            var shell = MaterialFactory.Create(new Color(0.13f, 0.11f, 0.15f), new Color(0.01f, 0.01f, 0.015f));
            var glow = MaterialFactory.Create(accent, accent * 1.3f);
            var leg = MaterialFactory.Create(new Color(0.08f, 0.07f, 0.08f), Color.black);
            var eye = MaterialFactory.Create(new Color(1f, 0.95f, 0.6f), new Color(1.6f, 1.4f, 0.5f));
            body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localPosition = new Vector3(0f, 0.12f, 0f);
            int legPairs = 3;
            float legReach = 0.16f, legSpread = 0.1f;
            switch (kind)
            {
                case 1: // ant: three segments
                {
                    var ant = MaterialFactory.Create(new Color(0.45f, 0.12f, 0.06f), new Color(0.06f, 0.01f, 0f));
                    Blood = new Color(0.45f, 0.5f, 0.08f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Abdomen", body, new Vector3(0f, 0.02f, -0.17f), new Vector3(0.2f, 0.17f, 0.26f), ant);
                    Shapes.Primitive(PrimitiveType.Sphere, "Thorax", body, new Vector3(0f, 0.01f, 0.02f), new Vector3(0.12f, 0.11f, 0.16f), ant);
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.03f, 0.17f), new Vector3(0.15f, 0.13f, 0.14f), ant);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(s * 0.055f, 0.06f, 0.21f), Vector3.one * 0.04f, eye);
                        Shapes.Primitive(PrimitiveType.Cylinder, "Mandible", body, new Vector3(s * 0.04f, 0f, 0.25f), new Vector3(0.02f, 0.04f, 0.02f), leg)
                            .transform.localRotation = Quaternion.Euler(90f, s * 25f, 0f);
                    }
                    legSpread = 0.08f;
                    break;
                }
                case 2: // spider: round, eight legs
                {
                    var fur = MaterialFactory.Create(new Color(0.2f, 0.16f, 0.12f), Color.black);
                    Blood = new Color(0.2f, 0.55f, 0.15f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Abdomen", body, new Vector3(0f, 0.06f, -0.08f), new Vector3(0.3f, 0.26f, 0.32f), fur);
                    Shapes.Primitive(PrimitiveType.Sphere, "Mark", body, new Vector3(0f, 0.18f, -0.08f), new Vector3(0.12f, 0.04f, 0.16f), glow);
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.02f, 0.12f), new Vector3(0.16f, 0.13f, 0.15f), fur);
                    for (int i = 0; i < 4; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(-0.045f + i * 0.03f, 0.06f, 0.19f), Vector3.one * 0.03f, eye);
                    legPairs = 4;
                    legReach = 0.22f;
                    legSpread = 0.07f;
                    break;
                }
                case 3: // ladybird: red dome with black spots
                {
                    var red = MaterialFactory.Create(new Color(0.9f, 0.12f, 0.1f), new Color(0.12f, 0.01f, 0f));
                    Blood = new Color(0.9f, 0.6f, 0.1f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Dome", body, new Vector3(0f, 0.04f, -0.02f), new Vector3(0.32f, 0.22f, 0.36f), red);
                    Shapes.Rounded("Split", body, new Vector3(0f, 0.15f, -0.02f), new Vector3(0.012f, 0.012f, 0.32f), 0.005f, shell);
                    var rng = new System.Random(7);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 1.05f + 0.4f, r = 0.08f + (i % 2) * 0.03f;
                        Shapes.Primitive(PrimitiveType.Sphere, "Spot", body, new Vector3(Mathf.Cos(a) * r, 0.13f, -0.02f + Mathf.Sin(a) * r), new Vector3(0.06f, 0.03f, 0.06f), shell);
                    }
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.01f, 0.17f), new Vector3(0.14f, 0.1f, 0.1f), shell);
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(s * 0.04f, 0.04f, 0.21f), Vector3.one * 0.035f, eye);
                    break;
                }
                case 4: // scorpion: claws and a raised sting
                {
                    var carapace = MaterialFactory.Create(new Color(0.38f, 0.3f, 0.12f), new Color(0.04f, 0.03f, 0.01f));
                    Blood = new Color(0.3f, 0.75f, 0.3f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Body", body, new Vector3(0f, 0.02f, 0f), new Vector3(0.24f, 0.13f, 0.32f), carapace);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Shapes.Primitive(PrimitiveType.Cylinder, "Arm", body, new Vector3(s * 0.13f, 0.01f, 0.17f), new Vector3(0.04f, 0.07f, 0.04f), carapace)
                            .transform.localRotation = Quaternion.Euler(90f, s * 30f, 0f);
                        Shapes.Primitive(PrimitiveType.Sphere, "Claw", body, new Vector3(s * 0.17f, 0.02f, 0.27f), new Vector3(0.09f, 0.06f, 0.11f), carapace);
                    }
                    sting = new GameObject("Tail").transform;
                    sting.SetParent(body, false);
                    sting.localPosition = new Vector3(0f, 0.04f, -0.16f);
                    for (int i = 0; i < 4; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Segment", sting, new Vector3(0f, 0.05f + i * 0.06f, -0.04f - i * 0.03f + i * i * 0.012f), Vector3.one * (0.09f - i * 0.01f), carapace);
                    Shapes.Primitive(PrimitiveType.Sphere, "Sting", sting, new Vector3(0f, 0.27f, 0.02f), new Vector3(0.05f, 0.08f, 0.05f), glow);
                    break;
                }
                default: // beetle: a domed shell with a glowing back
                {
                    Blood = new Color(0.55f, 0.04f, 0.04f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Shell", body, new Vector3(0f, 0.04f, -0.03f), new Vector3(0.32f, 0.22f, 0.4f), shell);
                    Shapes.Primitive(PrimitiveType.Sphere, "Back", body, new Vector3(0f, 0.11f, -0.04f), new Vector3(0.2f, 0.1f, 0.28f), glow);
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.01f, 0.19f), new Vector3(0.15f, 0.11f, 0.12f), shell);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Horn", body, new Vector3(0f, 0.05f, 0.26f), new Vector3(0.025f, 0.05f, 0.025f), shell)
                        .transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(s * 0.05f, 0.04f, 0.24f), Vector3.one * 0.035f, eye);
                    break;
                }
            }
            if (kind != 1 && kind != 4)
                foreach (float s in new[] { -1f, 1f })
                {
                    var feeler = Shapes.Primitive(PrimitiveType.Cylinder, "Feeler", body, new Vector3(s * 0.04f, 0.06f, 0.27f), new Vector3(0.012f, 0.07f, 0.012f), leg);
                    feeler.transform.localRotation = Quaternion.Euler(55f, s * 28f, 0f);
                }
            // Jointed legs: an upper part out to the side, a lower part down to the floor.
            for (int i = 0; i < legPairs * 2; i++)
            {
                float side = i < legPairs ? -1f : 1f;
                int k = i % legPairs;
                float z = (k - (legPairs - 1) * 0.5f) * legSpread;
                var pivot = new GameObject("Leg").transform;
                pivot.SetParent(body, false);
                pivot.localPosition = new Vector3(side * 0.1f, 0.01f, z);
                float yaw = side * (k - (legPairs - 1) * 0.5f) * -18f;
                pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
                legYaw.Add(yaw);
                var upper = Shapes.Primitive(PrimitiveType.Cylinder, "Upper", pivot, new Vector3(side * legReach * 0.4f, 0.03f, 0f), new Vector3(0.022f, legReach * 0.45f, 0.022f), leg);
                upper.transform.localRotation = Quaternion.Euler(0f, 0f, side * 70f);
                var lower = Shapes.Primitive(PrimitiveType.Cylinder, "Lower", pivot, new Vector3(side * legReach * 0.85f, -0.05f, 0f), new Vector3(0.018f, 0.07f, 0.018f), leg);
                lower.transform.localRotation = Quaternion.Euler(0f, 0f, side * 20f);
                legs.Add(pivot);
            }
        }

        /// <summary>Scuttling (legs going) for a moment.</summary>
        public void Scuttle() => moving = 0.45f;

        /// <summary>Flattened into the floor under a foot or a hammer.</summary>
        public void Squash() => squash = 0f;

        private void Update()
        {
            float t = Time.time * 30f;
            if (squash >= 0f)
            {
                // Pressed flat in a blink, a little wider, then sinking into the floor.
                squash += Time.deltaTime;
                float k = Mathf.Clamp01(squash / 0.08f);
                body.localScale = new Vector3(1f + 0.25f * k, Mathf.Lerp(1f, 0.12f, k), 1f + 0.25f * k);
                body.localPosition = new Vector3(0f, Mathf.Lerp(0.12f, 0.02f, k) - Mathf.Max(0f, squash - 0.5f) * 0.1f, 0f);
                for (int i = 0; i < legs.Count; i++) legs[i].localRotation = Quaternion.Euler(0f, legYaw[i], -20f);
                return;
            }
            moving = Mathf.Max(0f, moving - Time.deltaTime);
            float amp = moving > 0f ? 25f : 3f;
            for (int i = 0; i < legs.Count; i++)
                legs[i].localRotation = Quaternion.Euler(Mathf.Sin(t + i * 2.1f) * amp, legYaw[i], 0f);
            body.localPosition = new Vector3(0f, 0.12f + Mathf.Abs(Mathf.Sin(t * 0.5f)) * (moving > 0f ? 0.015f : 0.003f), 0f);
            if (sting != null) sting.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 4f) * 8f, 0f, 0f);
        }
    }
}
