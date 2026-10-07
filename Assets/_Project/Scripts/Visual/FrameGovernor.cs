using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    /// <summary>
    /// Keeps phones smooth. The 3D scene is drawn below the screen's native resolution (the UI stays sharp), and when the
    /// frame rate still drops — a big late floor on a slower phone — the scene resolution steps down a bit more, then MSAA
    /// goes; when frames are fast again for a while, it steps back up. Desktop builds are left alone.
    /// </summary>
    public class FrameGovernor : MonoBehaviour
    {
        private const float TargetPixels = 1700f; // the long screen side the scene is drawn at, at most
        private const float MinScale = 0.55f, Step = 0.08f;
        private const float Window = 2f;

        private UniversalRenderPipelineAsset urp;
        private float maxScale, scale;
        private int originalMsaa;
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
            originalMsaa = urp.msaaSampleCount;
            maxScale = Mathf.Clamp(TargetPixels / Mathf.Max(Screen.width, Screen.height, 1), MinScale, 1f);
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
                if (scale > MinScale + 0.001f) scale = Mathf.Max(MinScale, scale - Step);
                else if (urp.msaaSampleCount > 1) urp.msaaSampleCount = 1;
                urp.renderScale = scale;
            }
            else if (fps > target * 0.95f)
            {
                // Climb back slowly, so it doesn't flip back and forth.
                calm += Window;
                if (calm < 10f) return;
                calm = 0f;
                if (urp.msaaSampleCount < originalMsaa) urp.msaaSampleCount = originalMsaa;
                else if (scale < maxScale - 0.001f) scale = Mathf.Min(maxScale, scale + Step);
                urp.renderScale = scale;
            }
        }
    }
}
