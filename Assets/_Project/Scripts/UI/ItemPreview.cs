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
        /// A picture of a garage piece built on its own and framed whole: a head with its haircut from the front, a story
        /// badge face on. Null for the other kinds and the plain badge.
        /// </summary>
        public static Texture Cosmetic(Cosmetic c)
        {
            if (c == null || !(c.slot == Slot.Head || (c.slot == Slot.Badge && !c.IsDefault))) return null;
            string key = "cos." + c.id;
            if (cache.TryGetValue(key, out var rt) && rt != null && rt.IsCreated()) return rt;
            Setup();
            var holder = new GameObject("Preview").transform;
            holder.SetParent(stage, false);
            var piece = new GameObject("Piece").transform;
            piece.SetParent(holder, false);
            if (c.slot == Slot.Head) { int n = SkinnedHero.HeadNumber(c.id); SkinnedHero.HeadParts(piece, n); SkinnedHero.PaintHead(piece, n, null, null, null); } // (the head with its haircut, as made)
            else CosmeticModels.Badge(piece, c.id, c.color);
            holder.localRotation = Quaternion.Euler(c.slot == Slot.Head ? 0f : 10f, c.slot == Slot.Head ? 180f : 200f, 0f);
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

        /// <summary>Rank crown <paramref name="tier"/>'s still picture: from the front and a little above, its gem showing.</summary>
        public static Texture Crown(int tier)
        {
            string key = "crown:" + tier;
            if (cache.TryGetValue(key, out var rt) && rt != null && rt.IsCreated()) return rt;
            Setup();
            var holder = new GameObject("Preview").transform;
            holder.SetParent(stage, false);
            var model = RankCrown.Model(holder, tier);
            holder.localRotation = Quaternion.Euler(14f, 180f, 0f); // (its front is +z: turned to the camera)
            if (model != null) Centre(holder, model);
            cam.orthographicSize = 0.62f;
            rt = Snapshot.Take(cam, 256, "Crown " + tier);
            Object.DestroyImmediate(holder.gameObject);
            cache[key] = rt;
            return rt;
        }

        /// <summary>A shield's still picture, a little turned so its face and rim both show.</summary>
        public static Texture Shield(ShieldDef s)
        {
            string key = "shield:" + s.id;
            if (cache.TryGetValue(key, out var rt) && rt != null && rt.IsCreated()) return rt;
            Setup();
            var holder = new GameObject("Preview").transform;
            holder.SetParent(stage, false);
            GearFiles.Shield(holder, s.model, 0.8f, s.chest ? 0.15f : 0f);
            holder.localRotation = Quaternion.Euler(10f, 205f, 0f); // (its face is +Z: turned to the camera)
            cam.orthographicSize = 0.5f;
            rt = Snapshot.Take(cam, 512, "Shield " + s.id);
            Object.DestroyImmediate(holder.gameObject);
            cache[key] = rt;
            return rt;
        }

        /// <summary>Moves <paramref name="model"/> so its middle is at <paramref name="holder"/>'s centre; returns its longest side.</summary>
        private static float Centre(Transform holder, Transform model)
        {
            var rs = model.GetComponentsInChildren<Renderer>(true);
            Bounds b = default; bool any = false;
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer) continue; // (smoke and sparks: not the thing framed)
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return 1f;
            model.position += holder.position - b.center;
            float k = Mathf.Max(1e-4f, holder.lossyScale.y);
            return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) / k;
        }

        // ---------- The close look (one item, full screen, turned by the finger) ----------

        private static Transform viewHolder;
        private static RenderTexture viewRt;
        private static float viewSize;
        /// <summary>How the item in the close look is turned (degrees round the upright, and tipped).</summary>
        public static float ViewYaw, ViewPitch;
        /// <summary>Which close look is open now (each opening gets the next number).</summary>
        public static int ViewNumber { get; private set; }

        /// <summary>A weapon in the close look: built once on its own spot of the stage, drawn every frame until <see cref="CloseView"/>.</summary>
        public static Texture ViewWeapon(WeaponDef w)
        {
            var holder = OpenView();
            var model = WeaponModels.Build(holder, w, 1f);
            float length = w.kind == WeaponKind.Spear ? 1.05f : w.kind == WeaponKind.Gun ? 0.7f : 0.85f;
            model.localPosition = w.kind == WeaponKind.Gun ? new Vector3(0f, -0.02f, -0.12f) : new Vector3(0f, -length * 0.5f, 0f);
            if (w.kind == WeaponKind.Gun) model.localScale *= 1.15f;
            viewSize = length * (w.kind == WeaponKind.Gun ? 0.6f : 0.5f);
            if (GearFiles.HasWeapon(w.id)) viewSize = Centre(holder, model) * 0.55f; // (a model: framed on itself)
            ViewYaw = 200f; ViewPitch = 14f;
            DrawView();
            return viewRt;
        }

        /// <summary>A shield in the close look (as <see cref="ViewWeapon"/>).</summary>
        public static Texture ViewShield(ShieldDef s)
        {
            var holder = OpenView();
            GearFiles.Shield(holder, s.model, 0.8f, s.chest ? 0.15f : 0f);
            viewSize = 0.5f;
            ViewYaw = 205f; ViewPitch = 10f;
            DrawView();
            return viewRt;
        }

        /// <summary>foi itself in the close look (the bag's: its body by its parts, its battle wear), made by
        /// <paramref name="make"/> on the stage, turned to the camera.</summary>
        public static Texture ViewHero(System.Func<Transform, GameObject> make)
        {
            var holder = OpenView();
            var model = make != null ? make(holder) : null;
            if (model != null)
            {
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = holder.gameObject.layer;
                viewSize = Centre(holder, model.transform) * 0.55f;
            }
            else viewSize = 1f;
            ViewYaw = 180f; ViewPitch = 6f;
            DrawView();
            return viewRt;
        }

        /// <summary>The close look is gone: its model and its picture go (nothing is drawn for it any more).</summary>
        public static void CloseView(int number)
        {
            if (number == ViewNumber) CloseView();
        }

        private static void CloseView()
        {
            if (viewHolder != null) Object.Destroy(viewHolder.gameObject);
            viewHolder = null;
            if (viewRt != null) { viewRt.Release(); Object.Destroy(viewRt); }
            viewRt = null;
        }

        private static Transform OpenView()
        {
            CloseView();
            Setup();
            ViewNumber++;
            viewRt = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2, name = "Close look" }; // (sharp enough full screen; 4 samples at 1024 cost 120 MB)
            viewRt.Create();
            viewHolder = new GameObject("Close look").transform;
            viewHolder.SetParent(stage, false);
            viewHolder.localPosition = new Vector3(20f, 0f, 0f); // (away from where the still pictures are taken)
            return viewHolder;
        }

        private static void DrawView()
        {
            if (viewHolder == null || viewRt == null) return;
            if (!viewRt.IsCreated()) viewRt.Create();
            viewHolder.localRotation = Quaternion.Euler(ViewPitch, ViewYaw, 0f);
            cam.transform.localPosition = viewHolder.localPosition + new Vector3(0f, 0f, -4f);
            cam.orthographicSize = viewSize;
            cam.targetTexture = viewRt;
            cam.Render();
            cam.targetTexture = null;
            cam.transform.localPosition = new Vector3(0f, 0f, -4f);
        }

        /// <summary>Draws the close look each frame while it is open (nothing at all otherwise).</summary>
        private class Spinner : MonoBehaviour
        {
            private void LateUpdate() => DrawView();
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
            cam.backgroundColor = UiKit.A(UiKit.Style.black, 0f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 10f;
            cam.enabled = false;
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(camGo.transform, false);
            key.type = LightType.Point;
            key.range = 8f;
            key.intensity = 3f;
            key.transform.localPosition = new Vector3(-1.5f, 1.5f, 1.5f); // beside the camera (it moves with it to the close look)
            root.AddComponent<Spinner>();
        }
    }
}
