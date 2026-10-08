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
            key.transform.SetParent(root.transform, false);
            key.type = LightType.Point;
            key.range = 8f;
            key.intensity = 3f;
            key.transform.localPosition = new Vector3(-1.5f, 1.5f, -2.5f);
        }
    }
}
