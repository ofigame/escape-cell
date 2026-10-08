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
        private Transform hips, chest, armL, armR, legL, legR, head;
        private Material core, seam;
        private float raise, flash, stride;

        public static HumanoidBot Build(Transform parent, Color accent)
        {
            var root = new GameObject("HumanoidBot").transform;
            root.SetParent(parent, false);
            var h = root.gameObject.AddComponent<HumanoidBot>();
            h.Make(root, accent);
            return h;
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
        }
    }
}
