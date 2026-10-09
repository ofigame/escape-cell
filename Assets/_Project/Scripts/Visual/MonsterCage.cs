using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// vanG's cage: heavy corner posts and glowing bars all round a block of tiles. It
    /// stays shut while the crowd is on the floor; when the last of them falls the bars sink into the floor.
    /// </summary>
    public class MonsterCage : MonoBehaviour
    {
        private readonly List<Transform> bars = new List<Transform>();
        private readonly List<Vector3> barHome = new List<Vector3>();
        private Material glow;
        private float openT = -1f;

        /// <param name="min">World centre of the cage's lowest-x, lowest-z tile.</param>
        /// <param name="max">World centre of its highest-x, highest-z tile.</param>
        public static MonsterCage Create(Vector3 min, Vector3 max, Color accent)
        {
            var go = new GameObject("MonsterCage");
            var c = go.AddComponent<MonsterCage>();
            c.Build(min, max, accent);
            return c;
        }

        private void Build(Vector3 min, Vector3 max, Color accent)
        {
            var post = MaterialFactory.Create(new Color(0.18f, 0.16f, 0.24f), Color.black);
            glow = MaterialFactory.Create(new Color(0.75f, 0.35f, 1f), new Color(1.8f, 0.6f, 2.6f));
            var trim = MaterialFactory.Create(accent, accent * 1.2f);
            float y = min.y;
            float x0 = min.x - 0.5f, x1 = max.x + 0.5f, z0 = min.z - 0.5f, z1 = max.z + 0.5f;
            const float height = 2.2f;
            foreach (var (x, z) in new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1) })
            {
                Shapes.Rounded("Post", transform, new Vector3(x, y + height * 0.5f, z), new Vector3(0.2f, height, 0.2f), 0.05f, post);
                Shapes.Rounded("Cap", transform, new Vector3(x, y + height + 0.08f, z), new Vector3(0.28f, 0.16f, 0.28f), 0.05f, trim);
            }
            // Top frame.
            Shapes.Rounded("Top", transform, new Vector3((x0 + x1) * 0.5f, y + height, z0), new Vector3(x1 - x0, 0.12f, 0.12f), 0.04f, post);
            Shapes.Rounded("Top", transform, new Vector3((x0 + x1) * 0.5f, y + height, z1), new Vector3(x1 - x0, 0.12f, 0.12f), 0.04f, post);
            Shapes.Rounded("Top", transform, new Vector3(x0, y + height, (z0 + z1) * 0.5f), new Vector3(0.12f, 0.12f, z1 - z0), 0.04f, post);
            Shapes.Rounded("Top", transform, new Vector3(x1, y + height, (z0 + z1) * 0.5f), new Vector3(0.12f, 0.12f, z1 - z0), 0.04f, post);
            // Bars along every side (they sink away when the cage opens).
            void Side(Vector3 a, Vector3 b)
            {
                int n = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.32f));
                for (int i = 1; i < n; i++)
                {
                    var at = Vector3.Lerp(a, b, i / (float)n);
                    var bar = Shapes.Rounded("Bar", transform, new Vector3(at.x, y + height * 0.5f, at.z), new Vector3(0.06f, height - 0.1f, 0.06f), 0.025f, glow).transform;
                    bars.Add(bar);
                    barHome.Add(bar.localPosition);
                }
            }
            Side(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0));
            Side(new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1));
            Side(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1));
            Side(new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1));
        }

        /// <summary>The crowd is beaten: the bars sink into the floor.</summary>
        public void Open()
        {
            if (openT >= 0f) return;
            openT = 0f;
            MaterialFactory.SetColors(glow, new Color(1f, 0.4f, 0.35f), new Color(2.6f, 0.5f, 0.4f));
        }

        private void Update()
        {
            if (openT < 0f || openT >= 1f) return;
            openT = Mathf.Min(1f, openT + Time.deltaTime / 0.8f);
            float k = openT * openT;
            for (int i = 0; i < bars.Count; i++)
            {
                bars[i].localPosition = barHome[i] + Vector3.down * 2.2f * k;
                bars[i].gameObject.SetActive(openT < 1f);
            }
        }
    }
}
