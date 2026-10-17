using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The duel chests, old wooden chests with an arched lid: the plain one bare planks and iron corners with a green
    /// strap; the middle one bronze-cornered with bronze studs and plates and a yellow strap; the great one bigger, edged
    /// and patterned in gold with a red strap and a jewelled lock. The lid hinges at the back (its own transform).
    /// </summary>
    public static class ChestModels
    {
        public static Color StrapColour(ChestTier t) =>
            t == ChestTier.Basic ? new Color(0.25f, 0.75f, 0.3f) : t == ChestTier.Mid ? new Color(1f, 0.8f, 0.2f) : new Color(0.9f, 0.18f, 0.2f);

        /// <summary>Builds a chest standing on its parent's origin, facing +Z; <paramref name="lid"/> turns about X to open.</summary>
        public static Transform Build(Transform parent, ChestTier t, out Transform lid)
        {
            float w = t == ChestTier.Advanced ? 1.3f : t == ChestTier.Mid ? 1.12f : 1f;
            float d = w * 0.68f, h = w * 0.55f, lidH = w * 0.32f;
            var root = new GameObject("Chest" + t).transform;
            root.SetParent(parent, false);

            var woodA = MaterialFactory.Create(new Color(0.36f, 0.22f, 0.12f), Color.black);
            var woodB = MaterialFactory.Create(new Color(0.3f, 0.18f, 0.1f), Color.black);
            var woodDark = MaterialFactory.Create(new Color(0.22f, 0.13f, 0.07f), Color.black);
            Material metal = t == ChestTier.Basic ? MaterialFactory.CreateSurface(new Color(0.42f, 0.42f, 0.45f), Color.black, 0.5f, 0.7f)
                : t == ChestTier.Mid ? MaterialFactory.CreateSurface(new Color(0.66f, 0.4f, 0.2f), new Color(0.1f, 0.05f, 0.02f), 0.65f, 0.85f)
                : MaterialFactory.CreateSurface(new Color(1f, 0.78f, 0.28f), new Color(0.35f, 0.22f, 0.04f), 0.85f, 0.9f);
            var sc = StrapColour(t);
            var strap = MaterialFactory.Create(sc, sc * 0.25f);

            // The body: planks one above another, a dark inside, the frame's corner posts.
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            Shapes.Rounded("Inside", body, new Vector3(0f, h * 0.5f, 0f), new Vector3(w - 0.04f, h - 0.02f, d - 0.04f), 0.02f, woodDark);
            int planks = 3;
            for (int i = 0; i < planks; i++)
            {
                float y = h * (i + 0.5f) / planks;
                Shapes.Rounded("Plank", body, new Vector3(0f, y, 0f), new Vector3(w, h / planks - 0.012f, d), 0.015f, i % 2 == 0 ? woodA : woodB);
            }
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Shapes.Rounded("Corner", body, new Vector3(sx * (w * 0.5f - 0.03f), h * 0.5f, sz * (d * 0.5f - 0.03f)), new Vector3(0.09f, h + 0.02f, 0.09f), 0.02f, metal);
            Shapes.Rounded("Foot", body, new Vector3(0f, 0.02f, 0f), new Vector3(w + 0.04f, 0.05f, d + 0.04f), 0.02f, woodDark);

            // The lid: hinged at the back top edge; a box with an arch (a half cylinder lying along X) on it.
            lid = new GameObject("Lid").transform;
            lid.SetParent(root, false);
            lid.localPosition = new Vector3(0f, h, -d * 0.5f);
            var lidBody = new GameObject("LidBody").transform;
            lidBody.SetParent(lid, false);
            lidBody.localPosition = new Vector3(0f, 0f, d * 0.5f);
            Shapes.Rounded("LidBase", lidBody, new Vector3(0f, lidH * 0.18f, 0f), new Vector3(w, lidH * 0.36f, d), 0.015f, woodB);
            var arch = Shapes.Primitive(PrimitiveType.Cylinder, "Arch", lidBody, new Vector3(0f, lidH * 0.36f, 0f), new Vector3(lidH * 1.2f, w * 0.5f, d), woodA).transform;
            arch.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float sx in new[] { -1f, 1f })
            {
                var cap = Shapes.Primitive(PrimitiveType.Cylinder, "ArchRim", lidBody, new Vector3(sx * (w * 0.5f - 0.03f), lidH * 0.36f, 0f), new Vector3(lidH * 1.2f + 0.03f, 0.035f, d + 0.03f), metal).transform;
                cap.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // The strap over the middle: down the lid's front and the body's front.
            Shapes.Rounded("StrapBody", body, new Vector3(0f, h * 0.5f, d * 0.5f + 0.012f), new Vector3(w * 0.16f, h, 0.02f), 0.008f, strap);
            Shapes.Rounded("StrapBack", body, new Vector3(0f, h * 0.5f, -d * 0.5f - 0.012f), new Vector3(w * 0.16f, h, 0.02f), 0.008f, strap);
            var strapArc = Shapes.Primitive(PrimitiveType.Cylinder, "StrapLid", lidBody, new Vector3(0f, lidH * 0.36f, 0f), new Vector3(lidH * 1.2f + 0.035f, w * 0.08f, d + 0.035f), strap).transform;
            strapArc.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Shapes.Rounded("StrapLidFront", lidBody, new Vector3(0f, lidH * 0.18f, d * 0.5f + 0.012f), new Vector3(w * 0.16f, lidH * 0.36f, 0.02f), 0.008f, strap);

            // The lock on the front.
            Shapes.Rounded("Lock", body, new Vector3(0f, h - 0.08f, d * 0.5f + 0.03f), new Vector3(0.13f, 0.16f, 0.03f), 0.02f, metal);
            Shapes.Rounded("Keyhole", body, new Vector3(0f, h - 0.09f, d * 0.5f + 0.047f), new Vector3(0.025f, 0.05f, 0.005f), 0.01f, woodDark);

            if (t >= ChestTier.Mid)
            {
                // Bronze (or gold) bands along the planks and studs along them.
                foreach (float y in new[] { 0.06f, h - 0.03f })
                    Shapes.Rounded("Band", body, new Vector3(0f, y, 0f), new Vector3(w + 0.02f, 0.035f, d + 0.02f), 0.01f, metal);
                foreach (float sx in new[] { -1f, 1f })
                    for (int i = 0; i < 4; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Stud", body, new Vector3(sx * (w * 0.18f + i * w * 0.075f), h * 0.5f, d * 0.5f + 0.008f), Vector3.one * 0.035f, metal);
                foreach (float sx in new[] { -1f, 1f })
                {
                    var plate = Shapes.Rounded("Plate", body, new Vector3(sx * w * 0.3f, h * 0.5f, d * 0.5f + 0.01f), new Vector3(0.1f, 0.1f, 0.015f), 0.01f, metal).transform;
                    plate.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
                Shapes.Rounded("LidBand", lidBody, new Vector3(0f, 0.012f, d * 0.5f + 0.008f), new Vector3(w + 0.02f, 0.03f, 0.02f), 0.01f, metal);
            }
            if (t == ChestTier.Advanced)
            {
                // Gold patterns: scrolls on the front and the lid, a jewel in the lock, gold feet.
                var jewel = MaterialFactory.Create(new Color(1f, 0.2f, 0.25f), new Color(1.6f, 0.2f, 0.25f));
                foreach (float sx in new[] { -1f, 1f })
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var s = Shapes.Rounded("Scroll", body, new Vector3(sx * (w * 0.24f + i * 0.06f), h * 0.22f + i * 0.05f, d * 0.5f + 0.012f), new Vector3(0.09f, 0.02f, 0.012f), 0.008f, metal).transform;
                        s.localRotation = Quaternion.Euler(0f, 0f, sx * (20f + i * 25f));
                    }
                    var ls = Shapes.Rounded("LidScroll", lidBody, new Vector3(sx * w * 0.28f, lidH * 0.62f, d * 0.42f), new Vector3(0.16f, 0.025f, 0.015f), 0.008f, metal).transform;
                    ls.localRotation = Quaternion.Euler(-35f, 0f, sx * 12f);
                    foreach (float sz in new[] { -1f, 1f })
                        Shapes.Primitive(PrimitiveType.Sphere, "Foot", body, new Vector3(sx * (w * 0.5f - 0.04f), 0.0f, sz * (d * 0.5f - 0.04f)), new Vector3(0.11f, 0.08f, 0.11f), metal);
                }
                Shapes.Primitive(PrimitiveType.Sphere, "Jewel", body, new Vector3(0f, h - 0.06f, d * 0.5f + 0.05f), new Vector3(0.06f, 0.06f, 0.03f), jewel);
                Shapes.Primitive(PrimitiveType.Sphere, "LidJewel", lidBody, new Vector3(0f, lidH * 0.95f, 0f), new Vector3(0.08f, 0.06f, 0.08f), jewel);
            }
            return root;
        }
    }
}
