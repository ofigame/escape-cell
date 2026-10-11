using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    /// <summary>
    /// Pictures of the player's defences for the UI (the workshop's cards, the set-up screen's buttons): the real 3D
    /// model of each kind and tier, rendered once off to the side on a clear background and kept.
    /// </summary>
    public static class DefensePortraits
    {
        private const int Layer = 29, Size = 256;
        private static readonly Dictionary<(DefenseKind, int), Texture2D> cache = new Dictionary<(DefenseKind, int), Texture2D>();

        public static Texture2D Get(DefenseKind kind, int tier)
        {
            if (cache.TryGetValue((kind, tier), out var tex) && tex != null) return tex;
            tex = Render(kind, tier);
            cache[(kind, tier)] = tex;
            return tex;
        }

        private static Texture2D Render(DefenseKind kind, int tier)
        {
            var origin = new Vector3(-5000f, 0f, -5000f);
            var stage = new GameObject("PortraitStage").transform;
            stage.position = origin;
            bool shadows = Shapes.CastShadows;
            Shapes.CastShadows = false;
            var model = DefenseModel.Build(kind, tier, stage);
            Shapes.CastShadows = shadows;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
            foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;

            // Frame its bounds.
            var bounds = new Bounds(origin + Vector3.up, Vector3.one * 0.1f);
            foreach (var r in stage.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var camGo = new GameObject("PortraitCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << Layer;
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            float radius = bounds.extents.magnitude;
            var dir = new Vector3(0.2f, 0.45f, -1f).normalized;
            cam.transform.position = bounds.center + dir * (radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            cam.transform.LookAt(bounds.center);
            var key = new GameObject("PortraitKey").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.6f;
            key.cullingMask = 1 << Layer;
            key.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var fill = new GameObject("PortraitFill").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.6f, 0.85f, 1f);
            fill.intensity = 3f;
            fill.range = radius * 6f;
            fill.cullingMask = 1 << Layer;
            fill.transform.position = bounds.center + new Vector3(-1f, 0.5f, -1f).normalized * radius * 2.5f;

            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(camGo);
            Object.Destroy(key.gameObject);
            Object.Destroy(fill.gameObject);
            Object.Destroy(stage.gameObject);
            return tex;
        }
    }
}
