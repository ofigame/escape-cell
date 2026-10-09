using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Shows what a tap picked: a ring that snaps in around the target's tile and fades as it widens, with a small
    /// arrow dropping onto it. Gold when the target is in reach (a blow follows), cyan when foi has to step closer.
    /// </summary>
    public class TapMarker : MonoBehaviour
    {
        private const float Life = 0.45f;
        private Material mat;
        private Transform ring, arrow;
        private Color colour;
        private float t;

        public static void Create(Vector3 tile, Color colour)
        {
            var go = new GameObject("TapMarker");
            go.transform.position = tile + Vector3.up * 0.03f;
            go.AddComponent<TapMarker>().Build(colour);
        }

        private void Build(Color c)
        {
            colour = c;
            mat = MaterialFactory.CreateTransparent(new Color(c.r, c.g, c.b, 0.9f), c * 2f);
            ring = new GameObject("Ring").transform;
            ring.SetParent(transform, false);
            const int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Shapes.Rounded("Seg", ring, new Vector3(Mathf.Cos(a) * 0.48f, 0f, Mathf.Sin(a) * 0.48f), new Vector3(0.2f, 0.03f, 0.06f), 0.015f, mat)
                    .transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
            arrow = new GameObject("Arrow").transform;
            arrow.SetParent(transform, false);
            foreach (float s in new[] { -1f, 1f })
                Shapes.Rounded("Wing", arrow, new Vector3(s * 0.07f, 0.07f, 0f), new Vector3(0.05f, 0.2f, 0.05f), 0.02f, mat)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -s * 40f); // a "V" pointing down at the target
        }

        private void Update()
        {
            t += Time.deltaTime / Life;
            if (t >= 1f) { Destroy(gameObject); return; }
            float grow = 1f - (1f - t) * (1f - t);
            ring.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.15f, grow);
            arrow.localPosition = new Vector3(0f, Mathf.Lerp(1.6f, 1.05f, grow), 0f);
            var cam = Camera.main;
            if (cam != null) arrow.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            float a = 1f - t * t;
            MaterialFactory.SetColors(mat, new Color(colour.r, colour.g, colour.b, 0.9f * a), colour * 2f * a);
        }
    }
}
