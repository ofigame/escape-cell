using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A guard tower that shoots at foi, in twenty builds (after the user's reference sheets), five for each stage:
    /// underdeveloped (wooden watchtower, palisaded stone tower, spiked log tower, ballista tower, crated mortar),
    /// developed (archer keep, catapult siege tower, cannon-slit tower, cannon-top tower, boiling cauldron furnace),
    /// highly developed (cannon fortress, rocket launcher, mortar gatehouse, clockwork tower, tesla coil) and ultra
    /// developed (beam pylon, plasma cannon, shield dome, rotor antenna, crystal obelisk). The head turns to its
    /// target; <see cref="Muzzle"/> is where the shots leave from and <see cref="BoltColour"/> their colour.
    /// </summary>
    public class TowerModel : MonoBehaviour
    {
        public const int Designs = 20;

        private Transform head, spin;
        private Material core;
        private Color coreColor;
        private float charge, time, flash, spinSpeed;

        public Transform Muzzle { get; private set; }
        public Color BoltColour { get; private set; } = new Color(1f, 0.55f, 0.3f);

        /// <param name="design">0..19: five builds for each stage, from underdeveloped to ultra developed.</param>
        public static TowerModel Build(Transform parent, int design, Color accent)
        {
            var root = new GameObject("Tower").transform;
            root.SetParent(parent, false);
            var t = root.gameObject.AddComponent<TowerModel>();
            t.Make(Mathf.Clamp(design, 0, Designs - 1), accent);
            return t;
        }

        // ---------- Materials and parts ----------

        private static Material M(float r, float g, float b) => MaterialFactory.Create(new Color(r, g, b), Color.black);
        private static Material G(Color c, float glow) => MaterialFactory.Create(c, c * glow);

        private Material Wood => M(0.55f, 0.37f, 0.22f);
        private Material WoodDark => M(0.38f, 0.25f, 0.15f);
        private Material Stone => M(0.66f, 0.63f, 0.57f);
        private Material StoneDark => M(0.47f, 0.45f, 0.41f);
        private Material Straw => MaterialFactory.Create(new Color(0.86f, 0.71f, 0.38f), new Color(0.05f, 0.04f, 0.01f));
        private Material Iron => MaterialFactory.Create(new Color(0.33f, 0.34f, 0.38f), new Color(0.02f, 0.02f, 0.03f));
        private Material Steel => MaterialFactory.Create(new Color(0.6f, 0.62f, 0.68f), new Color(0.03f, 0.03f, 0.04f));
        private Material Bronze => MaterialFactory.Create(new Color(0.78f, 0.55f, 0.3f), new Color(0.12f, 0.07f, 0.02f));
        private Material Hull => MaterialFactory.Create(new Color(0.17f, 0.21f, 0.28f), new Color(0.01f, 0.02f, 0.03f));
        private Material Plate => MaterialFactory.Create(new Color(0.3f, 0.35f, 0.44f), new Color(0.02f, 0.03f, 0.05f));

        private static Transform Box(string n, Transform p, Vector3 at, Vector3 size, Material m, float r = 0.03f) =>
            Shapes.Rounded(n, p, at, size, Mathf.Min(r, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f), m).transform;

        private static Transform Cyl(string n, Transform p, Vector3 at, Vector3 size, Material m) =>
            Shapes.Primitive(PrimitiveType.Cylinder, n, p, at, size, m).transform;

        private static Transform Ball(string n, Transform p, Vector3 at, Vector3 size, Material m) =>
            Shapes.Primitive(PrimitiveType.Sphere, n, p, at, size, m).transform;

        private static readonly (float x, float z)[] Corners = { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) };

        private void Core(Color c, float glow = 1.6f)
        {
            coreColor = c;
            core = G(c, glow);
        }

        private Transform Point(Vector3 local)
        {
            var p = new GameObject("Muzzle").transform;
            p.SetParent(head, false);
            p.localPosition = local;
            return p;
        }

        /// <summary>A little soldier on the head's origin, facing +z.</summary>
        private void Figure(Transform parent, Color tunic)
        {
            var cloth = MaterialFactory.Create(tunic, Color.black);
            var skin = M(0.9f, 0.72f, 0.58f);
            Box("Legs", parent, new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.16f, 0.08f), M(0.25f, 0.2f, 0.18f));
            Box("Body", parent, new Vector3(0f, 0.25f, 0f), new Vector3(0.16f, 0.2f, 0.11f), cloth);
            Ball("Head", parent, new Vector3(0f, 0.41f, 0f), Vector3.one * 0.12f, skin);
            Ball("Hood", parent, new Vector3(0f, 0.44f, -0.01f), new Vector3(0.14f, 0.09f, 0.14f), cloth);
        }

        /// <summary>A cannon barrel along +z from <paramref name="at"/>.</summary>
        private static void Barrel(Transform p, Vector3 at, float length, float width, Material m, float tilt = 80f)
        {
            Cyl("Barrel", p, at + new Vector3(0f, Mathf.Cos(tilt * Mathf.Deg2Rad) * length * 0.5f, Mathf.Sin(tilt * Mathf.Deg2Rad) * length * 0.5f),
                new Vector3(width, length * 0.5f, width), m).localRotation = Quaternion.Euler(tilt, 0f, 0f);
        }

        private void Crenels(float y, float radius, int count, Material m, float size = 0.13f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                Box("Merlon", transform, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius), new Vector3(size, size * 1.3f, size * 0.8f), m)
                    .localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
        }

        private void SquareCrenels(float y, float half, Material m)
        {
            for (int i = 0; i < 4; i++)
                for (int k = -1; k <= 1; k++)
                {
                    var off = i switch { 0 => new Vector3(k * half * 0.66f, 0f, -half), 1 => new Vector3(k * half * 0.66f, 0f, half), 2 => new Vector3(-half, 0f, k * half * 0.66f), _ => new Vector3(half, 0f, k * half * 0.66f) };
                    Box("Merlon", transform, new Vector3(off.x, y, off.z), new Vector3(0.12f, 0.16f, 0.12f), m);
                }
        }

        // ---------- The twenty builds ----------

        private void Make(int design, Color accent)
        {
            head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            Core(new Color(1f, 0.6f, 0.25f));
            switch (design)
            {
                // ----- Underdeveloped -----
                case 0: // 1: wooden watchtower on stilts with a straw roof and an archer
                    foreach (var (x, z) in Corners) Box("Leg", transform, new Vector3(x * 0.32f, 0.8f, z * 0.32f), new Vector3(0.09f, 1.6f, 0.09f), Wood);
                    foreach (float y in new[] { 0.5f, 1.1f })
                        foreach (var r in new[] { 0f, 90f })
                            Box("Brace", transform, new Vector3(0f, y, 0f), new Vector3(0.75f, 0.05f, 0.05f), WoodDark).localRotation = Quaternion.Euler(0f, r + 45f, 22f);
                    Box("Deck", transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.95f, 0.08f, 0.95f), Wood);
                    foreach (var (x, z) in Corners) Box("Post", transform, new Vector3(x * 0.42f, 1.95f, z * 0.42f), new Vector3(0.06f, 0.7f, 0.06f), Wood);
                    Box("Rail", transform, new Vector3(0f, 1.8f, -0.45f), new Vector3(0.9f, 0.05f, 0.05f), WoodDark);
                    for (int i = 0; i < 3; i++) Box("Thatch", transform, new Vector3(0f, 2.35f + i * 0.13f, 0f), new Vector3(1.15f - i * 0.36f, 0.16f, 1.15f - i * 0.36f), Straw).localRotation = Quaternion.Euler(0f, 45f, 0f);
                    head.localPosition = new Vector3(0f, 1.64f, 0f);
                    Figure(head, new Color(0.45f, 0.55f, 0.3f));
                    Box("Bow", head, new Vector3(0.12f, 0.3f, 0.16f), new Vector3(0.03f, 0.32f, 0.04f), WoodDark);
                    Muzzle = Point(new Vector3(0.12f, 0.3f, 0.25f));
                    break;
                case 1: // 2: round stone tower with a straw cone roof inside a wooden palisade
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI * 2f / 16f;
                        Box("Stake", transform, new Vector3(Mathf.Cos(a) * 0.5f, 0.22f, Mathf.Sin(a) * 0.5f), new Vector3(0.09f, 0.44f, 0.09f), Wood);
                        Box("Tip", transform, new Vector3(Mathf.Cos(a) * 0.5f, 0.48f, Mathf.Sin(a) * 0.5f), new Vector3(0.05f, 0.08f, 0.05f), WoodDark);
                    }
                    Cyl("Tower", transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.52f, 0.9f, 0.52f), Stone);
                    foreach (float y in new[] { 0.6f, 1.1f, 1.5f }) Cyl("Course", transform, new Vector3(0f, y, 0f), new Vector3(0.54f, 0.015f, 0.54f), StoneDark);
                    Box("Window", transform, new Vector3(0f, 1.55f, -0.26f), new Vector3(0.14f, 0.16f, 0.03f), M(0.1f, 0.08f, 0.1f));
                    for (int i = 0; i < 4; i++) Cyl("Roof", transform, new Vector3(0f, 1.9f + i * 0.14f, 0f), new Vector3(0.75f - i * 0.18f, 0.08f, 0.75f - i * 0.18f), Straw);
                    head.localPosition = new Vector3(0f, 1.5f, 0f);
                    Muzzle = Point(new Vector3(0f, 0f, 0.3f));
                    break;
                case 2: // 3: stacked log tower on a stone footing with a spiked roof
                    Box("Footing", transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.8f, 0.36f, 0.8f), StoneDark, 0.08f);
                    for (int i = 0; i < 8; i++)
                    {
                        bool alongX = i % 2 == 0;
                        float y = 0.42f + i * 0.17f;
                        foreach (float s in new[] { -1f, 1f })
                            Cyl("Log", transform, alongX ? new Vector3(0f, y, s * 0.3f) : new Vector3(s * 0.3f, y, 0f), new Vector3(0.16f, 0.4f, 0.16f), i % 3 == 0 ? WoodDark : Wood)
                                .localRotation = Quaternion.Euler(alongX ? 0f : 90f, 0f, alongX ? 90f : 0f);
                    }
                    Box("Deck", transform, new Vector3(0f, 1.8f, 0f), new Vector3(0.9f, 0.08f, 0.9f), Wood);
                    for (int i = 0; i < 3; i++) Box("Roof", transform, new Vector3(0f, 2.05f + i * 0.13f, 0f), new Vector3(0.95f - i * 0.3f, 0.15f, 0.95f - i * 0.3f), WoodDark).localRotation = Quaternion.Euler(0f, 45f, 0f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Box("Spike", transform, new Vector3(Mathf.Cos(a) * 0.5f, 2.02f, Mathf.Sin(a) * 0.5f), new Vector3(0.04f, 0.25f, 0.04f), Iron).localRotation = Quaternion.Euler(Mathf.Sin(a) * 30f, 0f, -Mathf.Cos(a) * 30f);
                    }
                    head.localPosition = new Vector3(0f, 1.84f, 0f);
                    Figure(head, new Color(0.5f, 0.35f, 0.25f));
                    Muzzle = Point(new Vector3(0f, 0.3f, 0.2f));
                    break;
                case 3: // 4: wooden tower carrying a big ballista
                    foreach (var (x, z) in Corners) Box("Leg", transform, new Vector3(x * 0.3f, 0.75f, z * 0.3f), new Vector3(0.1f, 1.5f, 0.1f), Wood);
                    foreach (float y in new[] { 0.45f, 1.05f })
                        foreach (var r in new[] { 0f, 90f })
                            Box("Brace", transform, new Vector3(0f, y, 0f), new Vector3(0.72f, 0.05f, 0.05f), WoodDark).localRotation = Quaternion.Euler(0f, r, 25f);
                    Box("Ladder", transform, new Vector3(0.38f, 0.75f, 0f), new Vector3(0.04f, 1.5f, 0.2f), WoodDark).localRotation = Quaternion.Euler(0f, 0f, -8f);
                    Box("Deck", transform, new Vector3(0f, 1.52f, 0f), new Vector3(0.85f, 0.08f, 0.85f), Wood);
                    head.localPosition = new Vector3(0f, 1.6f, 0f);
                    Box("Stand", head, new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.24f, 0.12f), WoodDark);
                    Box("Rail", head, new Vector3(0f, 0.28f, 0.12f), new Vector3(0.1f, 0.08f, 0.7f), Wood);
                    Box("Arms", head, new Vector3(0f, 0.3f, 0.36f), new Vector3(0.75f, 0.06f, 0.08f), WoodDark);
                    Box("Bolt", head, new Vector3(0f, 0.34f, 0.2f), new Vector3(0.03f, 0.03f, 0.6f), Iron);
                    Muzzle = Point(new Vector3(0f, 0.34f, 0.52f));
                    break;
                case 4: // 5: a squat mortar in a wooden crate
                    Box("Crate", transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.9f, 0.44f, 0.9f), Wood, 0.05f);
                    foreach (var (x, z) in Corners) Box("Corner", transform, new Vector3(x * 0.43f, 0.22f, z * 0.43f), new Vector3(0.08f, 0.46f, 0.08f), WoodDark);
                    Box("Plank", transform, new Vector3(0f, 0.22f, -0.46f), new Vector3(0.9f, 0.08f, 0.02f), WoodDark);
                    head.localPosition = new Vector3(0f, 0.44f, 0f);
                    Ball("Base", head, new Vector3(0f, 0.08f, 0f), new Vector3(0.48f, 0.3f, 0.48f), Iron);
                    Cyl("Tube", head, new Vector3(0f, 0.3f, 0.1f), new Vector3(0.3f, 0.22f, 0.3f), Iron).localRotation = Quaternion.Euler(35f, 0f, 0f);
                    Cyl("Mouth", head, new Vector3(0f, 0.46f, 0.21f), new Vector3(0.24f, 0.02f, 0.24f), M(0.08f, 0.08f, 0.1f)).localRotation = Quaternion.Euler(35f, 0f, 0f);
                    Muzzle = Point(new Vector3(0f, 0.5f, 0.24f));
                    break;

                // ----- Developed -----
                case 5: // 6: square stone keep with battlements, an awning over the door and archers on top
                    Box("Keep", transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.82f, 1.7f, 0.82f), Stone, 0.04f);
                    foreach (float y in new[] { 0.45f, 0.95f, 1.45f }) Box("Course", transform, new Vector3(0f, y, 0f), new Vector3(0.84f, 0.03f, 0.84f), StoneDark);
                    Box("Door", transform, new Vector3(0f, 0.25f, -0.42f), new Vector3(0.26f, 0.42f, 0.03f), WoodDark, 0.1f);
                    Box("Awning", transform, new Vector3(0f, 0.52f, -0.5f), new Vector3(0.4f, 0.04f, 0.18f), M(0.65f, 0.3f, 0.2f)).localRotation = Quaternion.Euler(20f, 0f, 0f);
                    Box("Top", transform, new Vector3(0f, 1.76f, 0f), new Vector3(0.94f, 0.1f, 0.94f), StoneDark);
                    SquareCrenels(1.9f, 0.42f, Stone);
                    head.localPosition = new Vector3(0f, 1.81f, 0f);
                    Figure(head, new Color(0.3f, 0.4f, 0.65f));
                    Box("Bow", head, new Vector3(0.12f, 0.3f, 0.16f), new Vector3(0.03f, 0.32f, 0.04f), WoodDark);
                    Muzzle = Point(new Vector3(0.12f, 0.3f, 0.25f));
                    break;
                case 6: // 7: plank siege tower on wheels with a catapult on top
                    Box("Body", transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.78f, 1.5f, 0.78f), Wood, 0.03f);
                    for (int i = 0; i < 5; i++) Box("Plank", transform, new Vector3(0f, 0.25f + i * 0.3f, -0.4f), new Vector3(0.8f, 0.04f, 0.02f), WoodDark);
                    foreach (var (x, z) in Corners) Cyl("Wheel", transform, new Vector3(x * 0.42f, 0.14f, z * 0.28f), new Vector3(0.28f, 0.04f, 0.28f), WoodDark).localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Box("Deck", transform, new Vector3(0f, 1.62f, 0f), new Vector3(0.9f, 0.08f, 0.9f), WoodDark);
                    head.localPosition = new Vector3(0f, 1.66f, 0f);
                    Box("Frame", head, new Vector3(0f, 0.22f, 0f), new Vector3(0.5f, 0.44f, 0.08f), Wood);
                    Box("Arm", head, new Vector3(0f, 0.42f, 0.1f), new Vector3(0.07f, 0.07f, 0.75f), WoodDark).localRotation = Quaternion.Euler(-30f, 0f, 0f);
                    Ball("Bucket", head, new Vector3(0f, 0.62f, 0.42f), new Vector3(0.18f, 0.12f, 0.18f), WoodDark);
                    Ball("Stone", head, new Vector3(0f, 0.7f, 0.42f), Vector3.one * 0.13f, Stone);
                    Muzzle = Point(new Vector3(0f, 0.7f, 0.42f));
                    break;
                case 7: // 8: stone tower with a cannon looking out of the battlements
                    Box("Tower", transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.72f, 1.8f, 0.72f), Stone, 0.04f);
                    Box("Plinth", transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.86f, 0.24f, 0.86f), StoneDark);
                    Box("Arch", transform, new Vector3(0f, 0.38f, -0.37f), new Vector3(0.24f, 0.36f, 0.03f), M(0.12f, 0.1f, 0.12f), 0.1f);
                    Box("Top", transform, new Vector3(0f, 1.85f, 0f), new Vector3(0.84f, 0.1f, 0.84f), StoneDark);
                    SquareCrenels(1.98f, 0.37f, Stone);
                    head.localPosition = new Vector3(0f, 1.92f, 0f);
                    Box("Carriage", head, new Vector3(0f, 0.08f, 0f), new Vector3(0.22f, 0.14f, 0.3f), WoodDark);
                    Barrel(head, new Vector3(0f, 0.16f, 0f), 0.6f, 0.13f, Iron);
                    Muzzle = Point(new Vector3(0f, 0.2f, 0.62f));
                    break;
                case 8: // 9: stone tower carrying a big cannon on a turntable
                    Box("Tower", transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 1.7f, 0.8f), StoneDark, 0.04f);
                    foreach (float y in new[] { 0.5f, 1.1f }) Box("Course", transform, new Vector3(0f, y, 0f), new Vector3(0.82f, 0.03f, 0.82f), Stone);
                    Box("Door", transform, new Vector3(0f, 0.28f, -0.41f), new Vector3(0.24f, 0.44f, 0.03f), WoodDark, 0.1f);
                    Box("Top", transform, new Vector3(0f, 1.76f, 0f), new Vector3(0.92f, 0.12f, 0.92f), Stone);
                    head.localPosition = new Vector3(0f, 1.82f, 0f);
                    Cyl("Turntable", head, new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 0.04f, 0.6f), Iron);
                    Box("Carriage", head, new Vector3(0f, 0.16f, 0f), new Vector3(0.36f, 0.2f, 0.4f), WoodDark);
                    Barrel(head, new Vector3(0f, 0.26f, -0.1f), 0.95f, 0.2f, Iron, 75f);
                    Muzzle = Point(new Vector3(0f, 0.38f, 0.82f));
                    break;
                case 9: // 10: round stone furnace with a fire mouth and a cauldron of green brew on top
                    Cyl("Furnace", transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.8f, 0.55f, 0.8f), Stone);
                    foreach (float y in new[] { 0.3f, 0.7f }) Cyl("Course", transform, new Vector3(0f, y, 0f), new Vector3(0.82f, 0.015f, 0.82f), StoneDark);
                    Box("FireMouth", transform, new Vector3(0f, 0.35f, -0.4f), new Vector3(0.32f, 0.28f, 0.04f), G(new Color(1f, 0.5f, 0.15f), 2.6f), 0.08f);
                    Cyl("Rim", transform, new Vector3(0f, 1.12f, 0f), new Vector3(0.86f, 0.04f, 0.86f), Iron);
                    head.localPosition = new Vector3(0f, 1.14f, 0f);
                    Ball("Cauldron", head, new Vector3(0f, 0.25f, 0f), new Vector3(0.7f, 0.5f, 0.7f), Iron);
                    Core(new Color(0.55f, 1f, 0.3f), 1.8f);
                    Cyl("Brew", head, new Vector3(0f, 0.46f, 0f), new Vector3(0.58f, 0.02f, 0.58f), core);
                    foreach (float s in new[] { -1f, 1f }) Ball("Bubble", head, new Vector3(s * 0.12f, 0.5f, s * 0.08f), Vector3.one * 0.1f, core);
                    BoltColour = new Color(0.55f, 1f, 0.3f);
                    Muzzle = Point(new Vector3(0f, 0.5f, 0.2f));
                    break;

                // ----- Highly developed -----
                case 10: // 11: stone fortress with corner turrets, a cannon on each and one in the middle
                    Box("Fort", transform, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f), StoneDark, 0.04f);
                    foreach (var (x, z) in Corners)
                    {
                        Cyl("Turret", transform, new Vector3(x * 0.48f, 0.65f, z * 0.48f), new Vector3(0.32f, 0.65f, 0.32f), Stone);
                        Box("Cannon", transform, new Vector3(x * 0.48f, 1.34f, z * 0.48f - 0.12f), new Vector3(0.08f, 0.08f, 0.3f), Iron);
                    }
                    Box("Gate", transform, new Vector3(0f, 0.3f, -0.51f), new Vector3(0.3f, 0.5f, 0.03f), WoodDark, 0.12f);
                    SquareCrenels(1.08f, 0.38f, Stone);
                    head.localPosition = new Vector3(0f, 1.02f, 0f);
                    Box("Mount", head, new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.2f, 0.32f), Iron);
                    Barrel(head, new Vector3(0f, 0.18f, 0f), 0.75f, 0.16f, Steel, 78f);
                    Muzzle = Point(new Vector3(0f, 0.26f, 0.75f));
                    break;
                case 11: // 12: rocket launcher: a girder tower with a big rocket on top
                    foreach (var (x, z) in Corners) Box("Girder", transform, new Vector3(x * 0.28f, 0.8f, z * 0.28f), new Vector3(0.07f, 1.6f, 0.07f), Steel);
                    for (int i = 0; i < 4; i++)
                        foreach (var r in new[] { 45f, -45f })
                            Box("Lattice", transform, new Vector3(0f, 0.25f + i * 0.4f, -0.28f), new Vector3(0.6f, 0.03f, 0.03f), Iron).localRotation = Quaternion.Euler(0f, 0f, r);
                    Box("Platform", transform, new Vector3(0f, 1.62f, 0f), new Vector3(0.75f, 0.08f, 0.75f), Iron);
                    head.localPosition = new Vector3(0f, 1.66f, 0f);
                    Box("Cradle", head, new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.24f, 0.12f), Iron);
                    Cyl("Rocket", head, new Vector3(0f, 0.4f, 0.1f), new Vector3(0.2f, 0.42f, 0.2f), MaterialFactory.Create(new Color(0.9f, 0.9f, 0.92f), new Color(0.05f, 0.05f, 0.05f))).localRotation = Quaternion.Euler(60f, 0f, 0f);
                    Ball("Nose", head, new Vector3(0f, 0.62f, 0.48f), new Vector3(0.2f, 0.3f, 0.2f), M(0.85f, 0.3f, 0.2f)).localRotation = Quaternion.Euler(60f, 0f, 0f);
                    foreach (float s in new[] { -1f, 1f }) Box("Fin", head, new Vector3(s * 0.12f, 0.2f, -0.2f), new Vector3(0.12f, 0.12f, 0.02f), M(0.85f, 0.3f, 0.2f));
                    Muzzle = Point(new Vector3(0f, 0.65f, 0.55f));
                    break;
                case 12: // 13: tall stone gatehouse with a portcullis and a mortar on top
                    Box("Tower", transform, new Vector3(0f, 1f, 0f), new Vector3(0.78f, 2f, 0.78f), StoneDark, 0.04f);
                    foreach (var (x, z) in Corners) Box("Buttress", transform, new Vector3(x * 0.4f, 0.55f, z * 0.4f), new Vector3(0.18f, 1.1f, 0.18f), Stone);
                    Box("GateHole", transform, new Vector3(0f, 0.4f, -0.4f), new Vector3(0.32f, 0.6f, 0.02f), M(0.08f, 0.07f, 0.1f), 0.1f);
                    for (int i = 0; i < 4; i++) Box("Portcullis", transform, new Vector3(-0.11f + i * 0.075f, 0.4f, -0.41f), new Vector3(0.025f, 0.58f, 0.025f), Iron);
                    Box("Cross", transform, new Vector3(0f, 0.5f, -0.41f), new Vector3(0.3f, 0.025f, 0.025f), Iron);
                    Box("Top", transform, new Vector3(0f, 2.05f, 0f), new Vector3(0.9f, 0.1f, 0.9f), Stone);
                    SquareCrenels(2.18f, 0.4f, Stone);
                    head.localPosition = new Vector3(0f, 2.1f, 0f);
                    Ball("MortarBase", head, new Vector3(0f, 0.1f, 0f), new Vector3(0.42f, 0.24f, 0.42f), Iron);
                    Cyl("MortarTube", head, new Vector3(0f, 0.28f, 0.08f), new Vector3(0.28f, 0.2f, 0.28f), Iron).localRotation = Quaternion.Euler(30f, 0f, 0f);
                    Muzzle = Point(new Vector3(0f, 0.45f, 0.18f));
                    break;
                case 13: // 14: clockwork tower: bronze and iron with big turning gears and a clock face
                    Box("Body", transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.72f, 1.9f, 0.72f), WoodDark, 0.04f);
                    foreach (var (x, z) in Corners) Box("Strap", transform, new Vector3(x * 0.37f, 0.95f, z * 0.37f), new Vector3(0.06f, 1.9f, 0.06f), Bronze);
                    Cyl("ClockFace", transform, new Vector3(0f, 1.45f, -0.37f), new Vector3(0.5f, 0.02f, 0.5f), MaterialFactory.Create(new Color(0.95f, 0.9f, 0.75f), new Color(0.1f, 0.09f, 0.06f))).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Box("Hand", transform, new Vector3(0f, 1.52f, -0.39f), new Vector3(0.03f, 0.18f, 0.01f), Iron);
                    spin = new GameObject("Gear").transform;
                    spin.SetParent(transform, false);
                    spin.localPosition = new Vector3(0.38f, 0.75f, 0f);
                    spin.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Cyl("GearDisc", spin, Vector3.zero, new Vector3(0.6f, 0.04f, 0.6f), Bronze);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.PI / 5f;
                        Box("Tooth", spin, new Vector3(Mathf.Cos(a) * 0.32f, 0f, Mathf.Sin(a) * 0.32f), new Vector3(0.08f, 0.05f, 0.06f), Bronze).localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                    }
                    spinSpeed = 40f;
                    Box("Top", transform, new Vector3(0f, 1.95f, 0f), new Vector3(0.82f, 0.08f, 0.82f), Bronze);
                    head.localPosition = new Vector3(0f, 2f, 0f);
                    Ball("Dome", head, new Vector3(0f, 0.1f, 0f), new Vector3(0.5f, 0.32f, 0.5f), Bronze);
                    Barrel(head, new Vector3(0f, 0.12f, 0f), 0.6f, 0.12f, Iron, 82f);
                    Muzzle = Point(new Vector3(0f, 0.16f, 0.6f));
                    break;
                case 14: // 15: tesla coil: a domed base, a copper coil and a lightning ball
                    Ball("Base", transform, new Vector3(0f, 0.25f, 0f), new Vector3(1f, 0.6f, 1f), Iron);
                    Cyl("Ring", transform, new Vector3(0f, 0.12f, 0f), new Vector3(1.05f, 0.04f, 1.05f), Steel);
                    Cyl("Column", transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.2f, 0.65f, 0.2f), Steel);
                    for (int i = 0; i < 6; i++) Cyl("Coil", transform, new Vector3(0f, 0.6f + i * 0.15f, 0f), new Vector3(0.36f - i * 0.03f, 0.03f, 0.36f - i * 0.03f), Bronze);
                    Core(new Color(0.55f, 0.8f, 1f), 2.6f);
                    head.localPosition = new Vector3(0f, 1.65f, 0f);
                    Ball("Orb", head, Vector3.zero, Vector3.one * 0.38f, core);
                    Ball("Halo", head, Vector3.zero, Vector3.one * 0.6f, MaterialFactory.CreateTransparent(new Color(0.5f, 0.8f, 1f, 0.18f), new Color(0.4f, 0.8f, 1.6f)));
                    BoltColour = new Color(0.55f, 0.8f, 1f);
                    Muzzle = Point(Vector3.zero);
                    break;

                // ----- Ultra developed -----
                case 15: // 16: beam pylon: a glowing column firing a beam straight up between shots
                {
                    var line = G(new Color(0.3f, 0.85f, 1f), 2.4f);
                    Box("Base", transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.95f, 0.24f, 0.95f), Hull, 0.06f);
                    Box("Pylon", transform, new Vector3(0f, 1f, 0f), new Vector3(0.5f, 1.6f, 0.5f), Plate, 0.08f);
                    foreach (var (x, z) in new[] { (0f, -1f), (0f, 1f), (-1f, 0f), (1f, 0f) })
                        Box("Seam", transform, new Vector3(x * 0.26f, 1f, z * 0.26f), new Vector3(x != 0 ? 0.02f : 0.08f, 1.4f, z != 0 ? 0.02f : 0.08f), line);
                    Core(new Color(0.4f, 0.95f, 1f), 2.6f);
                    Box("Emitter", transform, new Vector3(0f, 1.9f, 0f), new Vector3(0.3f, 0.2f, 0.3f), core);
                    Cyl("Beam", transform, new Vector3(0f, 2.9f, 0f), new Vector3(0.08f, 0.9f, 0.08f), MaterialFactory.CreateTransparent(new Color(0.4f, 0.95f, 1f, 0.6f), new Color(0.8f, 2.5f, 3f)));
                    head.localPosition = new Vector3(0f, 1.9f, 0f);
                    BoltColour = new Color(0.4f, 0.95f, 1f);
                    Muzzle = Point(new Vector3(0f, 0f, 0.2f));
                    break;
                }
                case 16: // 17: plasma cannon: a heavy orange-lit cannon on a round armoured base
                {
                    var glow = G(new Color(1f, 0.6f, 0.2f), 2.4f);
                    Cyl("Base", transform, new Vector3(0f, 0.2f, 0f), new Vector3(1f, 0.2f, 1f), Hull);
                    Cyl("BaseGlow", transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.04f, 0.03f, 1.04f), glow);
                    Box("Neck", transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.4f, 0.4f, 0.4f), Plate, 0.06f);
                    Core(new Color(1f, 0.6f, 0.2f), 2.4f);
                    head.localPosition = new Vector3(0f, 0.85f, 0f);
                    Box("Housing", head, new Vector3(0f, 0.12f, -0.05f), new Vector3(0.5f, 0.36f, 0.6f), Plate, 0.08f);
                    Box("Barrel", head, new Vector3(0f, 0.18f, 0.5f), new Vector3(0.24f, 0.24f, 0.8f), Hull, 0.08f);
                    foreach (float z in new[] { 0.3f, 0.55f, 0.8f }) Box("Coil", head, new Vector3(0f, 0.18f, z), new Vector3(0.3f, 0.3f, 0.05f), core, 0.06f);
                    BoltColour = new Color(1f, 0.6f, 0.2f);
                    Muzzle = Point(new Vector3(0f, 0.18f, 0.92f));
                    break;
                }
                case 17: // 18: shield tower: a glowing column under a blue energy dome
                {
                    var line = G(new Color(0.3f, 0.75f, 1f), 2.2f);
                    Cyl("Base", transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.95f, 0.15f, 0.95f), Hull);
                    Cyl("Column", transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.42f, 0.55f, 0.42f), Plate);
                    foreach (float y in new[] { 0.5f, 0.9f, 1.25f }) Cyl("Band", transform, new Vector3(0f, y, 0f), new Vector3(0.46f, 0.03f, 0.46f), line);
                    Core(new Color(0.4f, 0.8f, 1f), 2.4f);
                    head.localPosition = new Vector3(0f, 1.45f, 0f);
                    Ball("Core", head, Vector3.zero, Vector3.one * 0.3f, core);
                    Ball("Dome", transform, new Vector3(0f, 0.75f, 0f), new Vector3(1.6f, 1.6f, 1.6f), MaterialFactory.CreateTransparent(new Color(0.3f, 0.7f, 1f, 0.14f), new Color(0.2f, 0.5f, 0.9f)));
                    BoltColour = new Color(0.4f, 0.8f, 1f);
                    Muzzle = Point(new Vector3(0f, 0f, 0.15f));
                    break;
                }
                case 18: // 19: rotor antenna: an X of glowing blades turning round an orb
                {
                    var line = G(new Color(0.35f, 0.9f, 1f), 2.4f);
                    Cyl("Base", transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.15f, 0.9f), Hull);
                    Box("Arm", transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.2f, 1.1f, 0.2f), Plate);
                    Box("Elbow", transform, new Vector3(0f, 1.32f, -0.1f), new Vector3(0.26f, 0.2f, 0.3f), Plate);
                    Core(new Color(0.4f, 0.95f, 1f), 2.6f);
                    head.localPosition = new Vector3(0f, 1.6f, 0f);
                    Ball("Orb", head, Vector3.zero, Vector3.one * 0.32f, core);
                    spin = new GameObject("Rotor").transform;
                    spin.SetParent(head, false);
                    foreach (float r in new[] { 45f, -45f })
                        Box("Blade", spin, Vector3.zero, new Vector3(1.1f, 0.08f, 0.04f), line).localRotation = Quaternion.Euler(0f, 0f, r);
                    spinSpeed = 160f;
                    BoltColour = new Color(0.4f, 0.95f, 1f);
                    Muzzle = Point(new Vector3(0f, 0f, 0.18f));
                    break;
                }
                default: // 20: crystal obelisk: a dark monolith with purple light and a crystal crown
                {
                    var purple = G(new Color(0.75f, 0.35f, 1f), 2.4f);
                    Box("Plinth", transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.9f, 0.2f, 0.9f), Hull);
                    Box("Obelisk", transform, new Vector3(0f, 1f, 0f), new Vector3(0.45f, 1.6f, 0.45f), MaterialFactory.Create(new Color(0.12f, 0.1f, 0.18f), new Color(0.02f, 0.01f, 0.04f)), 0.04f);
                    foreach (float s in new[] { -1f, 1f }) Box("Edge", transform, new Vector3(s * 0.23f, 1f, -0.23f), new Vector3(0.03f, 1.5f, 0.03f), purple);
                    Box("Rune", transform, new Vector3(0f, 0.8f, -0.235f), new Vector3(0.12f, 0.5f, 0.02f), purple);
                    Core(new Color(0.8f, 0.4f, 1f), 2.6f);
                    head.localPosition = new Vector3(0f, 1.95f, 0f);
                    Box("Crystal", head, new Vector3(0f, 0.15f, 0f), new Vector3(0.24f, 0.4f, 0.24f), core).localRotation = Quaternion.Euler(0f, 45f, 0f);
                    BoltColour = new Color(0.8f, 0.4f, 1f);
                    Muzzle = Point(new Vector3(0f, 0.15f, 0.15f));
                    break;
                }
            }
        }

        /// <summary>Turns the head towards a point (smoothly).</summary>
        public void Aim(Vector3 target, float dt)
        {
            var d = target - head.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(d), dt * 6f);
        }

        /// <summary>0..1 while winding up a shot (the core glows brighter).</summary>
        public void SetCharge(float k) => charge = Mathf.Clamp01(k);

        public void Flash() => flash = 1f;

        private void Update()
        {
            time += Time.deltaTime;
            flash = Mathf.Max(0f, flash - Time.deltaTime * 4f);
            if (spin != null) spin.localRotation = spin.name == "Gear" ? Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, time * spinSpeed, 0f) : Quaternion.Euler(0f, 0f, time * spinSpeed);
            float glow = 1.2f + charge * 2.5f + flash * 2f;
            MaterialFactory.SetColors(core, Color.Lerp(coreColor, Color.white, flash * 0.6f), coreColor * glow);
        }
    }
}
