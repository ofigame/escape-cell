using System;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// "This would make it easier": after a few losses on the same floor, a big card over the result shows one item
    /// from the shop that fits this floor — a stronger weapon (its picture), an upgrade or a tool (its icon) — with a
    /// line on why, and a button straight to it in the shop.
    /// </summary>
    public class TipCard : MonoBehaviour
    {
        private UiScreen screen;
        private RectTransform picture;
        private TextMeshProUGUI title, text, price;
        private Action go;

        public static TipCard Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("TipCard", canvasRoot, out var root);
            var tip = root.gameObject.AddComponent<TipCard>();
            tip.screen = s;
            tip.Build(root);
            return tip;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.02f, 0.02f, 0.06f, 0.7f));
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 1180f));
            card.pivot = new Vector2(0.5f, 0.5f);
            title = UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(820f, 90f), Loc.T("tip.title"), 52f, Palette.UiGold, title: true);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var halo = UiFactory.Box("Halo", card, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(560f, 560f));
            halo.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(halo, new Color(1f, 0.8f, 0.4f, 0.35f), UiSprites.Shadow, 0.4f).raycastTarget = false;
            picture = UiFactory.Box("Picture", card, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(420f, 420f));
            picture.pivot = new Vector2(0.5f, 0.5f);
            text = UiFactory.TextBox("Text", card, new Vector2(0.5f, 1f), new Vector2(0f, -680f), new Vector2(800f, 190f), "", 34f, Color.white);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.textWrappingMode = TextWrappingModes.Normal;
            price = UiFactory.TextBox("Price", card, new Vector2(0.5f, 1f), new Vector2(0f, -820f), new Vector2(600f, 60f), "", 40f, Palette.UiGold, title: true);
            price.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.MakeButton(card, Loc.T("tip.go"), Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(640f, 120f), () =>
            {
                screen.Hide();
                go?.Invoke();
            }, 52f).GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            UiFactory.MakeButton(card, Loc.T("btn.close"), Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(640f, 90f), () => screen.Hide(), 40f)
                .GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        }

        private void Clear()
        {
            for (int i = picture.childCount - 1; i >= 0; i--) Destroy(picture.GetChild(i).gameObject);
            var raw = picture.GetComponent<RawImage>();
            if (raw != null) Destroy(raw);
        }

        public void ShowWeapon(WeaponDef w, string why, Action onGo)
        {
            Clear();
            var img = UiFactory.Box("Image", picture, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 420f));
            img.pivot = new Vector2(0.5f, 0.5f);
            img.gameObject.AddComponent<RawImage>().texture = ItemPreview.Weapon(w);
            Fill(Loc.F("tip.weapon", Loc.T("weapon." + w.id), why), w.price, onGo);
        }

        /// <summary>An upgrade or tool, drawn by <paramref name="draw"/> into a 110-unit box on a disc of <paramref name="colour"/>.</summary>
        public void ShowIcon(Action<RectTransform> draw, Color colour, string name, string why, int cost, Action onGo)
        {
            Clear();
            var disc = UiFactory.Box("Disc", picture, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 360f));
            disc.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Chunky(disc, UiFactory.StyleOf(colour), 1f, 0f, UiSprites.Circle).raycastTarget = false;
            var icon = UiFactory.Box("Icon", picture, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.localScale = Vector3.one * 3f;
            draw(icon);
            Fill(Loc.F("tip.item", name, why), cost, onGo);
        }

        private void Fill(string line, int cost, Action onGo)
        {
            text.text = line;
            price.text = cost > 0 ? Loc.F("tip.price", cost) : "";
            go = onGo;
            screen.Show();
            transform.SetAsLastSibling();
        }
    }
}
