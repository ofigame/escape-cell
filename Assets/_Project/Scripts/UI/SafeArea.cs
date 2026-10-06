using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>
    /// Fits a RectTransform to the device safe area, so buttons and text stay clear of notches,
    /// camera cut-outs and the home indicator. All screens live under one of these.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform rect;
        private Rect applied;
        private Vector2Int screen;

        /// <summary>The usable part of the screen in pixels.</summary>
        public static Rect Current => Screen.safeArea;

        private void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (applied != Current || screen.x != Screen.width || screen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            applied = Current;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            rect.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            rect.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// For full-screen backgrounds (dims, the map backdrop) placed inside the safe area:
    /// stretches back out to the real screen edges so nothing looks cut off around the notch.
    /// Assumes its parent covers exactly the safe area.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class IgnoreSafeArea : MonoBehaviour
    {
        private RectTransform rect;
        private Canvas canvas;

        private void Awake()
        {
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        private void LateUpdate()
        {
            if (canvas == null) return;
            float s = Mathf.Max(0.0001f, canvas.scaleFactor);
            var safe = SafeArea.Current;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-safe.xMin / s, -safe.yMin / s);
            rect.offsetMax = new Vector2((Screen.width - safe.xMax) / s, (Screen.height - safe.yMax) / s);
        }
    }
}
