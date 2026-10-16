using System.Collections.Generic;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Kingdom
{
    /// <summary>
    /// The kingdom's buildings, built from the game's soft boxes and the kingdom's cones: stone, timber, slate-blue roofs
    /// and blue banners (foi's colour). Each model stands on its own transform; the materials are shared and go with
    /// the mode.
    /// </summary>
    public class KmModels
    {
        private readonly List<Material> mats = new List<Material>();
        public readonly Material stone, stoneDark, wood, roof, gold, red, banner, dark, glass;

        public KmModels()
        {
            stone = M(new Color(0.8f, 0.76f, 0.68f));
            stoneDark = M(new Color(0.56f, 0.53f, 0.49f));
            wood = M(new Color(0.48f, 0.32f, 0.19f));
            roof = M(new Color(0.22f, 0.32f, 0.58f));
            gold = M(new Color(0.95f, 0.76f, 0.3f), 0.15f);
            red = M(new Color(0.72f, 0.14f, 0.16f));
            banner = M(new Color(0.22f, 0.45f, 0.95f), 0.1f);
            dark = M(new Color(0.18f, 0.16f, 0.17f));
            glass = M(new Color(1f, 0.8f, 0.45f), 0.9f);
        }

        private Material M(Color c, float glow = 0f) { var m = MaterialFactory.Create(c, c * glow); mats.Add(m); return m; }

        public void Destroy() { foreach (var m in mats) if (m != null) Object.Destroy(m); }

        private static GameObject Box(string name, Transform p, Vector3 at, Vector3 size, Material m, float round = 0.06f) => Shapes.Rounded(name, p, at, size, round, m);

        private static GameObject Cone(string name, Transform p, Vector3 at, float width, float height, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(width, height, width);
            go.AddComponent<MeshFilter>().sharedMesh = KmMeshes.Cone();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            return go;
        }

        /// <summary>A round tower: a stone drum, a ring of battlements, a pointed roof and a banner on top.</summary>
        private void Tower(Transform p, Vector3 at, float r, float h)
        {
            Shapes.Primitive(PrimitiveType.Cylinder, "Tower", p, at + Vector3.up * h * 0.5f, new Vector3(r * 2f, h * 0.5f, r * 2f), stone);
            Shapes.Primitive(PrimitiveType.Cylinder, "Ring", p, at + Vector3.up * (h + 0.15f), new Vector3(r * 2.3f, 0.15f, r * 2.3f), stoneDark);
            Cone("Roof", p, at + Vector3.up * (h + 0.3f), r * 2.5f, r * 2.4f, roof);
            Box("Pole", p, at + Vector3.up * (h + 0.3f + r * 2.4f + 0.5f), new Vector3(0.06f, 1f, 0.06f), dark, 0.02f);
            Box("Flag", p, at + new Vector3(0.3f, h + 0.3f + r * 2.4f + 0.75f, 0f), new Vector3(0.55f, 0.36f, 0.03f), banner, 0.01f);
            Box("Window", p, at + new Vector3(0f, h * 0.65f, -r - 0.01f), new Vector3(0.22f, 0.4f, 0.05f), glass, 0.02f);
        }

        /// <summary>
        /// The palace (the kingdom's heart, AoE's town centre): a walled square, a tall keep with a slate roof, a round
        /// tower at each corner, and in front, on a stepped dais under a red canopy, the throne. Returns the seat.
        /// </summary>
        public Transform Palace(Transform parent, Vector3 at)
        {
            var root = new GameObject("Palace").transform;
            root.SetParent(parent, false);
            root.localPosition = at;
            // The wall round the square, with a gate to the south.
            const float half = 6f, wallH = 2.4f;
            foreach (float side in new[] { -1f, 1f })
            {
                Box("WallE", root, new Vector3(side * half, wallH * 0.5f, 0f), new Vector3(0.9f, wallH, half * 2f), stone);
                Box("WallN", root, new Vector3(0f, wallH * 0.5f, half), new Vector3(half * 2f, wallH, 0.9f), stone);
                Box("WallS", root, new Vector3(side * (half * 0.5f + 1.1f), wallH * 0.5f, -half), new Vector3(half - 2.2f, wallH, 0.9f), stone);
            }
            for (int i = -5; i <= 5; i += 2)
            {
                Box("Merlon", root, new Vector3(i, wallH + 0.25f, half), new Vector3(0.6f, 0.5f, 1f), stoneDark);
                Box("Merlon", root, new Vector3(-half, wallH + 0.25f, i), new Vector3(1f, 0.5f, 0.6f), stoneDark);
                Box("Merlon", root, new Vector3(half, wallH + 0.25f, i), new Vector3(1f, 0.5f, 0.6f), stoneDark);
            }
            // The gatehouse.
            foreach (float side in new[] { -1f, 1f }) Box("GatePier", root, new Vector3(side * 1.6f, 1.7f, -half), new Vector3(1.1f, 3.4f, 1.3f), stone);
            Box("GateTop", root, new Vector3(0f, 3.2f, -half), new Vector3(4.3f, 0.9f, 1.3f), stoneDark);
            Box("Gate", root, new Vector3(0f, 1.1f, -half + 0.2f), new Vector3(2.1f, 2.2f, 0.12f), wood, 0.03f);
            // The keep.
            Box("Keep", root, new Vector3(0f, 3.6f, 1.2f), new Vector3(5f, 7.2f, 5f), stone, 0.1f);
            Box("KeepBand", root, new Vector3(0f, 7.3f, 1.2f), new Vector3(5.5f, 0.4f, 5.5f), stoneDark);
            Cone("KeepRoof", root, new Vector3(0f, 7.5f, 1.2f), 6.4f, 4.2f, roof);
            Box("Pole", root, new Vector3(0f, 12.3f, 1.2f), new Vector3(0.08f, 1.4f, 0.08f), dark, 0.02f);
            Box("Flag", root, new Vector3(0.45f, 12.65f, 1.2f), new Vector3(0.85f, 0.55f, 0.04f), banner, 0.01f);
            for (int i = -1; i <= 1; i++)
            {
                Box("Window", root, new Vector3(i * 1.4f, 4.6f, -1.32f), new Vector3(0.45f, 0.8f, 0.06f), glass, 0.02f);
                Box("Window", root, new Vector3(i * 1.4f, 2.2f, -1.32f), new Vector3(0.45f, 0.8f, 0.06f), glass, 0.02f);
            }
            // The corner towers.
            foreach (var c in new[] { new Vector3(-half, 0f, -half), new Vector3(half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(half, 0f, half) })
                Tower(root, c, 1.15f, 4.2f);
            // The courtyard floor.
            Box("Yard", root, new Vector3(0f, 0.04f, 0f), new Vector3(half * 2f - 0.6f, 0.08f, half * 2f - 0.6f), stoneDark, 0.02f);

            // The throne, before the gate: a stepped dais, a red carpet, a gold throne under a red canopy.
            var dais = new Vector3(0f, 0f, -half - 4.2f);
            Box("Step1", root, dais + new Vector3(0f, 0.15f, 0f), new Vector3(4.6f, 0.3f, 3.6f), stone);
            Box("Step2", root, dais + new Vector3(0f, 0.45f, 0.3f), new Vector3(3.6f, 0.3f, 2.6f), stone);
            Box("Carpet", root, dais + new Vector3(0f, 0.32f, -1.6f), new Vector3(1.4f, 0.04f, 1.2f), red, 0.01f);
            Box("Carpet", root, new Vector3(0f, 0.05f, -half - 1.6f), new Vector3(1.4f, 0.04f, 3.2f), red, 0.01f);
            Box("Seat", root, dais + new Vector3(0f, 0.85f, 0.5f), new Vector3(1.3f, 0.5f, 1.1f), gold);
            Box("Back", root, dais + new Vector3(0f, 1.75f, 1.0f), new Vector3(1.4f, 1.9f, 0.25f), gold);
            Box("Cushion", root, dais + new Vector3(0f, 1.12f, 0.45f), new Vector3(1.05f, 0.08f, 0.9f), red, 0.03f);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Arm", root, dais + new Vector3(side * 0.62f, 1.25f, 0.5f), new Vector3(0.18f, 0.35f, 1f), gold);
                Box("CanopyPost", root, dais + new Vector3(side * 1.6f, 1.9f, 1.1f), new Vector3(0.14f, 3.2f, 0.14f), wood, 0.03f);
                Box("CanopyPostF", root, dais + new Vector3(side * 1.6f, 1.75f, -0.4f), new Vector3(0.14f, 2.9f, 0.14f), wood, 0.03f);
                Box("Banner", root, dais + new Vector3(side * 2.6f, 1.6f, 0.6f), new Vector3(0.7f, 2.2f, 0.05f), banner, 0.01f);
                Box("BannerPole", root, dais + new Vector3(side * 2.6f, 1.8f, 0.66f), new Vector3(0.06f, 3.6f, 0.06f), dark, 0.02f);
            }
            Box("Canopy", root, dais + new Vector3(0f, 3.5f, 0.35f), new Vector3(3.6f, 0.14f, 2.2f), red, 0.04f).transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            var seat = new GameObject("ThroneSeat").transform;
            seat.SetParent(root, false);
            seat.localPosition = dais + new Vector3(0f, 1.16f, 0.4f);
            return seat;
        }
    }
}
