using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// What makes each named guardian and each of vanG's avatars of the scenario its own: props added to the basic
    /// monster or to vanG's screen. Floors without a named one keep the plain look.
    /// </summary>
    public static class GuardianLooks
    {
        private static Material M(string hex, Color glow = default)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return MaterialFactory.Create(c, glow);
        }

        /// <summary>The guardian (monster fight) of a floor: its body kind, its colour, and the props it gets.</summary>
        public static bool Guardian(int floor, out Monster.Kind kind, out Color tint)
        {
            kind = Monster.Kind.Golem;
            tint = Color.gray;
            switch (floor)
            {
                case 2: kind = Monster.Kind.Golem; tint = new Color(0.55f, 0.4f, 0.28f); return true;    // Kütükbaş
                case 5: kind = Monster.Kind.Slime; tint = new Color(0.82f, 0.62f, 0.4f); return true;    // Solucan Ana
                case 6: kind = Monster.Kind.Golem; tint = new Color(0.85f, 0.45f, 0.3f); return true;    // Fırtına Akrebi
                case 8: kind = Monster.Kind.Golem; tint = new Color(0.95f, 0.75f, 0.3f); return true;    // Liman Vinci
                case 11: kind = Monster.Kind.Slime; tint = new Color(0.2f, 0.22f, 0.3f); return true;    // Penguen Kral
                case 12: kind = Monster.Kind.Golem; tint = new Color(0.92f, 0.95f, 1f); return true;     // Çığ Yeti
                case 16: kind = Monster.Kind.Cyclops; tint = new Color(0.55f, 0.85f, 0.35f); return true; // Asit Örümceği
                case 17: kind = Monster.Kind.Slime; tint = new Color(0.6f, 0.78f, 0.95f); return true;   // Bulut Balinası
                case 18: kind = Monster.Kind.Golem; tint = new Color(0.9f, 0.55f, 0.6f); return true;    // Balon Kaptan
                case 21: kind = Monster.Kind.Slime; tint = new Color(0.4f, 0.95f, 0.85f); return true;   // Veri Yılanı
                case 22: kind = Monster.Kind.Cyclops; tint = new Color(0.62f, 0.5f, 0.9f); return true;  // Meteor Çobanı
            }
            return false;
        }

        /// <summary>Adds the floor's guardian props under <paramref name="body"/> (the monster's own body).</summary>
        public static void DressGuardian(Transform body, int floor)
        {
            switch (floor)
            {
                case 2: // Kütükbaş: a crown of leaves and moss, wood rings on the chest
                    foreach (var (x, z) in new[] { (-0.2f, 0f), (0.2f, 0.05f), (0f, -0.15f), (0f, 0.18f) })
                        Shapes.Primitive(PrimitiveType.Sphere, "Leaf", body, new Vector3(x, 1.38f, z), Vector3.one * 0.32f, M("#5FAE58"));
                    for (int i = 0; i < 3; i++)
                        Shapes.Primitive(PrimitiveType.Cylinder, "Ring", body, new Vector3(0f, 0.55f, 0.36f), new Vector3(0.2f + i * 0.14f, 0.005f, 0.2f + i * 0.14f), M("#3E2A1C"))
                            .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case 5: // Solucan Ana: ringed body segments trailing behind
                    for (int i = 0; i < 4; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Segment", body, new Vector3(0f, 0.3f + Mathf.Sin(i) * 0.1f, -0.6f - i * 0.35f), Vector3.one * (0.7f - i * 0.1f), M("#C98B4F"));
                    break;
                case 6: // Fırtına Akrebi: a curled tail with a glowing stinger
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 4f * Mathf.PI * 0.85f;
                        Shapes.Primitive(PrimitiveType.Sphere, "Tail", body, new Vector3(0f, 0.5f + Mathf.Sin(a) * 0.9f, -0.45f - Mathf.Cos(a) * 0.5f + 0.5f), Vector3.one * (0.3f - i * 0.03f), M("#B5583A"));
                    }
                    Shapes.Primitive(PrimitiveType.Sphere, "Stinger", body, new Vector3(0f, 1.45f, 0.2f), Vector3.one * 0.16f, M("#FFE07A", new Color(2.4f, 1.8f, 0.4f)));
                    break;
                case 8: // Liman Vinci: a crane arm with a hanging hook
                    Shapes.Rounded("Mast", body, new Vector3(0.45f, 1.2f, 0f), new Vector3(0.12f, 0.9f, 0.12f), 0.04f, M("#F2C14E"));
                    Shapes.Rounded("Arm", body, new Vector3(0.2f, 1.62f, 0.3f), new Vector3(0.12f, 0.12f, 0.9f), 0.04f, M("#F2C14E"));
                    Shapes.Rounded("Cable", body, new Vector3(0.2f, 1.3f, 0.7f), new Vector3(0.03f, 0.6f, 0.03f), 0.01f, M("#3A3A4A"));
                    Shapes.Rounded("Hook", body, new Vector3(0.2f, 0.95f, 0.7f), new Vector3(0.18f, 0.14f, 0.06f), 0.04f, M("#9AA0B0"));
                    break;
                case 11: // Penguen Kral: a white belly and a golden crown
                    Shapes.Primitive(PrimitiveType.Sphere, "Belly", body, new Vector3(0f, 0.42f, 0.22f), new Vector3(0.75f, 0.65f, 0.6f), M("#FFFFFF"));
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 2f / 5f;
                        Shapes.Rounded("Crown", body, new Vector3(Mathf.Cos(a) * 0.2f, 1.08f, Mathf.Sin(a) * 0.2f), new Vector3(0.08f, 0.18f, 0.08f), 0.02f, M("#FFC94A", new Color(1.8f, 1.2f, 0.3f)));
                    }
                    Shapes.Rounded("Beak", body, new Vector3(0f, 0.62f, 0.55f), new Vector3(0.18f, 0.08f, 0.16f), 0.03f, M("#FF9A3D"));
                    break;
                case 12: // Çığ Yeti: shaggy white fur spikes and icy horns
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.PI * 2f / 10f;
                        Shapes.Rounded("Fur", body, new Vector3(Mathf.Cos(a) * 0.48f, 0.7f + (i % 2) * 0.15f, Mathf.Sin(a) * 0.38f), new Vector3(0.12f, 0.3f, 0.12f), 0.05f, M("#F2F6FF"))
                            .transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Cos(a) * -30f);
                    }
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Horn", body, new Vector3(s * 0.25f, 1.38f, 0f), new Vector3(0.1f, 0.3f, 0.1f), 0.04f, M("#BDEBFF", new Color(0.6f, 1.4f, 2f)))
                            .transform.localRotation = Quaternion.Euler(0f, 0f, -s * 20f);
                    break;
                case 16: // Asit Örümceği: eight legs and dripping acid
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i / 8f) * Mathf.PI * 2f;
                        var leg = Shapes.Rounded("Leg", body, new Vector3(Mathf.Cos(a) * 0.6f, 0.4f, Mathf.Sin(a) * 0.6f), new Vector3(0.08f, 0.08f, 0.6f), 0.03f, M("#2E4A26")).transform;
                        leg.localRotation = Quaternion.Euler(30f, -a * Mathf.Rad2Deg + 90f, 0f);
                    }
                    Shapes.Primitive(PrimitiveType.Sphere, "Drip", body, new Vector3(0.1f, 0.15f, 0.45f), Vector3.one * 0.12f, M("#C8FF7A", new Color(1.2f, 2.4f, 0.4f)));
                    break;
                case 17: // Bulut Balinası: a tail fin and a water spout
                    Shapes.Rounded("Fin", body, new Vector3(0f, 0.5f, -0.75f), new Vector3(0.9f, 0.1f, 0.35f), 0.05f, M("#8FB8E8"));
                    for (int i = 0; i < 3; i++)
                        Shapes.Primitive(PrimitiveType.Sphere, "Spout", body, new Vector3(0f, 1.15f + i * 0.2f, 0f), Vector3.one * (0.2f - i * 0.04f), M("#E8F6FF", new Color(0.5f, 0.8f, 1.2f)));
                    break;
                case 18: // Balon Kaptan: a big balloon on ropes and a captain's hat
                    Shapes.Primitive(PrimitiveType.Sphere, "Balloon", body, new Vector3(0f, 2.2f, 0f), new Vector3(0.9f, 1.05f, 0.9f), M("#FF7A8A"));
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Rope", body, new Vector3(s * 0.25f, 1.55f, 0f), new Vector3(0.02f, 0.6f, 0.02f), 0.01f, M("#5A4A3A"));
                    Shapes.Rounded("Hat", body, new Vector3(0f, 1.32f, 0.05f), new Vector3(0.5f, 0.12f, 0.45f), 0.05f, M("#2A2E5A"));
                    break;
                case 21: // Veri Yılanı: neon segments and a data glow
                    for (int i = 0; i < 6; i++)
                        Shapes.Rounded("Segment", body, new Vector3(Mathf.Sin(i * 0.9f) * 0.3f, 0.25f, -0.55f - i * 0.28f), Vector3.one * (0.42f - i * 0.04f), 0.08f, M("#2F6A6E", new Color(0.4f, 1.8f, 1.6f)));
                    break;
                case 22: // Meteor Çobanı: rocks orbiting its head
                    var orbit = new GameObject("Orbit").transform;
                    orbit.SetParent(body, false);
                    orbit.localPosition = new Vector3(0f, 1.1f, 0f);
                    orbit.gameObject.AddComponent<Spinner>().speed = 70f;
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI / 2f;
                        Shapes.Rounded("Rock", orbit, new Vector3(Mathf.Cos(a) * 0.75f, 0f, Mathf.Sin(a) * 0.75f), Vector3.one * 0.2f, 0.08f, M("#5A4A3A", new Color(1f, 0.4f, 0.2f)));
                    }
                    break;
            }
        }

        /// <summary>Adds the avatar vanG takes for the floor's boss fight around its screen (<paramref name="body"/>).</summary>
        public static void DressAvatar(Transform body, int floor)
        {
            switch (floor)
            {
                case 1: // Pres Kolu: a piston press above the screen
                    Shapes.Rounded("Piston", body, new Vector3(0f, 1.1f, 0f), new Vector3(0.3f, 0.8f, 0.3f), 0.06f, M("#9AA0B0"));
                    Shapes.Rounded("Plate", body, new Vector3(0f, 0.72f, 0f), new Vector3(1.3f, 0.12f, 0.6f), 0.04f, M("#F2C14E"));
                    break;
                case 4: // Orman Biçici: a big saw blade beside the screen
                    var saw = Shapes.Primitive(PrimitiveType.Cylinder, "Saw", body, new Vector3(1.1f, 0f, 0f), new Vector3(0.9f, 0.03f, 0.9f), M("#C9CFE0")).transform;
                    saw.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    saw.gameObject.AddComponent<Spinner>().speed = 400f;
                    break;
                case 7: // Maden Matkabı: a drill under the screen
                    var drill = Shapes.Primitive(PrimitiveType.Cylinder, "Drill", body, new Vector3(0f, -0.9f, 0f), new Vector3(0.35f, 0.45f, 0.35f), M("#B5583A")).transform;
                    drill.gameObject.AddComponent<Spinner>().speed = 300f;
                    break;
                case 9: // Demir Ahtapot: iron tentacles hanging from the screen
                    for (int i = 0; i < 4; i++)
                        Shapes.Rounded("Tentacle", body, new Vector3(-0.6f + i * 0.4f, -0.9f, 0f), new Vector3(0.14f, 0.7f, 0.14f), 0.06f, M("#5A6488"))
                            .transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1.5f) * 12f);
                    break;
                case 10: // Soğutma Çekirdeği: frosty pipes on both sides
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Pipe", body, new Vector3(s * 1.05f, 0f, 0f), new Vector3(0.18f, 1.3f, 0.18f), 0.08f, M("#BDEBFF", new Color(0.4f, 1f, 1.4f)));
                    break;
                case 13: // Buz Kalbi: an ice crystal frame
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        Shapes.Rounded("Ice", body, new Vector3(Mathf.Cos(a) * 1.05f, Mathf.Sin(a) * 0.8f, 0.05f), new Vector3(0.14f, 0.45f, 0.14f), 0.05f, M("#DFF6FF", new Color(0.6f, 1.4f, 2f)))
                            .transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
                    }
                    break;
                case 15: // Ayna vanG: a silver mirror frame
                    Shapes.Rounded("Mirror", body, new Vector3(0f, 0f, 0.1f), new Vector3(2.1f, 1.6f, 0.08f), 0.2f, M("#DCE6F2", new Color(0.4f, 0.5f, 0.6f)));
                    break;
                case 19: // Fırtına Gözü: storm clouds around the screen
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        Shapes.Primitive(PrimitiveType.Sphere, "Cloud", body, new Vector3(Mathf.Cos(a) * 1.1f, Mathf.Sin(a) * 0.85f, 0.1f), Vector3.one * 0.6f, M("#4A5070"));
                    }
                    break;
                case 20: // Kale Kapısı: a stone gate arch
                    Shapes.Rounded("Arch", body, new Vector3(0f, 0.85f, 0.12f), new Vector3(2.2f, 0.3f, 0.3f), 0.08f, M("#8A8EA0"));
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Post", body, new Vector3(s * 1.05f, -0.1f, 0.12f), new Vector3(0.3f, 1.9f, 0.3f), 0.08f, M("#8A8EA0"));
                    break;
                case 23: // Kara Delik vanG: a dark ring with a glowing rim
                    Shapes.Primitive(PrimitiveType.Cylinder, "Ring", body, new Vector3(0f, 0f, 0.15f), new Vector3(2.4f, 0.02f, 2.4f), M("#120A1A", new Color(0.6f, 0.2f, 0.8f)))
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case 24: // vanG Çekirdek: pulsing red rings
                    var rings = new GameObject("Rings").transform;
                    rings.SetParent(body, false);
                    rings.gameObject.AddComponent<Spinner>().speed = 40f;
                    for (int i = 0; i < 2; i++)
                        Shapes.Primitive(PrimitiveType.Cylinder, "Ring", rings, Vector3.zero, new Vector3(2f + i * 0.5f, 0.015f, 2f + i * 0.5f), M("#FF5A7A", new Color(2f, 0.4f, 0.6f)))
                            .transform.localRotation = Quaternion.Euler(70f + i * 40f, 0f, 0f);
                    break;
            }
        }
    }
}
