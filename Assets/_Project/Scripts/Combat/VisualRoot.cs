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

        private void Update()
        {
            if (t >= 1f) { Offset = Vector3.zero; return; }
            if (clock == null) clock = GetComponent<VisualTime>();
            float dt = clock != null ? clock.DeltaTime : Time.deltaTime;
            float total = OutSeconds + back;
            t = Mathf.Min(1f, t + dt / total);
            float s = t * total;
            float k = s < OutSeconds ? Mathf.SmoothStep(0f, 1f, s / OutSeconds) : 1f - Mathf.SmoothStep(0f, 1f, (s - OutSeconds) / back);
            Offset = push * k;
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
