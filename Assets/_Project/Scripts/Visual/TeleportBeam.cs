using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A teleport flash: a column of light that shoots up from a spot, with a bright core and rings climbing it, then
    /// narrows and fades in half a second.
    /// </summary>
    public class TeleportBeam : MonoBehaviour
    {
        private const float Life = 0.6f;
        private Material outer, core;
        private Transform column, inner;
        private Transform[] rings;
        private Color colour;
        private float t;

        public static void Create(Vector3 at, Color colour)
        {
            var go = new GameObject("TeleportBeam");
            go.transform.position = at;
            var b = go.AddComponent<TeleportBeam>();
            b.colour = colour;
            b.outer = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.45f), colour * 2.2f);
            b.core = MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.85f), Color.Lerp(colour, Color.white, 0.6f) * 3f);
            b.column = Shapes.Primitive(PrimitiveType.Cylinder, "Column", go.transform, new Vector3(0f, 2.5f, 0f), new Vector3(1.1f, 2.5f, 1.1f), b.outer).transform;
            b.inner = Shapes.Primitive(PrimitiveType.Cylinder, "Core", go.transform, new Vector3(0f, 2.5f, 0f), new Vector3(0.35f, 2.5f, 0.35f), b.core).transform;
            b.rings = new Transform[3];
            for (int i = 0; i < 3; i++)
                b.rings[i] = Shapes.Primitive(PrimitiveType.Cylinder, "Ring", go.transform, Vector3.zero, new Vector3(1.5f, 0.02f, 1.5f), b.outer).transform;
        }

        private void Update()
        {
            t += Time.deltaTime / Life;
            if (t >= 1f) { Destroy(gameObject); return; }
            float width = t < 0.2f ? Mathf.Lerp(0.2f, 1f, t / 0.2f) : Mathf.Lerp(1f, 0.05f, (t - 0.2f) / 0.8f);
            column.localScale = new Vector3(1.1f * width, 2.5f, 1.1f * width);
            inner.localScale = new Vector3(0.35f * width, 2.5f, 0.35f * width);
            for (int i = 0; i < rings.Length; i++)
            {
                float k = Mathf.Repeat(t * 2f + i / 3f, 1f);
                rings[i].localPosition = new Vector3(0f, k * 4f, 0f);
                rings[i].localScale = new Vector3(1.6f - k, 0.02f, 1.6f - k);
            }
            float a = 1f - t;
            MaterialFactory.SetColors(outer, new Color(colour.r, colour.g, colour.b, 0.45f * a), colour * 2.2f * a);
            MaterialFactory.SetColors(core, new Color(1f, 1f, 1f, 0.85f * a), Color.Lerp(colour, Color.white, 0.6f) * 3f * a);
        }
    }
}
