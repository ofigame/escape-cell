using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// One piece of a quest, waiting on its tile: a key, an energy core, a cage with a friend inside, an unlit lantern
    /// or a gem. It bobs and glows so it can be spotted across a big floor, and a light pillar marks it from afar.
    /// Lanterns stay where they are once lit; everything else is picked up.
    /// </summary>
    public class QuestItem : MonoBehaviour
    {
        private QuestKind kind;
        private Transform body, beam;
        private Material glow, beamMat;
        private float time, popT;
        private bool lit;

        public bool StaysWhenCollected => kind == QuestKind.Lanterns;

        public static QuestItem Create(QuestKind kind, Vector3 position)
        {
            var go = new GameObject("Quest " + kind);
            go.transform.position = position;
            var item = go.AddComponent<QuestItem>();
            item.kind = kind;
            item.Build();
            return item;
        }

        private static Color Tint(QuestKind k)
        {
            switch (k)
            {
                case QuestKind.Princess: return new Color(0.55f, 0.95f, 1f);
                case QuestKind.Cores: return new Color(0.5f, 1f, 0.6f);
                case QuestKind.Cages: return new Color(1f, 0.75f, 0.35f);
                case QuestKind.Lanterns: return new Color(1f, 0.85f, 0.45f);
                default: return new Color(1f, 0.45f, 0.85f);
            }
        }

        private void Build()
        {
            var tint = Tint(kind);
            glow = MaterialFactory.Create(tint, tint * 1.6f);
            var dark = MaterialFactory.Create(new Color(0.25f, 0.24f, 0.34f), Color.black);
            var white = MaterialFactory.Create(Color.white, new Color(0.6f, 0.6f, 0.7f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Disc", transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.7f, 0.004f, 0.7f),
                MaterialFactory.CreateTransparent(new Color(tint.r, tint.g, tint.b, 0.35f), tint));
            // A soft column of light so the piece can be found from across the floor.
            beamMat = MaterialFactory.CreateTransparent(new Color(tint.r, tint.g, tint.b, 0.3f), tint * 1.6f);
            beam = Shapes.Primitive(PrimitiveType.Cylinder, "Beam", transform, new Vector3(0f, 2.2f, 0f), new Vector3(0.32f, 2.2f, 0.32f), beamMat).transform;

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            switch (kind)
            {
                case QuestKind.Princess:
                    // A crystal key.
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.PI * 0.2f;
                        Shapes.Rounded("Bow", body, new Vector3(Mathf.Cos(a) * 0.11f, 0.13f + Mathf.Sin(a) * 0.11f, 0f), new Vector3(0.075f, 0.075f, 0.06f), 0.025f, glow)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                    }
                    Shapes.Rounded("Shaft", body, new Vector3(0f, -0.1f, 0f), new Vector3(0.06f, 0.3f, 0.06f), 0.025f, glow);
                    Shapes.Rounded("Tooth", body, new Vector3(0.06f, -0.2f, 0f), new Vector3(0.09f, 0.05f, 0.05f), 0.02f, glow);
                    break;
                case QuestKind.Cores:
                    // A glowing energy cell in a metal cage.
                    Shapes.Primitive(PrimitiveType.Sphere, "Core", body, Vector3.zero, Vector3.one * 0.26f, glow);
                    foreach (float y in new[] { -0.17f, 0.17f })
                        Shapes.Rounded("Cap", body, new Vector3(0f, y, 0f), new Vector3(0.26f, 0.06f, 0.26f), 0.03f, dark);
                    for (int i = 0; i < 4; i++)
                        Shapes.Rounded("Bar", body, new Vector3(Mathf.Cos(i * 1.57f) * 0.12f, 0f, Mathf.Sin(i * 1.57f) * 0.12f), new Vector3(0.03f, 0.34f, 0.03f), 0.01f, dark);
                    break;
                case QuestKind.Cages:
                {
                    // A little friend robot behind bars.
                    var friend = MaterialFactory.Create(new Color(0.65f, 0.8f, 1f), new Color(0.05f, 0.08f, 0.12f));
                    Shapes.Rounded("Friend", body, new Vector3(0f, -0.05f, 0f), new Vector3(0.24f, 0.22f, 0.22f), 0.05f, friend);
                    foreach (float s in new[] { -0.05f, 0.05f })
                        Shapes.Rounded("Eye", body, new Vector3(s, -0.03f, 0.11f), new Vector3(0.035f, 0.045f, 0.01f), 0.01f, MaterialFactory.Create(Palette.UiCyan, Palette.UiCyan * 1.5f));
                    Shapes.Rounded("Floor", body, new Vector3(0f, -0.2f, 0f), new Vector3(0.42f, 0.05f, 0.42f), 0.02f, dark);
                    Shapes.Rounded("Roof", body, new Vector3(0f, 0.2f, 0f), new Vector3(0.42f, 0.05f, 0.42f), 0.02f, glow);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Shapes.Rounded("Bar", body, new Vector3(Mathf.Cos(a) * 0.19f, 0f, Mathf.Sin(a) * 0.19f), new Vector3(0.025f, 0.4f, 0.025f), 0.01f, glow);
                    }
                    break;
                }
                case QuestKind.Lanterns:
                    // An unlit lantern on a post (it lights up when reached).
                    Shapes.Rounded("Post", body, new Vector3(0f, -0.05f, 0f), new Vector3(0.06f, 0.5f, 0.06f), 0.02f, dark);
                    Shapes.Rounded("Lamp", body, new Vector3(0f, 0.25f, 0f), new Vector3(0.2f, 0.24f, 0.2f), 0.05f, white);
                    Shapes.Rounded("Top", body, new Vector3(0f, 0.4f, 0f), new Vector3(0.26f, 0.05f, 0.26f), 0.02f, dark);
                    MaterialFactory.SetColors(glow, new Color(0.5f, 0.45f, 0.4f), Color.black);
                    body.Find("Lamp").GetComponent<Renderer>().sharedMaterial = glow;
                    break;
                default:
                    // A faceted gem.
                    var gem = Shapes.Rounded("Gem", body, Vector3.zero, new Vector3(0.24f, 0.24f, 0.24f), 0.03f, glow).transform;
                    gem.localRotation = Quaternion.Euler(45f, 0f, 45f);
                    Shapes.Rounded("Spark", body, new Vector3(0.06f, 0.08f, 0.1f), new Vector3(0.05f, 0.05f, 0.05f), 0.02f, white);
                    break;
            }
            popT = 0f;
            body.localScale = Vector3.zero;
            body.localPosition = new Vector3(0f, 0.45f, 0f);
        }

        /// <summary>Hop to another tile (the old one got hit), popping in again.</summary>
        public void MoveTo(Vector3 position)
        {
            transform.position = position;
            popT = 0f;
        }

        /// <summary>A lantern reached: it lights up for good and its beam goes out.</summary>
        public void Light()
        {
            lit = true;
            var tint = Tint(kind);
            MaterialFactory.SetColors(glow, tint, tint * 3f);
            beam.gameObject.SetActive(false);
            body.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            time += Time.deltaTime;
            popT = Mathf.Min(1f, popT + Time.deltaTime * 3f);
            float pop = popT < 1f ? Mathf.Sin(popT * Mathf.PI * 0.5f) * (1f + Mathf.Sin(popT * Mathf.PI) * 0.3f) : 1f;
            body.localScale = Vector3.one * pop * 1.75f;
            if (lit) return;
            body.localPosition = new Vector3(0f, 0.55f + Mathf.Sin(time * 3f) * 0.08f, 0f);
            if (kind != QuestKind.Lanterns) body.localRotation = Quaternion.Euler(0f, time * 90f, 0f);
            float b = 0.26f + Mathf.Sin(time * 4f) * 0.08f;
            var tint = Tint(kind);
            MaterialFactory.SetColors(beamMat, new Color(tint.r, tint.g, tint.b, b), tint * 1.6f);
        }
    }
}
