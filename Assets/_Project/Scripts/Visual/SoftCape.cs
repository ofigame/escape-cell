using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// foi's capes, as cloth: a sheet of points pinned across the back of the shoulders, falling under gravity, swung by
    /// how the body moves, and kept off the body by round shapes along its bones (back, hips, legs), so it lies on the
    /// back and drapes. In their tiers: plain ones (one colour) and middle ones (a light pattern) hang from a cord tied
    /// round the neck with a knot in front; the high ones are worn from the shoulders under a mantle with clasps, with
    /// gold edges and patterns of their own (diamonds, flames, snowflakes); the ranks' capes carry a beast in their
    /// metal (a bronze snake, a silver eagle, a gold wolf on green, a gold lion on the marshal's claret cape, which is
    /// broader, trails on the ground, has gold epaulettes and comes down the chest in front). Sits under the model's root
    /// and finds the bones itself, so copies of foi carry a working cape too.
    /// </summary>
    public class SoftCape : MonoBehaviour
    {
        private const int Cols = 11, Rows = 13, Iterations = 6;

        public Color color = Color.red;
        public string id = "back.cape";

        private Transform root, chest, neck, armL, armR, hips, legL, legR, kneeL, kneeR;
        private Vector3[] pos, prev;
        private Mesh mesh;
        private Material material;
        private Vector3[] verts;
        private float unit = 1f, span = 0.3f, length, topWidth, flare = 1.45f;
        private bool ready;



        // A cape made as a model, worn as it is: hung from the shoulders, swung back as foi moves, a slow breath of wind.
        private Transform worn;
        private bool wornFlip;
        private Vector3 lastShoulders;
        private float swing, wave;
        /// <summary>How far the cape's middle reaches toward the body from where it hangs (its own depth there).</summary>
        private float wornDepth;

        /// <summary>The cape's middle part (not its edges, which wrap round the sides): how far it comes toward the body.</summary>
        private static float BodySide(Transform worn, bool flip)
        {
            float half = 0f, most = 0f;
            var pts = new System.Collections.Generic.List<Vector3>();
            foreach (var mf in worn.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                foreach (var v in mf.sharedMesh.vertices) pts.Add(worn.InverseTransformPoint(mf.transform.TransformPoint(v)));
            }
            foreach (var p in pts) half = Mathf.Max(half, Mathf.Abs(p.x));
            foreach (var p in pts) if (Mathf.Abs(p.x) < half * 0.3f) most = Mathf.Max(most, flip ? -p.z : p.z);
            return most * worn.lossyScale.z; // (in the world)
        }

        private void HangWorn()
        {
            Frame(out var right, out var up, out var back, out var shoulders);
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            var v = (shoulders - lastShoulders) / dt; lastShoulders = shoulders;
            float forward = Vector3.Dot(v, -back) / Mathf.Max(1e-4f, unit); // moving ahead swings it back
            swing = Mathf.Lerp(swing, Mathf.Clamp(forward * 3f, -2f, 7f), dt * 3f); // (only a little: it never flies up)
            wave += dt;
            float sway = Mathf.Sin(wave * 1.5f) * 1.8f + Mathf.Sin(wave * 0.7f) * 1.2f; // a soft breath, standing still or not
            // its body side just behind the back (the armour is broad): never into the body
            float torso = Mathf.Max(0.09f * unit, span * 0.27f); // (close behind the back, its middle just off it)
            worn.position = shoulders + up * 0.05f * unit + back * (torso + Mathf.Clamp(wornDepth, 0f, span * 0.06f)); // (close on the back)
            var face = wornFlip ? back : -back; // (the model's front made to face the body or away, as it was made)
            worn.rotation = Quaternion.AngleAxis(swing + sway, right) * Quaternion.LookRotation(face, up);
            Ripple(Mathf.Abs(forward) + Mathf.Abs(v.y) / Mathf.Max(1e-4f, unit) * 0.5f);
            // A long cape (or one on a foi reeling, flung, lying) reaching below the floor: shortened to end on it, as if
            // its hem lay on the ground (never through it).
            if (!wornScaleKnown) { wornScale = worn.localScale; wornScaleKnown = true; }
            worn.localScale = wornScale;
            if (wornRenderers == null) wornRenderers = worn.GetComponentsInChildren<Renderer>(true);
            if (wornRenderers.Length == 0) return;
            float low = float.MaxValue;
            foreach (var r in wornRenderers) if (r != null) low = Mathf.Min(low, r.bounds.min.y);
            float floor = (root.parent != null ? root.parent.position.y : root.position.y) + 0.015f * unit;
            float top = worn.position.y;
            if (low < floor && top > floor + 0.05f * unit)
                worn.localScale = new Vector3(wornScale.x, wornScale.y * Mathf.Max(0.3f, (top - floor) / Mathf.Max(1e-4f, top - low)), wornScale.z);
        }

        /// <summary>
        /// Takes the cape off: it stops following the body; a made cape is handed back (to be hung up somewhere), a cloth
        /// one simply goes. Null when there was nothing to hand back.
        /// </summary>
        public Transform TakeOff()
        {
            enabled = false;
            if (fittings != null) fittings.gameObject.SetActive(false);
            var r = GetComponent<Renderer>(); if (r != null) r.enabled = false;
            var mr = GetComponentsInChildren<Renderer>(true);
            if (worn == null) { foreach (var x in mr) x.enabled = false; return null; }
            var off = worn;
            off.SetParent(null, true);
            worn = null;
            // its moving copies of the meshes go with it (this cape's own going, when foi is dressed again after a knock-down,
            // must not take them off the post)
            var keep = off.gameObject.AddComponent<HungCape>();
            foreach (var c in cloth) if (c.mesh != null) keep.meshes.Add(c.mesh);
            cloth.Clear();
            clothMade = true; // (never made again for a cape no longer worn)
            foreach (var x in GetComponentsInChildren<Renderer>(true)) x.enabled = false;
            return off;
        }

        private Vector3 wornScale;
        private bool wornScaleKnown;
        private Renderer[] wornRenderers;

        // The made cape as cloth: its own copy of each mesh, each point moved every frame by how far down the cape it is
        // (the top at the shoulders still, the hem the most): waves running down it, the hem fluttering, a gust as foi moves.
        private class Cloth
        {
            public Mesh mesh;
            public Vector3[] rest, now;
            public float[] down, across;
            public Vector3 depth, side; // (the cape's own back-and-forth and side to side, in the mesh's own space)
        }

        private readonly List<Cloth> cloth = new List<Cloth>();
        private bool clothMade;
        private float gust;

        private void MakeCloth()
        {
            clothMade = true;
            foreach (var mf in worn.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var c = new Cloth { mesh = Instantiate(mf.sharedMesh) };
                c.mesh.MarkDynamic();
                mf.sharedMesh = c.mesh;
                c.rest = c.mesh.vertices;
                c.now = (Vector3[])c.rest.Clone();
                var toWorn = worn.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var toMesh = toWorn.inverse;
                c.depth = toMesh.MultiplyVector(Vector3.forward).normalized;
                c.side = toMesh.MultiplyVector(Vector3.right).normalized;
                // how far down the cape (0 the shoulders .. 1 the hem) and across it (-1 .. 1) each point is
                float top = float.MinValue, bottom = float.MaxValue, half = 1e-4f;
                var at = new Vector3[c.rest.Length];
                for (int i = 0; i < at.Length; i++)
                {
                    at[i] = toWorn.MultiplyPoint3x4(c.rest[i]);
                    top = Mathf.Max(top, at[i].y); bottom = Mathf.Min(bottom, at[i].y); half = Mathf.Max(half, Mathf.Abs(at[i].x));
                }
                c.down = new float[at.Length]; c.across = new float[at.Length];
                for (int i = 0; i < at.Length; i++)
                {
                    c.down[i] = Mathf.Clamp01((top - at[i].y) / Mathf.Max(1e-4f, top - bottom));
                    c.across[i] = at[i].x / half;
                }
                cloth.Add(c);
            }
        }

        private void Ripple(float moving)
        {
            if (!clothMade) MakeCloth();
            if (cloth.Count == 0) return;
            gust = Mathf.Lerp(gust, Mathf.Clamp(moving, 0f, 3f), Time.deltaTime * 2.5f);
            float scale = Mathf.Max(1e-4f, Mathf.Abs(worn.lossyScale.z));
            float amp = 0.055f * unit / scale * (1f + 0.7f * gust); // (in the cape's own units: a few centimetres, more as it moves)
            float t = wave;
            foreach (var c in cloth)
            {
                for (int i = 0; i < c.rest.Length; i++)
                {
                    float d = c.down[i], a = c.across[i], w = d * d; // (the shoulders still, the hem the most)
                    float roll = Mathf.Sin(t * 2.4f - d * 5.5f + a * 1.3f) + 0.5f * Mathf.Sin(t * 3.7f - d * 8f - a * 2.1f); // waves running down it
                    float flutter = Mathf.Sin(t * 4.3f + a * 4f - d * 6f) * Mathf.Abs(a); // the edges and hem fluttering
                    c.now[i] = c.rest[i] + c.depth * (amp * w * (roll + gust * 0.9f)) + c.side * (amp * 0.45f * w * flutter);
                }
                c.mesh.SetVertices(c.now);
            }
        }

        private int Tier => id == "back.cape" || id == "back.cape.sky" || id == "back.cape.sun" ? 0
            : id == "back.cape.night" || id == "back.cape.teal" || id == "back.cape.wine" ? 1
            : id.StartsWith("back.cape.rank") ? 3 : 2;
        private bool Marshal => id == "back.cape.rank12";
        private Color Metal => id == "back.cape.rank3" ? new Color(0.78f, 0.48f, 0.22f) : id == "back.cape.rank6" ? new Color(0.88f, 0.9f, 0.97f) : new Color(1f, 0.8f, 0.3f);

        /// <summary>Hangs cape <paramref name="capeId"/> in <paramref name="capeColor"/> on the rigged model under <paramref name="skin"/>.</summary>
        public static SoftCape Build(Transform skin, string capeId, Color capeColor)
        {
            var go = new GameObject("Hero_Cape");
            go.transform.SetParent(skin, false);
            var cape = go.AddComponent<SoftCape>();
            cape.id = capeId; cape.color = capeColor;
            return cape;
        }

        private static Transform Bone(Transform under, string name)
        {
            foreach (var t in under.GetComponentsInChildren<Transform>(true))
                if (t.name == name || t.name.EndsWith(":" + name)) return t;
            return null;
        }

        private void Setup()
        {
            root = transform.parent;
            chest = Bone(root, "Spine2"); neck = Bone(root, "Neck");
            armL = Bone(root, "LeftArm"); armR = Bone(root, "RightArm"); hips = Bone(root, "Hips");
            legL = Bone(root, "LeftUpLeg"); legR = Bone(root, "RightUpLeg"); kneeL = Bone(root, "LeftLeg"); kneeR = Bone(root, "RightLeg");
            if (chest == null || armL == null || armR == null || hips == null) { enabled = false; return; }
            unit = Vector3.Distance(hips.position, neck != null ? neck.position : chest.position) / 0.26f; // a body's size, from hips to neck
            span = Vector3.Distance(armL.position, armR.position);
            int tier = Tier;
            topWidth = Vector3.Distance(armL.position, armR.position) * (tier <= 1 ? 0.95f : Marshal ? 1.4f : 1.2f);
            flare = tier <= 1 ? 1.7f : Marshal ? 1.35f : 1.4f;
            float shoulderY = (armL.position.y + armR.position.y) * 0.5f;
            float kneeY = kneeL != null && kneeR != null ? (kneeL.position.y + kneeR.position.y) * 0.5f : hips.position.y - 0.2f * unit;
            length = Marshal ? (shoulderY - root.position.y) * 1.08f : shoulderY - kneeY;

            if (GearFiles.CapeModel(id) != null)
            {
                // a cape made as a model hangs as it was made (its own folds and colours), from the shoulders, swaying
                // as long as foi is tall from the shoulders nearly to its feet, and never narrower than the back it covers
                float wornLength = (shoulderY - FeetY()) * 0.86f; // (ending above the feet: never into the floor)
                var old = transform.Find(id); if (old != null) DestroyImmediate(old.gameObject); // (a copy of foi brings the original's along: one only)
                worn = GearFiles.CapePicture(transform, id, wornLength);
                if (worn != null)
                {
                    var rs = worn.GetComponentsInChildren<Renderer>(true);
                    var wb = rs[0].bounds; foreach (var rr in rs) wb.Encapsulate(rr.bounds);
                    float wide = Mathf.Max(wb.size.x, wb.size.z), need = span * 1.55f;
                    if (wide < need) worn.localScale = Vector3.Scale(worn.localScale, new Vector3(need / wide, 1f, need / wide));
                }
                if (worn != null)
                {
                    foreach (var tr in worn.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = gameObject.layer;
                    wornFlip = GearFiles.CapeFlipped(id);
                    wornDepth = BodySide(worn, wornFlip);
                    lastShoulders = (armL.position + armR.position) * 0.5f;
                    ready = true;
                    return;
                }
            }
            int n = (Cols + 1) * (Rows + 1);
            pos = new Vector3[n]; prev = new Vector3[n];
            for (int j = 0; j <= Rows; j++)
                for (int i = 0; i <= Cols; i++) pos[j * (Cols + 1) + i] = prev[j * (Cols + 1) + i] = Rest(i, j);

            // the mesh: an outer and an inner face (a cloth is seen from both sides), its UVs across and down
            mesh = new Mesh { name = "Cape" };
            mesh.MarkDynamic();
            verts = new Vector3[n * 2];
            var uv = new Vector2[n * 2];
            var tris = new List<int>();
            for (int j = 0; j <= Rows; j++)
                for (int i = 0; i <= Cols; i++)
                {
                    int k = j * (Cols + 1) + i;
                    uv[k + n] = new Vector2(i / (float)Cols * 0.5f, 1f - j / (float)Rows); // outer (the second face, toward the back): the left half of the cloth's picture
                    uv[k] = new Vector2(0.5f + i / (float)Cols * 0.5f, 1f - j / (float)Rows); // inner (toward the body): its lining
                }
            for (int j = 0; j < Rows; j++)
                for (int i = 0; i < Cols; i++)
                {
                    int a = j * (Cols + 1) + i, b = a + 1, c = a + Cols + 1, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                    tris.AddRange(new[] { a + n, b + n, c + n, b + n, d + n, c + n });
                }
            mesh.vertices = verts; mesh.uv = uv; mesh.SetTriangles(tris, 0);
            var mf = GetComponent<MeshFilter>(); if (mf == null) mf = gameObject.AddComponent<MeshFilter>(); // (a copy of foi brings them along)
            mf.sharedMesh = mesh;
            var r = GetComponent<MeshRenderer>(); if (r == null) r = gameObject.AddComponent<MeshRenderer>();
            material = MaterialFactory.CreateSurface(Color.white, Color.black, 0.2f, tier >= 2 ? 0.15f : 0f);
            var tex = Fabric(color);
            material.SetTexture("_BaseMap", tex); material.SetTexture("_MainTex", tex);
            if (Marshal)
            {
                glow = new Texture2D(32, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Cape glow" };
                glowMask = new float[32 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        // the pattern's place on the outer face (the left half of the picture), sampled down
                        var p = tex.GetPixelBilinear(x / 31f * 0.5f, y / 63f);
                        var g = Metal;
                        glowMask[y * 32 + x] = Mathf.Abs(p.r - g.r) + Mathf.Abs(p.g - g.g) + Mathf.Abs(p.b - g.b) < 0.25f ? 1f : 0f;
                    }
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetColor("_EmissionColor", new Color(2.2f, 1.6f, 0.5f));
                material.SetTexture("_EmissionMap", glow);
            }
            r.sharedMaterial = material;
            r.shadowCastingMode = Shapes.CastShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (transform.Find("Fittings") == null && chest.Find("Fittings") == null) Fittings(tier);
            ready = true;
        }

        /// <summary>Where a point of the cape hangs at rest: across the back of the shoulders (or the neck), falling straight, widening.</summary>
        private Vector3 Rest(int i, int j)
        {
            float u = i / (float)Cols - 0.5f, v = j / (float)Rows;
            Frame(out var right, out var up, out var back, out var shoulders);
            float w = topWidth * Mathf.Lerp(1f, flare, v);
            Vector3 top;
            if (Tier <= 1) top = shoulders + up * 0.03f * unit + back * (span * 0.4f + 0.03f * (1f - 4f * u * u) * unit); // from the cord round the neck, over the back plate
            else
            {
                // from the shoulders: the edges lie on the shoulder tops, the middle on the back
                float side = Mathf.Abs(u) * 2f;
                top = shoulders + up * (0.04f + 0.05f * side * side) * unit + back * (span * 0.4f * (1f - side * side) + span * 0.08f);
            }
            return top + right * u * w - Vector3.up * v * length + back * v * 0.05f * unit;
        }

        private void Frame(out Vector3 right, out Vector3 up, out Vector3 back, out Vector3 shoulders)
        {
            right = (armR.position - armL.position).normalized;
            up = (neck != null ? neck.position - chest.position : chest.up).normalized;
            back = Vector3.Cross(up, right).normalized; // behind the body
            shoulders = (armL.position + armR.position) * 0.5f;
        }

        /// <summary>What holds the cape on: a cord and a knot at the neck, or a mantle with clasps (the marshal's with epaulettes and a front).</summary>
        private void Fittings(int tier)
        {
            Frame(out var right, out var up, out var back, out var shoulders);
            var fit = new GameObject("Fittings").transform;
            fit.SetParent(transform, false);
            fit.SetPositionAndRotation(shoulders, Quaternion.LookRotation(-back, up));
            fit.localScale = Vector3.one;
            float s = unit / Mathf.Max(1e-5f, fit.lossyScale.x); // (in the body's own size)
            var cloth = MaterialFactory.CreateSurface(color, Color.black, 0.2f, 0f);
            var metal = MaterialFactory.CreateSurface(Metal, Metal * 0.3f, 0.8f, 0.85f);
            Transform R(string n, Vector3 p, Vector3 size, float r, Material m) => Shapes.Rounded(n, fit, p * s, size * s, r * s, m).transform;
            if (tier <= 1)
            {
                var cord = MaterialFactory.CreateSurface(Color.Lerp(color, Color.black, 0.35f), Color.black, 0.3f, 0f);
                for (int k = 0; k < 10; k++)
                {
                    float a = Mathf.Lerp(-150f, 150f, k / 9f) * Mathf.Deg2Rad;
                    R("Cord", new Vector3(Mathf.Sin(a) * 0.055f, 0.07f, Mathf.Cos(a) * 0.05f), new Vector3(0.022f, 0.016f, 0.022f), 0.008f, cord);
                }
                Shapes.Primitive(PrimitiveType.Sphere, "Knot", fit, new Vector3(0f, 0.065f, 0.058f) * s, Vector3.one * 0.03f * s, cord);
            }
            else
            {
                float mw = Marshal ? 0.34f : 0.28f;
                R("Mantle", new Vector3(0f, 0.035f, -0.005f), new Vector3(mw, 0.05f, 0.13f), 0.025f, cloth);
                R("MantleEdge", new Vector3(0f, 0.012f, -0.005f), new Vector3(mw + 0.01f, 0.012f, 0.135f), 0.006f, metal);
                foreach (float x in new[] { -1f, 1f })
                    Shapes.Primitive(PrimitiveType.Sphere, "Clasp", fit, new Vector3(x * 0.06f, 0.03f, 0.065f) * s, Vector3.one * 0.026f * s, metal);
                if (Marshal)
                    foreach (float x in new[] { -1f, 1f })
                    {
                        Shapes.Primitive(PrimitiveType.Cylinder, "Epaulette", fit, new Vector3(x * 0.13f, 0.055f, 0f) * s, new Vector3(0.085f, 0.01f, 0.085f) * s, metal);
                        for (int k = 0; k < 5; k++)
                            Shapes.Primitive(PrimitiveType.Sphere, "Fringe", fit, new Vector3(x * (0.1f + k * 0.014f), 0.04f, 0.035f - (k % 2) * 0.008f) * s, new Vector3(0.01f, 0.024f, 0.01f) * s, metal);
                        R("Front", new Vector3(x * 0.045f, -0.06f, 0.06f), new Vector3(0.04f, 0.13f, 0.01f), 0.005f, cloth);
                        R("FrontEdge", new Vector3(x * 0.026f, -0.06f, 0.066f), new Vector3(0.007f, 0.13f, 0.004f), 0.002f, metal);
                    }
            }
            foreach (var tr in fit.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = gameObject.layer;
            fit.SetParent(chest, true); // rides the chest (built where it is now, so it stays put on the body)
            fittings = fit;
        }

        private Transform fittings;
        private Texture2D glow;
        private float[] glowMask;
        private readonly Color32[] glowPixels = new Color32[32 * 64];

        /// <summary>The marshal's pattern lit by light running down the cloth in waves.</summary>
        private void Shine()
        {
            if (glow == null) return;
            float t = Time.time;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 32; x++)
                {
                    float m = glowMask[y * 32 + x];
                    // the picture's rows run up the cloth: waves come down from the shoulders, a little slanted
                    float wave = 0.25f + 0.75f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin((1f - y / 63f) * 9f + x * 0.12f - t * 3.2f), 3f);
                    byte b = (byte)(255f * m * wave);
                    glowPixels[y * 32 + x] = new Color32(b, b, b, 255);
                }
            glow.SetPixels32(glowPixels);
            glow.Apply(false);
        }

        private Transform footL, footR;

        /// <summary>The floor under foi: its lower foot (or its root).</summary>
        private float FeetY()
        {
            if (footL == null) footL = Bone(root, "LeftFoot");
            if (footR == null) footR = Bone(root, "RightFoot");
            if (footL == null || footR == null) return root.position.y;
            return Mathf.Min(footL.position.y, footR.position.y) - 0.02f * unit;
        }

        /// <summary>Hangs it at once and lets it fall still (for a picture taken now).</summary>
        public void Settle(int steps = 45)
        {
            if (!ready) { Setup(); if (!ready) return; }
            if (worn != null) { HangWorn(); return; }
            for (int s = 0; s < steps; s++) Step(1f / 30f);
        }

        private void LateUpdate()
        {
            if (!ready) { Setup(); if (!ready) return; }
            if (worn != null) { HangWorn(); return; }
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f) return;
            Step(dt);
        }

        private void Step(float dt)
        {
            int stride = Cols + 1, n = pos.Length;
            var gravity = Vector3.down * 2.6f * unit;
            // move: the top row is pinned to the shoulders, the rest falls and keeps its swing
            for (int i = 0; i <= Cols; i++) { pos[i] = Rest(i, 0); prev[i] = pos[i]; }
            for (int k = stride; k < n; k++)
            {
                var p = pos[k];
                var vel = (p - prev[k]) * 0.94f;
                prev[k] = p;
                pos[k] = p + vel + gravity * dt * dt;
            }
            float dy = length / Rows;
            float ground = FeetY() + 0.01f * unit; // (the floor where foi stands, wherever its root is)
            for (int it = 0; it < Iterations; it++)
            {
                for (int j = 0; j <= Rows; j++)
                {
                    float rowW = topWidth * Mathf.Lerp(1f, flare, j / (float)Rows) / Cols;
                    for (int i = 0; i < Cols; i++) Keep(j * stride + i, j * stride + i + 1, rowW, j == 0);
                }
                for (int j = 0; j < Rows; j++)
                    for (int i = 0; i <= Cols; i++) Keep(j * stride + i, (j + 1) * stride + i, dy, j == 0);
                for (int j = 0; j + 2 <= Rows; j++)
                    for (int i = 0; i <= Cols; i++) Keep(j * stride + i, (j + 2) * stride + i, dy * 2f * 0.98f, j == 0, 0.3f);
                for (int k = stride; k < n; k++)
                {
                    var q = OffBody(pos[k]);
                    if (q.y < ground) q.y = ground; // the marshal's train lies on the ground
                    pos[k] = q;
                }
            }
            var toLocal = transform.worldToLocalMatrix;
            for (int k = 0; k < n; k++) verts[k] = verts[k + n] = toLocal.MultiplyPoint3x4(pos[k]);
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (Marshal && (Time.frameCount & 1) == 0) Shine();
        }

        /// <summary>Two points held at their length (a pinned first one does not move).</summary>
        private void Keep(int a, int b, float rest, bool aPinned, float stiffness = 1f)
        {
            var d = pos[b] - pos[a];
            float len = d.magnitude;
            if (len < 1e-6f) return;
            var fix = d * ((len - rest) / len) * stiffness;
            if (aPinned) pos[b] -= fix;
            else { pos[a] += fix * 0.5f; pos[b] -= fix * 0.5f; }
        }

        /// <summary>Pushes a point out of the body's round shapes (back, hips, legs).</summary>
        private Vector3 OffBody(Vector3 p)
        {
            float torso = Mathf.Max(0.115f * unit, span * 0.4f), leg = Mathf.Max(0.06f * unit, span * 0.24f); // (as broad as the body: armour is)
            p = OutOf(p, hips.position, chest.position, torso);
            if (neck != null) p = OutOf(p, chest.position, neck.position, torso * 0.85f);
            if (legL != null && kneeL != null) p = OutOf(p, legL.position, kneeL.position, leg);
            if (legR != null && kneeR != null) p = OutOf(p, legR.position, kneeR.position, leg);
            return p;
        }

        private static Vector3 OutOf(Vector3 p, Vector3 a, Vector3 b, float r)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            var c = a + ab * t;
            var d = p - c;
            float len = d.magnitude;
            return len < r && len > 1e-6f ? c + d * (r / len) : p;
        }

        private static readonly Dictionary<string, Texture2D> fabrics = new Dictionary<string, Texture2D>();

        /// <summary>
        /// The cloth's picture: the outer face on the left half (its colour with a soft weave, and by tier a pale band
        /// and dots, gold edges and hem with a pattern, or a rank's beast in its metal), the lining on the right half.
        /// </summary>
        private Texture2D Fabric(Color c)
        {
            string key = id + c;
            if (fabrics.TryGetValue(key, out var made) && made != null) return made;
            const int W = 128, H = 128, Half = W / 2;
            var px = new Color[W * H];
            int tier = Tier;
            var gold = Metal;
            var light = Color.Lerp(c, Color.white, 0.5f);
            var lining = Color.Lerp(c, Color.black, 0.45f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool outer = x < Half;
                    float u = (outer ? x : x - Half) / (float)(Half - 1), v = 1f - y / (float)(H - 1); // v: 0 top .. 1 hem
                    float weave = 0.94f + 0.06f * Mathf.Sin(x * 1.9f) * Mathf.Sin(y * 1.9f);
                    var col = (outer ? c : lining) * weave;
                    if (outer)
                    {
                        bool edge = u < 0.05f || u > 0.95f, hem = v > 0.93f;
                        if (tier == 0 && hem) col = lining;
                        if (tier == 1)
                        {
                            if (hem || Mathf.Abs(v - 0.35f) < 0.018f || Mathf.Abs(v - 0.62f) < 0.018f) col = light;
                            for (int k = -2; k <= 2; k++)
                                if (Vector2.Distance(new Vector2(u, v), new Vector2(0.5f + k * 0.17f, 0.485f)) < 0.03f) col = light;
                        }
                        if (tier >= 2)
                        {
                            if (edge || hem) col = gold;
                            if (tier == 2 && Pattern(id, u, v)) col = gold;
                            if (tier == 3 && Beast(id, u, v)) col = Color.Lerp(gold, Color.white, 0.15f);
                        }
                    }
                    col.a = 1f;
                    px[y * W + x] = col;
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Cape " + id };
            tex.SetPixels(px);
            tex.Apply();
            fabrics[key] = tex;
            return tex;
        }

        /// <summary>The high capes' gold patterns: flames rising from the hem, snowflakes, or diamonds down the middle with a band of small ones.</summary>
        private static bool Pattern(string capeId, float u, float v)
        {
            switch (capeId)
            {
                case "back.cape.flame":
                    for (int k = 0; k < 4; k++)
                    {
                        float cx = 0.2f + k * 0.2f, h = 0.32f + (k % 2) * 0.1f;
                        if (v < 1f - h) continue;
                        float t = (v - (1f - h)) / h; // 0 at the tip .. 1 at the hem
                        if (Mathf.Abs(u - cx - Mathf.Sin(t * 6f) * 0.02f) < 0.075f * t) return true;
                    }
                    return false;
                case "back.cape.frost":
                    foreach (var f in new[] { new Vector2(0.28f, 0.3f), new Vector2(0.72f, 0.45f), new Vector2(0.38f, 0.65f), new Vector2(0.68f, 0.8f) })
                    {
                        var d = new Vector2(u - f.x, (v - f.y) * 1.3f);
                        if (d.magnitude > 0.09f) continue;
                        for (int r = 0; r < 3; r++)
                        {
                            float a = r * Mathf.PI / 3f;
                            if (Mathf.Abs(d.x * Mathf.Sin(a) - d.y * Mathf.Cos(a)) < 0.012f) return true;
                        }
                    }
                    return false;
                default:
                    for (int k = 0; k < 4; k++)
                        if (Mathf.Abs(u - 0.5f) + Mathf.Abs(v - (0.2f + k * 0.2f)) * 0.8f < 0.06f) return true;
                    for (int k = -2; k <= 2; k++)
                        if (k != 0 && Mathf.Abs(u - (0.5f + k * 0.16f)) + Mathf.Abs(v - 0.4f) * 0.8f < 0.03f) return true;
                    return false;
            }
        }

        /// <summary>A rank cape's beast on its back: a snake winding up, an eagle with spread wings, a wolf's head, a lion in its mane.</summary>
        private static bool Beast(string capeId, float u, float v)
        {
            var p = new Vector2(u - 0.5f, 0.38f - v); // centred high on the back, y up
            switch (capeId)
            {
                case "back.cape.rank3":
                    for (int i = 0; i < 9; i++)
                        if ((p - new Vector2(Mathf.Sin(i * 0.95f) * 0.1f, -0.13f + i * 0.033f)).magnitude < (i == 8 ? 0.04f : 0.03f)) return true;
                    return false;
                case "back.cape.rank6":
                    if (new Vector2(p.x / 0.05f, p.y / 0.1f).magnitude < 1f) return true; // body
                    if ((p - new Vector2(0f, 0.11f)).magnitude < 0.035f) return true; // head
                    foreach (float s in new[] { -1f, 1f })
                    {
                        float wx = p.x * s - 0.13f, wy = p.y - 0.03f + p.x * s * 0.25f;
                        if (Mathf.Abs(wx) < 0.1f && Mathf.Abs(wy) < 0.03f + (0.1f - Mathf.Abs(wx)) * 0.15f) return true; // wings
                    }
                    return Mathf.Abs(p.x) < 0.04f && p.y < -0.08f && p.y > -0.15f; // tail
                case "back.cape.rank9":
                    if (new Vector2(p.x / 0.09f, p.y / 0.08f).magnitude < 1f) return true;
                    if (Mathf.Abs(p.x) < 0.035f && p.y < -0.03f && p.y > -0.13f) return true; // snout
                    foreach (float s in new[] { -1f, 1f })
                        if (p.y > 0.03f && p.y < 0.14f && Mathf.Abs(p.x * s - 0.05f - (p.y - 0.03f) * 0.2f) < 0.03f * (1f - (p.y - 0.03f) / 0.11f)) return true; // ears
                    return false;
                default: // a lion's face in a full mane
                    float r = p.magnitude;
                    if (r < 0.075f) return true;
                    return r < 0.13f && Mathf.Sin(Mathf.Atan2(p.y, p.x) * 12f) > -0.3f;
            }
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            if (glow != null) Destroy(glow);
            if (fittings != null) Destroy(fittings.gameObject);
            foreach (var c in cloth) if (c.mesh != null) Destroy(c.mesh); // (the cape's own moving copies)
        }
    }

    /// <summary>A cape taken off and hung up: it owns its meshes now (freed when it goes).</summary>
    public class HungCape : MonoBehaviour
    {
        public readonly System.Collections.Generic.List<Mesh> meshes = new System.Collections.Generic.List<Mesh>();

        private void OnDestroy()
        {
            foreach (var m in meshes) if (m != null) Destroy(m);
        }
    }
}
