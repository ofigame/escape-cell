using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// foi's shield, held in its left hand by the grip on its back: as tall as foi's body, carried a little out in
    /// front on the left, its face forward and out. When it takes a blow the arm swings it up across the front for a
    /// moment. Set after the hero's own pose each frame (the left arm reaches for the shield's grip).
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class ShieldHold : MonoBehaviour
    {
        private Transform arm, fore, hand, hips, neck, holder;
        private float raise = 1f, bash = 1f, size, guardK;
        private string shown;

        /// <summary>Puts the carried shield on the rigged foi under <paramref name="visual"/> (or takes it off: none carried).</summary>
        public static ShieldHold Attach(Transform visual)
        {
            if (visual == null) return null;
            var hero = visual.GetComponentInChildren<SkinnedHero>(true);
            if (hero == null) return null;
            var h = hero.GetComponent<ShieldHold>();
            if (h == null) h = hero.gameObject.AddComponent<ShieldHold>();
            h.Refresh();
            return h;
        }

        private static Transform Bone(Transform under, string end)
        {
            foreach (var t in under.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(":" + end) || t.name == end) return t;
            return null;
        }

        /// <summary>Shows the shield carried now (rebuilt when it changed).</summary>
        public void Refresh()
        {
            var s = Shields.Equipped;
            string want = s != null ? s.model : null;
            if (want == shown && holder != null) return;
            if (holder != null) Destroy(holder.gameObject);
            holder = null;
            shown = want;
            arm = Bone(transform, "LeftArm"); fore = Bone(transform, "LeftForeArm"); hand = Bone(transform, "LeftHand");
            hips = Bone(transform, "Hips"); neck = Bone(transform, "Neck");
            if (want == null || fore == null || hand == null || hips == null || neck == null) return;
            // as tall as the body (hips to neck, and a bit)
            size = Vector3.Distance(hips.position, neck.position) * 1.82f * s.size / Mathf.Max(1e-4f, transform.lossyScale.y); // (30% under the old 2.6: from the thighs to the shoulders; in the body's own size)
            holder = new GameObject("HeldShield").transform;
            holder.SetParent(transform, false);
            GearFiles.Shield(holder, want, 1f, s.chest ? 0.15f : 0f);
            holder.localScale = Vector3.one * size;
            foreach (var tr in holder.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = gameObject.layer;
        }

        /// <summary>A copy of foi brings the shield along but not its hold: made again here, on the copy's own arm.</summary>
        private void Start()
        {
            if (holder != null) return;
            var old = transform.Find("HeldShield");
            if (old != null) DestroyImmediate(old.gameObject);
            shown = null;
            Refresh();
        }

        /// <summary>The block: the shield swung up across the front.</summary>
        public void Raise() => raise = 0f;

        /// <summary>Held up across the front for as long as it is set (the duel's block).</summary>
        public bool Guarding { get; set; }

        /// <summary>A shield is carried (shown on the arm).</summary>
        public bool Carried => holder != null;

        /// <summary>A blow with the shield: thrust straight out in front, face first, and back (0.4 s).</summary>
        public void Bash() => bash = 0f;

        private Transform Body => transform.parent != null ? transform.parent : transform;

        private void LateUpdate()
        {
            if (holder == null || arm == null || fore == null || hand == null) return;
            var body = Body;
            Vector3 right = body.right, up = body.up, ahead = body.forward;
            float reach = Vector3.Distance(arm.position, fore.position) + Vector3.Distance(fore.position, hand.position);
            // carried: the hand low in front on the left; the block: up and across the front
            float k = 0f;
            if (raise < 1f) { raise = Mathf.Min(1f, raise + Time.deltaTime / 0.5f); k = Mathf.Sin(raise * Mathf.PI); }
            guardK = Mathf.MoveTowards(guardK, Guarding ? 1f : 0f, Time.deltaTime / 0.06f); // (up within a few frames, down a little slower)
            k = Mathf.Max(k, guardK);
            float b = 0f;
            if (bash < 1f) { bash = Mathf.Min(1f, bash + Time.deltaTime / 0.4f); b = bash < 0.3f ? bash / 0.3f : 1f - (bash - 0.3f) / 0.7f; b = b * b * (3f - 2f * b); }
            var carry = arm.position + ahead * reach * 0.3f - right * reach * 0.42f - up * reach * 0.72f; // (the arm down at the left side, a little forward)
            var guard = arm.position + ahead * reach * 0.75f + right * reach * 0.25f - up * reach * 0.1f;
            var hero = GetComponent<SkinnedHero>();
            // a two-handed weapon in the hands: the shield rides on the back (its hand on the weapon) until it is wanted
            bool wanted = k > 0.01f || b > 0.01f;
            if (hero != null) hero.OffHandOnShield = wanted;
            if (hero != null && hero.TwoHandedNow && !wanted)
            {
                var spine = neck != null ? neck.parent : null;
                var backAt = (spine != null ? spine.position : neck.position) - ahead * size * transform.lossyScale.y * 0.16f - up * size * transform.lossyScale.y * 0.12f;
                holder.rotation = Quaternion.LookRotation(-ahead, up);
                holder.position = backAt;
                return;
            }
            var thrust = arm.position + ahead * reach * 0.98f + right * reach * 0.1f - up * reach * 0.05f; // (the arm straight out ahead)
            if (hero == null || !hero.Flailing) Reach(arm, fore, hand, Vector3.Lerp(Vector3.Lerp(carry, guard, k), thrust, b)); // (the shield arm always its own, a clip playing too: a clip's hand at the chest took the shield into the body; thrown about: the shield goes with the hand)
            // the shield: its back on the hand (the grip), its face forward and out to the left
            var face = Vector3.Lerp((ahead * (0.55f + 0.45f * k) - right * (0.85f - 0.7f * k)).normalized, ahead, b).normalized;
            holder.rotation = Quaternion.LookRotation(face, up);
            float s = size * transform.lossyScale.y; // (as big as foi is now)
            holder.position = hand.position + face * s * 0.07f - right * s * 0.08f; // (the hand on its grip, behind its middle)
        }

        /// <summary>Two-joint reach: the elbow bends (on the side it already bends to) so the hand comes to the target.</summary>
        private static void Reach(Transform upper, Transform lower, Transform end, Vector3 target)
        {
            var root = upper.position;
            float a = Vector3.Distance(root, lower.position), b = Vector3.Distance(lower.position, end.position);
            var to = target - root;
            if (to.sqrMagnitude < 1e-8f || a < 1e-5f || b < 1e-5f) return;
            var dir = to.normalized;
            float c = Mathf.Clamp(to.magnitude, Mathf.Abs(a - b) + 1e-4f, a + b - 1e-4f);
            float x = (a * a - b * b + c * c) / (2f * c), h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            var bend = lower.position - root; bend -= dir * Vector3.Dot(bend, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.Cross(dir, Vector3.up);
            var elbow = root + dir * x + bend.normalized * h;
            upper.rotation = Quaternion.FromToRotation(lower.position - root, elbow - root) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, root + dir * c - lower.position) * lower.rotation;
        }
    }
}
