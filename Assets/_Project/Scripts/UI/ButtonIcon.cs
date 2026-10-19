using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BrickMuseum.UI
{
    /// <summary>
    /// A button's icon next to its label: on the left of wide buttons, above the label on square or round ones. It lays
    /// itself out whenever the button's size changes, since buttons are sized after they are made.
    /// </summary>
    public sealed class ButtonIcon : UIBehaviour
    {
        public Image icon;
        public TextMeshProUGUI label;
        public float margin = 12f;

        /// <summary>Adds an icon to a button with this label (none when <paramref name="sprite"/> is null).</summary>
        public static ButtonIcon Add(GameObject button, TextMeshProUGUI label, Sprite sprite, float margin = 12f)
        {
            var bi = button.AddComponent<ButtonIcon>();
            bi.label = label;
            bi.margin = margin;
            bi.icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            bi.icon.transform.SetParent(button.transform, false);
            bi.icon.raycastTarget = false;
            bi.icon.preserveAspect = true;
            bi.icon.color = label.color;
            bi.Set(sprite);
            return bi;
        }

        public void Set(Sprite sprite)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
            Layout();
        }

        protected override void OnEnable() { base.OnEnable(); Layout(); }
        protected override void OnRectTransformDimensionsChange() => Layout();

        private void Layout()
        {
            if (icon == null || label == null) return;
            var r = ((RectTransform)transform).rect;
            float w = r.width, h = r.height;
            var lt = label.rectTransform;
            if (!icon.gameObject.activeSelf || w <= 1f || h <= 1f) { lt.Fill(margin); return; }
            if (w / h < 1.6f)
            {
                // icon on top, label under it
                float s = Mathf.Min(h * 0.44f, w * 0.5f);
                icon.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(s, s), new Vector2(0, -h * 0.12f));
                lt.anchorMin = new Vector2(0, 0); lt.anchorMax = new Vector2(1, 0); lt.pivot = new Vector2(0.5f, 0);
                lt.sizeDelta = new Vector2(-margin, h * 0.36f);
                lt.anchoredPosition = new Vector2(0, h * 0.08f);
            }
            else
            {
                // icon on the left, label in the rest
                float s = h * 0.56f, pad = Mathf.Max(margin, h * 0.2f);
                icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(s, s), new Vector2(pad, 0));
                lt.anchorMin = Vector2.zero; lt.anchorMax = Vector2.one;
                lt.offsetMin = new Vector2(pad + s + 6f, margin * 0.6f);
                lt.offsetMax = new Vector2(-margin, -margin * 0.6f);
            }
        }
    }
}
