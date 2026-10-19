using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrickMuseum.UI
{
    /// <summary>The city screen: level and stars, inventory and collection buttons, the slot panel, the inventory list.</summary>
    public sealed class CityUi
    {
        public Canvas canvas;
        public PointerArea touch;
        public TextMeshProUGUI level, stars, help;
        public Image levelFill;
        public Button inventoryBtn, collectionBtn, walkBtn, characterBtn, buildBtn, langBtn, resumeBtn;
        public TextMeshProUGUI money;
        public Button scoreBtn, bonusBtn, farBtn, farmBtn, harvestBtn, megaBtn;
        // slot panel
        public GameObject panel;
        public TextMeshProUGUI panelTitle, panelInfo;
        public RawImage panelPic;
        public RectTransform panelButtons;
        // inventory list
        public GameObject list;
        public TextMeshProUGUI listTitle;
        public RectTransform listContent;
        // level up
        public GameObject levelUp;
        public TextMeshProUGUI levelUpText;

        public void Build(Transform parent)
        {
            canvas = Ui.MakeCanvas(parent, 11);
            var root = Ui.SafeRoot(canvas);
            var t = Ui.Plain(root, "Touch", new Color(0, 0, 0, 0));
            t.rectTransform.Fill();
            touch = t.gameObject.AddComponent<PointerArea>();

            var chip = Ui.Panel(root, "Level", Ui.Paper, false);
            chip.rectTransform.Place(new Vector2(0, 1), new Vector2(460, 120), new Vector2(24, -36));
            level = Ui.Text(chip.transform, "Seviye 1", 42, Ui.Ink, false, true);
            level.alignment = TextAlignmentOptions.Left;
            level.rectTransform.Place(new Vector2(0, 1), new Vector2(260, 64), new Vector2(24, -8));
            stars = Ui.Text(chip.transform, "", 30, new Color(0.55f, 0.42f, 0.05f), true);
            stars.alignment = TextAlignmentOptions.Right;
            stars.rectTransform.Place(new Vector2(1, 1), new Vector2(170, 64), new Vector2(-22, -8));
            var bar = Ui.Panel(chip.transform, "Bar", new Color(0.88f, 0.89f, 0.92f), false);
            bar.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(410, 26), new Vector2(0, 18));
            levelFill = Ui.Panel(bar.transform, "Fill", Ui.Yellow, false);
            levelFill.rectTransform.anchorMin = new Vector2(0, 0); levelFill.rectTransform.anchorMax = new Vector2(0, 1);
            levelFill.rectTransform.pivot = new Vector2(0, 0.5f);
            levelFill.rectTransform.offsetMin = Vector2.zero; levelFill.rectTransform.offsetMax = new Vector2(40, 0);

            collectionBtn = Ui.Button(root, "Koleksiyon", Color.white, Ui.Ink, 34, () => { });
            ((RectTransform)collectionBtn.transform).Place(new Vector2(1, 1), new Vector2(250, 96), new Vector2(-24, -36));
            inventoryBtn = Ui.Button(root, "Envanter", new Color(0.8f, 0.9f, 1f), Ui.Ink, 34, () => { });
            ((RectTransform)inventoryBtn.transform).Place(new Vector2(1, 1), new Vector2(250, 96), new Vector2(-24, -146));

            walkBtn = Ui.Button(root, "Şehri gez", Ui.Yellow, Ui.Ink, 34, () => { });
            ((RectTransform)walkBtn.transform).Place(new Vector2(1, 1), new Vector2(250, 96), new Vector2(-24, -256));
            characterBtn = Ui.Button(root, "Karakter", new Color(1f, 0.85f, 0.9f), Ui.Ink, 34, () => { });
            ((RectTransform)characterBtn.transform).Place(new Vector2(1, 1), new Vector2(250, 96), new Vector2(-24, -366));
            langBtn = Ui.Button(root, "Dil", Color.white, Ui.Ink, 30, () => { });
            ((RectTransform)langBtn.transform).Place(new Vector2(1, 1), new Vector2(250, 80), new Vector2(-24, -586));
            farBtn = Ui.Button(root, "Uzak semt", new Color(0.8f, 0.95f, 0.82f), Ui.Ink, 30, () => { });
            ((RectTransform)farBtn.transform).Place(new Vector2(1, 1), new Vector2(250, 80), new Vector2(-24, -676));
            megaBtn = Ui.Button(root, "Gökkent", new Color(0.82f, 0.88f, 1f), Ui.Ink, 30, () => { });
            ((RectTransform)megaBtn.transform).Place(new Vector2(1, 1), new Vector2(250, 80), new Vector2(-24, -766));
            var mp = Ui.Panel(root, "Money", new Color(1f, 0.96f, 0.82f, 0.95f), false);
            mp.rectTransform.Place(new Vector2(0, 1), new Vector2(230, 74), new Vector2(24, -168));   // under the level chip
            var coin = Ui.Plain(mp.transform, "Coin", new Color(0.95f, 0.7f, 0.1f), false);
            coin.sprite = Icons.Circle;
            coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(42, 42), new Vector2(14, 0));
            money = Ui.Text(mp.transform, "", 32, Ui.Ink, true);
            money.rectTransform.Fill(8); money.rectTransform.offsetMin = new Vector2(64, 8);
            scoreBtn = Ui.Button(root, "Puan", new Color(0.86f, 0.93f, 1f), Ui.Ink, 30, () => { });
            ((RectTransform)scoreBtn.transform).Place(new Vector2(0, 1), new Vector2(220, 74), new Vector2(264, -168));
            bonusBtn = Ui.Button(root, "Şehir bonusu", Ui.Yellow, Ui.Ink, 30, () => { });
            ((RectTransform)bonusBtn.transform).Place(new Vector2(0, 1), new Vector2(330, 80), new Vector2(24, -252));
            bonusBtn.gameObject.SetActive(false);
            farmBtn = Ui.Button(root, "Balıklar hazır", new Color(0.7f, 0.88f, 1f), Ui.Ink, 30, () => { });
            ((RectTransform)farmBtn.transform).Place(new Vector2(0, 1), new Vector2(330, 80), new Vector2(24, -340));
            farmBtn.gameObject.SetActive(false);
            harvestBtn = Ui.Button(root, "Hasat hazır", new Color(0.8f, 0.95f, 0.6f), Ui.Ink, 30, () => { });
            ((RectTransform)harvestBtn.transform).Place(new Vector2(0, 1), new Vector2(330, 80), new Vector2(24, -428));
            harvestBtn.gameObject.SetActive(false);
            resumeBtn = Ui.Button(root, "İnşaya devam et", Ui.Yellow, Ui.Ink, 40, () => { });
            ((RectTransform)resumeBtn.transform).Place(new Vector2(0.5f, 0), new Vector2(560, 116), new Vector2(0, 190));
            resumeBtn.gameObject.SetActive(false);
            buildBtn = Ui.Button(root, "Evimi yap", new Color(0.75f, 0.95f, 0.75f), Ui.Ink, 34, () => { });
            ((RectTransform)buildBtn.transform).Place(new Vector2(1, 1), new Vector2(250, 96), new Vector2(-24, -476));

            var hp = Ui.Panel(root, "HelpBg", new Color(1, 1, 1, 0.88f), false);
            hp.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(900, 110), new Vector2(0, 60));
            help = Ui.Text(hp.transform, "", 34, Ui.Ink, true);
            help.rectTransform.Fill(14);

            BuildPanel(root);
            BuildList(root);

            var lu = Ui.Panel(root, "LevelUp", new Color(1f, 0.93f, 0.55f), false);
            lu.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(900, 300), new Vector2(0, 260));
            levelUp = lu.gameObject;
            levelUpText = Ui.Text(lu.transform, "", 46, Ui.Ink, false, true);
            levelUpText.rectTransform.Fill(28);
            levelUp.SetActive(false);
        }

        private void BuildPanel(Transform root)
        {
            var p = Ui.Panel(root, "SlotPanel", Color.white);
            p.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(1000, 560), new Vector2(0, 30));
            panel = p.gameObject;
            var picBg = Ui.Panel(p.transform, "PicBg", new Color(0.9f, 0.93f, 0.95f), false);
            picBg.rectTransform.Place(new Vector2(0, 1), new Vector2(260, 260), new Vector2(30, -30));
            panelPic = Ui.Rect(picBg.transform, "Pic").gameObject.AddComponent<RawImage>();
            panelPic.rectTransform.Fill(8);
            panelPic.raycastTarget = false;
            panelTitle = Ui.Text(p.transform, "", 46, Ui.Ink, false, true);
            panelTitle.alignment = TextAlignmentOptions.Left;
            panelTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(660, 80), new Vector2(310, -36));
            panelInfo = Ui.Text(p.transform, "", 32, new Color(0.32f, 0.35f, 0.42f), true);
            panelInfo.alignment = TextAlignmentOptions.TopLeft;
            panelInfo.rectTransform.Place(new Vector2(0, 1), new Vector2(660, 180), new Vector2(310, -120));
            panelButtons = Ui.Rect(p.transform, "Buttons");
            panelButtons.Place(new Vector2(0.5f, 0), new Vector2(940, 210), new Vector2(0, 26));
            var h = panelButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 18; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.LowerCenter;
            panel.SetActive(false);
        }

        private void BuildList(Transform root)
        {
            var card = Ui.Modal(root, "Inventory", 960, 1500, out list);
            listTitle = Ui.Text(card, "Envanter", 58, Ui.Ink, false, true);
            listTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(900, 100), new Vector2(0, -24));
            var scroll = Ui.Rect(card, "Scroll");
            scroll.anchorMin = Vector2.zero; scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(24, 180); scroll.offsetMax = new Vector2(-24, -140);
            scroll.gameObject.AddComponent<RectMask2D>();
            var img = scroll.gameObject.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0.001f);
            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            listContent = Ui.Rect(scroll, "Content");
            listContent.anchorMin = new Vector2(0, 1); listContent.anchorMax = new Vector2(1, 1); listContent.pivot = new Vector2(0.5f, 1);
            listContent.offsetMin = listContent.offsetMax = Vector2.zero;
            var v = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = listContent; sr.horizontal = false; sr.viewport = scroll;
            var close = Ui.Button(card, "Kapat", new Color(0.92f, 0.93f, 0.95f), Ui.Ink, 44, () => list.SetActive(false));
            ((RectTransform)close.transform).Place(new Vector2(0.5f, 0), new Vector2(880, 130), new Vector2(0, 30));
            list.SetActive(false);
        }

        /// <summary>Fills the slot panel: picture (optional), title, info and up to three buttons.</summary>
        public void ShowPanel(string title, string info, Texture pic, params (string label, Color color, Action action)[] buttons)
        {
            panelTitle.text = title;
            panelInfo.text = info;
            panelPic.texture = pic;
            panelPic.transform.parent.gameObject.SetActive(pic != null);
            float x = pic != null ? 310 : 40, w = pic != null ? 660 : 920;
            panelTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(w, 80), new Vector2(x, -36));
            panelInfo.rectTransform.Place(new Vector2(0, 1), new Vector2(w, 180), new Vector2(x, -120));
            foreach (Transform c in panelButtons) UnityEngine.Object.Destroy(c.gameObject);
            foreach (var (label, color, action) in buttons)
            {
                var b = Ui.Button(panelButtons, label, color, Ui.Ink, 40, () => action());
                b.Height(130f);
            }
            panel.SetActive(true);
        }

        public void SetLevel(int lvl, int starsHave, int from, int to)
        {
            level.text = $"Seviye {lvl}";
            stars.text = to > from ? $"{starsHave}/{to} yıldız" : $"{starsHave} yıldız";
            float k = to > from ? Mathf.Clamp01((starsHave - from) / (float)(to - from)) : 1f;
            levelFill.rectTransform.offsetMax = new Vector2(Mathf.Max(26f, 410f * k) - 0f, 0);
        }

        /// <summary>A row in the inventory / choice list: picture, name, detail and an optional button.</summary>
        public void AddRow(Texture pic, string name, string detail, string button, Action onClick)
        {
            var row = Ui.Panel(listContent, "Row", new Color(0.95f, 0.96f, 0.97f), false).Height(160f);
            var p = Ui.Rect(row.transform, "Pic").gameObject.AddComponent<RawImage>();
            p.texture = pic;
            p.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(140, 140), new Vector2(12, 0));
            var n = Ui.Text(row.transform, name, 38, Ui.Ink, true);
            n.alignment = TextAlignmentOptions.Left;
            n.rectTransform.Place(new Vector2(0, 1), new Vector2(460, 70), new Vector2(170, -14));
            var d = Ui.Text(row.transform, detail, 28, new Color(0.4f, 0.43f, 0.5f), true);
            d.alignment = TextAlignmentOptions.Left;
            d.rectTransform.Place(new Vector2(0, 0), new Vector2(460, 60), new Vector2(170, 16));
            if (button != null)
            {
                var b = Ui.Button(row.transform, button, Ui.Yellow, Ui.Ink, 36, () => onClick());
                ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(220, 110), new Vector2(-18, 0));
            }
        }

        public void ClearRows() { foreach (Transform c in listContent) UnityEngine.Object.Destroy(c.gameObject); }
    }
}
