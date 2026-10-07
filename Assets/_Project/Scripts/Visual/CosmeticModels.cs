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
                case "hat.antenna":
                    Shapes.Rounded("Stalk", root, new Vector3(0f, 0.8f, 0f), new Vector3(0.025f, 0.18f, 0.025f), 0.01f, dark);
                    Shapes.Primitive(PrimitiveType.Sphere, "Tip", root, new Vector3(0f, 0.9f, 0f), Vector3.one * 0.07f, MaterialFactory.Create(color, color * 0.8f));
                    break;
                case "hat.antenna2":
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var stalk = Shapes.Rounded("Stalk", root, new Vector3(side * 0.1f, 0.79f, 0f), new Vector3(0.022f, 0.17f, 0.022f), 0.01f, dark);
                        stalk.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 18f);
                        Shapes.Primitive(PrimitiveType.Sphere, "Tip", root, new Vector3(side * 0.135f, 0.88f, 0f), Vector3.one * 0.06f, MaterialFactory.Create(color, color * 0.8f));
                    }
                    break;
                case "hat.cable":
                {
                    // A tuft of loose cables from the grey channel, with copper ends.
                    var copper = MaterialFactory.Create(new Color(0.95f, 0.6f, 0.3f), new Color(0.4f, 0.2f, 0.05f));
                    for (int k = 0; k < 5; k++)
                    {
                        float a = (k - 2) * 22f;
                        var cable = Shapes.Rounded("Cable", root, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.1f, 0.78f, -0.04f + (k % 2) * 0.05f), new Vector3(0.03f, 0.16f, 0.03f), 0.012f, main);
                        cable.transform.localRotation = Quaternion.Euler(-15f, 0f, -a);
                        Shapes.Rounded("End", root, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.17f, 0.86f, -0.06f + (k % 2) * 0.05f), new Vector3(0.04f, 0.04f, 0.04f), 0.015f, copper);
                    }
                    break;
                }
                case "hat.leaf":
                {
                    var leaf = MaterialFactory.Create(color, color * 0.2f);
                    for (int k = 0; k < 3; k++)
                    {
                        var l = Shapes.Rounded("Leaf", root, new Vector3((k - 1) * 0.09f, 0.78f, -0.02f), new Vector3(0.1f, 0.03f, 0.2f), 0.015f, leaf);
                        l.transform.localRotation = Quaternion.Euler(-30f, (k - 1) * 35f, (k - 1) * -25f);
                    }
                    Shapes.Rounded("Stem", root, new Vector3(0f, 0.75f, 0f), new Vector3(0.025f, 0.08f, 0.025f), 0.01f, MaterialFactory.Create(new Color(0.45f, 0.3f, 0.2f), Color.black));
                    break;
                }
                case "hat.icecrown":
                {
                    var ice = MaterialFactory.CreateTransparent(new Color(color.r, color.g, color.b, 0.75f), new Color(0.3f, 0.6f, 0.9f));
                    for (int k = 0; k < 5; k++)
                    {
                        float a = (k - 2) * 0.13f;
                        float h = k == 2 ? 0.22f : k % 2 == 1 ? 0.16f : 0.11f;
                        var shard = Shapes.Rounded("Shard", root, new Vector3(a, 0.72f + h * 0.5f, 0.05f), new Vector3(0.06f, h, 0.06f), 0.02f, ice);
                        shard.transform.localRotation = Quaternion.Euler(0f, 45f, -a * 60f);
                    }
                    break;
                }
                case "hat.cloud":
                {
                    var puff = MaterialFactory.Create(color, new Color(0.3f, 0.3f, 0.35f));
                    Shapes.Primitive(PrimitiveType.Sphere, "Puff", root, new Vector3(0f, 0.76f, 0.05f), new Vector3(0.24f, 0.16f, 0.2f), puff);
                    Shapes.Primitive(PrimitiveType.Sphere, "Puff", root, new Vector3(-0.12f, 0.74f, 0.08f), new Vector3(0.16f, 0.12f, 0.14f), puff);
                    Shapes.Primitive(PrimitiveType.Sphere, "Puff", root, new Vector3(0.11f, 0.75f, 0.1f), new Vector3(0.15f, 0.12f, 0.14f), puff);
                    break;
                }
                case "hat.diver":
                {
                    var glass = MaterialFactory.CreateTransparent(new Color(0.8f, 0.95f, 1f, 0.35f), new Color(0.3f, 0.6f, 0.8f));
                    var brass = MaterialFactory.Create(new Color(0.95f, 0.72f, 0.3f), new Color(0.3f, 0.18f, 0.02f));
                    Shapes.Primitive(PrimitiveType.Sphere, "Dome", root, new Vector3(0f, 0.52f, 0f), new Vector3(0.64f, 0.6f, 0.62f), glass);
                    Shapes.Rounded("Collar", root, new Vector3(0f, 0.27f, 0f), new Vector3(0.5f, 0.06f, 0.48f), 0.03f, brass);
                    Shapes.Rounded("Valve", root, new Vector3(0.24f, 0.62f, 0.12f), new Vector3(0.06f, 0.06f, 0.06f), 0.02f, brass);
                    break;
                }
                case "hat.lollipop":
                {
                    var stick = MaterialFactory.Create(Color.white, Color.black);
                    var candy = MaterialFactory.Create(color, color * 0.4f);
                    var swirl = MaterialFactory.Create(new Color(1f, 0.95f, 0.6f), new Color(0.4f, 0.35f, 0.1f));
                    Shapes.Rounded("Stick", root, new Vector3(0.12f, 0.82f, 0f), new Vector3(0.03f, 0.24f, 0.03f), 0.012f, stick);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Candy", root, new Vector3(0.12f, 1f, 0f), new Vector3(0.24f, 0.03f, 0.24f), candy).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Swirl", root, new Vector3(0.12f, 1f, 0.02f), new Vector3(0.12f, 0.035f, 0.12f), swirl).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                }
                case "hat.visor":
                {
                    var holo = MaterialFactory.CreateTransparent(new Color(0.3f, 1f, 0.95f, 0.55f), new Color(0.4f, 2f, 1.9f));
                    var frame = MaterialFactory.Create(new Color(0.2f, 0.22f, 0.3f), Color.black);
                    Shapes.Rounded("Band", root, new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 0.06f, 0.48f), 0.02f, frame);
                    Shapes.Rounded("Holo", root, new Vector3(0f, 0.5f, 0.245f), new Vector3(0.42f, 0.14f, 0.02f), 0.02f, holo);
                    Shapes.Rounded("Fin", root, new Vector3(0f, 0.76f, -0.05f), new Vector3(0.04f, 0.12f, 0.3f), 0.02f, holo);
                    break;
                }
                case "hat.astro":
                {
                    var white = MaterialFactory.Create(color, Color.black);
                    var gold = MaterialFactory.CreateTransparent(new Color(1f, 0.8f, 0.4f, 0.7f), new Color(0.6f, 0.4f, 0.1f));
                    Shapes.Rounded("Helmet", root, new Vector3(0f, 0.52f, -0.02f), new Vector3(0.6f, 0.56f, 0.58f), 0.2f, white);
                    Shapes.Rounded("Glass", root, new Vector3(0f, 0.5f, 0.27f), new Vector3(0.4f, 0.28f, 0.04f), 0.08f, gold);
                    Shapes.Rounded("Light", root, new Vector3(0.22f, 0.76f, 0.1f), new Vector3(0.06f, 0.06f, 0.06f), 0.02f, MaterialFactory.Create(new Color(1f, 0.4f, 0.4f), new Color(2f, 0.4f, 0.4f)));
                    break;
                }
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
                case "back.rustbox":
                {
                    // Bip's old rusty box: the very bag the workshop lives in.
                    var rust = MaterialFactory.Create(color, Color.black);
                    var strap = MaterialFactory.Create(new Color(0.35f, 0.25f, 0.2f), Color.black);
                    Shapes.Rounded("Box", root, new Vector3(0f, 0.24f, -0.19f), new Vector3(0.24f, 0.22f, 0.12f), 0.03f, rust);
                    Shapes.Rounded("Lid", root, new Vector3(0f, 0.36f, -0.19f), new Vector3(0.26f, 0.04f, 0.14f), 0.015f, strap);
                    Shapes.Rounded("Patch", root, new Vector3(0.06f, 0.22f, -0.252f), new Vector3(0.07f, 0.06f, 0.01f), 0.01f, MaterialFactory.Create(new Color(0.55f, 0.75f, 0.6f), Color.black));
                    break;
                }
                case "back.leafbag":
                {
                    var leaf = MaterialFactory.Create(color, color * 0.15f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Bag", root, new Vector3(0f, 0.24f, -0.2f), new Vector3(0.24f, 0.26f, 0.14f), leaf);
                    var top = Shapes.Rounded("Leaf", root, new Vector3(0f, 0.38f, -0.2f), new Vector3(0.18f, 0.03f, 0.1f), 0.015f, leaf);
                    top.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
                    break;
                }
                case "back.shell":
                {
                    var shell = MaterialFactory.Create(color, color * 0.2f);
                    for (int k = 0; k < 5; k++)
                    {
                        var rib = Shapes.Rounded("Rib", root, new Vector3((k - 2) * 0.05f, 0.25f, -0.19f), new Vector3(0.05f, 0.26f, 0.05f), 0.02f, shell);
                        rib.transform.localRotation = Quaternion.Euler(0f, 0f, (k - 2) * 14f);
                    }
                    break;
                }
                case "back.balloon":
                {
                    var rope = MaterialFactory.Create(new Color(0.85f, 0.8f, 0.7f), Color.black);
                    Shapes.Rounded("Basket", root, new Vector3(0f, 0.2f, -0.2f), new Vector3(0.18f, 0.12f, 0.12f), 0.03f, MaterialFactory.Create(new Color(0.75f, 0.55f, 0.35f), Color.black));
                    Shapes.Rounded("Rope", root, new Vector3(0f, 0.48f, -0.22f), new Vector3(0.012f, 0.4f, 0.012f), 0.005f, rope);
                    Shapes.Primitive(PrimitiveType.Sphere, "Balloon", root, new Vector3(0f, 0.86f, -0.24f), new Vector3(0.3f, 0.34f, 0.3f), MaterialFactory.Create(color, color * 0.25f));
                    break;
                }
                case "back.rocket":
                {
                    var body = MaterialFactory.Create(color, Color.black);
                    var red = MaterialFactory.Create(new Color(0.9f, 0.3f, 0.3f), Color.black);
                    var flame = MaterialFactory.Create(new Color(1f, 0.6f, 0.2f), new Color(2.6f, 1f, 0.2f));
                    Shapes.Rounded("Body", root, new Vector3(0f, 0.28f, -0.2f), new Vector3(0.12f, 0.3f, 0.12f), 0.055f, body);
                    Shapes.Rounded("Nose", root, new Vector3(0f, 0.45f, -0.2f), new Vector3(0.08f, 0.07f, 0.08f), 0.035f, red);
                    foreach (float side in new[] { -1f, 1f })
                        Shapes.Rounded("Fin", root, new Vector3(side * 0.08f, 0.16f, -0.2f), new Vector3(0.05f, 0.09f, 0.02f), 0.01f, red);
                    Shapes.Rounded("Flame", root, new Vector3(0f, 0.09f, -0.2f), new Vector3(0.06f, 0.07f, 0.06f), 0.03f, flame);
                    break;
                }
                case "back.raven":
                {
                    // Kuzgun's cape: dark feathers with a silver clasp.
                    var cloth = MaterialFactory.Create(color, Color.black);
                    var silver = MaterialFactory.Create(new Color(0.8f, 0.82f, 0.9f), new Color(0.3f, 0.3f, 0.35f));
                    var cape = Shapes.Rounded("Cape", root, new Vector3(0f, 0.18f, -0.17f), new Vector3(0.4f, 0.38f, 0.03f), 0.015f, cloth);
                    cape.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var tip = Shapes.Rounded("Feather", root, new Vector3(side * 0.16f, 0.02f, -0.21f), new Vector3(0.08f, 0.1f, 0.02f), 0.01f, cloth);
                        tip.transform.localRotation = Quaternion.Euler(-14f, 0f, side * 20f);
                    }
                    Shapes.Rounded("Collar", root, new Vector3(0f, 0.31f, -0.1f), new Vector3(0.32f, 0.05f, 0.1f), 0.015f, cloth);
                    Shapes.Rounded("Clasp", root, new Vector3(0f, 0.31f, 0.12f), new Vector3(0.06f, 0.05f, 0.02f), 0.015f, silver);
                    break;
                }
                case "back.tank":
                {
                    var metal = MaterialFactory.Create(color, new Color(0.2f, 0.15f, 0.02f));
                    var dark = MaterialFactory.Create(new Color(0.25f, 0.25f, 0.32f), Color.black);
                    Shapes.Rounded("Tank", root, new Vector3(0f, 0.25f, -0.19f), new Vector3(0.18f, 0.28f, 0.14f), 0.07f, metal);
                    Shapes.Rounded("Cap", root, new Vector3(0f, 0.41f, -0.19f), new Vector3(0.08f, 0.05f, 0.08f), 0.02f, dark);
                    break;
                }
                case "back.cyberwings":
                {
                    var neon = MaterialFactory.Create(color, color * 1.6f);
                    foreach (float side in new[] { -1f, 1f })
                        for (int k = 0; k < 3; k++)
                        {
                            var blade = Shapes.Rounded("Blade", root, new Vector3(side * (0.17f + k * 0.06f), 0.36f - k * 0.05f, -0.17f), new Vector3(0.22f, 0.025f, 0.02f), 0.01f, neon);
                            blade.transform.localRotation = Quaternion.Euler(0f, side * 15f, side * (35f - k * 15f));
                        }
                    break;
                }
                case "back.halo":
                {
                    var glow = MaterialFactory.Create(color, color * 1.8f);
                    const int n = 14;
                    for (int k = 0; k < n; k++)
                    {
                        float a = k * Mathf.PI * 2f / n;
                        Shapes.Rounded("Halo", root, new Vector3(Mathf.Cos(a) * 0.16f, 0.88f, Mathf.Sin(a) * 0.16f), new Vector3(0.06f, 0.02f, 0.04f), 0.01f, glow)
                            .transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                    }
                    break;
                }
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
        /// <summary>Arm pieces (they replace the plain arms at x = ±0.18, y = 0.2).</summary>
        public static void Arms(Transform root, string id, Color color)
        {
            var main = MaterialFactory.Create(color, Color.black);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 0.18f;
                switch (id)
                {
                    case "arms.brush":
                    case "arms.lumi":
                    {
                        // A paint-brush hand; Lumi's own brush has a glowing gold tip.
                        bool lumi = id == "arms.lumi";
                        var wood = MaterialFactory.Create(new Color(0.75f, 0.55f, 0.35f), Color.black);
                        var tip = lumi ? MaterialFactory.Create(color, new Color(1.8f, 1.3f, 0.3f)) : main;
                        Shapes.Rounded("Arm", root, new Vector3(x, 0.22f, 0f), new Vector3(0.06f, 0.1f, 0.08f), 0.018f, wood);
                        Shapes.Rounded("Ferrule", root, new Vector3(x, 0.15f, 0f), new Vector3(0.07f, 0.03f, 0.07f), 0.01f,
                            MaterialFactory.Create(new Color(0.8f, 0.82f, 0.88f), new Color(0.2f, 0.2f, 0.22f)));
                        Shapes.Rounded("Bristles", root, new Vector3(x, 0.1f, 0f), new Vector3(0.07f, 0.08f, 0.07f), 0.03f, tip);
                        break;
                    }
                    case "arms.spring":
                    {
                        var coil = MaterialFactory.Create(color, new Color(0.1f, 0.1f, 0.12f));
                        for (int k = 0; k < 4; k++)
                            Shapes.Primitive(PrimitiveType.Cylinder, "Coil", root, new Vector3(x, 0.26f - k * 0.035f, 0f), new Vector3(0.07f, 0.008f, 0.07f), coil);
                        Shapes.Primitive(PrimitiveType.Sphere, "Fist", root, new Vector3(x, 0.12f, 0f), Vector3.one * 0.075f,
                            MaterialFactory.Create(new Color(0.95f, 0.4f, 0.35f), Color.black));
                        break;
                    }
                    case "arms.claw":
                    {
                        Shapes.Rounded("Arm", root, new Vector3(x, 0.22f, 0f), new Vector3(0.06f, 0.1f, 0.08f), 0.018f, main);
                        foreach (float f in new[] { -1f, 1f })
                        {
                            var finger = Shapes.Rounded("Pincer", root, new Vector3(x + f * 0.022f, 0.135f, 0.01f), new Vector3(0.025f, 0.07f, 0.04f), 0.01f, main);
                            finger.transform.localRotation = Quaternion.Euler(0f, 0f, f * 18f);
                        }
                        break;
                    }
                    case "arms.crystal":
                    {
                        var crystal = MaterialFactory.CreateTransparent(new Color(color.r, color.g, color.b, 0.8f), new Color(0.4f, 0.8f, 1.2f));
                        var arm = Shapes.Rounded("Crystal", root, new Vector3(x, 0.19f, 0f), new Vector3(0.07f, 0.16f, 0.07f), 0.02f, crystal);
                        arm.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                        break;
                    }
                }
            }
        }

        /// <summary>Leg pieces (they replace the plain legs and feet).</summary>
        public static void Legs(Transform root, string id, Color color)
        {
            var main = MaterialFactory.Create(color, Color.black);
            var dark = MaterialFactory.Create(new Color(0.18f, 0.17f, 0.24f), Color.black);
            switch (id)
            {
                case "legs.spring":
                    foreach (float x in new[] { -0.08f, 0.08f })
                    {
                        for (int k = 0; k < 3; k++)
                            Shapes.Primitive(PrimitiveType.Cylinder, "Coil", root, new Vector3(x, 0.12f - k * 0.035f, 0f), new Vector3(0.09f, 0.008f, 0.09f), main);
                        Shapes.Rounded("Foot", root, new Vector3(x, 0.02f, 0.03f), new Vector3(0.11f, 0.04f, 0.16f), 0.012f, dark);
                    }
                    break;
                case "legs.wheel":
                {
                    // One wheel under the body, like a unicycle.
                    Shapes.Rounded("Fork", root, new Vector3(0f, 0.1f, 0f), new Vector3(0.14f, 0.08f, 0.06f), 0.02f, main);
                    var wheel = Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", root, new Vector3(0f, 0.065f, 0f), new Vector3(0.13f, 0.03f, 0.13f), dark).transform;
                    wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Hub", root, new Vector3(0f, 0.065f, 0f), new Vector3(0.05f, 0.032f, 0.05f), main)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                }
                case "legs.tracks":
                    foreach (float x in new[] { -0.09f, 0.09f })
                    {
                        Shapes.Rounded("Track", root, new Vector3(x, 0.05f, 0f), new Vector3(0.09f, 0.1f, 0.24f), 0.04f, dark);
                        Shapes.Rounded("Wheel", root, new Vector3(x * 1.35f, 0.05f, 0f), new Vector3(0.01f, 0.06f, 0.16f), 0.005f, main);
                    }
                    break;
                case "legs.jet":
                {
                    var flame = MaterialFactory.Create(new Color(1f, 0.6f, 0.2f), new Color(2.6f, 1f, 0.2f));
                    foreach (float x in new[] { -0.08f, 0.08f })
                    {
                        Shapes.Rounded("Boot", root, new Vector3(x, 0.08f, 0f), new Vector3(0.1f, 0.13f, 0.12f), 0.035f, main);
                        Shapes.Rounded("Flame", root, new Vector3(x, 0.0f, 0f), new Vector3(0.05f, 0.04f, 0.05f), 0.02f, flame);
                    }
                    break;
                }
                case "legs.snow":
                    foreach (float x in new[] { -0.08f, 0.08f })
                    {
                        Shapes.Rounded("Leg", root, new Vector3(x, 0.08f, 0f), new Vector3(0.09f, 0.12f, 0.11f), 0.027f, main);
                        Shapes.Rounded("Shoe", root, new Vector3(x, 0.015f, 0.02f), new Vector3(0.14f, 0.025f, 0.26f), 0.012f,
                            MaterialFactory.Create(new Color(0.55f, 0.4f, 0.3f), Color.black));
                    }
                    break;
            }
        }

        /// <summary>Badges: the press-arm pin on the shoulder, Kuzgun's CL-1 plate on the chest, vanG's beating gold heart.</summary>
        public static Transform Badge(Transform root, string id, Color color)
        {
            switch (id)
            {
                case "badge.press":
                {
                    var pin = Shapes.Rounded("Pin", root, new Vector3(-0.18f, 0.29f, 0.04f), new Vector3(0.07f, 0.07f, 0.02f), 0.02f,
                        MaterialFactory.Create(color, new Color(0.2f, 0.2f, 0.24f)));
                    Shapes.Rounded("Bar", root, new Vector3(-0.18f, 0.29f, 0.052f), new Vector3(0.05f, 0.015f, 0.01f), 0.005f,
                        MaterialFactory.Create(new Color(0.95f, 0.75f, 0.3f), new Color(0.5f, 0.35f, 0.05f)));
                    return pin.transform;
                }
                case "badge.cl1":
                {
                    var plate = Shapes.Rounded("Plate", root, new Vector3(0f, 0.21f, 0.125f), new Vector3(0.14f, 0.06f, 0.015f), 0.01f,
                        MaterialFactory.Create(color, Color.black));
                    foreach (float x in new[] { -0.035f, 0f, 0.035f })
                        Shapes.Rounded("Mark", root, new Vector3(x, 0.21f, 0.134f), new Vector3(0.016f, 0.03f, 0.005f), 0.003f,
                            MaterialFactory.Create(new Color(0.9f, 0.9f, 1f), new Color(0.5f, 0.5f, 0.6f)));
                    return plate.transform;
                }
                case "badge.heart":
                {
                    var heart = new GameObject("Heart").transform;
                    heart.SetParent(root, false);
                    heart.localPosition = new Vector3(0f, 0.22f, 0.125f);
                    var gold = MaterialFactory.Create(color, new Color(2.4f, 1.6f, 0.4f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var lobe = Shapes.Rounded("Lobe", heart, new Vector3(side * 0.016f, 0.008f, 0f), new Vector3(0.04f, 0.055f, 0.012f), 0.012f, gold);
                        lobe.transform.localRotation = Quaternion.Euler(0f, 0f, side * 40f);
                    }
                    heart.gameObject.AddComponent<Pulse>();
                    return heart;
                }
            }
            return null;
        }

        /// <summary>
        /// Shaped eyes: children of the two eye pieces (so the blink still squashes them); the plain eye is hidden.
        /// </summary>
        public static void EyeShape(Transform eye, string id, Material glow)
        {
            var holder = new GameObject("Shape").transform;
            holder.SetParent(eye, false);
            holder.localScale = new Vector3(0.09f, 0.09f, 0.025f);
            switch (id)
            {
                case "eyes.happy":
                    // An upturned arch: ^
                    foreach (float side in new[] { -1f, 1f })
                        Shapes.Cube("Arch", holder, new Vector3(side * 0.2f, 0f, 0f), new Vector3(0.55f, 0.18f, 1f), glow)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, side * -35f);
                    break;
                case "eyes.sleepy":
                    Shapes.Cube("Lid", holder, new Vector3(0f, -0.15f, 0f), new Vector3(1f, 0.18f, 1f), glow);
                    break;
                case "eyes.determined":
                    Shapes.Cube("Eye", holder, new Vector3(0f, -0.08f, 0f), new Vector3(0.9f, 0.55f, 1f), glow);
                    break;
                case "eyes.heart":
                    foreach (float side in new[] { -1f, 1f })
                        Shapes.Cube("Lobe", holder, new Vector3(side * 0.18f, 0.05f, 0f), new Vector3(0.45f, 0.7f, 1f), glow)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, side * 40f);
                    break;
                case "eyes.star":
                    for (int k = 0; k < 3; k++)
                        Shapes.Cube("Ray", holder, Vector3.zero, new Vector3(0.22f, 1.1f, 1f), glow)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, k * 60f);
                    break;
            }
        }
    }

    /// <summary>A slow heartbeat: scales the object up and down.</summary>
    public class Pulse : MonoBehaviour
    {
        private void Update()
        {
            float t = Mathf.Repeat(Time.time, 1.1f);
            float beat = Mathf.Exp(-t * 9f) + 0.6f * Mathf.Exp(-Mathf.Abs(t - 0.25f) * 14f);
            transform.localScale = Vector3.one * (1f + beat * 0.25f);
        }
    }
}
