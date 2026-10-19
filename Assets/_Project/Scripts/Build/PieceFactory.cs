using System.Collections.Generic;
using BrickMuseum.Data;
using BrickMuseum.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrickMuseum.Build
{
    public enum PieceLook { Solid, Ghost, Drag, Picture }

    /// <summary>
    /// Builds piece objects. Each piece's mesh is centred on its footprint with its bottom at y = 0, in its canonical
    /// orientation; <see cref="Place"/> turns and moves it to its spot in a model.
    /// </summary>
    public static class PieceFactory
    {
        private const float P = Piece.Plate, G = Piece.Gap;
        private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Material> stickerMats = new Dictionary<string, Material>();
        private static Mesh tire, hub;

        public static GameObject Create(Piece p, PieceLook look, Transform parent = null)
        {
            var go = new GameObject(p.key);
            if (parent != null) go.transform.SetParent(parent, false);
            if (p.t == "wheel")
            {
                tire ??= WheelPart(0.62f, 0.85f);
                hub ??= WheelPart(0.3f, 0.9f);
                AddMesh(go, tire, MaterialFor("black", look), look);
                AddMesh(go, hub, MaterialFor("gray", look), look);
                return go;
            }
            if (p.t == "sticker")
            {
                var m = look == PieceLook.Ghost ? Mats.Ghost("white") : look == PieceLook.Drag ? Mats.DragCopy(StickerMaterial(p.art)) : StickerMaterial(p.art);
                AddMesh(go, MeshFor(p), m, look);
                if (look == PieceLook.Solid || look == PieceLook.Picture) AddStickerText(go.transform, p); // not on the hand copy (its materials get destroyed)
                return go;
            }
            AddMesh(go, MeshFor(p), MaterialFor(p.c, look), look);
            return go;
        }

        /// <summary>Puts a piece object at its spot in a model (model offset ox/oz centres the model on the origin).</summary>
        public static void Place(Transform t, Piece p, ModelDef m)
        {
            t.localRotation = Rotation(p.steps);
            t.localPosition = new Vector3(p.x + p.fw / 2f + m.ox, p.y * P, p.z + p.fd / 2f + m.oz);
        }

        /// <summary>Unity yaw +90° turns +x toward -z (E → N): one quarter turn per facing step.</summary>
        public static Quaternion Rotation(int steps) => Quaternion.Euler(0f, 90f * steps, 0f);

        /// <summary>The whole finished model under one root (origin = bottom centre).</summary>
        public static GameObject BuildModel(ModelDef m, PieceLook look = PieceLook.Solid)
        {
            var root = new GameObject(m.name);
            foreach (var p in m.pieces) Place(Create(p, look, root.transform).transform, p, m);
            return root;
        }

        private static Material MaterialFor(string color, PieceLook look)
        {
            switch (look)
            {
                case PieceLook.Ghost: return Mats.Ghost(color);
                case PieceLook.Drag: return Mats.DragCopy(Mats.Brick(color));
                default: return Mats.Brick(color);
            }
        }

        private static void AddMesh(GameObject go, Mesh mesh, Material mat, PieceLook look)
        {
            var child = go.transform.childCount == 0 && go.GetComponent<MeshFilter>() == null ? go : new GameObject("part");
            if (child != go) child.transform.SetParent(go.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = child.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            bool shadows = look == PieceLook.Solid || look == PieceLook.Drag;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = look == PieceLook.Solid;
        }

        private static Mesh WheelPart(float r, float len)
        {
            var b = new MeshBuilder { Xf = Matrix4x4.TRS(new Vector3(0, 0.62f, 0), Quaternion.Euler(90, 0, 0), Vector3.one) };
            b.Cylinder(new Vector3(0, -len / 2, 0), r, r, len, 28);
            return b.ToMesh("wheel");
        }

        public static Mesh MeshFor(Piece p)
        {
            string key = $"{p.t}:{p.w}x{p.d}x{p.h}:{p.art}";
            if (meshes.TryGetValue(key, out var cached)) return cached;
            var b = new MeshBuilder();
            int w = p.w, d = p.d;
            float H = p.h * P, g = G / 2f;
            switch (p.t)
            {
                case "brick":
                    b.Box(new Vector3(0, H / 2, 0), new Vector3(w - G, H - 0.01f, d - G));
                    Studs(b, w, d, H);
                    break;
                case "tile":
                    b.Box(new Vector3(0, H / 2, 0), new Vector3(w - G, H - 0.01f, d - G));
                    break;
                case "slope":
                case "curve":
                {
                    float x0 = -w / 2f + g, x1 = w / 2f - g, top = H - 0.01f;
                    bool flat = p.t == "slope" && w >= 2;
                    var o = new List<Vector2> { new Vector2(x0, 0), new Vector2(x1, 0), new Vector2(x1, 0.2f) };
                    if (p.t == "slope") o.Add(new Vector2(x0 + (flat ? 1f : 0f), top));
                    else
                        for (int i = 1; i <= 16; i++)
                        {
                            float t = i / 16f, u = 1 - t; // quadratic curve (x1,0.2) → (x0,top), control (x1,top): convex
                            o.Add(u * u * new Vector2(x1, 0.2f) + 2 * u * t * new Vector2(x1, top) + t * t * new Vector2(x0, top));
                        }
                    if (flat) o.Add(new Vector2(x0, top));
                    b.Extrude(o, -(d - G) / 2f, (d - G) / 2f);
                    if (flat) for (int k = 0; k < d; k++) Stud(b, new Vector3(-w / 2f + 0.5f, H, k - d / 2f + 0.5f));
                    break;
                }
                case "round":
                    b.Cylinder(Vector3.zero, w / 2f - 0.03f, w / 2f - 0.03f, H - 0.01f, 28);
                    if (w == 1) Stud(b, new Vector3(0, H, 0));
                    else
                        for (int i = 0; i < w; i++)
                            for (int k = 0; k < w; k++)
                            {
                                var s = new Vector3(i - w / 2f + 0.5f, H, k - w / 2f + 0.5f);
                                if (new Vector2(s.x, s.z).magnitude < w / 2f - 0.3f) Stud(b, s);
                            }
                    break;
                case "cone":
                    b.Cylinder(Vector3.zero, w / 2f - 0.03f, 0.08f, H, 28);
                    break;
                case "spire":
                    b.Cylinder(Vector3.zero, 0.34f, 0.34f, 0.3f, 16);
                    b.Cylinder(new Vector3(0, 0.3f, 0), 0.26f, 0.02f, H - 0.3f, 16);
                    break;
                case "dome":
                    b.Dome(Vector3.zero, w / 2f - 0.03f, H, 28);
                    break;
                case "arch":
                {
                    int leg = w >= 8 ? 2 : 1;
                    float rx = (w - 2 * leg) / 2f, ry = Mathf.Min(rx * 1.2f, H - 0.45f);
                    var o = new List<Vector2> { new Vector2(-w / 2f + g, 0), new Vector2(-rx, 0) };
                    for (int i = 1; i < 24; i++) { float a = Mathf.PI - i * Mathf.PI / 24f; o.Add(new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry)); }
                    o.Add(new Vector2(rx, 0)); o.Add(new Vector2(w / 2f - g, 0));
                    o.Add(new Vector2(w / 2f - g, H - 0.01f)); o.Add(new Vector2(-w / 2f + g, H - 0.01f));
                    b.Extrude(o, -(d - G) / 2f, (d - G) / 2f);
                    Studs(b, w, d, H);
                    break;
                }
                case "disc":
                    b.Xf = Matrix4x4.TRS(new Vector3(0, w / 2f, -0.5f + 0.09f), Quaternion.Euler(90, 0, 0), Vector3.one);
                    b.Cylinder(new Vector3(0, -0.09f, 0), w / 2f - 0.05f, w / 2f - 0.05f, 0.18f, 36);
                    break;
                case "sticker":
                {
                    bool clock = p.art == "clock";
                    float pw = clock ? w - 0.4f : w - 0.15f;
                    b.Plane(new Vector3(0, clock ? w / 2f : 0.6f, -0.5f + (clock ? 0.19f : 0.015f)), pw, pw * Stickers.Aspect(p.art));
                    break;
                }
            }
            return meshes[key] = b.ToMesh(key);
        }

        private static void Studs(MeshBuilder b, int w, int d, float H)
        {
            for (int i = 0; i < w; i++)
                for (int k = 0; k < d; k++)
                    Stud(b, new Vector3(i - w / 2f + 0.5f, H, k - d / 2f + 0.5f));
        }

        /// <summary>
        /// Our own stud: lower than the classic one with a rounded (chamfered) top edge and a plain top, so the bricks have
        /// their own look (no lettering, different proportions).
        /// </summary>
        private static void Stud(MeshBuilder b, Vector3 at)
        {
            b.Cylinder(at, 0.29f, 0.29f, 0.09f, 16, false, false);
            b.Cylinder(at + new Vector3(0, 0.09f, 0), 0.29f, 0.22f, 0.045f, 16, true, false);
        }

        // ---------- stickers ----------

        private static Material StickerMaterial(string art)
        {
            if (stickerMats.TryGetValue(art, out var m)) return m;
            m = Mats.LitTexture(StickerArt.Texture(art), true);
            m.name = "Sticker " + art;
            return stickerMats[art] = m;
        }

        /// <summary>Printed words on the stickers that need them (taxi sign, number plate, bus sign).</summary>
        private static void AddStickerText(Transform parent, Piece p)
        {
            string text = p.art == "taxi" ? "TAKSİ" : p.art == "plate" ? "34 BRK 34" : p.art == "bus" ? "34 TAKSİM" : null;
            if (text == null) return;
            float pw = p.w - 0.15f, ph = pw * Stickers.Aspect(p.art);
            var go = new GameObject("text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(p.art == "plate" ? -0.06f * pw : 0f, 0.6f, -0.5f + 0.02f);
            go.transform.localRotation = Quaternion.Euler(0, 180, 0); // TMP reads along +z; the sticker faces +z
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = Fonts.Body;
            tmp.text = text;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f; tmp.fontSizeMax = 20f;
            tmp.rectTransform.sizeDelta = new Vector2(pw * (p.art == "plate" ? 0.78f : 0.92f), ph * 0.8f);
            tmp.color = p.art == "bus" ? new Color(1f, 0.6f, 0f) : new Color(0.07f, 0.07f, 0.07f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }
}
