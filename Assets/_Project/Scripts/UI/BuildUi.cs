using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrickMuseum.UI
{
    /// <summary>
    /// Free building (portrait): the lot in the middle (drag to turn round it, pinch to zoom, tap to put a piece),
    /// tabs and a strip of pieces with how many are left at the bottom, tools on the right (put / take away, turn,
    /// colour, storeys), back and undo at the top.
    /// </summary>
    public sealed class BuildUi
    {
        public Canvas canvas;
        public PointerArea touch;
        public Button backBtn, undoBtn, modeBtn, turnBtn, colourBtn, storeyBtn, switchBtn;
        public Image colourSwatch;
        public TextMeshProUGUI title, stockText, message;
        public GameObject colours;
        public RectTransform tabs, strip;
        private readonly List<(Image bg, string key, TextMeshProUGUI count)> cards = new List<(Image, string, TextMeshProUGUI)>();
        private readonly List<(Image bg, string group)> tabButtons = new List<(Image, string)>();
        private float messageUntil;

        public void Build(Transform parent)
        {
            canvas = Ui.MakeCanvas(parent, 14);
            var root = canvas.transform;

            var t = Ui.Plain(root, "Touch", new Color(0, 0, 0, 0));
            t.rectTransform.Fill();
            touch = t.gameObject.AddComponent<PointerArea>();

            backBtn = Ui.Button(root, "Şehre dön", Color.white, Ui.Ink, 36, () => { });
            ((RectTransform)backBtn.transform).Place(new Vector2(0, 1), new Vector2(270, 96), new Vector2(24, -24));
            undoBtn = Ui.Button(root, "Geri al", Color.white, Ui.Ink, 36, () => { });
            ((RectTransform)undoBtn.transform).Place(new Vector2(1, 1), new Vector2(230, 96), new Vector2(-24, -24));

            var head = Ui.Panel(root, "Head", new Color(1, 1, 1, 0.9f), false);
            head.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(460, 130), new Vector2(0, -20));
            title = Ui.Text(head.transform, "Evim", 44, Ui.Ink, true, true);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(440, 64), new Vector2(0, -6));
            stockText = Ui.Text(head.transform, "", 28, new Color(0.35f, 0.38f, 0.45f), true);
            stockText.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(440, 50), new Vector2(0, 8));

            switchBtn = Ui.Button(root, "Arabama geç", new Color(0.8f, 0.9f, 1f), Ui.Ink, 30, () => { });
            ((RectTransform)switchBtn.transform).Place(new Vector2(0.5f, 1), new Vector2(300, 76), new Vector2(0, -160));

            // tools on the right
            modeBtn = Tool(root, "Koy", 0);
            turnBtn = Tool(root, "Döndür", 1);
            colourBtn = Tool(root, "Renk", 2);
            colourSwatch = Ui.Panel(colourBtn.transform, "Swatch", Color.red, false);
            colourSwatch.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(60, 34), new Vector2(0, -10));
            colourBtn.GetComponentInChildren<TextMeshProUGUI>().rectTransform.offsetMax = new Vector2(-12, -40);
            storeyBtn = Tool(root, "Tüm katlar", 3);

            var msg = Ui.Panel(root, "Message", new Color(0.12f, 0.13f, 0.18f, 0.85f), false);
            msg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(700, 90), new Vector2(0, 470));
            message = Ui.Text(msg.transform, "", 34, Color.white, true);
            message.rectTransform.Fill(10);
            msg.gameObject.SetActive(false);

            // the piece strip and its tabs
            var bar = Ui.Panel(root, "Strip", new Color(1, 1, 1, 0.94f));
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(1, 0); bar.rectTransform.pivot = new Vector2(0.5f, 0);
            bar.rectTransform.sizeDelta = new Vector2(-24, 270); bar.rectTransform.anchoredPosition = new Vector2(0, 14);
            var view = Ui.Rect(bar.transform, "View");
            view.Fill(10);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);
            var sr = view.gameObject.AddComponent<ScrollRect>();
            strip = Ui.Rect(view, "Content");
            strip.anchorMin = new Vector2(0, 0); strip.anchorMax = new Vector2(0, 1); strip.pivot = new Vector2(0, 0.5f);
            strip.offsetMin = strip.offsetMax = Vector2.zero;
            var h = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            strip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = strip; sr.vertical = false; sr.horizontal = true; sr.viewport = view;

            tabs = Ui.Rect(root, "Tabs");
            tabs.anchorMin = new Vector2(0, 0); tabs.anchorMax = new Vector2(1, 0); tabs.pivot = new Vector2(0.5f, 0);
            tabs.sizeDelta = new Vector2(-24, 84); tabs.anchoredPosition = new Vector2(0, 296);
            var th = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 8; th.childControlWidth = true; th.childControlHeight = true; th.childForceExpandWidth = true; th.childForceExpandHeight = true;

            // colour picker
            var dim = Ui.Plain(root, "Colours", new Color(0, 0, 0, 0.45f));
            dim.rectTransform.Fill();
            colours = dim.gameObject;
            colours.AddComponent<Button>().onClick.AddListener(() => colours.SetActive(false));
            colours.SetActive(false);

            canvas.gameObject.SetActive(false);
        }

        private static Button Tool(Transform root, string label, int i)
        {
            var b = Ui.Button(root, label, Color.white, Ui.Ink, 30, () => { });
            ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(170, 120), new Vector2(-20, 330 - i * 135));
            return b;
        }

        public void SetMode(bool removing)
        {
            modeBtn.SetLabel(removing ? "Sil" : "Koy");
            modeBtn.GetComponent<Image>().color = removing ? new Color(1f, 0.72f, 0.7f) : Ui.Yellow;
        }

        public void Say(string text, float seconds = 1.6f)
        {
            message.text = text;
            message.transform.parent.gameObject.SetActive(true);
            messageUntil = Time.unscaledTime + seconds;
        }

        public void Tick()
        {
            if (message.transform.parent.gameObject.activeSelf && Time.unscaledTime > messageUntil) message.transform.parent.gameObject.SetActive(false);
        }

        // ---------- tabs and cards ----------

        public void SetTabs(IEnumerable<string> groups, Action<string> onTab)
        {
            foreach (Transform c in tabs) UnityEngine.Object.Destroy(c.gameObject);
            tabButtons.Clear();
            foreach (var g in groups)
            {
                var group = g;
                var b = Ui.Button(tabs, g, Color.white, Ui.Ink, 30, () => onTab(group));
                tabButtons.Add((b.GetComponent<Image>(), g));
            }
        }

        public void ShowTab(string group)
        {
            foreach (var (bg, g) in tabButtons) bg.color = g == group ? Ui.Yellow : Color.white;
        }

        public void ClearCards() { foreach (Transform c in strip) UnityEngine.Object.Destroy(c.gameObject); cards.Clear(); }

        public void AddCard(string key, Texture pic, string name, Action onClick)
        {
            var card = Ui.Panel(strip, "Card " + name, new Color(0.95f, 0.96f, 1f));
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 196;
            var img = new GameObject("Pic", typeof(RectTransform)).AddComponent<RawImage>();
            img.transform.SetParent(card.transform, false);
            img.texture = pic;
            img.raycastTarget = false;
            img.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(140, 140), new Vector2(0, -4));
            var label = Ui.Text(card.transform, name, 24, Ui.Ink, true);
            label.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(186, 46), new Vector2(0, 44));
            var count = Ui.Text(card.transform, "", 28, new Color(0.2f, 0.45f, 0.25f), true);
            count.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(186, 40), new Vector2(0, 6));
            card.gameObject.AddComponent<Button>().onClick.AddListener(() => { Core.Sfx.Play("pick", 0.6f); onClick(); });
            cards.Add((card, key, count));
        }

        public void UpdateCards(string selectedKey, Func<string, int> countOf)
        {
            foreach (var (bg, key, count) in cards)
            {
                int n = countOf(key);
                count.text = n > 0 ? $"× {n}" : "yok";
                count.color = n > 0 ? new Color(0.2f, 0.45f, 0.25f) : new Color(0.75f, 0.3f, 0.25f);
                bg.color = key == selectedKey ? Ui.Yellow : n > 0 ? new Color(0.95f, 0.96f, 1f) : new Color(0.88f, 0.88f, 0.9f);
            }
        }

        public void BuildColours(IEnumerable<(string key, Color color)> list, Action<string> onPick)
        {
            var card = Ui.Panel(colours.transform, "Card", Color.white);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(900, 620), Vector2.zero);
            var grid = card.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150, 150); grid.spacing = new Vector2(20, 20);
            grid.padding = new RectOffset(40, 40, 40, 40);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
            foreach (var (key, color) in list)
            {
                var k = key;
                var sw = Ui.Panel(card.transform, "Colour " + key, color);
                sw.gameObject.AddComponent<Button>().onClick.AddListener(() => { onPick(k); colours.SetActive(false); });
            }
        }
    }
}
