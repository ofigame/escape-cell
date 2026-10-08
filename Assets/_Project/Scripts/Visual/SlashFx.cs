using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A weapon strike you can't miss: a bright crescent swept through the target in the weapon's colour (it sweeps
    /// round, then fades), a white flash and a ring of light where the blow lands.
    /// </summary>
    public class SlashFx : MonoBehaviour
    {
        private const float Life = 0.32f;
        private Transform arc, flash;
        private Material arcMat, flashMat;
        private Color colour;
        private float t, size;

        public static void Create(Vector3 from, Vector3 target, Color colour, float size = 1f)
        {
            var go = new GameObject("SlashFx");
            var dir = target - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            go.transform.position = target + Vector3.up * 0.5f * size;
            go.transform.rotation = Quaternion.LookRotation(dir.normalized);
            var s = go.AddComponent<SlashFx>();
            s.colour = colour;
            s.size = size;
            s.Build();
        }

        private void Build()
        {
            arcMat = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.9f), colour * 3f);
            flashMat = MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.9f), new Color(3f, 3f, 3f));
            arc = new GameObject("Arc").transform;
            arc.SetParent(transform, false);
            // A crescent: thin glowing segments along a half circle across the blow, thickest in the middle.
            const int segments = 11;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(-80f, 80f, i / (float)(segments - 1)) * Mathf.Deg2Rad;
                float r = 0.85f * size;
                float thick = Mathf.Lerp(0.08f, 0.26f, 1f - Mathf.Abs(i - (segments - 1) * 0.5f) / ((segments - 1) * 0.5f)) * size;
                var seg = Shapes.Rounded("Seg", arc, new Vector3(Mathf.Sin(a) * r, Mathf.Cos(a) * r * 0.6f, 0f), new Vector3(0.34f * size, thick, 0.06f), 0.03f, arcMat);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
            }
            flash = Shapes.Primitive(PrimitiveType.Sphere, "Flash", transform, Vector3.zero, Vector3.one * 0.3f * size, flashMat).transform;
        }

        private void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Life);
            // The crescent sweeps down through the target and grows, the flash pops and shrinks.
            arc.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(70f, -50f, Mathf.SmoothStep(0f, 1f, k)));
            arc.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.25f, k);
            flash.localScale = Vector3.one * 0.3f * size * (1f + Mathf.Sin(Mathf.Clamp01(k * 2.5f) * Mathf.PI) * 2.2f) * (1f - k * 0.6f);
            float fade = 1f - k * k;
            MaterialFactory.SetColors(arcMat, new Color(colour.r, colour.g, colour.b, 0.9f * fade), colour * 3f * fade);
            MaterialFactory.SetColors(flashMat, new Color(1f, 1f, 1f, 0.9f * Mathf.Clamp01(1f - k * 1.8f)), new Color(3f, 3f, 3f) * (1f - k));
            if (t >= Life) Destroy(gameObject);
        }
    }
}
