using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>A small looping fire on a tile: glowing tongues that rise, shrink and restart.</summary>
    public class Flames : MonoBehaviour
    {
        private const int Tongues = 9;
        private const float Period = 0.7f;

        private static Material hot, warm;

        private Transform[] tongues;
        private Vector3[] bases;
        private float[] offsets;

        public static GameObject Create(Transform parent)
        {
            if (hot == null) hot = MaterialFactory.Create(new Color(1f, 0.85f, 0.4f), new Color(3f, 1.8f, 0.4f));
            if (warm == null) warm = MaterialFactory.Create(new Color(1f, 0.4f, 0.12f), new Color(2.6f, 0.6f, 0.08f));

            var root = new GameObject("Fire");
            root.transform.SetParent(parent, false);
            var flames = root.AddComponent<Flames>();
            flames.Build();
            return root;
        }

        private void Build()
        {
            tongues = new Transform[Tongues];
            bases = new Vector3[Tongues];
            offsets = new float[Tongues];
            for (int i = 0; i < Tongues; i++)
            {
                float a = i * Mathf.PI * 2f / Tongues;
                float r = i % 3 == 0 ? 0.08f : 0.24f;
                bases[i] = new Vector3(Mathf.Cos(a) * r, 0.08f, Mathf.Sin(a) * r);
                offsets[i] = Random.value;
                tongues[i] = Shapes.Rounded("Tongue", transform, bases[i], new Vector3(0.14f, 0.2f, 0.14f), 0.06f, i % 3 == 0 ? hot : warm).transform;
                tongues[i].localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
        }

        private void Update()
        {
            for (int i = 0; i < Tongues; i++)
            {
                float t = Mathf.Repeat(Time.time / Period + offsets[i], 1f);
                float rise = t * 0.55f;
                float wobble = Mathf.Sin((Time.time + offsets[i] * 7f) * 11f) * 0.03f;
                tongues[i].localPosition = bases[i] * (1f - t * 0.6f) + new Vector3(wobble, rise, 0f);
                float s = Mathf.Sin(t * Mathf.PI) * 1.1f + 0.1f;
                tongues[i].localScale = new Vector3(s, s * 1.3f, s);
            }
        }
    }
}
