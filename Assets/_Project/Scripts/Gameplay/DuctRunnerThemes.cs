using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The worlds a road (or a bonus run) passes through, in natural, restful colours: the plain duct first, then a
    /// forest trail, a red-rock canyon, the sea floor, a snowy mountain pass, a crystal cave, a bridge of clouds and,
    /// at the very end, a glass path through the stars. The running lane stays simple and readable in every one; the
    /// fun is around it (trees and fireflies, rock walls and dust, coral and bubbles, pines and snowfall...).
    /// Obstacles take the world's shape (logs, boulders, coral, snowballs, storm clouds, meteors), and the new kinds
    /// (rollers, walls, spinners) join as the levels go on.
    /// </summary>
    public partial class DuctRunner
    {
        private bool openTop;

        private bool IsThemed => kind != Kind.Duct && kind != Kind.Surf && kind != Kind.Mine;

        /// <summary>The world a road passes through for a level (0-based): simple ducts first, a new world every 30 levels or so.</summary>
        public static Kind RoadTheme(int level) =>
            level < 20 ? Kind.Duct : level < 50 ? Kind.Forest : level < 80 ? Kind.Canyon : level < 110 ? Kind.Ocean :
            level < 140 ? Kind.Snow : level < 170 ? Kind.Crystal : level < 210 ? Kind.Sky : Kind.Void;

        // Among the stars: lighter gravity and slower lane changes (less grip).
        private float GravityScale => kind == Kind.Void ? 0.7f : 1f;
        private float LaneScale => kind == Kind.Void ? 0.8f : 1f;

        // A burst of lens width on jump pads: the "warp" feeling.
        private float fovKick;
        // Tall, narrow phones (an iPhone is 19.5:9) would lose the sides of the course in a bend: the lens widens so the
        // view is always at least as wide as on a 16:9 screen.
        private float CameraFov => CameraFit.Fov(62f, rig.Cam.aspect) + fovKick * 18f;

        private Material themeA, themeB, themeC, themeD, glowMat, glassMat;
        private ParticleSystem speedLines, themeDust;
        private TrailRenderer trail;
        private Transform sky;

        private static Material M(Color c, Color e = default) => MaterialFactory.Create(c, e);

        private static Color H(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        private void ThemeMaterials()
        {
            switch (kind)
            {
                case Kind.Forest:
                    frameMat = M(H("#7E8A6A"), new Color(0.05f, 0.08f, 0.03f));
                    topMat = M(H("#B8AE8C"), new Color(0.06f, 0.05f, 0.03f));
                    slabMat = M(H("#5B4A36"));
                    themeA = M(H("#4E8A44"));                                   // leaves
                    themeB = M(H("#6FAE52"));                                   // light leaves
                    themeC = M(H("#6B4A30"));                                   // bark
                    themeD = M(H("#F2D27A"), new Color(0.6f, 0.45f, 0.1f));     // flowers
                    blockMat = M(H("#8A8478"));                                 // mossy rock
                    hurdleMat = themeC;
                    barMat = themeC;
                    break;
                case Kind.Canyon:
                    frameMat = M(H("#C9925E"), new Color(0.08f, 0.04f, 0.01f));
                    topMat = M(H("#E8C496"), new Color(0.08f, 0.05f, 0.02f));
                    slabMat = M(H("#A0603A"));
                    themeA = M(H("#B8603A"));                                   // rock
                    themeB = M(H("#D88A5A"));                                   // rock bands
                    themeC = M(H("#6E9A54"));                                   // cactus
                    themeD = M(H("#F4D9B4"));                                   // sand
                    blockMat = M(H("#A86A44"));
                    hurdleMat = themeA;
                    barMat = themeB;
                    break;
                case Kind.Ocean:
                    frameMat = M(H("#D8C49A"), new Color(0.04f, 0.06f, 0.08f));
                    topMat = M(H("#EEDDB8"), new Color(0.05f, 0.08f, 0.1f));
                    slabMat = M(H("#7A8A90"));
                    themeA = M(H("#E88A7A"));                                   // coral
                    themeB = M(H("#F2B26A"));                                   // coral
                    themeC = M(H("#4E9A7A"));                                   // seaweed
                    themeD = M(H("#6E8A9A"));                                   // rock
                    glowMat = MaterialFactory.CreateTransparent(new Color(0.7f, 0.9f, 1f, 0.12f), new Color(0.3f, 0.5f, 0.6f));
                    blockMat = M(H("#5A6670"));
                    hurdleMat = themeA;
                    barMat = M(H("#C8B48A"));                                   // driftwood
                    break;
                case Kind.Snow:
                    frameMat = M(H("#C8D8E8"), new Color(0.1f, 0.14f, 0.2f));
                    topMat = M(H("#F4F8FC"), new Color(0.12f, 0.14f, 0.18f));
                    slabMat = M(H("#8A9AB0"));
                    themeA = M(H("#3E6A52"));                                   // pine
                    themeB = M(H("#FFFFFF"), new Color(0.2f, 0.22f, 0.26f));    // snow
                    themeC = M(H("#5A4636"));                                   // trunk
                    themeD = M(H("#BFE0F4"), new Color(0.3f, 0.5f, 0.7f));      // ice
                    glowMat = M(H("#FFE8B0"), new Color(1.6f, 1.2f, 0.5f));
                    blockMat = themeD;
                    hurdleMat = themeB;
                    barMat = themeD;
                    break;
                case Kind.Sky:
                    frameMat = M(H("#E8EEF8"), new Color(0.15f, 0.17f, 0.22f));
                    topMat = M(H("#FFFFFF"), new Color(0.16f, 0.17f, 0.2f));
                    slabMat = M(H("#B8C6DC"));
                    themeA = MaterialFactory.CreateTransparent(new Color(1f, 1f, 1f, 0.75f), new Color(0.3f, 0.32f, 0.38f)); // clouds
                    themeB = M(H("#F2B8C8"));                                   // balloons
                    themeC = M(H("#A8D0F0"));
                    themeD = M(H("#FFE6A0"));
                    blockMat = M(H("#8A90A8"), new Color(0.05f, 0.05f, 0.1f));  // storm cloud
                    hurdleMat = themeA;
                    barMat = M(H("#E8D0F0"));
                    break;
                case Kind.Crystal:
                    frameMat = M(H("#A89AC0"), new Color(0.1f, 0.06f, 0.14f));
                    topMat = M(H("#D8CCE4"), new Color(0.1f, 0.06f, 0.14f));
                    slabMat = M(H("#5E5068"));
                    themeA = M(H("#6E6078"));                                   // rock
                    themeB = M(H("#C8A8E8"), new Color(0.7f, 0.4f, 1.0f));      // crystals, softly lit
                    themeC = M(H("#A8D8E8"), new Color(0.35f, 0.75f, 0.95f));
                    themeD = M(H("#E8B8D8"), new Color(0.9f, 0.4f, 0.7f));
                    blockMat = M(H("#7A6E82"));
                    hurdleMat = themeB;
                    barMat = themeA;
                    break;
                case Kind.Void:
                    glassMat = MaterialFactory.CreateTransparent(new Color(0.85f, 0.9f, 1f, 0.35f), new Color(0.25f, 0.3f, 0.45f));
                    frameMat = M(H("#C8D0F0"), new Color(0.3f, 0.35f, 0.6f));
                    topMat = MaterialFactory.CreateTransparent(new Color(0.75f, 0.8f, 1f, 0.45f), new Color(0.15f, 0.18f, 0.3f));
                    slabMat = MaterialFactory.CreateTransparent(new Color(0.6f, 0.65f, 0.85f, 0.15f), Color.black);
                    themeA = M(H("#E0E4FF"), new Color(0.7f, 0.75f, 1.1f));     // star lamps
                    blockMat = M(H("#7A6E6A"), new Color(0.2f, 0.08f, 0.03f));  // meteors
                    hurdleMat = frameMat;
                    barMat = M(H("#D8C8F0"), new Color(0.5f, 0.4f, 0.8f));
                    break;
            }
        }

        // ---------- Rows ----------

        private void BuildThemeRow(Transform root, int r)
        {
            // A road leaving a platform starts as an open bridge: while the robot still walks to the exit, the camera looks
            // down on it from above, and tall things right at the edge would cover the floor.
            openTop = roadMode && r < 8;
            var rng = new System.Random(r * 7717 + (int)kind * 31 + 3);
            switch (kind)
            {
                case Kind.Forest: ForestRow(root, r, rng); break;
                case Kind.Canyon: CanyonRow(root, r, rng); break;
                case Kind.Ocean: OceanRow(root, r, rng); break;
                case Kind.Snow: SnowRow(root, r, rng); break;
                case Kind.Sky: SkyRow(root, r, rng); break;
                case Kind.Crystal: CrystalRow(root, r, rng); break;
                case Kind.Void: VoidRow(root, r); break;
            }
        }

        private static float R(System.Random rng) => (float)rng.NextDouble();

        /// <summary>A forest trail: grass verges, trees and bushes a little way off, flowers and the odd mushroom.</summary>
        private void ForestRow(Transform root, int r, System.Random rng)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Shapes.Rounded("Grass", root, new Vector3(side * (WallX + 0.2f), -0.05f, 0f), new Vector3(0.9f, 0.22f, 1.1f), 0.1f, themeB);
                if (r % 2 == 0 && R(rng) < 0.5f)
                    Shapes.Primitive(PrimitiveType.Sphere, "Flower", root, new Vector3(side * (WallX + 0.1f + R(rng) * 0.4f), 0.12f, R(rng) - 0.5f), Vector3.one * 0.12f, themeD);
                if (openTop) continue;
                if ((r + (side > 0 ? 3 : 0)) % 5 == 0)
                {
                    // A tree: a trunk and a cluster of round leafy blobs.
                    float d = WallX + 1.4f + R(rng) * 1.2f, h = 1.6f + R(rng) * 1.2f;
                    Shapes.Rounded("Trunk", root, new Vector3(side * d, h * 0.5f, 0f), new Vector3(0.28f, h, 0.28f), 0.1f, themeC);
                    for (int i = 0; i < 3; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Leaves", root, new Vector3(side * d + (R(rng) - 0.5f) * 0.7f, h + 0.2f + R(rng) * 0.5f, (R(rng) - 0.5f) * 0.7f),
                            Vector3.one * (1.0f + R(rng) * 0.6f), i == 0 ? themeA : themeB);
                }
                else if (R(rng) < 0.3f)
                    Shapes.Primitive(PrimitiveType.Sphere, "Bush", root, new Vector3(side * (WallX + 0.9f + R(rng)), 0.25f, 0f), new Vector3(0.9f, 0.6f, 0.8f), themeA);
                if (R(rng) < 0.06f)
                {
                    Shapes.Rounded("Stem", root, new Vector3(side * (WallX + 0.5f), 0.15f, 0.2f), new Vector3(0.08f, 0.3f, 0.08f), 0.03f, themeD);
                    Shapes.Primitive(PrimitiveType.Sphere, "Cap", root, new Vector3(side * (WallX + 0.5f), 0.32f, 0.2f), new Vector3(0.3f, 0.16f, 0.3f), themeA);
                }
            }
        }

        /// <summary>A red-rock canyon: layered sandstone walls, cacti and loose rocks.</summary>
        private void CanyonRow(Transform root, int r, System.Random rng)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Shapes.Rounded("Sand", root, new Vector3(side * (WallX + 0.3f), -0.08f, 0f), new Vector3(1.0f, 0.2f, 1.1f), 0.08f, themeD);
                if (openTop) continue;
                // The canyon walls rise in layered bands further out.
                float h = 2.4f + Mathf.PerlinNoise(r * 0.15f, side * 3f) * 2.4f;
                Shapes.Rounded("Wall", root, new Vector3(side * (WallX + 1.9f), h * 0.5f - 0.2f, 0f), new Vector3(1.6f, h, 1.12f), 0.3f, themeA);
                Shapes.Rounded("Band", root, new Vector3(side * (WallX + 1.12f), h * 0.45f, 0f), new Vector3(0.08f, 0.3f, 1.13f), 0.04f, themeB);
                if (R(rng) < 0.12f)
                {
                    // A cactus with an arm.
                    float cx = side * (WallX + 0.6f);
                    Shapes.Rounded("Cactus", root, new Vector3(cx, 0.55f, 0f), new Vector3(0.2f, 1.1f, 0.2f), 0.09f, themeC);
                    Shapes.Rounded("Arm", root, new Vector3(cx + 0.15f, 0.65f, 0f), new Vector3(0.2f, 0.12f, 0.12f), 0.05f, themeC);
                    Shapes.Rounded("Arm", root, new Vector3(cx + 0.22f, 0.8f, 0f), new Vector3(0.12f, 0.35f, 0.12f), 0.05f, themeC);
                }
                else if (R(rng) < 0.2f)
                    Shapes.Rounded("Rock", root, new Vector3(side * (WallX + 0.7f), 0.15f, R(rng) - 0.5f), new Vector3(0.4f, 0.3f, 0.35f), 0.12f, themeB);
            }
        }

        /// <summary>The sea floor: sand banks, coral, swaying seaweed and slanting shafts of light.</summary>
        private void OceanRow(Transform root, int r, System.Random rng)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Shapes.Rounded("Bank", root, new Vector3(side * (WallX + 0.4f), -0.1f, 0f), new Vector3(1.2f, 0.24f, 1.1f), 0.1f, topMat);
                if (openTop) continue;
                if (R(rng) < 0.35f)
                {
                    // A coral: a few branching nubs.
                    float cx = side * (WallX + 0.5f + R(rng) * 0.8f);
                    var mat = R(rng) < 0.5f ? themeA : themeB;
                    for (int i = 0; i < 3; i++)
                        Shapes.Rounded("Coral", root, new Vector3(cx + (R(rng) - 0.5f) * 0.3f, 0.25f + i * 0.12f, (R(rng) - 0.5f) * 0.3f), new Vector3(0.12f, 0.4f + R(rng) * 0.3f, 0.12f), 0.05f, mat)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, (R(rng) - 0.5f) * 50f);
                }
                if (R(rng) < 0.3f)
                    for (int i = 0; i < 2; i++)
                        Shapes.Rounded("Weed", root, new Vector3(side * (WallX + 1.3f + R(rng)), 0.6f, (R(rng) - 0.5f) * 0.6f), new Vector3(0.08f, 1.2f + R(rng) * 0.8f, 0.04f), 0.03f, themeC)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, (R(rng) - 0.5f) * 25f);
                if (r % 7 == 0 && side < 0)
                    Shapes.Rounded("Rock", root, new Vector3(side * (WallX + 2.2f), 0.5f, 0f), new Vector3(1.2f, 1.1f, 1f), 0.4f, themeD);
            }
            if (r % 11 == 0 && !openTop)
            {
                // A shaft of sunlight from far above.
                var ray = Shapes.Rounded("Light", root, new Vector3((R(rng) - 0.5f) * 3f, 3.2f, 0f), new Vector3(0.6f, 6f, 0.6f), 0.2f, glowMat).transform;
                ray.localRotation = Quaternion.Euler(0f, 0f, 14f);
            }
        }

        /// <summary>A mountain pass: snowbanks, pines with snowy tiers, lanterns now and then.</summary>
        private void SnowRow(Transform root, int r, System.Random rng)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Shapes.Rounded("Bank", root, new Vector3(side * (WallX + 0.35f), 0.02f, 0f), new Vector3(1.0f, 0.35f + R(rng) * 0.15f, 1.12f), 0.16f, themeB);
                if (openTop) continue;
                if ((r + (side > 0 ? 2 : 0)) % 4 == 0)
                {
                    // A pine: three tiers, snow on each.
                    float px = side * (WallX + 1.3f + R(rng) * 1.4f), size = 0.8f + R(rng) * 0.5f;
                    Shapes.Rounded("Trunk", root, new Vector3(px, 0.3f, 0f), new Vector3(0.18f, 0.6f, 0.18f), 0.05f, themeC);
                    for (int i = 0; i < 3; i++)
                    {
                        float w = size * (1.2f - i * 0.3f), y0 = 0.6f + i * 0.55f * size;
                        Shapes.Rounded("Tier", root, new Vector3(px, y0, 0f), new Vector3(w, 0.5f * size, w), 0.2f, themeA).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                        Shapes.Rounded("Snow", root, new Vector3(px, y0 + 0.24f * size, 0f), new Vector3(w * 0.7f, 0.08f, w * 0.7f), 0.04f, themeB).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    }
                }
                if (r % 14 == 0)
                {
                    Shapes.Rounded("Post", root, new Vector3(side * (WallX + 0.25f), 0.6f, 0f), new Vector3(0.08f, 1.2f, 0.08f), 0.03f, themeC);
                    Shapes.Primitive(PrimitiveType.Sphere, "Lantern", root, new Vector3(side * (WallX + 0.25f), 1.25f, 0f), Vector3.one * 0.2f, glowMat);
                }
            }
        }

        /// <summary>A bridge in the sky: soft clouds drifting alongside and the odd bunch of pastel balloons.</summary>
        private void SkyRow(Transform root, int r, System.Random rng)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Shapes.Rounded("Rail", root, new Vector3(side * (WallX - 0.05f), 0.25f, 0f), new Vector3(0.08f, 0.08f, 1.08f), 0.03f, frameMat);
                if (r % 2 == 0) Shapes.Rounded("Post", root, new Vector3(side * (WallX - 0.05f), 0.12f, 0f), new Vector3(0.08f, 0.26f, 0.08f), 0.03f, frameMat);
                if (openTop) continue;
                if (R(rng) < 0.4f)
                    Shapes.Primitive(PrimitiveType.Sphere, "Cloud", root, new Vector3(side * (WallX + 1.4f + R(rng) * 2.5f), -0.6f + R(rng) * 2.2f, R(rng) - 0.5f),
                        new Vector3(1.4f + R(rng), 0.7f + R(rng) * 0.4f, 1f), themeA);
                if (r % 13 == 0 && side > 0)
                    for (int i = 0; i < 3; i++)
                    {
                        var at = new Vector3(side * (WallX + 0.9f) + i * 0.25f, 1.6f + i * 0.2f, 0f);
                        Shapes.Primitive(PrimitiveType.Sphere, "Balloon", root, at, new Vector3(0.36f, 0.44f, 0.36f), i == 0 ? themeB : i == 1 ? themeC : themeD);
                        Shapes.Rounded("String", root, at + new Vector3(0f, -0.5f, 0f), new Vector3(0.02f, 0.6f, 0.02f), 0.01f, frameMat);
                    }
            }
        }

        /// <summary>A cave floor: low rock walls a little way off and softly glowing crystals leaning in from the sides (nothing overhead).</summary>
        private void CrystalRow(Transform root, int r, System.Random rng)
        {
            var crystals = new[] { themeB, themeC, themeD };
            foreach (float side in new[] { -1f, 1f })
            {
                var rock = Shapes.Rounded("Rock", root, new Vector3(side * (WallX + 0.9f), 0.5f + R(rng) * 0.3f, 0f),
                    new Vector3(0.9f, 1.2f + R(rng) * 0.8f, 1.3f), 0.35f, themeA).transform;
                rock.localRotation = Quaternion.Euler(R(rng) * 20f, R(rng) * 30f, side * (8f + R(rng) * 10f));
                if (rng.Next(3) == 0)
                {
                    int n = 2 + rng.Next(2);
                    for (int i = 0; i < n; i++)
                    {
                        float h = 0.8f + R(rng) * 1.6f;
                        var c = Shapes.Rounded("Crystal", root, new Vector3(side * (WallX - 0.1f), h * 0.5f + 0.1f, (R(rng) - 0.5f) * 0.6f),
                            new Vector3(0.22f, h, 0.22f), 0.05f, crystals[rng.Next(crystals.Length)]).transform;
                        c.localRotation = Quaternion.Euler((R(rng) - 0.5f) * 30f, rng.Next(90), -side * (15f + R(rng) * 25f));
                    }
                }
            }
        }

        /// <summary>A glass path among the stars: a slim rail with little star lamps.</summary>
        private void VoidRow(Transform root, int r)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Shapes.Rounded("Rail", root, new Vector3(side * (WallX - 0.05f), 0.2f, 0f), new Vector3(0.05f, 0.05f, 1.06f), 0.02f, glassMat);
                if (r % 4 == 0) Shapes.Primitive(PrimitiveType.Sphere, "Lamp", root, new Vector3(side * (WallX - 0.05f), 0.3f, 0f), Vector3.one * 0.12f, themeA);
            }
        }

        // ---------- Obstacles ----------

        /// <summary>Gives obstacles the world's look; false = use the standard look.</summary>
        private bool ThemedObstacle(Obstacle o, Transform go, ObstacleKind k, int l, int r, float w)
        {
            switch (k)
            {
                case ObstacleKind.Hurdle:
                    switch (kind)
                    {
                        case Kind.Forest:
                        case Kind.Ocean:
                            // A fallen log (driftwood on the sea floor) across every lane.
                            Shapes.Rounded("Log", go, new Vector3(0f, 0.25f, 0f), new Vector3(w + 0.4f, 0.4f, 0.4f), 0.19f, kind == Kind.Ocean ? barMat : themeC);
                            if (kind == Kind.Forest) Shapes.Primitive(PrimitiveType.Sphere, "Moss", go, new Vector3(0.6f, 0.42f, 0f), new Vector3(0.5f, 0.15f, 0.35f), themeB);
                            return true;
                        case Kind.Canyon:
                        case Kind.Snow:
                        case Kind.Sky:
                            // A low ridge of rock, snow or cloud.
                            for (int i = 0; i < 4; i++)
                                Shapes.Primitive(PrimitiveType.Sphere, "Ridge", go, new Vector3(-w * 0.4f + i * w * 0.27f, 0.2f, 0f), new Vector3(1.1f, 0.45f, 0.5f), hurdleMat);
                            return true;
                    }
                    return false;
                case ObstacleKind.Bar:
                    if (kind == Kind.Void) return false;
                    // A beam at head height between two posts in the world's material: slide under it.
                    Shapes.Rounded("Beam", go, new Vector3(0f, 1.1f, 0f), new Vector3(w + 0.5f, 0.42f, 0.3f), 0.15f, barMat);
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Post", go, new Vector3(s * (w * 0.5f + 0.25f), 0.6f, 0f), new Vector3(0.22f, 1.25f, 0.22f), 0.08f, slabMat);
                    return true;
                case ObstacleKind.Drop when kind == Kind.Void:
                {
                    // Meteors drifting in the lanes instead of falling blocks.
                    var rock = Shapes.Rounded("Meteor", go, new Vector3(0f, -0.05f, 0f), new Vector3(0.85f, 0.75f, 0.8f), 0.3f, blockMat).transform;
                    rock.localRotation = Quaternion.Euler(r * 37f, r * 61f, r * 13f);
                    o.y = 0f;
                    o.landed = true;
                    return true;
                }
                case ObstacleKind.Roller:
                {
                    // What rolls: a log in the forest, a snowball in the snow, a boulder anywhere else.
                    if (kind == Kind.Forest)
                        Shapes.Primitive(PrimitiveType.Cylinder, "Log", go, new Vector3(0f, 0.42f, 0f), new Vector3(0.8f, 0.42f, 0.8f), themeC).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    else Shapes.Primitive(PrimitiveType.Sphere, "Ball", go, new Vector3(0f, 0.42f, 0f), Vector3.one * 0.84f, kind == Kind.Snow ? themeB : blockMat);
                    o.zf = r;
                    return true;
                }
                case ObstacleKind.Wall:
                {
                    o.openLane = l;
                    var mat = kind == Kind.Snow || kind == Kind.Ocean ? themeD : kind == Kind.Sky || kind == Kind.Forest ? themeA : slabMat;
                    for (int wl = 0; wl < Lanes; wl++)
                        if (wl != l) Shapes.Rounded("Wall", go, new Vector3(LaneX(wl), WallHeight * 0.5f, 0f), new Vector3(LaneWidth * 0.98f, WallHeight, 0.5f), 0.2f, mat);
                    return true;
                }
            }
            return false;
        }

        // ---------- Effects ----------

        private void StartThemeFx()
        {
            fovKick = 0f;
            // Speed lines streaming past the camera: a soft hint of speed, more in the open sky.
            var lineColor = new Color(0.9f, 0.95f, 1f) * (kind == Kind.Void ? 1.2f : 0.6f);
            speedLines = Stream("SpeedLines", Weather.AddMaterial, lineColor, kind == Kind.Sky || kind == Kind.Void ? 30f : 14f, 0.035f, new Vector3(5f, 3.2f, 1f));
            var r = speedLines.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.12f;

            // Something in the air for every world.
            switch (kind)
            {
                case Kind.Forest: themeDust = Ambient("Fireflies", Weather.AddMaterial, new Color(1.4f, 1.3f, 0.5f), 18f, 0.07f, -0.2f); break;
                case Kind.Canyon: themeDust = Ambient("Dust", Weather.AlphaMaterial, new Color(0.95f, 0.8f, 0.6f, 0.35f), 25f, 0.12f, -0.1f); break;
                case Kind.Ocean: themeDust = Ambient("Bubbles", Weather.AlphaMaterial, new Color(0.85f, 0.95f, 1f, 0.5f), 22f, 0.09f, 1.1f); break;
                case Kind.Snow: themeDust = Ambient("Snow", Weather.AlphaMaterial, new Color(1f, 1f, 1f, 0.85f), 45f, 0.07f, -1.2f); break;
                case Kind.Sky: themeDust = Ambient("Sparkles", Weather.AddMaterial, new Color(1.2f, 1.15f, 0.9f), 10f, 0.05f, 0.1f); break;
                case Kind.Crystal: themeDust = Ambient("Glints", Weather.AddMaterial, new Color(1.0f, 0.8f, 1.3f), 16f, 0.07f, 0f); break;
                case Kind.Void:
                    themeDust = Stream("StarDust", Weather.AddMaterial, new Color(0.8f, 0.85f, 1.2f), 30f, 0.06f, new Vector3(14f, 10f, 1f), 0.5f);
                    BuildSky();
                    break;
            }
            // A soft trail behind the robot: the garage's trail colour, or white.
            var trailColor = robot.TrailColor ?? new Color(0.9f, 0.95f, 1f);
            // Reuse the trail when the last run's one is still waiting for its deferred Destroy (a road run straight into a tunnel).
            trail = robot.GetComponent<TrailRenderer>();
            if (trail == null) trail = robot.gameObject.AddComponent<TrailRenderer>();
            trail.Clear();
            trail.enabled = true;
            trail.sharedMaterial = Weather.AddMaterial;
            trail.time = 0.3f;
            trail.minVertexDistance = 0.08f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(trailColor * 1.2f, 0f), new GradientColorKey(trailColor, 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Particles born ahead of the camera and flying back past it, so they always stream at the player.</summary>
        private ParticleSystem Stream(string name, Material mat, Color color, float rate, float size, Vector3 box, float speedFactor = 1f)
        {
            var ps = MakeParticles(name, mat, color, rate, size, box, 600);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-22f * speedFactor, -30f * speedFactor);
            ps.Play();
            return ps;
        }

        /// <summary>Gentle floating particles around the view (fireflies, dust, bubbles, snow): rising or falling slowly.</summary>
        private ParticleSystem Ambient(string name, Material mat, Color color, float rate, float size, float rise)
        {
            var ps = MakeParticles(name, mat, color, rate, size, new Vector3(9f, 6f, 14f), 400);
            ps.transform.localPosition = new Vector3(0f, 0f, 8f);
            var main = ps.main;
            main.startLifetime = 3.5f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            vel.y = new ParticleSystem.MinMaxCurve(rise - 0.2f, rise + 0.2f);
            vel.z = new ParticleSystem.MinMaxCurve(-6f, -4f);
            ps.Play();
            return ps;
        }

        private ParticleSystem MakeParticles(string name, Material mat, Color color, float rate, float size, Vector3 box, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig.Cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 16f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.rateOverTime = rate * Weather.Density;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.7f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>The starry sky: a soft spiral galaxy far below and ahead, and a few planets, kept at a fixed distance from the camera.</summary>
        private void BuildSky()
        {
            sky = new GameObject("StarSky").transform;
            var ps = sky.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1500;
            var emission = ps.emission;
            emission.enabled = false;
            var r = sky.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Weather.AddMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rng = new System.Random(5);
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < 1400; i++)
            {
                float t = (float)rng.NextDouble();
                int arm = rng.Next(2);
                float angle = t * 9f + arm * Mathf.PI;
                float dist = t * 34f;
                var jitter = new Vector3((float)rng.NextDouble() - 0.5f, ((float)rng.NextDouble() - 0.5f) * 0.3f, (float)rng.NextDouble() - 0.5f) * (2f + dist * 0.25f);
                p.position = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist) + jitter;
                p.startSize = Mathf.Lerp(1.0f, 0.3f, t) * (0.6f + (float)rng.NextDouble());
                // Mostly white and warm: a calm sky, not a rainbow.
                p.startColor = Color.Lerp(new Color(1.3f, 1.2f, 1.1f), new Color(0.7f, 0.8f, 1.2f), (float)rng.NextDouble()) * Mathf.Lerp(1f, 0.6f, t);
                p.startLifetime = 100000f;
                ps.Emit(p, 1);
            }
            var planets = new[] { (new Vector3(-30f, 14f, 40f), 6f, H("#C8A07A")), (new Vector3(36f, 22f, 60f), 9f, H("#8A9AC8")), (new Vector3(14f, -6f, 30f), 3f, H("#D8DCE8")) };
            foreach (var (pos, size, color) in planets)
                Shapes.Primitive(PrimitiveType.Sphere, "Planet", sky, pos, Vector3.one * size, MaterialFactory.Create(color, color * 0.25f));
        }

        private void UpdateThemeFx(float dt)
        {
            fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 1.4f);
            if (sky != null)
            {
                // Far below and ahead of the camera, turning slowly.
                var cam = rig.Cam.transform;
                sky.position = cam.position + new Vector3(cam.forward.x, 0f, cam.forward.z).normalized * 55f + Vector3.down * 9f;
                sky.rotation = Quaternion.LookRotation(new Vector3(cam.forward.x, 0f, cam.forward.z)) * Quaternion.Euler(-28f, 0f, 0f) * Quaternion.Euler(0f, time * 3f, 0f);
            }
        }

        private void StopThemeFx()
        {
            if (speedLines != null) Destroy(speedLines.gameObject);
            if (themeDust != null) Destroy(themeDust.gameObject);
            if (sky != null) Destroy(sky.gameObject);
            if (trail != null) DestroyImmediate(trail);
            speedLines = themeDust = null;
            sky = null;
            trail = null;
        }
    }
}
