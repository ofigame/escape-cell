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

        private UiScreen screen;
        private TextMeshProUGUI coinsText;
        private readonly List<Action> refreshers = new List<Action>();

        public UiScreen Screen => screen;

        private enum Item { Shield, Magnet, Hover, Lives, StartShield, ExtraRescue, Life, Tunnel }

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
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(520f, 110f), Loc.T("shop.title"), 64f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var coins = UiFactory.Pill("Coins", bar, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(260f, 100f), UiFactory.PillColor);
            UIController.CoinIcon(coins, new Vector2(56f, 0f));
            coinsText = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 90f), "0", 50f, Palette.UiGold, align: TextAlignmentOptions.Left);

            float y = -220f;
            Header(root, Loc.T("shop.upgrades"), ref y);
            Row(root, Item.Shield, ref y);
            Row(root, Item.Magnet, ref y);
            Row(root, Item.Hover, ref y);
            Row(root, Item.Lives, ref y);
            y -= 20f;
            Header(root, Loc.T("shop.boosts"), ref y);
            Row(root, Item.StartShield, ref y);
            Row(root, Item.ExtraRescue, ref y);
            Row(root, Item.Life, ref y);
            Row(root, Item.Tunnel, ref y);
        }

        private static void Header(Transform root, string text, ref float y)
        {
            var label = UiFactory.TextBox("Header", root, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(960f, 70f), text, 40f, Palette.UiCyan, align: TextAlignmentOptions.Left);
            label.characterSpacing = 4f;
            y -= 80f;
        }

        private void Row(Transform root, Item item, ref float y)
        {
            var row = UiFactory.Pill(item.ToString(), root, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(980f, 150f), new Color(0.16f, 0.15f, 0.33f, 0.9f));
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
            else if (item == Item.StartShield || item == Item.ExtraRescue)
            {
                owned = UiFactory.TextBox("Owned", row, new Vector2(0f, 0f), new Vector2(156f, 6f), new Vector2(300f, 32f), "", 26f, Palette.UiCyan, align: TextAlignmentOptions.Left);
            }

            // Mark as the goal to save up for (shown on every result card).
            string goalId = IsUpgrade(item) ? Goal.ForUpgrade(ToUpgrade(item)) : item == Item.StartShield ? Goal.ForBoost(Boost.StartShield) : item == Item.ExtraRescue ? Goal.ForBoost(Boost.ExtraRescue) : null;
            TextMeshProUGUI goalLabel = null;
            GameObject goalButton = null;
            if (goalId != null)
            {
                var gb = UiFactory.MakeButton(row, "", Kind.Secondary, new Vector2(0f, 0.5f), new Vector2(548f, 0f), new Vector2(150f, 64f), () =>
                {
                    Goal.Toggle(goalId);
                    AudioManager.PlaySfx(Sfx.Click, 0.7f, 1.2f);
                    Refresh();
                }, 26f);
                goalLabel = gb.GetComponentInChildren<TextMeshProUGUI>();
                goalButton = gb.gameObject;
            }

            var buy = UiFactory.MakeButton(row, "", Kind.Gold, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(250f, 110f), () => Buy(item, row), 46f);
            var label = buy.GetComponentInChildren<TextMeshProUGUI>();

            refreshers.Add(() =>
            {
                int price = PriceOf(item);
                bool maxed = IsUpgrade(item) && Shop.IsMaxed(ToUpgrade(item));
                bool full = item == Item.Life && Lives.IsFull;
                label.text = maxed ? Loc.T("shop.max") : full ? Loc.T("shop.full") : price.ToString();
                buy.interactable = !maxed && !full && SaveData.Coins >= price;
                if (IsUpgrade(item))
                {
                    int level = Shop.Level(ToUpgrade(item));
                    for (int i = 0; i < pips.Count; i++) pips[i].color = i < level ? new Color(0.36f, 0.85f, 0.6f) : new Color(1f, 1f, 1f, 0.18f);
                }
                if (owned != null) owned.text = Loc.F("shop.owned", Shop.Owned(item == Item.StartShield ? Boost.StartShield : Boost.ExtraRescue));
                desc.text = Describe(item);
                if (goalLabel != null)
                {
                    goalButton.SetActive(!maxed);
                    goalLabel.text = Loc.T(Goal.Is(goalId) ? "goal.on" : "goal.set");
                    goalLabel.color = Goal.Is(goalId) ? Palette.UiGold : Palette.UiText;
                }
            });
        }

        private static bool IsUpgrade(Item item) => item <= Item.Lives;
        private static Upgrade ToUpgrade(Item item) => (Upgrade)(int)item;

        private static int PriceOf(Item item)
        {
            switch (item)
            {
                case Item.StartShield: return Shop.Price(Boost.StartShield);
                case Item.ExtraRescue: return Shop.Price(Boost.ExtraRescue);
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
                default:
                    return Loc.T("shop." + item + ".desc");
            }
        }

        private void Buy(Item item, RectTransform row)
        {
            bool ok;
            switch (item)
            {
                case Item.StartShield: ok = Shop.TryBuy(Boost.StartShield); break;
                case Item.ExtraRescue: ok = Shop.TryBuy(Boost.ExtraRescue); break;
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
                Purchased?.Invoke();
            }
            else
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
            }
            Refresh();
        }

        public void Show()
        {
            Refresh();
            screen.Show();
        }

        public void Hide() => screen.Hide(true);

        private void Refresh()
        {
            coinsText.text = SaveData.Coins.ToString();
            foreach (var r in refreshers) r();
        }

        private void Update()
        {
            if (!screen.IsVisible) return;
            foreach (Transform child in transform)
                if (child.localScale.x > 1f) child.localScale = Vector3.Lerp(child.localScale, Vector3.one, Time.unscaledDeltaTime * 10f);
        }

        // ---------- Icons ----------

        private static Color IconColor(Item item)
        {
            switch (item)
            {
                case Item.Shield:
                case Item.StartShield: return new Color(0.45f, 0.75f, 1f);
                case Item.Magnet: return new Color(1f, 0.55f, 0.55f);
                case Item.Hover: return new Color(0.55f, 0.9f, 0.75f);
                case Item.Lives:
                case Item.Life: return new Color(1f, 0.55f, 0.65f);
                case Item.ExtraRescue: return new Color(0.75f, 0.65f, 1f);
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
                case Item.ExtraRescue:
                    foreach (bool vertical in new[] { true, false })
                    {
                        var bar = UiFactory.Box("Plus", icon, new Vector2(0.5f, 0.5f), Vector2.zero, vertical ? new Vector2(18f, 60f) : new Vector2(60f, 18f));
                        bar.pivot = new Vector2(0.5f, 0.5f);
                        UiFactory.Fill(bar, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    }
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
