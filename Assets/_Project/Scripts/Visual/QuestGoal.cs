using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Where a quest ends: Princess Lumi frozen in a block of ice, a dead generator, a rescue pad, the great beacon, or an
    /// old chest. It waits dim and locked; <see cref="Ready"/> wakes it once every piece is found (a beam of light points
    /// the way), and <see cref="Complete"/> plays its happy ending: the ice shatters and the princess dances, the generator
    /// roars, friends beam up, the beacon blazes, the chest bursts open.
    /// </summary>
    public class QuestGoal : MonoBehaviour
    {
        private QuestKind kind;
        private Transform root, ice, princess, lid, ring, flame, beam;
        private Material glow, beamMat, iceMat;
        private float time, readyT = -1f, doneT = -1f;
        private FxSystem fx;

        public static QuestGoal Create(QuestKind kind, Vector3 position, FxSystem fx)
        {
            var go = new GameObject("Goal " + kind);
            go.transform.position = position;
            var g = go.AddComponent<QuestGoal>();
            g.kind = kind;
            g.fx = fx;
            g.Build();
            return g;
        }

        private static Material M(Color c, Color e = default) => MaterialFactory.Create(c, e);

        private void Build()
        {
            root = new GameObject("Model").transform;
            root.SetParent(transform, false);
            root.localRotation = Quaternion.Euler(0f, 225f, 0f); // front towards the camera
            root.localScale = Vector3.one * 1.4f; // the story's goal should stand out on a big floor
            var dark = M(new Color(0.25f, 0.24f, 0.34f));
            glow = M(new Color(0.6f, 0.6f, 0.7f), Color.black);
            switch (kind)
            {
                case QuestKind.Princess:
                {
                    // Princess Lumi: a little pink robot with a golden crown, frozen inside a block of ice.
                    princess = new GameObject("Princess").transform;
                    princess.SetParent(root, false);
                    var body = M(new Color(1f, 0.7f, 0.85f), new Color(0.15f, 0.05f, 0.1f));
                    var gold = M(new Color(1f, 0.85f, 0.3f), new Color(0.8f, 0.55f, 0.1f));
                    Shapes.Rounded("Body", princess, new Vector3(0f, 0.22f, 0f), new Vector3(0.32f, 0.2f, 0.26f), 0.06f, body);
                    Shapes.Rounded("Skirt", princess, new Vector3(0f, 0.12f, 0f), new Vector3(0.42f, 0.14f, 0.36f), 0.06f, M(new Color(0.85f, 0.45f, 0.95f)));
                    Shapes.Rounded("Head", princess, new Vector3(0f, 0.52f, 0f), new Vector3(0.44f, 0.4f, 0.42f), 0.08f, body);
                    Shapes.Rounded("Visor", princess, new Vector3(0f, 0.52f, 0.21f), new Vector3(0.32f, 0.2f, 0.03f), 0.04f, M(new Color(0.3f, 0.2f, 0.35f)));
                    foreach (float s in new[] { -0.07f, 0.07f })
                        Shapes.Rounded("Eye", princess, new Vector3(s, 0.53f, 0.23f), new Vector3(0.06f, 0.08f, 0.02f), 0.02f, M(new Color(1f, 0.6f, 0.9f), new Color(2f, 0.8f, 1.6f)));
                    for (int i = 0; i < 5; i++)
                        Shapes.Rounded("Crown", princess, new Vector3((i - 2) * 0.07f, 0.8f + (i % 2) * 0.05f, 0f), new Vector3(0.05f, 0.12f + (i % 2) * 0.06f, 0.05f), 0.02f, gold);
                    Shapes.Rounded("Band", princess, new Vector3(0f, 0.75f, 0f), new Vector3(0.36f, 0.05f, 0.1f), 0.02f, gold);
                    iceMat = MaterialFactory.CreateTransparent(new Color(0.65f, 0.9f, 1f, 0.55f), new Color(0.2f, 0.5f, 0.7f));
                    ice = new GameObject("Ice").transform;
                    ice.SetParent(root, false);
                    Shapes.Rounded("Block", ice, new Vector3(0f, 0.5f, 0f), new Vector3(0.82f, 1.05f, 0.82f), 0.1f, iceMat);
                    for (int i = 0; i < 4; i++)
                        Shapes.Rounded("Shard", ice, new Vector3((i % 2 - 0.5f) * 0.6f, 0.15f + i * 0.08f, (i / 2 - 0.5f) * 0.6f), new Vector3(0.18f, 0.4f, 0.18f), 0.04f, iceMat)
                            .transform.localRotation = Quaternion.Euler((i % 2 - 0.5f) * 30f, i * 40f, (i / 2 - 0.5f) * 30f);
                    break;
                }
                case QuestKind.Cores:
                    // A generator pillar with empty sockets and a coil on top.
                    Shapes.Rounded("Base", root, new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.24f, 0.9f), 0.06f, dark);
                    Shapes.Rounded("Pillar", root, new Vector3(0f, 0.7f, 0f), new Vector3(0.36f, 1f, 0.36f), 0.06f, M(new Color(0.5f, 0.52f, 0.6f)));
                    for (int i = 0; i < 4; i++)
                        Shapes.Rounded("Socket", root, new Vector3(Mathf.Cos(i * 1.57f) * 0.2f, 0.6f, Mathf.Sin(i * 1.57f) * 0.2f), new Vector3(0.1f, 0.1f, 0.1f), 0.03f, glow);
                    ring = Shapes.Primitive(PrimitiveType.Cylinder, "Coil", root, new Vector3(0f, 1.3f, 0f), new Vector3(0.7f, 0.05f, 0.7f), glow).transform;
                    break;
                case QuestKind.Cages:
                    // A rescue pad with a ring of lights.
                    Shapes.Primitive(PrimitiveType.Cylinder, "Pad", root, new Vector3(0f, 0.04f, 0f), new Vector3(0.95f, 0.04f, 0.95f), dark);
                    ring = Shapes.Primitive(PrimitiveType.Cylinder, "Ring", root, new Vector3(0f, 0.08f, 0f), new Vector3(0.8f, 0.02f, 0.8f), glow).transform;
                    Shapes.Rounded("H1", root, new Vector3(-0.15f, 0.1f, 0f), new Vector3(0.06f, 0.02f, 0.4f), 0.01f, M(Color.white));
                    Shapes.Rounded("H2", root, new Vector3(0.15f, 0.1f, 0f), new Vector3(0.06f, 0.02f, 0.4f), 0.01f, M(Color.white));
                    Shapes.Rounded("H3", root, new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.02f, 0.06f), 0.01f, M(Color.white));
                    break;
                case QuestKind.Lanterns:
                    // The great beacon: a stone tower with a cold brazier.
                    Shapes.Rounded("Base", root, new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.3f, 0.9f), 0.06f, M(new Color(0.7f, 0.68f, 0.75f)));
                    Shapes.Rounded("Tower", root, new Vector3(0f, 0.8f, 0f), new Vector3(0.5f, 1.1f, 0.5f), 0.08f, M(new Color(0.8f, 0.78f, 0.85f)));
                    Shapes.Rounded("Bowl", root, new Vector3(0f, 1.42f, 0f), new Vector3(0.7f, 0.16f, 0.7f), 0.06f, dark);
                    flame = Shapes.Rounded("Flame", root, new Vector3(0f, 1.62f, 0f), new Vector3(0.36f, 0.4f, 0.36f), 0.14f, glow).transform;
                    flame.localScale = Vector3.one * 0.25f;
                    break;
                default:
                    // An old treasure chest.
                    var wood = M(new Color(0.6f, 0.38f, 0.22f));
                    var gold2 = M(new Color(1f, 0.82f, 0.3f), new Color(0.6f, 0.4f, 0.05f));
                    Shapes.Rounded("Box", root, new Vector3(0f, 0.22f, 0f), new Vector3(0.7f, 0.42f, 0.5f), 0.05f, wood);
                    Shapes.Rounded("Band", root, new Vector3(0f, 0.22f, 0f), new Vector3(0.72f, 0.08f, 0.52f), 0.02f, gold2);
                    lid = new GameObject("Lid").transform;
                    lid.SetParent(root, false);
                    lid.localPosition = new Vector3(0f, 0.43f, -0.25f);
                    Shapes.Rounded("LidTop", lid, new Vector3(0f, 0.06f, 0.25f), new Vector3(0.72f, 0.14f, 0.52f), 0.06f, wood);
                    Shapes.Rounded("Lock", root, new Vector3(0f, 0.36f, 0.26f), new Vector3(0.12f, 0.14f, 0.04f), 0.02f, gold2);
                    break;
            }
            beamMat = MaterialFactory.CreateTransparent(new Color(1f, 0.9f, 0.5f, 0f), new Color(1.5f, 1.2f, 0.4f));
            beam = Shapes.Primitive(PrimitiveType.Cylinder, "Beam", transform, new Vector3(0f, 3f, 0f), new Vector3(0.6f, 3f, 0.6f), beamMat).transform;
            beam.gameObject.SetActive(false);
        }

        /// <summary>Every piece found: the goal wakes up, glows and sends up a beam of light to point the way.</summary>
        public void Ready()
        {
            readyT = 0f;
            beam.gameObject.SetActive(true);
            MaterialFactory.SetColors(glow, new Color(1f, 0.9f, 0.5f), new Color(2.4f, 1.8f, 0.6f));
            if (fx != null) fx.Burst(transform.position + Vector3.up, new Color(1f, 0.9f, 0.5f), new Color(2f, 1.6f, 0.5f), 30, 5f);
        }

        /// <summary>The happy ending for this story.</summary>
        public void Complete()
        {
            doneT = 0f;
            beam.gameObject.SetActive(false);
            var at = transform.position + Vector3.up * 0.6f;
            switch (kind)
            {
                case QuestKind.Princess:
                    if (ice != null) Destroy(ice.gameObject);
                    fx?.Burst(at, new Color(0.7f, 0.95f, 1f), new Color(0.6f, 1.6f, 2.2f), 60, 7f);
                    fx?.Burst(at, new Color(1f, 0.7f, 0.9f), new Color(2.2f, 0.8f, 1.6f), 30, 5f);
                    break;
                case QuestKind.Cores:
                    fx?.Burst(at + Vector3.up * 0.7f, new Color(0.5f, 1f, 0.6f), new Color(0.6f, 2.4f, 0.8f), 60, 7f);
                    break;
                case QuestKind.Cages:
                    for (int i = 0; i < 4; i++) fx?.Burst(at + Random.insideUnitSphere * 0.5f, new Color(0.65f, 0.8f, 1f), Palette.UiCyan * 2f, 18, 6f);
                    break;
                case QuestKind.Lanterns:
                    fx?.Burst(at + Vector3.up, new Color(1f, 0.7f, 0.3f), new Color(3f, 1.4f, 0.3f), 60, 6f);
                    break;
                default:
                    fx?.Burst(at, Palette.Coin, Palette.CoinGlow, 70, 7f);
                    break;
            }
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (ring != null) ring.localRotation = Quaternion.Euler(0f, time * (doneT >= 0f ? 400f : readyT >= 0f ? 120f : 15f), 0f);
            if (readyT >= 0f && doneT < 0f)
            {
                readyT += Time.deltaTime;
                float a = 0.18f + Mathf.Sin(time * 4f) * 0.08f;
                MaterialFactory.SetColors(beamMat, new Color(1f, 0.9f, 0.5f, a), new Color(1.5f, 1.2f, 0.4f));
                root.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(time * 3f)) * 0.04f, 0f);
                if (iceMat != null) MaterialFactory.SetColors(iceMat, new Color(0.75f, 0.95f, 1f, 0.5f + Mathf.Sin(time * 6f) * 0.1f), new Color(0.6f, 1.4f, 2f));
            }
            if (doneT < 0f) return;
            doneT += Time.deltaTime;
            switch (kind)
            {
                case QuestKind.Princess:
                    // Free at last: Lumi hops and spins with joy.
                    if (princess != null)
                    {
                        princess.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(doneT * 7f)) * 0.35f;
                        princess.localRotation = Quaternion.Euler(0f, doneT * 360f, 0f);
                    }
                    break;
                case QuestKind.Lanterns:
                    if (flame != null)
                    {
                        flame.localScale = Vector3.one * Mathf.Min(1.2f, 0.25f + doneT * 2f) * (1f + Mathf.Sin(time * 14f) * 0.08f);
                        MaterialFactory.SetColors(glow, new Color(1f, 0.6f, 0.2f), new Color(3.4f, 1.4f, 0.3f));
                    }
                    break;
                case QuestKind.Treasure:
                    if (lid != null) lid.localRotation = Quaternion.Euler(-Mathf.Min(110f, doneT * 300f), 0f, 0f);
                    break;
                case QuestKind.Cages:
                case QuestKind.Cores:
                    root.localPosition = new Vector3(0f, Mathf.Sin(doneT * 30f) * 0.03f * Mathf.Max(0f, 1f - doneT), 0f);
                    break;
            }
        }
    }
}
