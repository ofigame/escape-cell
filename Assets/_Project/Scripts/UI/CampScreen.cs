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
    /// The roof camp screen: the camp itself in 3D above, and below a scrolling panel with the daily harvest,
    /// the rescued friends (tap one to take it along in the tunnels) and the decorations to buy.
    /// </summary>
    public class CampScreen : MonoBehaviour
    {
        public event Action BackPressed;
        /// <summary>The daily harvest was collected (coins).</summary>
        public event Action<int> Harvested;
        public event Action<CampDecor> DecorBought;

        private UiScreen screen;
        private TextMeshProUGUI coinsText, harvestLabel, friendsLabel;
        private Button harvestButton;
        private RectTransform content;
        private readonly List<Action> refreshers = new List<Action>();

        public UiScreen Screen => screen;

        public static CampScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("Camp", canvasRoot, out var root);
            var camp = root.gameObject.AddComponent<CampScreen>();
            camp.screen = screen;
            camp.Build(root);
            return camp;
        }

        private void Build(RectTransform root)
        {
            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -190f), Vector2.zero);
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(124f, 124f), () => BackPressed?.Invoke(), 64f);
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 110f), Loc.T("camp.title"), 60f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var coins = UiFactory.Pill("Coins", bar, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(260f, 100f), UiFactory.PillColor);
            UIController.CoinIcon(coins, new Vector2(56f, 0f));
            coinsText = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(150f, 90f), "0", 50f, Palette.UiGold, align: TextAlignmentOptions.Left);

            // Lower panel, scrolling.
            var panel = UiFactory.Card("Panel", root, new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 20f), new Vector2(1000f, 820f));
            var viewport = UiFactory.Rect("Viewport", panel, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;

            float y = -20f;
            harvestButton = UiFactory.MakeButton(content, "", Kind.Gold, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(640f, 120f), Harvest, 48f);
            harvestLabel = harvestButton.GetComponentInChildren<TextMeshProUGUI>();
            y -= 150f;

            friendsLabel = UiFactory.TextBox("Friends", content, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(940f, 60f), "", 34f, Palette.UiCyan, align: TextAlignmentOptions.Left);
            y -= 70f;
            const int columns = 7;
            const float cell = 132f;
            for (int i = 0; i < Camp.Friends.Length; i++)
            {
                int index = i;
                int col = i % columns, row = i / columns;
                var tile = UiFactory.Box("Friend" + i, content, new Vector2(0f, 1f), new Vector2(14f + col * cell, y - row * (cell + 30f)), new Vector2(120f, 150f));
                var bg = UiFactory.Fill(tile, new Color(0.12f, 0.1f, 0.26f, 0.9f), UiSprites.Rounded, 1.4f);
                var ring = UiFactory.Fill(UiFactory.Stretch("Ring", tile), Palette.UiGold, UiSprites.Ring, 1.4f);
                ring.raycastTarget = false;
                var face = UiFactory.Box("Face", tile, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(84f, 74f));
                var faceFill = UiFactory.Fill(face, RobotLooks.BodyColor(Camp.FriendWorld(i)) * 1.1f, UiSprites.Rounded, 3f);
                faceFill.raycastTarget = false;
                var visor = UiFactory.Box("Visor", face, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(62f, 38f));
                visor.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(visor, new Color(0.22f, 0.25f, 0.38f), UiSprites.Rounded, 4f).raycastTarget = false;
                foreach (float ex in new[] { -13f, 13f })
                {
                    var eye = UiFactory.Box("Eye", visor, new Vector2(0.5f, 0.5f), new Vector2(ex, 0f), new Vector2(12f, 14f));
                    eye.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Fill(eye, Palette.UiCyan, UiSprites.Rounded, 12f).raycastTarget = false;
                }
                var name = UiFactory.TextBox("Name", tile, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(116f, 40f), "", 26f, Palette.UiText);
                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                button.onClick.AddListener(() => PickCompanion(index));
                tile.gameObject.AddComponent<ButtonPress>();

                refreshers.Add(() =>
                {
                    bool freed = Camp.Rescued(index);
                    face.gameObject.SetActive(freed);
                    name.text = freed ? Camp.Friends[index] : Loc.F("camp.floor", index + 2);
                    name.color = freed ? Palette.UiText : new Color(1f, 1f, 1f, 0.4f);
                    ring.gameObject.SetActive(Camp.Companion == index);
                });
            }
            y -= Mathf.CeilToInt(Camp.Friends.Length / (float)columns) * (cell + 30f) + 20f;

            UiFactory.TextBox("DecorTitle", content, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(940f, 60f), Loc.T("camp.decor"), 34f, Palette.UiCyan, align: TextAlignmentOptions.Left);
            y -= 70f;
            for (int i = 0; i < Camp.Decor.Length; i++)
            {
                var d = Camp.Decor[i];
                int col = i % 3, row = i / 3;
                var tile = UiFactory.Box(d.id, content, new Vector2(0f, 1f), new Vector2(14f + col * 312f, y - row * 150f), new Vector2(300f, 138f));
                UiFactory.Fill(tile, new Color(0.16f, 0.15f, 0.33f, 0.9f), UiSprites.Rounded, 1.4f);
                UiFactory.TextBox("Name", tile, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(280f, 50f), Loc.T("camp." + d.id), 32f, Palette.UiText);
                var buy = UiFactory.MakeButton(tile, "", Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(220f, 66f), () => Buy(d), 34f);
                var label = buy.GetComponentInChildren<TextMeshProUGUI>();
                refreshers.Add(() =>
                {
                    bool owned = Camp.Owns(d);
                    label.text = owned ? Loc.T("camp.built") : d.price.ToString();
                    buy.interactable = !owned && SaveData.Coins >= d.price;
                });
            }
            y -= Mathf.CeilToInt(Camp.Decor.Length / 3f) * 150f + 20f;
            content.sizeDelta = new Vector2(0f, -y);
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
            friendsLabel.text = Loc.F("camp.friends", Camp.RescuedCount, Camp.Friends.Length);
            bool can = Camp.CanHarvest;
            harvestLabel.text = can ? Loc.F("camp.harvest", Camp.DailyCoins) : Loc.F("camp.tomorrow", Camp.DailyCoins);
            harvestButton.interactable = can;
            foreach (var r in refreshers) r();
        }

        private void Harvest()
        {
            int coins = Camp.Harvest();
            if (coins <= 0) return;
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.2f);
            Haptics.Medium();
            Harvested?.Invoke(coins);
            Refresh();
        }

        private void Buy(CampDecor d)
        {
            if (!Camp.TryBuy(d))
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                return;
            }
            AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
            Haptics.Medium();
            DecorBought?.Invoke(d);
            Refresh();
        }

        private void PickCompanion(int i)
        {
            if (!Camp.Rescued(i))
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.5f);
                return;
            }
            Camp.Companion = Camp.Companion == i ? -1 : i;
            AudioManager.PlaySfx(Sfx.Click, 0.7f, 1.3f);
            Refresh();
        }
    }
}
