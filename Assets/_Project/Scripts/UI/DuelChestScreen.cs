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
        private Button takeButton, equipButton, swapButton;
        private WeaponDef gifted;
        private Cosmetic giftedOutfit;
        private readonly TextMeshProUGUI[] counts = new TextMeshProUGUI[DuelChests.Tiers];
        private readonly Button[] openButtons = new Button[DuelChests.Tiers];
        private readonly TextMeshProUGUI[] openLabels = new TextMeshProUGUI[DuelChests.Tiers];
        private readonly RawImage[] pictures = new RawImage[DuelChests.Tiers];
        private bool busy;

        /// <summary>A gift was taken (the coins on the menu may need a refresh).</summary>
        public event Action Changed;
        /// <summary>An outfit from a chest was put on (foi dresses in it).</summary>
        public event Action OutfitWorn;

        public bool IsOpen => screen.IsVisible || opening.IsVisible || (contents != null && contents.IsVisible);

        public static DuelChestScreen Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("DuelChests", canvasRoot, out var root);
            var d = root.gameObject.AddComponent<DuelChestScreen>();
            d.screen = s;
            d.Build(root);
            d.opening = UiScreen.Create("ChestOpening", canvasRoot, out var openRoot);
            d.BuildOpening(openRoot);
            d.contents = UiScreen.Create("ChestContents", canvasRoot, out var contentsRoot);
            d.contents.BackAction = () => d.contents.Hide(); // (the Android back key: the same on every screen)
            d.BuildContents(contentsRoot);
            return d;
        }

        public static string TierName(ChestTier t) => Loc.T("chests.tier" + (int)t);

        private void Build(RectTransform root)
        {
            Flat.Background(root);
            var top = new Vector2(0.5f, 1f);
            headerCoins = UiKit.ScreenHeader(root, screen, Loc.T("chests.title"), null);
            freeLine = Flat.Label(root, "", top, new Vector2(0f, -212f), new Vector2(960f, 50f), 30f, Flat.Gold, TextAlignmentOptions.Center);
            freeLine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < DuelChests.Tiers; i++) Card(root, (ChestTier)i, -250f - i * 296f);
            var bottom = new Vector2(0.5f, 0f);
            // a plain chest for an ad (the player's choice); the hint moves up under the title
            adChest = Flat.Button(root, "", bottom, new Vector2(0f, Ads.BannerReserve + 80f), new Vector2(720f, 100f), new Color(0.62f, 0.4f, 1f), Color.white, AdChest, 32f);
            ((RectTransform)adChest.transform).pivot = new Vector2(0.5f, 0.5f);
            adChestLabel = adChest.GetComponentInChildren<TextMeshProUGUI>();
        }

        private TMPro.TextMeshProUGUI headerCoins;

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
            var odds = Flat.Label(row, DuelChests.OddsText(t), new Vector2(0f, 0.5f), new Vector2(300f, -92f), new Vector2(380f, 64f), 22f, Flat.Muted, wrap: true);
            odds.rectTransform.pivot = new Vector2(0f, 0.5f);
            // what may come out of it, with pictures (a reason to win the next duel)
            var see = Flat.Button(row, Loc.T("chests.contents"), new Vector2(1f, 0.5f), new Vector2(-30f, -92f), new Vector2(250f, 64f), Flat.CardHi, Flat.Text, () => ShowContents(t), 24f);
            ((RectTransform)see.transform).pivot = new Vector2(1f, 0.5f);
            openButtons[i] = Flat.Button(row, Loc.T("chests.open"), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(250f, 120f), new Color(0.2f, 0.72f, 0.36f), Color.white, () => TryOpen(t), 38f);
            ((RectTransform)openButtons[i].transform).pivot = new Vector2(1f, 0.5f);
            openLabels[i] = openButtons[i].GetComponentInChildren<TextMeshProUGUI>();
        }

        // ---------- What a chest may hold ----------

        private UiScreen contents;
        private RectTransform contentsGrid;
        private TextMeshProUGUI contentsTitle, contentsOdds;

        private void BuildContents(RectTransform root)
        {
            Flat.Background(root);
            var top = new Vector2(0.5f, 1f);
            contentsTitle = Flat.Label(root, "", top, new Vector2(0f, -80f), new Vector2(960f, 80f), 54f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            contentsTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            contentsOdds = Flat.Label(root, "", top, new Vector2(0f, -200f), new Vector2(960f, 140f), 26f, Flat.Muted, TextAlignmentOptions.Center, wrap: true);
            contentsOdds.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var viewport = UiFactory.Rect("Viewport", root, Vector2.zero, Vector2.one, new Vector2(30f, Ads.BannerReserve + 150f), new Vector2(-30f, -350f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            contentsGrid = UiFactory.Rect("Grid", viewport, new Vector2(0f, 1f), Vector2.one);
            contentsGrid.pivot = new Vector2(0.5f, 1f);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = contentsGrid; scroll.horizontal = false;
            var close = Flat.Button(root, Loc.T("btn.close"), new Vector2(0.5f, 0f), new Vector2(0f, Ads.BannerReserve + 70f), new Vector2(520f, 110f), Flat.Blue, Color.white, () => contents.Hide(), 42f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>The chest's possible gifts, each with its picture and name: its own rare weapons, shields and capes
        /// first, the workshop's weapons and the gold after.</summary>
        private void ShowContents(ChestTier t)
        {
            for (int i = contentsGrid.childCount - 1; i >= 0; i--) Destroy(contentsGrid.GetChild(i).gameObject);
            contentsTitle.text = TierName(t);
            contentsOdds.text = DuelChests.OddsText(t) + "\n" + DuelChests.DetailText(t);
            int n = 0;
            void Cell(Texture pic, string name, bool rare) => CellDrawn(box => { var raw = box.gameObject.AddComponent<RawImage>(); raw.texture = pic; raw.raycastTarget = false; }, name, rare);
            void CellDrawn(System.Action<RectTransform> draw, string name, bool rare)
            {
                const int Cols = 3; const float W = 300f, H = 360f, Gap = 18f;
                float x = (n % Cols - (Cols - 1) * 0.5f) * (W + Gap), y = -(n / Cols) * (H + Gap);
                var card = Flat.Panel(contentsGrid, "Gift", new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(W, H), rare ? new Color(0.32f, 0.22f, 0.08f) : Flat.Card);
                card.pivot = new Vector2(0.5f, 1f);
                var box = UiFactory.Box("Picture", card, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(250f, 250f));
                box.pivot = new Vector2(0.5f, 0.5f);
                draw(box);
                var label = Flat.Label(card, name, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(W - 20f, 70f), 26f, rare ? Flat.Gold : Flat.Text, TextAlignmentOptions.Center, bold: true, wrap: true);
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                n++;
            }
            if (t == ChestTier.Advanced)
            {
                foreach (var w in Armory.All) if (w.chest) Cell(ItemPreview.Weapon(w), Loc.T("weapon." + w.id), true);
                foreach (var s in Shields.All) if (s.chest) Cell(ItemPreview.Shield(s), Loc.T(s.id), true);
            }
            // the bag's items beside every draw (the best rarity this chest can hold each in; the repair potions it gives)
            var cat = ItemCatalog.Current;
            int ti = (int)t;
            var best = cat.chestEpic[ti] > 0f ? ItemRarity.Epic : cat.chestRare[ti] > 0f ? ItemRarity.Rare : ItemRarity.Common;
            foreach (var k in ItemBag.Kinds)
            {
                if (k == ItemKind.RepairSmall && cat.chestRepair[ti] <= 0f || k == ItemKind.RepairLarge && cat.chestGreatRepair[ti] <= 0f) continue;
                var r = ItemBag.HasRarity(k) ? best : ItemRarity.Common;
                CellDrawn(box => { ItemIcon.Draw(box, k, r); foreach (var g in box.GetComponentsInChildren<Graphic>()) g.raycastTarget = false; }, ItemBagScreen.NameOf(k), r == ItemRarity.Epic);
            }
            // (the chests' own outfits: every chest may hold one, the plain one more rarely)
            foreach (var c in Cosmetics.All) if (c.chest) Cell(ItemPreview.Cosmetic(c), Loc.T("cos." + c.id), true);
            var (lo, hi) = DuelChests.ShopTierRange(t); // (the very tiers the chest draws from)
            foreach (var w in Armory.All)
                if (!w.Special && !w.chest && w.price > 0 && w.tier >= lo && w.tier <= hi && w.kind != WeaponKind.Gun) Cell(ItemPreview.Weapon(w), Loc.T("weapon." + w.id), false);
            contentsGrid.sizeDelta = new Vector2(0f, ((n + 2) / 3) * 378f + 30f);
            contentsGrid.anchoredPosition = Vector2.zero;
            contents.Show();
            contents.transform.SetAsLastSibling();
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
            openNote = Flat.Label(root, "", mid, new Vector2(0f, -540f), new Vector2(960f, 110f), 30f, Flat.Gold, TextAlignmentOptions.Center);
            openNote.textWrappingMode = TextWrappingModes.Normal; openNote.enableAutoSizing = true; openNote.fontSizeMin = 18f; openNote.fontSizeMax = 30f; // (with the items it may run to two lines)
            openNote.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            takeButton = Flat.Button(root, Loc.T("chests.take"), new Vector2(0.5f, 0f), new Vector2(0f, Ads.BannerReserve + 120f), new Vector2(600f, 130f), new Color(0.2f, 0.72f, 0.36f), Color.white, Take, 46f);
            ((RectTransform)takeButton.transform).pivot = new Vector2(0.5f, 0.5f);
            equipButton = Flat.Button(root, Loc.T("chests.equip"), new Vector2(0.5f, 0f), new Vector2(-180f, Ads.BannerReserve + 120f), new Vector2(340f, 130f), new Color(0.62f, 0.4f, 1f), Color.white, Equip, 44f);
            ((RectTransform)equipButton.transform).pivot = new Vector2(0.5f, 0.5f);
            // Not happy with it? An ad swaps it for another draw (once).
            swapButton = Flat.Button(root, "▶ " + Loc.T("chests.swap"), new Vector2(0.5f, 0f), new Vector2(0f, Ads.BannerReserve + 270f), new Vector2(600f, 110f), new Color(0.95f, 0.6f, 0.15f), Color.white, Swap, 38f);
            ((RectTransform)swapButton.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>EQUIP: the weapon that came out goes straight into foi's hand (a gun into the gun slot).</summary>
        private void Equip()
        {
            var wonShield = shown != null ? shown.shield : null;
            DuelChests.Give(shown); // (owned first, then worn)
            shown = null;
            if (wonShield != null) { Shields.Equip(wonShield); OutfitWorn?.Invoke(); }
            if (gifted != null) Armory.Equip(gifted);
            if (giftedOutfit != null) { Cosmetics.Equip(giftedOutfit); OutfitWorn?.Invoke(); }
            Take();
        }

        public void Show()
        {
            if (!busy && DuelChests.GivePending()) Changed?.Invoke(); // (a gift left when the game closed during a reveal)
            for (int i = 0; i < DuelChests.Tiers; i++) pictures[i].texture = ChestStage.Thumbnail((ChestTier)i);
            Refresh();
            screen.Show();
            UiKit.RefreshCurrency(headerCoins);
        }

        private void Refresh()
        {
            int free = DuelChests.FreeLeft;
            freeLine.text = DuelChests.FreePerDay > 0 ? Loc.F("chests.free", free, DuelChests.FreePerDay) : Loc.T("chests.hint");
            freeLine.fontSize = DuelChests.FreePerDay > 0 ? 30f : 24f;
            int left = DuelChests.AdChestsLeft, wait = DuelChests.AdChestWait;
            adChest.gameObject.SetActive(left > 0 && Ads.Rewarded.IsReady);
            adChest.interactable = wait == 0;
            adChestLabel.text = wait == 0 ? Loc.F("chests.adFree", left, DuelChests.AdChestsADay) : Loc.F("chests.adWait", wait);
            for (int i = 0; i < DuelChests.Tiers; i++)
            {
                int n = DuelChests.Count((ChestTier)i);
                counts[i].text = n > 0 ? "x" + n : Loc.T("chests.none");
                openButtons[i].interactable = n > 0;
                openLabels[i].text = Loc.T("chests.open");
                pictures[i].color = n > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        private Button adChest;
        private TextMeshProUGUI adChestLabel;

        /// <summary>FREE CHEST: an ad watched to the end gives a plain chest.</summary>
        private void AdChest()
        {
            if (busy || DuelChests.AdChestsLeft <= 0 || DuelChests.AdChestWait > 0) return;
            if (!Ads.Rewarded.IsReady) { Refresh(); return; }
            adChest.interactable = false;
            Ads.Rewarded.Show(rewarded =>
            {
                if (rewarded) { Ads.MarkFullScreenShown(); DuelChests.TakeAdChest(); Audio.AudioManager.PlaySfx(Audio.Sfx.Coin, 1f, 1.1f); Changed?.Invoke(); }
                Refresh();
            });
        }

        /// <summary>OPEN: free, always (an ad only swaps what came out, if the player wants).</summary>
        private void TryOpen(ChestTier t)
        {
            if (busy || DuelChests.Count(t) <= 0) return;
            var reward = DuelChests.Open(t);
            if (reward == null) { Refresh(); return; }
            openedTier = t;
            swapped = false;
            Reveal(t, reward);
        }

        private ChestTier openedTier;
        private ChestReward shown;
        private bool swapped;

        /// <summary>SWAP: after an ad watched to the end, the chest is drawn again (once).</summary>
        private void Swap()
        {
            if (swapped || shown == null) return;
            if (!Ads.Rewarded.IsReady) { openNote.text = Loc.T("chests.noAd"); return; }
            swapButton.interactable = false;
            Ads.Rewarded.Show(rewarded =>
            {
                if (!rewarded) { swapButton.interactable = true; return; }
                swapped = true;
                ChestStage.Get().Stop();
                Reveal(openedTier, DuelChests.Redraw(openedTier, shown));
            });
        }

        private void Reveal(ChestTier t, ChestReward reward)
        {
            shown = reward;
            busy = true;
            openTitle.text = TierName(t);
            openGift.text = "";
            openNote.text = "";
            takeButton.gameObject.SetActive(false);
            equipButton.gameObject.SetActive(false);
            swapButton.gameObject.SetActive(false);
            gifted = reward.weapon;
            giftedOutfit = reward.outfit;
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
                else if (reward.outfit != null)
                {
                    openGift.text = Loc.T("cos." + reward.outfit.id);
                    openNote.text = reward.outfit.chest ? Loc.T("chests.outfitExclusive") : Loc.T("chests.outfit");
                }
                else if (reward.shield != null)
                {
                    openGift.text = Loc.T(reward.shield.id);
                    openNote.text = Loc.T("chests.exclusive");
                }
                else openGift.text = Loc.F("chests.gold", reward.gold) + (reward.cores > 0 ? " + " + Loc.F("login.cores", reward.cores) : "");
                // the items beside it (already in the bag)
                if (reward.items != null && reward.items.Count > 0) openNote.text = (openNote.text.Length > 0 ? openNote.text + "\n" : "") + "+ " + ItemDrops.Text(reward.items);
                // A weapon: EQUIP on the left, TAKE THE GIFT on the right; gold: TAKE THE GIFT alone, in the middle.
                var take = (RectTransform)takeButton.transform;
                bool wearable = gifted != null || giftedOutfit != null || reward.shield != null;
                take.anchoredPosition = new Vector2(wearable ? 180f : 0f, take.anchoredPosition.y);
                take.sizeDelta = new Vector2(wearable ? 340f : 600f, 130f);
                equipButton.gameObject.SetActive(wearable);
                takeButton.gameObject.SetActive(true);
                swapButton.gameObject.SetActive(!swapped);
                swapButton.interactable = true;
            });
        }

        private void Take()
        {
            DuelChests.Give(shown); // (given only now: a swapped draw never gave the first)
            shown = null;
            ChestStage.Get().Stop();
            opening.Hide();
            busy = false;
            Refresh();
            Changed?.Invoke();
        }
    }
}
