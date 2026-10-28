using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// A fighter's picture layer over its place: a blow pushes the body's picture back a little (out quickly, then
    /// springing home); the fighter's place on the board, the rules' and the server's, never moves for it. The body
    /// (its hips) takes <see cref="Offset"/> each frame.
    /// </summary>
    public class VisualRoot : MonoBehaviour
    {
        private const float OutSeconds = 0.06f;
        private Vector3 push;
        private float t = 1f, back = 0.2f;
        private VisualTime clock;
        // the lunge: the striker's picture sliding toward its rival for a blow's contact (the design's 2.1), or the struck
        // one's lean into it; out, held, back
        private Vector3 lunge;
        private float lt = -1f, lOut, lHold, lBack;

        /// <summary>Where the body's picture is pushed to now (world), from where it stands.</summary>
        public Vector3 Offset { get; private set; }

        /// <summary>Back home at once: no push, lunge or throw under way (the pose reset).</summary>
        public void Clear()
        {
            t = 1f; lt = -1f; tt = -1f;
            Offset = Vector3.zero;
        }

        /// <summary>Nothing under way and home (the pose reset's check).</summary>
        public bool AtRest => t >= 1f && lt < 0f && tt < 0f && Offset.sqrMagnitude < 1e-6f;

        /// <summary>Pushed <paramref name="distance"/> along <paramref name="direction"/>, home again in <paramref name="recovery"/> seconds.</summary>
        public void Push(Vector3 direction, float distance, float recovery)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f || distance <= 0f) return;
            push = direction.normalized * distance;
            back = Mathf.Max(0.05f, recovery);
            t = 0f;
        }

        /// <summary>The least distance kept between two fighters' middles when one lunges at the other.</summary>
        public const float MinBodyGap = 1.0f;

        /// <summary>A lunge of <paramref name="distance"/> from <paramref name="from"/> toward <paramref name="to"/>, cut so the
        /// two keep <see cref="MinBodyGap"/> (and <paramref name="spare"/> more) apart: never into the other.</summary>
        public static float KeptApart(Vector3 from, Vector3 to, float distance, float spare = 0f)
        {
            var d = to - from; d.y = 0f;
            float gap = SquashBot.Visual.SkinnedHero.ContactDistance > 0.01f ? SquashBot.Visual.SkinnedHero.ContactDistance : MinBodyGap; // (the bodies' own contact distance once known)
            return Mathf.Min(distance, Mathf.Max(0f, d.magnitude - gap - spare));
        }

        /// <summary>Slid <paramref name="distance"/> along <paramref name="direction"/> over <paramref name="outSeconds"/>, held
        /// <paramref name="holdSeconds"/>, home in <paramref name="backSeconds"/> (a blow's lunge; the place never moves).</summary>
        // a throw by a blow (the collision rules' section 4): out along its curve, fast then slowing, then home
        private Vector3 thrown;
        private float tt = -1f, tOut, tBack;

        /// <summary>Thrown <paramref name="distance"/> along <paramref name="direction"/> over <paramref name="seconds"/> by
        /// the settings' curve (fast at first, slowing), then home smoothly (a picture: its place never moves).</summary>
        public void Throw(Vector3 direction, float distance, float seconds)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f || distance <= 0f) return;
            thrown = direction.normalized * distance;
            tOut = Mathf.Max(0.04f, seconds);
            tBack = Mathf.Max(0.2f, seconds);
            tt = 0f;
        }

        /// <summary>Rushing out (or held there) on a lunge, not on its way home.</summary>
        public bool Lunging => lt >= 0f && lt < lOut + lHold;

        public void Lunge(Vector3 direction, float distance, float outSeconds, float holdSeconds, float backSeconds)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f || distance <= 0f) return;
            if (lt >= 0f && lt < lOut + lHold && lunge.magnitude >= distance && Vector3.Dot(lunge, direction) > 0f) return; // (already rushing further that way: a blow's own small lunge never pulls it back)
            lunge = direction.normalized * distance;
            lOut = Mathf.Max(0.01f, outSeconds); lHold = Mathf.Max(0f, holdSeconds); lBack = Mathf.Max(0.03f, backSeconds);
            lt = 0f;
        }

        private void Update()
        {
            if (t >= 1f && lt < 0f && tt < 0f) { Offset = Vector3.zero; return; }
            if (clock == null) clock = GetComponent<VisualTime>();
            float dt = clock != null ? clock.DeltaTime : Time.deltaTime;
            var off = Vector3.zero;
            if (t < 1f)
            {
                float total = OutSeconds + back;
                t = Mathf.Min(1f, t + dt / total);
                float s = t * total;
                float k = s < OutSeconds ? Mathf.SmoothStep(0f, 1f, s / OutSeconds) : 1f - Mathf.SmoothStep(0f, 1f, (s - OutSeconds) / back);
                off += push * k;
            }
            if (lt >= 0f)
            {
                lt += dt;
                float k = lt < lOut ? Mathf.SmoothStep(0f, 1f, lt / lOut)
                    : lt < lOut + lHold ? 1f
                    : 1f - Mathf.SmoothStep(0f, 1f, (lt - lOut - lHold) / lBack);
                off += lunge * k;
                if (lt >= lOut + lHold + lBack) lt = -1f;
            }
            if (tt >= 0f)
            {
                tt += dt;
                var curve = CollisionSettings.Current.throwCurve;
                float k = tt < tOut ? curve.Evaluate(tt / tOut) : 1f - Mathf.SmoothStep(0f, 1f, (tt - tOut) / tBack);
                off += thrown * k;
                if (tt >= tOut + tBack) tt = -1f;
            }
            Offset = off;
        }

        /// <summary>The picture layer on a visual root (made the first time it is asked for).</summary>
        public static VisualRoot On(Transform visualRoot)
        {
            if (visualRoot == null) return null;
            var v = visualRoot.GetComponent<VisualRoot>();
            return v != null ? v : visualRoot.gameObject.AddComponent<VisualRoot>();
        }
    }
}
