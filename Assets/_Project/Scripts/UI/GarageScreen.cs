using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// Bip's paint workshop: the real robot stands in the upper half (the camera leans in on it), the lower half holds
    /// the categories (two rows of tabs) and a scrolling grid of their items. Tapping an item tries it on at once; the
    /// button buys or equips it. A world set can be bought whole, 20% cheaper; story rewards come with the story.
    /// </summary>
    public class GarageScreen : MonoBehaviour
    {
        public event Action BackPressed;
        /// <summary>The robot should show this outfit (an item being tried on, or the equipped set).</summary>
        public event Action<Dictionary<Slot, Cosmetic>> PreviewChanged;
        /// <summary>A dance was picked: show it off.</summary>
        public event Action DancePreview;

        private const int TabsPerRow = 5;

        private UiScreen screen;
        private TextMeshProUGUI coinsText, itemName, actionLabel;
        private Button actionButton, goalButton, setButton;
        private TextMeshProUGUI goalLabel, setLabel;
        private RectTransform grid;
        private ScrollRect gridScroll;
        private BipTip bip;
        private readonly List<(Slot slot, Image bg)> tabs = new List<(Slot, Image)>();
        private Slot slot = Slot.Color;
        private Cosmetic selected;
        private int reachedWorld;
        private string setJustDone;

        public UiScreen Screen => screen;

        public static GarageScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("Garage", canvasRoot, out var root);
            var garage = root.gameObject.AddComponent<GarageScreen>();
            garage.screen = screen;
            garage.Build(root);
            return garage;
        }

        private void Build(RectTransform root)
        {
            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -190f), Vector2.zero);
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(124f, 124f), () => BackPressed?.Invoke(), 64f);
            UiFactory.Pill("TitleBack", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 96f), UiFactory.PillColor);
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 110f), Loc.T("garage.title"), 56f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var coins = UiFactory.Pill("Coins", bar, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(260f, 100f), UiFactory.PillColor);
            UIController.CoinIcon(coins, new Vector2(56f, 0f));
            coinsText = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 90f), "0", 50f, Palette.UiGold, align: TextAlignmentOptions.Left);

            // Lower panel
            var panel = UiFactory.Card("Panel", root, new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 20f), new Vector2(1000f, 1040f));
            itemName = UiFactory.TextBox("Name", panel, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(940f, 70f), "", 42f, Palette.UiText, title: true);
            itemName.enableAutoSizing = true;
            itemName.fontSizeMin = 26f;
            itemName.fontSizeMax = 42f;

            var tabRow = UiFactory.Box("Tabs", panel, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(940f, 190f));
            var slots = (Slot[])Enum.GetValues(typeof(Slot));
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                int col = i % TabsPerRow, row = i / TabsPerRow;
                var tab = UiFactory.MakeButton(tabRow, Loc.T("garage." + s), Kind.Secondary, new Vector2(0f, 1f), new Vector2(col * 188f, -row * 96f), new Vector2(180f, 88f), () => SelectSlot(s), 26f);
                tabs.Add((s, tab.targetGraphic as Image));
            }

            // The item grid scrolls when a category holds more than three rows.
            var viewport = UiFactory.Box("GridView", panel, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(950f, 540f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            grid = UiFactory.Rect("Grid", viewport, new Vector2(0f, 1f), Vector2.one);
            grid.pivot = new Vector2(0.5f, 1f);
            gridScroll = viewport.gameObject.AddComponent<ScrollRect>();
            gridScroll.viewport = viewport;
            gridScroll.content = grid;
            gridScroll.horizontal = false;
            gridScroll.movementType = ScrollRect.MovementType.Elastic;
            gridScroll.scrollSensitivity = 60f;

            actionButton = UiFactory.MakeButton(panel, "", Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(440f, 140f), Act, 52f);
            goalButton = UiFactory.MakeButton(panel, "", Kind.Secondary, new Vector2(0f, 0f), new Vector2(26f, 55f), new Vector2(200f, 110f), () =>
            {
                if (selected == null) return;
                Goal.Toggle(Goal.ForCosmetic(selected));
                AudioManager.PlaySfx(Sfx.Click, 0.7f, 1.2f);
                Refresh();
            }, 30f);
            goalLabel = goalButton.GetComponentInChildren<TextMeshProUGUI>();
            setButton = UiFactory.MakeButton(panel, "", Kind.Gold, new Vector2(1f, 0f), new Vector2(-26f, 55f), new Vector2(230f, 110f), BuySet, 28f);
            setLabel = setButton.GetComponentInChildren<TextMeshProUGUI>();
            setLabel.textWrappingMode = TextWrappingModes.Normal;
            actionLabel = actionButton.GetComponentInChildren<TextMeshProUGUI>();

            bip = BipTip.Create(root);
            bip.BaseY = Monetization.Ads.BannerReserve + 1080f;
        }

        public void Show(int worldReached)
        {
            reachedWorld = worldReached;
            screen.Show();
            SelectSlot(slot);
        }

        public void Hide()
        {
            bip.Hide();
            screen.Hide(true);
        }

        private void SelectSlot(Slot s)
        {
            slot = s;
            selected = Cosmetics.Equipped(s);
            foreach (var (ts, bg) in tabs)
                if (bg != null) bg.color = ts == s ? new Color(0.62f, 0.95f, 1f, 0.55f) : new Color(0.62f, 0.62f, 1f, 0.2f);
            RebuildGrid();
            grid.anchoredPosition = Vector2.zero;
            Refresh();
            PreviewChanged?.Invoke(Cosmetics.Outfit());
        }

        private bool Locked(Cosmetic c) => !Cosmetics.Owns(c) && (c.storyLevel > 0 || reachedWorld < c.world);

        private string LockText(Cosmetic c) => c.storyLevel > 0 ? Loc.F("garage.story", c.storyLevel) : Loc.F("garage.world", c.world + 1);

        private void RebuildGrid()
        {
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            var items = Cosmetics.InSlot(slot);
            const int columns = 5;
            const float cell = 172f, gap = 14f;
            for (int i = 0; i < items.Count; i++)
            {
                var c = items[i];
                int col = i % columns, row = i / columns;
                var tile = UiFactory.Box(c.id, grid, new Vector2(0f, 1f), new Vector2(col * (cell + gap) + 12f, -row * (cell + gap) - 4f), new Vector2(cell, cell));
                var bg = UiFactory.Fill(tile, new Color(0.04f, 0.04f, 0.09f, 0.6f) /* glass */, UiSprites.Rounded, 1.2f);
                var swatch = UiFactory.Box("Swatch", tile, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(96f, 96f));
                var sc = c.color;
                float m = Mathf.Max(1f, Mathf.Max(sc.r, Mathf.Max(sc.g, sc.b)));
                UiFactory.Fill(swatch, new Color(sc.r / m, sc.g / m, sc.b / m, 1f), slot == Slot.Color || slot == Slot.Eyes ? UiSprites.Circle : UiSprites.Rounded, 2f).raycastTarget = false;
                if (c.set != null || c.storyLevel > 0)
                {
                    // A little star marks set pieces and story rewards.
                    var mark = UiFactory.Box("Mark", tile, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(34f, 34f));
                    UiFactory.Fill(mark, c.storyLevel > 0 ? Palette.UiCyan : Palette.UiGold, UiSprites.Star).raycastTarget = false;
                }

                bool owned = Cosmetics.Owns(c);
                bool locked = Locked(c);
                string label = owned ? (Cosmetics.Equipped(slot) == c ? Loc.T("garage.on") : "") : locked ? LockText(c) : c.reward ? Loc.T("garage.prize") : c.price.ToString();
                UiFactory.TextBox("Price", tile, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(160f, 46f), label, 28f,
                    locked ? new Color(1f, 1f, 1f, 0.5f) : owned ? Palette.UiCyan : Palette.UiGold);
                if (selected == c) UiFactory.Fill(UiFactory.Stretch("Selected", tile), Palette.UiGold, UiSprites.Ring, 1.2f).raycastTarget = false;

                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                button.onClick.AddListener(() => Pick(c));
                tile.gameObject.AddComponent<ButtonPress>();
            }
            int rows = (items.Count + columns - 1) / columns;
            grid.sizeDelta = new Vector2(0f, rows * (cell + gap) + 10f);
        }

        private void Pick(Cosmetic c)
        {
            selected = c;
            AudioManager.PlaySfx(Sfx.Click, 0.7f, 1.1f);
            var keep = grid.anchoredPosition;
            RebuildGrid();
            grid.anchoredPosition = keep;
            Refresh();
            PreviewChanged?.Invoke(Cosmetics.Outfit(c));
            if (c.slot == Slot.Dance) DancePreview?.Invoke();
        }

        private void Act()
        {
            if (selected == null) return;
            if (Cosmetics.Owns(selected))
            {
                Cosmetics.Equip(selected);
                AudioManager.PlaySfx(Sfx.Shield, 0.7f, 1.3f);
                WorkshopTalk.Worn(bip, selected);
            }
            else if (Cosmetics.TryBuy(selected, reachedWorld))
            {
                Cosmetics.Equip(selected);
                AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
                Haptics.Medium();
                WorkshopTalk.CosmeticBought(bip, selected);
                if (selected.set != null && Cosmetics.SetComplete(selected.set)) setJustDone = selected.set;
            }
            else
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                if (!Locked(selected) && !selected.reward && SaveData.Coins < selected.price) WorkshopTalk.Poor(bip);
                return;
            }
            AfterChange();
        }

        /// <summary>The rest of the selected piece's set, bought together at a discount.</summary>
        private void BuySet()
        {
            if (selected == null || selected.set == null) return;
            if (!Cosmetics.TryBuySet(selected.set, reachedWorld))
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                if (SaveData.Coins < Cosmetics.SetPrice(selected.set)) WorkshopTalk.Poor(bip);
                return;
            }
            if (!selected.reward) Cosmetics.Equip(selected);
            AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
            Haptics.Medium();
            setJustDone = selected.set;
            AfterChange();
        }

        private void AfterChange()
        {
            var keep = grid.anchoredPosition;
            RebuildGrid();
            grid.anchoredPosition = keep;
            Refresh();
            PreviewChanged?.Invoke(Cosmetics.Outfit());
            if (setJustDone != null)
            {
                // The set is complete: its prize is unlocked.
                itemName.text = Loc.F("garage.setDone", Loc.T("set." + setJustDone));
                AudioManager.PlaySfx(Sfx.Win, 0.9f, 1.2f);
                WorkshopTalk.SetDone(bip);
                setJustDone = null;
            }
        }

        private void Refresh()
        {
            coinsText.text = SaveData.Coins.ToString();
            if (selected == null) return;
            itemName.text = selected.set == null ? Loc.T("cos." + selected.id)
                : Loc.T("cos." + selected.id) + "  ·  " + Loc.F("garage.set", Loc.T("set." + selected.set), Cosmetics.SetOwned(selected.set), Cosmetics.SetSize(selected.set));
            bool owned = Cosmetics.Owns(selected);
            bool equipped = owned && Cosmetics.Equipped(selected.slot) == selected;
            bool locked = Locked(selected);
            actionLabel.text = equipped ? Loc.T("garage.equipped") : owned ? Loc.T("garage.equip")
                : locked ? LockText(selected) : selected.reward ? Loc.T("garage.completeSet") : Loc.F("garage.buy", selected.price);
            actionButton.interactable = !equipped && !locked && (owned || !selected.reward);
            bool sellable = !owned && !selected.reward && selected.storyLevel == 0;
            goalButton.gameObject.SetActive(sellable);
            bool isGoal = Goal.Is(Goal.ForCosmetic(selected));
            goalLabel.text = Loc.T(isGoal ? "goal.on" : "goal.set");
            goalLabel.color = isGoal ? Palette.UiGold : Palette.UiText;

            // The whole set, cheaper: only while two or more pieces are still missing and the set's world is reached.
            string set = selected.set;
            bool offer = set != null && reachedWorld >= selected.world && Cosmetics.SetSize(set) - Cosmetics.SetOwned(set) >= 2;
            setButton.gameObject.SetActive(offer);
            if (offer) setLabel.text = Loc.F("garage.buySet", Cosmetics.SetPrice(set));
        }
    }
}
