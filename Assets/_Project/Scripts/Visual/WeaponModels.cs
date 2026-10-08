using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The armory's weapons, built from parts with the grip at the origin and the business end up +Y (about 0.8 units
    /// long at scale 1): hammers (the workshop's five hammer looks), swords with a guard and a long blade, axes with a
    /// curved head, spiked maces and spears with a long shaft. The tier picks the material, from wood and iron up to
    /// crystal, gold and fire.
    /// </summary>
    public static class WeaponModels
    {
        public static Color Glow(int tier)
        {
            switch (tier)
            {
                case 1: return new Color(0.85f, 0.65f, 0.4f);
                case 2: return new Color(0.82f, 0.86f, 0.95f);
                case 3: return new Color(0.55f, 0.85f, 1f);
                case 4: return new Color(0.75f, 0.5f, 1f);
                default: return new Color(1f, 0.6f, 0.2f);
            }
        }

        private static Material Metal(int tier)
        {
            switch (tier)
            {
                case 1: return MaterialFactory.Create(new Color(0.55f, 0.52f, 0.5f), Color.black);
                case 2: return MaterialFactory.Create(new Color(0.78f, 0.8f, 0.86f), new Color(0.05f, 0.05f, 0.06f));
                case 3: return MaterialFactory.Create(new Color(0.55f, 0.85f, 1f), new Color(0.3f, 0.7f, 1.1f));
                case 4: return MaterialFactory.Create(new Color(0.78f, 0.55f, 1f), new Color(0.7f, 0.35f, 1.4f));
                default: return MaterialFactory.Create(new Color(1f, 0.62f, 0.2f), new Color(2.2f, 0.9f, 0.2f));
            }
        }

        public static Transform Build(Transform parent, WeaponDef w, float scale = 1f)
        {
            if (w.kind == WeaponKind.Hammer) return HammerModels.Build(parent, Mathf.Clamp(w.tier, 1, 5), scale);
            var root = new GameObject(w.id).transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;
            var wood = MaterialFactory.Create(new Color(0.45f, 0.28f, 0.16f), Color.black);
            var grip = MaterialFactory.Create(new Color(0.2f, 0.15f, 0.12f), Color.black);
            var gold = MaterialFactory.Create(new Color(1f, 0.8f, 0.3f), new Color(0.3f, 0.2f, 0.02f));
            var metal = Metal(w.tier);
            switch (w.kind)
            {
                case WeaponKind.Sword:
                    Shapes.Primitive(PrimitiveType.Sphere, "Pommel", root, new Vector3(0f, -0.05f, 0f), Vector3.one * 0.07f, gold);
                    Shapes.Rounded("Grip", root, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.18f, 0.05f), 0.02f, grip);
                    Shapes.Rounded("Guard", root, new Vector3(0f, 0.16f, 0f), new Vector3(0.26f, 0.04f, 0.07f), 0.02f, gold);
                    Shapes.Rounded("Blade", root, new Vector3(0f, 0.48f, 0f), new Vector3(0.09f, 0.6f, 0.025f), 0.01f, metal);
                    Shapes.Rounded("Tip", root, new Vector3(0f, 0.8f, 0f), new Vector3(0.06f, 0.08f, 0.022f), 0.03f, metal)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    Shapes.Rounded("Fuller", root, new Vector3(0f, 0.46f, 0.013f), new Vector3(0.02f, 0.5f, 0.004f), 0.004f, grip);
                    break;
                case WeaponKind.Axe:
                    Shapes.Rounded("Haft", root, new Vector3(0f, 0.33f, 0f), new Vector3(0.05f, 0.72f, 0.05f), 0.02f, wood);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Head", root, new Vector3(0.1f, 0.62f, 0f), new Vector3(0.26f, 0.02f, 0.26f), metal)
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Shapes.Rounded("Socket", root, new Vector3(0f, 0.62f, 0f), new Vector3(0.09f, 0.12f, 0.08f), 0.02f, grip);
                    if (w.tier >= 3)
                        Shapes.Primitive(PrimitiveType.Cylinder, "Back", root, new Vector3(-0.08f, 0.62f, 0f), new Vector3(0.16f, 0.02f, 0.16f), metal)
                            .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case WeaponKind.Mace:
                    Shapes.Rounded("Haft", root, new Vector3(0f, 0.28f, 0f), new Vector3(0.05f, 0.6f, 0.05f), 0.02f, grip);
                    Shapes.Primitive(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 0.62f, 0f), Vector3.one * 0.2f, metal);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var dir = new Vector3(Mathf.Cos(a), (i % 2) * 0.6f - 0.3f, Mathf.Sin(a)).normalized;
                        Shapes.Primitive(PrimitiveType.Cylinder, "Spike", root, new Vector3(0f, 0.62f, 0f) + dir * 0.12f, new Vector3(0.035f, 0.05f, 0.035f), gold)
                            .transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                    }
                    Shapes.Primitive(PrimitiveType.Sphere, "Top", root, new Vector3(0f, 0.76f, 0f), Vector3.one * 0.06f, gold);
                    break;
                default: // spear
                    Shapes.Rounded("Shaft", root, new Vector3(0f, 0.36f, 0f), new Vector3(0.04f, 0.95f, 0.04f), 0.015f, wood);
                    Shapes.Rounded("Collar", root, new Vector3(0f, 0.84f, 0f), new Vector3(0.07f, 0.05f, 0.07f), 0.02f, gold);
                    Shapes.Rounded("Point", root, new Vector3(0f, 0.95f, 0f), new Vector3(0.08f, 0.2f, 0.03f), 0.04f, metal)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    if (w.tier >= 3)
                        foreach (float s in new[] { -1f, 1f })
                            Shapes.Rounded("Wing", root, new Vector3(s * 0.06f, 0.88f, 0f), new Vector3(0.06f, 0.03f, 0.02f), 0.01f, metal)
                                .transform.localRotation = Quaternion.Euler(0f, 0f, s * 30f);
                    break;
            }
            return root;
        }
    }

    /// <summary>The equipped weapon in the robot's hand on the floor: a rest pose, and a quick chop when it strikes.</summary>
    public class HeldWeapon : MonoBehaviour
    {
        private float swing = 1f;

        public static HeldWeapon Attach(Transform robotVisual, WeaponDef w)
        {
            var pivot = new GameObject("HeldWeapon").transform;
            pivot.SetParent(robotVisual, false);
            pivot.localPosition = new Vector3(0.28f, 0.2f, 0.04f);
            var h = pivot.gameObject.AddComponent<HeldWeapon>();
            WeaponModels.Build(pivot, w, w.kind == WeaponKind.Spear ? 0.62f : 0.72f);
            h.Pose(0f);
            return h;
        }

        public void Swing() => swing = 0f;

        private void Pose(float a) => transform.localRotation = Quaternion.Euler(25f + a, 0f, -12f);

        private void Update()
        {
            if (swing >= 1f) { Pose(Mathf.Sin(Time.time * 2f) * 3f); return; }
            swing = Mathf.Min(1f, swing + Time.deltaTime / 0.24f);
            // Wind back, chop down hard, settle.
            float a = swing < 0.3f ? Mathf.Lerp(0f, -70f, swing / 0.3f) : swing < 0.55f ? Mathf.Lerp(-70f, 115f, (swing - 0.3f) / 0.25f) : Mathf.Lerp(115f, 0f, (swing - 0.55f) / 0.45f);
            Pose(a);
        }
    }
}
