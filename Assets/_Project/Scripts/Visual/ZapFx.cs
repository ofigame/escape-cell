using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Fifi's zap: a jagged bolt of lightning from Fifi to its target (a bright core in a soft glow, re-drawn every few
    /// frames so it crackles), a spark at each end, flickering out in a moment.
    /// </summary>
    public class ZapFx : MonoBehaviour
    {
        private const float Life = 0.32f;
        private const int Segments = 6;
        private Material core, glow;
        private Color colour;
        private Vector3 from, to;
        private Transform[] cores, glows;
        private Transform sparkA, sparkB;
        private float t, redraw;

        public static void Create(Vector3 from, Vector3 to, Color colour)
        {
            var go = new GameObject("Zap");
            var z = go.AddComponent<ZapFx>();
            z.colour = colour;
            z.from = from;
            z.to = to;
            z.core = MaterialFactory.Create(Color.Lerp(colour, Color.white, 0.6f), colour * 4f);
            z.glow = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.45f), colour * 2.4f);
            z.cores = new Transform[Segments];
            z.glows = new Transform[Segments];
            for (int i = 0; i < Segments; i++)
            {
                z.cores[i] = Shapes.Rounded("Core", go.transform, Vector3.zero, Vector3.one, 0.02f, z.core).transform;
                z.glows[i] = Shapes.Rounded("Glow", go.transform, Vector3.zero, Vector3.one, 0.05f, z.glow).transform;
            }
            z.sparkA = Shapes.Primitive(PrimitiveType.Sphere, "SparkA", go.transform, from, Vector3.one * 0.3f, z.glow).transform;
            z.sparkB = Shapes.Primitive(PrimitiveType.Sphere, "SparkB", go.transform, to, Vector3.one * 0.55f, z.glow).transform;
            z.Draw();
        }

        /// <summary>A new jagged path from the start to the end.</summary>
        private void Draw()
        {
            var dir = to - from;
            var side = Vector3.Cross(dir.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            var prev = from;
            for (int i = 0; i < Segments; i++)
            {
                float k = (i + 1) / (float)Segments;
                var next = i == Segments - 1 ? to : Vector3.Lerp(from, to, k) + side * Random.Range(-0.22f, 0.22f) + Vector3.up * Random.Range(-0.15f, 0.15f);
                Place(cores[i], prev, next, 0.05f);
                Place(glows[i], prev, next, 0.16f);
                prev = next;
            }
        }

        private static void Place(Transform piece, Vector3 a, Vector3 b, float width)
        {
            var d = b - a;
            piece.position = (a + b) * 0.5f;
            if (d.sqrMagnitude > 0.0001f) piece.rotation = Quaternion.LookRotation(d);
            piece.localScale = new Vector3(width, width, d.magnitude + width * 0.5f);
        }

        private void Update()
        {
            t += Time.deltaTime / Life;
            if (t >= 1f) { Destroy(gameObject); return; }
            redraw -= Time.deltaTime;
            if (redraw <= 0f) { redraw = 0.05f; Draw(); }
            float a = (1f - t * t) * (0.75f + 0.25f * Mathf.Sin(t * 70f));
            MaterialFactory.SetColors(core, Color.Lerp(colour, Color.white, 0.6f), colour * 4f * a);
            MaterialFactory.SetColors(glow, new Color(colour.r, colour.g, colour.b, 0.45f * a), colour * 2.4f * a);
            sparkB.localScale = Vector3.one * (0.55f + t * 0.6f);
        }
    }
}
