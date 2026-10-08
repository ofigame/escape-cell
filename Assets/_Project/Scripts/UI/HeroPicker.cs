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
    /// Choose foi: the four builds as big cards with their pictures and names. At the very first start it is free and
    /// comes up once; later the garage opens it to switch build for a price (the build in use is marked and free).
    /// </summary>
    public class HeroPicker : MonoBehaviour
    {
        public const int SwitchPrice = 5000;

        /// <summary>A build was picked (and paid for, when switching).</summary>
        public event Action<int> Picked;

        private UiScreen screen;
        private RectTransform grid;
        private TextMeshProUGUI title, sub;
        private GameObject closeButton;
        private bool paid;

        public bool IsOpen => screen.IsVisible;

        public static HeroPicker Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("HeroPicker", canvasRoot, out var root);
            var p = root.gameObject.AddComponent<HeroPicker>();
            p.screen = s;
            p.Build(root);
            return p;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Fill(UiFactory.Stretch("Backdrop", root), new Color(0.03f, 0.03f, 0.08f, 0.96f)).gameObject.AddComponent<IgnoreSafeArea>();
            title = UiFactory.TextBox("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(980f, 120f), "", 76f, Palette.UiGold, title: true);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            sub = UiFactory.TextBox("Sub", root, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(960f, 70f), "", 34f, new Color(1f, 1f, 1f, 0.8f), FontStyles.Normal);
            sub.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            grid = UiFactory.Rect("Grid", root, Vector2.zero, Vector2.one, new Vector2(20f, 220f), new Vector2(-20f, -320f));
            var close = UiFactory.MakeButton(root, Loc.T("btn.close"), Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(480f, 110f), () => screen.Hide(), 48f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
            closeButton = close.gameObject;
        }

        /// <param name="switching">From the garage: changing costs <see cref="SwitchPrice"/> coins; otherwise the free first pick.</param>
        public void Show(bool switching)
        {
            paid = switching;
            title.text = Loc.T(switching ? "hero.switchTitle" : "hero.title");
            sub.text = switching ? Loc.F("hero.switchSub", SwitchPrice) : Loc.T("hero.sub");
            closeButton.SetActive(switching);
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            const float w = 470f, h = 640f, gap = 30f;
            for (int i = 0; i < HeroModels.Count; i++)
            {
                int hero = i;
                float x = (i % 2 == 0 ? -1f : 1f) * (w + gap) * 0.5f;
                float y = -(i / 2) * (h + gap);
                var card = UiFactory.Card("Hero" + i, grid, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(w, h));
                card.pivot = new Vector2(0.5f, 1f);
                var colour = HeroModels.Colours(i).body;
                var halo = UiFactory.Box("Halo", card, new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(400f, 400f));
                halo.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(halo, new Color(colour.r, colour.g, colour.b, 0.45f), UiSprites.Shadow, 0.5f).raycastTarget = false;
                var pic = UiFactory.Box("Picture", card, new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(350f, 350f));
                pic.pivot = new Vector2(0.5f, 0.5f);
                var raw = pic.gameObject.AddComponent<RawImage>();
                raw.texture = ItemPreview.Hero(i);
                raw.raycastTarget = false;
                UiFactory.TextBox("Name", card, new Vector2(0.5f, 1f), new Vector2(0f, -425f), new Vector2(440f, 60f), Loc.T("hero." + i), 44f, Palette.UiText, title: true)
                    .rectTransform.pivot = new Vector2(0.5f, 0.5f);
                bool current = switching && SaveData.Hero == i;
                if (current)
                    UiFactory.Fill(UiFactory.Stretch("InUse", card), new Color(0.3f, 1f, 0.55f, 0.9f), UiSprites.Ring, 0.6f).raycastTarget = false;
                string label = current ? Loc.T("hero.inUse") : switching ? SwitchPrice.ToString() : Loc.T("hero.pick");
                var b = UiFactory.MakeButton(card, label, current ? Kind.Secondary : switching ? Kind.Gold : Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(400f, 96f), () => Choose(hero), 42f);
                ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0f);
                b.interactable = !current && (!switching || SaveData.Coins >= SwitchPrice);
            }
            screen.Show();
            transform.SetAsLastSibling();
        }

        private void Choose(int hero)
        {
            if (paid)
            {
                if (hero == SaveData.Hero || !Shop.Spend(SwitchPrice)) { Audio.AudioManager.PlaySfx(Audio.Sfx.Bump, 0.6f); return; }
                Audio.AudioManager.PlaySfx(Audio.Sfx.Coin, 1f, 1.2f);
            }
            else Audio.AudioManager.PlaySfx(Audio.Sfx.Win, 0.7f, 1.2f);
            SaveData.Hero = hero;
            SaveData.HeroChosen = true;
            screen.Hide();
            Picked?.Invoke(hero);
        }
    }
}
