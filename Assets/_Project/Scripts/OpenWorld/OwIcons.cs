using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.OpenWorld
{
    /// <summary>
    /// Pictures for the open world's lists: each thing (a crop, a tree, a building with its animals, a defence, a
    /// soldier...) built from its real model off-screen, framed three-quarters from the front and drawn once into a
    /// small texture, kept while the open world is open.
    /// </summary>
    public static class OwIcons
    {
        private static readonly Dictionary<string, RenderTexture> cache = new Dictionary<string, RenderTexture>();
        private static Camera cam;
        private static Transform stage;
        private static readonly Vector3 Origin = new Vector3(0f, -600f, 0f);

        /// <summary>The picture of a thing (made the first time it is asked for).</summary>
        public static Texture Get(string key, Action<Transform> build)
        {
            if (cache.TryGetValue(key, out var rt) && rt != null && rt.IsCreated()) return rt;
            Setup();
            rt = new RenderTexture(192, 192, 16, RenderTextureFormat.ARGB32) { antiAliasing = 2, name = "OwIcon " + key }; // (kept while in the open world: light on memory)
            rt.Create();
            var holder = new GameObject("Icon").transform;
            holder.SetParent(stage, false);
            var model = new GameObject("Model").transform;
            model.SetParent(holder, false);
            build(model);
            // Turned three-quarters, framed on its bounds.
            holder.localRotation = Quaternion.Euler(18f, -35f, 0f);
            var renderers = holder.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                model.position -= b.center - stage.position;
                cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.12f + 0.05f;
            }
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(holder.gameObject);
            cache[key] = rt;
            return rt;
        }

        /// <summary>Lets every picture go (leaving the open world).</summary>
        public static void Clear()
        {
            foreach (var rt in cache.Values) if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
            cache.Clear();
        }

        private static void Setup()
        {
            if (cam != null) return;
            var root = new GameObject("OwIconStage");
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.transform.position = Origin;
            stage = new GameObject("Stage").transform;
            stage.SetParent(root.transform, false);
            var camGo = new GameObject("IconCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -20f);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            cam.enabled = false;
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(camGo.transform, false);
            key.type = LightType.Point;
            key.range = 60f;
            key.intensity = 4f;
            key.transform.localPosition = new Vector3(-6f, 8f, 4f);
        }
    }
}
