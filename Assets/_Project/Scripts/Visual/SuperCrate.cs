using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The super-power crate the helicopter brings: a golden box with a glowing star, waiting on its tile inside a
    /// column of light. It blinks in its last seconds (<see cref="Leaving"/>) before it fades away.
    /// </summary>
    public class SuperCrate : MonoBehaviour
    {
        public static readonly Color Gold = new Color(1f, 0.8f, 0.3f);
        public static readonly Color GoldGlow = new Color(2.6f, 1.8f, 0.4f);

        private Transform model;
        private Material beamMat;
        private float time, pop;

        public bool Leaving { get; set; }

        public static SuperCrate Create(Vector3 position)
        {
            var go = new GameObject("SuperCrate");
            go.transform.position = position;
            var c = go.AddComponent<SuperCrate>();
            c.model = BuildModel(go.transform);
            c.beamMat = MaterialFactory.CreateTransparent(new Color(Gold.r, Gold.g, Gold.b, 0.3f), GoldGlow);
            Shapes.Primitive(PrimitiveType.Cylinder, "Beam", go.transform, new Vector3(0f, 2.4f, 0f), new Vector3(0.5f, 2.4f, 0.5f), c.beamMat);
            Shapes.Primitive(PrimitiveType.Cylinder, "Disc", go.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.9f, 0.004f, 0.9f),
                MaterialFactory.CreateTransparent(new Color(Gold.r, Gold.g, Gold.b, 0.45f), GoldGlow * 0.6f));
            return c;
        }

        /// <summary>The crate itself (about 0.6 units), shared with the helicopter that carries it.</summary>
        public static Transform BuildModel(Transform parent)
        {
            var root = new GameObject("Crate").transform;
            root.SetParent(parent, false);
            var gold = MaterialFactory.Create(Gold, GoldGlow * 0.35f);
            var band = MaterialFactory.Create(new Color(0.95f, 0.95f, 1f), new Color(0.4f, 0.4f, 0.5f));
            var star = MaterialFactory.Create(Color.white, GoldGlow);
            Shapes.Rounded("Box", root, new Vector3(0f, 0.32f, 0f), new Vector3(0.62f, 0.62f, 0.62f), 0.1f, gold);
            Shapes.Rounded("Band", root, new Vector3(0f, 0.32f, 0f), new Vector3(0.66f, 0.12f, 0.66f), 0.04f, band);
            Shapes.Rounded("Band", root, new Vector3(0f, 0.32f, 0f), new Vector3(0.12f, 0.66f, 0.66f), 0.04f, band);
            // A star on the front and on top.
            foreach (var (pos, rot) in new[] { (new Vector3(0f, 0.32f, -0.33f), Quaternion.identity), (new Vector3(0f, 0.64f, 0f), Quaternion.Euler(90f, 0f, 0f)) })
                for (int i = 0; i < 5; i++)
                {
                    var arm = Shapes.Rounded("Star", root, pos, new Vector3(0.07f, 0.24f, 0.03f), 0.02f, star).transform;
                    arm.localRotation = rot * Quaternion.Euler(0f, 0f, i * 72f) * Quaternion.Euler(0f, 0f, 0f);
                    arm.localPosition = pos + arm.localRotation * new Vector3(0f, 0.08f, 0f);
                }
            return root;
        }

        private void Update()
        {
            time += Time.deltaTime;
            pop = Mathf.Min(1f, pop + Time.deltaTime * 4f);
            float s = pop < 1f ? 1f + Mathf.Sin(pop * Mathf.PI) * 0.3f : 1f;
            if (Leaving) s *= 0.8f + 0.2f * Mathf.Sin(time * 18f);
            model.localScale = Vector3.one * s;
            model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(time * 3f)) * 0.08f, 0f);
            model.localRotation = Quaternion.Euler(0f, time * 50f, 0f);
            MaterialFactory.SetColors(beamMat, new Color(Gold.r, Gold.g, Gold.b, 0.22f + Mathf.Sin(time * 5f) * 0.08f), GoldGlow);
        }
    }
}
