using System;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Combat;
using UnityEngine;
using WeaponFamily = SquashBot.Combat.WeaponFamily;
using Random = UnityEngine.Random;

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
                case 6: return new Color(0.45f, 1f, 0.95f); // utopian plasma
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
                case 6: return MaterialFactory.Create(new Color(0.8f, 1f, 0.97f), new Color(0.6f, 2.4f, 2.2f));
                default: return MaterialFactory.Create(new Color(1f, 0.62f, 0.2f), new Color(2.2f, 0.9f, 0.2f));
            }
        }

        /// <summary>A rank weapon's colour: its effect's (fire, ice, lightning, earth, royal gold, sun).</summary>
        public static Color FxColour(SpecialFx fx)
        {
            switch (fx)
            {
                case SpecialFx.Burn: return new Color(1f, 0.45f, 0.15f);
                case SpecialFx.Freeze: return new Color(0.5f, 0.85f, 1f);
                case SpecialFx.Chain: return new Color(0.75f, 0.6f, 1f);
                case SpecialFx.Quake: return new Color(0.95f, 0.7f, 0.35f);
                case SpecialFx.Cleave: return new Color(1f, 0.85f, 0.3f);
                case SpecialFx.Pierce: return new Color(1f, 0.95f, 0.55f);
                default: return Glow(6);
            }
        }

        /// <summary>The glow of a weapon's blows: a rank weapon's effect colour, else its tier's.</summary>
        public static Color GlowOf(WeaponDef w) => w.chest ? w.tint : w.Special ? FxColour(w.fx) : Glow(w.tier);

        /// <summary>A weapon's model; a rank weapon is bigger, made of its effect's glowing metal, with a glowing orb set in it.</summary>
        public static Transform Build(Transform parent, WeaponDef w, float scale = 1f)
        {
            // a weapon made as a model wears it (no orb or trim added: its picture has its own)
            if (GearFiles.HasWeapon(w.id))
            {
                float big = w.Special ? Mathf.Min(1.3f, w.size * 0.8f) : 1f;
                // a hammer or mace grows with its tier (0.85 the mallet .. 1.05 the nova mace)
                bool heavy = w.kind == WeaponKind.Hammer || w.kind == WeaponKind.Mace;
                var made = GearFiles.Weapon(parent, w.id, (heavy ? 0.8f + 0.05f * Mathf.Clamp(w.tier, 1, 5) : 0.9f) * big, w.Special ? 0.18f : 0f);
                made.localScale = Vector3.one * scale;
                return made;
            }
            // a drawn hammer or mace stays near the made ones' size (a little bigger at its higher tier)
            float grow = w.kind == WeaponKind.Hammer || w.kind == WeaponKind.Mace ? Mathf.Min(w.size, 1.25f) : w.size;
            var root = BuildPlain(parent, w, w.Special ? scale * grow : scale);
            if (!w.Special) return root;
            if (w.chest) { ChestTrim(root, w); return root; }
            var c = FxColour(w.fx);
            var orbMat = MaterialFactory.Create(c, c * 3f);
            float y = w.kind == WeaponKind.Gun ? 0.14f : w.kind == WeaponKind.Spear ? 1.05f : 0.2f;
            Shapes.Primitive(PrimitiveType.Sphere, "Orb", root, new Vector3(0f, y, w.kind == WeaponKind.Gun ? 0.1f : 0.04f), Vector3.one * 0.1f, orbMat);
            return root;
        }

        private static Transform BuildPlain(Transform parent, WeaponDef w, float scale)
        {
            if (w.kind == WeaponKind.Hammer && !w.chest) return HammerModels.Build(parent, Mathf.Clamp(w.tier, 1, 5), scale);
            var root = new GameObject(w.id).transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;
            var wood = MaterialFactory.Create(new Color(0.45f, 0.28f, 0.16f), Color.black);
            var grip = MaterialFactory.Create(new Color(0.2f, 0.15f, 0.12f), Color.black);
            var gold = MaterialFactory.Create(new Color(1f, 0.8f, 0.3f), new Color(0.3f, 0.2f, 0.02f));
            var metal = w.chest ? MaterialFactory.CreateSurface(w.tint, w.tint * 0.9f, 0.9f, 0.55f)
                : w.Special ? MaterialFactory.Create(FxColour(w.fx), FxColour(w.fx) * 1.6f) : Metal(w.tier);
            if (w.chest) gold = MaterialFactory.CreateSurface(w.trim, w.trim * 0.35f, 0.85f, 0.8f);
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
                case WeaponKind.Whirl:
                    // A twin blade: a short grip with a curved blade at each end (it is spun, not swung).
                    Shapes.Rounded("Grip", root, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.5f, 0.05f), 0.02f, grip);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var end = new Vector3(0f, 0.3f + s * 0.27f, 0f);
                        Shapes.Rounded("Collar", root, end, new Vector3(0.08f, 0.04f, 0.08f), 0.02f, gold);
                        Shapes.Rounded("Blade", root, end + new Vector3(0.08f * s, s * 0.12f, 0f), new Vector3(0.22f, 0.07f, 0.02f), 0.03f, metal)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                        Shapes.Rounded("Edge", root, end + new Vector3(-0.06f * s, s * 0.16f, 0f), new Vector3(0.14f, 0.05f, 0.02f), 0.02f, metal)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
                    }
                    break;
                case WeaponKind.Gun:
                {
                    // A chunky toy blaster pointing forward (+z): grip, body, barrel, a glowing muzzle and power cell.
                    var glow = MaterialFactory.Create(Glow(w.tier), Glow(w.tier) * 2.2f);
                    Shapes.Rounded("Grip", root, new Vector3(0f, -0.08f, -0.04f), new Vector3(0.07f, 0.16f, 0.08f), 0.02f, grip);
                    Shapes.Rounded("Body", root, new Vector3(0f, 0.02f, 0.06f), new Vector3(0.1f, 0.11f, 0.28f), 0.03f, metal);
                    Shapes.Rounded("Barrel", root, new Vector3(0f, 0.035f, 0.27f), new Vector3(0.055f, 0.055f, 0.2f), 0.02f, grip);
                    if (w.id == "twinBlaster")
                        Shapes.Rounded("Barrel2", root, new Vector3(0f, -0.025f, 0.25f), new Vector3(0.05f, 0.05f, 0.18f), 0.02f, grip);
                    Shapes.Rounded("Muzzle", root, new Vector3(0f, 0.035f, 0.38f), new Vector3(0.07f, 0.07f, 0.04f), 0.02f, glow);
                    Shapes.Rounded("Cell", root, new Vector3(0f, 0.09f, 0.04f), new Vector3(0.05f, 0.04f, 0.12f), 0.015f, glow);
                    if (w.tier >= 4) Shapes.Rounded("Fin", root, new Vector3(0f, 0.1f, 0.18f), new Vector3(0.02f, 0.05f, 0.14f), 0.01f, gold);
                    break;
                }
                case WeaponKind.Hammer: // (a chest maul: a crowned head of shining metal)
                    Shapes.Rounded("Grip", root, new Vector3(0f, 0.2f, 0f), new Vector3(0.08f, 0.46f, 0.08f), 0.035f, grip);
                    Shapes.Rounded("Collar", root, new Vector3(0f, 0.44f, 0f), new Vector3(0.13f, 0.06f, 0.13f), 0.02f, gold);
                    Shapes.Rounded("Head", root, new Vector3(0f, 0.58f, 0f), new Vector3(0.46f, 0.22f, 0.24f), 0.05f, metal);
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Face", root, new Vector3(s * 0.25f, 0.58f, 0f), new Vector3(0.05f, 0.27f, 0.28f), 0.02f, gold);
                    for (int i = 0; i < 5; i++)
                        Shapes.Rounded("Point", root, new Vector3(-0.16f + i * 0.08f, 0.72f, 0f), new Vector3(0.04f, 0.07f, 0.04f), 0.015f, gold)
                            .transform.localRotation = Quaternion.Euler(0f, 45f, 45f);
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
            Details(root, w, wood, grip, gold, metal);
            return root;
        }

        /// <summary>
        /// A chest weapon's finery: raised patterns in its trim colour along the blade (or round the head), gems set in
        /// it that glow, and small glints of light, so it shines in the hand.
        /// </summary>
        private static void ChestTrim(Transform root, WeaponDef w)
        {
            var trim = MaterialFactory.CreateSurface(w.trim, w.trim * 0.45f, 0.85f, 0.8f);
            var gemC = w.gem;
            var gem = MaterialFactory.Create(gemC, gemC * 3.2f);
            var glint = MaterialFactory.Create(Color.white, new Color(3f, 2.9f, 2.6f));
            switch (w.kind)
            {
                case WeaponKind.Sword:
                    // Raised diamonds down both faces of the blade, wings on the guard, a gem in its middle and the pommel.
                    for (int i = 0; i < 5; i++)
                        foreach (float z in new[] { -1f, 1f })
                            Shapes.Rounded("Emboss", root, new Vector3(0f, 0.27f + i * 0.11f, z * 0.014f), new Vector3(0.045f - i * 0.004f, 0.045f - i * 0.004f, 0.008f), 0.006f, trim)
                                .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Wing", root, new Vector3(s * 0.16f, 0.19f, 0f), new Vector3(0.1f, 0.035f, 0.06f), 0.015f, trim)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, s * 28f);
                    foreach (float z in new[] { -1f, 1f })
                        Shapes.Primitive(PrimitiveType.Sphere, "Gem", root, new Vector3(0f, 0.16f, z * 0.035f), new Vector3(0.06f, 0.06f, 0.03f), gem);
                    Shapes.Primitive(PrimitiveType.Sphere, "Gem", root, new Vector3(0f, -0.06f, 0f), Vector3.one * 0.06f, gem);
                    Shapes.Primitive(PrimitiveType.Sphere, "Glint", root, new Vector3(0.02f, 0.62f, 0.02f), Vector3.one * 0.035f, glint);
                    Shapes.Primitive(PrimitiveType.Sphere, "Glint", root, new Vector3(-0.015f, 0.4f, 0.02f), Vector3.one * 0.025f, glint);
                    break;
                case WeaponKind.Mace:
                    // Bands round the head, a ring of gems between them, a gem on top.
                    foreach (float y in new[] { 0.56f, 0.68f })
                        Shapes.Primitive(PrimitiveType.Cylinder, "Band", root, new Vector3(0f, y, 0f), new Vector3(0.215f, 0.012f, 0.215f), trim);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        Shapes.Primitive(PrimitiveType.Sphere, "Gem", root, new Vector3(Mathf.Cos(a) * 0.1f, 0.62f, Mathf.Sin(a) * 0.1f), Vector3.one * 0.04f, gem);
                    }
                    Shapes.Primitive(PrimitiveType.Sphere, "Gem", root, new Vector3(0f, 0.8f, 0f), Vector3.one * 0.07f, gem);
                    Shapes.Rounded("Grip", root, new Vector3(0f, 0.08f, 0f), new Vector3(0.065f, 0.05f, 0.065f), 0.02f, trim);
                    Shapes.Primitive(PrimitiveType.Sphere, "Glint", root, new Vector3(0.07f, 0.7f, 0.06f), Vector3.one * 0.03f, glint);
                    break;
                case WeaponKind.Hammer:
                    // Gems in both faces and down the head's side, a glint on it.
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Primitive(PrimitiveType.Sphere, "Gem", root, new Vector3(s * 0.28f, 0.58f, 0f), new Vector3(0.03f, 0.1f, 0.1f), gem);
                    foreach (float z in new[] { -1f, 1f })
                    {
                        Shapes.Primitive(PrimitiveType.Sphere, "Gem", root, new Vector3(0f, 0.58f, z * 0.122f), new Vector3(0.08f, 0.08f, 0.03f), gem);
                        Shapes.Rounded("Emboss", root, new Vector3(0f, 0.58f, z * 0.121f), new Vector3(0.36f, 0.025f, 0.01f), 0.008f, trim);
                    }
                    Shapes.Primitive(PrimitiveType.Sphere, "Glint", root, new Vector3(0.12f, 0.66f, 0.12f), Vector3.one * 0.03f, glint);
                    break;
            }
        }

        /// <summary>The newer weapons' own touches on top of their kind's build.</summary>
        private static void Details(Transform root, WeaponDef w, Material wood, Material grip, Material gold, Material metal)
        {
            switch (w.id)
            {
                case "woodSword":
                    foreach (var r in root.GetComponentsInChildren<Renderer>()) if (r.name == "Blade" || r.name == "Tip") r.sharedMaterial = wood;
                    break;
                case "katana":
                    // A long thin curved blade and a round guard.
                    foreach (Transform c in root) if (c.name == "Blade" || c.name == "Tip" || c.name == "Fuller" || c.name == "Guard") c.gameObject.SetActive(false);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Tsuba", root, new Vector3(0f, 0.16f, 0f), new Vector3(0.14f, 0.012f, 0.14f), gold);
                    for (int i = 0; i < 5; i++)
                        Shapes.Rounded("Blade", root, new Vector3(-0.012f * i * i, 0.24f + i * 0.14f, 0f), new Vector3(0.05f, 0.16f, 0.018f), 0.008f, metal)
                            .transform.localRotation = Quaternion.Euler(0f, 0f, 3f * i);
                    break;
                case "spikedClub":
                    foreach (var r in root.GetComponentsInChildren<Renderer>()) if (r.name == "Head" || r.name == "Haft") r.sharedMaterial = wood;
                    break;
                case "trident":
                    foreach (Transform c in root) if (c.name == "Point") c.gameObject.SetActive(false);
                    foreach (float s in new[] { -1f, 0f, 1f })
                        Shapes.Rounded("Prong", root, new Vector3(s * 0.07f, 0.96f - Mathf.Abs(s) * 0.03f, 0f), new Vector3(0.025f, 0.2f, 0.025f), 0.01f, metal);
                    Shapes.Rounded("Bar", root, new Vector3(0f, 0.86f, 0f), new Vector3(0.17f, 0.03f, 0.03f), 0.01f, metal);
                    break;
                case "halberd":
                    Shapes.Primitive(PrimitiveType.Cylinder, "AxeBlade", root, new Vector3(0.09f, 0.78f, 0f), new Vector3(0.2f, 0.015f, 0.2f), metal)
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Shapes.Rounded("Hook", root, new Vector3(-0.06f, 0.8f, 0f), new Vector3(0.08f, 0.03f, 0.02f), 0.01f, metal);
                    break;
                case "bubbleSmg":
                case "ionRifle":
                case "photonSmg":
                    // An automatic: a longer body, a magazine under it and a stock behind.
                    Shapes.Rounded("Magazine", root, new Vector3(0f, -0.09f, 0.12f), new Vector3(0.05f, 0.14f, 0.06f), 0.015f, grip);
                    Shapes.Rounded("Stock", root, new Vector3(0f, 0f, -0.12f), new Vector3(0.06f, 0.08f, 0.14f), 0.02f, metal);
                    break;
                case "railSniper":
                    // A long rail barrel and a scope.
                    Shapes.Rounded("Rail", root, new Vector3(0f, 0.035f, 0.45f), new Vector3(0.04f, 0.04f, 0.5f), 0.012f, metal);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Scope", root, new Vector3(0f, 0.12f, 0.1f), new Vector3(0.05f, 0.1f, 0.05f), grip)
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case "gatling":
                    // A ring of six barrels.
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        Shapes.Rounded("Barrel" + i, root, new Vector3(Mathf.Cos(a) * 0.045f, 0.035f + Mathf.Sin(a) * 0.045f, 0.34f), new Vector3(0.025f, 0.025f, 0.3f), 0.008f, grip);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// The equipped weapon in the robot's hand: a rest pose, and its family's blows in turn (the duel's four-blow chain:
    /// a sword slashes across and back, comes down, thrusts; a heavy weapon comes down from the shoulder ...). Each swing
    /// has its wind-up, contact and recovery from its <see cref="AttackMotionData"/>, runs on the fighter's picture clock
    /// (frozen by a hit, slowed in a big moment) and marks its phases (<see cref="PhaseReached"/>) as an animation's
    /// events would.
    /// </summary>
    [DefaultExecutionOrder(-50)] // (held off the bodies before the body's arm reaches for it)
    public class HeldWeapon : MonoBehaviour
    {
        /// <summary>A swing reached a phase (every weapon's): the weapon, the phase.</summary>
        public static event Action<HeldWeapon, SwingPhase> PhaseReached;
        /// <summary>This weapon's swing reached a phase.</summary>
        public event Action<SwingPhase> Phase;

        private float swing = 1f;
        private WeaponFamily family = WeaponFamily.Heavy;
        private float cooldown = 0.3f;
        private int chain;
        private float lastSwingAt = -10f;
        private AttackMotionData current;
        private float swingTime, swingTotal = 0.34f, playRate = 1f;
        private float windS, hitS, backS;
        private int phaseDone = 5;

        public static HeldWeapon Attach(Transform robotVisual, WeaponDef w)
        {
            var pivot = new GameObject("HeldWeapon").transform;
            pivot.SetParent(robotVisual, false); // (the body's grip keeps it in the hand: it goes where the body's picture is pushed or lunges)
            pivot.localPosition = new Vector3(0.32f, 0.22f, 0.06f);
            var h = pivot.gameObject.AddComponent<HeldWeapon>();
            WeaponModels.Build(pivot, w, w.kind == WeaponKind.Spear ? 0.95f : w.kind == WeaponKind.Gun ? 1.6f : 1.15f); // big enough to read on the floor
            h.isGun = w.kind == WeaponKind.Gun;
            h.family = CombatLibrary.FamilyOf(w.kind);
            h.cooldown = Mathf.Max(0.05f, w.cooldown);
            if (h.isGun) pivot.localPosition = new Vector3(0.3f, 0.32f, 0.14f); // held out ahead, at the hip
            else h.MakeTrail(w);
            h.Pose(0f);
            return h;
        }

        /// <summary>The weapon's business end (its tip, along its length): where its trail is drawn from.</summary>
        public Transform Tip { get; private set; }

        /// <summary>Its blow is live now (between its trail's start and end: the hit box open).</summary>
        public bool Striking => current != null && swing < 1f && phaseDone >= (int)SwingPhase.TrailStart && phaseDone < (int)SwingPhase.TrailEnd;
        private TrailRenderer trail;
        private float trailWidth, trailTime;

        /// <summary>
        /// The tip (at the far end of the weapon's length as built: grip at the origin, the length up +Y) and a trail on it,
        /// drawn only from the swing's trail start to its end: thin and long for a sword, short and thick for an axe or a
        /// heavy weapon; a rank or chest weapon in its own glow, others in the family's colour.
        /// </summary>
        private void MakeTrail(WeaponDef w)
        {
            float top = 0.6f;
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var bb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? bb.min.x : bb.max.x, (i & 2) == 0 ? bb.min.y : bb.max.y, (i & 4) == 0 ? bb.min.z : bb.max.z);
                    top = Mathf.Max(top, transform.InverseTransformPoint(mf.transform.TransformPoint(c)).y);
                }
            }
            Tip = new GameObject("Tip").transform;
            Tip.SetParent(transform, false);
            Tip.localPosition = new Vector3(0f, top * 0.94f, 0f);
            var profile = CombatLibrary.Family(family);
            var colour = w.Special || w.chest ? WeaponModels.GlowOf(w) : profile.trail;
            trail = Tip.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Weather.AddMaterial;
            trail.emitting = false;
            trailTime = family == WeaponFamily.Sword ? 0.12f : family == WeaponFamily.Spear ? 0.12f : family == WeaponFamily.Axe ? 0.16f : 0.18f;
            trail.time = trailTime;
            trailWidth = family == WeaponFamily.Sword ? 0.07f : family == WeaponFamily.Spear ? 0.09f : family == WeaponFamily.Axe ? 0.17f : 0.22f;
            trail.minVertexDistance = 0.02f;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(colour, Color.white, 0.55f), 0f), new GradientColorKey(colour, 0.4f), new GradientColorKey(colour, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
        }

        /// <summary>The trail on or off (as wide as the weapon is big on screen now).</summary>
        private void TrailOn(bool on)
        {
            if (trail == null) return;
            if (on)
            {
                // deeper into a combo, a thicker trail; the low quality setting a shorter one
                float combo = 1f + 0.15f * Mathf.Clamp(Combo - 2, 0, 4);
                trail.widthMultiplier = trailWidth * combo * Mathf.Max(0.2f, transform.lossyScale.y);
                trail.time = trailTime * (GraphicsQuality.Current == GraphicsTier.Low ? 0.6f : 1f);
                trail.Clear();
            }
            trail.emitting = on;
        }

        private bool isGun;

        /// <summary>The weapon's family (how it swings).</summary>
        public WeaponFamily Family => family;

        /// <summary>The blow now swinging (null: at rest).</summary>
        public AttackMotionData Current => swing < 1f ? current : null;

        /// <summary>How far through its swing (0 wound up from rest .. 0.3 wound up, 0.3 .. 0.55 the strike, .. 0.7
        /// followed through, .. 1 back at rest).</summary>
        public float Progress => swing;

        /// <summary>The whirl's turn now (degrees), or -1 when not whirling: the body turns with it.</summary>
        public float SpinAngle => spin < 1f ? spin * 360f * spinTurns : -1f;

        /// <summary>Seconds (on the picture clock) until this swing's contact; -1 when no swing is on its way there.</summary>
        public float ContactIn
        {
            get
            {
                if (swing >= 1f || current == null || phaseDone >= (int)SwingPhase.Contact) return -1f;
                float left = (windS + hitS * 0.5f) - swingTime;
                return left > 0f ? left / Mathf.Max(0.05f, playRate) : -1f;
            }
        }

        /// <summary>From a swing's start to its contact (seconds), for the next blow in the chain: a bot starts its swing that
        /// much before its blow lands, so the contact meets it.</summary>
        public float LeadSeconds
        {
            get
            {
                var next = NextBlow(false);
                if (next == null) return 0f;
                var (w, h, _, rate) = Timing(next);
                return (w + h * 0.5f) / rate;
            }
        }

        /// <summary>The next blow in the family's chain (back to the first after a pause as long as the combo's).</summary>
        public void Swing() => Swing(NextBlow(true));

        /// <summary>A given blow (its phases, direction and feel).</summary>
        public void Swing(AttackMotionData blow) { Begin(blow, false); Lunge(); }

        /// <summary>
        /// The next blow for a blow already landed this instant (the player's tap: the rules strike at once): its whole
        /// path still played (drawn back, then through), the wind-up quickened so the contact comes within
        /// <paramref name="contactWithin"/> seconds of the tap; the recovery as designed.
        /// </summary>
        public void SwingQuick(float contactWithin = 0.02f, bool exact = false) => SwingQuick(NextBlow(true), contactWithin, exact);

        /// <summary>A given blow landing now (a combo move, a flying strike): its wind-up quickened to meet the moment.</summary>
        public void SwingQuick(AttackMotionData blow, float contactWithin, bool exact = false)
        {
            Begin(blow, false);
            if (current == null) return;
            float fits = Mathf.Max(0.008f, contactWithin * playRate - hitS * 0.5f);
            windS = exact ? fits : Mathf.Min(windS, fits); // (exact: the wind-up drawn over the whole wait, the arm well back before it comes)
            swingTotal = windS + hitS + backS;
            Lunge();
        }

        /// <summary>The swing bounces back (off a shield, knocked aside): no follow-through, straight into its recovery.</summary>
        public void Recoil()
        {
            if (swing >= 1f || current == null) return;
            Mark(SwingPhase.Contact);
            swingTime = Mathf.Max(swingTime, windS + hitS + backS * 0.3f);
            TrailOn(false);
        }

        /// <summary>The next blow seen late (a live rival's, known only once struck): from its contact on, no wind-up.</summary>
        public void SwingLate() => Begin(NextBlow(true), true);

        /// <summary>The next blow, its contact in <paramref name="seconds"/> (picture time): started part-way through its
        /// wind-up when the blow comes sooner than a whole wind-up (a bot's quick flurry).</summary>
        public void SwingToContact(float seconds)
        {
            Begin(NextBlow(true), false);
            if (current == null) return;
            float toContact = windS + hitS * 0.5f;
            swingTime = Mathf.Clamp(toContact - Mathf.Max(0f, seconds) * playRate, 0f, toContact);
            Lunge();
        }

        // the hold a move asks for over the rest: straight out to the side (the whirlwind), raised over the head (the
        // quake, the chain lightning)
        private int holdPose;

        /// <summary>The weapon held straight out to the side (the whirlwind's spin), or back at rest.</summary>
        public void HoldOut(bool on) => holdPose = on ? 1 : holdPose == 1 ? 0 : holdPose;

        /// <summary>The weapon raised over the head (the quake, the chain lightning), or back at rest.</summary>
        public void HoldUp(bool on) => holdPose = on ? 2 : holdPose == 2 ? 0 : holdPose;

        /// <summary>The weapon's trail on or off (the whirlwind's ring from its tip).</summary>
        public void Trail(bool on) => TrailOn(on);

        private bool HeldPose()
        {
            if (holdPose == 0) return false;
            Vector3 p; Quaternion r;
            if (holdPose == 1) { p = new Vector3(0.78f, 0.42f, 0.0f); r = Quaternion.Euler(0f, 0f, -90f); } // (its length out along the arm)
            else { p = new Vector3(0.18f, 0.82f, 0.02f); r = Quaternion.Euler(-15f, 0f, 0f); } // (straight up, a little back)
            float k = Mathf.Min(1f, VisualTime.DeltaFor(this) * 14f);
            transform.localPosition = Vector3.Lerp(transform.localPosition, p, k);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, r, k);
            return true;
        }

        /// <summary>The fighter's visual this weapon is held by (the body's grip puts the weapon under a Grip_ holder on it).</summary>
        private Transform Fighter => transform.parent != null && transform.parent.name.StartsWith("Grip_") ? transform.parent.parent : transform.parent;


        /// <summary>Who this weapon's blows are aimed at, for its lunge (the duel sets it: the rival's model, or foi's;
        /// none: no lunge).</summary>
        public static Func<HeldWeapon, Transform> TargetOf;

        /// <summary>The most the struck one leans into the blow (a small give, not a step into the striker).</summary>
        private const float MaxCatchLean = 0.12f;

        /// <summary>
        /// The design's contact rule (2.1): the striker's picture slides toward its target as the blow winds up, so the
        /// weapon touches the body at contact, holds through the strike and comes home in the recovery; the target leans
        /// into it a little just before. Pictures only: the places, the rules' and the server's, never move.
        /// </summary>
        private void Lunge()
        {
            if (current == null) return;
            var target = TargetOf != null ? TargetOf(this) : null;
            float rate0 = Mathf.Max(0.1f, playRate);
            var body = Fighter != null ? Fighter.GetComponentInChildren<SkinnedHero>() : null;
            float untilContact = Mathf.Max(0f, (windS + hitS * 0.5f - swingTime) / rate0);
            if (body != null && SkinnedHero.ChainByClips && IsChainBlow(current))
            {
                // the plain chain by the animation map, by the weapon's mastery (looks only, the rules' section 5): the cut
                // and the thrust; from bodyBlowMastery a third, the family's body blow; from finishMastery the last the
                // family's finishing blow; the contact frame on this swing's
                var map = DuelAnimationMap.Current;
                int length = MasteryLevel >= map.bodyBlowMastery ? 3 : 2;
                int step = Mathf.Max(0, chain - 1) % length;
                bool last = step == length - 1 && length == 3;
                if (last && MasteryLevel >= map.finishMastery && body.PlayChainAction(FinishOf(family), untilContact)) { }
                else if (step == 2)
                {
                    var bodyBlow = BodyBlowOf(family);
                    body.StrikeWithBody(bodyBlow, Mathf.Max(0f, untilContact - hitS * 0.5f / rate0), hitS / rate0, backS / rate0, target != null ? target.position : transform.position + transform.forward);
                }
                else body.PlayChainAction(step == 0 ? DuelAction.ChainSlash : DuelAction.ChainStab, untilContact);
            }
            else if (current.body != BodyStrike.Weapon && body != null)
            {
                // the body's own blow (the chain's third): its pose timed to this swing's contact, from now
                body.StrikeWithBody(current.body, Mathf.Max(0f, untilContact - hitS * 0.5f / rate0), hitS / rate0, backS / rate0, target != null ? target.position : transform.position + transform.forward);
            }
            if (target == null) return;
            var me = CombatFeel.RootOf(Fighter);
            if (me == null) return;
            var dir = target.position - me.position; dir.y = 0f;
            float offset = CombatLibrary.ContactOffset(family, current);
            if (offset <= 0f || dir.sqrMagnitude < 1e-4f) return;
            // never into the other: the two bodies keep at least MinBodyGap between their middles at the contact (the
            // target's lean in included)
            var feelNow = CombatLibrary.Settings;
            float leanNow = Mathf.Min(feelNow != null ? feelNow.catchOffset : 0.3f, MaxCatchLean);
            offset = VisualRoot.KeptApart(me.position, target.position, offset, leanNow);
            if (offset <= 0.01f) return;
            float rate = Mathf.Max(0.1f, playRate);
            float toContact = Mathf.Max(0.02f, (windS + hitS * 0.5f - swingTime) / rate);
            float hold = hitS * 0.5f / rate + 0.05f, back = Mathf.Max(0.08f, backS * 0.8f / rate);
            VisualRoot.On(me).Lunge(dir, offset, toContact, hold, back);
            float lean = leanNow;
            var them = CombatFeel.RootOf(target);
            if (them != null && lean > 0f) VisualRoot.On(them).Lunge(-dir, lean, toContact, 0.02f, 0.12f);
        }



        /// <summary>The wielder's mastery of this weapon (0 .. 10; the game sets it: the player's, the rival's card's): how rich
        /// its plain chain looks.</summary>
        public int MasteryLevel
        {
            get => mastery;
            set { mastery = value; GlowTrail(); }
        }
        private int mastery;
        private bool glowing;

        /// <summary>The family's finishing blow in the mastery chain.</summary>
        private static DuelAction FinishOf(WeaponFamily f) =>
            f == WeaponFamily.Sword ? DuelAction.ChainFinishSword : f == WeaponFamily.Spear ? DuelAction.ChainFinishSpear : f == WeaponFamily.Axe ? DuelAction.ChainFinishAxe : DuelAction.ChainFinishHeavy;

        /// <summary>The family's body blow (its chain's own: the sword's heel kick, the spear's front kick, the axe's
        /// shoulder, the heavy weapon's headbutt).</summary>
        private BodyStrike BodyBlowOf(WeaponFamily f)
        {
            var chainOf = CombatLibrary.Family(f).chain;
            if (chainOf != null) foreach (var b in chainOf) if (b != null && b.body != BodyStrike.Weapon) return b.body;
            return f == WeaponFamily.Sword ? BodyStrike.HeelKick : f == WeaponFamily.Spear ? BodyStrike.FrontKick : f == WeaponFamily.Axe ? BodyStrike.Shoulder : BodyStrike.Headbutt;
        }

        /// <summary>A master's trail (the golden weapon's mastery): gold, brighter.</summary>
        private void GlowTrail()
        {
            if (trail == null || glowing || mastery < DuelAnimationMap.Current.glowMastery) return;
            glowing = true;
            var gold = new Color(1f, 0.82f, 0.3f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(gold, Color.white, 0.6f), 0f), new GradientColorKey(gold, 0.4f), new GradientColorKey(gold, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.5f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trailWidth *= 1.25f;
        }

        /// <summary>A blow of the family's plain chain (not its POWER HIT, KNOCKDOWN or flying blows).</summary>
        private bool IsChainBlow(AttackMotionData b)
        {
            var chainOf = CombatLibrary.Family(family).chain;
            if (chainOf == null || b == null) return false;
            foreach (var x in chainOf) if (x == b) return true;
            return false;
        }

        private AttackMotionData NextBlow(bool advance)
        {
            var profile = CombatLibrary.Family(family);
            if (Time.time - lastSwingAt > 1.4f) chain = 0; // (the chain starts over after a pause: the combo's own gap)
            var b = profile.Blow(chain);
            if (advance) { chain++; lastSwingAt = Time.time; }
            return b;
        }

        /// <summary>
        /// The duel's swings drawn slower than the design's frames, for the eye to follow them (the user's word: too quick
        /// to read): the wind-up and the strike this much longer, never sped up past their own pace. Pictures only: the
        /// rules' moments (the tap, the bot's blow) never move; the swing is started early enough to meet them.
        /// </summary>
        private const float DuelSlow = 1.6f, DuelSlowBack = 1.25f;

        /// <summary>A blow's wind-up, strike and recovery (seconds) and its play rate, as drawn here (slower in a duel).</summary>
        private (float wind, float hit, float back, float rate) Timing(AttackMotionData b)
        {
            float rate = RateFor(b);
            float w = b.anticipationMs / 1000f, h = b.activeMs / 1000f, k = b.recoveryMs / 1000f;
            if (DuelStance) { rate = Mathf.Min(rate, 1f); w *= DuelSlow; h *= DuelSlow; k *= DuelSlowBack; }
            return (w, h, k, rate);
        }

        /// <summary>How fast a blow plays to fit the weapon's pace (the design's 0.7 .. 1.6; outside it the blow does not fit).</summary>
        private float RateFor(AttackMotionData b)
        {
            float rate = b.TotalSeconds > cooldown ? b.TotalSeconds / cooldown : 1f;
            if (rate > 1.6f || rate < 0.7f) Debug.LogWarning("[Combat] blow " + b.name + " does not fit the weapon's pace (rate " + rate.ToString("0.00") + ")");
            return Mathf.Clamp(rate, 0.7f, 1.6f);
        }

        private void Begin(AttackMotionData blow, bool late)
        {
            current = blow;
            swing = 0f;
            swingTime = 0f;
            phaseDone = -1;
            if (blow != null)
            {
                (windS, hitS, backS, playRate) = Timing(blow);
                stroke = StrokeOf(blow.direction);
                fromRight = blow.direction != HitDirection.Horizontal_LeftToRight;
                if (blow.direction == HitDirection.Spin) Spin();
            }
            else
            {
                windS = 0.102f; hitS = 0.085f; backS = 0.153f; playRate = 1f;
                stroke = (stroke + 1) % 4; // the blows in turn: down from overhead, across from the right, a slant, across from the left
                fromRight = stroke == 1;
            }
            swingTotal = windS + hitS + backS;
            Mark(SwingPhase.WindupStart);
            if (late) { swingTime = windS; Mark(SwingPhase.TrailStart); }
        }

        private static int StrokeOf(HitDirection d) =>
            d == HitDirection.Overhead ? 0 : d == HitDirection.Horizontal_RightToLeft ? 1 : d == HitDirection.Upward ? 2 : d == HitDirection.Thrust ? 4 : 3;

        private void Mark(SwingPhase p)
        {
            if ((int)p <= phaseDone) return;
            phaseDone = (int)p;
            if (p == SwingPhase.TrailStart && current != null)
            {
                if (current.enableTrail) TrailOn(true);
                AudioManager.PlaySfx(current.whooshSfx, 0.45f, current.whooshPitch, 0.08f); // (the family's swing, a little different each time)
            }
            if (p == SwingPhase.TrailEnd || p == SwingPhase.WindupStart) TrailOn(false);
            Phase?.Invoke(p);
            PhaseReached?.Invoke(this, p);
        }

        private int stroke = 3;

        /// <summary>The last blow came down from overhead (it lands on the head; the others on the body).</summary>
        public bool Overhead => stroke == 0;

        /// <summary>A blow in three keys (wound up, the strike, followed through), then back to rest.</summary>
        private void Stroke(Vector3 upP, Vector3 upR, Vector3 hitP, Vector3 hitR, Vector3 outP, Vector3 outR)
        {
            Vector3 p; Quaternion r;
            var rest = Quaternion.Euler(25f, 0f, -12f);
            if (swing < 0.3f) { float k = Mathf.SmoothStep(0f, 1f, swing / 0.3f); p = Vector3.Lerp(restPosition, upP, k); r = Quaternion.Slerp(rest, Quaternion.Euler(upR), k); }
            else if (swing < 0.55f) { float k = Mathf.SmoothStep(0f, 1f, (swing - 0.3f) / 0.25f); p = Vector3.Lerp(upP, hitP, k); r = Quaternion.Slerp(Quaternion.Euler(upR), Quaternion.Euler(hitR), k); }
            else if (swing < 0.7f) { float k = (swing - 0.55f) / 0.15f; p = Vector3.Lerp(hitP, outP, k); r = Quaternion.Slerp(Quaternion.Euler(hitR), Quaternion.Euler(outR), k); }
            else { float k = Mathf.SmoothStep(0f, 1f, (swing - 0.7f) / 0.3f); p = Vector3.Lerp(outP, restPosition, k); r = Quaternion.Slerp(Quaternion.Euler(outR), rest, k); }
            transform.localPosition = p;
            transform.localRotation = r;
        }

        private bool fromRight;

        private float spin = 1f;
        private Vector3 restPosition;
        private bool rested;

        /// <summary>A whirl: the weapon sweeps a full circle round the robot.</summary>
        public void Spin() { spin = 0f; spinTurns = 1; }

        /// <summary>A whirl of several turns (the axe's POWER HIT: two full turns), the body turning with it.</summary>
        public void Spin(int turns) { spin = 0f; spinTurns = Mathf.Max(1, turns); }

        private int spinTurns = 1;

        /// <summary>The body's clip swings it now (the fighter's mapped move): held still in the fist, its blow's phases
        /// (contact, trail, sound) kept. Set by the body each frame.</summary>
        public bool BodyDriven { get; set; }

        /// <summary>The duel's stance (set by the game while a duel is on): the family's own rest, a twirl now and then.</summary>
        public static bool DuelStance;
        /// <summary>The blows in a row now (the duel's combo): from three the trail grows thicker, from four the weapon sparkles.</summary>
        public int Combo { get; set; }
        private float idleSince, twirlAt = -1f;

        /// <summary>The family's rest in a duel: a sword held up and ready (bouncing a little), a spear levelled across,
        /// an axe on the shoulder, a heavy weapon held up at its side.</summary>
        private bool DuelRest()
        {
            if (!DuelStance || isGun) return false;
            if (twirlAt < 0f) twirlAt = Time.time + Random.Range(5f, 8f);
            if (Time.time >= twirlAt && Time.time - idleSince > 1.5f) { twirlAt = Time.time + Random.Range(5f, 8f); spin = 0f; spinTurns = 1; return false; } // (a twirl now and then)
            float bob = Mathf.Sin(Time.time * 6f) * 0.02f;
            Vector3 p; Quaternion r;
            switch (family)
            {
                case WeaponFamily.Sword: p = restPosition + new Vector3(-0.04f, 0.1f + bob, 0.1f); r = Quaternion.Euler(-15f, 0f, -18f); break;
                case WeaponFamily.Spear: p = restPosition + new Vector3(-0.08f, 0.08f, 0.12f); r = Quaternion.Euler(70f, -20f, 0f); break;
                case WeaponFamily.Axe: p = new Vector3(0.22f, 0.58f, -0.1f); r = Quaternion.Euler(-60f, 0f, -20f); break;
                default: p = restPosition + new Vector3(-0.03f, 0.08f, 0.12f); r = Quaternion.Euler(-28f, 0f, -14f); break; // (a mace or hammer held up at its side, its head high: never over the face, never into the floor)
            }
            transform.localPosition = Vector3.Lerp(transform.localPosition, p, Mathf.Min(1f, Time.deltaTime * 10f));
            transform.localRotation = Quaternion.Slerp(transform.localRotation, r, Mathf.Min(1f, Time.deltaTime * 10f));
            return true;
        }
        private float flourish = -1f;

        /// <summary>
        /// The winner's flourish (1.5 s), by family: a sword twirled and lowered as if sheathed, a heavy weapon swung up
        /// onto the shoulder, an axe driven into the ground in front, a spear twirled and its butt struck on the ground.
        /// </summary>
        public void Flourish() { flourish = 0f; swing = 1f; spin = 1f; TrailOn(false); }

        /// <summary>The flourish's pose now (false once it is over).</summary>
        private bool Flourishing(float dt)
        {
            if (flourish < 0f) return false;
            flourish += dt;
            const float T = 1.5f;
            if (flourish >= T + 0.6f) { flourish = -1f; transform.localPosition = restPosition; return false; }
            float k = Mathf.Clamp01(flourish / T);
            float twirl = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flourish / 0.6f));
            Vector3 p; Quaternion r;
            switch (family)
            {
                case WeaponFamily.Sword:
                    // twirled twice in the hand, then point down at the side (sheathed)
                    p = Vector3.Lerp(restPosition, new Vector3(0.36f, 0.12f, -0.02f), Mathf.SmoothStep(0f, 1f, (k - 0.4f) / 0.4f));
                    r = Quaternion.Euler(0f, 0f, twirl * 720f) * Quaternion.Slerp(Quaternion.Euler(25f, 0f, -12f), Quaternion.Euler(175f, 0f, -8f), Mathf.SmoothStep(0f, 1f, (k - 0.4f) / 0.4f));
                    break;
                case WeaponFamily.Heavy:
                    // swung up and laid back on the shoulder
                    p = Vector3.Lerp(restPosition, new Vector3(0.2f, 0.62f, -0.12f), Mathf.SmoothStep(0f, 1f, k / 0.5f));
                    r = Quaternion.Slerp(Quaternion.Euler(25f, 0f, -12f), Quaternion.Euler(-70f, 0f, -18f), Mathf.SmoothStep(0f, 1f, k / 0.5f));
                    break;
                case WeaponFamily.Axe:
                    // raised, then driven down into the ground in front, held there
                    float up = Mathf.SmoothStep(0f, 1f, k / 0.35f), down = Mathf.SmoothStep(0f, 1f, (k - 0.35f) / 0.2f);
                    p = Vector3.Lerp(Vector3.Lerp(restPosition, new Vector3(0.16f, 0.62f, 0.05f), up), new Vector3(0.12f, -0.12f, 0.5f), down);
                    r = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(25f, 0f, -12f), Quaternion.Euler(-40f, 0f, 0f), up), Quaternion.Euler(150f, 0f, 0f), down);
                    break;
                default:
                    // twirled once, then its butt struck on the ground, upright beside the body
                    p = Vector3.Lerp(restPosition, new Vector3(0.34f, -0.05f, 0.08f), Mathf.SmoothStep(0f, 1f, (k - 0.4f) / 0.3f));
                    r = Quaternion.Euler(0f, twirl * 360f, 0f) * Quaternion.Slerp(Quaternion.Euler(80f, 0f, 0f), Quaternion.Euler(0f, 0f, -4f), Mathf.SmoothStep(0f, 1f, (k - 0.4f) / 0.3f));
                    break;
            }
            transform.localPosition = p;
            transform.localRotation = r;
            return true;
        }

        private void Pose(float a) => transform.localRotation = isGun ? Quaternion.Euler(-a * 0.25f, 0f, 0f) : Quaternion.Euler(25f + a, 0f, -12f);

        /// <summary>The swing's seconds turned into the strokes' 0..1 (wind-up to 0.3, contact 0.3..0.55, follow-through
        /// to 0.7, back to rest to 1), marking the phases as it passes them.</summary>
        private void Advance(float dt)
        {
            swingTime += dt * playRate;
            float a = windS, h = hitS, f = backS * 0.3f, b = backS * 0.7f;
            float s = swingTime;
            if (s < a) swing = 0.3f * s / Mathf.Max(1e-4f, a);
            else if (s < a + h) swing = 0.3f + 0.25f * (s - a) / Mathf.Max(1e-4f, h);
            else if (s < a + h + f) swing = 0.55f + 0.15f * (s - a - h) / Mathf.Max(1e-4f, f);
            else swing = Mathf.Min(1f, 0.7f + 0.3f * (s - a - h - f) / Mathf.Max(1e-4f, b));
            if (s >= a * 0.6f) Mark(SwingPhase.TrailStart);
            if (s >= a + h * 0.5f) Mark(SwingPhase.Contact);
            if (s >= a + h + f) Mark(SwingPhase.TrailEnd);
            if (s >= a + h + backS * 0.5f) Mark(SwingPhase.Recoverable);
        }

        private SkinnedHero ownBody;

        /// <summary>
        /// The weapon never passes through a rival's body: if its tip is inside another fighter's body capsule, it is
        /// turned back round its grip, away from that body, a few degrees at a time, until the tip lies on the surface
        /// (the blow seen to land on the body and stop there).
        /// </summary>
        private void StopAtBodies()
        {
            if (Tip == null || isGun) return;
            if (ownBody == null && transform.parent != null) ownBody = transform.root.GetComponentInChildren<SkinnedHero>();
            var own = ownBody != null ? ownBody.transform.root : transform.root;
            for (int b = 0; b < SkinnedHero.Live.Count; b++)
            {
                var body = SkinnedHero.Live[b];
                if (body == null || body.transform.root == own) continue;
                body.BodyCapsule(out var bottom, out var top, out float radius);
                for (int step = 0; step < 14; step++)
                {
                    // the deepest of three points along the blade (its middle, its upper part, its tip) inside the body
                    var grip = transform.position;
                    var tipNow = Tip.position;
                    Vector3 tip = tipNow, onAxis = Vector3.zero, away = Vector3.zero;
                    float worst = float.MaxValue;
                    for (int s = 0; s < 3; s++)
                    {
                        var pt = Vector3.Lerp(grip, tipNow, s == 0 ? 0.55f : s == 1 ? 0.8f : 1f);
                        var on = ClosestOnSegment(bottom, top, pt);
                        float d = (pt - on).sqrMagnitude;
                        if (d < worst) { worst = d; tip = pt; onAxis = on; away = pt - on; }
                    }
                    if (worst >= radius * radius) break;
                    // turn the weapon round its grip so that point moves away from the body's axis
                    var arm = tip - grip;
                    var push = away.sqrMagnitude > 1e-6f ? away.normalized : (grip - onAxis).normalized;
                    var axis = Vector3.Cross(arm, push);
                    if (axis.sqrMagnitude < 1e-8f) axis = Vector3.up;
                    transform.RotateAround(grip, axis.normalized, 7f);
                }
            }
            OwnHead();
        }

        /// <summary>
        /// The blade kept out of its own bearer's head and hat (the big head and short arms: an overhead blow would pass
        /// through them): turned round its grip, the arm's arc a little wider, until it clears them.
        /// </summary>
        private void OwnHead()
        {
            if (ownBody == null) return;
            var face = ownBody.FaceRenderer;
            if (face == null) return;
            var hb = face.bounds;
            var head = hb.center;
            float r = Mathf.Max(hb.extents.x, hb.extents.z) * 0.95f;
            var hat = ownBody.HatMount != null && ownBody.HatMount.childCount > 0 ? ownBody.HatMount.position + Vector3.up * r * 0.35f : head;
            for (int step = 0; step < 10; step++)
            {
                var grip = transform.position;
                var tipNow = Tip.position;
                Vector3 worstPt = tipNow, worstC = head;
                float worst = float.MaxValue;
                for (int s = 0; s < 3; s++)
                {
                    var pt = Vector3.Lerp(grip, tipNow, s == 0 ? 0.55f : s == 1 ? 0.8f : 1f);
                    foreach (var c in new[] { head, hat })
                    {
                        float d = (pt - c).magnitude - r;
                        if (d < worst) { worst = d; worstPt = pt; worstC = c; }
                    }
                }
                if (worst >= 0f) break;
                var arm = worstPt - grip;
                var push = (worstPt - worstC).sqrMagnitude > 1e-6f ? (worstPt - worstC).normalized : Vector3.up;
                var axis = Vector3.Cross(arm, push);
                if (axis.sqrMagnitude < 1e-8f) axis = transform.right;
                transform.RotateAround(grip, axis.normalized, 6f);
            }
        }

        private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return a + ab * t;
        }

        private void LateUpdate()
        {
            if (swing < 1f) ReachBody();
            if (swing < 1f || spin < 1f) StopAtBodies();
            if (trembleLeft > 0f)
            {
                // the "not ready" tremble: a quick small shake of the weapon in the hand
                trembleLeft -= Time.deltaTime;
                float k = Mathf.Max(0f, trembleLeft) / TrembleSeconds;
                transform.localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(trembleLeft * 90f) * 7f * k);
            }
        }

        private const float TrembleSeconds = 0.22f;
        private float trembleLeft;

        /// <summary>A tap too early for a blow: the weapon shakes a little in the hand (no words, section 3.3).</summary>
        public void Tremble() => trembleLeft = TrembleSeconds;

        /// <summary>The body's grip has put the weapon in the hand (after this weapon's own frame): the blade turned onto
        /// its target and kept out of the bodies once more, from where the hand now holds it.</summary>
        public void AfterGrip()
        {
            if (swing < 1f) ReachBody();
            if (swing < 1f || spin < 1f) StopAtBodies();
        }

        /// <summary>How far round its grip the blade may be turned onto its target's body at contact (degrees).</summary>
        private const float ReachTurn = 75f;

        /// <summary>
        /// The contact rule's last inches (2.1): around the blow's contact (its strike, 0.3 .. 0.58 of the swing) the blade is
        /// turned round its grip onto the target's body, at most <see cref="ReachTurn"/> degrees: what
        /// the lunge leaves short, the arm makes up. (A blade already inside the body is set back by StopAtBodies.)
        /// </summary>
        private void ReachBody()
        {
            if (Tip == null || isGun || TargetOf == null || current == null || current.body != BodyStrike.Weapon) return;
            // full through the strike and its first follow-through (a strike is shorter than a frame: the contact frame's swing is
            // already past it), faded in from the wind-up's end and out before the recovery
            float w = swing < 0.3f ? (swing - 0.22f) / 0.08f : swing <= 0.58f ? 1f : 1f - (swing - 0.58f) / 0.1f;
            if (w <= 0f) return;
            var target = TargetOf(this);
            var body = target != null ? target.GetComponentInChildren<SkinnedHero>() : null;
            if (body == null) return;
            body.BodyCapsule(out var bottom, out var top, out float radius);
            var grip = transform.position;
            var tip = Tip.position;
            var onAxis = ClosestOnSegment(bottom, top, tip);
            var out_ = tip - onAxis;
            if (out_.magnitude <= radius) return; // (already on it)
            var surface = onAxis + (out_.sqrMagnitude > 1e-6f ? out_.normalized : (grip - onAxis).normalized) * radius * 0.85f;
            var arm = tip - grip; var want = surface - grip;
            if (arm.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-6f) return;
            float angle = Vector3.Angle(arm, want);
            if (angle > 150f) return; // (the blade pointing away: the blow is aimed elsewhere)
            var turn = Quaternion.FromToRotation(arm, want);
            turn.ToAngleAxis(out float deg, out var axis);
            transform.RotateAround(grip, axis, Mathf.Min(deg, ReachTurn) * Mathf.SmoothStep(0f, 1f, w));
        }

        private void Update()
        {
            if (!rested) { restPosition = transform.localPosition; rested = true; }
            float dt = VisualTime.DeltaFor(this);
            if (trail != null && trail.emitting) trail.time = trailTime * Mathf.Clamp(Time.deltaTime > 0f ? Time.deltaTime / Mathf.Max(1e-4f, dt) : 1f, 1f, 20f); // (slowed or frozen: the trail lasts as long on screen)
            if (BodyDriven) { spin = 1f; if (swing < 1f) Advance(dt); transform.localPosition = restPosition; Pose(0f); return; } // (the body's clip swings it: held still in the fist, its phases kept)
            if (spin < 1f)
            {
                bool whirlBlow = current != null && current.direction == HitDirection.Spin && swing < 1f;
                spin = Mathf.Min(1f, spin + dt / Mathf.Max(0.12f, whirlBlow ? swingTotal / playRate : 0.32f));
                if (swing < 1f) Advance(dt);
                // a spinning blow: the turn timed so the blade points at the rival at the contact (the contact rule)
                float atContact = whirlBlow && swingTotal > 1e-4f ? (windS + hitS * 0.5f) / swingTotal : 0f;
                float ang = (spin - atContact) * 360f * spinTurns;
                transform.localPosition = Quaternion.Euler(0f, ang, 0f) * restPosition;
                transform.localRotation = Quaternion.Euler(0f, ang, 0f) * Quaternion.Euler(80f, 0f, -12f);
                if (spin >= 1f) transform.localPosition = restPosition;
                return;
            }
            if (Flourishing(dt)) return;
            if (swing >= 1f)
            {
                if (HeldPose()) return; // (a move's hold)
                if (DuelRest()) return;
                Pose(Mathf.Sin(Time.time * 2f) * 3f);
                return;
            }
            idleSince = Time.time;
            if (isGun)
            {
                // A shot: the gun kicks up and back, and settles.
                swing = Mathf.Min(1f, swing + dt / 0.16f);
                float kick = Mathf.Sin(swing * Mathf.PI);
                transform.localPosition = restPosition - Vector3.forward * 0.06f * kick;
                Pose(kick * 60f);
                return;
            }
            Advance(dt);
            if (current != null && current.body != BodyStrike.Weapon)
            {
                // the body strikes (a kick, a shoulder, a headbutt): the weapon kept in hand at rest
                if (!DuelRest()) Pose(0f);
                return;
            }
            if (stroke == 0)
            {
                // Down from overhead: the blade raised high behind the head, brought down hard in front.
                Stroke(new Vector3(0.18f, 0.62f, -0.08f), new Vector3(-40f, 0f, -5f), new Vector3(0.06f, 0.3f, 0.42f), new Vector3(95f, 0f, 0f), new Vector3(0.04f, 0.14f, 0.36f), new Vector3(128f, 0f, 0f));
                if (swing >= 1f) transform.localPosition = restPosition;
                return;
            }
            if (stroke == 2)
            {
                // A slant: from high on the right down across to the low left.
                Stroke(new Vector3(0.42f, 0.58f, 0f), new Vector3(-15f, 20f, -55f), new Vector3(-0.05f, 0.26f, 0.42f), new Vector3(70f, -35f, 50f), new Vector3(-0.28f, 0.12f, 0.24f), new Vector3(100f, -60f, 75f));
                if (swing >= 1f) transform.localPosition = restPosition;
                return;
            }
            if (stroke == 4)
            {
                // A thrust: drawn back at the hip, driven straight out ahead (the point first), drawn back.
                Stroke(new Vector3(0.24f, 0.24f, -0.16f), new Vector3(80f, 0f, -8f), new Vector3(0.1f, 0.3f, 0.62f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.28f, 0.5f), new Vector3(88f, 0f, 0f));
                if (swing >= 1f) transform.localPosition = restPosition;
                return;
            }
            // A sweep across the front: the hand draws back to one side, slashes across to the other (the blade near level,
            // reaching out ahead), then comes back to rest. Every other blow the other way round.
            float side = fromRight ? 1f : -1f;
            float yaw;
            if (swing < 0.3f) yaw = Mathf.Lerp(0f, 80f * side, swing / 0.3f);
            else if (swing < 0.6f) yaw = Mathf.Lerp(80f * side, -85f * side, Mathf.SmoothStep(0f, 1f, (swing - 0.3f) / 0.3f));
            else yaw = Mathf.Lerp(-85f * side, 0f, (swing - 0.6f) / 0.4f);
            float reach = Mathf.Sin(Mathf.Clamp01((swing - 0.1f) / 0.6f) * Mathf.PI); // out ahead in the middle of the sweep
            var arc = Quaternion.Euler(0f, yaw, 0f);
            transform.localPosition = Vector3.Lerp(restPosition, arc * new Vector3(0f, restPosition.y + 0.05f, 0.34f) + Vector3.right * restPosition.x * 0.3f, reach);
            transform.localRotation = Quaternion.Slerp(Quaternion.Euler(25f, 0f, -12f), arc * Quaternion.Euler(78f, 0f, -18f * side), reach);
            if (swing >= 1f) transform.localPosition = restPosition;
        }
    }
}
