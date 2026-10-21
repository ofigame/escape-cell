using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// foi's arm and leg gear from the paint workshop: bracers, shoulder plates, claws, knee pads, shin guards, skates and
    /// jet boots. Each piece is drawn in a limb's own frame (y runs down the limb from its joint, 1 = the limb's length;
    /// z faces the way foi faces), so the same piece sits on a body's bone or stands alone in a garage picture.
    /// </summary>
    public static class GearModels
    {
        private static readonly System.Collections.Generic.Dictionary<string, Material> made = new System.Collections.Generic.Dictionary<string, Material>();
        private static Material crystalMat;

        /// <summary>A material made once for its colours and finish (outfits change often: no new one each time).</summary>
        private static Material Surface(Color c, Color e, float smooth, float metal)
        {
            string key = c + "|" + e + "|" + smooth + "|" + metal;
            if (!made.TryGetValue(key, out var m) || m == null) made[key] = m = MaterialFactory.CreateSurface(c, e, smooth, metal);
            return m;
        }

        /// <summary>The limbs a piece is made for.</summary>
        public enum Limb { UpperArm, Forearm, Hand, Shin, Foot }

        /// <summary>Which limbs an arm or leg piece covers.</summary>
        public static Limb[] LimbsOf(string id)
        {
            switch (id)
            {
                case "arms.spring": return new[] { Limb.Forearm, Limb.UpperArm };
                case "arms.shoulder": return new[] { Limb.UpperArm };
                case "arms.claw": return new[] { Limb.Forearm, Limb.Hand };
                case "legs.wheel": return new[] { Limb.Foot };
                default: return id.StartsWith("arms.") ? new[] { Limb.Forearm } : new[] { Limb.Shin };
            }
        }

        /// <summary>Builds the part of <paramref name="id"/> that goes on <paramref name="limb"/> under <paramref name="frame"/>.</summary>
        public static void Build(Transform frame, string id, Limb limb, Color color)
        {
            var metal = Surface(new Color(0.62f, 0.65f, 0.72f), Color.black, 0.75f, 0.8f);
            var dark = Surface(new Color(0.14f, 0.14f, 0.17f), Color.black, 0.35f, 0.1f);
            var main = Surface(color, Color.black, 0.4f, 0.15f);
            var glow = Surface(Color.Lerp(color, Color.white, 0.3f), color * 1.8f, 0.5f, 0f);
            Transform R(string n, Vector3 p, Vector3 s, float r, Material m) => Shapes.Rounded(n, frame, p, s, r, m).transform;
            Transform B(string n, Vector3 p, Vector3 s, Material m) => Shapes.Primitive(PrimitiveType.Sphere, n, frame, p, s, m).transform;
            switch (id)
            {
                case "arms.brush": // leather bracers, two straps and a buckle
                {
                    var leather = Surface(new Color(0.45f, 0.28f, 0.15f), Color.black, 0.3f, 0f);
                    R("Bracer", new Vector3(0f, 0.58f, 0f), new Vector3(0.8f, 0.6f, 0.8f), 0.28f, leather);
                    foreach (float y in new[] { 0.4f, 0.76f }) R("Strap", new Vector3(0f, y, 0f), new Vector3(0.84f, 0.08f, 0.84f), 0.03f, dark);
                    R("Buckle", new Vector3(0f, 0.4f, 0.42f), new Vector3(0.16f, 0.12f, 0.04f), 0.02f, metal);
                    break;
                }
                case "arms.spring": // robot bracer with a light down it, a shoulder plate
                    if (limb == Limb.Forearm)
                    {
                        R("Bracer", new Vector3(0f, 0.56f, 0f), new Vector3(0.82f, 0.66f, 0.82f), 0.22f, metal);
                        R("Light", new Vector3(0f, 0.56f, 0.41f), new Vector3(0.12f, 0.5f, 0.04f), 0.03f, glow);
                        foreach (float y in new[] { 0.28f, 0.84f }) R("Ring", new Vector3(0f, y, 0f), new Vector3(0.86f, 0.07f, 0.86f), 0.03f, dark);
                    }
                    else
                    {
                        B("Plate", new Vector3(0f, 0.08f, 0f), new Vector3(1.05f, 0.62f, 1.0f), metal);
                        R("Light", new Vector3(0f, 0.12f, 0.5f), new Vector3(0.3f, 0.06f, 0.04f), 0.02f, glow);
                    }
                    break;
                case "arms.shoulder": // a heavy plate over the shoulder, gold trimmed
                {
                    var gold = Surface(new Color(1f, 0.78f, 0.3f), new Color(0.3f, 0.2f, 0.05f), 0.8f, 0.9f);
                    B("Plate", new Vector3(0f, 0.06f, 0f), new Vector3(1.25f, 0.7f, 1.15f), main);
                    R("Trim", new Vector3(0f, 0.32f, 0f), new Vector3(1.2f, 0.07f, 1.1f), 0.03f, gold);
                    B("Stud", new Vector3(0f, -0.2f, 0f), new Vector3(0.28f, 0.28f, 0.28f), gold);
                    break;
                }
                case "arms.crystal": // a dark band with crystals standing out of it
                {
                    R("Band", new Vector3(0f, 0.55f, 0f), new Vector3(0.84f, 0.3f, 0.84f), 0.1f, dark);
                    var crystal = crystalMat ?? (crystalMat = MaterialFactory.CreateTransparent(new Color(0.55f, 0.9f, 1f, 0.75f), new Color(0.4f, 1.3f, 1.8f)));
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * 90f + 45f;
                        var c = R("Crystal", Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 0.45f) + new Vector3(0f, 0.55f, 0f), new Vector3(0.16f, 0.5f, 0.16f), 0.05f, crystal);
                        c.localRotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(25f, 0f, 0f);
                    }
                    break;
                }
                case "arms.claw": // a bracer, and three claws over the fist
                    if (limb == Limb.Forearm)
                        R("Bracer", new Vector3(0f, 0.6f, 0f), new Vector3(0.82f, 0.56f, 0.82f), 0.22f, main);
                    else
                        foreach (float x in new[] { -0.28f, 0f, 0.28f })
                        {
                            var claw = R("Claw", new Vector3(x, 0.75f, 0.32f), new Vector3(0.09f, 1.1f, 0.14f), 0.04f, metal);
                            claw.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                        }
                    break;
                case "arms.lumi": // two glowing gold bands
                {
                    var gold = Surface(new Color(1f, 0.82f, 0.3f), new Color(1.4f, 1f, 0.3f), 0.5f, 0f);
                    foreach (float y in new[] { 0.38f, 0.78f }) R("Band", new Vector3(0f, y, 0f), new Vector3(0.86f, 0.12f, 0.86f), 0.05f, gold);
                    B("Gem", new Vector3(0f, 0.58f, 0.42f), new Vector3(0.16f, 0.16f, 0.08f), glow);
                    break;
                }
                case "legs.knee": // a padded knee pad on a strap
                    R("Pad", new Vector3(0f, 0.05f, 0.34f), new Vector3(0.68f, 0.46f, 0.26f), 0.12f, main);
                    R("Strap", new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.08f, 0.9f), 0.03f, dark);
                    break;
                case "legs.spring": // a metal knee cap with a light in it
                    B("Cap", new Vector3(0f, 0.03f, 0.3f), new Vector3(0.66f, 0.56f, 0.4f), metal);
                    B("Light", new Vector3(0f, 0.03f, 0.5f), new Vector3(0.14f, 0.14f, 0.06f), glow);
                    R("Strap", new Vector3(0f, 0.22f, 0f), new Vector3(0.88f, 0.08f, 0.88f), 0.03f, dark);
                    break;
                case "legs.tracks": // an armoured shin guard
                    R("Guard", new Vector3(0f, 0.5f, 0.3f), new Vector3(0.66f, 0.8f, 0.26f), 0.1f, main);
                    foreach (float y in new[] { 0.25f, 0.5f, 0.75f }) B("Rivet", new Vector3(0f, y, 0.44f), new Vector3(0.1f, 0.1f, 0.05f), metal);
                    B("Knee", new Vector3(0f, 0.04f, 0.3f), new Vector3(0.58f, 0.48f, 0.34f), dark);
                    break;
                case "legs.wheel": // skates: two wheels under each boot
                {
                    var wheel = Surface(color, Color.black, 0.5f, 0.1f);
                    foreach (float y in new[] { 0.05f, 0.75f })
                    {
                        var w = Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", frame, new Vector3(0f, y, -0.62f), new Vector3(0.4f, 0.1f, 0.4f), wheel).transform;
                        w.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                    R("Plate", new Vector3(0f, 0.4f, -0.48f), new Vector3(0.5f, 1.0f, 0.08f), 0.03f, metal);
                    break;
                }
                case "legs.snow": // fur at the knee and the ankle
                {
                    var fur = Surface(Color.Lerp(color, Color.white, 0.6f), Color.black, 0.1f, 0f);
                    foreach (float y in new[] { 0.02f, 0.95f })
                        for (int i = 0; i < 9; i++)
                        {
                            float a = i * Mathf.PI * 2f / 9f;
                            B("Fur", new Vector3(Mathf.Cos(a) * 0.42f, y, Mathf.Sin(a) * 0.42f), Vector3.one * 0.3f, fur);
                        }
                    break;
                }
                case "legs.jet": // thrusters on the calves, flames out of them
                {
                    var flame = Surface(new Color(1f, 0.6f, 0.2f), new Color(2.6f, 1f, 0.2f), 0.5f, 0f);
                    R("Thruster", new Vector3(0f, 0.55f, -0.46f), new Vector3(0.38f, 0.6f, 0.34f), 0.12f, metal);
                    R("Nozzle", new Vector3(0f, 0.9f, -0.46f), new Vector3(0.3f, 0.12f, 0.3f), 0.08f, dark);
                    B("Flame", new Vector3(0f, 1.08f, -0.46f), new Vector3(0.24f, 0.32f, 0.24f), flame);
                    B("Knee", new Vector3(0f, 0.04f, 0.3f), new Vector3(0.56f, 0.46f, 0.32f), main);
                    break;
                }
                default:
                    R("Pad", new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 0.5f, 0.8f), 0.2f, main);
                    break;
            }
        }

        /// <summary>A limb frame between two joints: y down the limb from <paramref name="from"/> (1 = its length), z the way <paramref name="facing"/> points.</summary>
        public static Transform Frame(string name, Vector3 from, Vector3 to, Vector3 facing, float thickness)
        {
            var t = new GameObject(name).transform;
            var down = to - from;
            float len = down.magnitude;
            var fwd = Vector3.ProjectOnPlane(facing, down.normalized);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(down, Vector3.right);
            t.SetPositionAndRotation(from, Quaternion.LookRotation(fwd.normalized, down / Mathf.Max(len, 1e-5f)));
            t.localScale = new Vector3(len * thickness, len, len * thickness);
            return t;
        }

        /// <summary>A piece on its own, as the garage shows it: its limb standing upright, a little turned.</summary>
        public static void Picture(Transform root, string id, Color color)
        {
            var limbs = LimbsOf(id);
            float y = 0f;
            foreach (var limb in limbs)
            {
                var f = new GameObject(limb.ToString()).transform;
                f.SetParent(root, false);
                f.localRotation = Quaternion.Euler(0f, 0f, 180f) * Quaternion.Euler(0f, 180f, 0f); // the limb hanging down, its front to the camera
                f.localPosition = new Vector3(0f, y, 0f);
                f.localScale = new Vector3(0.36f, 1f, 0.36f) * 0.3f;
                Build(f, id, limb, color);
                y -= 0.3f;
            }
        }
    }
}
