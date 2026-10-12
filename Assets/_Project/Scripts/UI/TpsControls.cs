using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The battle arenas' landscape TPS controls: the left of the screen moves foi (a stick that comes to the thumb),
    /// the right turns the camera (a drag anywhere there), with the strike, spin and jump buttons at the bottom right.
    /// Each part keeps only the finger that started on it, so moving, looking and striking work together. Its canvas
    /// sits under the HUD's, so the HUD's own buttons (pause, skills) still win their touches.
    /// </summary>
    public class TpsControls : MonoBehaviour
    {
        public const float StickRadius = 170f;

        public Vector2 Move { get; private set; }
        public event Action Strike, Spin, Jump;

        private Vector2 look;
        private Canvas canvas;

        /// <summary>The camera turn gathered since the last call (screen heights dragged, x right, y up).</summary>
        public Vector2 TakeLook()
        {
            var l = look;
            look = Vector2.zero;
            return l;
        }

        public static TpsControls Create()
        {
            var canvas = UiFactory.CreateCanvas("TpsControls", out _);
            canvas.sortingOrder = 9;
            var c = canvas.gameObject.AddComponent<TpsControls>();
            c.canvas = canvas;
            var root = canvas.transform;
            // The look area first (under everything else), the stick's area over its left part, the buttons on top.
            Area<LookArea>("LookArea", root, new Vector2(0.4f, 0f), new Vector2(1f, 0.82f)).owner = c;
            var stick = Area<Stick>("MoveArea", root, new Vector2(0f, 0f), new Vector2(0.4f, 0.82f));
            stick.Build(c);
            Button(root, "Strike", new Vector2(-230f, 250f), 250f, new Color(1f, 0.55f, 0.25f, 0.85f), 0, () => c.Strike?.Invoke());
            Button(root, "Spin", new Vector2(-470f, 170f), 170f, new Color(0.45f, 0.75f, 1f, 0.8f), 1, () => c.Spin?.Invoke());
            Button(root, "Jump", new Vector2(-260f, 520f), 160f, new Color(0.55f, 1f, 0.6f, 0.8f), 2, () => c.Jump?.Invoke());
            c.Show(false);
            return c;
        }

        public void Show(bool on)
        {
            canvas.gameObject.SetActive(on);
            if (!on) { Move = Vector2.zero; look = Vector2.zero; }
        }

        private static T Area<T>(string name, Transform parent, Vector2 min, Vector2 max) where T : Component
        {
            var area = UiFactory.Rect(name, parent, min, max);
            area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            return area.gameObject.AddComponent<T>();
        }

        private static void Button(Transform parent, string name, Vector2 fromBottomRight, float size, Color colour, int symbol, Action down)
        {
            var r = UiFactory.Box(name, parent, new Vector2(1f, 0f), fromBottomRight, new Vector2(size, size));
            r.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(r, colour, UiSprites.Circle);
            UiFactory.Fill(UiFactory.Stretch("Rim", r), new Color(1f, 1f, 1f, 0.6f), UiSprites.Ring, 0.4f).raycastTarget = false;
            // Drawn symbols (no font glyphs needed): a sword, a ring for the spin, a chevron for the jump.
            if (symbol == 0)
            {
                Bar(r, new Vector2(0f, 8f), new Vector2(size * 0.14f, size * 0.56f), 45f);
                Bar(r, new Vector2(-size * 0.12f, -size * 0.1f), new Vector2(size * 0.34f, size * 0.09f), 45f);
            }
            else if (symbol == 1)
            {
                var ring = UiFactory.Box("Ring", r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.5f, size * 0.5f));
                ring.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(ring, Color.white, UiSprites.Ring, 0.25f).raycastTarget = false;
            }
            else
            {
                Bar(r, new Vector2(-size * 0.1f, 0f), new Vector2(size * 0.1f, size * 0.36f), -40f);
                Bar(r, new Vector2(size * 0.1f, 0f), new Vector2(size * 0.1f, size * 0.36f), 40f);
            }
            r.gameObject.AddComponent<PressButton>().down = down;
        }

        private static void Bar(RectTransform parent, Vector2 at, Vector2 size, float angle)
        {
            var b = UiFactory.Box("Bar", parent, new Vector2(0.5f, 0.5f), at, size);
            b.pivot = new Vector2(0.5f, 0.5f);
            b.localRotation = Quaternion.Euler(0f, 0f, angle);
            UiFactory.Fill(b, Color.white, UiSprites.Rounded, 4f).raycastTarget = false;
        }

        private void OnDisable() { Move = Vector2.zero; look = Vector2.zero; }

        private class LookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
        {
            public TpsControls owner;
            private int pointer = int.MinValue;
            public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
            public void OnPointerDown(PointerEventData e) { if (pointer == int.MinValue) pointer = e.pointerId; }
            public void OnDrag(PointerEventData e)
            {
                if (e.pointerId != pointer) return;
                owner.look += e.delta / Mathf.Max(1f, Screen.height);
            }
            public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) pointer = int.MinValue; }
            private void OnDisable() => pointer = int.MinValue;
        }

        private class Stick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
        {
            private TpsControls owner;
            private RectTransform area, baseRect, knob;
            private Vector2 home = new Vector2(300f, 300f);
            private int pointer = int.MinValue;

            public void Build(TpsControls c)
            {
                owner = c;
                area = (RectTransform)transform;
                float r = StickRadius;
                baseRect = UiFactory.Box("Base", area, Vector2.zero, home, new Vector2(r * 2.3f, r * 2.3f));
                baseRect.pivot = new Vector2(0.5f, 0.5f);
                baseRect.anchoredPosition = home;
                UiFactory.Fill(baseRect, new Color(1f, 1f, 1f, 0.14f), UiSprites.Circle).raycastTarget = false;
                UiFactory.Fill(UiFactory.Stretch("Rim", baseRect), new Color(1f, 1f, 1f, 0.45f), UiSprites.Ring, 0.2f).raycastTarget = false;
                knob = UiFactory.Box("Knob", baseRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(r * 0.95f, r * 0.95f));
                knob.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(knob, new Color(1f, 1f, 1f, 0.75f), UiSprites.Circle).raycastTarget = false;
            }

            public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

            public void OnPointerDown(PointerEventData e)
            {
                if (pointer != int.MinValue) return;
                pointer = e.pointerId;
                var local = Local(e.position);
                if ((local - baseRect.anchoredPosition).magnitude > StickRadius * 1.1f) baseRect.anchoredPosition = local; // the stick comes to the thumb
                Track(local);
            }

            public void OnDrag(PointerEventData e) { if (e.pointerId == pointer) Track(Local(e.position)); }

            public void OnPointerUp(PointerEventData e)
            {
                if (e.pointerId != pointer) return;
                Release();
            }

            private void Release()
            {
                pointer = int.MinValue;
                if (owner != null) owner.Move = Vector2.zero;
                if (baseRect != null) { baseRect.anchoredPosition = home; knob.anchoredPosition = Vector2.zero; }
            }

            private void Track(Vector2 local)
            {
                var clamped = Vector2.ClampMagnitude(local - baseRect.anchoredPosition, StickRadius);
                knob.anchoredPosition = clamped;
                owner.Move = clamped / StickRadius;
            }

            private Vector2 Local(Vector2 screen)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
                return local + area.rect.size * area.pivot;
            }

            private void OnDisable() => Release();
        }

        private class PressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public Action down;
            private float pressed;
            private bool held;
            public void OnPointerDown(PointerEventData e) { held = true; pressed = 1f; down?.Invoke(); }
            public void OnPointerUp(PointerEventData e) => held = false;
            private void Update()
            {
                pressed = Mathf.MoveTowards(pressed, held ? 0.6f : 0f, Time.unscaledDeltaTime * 6f);
                transform.localScale = Vector3.one * (1f - pressed * 0.12f);
            }
            private void OnDisable() { held = false; pressed = 0f; transform.localScale = Vector3.one; }
        }
    }
}
