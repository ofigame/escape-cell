using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>A flat ring of force racing out over the floor from a slam, fading as it grows.</summary>
    public class Shockwave : MonoBehaviour
    {
        private const float Duration = 0.4f;
        private Transform ring, inner;
        private Material mat;
        private Color color;
        private float radius, t;

        public static void Create(Vector3 at, float radius, Color color)
        {
            var go = new GameObject("Shockwave");
            go.transform.position = at + Vector3.up * 0.04f;
            var s = go.AddComponent<Shockwave>();
            s.radius = radius;
            s.color = color;
            s.mat = MaterialFactory.CreateTransparent(new Color(color.r, color.g, color.b, 0.7f), color * 2.2f);
            // A ring: a bright disc with a darker floor-coloured disc over its middle.
            s.ring = Shapes.Primitive(PrimitiveType.Cylinder, "Ring", go.transform, Vector3.zero, Vector3.one * 0.1f, s.mat).transform;
            s.inner = Shapes.Primitive(PrimitiveType.Cylinder, "Hole", go.transform, Vector3.up * 0.005f, Vector3.one * 0.1f,
                MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.08f), Color.black)).transform;
        }

        private void Update()
        {
            t += Time.deltaTime / Duration;
            float k = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            float d = Mathf.Lerp(0.3f, radius * 2f, k);
            ring.localScale = new Vector3(d, 0.01f, d);
            inner.localScale = new Vector3(d * 0.82f, 0.012f, d * 0.82f);
            MaterialFactory.SetColors(mat, new Color(color.r, color.g, color.b, 0.75f * (1f - k)), color * (2.2f * (1f - k)));
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
