using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    /// <summary>
    /// Keeps phones smooth without blurring the picture. The scene is drawn at the screen's own resolution (only very
    /// tall screens, over 2400 px, are drawn a little smaller). When the frame rate drops, the cheap-looking savings come
    /// first: the glow (bloom) is computed at a quarter of the size instead of half, which looks the same. Only then does
    /// the scene resolution step down, never below 85%, upscaled with AMD FSR and sharpened so edges stay crisp. When
    /// frames are fast again for a while, everything steps back up. Desktop builds are left alone.
    /// </summary>
    public class FrameGovernor : MonoBehaviour
    {
        private const float MaxPixels = 2400f; // the long screen side the scene is drawn at, at most
        private const float MinScale = 0.85f, Step = 0.075f;
        private const float Window = 2f;

        private UniversalRenderPipelineAsset urp;
        private float maxScale, scale;
        private bool lightBloom;
        private float time;
        private int frames;
        private float calm;

        public static void Install()
        {
            if (!Application.isMobilePlatform || FindAnyObjectByType<FrameGovernor>() != null) return;
            var go = new GameObject("FrameGovernor");
            DontDestroyOnLoad(go);
            go.AddComponent<FrameGovernor>();
        }

        private void Awake()
        {
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                enabled = false;
                return;
            }
            // FSR only kicks in below 100%; its sharpening keeps the upscaled picture as crisp as native.
            urp.upscalingFilter = UpscalingFilterSelection.FSR;
            urp.fsrOverrideSharpness = true;
            urp.fsrSharpness = 0.9f;
            maxScale = Mathf.Clamp(MaxPixels / Mathf.Max(Screen.width, Screen.height, 1), MinScale, 1f);
            scale = maxScale;
            urp.renderScale = scale;
        }

        private void Update()
        {
            // Paused or in the background: nothing to judge.
            if (Time.timeScale <= 0f) return;
            time += Time.unscaledDeltaTime;
            frames++;
            if (time < Window) return;
            float fps = frames / time;
            time = 0f;
            frames = 0;

            int target = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
            if (fps < target * 0.8f)
            {
                calm = 0f;
                if (!lightBloom) SetLightBloom(true);
                else if (scale > MinScale + 0.001f) scale = Mathf.Max(MinScale, scale - Step);
                urp.renderScale = scale;
            }
            else if (fps > target * 0.95f)
            {
                // Climb back slowly, so it doesn't flip back and forth.
                calm += Window;
                if (calm < 8f) return;
                calm = 0f;
                if (scale < maxScale - 0.001f) scale = Mathf.Min(maxScale, scale + Step);
                else if (lightBloom) SetLightBloom(false);
                urp.renderScale = scale;
            }
        }

        private void SetLightBloom(bool on)
        {
            lightBloom = on;
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (volume.sharedProfile != null && volume.sharedProfile.TryGet<Bloom>(out var bloom))
                    bloom.downscale.Override(on ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
        }
    }
}
