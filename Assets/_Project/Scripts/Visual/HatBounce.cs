using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The hat on a fighter's head in the duel (the design's §2.3): a medium blow makes it hop a few centimetres and
    /// settle; a heavy blow or a throw sends it off the head, spinning in the air, and it lands back on the head within
    /// half a second (a comic beat); a knock-out drops it on the floor, and the next round it pops back onto the head.
    /// It moves the hat's mount from its resting place (the head carries it as before).
    /// </summary>
    public class HatBounce : MonoBehaviour
    {
        private enum State { Rest, Hop, Fly, Down }
        private State state;
        private Vector3 restPos;
        private Quaternion restRot;
        private Transform restParent;
        private float t, spin;
        private Vector3 flyDir, fallVel;
        private bool known;

        public static HatBounce On(Transform hatMount)
        {
            if (hatMount == null) return null;
            var h = hatMount.GetComponent<HatBounce>();
            return h != null ? h : hatMount.gameObject.AddComponent<HatBounce>();
        }

        private void Know()
        {
            if (known) return;
            known = true;
            restParent = transform.parent;
            restPos = transform.localPosition;
            restRot = transform.localRotation;
        }

        /// <summary>A medium blow: a little hop.</summary>
        public void Hop() { Know(); if (state != State.Rest) return; state = State.Hop; t = 0f; }

        /// <summary>A heavy blow or a throw: off the head (away from <paramref name="from"/>), a spin, back on.</summary>
        public void Fly(Vector3 from)
        {
            Know();
            if (state == State.Down) return;
            state = State.Fly;
            t = 0f;
            var d = transform.position - from; d.y = 0f;
            flyDir = d.sqrMagnitude > 1e-4f ? d.normalized : -transform.forward;
            spin = Random.value < 0.5f ? -1f : 1f;
        }

        /// <summary>A knock-out: the hat falls off onto the floor and lies there.</summary>
        public void KnockOff(Vector3 from)
        {
            Know();
            if (state == State.Down) return;
            state = State.Down;
            var d = transform.position - from; d.y = 0f;
            fallVel = (d.sqrMagnitude > 1e-4f ? d.normalized : -transform.forward) * 1.4f + Vector3.up * 2.2f;
            transform.SetParent(null, true);
        }

        /// <summary>Back on the head at once (a new round: it pops back with a flash).</summary>
        public void Restore()
        {
            if (!known) return;
            if (restParent != null && transform.parent != restParent) transform.SetParent(restParent, false);
            transform.localPosition = restPos;
            transform.localRotation = restRot;
            state = State.Rest;
        }

        private void LateUpdate()
        {
            if (!known || state == State.Rest) return;
            if (restParent == null) { Destroy(gameObject); return; } // (its head gone: it goes too)
            float dt = Time.deltaTime;
            t += dt;
            float unit = restParent.lossyScale.y;
            switch (state)
            {
                case State.Hop:
                {
                    const float T = 0.22f;
                    float k = Mathf.Clamp01(t / T);
                    transform.localPosition = restPos + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.03f / Mathf.Max(1e-4f, unit));
                    transform.localRotation = restRot * Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 8f, 0f, 0f);
                    if (k >= 1f) Restore();
                    break;
                }
                case State.Fly:
                {
                    const float T = 0.5f;
                    float k = Mathf.Clamp01(t / T);
                    float h = Mathf.Sin(k * Mathf.PI) * 0.55f;
                    var world = restParent.TransformPoint(restPos) + Vector3.up * h + flyDir * Mathf.Sin(k * Mathf.PI) * 0.25f;
                    transform.position = world;
                    transform.localRotation = restRot * Quaternion.Euler(0f, 0f, spin * 360f * k) * Quaternion.Euler(spin * 25f * Mathf.Sin(k * Mathf.PI), 0f, 0f);
                    if (k >= 1f) Restore();
                    break;
                }
                case State.Down:
                {
                    if (transform.parent != null) break;
                    fallVel += Physics.gravity * dt;
                    var p = transform.position + fallVel * dt;
                    float floor = restParent.root.position.y + 0.05f;
                    if (p.y < floor) { p.y = floor; fallVel = Vector3.zero; }
                    transform.position = p;
                    if (fallVel.sqrMagnitude > 0f) transform.rotation *= Quaternion.Euler(240f * dt, 0f, 120f * dt);
                    break;
                }
            }
        }
    }
}
