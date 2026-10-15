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
            float maxPixels = tier == GraphicsTier.High ? 3200f : tier == GraphicsTier.Medium ? 2400f : 2000f;
            maxScale = Mathf.Clamp(maxPixels / longSide, 0.8f, 1f);
            // High never trades sharpness for speed (the glow and weather give way instead); the others only a little.
            minScale = tier == GraphicsTier.High ? maxScale : Mathf.Min(maxScale, tier == GraphicsTier.Medium ? 0.85f : 0.8f);
            // FSR's extra full-size pass pays off on the stronger tiers; the weak one takes a plain, cheap upscale.
            urp.upscalingFilter = tier == GraphicsTier.Low ? UpscalingFilterSelection.Linear : UpscalingFilterSelection.FSR;
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
            // A long frame is a load (a floor or the map being built), not a slow GPU: start the count again after it.
            if (Time.unscaledDeltaTime > 0.2f) { time = 0f; frames = 0; return; }
            // The frame rate just changed (the pacer): the frames round it judge nothing.
            if (Time.unscaledTime - FramePacer.ChangedAt < 1f) { time = 0f; frames = 0; return; }
            time += Time.unscaledDeltaTime;
            frames++;
            if (time < Window) return;
            float fps = frames / time;
            time = 0f;
            frames = 0;

            int target = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
            // A screen or battery saver holding the game at 30: a cap, not an overload (lowering the picture would not help).
            float refresh = (float)Screen.currentResolution.refreshRateRatio.value;
            if (refresh > 1f && refresh < target) target = Mathf.RoundToInt(refresh);
            if (target > 40 && fps > 28.5f && fps < 31.5f) return;
            if (fps < target * 0.8f)
            {
                calm = 0f;
                if (!lightBloom && !baseQuarterBloom) { lightBloom = true; ApplyBloom(false); }
                else if (scale > minScale + 0.001f) scale = Mathf.Max(minScale, scale - Step);
                urp.renderScale = scale;
            }
            else if (fps > target * 0.9f)
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
