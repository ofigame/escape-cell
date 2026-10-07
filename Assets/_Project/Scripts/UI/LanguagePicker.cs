using System;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// The language list: every language the game speaks, each written in its own language, the current one ticked.
    /// Tapping one picks it; the X (or tapping outside) closes the list without a change.
    /// </summary>
    public class LanguagePicker : MonoBehaviour
    {
        public event Action<Language> Chosen;

        private UiScreen screen;

        public static LanguagePicker Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("Languages", canvasRoot, out var root);
            var p = root.gameObject.AddComponent<LanguagePicker>();
            p.screen = screen;
            p.Build(root);
            return p;
        }

        private void Build(RectTransform root)
        {
            var dim = UiFactory.Dim(root, new Color(0.04f, 0.03f, 0.12f, 0.6f));
            var close = dim.gameObject.AddComponent<UnityEngine.UI.Button>();
            close.transition = UnityEngine.UI.Selectable.Transition.None;
            close.onClick.AddListener(Hide);

            int count = Loc.Order.Length;
            float rowH = 128f;
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 220f + count * rowH));
            card.pivot = new Vector2(0.5f, 0.5f);
            screen.SetPopTarget(card);
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 100f), Loc.T("settings.language"), 64f, Palette.UiText, title: true);
            UiFactory.MakeButton(card, "X", Kind.Icon, new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(100f, 100f), Hide, 48f);

            for (int i = 0; i < count; i++)
            {
                var lang = Loc.Order[i];
                bool current = lang == Loc.Current;
                var button = UiFactory.MakeButton(card, Loc.NameOf(lang), current ? Kind.Primary : Kind.Secondary, new Vector2(0.5f, 1f),
                    new Vector2(0f, -170f - i * rowH), new Vector2(660f, rowH - 18f), () =>
                    {
                        Hide();
                        if (lang != Loc.Current) Chosen?.Invoke(lang);
                    }, 46f);
                if (current)
                {
                    var tick = UiFactory.Box("Tick", button.transform, new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(40f, 40f));
                    tick.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Fill(tick, UiFactory.TextDark, UiSprites.Circle).raycastTarget = false;
                }
            }
        }

        public void Show()
        {
            screen.Show();
            transform.SetAsLastSibling();
        }

        public void Hide() => screen.Hide();
    }
}
