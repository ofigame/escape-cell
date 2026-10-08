using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// What a squashed bug leaves on its tile: a burst of droplets thrown up and out that fall back onto the floor, and
    /// a flat irregular splat of its colour that spreads out in a blink, stays a few seconds and fades away.
    /// </summary>
    public class BloodSplat : MonoBehaviour
    {
        private const float Life = 4.5f;
        private Material mat;
        private Transform[] drops;
        private Vector3[] vel;
        private float t;
        private Color colour;

        public static void Create(Vector3 floorPoint, Color colour, int seed)
        {
            var go = new GameObject("BloodSplat");
            go.transform.position = floorPoint + Vector3.up * 0.012f;
            var s = go.AddComponent<BloodSplat>();
            s.colour = colour;
            s.Make(seed);
        }

        private void Make(int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            mat = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.92f), colour * 0.25f);
            // The splat: a big blob and a ring of smaller ones, flattened onto the floor.
            Shapes.Primitive(PrimitiveType.Sphere, "Blob", transform, Vector3.zero, new Vector3(0.42f, 0.01f, 0.36f), mat)
                .transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            for (int i = 0; i < 9; i++)
            {
                float a = R(0f, Mathf.PI * 2f), r = R(0.16f, 0.38f), size = R(0.05f, 0.14f);
                Shapes.Primitive(PrimitiveType.Sphere, "Drop", transform, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), new Vector3(size, 0.008f, size * R(0.7f, 1.6f)), mat)
                    .transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            }
            transform.localScale = new Vector3(0.2f, 1f, 0.2f);
            // The droplets in the air.
            drops = new Transform[10];
            vel = new Vector3[drops.Length];
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = Shapes.Primitive(PrimitiveType.Sphere, "Spray", null, transform.position + Vector3.up * 0.05f, Vector3.one * R(0.04f, 0.08f), mat).transform;
                float a = R(0f, Mathf.PI * 2f);
                vel[i] = new Vector3(Mathf.Cos(a) * R(0.8f, 2f), R(1.5f, 3.2f), Mathf.Sin(a) * R(0.8f, 2f));
            }
        }

        private void Update()
        {
            t += Time.deltaTime;
            float spread = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.12f), 3f);
            transform.localScale = new Vector3(0.2f + 0.8f * spread, 1f, 0.2f + 0.8f * spread);
            for (int i = 0; i < drops.Length; i++)
            {
                if (drops[i] == null) continue;
                vel[i] += Vector3.down * 12f * Time.deltaTime;
                drops[i].position += vel[i] * Time.deltaTime;
                if (drops[i].position.y < transform.position.y)
                {
                    Destroy(drops[i].gameObject);
                    drops[i] = null;
                }
            }
            float fade = Mathf.Clamp01((Life - t) / 1.2f);
            MaterialFactory.SetColors(mat, new Color(colour.r, colour.g, colour.b, 0.92f * fade), colour * 0.25f * fade);
            if (t >= Life)
            {
                foreach (var d in drops) if (d != null) Destroy(d.gameObject);
                Destroy(gameObject);
            }
        }
    }
}
