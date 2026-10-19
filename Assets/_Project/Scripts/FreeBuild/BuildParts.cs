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

        public static bool IsSpecial(string t) => t == "door" || t == "window" || t == "stairs" || t == "fence";

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
        public static Texture2D Picture(BuildKind k) => Previews.Photo("b:" + k.key, 160, () => Create(k.Make(k.t == "window" ? "white" : k.t == "door" ? "white" : k.t == "fence" ? "white" : k.t == "wheel" ? "black" : "red"), PieceLook.Picture));
    }
}
