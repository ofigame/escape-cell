using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The gun's sight on the floors: a slowly turning ring with four ticks round the target the FIRE button would hit,
    /// gliding from target to target. Red-gold when the gun is ready, grey while it cools or is empty.
    /// </summary>
    public class GunReticle : MonoBehaviour
    {
        private Material mat;
        private Transform ring;
        private Vector3 goal;
        private bool shown, snapped;
        private int shownFrame;
        private Color colour = new Color(1f, 0.35f, 0.25f);

        public static GunReticle Create(Transform parent)
        {
            var go = new GameObject("GunReticle");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<GunReticle>();
            r.Build();
            go.SetActive(false);
            return r;
        }

        private void Build()
        {
            mat = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.9f), colour * 2f);
            ring = new GameObject("Ring").transform;
            ring.SetParent(transform, false);
            const int segments = 12;
            for (int i = 0; i < segments; i++)
            {
                if (i % 3 == 0) continue; // gaps where the ticks are
                float a = i * Mathf.PI * 2f / segments;
                Shapes.Rounded("Seg", ring, new Vector3(Mathf.Cos(a) * 0.55f, 0f, Mathf.Sin(a) * 0.62f), new Vector3(0.3f, 0.05f, 0.09f), 0.015f, mat)
                    .transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                Shapes.Rounded("Tick", ring, new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.56f), new Vector3(0.09f, 0.05f, 0.34f), 0.015f, mat)
                    .transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
        }

        /// <summary>Over a target (its tile's surface point), ready or not.</summary>
        public void Show(Vector3 at, bool ready)
        {
            goal = at + Vector3.up * 0.08f;
            shownFrame = Time.frameCount;
            if (!shown) { shown = true; gameObject.SetActive(true); snapped = false; }
            if (!snapped) { transform.position = goal; snapped = true; }
            var c = ready ? new Color(1f, 0.35f, 0.25f) : new Color(0.6f, 0.62f, 0.68f);
            if (c != colour) { colour = c; MaterialFactory.SetColors(mat, new Color(c.r, c.g, c.b, 0.9f), c * (ready ? 2f : 0.6f)); }
        }

        public void Hide()
        {
            if (!shown) return;
            shown = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Time.frameCount - shownFrame > 2) { Hide(); return; } // the floor stopped (won, lost, paused): no sight
            transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-Time.deltaTime * 18f));
            ring.localRotation = Quaternion.Euler(0f, Time.time * 60f, 0f);
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 7f);
            ring.localScale = new Vector3(pulse, 1f, pulse);
        }

        private void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
