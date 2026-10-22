using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The gear made as models (Resources/Gear: a mesh and its colour picture each): weapons, shields and capes.
    /// The files are prepared so that a weapon stands up its length with the hand's place on its grip at the origin,
    /// a shield stands up facing the front, and a cape hangs down from its top; here they are put in place, sized and
    /// painted. Which item wears which model is listed below; the rest keep the looks made in code.
    /// </summary>
    public static class GearFiles
    {
        /// <summary>Weapon id → its model.</summary>
        private static readonly Dictionary<string, string> weapons = new Dictionary<string, string>
        {
            // the workshop's own
            ["mallet"] = "basit_cekic",
            ["steelHammer"] = "ikinci_seviye_cekic",
            ["thunderMaul"] = "orta_seviye_cekic",
            ["mace"] = "ikinci_seviye_gurz",
            ["novaMace"] = "ileri_seviye_gurz",
            // the ranks' swords
            ["emberBlade"] = "bascavus_kilic",
            ["frostEdge"] = "ustegmen_kilic",
            ["colonelBlade"] = "yarbay_kilic",
            ["kingsBlade"] = "maresal_kilic",
            // the golden chest's swords
            ["nightBlade"] = "altin_kasa_kilic_1",
            ["dawnBlade"] = "altin_kasa_kilic_2",
            ["emeraldBlade"] = "altin_kasa_kilic_3",
        };

        /// <summary>Cape id → its model.</summary>
        private static readonly Dictionary<string, string> capes = new Dictionary<string, string>
        {
            ["back.cape.basic"] = "basit_pelerin",
            ["back.cape.mid"] = "orta_seviye_pelerin",
            ["back.cape.rank3"] = "bascavus_pelerin",
            ["back.cape.rank6"] = "ustegmen_pelerin",
            ["back.cape.rank9"] = "yarbay_pelerin",
            ["back.cape.rank12"] = "maresal_pelerin",
        };

        /// <summary>The capes whose model was made seen from the front (its lining toward the viewer): worn turned round.</summary>
        private static readonly HashSet<string> flipped = new HashSet<string> { "back.cape.rank3" };

        public static bool CapeFlipped(string id) => flipped.Contains(id);

        public static bool HasWeapon(string id) => weapons.ContainsKey(id) && Resources.Load<GameObject>("Gear/" + weapons[id]) != null;
        public static string CapeModel(string id) => id != null && capes.TryGetValue(id, out var m) && Resources.Load<GameObject>("Gear/" + m) != null ? m : null;

        private static readonly Dictionary<string, Material> paints = new Dictionary<string, Material>();

        /// <summary>The model's paint: its own picture, a soft sheen; <paramref name="glow"/> lets it shine a little on its own.</summary>
        public static Material Paint(string model, float glow = 0f, bool twoSided = false)
        {
            string key = model + glow + twoSided;
            if (paints.TryGetValue(key, out var m) && m != null) return m;
            var tex = Resources.Load<Texture2D>("Gear/" + model + "_tex");
            m = MaterialFactory.CreateSurface(Color.white, Color.black, 0.35f, 0.1f); // (its own picture shows: little metal sheen)
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
                if (glow > 0f)
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    m.SetTexture("_EmissionMap", tex);
                    m.SetColor("_EmissionColor", Color.white * glow);
                }
            }
            if (twoSided && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            paints[key] = m;
            return m;
        }

        /// <summary>
        /// A model made upright: under a new root named <paramref name="name"/>, turned so its long side is up,
        /// <paramref name="height"/> tall; <paramref name="anchor"/> (0 its foot .. 1 its top) at the root's origin.
        /// </summary>
        public static Transform Place(Transform parent, string name, string model, float height, float anchor, Material paint, bool turnUp = true)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var prefab = Resources.Load<GameObject>("Gear/" + model);
            if (prefab == null) return root;
            var inner = Object.Instantiate(prefab, root, false).transform;
            inner.name = "Model";
            foreach (var r in inner.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = paint;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // its long side up (the file's own up may have come in turned by the import)
            var b = LocalBounds(root, inner);
            var s = b.size;
            if (!turnUp) { if (s.z > s.y && s.z >= s.x * 0.5f) inner.localRotation = Quaternion.Euler(-90f, 0f, 0f) * inner.localRotation; } // (a cape: made upright, only the import's z-up undone)
            else if (s.z > s.y && s.z >= s.x) inner.localRotation = Quaternion.Euler(-90f, 0f, 0f) * inner.localRotation;
            else if (s.x > s.y && s.x > s.z) inner.localRotation = Quaternion.Euler(0f, 0f, 90f) * inner.localRotation;
            b = LocalBounds(root, inner);
            float k = height / Mathf.Max(1e-4f, b.size.y);
            inner.localScale *= k;
            b = LocalBounds(root, inner);
            inner.localPosition -= new Vector3(b.center.x, b.min.y + b.size.y * anchor, b.center.z);
            return root;
        }

        private static Bounds LocalBounds(Transform root, Transform inner)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var mf in inner.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = root.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>A weapon's model: <paramref name="length"/> long, the hand's place on its grip at the origin (12% up).</summary>
        public static Transform Weapon(Transform parent, string id, float length, float glow)
        {
            string model = weapons[id];
            return Place(parent, id, model, length, 0.12f, Paint(model, glow));
        }

        /// <summary>A shield's model, <paramref name="height"/> tall, centred, facing +Z (out from the arm it is on).</summary>
        public static Transform Shield(Transform parent, string model, float height, float glow)
            => Place(parent, model, model, height, 0.5f, Paint(model, glow));

        /// <summary>A cape standing as a picture (the workshop's and the chests'), <paramref name="height"/> tall, its top at the origin.</summary>
        public static Transform CapePicture(Transform parent, string capeId, float height)
        {
            var m = CapeModel(capeId);
            return m == null ? null : Place(parent, capeId, m, height, 1f, Paint(m, 0f, true), false);
        }
    }
}
