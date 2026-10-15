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
            rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Weapon " + w.id };
            rt.Create();
            var holder = new GameObject("Preview").transform;
            holder.SetParent(stage, false);
            var model = WeaponModels.Build(holder, w, 1f);
            // Lay it diagonally across the picture, centred on its length.
            float length = w.kind == WeaponKind.Spear ? 1.05f : 0.85f;
            holder.localRotation = Quaternion.Euler(0f, 0f, -42f);
            model.localPosition = new Vector3(0f, -length * 0.5f, 0f);
            cam.orthographicSize = length * 0.46f;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
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
            rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Hero " + hero };
            rt.Create();
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
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
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
            l.rt = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Spin " + w.id };
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
            live[w.id] = l;
            liveOrder.Add(l);
            Draw(l);
            return l.rt;
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
                for (int i = 0; i < liveOrder.Count && drawn < 4; i++)
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
