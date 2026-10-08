using SquashBot.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SquashBot.Journey
{
    /// <summary>
    /// The left thumb's movement stick. It rests at the bottom left; a finger landing anywhere in its (large, left)
    /// area brings the base under the finger. It follows only the finger that started it (its pointer id), so other
    /// fingers on the look area or the buttons never disturb it. Pushing the knob far past the top offers the sprint
    /// lock (a padlock lights up): let go there and the robot keeps running; touching the stick again ends it.
    /// </summary>
    public class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        private JourneyInput input;
        private JourneyTuning tuning;
        private RectTransform area, baseRect, knob, lockIcon;
        private Image lockFill;
        private Vector2 home;
        private int pointer = int.MinValue;
        private bool offerLock;

        public static TouchStick Create(Transform parent, JourneyInput input, JourneyTuning tuning)
        {
            // The catching area: the left 42% of the screen below the top bar.
            var area = UiFactory.Rect("MoveArea", parent, new Vector2(0f, 0f), new Vector2(0.42f, 0.8f));
            var img = area.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            var s = area.gameObject.AddComponent<TouchStick>();
            s.input = input;
            s.tuning = tuning;
            s.area = area;
            float r = tuning.joystickRadius;
            s.baseRect = UiFactory.Box("Base", area, Vector2.zero, Vector2.zero, new Vector2(r * 2.3f, r * 2.3f));
            s.baseRect.pivot = new Vector2(0.5f, 0.5f);
            s.home = new Vector2(250f, 250f);
            s.baseRect.anchoredPosition = s.home;
            UiFactory.Fill(s.baseRect, new Color(1f, 1f, 1f, 0.14f), UiSprites.Circle, 0.3f).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", s.baseRect), new Color(1f, 1f, 1f, 0.45f), UiSprites.Ring, 0.2f).raycastTarget = false;
            s.knob = UiFactory.Box("Knob", s.baseRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(r * 0.95f, r * 0.95f));
            s.knob.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(s.knob, new Color(1f, 1f, 1f, 0.75f), UiSprites.Circle).raycastTarget = false;
            // The sprint padlock above the base.
            s.lockIcon = UiFactory.Box("SprintLock", s.baseRect, new Vector2(0.5f, 0.5f), new Vector2(0f, r * tuning.sprintLockReach + 40f), new Vector2(86f, 86f));
            s.lockIcon.pivot = new Vector2(0.5f, 0.5f);
            s.lockFill = UiFactory.Fill(s.lockIcon, new Color(1f, 1f, 1f, 0.25f), UiSprites.Circle);
            s.lockFill.raycastTarget = false;
            var shackle = UiFactory.Box("Shackle", s.lockIcon, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(34f, 30f));
            shackle.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(shackle, Color.white, UiSprites.Ring, 0.5f).raycastTarget = false;
            var body = UiFactory.Box("Body", s.lockIcon, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(40f, 28f));
            body.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(body, Color.white, UiSprites.Rounded, 6f).raycastTarget = false;
            s.lockIcon.gameObject.SetActive(false);
            return s;
        }

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false; // no dead travel before it moves

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId;
            input.Sprint = false; // touching the stick again ends the sprint lock
            var local = Local(e.position);
            // Inside the base: use it where it is; elsewhere in the area: it comes to the finger.
            if ((local - baseRect.anchoredPosition).magnitude > tuning.joystickRadius * 1.1f) baseRect.anchoredPosition = local;
            Track(local);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            Track(Local(e.position));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            pointer = int.MinValue;
            if (offerLock) input.Sprint = true;
            offerLock = false;
            input.SetTouchMove(Vector2.zero);
            baseRect.anchoredPosition = home;
            knob.anchoredPosition = input.Sprint ? new Vector2(0f, tuning.joystickRadius) : Vector2.zero;
        }

        private void Track(Vector2 local)
        {
            float r = tuning.joystickRadius;
            var d = local - baseRect.anchoredPosition;
            var clamped = Vector2.ClampMagnitude(d, r);
            knob.anchoredPosition = clamped;
            input.SetTouchMove(clamped / r);
            // Far above the base, mostly straight up: the sprint lock is offered.
            offerLock = d.y > r * tuning.sprintLockReach && Mathf.Abs(d.x) < d.y * 0.6f;
        }

        private Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            // Area-local (pivot centre) to anchored-from-bottom-left.
            return local + area.rect.size * area.pivot;
        }

        private void Update()
        {
            bool dragging = pointer != int.MinValue;
            lockIcon.gameObject.SetActive(dragging || input.Sprint);
            lockFill.color = offerLock || input.Sprint ? new Color(0.45f, 1f, 0.55f, 0.85f) : new Color(1f, 1f, 1f, 0.25f);
            if (!dragging && input.Sprint) knob.anchoredPosition = new Vector2(0f, tuning.joystickRadius);
        }

        private void OnDisable()
        {
            pointer = int.MinValue;
            offerLock = false;
            if (input != null) input.SetTouchMove(Vector2.zero);
        }
    }
}
