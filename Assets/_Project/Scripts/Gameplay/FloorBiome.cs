using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// A floor stands in its world's own land, the same one its road runs through: the stages rise out of wide ground
    /// (grass, sand, snow, black rock, the sea floor, a cave), and the space round them and between them — never on a
    /// tile, a bridge, a rampart or the stands — fills with that world's things: trees, dunes and cacti, glacier ice and
    /// pines, lava pools and flames, coral and rocks, crystals. Tall things only stand behind and between the stages;
    /// in front of the camera only low ones, under the floor's edge, so nothing ever hides the fight. The sky and the
    /// stars float as before, over a sea of clouds or the galaxy. Everything is still, merged into a few meshes with the
    /// fortress (one draw a material).
    /// </summary>
    public static class FloorBiome
    {
        /// <summary>The ground's top, just under the platforms' undersides.</summary>
        private const float GroundY = -0.7f;
        /// <summary>No scenery this close to a tile (tiles, ramparts and corner towers keep clear).</summary>
        private const float Clearance = 2.7f;

        private static readonly Dictionary<DuctRunner.Kind, Texture2D> skies = new Dictionary<DuctRunner.Kind, Texture2D>();
        private static Mesh blob;

        public static bool IsLand(DuctRunner.Kind k) =>
            k == DuctRunner.Kind.Forest || k == DuctRunner.Kind.Canyon || k == DuctRunner.Kind.Snow || k == DuctRunner.Kind.Lava || k == DuctRunner.Kind.Ocean || k == DuctRunner.Kind.Crystal;

        /// <summary>Builds the world's land round the floor under <paramref name="parent"/>; returns its sky for the backdrop.</summary>
        /// <param name="duel">The duel's board: the land starts past the stands round it, and the camera's side (east) stays open.</param>
        public static Texture Build(Transform parent, GridModel grid, DuctRunner.Kind kind, int seed, bool duel = false)
        {
            var root = new GameObject("Biome").transform;
            root.SetParent(parent, false);
            int w = grid.Width, h = grid.Height;
            var centre = new Vector3((w - 1) * 0.5f, 0f, (h - 1) * 0.5f);
            var m = new Mats(kind);

            if (kind == DuctRunner.Kind.Sky)
            {
                // A sea of clouds far below the floating floor.
                Shapes.Primitive(PrimitiveType.Cylinder, "CloudSea", root, centre + Vector3.down * 6f, new Vector3(w + 160f, 0.05f, h + 160f), m.ground);
                return Sky(kind);
            }
            if (kind == DuctRunner.Kind.Void) return SquashBot.UI.UiSprites.Galaxy.texture; // the stars all round

            // The ground: wide enough to fill the view all round.
            Shapes.Primitive(PrimitiveType.Cube, "Ground", root, new Vector3(centre.x, GroundY - 0.1f, centre.z), new Vector3(w + 140f, 0.2f, h + 140f), m.ground);

            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            int target = Mathf.Clamp((w + 28) * (h + 28) / 9, 70, duel ? 120 : 190);
            int placed = 0;
            for (int tries = 0; tries < target * 8 && placed < target; tries++)
            {
                float x = -16f + R() * (w + 32f), z = -16f + R() * (h + 32f);
                if (NearFloor(grid, x, z, duel ? 9.5f : Clearance)) continue;
                if (!duel && x > w - 1.5f && x < w + 9f && z > -2f && z < h + 1f) continue; // the stands along the east side
                bool front = duel ? x > w - 1f : x < -0.5f || z < -0.5f; // between the camera and the floor (the duel: its east side): low things only
                bool between = x > 0f && x < w - 1f && z > 0f && z < h - 1f; // in a gap between stages
                float maxH = front ? 0.55f : between ? 2.2f : 3.6f;
                Place(root, m, kind, new Vector3(x, GroundY, z), maxH, R);
                placed++;
            }
            return Sky(kind);
        }

        private static bool NearFloor(GridModel grid, float x, float z, float clearance)
        {
            int r = Mathf.CeilToInt(clearance);
            int cx = Mathf.RoundToInt(x), cz = Mathf.RoundToInt(z);
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    var p = new GridPos(cx + dx, cz + dz);
                    if (!grid.InBounds(p) || !grid.Exists(p)) continue;
                    if (Mathf.Abs(p.x - x) <= clearance && Mathf.Abs(p.y - z) <= clearance) return true;
                }
            return false;
        }

        // ---------- The things ----------

        private class Mats
        {
            public Material ground, a, b, c, d, glow;
            public Mats(DuctRunner.Kind k)
            {
                Material M(string hex, Color e = default) { ColorUtility.TryParseHtmlString(hex, out var col); return MaterialFactory.Create(col, e); }
                switch (k)
                {
                    case DuctRunner.Kind.Forest: ground = M("#5E9448"); a = M("#3F7A3A"); b = M("#6FAE52"); c = M("#6B4A30"); d = M("#8A8478"); break;
                    case DuctRunner.Kind.Canyon: ground = M("#E6C48C"); a = M("#F0D49E"); b = M("#B8603A"); c = M("#6E9A54"); d = M("#D8D0C0"); break;
                    case DuctRunner.Kind.Snow:
                        ground = M("#E8F0F8", new Color(0.1f, 0.12f, 0.16f)); a = M("#3E6A52"); b = M("#FFFFFF", new Color(0.2f, 0.22f, 0.26f)); c = M("#5A4636");
                        d = MaterialFactory.CreateTransparent(new Color(0.62f, 0.85f, 1f, 0.82f), new Color(0.15f, 0.35f, 0.5f));
                        break;
                    case DuctRunner.Kind.Lava:
                        ground = M("#2B2326"); a = M("#3A3032"); b = M("#4E3E3C"); c = M("#24191B");
                        d = M("#FF7A1E", new Color(2.4f, 0.8f, 0.12f)); glow = M("#FFC24A", new Color(2.6f, 1.4f, 0.3f));
                        break;
                    case DuctRunner.Kind.Ocean: ground = M("#D8C49A", new Color(0.04f, 0.06f, 0.08f)); a = M("#E88A7A"); b = M("#F2B26A"); c = M("#4E9A7A"); d = M("#6E8A9A"); break;
                    case DuctRunner.Kind.Crystal:
                        ground = M("#4A4052"); a = M("#5E5068"); b = M("#C8A8E8", new Color(0.7f, 0.4f, 1f)); c = M("#A8D8E8", new Color(0.35f, 0.75f, 0.95f)); d = M("#E8B8D8", new Color(0.9f, 0.4f, 0.7f));
                        break;
                    default:
                        ground = MaterialFactory.CreateTransparent(new Color(0.88f, 0.92f, 1f, 0.92f), new Color(0.15f, 0.17f, 0.22f));
                        break;
                }
            }
        }

        private static GameObject Blob(string name, Transform parent, Vector3 at, Vector3 size, Material mat)
        {
            if (blob == null) blob = MeshFactory.RoundedBox(Vector3.one, 0.3f);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = blob;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static GameObject Ball(string name, Transform parent, Vector3 at, Vector3 size, Material mat) =>
            Shapes.Primitive(PrimitiveType.Sphere, name, parent, at, size, mat);

        /// <summary>One thing of the world at the spot, no taller than <paramref name="maxH"/> above the ground.</summary>
        private static void Place(Transform root, Mats m, DuctRunner.Kind kind, Vector3 at, float maxH, System.Func<float> R)
        {
            float k = R();
            bool low = maxH < 1f;
            switch (kind)
            {
                case DuctRunner.Kind.Forest:
                    if (low) { Ball("Bush", root, at + Vector3.up * 0.2f, new Vector3(1f, 0.55f, 0.9f) * (0.7f + R() * 0.6f), k < 0.5f ? m.a : m.b); break; }
                    {
                        float s = Mathf.Min(maxH / 3.2f, 0.8f + R() * 0.5f) * 1.3f;
                        if (k < 0.5f)
                        {
                            Blob("Trunk", root, at + Vector3.up * 0.45f * s, new Vector3(0.28f, 0.9f, 0.28f) * s, m.c);
                            for (int i = 0; i < 3; i++)
                                Ball("Crown", root, at + Vector3.up * s * (1.2f + i * 0.65f), new Vector3(1.5f - i * 0.38f, 0.85f, 1.5f - i * 0.38f) * s, m.a);
                        }
                        else
                        {
                            Blob("Trunk", root, at + Vector3.up * 1f * s, new Vector3(0.32f, 2f, 0.32f) * s, m.c);
                            for (int i = 0; i < 3; i++)
                                Ball("Leaves", root, at + new Vector3((R() - 0.5f) * 0.8f, 2.1f + R() * 0.5f, (R() - 0.5f) * 0.8f) * s, Vector3.one * (1.2f + R() * 0.6f) * s, i == 0 ? m.a : m.b);
                        }
                    }
                    break;
                case DuctRunner.Kind.Canyon:
                    if (k < 0.55f || low)
                    {
                        var dune = Ball("Dune", root, at, new Vector3(4f + R() * 5f, Mathf.Min(maxH, 0.9f + R() * 1.3f) * 1.6f, 2.5f + R() * 2f), m.a).transform;
                        dune.localRotation = Quaternion.Euler(0f, R() * 180f, 0f);
                    }
                    else if (k < 0.8f)
                    {
                        float s = Mathf.Min(maxH / 1.5f, 0.9f + R() * 0.6f);
                        Blob("Cactus", root, at + Vector3.up * 0.7f * s, new Vector3(0.28f, 1.4f, 0.28f) * s, m.c);
                        Blob("Arm", root, at + new Vector3(0.24f, 0.9f, 0f) * s, new Vector3(0.26f, 0.15f, 0.15f) * s, m.c);
                        Blob("Arm", root, at + new Vector3(0.34f, 1.15f, 0f) * s, new Vector3(0.15f, 0.5f, 0.15f) * s, m.c);
                    }
                    else
                    {
                        float hh = Mathf.Min(maxH, 1.5f + R() * 2.5f);
                        Blob("Mesa", root, at + Vector3.up * hh * 0.5f, new Vector3(2.5f + R() * 2f, hh, 2f + R() * 2f), m.b);
                    }
                    break;
                case DuctRunner.Kind.Snow:
                    if (low) { Ball("Drift", root, at, new Vector3(2.2f, 0.8f, 1.6f) * (0.7f + R()), m.b); break; }
                    if (k < 0.35f)
                    {
                        var ice = Blob("Ice", root, at + Vector3.up * 0.6f, new Vector3(1.8f + R() * 2.5f, Mathf.Min(maxH, 1.4f + R() * 2f), 1.4f + R() * 2f), m.d).transform;
                        ice.localRotation = Quaternion.Euler((R() - 0.5f) * 16f, R() * 90f, (R() - 0.5f) * 20f);
                    }
                    else
                    {
                        float s = Mathf.Min(maxH / 2.6f, 0.9f + R() * 0.7f) * 1.25f;
                        Blob("Trunk", root, at + Vector3.up * 0.3f * s, new Vector3(0.2f, 0.6f, 0.2f) * s, m.c);
                        for (int i = 0; i < 3; i++)
                        {
                            float tw = s * (1.25f - i * 0.32f), y0 = (0.6f + i * 0.58f) * s;
                            Blob("Tier", root, at + Vector3.up * y0, new Vector3(tw, 0.55f * s, tw), m.a).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                            Blob("Snow", root, at + Vector3.up * (y0 + 0.26f * s), new Vector3(tw * 0.72f, 0.09f * s, tw * 0.72f), m.b).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                        }
                    }
                    break;
                case DuctRunner.Kind.Lava:
                    if (k < 0.4f)
                    {
                        float s = 1.2f + R() * 2.4f;
                        Shapes.Primitive(PrimitiveType.Cylinder, "Lava", root, at + Vector3.up * 0.03f, new Vector3(s * 1.4f, 0.03f, s), m.d);
                        if (!low)
                        {
                            int n = 1 + (int)(R() * 3f);
                            for (int i = 0; i < n; i++)
                            {
                                var f = at + new Vector3((R() - 0.5f) * s, 0f, (R() - 0.5f) * s * 0.6f);
                                float fh = Mathf.Min(maxH, 0.7f + R() * 1.1f);
                                var cone = DuelStands.Cone(6);
                                Piece(root, cone, f, new Vector3(0.55f, fh, 0.55f), m.d);
                                Piece(root, cone, f, new Vector3(0.28f, fh * 0.6f, 0.28f), m.glow);
                            }
                        }
                    }
                    else
                    {
                        var rock = Blob("Rock", root, at + Vector3.up * 0.4f, new Vector3(1f + R() * 1.8f, Mathf.Min(maxH, 0.7f + R() * 1.8f), 1f + R() * 1.6f), k < 0.75f ? m.a : m.b).transform;
                        rock.localRotation = Quaternion.Euler(R() * 12f, R() * 90f, R() * 12f);
                    }
                    break;
                case DuctRunner.Kind.Ocean:
                    if (k < 0.45f || low) Ball("Rock", root, at, new Vector3(1.3f, 0.8f, 1.2f) * (0.6f + R() * 1.4f), m.d);
                    else
                    {
                        // A coral: a few branching arms.
                        var mat = k < 0.7f ? m.a : m.b;
                        float s = Mathf.Min(maxH / 1.6f, 0.9f + R() * 0.7f);
                        for (int i = 0; i < 4; i++)
                            Blob("Coral", root, at + new Vector3((R() - 0.5f) * 0.6f, 0.45f + i * 0.2f, (R() - 0.5f) * 0.6f) * s, new Vector3(0.2f, 0.7f + R() * 0.5f, 0.2f) * s, mat)
                                .transform.localRotation = Quaternion.Euler(0f, 0f, (R() - 0.5f) * 50f);
                        if (R() < 0.5f) Blob("Weed", root, at + new Vector3(0.6f, 0.9f, 0f) * s, new Vector3(0.12f, 1.8f, 0.06f) * s, m.c);
                    }
                    break;
                case DuctRunner.Kind.Crystal:
                    if (k < 0.45f || low)
                    {
                        var rock = Blob("Rock", root, at + Vector3.up * 0.5f, new Vector3(1.4f + R() * 2f, Mathf.Min(maxH, 1f + R() * 2f), 1.4f + R() * 2f), m.a).transform;
                        rock.localRotation = Quaternion.Euler(R() * 18f, R() * 90f, R() * 18f);
                    }
                    else
                    {
                        int n = 2 + (int)(R() * 3f);
                        var mats = new[] { m.b, m.c, m.d };
                        for (int i = 0; i < n; i++)
                        {
                            float hh = Mathf.Min(maxH, 1.2f + R() * 2.6f);
                            var c = Blob("Crystal", root, at + new Vector3((R() - 0.5f) * 0.8f, hh * 0.45f, (R() - 0.5f) * 0.8f), new Vector3(0.45f, hh, 0.45f), mats[(int)(R() * 3f) % 3]).transform;
                            c.localRotation = Quaternion.Euler((R() - 0.5f) * 30f, R() * 90f, (R() - 0.5f) * 30f);
                        }
                    }
                    break;
            }
        }

        private static void Piece(Transform parent, Mesh mesh, Vector3 at, Vector3 scale, Material mat)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------- The sky ----------

        /// <summary>The world's sky (the same as its road's): its top colour, a bright haze at the horizon, its ground colour.</summary>
        private static Texture Sky(DuctRunner.Kind k)
        {
            if (skies.TryGetValue(k, out var tex) && tex != null) return tex;
            Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
            (Color top, Color haze, Color low) c = k switch
            {
                DuctRunner.Kind.Forest => (H("#5FA8E8"), H("#E4F2F4"), H("#7FAE68")),
                DuctRunner.Kind.Canyon => (H("#6AB0E6"), H("#FBE6C4"), H("#E2BE88")),
                DuctRunner.Kind.Snow => (H("#7EA6D8"), H("#EAF2FA"), H("#DCE6F0")),
                DuctRunner.Kind.Lava => (H("#2A0E12"), H("#E2561E"), H("#2B2326")),
                DuctRunner.Kind.Ocean => (H("#0E3A62"), H("#4A9AC0"), H("#B8A884")),
                DuctRunner.Kind.Crystal => (H("#140E22"), H("#5A3E7A"), H("#3A3244")),
                _ => (H("#5E9EF0"), H("#FCEADA"), H("#FFFFFF")),
            };
            const int Hgt = 256;
            tex = new Texture2D(4, Hgt, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
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
            tex.SetPixels(px);
            tex.Apply(false, true);
            skies[k] = tex;
            return tex;
        }
    }
}
