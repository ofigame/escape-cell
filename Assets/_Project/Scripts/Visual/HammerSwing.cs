using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>One swing of the Thunder Hammer onto a monster, ending in a lightning strike.</summary>
    public class HammerSwing : MonoBehaviour
    {
        private const float Duration = 0.28f;
        private Vector3 from, target;
        private Transform model;
        private float t;
        private int level = 3;

        public void Begin(Vector3 start, Vector3 end, int level = 3)
        {
            from = start + Vector3.up * 0.6f;
            target = end;
            transform.position = from;
            model = HammerModels.Build(transform, level, 1.3f);
            this.level = level;
        }

        private void Update()
        {
            t += Time.deltaTime / Duration;
            float k = Mathf.Clamp01(t);
            // Up over the robot's head and down onto the monster, the head leading.
            var pos = Vector3.Lerp(from, target + Vector3.up * 0.5f, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.1f;
            transform.position = pos;
            var flat = target - from;
            flat.y = 0f;
            transform.rotation = Quaternion.LookRotation(flat.sqrMagnitude > 0.001f ? flat : Vector3.forward) * Quaternion.Euler(Mathf.Lerp(-40f, 120f, k), 0f, 0f);
            if (t < 1f) return;
            if (level >= 3) ThunderHammer.Strike(target + Vector3.up * 0.4f); // thunder and up call the lightning
            Destroy(gameObject, 0.05f);
            enabled = false;
        }
    }
}
