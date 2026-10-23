using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A glowing ring on the floor under a fighter, in its side's colour (the duel: foi's own blue, the rival's red),
    /// breathing softly. It follows a point given each frame and hides when given none.
    /// </summary>
    public class FootRing : MonoBehaviour
    {
        private static Texture2D ringTex;
        private static Mesh quad;
        private Material mat;
        /// <summary>How bright the ring is (clear, but never brighter than foi over it).</summary>
        private const float Glow = 1.9f;
        private Color colour;

        public static FootRing Make(Color colour, float size = 1.7f, bool lit = false)
        {
            var go = new GameObject("FootRing");
            go.transform.localScale = new Vector3(size, 1f, size);
            var r = go.AddComponent<FootRing>();
            r.colour = colour;
            r.mat = new Material(Weather.AddMaterial);
            r.mat.SetTexture("_BaseMap", Ring()); r.mat.SetTexture("_MainTex", Ring());
            r.mat.SetColor("_BaseColor", colour * Glow);
            r.mat.renderQueue = 3100; // (over the floor and the platforms' glow)
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = r.mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (lit)
            {
                // a soft white light over foi (over its head, a little in front): foi shines, never left dull by the ring
                var l = new GameObject("FoiLight").AddComponent<Light>();
                l.transform.SetParent(go.transform, false);
                l.transform.localPosition = new Vector3(0f, 2.8f, 0f);
                l.type = LightType.Point; l.color = new Color(1f, 0.97f, 0.92f); l.intensity = 1.5f; l.range = 4.5f; l.shadows = LightShadows.None;
            }
            go.SetActive(false);
            return r;
        }

        /// <summary>Under this point this frame (null: hidden).</summary>
        public void Follow(Vector3? at)
        {
            if (!at.HasValue) { if (gameObject.activeSelf) gameObject.SetActive(false); return; }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.position = at.Value + Vector3.up * 0.06f;
            float k = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f);
            mat.SetColor("_BaseColor", colour * Glow * k);
        }

        private void OnDestroy() { if (mat != null) Destroy(mat); }

        /// <summary>A bright ring with a faint glow inside it (white: the material gives the colour).</summary>
        private static Texture2D Ring()
        {
            if (ringTex != null) return ringTex;
            const int N = 128;
            ringTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "FootRing", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N * 0.5f, N * 0.5f)) / (N * 0.5f);
                    float ring = Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.09f, 2f));
                    float inner = d < 0.8f ? 0.26f * (1f - d / 0.8f * 0.5f) : 0f;
                    float a = Mathf.Clamp01(ring + inner) * (d < 1f ? 1f : 0f);
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            ringTex.SetPixels32(px);
            ringTex.Apply(true, true);
            return ringTex;
        }

        /// <summary>A flat square, 1 across, facing up.</summary>
        private static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "FootRingQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            quad.colors = new[] { Color.white, Color.white, Color.white, Color.white }; // (the particle shader multiplies by them)
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }
    }
}
