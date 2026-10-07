using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>The moving enemies' models, built from simple shapes (about one tile in size, standing on the tile top).</summary>
    public static class EnemyModels
    {
        private static Material M(Color c, Color e = default) => MaterialFactory.Create(c, e);

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        /// <summary>Süpürgeç: a squat yellow cleaning cart with a rolling brush in front.</summary>
        public static Transform Sweeper(Transform root)
        {
            var body = M(Hex("#F2C14E"), new Color(0.3f, 0.2f, 0f));
            var dark = M(Hex("#3A3A4A"));
            Shapes.Rounded("Body", root, new Vector3(0f, 0.22f, -0.05f), new Vector3(0.6f, 0.32f, 0.5f), 0.1f, body);
            var brush = Shapes.Primitive(PrimitiveType.Cylinder, "Brush", root, new Vector3(0f, 0.12f, 0.28f), new Vector3(0.16f, 0.32f, 0.16f), M(Hex("#4FA3D1"))).transform;
            brush.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Shapes.Primitive(PrimitiveType.Sphere, "Light", root, new Vector3(0f, 0.44f, -0.05f), Vector3.one * 0.12f, M(Hex("#FF8A3D"), new Color(2f, 0.8f, 0.2f)));
            foreach (float x in new[] { -0.22f, 0.22f })
                Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", root, new Vector3(x, 0.08f, -0.2f), new Vector3(0.14f, 0.04f, 0.14f), dark).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return root;
        }

        /// <summary>Silgi-bot: a grey eraser on tracks, a pink rubber end and one cold eye.</summary>
        public static Transform Eraser(Transform root)
        {
            Shapes.Rounded("Tracks", root, new Vector3(0f, 0.08f, 0f), new Vector3(0.56f, 0.14f, 0.56f), 0.06f, M(Hex("#3A3A4A")));
            Shapes.Rounded("Body", root, new Vector3(0f, 0.3f, 0f), new Vector3(0.46f, 0.32f, 0.46f), 0.12f, M(Hex("#A9ADB8")));
            Shapes.Rounded("Rubber", root, new Vector3(0f, 0.18f, 0.24f), new Vector3(0.42f, 0.16f, 0.12f), 0.05f, M(Hex("#F28AA8")));
            Shapes.Primitive(PrimitiveType.Sphere, "Eye", root, new Vector3(0f, 0.36f, 0.23f), Vector3.one * 0.12f, M(Hex("#9FE7FF"), new Color(0.6f, 1.6f, 2.2f)));
            return root;
        }

        /// <summary>Gözcü dron: a hovering disc with a blue lens; it floats above the floor.</summary>
        public static Transform Drone(Transform root)
        {
            var body = new GameObject("Hover").transform;
            body.SetParent(root, false);
            body.localPosition = new Vector3(0f, 0.6f, 0f);
            Shapes.Primitive(PrimitiveType.Cylinder, "Disc", body, Vector3.zero, new Vector3(0.5f, 0.08f, 0.5f), M(Hex("#5A6488")));
            Shapes.Primitive(PrimitiveType.Sphere, "Lens", body, new Vector3(0f, -0.02f, 0.2f), Vector3.one * 0.16f, M(Hex("#7FD8FF"), new Color(0.6f, 1.6f, 2.4f)));
            foreach (float x in new[] { -0.3f, 0.3f })
                Shapes.Primitive(PrimitiveType.Cylinder, "Rotor", body, new Vector3(x, 0.08f, 0f), new Vector3(0.3f, 0.01f, 0.06f), M(Hex("#DDE4F4"))).AddComponent<Spinner>().speed = 900f;
            return root;
        }

        /// <summary>Kum solucanı: a ringed sand worm (shown only when it dives).</summary>
        public static Transform Sandworm(Transform root)
        {
            var skin = M(Hex("#C98B4F"));
            for (int i = 0; i < 5; i++)
                Shapes.Primitive(PrimitiveType.Sphere, "Ring", root, new Vector3(0f, 0.2f + Mathf.Sin(i * 0.8f) * 0.2f, -0.6f + i * 0.3f), Vector3.one * (0.42f - i * 0.03f), skin);
            Shapes.Primitive(PrimitiveType.Sphere, "Mouth", root, new Vector3(0f, 0.32f, 0.62f), Vector3.one * 0.22f, M(Hex("#3A1A10")));
            return root;
        }

        /// <summary>Yengeç-bot: a wide red crab robot with two big claws.</summary>
        public static Transform Crab(Transform root, out Transform clawL, out Transform clawR)
        {
            var shell = M(Hex("#E0604F"), new Color(0.3f, 0.05f, 0.03f));
            Shapes.Rounded("Shell", root, new Vector3(0f, 0.22f, 0f), new Vector3(0.6f, 0.24f, 0.42f), 0.12f, shell);
            foreach (float x in new[] { -0.08f, 0.08f })
                Shapes.Primitive(PrimitiveType.Sphere, "Eye", root, new Vector3(x, 0.4f, 0.12f), Vector3.one * 0.08f, M(Color.white, new Color(0.5f, 0.5f, 0.5f)));
            clawL = Shapes.Rounded("ClawL", root, new Vector3(-0.38f, 0.28f, 0.14f), new Vector3(0.16f, 0.16f, 0.2f), 0.06f, shell).transform;
            clawR = Shapes.Rounded("ClawR", root, new Vector3(0.38f, 0.28f, 0.14f), new Vector3(0.16f, 0.16f, 0.2f), 0.06f, shell).transform;
            for (int i = 0; i < 3; i++)
                foreach (float s in new[] { -1f, 1f })
                    Shapes.Rounded("Leg", root, new Vector3(s * 0.3f, 0.06f, -0.12f + i * 0.1f), new Vector3(0.16f, 0.04f, 0.04f), 0.015f, shell);
            return root;
        }

        /// <summary>Taret: a squat turret with a barrel pointing along its line.</summary>
        public static Transform Turret(Transform root)
        {
            Shapes.Primitive(PrimitiveType.Cylinder, "Base", root, new Vector3(0f, 0.15f, 0f), new Vector3(0.6f, 0.15f, 0.6f), M(Hex("#4A5070")));
            Shapes.Primitive(PrimitiveType.Sphere, "Dome", root, new Vector3(0f, 0.34f, 0f), Vector3.one * 0.44f, M(Hex("#6A7298")));
            var barrel = Shapes.Primitive(PrimitiveType.Cylinder, "Barrel", root, new Vector3(0f, 0.36f, 0.3f), new Vector3(0.12f, 0.22f, 0.12f), M(Hex("#2A2E40"))).transform;
            barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Shapes.Primitive(PrimitiveType.Sphere, "Light", root, new Vector3(0f, 0.5f, 0.12f), Vector3.one * 0.08f, M(Hex("#FF5A6A"), new Color(2.4f, 0.4f, 0.5f)));
            return root;
        }

        /// <summary>Penguen-bot: a round black-and-white robot penguin with an orange beak.</summary>
        public static Transform Penguin(Transform root)
        {
            Shapes.Primitive(PrimitiveType.Sphere, "Body", root, new Vector3(0f, 0.3f, 0f), new Vector3(0.46f, 0.56f, 0.42f), M(Hex("#2A2E40")));
            Shapes.Primitive(PrimitiveType.Sphere, "Belly", root, new Vector3(0f, 0.28f, 0.1f), new Vector3(0.34f, 0.44f, 0.28f), M(Color.white));
            Shapes.Rounded("Beak", root, new Vector3(0f, 0.44f, 0.24f), new Vector3(0.1f, 0.06f, 0.12f), 0.02f, M(Hex("#FF9A3D")));
            foreach (float x in new[] { -0.08f, 0.08f })
                Shapes.Primitive(PrimitiveType.Sphere, "Eye", root, new Vector3(x, 0.5f, 0.18f), Vector3.one * 0.07f, M(Hex("#9FE7FF"), new Color(0.6f, 1.6f, 2.2f)));
            return root;
        }

        /// <summary>Yay-bot: a small cube robot on a big coiled spring.</summary>
        public static Transform Springbot(Transform root)
        {
            var coil = M(Hex("#C9CFE0"));
            for (int i = 0; i < 4; i++)
                Shapes.Primitive(PrimitiveType.Cylinder, "Coil", root, new Vector3(0f, 0.06f + i * 0.08f, 0f), new Vector3(0.3f, 0.015f, 0.3f), coil);
            Shapes.Rounded("Body", root, new Vector3(0f, 0.5f, 0f), new Vector3(0.36f, 0.3f, 0.36f), 0.08f, M(Hex("#7ACB6A")));
            Shapes.Rounded("Visor", root, new Vector3(0f, 0.52f, 0.18f), new Vector3(0.26f, 0.12f, 0.03f), 0.03f, M(Hex("#22303A")));
            Shapes.Primitive(PrimitiveType.Sphere, "Eye", root, new Vector3(0f, 0.52f, 0.2f), Vector3.one * 0.07f, M(Hex("#FFE07A"), new Color(2f, 1.6f, 0.4f)));
            return root;
        }
    }
}
