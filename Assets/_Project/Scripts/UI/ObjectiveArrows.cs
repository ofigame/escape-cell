using System.Collections.Generic;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// Arrows at the edge of the screen pointing at goals the close camera can't see (keys, quest pieces, the Thunder
    /// Hammer, the door, the thief, the helicopter's crate...). Each one is a faint, gently pulsing chevron in the goal's
    /// colour, sitting on the screen edge in the goal's direction; it disappears as soon as the goal is in view.
    /// </summary>
    public class ObjectiveArrows : MonoBehaviour
    {
        private const int Max = 4;
        private const float EdgeMargin = 70f;

        private RectTransform area;
        private readonly List<(RectTransform root, Image body, Image tip)> arrows = new List<(RectTransform, Image, Image)>();
        private readonly List<(Vector3 world, Color color)> targets = new List<(Vector3, Color)>();
        private Camera cam;

        public static ObjectiveArrows Create(Transform parent)
        {
            var rt = UiFactory.Stretch("ObjectiveArrows", parent);
            var a = rt.gameObject.AddComponent<ObjectiveArrows>();
            a.area = rt;
            for (int i = 0; i < Max; i++) a.arrows.Add(a.Build(i));
            return a;
        }

        private (RectTransform, Image, Image) Build(int i)
        {
            var root = UiFactory.Box("Arrow" + i, area, Vector2.zero, Vector2.zero, new Vector2(110f, 110f));
            root.pivot = new Vector2(0.5f, 0.5f);
            var body = UiFactory.Fill(UiFactory.Stretch("Disc", root), Color.white, UiSprites.Circle);
            body.raycastTarget = false;
            // A chevron pointing up in local space; the root is rotated towards the goal.
            var tipRoot = UiFactory.Box("Chevron", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(60f, 60f));
            tipRoot.pivot = new Vector2(0.5f, 0.5f);
            Image tip = null;
            foreach (float s in new[] { -1f, 1f })
            {
                var arm = UiFactory.Box("Arm", tipRoot, new Vector2(0.5f, 0.5f), new Vector2(s * 12f, 0f), new Vector2(16f, 44f));
                arm.pivot = new Vector2(0.5f, 0.5f);
                arm.localRotation = Quaternion.Euler(0f, 0f, s * 40f);
                tip = UiFactory.Fill(arm, Color.white, UiSprites.Rounded, 4f);
                tip.raycastTarget = false;
            }
            root.gameObject.SetActive(false);
            return (root, body, tip);
        }

        /// <summary>The goals to point at this frame (world positions and colours) and the camera that shows the floor.</summary>
        public void Set(Camera camera, List<(Vector3 world, Color color)> goals)
        {
            cam = camera;
            targets.Clear();
            if (goals != null) targets.AddRange(goals);
        }

        public void Clear() => targets.Clear();

        private void LateUpdate()
        {
            int shown = 0;
            var rect = area.rect;
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 4f);
            if (cam != null)
                foreach (var (world, color) in targets)
                {
                    if (shown >= Max) break;
                    var vp = cam.WorldToViewportPoint(world);
                    bool behind = vp.z < 0f;
                    if (!behind && vp.x > 0.06f && vp.x < 0.94f && vp.y > 0.14f && vp.y < 0.84f) continue; // in sight
                    var dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                    if (behind) dir = -dir;
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
                    // Where the ray from the centre leaves the screen, kept a little inside the edge.
                    float hw = rect.width * 0.5f - EdgeMargin, hh = rect.height * 0.5f - EdgeMargin - 120f;
                    var scaled = new Vector2(dir.x * rect.width, dir.y * rect.height);
                    float k = Mathf.Min(hw / Mathf.Max(0.001f, Mathf.Abs(scaled.x)), hh / Mathf.Max(0.001f, Mathf.Abs(scaled.y)));
                    var pos = scaled * k;
                    var (root, body, tip) = arrows[shown++];
                    root.gameObject.SetActive(true);
                    root.anchoredPosition = pos + rect.size * 0.5f;
                    root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(scaled.y, scaled.x) * Mathf.Rad2Deg - 90f);
                    body.color = new Color(0.08f, 0.07f, 0.2f, 0.6f * pulse);
                    foreach (var img in root.GetComponentsInChildren<Image>())
                        if (img != body) img.color = new Color(color.r, color.g, color.b, pulse);
                    root.localScale = Vector3.one * (0.9f + 0.1f * pulse);
                }
            for (int i = shown; i < arrows.Count; i++)
                if (arrows[i].root.gameObject.activeSelf) arrows[i].root.gameObject.SetActive(false);
        }
    }
}
