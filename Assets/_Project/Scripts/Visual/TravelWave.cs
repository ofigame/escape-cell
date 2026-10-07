using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>A glowing crescent of energy sweeping over the floor along a direction (a monster's swipe).</summary>
    public class TravelWave : MonoBehaviour
    {
        private const float Duration = 0.32f;
        private Vector3 from, dir;
        private float length, t;
        private Material mat;
        private Color color;
        private FxSystem fx;
        private float sparkT;

        public static void Create(Vector3 from, Vector3 dir, float length, Color color, FxSystem fx)
        {
            var go = new GameObject("TravelWave");
            var w = go.AddComponent<TravelWave>();
            w.from = from + Vector3.up * 0.25f;
            dir.y = 0f;
            w.dir = dir.normalized;
            w.length = length;
            w.color = color;
            w.fx = fx;
            go.transform.rotation = Quaternion.LookRotation(w.dir);
            w.mat = MaterialFactory.CreateTransparent(new Color(color.r, color.g, color.b, 0.8f), color * 2.6f);
            for (int i = -2; i <= 2; i++)
            {
                // Five slats bent into an arc make the crescent.
                var slat = Shapes.Rounded("Slat", go.transform, new Vector3(i * 0.22f, 0f, -Mathf.Abs(i) * 0.08f), new Vector3(0.24f, 0.32f, 0.08f), 0.03f, w.mat);
                slat.transform.localRotation = Quaternion.Euler(0f, i * 14f, 0f);
            }
            go.transform.position = w.from;
        }

        private void Update()
        {
            t += Time.deltaTime / Duration;
            float k = Mathf.Clamp01(t);
            transform.position = from + dir * (length * k);
            transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.4f, k);
            MaterialFactory.SetColors(mat, new Color(color.r, color.g, color.b, 0.85f * (1f - k * k)), color * 2.6f * (1f - k));
            sparkT -= Time.deltaTime;
            if (sparkT <= 0f && fx != null)
            {
                sparkT = 0.05f;
                fx.Burst(transform.position, color, color * 2f, 4, 2.5f);
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
