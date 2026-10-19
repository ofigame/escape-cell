using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrickMuseum.UI
{
    /// <summary>
    /// Decorating the home (landscape): the room seen from above, a strip of furniture to add at the bottom, wall and
    /// floor colours at the top, turn / remove for the selected piece. Drag a piece to move it.
    /// </summary>
    public sealed class DecorUi
    {
        public Canvas canvas;
        public PointerArea touch;
        public Button doneBtn, wallBtn, floorBtn, turnBtn, removeBtn;
        public GameObject selectedBar;
        public TextMeshProUGUI selectedName, help;
        private RectTransform strip;

        public void Build(Transform parent)
        {
            canvas = Ui.MakeCanvas(parent, 13);
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            var root = canvas.transform;

            var t = Ui.Plain(root, "Touch", new Color(0, 0, 0, 0));
            t.rectTransform.Fill();
            touch = t.gameObject.AddComponent<PointerArea>();

            doneBtn = Ui.Button(root, "Bitti", Ui.Yellow, Ui.Ink, 40, () => { });
            ((RectTransform)doneBtn.transform).Place(new Vector2(0, 1), new Vector2(260, 100), new Vector2(30, -30));
            wallBtn = Ui.Button(root, "Duvar rengi", Color.white, Ui.Ink, 34, () => { });
            ((RectTransform)wallBtn.transform).Place(new Vector2(1, 1), new Vector2(300, 100), new Vector2(-350, -30));
            floorBtn = Ui.Button(root, "Zemin rengi", Color.white, Ui.Ink, 34, () => { });
            ((RectTransform)floorBtn.transform).Place(new Vector2(1, 1), new Vector2(300, 100), new Vector2(-30, -30));

            var hp = Ui.Panel(root, "Help", new Color(1, 1, 1, 0.85f), false);
            hp.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(760, 80), new Vector2(0, -150));
            help = Ui.Text(hp.transform, "Aşağıdan eşya ekle · eşyayı sürükleyip taşı", 30, Ui.Ink, true);
            help.rectTransform.Fill(10);

            // the selected piece: its name, turn and remove
            var sb = Ui.Panel(root, "Selected", new Color(1f, 0.95f, 0.7f));
            sb.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(900, 110), new Vector2(0, 270));
            selectedBar = sb.gameObject;
            selectedName = Ui.Text(sb.transform, "", 36, Ui.Ink, true);
            selectedName.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(360, 90), new Vector2(20, 0));
            turnBtn = Ui.Button(sb.transform, "Döndür", Color.white, Ui.Ink, 34, () => { });
            ((RectTransform)turnBtn.transform).Place(new Vector2(1, 0.5f), new Vector2(230, 86), new Vector2(-265, 0));
            removeBtn = Ui.Button(sb.transform, "Kaldır", new Color(1f, 0.8f, 0.78f), Ui.Ink, 34, () => { });
            ((RectTransform)removeBtn.transform).Place(new Vector2(1, 0.5f), new Vector2(230, 86), new Vector2(-15, 0));
            selectedBar.SetActive(false);

            // the furniture strip
            var bar = Ui.Panel(root, "Strip", new Color(1, 1, 1, 0.92f));
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(1, 0); bar.rectTransform.pivot = new Vector2(0.5f, 0);
            bar.rectTransform.sizeDelta = new Vector2(-40, 240); bar.rectTransform.anchoredPosition = new Vector2(0, 16);
            var view = Ui.Rect(bar.transform, "View");
            view.Fill(12);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);
            var sr = view.gameObject.AddComponent<ScrollRect>();
            strip = Ui.Rect(view, "Content");
            strip.anchorMin = new Vector2(0, 0); strip.anchorMax = new Vector2(0, 1); strip.pivot = new Vector2(0, 0.5f);
            strip.offsetMin = strip.offsetMax = Vector2.zero;
            var h = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            strip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = strip; sr.vertical = false; sr.horizontal = true; sr.viewport = view;

            canvas.gameObject.SetActive(false);
        }

        public void ClearCards() { foreach (Transform c in strip) UnityEngine.Object.Destroy(c.gameObject); }

        public void AddCard(Texture pic, string name, string lockText, Action onClick)
        {
            bool locked = lockText != null;
            var card = Ui.Panel(strip, "Card " + name, locked ? new Color(0.88f, 0.88f, 0.9f) : new Color(0.96f, 0.97f, 1f));
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 190;
            var img = new GameObject("Pic", typeof(RectTransform)).AddComponent<RawImage>();
            img.transform.SetParent(card.transform, false);
            img.texture = pic;
            img.color = locked ? new Color(1, 1, 1, 0.35f) : Color.white;
            img.raycastTarget = false;
            img.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(150, 150), new Vector2(0, -6));
            var label = Ui.Text(card.transform, locked ? lockText : name, 26, locked ? new Color(0.45f, 0.45f, 0.5f) : Ui.Ink, true);
            label.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(180, 56), new Vector2(0, 6));
            if (!locked) card.gameObject.AddComponent<Button>().onClick.AddListener(() => { Core.Sfx.Play("pick", 0.6f); onClick(); });
        }
    }
}
