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

        /// <summary>The falling obstacle for a world.</summary>
        public static GameObject Block(int world)
        {
            var root = new GameObject("Block").transform;
            switch (world >= 15 ? world + 100 : world % 15)
            {
                case 115: Crystal(root); break;
                case 116: NeonCube(root); break;
                case 117: Crate(root); break;
                case 118: Gumdrop(root); break;
                case 119: Crate(root); break;      // garden
                case 120: Sandstone(root); break;  // canyon
                case 121: IceCube(root); break;    // snowy peak
                case 122: Barrel(root); break;     // harbour
                case 123: StormCube(root); break;  // cloud meadow
                case 124: Meteor(root, Hex("#6A4A20"), new Color(1.6f, 1.1f, 0.3f)); break; // the roof
                case 1: Crate(root); break;
                case 2: IceCube(root); break;
                case 3: NeonCube(root); break;
                case 4: Meteor(root, Hex("#3A2422"), new Color(0.9f, 0.25f, 0.03f)); break;
                case 5: Boulder(root); break;
                case 6: Sandstone(root); break;
                case 7: DivingWeight(root); break;
                case 8: Gumdrop(root); break;
                case 9: Barrel(root); break;
                case 10: Meteor(root, Hex("#5A6488"), new Color(0.15f, 0.2f, 0.45f)); break;
                case 11: Crystal(root); break;
                case 12: StormCube(root); break;
                case 13: DataCube(root); break;
                case 14: Meteor(root, Hex("#4A2A7A"), new Color(1.1f, 0.35f, 1.4f)); break;
                default: Box(root, Vector3.zero, Vector3.one * 0.86f, 0.12f, M("red", Palette.Block, Palette.BlockGlow)); break;
            }
            return root.gameObject;
        }

        /// <summary>A round bomb with a sputtering fuse; its blast covers a plus shape.</summary>
        public static GameObject Bomb()
        {
            var root = new GameObject("Bomb").transform;
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
