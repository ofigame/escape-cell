using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    /// <summary>
    /// Little pictures of the city pieces for the building tray: each model is built far below the world,
    /// photographed once from the city's own angle into a small texture, and thrown away.
    /// </summary>
    public static class CityThumbs
    {
        private const int Size = 160;
        private static readonly Dictionary<string, RenderTexture> cache = new Dictionary<string, RenderTexture>();
        private static Camera cam;

        public static Texture Get(CityPiece piece)
        {
            if (cache.TryGetValue(piece.id, out var rt) && rt != null && rt.IsCreated()) return rt;
            rt = Render(piece);
            cache[piece.id] = rt;
            return rt;
        }

        private static RenderTexture Render(CityPiece piece)
        {
            var root = new GameObject("Thumb " + piece.id).transform;
            root.position = new Vector3(0f, -500f, 0f);
            CityModels.Build(piece, root);

            // Frame the model's bounds.
            var bounds = new Bounds(root.position, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);

            if (cam == null)
            {
                var go = new GameObject("ThumbCamera");
                Object.DontDestroyOnLoad(go);
                cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.2f, 0.19f, 0.4f, 1f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 60f;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = false;
                    data.antialiasing = AntialiasingMode.None;
                }
            }
            cam.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            cam.transform.position = bounds.center - cam.transform.forward * 30f;
            float extent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
            cam.orthographicSize = extent * 1.05f + 0.05f;

            var rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { name = "Thumb " + piece.id, antiAliasing = 2 };
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            Object.DestroyImmediate(root.gameObject);
            return rt;
        }
    }
}
