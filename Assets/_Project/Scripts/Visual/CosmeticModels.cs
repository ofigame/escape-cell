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
                case "hat.rank2":
                {
                    // A soldier's round helmet with a rim and a badge.
                    Shapes.Rounded("Helmet", root, new Vector3(0f, 0.74f, -0.01f), new Vector3(0.5f, 0.16f, 0.46f), 0.08f, main);
                    Shapes.Rounded("Rim", root, new Vector3(0f, 0.68f, -0.01f), new Vector3(0.54f, 0.03f, 0.5f), 0.015f, main);
                    Shapes.Rounded("Badge", root, new Vector3(0f, 0.75f, 0.225f), new Vector3(0.06f, 0.06f, 0.015f), 0.02f, MaterialFactory.Create(new Color(0.85f, 0.7f, 0.35f), new Color(0.4f, 0.3f, 0.1f)));
                    break;
                }
                case "hat.rank5":
                case "hat.rank11":
                {
                    // An officer's peaked cap (a general's: white with gold braid and a bigger badge).
                    var gold = MaterialFactory.Create(new Color(1f, 0.8f, 0.3f), new Color(0.7f, 0.5f, 0.12f));
                    bool general = id == "hat.rank11";
                    Shapes.Rounded("Band", root, new Vector3(0f, 0.73f, 0f), new Vector3(0.44f, 0.07f, 0.42f), 0.03f, general ? dark : main);
                    Shapes.Rounded("Crown", root, new Vector3(0f, 0.8f, -0.01f), new Vector3(0.5f, 0.07f, 0.48f), 0.035f, main);
                    Shapes.Rounded("Peak", root, new Vector3(0f, 0.7f, 0.24f), new Vector3(0.36f, 0.025f, 0.14f), 0.012f, dark);
                    Shapes.Rounded("Braid", root, new Vector3(0f, 0.735f, 0.215f), new Vector3(0.3f, 0.018f, 0.012f), 0.006f, gold);
                    Shapes.Rounded("Badge", root, new Vector3(0f, 0.775f, 0.218f), new Vector3(general ? 0.09f : 0.06f, general ? 0.07f : 0.05f, 0.015f), 0.02f, gold);
                    break;
                }
                case "hat.rank8":
                {
                    // A commander's beret, tilted, with a gold badge.
                    var beret = Shapes.Primitive(PrimitiveType.Sphere, "Beret", root, new Vector3(0.04f, 0.74f, 0f), new Vector3(0.52f, 0.12f, 0.48f), main).transform;
                    beret.localRotation = Quaternion.Euler(0f, 0f, -10f);
                    Shapes.Rounded("Badge", root, new Vector3(-0.12f, 0.73f, 0.2f), new Vector3(0.06f, 0.07f, 0.015f), 0.02f, MaterialFactory.Create(new Color(1f, 0.8f, 0.3f), new Color(0.7f, 0.5f, 0.12f)));
                    break;
                }
                case "hat.rank12":
                {
                    // The marshal's crown: a king's — a broad gold band set with gems, tall points with pearls, a velvet cap
                    // inside and a cross-less orb on top.
                    var gold = MaterialFactory.CreateSurface(new Color(1f, 0.8f, 0.3f), new Color(0.6f, 0.42f, 0.1f), 0.85f, 0.9f);
                    var velvet = MaterialFactory.Create(new Color(0.55f, 0.08f, 0.14f), new Color(0.08f, 0.01f, 0.02f));
                    var ruby = MaterialFactory.Create(new Color(1f, 0.15f, 0.25f), new Color(2f, 0.3f, 0.45f));
                    var pearl = MaterialFactory.Create(Color.white, new Color(0.5f, 0.5f, 0.55f));
                    Shapes.Primitive(PrimitiveType.Sphere, "Velvet", root, new Vector3(0f, 0.8f, 0f), new Vector3(0.3f, 0.2f, 0.28f), velvet);
                    Shapes.Rounded("Band", root, new Vector3(0f, 0.75f, 0f), new Vector3(0.36f, 0.08f, 0.34f), 0.03f, gold);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI * 2f / 8f;
                        var at = new Vector3(Mathf.Sin(ang) * 0.17f, 0.82f, Mathf.Cos(ang) * 0.16f);
                        Shapes.Rounded("Point", root, at, new Vector3(0.05f, 0.11f, 0.03f), 0.015f, gold).transform.localRotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
                        Shapes.Primitive(PrimitiveType.Sphere, "Pearl", root, at + Vector3.up * 0.065f, Vector3.one * 0.028f, pearl);
                        if (i % 2 == 0) Shapes.Primitive(PrimitiveType.Sphere, "Gem", root, new Vector3(Mathf.Sin(ang) * 0.18f, 0.75f, Mathf.Cos(ang) * 0.17f), new Vector3(0.035f, 0.035f, 0.02f), ruby);
                    }
                    Shapes.Primitive(PrimitiveType.Sphere, "Orb", root, new Vector3(0f, 0.93f, 0f), Vector3.one * 0.06f, gold);
                    break;
                }
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

        /// <summary>
        /// The capes, in their tiers. Plain ones (one colour, no pattern) and middle ones (a light pattern, the same in
        /// every colour) hang from a cord tied round the neck. The high ones are worn from the shoulders under a mantle,
        /// with gold patterns of their own. The ranks' capes, worn from the shoulders, carry an embossed beast: a bronze
        /// snake (staff sergeant), a silver eagle (first lieutenant), a gold wolf (lieutenant colonel) on green, and a gold
        /// lion on the marshal's claret cape — broader at the shoulders, long enough to trail on the ground, and coming
        /// down the chest in front as well. All wave (see <see cref="CapeWave"/>).
        /// </summary>
        private static void Cape(Transform root, string id, Color color)
        {
            int tier = id == "back.cape" || id == "back.cape.sky" || id == "back.cape.sun" ? 0
                : id == "back.cape.night" || id == "back.cape.teal" || id == "back.cape.wine" ? 1
                : id.StartsWith("back.cape.rank") ? 3 : 2;
            bool marshal = id == "back.cape.rank12";
            Color metal = id == "back.cape.rank3" ? new Color(0.78f, 0.48f, 0.22f) : id == "back.cape.rank6" ? new Color(0.88f, 0.9f, 0.97f) : new Color(1f, 0.8f, 0.3f);
            var cloth = MaterialFactory.Create(color, color * 0.05f);
            var lining = MaterialFactory.Create(Color.Lerp(color, Color.black, 0.45f), Color.black);
            var trim = MaterialFactory.Create(metal, metal * 0.35f);
            var emboss = MaterialFactory.CreateSurface(metal, metal * 0.3f, 0.8f, 0.85f);
            var light = MaterialFactory.Create(Color.Lerp(color, Color.white, 0.45f), Color.black);

            Transform top;
            float wTop, wBottom, h;
            int strips;
            if (tier <= 1)
            {
                // Tied at the neck: a cord round it, a knot in front, the cloth narrow at the top.
                var cord = MaterialFactory.Create(Color.Lerp(color, Color.black, 0.3f), Color.black);
                foreach (float s in new[] { -1f, 1f })
                {
                    var c = Shapes.Rounded("Cord", root, new Vector3(s * 0.075f, 0.385f, 0f), new Vector3(0.018f, 0.018f, 0.2f), 0.008f, cord).transform;
                    c.localRotation = Quaternion.Euler(0f, s * -22f, 0f);
                }
                Shapes.Primitive(PrimitiveType.Sphere, "Knot", root, new Vector3(0f, 0.38f, 0.095f), Vector3.one * 0.04f, cord);
                top = new GameObject("CapeTop").transform;
                top.SetParent(root, false);
                top.localPosition = new Vector3(0f, 0.385f, -0.1f);
                wTop = 0.2f; wBottom = 0.5f; h = 0.06f; strips = 6;
            }
            else
            {
                // From the shoulders: a mantle over them (the marshal's broader, with gold epaulettes).
                float mw = marshal ? 0.62f : 0.5f;
                Shapes.Rounded("Mantle", root, new Vector3(0f, marshal ? 0.37f : 0.36f, -0.06f), new Vector3(mw, marshal ? 0.09f : 0.07f, marshal ? 0.24f : 0.2f), 0.035f, cloth);
                Shapes.Rounded("MantleEdge", root, new Vector3(0f, 0.33f, -0.06f), new Vector3(mw + 0.02f, 0.02f, marshal ? 0.25f : 0.21f), 0.01f, trim);
                if (marshal)
                {
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var ep = Shapes.Primitive(PrimitiveType.Cylinder, "Epaulette", root, new Vector3(s * 0.24f, 0.395f, -0.02f), new Vector3(0.16f, 0.02f, 0.16f), trim).transform;
                        for (int k = 0; k < 5; k++)
                            Shapes.Primitive(PrimitiveType.Sphere, "Fringe", root, new Vector3(s * (0.19f + k * 0.025f), 0.37f, 0.02f + (k % 2) * 0.01f), new Vector3(0.018f, 0.04f, 0.018f), trim);
                        // Down the chest in front: a claret panel with a gold edge from each shoulder.
                        Shapes.Rounded("Front", root, new Vector3(s * 0.085f, 0.25f, 0.105f), new Vector3(0.075f, 0.24f, 0.016f), 0.01f, cloth);
                        Shapes.Rounded("FrontEdge", root, new Vector3(s * 0.05f, 0.25f, 0.114f), new Vector3(0.012f, 0.24f, 0.006f), 0.004f, trim);
                    }
                }
                foreach (float s in new[] { -1f, 1f })
                    Shapes.Primitive(PrimitiveType.Sphere, "Clasp", root, new Vector3(s * 0.12f, 0.35f, 0.09f), Vector3.one * 0.045f, trim);
                top = new GameObject("CapeTop").transform;
                top.SetParent(root, false);
                top.localPosition = new Vector3(0f, 0.36f, -0.15f);
                wTop = marshal ? 0.58f : 0.48f; wBottom = marshal ? 0.78f : 0.64f; h = 0.058f; strips = marshal ? 8 : 6;
            }

            var wave = top.gameObject.AddComponent<CapeWave>();
            Transform parent = top;
            for (int i = 0; i < strips; i++)
            {
                var seg = new GameObject("Seg" + i).transform;
                seg.SetParent(parent, false);
                seg.localPosition = i == 0 ? Vector3.zero : new Vector3(0f, -h, 0f);
                float w = Mathf.Lerp(wTop, wBottom, i / (strips - 1f));
                float y = -h * 0.5f;
                Shapes.Rounded("Cloth", seg, new Vector3(0f, y, 0f), new Vector3(w, h + 0.008f, 0.022f), 0.008f, cloth);
                Shapes.Rounded("Lining", seg, new Vector3(0f, y, 0.012f), new Vector3(w * 0.96f, h + 0.006f, 0.006f), 0.003f, lining);
                if (tier >= 2)
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Edge", seg, new Vector3(s * (w * 0.5f - 0.012f), y, -0.004f), new Vector3(0.022f, h + 0.008f, 0.026f), 0.006f, trim);
                if (tier == 1 && (i == 2 || i == 4))
                    // The middle capes' light pattern: a pale band across, a row of little dots.
                    Shapes.Rounded("Band", seg, new Vector3(0f, y, -0.013f), new Vector3(w * 0.9f, 0.012f, 0.004f), 0.003f, light);
                if (tier == 1 && i == 3)
                    for (int k = -2; k <= 2; k++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Dot", seg, new Vector3(k * w * 0.18f, y, -0.013f), new Vector3(0.022f, 0.022f, 0.006f), light);
                if (tier == 2 && i >= 1 && i <= 4) Pattern(seg, id, i, w, y, trim, trim);
                if (tier == 3 && i == 2) Beast(seg, id, y - 0.02f, emboss);
                if (i == strips - 1)
                {
                    Shapes.Rounded("Hem", seg, new Vector3(0f, -h - 0.006f, 0f), new Vector3(w + 0.012f, 0.024f, 0.03f), 0.01f, tier == 0 ? lining : tier == 1 ? light : trim);
                    if (marshal)
                    {
                        // The train: the last of the cape lying back along the ground.
                        var train = new GameObject("Train").transform;
                        train.SetParent(seg, false);
                        train.localPosition = new Vector3(0f, -h, 0f);
                        train.localRotation = Quaternion.Euler(78f, 0f, 0f);
                        Shapes.Rounded("Cloth", train, new Vector3(0f, -0.09f, 0f), new Vector3(w + 0.04f, 0.18f, 0.02f), 0.008f, cloth);
                        Shapes.Rounded("Hem", train, new Vector3(0f, -0.18f, 0f), new Vector3(w + 0.06f, 0.024f, 0.03f), 0.01f, trim);
                    }
                }
                wave.segments.Add(seg);
                parent = seg;
            }
        }

        /// <summary>An embossed beast on a rank cape's back: snake, eagle, wolf or lion, in the cape's metal.</summary>
        private static void Beast(Transform seg, string id, float y, Material m)
        {
            const float z = -0.02f;
            var c = new Vector3(0f, y, z);
            switch (id)
            {
                case "back.cape.rank3":
                    // A snake winding up in an S, its head at the top.
                    for (int i = 0; i < 8; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Coil", seg, c + new Vector3(Mathf.Sin(i * 0.95f) * 0.055f, -0.07f + i * 0.019f, 0f), new Vector3(0.034f, 0.034f, 0.014f), m);
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", seg, c + new Vector3(Mathf.Sin(8 * 0.95f) * 0.055f, 0.088f, 0f), new Vector3(0.05f, 0.04f, 0.016f), m);
                    break;
                case "back.cape.rank6":
                    // An eagle with its wings spread.
                    Shapes.Primitive(PrimitiveType.Sphere, "Body", seg, c, new Vector3(0.05f, 0.09f, 0.016f), m);
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", seg, c + new Vector3(0f, 0.065f, 0f), new Vector3(0.035f, 0.035f, 0.014f), m);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var wing = Shapes.Rounded("Wing", seg, c + new Vector3(s * 0.07f, 0.02f, 0f), new Vector3(0.11f, 0.035f, 0.01f), 0.01f, m).transform;
                        wing.localRotation = Quaternion.Euler(0f, 0f, s * 25f);
                        var tip = Shapes.Rounded("Feathers", seg, c + new Vector3(s * 0.12f, 0.005f, 0f), new Vector3(0.06f, 0.025f, 0.01f), 0.008f, m).transform;
                        tip.localRotation = Quaternion.Euler(0f, 0f, s * -10f);
                    }
                    Shapes.Rounded("Tail", seg, c + new Vector3(0f, -0.06f, 0f), new Vector3(0.04f, 0.035f, 0.01f), 0.008f, m);
                    break;
                case "back.cape.rank9":
                {
                    // A wolf's head: ears up, a long snout.
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", seg, c, new Vector3(0.09f, 0.08f, 0.018f), m);
                    Shapes.Rounded("Snout", seg, c + new Vector3(0f, -0.045f, -0.002f), new Vector3(0.04f, 0.05f, 0.016f), 0.012f, m);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var ear = Shapes.Rounded("Ear", seg, c + new Vector3(s * 0.03f, 0.05f, 0f), new Vector3(0.025f, 0.05f, 0.012f), 0.006f, m).transform;
                        ear.localRotation = Quaternion.Euler(0f, 0f, s * -18f);
                    }
                    break;
                }
                default:
                    // A lion's head in a full mane.
                    for (int i = 0; i < 12; i++)
                    {
                        float ang = i * Mathf.PI * 2f / 12f;
                        Shapes.Primitive(PrimitiveType.Sphere, "Mane", seg, c + new Vector3(Mathf.Cos(ang) * 0.06f, Mathf.Sin(ang) * 0.06f, 0.002f), new Vector3(0.04f, 0.04f, 0.014f), m);
                    }
                    Shapes.Primitive(PrimitiveType.Sphere, "Face", seg, c + new Vector3(0f, 0f, -0.004f), new Vector3(0.075f, 0.08f, 0.018f), m);
                    Shapes.Rounded("Nose", seg, c + new Vector3(0f, -0.018f, -0.012f), new Vector3(0.02f, 0.016f, 0.008f), 0.005f, m);
                    break;
            }
        }

        /// <summary>One strip's share of a high cape's gold pattern, on its outer face (-z).</summary>
        private static void Pattern(Transform seg, string id, int i, float w, float y, Material pattern, Material trim)
        {
            const float z = -0.014f;
            switch (id)
            {
                case "back.cape.flame":
                    // Gold tongues of flame rising from the hem.
                    if (i >= 3)
                        for (int k = 0; k < 4; k++)
                        {
                            var tongue = Shapes.Rounded("Flame", seg, new Vector3((k - 1.5f) * w * 0.22f, y, z), new Vector3(0.04f, 0.05f, 0.006f), 0.015f, pattern).transform;
                            tongue.localRotation = Quaternion.Euler(0f, 0f, 45f);
                        }
                    break;
                case "back.cape.frost":
                    // Gold snowflakes: three crossed bars.
                    if (i % 2 == 1)
                        for (int k = -1; k <= 1; k += 2)
                            for (int r = 0; r < 3; r++)
                                Shapes.Rounded("Flake", seg, new Vector3(k * w * 0.22f, y, z), new Vector3(0.008f, 0.05f, 0.006f), 0.003f, pattern)
                                    .transform.localRotation = Quaternion.Euler(0f, 0f, r * 60f);
                    break;
                default:
                    // Gold diamonds down the middle and a band of smaller ones across.
                    var diamond = Shapes.Rounded("Diamond", seg, new Vector3(0f, y, z), new Vector3(0.05f, 0.05f, 0.006f), 0.008f, pattern).transform;
                    diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    if (i == 2)
                        for (int k = -2; k <= 2; k++)
                            if (k != 0)
                                Shapes.Rounded("Dot", seg, new Vector3(k * w * 0.18f, y, z), new Vector3(0.025f, 0.025f, 0.006f), 0.006f, pattern)
                                    .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
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
                case "back.cape.sky":
                case "back.cape.sun":
                case "back.cape.teal":
                case "back.cape.wine":
                case "back.cape.night":
                case "back.cape.royal":
                case "back.cape.flame":
                case "back.cape.frost":
                case "back.cape.rank3":
                case "back.cape.rank6":
                case "back.cape.rank9":
                case "back.cape.rank12":
                    Cape(root, id, color);
                    break;
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
            // Big, thick arms, each one whole piece from the shoulder down, with its hand at the end.
            var main = MaterialFactory.Create(color, Color.black);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 0.2f;
                switch (id)
                {
                    case "arms.brush":
                    case "arms.lumi":
                    {
                        // A paint-brush arm; Lumi's own brush has a glowing gold tip.
                        bool lumi = id == "arms.lumi";
                        var wood = MaterialFactory.Create(new Color(0.75f, 0.55f, 0.35f), Color.black);
                        var tip = lumi ? MaterialFactory.Create(color, new Color(1.8f, 1.3f, 0.3f)) : main;
                        Shapes.Rounded("Arm", root, new Vector3(x, 0.25f, 0f), new Vector3(0.1f, 0.22f, 0.1f), 0.045f, wood);
                        Shapes.Rounded("Ferrule", root, new Vector3(x, 0.125f, 0f), new Vector3(0.11f, 0.04f, 0.11f), 0.015f,
                            MaterialFactory.Create(new Color(0.8f, 0.82f, 0.88f), new Color(0.2f, 0.2f, 0.22f)));
                        Shapes.Rounded("Bristles", root, new Vector3(x, 0.06f, 0f), new Vector3(0.11f, 0.1f, 0.11f), 0.045f, tip);
                        break;
                    }
                    case "arms.spring":
                    {
                        var coil = MaterialFactory.Create(color, new Color(0.1f, 0.1f, 0.12f));
                        Shapes.Primitive(PrimitiveType.Sphere, "Shoulder", root, new Vector3(x, 0.34f, 0f), Vector3.one * 0.11f, coil);
                        for (int k = 0; k < 5; k++)
                            Shapes.Primitive(PrimitiveType.Cylinder, "Coil", root, new Vector3(x, 0.3f - k * 0.04f, 0f), new Vector3(0.11f, 0.011f, 0.11f), coil);
                        Shapes.Primitive(PrimitiveType.Sphere, "Fist", root, new Vector3(x, 0.09f, 0f), Vector3.one * 0.12f,
                            MaterialFactory.Create(new Color(0.95f, 0.4f, 0.35f), Color.black));
                        break;
                    }
                    case "arms.claw":
                    {
                        Shapes.Rounded("Arm", root, new Vector3(x, 0.25f, 0f), new Vector3(0.11f, 0.22f, 0.115f), 0.05f, main);
                        foreach (float fz in new[] { -1f, 1f })
                        {
                            var finger = Shapes.Rounded("Pincer", root, new Vector3(x + fz * 0.032f, 0.1f, 0.012f), new Vector3(0.04f, 0.11f, 0.06f), 0.018f, main);
                            finger.transform.localRotation = Quaternion.Euler(0f, 0f, fz * 18f);
                        }
                        break;
                    }
                    case "arms.crystal":
                    {
                        var crystal = MaterialFactory.CreateTransparent(new Color(color.r, color.g, color.b, 0.85f), new Color(0.4f, 0.8f, 1.2f));
                        var arm = Shapes.Rounded("Crystal", root, new Vector3(x, 0.22f, 0f), new Vector3(0.11f, 0.26f, 0.11f), 0.03f, crystal);
                        arm.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                        break;
                    }
                }
            }
        }

        /// <summary>Leg pieces (they replace the plain legs and feet).</summary>
        public static void Legs(Transform root, string id, Color color)
        {
            // Big, sturdy legs: thick pieces standing wide, chunky feet.
            var main = MaterialFactory.Create(color, Color.black);
            var dark = MaterialFactory.Create(new Color(0.18f, 0.17f, 0.24f), Color.black);
            switch (id)
            {
                case "legs.spring":
                    foreach (float x in new[] { -0.09f, 0.09f })
                    {
                        for (int k = 0; k < 4; k++)
                            Shapes.Primitive(PrimitiveType.Cylinder, "Coil", root, new Vector3(x, 0.15f - k * 0.035f, 0f), new Vector3(0.12f, 0.01f, 0.12f), main);
                        Shapes.Rounded("Foot", root, new Vector3(x, 0.03f, 0.03f), new Vector3(0.14f, 0.06f, 0.2f), 0.025f, dark);
                    }
                    break;
                case "legs.wheel":
                {
                    // One big wheel under the body, like a unicycle.
                    Shapes.Rounded("Fork", root, new Vector3(0f, 0.13f, 0f), new Vector3(0.2f, 0.1f, 0.09f), 0.03f, main);
                    var wheel = Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", root, new Vector3(0f, 0.085f, 0f), new Vector3(0.17f, 0.045f, 0.17f), dark).transform;
                    wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Hub", root, new Vector3(0f, 0.085f, 0f), new Vector3(0.07f, 0.047f, 0.07f), main)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                }
                case "legs.tracks":
                    foreach (float x in new[] { -0.1f, 0.1f })
                    {
                        Shapes.Rounded("Track", root, new Vector3(x, 0.07f, 0f), new Vector3(0.12f, 0.14f, 0.3f), 0.05f, dark);
                        Shapes.Rounded("Wheel", root, new Vector3(x * 1.42f, 0.07f, 0f), new Vector3(0.012f, 0.08f, 0.2f), 0.006f, main);
                    }
                    break;
                case "legs.jet":
                {
                    var flame = MaterialFactory.Create(new Color(1f, 0.6f, 0.2f), new Color(2.6f, 1f, 0.2f));
                    foreach (float x in new[] { -0.09f, 0.09f })
                    {
                        Shapes.Rounded("Boot", root, new Vector3(x, 0.09f, 0f), new Vector3(0.13f, 0.17f, 0.15f), 0.05f, main);
                        Shapes.Rounded("Flame", root, new Vector3(x, 0.0f, 0f), new Vector3(0.07f, 0.05f, 0.07f), 0.025f, flame);
                    }
                    break;
                }
                case "legs.snow":
                    foreach (float x in new[] { -0.09f, 0.09f })
                    {
                        Shapes.Rounded("Leg", root, new Vector3(x, 0.095f, 0f), new Vector3(0.12f, 0.16f, 0.13f), 0.045f, main);
                        Shapes.Rounded("Shoe", root, new Vector3(x, 0.017f, 0.025f), new Vector3(0.17f, 0.03f, 0.3f), 0.014f,
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

    /// <summary>
    /// Waves a cape's strips: a gentle ripple running down it while foi stands, swept back and fluttering harder the
    /// faster foi moves (it watches its own movement in the world).
    /// </summary>
    public class CapeWave : MonoBehaviour
    {
        // (A plain public list: a copy of foi carries it over, pointing at its own strips.)
        public System.Collections.Generic.List<Transform> segments = new System.Collections.Generic.List<Transform>();
        private Vector3 last;
        private float speed, phase;

        private void OnEnable() => last = transform.position;

        private void LateUpdate()
        {
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            var p = transform.position;
            speed = Mathf.Lerp(speed, Mathf.Min(1f, (p - last).magnitude / dt / 5f), 1f - Mathf.Exp(-dt * 6f));
            last = p;
            phase += dt * (3.2f + speed * 7f);
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] == null) continue;
                float k = (i + 1f) / segments.Count;
                float sweep = i == 0 ? 10f + speed * 45f : speed * 6f; // swept back from the shoulders as foi runs
                float ripple = Mathf.Sin(phase - i * 0.8f) * (4f + speed * 10f) * k;
                float sway = Mathf.Sin(phase * 0.6f - i * 0.5f) * 3f * k;
                segments[i].localRotation = Quaternion.Euler(sweep + ripple, 0f, sway);
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
