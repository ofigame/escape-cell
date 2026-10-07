using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The five hammers of the workshop, handle along +Y with the head on top (about 0.7 units tall at scale 1):
    /// 1 wooden mallet, 2 iron hammer, 3 thunder hammer, 4 crystal sledge, 5 star hammer.
    /// </summary>
    public static class HammerModels
    {
        public static Color Glow(int level)
        {
            switch (level)
            {
                case 1: return new Color(0.9f, 0.7f, 0.45f);
                case 2: return new Color(0.8f, 0.85f, 0.95f);
                case 3: return ThunderHammer.Electric;
                case 4: return new Color(0.75f, 0.5f, 1f);
                default: return new Color(1f, 0.85f, 0.35f);
            }
        }

        public static Transform Build(Transform parent, int level, float scale = 1f)
        {
            if (level == 3) return ThunderHammer.BuildModel(parent, scale);
            var root = new GameObject("Hammer" + level).transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;
            switch (level)
            {
                case 1:
                {
                    var wood = MaterialFactory.Create(new Color(0.78f, 0.55f, 0.33f), Color.black);
                    var dark = MaterialFactory.Create(new Color(0.5f, 0.33f, 0.2f), Color.black);
                    Shapes.Rounded("Grip", root, new Vector3(0f, 0.2f, 0f), new Vector3(0.08f, 0.44f, 0.08f), 0.035f, wood);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Head", root, new Vector3(0f, 0.53f, 0f), new Vector3(0.24f, 0.2f, 0.24f), wood)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    foreach (float x in new[] { -0.19f, 0.19f })
                        Shapes.Primitive(PrimitiveType.Cylinder, "Band", root, new Vector3(x, 0.53f, 0f), new Vector3(0.25f, 0.015f, 0.25f), dark)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                }
                case 2:
                {
                    var iron = MaterialFactory.Create(new Color(0.62f, 0.66f, 0.74f), new Color(0.08f, 0.08f, 0.1f));
                    var grip = MaterialFactory.Create(new Color(0.3f, 0.22f, 0.18f), Color.black);
                    Shapes.Rounded("Grip", root, new Vector3(0f, 0.2f, 0f), new Vector3(0.09f, 0.44f, 0.09f), 0.04f, grip);
                    Shapes.Rounded("Collar", root, new Vector3(0f, 0.43f, 0f), new Vector3(0.13f, 0.05f, 0.13f), 0.02f, iron);
                    Shapes.Rounded("Head", root, new Vector3(0f, 0.55f, 0f), new Vector3(0.42f, 0.2f, 0.22f), 0.04f, iron);
                    Shapes.Rounded("Face", root, new Vector3(0.23f, 0.55f, 0f), new Vector3(0.04f, 0.24f, 0.26f), 0.015f, iron);
                    break;
                }
                case 4:
                {
                    var crystal = MaterialFactory.CreateTransparent(new Color(0.75f, 0.55f, 1f, 0.85f), new Color(1f, 0.6f, 2.2f));
                    var silver = MaterialFactory.Create(new Color(0.85f, 0.88f, 0.95f), new Color(0.2f, 0.2f, 0.25f));
                    Shapes.Rounded("Grip", root, new Vector3(0f, 0.22f, 0f), new Vector3(0.09f, 0.48f, 0.09f), 0.04f, silver);
                    var head = Shapes.Rounded("Head", root, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.26f, 0.26f), 0.05f, crystal);
                    foreach (float x in new[] { -0.3f, 0.3f })
                        Shapes.Rounded("Spike", root, new Vector3(x, 0.6f, 0f), new Vector3(0.14f, 0.14f, 0.14f), 0.03f, crystal)
                            .transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Glow", root, new Vector3(0f, 0.6f, 0f), Vector3.one * 0.6f,
                        MaterialFactory.CreateTransparent(new Color(0.75f, 0.5f, 1f, 0.18f), new Color(1f, 0.5f, 2f)));
                    break;
                }
                default:
                {
                    // The star hammer: gold, with a glowing star on each face and a halo of light.
                    var gold = MaterialFactory.Create(new Color(1f, 0.8f, 0.3f), new Color(1.6f, 1.1f, 0.3f));
                    var white = MaterialFactory.Create(Color.white, new Color(2.4f, 2.2f, 1.6f));
                    Shapes.Rounded("Grip", root, new Vector3(0f, 0.22f, 0f), new Vector3(0.1f, 0.48f, 0.1f), 0.045f, MaterialFactory.Create(new Color(0.95f, 0.95f, 1f), Color.black));
                    Shapes.Rounded("Head", root, new Vector3(0f, 0.62f, 0f), new Vector3(0.52f, 0.28f, 0.28f), 0.08f, gold);
                    foreach (float z in new[] { -0.145f, 0.145f })
                        for (int k = 0; k < 3; k++)
                            Shapes.Rounded("Star", root, new Vector3(0f, 0.62f, z), new Vector3(0.05f, 0.2f, 0.012f), 0.01f, white)
                                .transform.localRotation = Quaternion.Euler(0f, 0f, k * 60f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Halo", root, new Vector3(0f, 0.62f, 0f), Vector3.one * 0.75f,
                        MaterialFactory.CreateTransparent(new Color(1f, 0.85f, 0.4f, 0.16f), new Color(2f, 1.5f, 0.4f)));
                    break;
                }
            }
            return root;
        }

        /// <summary>The robot's hammer in its right hand (arena fights), head up.</summary>
        public static Transform Held(Transform robot, int level)
        {
            var held = new GameObject("ArenaHammer").transform;
            held.SetParent(robot, false);
            held.localPosition = new Vector3(0.24f, 0.18f, 0.06f);
            held.localRotation = Quaternion.Euler(20f, 0f, -15f);
            Build(held, level, 0.75f);
            return held;
        }
    }
}
