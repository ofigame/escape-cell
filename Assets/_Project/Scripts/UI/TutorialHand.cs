using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The tutorial's pointing hand (drawn in code: a fingertip disc with a soft ring): it shows each gesture where it is
    /// made — a tap pulsing, a drag sliding sideways, a swipe up or down, a press held on a button, a tap on a button.
    /// </summary>
    public class TutorialHand : MonoBehaviour
    {
        public enum Gesture { None, Tap, Drag, SwipeUp, SwipeDown, Hold, Press, Circle }

        private RectTransform rect, finger, ring;
        private Image ringImage;
        private Gesture gesture;
        private Vector2 at;
        private float t;

        public static TutorialHand Create(RectTransform parent)
        {
            var r = UiFactory.Box("TutorialHand", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
            r.pivot = new Vector2(0.5f, 0.5f);
            var h = r.gameObject.AddComponent<TutorialHand>();
            h.rect = r;
            h.ring = UiFactory.Box("Ring", r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
            h.ring.pivot = new Vector2(0.5f, 0.5f);
            h.ringImage = h.ring.gameObject.AddComponent<Image>();
            h.ringImage.sprite = DuelIcons.Ring;
            h.ringImage.raycastTarget = false;
            h.finger = UiFactory.Box("Finger", r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
            h.finger.pivot = new Vector2(0.5f, 0.5f);
            var f = UiFactory.Fill(h.finger, new Color(1f, 1f, 1f, 0.95f), UiSprites.Circle);
            f.raycastTarget = false;
            var shadow = r.gameObject.AddComponent<CanvasGroup>();
            shadow.blocksRaycasts = false;
            shadow.interactable = false;
            r.gameObject.SetActive(false);
            return h;
        }

        /// <summary>Shows <paramref name="g"/> at <paramref name="where"/> (the parent's local point; None hides it).</summary>
        public void Show(Gesture g, Vector2 where)
        {
            gesture = g;
            at = where;
            t = 0f;
            gameObject.SetActive(g != Gesture.None);
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = (t % 1.4f) / 1.4f;
            var offset = Vector2.zero;
            float press = 0f;
            switch (gesture)
            {
                case Gesture.Tap: case Gesture.Press: press = k < 0.3f ? k / 0.3f : k < 0.5f ? 1f : 0f; break;
                case Gesture.Hold: press = Mathf.Clamp01(k * 4f); break;
                case Gesture.Drag: offset = new Vector2(Mathf.Sin(k * Mathf.PI * 2f) * 180f, 0f); press = 1f; break;
                case Gesture.SwipeUp: offset = new Vector2(0f, Mathf.Lerp(-120f, 160f, Mathf.SmoothStep(0f, 1f, k))); press = k < 0.8f ? 1f : 0f; break;
                case Gesture.SwipeDown: offset = new Vector2(0f, Mathf.Lerp(120f, -160f, Mathf.SmoothStep(0f, 1f, k))); press = k < 0.8f ? 1f : 0f; break;
                case Gesture.Circle: offset = new Vector2(Mathf.Cos(k * Mathf.PI * 2f), Mathf.Sin(k * Mathf.PI * 2f)) * 150f; press = 1f; break; // (a circle drawn: the floors' spin)
            }
            rect.anchoredPosition = at + offset;
            finger.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, press);
            ring.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.4f, press);
            ringImage.color = new Color(1f, 0.9f, 0.4f, 0.8f * (1f - Mathf.Abs(press - 0.5f)));
        }
    }
}
