using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// What falls from the sky in each world (crates, ice cubes, meteors, barrels, ...), plus the bomb.
    /// Every model is centered on its origin and fits the ~0.86 block footprint.
    /// </summary>
    public static class HazardVisuals
    {
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        private static Material M(string key, Color color, Color emission)
        {
            if (!Materials.TryGetValue(key, out var m) || m == null)
                Materials[key] = m = MaterialFactory.Create(color, emission);
            return m;
        }

        private static Material T(string key, Color color, Color emission)
        {
            if (!Materials.TryGetValue(key, out var m) || m == null)
                Materials[key] = m = MaterialFactory.CreateTransparent(color, emission);
            return m;
        }

        private static GameObject Box(Transform p, Vector3 pos, Vector3 size, float r, Material m) => Shapes.Rounded("Part", p, pos, size, r, m);
        private static GameObject Ball(Transform p, Vector3 pos, float d, Material m) => Shapes.Primitive(PrimitiveType.Sphere, "Part", p, pos, Vector3.one * d, m);
        private static GameObject Cyl(Transform p, Vector3 pos, Vector3 scale, Material m) => Shapes.Primitive(PrimitiveType.Cylinder, "Part", p, pos, scale, m);

        /// <summary>
        /// The core loop's crates (0 wooden, 1 iron-banded, 2 steel, 3 reinforced with hazard bands, 4 armoured with
        /// glowing seams), or -1 for each world's own falling obstacle.
        /// </summary>
        public static int CrateTier = -1;

        /// <summary>A crate of one tier on its own (the map's world dioramas show each world's crate).</summary>
        public static GameObject CrateOfTier(int tier)
        {
            var root = new GameObject("Crate").transform;
            TierCrate(root, Mathf.Clamp(tier, 0, 4));
            return root.gameObject;
        }

        private static void TierCrate(Transform r, int tier)
        {
            switch (tier)
            {
                case 0: Crate(r); return;
                case 1:
                {
                    var wood = M("wood", Hex("#B8783F"), new Color(0.1f, 0.04f, 0.01f));
                    var iron = M("iron", Hex("#5C6068"), Color.black);
                    var rivet = M("rivet", Hex("#A8ADB5"), Color.black);
                    Box(r, Vector3.zero, Vector3.one * 0.84f, 0.05f, wood);
                    foreach (float y in new[] { -0.3f, 0.3f })
                        Box(r, new Vector3(0f, y, 0f), new Vector3(0.88f, 0.09f, 0.88f), 0.02f, iron);
                    foreach (float x in new[] { -0.3f, 0.3f })
                        Box(r, new Vector3(x, 0f, 0f), new Vector3(0.09f, 0.88f, 0.88f), 0.02f, iron);
                    foreach (float a in new[] { -0.3f, 0.3f })
                        foreach (float b in new[] { -0.3f, 0.3f })
                            Ball(r, new Vector3(a, b, 0.44f), 0.06f, rivet);
                    return;
                }
                case 2:
                {
                    var steel = M("steel", Hex("#8C939C"), new Color(0.03f, 0.03f, 0.04f));
                    var frame = M("steelFrame", Hex("#4A5058"), Color.black);
                    Box(r, Vector3.zero, Vector3.one * 0.82f, 0.04f, steel);
                    const float e = 0.41f, t = 0.08f, l = 0.88f;
                    foreach (float a in new[] { -e, e })
                        foreach (float b in new[] { -e, e })
                        {
                            Box(r, new Vector3(0f, a, b), new Vector3(l, t, t), 0.02f, frame);
                            Box(r, new Vector3(a, 0f, b), new Vector3(t, l, t), 0.02f, frame);
                            Box(r, new Vector3(a, b, 0f), new Vector3(t, t, l), 0.02f, frame);
                        }
                    Box(r, Vector3.zero, new Vector3(0.86f, 0.06f, 0.86f), 0.02f, frame);
                    return;
                }
                case 3:
                {
                    var dark = M("darkSteel", Hex("#3C424C"), new Color(0.02f, 0.02f, 0.03f));
                    var yellow = M("hazardYellow", Hex("#F2C230"), new Color(0.35f, 0.25f, 0.02f));
                    var black = M("hazardBlack", Hex("#1C1C20"), Color.black);
                    Box(r, Vector3.zero, Vector3.one * 0.84f, 0.05f, dark);
                    foreach (float y in new[] { -0.25f, 0.25f })
                    {
                        Box(r, new Vector3(0f, y, 0f), new Vector3(0.88f, 0.12f, 0.88f), 0.02f, yellow);
                        for (int i = -2; i <= 2; i++)
                            Box(r, new Vector3(i * 0.17f, y, 0.445f), new Vector3(0.06f, 0.13f, 0.01f), 0.005f, black)
                                .transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                    }
                    return;
                }
                default:
                {
                    var armour = M("armour", Hex("#2A2F3A"), new Color(0.02f, 0.02f, 0.04f));
                    var seam = M("armourSeam", Hex("#6FE8FF"), new Color(0.5f, 2.2f, 2.8f));
                    Box(r, Vector3.zero, Vector3.one * 0.86f, 0.08f, armour);
                    foreach (float y in new[] { -0.2f, 0.2f })
                        Box(r, new Vector3(0f, y, 0f), new Vector3(0.88f, 0.035f, 0.88f), 0.01f, seam);
                    Box(r, Vector3.zero, new Vector3(0.035f, 0.88f, 0.88f), 0.01f, seam);
                    Box(r, new Vector3(0f, 0.44f, 0f), new Vector3(0.36f, 0.04f, 0.36f), 0.02f, seam);
                    return;
                }
            }
        }

        /// <summary>The core loop's bomb: a red explosive crate with a sputtering fuse.</summary>
        private static void TntCrate(Transform r)
        {
            var red = M("tnt", Hex("#C8322A"), new Color(0.3f, 0.03f, 0.02f));
            var band = M("tntBand", Hex("#2A1E1A"), Color.black);
            Box(r, Vector3.zero, Vector3.one * 0.8f, 0.05f, red);
            foreach (float y in new[] { -0.24f, 0.24f })
                Box(r, new Vector3(0f, y, 0f), new Vector3(0.84f, 0.08f, 0.84f), 0.02f, band);
            Box(r, new Vector3(0f, 0f, 0.41f), new Vector3(0.36f, 0.24f, 0.01f), 0.01f, M("tntLabel", Hex("#F4E3C0"), new Color(0.2f, 0.18f, 0.12f)));
            Cyl(r, new Vector3(0.05f, 0.47f, 0f), new Vector3(0.035f, 0.08f, 0.035f), M("fuse", Hex("#C8B48A"), Color.black));
            var spark = Ball(r, new Vector3(0.07f, 0.58f, 0f), 0.12f, M("spark", Hex("#FFD27A"), new Color(3f, 1.6f, 0.3f)));
            spark.AddComponent<Flicker>();
        }

        /// <summary>The falling obstacle of the design being played (<see cref="WorldTheme.Current"/>).</summary>
        public static GameObject Block()
        {
            var root = new GameObject("Block").transform;
            if (CrateTier >= 0)
            {
                TierCrate(root, CrateTier);
                return root.gameObject;
            }
            switch (WorldTheme.Current.block)
            {
                case BlockStyle.Crate: Crate(root); break;
                case BlockStyle.IceCube: IceCube(root); break;
                case BlockStyle.NeonCube: NeonCube(root); break;
                case BlockStyle.Boulder: Boulder(root); break;
                case BlockStyle.Sandstone: Sandstone(root); break;
                case BlockStyle.DivingWeight: DivingWeight(root); break;
                case BlockStyle.Gumdrop: Gumdrop(root); break;
                case BlockStyle.Barrel: Barrel(root); break;
                case BlockStyle.Crystal: Crystal(root); break;
                case BlockStyle.StormCube: StormCube(root); break;
                case BlockStyle.DataCube: DataCube(root); break;
                case BlockStyle.Drone: Drone(root); break;
                case BlockStyle.MeteorLava: Meteor(root, Hex("#3A2422"), new Color(0.9f, 0.25f, 0.03f)); break;
                case BlockStyle.MeteorNight: Meteor(root, Hex("#5A6488"), new Color(0.15f, 0.2f, 0.45f)); break;
                case BlockStyle.MeteorGalaxy: Meteor(root, Hex("#4A2A7A"), new Color(1.1f, 0.35f, 1.4f)); break;
                case BlockStyle.MeteorGold: Meteor(root, Hex("#6A4A20"), new Color(1.6f, 1.1f, 0.3f)); break;
                case BlockStyle.MeteorHeart: Meteor(root, Hex("#4A1A2A"), new Color(1.8f, 0.3f, 0.5f)); break;
                default: Box(root, Vector3.zero, Vector3.one * 0.86f, 0.12f, M("red", Palette.Block, Palette.BlockGlow)); break;
            }
            return root.gameObject;
        }

        /// <summary>A round bomb with a sputtering fuse; its blast covers a plus shape.</summary>
        public static GameObject Bomb()
        {
            var root = new GameObject("Bomb").transform;
            if (CrateTier >= 0)
            {
                TntCrate(root);
                return root.gameObject;
            }
            Ball(root, Vector3.zero, 0.72f, M("bomb", Hex("#23233A"), new Color(0.05f, 0.05f, 0.12f)));
            Cyl(root, Vector3.zero, new Vector3(0.74f, 0.04f, 0.74f), M("bombBand", Hex("#FF5A3C"), new Color(1.8f, 0.35f, 0.15f)));
            Cyl(root, new Vector3(0f, 0.4f, 0f), new Vector3(0.12f, 0.06f, 0.12f), M("bombCap", Hex("#5A5A70"), Color.black));
            Cyl(root, new Vector3(0.03f, 0.5f, 0f), new Vector3(0.035f, 0.07f, 0.035f), M("fuse", Hex("#C8B48A"), Color.black));
            var spark = Ball(root, new Vector3(0.05f, 0.6f, 0f), 0.12f, M("spark", Hex("#FFD27A"), new Color(3f, 1.6f, 0.3f)));
            spark.AddComponent<Flicker>();
            return root.gameObject;
        }

        // ---------- World skins ----------

        private static void Crate(Transform r)
        {
            var wood = M("wood", Hex("#C08048"), new Color(0.12f, 0.05f, 0.01f));
            var strap = M("woodDark", Hex("#7A4A22"), Color.black);
            Box(r, Vector3.zero, Vector3.one * 0.84f, 0.05f, wood);
            foreach (float y in new[] { -0.26f, 0.26f })
                Box(r, new Vector3(0f, y, 0f), new Vector3(0.88f, 0.1f, 0.88f), 0.02f, strap);
            Box(r, Vector3.zero, new Vector3(0.1f, 0.88f, 0.88f), 0.02f, strap);
        }

        private static void IceCube(Transform r)
        {
            Box(r, Vector3.zero, Vector3.one * 0.5f, 0.1f, M("iceCore", Hex("#E8F8FF"), new Color(0.4f, 0.9f, 1.3f)));
            Box(r, Vector3.zero, Vector3.one * 0.86f, 0.1f, T("iceShell", new Color(0.7f, 0.92f, 1f, 0.55f), new Color(0.2f, 0.45f, 0.7f)));
        }

        private static void NeonCube(Transform r)
        {
            Box(r, Vector3.zero, Vector3.one * 0.8f, 0.04f, M("neonBody", Hex("#1B1838"), Color.black));
            var edge = M("neonEdge", Hex("#FF8CF0"), new Color(2.1f, 0.45f, 2.2f));
            const float e = 0.41f, t = 0.07f, l = 0.88f;
            foreach (float a in new[] { -e, e })
                foreach (float b in new[] { -e, e })
                {
                    Box(r, new Vector3(0f, a, b), new Vector3(l, t, t), 0.02f, edge);
                    Box(r, new Vector3(a, 0f, b), new Vector3(t, l, t), 0.02f, edge);
                    Box(r, new Vector3(a, b, 0f), new Vector3(t, t, l), 0.02f, edge);
                }
        }

        private static void Meteor(Transform r, Color rock, Color glow)
        {
            var body = M("rock" + rock, rock, glow * 0.25f);
            var hot = M("hot" + glow, rock, glow);
            Ball(r, Vector3.zero, 0.8f, body);
            Ball(r, new Vector3(0.22f, 0.18f, 0.12f), 0.42f, body);
            Ball(r, new Vector3(-0.2f, -0.15f, 0.18f), 0.38f, hot);
            Ball(r, new Vector3(0.05f, -0.22f, -0.22f), 0.36f, body);
        }

        private static void Boulder(Transform r)
        {
            var stone = M("stone", Hex("#8A9488"), new Color(0.03f, 0.04f, 0.03f));
            Ball(r, Vector3.zero, 0.82f, stone);
            Ball(r, new Vector3(-0.2f, 0.12f, 0.15f), 0.45f, stone);
            Ball(r, new Vector3(0.1f, 0.33f, 0f), 0.42f, M("moss", Hex("#5FBF6A"), new Color(0.15f, 0.5f, 0.2f)))
                .transform.localScale = new Vector3(0.55f, 0.18f, 0.5f);
        }

        private static void Sandstone(Transform r)
        {
            Box(r, Vector3.zero, Vector3.one * 0.86f, 0.14f, M("sand", Hex("#E8C088"), new Color(0.12f, 0.08f, 0.02f)));
            var band = M("sandDark", Hex("#B98A4E"), Color.black);
            foreach (float y in new[] { -0.2f, 0.12f })
                Box(r, new Vector3(0f, y, 0f), new Vector3(0.88f, 0.06f, 0.88f), 0.03f, band);
        }

        private static void DivingWeight(Transform r)
        {
            Ball(r, new Vector3(0f, -0.04f, 0f), 0.8f, M("navy", Hex("#22344E"), new Color(0.02f, 0.06f, 0.12f)));
            Cyl(r, Vector3.zero, new Vector3(0.84f, 0.05f, 0.84f), M("brass", Hex("#E0B050"), new Color(0.4f, 0.28f, 0.05f)));
            Box(r, new Vector3(0f, 0.42f, 0f), new Vector3(0.3f, 0.1f, 0.08f), 0.04f, M("brass", Hex("#E0B050"), new Color(0.4f, 0.28f, 0.05f)));
        }

        private static void Gumdrop(Transform r)
        {
            Box(r, new Vector3(0f, -0.05f, 0f), new Vector3(0.84f, 0.76f, 0.84f), 0.3f, M("candy", Hex("#FF7EC2"), new Color(0.6f, 0.15f, 0.4f)));
            Cyl(r, new Vector3(0f, 0.33f, 0f), new Vector3(0.5f, 0.05f, 0.5f), M("mint", Hex("#7FF0D8"), new Color(0.2f, 0.7f, 0.6f)));
            var sugar = M("sugar", Color.white, new Color(0.4f, 0.4f, 0.4f));
            Ball(r, new Vector3(0.3f, 0.05f, 0.3f), 0.1f, sugar);
            Ball(r, new Vector3(-0.32f, -0.1f, 0.25f), 0.1f, sugar);
            Ball(r, new Vector3(0.28f, -0.2f, -0.3f), 0.1f, sugar);
            Ball(r, new Vector3(-0.25f, 0.12f, -0.32f), 0.1f, sugar);
        }

        private static void Barrel(Transform r)
        {
            Cyl(r, Vector3.zero, new Vector3(0.76f, 0.43f, 0.76f), M("barrel", Hex("#3E7A2E"), new Color(0.03f, 0.08f, 0.02f)));
            var ring = M("barrelRing", Hex("#2A3A2A"), Color.black);
            foreach (float y in new[] { -0.25f, 0.25f })
                Cyl(r, new Vector3(0f, y, 0f), new Vector3(0.8f, 0.04f, 0.8f), ring);
            Cyl(r, new Vector3(0f, 0.43f, 0f), new Vector3(0.6f, 0.01f, 0.6f), M("toxic", Hex("#B6FF6A"), new Color(0.9f, 2.4f, 0.4f)));
        }

        private static void Crystal(Transform r)
        {
            var glass = T("crystal", new Color(0.75f, 0.6f, 1f, 0.6f), new Color(0.5f, 0.6f, 1.6f));
            var main = Box(r, Vector3.zero, new Vector3(0.42f, 0.86f, 0.42f), 0.06f, glass);
            main.transform.localRotation = Quaternion.Euler(0f, 45f, 8f);
            var a = Box(r, new Vector3(0.24f, -0.15f, 0.1f), new Vector3(0.26f, 0.5f, 0.26f), 0.05f, glass);
            a.transform.localRotation = Quaternion.Euler(0f, 30f, -25f);
            var b = Box(r, new Vector3(-0.22f, -0.2f, -0.12f), new Vector3(0.24f, 0.42f, 0.24f), 0.05f, glass);
            b.transform.localRotation = Quaternion.Euler(10f, 60f, 22f);
            Ball(r, Vector3.zero, 0.22f, M("crystalCore", Hex("#E0F0FF"), new Color(1.2f, 1.4f, 2.2f)));
        }

        private static void StormCube(Transform r)
        {
            Box(r, Vector3.zero, Vector3.one * 0.84f, 0.1f, M("storm", Hex("#3A3F55"), new Color(0.02f, 0.02f, 0.04f)));
            var bolt = M("bolt", Hex("#FFF07A"), new Color(2.2f, 2.0f, 0.4f));
            Box(r, Vector3.zero, new Vector3(0.88f, 0.07f, 0.88f), 0.03f, bolt);
            var zig = Box(r, new Vector3(0f, 0.45f, 0f), new Vector3(0.08f, 0.04f, 0.4f), 0.02f, bolt);
            zig.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
        }

        /// <summary>A hunter drone from vanG's sky castle: a squat body on four spinning rotors, one red eye.</summary>
        private static void Drone(Transform r)
        {
            var shell = M("droneShell", Hex("#4A5070"), new Color(0.05f, 0.05f, 0.1f));
            var trim = M("droneTrim", Hex("#FFD27A"), new Color(1.6f, 1.1f, 0.3f));
            var rotor = M("droneRotor", Hex("#DDE4F4"), new Color(0.2f, 0.25f, 0.35f));
            Box(r, new Vector3(0f, -0.05f, 0f), new Vector3(0.62f, 0.34f, 0.62f), 0.14f, shell);
            Box(r, new Vector3(0f, 0.04f, 0f), new Vector3(0.66f, 0.05f, 0.66f), 0.02f, trim);
            Ball(r, new Vector3(0f, -0.04f, 0.31f), 0.16f, M("droneEye", Hex("#FF5A6A"), new Color(2.4f, 0.4f, 0.5f)));
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                {
                    var arm = Box(r, new Vector3(x * 0.27f, 0.08f, z * 0.27f), new Vector3(0.26f, 0.05f, 0.06f), 0.02f, shell).transform;
                    arm.localRotation = Quaternion.Euler(0f, x * z * 45f, 0f);
                    var hub = new GameObject("Rotor").transform;
                    hub.SetParent(r, false);
                    hub.localPosition = new Vector3(x * 0.4f, 0.14f, z * 0.4f);
                    Cyl(hub, Vector3.zero, new Vector3(0.3f, 0.008f, 0.06f), rotor);
                    Cyl(hub, Vector3.zero, new Vector3(0.06f, 0.008f, 0.3f), rotor);
                    hub.gameObject.AddComponent<Spinner>().speed = 900f;
                }
        }

        private static void DataCube(Transform r)
        {
            Box(r, Vector3.zero, Vector3.one * 0.82f, 0.06f, M("data", Hex("#103538"), Color.black));
            var line = M("dataLine", Hex("#5CFFF0"), new Color(0.2f, 2.2f, 2.0f));
            foreach (float y in new[] { -0.18f, 0.18f })
                Box(r, new Vector3(0f, y, 0f), new Vector3(0.86f, 0.04f, 0.86f), 0.02f, line);
            foreach (float x in new[] { -0.18f, 0.18f })
                Box(r, new Vector3(x, 0f, 0f), new Vector3(0.04f, 0.86f, 0.86f), 0.02f, line);
        }
    }

    /// <summary>Random flicker of an emissive spark (bomb fuse).</summary>
    public class Flicker : MonoBehaviour
    {
        private Vector3 baseScale;
        private float seed;

        private void Awake()
        {
            baseScale = transform.localScale;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float s = 0.7f + Mathf.PerlinNoise(Time.time * 18f, seed) * 0.7f;
            transform.localScale = baseScale * s;
        }
    }

    /// <summary>Spins an object around its local Y axis (robot halos, orbiting planets).</summary>
    public class Spinner : MonoBehaviour
    {
        public float speed = 90f;

        private void Update() => transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
    }
}
