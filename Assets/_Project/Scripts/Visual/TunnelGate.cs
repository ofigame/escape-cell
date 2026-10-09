using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The way on to the next floor's tunnel, standing from the start in the middle of the floor's north edge: an arch
    /// in the world's colour with a barred door that glows red while it is shut. The monster waits in front of it;
    /// when the monster falls the bars sink into the floor and the arch turns green.
    /// </summary>
    public class TunnelGate : MonoBehaviour
    {
        private Transform bars;
        private Material glow;
        private float openT = -1f;

        public bool IsOpen => openT >= 0f;

        public static TunnelGate Create(Vector3 tile, Color accent)
        {
            var go = new GameObject("TunnelGate");
            go.transform.position = tile;
            var g = go.AddComponent<TunnelGate>();
            g.Build(accent);
            return g;
        }

        private void Build(Color accent)
        {
            var stone = MaterialFactory.Create(Color.Lerp(accent, new Color(0.25f, 0.24f, 0.32f), 0.6f), Color.black);
            glow = MaterialFactory.Create(new Color(1f, 0.35f, 0.35f), new Color(2.4f, 0.4f, 0.4f));
            var trim = MaterialFactory.Create(accent, accent * 1.2f);
            // The arch spans the tile on its far side (+z), facing the floor.
            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Rounded("Post", transform, new Vector3(s * 0.52f, 0.85f, 0.42f), new Vector3(0.22f, 1.7f, 0.26f), 0.06f, stone);
                Shapes.Rounded("PostTrim", transform, new Vector3(s * 0.52f, 0.85f, 0.28f), new Vector3(0.08f, 1.5f, 0.03f), 0.02f, trim);
            }
            Shapes.Rounded("Lintel", transform, new Vector3(0f, 1.78f, 0.42f), new Vector3(1.4f, 0.26f, 0.3f), 0.08f, stone);
            Shapes.Rounded("Sign", transform, new Vector3(0f, 1.78f, 0.26f), new Vector3(0.7f, 0.12f, 0.03f), 0.03f, glow);
            // The tunnel itself, in sight from the start: a big portal just beyond the gate with a dark mouth, lamps on
            // its frame and a short roofed stretch running on north (open at the back: the road runs straight through).
            var dark = MaterialFactory.Create(new Color(0.04f, 0.035f, 0.07f), Color.black);
            var lamp = MaterialFactory.Create(new Color(1f, 0.85f, 0.5f), new Color(2.4f, 1.8f, 0.7f));
            const float width = 3.6f, tall = 2.9f, front = 1.1f, depth = 3.2f;
            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Rounded("PortalSide", transform, new Vector3(s * (width * 0.5f + 0.25f), tall * 0.5f, front + depth * 0.5f), new Vector3(0.5f, tall, depth), 0.08f, stone);
                Shapes.Rounded("PortalTrim", transform, new Vector3(s * (width * 0.5f + 0.25f), tall * 0.5f, front - 0.02f), new Vector3(0.56f, tall + 0.1f, 0.12f), 0.05f, trim);
                Shapes.Primitive(PrimitiveType.Sphere, "Lamp", transform, new Vector3(s * (width * 0.5f + 0.25f), tall + 0.25f, front), Vector3.one * 0.3f, lamp);
            }
            Shapes.Rounded("PortalRoof", transform, new Vector3(0f, tall + 0.2f, front + depth * 0.5f), new Vector3(width + 1f, 0.4f, depth), 0.1f, stone);
            Shapes.Rounded("PortalCrown", transform, new Vector3(0f, tall + 0.05f, front - 0.04f), new Vector3(width + 1.1f, 0.22f, 0.14f), 0.05f, trim);
            Shapes.Rounded("Track", transform, new Vector3(0f, 0.02f, front + depth * 0.5f), new Vector3(width - 0.2f, 0.04f, depth), 0.02f, dark);
            bars = new GameObject("Bars").transform;
            bars.SetParent(transform, false);
            for (int i = 0; i < 5; i++)
                Shapes.Rounded("Bar", bars, new Vector3(-0.36f + i * 0.18f, 0.82f, 0.42f), new Vector3(0.06f, 1.55f, 0.06f), 0.025f, glow);
            Shapes.Rounded("Rail", bars, new Vector3(0f, 1.2f, 0.42f), new Vector3(0.86f, 0.06f, 0.06f), 0.025f, glow);
        }

        /// <summary>The monster is down: the bars sink away and the light turns green.</summary>
        public void Open()
        {
            if (openT >= 0f) return;
            openT = 0f;
            MaterialFactory.SetColors(glow, new Color(0.45f, 1f, 0.6f), new Color(0.5f, 2.4f, 0.9f));
        }

        private void Update()
        {
            if (openT < 0f || openT >= 1f) return;
            openT = Mathf.Min(1f, openT + Time.deltaTime / 0.9f);
            float k = openT * openT * (3f - 2f * openT);
            bars.localPosition = new Vector3(0f, -1.7f * k, 0f);
            if (openT >= 1f) bars.gameObject.SetActive(false);
        }
    }
}
