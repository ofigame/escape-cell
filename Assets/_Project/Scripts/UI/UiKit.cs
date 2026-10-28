using System;
using SquashBot.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// The shared UI kit (Docs/ARAYUZ_HATA_VE_TEMIZLIK.md, section 1.2): the parts every screen is built from, styled only
    /// by <see cref="UiStyle"/> — a screen gives no colour, font or size of its own. The game builds its UI in code (no
    /// prefabs), so the parts are builders: UiScreenHeader, UiPanel, the four buttons, UiTabBar, UiCurrencyBar, UiBadge,
    /// UiProgressBar, UiListItem, UiRewardItem and UiPopup.
    /// </summary>
    public static class UiKit
    {
        public static UiStyle Style => UiStyle.Current;

        // ---------- UiScreenHeader ----------

        /// <summary>
        /// A screen's header, the same on every screen: the back button on the left (the Android back key does the same),
        /// the title in the middle, the gold on the right (as on the main menu and the shop). Returns the gold's text.
        /// </summary>
        public static TextMeshProUGUI ScreenHeader(RectTransform root, UiScreen screen, string title, Action onBack)
        {
            var s = Style;
            var bar = UiFactory.Rect("Header", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -s.headerHeight), Vector2.zero);
            Action back = onBack ?? (() => screen.Hide());
            if (screen != null) screen.BackAction = back;
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(s.edge, 0f), new Vector2(s.backButton, s.backButton), () => back(), 64f);
            var t = UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 110f), title, s.titleSize, s.text, title: true);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.enableAutoSizing = true; t.fontSizeMin = s.titleSize * 0.6f; t.fontSizeMax = s.titleSize;
            return CurrencyBar(bar, new Vector2(1f, 0.5f), new Vector2(-s.edge, 0f));
        }

        // ---------- UiCurrencyBar ----------

        /// <summary>The gold pill (the menu's), anchored at <paramref name="anchor"/>; its text, kept up to date by <see cref="RefreshCurrency"/>.</summary>
        public static TextMeshProUGUI CurrencyBar(RectTransform parent, Vector2 anchor, Vector2 position)
        {
            var s = Style;
            var coins = UiFactory.Pill("Coins", parent, anchor, position, new Vector2(260f, 100f), s.pill);
            UIController.CoinIcon(coins, new Vector2(56f, 0f));
            var t = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 90f), SaveData.Coins.ToString(), s.numberSize, s.gold, align: TextAlignmentOptions.Left);
            t.enableAutoSizing = true; t.fontSizeMin = s.numberSize * 0.6f; t.fontSizeMax = s.numberSize;
            return t;
        }

        /// <summary>The gold shown again (a header's pill).</summary>
        public static void RefreshCurrency(TextMeshProUGUI coins) { if (coins != null) coins.text = SaveData.Coins.ToString(); }

        // ---------- UiPanel ----------

        /// <summary>A panel in the style's panel colour (<paramref name="raised"/>: the highlighted one).</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, bool raised = false) =>
            Flat.Panel(parent, name, anchor, position, size, raised ? Style.panelHi : Style.panel);

        // ---------- the buttons ----------

        public static Button PrimaryButton(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, Action onClick) =>
            UiFactory.MakeButton(parent, text, Kind.Primary, anchor, position, size, onClick, Style.subtitleSize);

        public static Button SecondaryButton(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, Action onClick) =>
            UiFactory.MakeButton(parent, text, Kind.Secondary, anchor, position, size, onClick, Style.bodySize);

        public static Button GoldButton(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, Action onClick) =>
            UiFactory.MakeButton(parent, text, Kind.Gold, anchor, position, size, onClick, Style.bodySize);

        public static Button IconButton(Transform parent, string text, Vector2 anchor, Vector2 position, Action onClick) =>
            UiFactory.MakeButton(parent, text, Kind.Icon, anchor, position, new Vector2(Style.backButton, Style.backButton), onClick, 56f);

        // ---------- UiTabBar ----------

        /// <summary>Tabs across <paramref name="parent"/> (one per label), <paramref name="onSelect"/> with the tab's index;
        /// <see cref="SelectTab"/> marks the chosen one.</summary>
        public static Button[] TabBar(RectTransform parent, string[] labels, Action<int> onSelect)
        {
            var tabs = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int k = i;
                tabs[i] = UiFactory.MakeButton(parent, labels[i], Kind.Secondary, new Vector2((i + 0.5f) / labels.Length, 0.5f), Vector2.zero,
                    new Vector2(Mathf.Min(300f, 1000f / labels.Length - Style.gap), 88f), () => onSelect(k), Style.smallSize + 2f);
                ((RectTransform)tabs[i].transform).pivot = new Vector2(0.5f, 0.5f);
            }
            return tabs;
        }

        /// <summary>The chosen tab lit, the others dimmed.</summary>
        public static void SelectTab(Button[] tabs, int chosen)
        {
            for (int i = 0; i < tabs.Length; i++) tabs[i].transform.localScale = Vector3.one * (i == chosen ? 1.06f : 0.94f);
            for (int i = 0; i < tabs.Length; i++) { var g = tabs[i].targetGraphic; if (g != null) g.color = i == chosen ? Color.white : new Color(1f, 1f, 1f, 0.55f); }
        }

        // ---------- UiBadge ----------

        /// <summary>The red count badge (the menu's), at a parent's top right; hidden at 0.</summary>
        public static TextMeshProUGUI Badge(RectTransform parent, int count)
        {
            var b = UiFactory.Box("Badge", parent, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(46f, 46f));
            b.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(b, Style.theirs, UiSprites.Circle).raycastTarget = false;
            var t = UiFactory.Text(b, count.ToString(), Style.smallSize, Color.white, title: true);
            t.raycastTarget = false;
            b.gameObject.SetActive(count > 0);
            return t;
        }

        // ---------- UiProgressBar ----------

        /// <summary>A rounded progress bar (<paramref name="fill"/>: its colour, the style's gold by default); returns the
        /// fill, set by <see cref="UiFactory.SetBar"/>.</summary>
        public static Image ProgressBar(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, Color? fill = null)
        {
            var track = UiFactory.Box("Progress", parent, anchor, position, size);
            UiFactory.Fill(track, new Color(0f, 0f, 0f, 0.35f), UiSprites.Rounded, 4f).raycastTarget = false;
            var f = UiFactory.Rect("Fill", track, Vector2.zero, new Vector2(0f, 1f), new Vector2(3f, 3f), new Vector2(-3f, -3f));
            var img = UiFactory.Fill(f, fill ?? Style.gold, UiSprites.Rounded, 4f);
            img.raycastTarget = false;
            return img;
        }

        // ---------- UiListItem / UiRewardItem ----------

        /// <summary>A list row: a panel with its title and a line under it (left), room on the right for a button or a reward.</summary>
        public static RectTransform ListItem(RectTransform parent, float y, float height, string title, string line, out TextMeshProUGUI titleText, out TextMeshProUGUI lineText)
        {
            var s = Style;
            var row = Panel(parent, "Row", new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(980f, height));
            row.pivot = new Vector2(0.5f, 1f);
            titleText = Flat.Label(row, title, new Vector2(0f, 1f), new Vector2(s.pad, -s.gap), new Vector2(640f, 48f), s.bodySize + 4f, s.text, bold: true);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            lineText = Flat.Label(row, line, new Vector2(0f, 1f), new Vector2(s.pad, -s.gap - 50f), new Vector2(640f, height - 66f), s.smallSize, s.textMuted, wrap: true);
            lineText.rectTransform.pivot = new Vector2(0f, 1f);
            return row;
        }

        /// <summary>A reward: its picture (drawn by <paramref name="draw"/>) over its label, in a small raised panel.</summary>
        public static RectTransform RewardItem(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, Action<RectTransform> draw, string label)
        {
            var s = Style;
            var box = Panel(parent, "Reward", anchor, position, size, raised: true);
            var pic = UiFactory.Box("Picture", box, new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.42f), new Vector2(size.x * 0.62f, size.x * 0.62f));
            pic.pivot = new Vector2(0.5f, 0.5f);
            draw?.Invoke(pic);
            var t = Flat.Label(box, label, new Vector2(0.5f, 0f), new Vector2(0f, s.gap + 18f), new Vector2(size.x - 12f, 40f), s.smallSize, s.text, TextAlignmentOptions.Center, bold: true);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return box;
        }

        // ---------- UiPopup ----------

        /// <summary>A popup card over a dim: its title on top, the close (X) at its top right; the card returned (the
        /// content goes in it).</summary>
        public static RectTransform Popup(RectTransform root, UiScreen screen, string title, Vector2 size, Action onClose = null)
        {
            var s = Style;
            UiFactory.Fill(UiFactory.Stretch("Dim", root), s.dim);
            var card = Panel(root, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, size);
            card.pivot = new Vector2(0.5f, 0.5f);
            Action close = onClose ?? (() => screen.Hide());
            if (screen != null) screen.BackAction = close;
            var t = Flat.Label(card, title, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(size.x - 220f, 80f), s.titleSize, s.gold, TextAlignmentOptions.Center, bold: true);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.MakeButton(card, "X", Kind.Icon, new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(96f, 96f), () => close(), 44f);
            return card;
        }
    }
}
