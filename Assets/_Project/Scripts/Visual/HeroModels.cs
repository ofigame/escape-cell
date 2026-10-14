using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The four builds of foi, the robot the player plays: foi (the white robot with a screen for a face), Volt (a round head with
    /// an antenna and one wide visor), Kaya (broad and heavy, on treads) and Zip (slim and tall with big round eyes).
    /// All share the same named parts (LegL/R, FootL/R, Body, ArmL/R, Head, Visor, EyeL/R) the animations and the garage
    /// use, so only their shapes and base colours differ. Facing +z, about 0.75 units tall.
    /// </summary>
    public static class HeroModels
    {
        public const int Count = 4;

        /// <summary>Each build's own base colours: body, light parts, eyes.</summary>
        public static (Color body, Color light, Color eye) Colours(int hero)
        {
            switch (hero)
            {
                case 1: return (new Color(0.2f, 0.62f, 0.66f), new Color(0.96f, 0.86f, 0.4f), new Color(1f, 0.95f, 0.5f));
                case 2: return (new Color(0.72f, 0.3f, 0.26f), new Color(0.78f, 0.8f, 0.84f), new Color(0.5f, 1f, 0.9f));
                case 3: return (new Color(0.5f, 0.3f, 0.86f), new Color(1f, 0.55f, 0.35f), new Color(0.55f, 1f, 1f));
                default: return (new Color(0.94f, 0.95f, 0.96f), new Color(0.62f, 0.65f, 0.7f), new Color(0.4f, 1f, 1f)); // foi: white, grey, glowing cyan
            }
        }

        /// <summary>
        /// How much longer than the old design this build's torso is (foi's own build: a longer body). Everything above
        /// the hips sits this much higher, and so must the garage gear worn there.
        /// </summary>
        public static float TorsoExtra(int hero) => hero == 0 ? Extra : 0f;

        private const float Extra = 0.06f;

        /// <summary>Builds the body parts of <paramref name="hero"/> under <paramref name="visual"/>; returns the two eyes.</summary>
        public static (Transform eyeL, Transform eyeR) Build(Transform visual, int hero, Material body, Material light, Material dark, Material eye)
        {
            switch (hero)
            {
                case 1: // Volt
                {
                    Pair(visual, "Leg", new Vector3(0.08f, 0.07f, 0f), new Vector3(0.08f, 0.14f, 0.1f), 0.03f, body);
                    Pair(visual, "Foot", new Vector3(0.08f, 0.02f, 0.03f), new Vector3(0.1f, 0.04f, 0.15f), 0.015f, dark);
                    Shapes.Rounded("Body", visual, new Vector3(0f, 0.2f, 0f), new Vector3(0.28f, 0.17f, 0.22f), 0.07f, light);
                    Pair(visual, "Arm", new Vector3(0.17f, 0.2f, 0f), new Vector3(0.06f, 0.13f, 0.07f), 0.03f, body);
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 0.5f, 0f), new Vector3(0.48f, 0.44f, 0.46f), body);
                    Shapes.Rounded("Visor", visual, new Vector3(0f, 0.51f, 0.2f), new Vector3(0.36f, 0.13f, 0.06f), 0.06f, dark);
                    Shapes.Rounded("Hero_Antenna", visual, new Vector3(0f, 0.78f, 0f), new Vector3(0.025f, 0.14f, 0.025f), 0.01f, dark);
                    Shapes.Primitive(PrimitiveType.Sphere, "Hero_Tip", visual, new Vector3(0f, 0.87f, 0f), Vector3.one * 0.07f, eye);
                    var l = Shapes.Rounded("EyeL", visual, new Vector3(-0.07f, 0.52f, 0.235f), new Vector3(0.08f, 0.05f, 0.02f), 0.02f, eye).transform;
                    var r = Shapes.Rounded("EyeR", visual, new Vector3(0.07f, 0.52f, 0.235f), new Vector3(0.08f, 0.05f, 0.02f), 0.02f, eye).transform;
                    return (l, r);
                }
                case 2: // Kaya: broad, on treads
                {
                    Pair(visual, "Leg", new Vector3(0.11f, 0.07f, 0f), new Vector3(0.11f, 0.12f, 0.2f), 0.03f, dark);
                    Pair(visual, "Foot", new Vector3(0.11f, 0.03f, 0.02f), new Vector3(0.13f, 0.06f, 0.24f), 0.03f, dark);
                    Shapes.Rounded("Body", visual, new Vector3(0f, 0.21f, 0f), new Vector3(0.4f, 0.18f, 0.28f), 0.04f, light);
                    Pair(visual, "Arm", new Vector3(0.24f, 0.21f, 0f), new Vector3(0.09f, 0.16f, 0.11f), 0.03f, body);
                    Shapes.Rounded("Head", visual, new Vector3(0f, 0.47f, 0f), new Vector3(0.54f, 0.34f, 0.44f), 0.05f, body);
                    Shapes.Rounded("Visor", visual, new Vector3(0f, 0.48f, 0.222f), new Vector3(0.42f, 0.14f, 0.03f), 0.03f, dark);
                    Pair(visual, "Hero_Bolt", new Vector3(0.28f, 0.47f, 0f), new Vector3(0.04f, 0.1f, 0.1f), 0.02f, light);
                    var l = Shapes.Rounded("EyeL", visual, new Vector3(-0.1f, 0.48f, 0.24f), new Vector3(0.1f, 0.06f, 0.02f), 0.01f, eye).transform;
                    var r = Shapes.Rounded("EyeR", visual, new Vector3(0.1f, 0.48f, 0.24f), new Vector3(0.1f, 0.06f, 0.02f), 0.01f, eye).transform;
                    return (l, r);
                }
                case 3: // Zip: slim and tall
                {
                    // A sleek racer: a squared-off head with a crest fin, a tall torso with a glowing core, twin jets on
                    // its back and thin quick legs. Hard edges, so it reads as a machine from the far play camera.
                    Pair(visual, "Leg", new Vector3(0.065f, 0.09f, 0f), new Vector3(0.065f, 0.18f, 0.09f), 0.02f, dark);
                    Pair(visual, "Foot", new Vector3(0.065f, 0.02f, 0.035f), new Vector3(0.09f, 0.045f, 0.16f), 0.012f, body);
                    Shapes.Rounded("Body", visual, new Vector3(0f, 0.28f, 0f), new Vector3(0.26f, 0.22f, 0.2f), 0.035f, light);
                    Shapes.Rounded("Hero_Chest", visual, new Vector3(0f, 0.29f, 0.1f), new Vector3(0.17f, 0.12f, 0.02f), 0.02f, body);
                    Shapes.Primitive(PrimitiveType.Sphere, "Hero_Core", visual, new Vector3(0f, 0.29f, 0.115f), new Vector3(0.06f, 0.06f, 0.02f), eye);
                    Pair(visual, "Arm", new Vector3(0.165f, 0.27f, 0f), new Vector3(0.05f, 0.17f, 0.07f), 0.018f, dark);
                    Pair(visual, "Hero_Hand", new Vector3(0.165f, 0.17f, 0f), new Vector3(0.065f, 0.05f, 0.08f), 0.015f, body);
                    Pair(visual, "Hero_Jet", new Vector3(0.07f, 0.3f, -0.13f), new Vector3(0.08f, 0.18f, 0.08f), 0.03f, dark);
                    Pair(visual, "Hero_Flame", new Vector3(0.07f, 0.19f, -0.13f), new Vector3(0.05f, 0.05f, 0.05f), 0.02f, eye);
                    Shapes.Rounded("Head", visual, new Vector3(0f, 0.55f, 0f), new Vector3(0.36f, 0.3f, 0.32f), 0.06f, body);
                    Shapes.Rounded("Visor", visual, new Vector3(0f, 0.55f, 0.155f), new Vector3(0.3f, 0.17f, 0.03f), 0.04f, dark);
                    Shapes.Rounded("Hero_Crest", visual, new Vector3(0f, 0.73f, -0.02f), new Vector3(0.05f, 0.08f, 0.26f), 0.02f, light);
                    Pair(visual, "Hero_Ear", new Vector3(0.195f, 0.56f, 0f), new Vector3(0.04f, 0.12f, 0.12f), 0.015f, body);
                    var l = Shapes.Rounded("EyeL", visual, new Vector3(-0.072f, 0.56f, 0.172f), new Vector3(0.09f, 0.1f, 0.03f), 0.04f, eye).transform; // baked size: the blink scales eyes from 1
                    var r = Shapes.Rounded("EyeR", visual, new Vector3(0.072f, 0.56f, 0.172f), new Vector3(0.09f, 0.1f, 0.03f), 0.04f, eye).transform;
                    return (l, r);
                }
                default: // foi: the white robot with a screen for a face
                {
                    // A big bevelled white head with a dark teal screen and two tall glowing pill eyes, grey ear discs;
                    // a white torso with an orange light band at the waist; white upper limbs over grey forearms and
                    // shins, charcoal joints with a warm glow, chunky white boots on grey soles. The details hang
                    // under the named parts, so hiding a part (outfits) hides its details too.
                    var screen = MaterialFactory.Create(new Color(0.06f, 0.2f, 0.24f), new Color(0.01f, 0.05f, 0.06f));
                    var glow = MaterialFactory.Create(new Color(1f, 0.62f, 0.25f), new Color(2.2f, 1.1f, 0.35f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        string side = s < 0 ? "L" : "R";
                        // Boot and sole.
                        var foot = Shapes.Rounded("Foot" + side, visual, new Vector3(0.075f * s, 0.032f, 0.02f), new Vector3(0.11f, 0.06f, 0.16f), 0.025f, body).transform;
                        Shapes.Rounded("Sole", foot, new Vector3(0f, -0.028f, 0f), new Vector3(0.116f, 0.022f, 0.166f), 0.008f, dark);
                        Shapes.Rounded("Light", foot, new Vector3(0f, 0.005f, 0.081f), new Vector3(0.03f, 0.015f, 0.005f), 0.004f, glow);
                        // Shin with a knee joint.
                        var leg = Shapes.Rounded("Leg" + side, visual, new Vector3(0.075f * s, 0.115f, 0f), new Vector3(0.075f, 0.11f, 0.085f), 0.025f, light).transform;
                        Shapes.Rounded("Knee", leg, new Vector3(0f, 0.055f, 0f), new Vector3(0.085f, 0.03f, 0.09f), 0.012f, dark);
                        // Arm: shoulder ball, white upper arm, grey forearm with a warm stripe, charcoal hand.
                        var arm = Shapes.Rounded("Arm" + side, visual, new Vector3(0.168f * s, 0.27f + Extra, 0f), new Vector3(0.065f, 0.085f, 0.075f), 0.025f, body).transform;
                        Shapes.Primitive(PrimitiveType.Sphere, "Shoulder", arm, new Vector3(-0.012f * s, 0.05f, 0f), Vector3.one * 0.075f, dark);
                        Shapes.Rounded("Forearm", arm, new Vector3(0.005f * s, -0.08f, 0.005f), new Vector3(0.072f, 0.085f, 0.08f), 0.025f, light);
                        Shapes.Rounded("Stripe", arm, new Vector3(0.039f * s, -0.075f, 0.005f), new Vector3(0.006f, 0.05f, 0.016f), 0.003f, glow);
                        Shapes.Rounded("Hand", arm, new Vector3(0.005f * s, -0.142f, 0.01f), new Vector3(0.062f, 0.05f, 0.07f), 0.02f, dark);
                    }
                    Shapes.Rounded("Body", visual, new Vector3(0f, 0.255f + Extra * 0.5f, 0f), new Vector3(0.25f, 0.16f + Extra, 0.19f), 0.05f, body); // a longer body than the first design
                    Shapes.Rounded("Hero_Chest", visual, new Vector3(0f, 0.27f + Extra * 0.75f, 0.093f), new Vector3(0.14f, 0.09f + Extra * 0.4f, 0.015f), 0.03f, body);
                    Shapes.Rounded("Hero_Belt", visual, new Vector3(0f, 0.183f, 0f), new Vector3(0.235f, 0.022f, 0.18f), 0.01f, glow);
                    Shapes.Rounded("Hero_Hips", visual, new Vector3(0f, 0.16f, 0f), new Vector3(0.19f, 0.05f, 0.15f), 0.02f, dark);
                    Shapes.Rounded("Hero_Neck", visual, new Vector3(0f, 0.345f + Extra, 0f), new Vector3(0.11f, 0.04f, 0.09f), 0.015f, dark);
                    Shapes.Rounded("Head", visual, new Vector3(0f, 0.525f + Extra, 0f), new Vector3(0.44f, 0.36f, 0.4f), 0.085f, body);
                    Shapes.Rounded("Visor", visual, new Vector3(0f, 0.52f + Extra, 0.196f), new Vector3(0.32f, 0.22f, 0.02f), 0.055f, screen);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var ear = Shapes.Primitive(PrimitiveType.Cylinder, s < 0 ? "Hero_EarL" : "Hero_EarR", visual, new Vector3(0.225f * s, 0.53f + Extra, -0.01f), new Vector3(0.14f, 0.022f, 0.14f), light).transform;
                        ear.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        Shapes.Primitive(PrimitiveType.Cylinder, "Core", ear, new Vector3(0f, 0.6f, 0f), new Vector3(0.6f, 0.5f, 0.6f), dark);
                    }
                    var l = Shapes.Rounded("EyeL", visual, new Vector3(-0.062f, 0.525f + Extra, 0.209f), new Vector3(0.048f, 0.1f, 0.014f), 0.022f, eye).transform;
                    var r = Shapes.Rounded("EyeR", visual, new Vector3(0.062f, 0.525f + Extra, 0.209f), new Vector3(0.048f, 0.1f, 0.014f), 0.022f, eye).transform;
                    return (l, r);
                }
            }
        }

        /// <summary>A left/right pair named NameL / NameR, mirrored across x.</summary>
        private static void Pair(Transform parent, string name, Vector3 right, Vector3 size, float radius, Material m)
        {
            Shapes.Rounded(name + "L", parent, new Vector3(-right.x, right.y, right.z), size, radius, m);
            Shapes.Rounded(name + "R", parent, right, size, radius, m);
        }

        /// <summary>The names of the parts a build is made of (removed before another build replaces them).</summary>
        public static bool IsPart(string name) =>
            name.StartsWith("Hero_") || name == "Body" || name == "Head" || name == "Visor" || name.StartsWith("Leg") || name.StartsWith("Foot") || name.StartsWith("Arm") || name.StartsWith("Eye");
    }
}
