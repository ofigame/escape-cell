using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The Thunder Hammer that beats a monster: a chunky golden hammer with a glowing blue head, floating and turning
    /// over a tile inside a column of light, with sparks circling it. The robot carries it over its shoulder
    /// (<see cref="CreateHeld"/>); running into the monster swings it down and calls a lightning bolt (<see cref="Swing"/>).
    /// </summary>
    public class ThunderHammer : MonoBehaviour
    {
        public static readonly Color Electric = new Color(0.45f, 0.8f, 1f);
        public static readonly Color ElectricGlow = new Color(1.2f, 2.4f, 3.6f);

        private Transform model, sparks, beam;
        private Material beamMat;
        private float time, popT;

        /// <summary>True in the hammer's last seconds on a tile: it flickers before jumping to another one.</summary>
        public bool Leaving { get; set; }

        public static ThunderHammer Create(Vector3 position)
        {
            var go = new GameObject("ThunderHammer");
            go.transform.position = position;
            var hammer = go.AddComponent<ThunderHammer>();
            hammer.Build();
            return hammer;
        }

        /// <summary>The hammer model itself (handle along +Y, head on top), about 0.7 units tall.</summary>
        public static Transform BuildModel(Transform parent, float scale = 1f)
        {
            var root = new GameObject("Hammer").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;
            var gold = MaterialFactory.Create(new Color(1f, 0.78f, 0.3f), new Color(1.4f, 0.9f, 0.2f));
            var grip = MaterialFactory.Create(new Color(0.35f, 0.22f, 0.5f), Color.black);
            var steel = MaterialFactory.Create(new Color(0.75f, 0.85f, 1f), ElectricGlow * 0.55f);
            var core = MaterialFactory.Create(Electric, ElectricGlow);
            // Handle: a wrapped grip, a golden collar and pommel.
            Shapes.Rounded("Grip", root, new Vector3(0f, 0.2f, 0f), new Vector3(0.09f, 0.42f, 0.09f), 0.04f, grip);
            Shapes.Rounded("Pommel", root, new Vector3(0f, -0.02f, 0f), new Vector3(0.14f, 0.08f, 0.14f), 0.035f, gold);
            Shapes.Rounded("Collar", root, new Vector3(0f, 0.43f, 0f), new Vector3(0.14f, 0.06f, 0.14f), 0.025f, gold);
            // Head: a block with golden caps and a glowing lightning core on each face.
            Shapes.Rounded("Head", root, new Vector3(0f, 0.56f, 0f), new Vector3(0.44f, 0.22f, 0.24f), 0.06f, steel);
            foreach (float x in new[] { -0.24f, 0.24f })
                Shapes.Rounded("Cap", root, new Vector3(x, 0.56f, 0f), new Vector3(0.06f, 0.26f, 0.28f), 0.025f, gold);
            foreach (float z in new[] { -0.125f, 0.125f })
            {
                // A small zig-zag bolt on each side of the head.
                Shapes.Rounded("Bolt", root, new Vector3(-0.04f, 0.6f, z), new Vector3(0.1f, 0.035f, 0.012f), 0.01f, core).transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
                Shapes.Rounded("Bolt", root, new Vector3(0.04f, 0.53f, z), new Vector3(0.1f, 0.035f, 0.012f), 0.01f, core).transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            }
            return root;
        }

        private void Build()
        {
            Shapes.Primitive(PrimitiveType.Cylinder, "Disc", transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.8f, 0.004f, 0.8f),
                MaterialFactory.CreateTransparent(new Color(Electric.r, Electric.g, Electric.b, 0.4f), ElectricGlow * 0.5f));
            model = BuildModel(transform, 1f);
            model.localPosition = new Vector3(0f, 0.25f, 0f);
            model.localRotation = Quaternion.Euler(0f, 0f, 18f);
            sparks = new GameObject("Sparks").transform;
            sparks.SetParent(transform, false);
            sparks.localPosition = new Vector3(0f, 0.6f, 0f);
            var spark = MaterialFactory.Create(Electric, ElectricGlow * 1.3f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Shapes.Rounded("Spark", sparks, new Vector3(Mathf.Cos(a) * 0.42f, (i % 2) * 0.12f - 0.06f, Mathf.Sin(a) * 0.42f), new Vector3(0.05f, 0.05f, 0.05f), 0.02f, spark);
            }
            beamMat = MaterialFactory.CreateTransparent(new Color(Electric.r, Electric.g, Electric.b, 0.3f), ElectricGlow);
            beam = Shapes.Primitive(PrimitiveType.Cylinder, "Beam", transform, new Vector3(0f, 2.2f, 0f), new Vector3(0.34f, 2.2f, 0.34f), beamMat).transform;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            time += Time.deltaTime;
            popT = Mathf.Min(1f, popT + Time.deltaTime * 3f);
            transform.localScale = Vector3.one * (popT < 1f ? Mathf.Sin(popT * Mathf.PI * 0.5f) * (1f + Mathf.Sin(popT * Mathf.PI) * 0.3f) : 1f);
            if (Leaving) transform.localScale *= 0.8f + 0.2f * Mathf.Sin(time * 18f);
            model.localPosition = new Vector3(0f, 0.25f + Mathf.Sin(time * 3f) * 0.08f, 0f);
            model.localRotation = Quaternion.Euler(0f, time * 90f, 18f);
            sparks.localRotation = Quaternion.Euler(0f, time * -160f, 0f);
            MaterialFactory.SetColors(beamMat, new Color(Electric.r, Electric.g, Electric.b, 0.22f + Mathf.Sin(time * 4f) * 0.08f), ElectricGlow);
        }

        /// <summary>The hammer on the robot's shoulder while it carries it, with a faint crackle around the head.</summary>
        public static GameObject CreateHeld(Transform robot)
        {
            var held = new GameObject("HeldHammer");
            held.transform.SetParent(robot, false);
            held.transform.localPosition = new Vector3(0.32f, 0.35f, -0.08f);
            held.transform.localRotation = Quaternion.Euler(-20f, 0f, -35f);
            var model = BuildModel(held.transform, 0.75f);
            Shapes.Primitive(PrimitiveType.Sphere, "Glow", model, new Vector3(0f, 0.56f, 0f), Vector3.one * 0.55f,
                MaterialFactory.CreateTransparent(new Color(Electric.r, Electric.g, Electric.b, 0.22f), ElectricGlow));
            held.AddComponent<HeldHammerBob>();
            return held;
        }

        /// <summary>
        /// The hit: a hammer arcs from the robot down onto the monster, and a lightning bolt drops from the sky at
        /// the moment it lands.
        /// </summary>
        public static void Swing(Vector3 from, Vector3 target, int level = 3)
        {
            var go = new GameObject("HammerSwing");
            go.AddComponent<HammerSwing>().Begin(from, target, level);
        }

        /// <summary>A jagged lightning bolt from high above down to <paramref name="target"/>, fading in a moment.</summary>
        public static void Strike(Vector3 target)
        {
            for (int b = 0; b < 2; b++)
            {
                var go = new GameObject("Lightning");
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = Weather.AddMaterial;
                line.useWorldSpace = true;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                const int points = 12;
                line.positionCount = points;
                var top = target + new Vector3(Random.Range(-1.2f, 1.2f), 9f, Random.Range(-1.2f, 1.2f));
                for (int i = 0; i < points; i++)
                {
                    float t = i / (float)(points - 1);
                    var p = Vector3.Lerp(top, target, t);
                    if (i > 0 && i < points - 1) p += new Vector3(Random.Range(-0.35f, 0.35f), 0f, Random.Range(-0.35f, 0.35f)) * (1f - t * 0.5f);
                    line.SetPosition(i, p);
                }
                float w = b == 0 ? 0.22f : 0.08f;
                line.widthCurve = new AnimationCurve(new Keyframe(0f, w * 0.6f), new Keyframe(1f, w));
                var c = b == 0 ? new Color(0.6f, 0.85f, 1f) * 2.5f : Color.white * 3f;
                line.startColor = line.endColor = c;
                go.AddComponent<FadeLine>();
            }
        }
    }
}
