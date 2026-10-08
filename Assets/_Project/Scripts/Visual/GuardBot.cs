using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// vanG's guard robots, a different build every few worlds: the stocky walker (red visor, glowing core, clamp
    /// arms), the dome-headed tank on treads, the horned cyclops, the spiked brawler with drill fists and the hover
    /// drone on jets. All about 1.1 units tall, facing +z, with two arms they raise before a slam.
    /// </summary>
    public class GuardBot : MonoBehaviour
    {
        public const int Variants = 5;

        public Transform ArmL { get; private set; }
        public Transform ArmR { get; private set; }
        private Transform body, spin;
        private Material core;
        private float raise, flash;
        private int variant;

        public static GuardBot Build(Transform parent, Color? plates = null, int variant = 0)
        {
            var root = new GameObject("GuardBot").transform;
            root.SetParent(parent, false);
            var g = root.gameObject.AddComponent<GuardBot>();
            g.variant = ((variant % Variants) + Variants) % Variants;
            g.Make(root, plates ?? new Color(0.55f, 0.2f, 0.18f));
            return g;
        }

        private void Make(Transform root, Color plateColor)
        {
            var metal = MaterialFactory.Create(new Color(0.3f, 0.32f, 0.38f), Color.black);
            var dark = MaterialFactory.Create(new Color(0.14f, 0.15f, 0.19f), Color.black);
            var plate = MaterialFactory.Create(plateColor, plateColor * 0.12f);
            var red = MaterialFactory.Create(new Color(1f, 0.25f, 0.18f), new Color(2.6f, 0.4f, 0.2f));
            core = MaterialFactory.Create(new Color(1f, 0.45f, 0.2f), new Color(2.4f, 0.8f, 0.25f));

            // Lower body: legs, treads or jets.
            switch (variant)
            {
                case 1:
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Shapes.Rounded("Tread", root, new Vector3(s * 0.2f, 0.1f, 0f), new Vector3(0.16f, 0.2f, 0.5f), 0.08f, dark);
                        for (int i = -1; i <= 1; i++)
                            Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", root, new Vector3(s * 0.2f, 0.1f, i * 0.15f), new Vector3(0.14f, 0.09f, 0.14f), metal)
                                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                    break;
                case 4:
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Shapes.Primitive(PrimitiveType.Cylinder, "Jet", root, new Vector3(s * 0.15f, 0.2f, -0.05f), new Vector3(0.12f, 0.08f, 0.12f), dark);
                        Shapes.Primitive(PrimitiveType.Sphere, "Flame", root, new Vector3(s * 0.15f, 0.1f, -0.05f), new Vector3(0.1f, 0.14f, 0.1f), core);
                    }
                    break;
                default:
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Shapes.Rounded("Leg", root, new Vector3(s * 0.15f, 0.14f, 0f), new Vector3(0.14f, 0.28f, 0.16f), 0.04f, dark);
                        Shapes.Rounded("Foot", root, new Vector3(s * 0.15f, 0.04f, 0.04f), new Vector3(0.18f, 0.08f, 0.26f), 0.03f, metal);
                    }
                    break;
            }

            body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localPosition = new Vector3(0f, 0.3f, 0f);
            Shapes.Rounded("Torso", body, new Vector3(0f, 0.27f, 0f), new Vector3(0.62f, 0.5f, 0.44f), 0.1f, metal);
            Shapes.Rounded("Belly", body, new Vector3(0f, 0.08f, 0.02f), new Vector3(0.5f, 0.14f, 0.38f), 0.05f, dark);
            Shapes.Primitive(PrimitiveType.Sphere, "Core", body, new Vector3(0f, 0.28f, 0.22f), Vector3.one * 0.13f, core);

            // Head.
            switch (variant)
            {
                case 1:
                    Shapes.Primitive(PrimitiveType.Sphere, "Dome", body, new Vector3(0f, 0.62f, 0f), new Vector3(0.4f, 0.3f, 0.38f), plate);
                    Shapes.Rounded("Visor", body, new Vector3(0f, 0.62f, 0.17f), new Vector3(0.28f, 0.07f, 0.04f), 0.02f, red);
                    break;
                case 2:
                    Shapes.Rounded("Head", body, new Vector3(0f, 0.63f, 0.02f), new Vector3(0.38f, 0.28f, 0.34f), 0.1f, metal);
                    Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(0f, 0.64f, 0.18f), Vector3.one * 0.15f, red);
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Primitive(PrimitiveType.Cylinder, "Horn", body, new Vector3(s * 0.15f, 0.82f, 0f), new Vector3(0.05f, 0.1f, 0.05f), plate)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, -s * 25f);
                    break;
                case 4:
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.63f, 0f), Vector3.one * 0.34f, metal);
                    Shapes.Rounded("Visor", body, new Vector3(0f, 0.64f, 0.15f), new Vector3(0.26f, 0.08f, 0.04f), 0.02f, red);
                    spin = new GameObject("Rotor").transform;
                    spin.SetParent(body, false);
                    spin.localPosition = new Vector3(0f, 0.86f, 0f);
                    Shapes.Rounded("Blade", spin, Vector3.zero, new Vector3(0.7f, 0.02f, 0.07f), 0.01f, dark);
                    break;
                default:
                    Shapes.Rounded("Head", body, new Vector3(0f, 0.63f, 0.02f), new Vector3(0.36f, 0.24f, 0.32f), 0.07f, metal);
                    Shapes.Rounded("Visor", body, new Vector3(0f, 0.64f, 0.18f), new Vector3(0.3f, 0.08f, 0.03f), 0.02f, red);
                    Shapes.Rounded("Antenna", body, new Vector3(0.1f, 0.82f, -0.04f), new Vector3(0.03f, 0.16f, 0.03f), 0.01f, dark);
                    Shapes.Primitive(PrimitiveType.Sphere, "Tip", body, new Vector3(0.1f, 0.91f, -0.04f), Vector3.one * 0.05f, red);
                    break;
            }
            foreach (float s in new[] { -1f, 1f })
            {
                var pauldron = Shapes.Rounded("Pauldron", body, new Vector3(s * 0.36f, 0.48f, 0f), new Vector3(0.2f, 0.12f, 0.3f), 0.05f, plate);
                pauldron.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 18f);
                if (variant == 3)
                    for (int k = 0; k < 3; k++)
                        Shapes.Primitive(PrimitiveType.Cylinder, "Spike", body, new Vector3(s * (0.38f + k * 0.02f), 0.58f, -0.1f + k * 0.1f), new Vector3(0.04f, 0.07f, 0.04f), metal)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, -s * 30f);
            }

            ArmL = Arm(body, -1f, metal, dark, plate, variant);
            ArmR = Arm(body, 1f, metal, dark, plate, variant);
        }

        private static Transform Arm(Transform body, float side, Material metal, Material dark, Material plate, int variant)
        {
            var pivot = new GameObject("Arm").transform;
            pivot.SetParent(body, false);
            pivot.localPosition = new Vector3(side * 0.4f, 0.42f, 0f);
            Shapes.Rounded("Upper", pivot, new Vector3(0f, -0.16f, 0f), new Vector3(0.12f, 0.3f, 0.14f), 0.04f, dark);
            if (variant == 3)
            {
                // A drill fist.
                Shapes.Primitive(PrimitiveType.Cylinder, "Drill", pivot, new Vector3(0f, -0.4f, 0.04f), new Vector3(0.16f, 0.1f, 0.16f), metal);
                Shapes.Primitive(PrimitiveType.Sphere, "Bit", pivot, new Vector3(0f, -0.52f, 0.04f), new Vector3(0.1f, 0.16f, 0.1f), plate);
                return pivot;
            }
            Shapes.Rounded("Fist", pivot, new Vector3(0f, -0.36f, 0.04f), new Vector3(0.2f, 0.18f, 0.22f), 0.05f, metal);
            foreach (float f in new[] { -1f, 1f })
                Shapes.Rounded("Clamp", pivot, new Vector3(f * 0.06f, -0.48f, 0.06f), new Vector3(0.05f, 0.1f, 0.08f), 0.02f, plate);
            return pivot;
        }

        /// <summary>0 = arms down, 1 = raised high for a slam.</summary>
        public void SetRaise(float amount) => raise = Mathf.Clamp01(amount);

        /// <summary>A hit: the core flares and the body jolts.</summary>
        public void Flash() => flash = 1f;

        private void Update()
        {
            float t = Time.time;
            ArmL.localRotation = Quaternion.Euler(-160f * raise + Mathf.Sin(t * 3f) * 6f, 0f, 0f);
            ArmR.localRotation = Quaternion.Euler(-160f * raise - Mathf.Sin(t * 3f) * 6f, 0f, 0f);
            if (flash > 0f) flash = Mathf.Max(0f, flash - Time.deltaTime * 3f);
            float hover = variant == 4 ? 0.12f + Mathf.Sin(t * 3f) * 0.04f : 0f;
            body.localPosition = new Vector3(Random.Range(-1f, 1f) * flash * 0.05f, 0.3f + hover + Mathf.Abs(Mathf.Sin(t * 6f)) * 0.01f, 0f);
            if (spin != null) spin.localRotation = Quaternion.Euler(0f, t * 900f, 0f);
            float glow = 1f + Mathf.Sin(t * 5f) * 0.25f + flash * 2f;
            MaterialFactory.SetColors(core, new Color(1f, 0.45f, 0.2f), new Color(2.4f, 0.8f, 0.25f) * glow);
        }
    }
}
