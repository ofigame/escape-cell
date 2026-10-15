using System;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A gun's shot: a glowing ball of energy in the gun's colour flying from the muzzle to the target, a few sparks
    /// trailing it, a burst where it lands (then <c>arrived</c> runs).
    /// </summary>
    public class EnergyBall : MonoBehaviour
    {
        private Vector3 from, to;
        private float t, duration;
        private Action arrived;
        private Color colour;
        private FxSystem fx;
        private float sparkAt;

        public static void Fly(Transform parent, Vector3 from, Vector3 to, Color colour, float size, FxSystem fx, Action arrived)
        {
            var go = new GameObject("EnergyBall");
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            var core = MaterialFactory.Create(Color.Lerp(colour, Color.white, 0.55f), colour * 3f);
            var halo = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.4f), colour * 1.6f);
            Shapes.Primitive(PrimitiveType.Sphere, "Core", go.transform, Vector3.zero, Vector3.one * size, core);
            Shapes.Primitive(PrimitiveType.Sphere, "Halo", go.transform, Vector3.zero, Vector3.one * size * 2.1f, halo);
            var b = go.AddComponent<EnergyBall>();
            b.from = from;
            b.to = to;
            b.colour = colour;
            b.fx = fx;
            b.arrived = arrived;
            b.duration = Mathf.Clamp(Vector3.Distance(from, to) / 22f, 0.06f, 0.4f); // quick: a ball of light, not a lob
        }

        private void Update()
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t));
            transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 40f) * 0.08f);
            if (fx != null && Time.time >= sparkAt) { sparkAt = Time.time + 0.03f; fx.Burst(transform.position, colour, colour * 2f, 2, 0.8f); }
            if (t < 1f) return;
            if (fx != null) fx.Burst(to, colour, colour * 2.4f, 12, 3f);
            arrived?.Invoke();
            Destroy(gameObject);
        }
    }
}
