using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.Combat
{
    /// <summary>
    /// The whole screen for a moment: dimmed (an ultimate's start: the rival and the land darker) or flashed white (its
    /// final blow). Its own overlay over the scene; off on the low quality setting.
    /// </summary>
    public class ScreenFlash : MonoBehaviour
    {
        private static ScreenFlash instance;
        private Image image;
        private Color colour;
        private float t = 1f, length = 0.3f, peak;
        private bool hold;

        /// <summary>The screen darkened (<paramref name="alpha"/>) for <paramref name="seconds"/>, easing in and out.</summary>
        public static void Dim(float alpha, float seconds) => Show(new Color(0f, 0f, 0.04f), alpha, seconds);

        /// <summary>A white flash for <paramref name="seconds"/>.</summary>
        public static void White(float seconds) => Show(Color.white, 0.85f, seconds);

        private static void Show(Color c, float alpha, float seconds)
        {
            var s = CombatLibrary.Settings;
            if (s.lowQualityTrims && Data.GraphicsQuality.Current == Data.GraphicsTier.Low) return;
            if (instance == null)
            {
                var go = new GameObject("ScreenFlash");
                DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 400; // (over the scene and the HUD, under the duel lab's panel)
                instance = go.AddComponent<ScreenFlash>();
                var img = new GameObject("Fill").AddComponent<Image>();
                img.transform.SetParent(go.transform, false);
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                instance.image = img;
            }
            instance.colour = c;
            instance.peak = Mathf.Clamp01(alpha);
            instance.length = Mathf.Max(0.02f, seconds);
            instance.t = 0f;
            instance.image.enabled = true;
        }

        private void Update()
        {
            if (t >= 1f) { if (image.enabled) image.enabled = false; return; }
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / length);
            float a = t < 0.2f ? t / 0.2f : t > 0.75f ? (1f - t) / 0.25f : 1f;
            image.color = new Color(colour.r, colour.g, colour.b, peak * a);
        }
    }
}
