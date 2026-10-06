using SquashBot.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// A floating stick: wherever a finger lands on the screen, the pad appears under it and the knob follows the drag;
    /// <see cref="Value"/> points the way (length 0-1). Lifting the finger hides it. The city controller drives it from
    /// its own touch tracking (<see cref="Drive"/>); the arrow keys / WASD steer it too, for testing on a computer.
    /// </summary>
    public class Joystick : MonoBehaviour
    {
        private RectTransform area, pad, knob;
        private float radius;
        private Vector2 touch;

        /// <summary>Test hooks steer the stick from code.</summary>
        public static Vector2? Override;

        public Vector2 Value
        {
            get
            {
                if (Override.HasValue) return Override.Value;
                var keys = Keys();
                return keys != Vector2.zero ? keys : touch;
            }
        }

        /// <summary>The stick fills <paramref name="parent"/>; <paramref name="size"/> is the pad's diameter.</summary>
        public static Joystick Create(Transform parent, float size)
        {
            var area = UiFactory.Stretch("Joystick", parent);
            var stick = area.gameObject.AddComponent<Joystick>();
            stick.area = area;
            stick.pad = UiFactory.Box("Pad", area, new Vector2(0f, 0f), Vector2.zero, new Vector2(size, size));
            stick.pad.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(stick.pad, new Color(0.14f, 0.13f, 0.3f, 0.45f), UiSprites.Circle).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", stick.pad), new Color(0.62f, 0.92f, 1f, 0.55f), UiSprites.Ring, 0.4f).raycastTarget = false;
            stick.knob = UiFactory.Box("Knob", stick.pad, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.42f, size * 0.42f));
            stick.knob.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(stick.knob, Palette.UiCyan, UiSprites.Circle).raycastTarget = false;
            stick.radius = size * 0.38f;
            stick.pad.gameObject.SetActive(false);
            return stick;
        }

        /// <summary>
        /// A finger went down at <paramref name="origin"/> and is now at <paramref name="current"/> (screen pixels);
        /// null origin = no finger, the stick hides and centres.
        /// </summary>
        public void Drive(Vector2? origin, Vector2 current)
        {
            if (!origin.HasValue)
            {
                Release();
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, origin.Value, null, out var o);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, current, null, out var c);
            if (!pad.gameObject.activeSelf) pad.gameObject.SetActive(true);
            // Local points are relative to the area's pivot (its centre); the pad is anchored bottom-left.
            pad.anchoredPosition = o + area.rect.size * 0.5f;
            touch = Vector2.ClampMagnitude((c - o) / radius, 1f);
            knob.anchoredPosition = touch * radius;
        }

        public void Release()
        {
            touch = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (pad != null && pad.gameObject.activeSelf) pad.gameObject.SetActive(false);
        }

        private void OnDisable() => Release();

        private static Vector2 Keys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return Vector2.zero;
            var v = Vector2.zero;
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) v.y += 1f;
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed) v.y -= 1f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) v.x -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) v.x += 1f;
            return Vector2.ClampMagnitude(v, 1f);
#else
            return Vector2.zero;
#endif
        }
    }
}
