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
    /// The garage: the real robot stands in the upper half (the camera leans in on it), the lower half holds
    /// six categories and their items. Tapping an item tries it on at once; the button buys or equips it.
    /// </summary>
    public class GarageScreen : MonoBehaviour
    {
        public event Action BackPressed;
        /// <summary>The robot should show this outfit (an item being tried on, or the equipped set).</summary>
        public event Action<Dictionary<Slot, Cosmetic>> PreviewChanged;
        /// <summary>A dance was picked: show it off.</summary>
        public event Action DancePreview;

        private UiScreen screen;
        private TextMeshProUGUI coinsText, itemName, actionLabel;
        private Button actionButton, goalButton;
        private TextMeshProUGUI goalLabel;
        private RectTransform grid;
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
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 110f), Loc.T("garage.title"), 64f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var coins = UiFactory.Pill("Coins", bar, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(260f, 100f), UiFactory.PillColor);
            UIController.CoinIcon(coins, new Vector2(56f, 0f));
            coinsText = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 90f), "0", 50f, Palette.UiGold, align: TextAlignmentOptions.Left);

            // Lower panel
            var panel = UiFactory.Card("Panel", root, new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 20f), new Vector2(1000f, 1000f));
            itemName = UiFactory.TextBox("Name", panel, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(900f, 70f), "", 48f, Palette.UiText, title: true);

            var tabRow = UiFactory.Box("Tabs", panel, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(940f, 110f));
            var slots = (Slot[])Enum.GetValues(typeof(Slot));
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                var tab = UiFactory.MakeButton(tabRow, Loc.T("garage." + s), Kind.Secondary, new Vector2(0f, 0.5f), new Vector2(i * 157f, 0f), new Vector2(148f, 100f), () => SelectSlot(s), 28f);
                tabs.Add((s, tab.targetGraphic as Image));
            }

            grid = UiFactory.Box("Grid", panel, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(940f, 560f));

            actionButton = UiFactory.MakeButton(panel, "", Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(500f, 140f), Act, 60f);
            goalButton = UiFactory.MakeButton(panel, "", Kind.Secondary, new Vector2(0f, 0f), new Vector2(30f, 55f), new Vector2(200f, 110f), () =>
            {
                if (selected == null) return;
                Goal.Toggle(Goal.ForCosmetic(selected));
                AudioManager.PlaySfx(Sfx.Click, 0.7f, 1.2f);
                Refresh();
            }, 30f);
            goalLabel = goalButton.GetComponentInChildren<TextMeshProUGUI>();
            actionLabel = actionButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        public void Show(int worldReached)
        {
            reachedWorld = worldReached;
            screen.Show();
            SelectSlot(slot);
        }

        public void Hide() => screen.Hide(true);

        private void SelectSlot(Slot s)
        {
            slot = s;
            selected = Cosmetics.Equipped(s);
            foreach (var (ts, bg) in tabs)
                if (bg != null) bg.color = ts == s ? new Color(0.62f, 0.95f, 1f, 0.55f) : new Color(0.62f, 0.62f, 1f, 0.2f);
            RebuildGrid();
            Refresh();
            PreviewChanged?.Invoke(Cosmetics.Outfit());
        }

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
                var tile = UiFactory.Box(c.id, grid, new Vector2(0f, 1f), new Vector2(col * (cell + gap) + 10f, -row * (cell + gap)), new Vector2(cell, cell));
                var bg = UiFactory.Fill(tile, new Color(0.12f, 0.1f, 0.26f, 0.9f), UiSprites.Rounded, 1.2f);
                var swatch = UiFactory.Box("Swatch", tile, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(96f, 96f));
                var sc = c.color;
                float m = Mathf.Max(1f, Mathf.Max(sc.r, Mathf.Max(sc.g, sc.b)));
                UiFactory.Fill(swatch, new Color(sc.r / m, sc.g / m, sc.b / m, 1f), slot == Slot.Color || slot == Slot.Eyes ? UiSprites.Circle : UiSprites.Rounded, 2f).raycastTarget = false;

                bool owned = Cosmetics.Owns(c);
                bool locked = reachedWorld < c.world;
                string label = owned ? (Cosmetics.Equipped(slot) == c ? Loc.T("garage.on") : "") : locked ? Loc.F("garage.world", c.world + 1) : c.reward ? Loc.T("garage.prize") : c.price.ToString();
                UiFactory.TextBox("Price", tile, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(160f, 46f), label, 30f,
                    locked ? new Color(1f, 1f, 1f, 0.5f) : owned ? Palette.UiCyan : Palette.UiGold);
                if (selected == c) UiFactory.Fill(UiFactory.Stretch("Selected", tile), Palette.UiGold, UiSprites.Ring, 1.2f).raycastTarget = false;

                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                button.onClick.AddListener(() => Pick(c));
                tile.gameObject.AddComponent<ButtonPress>();
            }
        }

        private void Pick(Cosmetic c)
        {
            selected = c;
            AudioManager.PlaySfx(Sfx.Click, 0.7f, 1.1f);
            RebuildGrid();
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
            }
            else if (Cosmetics.TryBuy(selected, reachedWorld))
            {
                Cosmetics.Equip(selected);
                AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
                Haptics.Medium();
                if (selected.set != null && Cosmetics.SetComplete(selected.set)) setJustDone = selected.set;
            }
            else
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                return;
            }
            RebuildGrid();
            Refresh();
            PreviewChanged?.Invoke(Cosmetics.Outfit());
            if (setJustDone != null)
            {
                // The set is complete: its prize is unlocked.
                itemName.text = Loc.F("garage.setDone", Loc.T("set." + setJustDone));
                AudioManager.PlaySfx(Sfx.Win, 0.9f, 1.2f);
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
            bool locked = reachedWorld < selected.world;
            actionLabel.text = equipped ? Loc.T("garage.equipped") : owned ? Loc.T("garage.equip")
                : locked ? Loc.F("garage.world", selected.world + 1) : selected.reward ? Loc.T("garage.completeSet") : Loc.F("garage.buy", selected.price);
            actionButton.interactable = !equipped && !locked && (owned || (!selected.reward && SaveData.Coins >= selected.price));
            goalButton.gameObject.SetActive(!owned && !selected.reward);
            bool isGoal = Goal.Is(Goal.ForCosmetic(selected));
            goalLabel.text = Loc.T(isGoal ? "goal.on" : "goal.set");
            goalLabel.color = isGoal ? Palette.UiGold : Palette.UiText;
        }
    }
}
