using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Princess Mira: a small light-giving robot princess with a pearl body, a soft lilac skirt, a glowing crown and
    /// big friendly eyes. vanG locks her in electrified cages; Cell breaks them open. Built facing +z, standing on y = 0,
    /// about 0.75 units tall.
    /// </summary>
    public static class PrincessMira
    {
        public static readonly Color Light = new Color(1f, 0.85f, 0.95f);

        public static Transform Build(Transform parent, float scale = 1f)
        {
            var root = new GameObject("Mira").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;

            var pearl = MaterialFactory.Create(new Color(0.97f, 0.95f, 1f), new Color(0.15f, 0.12f, 0.18f));
            var lilac = MaterialFactory.Create(new Color(0.78f, 0.6f, 0.98f), new Color(0.15f, 0.08f, 0.25f));
            var gold = MaterialFactory.Create(new Color(1f, 0.82f, 0.35f), new Color(1.6f, 1.1f, 0.3f));
            var visor = MaterialFactory.Create(new Color(0.2f, 0.16f, 0.32f), Color.black);
            var eye = MaterialFactory.Create(new Color(1f, 0.8f, 0.95f), new Color(2.4f, 1.4f, 2.2f));
            var gem = MaterialFactory.Create(new Color(0.55f, 0.9f, 1f), new Color(0.8f, 2f, 2.6f));

            // Skirt and body.
            Shapes.Primitive(PrimitiveType.Cylinder, "Skirt", root, new Vector3(0f, 0.12f, 0f), new Vector3(0.42f, 0.12f, 0.42f), lilac);
            Shapes.Primitive(PrimitiveType.Cylinder, "Hem", root, new Vector3(0f, 0.02f, 0f), new Vector3(0.48f, 0.02f, 0.48f), gold);
            Shapes.Rounded("Body", root, new Vector3(0f, 0.3f, 0f), new Vector3(0.24f, 0.16f, 0.2f), 0.06f, pearl);
            Shapes.Rounded("Gem", root, new Vector3(0f, 0.3f, 0.105f), new Vector3(0.06f, 0.06f, 0.02f), 0.02f, gem);
            foreach (float s in new[] { -1f, 1f })
                Shapes.Rounded("Arm", root, new Vector3(s * 0.15f, 0.29f, 0f), new Vector3(0.05f, 0.12f, 0.06f), 0.02f, pearl)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, s * 15f);

            // Head with a visor face and two big eyes.
            Shapes.Rounded("Head", root, new Vector3(0f, 0.52f, 0f), new Vector3(0.36f, 0.3f, 0.32f), 0.12f, pearl);
            Shapes.Rounded("Visor", root, new Vector3(0f, 0.52f, 0.155f), new Vector3(0.26f, 0.16f, 0.03f), 0.05f, visor);
            foreach (float s in new[] { -0.06f, 0.06f })
                Shapes.Rounded("Eye", root, new Vector3(s, 0.53f, 0.172f), new Vector3(0.055f, 0.07f, 0.01f), 0.02f, eye);

            // The crown: a gold band with three points and a light at the front.
            Shapes.Primitive(PrimitiveType.Cylinder, "Band", root, new Vector3(0f, 0.69f, 0f), new Vector3(0.22f, 0.025f, 0.22f), gold);
            for (int i = 0; i < 3; i++)
            {
                float a = (i - 1) * 0.6f;
                Shapes.Rounded("Point", root, new Vector3(Mathf.Sin(a) * 0.09f, 0.74f, Mathf.Cos(a) * 0.09f), new Vector3(0.04f, 0.08f, 0.04f), 0.015f, gold);
            }
            Shapes.Primitive(PrimitiveType.Sphere, "Light", root, new Vector3(0f, 0.75f, 0.1f), Vector3.one * 0.05f, gem);
            Shapes.Primitive(PrimitiveType.Sphere, "Glow", root, new Vector3(0f, 0.4f, 0f), Vector3.one * 0.9f,
                MaterialFactory.CreateTransparent(new Color(1f, 0.85f, 0.95f, 0.12f), new Color(1.2f, 0.9f, 1.2f)));
            return root;
        }

        /// <summary>
        /// vanG's electrified cage around Mira: a dark base, gold bars, a domed top and crackling blue rings
        /// (about 1 unit wide, 1.2 tall, local to <paramref name="parent"/>).
        /// </summary>
        public static void BuildCage(Transform parent, Material bars)
        {
            var dark = MaterialFactory.Create(new Color(0.22f, 0.22f, 0.3f), Color.black);
            var shock = MaterialFactory.Create(new Color(0.5f, 0.85f, 1f), new Color(1f, 2.2f, 3f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Base", parent, new Vector3(0f, 0.05f, 0f), new Vector3(1f, 0.05f, 1f), dark);
            Shapes.Primitive(PrimitiveType.Cylinder, "Top", parent, new Vector3(0f, 1.1f, 0f), new Vector3(1f, 0.04f, 1f), dark);
            Shapes.Primitive(PrimitiveType.Sphere, "Dome", parent, new Vector3(0f, 1.12f, 0f), new Vector3(0.95f, 0.4f, 0.95f), bars);
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Shapes.Rounded("Bar", parent, new Vector3(Mathf.Cos(a) * 0.47f, 0.58f, Mathf.Sin(a) * 0.47f), new Vector3(0.045f, 1.04f, 0.045f), 0.02f, bars);
            }
            foreach (float y in new[] { 0.35f, 0.8f })
                Shapes.Primitive(PrimitiveType.Cylinder, "Shock", parent, new Vector3(0f, y, 0f), new Vector3(1.02f, 0.012f, 1.02f), shock);
        }
    }
}
