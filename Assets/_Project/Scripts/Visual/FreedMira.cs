using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>Princess Mira, free at last: she rises out of the broken cage in a burst of light, twirls and waves.</summary>
    public class FreedMira : MonoBehaviour
    {
        private Transform model;
        private float t;

        public static FreedMira Create(Vector3 at, Vector3 lookAt)
        {
            var go = new GameObject("FreedMira");
            go.transform.position = at;
            var flat = lookAt - at;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(flat);
            var mira = go.AddComponent<FreedMira>();
            mira.model = PrincessMira.Build(go.transform, 1.3f);
            return mira;
        }

        private void Update()
        {
            t += Time.deltaTime;
            // Up out of the cage, a twirl, then a happy bob.
            float rise = Mathf.Clamp01(t / 0.6f);
            float y = Mathf.Sin(rise * Mathf.PI * 0.5f) * 0.25f + (t > 0.6f ? Mathf.Abs(Mathf.Sin((t - 0.6f) * 5f)) * 0.12f : 0f);
            model.localPosition = new Vector3(0f, y, 0f);
            model.localRotation = Quaternion.Euler(0f, t < 1.2f ? t / 1.2f * 360f : 0f, 0f);
        }
    }
}
