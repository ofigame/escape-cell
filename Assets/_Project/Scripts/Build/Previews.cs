using System.Collections.Generic;
using BrickMuseum.Data;
using UnityEngine;

namespace BrickMuseum.Build
{
    /// <summary>
    /// Pictures of pieces (bag) and models (roulette, collection), photographed on a hidden stage far below the world into
    /// readable textures (the bag hit-tests their alpha), plus the live turning 3D view of the target model.
    /// </summary>
    public static class Previews
    {
        public const int PictureLayer = 30;
        private static readonly Vector3 Stage = new Vector3(0f, -2000f, 0f);
        private static readonly Vector3 LiveStage = new Vector3(0f, -3000f, 0f);
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private static Camera cam, liveCam;
        private static Transform liveHolder, livePivot;
        private static RenderTexture liveRt;

        private static void Setup()
        {
            if (cam != null) return;
            cam = MakeCamera("PictureCamera", Stage);
            var key = new GameObject("PictureKey").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.2f;
            key.cullingMask = 1 << PictureLayer;
            key.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            key.shadows = LightShadows.None;
        }

        private static Camera MakeCamera(string name, Vector3 at)
        {
            var c = new GameObject(name).AddComponent<Camera>();
            c.transform.position = at;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0f, 0f, 0f, 0f);
            c.cullingMask = 1 << PictureLayer;
            c.fieldOfView = 30f;
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 200f;
            c.enabled = false;
            return c;
        }

        public static Texture2D Piece(Piece kind) => Photo("p:" + kind.key, 160, () => PieceFactory.Create(kind, PieceLook.Picture));

        public static Texture2D Model(ModelDef m, int size = 256) => Photo($"m:{m.id}:{size}", size, () => PieceFactory.BuildModel(m, PieceLook.Picture));

        public static Texture2D Photo(string key, int size, System.Func<GameObject> make)
        {
            if (cache.TryGetValue(key, out var tex)) return tex;
            Setup();
            var go = make();
            go.transform.position = Stage;
            SetLayer(go, PictureLayer);
            var b = Bounds(go);
            go.transform.position += Stage - b.center;
            float r = b.extents.magnitude;
            cam.transform.position = Stage + new Vector3(0.9f, 1.0f, 1.5f).normalized * (r / Mathf.Sin(15f * Mathf.Deg2Rad) * 1.02f);
            cam.transform.LookAt(Stage);
            var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = key };
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(go);
            return cache[key] = tex;
        }

        public static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // ---------- live view ----------

        /// <summary>The texture the target card shows: the model turning slowly (drag turns it by hand).</summary>
        public static RenderTexture Live
        {
            get
            {
                if (liveRt != null) return liveRt;
                Setup();
                liveRt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "LiveTarget" };
                liveRt.Create();
                liveCam = MakeCamera("LiveCamera", LiveStage);
                liveCam.targetTexture = liveRt;
                liveCam.enabled = true;
                livePivot = new GameObject("LivePivot").transform;
                livePivot.position = LiveStage;
                return liveRt;
            }
        }

        public static void ShowLive(ModelDef m)
        {
            _ = Live;
            if (liveHolder != null) Object.Destroy(liveHolder.gameObject);
            var go = PieceFactory.BuildModel(m, PieceLook.Picture);
            liveHolder = go.transform;
            liveHolder.SetParent(livePivot, false);
            SetLayer(go, PictureLayer);
            livePivot.rotation = Quaternion.identity;
            var b = Bounds(go);
            liveHolder.position += LiveStage - b.center;
            float r = b.extents.magnitude;
            liveCam.transform.position = LiveStage + new Vector3(0f, 0.55f, 1.5f).normalized * (r / Mathf.Sin(16f * Mathf.Deg2Rad) * 1.02f);
            liveCam.transform.LookAt(LiveStage);
        }

        public static float LiveSpin = 25f;

        public static void TurnLive(float degrees) { if (livePivot != null) livePivot.Rotate(0f, degrees, 0f, Space.World); }

        public static void Tick(float dt, bool liveVisible)
        {
            if (liveCam != null) liveCam.enabled = liveVisible;
            if (livePivot != null && liveVisible) livePivot.Rotate(0f, LiveSpin * dt, 0f, Space.World);
        }
    }
}
