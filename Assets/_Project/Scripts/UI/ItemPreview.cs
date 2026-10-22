using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>
    /// Big pictures of the armory's weapons for the shop cards and the "try this" hint: each weapon is built once on a
    /// little stage far below the world, lit and photographed at an angle into its own texture, then put away.
    /// </summary>
    public static class ItemPreview
    {
        private static readonly Dictionary<string, RenderTexture> cache = new Dictionary<string, RenderTexture>();
        private static Camera cam;
        private static Transform stage;
        private static readonly Vector3 Origin = new Vector3(0f, -400f, 0f);

        public static Texture Weapon(WeaponDef w)
        {
            if (cache.TryGetValue(w.id, out var rt) && rt != null && rt.IsCreated()) return rt;
            Setup();
            var holder = new GameObject("Preview").transform;
            holder.SetParent(stage, false);
            var model = WeaponModels.Build(holder, w, 1f);
            // Lay it diagonally across the picture, centred on its length.
            float length = w.kind == WeaponKind.Spear ? 1.05f : 0.85f;
            holder.localRotation = Quaternion.Euler(0f, 0f, -42f);
            model.localPosition = new Vector3(0f, -length * 0.5f, 0f);
            cam.orthographicSize = length * 0.46f;
            if (GearFiles.HasWeapon(w.id)) cam.orthographicSize = Centre(holder, model) * 0.5f;
            rt = Snapshot.Take(cam, 512, "Weapon " + w.id);
            Object.DestroyImmediate(holder.gameObject); // now, not at the end of the frame: the next picture must not show it
            cache[w.id] = rt;
            return rt;
        }

        /// <summary>A picture of one of foi's builds, three-quarters from the front.</summary>
        public static Texture Hero(int hero)
        {
            string key = "hero" + hero;
            if (cache.TryGetValue(key, out var rt) && rt != null && rt.IsCreated()) return rt;
            Setup();
            var holder = new GameObject("Preview").transform;
            holder.SetParent(stage, false);
            var colours = HeroModels.Colours(hero);
            var body = MaterialFactory.Create(colours.body, Color.black);
            var light = MaterialFactory.Create(colours.light, Color.black);
            var dark = MaterialFactory.Create(Palette.RobotDark, Color.black);
            var eye = MaterialFactory.Create(colours.eye, colours.eye);
            HeroModels.Build(holder, hero, body, light, dark, eye);
            holder.localRotation = Quaternion.Euler(0f, 205f, 0f);
            holder.localPosition = new Vector3(0f, -0.44f, 0f);
            cam.orthographicSize = 0.52f;
            rt = Snapshot.Take(cam, 512, "Hero " + hero);
            Object.DestroyImmediate(holder.gameObject);
            cache[key] = rt;
            return rt;
        }

        /// <summary>
        /// A picture of a garage piece (a hat, a cape or back gear, arms, legs), built on its own and framed whole: hats,
        /// arms and legs three-quarters from the front, back gear from behind. Null for the other kinds and the plain ones.
        /// </summary>
        public static Texture Cosmetic(Cosmetic c)
        {
            if (c == null || !(c.slot == Slot.Head || (!c.IsDefault && (c.slot == Slot.Hat || c.slot == Slot.Back || c.slot == Slot.Arms || c.slot == Slot.Legs || c.slot == Slot.Suit)))) return null;
            string key = "cos." + c.id;
            if (cache.TryGetValue(key, out var rt) && rt != null && rt.IsCreated()) return rt;
            Setup();
            var holder = new GameObject("Preview").transform;
            holder.SetParent(stage, false);
            var piece = new GameObject("Piece").transform;
            piece.SetParent(holder, false);
            switch (c.slot)
            {
                case Slot.Hat:
                    if (c.id.StartsWith("hat.hood"))
                    {
                        var hp = Resources.Load<GameObject>("Heroes/Foi/hood");
                        if (hp != null) { var made = Object.Instantiate(hp, piece, false); var m = MaterialFactory.CreateSurface(c.color, Color.black, 0.15f, 0f); var tx = Resources.Load<Texture2D>("Heroes/Foi/hood"); if (tx != null) { m.SetTexture("_BaseMap", tx); m.SetTexture("_MainTex", tx); } foreach (var r in made.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m; }
                    }
                    else CosmeticModels.Hat(piece, c.id, c.color);
                    break;
                case Slot.Back:
                    if (c.id.StartsWith("back.cape"))
                    {
                        // a cape as it is worn: foi seen from behind, the cape hanging on it
                        var look = new GameObject("Foi").transform;
                        look.SetParent(piece, false);
                        SkinnedHero.Build(look);
                        var body = look.GetComponentInChildren<SkinnedHero>().transform;
                        SkinnedHero.Dress(body, new SkinnedHero.Look { head = 1 });
                        SoftCape.Build(body, c.id, c.color).Settle();
                        // the cape alone in the picture (foi only holds it up)
                        foreach (var r in look.GetComponentsInChildren<Renderer>(true))
                        {
                            bool cape = false;
                            for (var p = r.transform; p != null && p != look; p = p.parent) if (p.name == "Hero_Cape" || p.name == "Fittings") { cape = true; break; }
                            if (!cape) r.enabled = false;
                        }
                    }
                    else CosmeticModels.Back(piece, c.id, c.color);
                    break;
                case Slot.Suit:
                {
                    // an armour paint: foi wearing it
                    var look = new GameObject("Foi").transform;
                    look.SetParent(piece, false);
                    SkinnedHero.Build(look);
                    var body = look.GetComponentInChildren<SkinnedHero>().transform;
                    SkinnedHero.Dress(body, new SkinnedHero.Look { head = 1, suit = c.IsDefault ? null : c.id.Replace("suit.", "") });
                    body.GetComponent<SkinnedHero>().PoseNow(); // (standing, not in its T)
                    break;
                }
                case Slot.Head: { var h = Resources.Load<GameObject>("Heroes/Foi/head_" + c.id.Replace("head.", "")); if (h != null) { var made = Object.Instantiate(h, piece, false); foreach (var r in made.GetComponentsInChildren<Renderer>()) r.sharedMaterial = SkinnedHero.HeadMaterial(int.Parse(c.id.Replace("head.", ""))); } break; } // (its own picture)
                default: GearModels.Picture(piece, c.id, c.color); break; // arm and leg gear, as foi wears it
            }
            holder.localRotation = Quaternion.Euler(c.slot == Slot.Head ? 0f : 10f, c.slot == Slot.Back ? (c.id.StartsWith("back.cape") ? 20f : 330f) : c.slot == Slot.Head ? 180f : 200f, 0f); // (a cape: foi from behind)
            // Framed on what was built: its middle in the middle of the picture, all of it in view.
            var renderers = System.Array.FindAll(holder.GetComponentsInChildren<Renderer>(), r => r.enabled); // (what shows)
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                holder.position += stage.position - b.center;
                cam.orthographicSize = Mathf.Max(0.05f, Mathf.Max(b.extents.y, b.extents.x) * 1.18f);
            }
            cam.transform.localPosition = new Vector3(0f, 0f, -4f);
            rt = Snapshot.Take(cam, 256, "Cosmetic " + c.id);
            Object.DestroyImmediate(holder.gameObject);
            cache[key] = rt;
            return rt;
        }

        // ---------- Turning pictures (the shop's weapon cards) ----------

        private class Live
        {
            public string id;
            public Transform holder;
            public RenderTexture rt;
            public float size, phase;
            public int users;
            /// <summary>Cards showing it now (a hidden shop's cards are not drawn for).</summary>
            public int visible;
        }

        private static readonly Dictionary<string, Live> live = new Dictionary<string, Live>();
        private static readonly List<Live> liveOrder = new List<Live>();
        private static int liveNext, liveSlot;

        /// <summary>
        /// A weapon's picture turning all the way round (360°) on its own little turntable, for as long as a card shows it
        /// (<see cref="Release"/> when the card goes). A few are drawn each frame, in turn.
        /// </summary>
        public static Texture Spinning(WeaponDef w)
        {
            Setup();
            if (live.TryGetValue(w.id, out var l) && l.rt != null && l.holder != null)
            {
                // Already turning (its texture may have been lost: made again in place, the cards keep it).
                if (!l.rt.IsCreated()) { l.rt.Create(); Draw(l); }
                l.users++;
                return l.rt;
            }
            l = new Live { id = w.id, users = 1, phase = liveSlot * 47f };
            l.rt = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2, name = "Spin " + w.id };
            l.rt.Create();
            l.holder = new GameObject("Spin " + w.id).transform;
            l.holder.SetParent(stage, false);
            l.holder.localPosition = new Vector3(10f + 6f * (liveSlot++ % 64), 0f, 0f);
            var model = WeaponModels.Build(l.holder, w, 1f);
            float length = w.kind == WeaponKind.Spear ? 1.05f : w.kind == WeaponKind.Gun ? 0.7f : 0.85f;
            // Centred on the turntable: a blade stands up (its middle at the centre), a gun lies level.
            model.localPosition = w.kind == WeaponKind.Gun ? new Vector3(0f, -0.02f, -0.12f) : new Vector3(0f, -length * 0.5f, 0f);
            if (w.kind == WeaponKind.Gun) model.localScale *= 1.15f;
            l.size = length * (w.kind == WeaponKind.Gun ? 0.6f : 0.5f);
            if (GearFiles.HasWeapon(w.id)) l.size = Centre(l.holder, model) * 0.55f; // (a model: framed on itself)
            live[w.id] = l;
            liveOrder.Add(l);
            Draw(l);
            return l.rt;
        }

        /// <summary>A shield's picture turning round (as <see cref="Spinning"/>), for as long as a card shows it.</summary>
        public static Texture SpinningShield(ShieldDef s)
        {
            Setup();
            if (live.TryGetValue(s.id, out var l) && l.rt != null && l.holder != null)
            {
                if (!l.rt.IsCreated()) { l.rt.Create(); Draw(l); }
                l.users++;
                return l.rt;
            }
            l = new Live { id = s.id, users = 1, phase = liveSlot * 47f };
            l.rt = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2, name = "Spin " + s.id };
            l.rt.Create();
            l.holder = new GameObject("Spin " + s.id).transform;
            l.holder.SetParent(stage, false);
            l.holder.localPosition = new Vector3(10f + 6f * (liveSlot++ % 64), 0f, 0f);
            GearFiles.Shield(l.holder, s.model, 0.8f, s.chest ? 0.15f : 0f);
            l.size = 0.5f;
            live[s.id] = l;
            liveOrder.Add(l);
            Draw(l);
            return l.rt;
        }

        /// <summary>Moves <paramref name="model"/> so its middle is at <paramref name="holder"/>'s centre; returns its longest side.</summary>
        private static float Centre(Transform holder, Transform model)
        {
            var rs = model.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return 1f;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            model.position += holder.position - b.center;
            float k = Mathf.Max(1e-4f, holder.lossyScale.y);
            return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) / k;
        }

        /// <summary>A card showing a turning picture is gone: its turntable goes when no card shows it.</summary>
        public static void Release(string id)
        {
            if (!live.TryGetValue(id, out var l) || --l.users > 0) return;
            live.Remove(id);
            liveOrder.Remove(l);
            if (l.holder != null) Object.Destroy(l.holder.gameObject);
            if (l.rt != null) { l.rt.Release(); Object.Destroy(l.rt); }
        }

        private static void Draw(Live l)
        {
            // A slow turn round the upright, tipped a little towards the camera so the top shows too.
            l.holder.localRotation = Quaternion.Euler(14f, Time.unscaledTime * 75f + l.phase, 0f);
            var camPos = l.holder.localPosition + new Vector3(0f, 0f, -4f);
            cam.transform.localPosition = camPos;
            cam.orthographicSize = l.size;
            cam.targetTexture = l.rt;
            cam.Render();
            cam.targetTexture = null;
            cam.transform.localPosition = new Vector3(0f, 0f, -4f);
        }

        /// <summary>Draws a few of the turning pictures each frame (all of them in turn).</summary>
        private class Spinner : MonoBehaviour
        {
            private void LateUpdate()
            {
                // Only the ones a card on screen shows: a closed shop costs nothing.
                int drawn = 0;
                for (int i = 0; i < liveOrder.Count && drawn < 2; i++)
                {
                    liveNext = (liveNext + 1) % liveOrder.Count;
                    var l = liveOrder[liveNext];
                    if (l.holder == null || l.rt == null || l.visible <= 0) continue;
                    Draw(l);
                    drawn++;
                }
            }
        }

        /// <summary>Put on a card's picture: tells the turntable when the card is gone.</summary>
        public class Lease : MonoBehaviour
        {
            public string id;
            private bool counted;
            private void OnEnable() => Count(true);
            private void Start() => Count(true); // (the id is set just after the lease is put on)
            private void OnDisable() => Count(false);
            private void OnDestroy() { if (!string.IsNullOrEmpty(id)) Release(id); }

            private void Count(bool on)
            {
                if (on == counted || string.IsNullOrEmpty(id) || !live.TryGetValue(id, out var l)) return;
                counted = on;
                l.visible += on ? 1 : -1;
            }
        }

        private static void Setup()
        {
            if (cam != null) return;
            var root = new GameObject("ItemPreviewStage");
            Object.DontDestroyOnLoad(root);
            root.transform.position = Origin;
            stage = new GameObject("Stage").transform;
            stage.SetParent(root.transform, false);
            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -4f);
            camGo.transform.localRotation = Quaternion.identity;
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 10f;
            cam.enabled = false;
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(camGo.transform, false);
            key.type = LightType.Point;
            key.range = 8f;
            key.intensity = 3f;
            key.transform.localPosition = new Vector3(-1.5f, 1.5f, 1.5f); // beside the camera (it moves from turntable to turntable)
            root.AddComponent<Spinner>();
        }
    }
}
