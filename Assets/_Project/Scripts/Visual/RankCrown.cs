using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The rank crown on foi's head (Docs/RUTBE_KUSAMI_TAC_SAC_BIRLESIK.md, sections 2 and 6). It sits on the bald head
    /// just over the forehead, its ring 3% wider than the head there; with hair, method A first (the crown grows a little
    /// and rises to sit on the hair), else method B (it stays low and the hair inside and under its ring is pressed down,
    /// once, into a copy of the hair's mesh kept for that hair and crown). It never falls off; a blow makes it hop.
    /// The Marshal's has a cyan core that beats and a few motes about it.
    /// </summary>
    public class RankCrown : MonoBehaviour
    {
        private const string Folder = "Rank/Crowns/";

        /// <summary>A crown's measures in its own model (outer width 1, its bottom at 0).</summary>
        [System.Serializable]
        private class CrownInfo { public int tier; public float height, outer, inner; }

        [System.Serializable]
        private class ProfileRow { public string name; public float[] r; }

        [System.Serializable]
        private class Profiles { public float z0, dz; public ProfileRow[] rows; }

        /// <summary>How a crown sits on a head and its hair (head units: the neck at 0, the bald top ~0.98).</summary>
        public struct Fit
        {
            public float seat, scale, inner, height;
            public bool pressed;
            public override string ToString() => (pressed ? "B" : "A") + " seat " + seat.ToString("0.000") + " scale " + scale.ToString("0.000");
        }

        [SerializeField] private int tier;
        public int Tier { get => tier; private set => tier = value; }
        public Fit Seat { get; private set; }

        private static Profiles profiles;
        private static readonly Dictionary<int, CrownInfo> infos = new Dictionary<int, CrownInfo>();
        private static readonly Dictionary<int, Material> paints = new Dictionary<int, Material>();
        private static readonly Dictionary<string, Mesh> pressed = new Dictionary<string, Mesh>();

        private readonly List<MeshFilter> hairs = new List<MeshFilter>();
        private readonly List<Mesh> hairsAsMade = new List<Mesh>();
        [SerializeField] private Transform model, core; // (serialized: a lookalike copy keeps them)
        private Material flashPaint;
        [SerializeField] private Material corePaint;
        [SerializeField] private Vector3 seatPos;
        private float hopLeft, flashLeft, ringLeft;
        [SerializeField] private Transform ring;
        [SerializeField] private List<Transform> motes = new List<Transform>();

        /// <summary>Crown tier <paramref name="tier"/> (1..6; 0 takes it off) on <paramref name="head"/> (Hero_Head) of head <paramref name="headN"/>.</summary>
        public static RankCrown Put(Transform head, int headN, int tier)
        {
            if (head == null) return null;
            var old = head.Find("Mount_Crown");
            if (old != null)
            {
                var oc = old.GetComponent<RankCrown>();
                if (oc != null && oc.Tier == tier) return oc;
                if (oc != null) oc.RestoreHair();
                DestroyImmediate(old.gameObject);
            }
            if (tier <= 0) return null;
            var data = RankGearData.Current;
            var row = data.Of(tier);
            var info = InfoOf(tier);
            var prefab = Resources.Load<GameObject>(Folder + row.crown);
            if (info == null || prefab == null) return null;

            var mount = new GameObject("Mount_Crown").transform;
            var crown = mount.gameObject.AddComponent<RankCrown>();
            crown.Tier = tier;
            var squash = Model(mount, tier);
            crown.model = squash;

            var fit = FitOf(headN, tier);
            crown.Seat = fit;
            mount.SetParent(head, false);
            mount.localScale = Vector3.one * fit.scale * row.crownScale;
            mount.localRotation = Quaternion.Euler(data.tilt, 0f, 0f);
            crown.seatPos = new Vector3(0f, fit.seat + row.crownLift, 0f);
            mount.localPosition = crown.seatPos;
            if (fit.pressed) crown.PressHair(head, headN, fit);
            if (tier >= 6) crown.MakeMarshal(info);
            return crown;
        }

        /// <summary>
        /// Crown <paramref name="tier"/>'s model alone under <paramref name="parent"/>: its bottom centre at the parent's
        /// origin, outer width 1, its front toward +z (its height squashed by the tier's row). Null when it is missing.
        /// </summary>
        public static Transform Model(Transform parent, int tier)
        {
            var row = RankGearData.Current.Of(tier);
            var prefab = Resources.Load<GameObject>(Folder + row.crown);
            if (prefab == null) return null;
            // made upright at the world's origin first, then put under the parent
            var squash = new GameObject("Crown").transform;
            var m = Instantiate(prefab, squash, false).transform;
            m.name = "Model";
            var paint = PaintOf(tier, row.crown);
            var rs = m.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rs) { r.sharedMaterial = paint; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            if (rs.Length > 0)
            {
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                float w = Mathf.Max(bb.size.x, bb.size.z);
                if (w > 1e-5f) m.localScale *= 1f / w;
                bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                m.localPosition -= new Vector3(bb.center.x, bb.min.y, bb.center.z);
            }
            squash.localScale = new Vector3(1f, row.crownHeight, 1f);
            squash.SetParent(parent, false);
            return squash;
        }

        /// <summary>Where crown <paramref name="tier"/> sits on head <paramref name="headN"/> with its hair.</summary>
        public static Fit FitOf(int headN, int tier)
        {
            var data = RankGearData.Current;
            var info = InfoOf(tier) ?? new CrownInfo { height = 0.5f, outer = 0.5f, inner = 0.36f };
            string g = headN <= 3 ? "m" : "f";
            var bald = Profile("head_" + g);
            var hair = Profile("hair_" + g + (headN <= 3 ? headN : headN - 3)) ?? bald;
            float headR = Reach(bald, data.socket);
            float innerBald = headR * data.ringPad;
            float scale = innerBald / Mathf.Max(0.05f, info.inner);
            float baldSeat = RestOn(bald, innerBald);
            var fit = new Fit { scale = scale, seat = baldSeat, inner = innerBald, height = info.height * data.Of(tier).crownHeight * scale };
            if (hair == bald) return fit;
            // method A: grow a little and rise onto the hair, within the limits
            for (float grow = 1f; grow <= data.maxGrow + 1e-4f; grow += 0.01f)
            {
                float seat = RestOn(hair, innerBald * grow);
                if (seat - baldSeat <= data.maxLift)
                {
                    fit.scale = scale * grow; fit.inner = innerBald * grow; fit.seat = seat; fit.height = info.height * data.Of(tier).crownHeight * fit.scale;
                    return fit;
                }
            }
            // method B: low on the head, the hair pressed under it
            fit.scale = scale * data.pressGrow; fit.inner = innerBald * data.pressGrow; fit.seat = baldSeat + data.pressLift;
            fit.height = info.height * data.Of(tier).crownHeight * fit.scale;
            fit.pressed = true;
            return fit;
        }

        /// <summary>A light hop on a blow (it never flies off or falls).</summary>
        public void Hop() => hopLeft = 0.22f;

        private float dropLeft, dropTime;

        /// <summary>The ceremony: it comes down onto the head from above over <paramref name="seconds"/>.</summary>
        public void Descend(float seconds) { dropTime = Mathf.Max(0.05f, seconds); dropLeft = dropTime; }

        /// <summary>The duel's countdown: a short glint (the Marshal's: a ring of light spreads from it).</summary>
        public void Shine()
        {
            flashLeft = 0.45f;
            if (Tier < 6) return;
            if (ring == null)
            {
                ring = Shapes.Primitive(PrimitiveType.Cylinder, "Ring", transform, Vector3.zero, Vector3.one, RingPaint()).transform;
                Destroy(ring.GetComponent<Collider>());
            }
            ringLeft = 0.7f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            var data = RankGearData.Current;
            float lift = 0f;
            if (hopLeft > 0f) { hopLeft -= dt; float t = 1f - Mathf.Clamp01(hopLeft / 0.22f); lift = Mathf.Sin(t * Mathf.PI) * data.hop; }
            if (dropLeft > 0f) { dropLeft -= Time.unscaledDeltaTime; lift += Mathf.SmoothStep(0f, 1.2f, Mathf.Clamp01(dropLeft / dropTime)); }
            transform.localPosition = seatPos + Vector3.up * lift;
            if (flashLeft > 0f)
            {
                flashLeft -= dt;
                float k = Mathf.Sin(Mathf.Clamp01(1f - flashLeft / 0.45f) * Mathf.PI);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = k > 0.05f ? FlashPaint(r.sharedMaterial, k) : PaintOf(Tier, data.Of(Tier).crown);
            }
            if (ring != null)
            {
                ring.gameObject.SetActive(ringLeft > 0f);
                if (ringLeft > 0f)
                {
                    ringLeft -= dt;
                    float t = 1f - Mathf.Clamp01(ringLeft / 0.7f);
                    float w = Mathf.Lerp(0.6f, 2.6f, t);
                    ring.localScale = new Vector3(w, 0.004f, w);
                    ring.localPosition = Vector3.up * 0.15f;
                    var c = data.marshalGlow; c.a = 0.55f * (1f - t);
                    MaterialFactory.SetColors(ring.GetComponent<Renderer>().sharedMaterial, c, data.marshalGlow * (1f - t));
                }
            }
            if (core != null)
            {
                var cr = core.GetComponent<Renderer>();
                if (cr != null && cr.sharedMaterial != null && cr.sharedMaterial.name.StartsWith(CorePaintName)) corePaint = cr.sharedMaterial; // (a copy's own)
                else if (cr != null) { cr.sharedMaterial = corePaint; foreach (var m in motes) { var mr = m.GetComponent<Renderer>(); if (mr != null) mr.sharedMaterial = corePaint; } } // (never another's paint: the glow coloured it)
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(0.2f, data.marshalPulse));
                MaterialFactory.SetColors(corePaint, data.marshalGlow, data.marshalGlow * Mathf.Lerp(0.8f, 2.6f, beat));
                for (int i = 0; i < motes.Count; i++)
                {
                    float a = Time.time * 0.6f + i * Mathf.PI * 2f / motes.Count;
                    motes[i].localPosition = new Vector3(Mathf.Cos(a) * 0.62f, 0.2f + 0.12f * Mathf.Sin(Time.time * 1.3f + i), Mathf.Sin(a) * 0.62f);
                }
            }
        }

        private void OnDestroy() => RestoreHair();

        private const string CorePaintName = "CrownCore";

        // the Marshal's: the cyan core on its front gem, and the motes (none on low quality)
        private void MakeMarshal(CrownInfo info)
        {
            var data = RankGearData.Current;
            corePaint = MaterialFactory.Create(data.marshalGlow, data.marshalGlow * 2f);
            corePaint.name = CorePaintName;
            float h = info.height * data.Of(Tier).crownHeight;
            core = Shapes.Primitive(PrimitiveType.Sphere, "Core", transform, new Vector3(0f, h * 0.36f, info.outer * 0.93f), Vector3.one * 0.075f, corePaint).transform;
            Destroy(core.GetComponent<Collider>());
            int n = GraphicsQuality.Current == GraphicsTier.Low ? 0 : data.marshalMotes;
            for (int i = 0; i < n; i++)
            {
                var mote = Shapes.Primitive(PrimitiveType.Sphere, "Mote", transform, Vector3.zero, Vector3.one * 0.035f, corePaint).transform;
                Destroy(mote.GetComponent<Collider>());
                motes.Add(mote);
            }
        }

        // method B: the hair inside the ring pressed down under its rim, the hair under the ring's wall drawn in to it;
        // made once per hair and crown, and the hair's own mesh put back when the crown goes
        /// <summary>The hair kept over the scalp when a crown presses it (head units).</summary>
        private const float KeepHair = 0.035f;

        private void PressHair(Transform head, int headN, Fit fit)
        {
            var data = RankGearData.Current;
            var bald = Profile("head_" + (headN <= 3 ? "m" : "f")); // (the scalp: the hair is never pressed into it)
            foreach (var mf in head.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.ToLowerInvariant().Contains("hair") || mf.sharedMesh == null) continue;
                var asMade = mf.sharedMesh;
                string key = asMade.name + "|" + headN + "|" + Tier + "|k";
                if (!pressed.TryGetValue(key, out var mesh) || mesh == null)
                {
                    if (!asMade.isReadable) continue;
                    mesh = Instantiate(asMade);
                    mesh.name = asMade.name + "_crown" + Tier;
                    var vs = mesh.vertices;
                    float rim = fit.seat + fit.height * data.pressInside;
                    float top = float.MinValue;
                    for (int i = 0; i < vs.Length; i++) top = Mathf.Max(top, head.InverseTransformPoint(mf.transform.TransformPoint(vs[i])).y);
                    float squeeze = top > rim ? (rim - fit.seat) / Mathf.Max(0.01f, top - fit.seat) : 1f;
                    for (int i = 0; i < vs.Length; i++)
                    {
                        var p = head.InverseTransformPoint(mf.transform.TransformPoint(vs[i]));
                        float d = new Vector2(p.x, p.z).magnitude;
                        // drawn in under the ring's wall: a soft fold from a little under the ring up
                        float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fit.seat - 0.05f, fit.seat + 0.02f, p.y));
                        float inner = fit.inner * 0.94f;
                        if (d > inner && w > 0f)
                        {
                            float nd = Mathf.Lerp(d, inner, w);
                            p.x *= nd / d; p.z *= nd / d;
                        }
                        // and pressed down inside it, never through the scalp (a thin layer kept on it: no bald top under a crown)
                        if (p.y > fit.seat)
                        {
                            float scalp = bald != null ? RestOn(bald, d) : fit.seat;
                            p.y = Mathf.Max(fit.seat + (p.y - fit.seat) * squeeze, Mathf.Min(p.y, scalp + KeepHair));
                        }
                        vs[i] = mf.transform.InverseTransformPoint(head.TransformPoint(p));
                    }
                    mesh.vertices = vs;
                    mesh.RecalculateBounds();
                    pressed[key] = mesh;
                }
                hairs.Add(mf); hairsAsMade.Add(asMade);
                mf.sharedMesh = mesh;
            }
        }

        private void RestoreHair()
        {
            for (int i = 0; i < hairs.Count; i++) if (hairs[i] != null) hairs[i].sharedMesh = hairsAsMade[i];
            hairs.Clear(); hairsAsMade.Clear();
        }

        private static CrownInfo InfoOf(int tier)
        {
            if (infos.TryGetValue(tier, out var i)) return i;
            var ta = Resources.Load<TextAsset>(Folder + RankGearData.Current.Of(tier).crown);
            i = ta != null ? JsonUtility.FromJson<CrownInfo>(ta.text) : null;
            infos[tier] = i;
            return i;
        }

        private static float[] Profile(string name)
        {
            if (profiles == null)
            {
                var ta = Resources.Load<TextAsset>(Folder + "CrownFitProfiles");
                profiles = ta != null ? JsonUtility.FromJson<Profiles>(ta.text) : new Profiles { rows = new ProfileRow[0] };
            }
            foreach (var r in profiles.rows) if (r.name == name) return r.r;
            return null;
        }

        // the widest reach of head (and hair) at or above height z
        private static float Reach(float[] r, float z)
        {
            if (r == null || r.Length == 0) return 0.42f;
            float f = (z - profiles.z0) / profiles.dz;
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, r.Length - 1), j = Mathf.Min(i + 1, r.Length - 1);
            return Mathf.Lerp(r[i], r[j], Mathf.Clamp01(f - i));
        }

        // where a ring of inner radius ring comes to rest coming down over the head (and hair): the lowest height above
        // which everything fits inside it
        private static float RestOn(float[] r, float ring)
        {
            if (r == null || r.Length == 0) return RankGearData.Current.socket;
            for (int i = 0; i < r.Length; i++)
                if (r[i] <= ring)
                {
                    if (i == 0) return profiles.z0;
                    float t = Mathf.InverseLerp(r[i - 1], r[i], ring);
                    return profiles.z0 + (i - 1 + t) * profiles.dz;
                }
            return profiles.z0 + r.Length * profiles.dz;
        }

        private static Material PaintOf(int tier, string crown)
        {
            if (paints.TryGetValue(tier, out var m) && m != null) return m;
            var data = RankGearData.Current.Of(tier);
            m = MaterialFactory.CreateSurface(Color.white, Color.black, 0.55f, Mathf.Min(0.5f, data.metallic * 0.6f));
            var tex = Resources.Load<Texture2D>(Folder + crown);
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex); }
            paints[tier] = m;
            return m;
        }

        private Material FlashPaint(Material from, float k)
        {
            if (flashPaint == null) { flashPaint = new Material(PaintOf(Tier, RankGearData.Current.Of(Tier).crown)); flashPaint.EnableKeyword("_EMISSION"); }
            flashPaint.SetColor("_EmissionColor", (Tier >= 6 ? RankGearData.Current.marshalGlow : Color.white) * k * 0.9f);
            return flashPaint;
        }

        private static Material ringPaint;

        private static Material RingPaint()
        {
            if (ringPaint == null) ringPaint = MaterialFactory.CreateTransparent(RankGearData.Current.marshalGlow, RankGearData.Current.marshalGlow);
            return ringPaint;
        }
    }
}
