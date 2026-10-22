using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The menu's night jungle (round the platform, foi and the beam): a deep blue glow like the logo's, a waterfall
    /// falling from dark cliffs behind, mist where it lands, vines hanging in from the top of the view with glowing
    /// leaves, glowing plants and mushrooms along the edges, all softly swaying; a world lit only by its own light.
    /// </summary>
    public class LobbyJungle : MonoBehaviour
    {
        private Material water, mist;
        private readonly List<(Transform t, Quaternion rest, float phase, float amount)> sway = new List<(Transform, Quaternion, float, float)>();
        private readonly List<(Material m, Color c, float phase)> glows = new List<(Material, Color, float)>();
        private float time;

        /// <summary>An empty one under <paramref name="parent"/>: its mushrooms and vines placed by the caller (they sway and glow).</summary>
        public static LobbyJungle Decor(Transform parent)
        {
            var go = new GameObject("ForestDecor");
            go.transform.SetParent(parent, false);
            return go.AddComponent<LobbyJungle>();
        }

        public static LobbyJungle Build(Transform stage)
        {
            var go = new GameObject("Jungle");
            go.transform.SetParent(stage, false);
            var j = go.AddComponent<LobbyJungle>();
            j.Make();
            return j;
        }

        private static readonly Color Cyan = new Color(0.35f, 0.95f, 1f), Teal = new Color(0.2f, 0.85f, 0.75f), Violet = new Color(0.7f, 0.4f, 1f), Pink = new Color(1f, 0.45f, 0.85f);

        public Material Glow(Color c, float strength)
        {
            var m = MaterialFactory.Create(c * 0.5f, c * strength);
            glows.Add((m, c * strength, Random.value * 6f));
            return m;
        }

        /// <summary>Where the painted forest stands: near enough that its own ground meets the floor.</summary>
        private const float FallsX = 2.6f; // (to the right of the beam, where it shows)
        private const float ForestZ = 14f, ForestH = 12.5f, ForestY = -0.42f + ForestH * 0.5f - ForestH * 0.1f;

        private void Make()
        {
            Random.InitState(11);
            var t = transform;
            // the painted forest filling the view behind (portrait: wider than any phone, cropped at the sides)
            Shapes.Primitive(PrimitiveType.Quad, "Forest", t, new Vector3(0f, ForestY, ForestZ), new Vector3(ForestH * 0.62f, ForestH, 1f), MaterialFactory.CreateUnlit(ForestPainting.Portrait)); // (its ground band meets the floor)

            // (no waterfall: the forest alone)
            // (no mist or pool quads: their soft picture showed as squares)

            // glowing specks on the dark ground
            var speck = Glow(Cyan, 2.5f);
            for (int i = 0; i < 40; i++)
                Shapes.Primitive(PrimitiveType.Sphere, "Speck", t, new Vector3(Random.Range(-5f, 5f), -0.39f, Random.Range(-2f, ForestZ - 1f)), Vector3.one * Random.Range(0.03f, 0.07f), speck);
        }

        /// <summary>A vine from <paramref name="top"/> down: a chain of short pieces (each swaying), leaves and glowing buds.</summary>
        public void Vine(Vector3 top, float length, Material stem, Material leaf, Material[] bud, float thick = 1f)
        {
            Transform parent = transform;
            var root = new GameObject("Vine").transform;
            root.SetParent(transform, false);
            root.localPosition = top;
            root.localScale = Vector3.one * thick;
            length /= thick;
            parent = root;
            int n = Mathf.Max(3, Mathf.RoundToInt(length / 0.35f));
            float seg = length / n, phase = Random.value * 6f;
            for (int i = 0; i < n; i++)
            {
                var joint = new GameObject("Joint").transform;
                joint.SetParent(parent, false);
                joint.localPosition = i == 0 ? Vector3.zero : Vector3.down * seg;
                joint.localRotation = Quaternion.Euler(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                sway.Add((joint, joint.localRotation, phase + i * 0.35f, 2.5f + i * 0.4f));
                Shapes.Primitive(PrimitiveType.Cylinder, "Stem", joint, Vector3.down * seg * 0.5f, new Vector3(0.035f, seg * 0.5f, 0.035f), stem);
                if (i % 2 == 1)
                {
                    float side = Random.value < 0.5f ? -1f : 1f;
                    var l = Shapes.Primitive(PrimitiveType.Sphere, "Leaf", joint, new Vector3(side * 0.09f, -seg * 0.4f, 0f), new Vector3(0.16f, 0.05f, 0.1f), leaf).transform;
                    l.localRotation = Quaternion.Euler(0f, 0f, side * 35f);
                }
                if (Random.value < 0.35f)
                    Shapes.Primitive(PrimitiveType.Sphere, "Bud", joint, new Vector3(Random.Range(-0.05f, 0.05f), -seg * 0.6f, -0.04f), Vector3.one * Random.Range(0.05f, 0.09f), bud[Random.Range(0, bud.Length)]);
                parent = joint;
            }
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", parent, Vector3.down * seg, Vector3.one * 0.1f, bud[Random.Range(0, bud.Length)]);
        }

        /// <summary>A glowing mushroom: a pale bent stalk, a wide domed cap glowing in its colour (brightest underneath),
        /// pale spots on top.</summary>
        public void Mushroom(Vector3 at, float height, Color colour)
        {
            var root = new GameObject("Mushroom").transform;
            root.SetParent(transform, false);
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            sway.Add((root, root.localRotation, Random.value * 6f, 1.2f));
            var stalk = MaterialFactory.Create(Color.Lerp(new Color(0.8f, 0.85f, 0.9f), colour, 0.25f), colour * 0.35f);
            float w = height * 0.16f;
            Shapes.Primitive(PrimitiveType.Cylinder, "Stalk", root, Vector3.up * height * 0.45f, new Vector3(w, height * 0.45f, w), stalk);
            Shapes.Primitive(PrimitiveType.Sphere, "Foot", root, Vector3.up * w * 0.4f, new Vector3(w * 1.8f, w, w * 1.8f), stalk);
            float r = height * 0.62f;
            Shapes.Primitive(PrimitiveType.Sphere, "Gills", root, Vector3.up * height * 0.9f, new Vector3(r * 1.9f, r * 0.25f, r * 1.9f), Glow(colour, 2.4f));
            Shapes.Primitive(PrimitiveType.Sphere, "Cap", root, Vector3.up * (height * 0.93f), new Vector3(r * 2f, r * 0.85f, r * 2f), Glow(colour, 1.3f));
            var dot = Glow(Color.Lerp(colour, Color.white, 0.7f), 1.6f);
            for (int i = 0; i < 7; i++)
            {
                float a = Random.value * Mathf.PI * 2f, d = Random.Range(0.2f, 0.75f);
                var p = new Vector3(Mathf.Cos(a) * d * r, height * 0.93f + Mathf.Sqrt(Mathf.Max(0f, 1f - d * d)) * r * 0.42f, Mathf.Sin(a) * d * r);
                Shapes.Primitive(PrimitiveType.Sphere, "Spot", root, p, new Vector3(r * 0.16f, r * 0.05f, r * 0.16f), dot);
            }
            if (height > 0.6f)
            {
                var lamp = new GameObject("Glow").AddComponent<Light>();
                lamp.transform.SetParent(root, false);
                lamp.transform.localPosition = Vector3.up * height * 0.7f;
                lamp.type = LightType.Point; lamp.color = colour; lamp.intensity = 1.2f; lamp.range = height * 3f; lamp.shadows = LightShadows.None;
            }
        }

        /// <summary>A glowing plant: a fan of long thin fronds, a bud at each tip.</summary>
        private void Plant(Vector3 at, Material frond, Material bud)
        {
            var root = new GameObject("Plant").transform;
            root.SetParent(transform, false);
            root.localPosition = at;
            sway.Add((root, Quaternion.identity, Random.value * 6f, 3f));
            int n = 4 + Random.Range(0, 3);
            float h = 0.6f + Random.value * 0.9f;
            for (int i = 0; i < n; i++)
            {
                var f = new GameObject("Frond").transform;
                f.SetParent(root, false);
                f.localRotation = Quaternion.Euler(Random.Range(-35f, 35f), i * 360f / n, 0f) * Quaternion.Euler(Random.Range(10f, 30f), 0f, 0f);
                Shapes.Primitive(PrimitiveType.Cube, "Blade", f, Vector3.up * h * 0.5f, new Vector3(0.05f, h, 0.015f), frond);
                Shapes.Primitive(PrimitiveType.Sphere, "Bud", f, Vector3.up * h, Vector3.one * 0.07f, bud);
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            time += dt;
            if (water != null) water.SetTextureOffset("_BaseMap", new Vector2(0f, time * 0.9f));
            foreach (var s in sway)
                if (s.t != null) s.t.localRotation = s.rest * Quaternion.Euler(Mathf.Sin(time * 0.9f + s.phase) * s.amount * 0.4f, 0f, Mathf.Sin(time * 0.7f + s.phase) * s.amount);
            foreach (var g in glows)
                MaterialFactory.SetColors(g.m, g.c * 0.25f, g.c * (0.75f + 0.25f * Mathf.Sin(time * 1.3f + g.phase)));
        }

        /// <summary>Streaming water: pale streaks of different widths and brightness, soft at the sides (tiles up and down).</summary>
        private static Texture2D WaterTexture()
        {
            const int W = 64, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float streak = Mathf.PerlinNoise(u * 18f, v * 2.5f * 4f) * 0.6f + Mathf.PerlinNoise(u * 40f + 7f, v * 6f) * 0.4f;
                    float edge = Mathf.Sin(u * Mathf.PI);
                    float a = Mathf.Clamp01((streak - 0.3f) * 1.8f) * Mathf.Pow(edge, 0.6f);
                    float b = 0.75f + streak * 0.25f;
                    px[y * W + x] = new Color(b, b, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>The far view: a deep navy night, the logo's blue glowing round the falls, dark tree tops, a few stars.</summary>
        private static Texture2D Backdrop()
        {
            const int W = 256, H = 384;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color[W * H];
            var top = new Color(0.01f, 0.025f, 0.07f);
            var mid = new Color(0.04f, 0.22f, 0.45f);   // the logo's blue
            var glow = new Color(0.12f, 0.55f, 0.85f);
            var trees = new Color(0.008f, 0.03f, 0.05f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    var c = Color.Lerp(mid, top, Mathf.SmoothStep(0.3f, 0.95f, v));
                    float dx = (u - 0.5f) * 2.2f, dy = (v - 0.42f) * 1.6f;
                    c += glow * Mathf.Exp(-(dx * dx + dy * dy) * 2.2f) * 0.8f;
                    // tree tops: a ragged dark line low down, a second fainter one behind
                    float line1 = 0.3f + Mathf.PerlinNoise(u * 9f, 1f) * 0.12f + Mathf.PerlinNoise(u * 30f, 3f) * 0.04f;
                    float line2 = 0.4f + Mathf.PerlinNoise(u * 6f, 5f) * 0.12f;
                    if (v < line2) c = Color.Lerp(c, trees * 2.5f + mid * 0.15f, 0.7f);
                    if (v < line1) c = trees;
                    // stars, only in the dark sky
                    if (v > 0.6f && Mathf.PerlinNoise(u * 120f, v * 120f) > 0.86f) c += new Color(0.5f, 0.6f, 0.8f) * (v - 0.6f) * 2f;
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
