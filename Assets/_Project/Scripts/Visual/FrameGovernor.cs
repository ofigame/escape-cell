using SquashBot.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GraphicsTier = SquashBot.Data.GraphicsTier;

namespace SquashBot.Visual
{
    /// <summary>
    /// Sets the picture quality from the graphics tier (<see cref="GraphicsQuality"/>) and keeps phones smooth.
    /// High: the screen's full resolution, 4x MSAA, high-quality glow, full weather. Medium: 2x MSAA and a slightly
    /// smaller scene on very tall screens. Low: no MSAA, a smaller scene, quarter-size glow and half the weather
    /// particles. On phones, when the frame rate still drops, the glow gets cheaper first, then the scene resolution
    /// steps down a little (upscaled with AMD FSR and sharpened); when frames are fast again, it steps back up.
    /// </summary>
    public class FrameGovernor : MonoBehaviour
    {
        private const float Step = 0.05f;
        private const float Window = 2f;

        private UniversalRenderPipelineAsset urp;
        private bool adaptive;
        private float maxScale, minScale, scale;
        private bool baseQuarterBloom, lightBloom;
        private float time;
        private int frames;
        private float calm;

        public static void Install()
        {
            if (FindAnyObjectByType<FrameGovernor>() != null) return;
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
            adaptive = Application.isMobilePlatform;
            // FSR only kicks in below 100%; its sharpening keeps the upscaled picture as crisp as native.
            urp.upscalingFilter = UpscalingFilterSelection.FSR;
            urp.fsrOverrideSharpness = true;
            urp.fsrSharpness = 0.9f;
            GraphicsQuality.Changed += Apply;
            Apply();
        }

        private void OnDestroy() => GraphicsQuality.Changed -= Apply;

        /// <summary>Applies the current tier (at start, and when the player picks another one).</summary>
        private void Apply()
        {
            var tier = GraphicsQuality.Current;
            float longSide = Mathf.Max(Screen.width, Screen.height, 1);
            float maxPixels = tier == GraphicsTier.High ? 3200f : tier == GraphicsTier.Medium ? 2200f : 1700f;
            minScale = tier == GraphicsTier.High ? 0.9f : tier == GraphicsTier.Medium ? 0.85f : 0.75f;
            maxScale = Mathf.Clamp(maxPixels / longSide, minScale, 1f);
            scale = maxScale;
            urp.renderScale = scale;
            if (adaptive) urp.msaaSampleCount = tier == GraphicsTier.High ? 4 : tier == GraphicsTier.Medium ? 2 : 1;
            baseQuarterBloom = tier == GraphicsTier.Low;
            lightBloom = false;
            ApplyBloom(tier == GraphicsTier.High);
            Weather.Density = tier == GraphicsTier.Low ? 0.5f : tier == GraphicsTier.Medium ? 0.8f : 1f;
            time = 0f;
            frames = 0;
            calm = 0f;
        }

        private void Update()
        {
            // Paused or in the background: nothing to judge.
            if (!adaptive || Time.timeScale <= 0f) return;
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
                if (!lightBloom && !baseQuarterBloom) { lightBloom = true; ApplyBloom(false); }
                else if (scale > minScale + 0.001f) scale = Mathf.Max(minScale, scale - Step);
                urp.renderScale = scale;
            }
            else if (fps > target * 0.95f)
            {
                // Climb back slowly, so it doesn't flip back and forth.
                calm += Window;
                if (calm < 8f) return;
                calm = 0f;
                if (scale < maxScale - 0.001f) scale = Mathf.Min(maxScale, scale + Step);
                else if (lightBloom) { lightBloom = false; ApplyBloom(GraphicsQuality.Current == GraphicsTier.High); }
                urp.renderScale = scale;
            }
        }

        private void ApplyBloom(bool highQuality)
        {
            bool quarter = baseQuarterBloom || lightBloom;
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (volume.sharedProfile != null && volume.sharedProfile.TryGet<Bloom>(out var bloom))
                {
                    bloom.downscale.Override(quarter ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
                    bloom.highQualityFiltering.Override(highQuality && !quarter);
                }
        }
    }
}
