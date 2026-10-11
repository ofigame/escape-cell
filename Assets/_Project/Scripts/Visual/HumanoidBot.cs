using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// vanG's enforcer: a tall humanoid robot (about 1.6 units) in dark armour with glowing seams — jointed legs that
    /// stride, a broad chest plate with a burning core, heavy shoulder guards, long arms ending in big fists it lifts
    /// overhead before smashing the ground, and a narrow helmet with a slit visor. Facing +z.
    /// </summary>
    public class HumanoidBot : MonoBehaviour
    {
        private Transform hips, chest, armL, armR, legL, legR, head, aura;
        private Material core, seam, auraMat, eyeMat, flameMat;
        private float raise, flash, stride;
        private int menace;

        /// <param name="menace">0 the plain enforcer; 1-4 one of vanG's warlords (the very strong robots), more terrifying
        /// with every step: red eyes, horns and spiked shoulders, then arm blades and a burning back, a red aura, more bulk.</param>
        public static HumanoidBot Build(Transform parent, Color accent, int menace = 0)
        {
            var root = new GameObject("HumanoidBot").transform;
            root.SetParent(parent, false);
            var h = root.gameObject.AddComponent<HumanoidBot>();
            h.menace = menace;
            h.Make(root, menace > 0 ? new Color(1f, 0.15f, 0.1f) : accent);
            if (menace > 0) h.Warlord();
            return h;
        }

        /// <summary>The warlord's extra armour and glow (see <see cref="Build"/>).</summary>
        private void Warlord()
        {
            var black = MaterialFactory.CreateSurface(new Color(0.08f, 0.07f, 0.09f), Color.black, 0.55f, 0.6f);
            var steel = MaterialFactory.CreateSurface(new Color(0.42f, 0.4f, 0.44f), Color.black, 0.7f, 0.85f);
            eyeMat = MaterialFactory.Create(new Color(1f, 0.1f, 0.05f), new Color(4f, 0.3f, 0.1f));
            flameMat = MaterialFactory.Create(new Color(1f, 0.5f, 0.1f), new Color(3.5f, 1.2f, 0.2f));
            // Red eyes in the visor, horns curling up off the helmet.
            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Primitive(PrimitiveType.Sphere, "Eye", head, new Vector3(s * 0.055f, 0.1f, 0.135f), Vector3.one * 0.055f, eyeMat);
                var horn = new GameObject("Horn").transform;
                horn.SetParent(head, false);
                horn.localPosition = new Vector3(s * 0.11f, 0.17f, -0.01f);
                horn.localRotation = Quaternion.Euler(-15f, 0f, -s * 35f);
                Shapes.Rounded("HornBase", horn, new Vector3(0f, 0.06f, 0f), new Vector3(0.06f, 0.14f, 0.06f), 0.025f, black);
                Shapes.Rounded("HornTip", horn, new Vector3(-s * 0.03f, 0.17f, 0f), new Vector3(0.035f, 0.12f, 0.035f), 0.015f, steel)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, s * 30f);
                // Spiked pauldrons.
                for (int i = 0; i < 3; i++)
                    Shapes.Rounded("Spike", chest, new Vector3(s * (0.3f + i * 0.07f), 0.66f, -0.06f + i * 0.05f), new Vector3(0.04f, 0.16f, 0.04f), 0.015f, steel)
                        .transform.localRotation = Quaternion.Euler(-10f, 0f, -s * (20f + i * 12f));
            }
            Shapes.Rounded("Jaw", head, new Vector3(0f, 0.0f, 0.12f), new Vector3(0.2f, 0.06f, 0.06f), 0.02f, steel);
            if (menace >= 2)
            {
                // Blades along the forearms, a chest grille glowing red.
                foreach (var arm in new[] { armL, armR })
                    Shapes.Rounded("Blade", arm, new Vector3(0f, -0.5f, 0.13f), new Vector3(0.03f, 0.42f, 0.09f), 0.01f, steel)
                        .transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
                for (int i = 0; i < 3; i++)
                    Shapes.Rounded("Grille", chest, new Vector3(0f, 0.3f + i * 0.06f, 0.185f), new Vector3(0.34f - i * 0.05f, 0.02f, 0.02f), 0.008f, eyeMat);
            }
            if (menace >= 3)
            {
                // Exhausts on the back that breathe fire.
                foreach (float s in new[] { -1f, 1f })
                {
                    Shapes.Primitive(PrimitiveType.Cylinder, "Exhaust", chest, new Vector3(s * 0.16f, 0.62f, -0.2f), new Vector3(0.1f, 0.14f, 0.1f), black);
                    Shapes.Primitive(PrimitiveType.Sphere, "Flame", chest, new Vector3(s * 0.16f, 0.8f, -0.2f), new Vector3(0.09f, 0.14f, 0.09f), flameMat);
                }
                Shapes.Rounded("BackPlate", chest, new Vector3(0f, 0.42f, -0.19f), new Vector3(0.5f, 0.34f, 0.08f), 0.05f, black);
            }
            // A red aura round its feet (pulses), and more bulk with every step of menace.
            auraMat = MaterialFactory.CreateTransparent(new Color(1f, 0.15f, 0.1f, 0.3f), new Color(2.2f, 0.3f, 0.15f));
            aura = Shapes.Primitive(PrimitiveType.Cylinder, "Aura", transform, new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.005f, 1.1f), auraMat).transform;
            chest.localScale = Vector3.one * (1.08f + 0.05f * menace);
        }

        private void Make(Transform root, Color accent)
        {
            var armour = MaterialFactory.Create(new Color(0.18f, 0.19f, 0.24f), new Color(0.01f, 0.01f, 0.02f));
            var plate = MaterialFactory.Create(new Color(0.34f, 0.35f, 0.42f), Color.black);
            var joint = MaterialFactory.Create(new Color(0.08f, 0.08f, 0.1f), Color.black);
            seam = MaterialFactory.Create(accent, accent * 2.2f);
            core = MaterialFactory.Create(new Color(1f, 0.4f, 0.2f), new Color(2.8f, 0.7f, 0.2f));

            hips = new GameObject("Hips").transform;
            hips.SetParent(root, false);
            hips.localPosition = new Vector3(0f, 0.72f, 0f);
            Shapes.Rounded("Pelvis", hips, Vector3.zero, new Vector3(0.42f, 0.16f, 0.26f), 0.06f, armour);
            legL = Leg(hips, -1f, armour, plate, joint);
            legR = Leg(hips, 1f, armour, plate, joint);

            chest = new GameObject("Chest").transform;
            chest.SetParent(hips, false);
            chest.localPosition = new Vector3(0f, 0.1f, 0f);
            Shapes.Rounded("Abdomen", chest, new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.2f, 0.22f), 0.06f, joint);
            Shapes.Rounded("Torso", chest, new Vector3(0f, 0.38f, 0f), new Vector3(0.6f, 0.38f, 0.34f), 0.1f, armour);
            Shapes.Rounded("ChestPlate", chest, new Vector3(0f, 0.4f, 0.13f), new Vector3(0.46f, 0.28f, 0.1f), 0.05f, plate);
            Shapes.Primitive(PrimitiveType.Sphere, "Core", chest, new Vector3(0f, 0.4f, 0.19f), Vector3.one * 0.12f, core);
            foreach (float y in new[] { 0.3f, 0.5f })
                Shapes.Rounded("Seam", chest, new Vector3(0f, y, 0.175f), new Vector3(0.5f, 0.015f, 0.01f), 0.005f, seam);
            foreach (float s in new[] { -1f, 1f })
                Shapes.Rounded("Shoulder", chest, new Vector3(s * 0.37f, 0.54f, 0f), new Vector3(0.24f, 0.16f, 0.3f), 0.07f, plate)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -s * 15f);

            head = new GameObject("Head").transform;
            head.SetParent(chest, false);
            head.localPosition = new Vector3(0f, 0.68f, 0f);
            Shapes.Rounded("Neck", head, new Vector3(0f, -0.06f, 0f), new Vector3(0.12f, 0.08f, 0.12f), 0.03f, joint);
            Shapes.Rounded("Helmet", head, new Vector3(0f, 0.08f, 0f), new Vector3(0.24f, 0.24f, 0.26f), 0.08f, armour);
            Shapes.Rounded("Visor", head, new Vector3(0f, 0.09f, 0.125f), new Vector3(0.18f, 0.035f, 0.02f), 0.01f, core);
            Shapes.Rounded("Crest", head, new Vector3(0f, 0.22f, -0.02f), new Vector3(0.04f, 0.06f, 0.2f), 0.02f, seam);

            armL = Arm(chest, -1f, armour, plate, joint);
            armR = Arm(chest, 1f, armour, plate, joint);
        }

        private static Transform Leg(Transform hips, float side, Material armour, Material plate, Material joint)
        {
            var pivot = new GameObject("Leg").transform;
            pivot.SetParent(hips, false);
            pivot.localPosition = new Vector3(side * 0.13f, -0.04f, 0f);
            Shapes.Rounded("Thigh", pivot, new Vector3(0f, -0.17f, 0f), new Vector3(0.15f, 0.3f, 0.17f), 0.05f, armour);
            Shapes.Primitive(PrimitiveType.Sphere, "Knee", pivot, new Vector3(0f, -0.34f, 0.03f), Vector3.one * 0.12f, joint);
            Shapes.Rounded("Shin", pivot, new Vector3(0f, -0.5f, 0f), new Vector3(0.14f, 0.28f, 0.16f), 0.05f, plate);
            Shapes.Rounded("Foot", pivot, new Vector3(0f, -0.66f, 0.05f), new Vector3(0.17f, 0.08f, 0.28f), 0.03f, joint);
            return pivot;
        }

        private static Transform Arm(Transform chest, float side, Material armour, Material plate, Material joint)
        {
            var pivot = new GameObject("Arm").transform;
            pivot.SetParent(chest, false);
            pivot.localPosition = new Vector3(side * 0.4f, 0.5f, 0f);
            Shapes.Rounded("UpperArm", pivot, new Vector3(0f, -0.16f, 0f), new Vector3(0.13f, 0.28f, 0.14f), 0.04f, armour);
            Shapes.Primitive(PrimitiveType.Sphere, "Elbow", pivot, new Vector3(0f, -0.32f, 0f), Vector3.one * 0.11f, joint);
            Shapes.Rounded("Forearm", pivot, new Vector3(0f, -0.46f, 0.02f), new Vector3(0.15f, 0.26f, 0.16f), 0.05f, plate);
            Shapes.Rounded("Fist", pivot, new Vector3(0f, -0.64f, 0.04f), new Vector3(0.22f, 0.2f, 0.22f), 0.06f, armour);
            return pivot;
        }

        /// <summary>0 = arms down, 1 = both fists high overhead.</summary>
        public void SetRaise(float amount) => raise = Mathf.Clamp01(amount);

        public void Flash() => flash = 1f;

        /// <summary>Strides for a moment (call when it steps).</summary>
        public void Step() => stride = 1f;

        private void Update()
        {
            float t = Time.time;
            stride = Mathf.Max(0f, stride - Time.deltaTime * 2.5f);
            float swing = Mathf.Sin(t * 9f) * 28f * stride;
            legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            armL.localRotation = Quaternion.Euler(-175f * raise - swing * 0.6f, 0f, 8f);
            armR.localRotation = Quaternion.Euler(-175f * raise + swing * 0.6f, 0f, -8f);
            if (flash > 0f) flash = Mathf.Max(0f, flash - Time.deltaTime * 3f);
            chest.localRotation = Quaternion.Euler(raise * -12f, Mathf.Sin(t * 1.5f) * 4f, 0f);
            hips.localPosition = new Vector3(Random.Range(-1f, 1f) * flash * 0.05f, 0.72f + Mathf.Abs(Mathf.Sin(t * 9f)) * 0.03f * stride, 0f);
            float glow = 1f + Mathf.Sin(t * 4f) * 0.3f + flash * 2f + raise;
            MaterialFactory.SetColors(core, new Color(1f, 0.4f, 0.2f), new Color(2.8f, 0.7f, 0.2f) * glow);
            if (aura != null)
            {
                float pulse = 0.75f + Mathf.Sin(t * 5f) * 0.25f;
                aura.localScale = new Vector3(1.1f + pulse * 0.25f, 0.005f, 1.1f + pulse * 0.25f);
                MaterialFactory.SetColors(auraMat, new Color(1f, 0.15f, 0.1f, 0.22f + 0.15f * pulse), new Color(2.2f, 0.3f, 0.15f) * pulse);
                MaterialFactory.SetColors(eyeMat, new Color(1f, 0.1f, 0.05f), new Color(4f, 0.3f, 0.1f) * (glow * 0.8f));
                if (flameMat != null) MaterialFactory.SetColors(flameMat, new Color(1f, 0.5f, 0.1f), new Color(3.5f, 1.2f, 0.2f) * (0.7f + Mathf.PerlinNoise(t * 9f, 0f) * 0.8f));
            }
        }
    }
}
