using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The gate on a bridge between two fortress stages: two stone pylons with a humming energy wall between them,
    /// shut until the stage before is beaten. <see cref="Open"/> flashes the wall white and lets it drop into the floor.
    /// </summary>
    public class StageGate : MonoBehaviour
    {
        private Material wall;
        private Transform field;
        private Color tint;
        private float time, openT = -1f;

        public static StageGate Create(Vector3 tile, Color accent)
        {
            var go = new GameObject("StageGate");
            go.transform.position = tile;
            var g = go.AddComponent<StageGate>();
            g.Build(accent);
            return g;
        }

        private void Build(Color accent)
        {
            var t = transform;
            tint = Color.Lerp(accent, new Color(1f, 0.45f, 0.3f), 0.5f);
            var stone = MaterialFactory.CreateSurface(new Color(0.32f, 0.3f, 0.33f), Color.black, 0.2f, 0.35f);
            var glow = MaterialFactory.Create(tint, tint * 2.4f);
            foreach (float x in new[] { -0.56f, 0.56f })
            {
                Shapes.Rounded("Pylon", t, new Vector3(x, 0.75f, 0f), new Vector3(0.22f, 1.5f, 0.3f), 0.04f, stone);
                Shapes.Rounded("PylonCap", t, new Vector3(x, 1.55f, 0f), new Vector3(0.28f, 0.12f, 0.36f), 0.03f, stone);
                Shapes.Rounded("PylonGlow", t, new Vector3(x * 0.84f, 0.75f, 0f), new Vector3(0.04f, 1.3f, 0.32f), 0.01f, glow);
            }
            wall = MaterialFactory.CreateTransparent(new Color(tint.r, tint.g, tint.b, 0.35f), tint * 1.6f);
            field = new GameObject("Field").transform;
            field.SetParent(t, false);
            Shapes.Rounded("Wall", field, new Vector3(0f, 0.7f, 0f), new Vector3(0.9f, 1.3f, 0.04f), 0.01f, wall);
            for (int i = 0; i < 4; i++)
                Shapes.Rounded("Bar", field, new Vector3(0f, 0.2f + i * 0.34f, 0f), new Vector3(0.9f, 0.03f, 0.06f), 0.01f, glow);
        }

        public void Open()
        {
            if (openT < 0f) openT = 0f;
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (openT < 0f)
            {
                float k = 0.8f + Mathf.Sin(time * 5f) * 0.2f;
                MaterialFactory.SetColors(wall, new Color(tint.r, tint.g, tint.b, 0.28f + 0.1f * k), tint * 1.6f * k);
                return;
            }
            openT += Time.deltaTime;
            float f = Mathf.Clamp01(openT / 0.25f);
            float drop = Mathf.Clamp01((openT - 0.2f) / 0.5f);
            MaterialFactory.SetColors(wall, Color.Lerp(new Color(tint.r, tint.g, tint.b, 0.4f), new Color(1f, 1f, 1f, 0.8f), f) * new Color(1f, 1f, 1f, 1f - drop),
                Color.Lerp(tint * 1.6f, new Color(3f, 3f, 3f), f) * (1f - drop));
            field.localScale = new Vector3(1f, 1f - drop, 1f);
            if (drop >= 1f) field.gameObject.SetActive(false);
        }
    }
}
