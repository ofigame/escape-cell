using UnityEngine;

namespace BrickMuseum.UI
{
    /// <summary>
    /// Keeps a screen's controls inside the phone's safe area (clear of an iPhone's notch and home bar, of rounded
    /// corners), following rotation: the rect's anchors are the safe area as a share of the screen.
    /// </summary>
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect last;
        private Vector2Int size;
        private RectTransform rect;

        private void Awake() { rect = (RectTransform)transform; Apply(); }

        private void Update()
        {
            if (Screen.safeArea != last || size.x != Screen.width || size.y != Screen.height) Apply();
        }

        private void Apply()
        {
            last = Screen.safeArea;
            size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var min = last.position;
            var max = last.position + last.size;
            rect.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            rect.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
