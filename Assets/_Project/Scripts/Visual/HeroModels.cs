using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The four builds of foi, the robot the player plays: Classic (the square-headed original), Volt (a round head with
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
                case 3: return (new Color(0.56f, 0.38f, 0.86f), new Color(1f, 0.68f, 0.82f), new Color(0.75f, 1f, 1f));
                default: return (Palette.RobotBody, Palette.RobotLight, Palette.RobotEye);
            }
        }

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
                    Pair(visual, "Leg", new Vector3(0.06f, 0.09f, 0f), new Vector3(0.06f, 0.18f, 0.08f), 0.025f, body);
                    Pair(visual, "Foot", new Vector3(0.06f, 0.02f, 0.03f), new Vector3(0.08f, 0.04f, 0.14f), 0.012f, dark);
                    Shapes.Rounded("Body", visual, new Vector3(0f, 0.25f, 0f), new Vector3(0.22f, 0.16f, 0.18f), 0.06f, light);
                    Pair(visual, "Arm", new Vector3(0.14f, 0.25f, 0f), new Vector3(0.045f, 0.16f, 0.06f), 0.02f, body);
                    Shapes.Rounded("Head", visual, new Vector3(0f, 0.57f, 0f), new Vector3(0.38f, 0.46f, 0.36f), 0.15f, body);
                    Shapes.Rounded("Visor", visual, new Vector3(0f, 0.58f, 0.18f), new Vector3(0.3f, 0.24f, 0.03f), 0.08f, dark);
                    Pair(visual, "Hero_Ear", new Vector3(0.21f, 0.62f, 0f), new Vector3(0.04f, 0.14f, 0.08f), 0.02f, light);
                    var l = Shapes.Primitive(PrimitiveType.Sphere, "EyeL", visual, new Vector3(-0.07f, 0.59f, 0.2f), new Vector3(0.1f, 0.12f, 0.03f), eye).transform;
                    var r = Shapes.Primitive(PrimitiveType.Sphere, "EyeR", visual, new Vector3(0.07f, 0.59f, 0.2f), new Vector3(0.1f, 0.12f, 0.03f), eye).transform;
                    return (l, r);
                }
                default: // Classic
                {
                    Pair(visual, "Leg", new Vector3(0.08f, 0.07f, 0f), new Vector3(0.09f, 0.14f, 0.11f), 0.027f, body);
                    Pair(visual, "Foot", new Vector3(0.08f, 0.02f, 0.03f), new Vector3(0.11f, 0.04f, 0.16f), 0.012f, dark);
                    Shapes.Rounded("Body", visual, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.16f, 0.24f), 0.048f, light);
                    Pair(visual, "Arm", new Vector3(0.18f, 0.2f, 0f), new Vector3(0.06f, 0.14f, 0.08f), 0.018f, body);
                    Shapes.Rounded("Head", visual, new Vector3(0f, 0.5f, 0f), new Vector3(0.46f, 0.42f, 0.44f), 0.07f, body);
                    Shapes.Rounded("Visor", visual, new Vector3(0f, 0.5f, 0.222f), new Vector3(0.34f, 0.22f, 0.03f), 0.04f, dark);
                    var l = Shapes.Rounded("EyeL", visual, new Vector3(-0.075f, 0.51f, 0.24f), new Vector3(0.07f, 0.08f, 0.02f), 0.01f, eye).transform;
                    var r = Shapes.Rounded("EyeR", visual, new Vector3(0.075f, 0.51f, 0.24f), new Vector3(0.07f, 0.08f, 0.02f), 0.01f, eye).transform;
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
