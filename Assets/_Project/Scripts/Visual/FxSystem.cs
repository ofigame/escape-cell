using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>Cheap neon shard bursts made from pooled tiny cubes (no particle assets needed).</summary>
    public class FxSystem : MonoBehaviour
    {
        private class Shard
        {
            public Transform transform;
            public Material material;
            public Vector3 velocity;
            public Vector3 spin;
            public float life;
            public float maxLife;
            public float size;
        }

        private readonly List<Shard> active = new List<Shard>();
        private readonly Stack<Shard> pool = new Stack<Shard>();

        public void Burst(Vector3 position, Color color, Color glow, int count, float speed = 4f)
        {
            for (int i = 0; i < count; i++)
            {
                var shard = NextShard();
                shard.transform.gameObject.SetActive(true);
                shard.transform.position = position + Random.insideUnitSphere * 0.2f;
                shard.transform.rotation = Random.rotation;
                MaterialFactory.SetColors(shard.material, color, glow);

                var dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.4f;
                shard.velocity = dir.normalized * speed * Random.Range(0.5f, 1.2f);
                shard.spin = Random.insideUnitSphere * 720f;
                shard.maxLife = shard.life = Random.Range(0.4f, 0.8f);
                shard.size = Random.Range(0.06f, 0.14f);
                active.Add(shard);
            }
        }

        /// <summary>A burst thrown along <paramref name="along"/> (a blow's sparks flying on the way it struck): a cone about
        /// it, a little upward.</summary>
        public void BurstAlong(Vector3 position, Vector3 along, Color color, Color glow, int count, float speed = 4f)
        {
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                var shard = NextShard();
                shard.transform.gameObject.SetActive(true);
                shard.transform.position = position + Random.insideUnitSphere * 0.08f;
                shard.transform.rotation = Random.rotation;
                MaterialFactory.SetColors(shard.material, color, glow);
                var dir = along + Random.insideUnitSphere * 0.55f + Vector3.up * 0.25f;
                shard.velocity = dir.normalized * speed * Random.Range(0.6f, 1.2f);
                shard.spin = Random.insideUnitSphere * 720f;
                shard.maxLife = shard.life = Random.Range(0.3f, 0.6f);
                shard.size = Random.Range(0.06f, 0.13f);
                active.Add(shard);
            }
        }

        /// <summary>A low, outward ring of puffs: footsteps, landings, blocks hitting the floor.</summary>
        public void Dust(Vector3 position, Color color, int count, float speed = 1.6f)
        {
            for (int i = 0; i < count; i++)
            {
                var shard = NextShard();
                shard.transform.gameObject.SetActive(true);
                float a = (i + Random.value * 0.5f) * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                shard.transform.position = position + dir * 0.18f;
                shard.transform.rotation = Random.rotation;
                MaterialFactory.SetColors(shard.material, color, color * 0.4f);

                shard.velocity = dir * speed * Random.Range(0.7f, 1.1f) + Vector3.up * Random.Range(0.8f, 1.6f);
                shard.spin = Random.insideUnitSphere * 360f;
                shard.maxLife = shard.life = Random.Range(0.25f, 0.4f);
                shard.size = Random.Range(0.05f, 0.09f);
                active.Add(shard);
            }
        }

        /// <summary>The most shards there ever are: past it the oldest flying one is taken (a big blast stays cheap: every shard
        /// is a draw call and a material).</summary>
        private const int MaxShards = 180;
        private int made;

        private Shard NextShard()
        {
            if (pool.Count > 0) return pool.Pop();
            if (made < MaxShards || active.Count == 0) { made++; return CreateShard(); }
            var oldest = active[0];
            active.RemoveAt(0);
            return oldest;
        }

        private Shard CreateShard()
        {
            var go = Shapes.Cube("Shard", transform, Vector3.zero, Vector3.one * 0.1f, null);
            var material = MaterialFactory.Create(Color.white, Color.white);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return new Shard { transform = go.transform, material = material };
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var s = active[i];
                s.life -= dt;
                if (s.life <= 0f)
                {
                    s.transform.gameObject.SetActive(false);
                    active.RemoveAt(i);
                    pool.Push(s);
                    continue;
                }

                s.velocity += Physics.gravity * dt;
                s.transform.position += s.velocity * dt;
                s.transform.Rotate(s.spin * dt);
                s.transform.localScale = Vector3.one * (s.size * (s.life / s.maxLife));
            }
        }
    }
}
