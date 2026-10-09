using System;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>A tower's shot: a glowing bolt with a short trail flying in a low arc to its tile, then a small burst.</summary>
    public class TowerBolt : MonoBehaviour
    {
        private Vector3 from, to;
        private float t, duration;
        private Action landed;
        private Transform trail;

        public static void Fire(Vector3 from, Vector3 to, Color colour, float duration, Action landed)
        {
            var go = new GameObject("TowerBolt");
            go.transform.position = from;
            var b = go.AddComponent<TowerBolt>();
            b.from = from;
            b.to = to;
            b.duration = Mathf.Max(0.05f, duration);
            b.landed = landed;
            var m = MaterialFactory.Create(colour, colour * 3f);
            Shapes.Primitive(PrimitiveType.Sphere, "Bolt", go.transform, Vector3.zero, Vector3.one * 0.22f, m);
            b.trail = Shapes.Rounded("Trail", go.transform, new Vector3(0f, 0f, -0.2f), new Vector3(0.1f, 0.1f, 0.4f), 0.05f,
                MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.5f), colour * 2f)).transform;
        }

        private void Update()
        {
            t = Mathf.Min(1f, t + Time.deltaTime / duration);
            var p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
            var dir = p - transform.position;
            transform.position = p;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
            if (t < 1f) return;
            landed?.Invoke();
            Destroy(gameObject);
        }
    }
}
