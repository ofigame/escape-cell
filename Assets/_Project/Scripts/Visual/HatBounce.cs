using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// What sits on a fighter's head in the duel (the crown, on the hat's mount): a blow makes it hop a few centimetres
    /// and settle. It never flies off or falls (the old hats did: Docs/RUTBE_KUSAMI_TAC_SAC_BIRLESIK.md, 1.2). It moves
    /// the mount from its resting place (the head carries it as before).
    /// </summary>
    public class HatBounce : MonoBehaviour
    {
        private enum State { Rest, Hop }
        private State state;
        private Vector3 restPos;
        private Quaternion restRot;
        private Transform restParent;
        private float t;
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

        /// <summary>A blow: a little hop.</summary>
        public void Hop() { Know(); if (state != State.Rest) return; state = State.Hop; t = 0f; }

        /// <summary>Back in its place at once (a new round).</summary>
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
            t += Time.deltaTime;
            float unit = restParent.lossyScale.y;
            const float T = 0.22f;
            float k = Mathf.Clamp01(t / T);
            transform.localPosition = restPos + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.03f / Mathf.Max(1e-4f, unit));
            transform.localRotation = restRot * Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 8f, 0f, 0f);
            if (k >= 1f) Restore();
        }
    }
}
