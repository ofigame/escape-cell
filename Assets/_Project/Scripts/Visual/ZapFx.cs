using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>Fifi's zap: a bright beam from Fifi to its target that flickers and fades in a moment.</summary>
    public class ZapFx : MonoBehaviour
    {
        private const float Life = 0.2f;
        private Material mat;
        private Color colour;
        private Transform beam;
        private float t;

        public static void Create(Vector3 from, Vector3 to, Color colour)
        {
            var go = new GameObject("Zap");
            var z = go.AddComponent<ZapFx>();
            z.colour = colour;
            z.mat = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.9f), colour * 3f);
            var mid = (from + to) * 0.5f;
            var dir = to - from;
            go.transform.position = mid;
            if (dir.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.LookRotation(dir);
            z.beam = Shapes.Rounded("Beam", go.transform, Vector3.zero, new Vector3(0.07f, 0.07f, dir.magnitude), 0.03f, z.mat).transform;
            Shapes.Primitive(PrimitiveType.Sphere, "Spark", go.transform, new Vector3(0f, 0f, dir.magnitude * 0.5f), Vector3.one * 0.26f, z.mat);
        }

        private void Update()
        {
            t += Time.deltaTime / Life;
            if (t >= 1f) { Destroy(gameObject); return; }
            float a = (1f - t) * (0.7f + 0.3f * Mathf.Sin(t * 60f));
            beam.localScale = new Vector3(1f + t, 1f + t, 1f);
            MaterialFactory.SetColors(mat, new Color(colour.r, colour.g, colour.b, 0.9f * a), colour * 3f * a);
        }
    }
}
