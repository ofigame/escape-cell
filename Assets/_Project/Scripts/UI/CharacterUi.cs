using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrickMuseum.UI
{
    /// <summary>The character workshop screen: the turning person on top, tabs for each wardrobe slot, the choices below.</summary>
    public sealed class CharacterUi
    {
        public Canvas canvas;
        public RawImage preview;
        public PointerArea previewDrag;
        public Button doneBtn, randomBtn;
        public TextMeshProUGUI slotTitle;
        public readonly List<Button> tabs = new List<Button>();
        public RectTransform tabRow, options;

        public void Build(Transform parent)
        {
            canvas = Ui.MakeCanvas(parent, 15);
            var root = canvas.transform;
            var bg = Ui.Plain(root, "Bg", new Color(0.93f, 0.95f, 0.97f));
            bg.rectTransform.Fill();

            var title = Ui.Text(root, "Karakterim", 60, Ui.Ink, false, true);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(600, 100), new Vector2(0, -40));
            doneBtn = Ui.Button(root, "Tamam", Ui.Yellow, Ui.Ink, 40, () => { });
            ((RectTransform)doneBtn.transform).Place(new Vector2(1, 1), new Vector2(220, 100), new Vector2(-30, -40));
            randomBtn = Ui.Button(root, "Rastgele", Color.white, Ui.Ink, 34, () => { });
            ((RectTransform)randomBtn.transform).Place(new Vector2(0, 1), new Vector2(220, 100), new Vector2(30, -40));

            var stage = Ui.Panel(root, "Stage", new Color(0.84f, 0.88f, 0.92f));
            stage.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(760, 760), new Vector2(0, -160));
            preview = Ui.Rect(stage.transform, "Person").gameObject.AddComponent<RawImage>();
            preview.rectTransform.Fill(4);
            preview.raycastTarget = false;
            previewDrag = stage.gameObject.AddComponent<PointerArea>();
            var hint = Ui.Text(stage.transform, "Çevirmek için sürükle", 26, new Color(0.35f, 0.4f, 0.48f), true);
            hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(600, 50), new Vector2(0, 10));

            tabRow = Ui.Rect(root, "Tabs");
            tabRow.Place(new Vector2(0.5f, 1), new Vector2(1020, 210), new Vector2(0, -940));
            var g = tabRow.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(240, 95); g.spacing = new Vector2(20, 18); g.childAlignment = TextAnchor.UpperCenter;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 4;

            slotTitle = Ui.Text(root, "", 40, Ui.Ink, false, true);
            slotTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(900, 70), new Vector2(0, -1160));

            options = Ui.Rect(root, "Options");
            options.anchorMin = new Vector2(0, 0); options.anchorMax = new Vector2(1, 1);
            options.offsetMin = new Vector2(40, 40); options.offsetMax = new Vector2(-40, -1240);
            var og = options.gameObject.AddComponent<GridLayoutGroup>();
            og.cellSize = new Vector2(180, 180); og.spacing = new Vector2(20, 20); og.childAlignment = TextAnchor.UpperCenter;
            canvas.gameObject.SetActive(false);
        }

        public Button AddTab(string label, Action onClick)
        {
            var b = Ui.Button(tabRow, label, Color.white, Ui.Ink, 32, () => onClick());
            tabs.Add(b);
            return b;
        }

        public void SelectTab(int i)
        {
            for (int k = 0; k < tabs.Count; k++) tabs[k].GetComponent<Image>().color = k == i ? Ui.Yellow : Color.white;
        }

        public void ClearOptions() { foreach (Transform c in options) UnityEngine.Object.Destroy(c.gameObject); }

        /// <summary>A choice tile: a colour swatch or a word, a ring when chosen, greyed with the level when still locked.</summary>
        public void AddOption(string label, Color? swatch, bool chosen, int lockedUntilLevel, Action onClick)
        {
            bool locked = lockedUntilLevel > 0;
            var tile = Ui.Panel(options, "Option", chosen ? Ui.Yellow : Color.white);
            var b = tile.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => onClick());
            if (swatch.HasValue)
            {
                var s = Ui.Panel(tile.transform, "Swatch", locked ? Color.Lerp(swatch.Value, Color.gray, 0.6f) : swatch.Value, false);
                s.rectTransform.Fill(chosen ? 22 : 16);
                s.rectTransform.offsetMin = new Vector2(s.rectTransform.offsetMin.x, 46);
            }
            var t = Ui.Text(tile.transform, locked ? $"Sv {lockedUntilLevel}" : label, swatch.HasValue ? 24 : 34, locked ? new Color(0.5f, 0.5f, 0.55f) : Ui.Ink, true);
            if (swatch.HasValue) t.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(170, 46), new Vector2(0, 2));
            else t.rectTransform.Fill(10);
        }
    }
}
