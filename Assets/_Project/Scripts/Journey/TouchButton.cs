using System;
using SquashBot.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SquashBot.Journey
{
    /// <summary>
    /// A round on-screen button that acts the moment a finger lands on it (not on release), keeps its own finger
    /// and shows its cooldown as a dark sweep. A locked button shows a padlock and the leg it opens at.
    /// </summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Action down, up;
        private int pointer = int.MinValue;
        private Image face, sweep;
        private RectTransform rect;
        private TextMeshProUGUI lockText;
        private RectTransform icon;
        private Color colour;
        private float pressedT;
        public bool Locked { get; private set; }

        public static TouchButton Create(Transform parent, string name, Vector2 fromBottomRight, float size, Color colour, Action down, Action up = null)
        {
            var r = UiFactory.Box(name, parent, new Vector2(1f, 0f), fromBottomRight, new Vector2(size, size));
            r.pivot = new Vector2(0.5f, 0.5f);
            var b = r.gameObject.AddComponent<TouchButton>();
            b.rect = r;
            b.down = down;
            b.up = up;
            b.colour = colour;
            b.face = UiFactory.Fill(r, colour, UiSprites.Circle);
            UiFactory.Fill(UiFactory.Stretch("Rim", r), new Color(1f, 1f, 1f, 0.55f), UiSprites.Ring, 0.4f).raycastTarget = false;
            b.sweep = UiFactory.Fill(UiFactory.Stretch("Cooldown", r), new Color(0f, 0f, 0f, 0.55f), UiSprites.Circle);
            b.sweep.raycastTarget = false;
            b.sweep.type = Image.Type.Filled;
            b.sweep.fillMethod = Image.FillMethod.Radial360;
            b.sweep.fillOrigin = (int)Image.Origin360.Top;
            b.sweep.fillClockwise = false;
            b.sweep.fillAmount = 0f;
            return b;
        }

        /// <summary>The icon area (a child rect to draw the button's symbol into).</summary>
        public RectTransform Icon(float scale = 0.6f)
        {
            var i = UiFactory.Box("Icon", rect, new Vector2(0.5f, 0.5f), Vector2.zero, rect.sizeDelta * scale);
            i.pivot = new Vector2(0.5f, 0.5f);
            i.SetSiblingIndex(2); // under the cooldown sweep
            icon = i;
            return i;
        }

        /// <summary>0 = ready, 1 = just used.</summary>
        public void SetCooldown(float left01) => sweep.fillAmount = Mathf.Clamp01(left01);

        public void SetLocked(bool locked, string label)
        {
            Locked = locked;
            if (locked && lockText == null)
            {
                lockText = UiFactory.TextBox("Lock", rect, new Vector2(0.5f, 0.5f), Vector2.zero, rect.sizeDelta * 0.8f, "", 24f, Color.white);
                lockText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                lockText.raycastTarget = false;
            }
            if (lockText != null)
            {
                lockText.gameObject.SetActive(locked);
                lockText.text = label;
            }
            face.color = locked ? new Color(0.3f, 0.3f, 0.32f, 0.6f) : colour;
            if (icon != null) icon.gameObject.SetActive(!locked);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId;
            pressedT = 1f;
            if (!Locked) down?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            pointer = int.MinValue;
            up?.Invoke();
        }

        private void Update()
        {
            // A quick press-in squash for feedback.
            pressedT = Mathf.MoveTowards(pressedT, pointer != int.MinValue ? 0.6f : 0f, Time.unscaledDeltaTime * 6f);
            rect.localScale = Vector3.one * (1f - pressedT * 0.12f);
        }

        private void OnDisable()
        {
            if (pointer != int.MinValue) up?.Invoke();
            pointer = int.MinValue;
        }
    }
}
