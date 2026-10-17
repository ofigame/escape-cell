using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The builds of foi, the robot the player plays (only foi itself is offered now): foi (the grey robot with a screen face and star eyes), Volt (a round head with
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
                default: return (new Color(0.74f, 0.76f, 0.81f), new Color(0.52f, 0.55f, 0.61f), new Color(1.6f, 1.25f, 0.3f)); // foi: soft grey, darker grey, glowing yellow stars
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
                default: // foi: the soft grey robot with a screen for a face and two star eyes
                {
                    // Soft, rounded shapes all over (an oval head, a pebble body): a big grey head with a dark navy screen
                    // and two glowing star eyes, round ear discs; a short body with a dark chest panel and a warm light;
                    // clear arms (upper arm, forearm, round hand), legs with knee joints and chunky dark boots. The
                    // details hang under the named parts, so hiding a part (outfits) hides its details too.
                    var screen = MaterialFactory.Create(new Color(0.05f, 0.08f, 0.17f), new Color(0.01f, 0.02f, 0.05f));
                    var frame = MaterialFactory.Create(new Color(0.3f, 0.32f, 0.38f), Color.black);
                    var glow = MaterialFactory.Create(new Color(1f, 0.78f, 0.3f), new Color(2.2f, 1.5f, 0.4f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        string side = s < 0 ? "L" : "R";
                        // A chunky rounded boot on a dark sole.
                        var foot = Shapes.Rounded("Foot" + side, visual, new Vector3(0.08f * s, 0.036f, 0.022f), new Vector3(0.125f, 0.07f, 0.175f), 0.034f, dark).transform;
                        Shapes.Rounded("Sole", foot, new Vector3(0f, -0.03f, 0f), new Vector3(0.13f, 0.018f, 0.18f), 0.008f, frame);
                        Shapes.Rounded("Cuff", foot, new Vector3(0f, 0.04f, -0.01f), new Vector3(0.1f, 0.025f, 0.11f), 0.012f, light);
                        // A leg with a knee joint.
                        var leg = Shapes.Rounded("Leg" + side, visual, new Vector3(0.08f * s, 0.12f, 0f), new Vector3(0.078f, 0.11f, 0.084f), 0.034f, body).transform;
                        Shapes.Primitive(PrimitiveType.Sphere, "Knee", leg, new Vector3(0f, 0.045f, 0.01f), Vector3.one * 0.07f, dark);
                        // An arm out from the body: shoulder ball, upper arm, forearm, a round hand.
                        var arm = Shapes.Rounded("Arm" + side, visual, new Vector3(0.185f * s, 0.27f + Extra, 0f), new Vector3(0.07f, 0.09f, 0.075f), 0.032f, body).transform;
                        Shapes.Primitive(PrimitiveType.Sphere, "Shoulder", arm, new Vector3(-0.014f * s, 0.05f, 0f), Vector3.one * 0.08f, dark);
                        Shapes.Rounded("Forearm", arm, new Vector3(0.006f * s, -0.082f, 0.006f), new Vector3(0.072f, 0.085f, 0.078f), 0.033f, light);
                        Shapes.Primitive(PrimitiveType.Sphere, "Hand", arm, new Vector3(0.006f * s, -0.15f, 0.012f), Vector3.one * 0.078f, dark);
                    }
                    Shapes.Rounded("Body", visual, new Vector3(0f, 0.255f + Extra * 0.5f, 0f), new Vector3(0.27f, 0.17f + Extra, 0.2f), 0.08f, body);
                    Shapes.Rounded("Hero_Chest", visual, new Vector3(0f, 0.27f + Extra * 0.7f, 0.096f), new Vector3(0.15f, 0.1f + Extra * 0.4f, 0.016f), 0.04f, frame);
                    Shapes.Primitive(PrimitiveType.Sphere, "Hero_Light", visual, new Vector3(0f, 0.27f + Extra * 0.7f, 0.106f), new Vector3(0.045f, 0.045f, 0.012f), glow);
                    Shapes.Rounded("Hero_Hips", visual, new Vector3(0f, 0.165f, 0f), new Vector3(0.2f, 0.05f, 0.15f), 0.024f, dark);
                    Shapes.Rounded("Hero_Neck", visual, new Vector3(0f, 0.35f + Extra, 0f), new Vector3(0.1f, 0.04f, 0.08f), 0.018f, dark);
                    Shapes.Rounded("Head", visual, new Vector3(0f, 0.53f + Extra, 0f), new Vector3(0.46f, 0.37f, 0.4f), 0.13f, body);
                    Shapes.Rounded("Hero_Frame", visual, new Vector3(0f, 0.525f + Extra, 0.19f), new Vector3(0.37f, 0.27f, 0.03f), 0.09f, frame);
                    Shapes.Rounded("Visor", visual, new Vector3(0f, 0.525f + Extra, 0.201f), new Vector3(0.33f, 0.23f, 0.02f), 0.08f, screen);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var ear = Shapes.Primitive(PrimitiveType.Cylinder, s < 0 ? "Hero_EarL" : "Hero_EarR", visual, new Vector3(0.232f * s, 0.53f + Extra, -0.01f), new Vector3(0.12f, 0.022f, 0.12f), light).transform;
                        ear.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        Shapes.Primitive(PrimitiveType.Cylinder, "Core", ear, new Vector3(0f, 0.6f, 0f), new Vector3(0.55f, 0.5f, 0.55f), dark);
                    }
                    var l = StarEye("EyeL", visual, new Vector3(-0.068f, 0.53f + Extra, 0.213f), eye);
                    var r = StarEye("EyeR", visual, new Vector3(0.068f, 0.53f + Extra, 0.213f), eye);
                    return (l, r);
                }
            }
        }

        private static Mesh star;

        /// <summary>A glowing five-pointed star eye (its size baked in: the blink scales eyes from 1).</summary>
        private static Transform StarEye(string name, Transform parent, Vector3 at, Material eye)
        {
            if (star == null) star = StarMesh(0.05f, 0.022f, 0.014f);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = star;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = eye;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>A flat star prism facing +z: <paramref name="outer"/> and <paramref name="inner"/> radii, <paramref name="depth"/> thick.</summary>
        private static Mesh StarMesh(float outer, float inner, float depth)
        {
            var v = new System.Collections.Generic.List<Vector3>();
            var n = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            var rim = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                rim[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            float hz = depth * 0.5f;
            // Front and back faces: fans round the centre.
            foreach (float side in new[] { 1f, -1f })
            {
                int c = v.Count;
                v.Add(new Vector3(0f, 0f, side * hz)); n.Add(new Vector3(0f, 0f, side));
                for (int i = 0; i < 10; i++) { v.Add(new Vector3(rim[i].x, rim[i].y, side * hz)); n.Add(new Vector3(0f, 0f, side)); }
                for (int i = 0; i < 10; i++)
                {
                    int a = c + 1 + i, b = c + 1 + (i + 1) % 10;
                    if (side > 0) { tris.Add(c); tris.Add(a); tris.Add(b); }
                    else { tris.Add(c); tris.Add(b); tris.Add(a); }
                }
            }
            // The sides.
            for (int i = 0; i < 10; i++)
            {
                var p = rim[i]; var q = rim[(i + 1) % 10];
                var normal = new Vector3(q.y - p.y, -(q.x - p.x), 0f).normalized;
                int k = v.Count;
                v.Add(new Vector3(p.x, p.y, hz)); v.Add(new Vector3(q.x, q.y, hz)); v.Add(new Vector3(q.x, q.y, -hz)); v.Add(new Vector3(p.x, p.y, -hz));
                for (int j = 0; j < 4; j++) n.Add(normal);
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1); tris.Add(k); tris.Add(k + 3); tris.Add(k + 2);
            }
            var m = new Mesh { name = "StarEye" };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
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
