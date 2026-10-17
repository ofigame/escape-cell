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
            var root = BuildPlain(parent, w, w.Special ? scale * w.size : scale);
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

    /// <summary>The equipped weapon in the robot's hand on the floor: a rest pose, and a quick chop when it strikes.</summary>
    public class HeldWeapon : MonoBehaviour
    {
        private float swing = 1f;

        public static HeldWeapon Attach(Transform robotVisual, WeaponDef w)
        {
            var pivot = new GameObject("HeldWeapon").transform;
            pivot.SetParent(robotVisual, false);
            pivot.localPosition = new Vector3(0.32f, 0.22f, 0.06f);
            var h = pivot.gameObject.AddComponent<HeldWeapon>();
            WeaponModels.Build(pivot, w, w.kind == WeaponKind.Spear ? 0.95f : w.kind == WeaponKind.Gun ? 1.6f : 1.15f); // big enough to read on the floor
            h.isGun = w.kind == WeaponKind.Gun;
            if (h.isGun) pivot.localPosition = new Vector3(0.3f, 0.32f, 0.14f); // held out ahead, at the hip
            h.Pose(0f);
            return h;
        }

        private bool isGun;

        public void Swing()
        {
            swing = 0f;
            fromRight = !fromRight; // each blow the other way: right to left, then left to right
        }

        private bool fromRight;

        private float spin = 1f;
        private Vector3 restPosition;
        private bool rested;

        /// <summary>A whirl: the weapon sweeps a full circle round the robot.</summary>
        public void Spin() => spin = 0f;

        private void Pose(float a) => transform.localRotation = isGun ? Quaternion.Euler(-a * 0.25f, 0f, 0f) : Quaternion.Euler(25f + a, 0f, -12f);

        private void Update()
        {
            if (!rested) { restPosition = transform.localPosition; rested = true; }
            if (spin < 1f)
            {
                spin = Mathf.Min(1f, spin + Time.deltaTime / 0.32f);
                float ang = spin * 360f;
                transform.localPosition = Quaternion.Euler(0f, ang, 0f) * restPosition;
                transform.localRotation = Quaternion.Euler(0f, ang, 0f) * Quaternion.Euler(80f, 0f, -12f);
                if (spin >= 1f) transform.localPosition = restPosition;
                return;
            }
            if (swing >= 1f) { Pose(Mathf.Sin(Time.time * 2f) * 3f); return; }
            if (isGun)
            {
                // A shot: the gun kicks up and back, and settles.
                swing = Mathf.Min(1f, swing + Time.deltaTime / 0.16f);
                float kick = Mathf.Sin(swing * Mathf.PI);
                transform.localPosition = restPosition - Vector3.forward * 0.06f * kick;
                Pose(kick * 60f);
                return;
            }
            swing = Mathf.Min(1f, swing + Time.deltaTime / 0.26f);
            // A sweep across the front: the hand draws back to one side, slashes across to the other (the blade near level,
            // reaching out ahead), then comes back to rest. Every other blow the other way round.
            float side = fromRight ? 1f : -1f;
            float yaw;
            if (swing < 0.2f) yaw = Mathf.Lerp(0f, 80f * side, swing / 0.2f);
            else if (swing < 0.6f) yaw = Mathf.Lerp(80f * side, -85f * side, Mathf.SmoothStep(0f, 1f, (swing - 0.2f) / 0.4f));
            else yaw = Mathf.Lerp(-85f * side, 0f, (swing - 0.6f) / 0.4f);
            float reach = Mathf.Sin(Mathf.Clamp01((swing - 0.1f) / 0.6f) * Mathf.PI); // out ahead in the middle of the sweep
            var arc = Quaternion.Euler(0f, yaw, 0f);
            transform.localPosition = Vector3.Lerp(restPosition, arc * new Vector3(0f, restPosition.y + 0.05f, 0.34f) + Vector3.right * restPosition.x * 0.3f, reach);
            transform.localRotation = Quaternion.Slerp(Quaternion.Euler(25f, 0f, -12f), arc * Quaternion.Euler(78f, 0f, -18f * side), reach);
            if (swing >= 1f) transform.localPosition = restPosition;
        }
    }
}
