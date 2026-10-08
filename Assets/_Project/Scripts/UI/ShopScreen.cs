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
    /// Bip's workshop as a grid of big cards on four shelves (tabs): weapons (3D pictures of hammers, swords, axes,
    /// maces and spears, each opening at a level), tools, upgrades and the daily counter. Every card shows a big picture,
    /// what it does, and its price (greyed when it can't be afforded, locked with the level it opens at).
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

        private enum Tab { Weapons, Tools, Upgrades, Counter }

        private const float CellW = 470f, CellH = 650f, Gap = 30f, TabBarH = 120f;
        private Tab tab = Tab.Weapons;
        private readonly Dictionary<Tab, Image> tabFaces = new Dictionary<Tab, Image>();
        private ScrollRect scroll;
        private int cells;

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
            UiFactory.Fill(UiFactory.Stretch("Backdrop", root), new Color(0.03f, 0.03f, 0.07f, 0.94f)).gameObject.AddComponent<IgnoreSafeArea>();

            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -190f), Vector2.zero);
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(124f, 124f), () => BackPressed?.Invoke(), 64f);
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(520f, 110f), Loc.T("ws.title"), 56f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var coins = UiFactory.Pill("Coins", bar, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(260f, 100f), UiFactory.PillColor);
            UIController.CoinIcon(coins, new Vector2(56f, 0f));
            coinsText = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 90f), "0", 50f, Palette.UiGold, align: TextAlignmentOptions.Left);

            // The shelves as tabs.
            var tabs = UiFactory.Rect("Tabs", root, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -190f - TabBarH), new Vector2(-24f, -200f));
            var names = new[] { (Tab.Weapons, "ws.weapons"), (Tab.Tools, "ws.tools"), (Tab.Upgrades, "ws.upgrades"), (Tab.Counter, "ws.counter") };
            for (int i = 0; i < names.Length; i++)
            {
                var (t, key) = names[i];
                var b = UiFactory.MakeButton(tabs, Loc.T(key), Kind.Secondary, new Vector2((i + 0.5f) / names.Length, 0.5f), Vector2.zero, new Vector2(236f, 92f), () => SetTab(t), 30f);
                ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
                tabFaces[t] = (Image)b.targetGraphic;
            }

            // The grid of cards below scrolls.
            var viewport = UiFactory.Rect("Viewport", root, Vector2.zero, Vector2.one, new Vector2(0f, Monetization.Ads.BannerReserve), new Vector2(0f, -200f - TabBarH));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;

            bip = BipTip.Create(root);
        }

        private void SetTab(Tab t)
        {
            tab = t;
            Populate();
            Refresh();
            content.anchoredPosition = Vector2.zero;
        }

        /// <summary>Fills the open shelf with its cards (big picture, name, what it does, price or equip).</summary>
        private void Populate()
        {
            builtDay = System.DateTime.Today.DayOfYear;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            refreshers.Clear();
            cells = 0;
            foreach (var kv in tabFaces) kv.Value.color = kv.Key == tab ? UiFactory.CyanStyle.face : UiFactory.PurpleStyle.face;

            switch (tab)
            {
                case Tab.Weapons:
                    PackCell();
                    foreach (var w in Armory.All) WeaponCell(w);
                    break;
                case Tab.Tools:
                    PackCell();
                    BagCell();
                    foreach (var t in Tools.All) ToolCell(t);
                    break;
                case Tab.Upgrades:
                    foreach (var item in new[] { Item.Shield, Item.Armor, Item.Rescue, Item.Magnet, Item.Hover, Item.Lives }) ItemCell(item);
                    PaintCell();
                    break;
                default:
                    foreach (var b in Shop.Counter()) ItemCell((Item)((int)Item.StartShield + (int)b));
                    ItemCell(Item.Life);
                    ItemCell(Item.Tunnel);
                    break;
            }
            int rows = (cells + 1) / 2;
            content.sizeDelta = new Vector2(0f, rows * (CellH + Gap) + 60f);
        }

        // ---------- Cards ----------

        /// <summary>A card at the next place of the two-column grid, with its big picture area filled in.</summary>
        private RectTransform Cell(string name, Color glow, out RectTransform picture)
        {
            int i = cells++;
            float x = (i % 2 == 0 ? -1f : 1f) * (CellW + Gap) * 0.5f;
            float y = -30f - (i / 2) * (CellH + Gap);
            var card = UiFactory.Card(name, content, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(CellW, CellH));
            card.pivot = new Vector2(0.5f, 1f);
            // The picture sits on a soft glow of the item's colour.
            var halo = UiFactory.Box("Halo", card, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(380f, 380f));
            halo.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(halo, new Color(glow.r, glow.g, glow.b, 0.35f), UiSprites.Shadow, 0.5f).raycastTarget = false;
            picture = UiFactory.Box("Picture", card, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(300f, 300f));
            picture.pivot = new Vector2(0.5f, 0.5f);
            return card;
        }

        /// <summary>A shop icon (drawn for a 110-unit box) blown up to fill the picture.</summary>
        private static RectTransform BigIcon(RectTransform picture, Color colour)
        {
            var disc = UiFactory.Box("Disc", picture, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f));
            disc.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Chunky(disc, UiFactory.StyleOf(colour), 1f, 0f, UiSprites.Circle).raycastTarget = false;
            var icon = UiFactory.Box("Icon", picture, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.localScale = Vector3.one * 1.9f;
            return icon;
        }

        private static TextMeshProUGUI CardName(RectTransform card, string text) =>
            UiFactory.TextBox("Name", card, new Vector2(0.5f, 1f), new Vector2(0f, -352f), new Vector2(430f, 56f), text, 38f, Palette.UiText, title: true);

        private static TextMeshProUGUI CardDesc(RectTransform card)
        {
            var d = UiFactory.TextBox("Desc", card, new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(420f, 96f), "", 25f, new Color(0.88f, 0.9f, 1f, 0.82f), FontStyles.Normal);
            d.textWrappingMode = TextWrappingModes.Normal;
            d.alignment = TextAlignmentOptions.Top;
            return d;
        }

        private static List<Image> Pips(RectTransform card, int count)
        {
            var pips = new List<Image>();
            float w = 40f, gap = 10f, start = -(count * w + (count - 1) * gap) * 0.5f + w * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var pip = UiFactory.Box("Pip", card, new Vector2(0.5f, 0f), new Vector2(start + i * (w + gap), 140f), new Vector2(w, 12f));
                pip.pivot = new Vector2(0.5f, 0.5f);
                pips.Add(UiFactory.Fill(pip, Color.white, UiSprites.Rounded, 8f));
                pips[i].raycastTarget = false;
            }
            return pips;
        }

        private static void SetPips(List<Image> pips, int level)
        {
            for (int i = 0; i < pips.Count; i++) pips[i].color = i < level ? new Color(0.36f, 0.85f, 0.6f) : new Color(1f, 1f, 1f, 0.18f);
        }

        /// <summary>A grey veil with a padlock and the level it opens at (shown while locked).</summary>
        private static TextMeshProUGUI LockVeil(RectTransform card, out GameObject veil)
        {
            var v = UiFactory.Box("Lock", card, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(300f, 300f));
            v.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(v, new Color(0.02f, 0.02f, 0.05f, 0.7f), UiSprites.Circle).raycastTarget = false;
            var label = UiFactory.TextBox("Text", v, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 90f), "", 34f, Color.white, title: true);
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            veil = v.gameObject;
            return label;
        }

        private Button BuyButton(RectTransform card, Action onClick, float width = 410f, float x = 0f) =>
            UiFactory.MakeButton(card, "", Kind.Gold, new Vector2(0.5f, 0f), new Vector2(x, 34f), new Vector2(width, 92f), onClick, 40f);

        private static void Afford(Button b, bool can, bool enough)
        {
            b.interactable = can;
            b.targetGraphic.color = can && enough ? UiFactory.GoldStyle.face : new Color(0.45f, 0.45f, 0.5f, 0.55f);
        }

        private void Pop(RectTransform card) => card.localScale = Vector3.one * 1.05f;

        /// <summary>A weapon: its picture, how it hits, buy it (opens at its level) or take it in hand.</summary>
        private void WeaponCell(WeaponDef w)
        {
            var card = Cell(w.id, WeaponModels.Glow(w.tier), out var picture);
            var image = picture.gameObject.AddComponent<RawImage>();
            image.texture = ItemPreview.Weapon(w);
            image.raycastTarget = false;
            CardName(card, Loc.T("weapon." + w.id));
            var desc = CardDesc(card);
            desc.text = Loc.F("weapon.line", w.damage, w.reach, Loc.T(w.cooldown <= 0.16f ? "weapon.fast" : w.cooldown >= 0.3f ? "weapon.slow" : "weapon.normal"))
                        + "\n" + Loc.T("weapon.kind." + w.kind) + "  ·  " + Loc.F("pack.weight", Backpack.Weight(w));
            var lockText = LockVeil(card, out var veil);
            var buy = BuyButton(card, () =>
            {
                if (Armory.Owned(w)) Armory.Equip(w);
                else if (Armory.TryBuy(w))
                {
                    AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
                    Haptics.Medium();
                    Pop(card);
                    bip.Queue(Loc.T("ws.bip.hammer"));
                    Purchased?.Invoke();
                }
                else
                {
                    AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                    if (Armory.Unlocked(w) && !Backpack.Fits(Backpack.Weight(w))) bip.Queue(Loc.F("pack.tooFull", Backpack.LevelNeeded(Backpack.Weight(w))));
                    else if (Armory.Unlocked(w) && SaveData.Coins < w.price) WorkshopTalk.Poor(bip);
                }
                Refresh();
            });
            var label = buy.GetComponentInChildren<TextMeshProUGUI>();
            refreshers.Add(() =>
            {
                bool owned = Armory.Owned(w), unlocked = Armory.Unlocked(w), equipped = Armory.Equipped == w;
                veil.SetActive(!unlocked);
                lockText.text = Loc.F("ws.lockedAt", w.unlockAt + 1);
                image.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                bool fits = Backpack.Fits(Backpack.Weight(w));
                label.text = equipped ? Loc.T("weapon.equipped") : owned ? Loc.T("weapon.equip") : !unlocked ? Loc.F("ws.lockedAt", w.unlockAt + 1)
                    : fits ? w.price.ToString() : Loc.F("pack.needs", Backpack.LevelNeeded(Backpack.Weight(w)));
                if (owned)
                {
                    buy.interactable = !equipped;
                    buy.targetGraphic.color = equipped ? UiFactory.GreenStyle.face : UiFactory.CyanStyle.face;
                }
                else
                {
                    Afford(buy, unlocked, fits && SaveData.Coins >= w.price);
                    buy.interactable = unlocked; // a tap on a full backpack still says why
                }
            });
        }

        private void ItemCell(Item item)
        {
            var card = Cell(item.ToString(), IconColor(item), out var picture);
            DrawIcon(BigIcon(picture, IconColor(item)), item);
            string key = "shop." + item;
            CardName(card, Loc.T(key));
            var desc = CardDesc(card);
            var pips = IsUpgrade(item) ? Pips(card, Shop.MaxLevel(ToUpgrade(item))) : null;
            TextMeshProUGUI owned = null;
            if (IsBoost(item))
            {
                owned = UiFactory.TextBox("Owned", card, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(400f, 36f), "", 26f, Palette.UiCyan);
                owned.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            var buy = BuyButton(card, () => Buy(item, card));
            var label = buy.GetComponentInChildren<TextMeshProUGUI>();
            refreshers.Add(() =>
            {
                int price = PriceOf(item);
                bool maxed = IsUpgrade(item) && Shop.IsMaxed(ToUpgrade(item));
                bool locked = IsUpgrade(item) && !Shop.Unlocked(ToUpgrade(item));
                bool full = item == Item.Life && Lives.IsFull || IsBoost(item) && Shop.Full(ToBoost(item));
                label.text = locked ? Loc.F("ws.lockedAt", Shop.UnlockLevel(ToUpgrade(item)) + 1) : maxed ? Loc.T("shop.max") : full ? Loc.T("shop.full") : price.ToString();
                Afford(buy, !locked && !maxed && !full, SaveData.Coins >= price);
                if (pips != null) SetPips(pips, Shop.Level(ToUpgrade(item)));
                if (owned != null) owned.text = Loc.F("shop.owned", Shop.Owned(ToBoost(item)));
                desc.text = locked ? Loc.T("ws.src." + ToUpgrade(item)) : Describe(item);
            });
        }

        /// <summary>A tool: unlock, upgrade (three levels), put in or take out of the bag.</summary>
        private void ToolCell(Tool tool)
        {
            var card = Cell(tool.ToString(), Palette.UiGold, out var picture);
            ToolButton.DrawIcon(BigIcon(picture, Palette.UiGold), tool);
            CardName(card, Loc.T("tool." + tool));
            var desc = CardDesc(card);
            var pips = Pips(card, Tools.MaxLevel);
            var lockText = LockVeil(card, out var veil);
            var mid = UiFactory.MakeButton(card, "", Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(-108f, 34f), new Vector2(196f, 92f), () =>
            {
                if (Tools.Owned(tool)) Tools.ToggleEquip(tool);
                AudioManager.PlaySfx(Sfx.Click, 0.7f, 1.2f);
                Refresh();
            }, 30f);
            var midLabel = mid.GetComponentInChildren<TextMeshProUGUI>();
            var buy = BuyButton(card, () =>
            {
                bool first = !Tools.Owned(tool);
                if (Tools.TryBuy(tool))
                {
                    AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
                    Haptics.Medium();
                    Pop(card);
                    WorkshopTalk.ToolBought(bip, tool, first);
                    Purchased?.Invoke();
                }
                else
                {
                    AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                    if (Tools.Unlocked(tool) && !Tools.IsMaxed(tool) && !Backpack.Fits(Backpack.Weight(tool)))
                        bip.Queue(Loc.F("pack.tooFull", Backpack.LevelNeeded(Backpack.Weight(tool))));
                    else if (Tools.Unlocked(tool) && !Tools.IsMaxed(tool)) WorkshopTalk.Poor(bip);
                }
                Refresh();
            });
            var buyLabel = buy.GetComponentInChildren<TextMeshProUGUI>();
            var buyRect = (RectTransform)buy.transform;
            refreshers.Add(() =>
            {
                int level = Tools.Level(tool);
                SetPips(pips, level);
                bool maxed = Tools.IsMaxed(tool), locked = !Tools.Unlocked(tool), ownedTool = Tools.Owned(tool);
                int price = Tools.NextPrice(tool);
                veil.SetActive(locked);
                lockText.text = Loc.F("ws.lockedAt", Tools.UnlockLevel(tool) + 1);
                bool fits = Backpack.Fits(Backpack.Weight(tool));
                buyLabel.text = locked ? Loc.F("ws.lockedAt", Tools.UnlockLevel(tool) + 1) : maxed ? Loc.T("shop.max")
                    : fits ? price.ToString() : Loc.F("pack.needs", Backpack.LevelNeeded(Backpack.Weight(tool)));
                Afford(buy, !locked && !maxed, fits && SaveData.Coins >= price);
                desc.text = locked ? Loc.T("ws.src." + tool) : Loc.T(level == 0 ? "tool." + tool + ".desc" : level < Tools.MaxLevel ? "tool." + tool + ".next" : "tool.maxed")
                    + (locked || maxed ? "" : "  ·  " + Loc.F("pack.weight", Backpack.Weight(tool)));
                // Owned: the wear/take-off button shares the bottom with the upgrade button.
                mid.gameObject.SetActive(ownedTool);
                buyRect.sizeDelta = new Vector2(ownedTool ? 196f : 410f, 92f);
                buyRect.anchoredPosition = new Vector2(ownedTool ? 108f : 0f, 34f);
                if (ownedTool)
                {
                    bool on = Tools.IsEquipped(tool);
                    midLabel.text = Loc.T(on ? "tool.off" : "tool.on");
                    mid.targetGraphic.color = on ? UiFactory.GreenStyle.face : UiFactory.PurpleStyle.face;
                }
            });
        }

        /// <summary>The bag: which tools ride along (two slots; the second is bought once).</summary>
        private void BagCell()
        {
            var card = Cell("Bag", Palette.UiCyan, out var picture);
            CardName(card, Loc.T("shop.bag"));
            var desc = CardDesc(card);
            var slotIcons = new RectTransform[2];
            for (int s = 0; s < 2; s++)
            {
                var box = UiFactory.Box("Slot" + s, picture, new Vector2(0.5f, 0.5f), new Vector2((s == 0 ? -1f : 1f) * 78f, 0f), new Vector2(140f, 140f));
                box.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(box, new Color(1f, 1f, 1f, 0.08f), UiSprites.Rounded, 1f).raycastTarget = false;
                UiFactory.Fill(UiFactory.Stretch("Rim", box), new Color(1f, 1f, 1f, 0.3f), UiSprites.Ring, 1f).raycastTarget = false;
                var icon = UiFactory.Box("Icon", box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
                icon.pivot = new Vector2(0.5f, 0.5f);
                slotIcons[s] = icon;
            }
            var buySlot = BuyButton(card, () =>
            {
                if (Tools.TryBuySecondSlot()) { AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f); Purchased?.Invoke(); }
                else AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                Refresh();
            });
            var label = buySlot.GetComponentInChildren<TextMeshProUGUI>();
            var drawn = new Tool?[2];
            var drawnEmpty = new bool[2];
            refreshers.Add(() =>
            {
                bool second = Tools.Slots >= 2;
                buySlot.gameObject.SetActive(!second);
                label.text = Tools.SecondSlotPrice.ToString();
                Afford(buySlot, true, SaveData.Coins >= Tools.SecondSlotPrice);
                var names = new List<string>();
                for (int s = 0; s < 2; s++)
                {
                    var t = s < Tools.Slots ? Tools.Equipped(s) : null;
                    if (t.HasValue) names.Add(Loc.T("tool." + t.Value));
                    if (drawn[s] == t && drawnEmpty[s] == !t.HasValue) continue;
                    for (int i = slotIcons[s].childCount - 1; i >= 0; i--) Destroy(slotIcons[s].GetChild(i).gameObject);
                    if (t.HasValue) ToolButton.DrawIcon(slotIcons[s], t.Value);
                    drawn[s] = t;
                    drawnEmpty[s] = !t.HasValue;
                }
                desc.text = names.Count > 0 ? string.Join(" · ", names) : Loc.T("shop.bagEmpty");
            });
        }

        /// <summary>
        /// The backpack: its level, how full it is, and the upgrade to the next level (more room for bigger weapons and
        /// tools, and for more skill orbs).
        /// </summary>
        private void PackCell()
        {
            var card = Cell("Backpack", new Color(1f, 0.62f, 0.3f), out var picture);
            var icon = UiFactory.Box("Icon", picture, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(110f, 110f));
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.localScale = Vector3.one * 2.2f;
            DrawBackpack(icon);
            var levelText = UiFactory.TextBox("Level", picture, new Vector2(0.5f, 0f), new Vector2(0f, -6f), new Vector2(300f, 60f), "", 40f, Palette.UiGold, title: true);
            levelText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            CardName(card, Loc.T("pack.name"));
            var desc = CardDesc(card);
            // How full it is.
            var track = UiFactory.Box("Track", card, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(400f, 22f));
            track.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(track, new Color(1f, 1f, 1f, 0.14f), UiSprites.Rounded, 6f).raycastTarget = false;
            var bar = UiFactory.Box("Bar", track, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 22f));
            bar.pivot = new Vector2(0f, 0.5f);
            var barImage = UiFactory.Fill(bar, new Color(1f, 0.7f, 0.3f), UiSprites.Rounded, 6f);
            barImage.raycastTarget = false;
            var buy = BuyButton(card, () =>
            {
                if (Backpack.TryUpgrade())
                {
                    AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
                    Haptics.Medium();
                    Pop(card);
                    bip.Queue(Loc.F("pack.bip", Backpack.Level));
                    Purchased?.Invoke();
                }
                else
                {
                    AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                    if (!Backpack.IsMaxed) WorkshopTalk.Poor(bip);
                }
                Refresh();
            });
            var label = buy.GetComponentInChildren<TextMeshProUGUI>();
            refreshers.Add(() =>
            {
                int used = Backpack.Used, cap = Backpack.Capacity;
                levelText.text = Loc.F("pack.level", Backpack.Level);
                bar.sizeDelta = new Vector2(400f * Mathf.Clamp01(used / (float)cap), 22f);
                barImage.color = used >= cap ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 0.7f, 0.3f);
                desc.text = Loc.F("pack.room", used, cap, Backpack.OrbCapacity)
                            + (Backpack.IsMaxed ? "" : "\n" + Loc.F("pack.next", Backpack.CapacityAt(Backpack.Level + 1)));
                label.text = Backpack.IsMaxed ? Loc.T("shop.max") : Backpack.NextPrice.ToString();
                Afford(buy, !Backpack.IsMaxed, SaveData.Coins >= Backpack.NextPrice);
            });
        }

        /// <summary>A backpack icon for a 110-unit box: body, flap, pocket and straps.</summary>
        public static void DrawBackpack(RectTransform icon)
        {
            void Part(string name, Vector2 pos, Vector2 size, Color c, Sprite s = null)
            {
                var r = UiFactory.Box(name, icon, new Vector2(0.5f, 0.5f), pos, size);
                r.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(r, c, s ?? UiSprites.Rounded, 3f).raycastTarget = false;
            }
            var dark = new Color(0.55f, 0.3f, 0.12f);
            Part("Handle", new Vector2(0f, 42f), new Vector2(34f, 20f), dark);
            Part("Body", new Vector2(0f, -4f), new Vector2(84f, 92f), new Color(1f, 0.62f, 0.28f));
            Part("Flap", new Vector2(0f, 26f), new Vector2(84f, 34f), new Color(0.95f, 0.5f, 0.2f));
            Part("Pocket", new Vector2(0f, -24f), new Vector2(54f, 34f), new Color(0.9f, 0.46f, 0.18f));
            Part("Buckle", new Vector2(0f, 10f), new Vector2(16f, 16f), new Color(1f, 0.88f, 0.45f));
            Part("StrapL", new Vector2(-30f, -4f), new Vector2(8f, 80f), dark);
            Part("StrapR", new Vector2(30f, -4f), new Vector2(8f, 80f), dark);
        }

        /// <summary>The paint workshop: a door into the garage.</summary>
        private void PaintCell()
        {
            var card = Cell("Paint", new Color(1f, 0.6f, 0.75f), out var picture);
            foreach (var (pos, c) in new[] { (new Vector2(-55f, 35f), new Color(0.45f, 0.85f, 0.7f)), (new Vector2(55f, 35f), new Color(1f, 0.8f, 0.35f)), (new Vector2(0f, -50f), new Color(0.35f, 0.6f, 1f)) })
            {
                var drop = UiFactory.Box("Drop", picture, new Vector2(0.5f, 0.5f), pos, new Vector2(110f, 110f));
                drop.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Chunky(drop, UiFactory.StyleOf(c), 1f, 0f, UiSprites.Circle).raycastTarget = false;
            }
            CardName(card, Loc.T("ws.paintName"));
            CardDesc(card).text = Loc.T("ws.paintDesc");
            var open = UiFactory.MakeButton(card, Loc.T("ws.paintOpen"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(410f, 92f), () => PaintPressed?.Invoke(), 40f);
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

        /// <summary>The card's text: what the next level brings (or what the item does).</summary>
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

        private void Buy(Item item, RectTransform card)
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
                Pop(card);
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

        /// <summary>Opens the shop on the weapons shelf, scrolled to a weapon (the "try this" hint).</summary>
        public void ShowWeapons()
        {
            tab = Tab.Weapons;
            Populate();
            Show();
        }

        /// <summary>Opens the shop on the tools/upgrades shelf (the "try this" hint).</summary>
        public void ShowShelf(bool tools)
        {
            tab = tools ? Tab.Tools : Tab.Upgrades;
            Populate();
            Show();
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

        /// <summary>An upgrade's shop icon and colour (for the "try this" hint).</summary>
        public static void DrawUpgrade(RectTransform icon, Upgrade u) => DrawIcon(icon, (Item)(int)u);
        public static Color UpgradeColor(Upgrade u) => IconColor((Item)(int)u);

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
