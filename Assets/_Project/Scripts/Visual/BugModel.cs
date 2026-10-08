using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A floor bug: a round shiny beetle on six scuttling legs, two feelers and a glowing back pattern in its
    /// world's colour. Scuttles while it moves, flattens when squashed. About 0.45 units long, facing +z.
    /// </summary>
    public class BugModel : MonoBehaviour
    {
        private readonly Transform[] legs = new Transform[6];
        private Transform body;
        private float moving, squash = -1f;

        public static BugModel Build(Transform parent, Color back)
        {
            var root = new GameObject("Bug").transform;
            root.SetParent(parent, false);
            var b = root.gameObject.AddComponent<BugModel>();
            b.Make(root, back);
            return b;
        }

        private void Make(Transform root, Color back)
        {
            var shell = MaterialFactory.Create(new Color(0.16f, 0.14f, 0.18f), new Color(0.02f, 0.02f, 0.03f));
            var glow = MaterialFactory.Create(back, back * 1.4f);
            var leg = MaterialFactory.Create(new Color(0.1f, 0.09f, 0.1f), Color.black);
            body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localPosition = new Vector3(0f, 0.11f, 0f);
            Shapes.Primitive(PrimitiveType.Sphere, "Shell", body, Vector3.zero, new Vector3(0.34f, 0.16f, 0.42f), shell);
            Shapes.Primitive(PrimitiveType.Sphere, "Back", body, new Vector3(0f, 0.05f, -0.02f), new Vector3(0.22f, 0.08f, 0.3f), glow);
            Shapes.Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0f, 0.22f), new Vector3(0.16f, 0.11f, 0.13f), shell);
            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(s * 0.05f, 0.03f, 0.28f), Vector3.one * 0.04f, glow);
                var feeler = Shapes.Primitive(PrimitiveType.Cylinder, "Feeler", body, new Vector3(s * 0.05f, 0.06f, 0.33f), new Vector3(0.012f, 0.06f, 0.012f), leg);
                feeler.transform.localRotation = Quaternion.Euler(60f, s * 25f, 0f);
            }
            for (int i = 0; i < 6; i++)
            {
                float side = i < 3 ? -1f : 1f;
                float z = -0.1f + (i % 3) * 0.1f;
                var pivot = new GameObject("Leg").transform;
                pivot.SetParent(body, false);
                pivot.localPosition = new Vector3(side * 0.14f, -0.02f, z);
                var l = Shapes.Primitive(PrimitiveType.Cylinder, "Leg", pivot, new Vector3(side * 0.06f, -0.04f, 0f), new Vector3(0.018f, 0.07f, 0.018f), leg);
                l.transform.localRotation = Quaternion.Euler(0f, 0f, side * 55f);
                legs[i] = pivot;
            }
        }

        /// <summary>Scuttling (legs going) for a moment.</summary>
        public void Scuttle() => moving = 0.45f;

        /// <summary>Flattened under a foot or a hammer.</summary>
        public void Squash() => squash = 0f;

        private void Update()
        {
            float t = Time.time * 30f;
            if (squash >= 0f)
            {
                squash += Time.deltaTime;
                body.localScale = new Vector3(1f + squash * 2f, Mathf.Max(0.15f, 1f - squash * 6f), 1f + squash * 2f);
                return;
            }
            moving = Mathf.Max(0f, moving - Time.deltaTime);
            float amp = moving > 0f ? 25f : 3f;
            for (int i = 0; i < 6; i++)
                legs[i].localRotation = Quaternion.Euler(Mathf.Sin(t + i * 2.1f) * amp, 0f, 0f);
            body.localPosition = new Vector3(0f, 0.11f + Mathf.Abs(Mathf.Sin(t * 0.5f)) * (moving > 0f ? 0.015f : 0.003f), 0f);
        }
    }
}
