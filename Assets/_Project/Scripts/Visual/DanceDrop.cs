using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The victory dance with free hands: what foi holds (its weapon, its shield) is thrown down beside its feet as it
    /// starts and lies there on the ground while it dances; back in its hands once the dance is over.
    /// </summary>
    public class DanceDrop : MonoBehaviour
    {
        private readonly List<GameObject> hidden = new List<GameObject>();
        private readonly List<(Transform t, Vector3 from, Vector3 to, Quaternion fromTurn, Quaternion toTurn)> thrown = new List<(Transform, Vector3, Vector3, Quaternion, Quaternion)>();
        private SkinnedHero hero;
        private float t, grace;
        private Vector3 dancedAt;

        /// <summary>Throws down what <paramref name="hero"/> holds (nothing held: nothing done).</summary>
        public static void Drop(SkinnedHero hero)
        {
            if (hero == null || hero.transform.parent == null) return;
            var d = hero.GetComponent<DanceDrop>();
            if (d == null) d = hero.gameObject.AddComponent<DanceDrop>();
            d.hero = hero;
            d.Throw();
        }

        private void Throw()
        {
            Restore();
            var visual = transform.parent;
            var held = new List<Transform>();
            foreach (var c in visual.GetComponentsInChildren<Transform>(false))
                if (c.name == "HeldWeapon" || c.name == "HeldShield" || c.name == "HeldHammer" || c.name == "ArenaHammer") held.Add(c);
            if (held.Count == 0) { enabled = false; return; }
            float ground = visual.position.y;
            var fwd = visual.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            float s = visual.lossyScale.y;
            int i = 0;
            foreach (var h in held)
            {
                var copy = Instantiate(h.gameObject, h.position, h.rotation).transform;
                copy.name = "Dropped_" + h.name;
                copy.localScale = h.lossyScale;
                foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(b);
                foreach (var l in copy.GetComponentsInChildren<Light>(true)) l.enabled = false;
                foreach (var tr in copy.GetComponentsInChildren<TrailRenderer>(true)) Destroy(tr);
                foreach (var ps in copy.GetComponentsInChildren<ParticleSystem>(true)) Destroy(ps.gameObject);
                h.gameObject.SetActive(false);
                hidden.Add(h.gameObject);
                // down on the ground: a weapon (its length up its +Y) laid along the floor, a shield (facing +Z) face up
                bool shield = h.name == "HeldShield";
                float side = shield ? -1f : 1f;
                var to = visual.position + right * side * 0.75f * s + fwd * (0.35f + 0.1f * i) * s;
                var turn = Quaternion.LookRotation(fwd, Vector3.up) * (shield ? Quaternion.Euler(-90f, 20f * side, 0f) : Quaternion.Euler(90f, 0f, 70f * side));
                copy.rotation = turn;
                var bounds = Bounds(copy);
                to.y = ground + (copy.position.y - bounds.min.y) + 0.01f;
                copy.rotation = h.rotation;
                thrown.Add((copy, h.position, to, h.rotation, turn));
                i++;
            }
            t = 0f; grace = 0.5f;
            dancedAt = visual.position;
            enabled = true;
        }

        private static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private void Update()
        {
            // the toss: out and down in a small arc (0.35 s)
            if (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 0.35f);
                foreach (var x in thrown)
                {
                    if (x.t == null) continue;
                    x.t.position = Vector3.Lerp(x.from, x.to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.25f;
                    x.t.rotation = Quaternion.Slerp(x.fromTurn, x.toTurn, t);
                }
            }
            // the dance over (or walked off): back in the hands
            grace -= Time.deltaTime;
            var at = transform.parent != null ? transform.parent.position : dancedAt; at.y = dancedAt.y;
            if (grace <= 0f && (hero == null || !hero.PlayingMove || (at - dancedAt).sqrMagnitude > 0.04f)) Restore();
        }

        /// <summary>The dropped pieces taken off the ground and shown in the hands again.</summary>
        public void Restore()
        {
            foreach (var x in thrown) if (x.t != null) Destroy(x.t.gameObject);
            thrown.Clear();
            foreach (var g in hidden) if (g != null) g.SetActive(true);
            hidden.Clear();
            enabled = false;
        }

        private void OnDestroy()
        {
            foreach (var x in thrown) if (x.t != null) Destroy(x.t.gameObject);
        }
    }
}
