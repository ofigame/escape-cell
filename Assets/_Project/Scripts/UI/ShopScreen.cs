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
    /// The coin shop: permanent upgrades (with level pips) and one-use items. Every row shows its price and
    /// greys out when the player can't afford it; buying pops the row and rings the till.
    /// </summary>
    public class ShopScreen : MonoBehaviour
    {
        public event Action BackPressed;
        /// <summary>Raised after any purchase (the caller refreshes coins, lives and bonus rounds).</summary>
        public event Action Purchased;
        /// <summary>The paint workshop (the garage) was asked for.</summary>
        public event Action PaintPressed;

        private UiScreen screen;
        private TextMeshProUGUI coinsText;
        private RectTransform content;
        private readonly List<Action> refreshers = new List<Action>();
        private BipTip bip;
        private int builtDay = -1;

        public UiScreen Screen => screen;

        private enum Item { Shield, Magnet, Hover, Lives, Rescue, Armor, StartShield, ExtraRescue, StartHammer, DoubleCoins, CoinMagnet, TunnelBoost, Life, Tunnel }

        public static ShopScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("Shop", canvasRoot, out var root);
            var shop = root.gameObject.AddComponent<ShopScreen>();
            shop.screen = screen;
            shop.Build(root);
            return shop;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Fill(UiFactory.Stretch("Backdrop", root), new Color(0.08f, 0.07f, 0.18f, 0.96f)).gameObject.AddComponent<IgnoreSafeArea>();

            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -190f), Vector2.zero);
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(124f, 124f), () => BackPressed?.Invoke(), 64f);
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(520f, 110f), Loc.T("ws.title"), 56f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var coins = UiFactory.Pill("Coins", bar, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(260f, 100f), UiFactory.PillColor);
            UIController.CoinIcon(coins, new Vector2(56f, 0f));
            coinsText = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 90f), "0", 50f, Palette.UiGold, align: TextAlignmentOptions.Left);

            // Everything below the top bar scrolls.
            var viewport = UiFactory.Rect("Viewport", root, Vector2.zero, Vector2.one, new Vector2(0f, Monetization.Ads.BannerReserve), new Vector2(0f, -190f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;

            bip = BipTip.Create(root);
        }

        /// <summary>
        /// Fills the three shelves: the tool bag (story-locked skills, upgraded with coins), the paint workshop (the garage)
        /// and the daily counter (a different few one-use items every day). Rebuilt when the day changes.
        /// </summary>
        private void Populate()
        {
            builtDay = System.DateTime.Today.DayOfYear;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            refreshers.Clear();

            float y = -24f;
            Header(content, Loc.T("ws.weapons"), ref y);
            WeaponRow(content, ref y);
            y -= 20f;
            Header(content, Loc.T("ws.tools"), ref y);
            SlotsRow(content, ref y);
            foreach (var t in Tools.All) ToolRow(content, t, ref y);
            Row(content, Item.Shield, ref y);
            Row(content, Item.Armor, ref y);
            Row(content, Item.Rescue, ref y);
            Row(content, Item.Magnet, ref y);
            Row(content, Item.Hover, ref y);
            Row(content, Item.Lives, ref y);
            y -= 20f;
            Header(content, Loc.T("ws.paint"), ref y);
            PaintRow(content, ref y);
            y -= 20f;
            Header(content, Loc.T("ws.counter"), ref y);
            foreach (var b in Shop.Counter()) Row(content, (Item)((int)Item.StartShield + (int)b), ref y);
            Row(content, Item.Life, ref y);
            Row(content, Item.Tunnel, ref y);
            content.sizeDelta = new Vector2(0f, -y + 40f);
        }

        /// <summary>The hammer: five levels, each hitting harder, holding more blows and looking different.</summary>
        private void WeaponRow(Transform parent, ref float y)
        {
            var row = UiFactory.Pill("Hammer", parent, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(980f, 170f), new Color(0.3f, 0.2f, 0.45f, 0.95f));
            y -= 186f;
            var icon = UiFactory.Box("Icon", row, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(120f, 120f));
            var iconFill = UiFactory.Fill(icon, HammerModels.Glow(Weapons.Level), UiSprites.Rounded, 2f);
            iconFill.raycastTarget = false;
            var handle = UiFactory.Box("Handle", icon, new Vector2(0.5f, 0.5f), new Vector2(-6f, -12f), new Vector2(16f, 70f));
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.localRotation = Quaternion.Euler(0f, 0f, 35f);
            UiFactory.Fill(handle, UiFactory.TextDark, UiSprites.Rounded, 8f).raycastTarget = false;
            var head = UiFactory.Box("Head", icon, new Vector2(0.5f, 0.5f), new Vector2(10f, 16f), new Vector2(70f, 34f));
            head.pivot = new Vector2(0.5f, 0.5f);
            head.localRotation = Quaternion.Euler(0f, 0f, 35f);
            UiFactory.Fill(head, UiFactory.TextDark, UiSprites.Rounded, 8f).raycastTarget = false;

            var name = UiFactory.TextBox("Name", row, new Vector2(0f, 1f), new Vector2(166f, -18f), new Vector2(420f, 56f), "", 42f, Palette.UiText, align: TextAlignmentOptions.Left);
            var desc = UiFactory.TextBox("Desc", row, new Vector2(0f, 1f), new Vector2(166f, -66f), new Vector2(420f, 70f), "", 27f,
                new Color(0.85f, 0.86f, 1f, 0.8f), FontStyles.Normal, align: TextAlignmentOptions.TopLeft);
            desc.textWrappingMode = TextWrappingModes.Normal;
            var pips = new List<Image>();
            for (int i = 0; i < Weapons.MaxLevel; i++)
            {
                var pip = UiFactory.Box("Pip", row, new Vector2(0f, 0f), new Vector2(166f + i * 44f, 14f), new Vector2(34f, 12f));
                pips.Add(UiFactory.Fill(pip, Color.white, UiSprites.Rounded, 8f));
                pips[i].raycastTarget = false;
            }
            var buy = UiFactory.MakeButton(row, "", Kind.Gold, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(250f, 120f), () =>
            {
                if (Weapons.TryUpgrade())
                {
                    AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
                    Haptics.Medium();
                    row.localScale = Vector3.one * 1.05f;
                    bip.Queue(Loc.T("ws.bip.hammer"));
                    Purchased?.Invoke();
                }
                else
                {
                    AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                    if (Weapons.NextUnlocked && SaveData.Coins < Weapons.NextPrice) WorkshopTalk.Poor(bip);
                }
                Refresh();
            }, 44f);
            var label = buy.GetComponentInChildren<TextMeshProUGUI>();
            refreshers.Add(() =>
            {
                int level = Weapons.Level;
                iconFill.color = HammerModels.Glow(level);
                name.text = Loc.T("weapon.hammer." + level);
                string now = Loc.F("weapon.stats", Weapons.DamageAt(level), Weapons.AmmoAt(level));
                desc.text = Weapons.IsMaxed ? now : now + "\n" + Loc.F("weapon.next", Loc.T("weapon.hammer." + (level + 1)), Weapons.DamageAt(level + 1), Weapons.AmmoAt(level + 1));
                for (int i = 0; i < pips.Count; i++) pips[i].color = i < level ? new Color(0.36f, 0.85f, 0.6f) : new Color(1f, 1f, 1f, 0.18f);
                bool locked = !Weapons.IsMaxed && !Weapons.NextUnlocked;
                label.text = Weapons.IsMaxed ? Loc.T("shop.max") : locked ? Loc.F("ws.lockedAt", Weapons.NextUnlockLevel + 1) : Weapons.NextPrice.ToString();
                buy.interactable = !Weapons.IsMaxed && !locked;
                buy.targetGraphic.color = buy.interactable && SaveData.Coins >= Weapons.NextPrice ? UiFactory.GoldStyle.face : new Color(0.45f, 0.45f, 0.5f, 0.55f);
            });
        }

        /// <summary>The paint shelf: a door into the garage, where the robot gets its colours back.</summary>
        private void PaintRow(Transform parent, ref float y)
        {
            var row = UiFactory.Pill("Paint", parent, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(980f, 150f), new Color(0.04f, 0.04f, 0.09f, 0.66f) /* glass */);
            y -= 166f;
            var icon = UiFactory.Box("Icon", row, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(110f, 110f));
            UiFactory.Fill(icon, new Color(1f, 0.6f, 0.75f), UiSprites.Rounded, 2f).raycastTarget = false;
            foreach (var (pos, c) in new[] { (new Vector2(-20f, 14f), new Color(0.45f, 0.85f, 0.7f)), (new Vector2(20f, 14f), new Color(1f, 0.8f, 0.35f)), (new Vector2(0f, -18f), new Color(0.35f, 0.6f, 1f)) })
            {
                var drop = UiFactory.Box("Drop", icon, new Vector2(0.5f, 0.5f), pos, new Vector2(40f, 40f));
                drop.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(drop, c, UiSprites.Circle).raycastTarget = false;
            }
            UiFactory.TextBox("Name", row, new Vector2(0f, 1f), new Vector2(156f, -22f), new Vector2(440f, 56f), Loc.T("ws.paintName"), 42f, Palette.UiText, align: TextAlignmentOptions.Left);
            UiFactory.TextBox("Desc", row, new Vector2(0f, 1f), new Vector2(156f, -68f), new Vector2(440f, 70f), Loc.T("ws.paintDesc"), 28f,
                new Color(0.85f, 0.86f, 1f, 0.75f), FontStyles.Normal, align: TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
            UiFactory.MakeButton(row, Loc.T("ws.paintOpen"), Kind.Primary, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(250f, 110f), () => PaintPressed?.Invoke(), 40f);
        }

        private static void Header(Transform root, string text, ref float y)
        {
            var label = UiFactory.TextBox("Header", root, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(960f, 70f), text, 40f, Palette.UiCyan, align: TextAlignmentOptions.Left);
            label.characterSpacing = 4f;
            y -= 80f;
        }

        private void Row(Transform root, Item item, ref float y)
        {
            var row = UiFactory.Pill(item.ToString(), root, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(980f, 150f), new Color(0.04f, 0.04f, 0.09f, 0.6f) /* glass */);
            y -= 166f;

            var icon = UiFactory.Box("Icon", row, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(110f, 110f));
            UiFactory.Fill(icon, IconColor(item), UiSprites.Rounded, 2f).raycastTarget = false;
            DrawIcon(icon, item);

            string key = "shop." + item;
            UiFactory.TextBox("Name", row, new Vector2(0f, 1f), new Vector2(156f, -22f), new Vector2(380f, 56f), Loc.T(key), 42f, Palette.UiText, align: TextAlignmentOptions.Left);
            var desc = UiFactory.TextBox("Desc", row, new Vector2(0f, 1f), new Vector2(156f, -68f), new Vector2(380f, 40f), Loc.T(key + ".desc"), 30f,
                new Color(0.85f, 0.86f, 1f, 0.75f), FontStyles.Normal, align: TextAlignmentOptions.Left);

            // Level pips (upgrades) or the owned count (items).
            var pips = new List<Image>();
            TextMeshProUGUI owned = null;
            if (IsUpgrade(item))
            {
                int max = Shop.MaxLevel(ToUpgrade(item));
                for (int i = 0; i < max; i++)
                {
                    var pip = UiFactory.Box("Pip", row, new Vector2(0f, 0f), new Vector2(156f + i * 44f, 14f), new Vector2(34f, 12f));
                    pips.Add(UiFactory.Fill(pip, Color.white, UiSprites.Rounded, 8f));
                    pips[i].raycastTarget = false;
                }
            }
            else if (IsBoost(item))
            {
                owned = UiFactory.TextBox("Owned", row, new Vector2(0f, 0f), new Vector2(156f, 6f), new Vector2(300f, 32f), "", 26f, Palette.UiCyan, align: TextAlignmentOptions.Left);
            }

            // Saving up: how many coins are still missing, with a little bar filling up.
            var need = UiFactory.TextBox("Need", row, new Vector2(0f, 0.5f), new Vector2(556f, 14f), new Vector2(150f, 50f), "", 24f, new Color(1f, 1f, 1f, 0.7f), FontStyles.Normal);
            var needBar = UiFactory.Bar(row, new Vector2(0f, 0.5f), new Vector2(556f, -22f), new Vector2(150f, 14f), new Color(1f, 1f, 1f, 0.12f), out var needFill);

            var buy = UiFactory.MakeButton(row, "", Kind.Gold, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(250f, 110f), () => Buy(item, row), 46f);
            var label = buy.GetComponentInChildren<TextMeshProUGUI>();

            refreshers.Add(() =>
            {
                int price = PriceOf(item);
                bool maxed = IsUpgrade(item) && Shop.IsMaxed(ToUpgrade(item));
                bool locked = IsUpgrade(item) && !Shop.Unlocked(ToUpgrade(item));
                bool full = item == Item.Life && Lives.IsFull || IsBoost(item) && Shop.Full(ToBoost(item));
                label.text = locked ? Loc.F("ws.lockedAt", Shop.UnlockLevel(ToUpgrade(item)) + 1) : maxed ? Loc.T("shop.max") : full ? Loc.T("shop.full") : price.ToString();
                buy.interactable = !locked && !maxed && !full;
                buy.targetGraphic.color = !locked && !maxed && !full && SaveData.Coins >= price ? UiFactory.GoldStyle.face : new Color(0.45f, 0.45f, 0.5f, 0.55f);
                if (IsUpgrade(item))
                {
                    int level = Shop.Level(ToUpgrade(item));
                    for (int i = 0; i < pips.Count; i++) pips[i].color = i < level ? new Color(0.36f, 0.85f, 0.6f) : new Color(1f, 1f, 1f, 0.18f);
                }
                if (owned != null) owned.text = Loc.F("shop.owned", Shop.Owned(ToBoost(item)));
                desc.text = locked ? Loc.T("ws.src." + ToUpgrade(item)) : Describe(item);
                bool saving = !locked && !maxed && !full && SaveData.Coins < price;
                need.gameObject.SetActive(saving);
                needBar.gameObject.SetActive(saving);
                if (saving)
                {
                    need.text = Loc.F("shop.need", price - SaveData.Coins);
                    UiFactory.SetBar(needFill, SaveData.Coins / (float)price);
                }
            });
        }

        /// <summary>The bag itself: two slots showing the tools that ride along; the second slot is bought once.</summary>
        private void SlotsRow(Transform parent, ref float y)
        {
            var row = UiFactory.Pill("Bag", parent, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(980f, 150f), new Color(0.04f, 0.04f, 0.09f, 0.66f) /* glass */);
            y -= 166f;
            UiFactory.TextBox("Label", row, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(240f, 60f), Loc.T("shop.bag"), 36f, Palette.UiText, align: TextAlignmentOptions.Left);

            for (int s = 0; s < 2; s++)
            {
                int slot = s;
                var box = UiFactory.Box("Slot" + s, row, new Vector2(0f, 0.5f), new Vector2(270f + s * 350f, 0f), new Vector2(330f, 120f));
                box.pivot = new Vector2(0f, 0.5f);
                UiFactory.Fill(box, new Color(0.04f, 0.04f, 0.09f, 0.6f) /* glass */, UiSprites.Rounded, 1.4f);
                var icon = UiFactory.Box("Icon", box, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(92f, 92f));
                icon.pivot = new Vector2(0f, 0.5f);
                var iconFill = UiFactory.Fill(icon, Palette.UiGold, UiSprites.Rounded, 2f);
                iconFill.raycastTarget = false;
                var label = UiFactory.TextBox("Name", box, new Vector2(0f, 0.5f), new Vector2(118f, 0f), new Vector2(200f, 90f), "", 30f, Palette.UiText, align: TextAlignmentOptions.Left);
                label.textWrappingMode = TextWrappingModes.Normal;

                Button buySlot = null;
                if (s == 1)
                {
                    buySlot = UiFactory.MakeButton(box, Tools.SecondSlotPrice.ToString(), Kind.Gold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 100f), () =>
                    {
                        if (Tools.TryBuySecondSlot()) { AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f); Purchased?.Invoke(); }
                        else AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                        Refresh();
                    }, 40f);
                    ((RectTransform)buySlot.transform).pivot = new Vector2(0.5f, 0.5f);
                }

                Tool? drawn = null;
                bool drawnEmpty = false;
                refreshers.Add(() =>
                {
                    bool locked = slot >= Tools.Slots;
                    if (buySlot != null)
                    {
                        buySlot.gameObject.SetActive(locked);
                        buySlot.interactable = SaveData.Coins >= Tools.SecondSlotPrice;
                    }
                    icon.gameObject.SetActive(!locked);
                    label.gameObject.SetActive(!locked);
                    if (locked) return;
                    var t = Tools.Equipped(slot);
                    label.text = t.HasValue ? Loc.T("tool." + t.Value) : Loc.T("shop.bagEmpty");
                    iconFill.color = t.HasValue ? Palette.UiGold : new Color(1f, 1f, 1f, 0.12f);
                    if (drawn != t || drawnEmpty != !t.HasValue)
                    {
                        for (int i = icon.childCount - 1; i >= 0; i--) Destroy(icon.GetChild(i).gameObject);
                        if (t.HasValue) ToolButton.DrawIcon(icon, t.Value);
                        drawn = t;
                        drawnEmpty = !t.HasValue;
                    }
                });
            }
        }

        /// <summary>A tool: unlock, upgrade (three levels), put in or take out of the bag.</summary>
        private void ToolRow(Transform parent, Tool tool, ref float y)
        {
            var row = UiFactory.Pill(tool.ToString(), parent, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(980f, 150f), new Color(0.04f, 0.04f, 0.09f, 0.6f) /* glass */);
            y -= 166f;

            var icon = UiFactory.Box("Icon", row, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(110f, 110f));
            UiFactory.Fill(icon, Palette.UiGold, UiSprites.Rounded, 2f).raycastTarget = false;
            ToolButton.DrawIcon(icon, tool);

            UiFactory.TextBox("Name", row, new Vector2(0f, 1f), new Vector2(156f, -22f), new Vector2(380f, 56f), Loc.T("tool." + tool), 42f, Palette.UiText, align: TextAlignmentOptions.Left);
            var desc = UiFactory.TextBox("Desc", row, new Vector2(0f, 1f), new Vector2(156f, -68f), new Vector2(380f, 40f), "", 28f,
                new Color(0.85f, 0.86f, 1f, 0.75f), FontStyles.Normal, align: TextAlignmentOptions.Left);
            var pips = new List<Image>();
            for (int i = 0; i < Tools.MaxLevel; i++)
            {
                var pip = UiFactory.Box("Pip", row, new Vector2(0f, 0f), new Vector2(156f + i * 44f, 14f), new Vector2(34f, 12f));
                pips.Add(UiFactory.Fill(pip, Color.white, UiSprites.Rounded, 8f));
                pips[i].raycastTarget = false;
            }

            // Middle button: WEAR / TAKE OFF once owned; before that, how many coins are still missing.
            var need = UiFactory.TextBox("Need", row, new Vector2(0f, 0.5f), new Vector2(556f, 14f), new Vector2(150f, 50f), "", 24f, new Color(1f, 1f, 1f, 0.7f), FontStyles.Normal);
            var needBar = UiFactory.Bar(row, new Vector2(0f, 0.5f), new Vector2(556f, -22f), new Vector2(150f, 14f), new Color(1f, 1f, 1f, 0.12f), out var needFill);
            var mid = UiFactory.MakeButton(row, "", Kind.Secondary, new Vector2(0f, 0.5f), new Vector2(548f, 0f), new Vector2(150f, 64f), () =>
            {
                if (Tools.Owned(tool)) Tools.ToggleEquip(tool);
                AudioManager.PlaySfx(Sfx.Click, 0.7f, 1.2f);
                Refresh();
            }, 26f);
            var midLabel = mid.GetComponentInChildren<TextMeshProUGUI>();

            var buy = UiFactory.MakeButton(row, "", Kind.Gold, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(250f, 110f), () =>
            {
                bool first = !Tools.Owned(tool);
                if (Tools.TryBuy(tool))
                {
                    AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
                    Haptics.Medium();
                    row.localScale = Vector3.one * 1.05f;
                    WorkshopTalk.ToolBought(bip, tool, first);
                    Purchased?.Invoke();
                }
                else
                {
                    AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                    if (Tools.Unlocked(tool) && !Tools.IsMaxed(tool)) WorkshopTalk.Poor(bip);
                }
                Refresh();
            }, 46f);
            var buyLabel = buy.GetComponentInChildren<TextMeshProUGUI>();

            refreshers.Add(() =>
            {
                int level = Tools.Level(tool);
                for (int i = 0; i < pips.Count; i++) pips[i].color = i < level ? new Color(0.36f, 0.85f, 0.6f) : new Color(1f, 1f, 1f, 0.18f);
                bool maxed = Tools.IsMaxed(tool);
                int price = Tools.NextPrice(tool);
                bool locked = !Tools.Unlocked(tool);
                buyLabel.text = locked ? Loc.F("ws.lockedAt", Tools.UnlockLevel(tool) + 1) : maxed ? Loc.T("shop.max") : price.ToString();
                buy.interactable = !locked && !maxed;
                buy.targetGraphic.color = !locked && !maxed && SaveData.Coins >= price ? UiFactory.GoldStyle.face : new Color(0.45f, 0.45f, 0.5f, 0.55f);
                desc.text = locked ? Loc.T("ws.src." + tool) : Loc.T(level == 0 ? "tool." + tool + ".desc" : level < Tools.MaxLevel ? "tool." + tool + ".next" : "tool.maxed");
                bool ownedTool = Tools.Owned(tool);
                mid.gameObject.SetActive(ownedTool);
                if (ownedTool)
                {
                    bool on = Tools.IsEquipped(tool);
                    midLabel.text = Loc.T(on ? "tool.off" : "tool.on");
                    midLabel.color = on ? Palette.UiCyan : Palette.UiText;
                }
                bool saving = !locked && !ownedTool && SaveData.Coins < price;
                need.gameObject.SetActive(saving);
                needBar.gameObject.SetActive(saving);
                if (saving)
                {
                    need.text = Loc.F("shop.need", price - SaveData.Coins);
                    UiFactory.SetBar(needFill, SaveData.Coins / (float)price);
                }
            });
        }

        private static bool IsUpgrade(Item item) => item <= Item.Armor;
        private static bool IsBoost(Item item) => item >= Item.StartShield && item <= Item.TunnelBoost;
        private static Boost ToBoost(Item item) => (Boost)(item - Item.StartShield);
        private static Upgrade ToUpgrade(Item item) => (Upgrade)(int)item;

        private static int PriceOf(Item item)
        {
            switch (item)
            {
                case Item.StartShield:
                case Item.ExtraRescue:
                case Item.StartHammer:
                case Item.DoubleCoins:
                case Item.CoinMagnet:
                case Item.TunnelBoost: return Shop.Price(ToBoost(item));
                case Item.Life: return Shop.LifePrice;
                case Item.Tunnel: return Shop.TunnelPrice;
                default: return Shop.NextPrice(ToUpgrade(item));
            }
        }

        /// <summary>The row's second line: what the next level brings (or what the item does).</summary>
        private static string Describe(Item item)
        {
            switch (item)
            {
                case Item.Shield:
                    return Shop.IsMaxed(Upgrade.Shield) ? Loc.F("shop.Shield.now", Shop.ShieldHits) : Loc.T("shop.Shield.desc");
                case Item.Magnet:
                    return Shop.MagnetRange > 0 ? Loc.F("shop.Magnet.now", Shop.MagnetRange) : Loc.T("shop.Magnet.desc");
                case Item.Hover:
                    return Loc.F("shop.Hover.now", Shop.HoverSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                case Item.Lives:
                    return Loc.F("shop.Lives.now", Shop.MaxLives);
                case Item.Armor:
                    return Shop.Level(Upgrade.Armor) > 0 ? Loc.F("shop.Armor.now", Mathf.RoundToInt(Shop.ArmorShare * 100f)) : Loc.T("shop.Armor.desc");
                case Item.Rescue:
                    return Shop.Level(Upgrade.Rescue) > 0 ? Loc.F("shop.Rescue.now", Shop.MaxRescues) : Loc.T("shop.Rescue.desc");
                default:
                    return Loc.T("shop." + item + ".desc");
            }
        }

        private void Buy(Item item, RectTransform row)
        {
            bool ok;
            switch (item)
            {
                case Item.StartShield:
                case Item.ExtraRescue:
                case Item.StartHammer:
                case Item.DoubleCoins:
                case Item.CoinMagnet:
                case Item.TunnelBoost: ok = Shop.TryBuy(ToBoost(item)); break;
                case Item.Life:
                    ok = !Lives.IsFull && Shop.Spend(Shop.LifePrice);
                    if (ok) Lives.Add(1);
                    break;
                case Item.Tunnel:
                    ok = Shop.Spend(Shop.TunnelPrice);
                    if (ok) Progress.BonusTokens++;
                    break;
                default: ok = Shop.TryBuy(ToUpgrade(item)); break;
            }

            if (ok)
            {
                AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
                Haptics.Medium();
                row.localScale = Vector3.one * 1.05f;
                if (IsUpgrade(item)) WorkshopTalk.UpgradeBought(bip, ToUpgrade(item));
                else if (IsBoost(item)) WorkshopTalk.CounterBought(bip);
                Purchased?.Invoke();
            }
            else
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                if (SaveData.Coins < PriceOf(item)) WorkshopTalk.Poor(bip);
            }
            Refresh();
        }

        public void Show()
        {
            if (builtDay != System.DateTime.Today.DayOfYear) Populate();
            Refresh();
            screen.Show();
            if (WorkshopTalk.IntroDue) WorkshopTalk.Welcome(bip);
        }

        public void Hide()
        {
            bip.Hide();
            screen.Hide(true);
        }

        private void Refresh()
        {
            coinsText.text = SaveData.Coins.ToString();
            foreach (var r in refreshers) r();
        }

        private void Update()
        {
            if (!screen.IsVisible) return;
            foreach (Transform child in content)
                if (child.localScale.x > 1f) child.localScale = Vector3.Lerp(child.localScale, Vector3.one, Time.unscaledDeltaTime * 10f);
        }

        // ---------- Icons ----------

        private static Color IconColor(Item item)
        {
            switch (item)
            {
                case Item.Shield:
                case Item.StartShield: return new Color(0.45f, 0.75f, 1f);
                case Item.Armor: return new Color(0.75f, 0.8f, 0.9f);
                case Item.Magnet:
                case Item.CoinMagnet: return new Color(1f, 0.55f, 0.55f);
                case Item.Hover: return new Color(0.55f, 0.9f, 0.75f);
                case Item.Lives:
                case Item.Life: return new Color(1f, 0.55f, 0.65f);
                case Item.Rescue:
                case Item.ExtraRescue: return new Color(0.75f, 0.65f, 1f);
                case Item.TunnelBoost: return new Color(0.55f, 0.9f, 0.75f);
                case Item.StartHammer: return ThunderHammer.Electric;
                default: return Palette.UiGold;
            }
        }

        private static void DrawIcon(RectTransform icon, Item item)
        {
            var dark = UiFactory.TextDark;
            switch (item)
            {
                case Item.Shield:
                case Item.StartShield:
                case Item.Armor:
                {
                    var ring = UiFactory.Box("Ring", icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
                    ring.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Fill(ring, dark, UiSprites.Ring, 0.6f).raycastTarget = false;
                    var core = UiFactory.Box("Core", icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
                    core.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Fill(core, dark, UiSprites.Circle).raycastTarget = false;
                    break;
                }
                case Item.Magnet:
                case Item.CoinMagnet:
                    foreach (float x in new[] { -18f, 18f })
                    {
                        var leg = UiFactory.Box("Leg", icon, new Vector2(0.5f, 0.5f), new Vector2(x, 6f), new Vector2(18f, 50f));
                        leg.pivot = new Vector2(0.5f, 0.5f);
                        UiFactory.Fill(leg, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    }
                    var bridge = UiFactory.Box("Bridge", icon, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(54f, 18f));
                    bridge.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Fill(bridge, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    break;
                case Item.Hover:
                    foreach (float r in new[] { 40f, -40f })
                    {
                        var bar = UiFactory.Box("Arrow", icon, new Vector2(0.5f, 0.5f), new Vector2(r > 0 ? -11f : 11f, 4f), new Vector2(14f, 46f));
                        bar.pivot = new Vector2(0.5f, 0.5f);
                        bar.localRotation = Quaternion.Euler(0f, 0f, r);
                        UiFactory.Fill(bar, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    }
                    break;
                case Item.Lives:
                case Item.Life:
                    UIController.HeartIcon(icon, new Vector2(55f, 0f), 62f);
                    break;
                case Item.Rescue:
                case Item.ExtraRescue:
                    foreach (bool vertical in new[] { true, false })
                    {
                        var bar = UiFactory.Box("Plus", icon, new Vector2(0.5f, 0.5f), Vector2.zero, vertical ? new Vector2(18f, 60f) : new Vector2(60f, 18f));
                        bar.pivot = new Vector2(0.5f, 0.5f);
                        UiFactory.Fill(bar, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    }
                    break;
                case Item.StartHammer:
                {
                    var handle = UiFactory.Box("Handle", icon, new Vector2(0.5f, 0.5f), new Vector2(-6f, -10f), new Vector2(16f, 66f));
                    handle.pivot = new Vector2(0.5f, 0.5f);
                    handle.localRotation = Quaternion.Euler(0f, 0f, 35f);
                    UiFactory.Fill(handle, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    var head = UiFactory.Box("Head", icon, new Vector2(0.5f, 0.5f), new Vector2(10f, 16f), new Vector2(66f, 30f));
                    head.pivot = new Vector2(0.5f, 0.5f);
                    head.localRotation = Quaternion.Euler(0f, 0f, 35f);
                    UiFactory.Fill(head, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    break;
                }
                case Item.DoubleCoins:
                    UiFactory.TextBox("X2", icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 90f), "x2", 56f, dark, title: true)
                        .rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    break;
                default:
                {
                    var ring = UiFactory.Box("Vent", icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 74f));
                    ring.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Fill(ring, dark, UiSprites.Ring, 0.5f).raycastTarget = false;
                    break;
                }
            }
        }
    }
}
