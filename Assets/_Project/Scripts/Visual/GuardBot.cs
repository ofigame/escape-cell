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
        private bool utopian;
        private Color coreColour = new Color(1f, 0.45f, 0.2f), coreGlow = new Color(2.4f, 0.8f, 0.25f);

        /// <param name="utopian">The perfect city's guards (level 101 on): pearl and gold, floating on a glowing ring;
        /// <paramref name="plates"/> is then the colour of their light.</param>
        public static GuardBot Build(Transform parent, Color? plates = null, int variant = 0, bool utopian = false)
        {
            var root = new GameObject("GuardBot").transform;
            root.SetParent(parent, false);
            var g = root.gameObject.AddComponent<GuardBot>();
            g.variant = ((variant % Variants) + Variants) % Variants;
            g.utopian = utopian;
            if (utopian) g.MakeUtopian(root, plates ?? new Color(0.4f, 0.9f, 1f));
            else g.Make(root, plates ?? new Color(0.55f, 0.2f, 0.18f));
            return g;
        }

        /// <summary>
        /// A guard of the perfect city: a smooth pearl body with gold trim floating over a glowing ring (no legs), an
        /// oval head behind a dark glass face with a light band for a visor, a halo, and slim arms ending in light
        /// blades. The light colour lights the visor, the core, the halo and the blades.
        /// </summary>
        private void MakeUtopian(Transform root, Color light)
        {
            var pearl = MaterialFactory.Create(new Color(0.94f, 0.95f, 0.97f), new Color(0.08f, 0.08f, 0.09f));
            var gold = MaterialFactory.Create(new Color(0.95f, 0.78f, 0.4f), new Color(0.35f, 0.24f, 0.06f));
            var glass = MaterialFactory.Create(new Color(0.16f, 0.2f, 0.28f), new Color(0.01f, 0.02f, 0.03f));
            var glow = MaterialFactory.Create(light, light * 2.2f);
            coreColour = light;
            coreGlow = light * 2.2f;
            core = MaterialFactory.Create(coreColour, coreGlow);

            // The hover ring and its light on the floor (it floats: no legs).
            Shapes.Primitive(PrimitiveType.Cylinder, "HoverRing", root, new Vector3(0f, 0.12f, 0f), new Vector3(0.46f, 0.03f, 0.46f), gold);
            Shapes.Primitive(PrimitiveType.Cylinder, "HoverLight", root, new Vector3(0f, 0.09f, 0f), new Vector3(0.36f, 0.02f, 0.36f), glow);
            Shapes.Primitive(PrimitiveType.Sphere, "Wash", root, new Vector3(0f, 0.02f, 0f), new Vector3(0.6f, 0.02f, 0.6f),
                MaterialFactory.CreateTransparent(new Color(light.r, light.g, light.b, 0.3f), light * 0.8f));

            body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localPosition = new Vector3(0f, 0.3f, 0f);
            Shapes.Primitive(PrimitiveType.Sphere, "Hips", body, new Vector3(0f, 0.02f, 0f), new Vector3(0.34f, 0.26f, 0.3f), pearl);
            Shapes.Rounded("Torso", body, new Vector3(0f, 0.3f, 0f), new Vector3(0.52f, 0.44f, 0.38f), 0.18f, pearl);
            Shapes.Rounded("Belt", body, new Vector3(0f, 0.11f, 0f), new Vector3(0.46f, 0.05f, 0.36f), 0.02f, gold);
            Shapes.Rounded("Collar", body, new Vector3(0f, 0.52f, 0f), new Vector3(0.4f, 0.05f, 0.3f), 0.02f, gold);
            Shapes.Primitive(PrimitiveType.Sphere, "Core", body, new Vector3(0f, 0.32f, 0.18f), new Vector3(0.14f, 0.14f, 0.06f), core);
            Shapes.Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.7f, 0f), new Vector3(0.3f, 0.34f, 0.3f), pearl);
            Shapes.Rounded("Face", body, new Vector3(0f, 0.7f, 0.12f), new Vector3(0.24f, 0.14f, 0.06f), 0.05f, glass);
            Shapes.Rounded("Visor", body, new Vector3(0f, 0.71f, 0.15f), new Vector3(0.2f, 0.035f, 0.02f), 0.012f, glow);
            var halo = Shapes.Primitive(PrimitiveType.Cylinder, "Halo", body, new Vector3(0f, 0.95f, -0.02f), new Vector3(0.3f, 0.008f, 0.3f), gold).transform;
            halo.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Shapes.Primitive(PrimitiveType.Cylinder, "HaloLight", halo, Vector3.zero, new Vector3(0.8f, 1.4f, 0.8f), glow);
            foreach (float s in new[] { -1f, 1f })
                Shapes.Primitive(PrimitiveType.Sphere, "Shoulder", body, new Vector3(s * 0.3f, 0.46f, 0f), new Vector3(0.18f, 0.14f, 0.2f), gold);

            ArmL = UtopianArm(-1f, pearl, gold, glow);
            ArmR = UtopianArm(1f, pearl, gold, glow);
        }

        private Transform UtopianArm(float side, Material pearl, Material gold, Material glow)
        {
            var pivot = new GameObject("Arm").transform;
            pivot.SetParent(body, false);
            pivot.localPosition = new Vector3(side * 0.34f, 0.44f, 0f);
            Shapes.Rounded("Upper", pivot, new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.28f, 0.11f), 0.05f, pearl);
            Shapes.Rounded("Cuff", pivot, new Vector3(0f, -0.3f, 0f), new Vector3(0.13f, 0.05f, 0.13f), 0.02f, gold);
            Shapes.Rounded("Blade", pivot, new Vector3(0f, -0.45f, 0.06f), new Vector3(0.03f, 0.26f, 0.09f), 0.012f, glow);
            return pivot;
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
            float hover = utopian ? 0.1f + Mathf.Sin(t * 2.4f) * 0.05f : variant == 4 ? 0.12f + Mathf.Sin(t * 3f) * 0.04f : 0f;
            body.localPosition = new Vector3(Random.Range(-1f, 1f) * flash * 0.05f, 0.3f + hover + Mathf.Abs(Mathf.Sin(t * 6f)) * 0.01f, 0f);
            if (spin != null) spin.localRotation = Quaternion.Euler(0f, t * 900f, 0f);
            float glow = 1f + Mathf.Sin(t * 5f) * 0.25f + flash * 2f;
            MaterialFactory.SetColors(core, coreColour, coreGlow * glow);
        }
    }
}
