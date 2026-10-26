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

        /// <summary>Pushed <paramref name="distance"/> along <paramref name="direction"/>, home again in <paramref name="recovery"/> seconds.</summary>
        public void Push(Vector3 direction, float distance, float recovery)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f || distance <= 0f) return;
            push = direction.normalized * distance;
            back = Mathf.Max(0.05f, recovery);
            t = 0f;
        }

        /// <summary>Slid <paramref name="distance"/> along <paramref name="direction"/> over <paramref name="outSeconds"/>, held
        /// <paramref name="holdSeconds"/>, home in <paramref name="backSeconds"/> (a blow's lunge; the place never moves).</summary>
        public void Lunge(Vector3 direction, float distance, float outSeconds, float holdSeconds, float backSeconds)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f || distance <= 0f) return;
            lunge = direction.normalized * distance;
            lOut = Mathf.Max(0.01f, outSeconds); lHold = Mathf.Max(0f, holdSeconds); lBack = Mathf.Max(0.03f, backSeconds);
            lt = 0f;
        }

        private void Update()
        {
            if (t >= 1f && lt < 0f) { Offset = Vector3.zero; return; }
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
