using System;
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
    /// The chest at the end of a won floor, over the result card: a wooden, silver or golden chest that wobbles until
    /// it is tapped open, then its lid flies up in a burst of light and the reward shows. "Take" closes it; the gold
    /// button doubles the coins for an ad.
    /// </summary>
    public class ChestCard : MonoBehaviour
    {
        private UiScreen screen;
        private RectTransform chest, lid, glow;
        private Image body, lidFace, band, glowImage;
        private TextMeshProUGUI title, rarityLabel, reward;
        private Button open, take, doubleIt;
        private Func<string> onOpen;
        private Action onDouble;
        private float t, openedAt = -1f;
        private Color tint;

        public bool IsOpen => screen.IsVisible;

        public static ChestCard Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("Chest", canvasRoot, out var root);
            var c = root.gameObject.AddComponent<ChestCard>();
            c.screen = s;
            c.Build(root);
            return c;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.02f, 0.02f, 0.06f, 0.72f)).gameObject.AddComponent<IgnoreSafeArea>();
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 1040f));
            card.pivot = new Vector2(0.5f, 0.5f);
            title = UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(780f, 100f), Loc.T("chest.title"), 64f, Palette.UiGold, title: true);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rarityLabel = UiFactory.TextBox("Rarity", card, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(780f, 60f), "", 40f, Color.white, title: true);
            rarityLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            glow = UiFactory.Box("Glow", card, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(640f, 640f));
            glow.pivot = new Vector2(0.5f, 0.5f);
            glowImage = UiFactory.Fill(glow, new Color(1f, 0.85f, 0.4f, 0.4f), UiSprites.Circle);
            glowImage.raycastTarget = false;

            // The chest: a body with a metal band and lock, and its lid on top (it flies off when opened).
            chest = UiFactory.Box("Chest", card, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(360f, 260f));
            chest.pivot = new Vector2(0.5f, 0.5f);
            var b = UiFactory.Box("Body", chest, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(360f, 190f));
            b.pivot = new Vector2(0.5f, 0f);
            body = UiFactory.Fill(b, Color.white, UiSprites.Rounded, 22f);
            body.raycastTarget = false;
            var bd = UiFactory.Box("Band", chest, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(372f, 26f));
            bd.pivot = new Vector2(0.5f, 0.5f);
            band = UiFactory.Fill(bd, Color.white, UiSprites.Rounded, 8f);
            band.raycastTarget = false;
            var lk = UiFactory.Box("Lock", chest, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(64f, 76f));
            lk.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(lk, new Color(1f, 0.85f, 0.35f), UiSprites.Rounded, 14f).raycastTarget = false;
            lid = UiFactory.Box("Lid", chest, new Vector2(0.5f, 0f), new Vector2(0f, 188f), new Vector2(384f, 96f));
            lid.pivot = new Vector2(0.5f, 0f);
            lidFace = UiFactory.Fill(lid, Color.white, UiSprites.Rounded, 30f);
            lidFace.raycastTarget = false;

            reward = UiFactory.TextBox("Reward", card, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(780f, 140f), "", 64f, Palette.UiGold, title: true);
            reward.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            reward.textWrappingMode = TextWrappingModes.Normal;

            open = UiFactory.MakeButton(card, Loc.T("chest.open"), Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(560f, 140f), Open, 60f);
            ((RectTransform)open.transform).pivot = new Vector2(0.5f, 0.5f);
            doubleIt = UiFactory.MakeButton(card, Loc.T("chest.double"), Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(620f, 130f), () =>
            {
                doubleIt.gameObject.SetActive(false);
                onDouble?.Invoke();
            }, 46f);
            ((RectTransform)doubleIt.transform).pivot = new Vector2(0.5f, 0.5f);
            take = UiFactory.MakeButton(card, Loc.T("chest.take"), Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(420f, 90f), () => screen.Hide(), 38f);
            ((RectTransform)take.transform).pivot = new Vector2(0.5f, 0.5f);
            // A tap on the chest itself opens it too.
            var tap = chest.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => { if (openedAt < 0f) Open(); });
            var hit = chest.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
        }

        /// <param name="opened">Called on the tap: grants the reward and returns the line to show.</param>
        /// <param name="watchToDouble">Null when no ad is ready (the button stays hidden).</param>
        public void Show(ChestRarity rarity, Func<string> opened, Action watchToDouble)
        {
            onOpen = opened;
            onDouble = watchToDouble;
            tint = rarity == ChestRarity.Epic ? new Color(1f, 0.78f, 0.25f) : rarity == ChestRarity.Rare ? new Color(0.78f, 0.84f, 0.95f) : new Color(0.62f, 0.4f, 0.22f);
            body.color = tint;
            lidFace.color = Color.Lerp(tint, Color.white, 0.2f);
            band.color = Color.Lerp(tint, Color.black, 0.35f);
            glowImage.color = new Color(tint.r, tint.g, tint.b, 0f);
            rarityLabel.text = Loc.T("chest.rarity." + rarity);
            rarityLabel.color = Color.Lerp(tint, Color.white, 0.3f);
            reward.text = "";
            lid.anchoredPosition = new Vector2(0f, 188f);
            lid.localRotation = Quaternion.identity;
            open.gameObject.SetActive(true);
            take.gameObject.SetActive(false);
            doubleIt.gameObject.SetActive(false);
            openedAt = -1f;
            t = 0f;
            screen.Show();
            transform.SetAsLastSibling();
        }

        /// <summary>The coins were doubled: the new line.</summary>
        public void ShowDoubled(string line)
        {
            reward.text = line;
            reward.transform.localScale = Vector3.one * 1.3f;
            AudioManager.PlaySfx(Sfx.Coin, 1f, 1.5f);
        }

        private void Open()
        {
            if (openedAt >= 0f) return;
            openedAt = t;
            open.gameObject.SetActive(false);
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.2f);
            Audio.Haptics.Medium();
            reward.text = onOpen != null ? onOpen() : "";
            reward.transform.localScale = Vector3.one * 0.2f;
        }

        private void Update()
        {
            if (!screen.IsVisible) return;
            t += Time.unscaledDeltaTime;
            if (openedAt < 0f)
            {
                // Waiting: it wobbles and hops a little, begging to be opened.
                float wob = Mathf.Sin(t * 9f) * (Mathf.Repeat(t, 1.6f) < 0.5f ? 6f : 1.5f);
                chest.localRotation = Quaternion.Euler(0f, 0f, wob);
                chest.anchoredPosition = new Vector2(0f, 60f + Mathf.Abs(Mathf.Sin(t * 4.5f)) * 8f);
                return;
            }
            float k = t - openedAt;
            chest.localRotation = Quaternion.identity;
            // The lid flies up and tumbles away; the light swells; the reward pops in.
            lid.anchoredPosition = new Vector2(k * 180f, 188f + k * 420f - k * k * 300f);
            lid.localRotation = Quaternion.Euler(0f, 0f, -k * 160f);
            float g = Mathf.Clamp01(k * 3f);
            glowImage.color = new Color(tint.r, tint.g, tint.b, 0.3f * g);
            glow.localScale = Vector3.one * (0.8f + 0.3f * g + Mathf.Sin(t * 3f) * 0.04f);
            reward.transform.localScale = Vector3.Lerp(reward.transform.localScale, Vector3.one, Time.unscaledDeltaTime * 10f);
            if (k > 0.6f && !take.gameObject.activeSelf)
            {
                take.gameObject.SetActive(true);
                doubleIt.gameObject.SetActive(onDouble != null);
            }
        }
    }
}
