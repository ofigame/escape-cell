using System;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// A small offer over the screen: a title, a line of what it gives, a gold "watch an ad" button and a quiet
    /// "no thanks". Used for the daily chest's double.
    /// </summary>
    public class AdOfferCard : MonoBehaviour
    {
        private UiScreen screen;
        private TextMeshProUGUI title, body;
        private Action onWatch;

        public static AdOfferCard Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("AdOffer", canvasRoot, out var root);
            var c = root.gameObject.AddComponent<AdOfferCard>();
            c.screen = s;
            c.Build(root);
            return c;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.02f, 0.02f, 0.06f, 0.6f)).gameObject.AddComponent<IgnoreSafeArea>();
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 620f));
            card.pivot = new Vector2(0.5f, 0.5f);
            title = UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(780f, 100f), "", 64f, Palette.UiGold, title: true);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            body = UiFactory.TextBox("Body", card, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(760f, 160f), "", 40f, Palette.UiText, FontStyles.Normal);
            body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            body.textWrappingMode = TextWrappingModes.Normal;
            var watch = UiFactory.MakeButton(card, Loc.T("ad.watch"), Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(620f, 130f), () =>
            {
                screen.Hide();
                onWatch?.Invoke();
            }, 52f);
            ((RectTransform)watch.transform).pivot = new Vector2(0.5f, 0.5f);
            var no = UiFactory.MakeButton(card, Loc.T("ad.noThanks"), Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(420f, 80f), () => screen.Hide(), 34f);
            ((RectTransform)no.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        public void Hide() => screen.Hide();

        public void Show(string titleText, string bodyText, Action watchAd)
        {
            title.text = titleText;
            body.text = bodyText;
            onWatch = watchAd;
            screen.Show();
            transform.SetAsLastSibling();
        }
    }
}
