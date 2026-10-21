using System.Collections.Generic;
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
    public class SkinnedHero : MonoBehaviour
    {
        private const string Folder = "Heroes/Foi/";
        /// <summary>The head joint's height in the build (about 1.05 tall in all, the head included).</summary>
        private const float HeadJointHeight = 0.67f;
        /// <summary>The head's height against the body's (headless) height: a big, cartoon head.</summary>
        private const float HeadShare = 0.7f;

        /// <summary>True while foi is off duty (menus, garage): it stands with its arms folded; in play, at ease.</summary>
        public static System.Func<bool> OffDuty = () => true;

        /// <summary>What foi looks like: its head (1..), hair and skin colour (null = the head's own), the armour's paint (null = plain).</summary>
        public struct Look
        {
            public int head;
            public Color? hair, skin;
            public string suit;
        }

        public const int Heads = 4;

        [System.Serializable]
        private class EyeInfo { public float x, y, z, w, h; }
        [System.Serializable]
        private class HeadInfo { public float height, width, depth, top, front, neckWidth, hatTop, hatWidth; public float[] skin, hair; public EyeInfo[] eyes; }
        [System.Serializable]
        private class BodyInfo { public float height, ringTop, ringX, ringY, ringWidth, shoulders; public float[] head, neck; }

        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static readonly Dictionary<int, HeadInfo> headInfos = new Dictionary<int, HeadInfo>();
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

        private static HeadInfo InfoOf(int head)
        {
            if (headInfos.TryGetValue(head, out var i)) return i;
            var text = Resources.Load<TextAsset>(Folder + "head_" + head);
            i = text != null ? JsonUtility.FromJson<HeadInfo>(text.text) : new HeadInfo { height = 0.98f, width = 0.8f, top = 0.98f, neckWidth = 0.3f };
            headInfos[head] = i;
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
            foreach (var sm in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true)) sm.updateWhenOffscreen = true;
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
            var prefab = Resources.Load<GameObject>(Folder + "head_" + n);
            head = new GameObject("Hero_Head").transform;
            head.SetParent(visual, false);
            if (prefab != null)
            {
                // the model under it as imported (its own turn and size from the file), sized so the head's units are the
                // measures' (its neck at the origin, the face toward +z)
                var model = Instantiate(prefab, head, false).transform;
                var rs = model.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                    float hb = bb.size.y / Mathf.Max(1e-6f, head.lossyScale.y);
                    if (hb > 1e-5f) model.localScale *= info.height / hb;
                }
            }
            // as big as the others by the face, not the hair: a spike (head 2) or a bun (head 4) on top made them look small
            float k = b.height * HeadShare * unit / Mathf.Max(0.01f, info.top) * (n == 2 ? 1.25f : n == 4 ? 1.15f : 1f);
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
            float crown = info.hatTop > 0f ? info.hatTop : info.top * 0.94f; // where a hat sits: the skull's top (not a spike of hair)
            hat.localPosition = new Vector3(0f, crown - FoiHeadTop * hatScale, 0f);
            HatMount = hat;
            head.SetParent(bone, true);
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
        private static readonly Dictionary<int, (Color32[] px, Color32[] mk, int w, int mw)> pixels = new Dictionary<int, (Color32[], Color32[], int, int)>();

        /// <summary>Livelier skin (as on the icon): more colour, a touch warmer, so the blush on the nose and cheeks shows.</summary>
        private static Color32 Vivid(Color32 p, float amount)
        {
            if (amount < 0.01f) return p;
            float r = p.r, g = p.g, b = p.b, lum = r * 0.3f + g * 0.59f + b * 0.11f;
            float sat = 1f + 0.3f * amount;
            r = lum + (r - lum) * sat; g = lum + (g - lum) * sat; b = lum + (b - lum) * sat;
            r *= 1f + 0.04f * amount; b *= 1f - 0.03f * amount;
            return new Color32((byte)Mathf.Clamp(r, 0f, 255f), (byte)Mathf.Clamp(g, 0f, 255f), (byte)Mathf.Clamp(b, 0f, 255f), 255);
        }

        /// <summary>The head's own light: a part of its colour that no light or shadow takes away, so the face never
        /// darkens as the lights round it change (on phones each thing gets only a few of the scene's lights).</summary>
        private const float HeadGlow = 0.24f;

        private static Material HeadPaint(int n, Color? hair, Color? skin)
        {
            string key = "head" + n + hair + skin;
            if (materials.TryGetValue(key, out var m) && m != null) { recent.Remove(key); recent.Add(key); return m; }
            var baseTex = Resources.Load<Texture2D>(Folder + "head_" + n);
            Texture2D tex = baseTex;
            var mask = Resources.Load<Texture2D>(Folder + "head_" + n + "_mask");
            var info = InfoOf(n);
            if (baseTex != null && mask != null && baseTex.isReadable && mask.isReadable) // (always: the skin is warmed up too)
            {
                if (!pixels.TryGetValue(n, out var src)) pixels[n] = src = (baseTex.GetPixels32(), mask.GetPixels32(), baseTex.width, mask.width);
                int W = src.w / 2, mw = src.mw;
                var px = new Color32[W * W];
                var mk = new Color32[W * W];
                for (int y = 0; y < W; y++)
                    for (int x = 0; x < W; x++)
                    {
                        px[y * W + x] = src.px[(y * 2) * src.w + x * 2]; // (half size: plenty for a head on a phone)
                        mk[y * W + x] = src.mk[(y * mw / W) * mw + x * mw / W];
                    }
                {
                    var hRef = info.hair != null ? new Color(info.hair[0], info.hair[1], info.hair[2]) : new Color(0.35f, 0.18f, 0.11f);
                    var sRef = info.skin != null ? new Color(info.skin[0], info.skin[1], info.skin[2]) : new Color(0.66f, 0.55f, 0.49f);
                    Vector3 Ratio(Color target, Color reference) => new Vector3(target.r / Mathf.Max(0.04f, reference.r), target.g / Mathf.Max(0.04f, reference.g), target.b / Mathf.Max(0.04f, reference.b));
                    var hr = hair.HasValue ? Ratio(hair.Value, hRef) : Vector3.one;
                    var sr = skin.HasValue ? Ratio(skin.Value, sRef) : Vector3.one;
                    for (int i = 0; i < px.Length; i++)
                    {
                        float wh = mk[i].r / 255f, ws = mk[i].g / 255f;
                        if (wh < 0.01f && ws < 0.01f) continue;
                        var mul = Vector3.one + (hr - Vector3.one) * wh + (sr - Vector3.one) * ws;
                        var p = px[i];
                        px[i] = Vivid(new Color32((byte)Mathf.Min(255f, p.r * mul.x), (byte)Mathf.Min(255f, p.g * mul.y), (byte)Mathf.Min(255f, p.b * mul.z), 255), ws);
                    }
                    tex = new Texture2D(W, W, TextureFormat.RGBA32, true) { name = key };
                    tex.SetPixels32(px);
                    tex.Apply(true, true);
                }
            }
            m = MaterialFactory.CreateSurface(Color.white, Color.black, 0.18f, 0f); // matte skin
            if (tex != null)
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
                    if (materials.TryGetValue(old, out var om) && om != null)
                    {
                        var ot = om.GetTexture("_BaseMap");
                        Destroy(om);
                        if (ot != null) Destroy(ot);
                    }
                    materials.Remove(old);
                }
            }
            return m;
        }

        /// <summary>Head <paramref name="n"/> as made (for its picture in the workshop).</summary>
        public static Material HeadMaterial(int n) => HeadPaint(Mathf.Clamp(n, 1, Heads), null, null);

        /// <summary>The eyelids' paint: the skin's colour, livened and lit as the face is.</summary>
        private static Material LidPaint(Color c)
        {
            var v = Vivid(c, 1f); var cv = new Color(v.r / 255f, v.g / 255f, v.b / 255f);
            string key = "lid" + cv;
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = MaterialFactory.CreateSurface(cv, Color.black, 0.18f, 0f);
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

        /// <summary>Dresses a build already made: its head, hair and skin colours and the armour's paint.</summary>
        public static void Dress(Transform skin, Look look)
        {
            var me = skin.GetComponent<SkinnedHero>();
            int n = Mathf.Clamp(look.head <= 0 ? 1 : look.head, 1, Heads);
            if (me != null)
            {
                me.lidMaterial = LidPaint(SkinOf(n, look.skin));
                me.PutHead(n);
                foreach (var r in me.head.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = r.name == "Cover" ? me.lidMaterial : HeadPaint(n, look.hair, look.skin);
                    r.receiveShadows = false; // (no flickering self-shadow on the face from the hair, hat or crown)
                }
            }
            var armour = Armour(look.suit);
            foreach (var r in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.sharedMaterial = armour;
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
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationClipPlayable clipPlay;
        private float playingTime, playingRate = 1f;
        private bool playingReal;

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
        public bool PlayMove(string name, float rate = 1f, bool realTime = false)
        {
            if (!ready) Setup();
            var c = ClipOf(name);
            if (c == null) return false;
            playing = c; playingTime = 0f; playingRate = rate; playingReal = realTime;
            // played through the body's animator (a graph of its own, evaluated by hand)
            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            if (!graph.IsValid())
            {
                graph = PlayableGraph.Create(name + " moves");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                output = AnimationPlayableOutput.Create(graph, "out", animator);
            }
            if (clipPlay.IsValid()) clipPlay.Destroy();
            clipPlay = AnimationClipPlayable.Create(graph, c);
            output.SetSourcePlayable(clipPlay);
            clap = 1f; chop = 1f;
            return true;
        }

        /// <summary>The length of move <paramref name="name"/> in seconds (0: none).</summary>
        public static float MoveLength(string name) { var c = ClipOf(name); return c != null ? c.length : 0f; }

        /// <summary>A win: the dance chosen in the workshop (or clapping).</summary>
        public void Celebrate(string dance)
        {
            string move = dance != null && dance.StartsWith("dance.d") ? "dance" + dance.Substring(7) : null;
            if (move == null || !PlayMove(move)) Clap();
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

        private void Turn(Transform b, Vector3 axis, float degrees)
        {
            if (b == null || !restRel.ContainsKey(b)) return;
            var pr = b.parent != null && restRel.ContainsKey(b.parent) ? restRel[b.parent] : Quaternion.Inverse(Frame) * (b.parent != null ? b.parent.rotation : Quaternion.identity);
            b.localRotation = Quaternion.Inverse(pr) * Quaternion.AngleAxis(degrees, axis) * pr * b.localRotation;
        }

        private void Rest()
        {
            foreach (var kv in restLocal) kv.Key.localRotation = kv.Value;
        }

        private void Setup()
        {
            ready = true;
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

        /// <summary>This frame's pose: standing (breathing), walking (legs and arms swinging), a chop, a clap.</summary>
        private void Pose(float time)
        {
            Rest();
            var right = Vector3.right; var up = Vector3.up; var fwd = Vector3.forward;
            float breathe = Mathf.Sin(time * 2.2f);
            float w = walk, s = Mathf.Sin(stride), c = Mathf.Cos(stride);
            // arms down at the sides, a little out, the elbows soft
            Turn(armL, fwd, downL * (60f + breathe * 1.5f)); // (not all the way down: the armour is broad)
            Turn(armR, fwd, downR * (60f + breathe * 1.5f));
            Turn(foreL, fwd, downL * 8f); Turn(foreR, fwd, downR * 8f);
            // walking: legs forward and back, the knee bending as it comes through, the arms against the legs
            Turn(legL, right, -forward * 26f * s * w); Turn(legR, right, forward * 26f * s * w);
            Turn(kneeL, right, kneeBack * (6f + 34f * Mathf.Max(0f, c)) * w); Turn(kneeR, right, kneeBack * (6f + 34f * Mathf.Max(0f, -c)) * w);
            Turn(armL, right, forward * 22f * s * w); Turn(armR, right, -forward * 22f * s * w);
            Turn(spine, up, 5f * s * w);
            Turn(chest, right, forward * 1.5f * breathe * (1f - w));
            // a chop: the right arm up and back, then down hard forward
            if (chop < 1f)
            {
                float k = chop < 0.35f ? Mathf.SmoothStep(0f, 1f, chop / 0.35f) : 1f - Mathf.SmoothStep(0f, 1f, (chop - 0.35f) / 0.25f);
                float down = chop < 0.35f ? 0f : Mathf.Clamp01((chop - 0.35f) / 0.25f) * (1f - Mathf.Clamp01((chop - 0.75f) / 0.25f));
                Turn(armR, right, forward * (120f * k + 40f * down));
                Turn(foreR, right, forward * 35f * k);
                Turn(chest, up, -14f * k + 10f * down);
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
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            time += dt;
            Breathe();

            // What the body does: walking (how fast), chopping, clapping, or standing.
            var p = transform.position; var d = p - lastPos; d.y = 0f; lastPos = p;
            var sc = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float scale = Mathf.Max(0.01f, Mathf.Pow(Mathf.Abs(sc.x * sc.y * sc.z), 1f / 3f)); // (the hop's squash keeps the volume: no jitter)
            speed = Mathf.Lerp(speed, dt > 0f ? d.magnitude / dt / scale : 0f, dt * 10f);
            punchLeft -= dt; angerLeft -= dt; clapLeft -= dt;
            walk = Mathf.MoveTowards(walk, speed > 0.15f ? 1f : 0f, dt * 5f);
            stride += dt * Mathf.Clamp(speed * 3.2f, 5f, 16f) * (walk > 0.01f ? 1f : 0f);
            chop = Mathf.Min(1f, chop + dt / 0.55f);
            clap = Mathf.Min(1f, clap + dt / 2.6f);
            if (playing != null)
            {
                playingTime += (playingReal || Time.timeScale <= 0.01f ? Time.unscaledDeltaTime : dt) * playingRate;
                if (playingTime >= playing.length || speed > 0.6f) playing = null; // done, or it walked off
            }
            if (playing != null)
            {
                Rest();
                clipPlay.SetTime(playingTime);
                graph.Evaluate(0f);
            }
            else Pose(time);
            Grip();

            // Lids: a blink now and then; half down when angry or striking (the eyes narrow).
            blinkTimer -= dt;
            if (blinkTimer < -0.16f) blinkTimer = Random.Range(2f, 4.5f);
            float shut = blinkTimer < 0f ? Mathf.Sin(-blinkTimer / 0.16f * Mathf.PI) : 0f;
            float narrow = angerLeft > 0f ? 0.45f : punchLeft > 0f ? 0.3f : 0f;
            shut = Mathf.Max(shut, narrow);
            if (lids.Count == 0 && head != null)
                foreach (Transform t in head) if (t.name == "Lid") lids.Add(t); // (a copy finds its own)
            foreach (var lid in lids)
            {
                if (lid == null) continue;
                lid.localScale = new Vector3(1f, Mathf.Max(0.001f, shut), 1f);
                if (lid.childCount > 0) lid.GetChild(0).gameObject.SetActive(shut > 0.03f);
            }
        }

        private Transform armR, foreR, handR, fingerR;
        private Vector3 handRest, gripRest;
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
            if (weapon == null) return;
            weapon.parent.localPosition = gripRest; // each frame from its own place (the snap below must not add up)
            Reach(armR, foreR, handR, weapon.position);
            // then the weapon into the palm itself (between the wrist and the middle finger's root), so the fist closes on it
            var palm = fingerR != null ? Vector3.Lerp(handR.position, fingerR.position, 0.65f) : handR.position;
            weapon.parent.position += palm - weapon.position;
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
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
