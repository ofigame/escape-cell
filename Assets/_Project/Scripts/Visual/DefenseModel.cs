using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The look of one of the player's defences, in foi's white and blue so they never pass for vanG's: an archer with
    /// a bow (on a corner tower), a cannon on a turntable (on a rampart), a turret tower (on the floor). Higher tiers
    /// add armour, metal and glow — the tier-3 tower carries a plasma crystal. <see cref="Aim"/> turns it (its turret)
    /// to a target, <see cref="Fire"/> kicks it back, <see cref="Muzzle"/> is where shots leave.
    /// </summary>
    public class DefenseModel : MonoBehaviour
    {
        private Transform turret, muzzle, bar, barFill;
        private Material barMat, glowMat;
        private float kick, idle;
        private Vector3 basePos;
        private bool baseSet;
        private Quaternion aimRot = Quaternion.identity;
        public DefenseKind Kind { get; private set; }
        public int Tier { get; private set; }

        public Vector3 Muzzle => muzzle != null ? muzzle.position : transform.position + Vector3.up;

        public static DefenseModel Build(DefenseKind kind, int tier, Transform parent)
        {
            var go = new GameObject("Defense " + kind);
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<DefenseModel>();
            m.Kind = kind;
            m.Tier = tier;
            m.idle = Random.value * 10f;
            var white = MaterialFactory.CreateSurface(new Color(0.9f, 0.92f, 0.96f), Color.black, 0.5f, 0.1f);
            var blue = MaterialFactory.CreateSurface(new Color(0.22f, 0.45f, 0.9f), Color.black, 0.45f, 0.15f);
            var dark = MaterialFactory.CreateSurface(new Color(0.16f, 0.18f, 0.24f), Color.black, 0.5f, 0.5f);
            var gold = MaterialFactory.CreateSurface(new Color(1f, 0.78f, 0.3f), new Color(0.3f, 0.2f, 0.05f), 0.75f, 0.85f);
            var wood = MaterialFactory.CreateSurface(new Color(0.55f, 0.36f, 0.2f), Color.black, 0.2f, 0f);
            m.glowMat = MaterialFactory.Create(new Color(0.5f, 0.9f, 1f), new Color(0.8f, 2.2f, 3f));
            switch (kind)
            {
                case DefenseKind.Archer: m.BuildArcher(tier, white, blue, dark, gold, wood); break;
                case DefenseKind.Cannon: m.BuildCannon(tier, white, blue, dark, gold, wood); break;
                default: m.BuildTower(tier, white, blue, dark, gold); break;
            }
            return m;
        }

        private void BuildArcher(int tier, Material white, Material blue, Material dark, Material gold, Material wood)
        {
            var t = transform;
            var cloth = tier == 0 ? wood : blue;
            turret = new GameObject("Body").transform;
            turret.SetParent(t, false);
            Shapes.Rounded("Legs", turret, new Vector3(0f, 0.18f, 0f), new Vector3(0.22f, 0.36f, 0.16f), 0.06f, dark);
            Shapes.Rounded("Tunic", turret, new Vector3(0f, 0.5f, 0f), new Vector3(0.3f, 0.34f, 0.2f), 0.08f, cloth);
            Shapes.Primitive(PrimitiveType.Sphere, "Head", turret, new Vector3(0f, 0.8f, 0f), Vector3.one * 0.22f, white);
            Shapes.Rounded("Visor", turret, new Vector3(0f, 0.81f, 0.1f), new Vector3(0.16f, 0.05f, 0.03f), 0.015f, glowMat);
            Shapes.Rounded("Helmet", turret, new Vector3(0f, 0.9f, -0.01f), new Vector3(0.24f, 0.08f, 0.24f), 0.04f, tier >= 3 ? gold : tier >= 1 ? white : wood);
            if (tier >= 2)
            {
                foreach (float s in new[] { -1f, 1f })
                    Shapes.Rounded("Pauldron", turret, new Vector3(s * 0.19f, 0.64f, 0f), new Vector3(0.12f, 0.08f, 0.18f), 0.04f, tier >= 3 ? gold : white);
                Shapes.Rounded("Cape", turret, new Vector3(0f, 0.5f, -0.12f), new Vector3(0.28f, 0.42f, 0.03f), 0.02f, blue);
            }
            Shapes.Rounded("Quiver", turret, new Vector3(0.09f, 0.6f, -0.13f), new Vector3(0.08f, 0.26f, 0.08f), 0.03f, wood);
            // The bow: an arc of three staves held out in front, a string of light.
            var bowMat = tier >= 3 ? gold : wood;
            var bow = new GameObject("Bow").transform;
            bow.SetParent(turret, false);
            bow.localPosition = new Vector3(-0.08f, 0.56f, 0.2f);
            Shapes.Rounded("Grip", bow, Vector3.zero, new Vector3(0.04f, 0.14f, 0.04f), 0.015f, bowMat);
            foreach (float s in new[] { -1f, 1f })
                Shapes.Rounded("Limb", bow, new Vector3(0f, s * 0.15f, -0.04f), new Vector3(0.035f, 0.18f, 0.035f), 0.015f, bowMat)
                    .transform.localRotation = Quaternion.Euler(-s * 25f, 0f, 0f);
            Shapes.Rounded("String", bow, new Vector3(0f, 0f, -0.1f), new Vector3(0.01f, 0.44f, 0.01f), 0.004f, tier >= 2 ? glowMat : white);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(bow, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.08f);
            t.localScale = Vector3.one * 2.1f;
        }

        private void BuildCannon(int tier, Material white, Material blue, Material dark, Material gold, Material wood)
        {
            var t = transform;
            Shapes.Primitive(PrimitiveType.Cylinder, "Turntable", t, new Vector3(0f, 0.04f, 0f), new Vector3(0.62f, 0.04f, 0.62f), dark);
            turret = new GameObject("Turret").transform;
            turret.SetParent(t, false);
            turret.localPosition = new Vector3(0f, 0.08f, 0f);
            Shapes.Rounded("Carriage", turret, new Vector3(0f, 0.12f, -0.04f), new Vector3(0.4f, 0.2f, 0.42f), 0.06f, tier >= 2 ? white : wood);
            foreach (float s in new[] { -1f, 1f })
                Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", turret, new Vector3(s * 0.23f, 0.12f, -0.02f), new Vector3(0.26f, 0.03f, 0.26f), dark)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var barrelMat = tier >= 3 ? gold : tier >= 2 ? blue : dark;
            int barrels = tier >= 3 ? 2 : 1;
            for (int b = 0; b < barrels; b++)
            {
                float x = barrels == 1 ? 0f : (b == 0 ? -0.09f : 0.09f);
                var barrel = Shapes.Primitive(PrimitiveType.Cylinder, "Barrel", turret, new Vector3(x, 0.3f, 0.16f), new Vector3(0.15f, 0.26f, 0.15f), barrelMat).transform;
                barrel.localRotation = Quaternion.Euler(70f, 0f, 0f);
                Shapes.Primitive(PrimitiveType.Cylinder, "Rim", turret, new Vector3(x, 0.38f, 0.4f), new Vector3(0.19f, 0.03f, 0.19f), tier >= 1 ? white : dark)
                    .transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            }
            if (tier >= 2) Shapes.Primitive(PrimitiveType.Sphere, "Core", turret, new Vector3(0f, 0.28f, -0.16f), Vector3.one * 0.12f, glowMat);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(turret, false);
            muzzle.localPosition = new Vector3(0f, 0.42f, 0.46f);
            t.localScale = Vector3.one * 2.0f;
        }

        private void BuildTower(int tier, Material white, Material blue, Material dark, Material gold)
        {
            var t = transform;
            Shapes.Rounded("Base", t, new Vector3(0f, 0.12f, 0f), new Vector3(0.82f, 0.24f, 0.82f), 0.12f, dark);
            Shapes.Rounded("Column", t, new Vector3(0f, 0.62f, 0f), new Vector3(0.44f, 0.8f, 0.44f), 0.16f, white);
            Shapes.Rounded("Band", t, new Vector3(0f, 0.75f, 0f), new Vector3(0.47f, 0.06f, 0.47f), 0.02f, glowMat);
            Shapes.Rounded("Collar", t, new Vector3(0f, 1.05f, 0f), new Vector3(0.6f, 0.12f, 0.6f), 0.05f, blue);
            turret = new GameObject("Turret").transform;
            turret.SetParent(t, false);
            turret.localPosition = new Vector3(0f, 1.15f, 0f);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(turret, false);
            if (tier >= 3)
            {
                // The plasma tower: a floating crystal between rings.
                Shapes.Rounded("Cradle", turret, new Vector3(0f, 0.08f, 0f), new Vector3(0.5f, 0.14f, 0.5f), 0.06f, gold);
                var crystal = Shapes.Rounded("Crystal", turret, new Vector3(0f, 0.5f, 0f), new Vector3(0.26f, 0.5f, 0.26f), 0.08f, glowMat).transform;
                crystal.localRotation = Quaternion.Euler(0f, 45f, 0f);
                foreach (float y in new[] { 0.32f, 0.68f })
                    Shapes.Primitive(PrimitiveType.Cylinder, "Ring", turret, new Vector3(0f, y, 0f), new Vector3(0.52f, 0.02f, 0.52f), gold);
                muzzle.localPosition = new Vector3(0f, 0.5f, 0f);
            }
            else if (tier >= 2)
            {
                // Tesla: stacked coils and a glowing ball on top.
                for (int i = 0; i < 3; i++)
                    Shapes.Primitive(PrimitiveType.Cylinder, "Coil", turret, new Vector3(0f, 0.1f + i * 0.13f, 0f), new Vector3(0.42f - i * 0.08f, 0.04f, 0.42f - i * 0.08f), i % 2 == 0 ? blue : white);
                Shapes.Primitive(PrimitiveType.Sphere, "Ball", turret, new Vector3(0f, 0.55f, 0f), Vector3.one * 0.24f, glowMat);
                muzzle.localPosition = new Vector3(0f, 0.55f, 0f);
            }
            else
            {
                // The gun turret: a domed head with a barrel.
                Shapes.Primitive(PrimitiveType.Sphere, "Dome", turret, new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.36f, 0.5f), white);
                Shapes.Rounded("Eye", turret, new Vector3(0f, 0.2f, 0.22f), new Vector3(0.18f, 0.06f, 0.04f), 0.02f, glowMat);
                Shapes.Primitive(PrimitiveType.Cylinder, "Barrel", turret, new Vector3(0f, 0.15f, 0.36f), new Vector3(0.1f, 0.18f, 0.1f), dark)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                muzzle.localPosition = new Vector3(0f, 0.15f, 0.55f);
            }
            t.localScale = Vector3.one * 1.7f;
        }

        /// <summary>Turns towards a point (its turret, or the archer's whole body).</summary>
        public void Aim(Vector3 target)
        {
            if (turret == null) return;
            var d = target - turret.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) aimRot = Quaternion.LookRotation(d);
        }

        public void Fire() => kick = 1f;

        /// <summary>A health bar over a floor tower (0..1); negative hides it.</summary>
        public void SetHealth(float k)
        {
            if (bar == null)
            {
                if (k < 0f) return;
                bar = new GameObject("HealthBar").transform;
                bar.SetParent(transform, false);
                bar.localPosition = new Vector3(0f, 2.05f, 0f);
                Shapes.Rounded("Back", bar, Vector3.zero, new Vector3(0.7f, 0.1f, 0.03f), 0.03f, MaterialFactory.Create(new Color(0.08f, 0.06f, 0.12f), Color.black));
                barMat = MaterialFactory.Create(new Color(0.45f, 0.9f, 1f), new Color(0.5f, 1.4f, 1.8f));
                barFill = new GameObject("Fill").transform;
                barFill.SetParent(bar, false);
                barFill.localPosition = new Vector3(-0.32f, 0f, -0.02f);
                Shapes.Rounded("Fill", barFill, new Vector3(0.32f, 0f, 0f), new Vector3(0.64f, 0.06f, 0.02f), 0.02f, barMat);
            }
            bar.gameObject.SetActive(k >= 0f && k < 0.999f);
            if (k < 0f) return;
            barFill.localScale = new Vector3(Mathf.Max(0.001f, Mathf.Clamp01(k)), 1f, 1f);
            var c = Color.Lerp(new Color(1f, 0.35f, 0.35f), new Color(0.45f, 0.9f, 1f), k);
            MaterialFactory.SetColors(barMat, c, c * 1.5f);
        }

        private void Update()
        {
            if (turret != null) turret.rotation = Quaternion.Slerp(turret.rotation, aimRot, Time.deltaTime * 8f);
            kick = Mathf.Max(0f, kick - Time.deltaTime * 5f);
            if (turret != null && Kind != DefenseKind.Archer)
            {
                if (!baseSet) { basePos = turret.localPosition; baseSet = true; }
                turret.localPosition = basePos + turret.parent.InverseTransformDirection(-turret.forward) * (kick * 0.08f); // recoil
            }
            float g = 0.8f + Mathf.Sin((Time.time + idle) * 3f) * 0.2f + kick;
            MaterialFactory.SetColors(glowMat, new Color(0.5f, 0.9f, 1f), new Color(0.8f, 2.2f, 3f) * g);
            if (bar != null)
            {
                var cam = Camera.main;
                if (cam != null) bar.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            }
        }
    }
}
