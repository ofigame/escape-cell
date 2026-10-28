using System.Collections.Generic;
using SquashBot.Combat;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SquashBot.Visual
{
    /// <summary>
    /// foi: an armoured body with its own clips (Resources/Heroes/Foi) and a big cartoon head on its neck ring, one of
    /// several, whose hair and skin take any colour (the skin shows only on the head) and whose eyes blink and narrow in
    /// a fight. It moves by code on its own bones (no borrowed clips: they bent this body out of shape): it stands with
    /// its arms down and breathes, walks with its arms swinging, raises its right arm and brings it down on what it
    /// strikes, raises both arms and claps when it wins. The workshop dresses it: the head, hair and skin colour, the armour's paint
    /// (camouflage or colours), a hat, a cape or back piece, arm and leg gear and a badge, each riding its bone, and a
    /// weapon in its right hand. It reads what the body does from how it moves (and from <see cref="Punch"/> /
    /// <see cref="Anger"/> / <see cref="Clap"/>), so copies of it (lookalikes, rivals) come alive too.
    /// </summary>
    public partial class SkinnedHero : MonoBehaviour
    {
        private const string Folder = "Heroes/Foi/";
        /// <summary>The head joint's height in the build (about 1.05 tall in all, the head included).</summary>
        private const float HeadJointHeight = 0.92f; // (0.80 then 15% bigger again, as asked: the remade body with its smaller head had come out small)
        /// <summary>The remade body (branch foi-yeni-beden) wears its own paint: the old suits' pictures are laid out for the
        /// old body and do not fit it; nor is it thickened (it is broad as made).</summary>
        private const bool OwnPaint = true;
        /// <summary>The head's height against the body's (headless) height: a big, cartoon head.</summary>
        private const float HeadShare = 0.5f;

        /// <summary>True while foi is off duty (menus, garage): it stands with its arms folded; in play, at ease.</summary>
        public static System.Func<bool> OffDuty = () => true;

        /// <summary>What foi looks like: its head (1..), hair and skin colour (null = the head's own), the armour's paint (null = plain).</summary>
        public struct Look
        {
            public int head;
            public Color? hair, skin, eyes;
            public string suit;
        }

        /// <summary>
        /// The heads: 1-3 the man's head with his three haircuts, 4-6 the woman's with hers. Each is a bald head (its
        /// skin and eyes on its picture), its brows and its hair as their own models, each recoloured on its own.
        /// </summary>
        public const int Heads = 6;

        /// <summary>A head piece's number (head.m1-m3: 1-3, head.f1-f3: 4-6).</summary>
        public static int HeadNumber(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 7 || !id.StartsWith("head.")) return 1;
            int k = id[6] - '0';
            if (k < 1 || k > 3) return 1;
            return id[5] == 'f' ? 3 + k : k;
        }

        /// <summary>The head model a head wears (the man's or the woman's).</summary>
        public static string HeadFile(int n) => n <= 3 ? "head_m" : "head_f";

        /// <summary>Its haircut's model.</summary>
        public static string HairFile(int n) => "hair_" + (n <= 3 ? "m" + n : "f" + (n - 3));

        /// <summary>The hair and eyes when none is chosen: brown, as most people's.</summary>
        public static readonly Color DefaultHair = new Color(0.36f, 0.2f, 0.11f), DefaultEyes = new Color(0.42f, 0.26f, 0.13f);

        [System.Serializable]
        private class EyeInfo { public float x, y, z, w, h; }
        [System.Serializable]
        private class HeadInfo { public float height, width, depth, top, front, neckWidth, hatTop, hatWidth, iris = 0.5f; public float[] skin, hair; public EyeInfo[] eyes; }
        [System.Serializable]
        private class BodyInfo { public float height, ringTop, ringX, ringY, ringWidth, shoulders; public float[] head, neck; }

        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, HeadInfo> headInfos = new Dictionary<string, HeadInfo>();
        private static BodyInfo bodyInfo;

        private Transform hips;
        private Vector3 lastPos;
        private float stride, walk, chop = 1f, clap = 1f;
        private float speed, punchLeft, angerLeft, clapLeft, blinkTimer = 2f;
        private bool ready;

        /// <summary>Where the workshop's hat, back piece and badge go (empty mounts riding the head and the chest).</summary>
        public Transform HatMount { get; private set; }
        public Transform BackMount { get; private set; }
        public Transform BadgeMount { get; private set; }

        private int headShown;

        private static HeadInfo InfoOf(int head) => InfoOf(HeadFile(head));

        private static HeadInfo InfoOf(string file)
        {
            if (headInfos.TryGetValue(file, out var i)) return i;
            var text = Resources.Load<TextAsset>(Folder + file);
            i = text != null ? JsonUtility.FromJson<HeadInfo>(text.text) : new HeadInfo { height = 0.98f, width = 0.8f, top = 0.98f, neckWidth = 0.3f };
            headInfos[file] = i;
            return i;
        }

        private static BodyInfo Body()
        {
            if (bodyInfo != null) return bodyInfo;
            var text = Resources.Load<TextAsset>(Folder + "armour");
            bodyInfo = text != null ? JsonUtility.FromJson<BodyInfo>(text.text) : new BodyInfo { height = 0.62f, ringTop = 0.62f, ringY = 0.027f, ringWidth = 0.24f, shoulders = 0.275f, head = new[] { 0f, 0f, 0.553f } };
            return bodyInfo;
        }

        /// <summary>Puts foi under <paramref name="visual"/>; the robot's code finds its usual named parts there as empty
        /// stand-ins. Returns the (hidden) eye stand-ins.</summary>
        public static (Transform eyeL, Transform eyeR) Build(Transform visual)
        {
            foreach (var n in new[] { "LegL", "LegR", "FootL", "FootR", "ArmL", "ArmR", "Body", "Head", "Visor" })
                new GameObject(n).transform.SetParent(visual, false);
            var eyeL = new GameObject("EyeL", typeof(MeshRenderer)).transform; eyeL.SetParent(visual, false); // (empty renderers: the outfit code toggles them)
            var eyeR = new GameObject("EyeR", typeof(MeshRenderer)).transform; eyeR.SetParent(visual, false);

            var skin = Instantiate(Resources.Load<GameObject>(Folder + "body"), visual, false).transform;
            skin.name = "Hero_Skin";
            skin.localPosition = Vector3.zero;
            skin.localRotation = Quaternion.identity;
            foreach (var sm in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { sm.updateWhenOffscreen = true; if (!OwnPaint) sm.sharedMesh = Thick(sm); }
            var headBone = FindBone(skin, "Head");
            float raw = visual.InverseTransformPoint(headBone.position).y;
            skin.localScale = Vector3.one * (raw > 0.0001f ? HeadJointHeight / raw : 1f);

            var chest = FindBone(skin, "Spine2");
            float unit = skin.localScale.x; // the body's measures (as made) to the build's
            var b = Body();
            var chestLocal = chest != null ? visual.InverseTransformPoint(chest.position) : new Vector3(0f, 0.45f, 0f);
            float gear = unit * 1.6f; // the old foi's pieces a little bigger on this broad body
            var back = Mount("Mount_Back", visual, new Vector3(0f, chestLocal.y - FoiBackY * gear, chestLocal.z - 0.02f * unit), gear, chest != null ? chest : skin);
            var badge = Mount("Mount_Badge", visual, new Vector3(0f, chestLocal.y - FoiChestY * gear, chestLocal.z + b.shoulders * 0.32f * unit - FoiChestFront * gear), gear, chest != null ? chest : skin);

            var me = skin.gameObject.AddComponent<SkinnedHero>();
            me.BackMount = back; me.BadgeMount = badge;
            Dress(skin, new Look { head = 1 });
            return (eyeL, eyeR);
        }

        /// <summary>The old foi's head top and width, its chest's back and front, as the workshop's pieces are made about them.</summary>
        private const float FoiHeadTop = 0.84f, FoiHeadWidth = 0.368f, FoiBackY = 0.3f, FoiChestY = 0.25f, FoiChestFront = 0.11f;

        private static Transform Mount(string name, Transform visual, Vector3 at, float scale, Transform rider)
        {
            var t = new GameObject(name).transform;
            t.SetParent(visual, false);
            t.localPosition = at;
            t.localScale = Vector3.one * scale;
            t.SetParent(rider, true);
            return t;
        }

        private Transform head;
        private readonly List<Transform> lids = new List<Transform>();
        private Material lidMaterial;

        /// <summary>
        /// The head <paramref name="n"/> on the neck ring, sized to the body, its eyelids, and the hat's mount on its top
        /// (the one there before goes, with what was on its mount).
        /// </summary>
        private void PutHead(int n)
        {
            if (head == null) head = FindHead();
            if (n == headShown && head != null) return;
            var bone = FindBone(transform, "Head");
            if (head != null) DestroyImmediate(head.gameObject);
            if (HatMount != null) DestroyImmediate(HatMount.gameObject);
            headShown = n;
            var info = InfoOf(n);
            var b = Body();
            var visual = transform.parent;
            float unit = transform.localScale.x;
            head = new GameObject("Hero_Head").transform;
            head.SetParent(visual, false);
            HeadParts(head, n);
            float k = b.height * HeadShare * unit / Mathf.Max(0.01f, info.top);
            head.localScale = Vector3.one * k;
            head.localRotation = Quaternion.identity;
            // its neck sits down in the ring (measured from the head joint; the model's front is +z here)
            var jointLocal = visual.InverseTransformPoint(bone.position);
            head.localPosition = jointLocal + new Vector3(0f, (b.ringTop - b.head[2]) * unit - 0.055f * unit, -(b.ringY - b.head[1]) * unit + 0.04f * unit); // (down into the ring and a little forward: over the chest, not the back)
            foreach (var r in head.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = Shapes.CastShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            // eyelids: a little dome over each eye, of the skin's colour, hung from its top; it drops to blink
            lids.Clear();
            if (info.eyes != null)
                foreach (var e in info.eyes)
                {
                    var pivot = new GameObject("Lid").transform;
                    pivot.SetParent(head, false);
                    pivot.localPosition = new Vector3(e.x, e.z + e.h * 0.62f, -e.y - 0.004f);
                    var cover = Shapes.Primitive(PrimitiveType.Sphere, "Cover", pivot, new Vector3(0f, -e.h * 0.62f, -0.015f), new Vector3(e.w * 1.12f, e.h * 1.24f, Mathf.Max(0.04f, e.w * 0.5f)), lidMaterial ?? Paint(Color.white)).transform;
                    pivot.localScale = new Vector3(1f, 0.001f, 1f);
                    cover.gameObject.SetActive(false);
                    lids.Add(pivot);
                }
            // the hat's mount: the old foi's head frame sized to this head's width, on its top
            var hat = new GameObject("Mount_Hat").transform;
            hat.SetParent(head, false);
            float hatScale = info.width * 0.86f / FoiHeadWidth;
            hat.localScale = Vector3.one * hatScale;
            float crown = (info.hatTop > 0f ? info.hatTop : info.top * 0.94f) + 0.05f * info.height; // where a hat sits: on the hair over the skull's top
            hat.localPosition = new Vector3(0f, crown - FoiHeadTop * hatScale, 0f);
            HatMount = hat;
            head.SetParent(bone, true);
            headMadeRot = head.localRotation; headMadePos = head.localPosition; headMadeKnown = true; // (its place on the neck as made: the pose reset puts it back)
        }

        /// <summary>
        /// Head <paramref name="n"/>'s models under <paramref name="holder"/>: the head as imported (its own turn and size
        /// from the file), sized so its units are the measures' (its neck at the origin, the face toward +z), and its brows
        /// and hair in the same frame and size. Returns the head model.
        /// </summary>
        public static Transform HeadParts(Transform holder, int n)
        {
            n = Mathf.Clamp(n, 1, Heads);
            var info = InfoOf(n);
            var prefab = Resources.Load<GameObject>(Folder + HeadFile(n));
            if (prefab == null) return null;
            var model = Instantiate(prefab, holder, false).transform;
            var rs = model.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                float hb = bb.size.y / Mathf.Max(1e-6f, holder.lossyScale.y);
                if (hb > 1e-5f) model.localScale *= info.height / hb;
            }
            foreach (var part in new[] { HeadFile(n) + "_brows", HairFile(n) })
            {
                var pp = Resources.Load<GameObject>(Folder + part);
                if (pp == null) continue;
                var pm = Instantiate(pp, holder, false).transform;
                pm.localPosition = model.localPosition; pm.localRotation = model.localRotation; pm.localScale = model.localScale;
            }
            return model;
        }

        /// <summary>Paints head <paramref name="n"/>'s parts under <paramref name="holder"/>: skin and eyes, brows and hair (the eyelids too).</summary>
        public static void PaintHead(Transform holder, int n, Color? hair, Color? skin, Color? eyes, Material lid = null)
        {
            var hc = hair ?? DefaultHair;
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                string nm = r.name.ToLowerInvariant();
                r.sharedMaterial = r.name == "Cover" ? (lid ?? LidPaint(SkinOf(n, skin))) : nm.Contains("hair") ? HairPaint(hc) : nm.Contains("brows") ? BrowPaint(hc) : HeadPaint(n, skin, eyes);
                r.receiveShadows = false; // (no flickering self-shadow on the face from the hair, hat or crown)
                if (r.name != "Cover" && !nm.Contains("hair") && !nm.Contains("brows") && r.sharedMaterial.shader.name != FoiShaderName) WithRim(r); // (foi's own paint has its rim; not on the hair: every lock's edge would glow)
            }
        }

        private Transform FindHead()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "Hero_Head") return t;
            return null;
        }

        private static Material Paint(Color c)
        {
            string key = "paint" + c;
            if (!materials.TryGetValue(key, out var m) || m == null) materials[key] = m = MaterialFactory.CreateSurface(c, Color.black, 0.18f, 0f);
            return m;
        }

        /// <summary>The chests' own armour paints: they shine and glow.</summary>
        private static readonly string[] Premium = { "elite", "ghost", "ranger", "crimson", "fox" };
        private static readonly Dictionary<Material, Color> glowing = new Dictionary<Material, Color>();
        private static int glowFrame = -1;

        private static Color PremiumGlow(string suit) =>
            suit == "elite" ? new Color(1.6f, 1.1f, 0.3f) : suit == "ghost" ? new Color(0.7f, 1.2f, 1.6f) : suit == "ranger" ? new Color(0.4f, 1.4f, 0.5f)
            : suit == "crimson" ? new Color(1.6f, 0.2f, 0.3f) : new Color(1.6f, 0.7f, 0.2f);

        /// <summary>The premium paints' glow breathing (once a frame, however many wear them).</summary>
        private static void Breathe()
        {
            if (glowFrame == Time.frameCount || glowing.Count == 0) return;
            glowFrame = Time.frameCount;
            float k = 0.45f + 0.35f * Mathf.Sin(Time.time * 2.4f);
            foreach (var kv in glowing) if (kv.Key != null) kv.Key.SetColor("_EmissionColor", kv.Value * k);
        }

        private static Material Armour(string suit)
        {
            if (OwnPaint) suit = null;
            string key = "armour" + (suit ?? "");
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            var tex = suit != null ? Resources.Load<Texture2D>(Folder + "armour_" + suit) : null;
            if (tex == null) tex = Resources.Load<Texture2D>(Folder + "armour");
            bool premium = System.Array.IndexOf(Premium, suit) >= 0;
            m = MaterialFactory.CreateSurface(Color.white, Color.black, premium ? 0.75f : 0.5f, premium ? 0.45f : 0.25f); // (polished, but lit by its own colour: a dark arena with nothing to reflect does not dull it)
            if (premium)
            {
                var mask = Resources.Load<Texture2D>(Folder + "armour_mask");
                if (mask != null)
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    m.SetTexture("_EmissionMap", mask);
                    m.SetColor("_EmissionColor", PremiumGlow(suit) * 0.6f);
                    glowing[m] = PremiumGlow(suit);
                }
            }
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
            if (!premium && tex != null)
            {
                // a little of its own light, as the icon shows it: the brushed metal never goes dull in a dark arena
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", Color.white * 0.12f);
            }
            // (no normal map: its shader variant is not in the build)
            materials[key] = m;
            return m;
        }

        /// <summary>The head's picture with its hair and skin repainted (kept for the next time the same look is asked for).</summary>
        private static readonly List<string> recent = new List<string>();
        private static readonly Dictionary<string, (Color32[] px, Color32[] mk, int w, int mw)> pixels = new Dictionary<string, (Color32[], Color32[], int, int)>();

        /// <summary>Livelier skin (as on the icon): more colour, a touch warmer, so the blush on the nose and cheeks shows.</summary>
        /// <summary>
        /// A hair pixel in a new colour: the colour times how light the pixel is against the hair's own colour, so the
        /// strands keep their shading; the lightest roll off softly instead of clipping to a flat, shiny glare.
        /// </summary>
        private static Color32 HairTint(Color32 p, Color target, float refLum)
        {
            float lum = (p.r * 0.3f + p.g * 0.59f + p.b * 0.11f) / 255f;
            float f = lum / Mathf.Max(0.02f, refLum);
            var c = new Vector3(target.r, target.g, target.b) * f;
            float m = Mathf.Max(c.x, Mathf.Max(c.y, c.z));
            const float Knee = 0.72f;
            if (m > Knee) c *= (Knee + (1f - Mathf.Exp(-(m - Knee) * 2.2f)) * (0.95f - Knee)) / m;
            return new Color32((byte)Mathf.Clamp(c.x * 255f, 0f, 255f), (byte)Mathf.Clamp(c.y * 255f, 0f, 255f), (byte)Mathf.Clamp(c.z * 255f, 0f, 255f), 255);
        }

        private static Color32 Vivid(Color32 p, float amount)
        {
            if (amount < 0.01f) return p;
            float r = p.r, g = p.g, b = p.b, lum = r * 0.3f + g * 0.59f + b * 0.11f;
            float sat = 1f + 0.3f * amount;
            r = lum + (r - lum) * sat; g = lum + (g - lum) * sat; b = lum + (b - lum) * sat;
            r *= 1f + 0.04f * amount; b *= 1f - 0.03f * amount;
            return new Color32((byte)Mathf.Clamp(r, 0f, 255f), (byte)Mathf.Clamp(g, 0f, 255f), (byte)Mathf.Clamp(b, 0f, 255f), 255);
        }

        /// <summary>The head's own light: most of its colour, which no light or shadow takes away, so the face keeps the
        /// same skin in every scene (orange suns, blue ice, the menu) and never darkens as the lights round it change.</summary>
        private const float HeadGlow = 0.72f;

        /// <summary>How much the face takes from the lights round it (the rest is its own light): coloured lamps and flames
        /// on a floor tint it only a little, and it never goes dark or orange.</summary>
        private const float HeadLit = 0.2f; // (a little: the shade baked on it shapes it; no lamp tints its skin or hair)

        private static Material HeadPaint(int n, Color? skin, Color? eyes)
        {
            string file = HeadFile(n);
            var eye = eyes ?? DefaultEyes;
            string key = file + skin + eye;
            if (materials.TryGetValue(key, out var m) && m != null) { recent.Remove(key); recent.Add(key); return m; }
            var baseTex = Resources.Load<Texture2D>(Folder + file);
            Texture2D tex = baseTex;
            var mask = Resources.Load<Texture2D>(Folder + file + "_mask");
            var info = InfoOf(file);
            (Color32[] px, Color32[] mk, int w, int mw) src = default;
            bool canRead = baseTex != null && mask != null && baseTex.isReadable && mask.isReadable;
            if (canRead && !pixels.TryGetValue(file, out src))
            {
                // (the pictures are imported uncompressed for this; a phone that still cannot read them keeps the head as made)
                try { pixels[file] = src = (baseTex.GetPixels32(), mask.GetPixels32(), baseTex.width, mask.width); }
                catch (System.Exception e) { Debug.LogWarning("[foi] head " + file + " not recoloured: " + e.Message); canRead = false; }
            }
            if (canRead && src.px != null && src.px.Length == src.w * src.w)
            {
                int W = src.w, mw = src.mw;
                var sRef = info.skin != null ? new Color(info.skin[0], info.skin[1], info.skin[2]) : new Color(0.78f, 0.64f, 0.56f);
                Vector3 sr = Vector3.one;
                if (skin.HasValue) sr = new Vector3(skin.Value.r / Mathf.Max(0.04f, sRef.r), skin.Value.g / Mathf.Max(0.04f, sRef.g), skin.Value.b / Mathf.Max(0.04f, sRef.b));
                var px = new Color32[W * W];
                for (int y = 0; y < W; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int i = y * W + x;
                        var p = src.px[i];
                        var mk = src.mk[(y * mw / W) * mw + x * mw / W];
                        float ws = mk.g / 255f, wi = mk.b / 255f;
                        if (ws > 0.01f)
                        {
                            var mul = Vector3.one + (sr - Vector3.one) * ws;
                            p = Vivid(new Color32((byte)Mathf.Min(255f, p.r * mul.x), (byte)Mathf.Min(255f, p.g * mul.y), (byte)Mathf.Min(255f, p.b * mul.z), 255), ws * 0.35f); // (the skin a touch richer: alive, never grey)
                        }
                        if (wi > 0.01f) p = Color32.Lerp(p, HairTint(src.px[i], eye, info.iris), wi); // (the grey iris in the eye colour, its streaks kept)
                        px[i] = p;
                    }
                tex = new Texture2D(W, W, TextureFormat.RGBA32, true) { name = key };
                tex.SetPixels32(px);
                tex.Apply(true, true);
            }
            m = FoiPaint(tex, Color.white, Resources.Load<Texture2D>(Folder + file + "_normal"), false);
            if (m == null)
            {
            m = MaterialFactory.CreateSurface(new Color(HeadLit, HeadLit, HeadLit), Color.black, 0f, 0f); // matte skin, nearly all its own light
            Matte(m);
            }
            if (tex != null && m.shader.name != FoiShaderName)
            {
                m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", Color.white * HeadGlow);
            }
            materials[key] = m;
            if (tex != baseTex)
            {
                // only a few recoloured heads are kept (rivals bring their own): the oldest goes, texture and all
                recent.Add(key);
                while (recent.Count > 10)
                {
                    var old = recent[0]; recent.RemoveAt(0);
                    materials.Remove(old); // (not destroyed: a head may still wear it; once none does, the game's sweeps free it)
                }
            }
            return m;
        }

        /// <summary>The hair's paint: its grey strand picture in the hair's colour, matte, mostly its own light (some of the
        /// scene's, so its locks' shapes show).</summary>
        private static Material HairPaint(Color c)
        {
            string key = "hair" + c;
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            var tex = Resources.Load<Texture2D>(Folder + "hair_strands");
            m = FoiPaint(tex, c, null, true);
            if (m != null) { materials[key] = m; return m; }
            m = MaterialFactory.CreateSurface(c * HairLit, Color.black, 0f, 0f);
            Matte(m);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", c * HairGlow);
            }
            materials[key] = m;
            return m;
        }

        /// <summary>The brows: the hair's colour, a shade darker, flat and matte.</summary>
        private static Material BrowPaint(Color c)
        {
            var d = c * 0.82f; d.a = 1f;
            string key = "brow" + d;
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = FoiPaint(null, d, null, false);
            if (m != null) { m.SetColor("_Sheen", Color.black); materials[key] = m; return m; }
            m = MaterialFactory.CreateSurface(d * HeadLit, Color.black, 0f, 0f);
            Matte(m);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", d * HeadGlow);
            materials[key] = m;
            return m;
        }

        private const string FoiShaderName = "SquashBot/FoiSkin";
        private static Shader foiShader;

        /// <summary>
        /// foi's own paint (skin, hair, brows): lit by its own light fixed to the camera, so it looks the same on every
        /// stage, with a real falloff into shade, warmth under the skin, a dry sheen (along each lock on the hair) and fine
        /// pores from <paramref name="normal"/>. Null where the phone cannot draw it (the old paint is used).
        /// </summary>
        private static Material FoiPaint(Texture tex, Color tint, Texture normal, bool hair)
        {
            if (foiShader == null) foiShader = Resources.Load<Shader>("Shaders/FoiSkin");
            if (foiShader == null || !foiShader.isSupported) return null;
            var m = new Material(foiShader) { name = hair ? "FoiHair" : "FoiSkin" };
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 0.8f); }
            if (hair)
            {
                m.SetFloat("_Hair", 1f);
                m.SetFloat("_Gloss", 40f);
                m.SetColor("_Sheen", new Color(0.32f, 0.29f, 0.25f) * Mathf.Lerp(0.6f, 1.2f, tint.grayscale));
                m.SetColor("_FillColor", new Color(0.4f, 0.39f, 0.44f));
                m.SetColor("_RimColor", new Color(1f, 0.94f, 0.86f, 0.2f));
            }
            return m;
        }

        private static Material rimMat;

        /// <summary>foi's rim light (a soft warm edge where it turns from the camera: it stands out from any background).</summary>
        private static Material Rim()
        {
            if (rimMat != null) return rimMat;
            var sh = Resources.Load<Shader>("Shaders/FoiRim");
            if (sh == null || !sh.isSupported) return null;
            rimMat = new Material(sh) { name = "FoiRim" };
            rimMat.SetColor("_RimColor", new Color(1f, 0.93f, 0.8f, 0.55f));
            rimMat.SetFloat("_RimPower", 3.2f);
            return rimMat;
        }

        /// <summary>The rim light drawn over a renderer's own paint (once).</summary>
        private static void WithRim(Renderer r)
        {
            var rim = Rim();
            if (rim == null) return;
            var ms = r.sharedMaterials;
            foreach (var m in ms) if (m == rim) return;
            var all = new Material[ms.Length + 1];
            ms.CopyTo(all, 0);
            all[ms.Length] = rim;
            r.sharedMaterials = all;
        }

        private static void Matte(Material m)
        {
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }

        /// <summary>How much of the scene's light the hair takes (the rest its own): enough for its locks to show.</summary>
        private const float HairLit = 0.4f, HairGlow = 0.62f;

        /// <summary>The eyelids' paint: the skin's colour, livened and lit as the face is.</summary>
        private static Material LidPaint(Color c)
        {
            var v = Vivid(c, 1f); var cv = new Color(v.r / 255f, v.g / 255f, v.b / 255f);
            string key = "lid" + cv;
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = FoiPaint(null, cv * 0.9f, null, false);
            if (m != null) { materials[key] = m; return m; }
            m = MaterialFactory.CreateSurface(cv * HeadLit, Color.black, 0f, 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", cv * HeadGlow);
            materials[key] = m;
            return m;
        }

        /// <summary>The head's skin colour as the eyelids wear it.</summary>
        private static Color SkinOf(int n, Color? skin)
        {
            if (skin.HasValue) return skin.Value;
            var i = InfoOf(n);
            return i.skin != null ? new Color(i.skin[0], i.skin[1], i.skin[2]) : new Color(0.9f, 0.75f, 0.65f);
        }

        private static readonly Dictionary<Mesh, Mesh> thick = new Dictionary<Mesh, Mesh>();
        /// <summary>How far the body is puffed out, of its height (arms, legs and body a little thicker).</summary>
        public const float Puff = 0.012f;

        /// <summary>The body's mesh a little thicker: each point pushed out along its normal, as much as it moves with the
        /// arms, legs or body (the neck, hands and feet as made: the head, the grip and the steps fit as before). Made once.</summary>
        private static Mesh Thick(SkinnedMeshRenderer sm)
        {
            var src = sm.sharedMesh;
            if (src == null || !src.isReadable) return src;
            if (thick.TryGetValue(src, out var done)) return done;
            var bones = sm.bones;
            var puffs = new float[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                string n = bones[i] != null ? bones[i].name : "";
                bool keep = n.Contains("Neck") || n.Contains("Head") || n.Contains("Hand") || n.Contains("Foot") || n.Contains("Toe");
                puffs[i] = keep ? 0f : 1f;
            }
            var mesh = Instantiate(src);
            mesh.name = src.name + "_thick";
            var v = mesh.vertices;
            var nrm = mesh.normals;
            var w = mesh.boneWeights;
            if (nrm.Length != v.Length || w.Length != v.Length) { thick[src] = src; Destroy(mesh); return src; }
            var size = src.bounds.size;
            float d = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * Puff;
            float P(int b) => b >= 0 && b < puffs.Length ? puffs[b] : 0f;
            for (int i = 0; i < v.Length; i++)
            {
                var bw = w[i];
                float k = bw.weight0 * P(bw.boneIndex0) + bw.weight1 * P(bw.boneIndex1) + bw.weight2 * P(bw.boneIndex2) + bw.weight3 * P(bw.boneIndex3);
                v[i] += nrm[i] * (d * k);
            }
            mesh.vertices = v;
            mesh.RecalculateBounds();
            thick[src] = mesh;
            return mesh;
        }

        /// <summary>Dresses a build already made: its head, hair and skin colours and the armour's paint.</summary>
        public static void Dress(Transform skin, Look look)
        {
            var me = skin.GetComponent<SkinnedHero>();
            int n = Mathf.Clamp(look.head <= 0 ? 1 : look.head, 1, Heads);
            if (me != null)
            {
                me.lidMaterial = LidPaint(SkinOf(n, look.skin));
                me.SkinColor = SkinOf(n, look.skin);
                me.HeadFileName = HeadFile(n);
                me.faceRenderer = null; me.hairPart = null;
                me.PutHead(n);
                PaintHead(me.head, n, look.hair, look.skin, look.eyes, me.lidMaterial);
            }
            var armour = Armour(look.suit);
            foreach (var r in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.sharedMaterial = armour;
                WithRim(r);
                r.shadowCastingMode = Shapes.CastShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        [System.Serializable]
        private class HoodInfo { public float width, depth, height, hoodHeight; }
        private static HoodInfo hoodInfo;

        /// <summary>The hooded cloak over the head (null: none), sized to the head worn, its cape on the shoulders.</summary>
        public static void Hood(Transform skin, Color? color)
        {
            var me = skin.GetComponent<SkinnedHero>();
            if (me == null) return;
            if (me.head == null) me.head = me.FindHead();
            if (me.head == null) return;
            var old = me.head.Find("Hero_Hood");
            if (old != null) DestroyImmediate(old.gameObject);
            foreach (var r in me.head.GetComponentsInChildren<Renderer>(true)) if (r.name.ToLowerInvariant().Contains("hair")) r.enabled = !color.HasValue; // (under a hood: no hair)
            if (!color.HasValue) return;
            var prefab = Resources.Load<GameObject>(Folder + "hood");
            if (prefab == null) return;
            if (hoodInfo == null) { var text = Resources.Load<TextAsset>(Folder + "hood"); hoodInfo = text != null ? JsonUtility.FromJson<HoodInfo>(text.text) : new HoodInfo { width = 0.57f, depth = 0.6f, height = 0.76f }; }
            var info = InfoOf(Mathf.Max(1, me.headShown));
            var hood = new GameObject("Hero_Hood").transform;
            hood.SetParent(me.head, false);
            var model = Instantiate(prefab, hood, false).transform;
            var rs = model.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                float hb = bb.size.y / Mathf.Max(1e-6f, hood.lossyScale.y);
                if (hb > 1e-5f) model.localScale *= hoodInfo.height / hb; // (its units: the measures')
            }
            // a little bigger than the head all round, its top just over the head's
            float s = Mathf.Max(info.width * 1.2f / hoodInfo.width, info.depth * 1.16f / hoodInfo.depth); // (roomy: the hair stays inside)
            hood.localScale = Vector3.one * s;
            hood.localPosition = new Vector3(0f, info.top + 0.07f, 0.0f);
            string key = "hood" + color.Value;
            if (!materials.TryGetValue(key, out var m) || m == null)
            {
                m = MaterialFactory.CreateSurface(color.Value, Color.black, 0.15f, 0f);
                var tex = Resources.Load<Texture2D>(Folder + "hood");
                if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex); }
                materials[key] = m;
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = m;
                r.shadowCastingMode = Shapes.CastShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>The workshop's arm and leg gear on the bones (what was there before goes).</summary>
        public static void Gear(Transform skin, string arms, Color armsColor, string legs, Color legsColor)
        {
            var old = new List<GameObject>();
            foreach (var t in skin.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Gear_")) old.Add(t.gameObject);
            foreach (var g in old) if (g != null) DestroyImmediate(g);
            var facing = skin.forward;
            foreach (var side in new[] { "Left", "Right" })
            {
                if (arms != null)
                    foreach (var limb in GearModels.LimbsOf(arms))
                        Put(skin, arms, limb, armsColor, side, facing);
                if (legs != null)
                    foreach (var limb in GearModels.LimbsOf(legs))
                        Put(skin, legs, limb, legsColor, side, facing);
            }
        }

        private static void Put(Transform skin, string id, GearModels.Limb limb, Color color, string side, Vector3 facing)
        {
            Transform a, b; float thick; var face = facing;
            switch (limb) // (armoured limbs: thicker than the old foi's)
            {
                case GearModels.Limb.UpperArm: a = FindBone(skin, side + "Arm"); b = FindBone(skin, side + "ForeArm"); thick = 1.45f; break;
                case GearModels.Limb.Forearm: a = FindBone(skin, side + "ForeArm"); b = FindBone(skin, side + "Hand"); thick = 1.5f; break;
                case GearModels.Limb.Hand: a = FindBone(skin, side + "Hand"); b = FindBone(skin, side + "HandMiddle4") ?? FindBone(skin, side + "HandMiddle2"); thick = 1.3f; break;
                case GearModels.Limb.Foot: a = FindBone(skin, side + "Foot"); b = FindBone(skin, side + "ToeBase"); thick = 1.5f; face = skin.up; break;
                default: a = FindBone(skin, side + "Leg"); b = FindBone(skin, side + "Foot"); thick = 1.5f; break;
            }
            if (a == null || b == null) return;
            var frame = GearModels.Frame("Gear_" + limb, a.position, b.position, face, thick);
            GearModels.Build(frame, id, limb, color);
            frame.SetParent(a, true);
        }

        /// <summary>A blow: a chop, the eyes narrowed.</summary>
        public void Punch()
        {
            if (!ready) Setup();
            punchLeft = 1.1f;
            chop = 0f;
        }

        /// <summary>Hurt or cross: the eyes narrow.</summary>
        public void Anger() { angerLeft = 1.2f; }

        /// <summary>A win: it claps.</summary>
        public void Clap()
        {
            if (!ready) Setup();
            clapLeft = 2.6f;
            clap = 0f;
        }

        // Moves made on this very body (Resources/Heroes/Foi/hero_*): a dance, the ultimate's cast and kick. One plays at a
        // time over the code's poses, from its start to its end (or until foi walks off).
        private static readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        private AnimationClip playing;
        /// <summary>The body's animation layers (base, upper body, full body, additive reaction): see <see cref="HeroAnimLayers"/>.</summary>
        private HeroAnimLayers layers;
        private AnimationClip upperPlaying;
        private float upperTime, upperRate = 1f;
        private float playingTime, playingRate = 1f;
        private bool playingReal, playingLoop;
        /// <summary>The clip playing holds its last frame once done (a fall: lying until the get-up), and its body's own
        /// travel (a lunge, a dodge) never ends it as walking off would.</summary>
        private bool playingHold, playingStay;

        private static AnimationClip ClipOf(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            c = null;
            foreach (var x in Resources.LoadAll<AnimationClip>(Folder + "hero_" + name)) if (!x.name.StartsWith("__preview__")) { c = x; break; }
            clips[name] = c;
            return c;
        }

        /// <summary>Plays move <paramref name="name"/> (dance1..4, cast, kick) once; false if there is no such move.</summary>
        /// <param name="realTime">Plays in real seconds, whatever the slow motion round it (the ultimates).</param>
        /// <param name="loop">Over and over until <see cref="StopMove"/> (the ultimate, start to end).</param>
        public bool PlayMove(string name, float rate = 1f, bool realTime = false, bool loop = false)
        {
            if (!ready) Setup();
            var c = ClipOf(name);
            if (c == null) return false;
            playing = c; playingTime = 0f; playingFrom = 0f; playingTo = c.length; playingRate = rate; playingReal = realTime; playingLoop = loop; playingHold = playingStay = false;
            // (from where it stands now: a leap's landing just before must not read as walking off and end the move at once)
            lastPos = transform.position; speed = 0f;
            // played through the body's animator (a graph of its own, evaluated by hand)
            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            Layers(animator).Play(HeroAnimLayers.FullBody, c);
            clap = 1f; chop = 1f;
            return true;
        }

        /// <summary>The move playing ends now (a looping one included).</summary>
        public void StopMove() { if (playing != null) ClipEnded(playing, Mathf.Min(playingTime, playingTo)); playing = null; playingLoop = playingHold = playingStay = false; }

        /// <summary>A clip played that holds its last frame (<paramref name="hold"/>) and is not ended by the body's own
        /// travel (<paramref name="stay"/>).</summary>
        private bool PlayClip(string name, float rate, bool hold, bool stay)
        {
            if (!PlayMove(name, rate)) return false;
            playingHold = hold; playingStay = stay;
            return true;
        }

        /// <summary>The clip playing is done (a held one on its last frame).</summary>
        private bool ClipDone => playing == null || playingHold && playingTime >= playingTo;

        private HeroAnimLayers Layers(Animator animator)
        {
            if (layers == null || !layers.Valid) layers = new HeroAnimLayers(animator, transform, spine, name);
            return layers;
        }

        /// <summary>Plays move <paramref name="name"/> on the upper body only (the spine and up), the legs still the body's own
        /// (walking, standing); false if there is no such move.</summary>
        public bool PlayUpper(string name, float rate = 1f)
        {
            if (!ready) Setup();
            var c = ClipOf(name);
            if (c == null) return false;
            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            Layers(animator).Play(HeroAnimLayers.UpperBody, c);
            upperPlaying = c; upperTime = 0f; upperRate = rate;
            return true;
        }

        /// <summary>The upper-body move ends now.</summary>
        public void StopUpper() { upperPlaying = null; if (layers != null) layers.Stop(HeroAnimLayers.UpperBody); }

        // ---------- The picture clock and layer (the hit freeze, the slow motion, the push of a blow) ----------

        private VisualTime vclock;
        private VisualRoot vroot;
        private Vector3 hipsRest;
        private bool hipsKnown;

        /// <summary>This body's frame time on its picture clock (frozen by a hit, slowed in a big moment).</summary>
        private float VDt => vclock != null ? vclock.DeltaTime : Time.deltaTime * VisualTime.GlobalScale;

        // ---------- Struck: the body's springy answer, the face, the squash ----------

        private readonly HitSpring spring = new HitSpring();
        /// <summary>The last reaction played (direction / height / strength / variant), for the duel lab's readings.</summary>
        public string LastReaction { get; private set; } = "";
        private float squintUntil, squintAmount, shutUntil;
        /// <summary>The face's own lids (0 open .. 1 shut), one lid shut (a wink), the brows (-1 down .. 1 up), the hair on end:
        /// set by the face's expression.</summary>
        public float FaceLid { get; set; }
        public bool FaceWink { get; set; }
        public float FaceBrows { get; set; }
        public bool HairUp { get; set; }
        /// <summary>The head worn (its file) and its skin's colour.</summary>
        public string HeadFileName { get; private set; } = "head_m";
        public Color SkinColor { get; private set; } = new Color(0.78f, 0.64f, 0.56f);
        private Renderer faceRenderer;
        private Transform hairPart;
        private Vector3 hairRest;

        /// <summary>The head's own skin (its face: not the hair, the brows or the lids).</summary>
        public Renderer FaceRenderer
        {
            get
            {
                if (faceRenderer != null || head == null) return faceRenderer;
                foreach (var r in head.GetComponentsInChildren<Renderer>(true))
                {
                    string nm = r.name.ToLowerInvariant();
                    if (r.name == "Cover" || nm.Contains("hair") || nm.Contains("brows")) continue;
                    faceRenderer = r;
                    break;
                }
                return faceRenderer;
            }
        }
        private float squashT = 1f, squashAmp;
        private Vector3 skinScale;
        private bool skinScaleKnown;
        private Transform brows;
        private Vector3 browsRest;
        private bool browsKnown;
        private float browsDownUntil, browsDown;

        /// <summary>
        /// A blow felt (its feel's contact): the spine, chest, head and shoulders thrown by its direction, height and
        /// strength and springing back (a different variant each time); a medium blow adds a step back, a heavy one two
        /// with the arms out (the stagger), and squashes the body a moment; a launch throws it up and down on its back.
        /// The eyes squint (hard on a heavy blow), the brows come down. Only the picture: the place is the rules'.
        /// </summary>
        public void React(Combat.HitDirection dir, Combat.HitHeight height, Combat.HitStrength strength, Vector3 from, float intensity = 1f)
        {
            if (!ready) Setup();
            var r = Combat.CombatLibrary.Reactions;
            if (strength == Combat.HitStrength.Launch) { LaunchUp(from); Squint(1f, 0.9f); return; } // (the design's 1 s in the air, a back flip)
            int variant = spring.Kick(dir, height, r.Degrees(strength) * Mathf.Clamp(intensity, 0.5f, 2f), r.Recovery(strength));
            LastReaction = dir + "/" + height + "/" + strength + "/v" + variant;
            switch (strength)
            {
                case Combat.HitStrength.Light: Squint(0.45f, 0.15f); if (PlayAction(Combat.DuelAction.HitLight)) ReactionClip = "hit_small"; break;
                case Combat.HitStrength.Medium: Rock(from, intensity); Squint(0.6f, 0.25f); Brows(0.6f, 0.3f); break; // (the stagger: a step back, rocking)
                default: DriveBack(from); Squint(1f, 0.4f); Brows(1f, 0.45f); Squash(1f); break; // (driven back two steps, onto a knee)
            }
        }

        private float crouchUntil, crouchAmount, crouchFor = 0.15f;

        /// <summary>Low on health (the game sets it in a duel): the body hunched, the shoulders heaving with its breath.</summary>
        public bool Weary { get; set; }

        /// <summary>The body crouches (0..1: the knees giving, the hips down) for <paramref name="seconds"/>, easing back up.</summary>
        public void Crouch(float amount, float seconds)
        {
            crouchAmount = Mathf.Clamp01(amount);
            crouchFor = Mathf.Max(0.05f, seconds);
            crouchUntil = Time.time + crouchFor;
        }

        /// <summary>The eyes squeezed (0..1) for a while.</summary>
        public void Squint(float amount, float seconds)
        {
            squintAmount = Mathf.Max(Time.time < squintUntil ? squintAmount : 0f, Mathf.Clamp01(amount));
            squintUntil = Mathf.Max(squintUntil, Time.time + seconds);
        }

        /// <summary>The eyes shut for a while (knocked out); 0 opens them again at once.</summary>
        public void EyesShut(float seconds) => shutUntil = seconds <= 0f ? 0f : Time.time + seconds;

        /// <summary>The body squashed (8%) and stretched (4%) back in a moment (a heavy blow, a landing).</summary>
        public void Squash(float amount) { squashT = 0f; squashAmp = Mathf.Clamp01(amount); }

        private void Brows(float amount, float seconds)
        {
            browsDown = Mathf.Clamp01(amount);
            browsDownUntil = Time.time + seconds;
        }

        private float ExpressionLids()
        {
            if (Time.time < shutUntil) return 1f;
            if (Time.time < squintUntil) return squintAmount * Mathf.Clamp01((squintUntil - Time.time) / 0.12f + 0.3f);
            return 0f;
        }

        private void Reacting(float dt)
        {
            // the springy answer on the bones (in the body's own frame: it faces its striker)
            spring.Step(dt);
            if (spring.Active && (playing == null || ReactionClip == "hit_small" || ReactionClip == "stagger") && knock == null)
            {
                var frame = transform.parent != null ? transform.parent : transform;
                Vector3 up = frame.up, right = frame.right, ahead = frame.forward;
                var bend = Quaternion.AngleAxis(spring[HitSpring.Yaw] * 0.5f, up) * Quaternion.AngleAxis(spring[HitSpring.Roll] * 0.5f, -ahead) * Quaternion.AngleAxis(spring[HitSpring.Pitch] * 0.5f, right);
                if (spine != null) spine.rotation = bend * spine.rotation;
                if (chest != null) chest.rotation = bend * chest.rotation;
                if (neck != null) neck.rotation = Quaternion.AngleAxis(spring[HitSpring.Head], right) * neck.rotation;
                if (armL != null) armL.rotation = Quaternion.AngleAxis(spring[HitSpring.ShoulderL], ahead) * armL.rotation;
                if (armR != null) armR.rotation = Quaternion.AngleAxis(-spring[HitSpring.ShoulderR], ahead) * armR.rotation;
                if (hips != null && Mathf.Abs(spring[HitSpring.Hips]) > 1e-4f)
                {
                    float height = head != null ? Mathf.Max(0.3f, head.position.y - transform.position.y) : 1f;
                    hips.position += up * spring[HitSpring.Hips] * height;
                }
            }
            // in a duel: the weary hunch (low health), a sword fighter's light bounce on its toes
            if (playing == null && knock == null && speed < 0.4f)
            {
                var fr = transform.parent != null ? transform.parent : transform;
                float hgt = head != null ? Mathf.Max(0.3f, head.position.y - transform.position.y) : 1f;
                if (Weary)
                {
                    float breath = Mathf.Sin(time * 3.2f);
                    var hunch = Quaternion.AngleAxis(9f + 2f * breath, fr.right);
                    if (chest != null) chest.rotation = hunch * chest.rotation;
                    if (armL != null) armL.rotation = Quaternion.AngleAxis(3f * breath, fr.forward) * armL.rotation;
                    if (armR != null) armR.rotation = Quaternion.AngleAxis(-3f * breath, fr.forward) * armR.rotation;
                }
                if (HeldWeapon.DuelStance && hips != null && heldCache != null && heldCache.Family == Combat.WeaponFamily.Sword && heldCache.Current == null)
                    hips.position += fr.up * hgt * 0.012f * Mathf.Abs(Mathf.Sin(time * 5f));
            }
            // a crouch: down quickly, easing back up
            if (Time.time < crouchUntil && hips != null && playing == null)
            {
                float left = (crouchUntil - Time.time) / crouchFor;
                float k = crouchAmount * (left > 0.7f ? (1f - left) / 0.3f : Mathf.SmoothStep(0f, 1f, left / 0.7f)); // (down quickly, easing back up)
                float height = head != null ? Mathf.Max(0.3f, head.position.y - transform.position.y) : 1f;
                var frame = transform.parent != null ? transform.parent : transform;
                hips.position -= frame.up * height * 0.09f * Mathf.Clamp01(k);
            }
            // squash and stretch
            if (!skinScaleKnown) { skinScale = transform.localScale; skinScaleKnown = true; }
            if (squashT < 1f)
            {
                squashT = Mathf.Min(1f, squashT + dt / 0.12f);
                float y = squashT < 0.25f ? Mathf.Lerp(0f, -0.08f, squashT / 0.25f) : squashT < 0.65f ? Mathf.Lerp(-0.08f, 0.04f, (squashT - 0.25f) / 0.4f) : Mathf.Lerp(0.04f, 0f, (squashT - 0.65f) / 0.35f);
                float sy = 1f + y * squashAmp;
                float sx = 1f / Mathf.Sqrt(Mathf.Max(0.5f, sy));
                transform.localScale = new Vector3(skinScale.x * sx, skinScale.y * sy, skinScale.z * sx);
                if (squashT >= 1f) transform.localScale = skinScale;
            }
            // the brows down a moment (a hard blow)
            if (!browsKnown && head != null)
            {
                browsKnown = true;
                foreach (var r in head.GetComponentsInChildren<Renderer>(true)) if (r.name.ToLowerInvariant().Contains("brows")) { brows = r.transform; break; }
                if (brows != null) browsRest = brows.localPosition;
            }
            if (brows != null)
            {
                float k = Time.time < browsDownUntil ? browsDown : 0f;
                brows.localPosition = Vector3.Lerp(brows.localPosition, browsRest + Vector3.down * 0.035f * k + Vector3.up * 0.032f * FaceBrows, Mathf.Min(1f, dt * 25f)); // (the face's brows: up worried or surprised, down angry)
            }
            // the hair standing on end (a shock)
            if (hairPart == null && head != null) foreach (var r in head.GetComponentsInChildren<Renderer>(true)) if (r.name.ToLowerInvariant().Contains("hair")) { hairPart = r.transform; hairRest = hairPart.localScale; break; }
            if (hairPart != null)
            {
                var want = HairUp ? new Vector3(hairRest.x * 1.04f, hairRest.y * (1.16f + 0.03f * Mathf.Sin(Time.time * 60f)), hairRest.z * 1.04f) : hairRest;
                hairPart.localScale = Vector3.Lerp(hairPart.localScale, want, Mathf.Min(1f, dt * 30f));
            }
        }

        // ---------- The body in its own blows (the weapon's swing read each frame) ----------

        /// <summary>Every rigged body in play (a weapon stops on the others' bodies, never passing through).</summary>
        public static readonly List<SkinnedHero> Live = new List<SkinnedHero>();

        private void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
        private void OnDisable() { Live.Remove(this); if (ready) CleanSlate(); } // (hidden mid-reaction: its coroutines stop here, so nothing they set may stay behind)

        /// <summary>The body as a capsule (world): from above its feet to its neck, and how thick.</summary>
        public void BodyCapsule(out Vector3 bottom, out Vector3 top, out float radius)
        {
            var up = transform.parent != null ? transform.parent.up : Vector3.up;
            float height = head != null ? Mathf.Max(0.3f, head.position.y - transform.position.y) : 1.2f;
            var basePos = hips != null ? new Vector3(hips.position.x, transform.position.y, hips.position.z) : transform.position;
            bottom = basePos + up * height * 0.18f;
            top = basePos + up * height * 0.95f;
            radius = height * 0.2f;
        }

        private HeldWeapon heldCache;
        private float heldLookAt;
        private bool spinning;
        private Quaternion spinBase;

        /// <summary>
        /// The body goes with the weapon's blow, by the blow's direction and the weapon's family (a sword lightly, an axe and
        /// a heavy weapon with all its weight): a sweep turns the chest back then through; a blow from overhead leans back,
        /// then forward with the knees giving (a heavy weapon most); an upward blow crouches, then rises leaning back; a
        /// thrust lunges forward; a whirl turns the whole body round with the weapon. Only the picture: the place is the rules'.
        /// </summary>
        private void BodyIntoBlow()
        {
            if ((heldCache == null || !heldCache) && Time.time >= heldLookAt)
            {
                heldLookAt = Time.time + 0.5f;
                heldCache = transform.parent != null ? transform.parent.GetComponentInChildren<HeldWeapon>() : null;
            }
            var blow = heldCache != null ? heldCache.Current : null;
            float spinAng = heldCache != null ? heldCache.SpinAngle : -1f;
            bool busy = playing != null || Flailing || knock != null;
            if (spinning && (spinAng < 0f || busy)) { spinning = false; transform.localRotation = spinBase; }
            if (blow == null || busy) return;
            var fam = heldCache.Family;
            float amp = fam == Combat.WeaponFamily.Sword ? 0.8f : fam == Combat.WeaponFamily.Spear ? 0.65f : fam == Combat.WeaponFamily.Axe ? 1.2f : 1.35f;
            float p = heldCache.Progress;
            // -1 wound up .. +1 struck through, held, and back to 0 at rest
            float c = p < 0.3f ? -Mathf.SmoothStep(0f, 1f, p / 0.3f)
                : p < 0.55f ? Mathf.Lerp(-1f, 1f, Mathf.SmoothStep(0f, 1f, (p - 0.3f) / 0.25f))
                : p < 0.7f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (p - 0.7f) / 0.3f);
            var frame = transform.parent != null ? transform.parent : transform;
            Vector3 up = frame.up, right = frame.right, ahead = frame.forward;
            float height = head != null ? Mathf.Max(0.3f, head.position.y - transform.position.y) : 1f;
            float yaw = 0f, pitch = 0f, drop = 0f, lunge = 0f;
            switch (blow.direction)
            {
                case Combat.HitDirection.Horizontal_RightToLeft: yaw = -c * 32f * amp; break;
                case Combat.HitDirection.Horizontal_LeftToRight: yaw = c * 32f * amp; break;
                case Combat.HitDirection.Overhead:
                    pitch = c * 22f * amp;
                    drop = Mathf.Max(0f, c) * 0.035f * height * (fam == Combat.WeaponFamily.Heavy || fam == Combat.WeaponFamily.Axe ? 1.3f : 0.5f);
                    break;
                case Combat.HitDirection.Upward:
                    pitch = -c * 18f * amp;
                    drop = Mathf.Max(0f, -c) * 0.03f * height;
                    break;
                case Combat.HitDirection.Thrust:
                    pitch = Mathf.Max(0f, c) * 14f * amp;
                    lunge = Mathf.Max(0f, c) * 0.06f * height * amp;
                    break;
                case Combat.HitDirection.Spin:
                    if (spinAng >= 0f)
                    {
                        if (!spinning) { spinning = true; spinBase = transform.localRotation; }
                        transform.localRotation = spinBase * Quaternion.Euler(0f, spinAng, 0f);
                    }
                    break;
            }
            var bend = Quaternion.AngleAxis(yaw * 0.5f, up) * Quaternion.AngleAxis(pitch * 0.5f, right);
            if (spine != null) spine.rotation = bend * spine.rotation;
            if (chest != null) chest.rotation = bend * chest.rotation;
            if (hips != null && (drop > 0f || lunge > 0f)) hips.position += ahead * lunge - up * drop;
        }

        /// <summary>The blow's push on the body's picture: the hips (and all on them) moved by the visual root's offset.</summary>
        private void PushedByBlow()
        {
            if (hips == null || vroot == null) return;
            if (!hipsKnown) { hipsRest = hips.localPosition; hipsKnown = true; }
            if (playing == null && !clipsWrote) hips.localPosition = hipsRest; // (a clip writes the hips itself)
            var off = vroot.Offset;
            if (off.sqrMagnitude > 1e-8f) hips.position += off;
        }

        // ---------- Knocks (the duel's big blows) ----------

        private Coroutine knock, pop, hop, stagger;

        /// <summary>How much its arms, legs and body are thrown about (0 in its own control .. 1 flung through the air);
        /// 'flat' is how much it lies spread on the ground.</summary>
        private float flail, flat;
        /// <summary>Stepping back under a blow: how much (0..1), and how far through its steps.</summary>
        private float reel, reelStep;
        /// <summary>Getting up: how deep its crouch (the knees drawn up and bent), and how much one hand pushes on the ground.</summary>
        private float rise, push;

        /// <summary>Thrown about (reeling, flying, lying): the arms are its own, what it holds goes with its hands.</summary>
        public bool Flailing => flail > 0.04f || flat > 0.04f || rise > 0.04f || push > 0.04f;

        /// <summary>
        /// Struck: it reels back (away from <paramref name="from"/>) and a little to one side, and finds its feet again;
        /// <paramref name="strength"/> 0..1 (blows in a row hit harder: it reels further, leans further).
        /// </summary>
        public void Stagger(Vector3 from, float strength)
        {
            reactedAt = Time.time;
            if (knock != null) return; // (already going down)
            KnowRest();
            if (stagger != null) StopCoroutine(stagger);
            stagger = StartCoroutine(Staggering(from, Mathf.Clamp01(strength)));
        }

        private Vector3 bodyPos;
        private Quaternion bodyRot;
        private bool bodyKnown;

        /// <summary>Where a blow lands on it: its head, or the middle of its chest (in the world).</summary>
        public Vector3 HeadPoint => head != null ? head.position + Vector3.up * 0.15f * HeadSize : transform.position + Vector3.up * 1.3f;
        public Vector3 ChestPoint => chest != null ? chest.position : transform.position + Vector3.up * 0.8f;
        private float HeadSize => head != null ? Mathf.Max(0.2f, head.lossyScale.y) : 1f;

        private Coroutine jolt;

        private Quaternion headRest;
        private bool headRestKnown;

        private float shudderT = 1f, shudderPow;
        private Vector3 shudderAxis;

        /// <summary>A blow felt in the body: the chest and back shake hard away from <paramref name="from"/> and settle,
        /// the arms jerk (0.4 s; <paramref name="strength"/> 0..1).</summary>
        public void Shudder(Vector3 from, float strength)
        {
            reactedAt = Time.time;
            var away = transform.position - from; away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            shudderAxis = Vector3.Cross(Vector3.up, away.normalized);
            shudderPow = Mathf.Clamp01(strength);
            shudderT = 0f;
        }

        private void Shuddering(float dt)
        {
            if (shudderT >= 1f) return;
            shudderT = Mathf.Min(1f, shudderT + dt / 0.4f);
            float fade = (1f - shudderT) * (1f - shudderT);
            float a = (16f + 18f * shudderPow) * fade * Mathf.Cos(shudderT * Mathf.PI * 7f); // (thrown back first, then shaking out)
            float twist = (9f + 9f * shudderPow) * fade * Mathf.Sin(shudderT * Mathf.PI * 9f);
            var bend = Quaternion.AngleAxis(a * 0.5f, shudderAxis) * Quaternion.AngleAxis(twist * 0.5f, Vector3.up);
            if (spine != null) spine.rotation = bend * spine.rotation;
            if (chest != null) chest.rotation = bend * chest.rotation;
            if (neck != null) neck.rotation = Quaternion.AngleAxis(-a * 0.35f, shudderAxis) * neck.rotation;
            if (armL != null) armL.rotation = Quaternion.AngleAxis(a * 0.8f, shudderAxis) * armL.rotation;
            if (armR != null) armR.rotation = Quaternion.AngleAxis(a * 0.4f, shudderAxis) * armR.rotation; // (the weapon arm less)
        }

        /// <summary>Whatever knock it was in, it stands again at once (in its own place, nothing thrown about).</summary>
        public void StandUp()
        {
            spring.Clear();
            foreach (var c in new[] { knock, stagger, hop, pop }) if (c != null) StopCoroutine(c);
            knock = stagger = hop = pop = null;
            flail = flat = rise = push = reel = 0f;
            InAir = false; kneelLean = 0f; ReactionClip = "";
            if (playingHold) StopMove(); // (a fall's clip held lying: let go)
            if (bodyKnown) { transform.localPosition = bodyPos; transform.localRotation = bodyRot; }
        }

        /// <summary>Its own standing place under its parent, taken once before any knock moves it.</summary>
        private void KnowRest()
        {
            if (bodyKnown) return;
            bodyKnown = true;
            bodyPos = transform.localPosition; bodyRot = transform.localRotation;
        }

        private Vector3 staggerRest;
        private bool staggerRestKnown;

        private System.Collections.IEnumerator Staggering(Vector3 from, float strength)
        {
            var restRot = bodyRot;
            staggerRest = bodyPos; staggerRestKnown = true;
            var away = transform.position - from; away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            away.Normalize();
            var side = Vector3.Cross(Vector3.up, away) * (Random.value < 0.5f ? -1f : 1f);
            var push = (away + side * 0.45f).normalized * (0.12f + 0.45f * strength); // (world units: back and a little aside)
            var axis = Vector3.Cross(Vector3.up, away);
            float lean = 3f + 7f * strength, scale = transform.parent != null ? Mathf.Max(0.01f, transform.parent.lossyScale.y) : 1f; // (it rocks, it never topples)
            float outT = 0.09f + 0.05f * strength, backT = 0.28f + 0.25f * strength;
            for (float e = 0f; e < outT + backT; e += VDt)
            {
                float k = e < outT ? Mathf.SmoothStep(0f, 1f, e / outT) : 1f - Mathf.SmoothStep(0f, 1f, (e - outT) / backT);
                var parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
                transform.rotation = Quaternion.AngleAxis(k * lean, axis) * parentRot * restRot;
                var local = transform.parent != null ? transform.parent.InverseTransformVector(push) : push;
                transform.localPosition = staggerRest + local * k + Vector3.up * (0.03f * strength * Mathf.Sin(k * Mathf.PI) / scale);
                flail = k * (0.12f + 0.2f * strength); // (the arms only thrown a little)
                reel = k * (0.5f + 0.5f * strength); reelStep = e / (outT + backT); // the feet stepping back under it
                yield return null;
            }
            transform.localRotation = restRot;
            transform.localPosition = staggerRest;
            flail = 0f; reel = 0f;
            stagger = null;
        }

        /// <summary>A big blow: it flies back off its feet (away from <paramref name="from"/>), lands flat on its back, gets up.</summary>
        public void Launch(Vector3 from, float height = 1.3f, float lie = 0.3f)
        {
            if (knock != null) return; // (already down: it gets up, never knocked down again on the ground)
            KnowRest();
            if (hop != null) { StopCoroutine(hop); hop = null; }
            if (stagger != null) { StopCoroutine(stagger); stagger = null; if (staggerRestKnown) transform.localPosition = staggerRest; }
            if (knock != null) StopCoroutine(knock);
            knock = StartCoroutine(Knocked(from, lie, height));
        }

        private System.Collections.IEnumerator Knocked(Vector3 from, float lie, float arc = 0f)
        {
            var away = transform.position - from; away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            var axis = Vector3.Cross(Vector3.up, away.normalized); // (tipping over this: the top goes away from the blow)
            var restLocal = bodyRot; // (its own standing place: never one a blow left it in)
            var restPos = bodyPos;
            float lift = 0.22f / Mathf.Max(0.01f, transform.parent != null ? transform.parent.lossyScale.y : 1f); // (its back's thickness, in its parent's units)
            float Tilt(float k)
            {
                var parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
                transform.rotation = Quaternion.AngleAxis(k * 82f, axis) * parentRot * restLocal;
                transform.localPosition = restPos + Vector3.up * (lift * Mathf.Clamp01(k)); // (raised as it goes over: lying on the floor, not sunk in it)
                return k;
            }
            if (arc > 0f)
            {
                // off its feet: up in an arc, turning over onto its back on the way, down flat
                float up = arc / Mathf.Max(0.01f, transform.parent != null ? transform.parent.lossyScale.y : 1f);
                const float Air = 0.5f;
                for (float e = 0f; e < Air; e += VDt)
                {
                    float k = e / Air;
                    float tk = Tilt(Mathf.SmoothStep(0f, 1f, k));
                    transform.localPosition = restPos + Vector3.up * (4f * k * (1f - k) * up + lift * tk);
                    flail = Mathf.Min(1f, k * 4f);
                    yield return null;
                }
            }
            else
                for (float e = 0f; e < 0.32f; e += VDt) { float k = e / 0.32f; Tilt(k * k); yield return null; } // falling faster and faster
            Tilt(1f);
            for (float e = 0f; e < lie; e += VDt)
            {
                Tilt(1f + Mathf.Sin(e * 20f) * Mathf.Exp(-e * 6f) * 0.06f); // a bounce, then still
                flail = Mathf.Max(0f, 1f - e * 5f) * 0.8f; // (the arms and legs still going as it lands)
                flat = Mathf.Min(1f, e * 4f);
                yield return null;
            }
            // Up again at once, as a person gets up: it sits up with a hand pushing on the ground behind it, the knees
            // drawn up and bent, then stands up from the crouch.
            flail = 0f;
            const float SitUp = 0.32f, StandUp = 0.36f;
            for (float e = 0f; e < SitUp; e += VDt)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / SitUp);
                Tilt(Mathf.Lerp(1f, 0.42f, k)); // (the back off the ground: sitting)
                flat = 1f - k;
                rise = k; push = k;
                yield return null;
            }
            for (float e = 0f; e < StandUp; e += VDt)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / StandUp);
                Tilt(Mathf.Lerp(0.42f, 0f, k)); // (up onto its feet)
                rise = 1f - k; push = Mathf.Max(0f, 1f - k * 2.5f); // (the hand leaves the ground first)
                yield return null;
            }
            flail = flat = rise = push = 0f;
            transform.localRotation = restLocal;
            transform.localPosition = restPos;
            reel = 0f;
            knock = null;
        }

        /// <summary>A jump straight up and down (<paramref name="height"/> in world units, <paramref name="time"/> seconds).</summary>
        public void Hop(float height, float time)
        {
            if (hop != null || knock != null) return;
            KnowRest();
            hop = StartCoroutine(Hopping(height, time));
        }

        private System.Collections.IEnumerator Hopping(float height, float time)
        {
            var restPos = bodyPos;
            float local = height / Mathf.Max(0.01f, transform.parent != null ? transform.parent.lossyScale.y : 1f);
            for (float e = 0f; e < time; e += VDt)
            {
                float k = e / time;
                transform.localPosition = restPos + Vector3.up * (4f * k * (1f - k)) * local;
                yield return null;
            }
            transform.localPosition = restPos;
            hop = null;
        }

        /// <summary>The length of move <paramref name="name"/> in seconds (0: none).</summary>
        public static float MoveLength(string name) { var c = ClipOf(name); return c != null ? c.length : 0f; }

        /// <summary>A win: the dance chosen in the workshop (the first one by default; clapping if there is no clip). True
        /// when a dance clip plays (the body moves itself then: no spinning it around).</summary>
        public bool Celebrate(string dance)
        {
            string move = dance != null && dance.StartsWith("dance.d") ? "dance" + dance.Substring(7) : "dance0";
            if (PlayMove(move)) { DanceDrop.Drop(this); return true; } // (danced with free hands: the weapon and the shield on the ground)
            if (!PlayMove("victory")) Clap();
            return false;
        }

        private static Transform FindBone(Transform root, string bone)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == bone || t.name.EndsWith(":" + bone)) return t;
            return null;
        }

        // The pose, by code: each bone is turned in the body's own frame (right, up, forward), about where it stood when made
        // (arms out to the sides), carried along by the bones above it.
        private readonly Dictionary<Transform, Quaternion> restLocal = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> restRel = new Dictionary<Transform, Quaternion>();
        private Transform armL, foreL, legL, legR, kneeL, kneeR, spine, chest, neck;
        /// <summary>Which way each turn goes on this skeleton (found once, by trying): the arms down, the arm forward, the knee back.</summary>
        private float downL = 1f, downR = -1f, forward = 1f, kneeBack = 1f;

        private Quaternion Frame => transform.parent != null ? transform.parent.rotation : transform.rotation;
        /// <summary>Where this body is in its own rhythm (set once: two side by side never move as one).</summary>
        private float phase = -1f;

        private void Turn(Transform b, Vector3 axis, float degrees)
        {
            if (b == null || !restRel.ContainsKey(b)) return;
            var pr = b.parent != null && restRel.ContainsKey(b.parent) ? restRel[b.parent] : Quaternion.Inverse(Frame) * (b.parent != null ? b.parent.rotation : Quaternion.identity);
            b.localRotation = Quaternion.Inverse(pr) * Quaternion.AngleAxis(degrees, axis) * pr * b.localRotation;
        }

        /// <summary>Stands in its idle pose at once (for a picture taken before its first frame).</summary>
        public void PoseNow()
        {
            if (!ready) Setup();
            if (!ready) return;
            Rest();
            Pose(0f);
        }

        private void Rest()
        {
            foreach (var kv in restLocal) kv.Key.localRotation = kv.Value;
        }

        private void Setup()
        {
            ready = true;
            if (phase < 0f) phase = Random.Range(0f, 10f);
            var anim = GetComponent<Animator>();
            if (anim != null) anim.enabled = true; // (no controller: it only moves the body when a move's graph is evaluated)
            hips = FindBone(transform, "Hips"); spine = FindBone(transform, "Spine"); chest = FindBone(transform, "Spine2"); neck = FindBone(transform, "Neck");
            armL = FindBone(transform, "LeftArm"); foreL = FindBone(transform, "LeftForeArm");
            armR = FindBone(transform, "RightArm"); foreR = FindBone(transform, "RightForeArm"); handR = FindBone(transform, "RightHand"); fingerR = FindBone(transform, "RightHandMiddle1");
            legL = FindBone(transform, "LeftUpLeg"); legR = FindBone(transform, "RightUpLeg"); kneeL = FindBone(transform, "LeftLeg"); kneeR = FindBone(transform, "RightLeg");
            // the rest pose from the model as made (a copy is made of a posed body: its bones are not at rest)
            var asMade = new Dictionary<string, Quaternion>();
            var prefab = Resources.Load<GameObject>(Folder + "body");
            if (prefab != null)
                foreach (var pb in prefab.GetComponentsInChildren<Transform>(true)) asMade[pb.name] = pb.localRotation;
            foreach (var bone in transform.GetComponentsInChildren<Transform>(true))
            {
                if (!bone.name.Contains(":")) continue; // the skeleton's bones only
                restLocal[bone] = asMade.TryGetValue(bone.name, out var q) ? q : bone.localRotation;
            }
            Rest();
            KnowFingers(); // (the fingers' turns toward the palms, worked out at rest)
            var frameInv = Quaternion.Inverse(Frame);
            foreach (var kv in restLocal) restRel[kv.Key] = frameInv * kv.Key.rotation;
            if (head == null) head = FindHead();
            // which way is which on this skeleton: try each turn and keep the sign that does it
            var handL = FindBone(transform, "LeftHand");
            float Try(Transform b, Vector3 axis, Transform end, System.Func<Vector3, float> measure)
            {
                Rest(); float before = measure(end.position);
                Turn(b, axis, 40f); float after = measure(end.position);
                Rest();
                return after > before ? 1f : -1f;
            }
            var up = Vector3.up; var right = Vector3.right; var fwd = Vector3.forward;
            Vector3 Local(Vector3 w) => Quaternion.Inverse(Frame) * (w - transform.position);
            if (armL != null && handL != null) downL = -Try(armL, fwd, handL, w => Local(w).y);
            if (armR != null && handR != null) downR = -Try(armR, fwd, handR, w => Local(w).y);
            if (armR != null && handR != null) { Rest(); Turn(armR, fwd, downR * 75f); float before = Local(handR.position).z; Turn(armR, right, 40f); forward = Local(handR.position).z > before ? 1f : -1f; }
            var footL = FindBone(transform, "LeftFoot");
            if (kneeL != null && footL != null) kneeBack = -Try(kneeL, right, footL, w => Local(w).z);
            Rest();
            lastPos = transform.position;
            Pose(0f);
            if (handR != null && transform.parent != null) handRest = transform.parent.InverseTransformPoint(handR.position) + new Vector3(0.02f, 0.04f, 0.06f); // a little forward and up: held, not hanging
        }

        /// <summary>This frame's pose: standing (breathing, its weight shifting, ready), walking (legs and arms swinging,
        /// leaning into it), a chop (the whole body behind it), a clap.</summary>
        private void Pose(float time)
        {
            Rest();
            var right = Vector3.right; var up = Vector3.up; var fwd = Vector3.forward;
            float t = time + phase; // (each its own rhythm: two foi side by side never move as one)
            float breathe = Mathf.Sin(t * 2.2f);
            float w = walk, s = Mathf.Sin(stride), c = Mathf.Cos(stride);
            float still = 1f - w;
            float sway = Mathf.Sin(t * 1.3f), drift = Mathf.Sin(t * 0.83f + 1.1f), nod = Mathf.Sin(t * 1.9f + 0.4f);
            // arms down at the sides, a little out, the elbows soft and moving with the breath
            Turn(armL, fwd, downL * (58f + breathe * 2.5f + sway * 2f * still));
            Turn(armR, fwd, downR * (58f + breathe * 2.5f - sway * 2f * still));
            Turn(foreL, fwd, downL * (16f + 5f * drift * still)); Turn(foreR, fwd, downR * (16f - 5f * drift * still));
            Turn(armL, right, forward * 6f * drift * still); Turn(armR, right, -forward * 6f * drift * still);
            // standing ready: the knees a little bent, the weight going from one foot to the other, the body turning a touch
            Turn(hips, up, 5f * sway * still);
            Turn(spine, fwd, 2.5f * drift * still);
            Turn(legL, right, -forward * (5f + 3f * sway) * still); Turn(legR, right, -forward * (5f - 3f * sway) * still);
            Turn(kneeL, right, kneeBack * (10f + 5f * Mathf.Max(0f, sway)) * still); Turn(kneeR, right, kneeBack * (10f + 5f * Mathf.Max(0f, -sway)) * still);
            Turn(neck, right, forward * 3f * nod * still);
            // walking: legs well forward and back, the knee bending as it comes through, the arms against the legs, leaning in
            Turn(legL, right, -forward * 34f * s * w); Turn(legR, right, forward * 34f * s * w);
            Turn(kneeL, right, kneeBack * (8f + 48f * Mathf.Max(0f, c)) * w); Turn(kneeR, right, kneeBack * (8f + 48f * Mathf.Max(0f, -c)) * w);
            Turn(armL, right, forward * 30f * s * w); Turn(armR, right, -forward * 30f * s * w);
            Turn(foreL, right, forward * (12f + 14f * Mathf.Max(0f, s)) * w); Turn(foreR, right, forward * (12f + 14f * Mathf.Max(0f, -s)) * w);
            Turn(spine, up, 9f * s * w);
            Turn(spine, right, forward * 6f * w);
            Turn(chest, right, forward * 1.5f * breathe * still);
            // a chop: the right arm up and back, then down hard forward; the body winds up and throws itself into it
            if (chop < 1f)
            {
                float k = chop < 0.35f ? Mathf.SmoothStep(0f, 1f, chop / 0.35f) : 1f - Mathf.SmoothStep(0f, 1f, (chop - 0.35f) / 0.25f);
                float down = chop < 0.35f ? 0f : Mathf.Clamp01((chop - 0.35f) / 0.25f) * (1f - Mathf.Clamp01((chop - 0.75f) / 0.25f));
                Turn(armR, right, forward * (120f * k + 40f * down));
                Turn(foreR, right, forward * 35f * k);
                Turn(chest, up, -18f * k + 14f * down);
                Turn(hips, up, -8f * k + 10f * down);
                Turn(spine, right, forward * (-5f * k + 12f * down));
                Turn(armL, right, forward * (20f * k - 10f * down)); // (the other arm swings against it)
                Turn(kneeL, right, kneeBack * 12f * down); Turn(kneeR, right, kneeBack * 12f * down);
            }
            // getting up: the knees drawn up and bent under it, the body forward over them, the left hand down pushing on
            // the ground behind, the right (with its weapon) out for balance
            if (rise > 0.01f || push > 0.01f)
            {
                Turn(legL, right, -forward * 78f * rise); Turn(legR, right, -forward * 64f * rise);
                Turn(kneeL, right, kneeBack * 105f * rise); Turn(kneeR, right, kneeBack * 90f * rise);
                Turn(spine, right, forward * 22f * rise);
                Turn(armL, fwd, downL * 18f * push); Turn(armL, right, -forward * 38f * push); // (down and back: the palm on the ground)
                Turn(foreL, fwd, -downL * 6f * push);
                Turn(armR, right, forward * 30f * rise); Turn(foreR, fwd, downR * 25f * rise);
            }
            // stepping back under a blow: a quick step back with one foot, then the other, the knees lifting, a little crouch
            if (reel > 0.01f)
            {
                float st = Mathf.Sin(reelStep * Mathf.PI * 3f); // (a step and a half)
                Turn(legL, right, forward * 26f * st * reel); Turn(legR, right, -forward * 26f * st * reel);
                Turn(kneeL, right, kneeBack * (12f + 32f * Mathf.Max(0f, st)) * reel); Turn(kneeR, right, kneeBack * (12f + 32f * Mathf.Max(0f, -st)) * reel);
                Turn(armL, right, -forward * 14f * st * reel); Turn(armR, right, forward * 14f * st * reel); // (the arms against the legs, for balance)
            }
            // thrown about: the arms flung up and out, each in its own rhythm and lagging, the forearms whipping, the legs
            // kicking, the body bending and the head going with it; lying, spread out flat
            if (flail > 0.01f || flat > 0.01f)
            {
                float f = flail, ft = time * 13f;
                float a1 = Mathf.Sin(ft + 0.3f), a2 = Mathf.Sin(ft * 1.27f + 2.1f), l1 = Mathf.Sin(ft * 0.9f + 1.2f), l2 = Mathf.Sin(ft * 1.13f + 4f);
                Turn(armL, fwd, -downL * (55f * f + 25f * f * a1 + 50f * flat)); // (up from hanging: out to the side)
                Turn(armR, fwd, -downR * (55f * f + 25f * f * a2 + 50f * flat));
                Turn(armL, right, forward * (30f * a2 * f - 20f * flat)); Turn(armR, right, forward * (30f * a1 * f - 20f * flat));
                Turn(foreL, fwd, downL * (25f + 30f * Mathf.Abs(a1)) * f); Turn(foreR, fwd, downR * (25f + 30f * Mathf.Abs(a2)) * f);
                Turn(legL, right, -forward * (28f * l1 + 18f) * f); Turn(legR, right, -forward * (28f * l2 + 10f) * f);
                Turn(legL, fwd, 10f * flat); Turn(legR, fwd, -10f * flat); // (lying: the legs apart)
                Turn(kneeL, right, kneeBack * (20f + 30f * Mathf.Abs(l2)) * f); Turn(kneeR, right, kneeBack * (20f + 30f * Mathf.Abs(l1)) * f);
                Turn(spine, right, -forward * 14f * f * (0.6f + 0.4f * a1)); // (bent back by the blow)
                Turn(spine, up, 10f * l1 * f);
                Turn(neck, right, forward * 18f * f * a2);
            }
            // a clap: both arms up in front, the hands meeting again and again
            if (clap < 1f)
            {
                float k = Mathf.Clamp01(clap / 0.12f) * Mathf.Clamp01((1f - clap) / 0.12f);
                float beat = Mathf.Abs(Mathf.Sin(clap * 2.6f * 5f * Mathf.PI));
                Turn(armL, right, forward * 80f * k); Turn(armR, right, forward * 80f * k);
                Turn(armL, up, -(26f - 12f * beat) * k * downL); Turn(armR, up, -(26f - 12f * beat) * k * downR);
                Turn(foreL, right, forward * 30f * k); Turn(foreR, right, forward * 30f * k);
            }
        }

        private float time;

        private void LateUpdate()
        {
            if (!ready) Setup();
            if (vclock == null && transform.parent != null) { vclock = VisualTime.On(transform.parent); vroot = VisualRoot.On(transform.parent); }
            float dt = Mathf.Min(VDt, 0.1f);
            time += dt;
            Breathe();

            // What the body does: walking (how fast), chopping, clapping, or standing.
            var p = transform.position; var d = p - lastPos; d.y = 0f; lastPos = p;
            if (d.sqrMagnitude > 1e-6f) walkBack = Vector3.Dot(d, transform.forward) < -0.3f * d.magnitude; // (stepping back: the walk's clip runs backwards)
            var sc = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float scale = Mathf.Max(0.01f, Mathf.Pow(Mathf.Abs(sc.x * sc.y * sc.z), 1f / 3f)); // (the hop's squash keeps the volume: no jitter)
            if (dt > 1e-4f) speed = Mathf.Lerp(speed, d.magnitude / dt / scale, dt * 10f); // (frozen by a hit: the pace held as it was)
            punchLeft -= dt; angerLeft -= dt; clapLeft -= dt;
            walk = Mathf.MoveTowards(walk, speed > 0.15f ? 1f : 0f, dt * 5f);
            stride += dt * Mathf.Clamp(speed * 3.2f, 5f, 16f) * (walk > 0.01f ? 1f : 0f);
            chop = Mathf.Min(1f, chop + dt / 0.55f);
            clap = Mathf.Min(1f, clap + dt / 2.6f);
            if (playing != null)
            {
                playingTime += (playingReal || Time.timeScale <= 0.01f ? Time.unscaledDeltaTime : dt) * playingRate;
                if (playingLoop && playingTime >= playingTo) playingTime = playingFrom + Mathf.Repeat(playingTime - playingFrom, Mathf.Max(0.05f, playingTo - playingFrom)); // round again
                else if (playingHold && playingTime >= playingTo) playingTime = playingTo; // (held on its last frame)
                else if (playingTime >= playingTo || speed > 0.6f && !playingStay) { ClipEnded(playing, Mathf.Min(playingTime, playingTo)); playing = null; actionNow = null; } // done, or it walked off (eased back into the pose)
            }
            HealPose();
            if (playing != null && clipWeight >= 0.999f)
            {
                Rest();
                layers.SetTime(HeroAnimLayers.FullBody, playingTime);
                layers.Evaluate();
                BlendFromSnapshot(dt); // (a section cut in on another: from the pose it left)
            }
            else if (playing != null)
            {
                Pose(time); // (the coded pose under the clip as it eases in)
                LayClips(dt, walk > 0.5f);
            }
            else
            {
                Pose(time);
                if (upperPlaying != null && layers != null)
                {
                    // a move on the upper body over the coded pose (the legs keep walking or standing)
                    upperTime += dt * upperRate;
                    if (upperTime >= upperPlaying.length) StopUpper();
                    else { layers.SetTime(HeroAnimLayers.UpperBody, upperTime); layers.EvaluateUpperOverPose(); }
                }
                else LayClips(dt, walk > 0.5f); // (the fighting stance, a reaction easing out)
            }
            Shuddering(dt);
            PushedByBlow();
            BodyIntoBlow();
            Reacting(dt);
            ReactionPose(dt);
            MovePose(dt);
            StrikePose(dt);
            KeepAboveGround(dt); // (never into the floor: before the grip, so the weapon follows the lifted hand)
            if (DuelOther != null && HeldWeapon.DuelStance) { finishPending = true; if (late == null) late = gameObject.AddComponent<SkinnedHeroLate>(); } // (the duel: kept out of the other first, then the grip)
            else Grip();

            // Lids: a blink now and then; half down when angry or striking (the eyes narrow).
            blinkTimer -= dt;
            if (blinkTimer < -0.16f) blinkTimer = Random.Range(2f, 4.5f);
            float shut = blinkTimer < 0f ? Mathf.Sin(-blinkTimer / 0.16f * Mathf.PI) : 0f;
            float narrow = angerLeft > 0f ? 0.45f : punchLeft > 0f ? 0.3f : 0f;
            shut = Mathf.Max(shut, Mathf.Max(narrow, Mathf.Max(ExpressionLids(), FaceLid)));
            if (lids.Count == 0 && head != null)
                foreach (Transform t in head) if (t.name == "Lid") lids.Add(t); // (a copy finds its own)
            for (int li = 0; li < lids.Count; li++)
            {
                var lid = lids[li];
                if (lid == null) continue;
                float s = FaceWink && li == 0 ? 1f : shut; // (a wink: the one lid shut)
                lid.localScale = new Vector3(1f, Mathf.Max(0.001f, s), 1f);
                if (lid.childCount > 0) lid.GetChild(0).gameObject.SetActive(s > 0.03f);
            }
        }

        private Transform armR, foreR, handR, fingerR;
        private Vector3 handRest, gripRest;
        private Quaternion gripTurn = Quaternion.identity;

        /// <summary>A move (a dance, the ultimate's combo) is playing: the arms are its own.</summary>
        public bool PlayingMove => playing != null;
        private bool gripKnown;
        /// <summary>What the game hangs by the body to be held (a weapon, a hammer).</summary>
        private static readonly string[] HeldNames = { "HeldWeapon", "ArenaHammer", "HeldHammer" };

        /// <summary>
        /// A weapon the game hangs beside the body is moved into the right hand's reach (in a grip of its own, so its own
        /// swings and spins still play), and the arm reaches for it every frame: the hand holds it and follows its blows.
        /// </summary>
        private void Grip()
        {
            var visual = transform.parent;
            if (visual == null || armR == null || foreR == null || handR == null) return;
            Transform weapon = null;
            for (int i = 0; i < visual.childCount; i++)
            {
                var c = visual.GetChild(i);
                if (System.Array.IndexOf(HeldNames, c.name) >= 0)
                {
                    var grip = new GameObject("Grip_" + c.name).transform;
                    grip.SetParent(visual, false);
                    grip.localPosition = handRest - c.localPosition;
                    gripRest = grip.localPosition; gripKnown = true;
                    c.SetParent(grip, false);
                }
                else if (c.name.StartsWith("Grip_"))
                {
                    if (c.childCount == 0) { Destroy(c.gameObject); continue; } // its weapon was put away
                    if (!gripKnown) { gripRest = c.localPosition; gripKnown = true; } // (a copy brings its grip along)
                    if (c.GetChild(0).gameObject.activeInHierarchy) weapon = c.GetChild(0);
                }
            }
            CloseHands(); // (both fists closed: on the weapon's grip, on the shield's strap)
            if (weapon == null) return;
            weapon.parent.localPosition = gripRest; // each frame from its own place (the snap below must not add up)
            if (playing != null || Flailing)
            {
                // a move plays (or it is thrown about): the arm is its own; the weapon follows the hand (turned with it from how it rests)
                weapon.parent.rotation = handR.rotation * gripTurn;
            }
            else
            {
                weapon.parent.localRotation = Quaternion.identity;
                Reach(armR, foreR, handR, weapon.position);
                HandAlong(weapon.up); // (the grip through the fist, not across the arm)
            }
            // then the weapon into the fist itself, so the fingers close round its grip
            var palm = FistMiddle;
            weapon.parent.position += palm - weapon.position;
            if (playing == null && !Flailing) gripTurn = Quaternion.Inverse(handR.rotation) * weapon.parent.rotation; // (how it sits in the hand at rest: a move keeps it so)
            var held = weapon.GetComponentInChildren<HeldWeapon>();
            if (held != null) { held.BodyDriven = AttackPlaying; held.AfterGrip(); } // (a mapped attack swings it with the hand; the blade onto its target from where the hand holds it)
        }

        /// <summary>Two-joint reach: the elbow bends (on the side it already bends to) so the hand comes to the target.</summary>
        private static void Reach(Transform upper, Transform lower, Transform end, Vector3 target)
        {
            var root = upper.position;
            float a = Vector3.Distance(root, lower.position), b = Vector3.Distance(lower.position, end.position);
            var to = target - root;
            if (to.sqrMagnitude < 1e-8f || a < 1e-5f || b < 1e-5f) return;
            var dir = to.normalized;
            float c = Mathf.Clamp(to.magnitude, Mathf.Abs(a - b) + 1e-4f, a + b - 1e-4f);
            float x = (a * a - b * b + c * c) / (2f * c), h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            var bend = lower.position - root; bend -= dir * Vector3.Dot(bend, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.Cross(dir, Vector3.up);
            var elbow = root + dir * x + bend.normalized * h;
            upper.rotation = Quaternion.FromToRotation(lower.position - root, elbow - root) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, root + dir * c - lower.position) * lower.rotation;
        }

        private void OnDestroy()
        {
            if (layers != null) layers.Destroy();
        }
    }
}
