using SquashBot.Core;
using SquashBot.Gameplay;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The grand look of a battle arena (after the user's reference art): white, orange and blue sci-fi towers on every
    /// corner of the octagonal courtyard — tall spires at the back, drum towers with blue domes at the sides, squat
    /// turrets at the front so they never hide the fight — a giant spire and a domed hall rising behind the far wall,
    /// heavy cannon bastions on the two front corners, and a hexagonal pad round foi's cell. Decoration only.
    /// </summary>
    public static class ArenaDecor
    {
        private static Material white, orange, blue, dark, glowBlue, glowOrange;

        private static void Materials()
        {
            white = MaterialFactory.CreateSurface(new Color(0.92f, 0.9f, 0.86f), Color.black, 0.55f, 0.1f);
            orange = MaterialFactory.CreateSurface(new Color(1f, 0.5f, 0.12f), new Color(0.25f, 0.08f, 0f), 0.6f, 0.3f);
            blue = MaterialFactory.CreateSurface(new Color(0.2f, 0.42f, 0.88f), new Color(0.05f, 0.12f, 0.3f), 0.7f, 0.3f);
            dark = MaterialFactory.CreateSurface(new Color(0.24f, 0.25f, 0.3f), Color.black, 0.5f, 0.55f);
            glowBlue = MaterialFactory.Create(new Color(0.45f, 0.75f, 1f), new Color(0.8f, 1.8f, 3.2f));
            glowOrange = MaterialFactory.Create(new Color(1f, 0.65f, 0.3f), new Color(3f, 1.4f, 0.4f));
        }

        /// <param name="b">The square the courtyard is cut from (x0, y0, x1, y1), <paramref name="cut"/> its corner cut.</param>
        public static void Build(Transform root, int[] b, int cut, int arena, GridPos start)
        {
            Materials();
            int x0 = b[0], y0 = b[1], x1 = b[2], y1 = b[3];
            float grow = 1.9f + arena * 0.06f; // towering, above the high ramparts
            // The eight corners of the octagon, each with a tower just outside it.
            var corners = new (int x, int y, float ox, float oy)[]
            {
                (x0, y0 + cut, -1f, -0.4f), (x0 + cut, y0, -0.4f, -1f), (x1 - cut, y0, 0.4f, -1f), (x1, y0 + cut, 1f, -0.4f),
                (x1, y1 - cut, 1f, 0.4f), (x1 - cut, y1, 0.4f, 1f), (x0 + cut, y1, -0.4f, 1f), (x0, y1 - cut, -1f, 0.4f),
            };
            for (int i = 0; i < corners.Length; i++)
            {
                var c = corners[i];
                var at = GridView.ToWorld(new GridPos(c.x, c.y)) + new Vector3(c.ox, 0f, c.oy).normalized * 1.9f + Vector3.up * (GridView.SurfaceY - 0.1f);
                if (i == 1 || i == 2) Turret(root, at, grow * 0.9f);           // the front: low, never in the way
                else if (i == 5 || i == 6) Spire(root, at, grow * (1.15f + (i % 2) * 0.25f)); // the back: tall spires
                else Drum(root, at, grow * (i == 0 || i == 3 ? 0.85f : 1.05f));  // the sides: domed drums
            }
            // Landmarks beyond the far wall: a giant spire to the right, a domed hall to the left.
            float midX = (x0 + x1) * 0.5f;
            var far = new Vector3(0f, GridView.SurfaceY - 0.1f, y1 + 3.6f);
            Spire(root, new Vector3(midX + (x1 - x0) * 0.22f, far.y, far.z), grow * 1.9f);
            Hall(root, new Vector3(midX - (x1 - x0) * 0.2f, far.y, far.z + 0.5f), grow * 1.3f);
            // Cannon bastions on the two front corners.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s < 0 ? x0 + cut * 0.35f - 1.6f : x1 - cut * 0.35f + 1.6f;
                Bastion(root, new Vector3(x, GridView.SurfaceY - 0.1f, y0 + cut * 0.35f - 1.6f), s, grow);
            }
            // The hexagonal pad round foi's cell, with orange spokes.
            var centre = GridView.ToWorld(start) + Vector3.up * (GridView.SurfaceY + 0.06f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + Mathf.PI / 6f;
                var mid = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.2f;
                Box("HexEdge", root, mid, new Vector3(2.5f, 0.04f, 0.14f), orange, 0.02f).localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                Box("Spoke", root, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.2f, new Vector3(1.4f, 0.03f, 0.06f), glowOrange, 0.01f)
                    .localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
        }

        /// <summary>A tall white spire: stacked tapering drums with orange bands, a blue window strip, an antenna.</summary>
        private static void Spire(Transform root, Vector3 at, float scale)
        {
            var t = Group("Spire", root, at, scale);
            Cyl(t, 0f, 0.6f, 1.9f, dark);
            Cyl(t, 0.6f, 0.15f, 2.0f, orange);
            Cyl(t, 0.75f, 2.4f, 1.45f, white);
            Box("Window", t, new Vector3(0f, 1.9f, -0.72f), new Vector3(0.3f, 1.8f, 0.06f), glowBlue, 0.05f);
            Cyl(t, 3.15f, 0.18f, 1.6f, orange);
            Cyl(t, 3.33f, 2.0f, 1.0f, white);
            Box("Window2", t, new Vector3(0f, 4.2f, -0.5f), new Vector3(0.22f, 1.3f, 0.05f), glowBlue, 0.04f);
            Cyl(t, 5.33f, 0.16f, 1.15f, orange);
            Cyl(t, 5.49f, 0.9f, 0.6f, white);
            Shapes.Primitive(PrimitiveType.Sphere, "Cap", t, new Vector3(0f, 6.5f, 0f), new Vector3(0.7f, 0.5f, 0.7f), blue);
            Cyl(t, 6.7f, 1.4f, 0.08f, orange);
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", t, new Vector3(0f, 8.2f, 0f), Vector3.one * 0.2f, glowOrange);
        }

        /// <summary>A drum tower with a blue dome and orange rings.</summary>
        private static void Drum(Transform root, Vector3 at, float scale)
        {
            var t = Group("Drum", root, at, scale);
            Cyl(t, 0f, 0.5f, 2.0f, dark);
            Cyl(t, 0.5f, 2.2f, 1.7f, white);
            for (int i = 0; i < 2; i++) Cyl(t, 1.1f + i * 1.0f, 0.14f, 1.78f, orange);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                Box("Window", t, new Vector3(Mathf.Cos(a) * 0.86f, 1.6f, Mathf.Sin(a) * 0.86f), new Vector3(0.3f, 0.5f, 0.06f), glowBlue, 0.03f)
                    .localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
            Cyl(t, 2.7f, 0.2f, 1.9f, orange);
            Shapes.Primitive(PrimitiveType.Sphere, "Dome", t, new Vector3(0f, 2.9f, 0f), new Vector3(1.5f, 1.1f, 1.5f), blue);
            Cyl(t, 3.4f, 0.6f, 0.12f, white);
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", t, new Vector3(0f, 4.1f, 0f), Vector3.one * 0.22f, glowOrange);
        }

        /// <summary>A squat front turret (it never hides the fight): a wide drum with a gun and a blue light.</summary>
        private static void Turret(Transform root, Vector3 at, float scale)
        {
            var t = Group("Turret", root, at, scale);
            Cyl(t, 0f, 0.5f, 2.0f, dark);
            Cyl(t, 0.5f, 0.7f, 1.7f, white);
            Cyl(t, 1.2f, 0.16f, 1.85f, orange);
            Shapes.Primitive(PrimitiveType.Sphere, "Head", t, new Vector3(0f, 1.45f, 0f), new Vector3(1.2f, 0.7f, 1.2f), white);
            Shapes.Primitive(PrimitiveType.Cylinder, "Gun", t, new Vector3(0f, 1.55f, 0.75f), new Vector3(0.22f, 0.45f, 0.22f), dark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Shapes.Primitive(PrimitiveType.Sphere, "Light", t, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.3f, glowBlue);
        }

        /// <summary>A domed hall: a wide white building with blue windows and an orange-ringed dome.</summary>
        private static void Hall(Transform root, Vector3 at, float scale)
        {
            var t = Group("Hall", root, at, scale);
            Box("Base", t, new Vector3(0f, 0.8f, 0f), new Vector3(3.4f, 1.6f, 2.6f), white, 0.25f);
            Box("Trim", t, new Vector3(0f, 1.65f, 0f), new Vector3(3.5f, 0.16f, 2.7f), orange, 0.06f);
            for (int i = 0; i < 3; i++) Box("Window", t, new Vector3(-1.1f + i * 1.1f, 0.95f, -1.31f), new Vector3(0.6f, 0.7f, 0.05f), glowBlue, 0.05f);
            Cyl(t, 1.7f, 0.6f, 2.2f, white);
            Cyl(t, 2.3f, 0.14f, 2.3f, orange);
            Shapes.Primitive(PrimitiveType.Sphere, "Dome", t, new Vector3(0f, 2.45f, 0f), new Vector3(2.1f, 1.5f, 2.1f), blue);
            Shapes.Primitive(PrimitiveType.Cylinder, "Mast", t, new Vector3(0f, 3.6f, 0f), new Vector3(0.1f, 0.6f, 0.1f), orange);
        }

        /// <summary>A heavy cannon bastion on a front corner: an armoured block with a big barrel pointing out.</summary>
        private static void Bastion(Transform root, Vector3 at, float side, float scale)
        {
            var t = Group("Bastion", root, at, scale);
            Box("Block", t, new Vector3(0f, 0.7f, 0f), new Vector3(2.2f, 1.4f, 2.2f), dark, 0.25f);
            Box("Armour", t, new Vector3(0f, 1.1f, 0f), new Vector3(2.0f, 0.9f, 2.0f), white, 0.3f);
            Box("Band", t, new Vector3(0f, 1.58f, 0f), new Vector3(2.1f, 0.14f, 2.1f), orange, 0.05f);
            var gun = Shapes.Primitive(PrimitiveType.Cylinder, "Barrel", t, new Vector3(side * 0.6f, 1.15f, -0.7f), new Vector3(0.7f, 0.9f, 0.7f), white).transform;
            gun.localRotation = Quaternion.Euler(70f, side * -35f, 0f);
            Shapes.Primitive(PrimitiveType.Cylinder, "Muzzle", t, new Vector3(side * 1.15f, 1.0f, -1.45f), new Vector3(0.82f, 0.12f, 0.82f), orange)
                .transform.localRotation = Quaternion.Euler(70f, side * -35f, 0f);
            Shapes.Primitive(PrimitiveType.Sphere, "Lens", t, new Vector3(side * 1.2f, 0.98f, -1.52f), Vector3.one * 0.4f, glowBlue);
        }

        private static Transform Group(string name, Transform root, Vector3 at, float scale)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.position = at;
            t.localScale = Vector3.one * scale * 0.8f;
            return t;
        }

        private static void Cyl(Transform t, float y, float h, float d, Material m) =>
            Shapes.Primitive(PrimitiveType.Cylinder, "Drum", t, new Vector3(0f, y + h * 0.5f, 0f), new Vector3(d, h * 0.5f, d), m);

        private static Transform Box(string n, Transform p, Vector3 at, Vector3 size, Material m, float r)
        {
            var go = Shapes.Rounded(n, p, Vector3.zero, size, Mathf.Min(r, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f), m);
            go.transform.localPosition = at;
            return go.transform;
        }
    }
}
