using System;
using SquashBot.Data;
using SquashBot.Monetization;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The duel chests won: one card for each kind (its picture, name and how many), OPEN on each — free twice a day,
    /// after that for an ad. Opening fills the screen with the chest alone: it shakes, bursts open, fades, and the gift
    /// stays turning in its place until TAKE THE GIFT.
    /// </summary>
    public class DuelChestScreen : MonoBehaviour
    {
        private UiScreen screen, opening;
        private TextMeshProUGUI freeLine, openTitle, openGift, openNote;
        private Button takeButton, equipButton;
        private WeaponDef gifted;
        private readonly TextMeshProUGUI[] counts = new TextMeshProUGUI[DuelChests.Tiers];
        private readonly Button[] openButtons = new Button[DuelChests.Tiers];
        private readonly TextMeshProUGUI[] openLabels = new TextMeshProUGUI[DuelChests.Tiers];
        private readonly RawImage[] pictures = new RawImage[DuelChests.Tiers];
        private bool busy;

        /// <summary>A gift was taken (the coins on the menu may need a refresh).</summary>
        public event Action Changed;

        public bool IsOpen => screen.IsVisible || opening.IsVisible;

        public static DuelChestScreen Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("DuelChests", canvasRoot, out var root);
            var d = root.gameObject.AddComponent<DuelChestScreen>();
            d.screen = s;
            d.Build(root);
            d.opening = UiScreen.Create("ChestOpening", canvasRoot, out var openRoot);
            d.BuildOpening(openRoot);
            return d;
        }

        public static string TierName(ChestTier t) => Loc.T("chests.tier" + (int)t);

        private void Build(RectTransform root)
        {
            Flat.Background(root);
            var top = new Vector2(0.5f, 1f);
            Flat.Label(root, Loc.T("chests.title"), top, new Vector2(0f, -70f), new Vector2(960f, 80f), 58f, Flat.Text, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            freeLine = Flat.Label(root, "", top, new Vector2(0f, -140f), new Vector2(960f, 50f), 30f, Flat.Gold, TextAlignmentOptions.Center);
            freeLine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < DuelChests.Tiers; i++) Card(root, (ChestTier)i, -190f - i * 300f);
            var bottom = new Vector2(0.5f, 0f);
            var hint = Flat.Label(root, Loc.T("chests.hint"), bottom, new Vector2(0f, Ads.BannerReserve + 200f), new Vector2(940f, 100f), 28f, Flat.Muted, TextAlignmentOptions.Center, wrap: true);
            hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var close = Flat.Button(root, Loc.T("btn.close"), bottom, new Vector2(0f, Ads.BannerReserve + 70f), new Vector2(520f, 110f), Flat.Blue, Color.white, () => screen.Hide(), 42f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        private void Card(RectTransform root, ChestTier t, float y)
        {
            int i = (int)t;
            var row = Flat.Panel(root, "Chest" + i, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(960f, 280f), Flat.Card);
            row.pivot = new Vector2(0.5f, 1f);
            var stripe = UiFactory.Box("Stripe", row, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(10f, 230f));
            stripe.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(stripe, ChestModels.StrapColour(t)).raycastTarget = false;
            var pic = UiFactory.Box("Picture", row, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(250f, 250f));
            pic.pivot = new Vector2(0f, 0.5f);
            pictures[i] = pic.gameObject.AddComponent<RawImage>();
            pictures[i].raycastTarget = false;
            Flat.Label(row, TierName(t), new Vector2(0f, 0.5f), new Vector2(300f, 70f), new Vector2(380f, 64f), 44f, Flat.Text, bold: true)
                .rectTransform.pivot = new Vector2(0f, 0.5f);
            counts[i] = Flat.Label(row, "", new Vector2(0f, 0.5f), new Vector2(300f, 10f), new Vector2(380f, 56f), 36f, ChestModels.StrapColour(t));
            counts[i].rectTransform.pivot = new Vector2(0f, 0.5f);
            // What may come out, and how likely (shown before any chest is opened).
            var odds = Flat.Label(row, Loc.T("chests.odds" + i), new Vector2(0f, 0.5f), new Vector2(300f, -92f), new Vector2(380f, 64f), 22f, Flat.Muted, wrap: true);
            odds.rectTransform.pivot = new Vector2(0f, 0.5f);
            openButtons[i] = Flat.Button(row, Loc.T("chests.open"), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(250f, 120f), new Color(0.2f, 0.72f, 0.36f), Color.white, () => TryOpen(t), 38f);
            ((RectTransform)openButtons[i].transform).pivot = new Vector2(1f, 0.5f);
            openLabels[i] = openButtons[i].GetComponentInChildren<TextMeshProUGUI>();
        }

        private void BuildOpening(RectTransform root)
        {
            Flat.Background(root); // opaque: the chest alone on the screen
            var mid = new Vector2(0.5f, 0.5f);
            openTitle = Flat.Label(root, "", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(960f, 80f), 54f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            openTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var view = UiFactory.Box("View", root, mid, new Vector2(0f, 120f), new Vector2(1000f, 1000f));
            var raw = view.gameObject.AddComponent<RawImage>();
            raw.texture = ChestStage.Get().View;
            raw.raycastTarget = false;
            openGift = Flat.Label(root, "", mid, new Vector2(0f, -440f), new Vector2(960f, 90f), 56f, Color.white, TextAlignmentOptions.Center, bold: true);
            openGift.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            openNote = Flat.Label(root, "", mid, new Vector2(0f, -520f), new Vector2(960f, 60f), 32f, Flat.Gold, TextAlignmentOptions.Center);
            openNote.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            takeButton = Flat.Button(root, Loc.T("chests.take"), new Vector2(0.5f, 0f), new Vector2(0f, Ads.BannerReserve + 120f), new Vector2(600f, 130f), new Color(0.2f, 0.72f, 0.36f), Color.white, Take, 46f);
            ((RectTransform)takeButton.transform).pivot = new Vector2(0.5f, 0.5f);
            equipButton = Flat.Button(root, Loc.T("chests.equip"), new Vector2(0.5f, 0f), new Vector2(-180f, Ads.BannerReserve + 120f), new Vector2(340f, 130f), new Color(0.62f, 0.4f, 1f), Color.white, Equip, 44f);
            ((RectTransform)equipButton.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>EQUIP: the weapon that came out goes straight into foi's hand (a gun into the gun slot).</summary>
        private void Equip()
        {
            if (gifted != null) Armory.Equip(gifted);
            Take();
        }

        public void Show()
        {
            for (int i = 0; i < DuelChests.Tiers; i++) pictures[i].texture = ChestStage.Thumbnail((ChestTier)i);
            Refresh();
            screen.Show();
        }

        private void Refresh()
        {
            int free = DuelChests.FreeLeft;
            freeLine.text = Loc.F("chests.free", free, DuelChests.FreePerDay);
            for (int i = 0; i < DuelChests.Tiers; i++)
            {
                int n = DuelChests.Count((ChestTier)i);
                counts[i].text = n > 0 ? "x" + n : Loc.T("chests.none");
                openButtons[i].interactable = n > 0;
                openLabels[i].text = free > 0 ? Loc.T("chests.open") : "▶ " + Loc.T("chests.open");
                pictures[i].color = n > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        /// <summary>OPEN: free while today's free openings last, else after an ad watched to the end.</summary>
        private void TryOpen(ChestTier t)
        {
            if (busy || DuelChests.Count(t) <= 0) return;
            if (DuelChests.FreeLeft > 0)
            {
                DuelChests.UseFree();
                Open(t);
                return;
            }
            if (!Ads.Rewarded.IsReady)
            {
                freeLine.text = Loc.T("chests.noAd");
                return;
            }
            busy = true;
            Ads.Rewarded.Show(rewarded =>
            {
                busy = false;
                if (rewarded) Open(t);
                else Refresh();
            });
        }

        private void Open(ChestTier t)
        {
            var reward = DuelChests.Open(t);
            if (reward == null) { Refresh(); return; }
            busy = true;
            openTitle.text = TierName(t);
            openGift.text = "";
            openNote.text = "";
            takeButton.gameObject.SetActive(false);
            equipButton.gameObject.SetActive(false);
            gifted = reward.weapon;
            opening.Show();
            Audio.AudioManager.PlaySfx(Audio.Sfx.Click, 0.8f, 0.7f);
            ChestStage.Get().Play(t, reward, () =>
            {
                Audio.AudioManager.PlaySfx(Audio.Sfx.Win, 0.9f);
                Audio.Haptics.Medium();
                if (reward.weapon != null)
                {
                    openGift.text = Loc.T("weapon." + reward.weapon.id);
                    openNote.text = reward.weapon.chest ? Loc.T("chests.exclusive") : Loc.T("chests.newWeapon");
                }
                else openGift.text = Loc.F("chests.gold", reward.gold);
                // A weapon: EQUIP on the left, TAKE THE GIFT on the right; gold: TAKE THE GIFT alone, in the middle.
                var take = (RectTransform)takeButton.transform;
                take.anchoredPosition = new Vector2(gifted != null ? 180f : 0f, take.anchoredPosition.y);
                take.sizeDelta = new Vector2(gifted != null ? 340f : 600f, 130f);
                equipButton.gameObject.SetActive(gifted != null);
                takeButton.gameObject.SetActive(true);
            });
        }

        private void Take()
        {
            ChestStage.Get().Stop();
            opening.Hide();
            busy = false;
            Refresh();
            Changed?.Invoke();
        }
    }
}
