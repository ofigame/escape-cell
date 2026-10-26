using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// A knocked-out fighter's fall, played on a stand-in of its body as it was struck (the fighter itself already gone by
    /// the rules): it sags to its knees, topples over onto its side, lies a moment with electric arcs crackling, and
    /// sinks away. Only a picture.
    /// </summary>
    public class KnockoutFall : MonoBehaviour
    {
        private const float Kneel = 0.35f, Topple = 0.45f, Lie = 1.1f, Sink = 0.4f, BodyFall = 1.2f;
        private Vector3 start, side;
        private Quaternion startRot;
        private float t, height, arcAt;
        private bool bodyFalls;

        /// <summary>The fall on <paramref name="standIn"/> (its height <paramref name="bodyHeight"/>), toppling away from <paramref name="from"/>.</summary>
        public static void Play(Transform standIn, float bodyHeight, Vector3 from) => Play(standIn, bodyHeight, from, false);

        /// <summary>The same; <paramref name="bodyFalls"/>: the stand-in's own knockout clip does the falling (it is not
        /// toppled), only the arcs while it lies and the sinking away are added.</summary>
        public static void Play(Transform standIn, float bodyHeight, Vector3 from, bool bodyFalls)
        {
            if (standIn == null) return;
            var f = standIn.gameObject.AddComponent<KnockoutFall>();
            f.bodyFalls = bodyFalls;
            if (bodyFalls) f.t = Kneel + Topple - BodyFall; // (its clip's fall takes longer: the arcs and the sinking after it)
            f.start = standIn.position;
            f.startRot = standIn.rotation;
            f.height = Mathf.Max(0.3f, bodyHeight);
            var away = standIn.position - from; away.y = 0f;
            f.side = away.sqrMagnitude > 1e-4f ? away.normalized : -standIn.forward;
        }

        private void Update()
        {
            t += Time.deltaTime * VisualTime.GlobalScale;
            var axis = Vector3.Cross(Vector3.up, side); // (it topples away from the blow)
            if (bodyFalls && t < Kneel + Topple) { if (t >= Kneel + Topple * 0.9f && arcAt <= 0f) arcAt = t; return; } // (its clip falls)
            if (t < Kneel)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Kneel);
                transform.position = start - Vector3.up * height * 0.22f * k;
                transform.rotation = Quaternion.AngleAxis(-12f * k, axis) * startRot;
            }
            else if (t < Kneel + Topple)
            {
                float k = (t - Kneel) / Topple; k = k * k; // (falling faster and faster)
                transform.position = start - Vector3.up * height * (0.22f + 0.18f * k) + side * height * 0.25f * k;
                transform.rotation = Quaternion.AngleAxis(Mathf.Lerp(-12f, 82f, k), axis) * startRot;
                if (k >= 0.98f && arcAt <= 0f) arcAt = t; // (it lands)
            }
            else if (t < Kneel + Topple + Lie)
            {
                if (Time.frameCount % 20 == 0)
                {
                    var c = transform.position + Vector3.up * height * 0.2f;
                    float s = height * 0.25f;
                    Visual.MoveFx.Bolt(c + Random.insideUnitSphere * s, c + Random.insideUnitSphere * s, new Color(0.6f, 0.85f, 1f), 0.035f * height, 0.14f);
                }
            }
            else
            {
                float k = Mathf.Clamp01((t - Kneel - Topple - Lie) / Sink);
                transform.position += Vector3.down * height * 0.6f * Time.deltaTime / Sink;
                transform.localScale = Vector3.one * Mathf.Max(0.01f, 1f - k);
                if (k >= 1f) Destroy(gameObject);
            }
        }
    }
}
