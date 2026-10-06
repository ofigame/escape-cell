using SquashBot.Visual;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// A virtual stick for the lower left corner: press anywhere on its pad, drag, and <see cref="Value"/> points the way
    /// (length 0-1). Letting go snaps it back. The arrow keys / WASD steer it too, for testing on a computer.
    /// </summary>
    public class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform pad, knob;
        private float radius;
        private Vector2 touch;
        private bool held;

        public Vector2 Value
        {
            get
            {
                var keys = Keys();
                return keys != Vector2.zero ? keys : held ? touch : Vector2.zero;
            }
        }

        public static Joystick Create(Transform parent, Vector2 anchor, Vector2 position, float size)
        {
            var area = UiFactory.Box("Joystick", parent, anchor, position, new Vector2(size, size));
            UiFactory.Fill(area, new Color(0f, 0f, 0f, 0.001f));
            var stick = area.gameObject.AddComponent<Joystick>();
            stick.pad = UiFactory.Box("Pad", area, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.8f, size * 0.8f));
            stick.pad.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(stick.pad, new Color(0.14f, 0.13f, 0.3f, 0.55f), UiSprites.Circle).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", stick.pad), new Color(0.62f, 0.92f, 1f, 0.5f), UiSprites.Ring, 0.4f).raycastTarget = false;
            stick.knob = UiFactory.Box("Knob", stick.pad, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.36f, size * 0.36f));
            stick.knob.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(stick.knob, Palette.UiCyan, UiSprites.Circle).raycastTarget = false;
            stick.radius = size * 0.3f;
            return stick;
        }

        public void OnPointerDown(PointerEventData e)
        {
            held = true;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(pad, e.position, e.pressEventCamera, out var local);
            touch = Vector2.ClampMagnitude(local / radius, 1f);
            knob.anchoredPosition = touch * radius;
        }

        public void OnPointerUp(PointerEventData e) => Release();

        public void Release()
        {
            held = false;
            touch = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
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
