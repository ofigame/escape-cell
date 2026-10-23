using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>A weapon's own light breathing softly (fire flickers a little faster than ice).</summary>
    public class BladeGlow : MonoBehaviour
    {
        private Light glow;
        private float baseIntensity, phase;

        private void Start()
        {
            glow = GetComponent<Light>();
            baseIntensity = glow != null ? glow.intensity : 1f;
            phase = Random.value * 10f;
        }

        private void Update()
        {
            if (glow == null) return;
            float t = Time.time + phase;
            glow.intensity = baseIntensity * (0.8f + 0.12f * Mathf.Sin(t * 3.1f) + 0.08f * Mathf.Sin(t * 7.3f));
        }
    }
}
