using SquashBot.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SquashBot.Journey
{
    /// <summary>
    /// The right thumb's camera area: a big invisible panel over the right side of the screen, under the buttons. A
    /// finger dragged on it turns the camera by its frame-to-frame movement (never by where it is), scaled by the
    /// screen's height so every device turns alike. Only the finger that started on it counts; a finger that starts
    /// on a button belongs to the button (the buttons sit above this panel), so the camera never turns by accident.
    /// </summary>
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        private JourneyInput input;
        private JourneyTuning tuning;
        private int pointer = int.MinValue;

        public static TouchLookArea Create(Transform parent, JourneyInput input, JourneyTuning tuning)
        {
            var area = UiFactory.Rect("LookArea", parent, new Vector2(0.42f, 0f), new Vector2(1f, 0.85f));
            var img = area.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            var l = area.gameObject.AddComponent<TouchLookArea>();
            l.input = input;
            l.tuning = tuning;
            return l;
        }

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer == int.MinValue) pointer = e.pointerId;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            var deg = e.delta / Mathf.Max(1f, Screen.height) * new Vector2(tuning.lookSensitivityX, tuning.lookSensitivityY);
            input.AddTouchLook(deg);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointer) pointer = int.MinValue;
        }

        private void OnDisable() => pointer = int.MinValue;
    }
}
