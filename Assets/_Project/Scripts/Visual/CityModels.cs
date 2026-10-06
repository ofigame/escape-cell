using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Builds the city's pieces from rounded boxes, spheres and cylinders, in the colours of the world they come from.
    /// A model is centred on its footprint (w x h cells) with y = 0 on the tile surface. Lit parts (windows, lamps,
    /// signs) go into <see cref="Lights"/> so the scene can brighten them in the evening.
    /// </summary>
    public class CityModels
    {
        public readonly List<(Material m, Color color, Color glow)> Lights = new List<(Material, Color, Color)>();
        private Transform t;
        private WorldTheme theme;

        public static CityModels Build(CityPiece piece, Transform parent)
        {
            // Fronts face -z: the city camera looks in from the plot's -x/-z corner, like the main game's view.
            var front = new GameObject("Front").transform;
            front.SetParent(parent, false);
            front.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var b = new CityModels { t = front, theme = WorldTheme.ForWorld(piece.world) };
            b.Make(piece);
            return b;
        }

        // ---------- Helpers ----------

        private static Material M(Color c, Color glow = default) => MaterialFactory.Create(c, glow);
        private static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        private Material L(Color c, Color glow)
        {
            var m = MaterialFactory.Create(c, glow * 0.35f);
            Lights.Add((m, c, glow));
            return m;
        }

        private Transform Box(Vector3 pos, Vector3 size, Material m, float r = 0.04f) => Shapes.Rounded("Part", t, pos, size, Mathf.Min(r, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f), m).transform;
        private Transform Ball(Vector3 pos, Vector3 size, Material m) => Shapes.Primitive(PrimitiveType.Sphere, "Part", t, pos, size, m).transform;
        /// <summary>A cylinder <paramref name="height"/> tall standing on <paramref name="pos"/>.</summary>
        private Transform Cyl(Vector3 pos, float diameter, float height, Material m) => Shapes.Primitive(PrimitiveType.Cylinder, "Part", t, pos + Vector3.up * height * 0.5f, new Vector3(diameter, height * 0.5f, diameter), m).transform;

        private Transform Tilt(Transform p, float x, float y, float z) { p.localRotation = Quaternion.Euler(x, y, z); return p; }

        /// <summary>A pitched roof: two slabs meeting at a ridge along x.</summary>
        private void Roof(Vector3 at, float width, float depth, float rise, Material m)
        {
            float half = depth * 0.5f;
            float slope = Mathf.Sqrt(half * half + rise * rise);
            float angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
            foreach (float side in new[] { -1f, 1f })
                Tilt(Box(at + new Vector3(0f, rise * 0.5f, side * half * 0.5f), new Vector3(width, 0.07f, slope + 0.05f), m, 0.03f), side * angle, 0f, 0f);
        }

        private void Windows(Vector3 center, float width, int count, float y, float z, Material m)
        {
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * (width / count);
                Box(center + new Vector3(x, y, z), new Vector3(0.16f, 0.16f, 0.03f), m, 0.02f);
            }
        }

        private Material Accent => M(theme.accent, theme.accent * 0.25f);
        private Material Wall => M(Color.Lerp(theme.tileTop, Color.white, 0.2f));
        private Material Trim => M(theme.pillar);
        private Material Warm => L(new Color(1f, 0.88f, 0.55f), new Color(2.2f, 1.7f, 0.7f));
        private static Material Wood => M(C("#9A6A45"));
        private static Material Leaf => M(C("#5CC46E"), new Color(0.05f, 0.18f, 0.06f));
        private static Material Stone => M(C("#B8B8C8"));
        private static Material Metal => M(C("#8E92A8"));
        private static Material Gold => M(C("#FFD25A"), new Color(0.55f, 0.38f, 0.05f));

        // ---------- Models ----------

        private void Make(CityPiece p)
        {
            switch (p.id)
            {
                case "house": House(Wall, M(C("#E06A5A")), 0.7f); break;
                case "wall": Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 0.22f), Wall); Box(new Vector3(0f, 0.53f, 0f), new Vector3(0.94f, 0.07f, 0.26f), Trim); break;
                case "floor": Box(new Vector3(0f, 0.025f, 0f), new Vector3(0.92f, 0.05f, 0.92f), M(Color.Lerp(theme.slab, Color.white, 0.3f))); for (int i = 0; i < 4; i++) Box(new Vector3((i % 2 - 0.5f) * 0.44f, 0.055f, (i / 2 - 0.5f) * 0.44f), new Vector3(0.4f, 0.02f, 0.4f), M(theme.tileTop)); break;
                case "door":
                    foreach (float s in new[] { -1f, 1f }) Box(new Vector3(s * 0.36f, 0.42f, 0f), new Vector3(0.16f, 0.84f, 0.22f), Wall);
                    Box(new Vector3(0f, 0.86f, 0f), new Vector3(0.9f, 0.12f, 0.24f), Trim);
                    Box(new Vector3(0f, 0.36f, 0f), new Vector3(0.5f, 0.72f, 0.06f), Wood);
                    Ball(new Vector3(0.14f, 0.36f, 0.05f), Vector3.one * 0.06f, Gold);
                    break;
                case "window":
                    Box(new Vector3(0f, 0.32f, 0f), new Vector3(0.9f, 0.64f, 0.22f), Wall);
                    Box(new Vector3(0f, 0.38f, 0.1f), new Vector3(0.44f, 0.34f, 0.04f), Warm);
                    Box(new Vector3(0f, 0.38f, 0.125f), new Vector3(0.04f, 0.34f, 0.02f), Trim);
                    Box(new Vector3(0f, 0.19f, 0.14f), new Vector3(0.5f, 0.05f, 0.08f), Trim);
                    break;
                case "bush": Ball(new Vector3(0f, 0.22f, 0f), new Vector3(0.6f, 0.45f, 0.6f), Leaf); Ball(new Vector3(0.15f, 0.36f, 0.08f), Vector3.one * 0.3f, Leaf); break;
                case "flowers":
                    Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.7f, 0.1f, 0.7f), M(C("#7A5238")));
                    for (int i = 0; i < 6; i++)
                    {
                        var pos = new Vector3((i % 3 - 1) * 0.2f, 0.2f, (i / 3 - 0.5f) * 0.25f);
                        Box(pos - Vector3.up * 0.06f, new Vector3(0.03f, 0.14f, 0.03f), Leaf);
                        Ball(pos + Vector3.up * 0.03f, Vector3.one * 0.12f, M(i % 2 == 0 ? C("#FF7FA8") : C("#FFE066"), new Color(0.3f, 0.15f, 0.1f)));
                    }
                    break;
                case "antenna":
                    Box(new Vector3(0f, 0.08f, 0f), new Vector3(0.5f, 0.16f, 0.5f), Trim);
                    Box(new Vector3(0f, 0.8f, 0f), new Vector3(0.08f, 1.4f, 0.08f), Metal);
                    Tilt(Ball(new Vector3(0.12f, 1.25f, 0f), new Vector3(0.5f, 0.5f, 0.12f), M(Color.white)), 0f, 60f, 0f);
                    Ball(new Vector3(0f, 1.55f, 0f), Vector3.one * 0.14f, L(theme.accent, theme.accent * 2.2f));
                    break;

                case "mine":
                    // A rocky hill with a timber entrance, rails and a cart of gold.
                    Ball(new Vector3(0f, 0.25f, -0.25f), new Vector3(1.7f, 0.9f, 1.2f), M(C("#8A7A6E")));
                    Box(new Vector3(0f, 0.32f, 0.3f), new Vector3(0.7f, 0.64f, 0.12f), M(C("#2A2230")));
                    foreach (float s in new[] { -1f, 1f }) Box(new Vector3(s * 0.38f, 0.36f, 0.36f), new Vector3(0.1f, 0.72f, 0.12f), Wood);
                    Box(new Vector3(0f, 0.74f, 0.36f), new Vector3(0.9f, 0.1f, 0.14f), Wood);
                    foreach (float s in new[] { -0.15f, 0.15f }) Box(new Vector3(s, 0.02f, 0.65f), new Vector3(0.04f, 0.03f, 0.7f), Metal);
                    Box(new Vector3(0f, 0.18f, 0.7f), new Vector3(0.42f, 0.22f, 0.36f), Wood);
                    for (int i = 0; i < 3; i++) Ball(new Vector3((i - 1) * 0.11f, 0.33f, 0.7f), Vector3.one * 0.13f, Gold);
                    Ball(new Vector3(0f, 0.95f, 0.35f), Vector3.one * 0.12f, Warm);
                    break;
                case "porch":
                    Box(new Vector3(0f, 0.06f, 0f), new Vector3(1.85f, 0.12f, 0.85f), Wood);
                    foreach (float s in new[] { -0.82f, 0.82f }) Box(new Vector3(s, 0.45f, 0.32f), new Vector3(0.08f, 0.8f, 0.08f), Wood);
                    Box(new Vector3(0f, 0.86f, 0.05f), new Vector3(1.9f, 0.06f, 0.8f), M(theme.accent));
                    Box(new Vector3(-0.3f, 0.3f, -0.1f), new Vector3(0.4f, 0.06f, 0.3f), M(C("#FFFFFF")));
                    break;
                case "hammock":
                    foreach (float s in new[] { -0.8f, 0.8f }) { Cyl(new Vector3(s, 0f, 0f), 0.14f, 1.0f, Wood); Ball(new Vector3(s, 1.15f, 0f), new Vector3(0.45f, 0.3f, 0.45f), Leaf); }
                    Box(new Vector3(0f, 0.42f, 0f), new Vector3(1.4f, 0.05f, 0.4f), M(C("#F5D06A"), new Color(0.2f, 0.15f, 0.02f)));
                    break;
                case "umbrella":
                    Cyl(Vector3.zero, 0.05f, 0.9f, M(Color.white));
                    Tilt(Cyl(new Vector3(0f, 0.85f, 0f), 0.85f, 0.06f, M(theme.accent)), 0f, 0f, 0f);
                    Ball(new Vector3(0f, 0.9f, 0f), new Vector3(0.84f, 0.18f, 0.84f), M(C("#FF8F70")));
                    Box(new Vector3(0.2f, 0.06f, 0.1f), new Vector3(0.36f, 0.06f, 0.5f), M(C("#7FD3FF")));
                    break;
                case "sunsetTower":
                    Cyl(Vector3.zero, 0.55f, 1.3f, M(C("#F2B48A")));
                    Cyl(new Vector3(0f, 1.3f, 0f), 0.65f, 0.1f, Trim);
                    Box(new Vector3(0f, 1.5f, 0f), new Vector3(0.4f, 0.3f, 0.4f), L(C("#FFB070"), new Color(2.4f, 1.1f, 0.4f)));
                    Tilt(Box(new Vector3(0f, 1.75f, 0f), new Vector3(0.5f, 0.06f, 0.5f), M(C("#E06A5A"))), 0f, 45f, 0f);
                    break;

                case "workshop":
                    House(M(C("#C8B8A8")), M(C("#6A7A9A")), 0.55f);
                    Tilt(Cyl(new Vector3(0.62f, 0.6f, 0.62f), 0.5f, 0.08f, Metal), 90f, 0f, 0f);
                    Box(new Vector3(-0.55f, 0.95f, -0.3f), new Vector3(0.16f, 0.6f, 0.16f), Metal);
                    break;
                case "igloo":
                    Ball(new Vector3(0f, 0.05f, 0f), new Vector3(1.6f, 1.3f, 1.6f), M(C("#F2FAFF"), new Color(0.12f, 0.16f, 0.2f)));
                    Box(new Vector3(0f, 0.25f, 0.75f), new Vector3(0.5f, 0.5f, 0.4f), M(C("#E6F4FF")));
                    Box(new Vector3(0f, 0.22f, 0.96f), new Vector3(0.32f, 0.38f, 0.04f), Warm);
                    break;
                case "iceBlock": Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), M(C("#BFEFFF"), new Color(0.2f, 0.5f, 0.7f)), 0.06f); break;
                case "snowman":
                    Ball(new Vector3(0f, 0.28f, 0f), Vector3.one * 0.6f, M(Color.white, new Color(0.15f, 0.15f, 0.2f)));
                    Ball(new Vector3(0f, 0.72f, 0f), Vector3.one * 0.42f, M(Color.white, new Color(0.15f, 0.15f, 0.2f)));
                    Ball(new Vector3(0f, 1.05f, 0f), Vector3.one * 0.3f, M(Color.white, new Color(0.15f, 0.15f, 0.2f)));
                    Tilt(Cyl(new Vector3(0f, 1.05f, 0.15f), 0.06f, 0.18f, M(C("#FF8A30"))), 90f, 0f, 0f);
                    Cyl(new Vector3(0f, 1.18f, 0f), 0.26f, 0.22f, M(C("#2A2A3A")));
                    Box(new Vector3(0f, 0.88f, 0f), new Vector3(0.38f, 0.07f, 0.38f), M(C("#E04A5A")));
                    break;

                case "training":
                    Box(new Vector3(0f, 0.03f, 0f), new Vector3(1.85f, 0.06f, 1.85f), M(C("#5FB36E")));
                    Box(new Vector3(0f, 0.065f, 0f), new Vector3(1.3f, 0.02f, 1.3f), M(C("#E9E2CF")));
                    Box(new Vector3(0f, 0.07f, 0f), new Vector3(1.0f, 0.03f, 1.0f), M(C("#5FB36E")));
                    for (int i = 0; i < 4; i++) Cyl(new Vector3((i % 2 - 0.5f) * 1.5f, 0f, (i / 2 - 0.5f) * 1.5f), 0.14f, 0.26f, M(C("#FF8A30")));
                    Box(new Vector3(0f, 0.55f, -0.85f), new Vector3(1.2f, 0.4f, 0.06f), L(theme.accent, theme.accent * 1.6f));
                    foreach (float s in new[] { -0.55f, 0.55f }) Box(new Vector3(s, 0.3f, -0.85f), new Vector3(0.06f, 0.6f, 0.06f), Metal);
                    break;
                case "neonSign":
                    Box(new Vector3(0f, 0.35f, 0f), new Vector3(0.08f, 0.7f, 0.08f), Metal);
                    Box(new Vector3(0f, 0.85f, 0f), new Vector3(0.75f, 0.42f, 0.08f), M(C("#22203A")));
                    Box(new Vector3(-0.14f, 0.85f, 0.05f), new Vector3(0.3f, 0.07f, 0.03f), L(C("#FF4FD8"), new Color(2.6f, 0.5f, 2.2f)));
                    Box(new Vector3(0.14f, 0.92f, 0.05f), new Vector3(0.3f, 0.07f, 0.03f), L(C("#4FF0FF"), new Color(0.5f, 2.2f, 2.6f)));
                    Box(new Vector3(0f, 0.76f, 0.05f), new Vector3(0.5f, 0.05f, 0.03f), L(C("#FFF06A"), new Color(2.4f, 2.2f, 0.5f)));
                    break;
                case "lightPath":
                    Box(new Vector3(0f, 0.025f, 0f), new Vector3(0.92f, 0.05f, 0.92f), M(C("#2A2840")));
                    foreach (float s in new[] { -0.38f, 0.38f }) Box(new Vector3(s, 0.06f, 0f), new Vector3(0.06f, 0.03f, 0.9f), L(theme.accent, theme.accent * 2f));
                    break;
                case "lightArch":
                    foreach (float s in new[] { -0.8f, 0.8f }) Box(new Vector3(s, 0.6f, 0f), new Vector3(0.18f, 1.2f, 0.18f), Trim);
                    for (int i = 0; i < 9; i++)
                    {
                        float a = Mathf.PI * i / 8f;
                        Box(new Vector3(-Mathf.Cos(a) * 0.8f, 1.2f + Mathf.Sin(a) * 0.45f, 0f), new Vector3(0.2f, 0.12f, 0.16f), L(i % 2 == 0 ? C("#FF6AD5") : C("#6AF0FF"), i % 2 == 0 ? new Color(2.4f, 0.6f, 2f) : new Color(0.6f, 2.2f, 2.4f)));
                    }
                    break;

                case "magmaLamp":
                    Box(new Vector3(0f, 0.12f, 0f), new Vector3(0.36f, 0.24f, 0.36f), M(C("#3A2A2A")));
                    Ball(new Vector3(0f, 0.55f, 0f), new Vector3(0.36f, 0.6f, 0.36f), L(C("#FF6A2A"), new Color(2.8f, 0.9f, 0.2f)));
                    Box(new Vector3(0f, 0.88f, 0f), new Vector3(0.3f, 0.08f, 0.3f), M(C("#3A2A2A")));
                    break;
                case "stoneOven":
                    Ball(new Vector3(0f, 0.15f, 0f), new Vector3(0.8f, 0.8f, 0.8f), Stone);
                    Box(new Vector3(0f, 0.2f, 0.33f), new Vector3(0.3f, 0.24f, 0.1f), L(C("#FF8A3A"), new Color(2.6f, 1f, 0.2f)));
                    Cyl(new Vector3(0.15f, 0.45f, -0.1f), 0.14f, 0.4f, Stone);
                    break;
                case "lavaFall":
                    Ball(new Vector3(0f, 0.4f, -0.3f), new Vector3(1.8f, 1.3f, 1.1f), M(C("#4A3434")));
                    Box(new Vector3(0f, 0.55f, 0.2f), new Vector3(0.36f, 1.0f, 0.12f), L(C("#FF7A2A"), new Color(3f, 1f, 0.2f)));
                    Box(new Vector3(0f, 0.04f, 0.55f), new Vector3(1.2f, 0.06f, 0.6f), L(C("#FF5A1A"), new Color(2.6f, 0.7f, 0.1f)));
                    foreach (float s in new[] { -0.7f, 0.7f }) Ball(new Vector3(s, 0.15f, 0.55f), Vector3.one * 0.35f, M(C("#5A4040")));
                    break;

                case "square":
                    Box(new Vector3(0f, 0.03f, 0f), new Vector3(1.9f, 0.06f, 1.9f), M(C("#E8DCC8")));
                    for (int i = 0; i < 4; i++) Box(new Vector3((i % 2 - 0.5f) * 0.9f, 0.065f, (i / 2 - 0.5f) * 0.9f), new Vector3(0.8f, 0.02f, 0.8f), M(C("#D8C8B0")));
                    Cyl(Vector3.zero, 0.36f, 0.25f, Stone);
                    Cyl(new Vector3(0f, 0.25f, 0f), 0.12f, 0.9f, Stone);
                    Ball(new Vector3(0f, 1.2f, 0f), Vector3.one * 0.26f, Gold);
                    foreach (float s in new[] { -1f, 1f }) { Box(new Vector3(s * 0.7f, 0.18f, 0.6f), new Vector3(0.45f, 0.06f, 0.18f), Wood); Box(new Vector3(s * 0.7f, 0.5f, -0.7f), new Vector3(0.06f, 1f, 0.06f), Metal); Ball(new Vector3(s * 0.7f, 1.02f, -0.7f), Vector3.one * 0.16f, Warm); }
                    break;
                case "tree":
                    Cyl(Vector3.zero, 0.14f, 0.5f, Wood);
                    Ball(new Vector3(0f, 0.75f, 0f), new Vector3(0.62f, 0.6f, 0.62f), Leaf);
                    Ball(new Vector3(0.1f, 1.02f, 0.05f), Vector3.one * 0.38f, Leaf);
                    break;
                case "fence":
                    foreach (float s in new[] { -0.36f, 0f, 0.36f }) Box(new Vector3(s, 0.25f, 0f), new Vector3(0.08f, 0.5f, 0.08f), M(Color.white));
                    foreach (float y in new[] { 0.18f, 0.36f }) Box(new Vector3(0f, y, 0f), new Vector3(0.9f, 0.06f, 0.04f), M(Color.white));
                    break;
                case "flowerBed":
                    Box(new Vector3(0f, 0.08f, 0f), new Vector3(0.8f, 0.16f, 0.8f), Wood);
                    Box(new Vector3(0f, 0.15f, 0f), new Vector3(0.7f, 0.04f, 0.7f), M(C("#6A4A30")));
                    for (int i = 0; i < 9; i++) Ball(new Vector3((i % 3 - 1) * 0.22f, 0.26f, (i / 3 - 1) * 0.22f), Vector3.one * 0.14f, M(i % 3 == 0 ? C("#FF6A8A") : i % 3 == 1 ? C("#B88AFF") : C("#FFE066"), new Color(0.3f, 0.1f, 0.15f)));
                    break;
                case "treeHouse":
                    Cyl(Vector3.zero, 0.4f, 1.1f, Wood);
                    Ball(new Vector3(0f, 1.6f, 0f), new Vector3(1.8f, 1.1f, 1.8f), Leaf);
                    Box(new Vector3(0f, 1.15f, 0.3f), new Vector3(0.9f, 0.55f, 0.7f), M(C("#C8945A")));
                    Box(new Vector3(0f, 1.15f, 0.66f), new Vector3(0.26f, 0.26f, 0.03f), Warm);
                    Tilt(Box(new Vector3(0.55f, 0.55f, 0.55f), new Vector3(0.08f, 1.2f, 0.25f), Wood), 20f, 0f, -10f);
                    break;

                case "sandHouse":
                    Box(new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 1.5f), M(C("#E8C890")), 0.12f);
                    Box(new Vector3(0.35f, 1.05f, -0.2f), new Vector3(0.7f, 0.35f, 0.7f), M(C("#E0BC80")), 0.1f);
                    Box(new Vector3(0f, 0.3f, 0.76f), new Vector3(0.36f, 0.6f, 0.04f), M(C("#7A4A2A")));
                    Windows(new Vector3(0f, 0f, 0f), 1.2f, 2, 0.6f, 0.76f, Warm);
                    Ball(new Vector3(-0.4f, 1.0f, -0.3f), new Vector3(0.5f, 0.35f, 0.5f), M(C("#4FB3E8")));
                    break;
                case "palm":
                    Tilt(Cyl(Vector3.zero, 0.12f, 1.2f, M(C("#B08050"))), 0f, 0f, 6f);
                    for (int i = 0; i < 5; i++) Tilt(Box(new Vector3(-0.1f, 1.2f, 0f), new Vector3(0.7f, 0.04f, 0.18f), Leaf), 0f, i * 72f, -18f);
                    Ball(new Vector3(-0.08f, 1.12f, 0.06f), Vector3.one * 0.12f, M(C("#7A5030")));
                    break;
                case "desertFountain":
                    Cyl(Vector3.zero, 1.7f, 0.25f, M(C("#E0C08A")));
                    Cyl(new Vector3(0f, 0.25f, 0f), 1.5f, 0.02f, L(C("#4FC8FF"), new Color(0.4f, 1.4f, 2f)));
                    Cyl(new Vector3(0f, 0.25f, 0f), 0.3f, 0.6f, M(C("#E0C08A")));
                    Cyl(new Vector3(0f, 0.85f, 0f), 0.8f, 0.1f, M(C("#E0C08A")));
                    Ball(new Vector3(0f, 1.05f, 0f), new Vector3(0.3f, 0.4f, 0.3f), L(C("#7FDFFF"), new Color(0.6f, 1.8f, 2.4f)));
                    foreach (float s in new[] { -1f, 1f }) Tilt(Box(new Vector3(s * 0.75f, 0.6f, s * 0.75f), new Vector3(0.6f, 0.04f, 0.16f), Leaf), 0f, 45f, s * 20f);
                    break;

                case "repairShop":
                    House(M(C("#8AB8E8")), M(C("#E86A5A")), 0.5f);
                    Box(new Vector3(0f, 0.3f, 0.76f), new Vector3(0.9f, 0.55f, 0.04f), M(C("#4A4A5A")));
                    for (int i = 0; i < 4; i++) Box(new Vector3(0f, 0.1f + i * 0.13f, 0.785f), new Vector3(0.86f, 0.02f, 0.02f), Metal);
                    Tilt(Box(new Vector3(0f, 1.25f, 0f), new Vector3(0.5f, 0.1f, 0.06f), Gold), 0f, 0f, 30f);
                    break;
                case "aquarium":
                    Box(new Vector3(0f, 0.1f, 0f), new Vector3(1.6f, 0.2f, 0.6f), Trim);
                    Box(new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 0.6f, 0.5f), L(C("#5AC8FF"), new Color(0.3f, 1.2f, 2f)));
                    Ball(new Vector3(-0.3f, 0.5f, 0.2f), new Vector3(0.18f, 0.12f, 0.06f), M(C("#FF8A3A")));
                    Ball(new Vector3(0.35f, 0.62f, 0.2f), new Vector3(0.16f, 0.1f, 0.06f), M(C("#FFE04A")));
                    Box(new Vector3(0f, 0.84f, 0f), new Vector3(1.56f, 0.06f, 0.56f), Trim);
                    break;
                case "pier":
                    for (int i = 0; i < 6; i++) Box(new Vector3((i - 2.5f) * 0.31f, 0.05f, 0f), new Vector3(0.28f, 0.06f, 0.85f), Wood);
                    foreach (float s in new[] { -0.85f, 0.85f }) foreach (float z in new[] { -0.38f, 0.38f }) Cyl(new Vector3(s, 0f, z), 0.1f, 0.3f, M(C("#7A5030")));
                    break;
                case "lighthouse":
                    Ball(new Vector3(0f, 0.05f, 0f), new Vector3(1.8f, 0.4f, 2.6f), Stone);
                    for (int i = 0; i < 4; i++) Cyl(new Vector3(0f, 0.2f + i * 0.5f, 0f), 0.75f - i * 0.08f, 0.5f, M(i % 2 == 0 ? Color.white : C("#E04A4A")));
                    Cyl(new Vector3(0f, 2.2f, 0f), 0.6f, 0.4f, L(C("#FFF2A0"), new Color(2.6f, 2.4f, 1f)));
                    Cyl(new Vector3(0f, 2.6f, 0f), 0.7f, 0.08f, M(C("#2A2A3A")));
                    Tilt(Box(new Vector3(0f, 2.75f, 0f), new Vector3(0.5f, 0.3f, 0.5f), M(C("#E04A4A"))), 0f, 45f, 0f);
                    break;

                case "candyHouse":
                    House(M(C("#FFC8DC")), M(C("#7A4A3A")), 0.7f);
                    for (int i = 0; i < 5; i++) Ball(new Vector3((i - 2) * 0.3f, 1.25f, 0f), Vector3.one * 0.16f, M(i % 2 == 0 ? C("#FF5A8A") : C("#5AD8FF")));
                    Box(new Vector3(0f, 0.3f, 0.76f), new Vector3(0.34f, 0.6f, 0.04f), M(C("#FFFFFF")));
                    break;
                case "lollipop":
                    Cyl(Vector3.zero, 0.06f, 0.75f, M(Color.white));
                    Tilt(Cyl(new Vector3(0f, 0.95f, -0.03f), 0.5f, 0.06f, M(C("#FF6AB0"), new Color(0.3f, 0.08f, 0.15f))), 90f, 0f, 0f);
                    Tilt(Cyl(new Vector3(0f, 0.95f, 0.02f), 0.3f, 0.06f, M(C("#FFFFFF"))), 90f, 0f, 0f);
                    break;
                case "lollipopGarden":
                    Box(new Vector3(0f, 0.04f, 0f), new Vector3(1.85f, 0.08f, 1.85f), M(C("#9AE8A0")));
                    for (int i = 0; i < 5; i++)
                    {
                        var at = new Vector3(Mathf.Cos(i * 1.256f) * 0.6f, 0f, Mathf.Sin(i * 1.256f) * 0.6f);
                        Cyl(at, 0.06f, 0.6f + (i % 2) * 0.3f, M(Color.white));
                        Ball(at + Vector3.up * (0.75f + (i % 2) * 0.3f), Vector3.one * 0.36f, M(i % 3 == 0 ? C("#FF6AB0") : i % 3 == 1 ? C("#6AD0FF") : C("#FFE06A"), new Color(0.3f, 0.15f, 0.15f)));
                    }
                    Ball(new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.5f), M(C("#B88AFF")));
                    break;

                case "lab":
                    Box(new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 1.5f), M(C("#E0F0E8")));
                    Ball(new Vector3(0f, 0.95f, 0f), new Vector3(1.1f, 0.7f, 1.1f), L(C("#8AFF8A"), new Color(0.6f, 2f, 0.6f)));
                    Windows(Vector3.zero, 1.2f, 3, 0.5f, 0.76f, L(C("#AAFFAA"), new Color(0.8f, 2f, 0.8f)));
                    Cyl(new Vector3(0.6f, 0.9f, -0.5f), 0.18f, 0.6f, Metal);
                    break;
                case "tubeRack":
                    Box(new Vector3(0f, 0.1f, 0f), new Vector3(0.7f, 0.2f, 0.4f), Metal);
                    for (int i = 0; i < 3; i++) Cyl(new Vector3((i - 1) * 0.2f, 0.2f, 0f), 0.13f, 0.55f, L(i == 1 ? C("#FF6AF0") : C("#6AFF8A"), i == 1 ? new Color(2.2f, 0.5f, 2f) : new Color(0.5f, 2.2f, 0.7f)));
                    break;
                case "bubbleTube":
                    Cyl(Vector3.zero, 0.6f, 0.2f, Metal);
                    Cyl(new Vector3(0f, 0.2f, 0f), 0.45f, 1.3f, L(C("#7AFF9A"), new Color(0.6f, 2.4f, 0.8f)));
                    for (int i = 0; i < 4; i++) Ball(new Vector3((i % 2 - 0.5f) * 0.15f, 0.45f + i * 0.28f, 0.05f), Vector3.one * 0.1f, M(Color.white, new Color(0.6f, 1f, 0.6f)));
                    Cyl(new Vector3(0f, 1.5f, 0f), 0.6f, 0.15f, Metal);
                    break;

                case "streetLamp":
                    Cyl(Vector3.zero, 0.2f, 0.08f, M(C("#2A2A3A")));
                    Cyl(Vector3.zero, 0.07f, 1.2f, M(C("#2A2A3A")));
                    Box(new Vector3(0.12f, 1.2f, 0f), new Vector3(0.3f, 0.05f, 0.05f), M(C("#2A2A3A")));
                    Ball(new Vector3(0.25f, 1.12f, 0f), new Vector3(0.2f, 0.16f, 0.2f), L(C("#FFE8A0"), new Color(2.6f, 2.2f, 1f)));
                    break;
                case "bench":
                    Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.8f, 0.06f, 0.3f), Wood);
                    Box(new Vector3(0f, 0.45f, -0.13f), new Vector3(0.8f, 0.25f, 0.05f), Wood);
                    foreach (float s in new[] { -0.32f, 0.32f }) Box(new Vector3(s, 0.12f, 0f), new Vector3(0.06f, 0.24f, 0.28f), M(C("#2A2A3A")));
                    break;
                case "watchTower":
                    foreach (var o in new[] { new Vector2(-0.6f, -0.6f), new Vector2(0.6f, -0.6f), new Vector2(-0.6f, 0.6f), new Vector2(0.6f, 0.6f) })
                        Box(new Vector3(o.x, 0.8f, o.y), new Vector3(0.12f, 1.6f, 0.12f), Wood);
                    Box(new Vector3(0f, 1.65f, 0f), new Vector3(1.5f, 0.1f, 1.5f), Wood);
                    Box(new Vector3(0f, 1.95f, 0f), new Vector3(1.2f, 0.5f, 1.2f), M(C("#3A3A5A")));
                    Box(new Vector3(0f, 2.0f, 0.61f), new Vector3(0.8f, 0.2f, 0.03f), Warm);
                    Roof(new Vector3(0f, 2.2f, 0f), 1.4f, 1.4f, 0.4f, Trim);
                    Box(new Vector3(0.5f, 2.6f, 0f), new Vector3(0.1f, 0.1f, 0.1f), L(C("#FF6A6A"), new Color(2.4f, 0.5f, 0.5f)));
                    break;

                case "polarTent":
                    Ball(new Vector3(0f, 0f, 0f), new Vector3(1.7f, 1.4f, 1.7f), M(C("#E8F0FF")));
                    for (int i = 0; i < 3; i++) Tilt(Box(new Vector3(0f, 0.6f, 0f), new Vector3(1.7f, 0.04f, 0.06f), M(C("#4A6AE8"))), 0f, i * 60f, 0f);
                    Box(new Vector3(0f, 0.25f, 0.8f), new Vector3(0.4f, 0.5f, 0.06f), Warm);
                    Cyl(new Vector3(0f, 0.6f, 0f), 0.06f, 0.6f, Metal);
                    break;
                case "snowPine":
                    Cyl(Vector3.zero, 0.12f, 0.25f, Wood);
                    for (int i = 0; i < 3; i++) Tilt(Cyl(new Vector3(0f, 0.25f + i * 0.28f, 0f), 0.7f - i * 0.18f, 0.35f, M(C("#3A8A6A"))), 0f, i * 20f, 0f);
                    Ball(new Vector3(0f, 1.15f, 0f), Vector3.one * 0.2f, M(Color.white));
                    break;
                case "auroraDome":
                    Cyl(Vector3.zero, 1.8f, 0.15f, M(C("#3A4A6A")));
                    Ball(new Vector3(0f, 0.15f, 0f), new Vector3(1.6f, 1.6f, 1.6f), L(C("#6AF0C8"), new Color(0.5f, 2f, 1.6f)));
                    foreach (float s in new[] { -0.4f, 0f, 0.4f }) Tilt(Box(new Vector3(s, 1.4f, 0f), new Vector3(0.12f, 0.6f, 0.04f), L(C("#B88AFF"), new Color(1.4f, 0.8f, 2.4f))), 0f, 0f, s * 40f);
                    break;

                case "windVane":
                    Cyl(Vector3.zero, 0.07f, 1.1f, Metal);
                    Box(new Vector3(0f, 1.1f, 0f), new Vector3(0.6f, 0.04f, 0.04f), Metal);
                    Tilt(Box(new Vector3(0.28f, 1.1f, 0f), new Vector3(0.14f, 0.14f, 0.03f), Gold), 0f, 0f, 45f);
                    Box(new Vector3(-0.26f, 1.13f, 0f), new Vector3(0.18f, 0.14f, 0.02f), Gold);
                    Ball(new Vector3(0f, 1.25f, 0f), Vector3.one * 0.12f, Gold);
                    break;
                case "rainGarden":
                    Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.85f, 0.08f, 0.85f), M(C("#4A6A8A")));
                    Cyl(new Vector3(0f, 0.05f, 0f), 0.5f, 0.02f, L(C("#6AC8FF"), new Color(0.4f, 1.2f, 2f)));
                    for (int i = 0; i < 4; i++) Ball(new Vector3((i % 2 - 0.5f) * 0.6f, 0.15f, (i / 2 - 0.5f) * 0.6f), Vector3.one * 0.22f, Leaf);
                    break;
                case "lightningRod":
                    Cyl(Vector3.zero, 0.4f, 0.2f, Stone);
                    Cyl(new Vector3(0f, 0.2f, 0f), 0.07f, 1.6f, Metal);
                    for (int i = 0; i < 3; i++) Tilt(Box(new Vector3(0f, 1.0f + i * 0.25f, 0f), new Vector3(0.3f - i * 0.06f, 0.03f, 0.03f), Metal), 0f, i * 60f, 0f);
                    Tilt(Box(new Vector3(0.05f, 1.95f, 0f), new Vector3(0.12f, 0.35f, 0.04f), L(C("#FFF06A"), new Color(2.8f, 2.6f, 0.6f))), 0f, 0f, 20f);
                    break;

                case "hologram":
                    Box(new Vector3(0f, 0.1f, 0f), new Vector3(1.7f, 0.2f, 1.7f), M(C("#2A2A4A")));
                    var holo = MaterialFactory.CreateTransparent(new Color(0.4f, 0.9f, 1f, 0.45f), new Color(0.4f, 1.6f, 2.2f));
                    Lights.Add((holo, new Color(0.4f, 0.9f, 1f, 0.45f), new Color(0.4f, 1.6f, 2.2f)));
                    Box(new Vector3(0f, 0.75f, 0f), new Vector3(1.4f, 1.1f, 1.3f), holo);
                    Box(new Vector3(0.3f, 1.5f, -0.2f), new Vector3(0.7f, 0.5f, 0.7f), holo);
                    foreach (float s in new[] { -0.8f, 0.8f }) Box(new Vector3(s, 0.22f, 0.8f), new Vector3(0.1f, 0.04f, 0.1f), L(C("#4FF0FF"), new Color(0.5f, 2.2f, 2.6f)));
                    break;
                case "dataPost":
                    Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.25f, 1f, 0.25f), M(C("#2A2A4A")));
                    for (int i = 0; i < 4; i++) Box(new Vector3(0f, 0.2f + i * 0.22f, 0.13f), new Vector3(0.18f, 0.05f, 0.02f), L(C("#4FF0FF"), new Color(0.5f, 2.2f, 2.6f)));
                    break;
                case "dataTower":
                    Box(new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 1.8f, 0.6f), M(C("#2A2A4A")));
                    for (int i = 0; i < 7; i++) Box(new Vector3(0f, 0.2f + i * 0.24f, 0.31f), new Vector3(0.5f, 0.05f, 0.02f), L(C("#4FF0FF"), new Color(0.5f, 2.2f, 2.6f)));
                    Box(new Vector3(0f, 1.95f, 0f), new Vector3(0.08f, 0.3f, 0.08f), Metal);
                    Ball(new Vector3(0f, 2.15f, 0f), Vector3.one * 0.15f, L(C("#FF4FD8"), new Color(2.4f, 0.5f, 2.2f)));
                    break;

                case "planetStatue":
                    Box(new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.3f, 0.5f), Stone);
                    Ball(new Vector3(0f, 0.6f, 0f), Vector3.one * 0.45f, M(C("#B88AFF"), new Color(0.3f, 0.15f, 0.5f)));
                    Tilt(Cyl(new Vector3(0f, 0.6f, 0f), 0.8f, 0.02f, L(C("#FFD06A"), new Color(1.6f, 1.2f, 0.4f))), 20f, 0f, 10f);
                    break;
                case "starFlower":
                    Cyl(Vector3.zero, 0.05f, 0.5f, Leaf);
                    for (int i = 0; i < 5; i++) Tilt(Box(new Vector3(Mathf.Cos(i * 1.256f) * 0.12f, 0.55f, Mathf.Sin(i * 1.256f) * 0.12f), new Vector3(0.2f, 0.04f, 0.1f), L(C("#FFE06A"), new Color(2.2f, 1.8f, 0.5f))), 0f, -i * 72f, 0f);
                    Ball(new Vector3(0f, 0.57f, 0f), Vector3.one * 0.1f, M(C("#FF8A3A")));
                    break;
                case "dish":
                    Box(new Vector3(0f, 0.2f, 0f), new Vector3(0.8f, 0.4f, 0.8f), Trim);
                    Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.14f, 0.5f, 0.14f), Metal);
                    Tilt(Ball(new Vector3(0f, 1.05f, 0.1f), new Vector3(1.5f, 1.5f, 0.35f), M(Color.white)), -40f, 0f, 0f);
                    Tilt(Box(new Vector3(0f, 1.2f, 0.45f), new Vector3(0.05f, 0.05f, 0.5f), Metal), -40f, 0f, 0f);
                    Ball(new Vector3(0f, 1.42f, 0.62f), Vector3.one * 0.12f, L(C("#FF6A6A"), new Color(2.4f, 0.5f, 0.5f)));
                    break;

                case "crystal":
                    for (int i = 0; i < 4; i++) Tilt(Box(new Vector3((i % 2 - 0.5f) * 0.25f, 0.25f + i * 0.06f, (i / 2 - 0.5f) * 0.2f), new Vector3(0.18f, 0.5f + i * 0.12f, 0.18f), L(C("#8AF0FF"), new Color(0.6f, 1.8f, 2.2f)), 0.02f), (i - 1.5f) * 10f, i * 25f, (i - 1.5f) * 12f);
                    break;
                case "lightStone":
                    Ball(new Vector3(0f, 0.18f, 0f), new Vector3(0.6f, 0.4f, 0.5f), Stone);
                    Ball(new Vector3(0.05f, 0.35f, 0.05f), Vector3.one * 0.22f, L(C("#AAFFF0"), new Color(0.8f, 2.2f, 2f)));
                    break;
                case "crystalPalace":
                    Box(new Vector3(0f, 0.15f, 0f), new Vector3(1.8f, 0.3f, 1.8f), M(C("#CDEFF3")));
                    for (int i = 0; i < 5; i++)
                    {
                        var at = i == 4 ? Vector3.zero : new Vector3((i % 2 - 0.5f) * 1.1f, 0f, (i / 2 - 0.5f) * 1.1f);
                        float hgt = i == 4 ? 1.9f : 1.1f;
                        Tilt(Box(at + Vector3.up * (0.3f + hgt * 0.5f), new Vector3(0.36f, hgt, 0.36f), L(C("#8AF0FF"), new Color(0.6f, 1.8f, 2.2f)), 0.03f), 0f, 45f, 0f);
                    }
                    break;

                case "lanterns":
                    foreach (float s in new[] { -0.85f, 0.85f }) Cyl(new Vector3(s, 0f, 0f), 0.08f, 1.2f, Wood);
                    for (int i = 0; i < 6; i++)
                    {
                        float x = (i - 2.5f) * 0.3f;
                        float y = 1.1f - Mathf.Sin((i + 0.5f) / 6f * Mathf.PI) * 0.2f;
                        Ball(new Vector3(x, y, 0f), new Vector3(0.18f, 0.24f, 0.18f), L(i % 2 == 0 ? C("#FF6A5A") : C("#FFD06A"), i % 2 == 0 ? new Color(2.4f, 0.6f, 0.4f) : new Color(2.4f, 1.8f, 0.5f)));
                    }
                    Box(new Vector3(0f, 1.15f, 0f), new Vector3(1.7f, 0.02f, 0.02f), M(C("#2A2A3A")));
                    break;
                case "balloonStand":
                    Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.5f, 0.4f), M(C("#FF8AC8")));
                    Box(new Vector3(0f, 0.52f, 0f), new Vector3(0.66f, 0.06f, 0.46f), M(Color.white));
                    for (int i = 0; i < 4; i++)
                    {
                        var top = new Vector3((i - 1.5f) * 0.18f, 1.05f + (i % 2) * 0.15f, 0f);
                        Box(new Vector3(top.x, 0.8f, 0f), new Vector3(0.01f, 0.5f, 0.01f), M(Color.white));
                        Ball(top, new Vector3(0.22f, 0.27f, 0.22f), M(i % 3 == 0 ? C("#FF5A6A") : i % 3 == 1 ? C("#5AC8FF") : C("#FFE05A"), new Color(0.3f, 0.2f, 0.2f)));
                    }
                    break;
                case "fireworkTower":
                    Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 1.2f, 0.5f), M(C("#7A3E8A")));
                    Tilt(Box(new Vector3(0f, 1.3f, 0f), new Vector3(0.45f, 0.3f, 0.45f), Gold), 0f, 45f, 0f);
                    for (int i = 0; i < 6; i++) Box(new Vector3(Mathf.Cos(i * 1.047f) * 0.35f, 1.8f + Mathf.Sin(i * 1.047f) * 0.35f, 0f), new Vector3(0.08f, 0.08f, 0.08f), L(C("#FF9BF0"), new Color(2.4f, 1f, 2.2f)));
                    break;

                case "gear":
                    Tilt(Cyl(new Vector3(0f, 0.45f, 0f), 0.7f, 0.15f, Metal), 90f, 0f, 0f);
                    for (int i = 0; i < 8; i++) Tilt(Box(new Vector3(Mathf.Cos(i * 0.785f) * 0.4f, 0.45f + Mathf.Sin(i * 0.785f) * 0.4f, 0f), new Vector3(0.14f, 0.14f, 0.14f), Metal), 0f, 0f, i * 45f);
                    Tilt(Cyl(new Vector3(0f, 0.45f, 0.05f), 0.2f, 0.12f, Gold), 90f, 0f, 0f);
                    Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.1f, 0.3f), M(C("#4A3A3A")));
                    break;
                case "pipe":
                    Cyl(new Vector3(-0.25f, 0f, 0f), 0.22f, 0.7f, M(C("#C88A5A")));
                    Tilt(Cyl(new Vector3(-0.25f, 0.7f, 0f), 0.22f, 0.5f, M(C("#C88A5A"))), 0f, 0f, -90f);
                    Cyl(new Vector3(0.28f, 0f, 0f), 0.22f, 0.7f, M(C("#C88A5A")));
                    Ball(new Vector3(0f, 0.7f, 0f), Vector3.one * 0.12f, Gold);
                    break;
                case "steamTower":
                    Box(new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1f, 1.4f), M(C("#A86A4A")));
                    Windows(Vector3.zero, 1.1f, 3, 0.6f, 0.71f, Warm);
                    Cyl(new Vector3(0.35f, 1f, -0.2f), 0.4f, 1.0f, M(C("#6A4A3A")));
                    Cyl(new Vector3(-0.35f, 1f, 0.2f), 0.3f, 0.7f, M(C("#6A4A3A")));
                    Ball(new Vector3(0.35f, 2.2f, -0.2f), new Vector3(0.6f, 0.4f, 0.6f), M(new Color(0.95f, 0.95f, 1f), new Color(0.3f, 0.3f, 0.35f)));
                    break;

                case "ticketBooth":
                    Box(new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 0.6f), M(C("#FF6A7A")));
                    Box(new Vector3(0f, 0.6f, 0.31f), new Vector3(0.4f, 0.25f, 0.03f), Warm);
                    Tilt(Box(new Vector3(0f, 1.0f, 0f), new Vector3(0.6f, 0.06f, 0.6f), M(C("#FFD36B"))), 0f, 45f, 0f);
                    Ball(new Vector3(0f, 1.1f, 0f), Vector3.one * 0.14f, Gold);
                    break;
                case "carousel":
                    Cyl(Vector3.zero, 1.8f, 0.15f, M(C("#F8DCE2")));
                    Cyl(new Vector3(0f, 0.15f, 0f), 0.2f, 1.0f, Gold);
                    for (int i = 0; i < 6; i++)
                    {
                        var at = new Vector3(Mathf.Cos(i * 1.047f) * 0.65f, 0f, Mathf.Sin(i * 1.047f) * 0.65f);
                        Box(at + Vector3.up * 0.6f, new Vector3(0.03f, 0.9f, 0.03f), Gold);
                        Ball(at + Vector3.up * 0.45f, new Vector3(0.2f, 0.18f, 0.32f), M(i % 2 == 0 ? Color.white : C("#FFD36B")));
                    }
                    Ball(new Vector3(0f, 1.15f, 0f), new Vector3(1.9f, 0.5f, 1.9f), M(C("#FF6A7A")));
                    Ball(new Vector3(0f, 1.45f, 0f), Vector3.one * 0.2f, L(C("#FFE07A"), new Color(2.4f, 2f, 0.6f)));
                    break;
                case "ferrisWheel":
                    foreach (float s in new[] { -0.3f, 0.3f }) { Tilt(Box(new Vector3(-0.4f, 0.8f, s), new Vector3(0.08f, 1.8f, 0.08f), Metal), 0f, 0f, -18f); Tilt(Box(new Vector3(0.4f, 0.8f, s), new Vector3(0.08f, 1.8f, 0.08f), Metal), 0f, 0f, 18f); }
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI / 6f;
                        Tilt(Box(new Vector3(Mathf.Cos(a) * 0.55f, 1.6f + Mathf.Sin(a) * 0.55f, 0f), new Vector3(1.1f, 0.03f, 0.03f), Metal), 0f, 0f, a * Mathf.Rad2Deg);
                        Box(new Vector3(Mathf.Cos(a) * 1.1f, 1.6f + Mathf.Sin(a) * 1.1f, 0f), new Vector3(0.12f, 0.12f, 0.12f), L(C("#FFE07A"), new Color(2.4f, 2f, 0.6f)));
                        if (i % 2 == 0) Box(new Vector3(Mathf.Cos(a) * 1.1f, 1.45f + Mathf.Sin(a) * 1.1f, 0f), new Vector3(0.22f, 0.18f, 0.3f), M(i % 4 == 0 ? C("#FF6A7A") : C("#6AC8FF")));
                    }
                    break;

                case "roofGarden":
                    Box(new Vector3(0f, 0.12f, 0f), new Vector3(1.85f, 0.24f, 1.85f), Wood);
                    Box(new Vector3(0f, 0.25f, 0f), new Vector3(1.7f, 0.04f, 1.7f), M(C("#6A4A30")));
                    for (int i = 0; i < 9; i++) Ball(new Vector3((i % 3 - 1) * 0.5f, 0.42f, (i / 3 - 1) * 0.5f), Vector3.one * (0.3f + (i % 2) * 0.1f), i % 4 == 0 ? M(C("#FF8A6A")) : Leaf);
                    break;
                case "solar":
                    Box(new Vector3(0f, 0.2f, 0f), new Vector3(0.06f, 0.4f, 0.06f), Metal);
                    Tilt(Box(new Vector3(0f, 0.45f, 0f), new Vector3(0.85f, 0.04f, 0.6f), M(C("#2A4AA8"), new Color(0.1f, 0.3f, 0.8f))), -25f, 0f, 0f);
                    break;
                case "goldStatue":
                    Box(new Vector3(0f, 0.25f, 0f), new Vector3(1.2f, 0.5f, 1.2f), M(C("#E8E8F0")));
                    Box(new Vector3(0f, 0.85f, 0f), new Vector3(0.5f, 0.6f, 0.4f), Gold, 0.1f);
                    Box(new Vector3(0f, 1.45f, 0f), new Vector3(0.75f, 0.65f, 0.65f), Gold, 0.14f);
                    Box(new Vector3(0f, 1.45f, 0.33f), new Vector3(0.55f, 0.32f, 0.03f), M(C("#3A3A5A")));
                    foreach (float s in new[] { -0.13f, 0.13f }) Ball(new Vector3(s, 1.47f, 0.35f), new Vector3(0.1f, 0.12f, 0.02f), L(C("#7FF0FF"), new Color(0.6f, 2.2f, 2.6f)));
                    Tilt(Box(new Vector3(0.4f, 1.15f, 0f), new Vector3(0.12f, 0.55f, 0.14f), Gold), 0f, 0f, 35f);
                    break;

                default:
                    Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), Accent);
                    break;
            }
        }

        /// <summary>A little 2x2 house: walls, a pitched roof, a door and lit windows.</summary>
        private void House(Material wall, Material roof, float rise)
        {
            Box(new Vector3(0f, 0.42f, 0f), new Vector3(1.5f, 0.84f, 1.4f), wall, 0.06f);
            Roof(new Vector3(0f, 0.84f, 0f), 1.7f, 1.6f, rise, roof);
            Box(new Vector3(0f, 0.3f, 0.71f), new Vector3(0.32f, 0.58f, 0.04f), Wood);
            Windows(Vector3.zero, 1.2f, 2, 0.5f, 0.71f, Warm);
            Box(new Vector3(0.45f, 1.0f + rise * 0.3f, -0.3f), new Vector3(0.18f, 0.5f, 0.18f), Trim);
        }
    }
}
