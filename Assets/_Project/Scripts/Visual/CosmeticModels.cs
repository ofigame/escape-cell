using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Garage hats and back gear, built from rounded boxes like the robot itself.
    /// Coordinates are in the robot's model space: head centre (0, 0.5, 0), top of the head y ≈ 0.71, face toward +z.
    /// </summary>
    public static class CosmeticModels
    {
        public static void Hat(Transform root, string id, Color color)
        {
            var main = MaterialFactory.Create(color, Color.black);
            var dark = MaterialFactory.Create(new Color(0.15f, 0.14f, 0.2f), Color.black);
            var shine = MaterialFactory.Create(Color.white, new Color(0.8f, 0.8f, 0.8f));
            switch (id)
            {
                case "hat.party":
                    Shapes.Rounded("Tier1", root, new Vector3(0f, 0.75f, 0f), new Vector3(0.24f, 0.08f, 0.24f), 0.03f, main);
                    Shapes.Rounded("Tier2", root, new Vector3(0f, 0.82f, 0f), new Vector3(0.17f, 0.08f, 0.17f), 0.03f, shine);
                    Shapes.Rounded("Tier3", root, new Vector3(0f, 0.89f, 0f), new Vector3(0.11f, 0.08f, 0.11f), 0.03f, main);
                    Shapes.Rounded("Pom", root, new Vector3(0f, 0.96f, 0f), new Vector3(0.08f, 0.08f, 0.08f), 0.04f,
                        MaterialFactory.Create(new Color(1f, 0.9f, 0.4f), new Color(1.4f, 1.1f, 0.3f)));
                    break;
                case "hat.cap":
                    Shapes.Rounded("Dome", root, new Vector3(0f, 0.74f, -0.01f), new Vector3(0.48f, 0.1f, 0.46f), 0.05f, main);
                    Shapes.Rounded("Brim", root, new Vector3(0f, 0.71f, 0.27f), new Vector3(0.38f, 0.03f, 0.16f), 0.015f, main);
                    Shapes.Rounded("Button", root, new Vector3(0f, 0.8f, -0.01f), new Vector3(0.06f, 0.03f, 0.06f), 0.015f, shine);
                    break;
                case "hat.phones":
                    Shapes.Rounded("Band", root, new Vector3(0f, 0.75f, 0f), new Vector3(0.54f, 0.05f, 0.08f), 0.02f, main);
                    foreach (float x in new[] { -0.26f, 0.26f })
                    {
                        Shapes.Rounded("Cup", root, new Vector3(x, 0.52f, 0f), new Vector3(0.08f, 0.2f, 0.18f), 0.04f, dark);
                        Shapes.Rounded("Glow", root, new Vector3(x * 1.12f, 0.52f, 0f), new Vector3(0.02f, 0.12f, 0.1f), 0.01f,
                            MaterialFactory.Create(new Color(0.5f, 1f, 1f), new Color(0.5f, 1.8f, 2.2f)));
                    }
                    break;
                case "hat.top":
                    Shapes.Rounded("Brim", root, new Vector3(0f, 0.725f, 0f), new Vector3(0.5f, 0.03f, 0.48f), 0.015f, main);
                    Shapes.Rounded("Crown", root, new Vector3(0f, 0.87f, 0f), new Vector3(0.3f, 0.27f, 0.3f), 0.03f, main);
                    Shapes.Rounded("Band", root, new Vector3(0f, 0.77f, 0f), new Vector3(0.31f, 0.05f, 0.31f), 0.015f,
                        MaterialFactory.Create(new Color(0.9f, 0.25f, 0.35f), Color.black));
                    break;
                case "hat.pirate":
                    Shapes.Rounded("Wide", root, new Vector3(0f, 0.74f, 0f), new Vector3(0.6f, 0.05f, 0.36f), 0.02f, main);
                    Shapes.Rounded("Top", root, new Vector3(0f, 0.81f, 0f), new Vector3(0.4f, 0.12f, 0.28f), 0.04f, main);
                    Shapes.Rounded("Skull", root, new Vector3(0f, 0.81f, 0.145f), new Vector3(0.08f, 0.07f, 0.02f), 0.02f, shine);
                    break;
                case "hat.crown":
                {
                    var gold = MaterialFactory.Create(color, color * 0.6f);
                    var gem = MaterialFactory.Create(new Color(1f, 0.3f, 0.45f), new Color(2f, 0.4f, 0.6f));
                    Shapes.Rounded("Ring", root, new Vector3(0f, 0.75f, 0f), new Vector3(0.42f, 0.08f, 0.4f), 0.02f, gold);
                    foreach (var p in new[] { new Vector2(-0.17f, -0.16f), new Vector2(0.17f, -0.16f), new Vector2(-0.17f, 0.16f), new Vector2(0.17f, 0.16f) })
                        Shapes.Rounded("Point", root, new Vector3(p.x, 0.83f, p.y), new Vector3(0.07f, 0.1f, 0.07f), 0.025f, gold);
                    Shapes.Rounded("Gem", root, new Vector3(0f, 0.76f, 0.205f), new Vector3(0.07f, 0.06f, 0.02f), 0.02f, gem);
                    break;
                }
            }
        }

        public static void Back(Transform root, string id, Color color)
        {
            switch (id)
            {
                case "back.cape":
                {
                    var cloth = MaterialFactory.Create(color, Color.black);
                    var cape = Shapes.Rounded("Cape", root, new Vector3(0f, 0.2f, -0.17f), new Vector3(0.36f, 0.34f, 0.03f), 0.015f, cloth);
                    cape.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                    Shapes.Rounded("Collar", root, new Vector3(0f, 0.31f, -0.1f), new Vector3(0.3f, 0.04f, 0.1f), 0.015f, cloth);
                    break;
                }
                case "back.jetpack":
                {
                    var metal = MaterialFactory.Create(color, Color.black);
                    var flame = MaterialFactory.Create(new Color(1f, 0.6f, 0.2f), new Color(2.6f, 1f, 0.2f));
                    foreach (float x in new[] { -0.07f, 0.07f })
                    {
                        Shapes.Rounded("Tank", root, new Vector3(x, 0.24f, -0.18f), new Vector3(0.1f, 0.22f, 0.1f), 0.045f, metal);
                        Shapes.Rounded("Flame", root, new Vector3(x, 0.1f, -0.18f), new Vector3(0.06f, 0.07f, 0.06f), 0.03f, flame);
                    }
                    break;
                }
                case "back.wings":
                {
                    var feather = MaterialFactory.Create(color, color * 0.5f);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var wing = Shapes.Rounded("Wing", root, new Vector3(side * 0.2f, 0.32f, -0.16f), new Vector3(0.3f, 0.14f, 0.02f), 0.06f, feather);
                        wing.transform.localRotation = Quaternion.Euler(0f, side * 20f, side * 25f);
                    }
                    break;
                }
            }
        }
    }
}
