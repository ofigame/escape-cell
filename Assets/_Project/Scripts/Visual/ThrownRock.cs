using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>A boulder hurled by a monster: it arcs high and lands on its target at the given moment, spinning.</summary>
    public class ThrownRock : MonoBehaviour
    {
        private Vector3 from, to;
        private float duration, t, height;
        private Transform rock, shadow;

        public static ThrownRock Create(Vector3 from, Vector3 to, float duration, Color tint)
        {
            var go = new GameObject("ThrownRock");
            var r = go.AddComponent<ThrownRock>();
            r.from = from;
            r.to = to;
            r.duration = Mathf.Max(0.2f, duration);
            r.height = 1.6f + Vector3.Distance(from, to) * 0.25f;
            var stone = MaterialFactory.Create(Color.Lerp(new Color(0.55f, 0.5f, 0.48f), tint, 0.25f), Color.black);
            r.rock = new GameObject("Rock").transform;
            r.rock.SetParent(go.transform, false);
            Shapes.Rounded("Core", r.rock, Vector3.zero, new Vector3(0.42f, 0.36f, 0.4f), 0.12f, stone);
            Shapes.Rounded("Chunk", r.rock, new Vector3(0.12f, 0.1f, -0.05f), new Vector3(0.22f, 0.2f, 0.22f), 0.07f, stone);
            r.shadow = Shapes.Primitive(PrimitiveType.Cylinder, "Shadow", go.transform, Vector3.zero, new Vector3(0.1f, 0.003f, 0.1f),
                MaterialFactory.CreateTransparent(new Color(0f, 0f, 0f, 0.35f), Color.black)).transform;
            go.transform.position = from;
            return r;
        }

        private void Update()
        {
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            var p = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * height);
            rock.position = p;
            rock.rotation = Quaternion.Euler(k * 540f, k * 220f, 0f);
            shadow.position = new Vector3(Mathf.Lerp(from.x, to.x, k), to.y + 0.02f, Mathf.Lerp(from.z, to.z, k));
            float s = Mathf.Lerp(0.15f, 0.7f, k);
            shadow.localScale = new Vector3(s, 0.003f, s);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
