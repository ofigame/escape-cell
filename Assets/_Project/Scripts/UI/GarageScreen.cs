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
    /// Bip's workshop: the real robot stands in the upper half (the camera leans in on it), the lower half holds the
    /// categories (head, hair, skin, eye colour, badge, dance) and a scrolling grid of their items. Tapping an item tries
    /// it on at once; the button buys or equips it. The badges come with the story.
    /// </summary>
    public class GarageScreen : MonoBehaviour
    {
        public event Action BackPressed;
        /// <summary>The robot should show this outfit (an item being tried on, or the equipped set).</summary>
        public event Action<Dictionary<Slot, Cosmetic>> PreviewChanged;
        /// <summary>A dance was picked: show it off.</summary>
        public event Action DancePreview;
        /// <summary>The "change build" button (foi's four builds) was pressed.</summary>
        public event Action HeroPressed;

        private const int TabsPerRow = 6;

        private UiScreen screen;
        private TextMeshProUGUI coinsText, itemName, actionLabel;
        private Button actionButton, goalButton;
        private TextMeshProUGUI goalLabel;
        private RectTransform grid;
        private ScrollRect gridScroll;
        private BipTip bip;
        private readonly List<(Slot slot, Image bg)> tabs = new List<(Slot, Image)>();
        private Slot slot = Slot.Head;
        private Cosmetic selected;
        private int reachedWorld;

        public UiScreen Screen => screen;

        /// <summary>A drag across the open space: foi turns round by this many screen widths.</summary>
        public event Action<float> Turned;

        /// <summary>A drag area that reports how far the finger moved sideways.</summary>
        private class DragTurn : MonoBehaviour, UnityEngine.EventSystems.IDragHandler
        {
            public Action<float> turned;
            public void OnDrag(UnityEngine.EventSystems.PointerEventData e) => turned?.Invoke(e.delta.x / Mathf.Max(1f, UnityEngine.Screen.width));
        }

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
            coinsText = UiKit.ScreenHeader(root, screen, Loc.T("garage.title"), () => BackPressed?.Invoke()); // (the kit's header: back, title, gold)
            var bar = (RectTransform)root.Find("Header");

            // A drag across the open space above the panel turns foi round (all the way: see its back and its outfit).
            var turn = UiFactory.Rect("TurnArea", root, Vector2.zero, Vector2.one);
            UiFactory.Fill(turn, UiKit.A(UiKit.Style.black, 0f));
            turn.gameObject.AddComponent<DragTurn>().turned = dx => Turned?.Invoke(dx);
            turn.SetAsFirstSibling();

            // Lower panel
            var panel = UiFactory.Card("Panel", root, new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 20f), new Vector2(1000f, 868f)); // two rows of items (the rest scroll): more room for foi
            itemName = UiFactory.TextBox("Name", panel, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(940f, 70f), "", 42f, Palette.UiText, title: true);
            itemName.enableAutoSizing = true;
            itemName.fontSizeMin = 26f;
            itemName.fontSizeMax = 42f;

            var tabRow = UiFactory.Box("Tabs", panel, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(940f, 190f));
            // foi's tabs: its head first, its looks, its story badges, its dances (the hats, armour paints, capes and arm
            // gear are gone: the rank gear comes with the rank; paint, eyes, step marks, trails and legs were the old robot's)
            var slots = new[] { Slot.Head, Slot.Hair, Slot.Skin, Slot.Iris, Slot.Badge, Slot.Dance };
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                int col = i % TabsPerRow, row = i / TabsPerRow;
                var tab = UiFactory.MakeButton(tabRow, Loc.T("garage." + s), Kind.Secondary, new Vector2(0f, 1f), new Vector2(col * 157f, -row * 96f), new Vector2(150f, 88f), () => SelectSlot(s), 24f);
                tabs.Add((s, tab.targetGraphic as Image));
            }

            // The item grid shows two rows and scrolls for the rest.
            var viewport = UiFactory.Box("GridView", panel, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(950f, 372f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, UiKit.A(UiKit.Style.black, 0.001f));
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
            actionLabel = actionButton.GetComponentInChildren<TextMeshProUGUI>();

            bip = BipTip.Create(root);
            bip.BaseY = Monetization.Ads.BannerReserve + 908f;
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
                if (bg != null) bg.color = ts == s ? UiKit.A(UiKit.Style.cyan, 0.55f) : UiKit.A(UiKit.Style.textMuted, 0.2f);
            RebuildGrid();
            grid.anchoredPosition = Vector2.zero;
            Refresh();
            PreviewChanged?.Invoke(Cosmetics.Outfit());
        }

        private bool Locked(Cosmetic c) => !Cosmetics.Owns(c) && (c.storyLevel > 0 || reachedWorld < c.world);

        private string LockText(Cosmetic c) => c.storyLevel > 0 ? Loc.F("garage.story", c.storyLevel) : Loc.F("garage.world", c.world + 1);

        /// <summary>What foi can wear: its own dances (made on its body) only.</summary>
        private static bool Fits(Cosmetic c) => c.slot != Slot.Dance || c.IsDefault || c.id.StartsWith("dance.d");

        private void RebuildGrid()
        {
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            var items = Cosmetics.InSlot(slot).FindAll(Fits);
            const int columns = 5;
            const float cell = 172f, gap = 14f;
            for (int i = 0; i < items.Count; i++)
            {
                var c = items[i];
                int col = i % columns, row = i / columns;
                var tile = UiFactory.Box(c.id, grid, new Vector2(0f, 1f), new Vector2(col * (cell + gap) + 12f, -row * (cell + gap) - 4f), new Vector2(cell, cell));
                var bg = UiFactory.Fill(tile, UiKit.A(UiKit.Style.backDeep, 0.6f) /* glass */, UiSprites.Rounded, 1.2f);
                var swatch = UiFactory.Box("Swatch", tile, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(96f, 96f));
                var sc = c.color;
                float m = Mathf.Max(1f, Mathf.Max(sc.r, Mathf.Max(sc.g, sc.b)));
                var picture = ItemPreview.Cosmetic(c); // heads and badges show themselves
                if (picture != null)
                {
                    swatch.sizeDelta = new Vector2(124f, 124f);
                    swatch.anchoredPosition = new Vector2(0f, -6f);
                    var raw = swatch.gameObject.AddComponent<RawImage>();
                    raw.texture = picture;
                    raw.raycastTarget = false;
                }
                else UiFactory.Fill(swatch, new Color(sc.r / m, sc.g / m, sc.b / m, 1f), slot == Slot.Iris ? UiSprites.Circle : UiSprites.Rounded, 2f).raycastTarget = false;
                if (c.storyLevel > 0)
                {
                    // A little star marks the story rewards.
                    var mark = UiFactory.Box("Mark", tile, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(34f, 34f));
                    UiFactory.Fill(mark, Palette.UiCyan, UiSprites.Star).raycastTarget = false;
                }

                bool owned = Cosmetics.Owns(c);
                bool locked = Locked(c);
                string label = owned ? (Cosmetics.Equipped(slot) == c ? Loc.T("garage.on") : "") : locked ? LockText(c) : c.price.ToString();
                UiFactory.TextBox("Price", tile, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(160f, 46f), label, 28f,
                    locked ? UiKit.A(UiKit.Style.white, 0.5f) : owned ? Palette.UiCyan : Palette.UiGold);
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
            }
            else
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                if (!Locked(selected) && SaveData.Coins < selected.price) WorkshopTalk.Poor(bip);
                return;
            }
            AfterChange();
        }

        private void AfterChange()
        {
            var keep = grid.anchoredPosition;
            RebuildGrid();
            grid.anchoredPosition = keep;
            Refresh();
            PreviewChanged?.Invoke(Cosmetics.Outfit());
        }

        private void Refresh()
        {
            coinsText.text = SaveData.Coins.ToString();
            if (selected == null) return;
            itemName.text = Loc.T("cos." + selected.id);
            bool owned = Cosmetics.Owns(selected);
            bool equipped = owned && Cosmetics.Equipped(selected.slot) == selected;
            bool locked = Locked(selected);
            actionLabel.text = equipped ? Loc.T("garage.equipped") : owned ? Loc.T("garage.equip")
                : locked ? LockText(selected) : Loc.F("garage.buy", selected.price);
            actionButton.interactable = !equipped && !locked;
            bool sellable = !owned && selected.storyLevel == 0;
            goalButton.gameObject.SetActive(sellable);
            bool isGoal = Goal.Is(Goal.ForCosmetic(selected));
            goalLabel.text = Loc.T(isGoal ? "goal.on" : "goal.set");
            goalLabel.color = isGoal ? Palette.UiGold : Palette.UiText;
        }
    }
}
