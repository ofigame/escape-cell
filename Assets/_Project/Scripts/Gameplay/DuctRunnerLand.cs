using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Roads on the land: most worlds lay the road on wide ground of their own (grass, sand, snow, black rock, the sea
    /// floor, a cave) with scenery spread far out on both sides, a far ring of hills, mesas, snowy peaks or volcanoes
    /// round the horizon and a sky of their own behind it. Only the sky bridge and the star path float. The ground
    /// follows the road's long bends and hills; the bends are wide enough that nothing folds over the road.
    /// </summary>
    public partial class DuctRunner
    {
        /// <summary>How far the ground reaches out to each side of the road.</summary>
        private const float GroundHalf = 34f;
        /// <summary>The road's own ground starts this many rows out from the floor's edge.</summary>
        private const int GroundFrom = 6;

        private static Mesh blobMesh;

        /// <summary>A rounded piece of scenery: one shared rounded mesh, scaled to the size.</summary>
        private static GameObject Blob(string name, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            if (blobMesh == null) blobMesh = MeshFactory.RoundedBox(Vector3.one, 0.3f);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = blobMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private bool IsLand => kind == Kind.Forest || kind == Kind.Canyon || kind == Kind.Snow || kind == Kind.Lava || kind == Kind.Ocean || kind == Kind.Crystal;

        private Material groundMat, groundFarMat, pitMat, landA, landB, landC, landD, lavaGlow, flameMat;
        private Transform skyline, farGround;
        private Texture2D backdrop;
        private float lowestY;

        /// <summary>Colours of the land worlds' ground, scenery and lava (and the whole lava world's road).</summary>
        private void LandMaterials()
        {
            groundMat = groundFarMat = pitMat = landA = landB = landC = landD = lavaGlow = flameMat = null;
            switch (kind)
            {
                case Kind.Forest:
                    groundMat = M(H("#5E9448"));
                    landA = M(H("#3F7A3A"));                                    // dark pines
                    landB = M(H("#7DB35A"));                                    // far hills
                    landC = M(H("#7A8C9E"));                                    // far mountains
                    landD = M(H("#E8EEF4"));                                    // their snow
                    break;
                case Kind.Canyon:
                    // The desert: open sand to the horizon, dunes, cacti, bleached rocks and red mesas far off.
                    frameMat = M(H("#D6A86E"), new Color(0.08f, 0.05f, 0.02f));
                    topMat = M(H("#F0D2A0"), new Color(0.08f, 0.06f, 0.03f));
                    slabMat = M(H("#B07A4A"));
                    groundMat = M(H("#E6C48C"));
                    landA = M(H("#F0D49E"));                                    // dunes
                    landB = M(H("#B8603A"));                                    // mesas
                    landC = M(H("#D88A5A"));                                    // mesa bands
                    landD = M(H("#D8D0C0"));                                    // bleached rock
                    break;
                case Kind.Snow:
                    // A glacier among mountains: snowfields, blue ice, pines and white peaks all round.
                    groundMat = M(H("#E8F0F8"), new Color(0.1f, 0.12f, 0.16f));
                    landA = MaterialFactory.CreateTransparent(new Color(0.62f, 0.85f, 1f, 0.8f), new Color(0.15f, 0.35f, 0.5f)); // glacier ice
                    landB = M(H("#6E7E94"));                                    // mountain rock
                    landC = M(H("#FFFFFF"), new Color(0.25f, 0.27f, 0.3f));     // snow caps
                    landD = M(H("#9EC8E8"), new Color(0.2f, 0.35f, 0.5f));      // ice pools
                    break;
                case Kind.Lava:
                    // Between rivers of lava: black rock, glowing cracks, flames along the edges, volcanoes smoking far off.
                    frameMat = M(H("#4A3A3A"), new Color(0.25f, 0.06f, 0.02f));
                    topMat = M(H("#6A5552"), new Color(0.1f, 0.03f, 0.01f));
                    slabMat = M(H("#2E2426"));
                    groundMat = M(H("#2B2326"));
                    themeA = M(H("#3A3032"));                                   // rocks
                    themeB = M(H("#FF7A1E"), new Color(2.4f, 0.8f, 0.12f));      // lava
                    themeC = M(H("#4E3E3C"));                                   // rock bands
                    themeD = M(H("#FFC24A"), new Color(2.6f, 1.4f, 0.3f));       // flame cores
                    landA = M(H("#24191B"));                                    // volcanoes
                    landB = themeB;
                    lavaGlow = themeB;
                    flameMat = MaterialFactory.CreateTransparent(new Color(1f, 0.45f, 0.1f, 0.75f), new Color(2.2f, 0.7f, 0.1f));
                    blockMat = M(H("#5A3A30"), new Color(0.9f, 0.25f, 0.05f));   // glowing boulders
                    hurdleMat = themeA;
                    barMat = M(H("#4E3A34"), new Color(0.5f, 0.12f, 0.02f));
                    wallGlowMat = themeB;
                    break;
                case Kind.Ocean:
                    groundMat = M(H("#D8C49A"), new Color(0.04f, 0.06f, 0.08f));
                    landA = M(H("#6E8A9A"));                                    // rocks
                    landB = M(H("#4E9A7A"));
                    break;
                case Kind.Crystal:
                    groundMat = M(H("#4A4052"));
                    landA = M(H("#5E5068"));                                    // cave rock
                    landB = M(H("#C8A8E8"), new Color(0.7f, 0.4f, 1.0f));       // great crystals
                    break;
                case Kind.Sky:
                    groundFarMat = MaterialFactory.CreateTransparent(new Color(0.86f, 0.9f, 0.98f, 0.9f), new Color(0.12f, 0.14f, 0.2f)); // the sea of clouds below
                    break;
            }
            if (groundMat != null)
            {
                var g = groundMat.color;
                groundFarMat = M(g * 0.86f + new Color(0f, 0f, 0f, 1f));
                pitMat = M(new Color(g.r * 0.25f, g.g * 0.22f, g.b * 0.22f));
            }
            if (kind == Kind.Lava) pitMat = lavaGlow; // a hole in the road drops into lava
        }

        // ---------- Ground and scenery per row ----------

        /// <summary>The ground beside the road (and a dark pit under its holes), wide enough to reach the far ring.</summary>
        private void LandGround(Transform root, int r)
        {
            if (!IsLand || r < GroundFrom || groundMat == null) return;
            float edge = WallX + 0.25f;
            float w = GroundHalf - edge;
            // A little over a row deep, so the outside of a bend never shows a gap.
            foreach (float side in new[] { -1f, 1f })
                Shapes.Primitive(PrimitiveType.Cube, "Ground", root, new Vector3(side * (edge + w * 0.5f), -0.75f, 0f), new Vector3(w, 0.9f, 2.4f), groundMat);
            // Under the lanes: solid ground where the road is whole, a deep dark pit (lava) under a hole.
            bool hole = false;
            for (int l = 0; l < Lanes; l++) hole |= !floorPlan[r, l];
            Shapes.Primitive(PrimitiveType.Cube, hole ? "Pit" : "Under", root, new Vector3(0f, hole ? -1.9f : -0.8f, 0f), new Vector3(edge * 2f, 0.9f, 1.06f), hole ? pitMat : groundMat);
        }

        /// <summary>The far scenery on both sides of a land road, past the near verges (which each world draws itself).</summary>
        private void LandScenery(Transform root, int r, System.Random rng)
        {
            if (!IsLand || r < GroundFrom + 2 || r % 2 == 1) return; // every other row is plenty, and half the objects
            foreach (float side in new[] { -1f, 1f })
            {
                // Two or three things a row per side, between 5 and 30 out: a world full of scenery, not a strip.
                int count = 1 + rng.Next(2);
                for (int i = 0; i < count; i++)
                {
                    float d = 5f + Mathf.Pow(R(rng), 0.8f) * 25f;
                    var at = new Vector3(side * d, -0.3f, (R(rng) - 0.5f) * 0.8f);
                    switch (kind)
                    {
                        case Kind.Forest: Tree(root, at, rng, R(rng) < 0.5f); break;
                        case Kind.Canyon: DesertThing(root, at, rng); break;
                        case Kind.Snow: GlacierThing(root, at, rng, d); break;
                        case Kind.Lava: LavaThing(root, at, rng); break;
                        case Kind.Ocean: if (R(rng) < 0.5f) Shapes.Primitive(PrimitiveType.Sphere, "Rock", root, at, new Vector3(1.2f, 0.8f, 1.1f) * (0.6f + R(rng) * 1.4f), landA); break;
                        case Kind.Crystal: CaveThing(root, at, rng, d); break;
                    }
                }
            }
        }

        private void Tree(Transform root, Vector3 at, System.Random rng, bool pine)
        {
            float s = 0.9f + R(rng) * 0.9f;
            if (pine)
            {
                Blob("Trunk", root, at + new Vector3(0f, 0.4f * s, 0f), new Vector3(0.22f, 0.8f, 0.22f) * s, themeC);
                for (int i = 0; i < 3; i++)
                {
                    float w = s * (1.5f - i * 0.38f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Crown", root, at + new Vector3(0f, s * (1.1f + i * 0.62f), 0f), new Vector3(w, 0.8f * s, w), landA);
                }
            }
            else
            {
                float h = (1.6f + R(rng) * 1.2f) * s;
                Blob("Trunk", root, at + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.3f * s, h, 0.3f * s), themeC);
                for (int i = 0; i < 3; i++)
                    Shapes.Primitive(PrimitiveType.Sphere, "Leaves", root, at + new Vector3((R(rng) - 0.5f) * 0.8f * s, h + (0.2f + R(rng) * 0.5f) * s, (R(rng) - 0.5f) * 0.8f * s),
                        Vector3.one * (1.1f + R(rng) * 0.7f) * s, i == 0 ? themeA : themeB);
            }
        }

        private void DesertThing(Transform root, Vector3 at, System.Random rng)
        {
            float k = R(rng);
            if (k < 0.5f)
            {
                // A dune: a long, low swell of sand.
                var dune = Shapes.Primitive(PrimitiveType.Sphere, "Dune", root, at + new Vector3(0f, -0.2f, 0f), new Vector3(4f + R(rng) * 5f, 1f + R(rng) * 1.4f, 2.5f + R(rng) * 2f), landA).transform;
                dune.localRotation = Quaternion.Euler(0f, R(rng) * 60f - 30f, 0f);
            }
            else if (k < 0.72f)
            {
                float s = 0.8f + R(rng) * 0.8f;
                Blob("Cactus", root, at + new Vector3(0f, 0.7f * s, 0f), new Vector3(0.26f, 1.4f, 0.26f) * s, themeC);
                Blob("Arm", root, at + new Vector3(0.22f * s, 0.9f * s, 0f), new Vector3(0.24f, 0.14f, 0.14f) * s, themeC);
                Blob("Arm", root, at + new Vector3(0.32f * s, 1.15f * s, 0f), new Vector3(0.14f, 0.5f, 0.14f) * s, themeC);
            }
            else if (k < 0.85f)
                Blob("Rock", root, at + new Vector3(0f, 0.3f, 0f), new Vector3(0.9f + R(rng), 0.6f + R(rng) * 0.6f, 0.8f + R(rng)), landD);
        }

        private void GlacierThing(Transform root, Vector3 at, System.Random rng, float d)
        {
            float k = R(rng);
            if (k < 0.3f && d > 9f)
            {
                // A tilted slab of glacier ice.
                var ice = Blob("Ice", root, at + new Vector3(0f, 0.9f, 0f), new Vector3(2.4f + R(rng) * 3f, 1.8f + R(rng) * 2.6f, 1.6f + R(rng) * 2f), landA).transform;
                ice.localRotation = Quaternion.Euler((R(rng) - 0.5f) * 20f, R(rng) * 90f, (R(rng) - 0.5f) * 25f);
            }
            else if (k < 0.75f)
            {
                // A snowy pine.
                float s = 0.9f + R(rng) * 0.8f;
                Blob("Trunk", root, at + new Vector3(0f, 0.3f * s, 0f), new Vector3(0.18f, 0.6f, 0.18f) * s, themeC);
                for (int i = 0; i < 3; i++)
                {
                    float w = s * (1.2f - i * 0.3f), y0 = (0.6f + i * 0.55f) * s;
                    Blob("Tier", root, at + new Vector3(0f, y0, 0f), new Vector3(w, 0.5f * s, w), themeA).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    Blob("Snow", root, at + new Vector3(0f, y0 + 0.24f * s, 0f), new Vector3(w * 0.7f, 0.08f, w * 0.7f), themeB).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                }
            }
            else if (k < 0.85f)
                Shapes.Primitive(PrimitiveType.Cylinder, "Pool", root, at + new Vector3(0f, 0.02f, 0f), new Vector3(2f + R(rng) * 2f, 0.02f, 1.4f + R(rng) * 1.5f), landD);
            else
                Shapes.Primitive(PrimitiveType.Sphere, "Drift", root, at, new Vector3(2.2f, 0.7f, 1.6f) * (0.7f + R(rng)), themeB);
        }

        private void LavaThing(Transform root, Vector3 at, System.Random rng)
        {
            float k = R(rng);
            if (k < 0.35f)
            {
                // A pool of lava with flames licking up from it.
                float s = 1.2f + R(rng) * 2.2f;
                Shapes.Primitive(PrimitiveType.Cylinder, "Lava", root, at + new Vector3(0f, 0.03f, 0f), new Vector3(s * 1.4f, 0.03f, s), lavaGlow);
                int flames = 1 + rng.Next(3);
                for (int i = 0; i < flames; i++) Flame(root, at + new Vector3((R(rng) - 0.5f) * s, 0f, (R(rng) - 0.5f) * s * 0.6f), 0.6f + R(rng) * 0.9f);
            }
            else if (k < 0.7f)
            {
                // Black rock, some of it cracked and glowing.
                var rock = Blob("Rock", root, at + new Vector3(0f, 0.4f, 0f), new Vector3(1f + R(rng) * 1.6f, 0.8f + R(rng) * 1.6f, 1f + R(rng) * 1.4f), themeA).transform;
                rock.localRotation = Quaternion.Euler(R(rng) * 15f, R(rng) * 90f, R(rng) * 15f);
                if (R(rng) < 0.4f) Shapes.Primitive(PrimitiveType.Cylinder, "Seam", root, at + new Vector3(0f, 0.02f, 0f), new Vector3(1.6f, 0.02f, 1.6f), lavaGlow);
            }
            else if (k < 0.85f)
                Flame(root, at, 0.8f + R(rng) * 1.4f); // the ground itself burning
        }

        /// <summary>A flame: a glowing core inside a see-through orange tongue, gently swaying (see <see cref="UpdateLand"/>).</summary>
        private void Flame(Transform root, Vector3 at, float size)
        {
            var f = new GameObject("Flame").transform;
            f.SetParent(root, false);
            f.localPosition = at;
            var cone = DuelStands.Cone(6);
            Piece(f, cone, new Vector3(0f, 0f, 0f), new Vector3(0.7f, 1.6f, 0.7f) * size, flameMat);
            Piece(f, cone, new Vector3(0f, 0f, 0f), new Vector3(0.35f, 0.9f, 0.35f) * size, themeD);
            flames.Add(f);
        }

        private readonly System.Collections.Generic.List<Transform> flames = new System.Collections.Generic.List<Transform>();

        private static void Piece(Transform parent, Mesh mesh, Vector3 at, Vector3 scale, Material mat)
        {
            var go = new GameObject("Piece");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void CaveThing(Transform root, Vector3 at, System.Random rng, float d)
        {
            if (R(rng) < 0.4f)
            {
                var rock = Blob("Rock", root, at + new Vector3(0f, 0.6f, 0f), new Vector3(1.4f + R(rng) * 2f, 1.2f + R(rng) * 2.5f, 1.4f + R(rng) * 2f), landA).transform;
                rock.localRotation = Quaternion.Euler(R(rng) * 20f, R(rng) * 90f, R(rng) * 20f);
            }
            else if (R(rng) < 0.5f && d > 7f)
            {
                float h = 1.5f + R(rng) * 3.5f;
                var c = Blob("Crystal", root, at + new Vector3(0f, h * 0.45f, 0f), new Vector3(0.5f, h, 0.5f), landB).transform;
                c.localRotation = Quaternion.Euler((R(rng) - 0.5f) * 30f, R(rng) * 90f, (R(rng) - 0.5f) * 30f);
            }
        }

        // ---------- The far ring and the sky ----------

        /// <summary>The ground under the horizon and the ring of far shapes, both kept round the camera; the world's own sky.</summary>
        private void StartLand()
        {
            if (!IsLand && kind != Kind.Sky && kind != Kind.Void) return; // the bonus runs keep their own look
            lowestY = 0f;
            if (centers != null) foreach (var c in centers) lowestY = Mathf.Min(lowestY, c.y);
            rig.SetBackdrop(BackdropFor(kind));
            if (groundFarMat != null)
            {
                farGround = Shapes.Primitive(PrimitiveType.Cylinder, "FarGround", null, Vector3.zero, new Vector3(380f, 0.05f, 380f), groundFarMat).transform;
            }
            skyline = new GameObject("Skyline").transform;
            var rng = new System.Random((int)kind * 101 + 7);
            float Rn(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int count = kind == Kind.Sky ? 0 : 26;
            for (int i = 0; i < count; i++)
            {
                float ang = i / (float)count * Mathf.PI * 2f + Rn(-0.1f, 0.1f);
                float dist = Rn(105f, 165f);
                var at = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                switch (kind)
                {
                    case Kind.Forest:
                        if (i % 3 == 0) { float s = Rn(30f, 55f); Piece(skyline, DuelStands.Cone(7), at, new Vector3(s, s * Rn(0.5f, 0.8f), s), landC); Piece(skyline, DuelStands.Cone(7), at + Vector3.up * s * 0.32f, new Vector3(s, s, s) * 0.36f, landD); }
                        else Shapes.Primitive(PrimitiveType.Sphere, "Hill", skyline, at, new Vector3(Rn(30f, 50f), Rn(10f, 20f), Rn(25f, 40f)), landB);
                        break;
                    case Kind.Canyon:
                        if (i % 2 == 0)
                        {
                            float h = Rn(10f, 22f), w = Rn(14f, 30f);
                            Blob("Mesa", skyline, at + Vector3.up * h * 0.5f, new Vector3(w, h, w * Rn(0.6f, 1f)), landB);
                            Blob("Band", skyline, at + Vector3.up * h * 0.6f, new Vector3(w * 1.01f, h * 0.12f, w * Rn(0.6f, 1f) * 1.01f), landC);
                        }
                        else Shapes.Primitive(PrimitiveType.Sphere, "Dune", skyline, at, new Vector3(Rn(30f, 50f), Rn(6f, 12f), Rn(20f, 35f)), landA);
                        break;
                    case Kind.Snow:
                    {
                        float s = Rn(34f, 70f), h = s * Rn(0.7f, 1.1f);
                        Piece(skyline, DuelStands.Cone(5), at, new Vector3(s, h, s), landB);
                        Piece(skyline, DuelStands.Cone(5), at + Vector3.up * h * 0.55f, new Vector3(s, h, s) * 0.45f, landC);
                        break;
                    }
                    case Kind.Lava:
                    {
                        float s = Rn(30f, 60f), h = s * Rn(0.45f, 0.7f);
                        Piece(skyline, DuelStands.Cone(8), at, new Vector3(s, h, s), landA);
                        if (i % 3 == 0) Piece(skyline, DuelStands.Cone(8), at + Vector3.up * h * 0.78f, new Vector3(s * 0.24f, h * 0.24f, s * 0.24f), lavaGlow);
                        break;
                    }
                    case Kind.Ocean:
                        Shapes.Primitive(PrimitiveType.Sphere, "Reef", skyline, at, new Vector3(Rn(25f, 45f), Rn(8f, 18f), Rn(20f, 35f)), landA);
                        break;
                    case Kind.Crystal:
                    {
                        float h = Rn(20f, 50f);
                        var c = Blob("Spire", skyline, at + Vector3.up * h * 0.4f, new Vector3(h * 0.18f, h, h * 0.18f), i % 2 == 0 ? landB : landA).transform;
                        c.localRotation = Quaternion.Euler(Rn(-15f, 15f), Rn(0f, 90f), Rn(-15f, 15f));
                        break;
                    }
                }
            }
            if (kind == Kind.Lava)
                themeDust = Ambient("Embers", Weather.AddMaterial, new Color(1.8f, 0.7f, 0.15f), 40f, 0.08f, 1.4f);
            UpdateLand(0f);
        }

        private void UpdateLand(float dt)
        {
            if (rig == null) return;
            var cam = rig.Cam.transform.position;
            float baseY = transform.TransformPoint(new Vector3(0f, lowestY, 0f)).y;
            if (farGround != null) farGround.position = new Vector3(cam.x, kind == Kind.Sky ? baseY - 7f : baseY - 1.25f, cam.z);
            if (skyline != null) skyline.position = new Vector3(cam.x, baseY - 2f, cam.z);
            if (flames.Count > 0)
            {
                // Flames flicker: each one stretches and sways on its own beat.
                for (int i = flames.Count - 1; i >= 0; i--)
                {
                    var f = flames[i];
                    if (f == null) { flames.RemoveAt(i); continue; }
                    float k = time * 7f + i * 1.7f;
                    f.localScale = new Vector3(1f, 0.8f + 0.25f * Mathf.Sin(k) + 0.1f * Mathf.Sin(k * 2.3f), 1f);
                    f.localRotation = Quaternion.Euler(Mathf.Sin(k * 0.7f) * 6f, 0f, Mathf.Cos(k * 0.9f) * 6f);
                }
            }
        }

        private void StopLand()
        {
            if (farGround != null) Destroy(farGround.gameObject);
            if (skyline != null) Destroy(skyline.gameObject);
            farGround = skyline = null;
            flames.Clear();
            if (rig != null) rig.SetBackdrop(null);
        }

        /// <summary>The world's sky: a soft gradient from its top colour through a bright haze at the horizon to its ground colour.</summary>
        private Texture BackdropFor(Kind k)
        {
            if (k == Kind.Void) return SquashBot.UI.UiSprites.Galaxy.texture;
            (Color top, Color haze, Color low) c = k switch
            {
                Kind.Forest => (H("#5FA8E8"), H("#E4F2F4"), H("#7FAE68")),
                Kind.Canyon => (H("#6AB0E6"), H("#FBE6C4"), H("#E2BE88")),
                Kind.Snow => (H("#7EA6D8"), H("#EAF2FA"), H("#DCE6F0")),
                Kind.Lava => (H("#2A0E12"), H("#E2561E"), H("#2B2326")),
                Kind.Ocean => (H("#0E3A62"), H("#4A9AC0"), H("#B8A884")),
                Kind.Crystal => (H("#140E22"), H("#5A3E7A"), H("#3A3244")),
                Kind.Sky => (H("#5E9EF0"), H("#FCEADA"), H("#FFFFFF")),
                _ => (H("#3E5A8A"), H("#C8D8E8"), H("#506070")),
            };
            if (backdrop != null) Destroy(backdrop);
            const int Hgt = 256;
            backdrop = new Texture2D(4, Hgt, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[4 * Hgt];
            for (int y = 0; y < Hgt; y++)
            {
                float t = y / (float)(Hgt - 1);
                const float horizon = 0.46f;
                var col = t > horizon ? Color.Lerp(c.haze, c.top, Mathf.Pow((t - horizon) / (1f - horizon), 0.6f))
                    : Color.Lerp(c.low, c.haze, Mathf.Pow(t / horizon, 6f));
                col.a = 1f;
                for (int x = 0; x < 4; x++) px[y * 4 + x] = col;
            }
            backdrop.SetPixels(px);
            backdrop.Apply(false, false);
            return backdrop;
        }
    }
}
