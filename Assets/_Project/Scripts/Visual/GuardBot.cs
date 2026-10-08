using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// vanG's guard robot (the forest's enemies): a stocky gunmetal body on two short legs, armour plates on the
    /// shoulders, a red visor and a glowing core in its chest, and two heavy clamp arms it raises before a slam.
    /// About 1.1 units tall, facing +z.
    /// </summary>
    public class GuardBot : MonoBehaviour
    {
        public Transform ArmL { get; private set; }
        public Transform ArmR { get; private set; }
        private Transform body;
        private Material core;
        private float raise, flash;

        public static GuardBot Build(Transform parent)
        {
            var root = new GameObject("GuardBot").transform;
            root.SetParent(parent, false);
            var g = root.gameObject.AddComponent<GuardBot>();
            g.Make(root);
            return g;
        }

        private void Make(Transform root)
        {
            var metal = MaterialFactory.Create(new Color(0.3f, 0.32f, 0.38f), Color.black);
            var dark = MaterialFactory.Create(new Color(0.14f, 0.15f, 0.19f), Color.black);
            var plate = MaterialFactory.Create(new Color(0.55f, 0.2f, 0.18f), new Color(0.08f, 0.02f, 0.02f));
            var red = MaterialFactory.Create(new Color(1f, 0.25f, 0.18f), new Color(2.6f, 0.4f, 0.2f));
            core = MaterialFactory.Create(new Color(1f, 0.45f, 0.2f), new Color(2.4f, 0.8f, 0.25f));

            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Rounded("Leg", root, new Vector3(s * 0.15f, 0.14f, 0f), new Vector3(0.14f, 0.28f, 0.16f), 0.04f, dark);
                Shapes.Rounded("Foot", root, new Vector3(s * 0.15f, 0.04f, 0.04f), new Vector3(0.18f, 0.08f, 0.26f), 0.03f, metal);
            }
            body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localPosition = new Vector3(0f, 0.3f, 0f);
            Shapes.Rounded("Torso", body, new Vector3(0f, 0.27f, 0f), new Vector3(0.62f, 0.5f, 0.44f), 0.1f, metal);
            Shapes.Rounded("Belly", body, new Vector3(0f, 0.08f, 0.02f), new Vector3(0.5f, 0.14f, 0.38f), 0.05f, dark);
            Shapes.Primitive(PrimitiveType.Sphere, "Core", body, new Vector3(0f, 0.28f, 0.22f), Vector3.one * 0.13f, core);
            Shapes.Rounded("Head", body, new Vector3(0f, 0.63f, 0.02f), new Vector3(0.36f, 0.24f, 0.32f), 0.07f, metal);
            Shapes.Rounded("Visor", body, new Vector3(0f, 0.64f, 0.18f), new Vector3(0.3f, 0.08f, 0.03f), 0.02f, red);
            Shapes.Rounded("Antenna", body, new Vector3(0.1f, 0.82f, -0.04f), new Vector3(0.03f, 0.16f, 0.03f), 0.01f, dark);
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", body, new Vector3(0.1f, 0.91f, -0.04f), Vector3.one * 0.05f, red);
            foreach (float s in new[] { -1f, 1f })
                Shapes.Rounded("Pauldron", body, new Vector3(s * 0.36f, 0.48f, 0f), new Vector3(0.2f, 0.12f, 0.3f), 0.05f, plate)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -s * 18f);

            ArmL = Arm(body, -1f, metal, dark, plate);
            ArmR = Arm(body, 1f, metal, dark, plate);
        }

        private static Transform Arm(Transform body, float side, Material metal, Material dark, Material plate)
        {
            var pivot = new GameObject("Arm").transform;
            pivot.SetParent(body, false);
            pivot.localPosition = new Vector3(side * 0.4f, 0.42f, 0f);
            Shapes.Rounded("Upper", pivot, new Vector3(0f, -0.16f, 0f), new Vector3(0.12f, 0.3f, 0.14f), 0.04f, dark);
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
            body.localPosition = new Vector3(Random.Range(-1f, 1f) * flash * 0.05f, 0.3f + Mathf.Abs(Mathf.Sin(t * 6f)) * 0.01f, 0f);
            float glow = 1f + Mathf.Sin(t * 5f) * 0.25f + flash * 2f;
            MaterialFactory.SetColors(core, new Color(1f, 0.45f, 0.2f), new Color(2.4f, 0.8f, 0.25f) * glow);
        }
    }
}
