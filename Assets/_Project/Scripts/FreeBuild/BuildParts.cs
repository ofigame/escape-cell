using System.Collections.Generic;
using BrickMuseum.Build;
using BrickMuseum.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrickMuseum.FreeBuild
{
    /// <summary>
    /// The free-building pieces that the models don't use: a door (a frame with a leaf that swings open), a window, a
    /// flight of stairs (one storey: 10 plates up over 10 studs, high end at -x like a slope) and a railing. Like every
    /// piece they are centred on their footprint, bottom at y = 0, width w along x and depth d along z.
    /// Everything else comes from <see cref="PieceFactory"/>.
    /// </summary>
    public static class BuildParts
    {
        private const float P = Piece.Plate, G = Piece.Gap;
        private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public static bool IsSpecial(string t) => t == "door" || t == "window" || t == "stairs" || t == "fence" || t == "taxisign" || IsGarden(t);
        public static bool IsGarden(string t) => t == "picket" || t == "hedge" || t == "pool" || t == "gtree" || t == "flowers" || t == "stones" || t == "glamp";

        public static GameObject Create(Piece p, PieceLook look = PieceLook.Solid, Transform parent = null)
        {
            if (!IsSpecial(p.t)) return PieceFactory.Create(p, look, parent);
            var go = new GameObject(p.key);
            if (parent != null) go.transform.SetParent(parent, false);
            float w = p.w, d = p.d, H = p.h * P;
            switch (p.t)
            {
                case "door":
                {
                    var frame = Mesh("door-frame", b =>
                    {
                        b.Box(new Vector3(-w / 2f + 0.2f, H / 2f, 0), new Vector3(0.36f, H - 0.01f, d - G));
                        b.Box(new Vector3(w / 2f - 0.2f, H / 2f, 0), new Vector3(0.36f, H - 0.01f, d - G));
                        b.Box(new Vector3(0, H - 0.25f, 0), new Vector3(w - G, 0.5f, d - G));
                    }, w, d, H);
                    Part(go, frame, Colour(p.c, look));
                    var leaf = new GameObject("Leaf").transform;                     // hinged on the left post
                    leaf.SetParent(go.transform, false);
                    leaf.localPosition = new Vector3(-w / 2f + 0.38f, 0, 0.25f);
                    float lw = w - 0.76f, lh = H - 0.52f;
                    var panel = Mesh("door-leaf", b =>
                    {
                        b.Box(new Vector3(lw / 2f, lh / 2f + 0.02f, 0), new Vector3(lw - 0.04f, lh - 0.04f, 0.16f));
                    }, w, d, H);
                    Part(leaf.gameObject, panel, Colour(p.c == "brown" ? "cream" : "brown", look));
                    var knob = Mesh("door-knob", b => b.Box(new Vector3(lw - 0.3f, 1.3f, 0.14f), new Vector3(0.14f, 0.14f, 0.14f)), w, d, H);
                    var k = new GameObject("Knob");
                    k.transform.SetParent(leaf, false);
                    Part(k, knob, Colour("gold", look));
                    break;
                }
                case "window":
                {
                    var frame = Mesh("window-frame", b =>
                    {
                        b.Box(new Vector3(-w / 2f + 0.12f, H / 2f, 0), new Vector3(0.2f, H - 0.01f, d - G));
                        b.Box(new Vector3(w / 2f - 0.12f, H / 2f, 0), new Vector3(0.2f, H - 0.01f, d - G));
                        b.Box(new Vector3(0, 0.12f, 0), new Vector3(w - G, 0.24f, d - G));
                        b.Box(new Vector3(0, H - 0.12f, 0), new Vector3(w - G, 0.24f, d - G));
                        b.Box(new Vector3(0, H / 2f, 0), new Vector3(0.1f, H - 0.4f, 0.14f));                      // the middle bar
                        if (w >= 4) b.Box(new Vector3(0, H / 2f, 0), new Vector3(w - 0.4f, 0.1f, 0.14f));
                    }, w, d, H);
                    Part(go, frame, Colour(p.c, look));
                    var glass = Mesh("window-glass", b => b.Box(new Vector3(0, H / 2f, 0), new Vector3(w - 0.3f, H - 0.4f, 0.06f)), w, d, H);
                    Part(go, glass, look == PieceLook.Ghost ? Mats.Ghost("tblue") : Mats.Brick("tblue"));
                    break;
                }
                case "stairs":
                {
                    int steps = p.w;
                    var m = Mesh("stairs", b =>
                    {
                        for (int i = 0; i < steps; i++)
                        {
                            float top = (i + 1) * (H / steps);
                            float x1 = w / 2f - i, x0 = x1 - 1f;                                       // climbing toward -x
                            b.Box(new Vector3((x0 + x1) / 2f, top / 2f, 0), new Vector3(1f - (i == steps - 1 ? G / 2f : 0f), top - 0.01f, d - G));
                        }
                    }, w, d, H);
                    Part(go, m, Colour(p.c, look));
                    break;
                }
                case "picket":
                {
                    // a picket fence: pointed boards on two rails
                    var m = Mesh("picket", b =>
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            float x = -w / 2f + 0.25f + i * (w - 0.5f) / 3f;
                            b.Box(new Vector3(x, (H - 0.15f) / 2f, 0), new Vector3(0.2f, H - 0.15f, 0.08f));
                            b.Cylinder(new Vector3(x, H - 0.15f, 0), 0.1f, 0.01f, 0.15f, 4);
                        }
                        b.Box(new Vector3(0, H * 0.3f, -0.07f), new Vector3(w - 0.05f, 0.12f, 0.06f));
                        b.Box(new Vector3(0, H * 0.7f, -0.07f), new Vector3(w - 0.05f, 0.12f, 0.06f));
                    }, w, d, H);
                    Part(go, m, Colour(p.c, look));
                    break;
                }
                case "hedge":
                {
                    // a clipped hedge: a soft box with a few rounded tufts on top
                    var m = Mesh("hedge", b =>
                    {
                        b.Box(new Vector3(0, H * 0.42f, 0), new Vector3(w - 0.06f, H * 0.84f, d - 0.06f));
                        for (int i = 0; i < p.w; i++) b.Dome(new Vector3(-w / 2f + 0.5f + i, H * 0.84f, 0), 0.46f, 0.3f, 10);
                    }, w, d, H);
                    Part(go, m, Colour(p.c, look));
                    break;
                }
                case "pool":
                {
                    // a pool: a stone rim round living water
                    var rim = Mesh("pool-rim", b =>
                    {
                        b.Box(new Vector3(0, H / 2f, -d / 2f + 0.25f), new Vector3(w - 0.04f, H, 0.5f));
                        b.Box(new Vector3(0, H / 2f, d / 2f - 0.25f), new Vector3(w - 0.04f, H, 0.5f));
                        b.Box(new Vector3(-w / 2f + 0.25f, H / 2f, 0), new Vector3(0.5f, H, d - 1f));
                        b.Box(new Vector3(w / 2f - 0.25f, H / 2f, 0), new Vector3(0.5f, H, d - 1f));
                    }, w, d, H);
                    Part(go, rim, Colour(p.c, look));
                    var water = Mesh("pool-water", b => b.Box(new Vector3(0, H * 0.6f, 0), new Vector3(w - 1f, H * 0.4f, d - 1f)), w, d, H);
                    var wm = Object.Instantiate(water);
                    World.Water.PlanarUV(wm, 0.3f);
                    Part(go, wm, look == PieceLook.Ghost ? Mats.Ghost("lblue") : World.Water.Lake);
                    break;
                }
                case "gtree":
                {
                    // an ornamental tree: a trunk and a round crown (the colour is the crown's)
                    var trunk = Mesh("gtree-trunk", b => b.Cylinder(Vector3.zero, 0.2f, 0.16f, H * 0.5f, 8), w, d, H);
                    Part(go, trunk, Colour("brown", look));
                    var crown = Mesh("gtree-crown", b =>
                    {
                        b.Cylinder(new Vector3(0, H * 0.42f, 0), w * 0.3f, w * 0.48f, H * 0.18f, 12, false, true);
                        b.Dome(new Vector3(0, H * 0.6f, 0), w * 0.48f, H * 0.42f, 14);
                    }, w, d, H);
                    Part(go, crown, Colour(p.c, look));
                    break;
                }
                case "flowers":
                {
                    var bed = Mesh("flowers-bed", b => b.Box(new Vector3(0, H * 0.4f, 0), new Vector3(w - 0.05f, H * 0.8f, d - 0.05f)), w, d, H);
                    Part(go, bed, Colour("dgreen", look));
                    var blooms = Mesh("flowers-blooms", b =>
                    {
                        foreach (var (x, z) in new[] { (-0.22f, -0.2f), (0.22f, -0.12f), (0f, 0.22f) })
                        {
                            b.Cylinder(new Vector3(x, H * 0.8f, z), 0.03f, 0.03f, 0.25f, 4);
                            b.Dome(new Vector3(x, H * 0.8f + 0.25f, z), 0.13f, 0.12f, 8);
                        }
                    }, w, d, H);
                    Part(go, blooms, Colour(p.c, look));
                    break;
                }
                case "stones":
                {
                    var m = Mesh("stones", b =>
                    {
                        foreach (var (x, z, r) in new[] { (-0.5f, -0.45f, 0.42f), (0.45f, -0.5f, 0.36f), (-0.4f, 0.5f, 0.38f), (0.5f, 0.45f, 0.44f) })
                            b.Cylinder(new Vector3(x, 0, z), r, r * 0.9f, H * 0.7f, 7);
                    }, w, d, H);
                    Part(go, m, Colour(p.c, look));
                    break;
                }
                case "glamp":
                {
                    var post = Mesh("glamp-post", b => { b.Cylinder(Vector3.zero, 0.16f, 0.12f, H * 0.82f, 8); b.Box(new Vector3(0, H * 0.82f, 0), new Vector3(0.42f, 0.06f, 0.42f)); }, w, d, H);
                    Part(go, post, Colour(p.c, look));
                    var light = Mesh("glamp-light", b => b.Box(new Vector3(0, H * 0.9f, 0), new Vector3(0.34f, H * 0.14f, 0.34f)), w, d, H);
                    Part(go, light, look == PieceLook.Ghost ? Mats.Ghost("yellow") : Mats.Unlit(new Color(1f, 0.92f, 0.6f)));
                    break;
                }
                case "taxisign":
                {
                    // the roof sign: a yellow box with a black chequered band (the car becomes a taxi)
                    var box = Mesh("taxi-box", b => b.Box(new Vector3(0, H / 2f, 0), new Vector3(w - 0.1f, H - 0.02f, d - 0.15f)), w, d, H);
                    Part(go, box, Colour("yellow", look));
                    var chk = Mesh("taxi-check", b =>
                    {
                        for (int i = 0; i < 6; i++)
                            foreach (float side in new[] { -1f, 1f })
                                b.Box(new Vector3(-w / 2f + 0.2f + i * (w - 0.4f) / 5f, H * (i % 2 == 0 ? 0.42f : 0.62f), side * (d / 2f - 0.06f)), new Vector3(0.3f, H * 0.2f, 0.04f));
                    }, w, d, H);
                    Part(go, chk, Colour("black", look));
                    break;
                }
                case "fence":
                {
                    var m = Mesh("fence", b =>
                    {
                        for (int i = 0; i <= p.w; i++) b.Cylinder(new Vector3(-w / 2f + 0.1f + i * (w - 0.2f) / p.w, 0, 0), 0.08f, 0.08f, H - 0.05f, 8);
                        b.Box(new Vector3(0, H - 0.08f, 0), new Vector3(w - G, 0.14f, 0.2f));
                        b.Box(new Vector3(0, H * 0.45f, 0), new Vector3(w - G, 0.08f, 0.12f));
                    }, w, d, H);
                    Part(go, m, Colour(p.c, look));
                    break;
                }
            }
            return go;
        }

        private static Material Colour(string c, PieceLook look) => look == PieceLook.Ghost ? Mats.Ghost(c) : Mats.Brick(c);

        private static Mesh Mesh(string name, System.Action<MeshBuilder> build, float w, float d, float H)
        {
            string key = $"{name}:{w}x{d}x{H}";
            if (meshes.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder();
            build(b);
            return meshes[key] = b.ToMesh(key);
        }

        private static void Part(GameObject go, Mesh mesh, Material mat)
        {
            var child = new GameObject("part");
            child.transform.SetParent(go.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = child.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>Picture of a kind for the strip (cached).</summary>
        public static Texture2D Picture(BuildKind k) => Previews.Photo("b:" + k.key, 160, () => Create(k.Make(PictureColour(k.t)), PieceLook.Picture));

        /// <summary>The colour a kind is shown in on its card (and a good first colour for it).</summary>
        public static string PictureColour(string t)
        {
            switch (t)
            {
                case "window": case "door": case "fence": case "picket": case "pool": return "white";
                case "wheel": return "black";
                case "hedge": case "gtree": return "green";
                case "flowers": return "rose";
                case "stones": return "gray";
                case "glamp": return "dgray";
                default: return "red";
            }
        }
    }
}
