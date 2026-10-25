using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// A few bolts and nuts knocked off a fighter by a heavy blow: they fly out from the blow, spin, bounce once on the
    /// floor and are gone in a second. From a small pool (never more than six at once, no new objects a blow).
    /// </summary>
    public class ScrapBits : MonoBehaviour
    {
        private const int Max = 6;
        private const float Life = 1f;
        private static readonly List<ScrapBits> pool = new List<ScrapBits>(Max);
        private static Material metal;
        private Vector3 vel, spinAxis;
        private float t = Life, floor, spinRate;

        /// <summary>Up to <paramref name="count"/> bits from <paramref name="at"/>, flung along <paramref name="away"/>
        /// (sized to the fighter, <paramref name="scale"/>); <paramref name="floorY"/> where they bounce.</summary>
        public static void Spawn(Vector3 at, Vector3 away, int count, float scale, float floorY)
        {
            if (metal == null) metal = Visual.MaterialFactory.CreateSurface(new Color(0.72f, 0.72f, 0.75f), new Color(0.2f, 0.18f, 0.12f), 0.8f, 0.9f);
            away.y = 0f;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward;
            for (int n = 0; n < count; n++)
            {
                var b = Take();
                if (b == null) return;
                b.transform.position = at;
                b.transform.localScale = Vector3.one * scale;
                var side = Vector3.Cross(Vector3.up, away);
                b.vel = (away * Random.Range(2.5f, 4.5f) + side * Random.Range(-2f, 2f) + Vector3.up * Random.Range(3f, 5.5f)) * scale;
                b.spinAxis = Random.onUnitSphere;
                b.spinRate = Random.Range(500f, 1100f);
                b.floor = floorY + 0.03f * scale;
                b.t = 0f;
                b.gameObject.SetActive(true);
            }
        }

        private static ScrapBits Take()
        {
            foreach (var b in pool) if (b != null && !b.gameObject.activeSelf) return b;
            pool.RemoveAll(b => b == null);
            if (pool.Count >= Max) return null;
            var go = new GameObject("ScrapBit");
            // a bolt: a short shaft and a hexagon-ish head
            Visual.Shapes.Primitive(PrimitiveType.Cylinder, "Shaft", go.transform, Vector3.zero, new Vector3(0.025f, 0.05f, 0.025f), metal);
            Visual.Shapes.Primitive(PrimitiveType.Cylinder, "Head", go.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.012f, 0.05f), metal);
            var bit = go.AddComponent<ScrapBits>();
            go.SetActive(false);
            pool.Add(bit);
            return bit;
        }

        private void Update()
        {
            t += Time.deltaTime;
            if (t >= Life) { gameObject.SetActive(false); return; }
            vel += Physics.gravity * Time.deltaTime;
            var p = transform.position + vel * Time.deltaTime;
            if (p.y < floor && vel.y < 0f) { p.y = floor; vel = new Vector3(vel.x * 0.5f, -vel.y * 0.35f, vel.z * 0.5f); } // (one bounce, then it rolls out)
            transform.position = p;
            transform.Rotate(spinAxis, spinRate * Time.deltaTime, Space.World);
            if (t > Life * 0.75f) transform.localScale *= 0.92f; // (it shrinks away)
        }
    }
}
