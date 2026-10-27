using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// foi's cell: a glowing blue glass cube on a dark base where every floor begins. It hums and pulses while foi is
    /// inside; <see cref="Shatter"/> breaks it — the panes crack white, then burst into shards that fly out, tumble and
    /// fade — and foi is free.
    /// </summary>
    public class GlassCell : MonoBehaviour
    {
        /// <summary>Its materials (made for it alone) go with it.</summary>
        private void Start() => OwnedMaterials.OwnAllUnder(gameObject);

        private const float Size = 1.25f, Height = 1.7f;
        /// <summary>Its inside height at scale 1 (the game sizes it to foi by it).</summary>
        public const float InnerHeight = Height;
        private Material glass, frame;
        private readonly List<Transform> panes = new List<Transform>();
        private float time, crack = -1f;
        private readonly List<(Transform t, Vector3 v, Vector3 spin)> shards = new List<(Transform, Vector3, Vector3)>();
        private Material shardMat;
        private float shardT = -1f;

        public bool Broken => crack >= 0f;

        public static GlassCell Create(Vector3 at, float scale)
        {
            var go = new GameObject("GlassCell");
            go.transform.position = at;
            go.transform.localScale = Vector3.one * scale;
            var c = go.AddComponent<GlassCell>();
            c.Build();
            return c;
        }

        private void Build()
        {
            var t = transform;
            glass = MaterialFactory.CreateTransparent(new Color(0.45f, 0.85f, 1f, 0.22f), new Color(0.2f, 0.6f, 1f));
            frame = MaterialFactory.Create(new Color(0.5f, 0.9f, 1f), new Color(0.8f, 2.2f, 3f));
            var baseMat = MaterialFactory.CreateSurface(new Color(0.16f, 0.18f, 0.23f), Color.black, 0.45f, 0.5f);
            float h = Size * 0.5f;
            Shapes.Rounded("Base", t, new Vector3(0f, 0.04f, 0f), new Vector3(Size + 0.16f, 0.1f, Size + 0.16f), 0.03f, baseMat);
            Shapes.Rounded("BaseGlow", t, new Vector3(0f, 0.1f, 0f), new Vector3(Size + 0.02f, 0.02f, Size + 0.02f), 0.01f, frame);
            // Four panes and a lid.
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, Height * 0.5f, -h), new Vector3(Size, Height, 0.03f)), (new Vector3(0f, Height * 0.5f, h), new Vector3(Size, Height, 0.03f)),
                (new Vector3(-h, Height * 0.5f, 0f), new Vector3(0.03f, Height, Size)), (new Vector3(h, Height * 0.5f, 0f), new Vector3(0.03f, Height, Size)),
                (new Vector3(0f, Height, 0f), new Vector3(Size, 0.03f, Size)),
            })
                panes.Add(Shapes.Rounded("Pane", t, pos, size, 0.01f, glass).transform);
            // The glowing edges of the cube.
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                Shapes.Rounded("Edge", t, new Vector3(sx * h, Height * 0.5f, sz * h), new Vector3(0.05f, Height, 0.05f), 0.01f, frame);
            foreach (float y in new[] { 0.12f, Height })
            {
                Shapes.Rounded("EdgeX", t, new Vector3(0f, y, -h), new Vector3(Size, 0.05f, 0.05f), 0.01f, frame);
                Shapes.Rounded("EdgeX", t, new Vector3(0f, y, h), new Vector3(Size, 0.05f, 0.05f), 0.01f, frame);
                Shapes.Rounded("EdgeZ", t, new Vector3(-h, y, 0f), new Vector3(0.05f, 0.05f, Size), 0.01f, frame);
                Shapes.Rounded("EdgeZ", t, new Vector3(h, y, 0f), new Vector3(0.05f, 0.05f, Size), 0.01f, frame);
            }
        }

        /// <summary>Breaks the cell: a white crack flash, then the shards fly.</summary>
        public void Shatter()
        {
            if (crack >= 0f) return;
            crack = 0f;
        }

        private void Burst()
        {
            foreach (var p in panes) p.gameObject.SetActive(false);
            foreach (Transform c in transform) if (c.name.StartsWith("Edge")) c.gameObject.SetActive(false);
            shardMat = MaterialFactory.CreateTransparent(new Color(0.6f, 0.9f, 1f, 0.7f), new Color(0.6f, 1.6f, 2.4f));
            OwnedMaterials.Own(gameObject, shardMat); // (made after Start: owned here)
            var rng = new System.Random(Mathf.RoundToInt(transform.position.x * 31f + transform.position.z * 17f));
            float R() => (float)rng.NextDouble();
            for (int i = 0; i < 34; i++)
            {
                var dir = new Vector3(R() * 2f - 1f, 0f, R() * 2f - 1f).normalized;
                var at = new Vector3(dir.x * Size * 0.5f, 0.2f + R() * Height, dir.z * Size * 0.5f);
                var s = Shapes.Rounded("Shard", transform, at, new Vector3(0.06f + R() * 0.16f, 0.06f + R() * 0.2f, 0.02f), 0.005f, shardMat).transform;
                s.localRotation = Quaternion.Euler(R() * 360f, R() * 360f, R() * 360f);
                shards.Add((s, dir * (2.5f + R() * 3f) + Vector3.up * (2f + R() * 3f), new Vector3(R(), R(), R()) * 720f));
            }
            shardT = 0f;
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (crack < 0f)
            {
                // Humming while foi is inside.
                float k = 0.85f + Mathf.Sin(time * 3f) * 0.15f;
                MaterialFactory.SetColors(glass, new Color(0.45f, 0.85f, 1f, 0.2f + 0.05f * k), new Color(0.2f, 0.6f, 1f) * k);
                return;
            }
            if (shardT < 0f)
            {
                // The crack: the glass flashes white for a moment, then gives way.
                crack += Time.deltaTime / 0.18f;
                float f = Mathf.Clamp01(crack);
                MaterialFactory.SetColors(glass, Color.Lerp(new Color(0.45f, 0.85f, 1f, 0.25f), new Color(1f, 1f, 1f, 0.7f), f), Color.Lerp(new Color(0.2f, 0.6f, 1f), new Color(3f, 3f, 3f), f));
                if (crack >= 1f) Burst();
                return;
            }
            shardT += Time.deltaTime;
            float dt = Time.deltaTime;
            for (int i = 0; i < shards.Count; i++)
            {
                var (t, v, spin) = shards[i];
                v += Physics.gravity * dt;
                var lp = t.localPosition + v * dt / transform.localScale.x;
                if (lp.y < 0.05f) { lp.y = 0.05f; v = new Vector3(v.x * 0.4f, -v.y * 0.25f, v.z * 0.4f); spin *= 0.5f; }
                t.localPosition = lp;
                t.Rotate(spin * dt);
                shards[i] = (t, v, spin);
            }
            float a = Mathf.Clamp01(1f - (shardT - 0.8f) / 0.8f);
            MaterialFactory.SetColors(shardMat, new Color(0.6f, 0.9f, 1f, 0.7f * a), new Color(0.6f, 1.6f, 2.4f) * a);
            if (shardT > 1.7f) Destroy(gameObject);
        }
    }
}
