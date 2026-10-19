using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrickMuseum.UI
{
    /// <summary>
    /// The tutorial overlay: the screen dims, a pulsing ring shows the button the tip is about, and a card says what it is
    /// for, with "Anladım". It sits above every other screen and works in portrait and in landscape.
    /// </summary>
    public sealed class CoachUi
    {
        public Canvas canvas;
        public RectTransform ring, card;
        public TextMeshProUGUI title, body, step;
        public Button ok;
        private RectTransform area;

        public void Build(Transform parent)
        {
            canvas = Ui.MakeCanvas(parent, 40);
            area = (RectTransform)canvas.transform;
            var dim = Ui.Plain(canvas.transform, "Dim", new Color(0.02f, 0.03f, 0.08f, 0.62f));
            dim.rectTransform.Fill();
            var r = Ui.Plain(canvas.transform, "Ring", Ui.Yellow, false);
            r.sprite = Icons.Halo;
            ring = r.rectTransform;
            ring.anchorMin = ring.anchorMax = ring.pivot = new Vector2(0.5f, 0.5f);
            var root = Ui.SafeRoot(canvas);
            var c = Ui.Panel(root, "Card", Color.white);
            card = c.rectTransform;
            var badge = Ui.Panel(card, "Badge", Ui.Yellow, false);
            badge.rectTransform.Place(new Vector2(0, 1), new Vector2(96, 96), new Vector2(28, -28));
            var bulb = Ui.Plain(badge.transform, "Bulb", Ui.Ink, false);
            bulb.sprite = Icons.Bulb;
            bulb.rectTransform.Fill(18);
            title = Ui.Text(card, "", 44, Ui.Ink, false, true);
            title.alignment = TextAlignmentOptions.Left;
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(700, 80), new Vector2(144, -36));
            step = Ui.Text(card, "", 26, new Color(0.5f, 0.52f, 0.58f), true);
            step.alignment = TextAlignmentOptions.Right;
            step.rectTransform.Place(new Vector2(1, 1), new Vector2(150, 50), new Vector2(-28, -40));
            body = Ui.Text(card, "", 34, new Color(0.25f, 0.27f, 0.33f), false);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.rectTransform.Place(new Vector2(0, 1), new Vector2(880, 200), new Vector2(40, -132));
            ok = Ui.Button(card, "Anladım", Ui.Yellow, Ui.Ink, 38, () => { });
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(420, 100), new Vector2(0, 28));
            canvas.gameObject.SetActive(false);
        }

        /// <summary>Shows a tip, the ring round the target (none: no ring), the card away from the target.</summary>
        public void Show(string t, string text, string counter, RectTransform target)
        {
            title.text = t;
            body.text = text;
            step.text = counter;
            canvas.gameObject.SetActive(true);
            bool top = false;
            ring.gameObject.SetActive(target != null && target.gameObject.activeInHierarchy);
            if (ring.gameObject.activeSelf)
            {
                Canvas.ForceUpdateCanvases();
                var screen = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
                float k = target.lossyScale.x / Mathf.Max(0.0001f, area.lossyScale.x);
                ring.anchoredPosition = local;
                ring.sizeDelta = new Vector2(target.rect.width * k + 70f, target.rect.height * k + 70f);   // an oval round the button
                top = screen.y < Screen.height * 0.45f;    // the target is low: the card goes up
            }
            float w = Mathf.Min(960f, ((RectTransform)card.parent).rect.width - 40f);
            card.Place(new Vector2(0.5f, top ? 1f : 0f), new Vector2(w, 470f), new Vector2(0, top ? -150f : 150f));
        }

        public void Hide() => canvas.gameObject.SetActive(false);

        public bool Shown => canvas.gameObject.activeSelf;

        /// <summary>The ring breathes.</summary>
        public void Tick(float time)
        {
            if (!Shown || !ring.gameObject.activeSelf) return;
            ring.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(time * 5f));
        }
    }
}
