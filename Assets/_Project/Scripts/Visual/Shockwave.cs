using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>A flat ring of force racing out over the floor from a slam, fading as it grows (from a pool: no new object a blow).</summary>
    public class Shockwave : MonoBehaviour
    {
        /// <summary>Its materials (made for it alone) go with it.</summary>
        private void Start() => OwnedMaterials.OwnAllUnder(gameObject);

        private const float Duration = 0.4f;
        private Transform ring, inner;
        private Material mat;
        private Color color;
        private float radius, t;

        /// <summary>How many are on screen: a burst of blows (a stomp over two dozen tiles) adds no more past a dozen.</summary>
        private static int live;
        private const int MaxLive = 12;

        private static readonly Stack<Shockwave> free = new Stack<Shockwave>();
        private bool inUse;

        private void OnDestroy() { if (inUse) live--; }

        public static void Create(Vector3 at, float radius, Color color)
        {
            if (live >= MaxLive) return;
            Shockwave s = null;
            while (free.Count > 0 && s == null) s = free.Pop(); // (one destroyed with its scene is skipped)
            if (s == null)
            {
                var go = new GameObject("Shockwave");
                s = go.AddComponent<Shockwave>();
                s.mat = MaterialFactory.CreateTransparent(new Color(color.r, color.g, color.b, 0.7f), color * 2.2f);
                // A ring: a bright disc with a darker floor-coloured disc over its middle.
                s.ring = Shapes.Primitive(PrimitiveType.Cylinder, "Ring", go.transform, Vector3.zero, Vector3.one * 0.1f, s.mat).transform;
                s.inner = Shapes.Primitive(PrimitiveType.Cylinder, "Hole", go.transform, Vector3.up * 0.005f, Vector3.one * 0.1f,
                    MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.08f), Color.black)).transform;
            }
            live++;
            s.inUse = true;
            s.transform.position = at + Vector3.up * 0.04f;
            s.radius = radius;
            s.color = color;
            s.t = 0f;
            s.gameObject.SetActive(true);
        }

        private void Update()
        {
            t += Time.deltaTime / Duration;
            float k = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            float d = Mathf.Lerp(0.3f, radius * 2f, k);
            ring.localScale = new Vector3(d, 0.01f, d);
            inner.localScale = new Vector3(d * 0.82f, 0.012f, d * 0.82f);
            MaterialFactory.SetColors(mat, new Color(color.r, color.g, color.b, 0.75f * (1f - k)), color * (2.2f * (1f - k)));
            if (t >= 1f)
            {
                // back to the pool
                inUse = false;
                live--;
                gameObject.SetActive(false);
                free.Push(this);
            }
        }
    }
}
