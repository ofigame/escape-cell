using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>A lightning line that flickers and fades, then removes itself.</summary>
    public class FadeLine : MonoBehaviour
    {
        private const float Life = 0.35f;
        private LineRenderer line;
        private Color start;
        private float age;

        private void Start()
        {
            line = GetComponent<LineRenderer>();
            start = line.startColor;
        }

        private void Update()
        {
            age += Time.deltaTime;
            // A flicker, then a fade.
            float a = Mathf.Clamp01(1f - age / Life) * (age < 0.12f ? (Mathf.Sin(age * 120f) > 0f ? 1f : 0.35f) : 1f);
            line.startColor = line.endColor = start * a;
            if (age >= Life) Destroy(gameObject);
        }
    }
}
