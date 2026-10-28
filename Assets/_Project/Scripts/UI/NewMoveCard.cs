using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// A world's monster beaten (the fixes design's job 8): "NEW MOVE: HOOK", what it does (its numbers from the duel
    /// table), the world's gifts under it, and ADD TO MY SET (it becomes the set's special, ultimate or passive; a passive
    /// only once its slot is open) or CLOSE.
    /// </summary>
    public class NewMoveCard : MonoBehaviour
    {
        private UiScreen screen;
        private TextMeshProUGUI title, body, gifts, addLabel;
        private UnityEngine.UI.Button add;
        private string moveKey;

        public static NewMoveCard Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("NewMove", canvasRoot, out var root);
            var c = root.gameObject.AddComponent<NewMoveCard>();
            c.screen = s;
            c.Build(root);
            return c;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, UiKit.A(UiKit.Style.backDeep, 0.65f)).gameObject.AddComponent<IgnoreSafeArea>();
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 900f));
            card.pivot = new Vector2(0.5f, 0.5f);
            title = UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(800f, 110f), "", 62f, Palette.UiGold, title: true);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            title.enableAutoSizing = true; title.fontSizeMin = 36f; title.fontSizeMax = 62f;
            body = UiFactory.TextBox("Body", card, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(780f, 180f), "", 40f, Palette.UiText, FontStyles.Normal);
            body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true; body.fontSizeMin = 26f; body.fontSizeMax = 40f;
            gifts = UiFactory.TextBox("Gifts", card, new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(780f, 200f), "", 32f, UiKit.Style.silver, FontStyles.Normal);
            gifts.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            gifts.textWrappingMode = TextWrappingModes.Normal;
            add = UiFactory.MakeButton(card, Loc.T("newmove.add"), Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(640f, 130f), Add, 48f);
            ((RectTransform)add.transform).pivot = new Vector2(0.5f, 0.5f);
            addLabel = add.GetComponentInChildren<TextMeshProUGUI>();
            var close = UiFactory.MakeButton(card, Loc.T("btn.close"), Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(420f, 90f), () => screen.Hide(), 36f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        public bool IsOpen => screen.IsVisible;

        /// <summary>Shows the move won (its key "move.s.Hook"…) with the world's gift lines under it.</summary>
        public void Show(string key, string giftLines)
        {
            moveKey = key;
            title.text = Loc.F("newmove.title", Loc.T(key));
            body.text = MoveTexts.Describe(key);
            gifts.text = giftLines;
            bool passiveShut = key.StartsWith("move.p.") && !MoveSet.PassiveOpen;
            add.interactable = !passiveShut;
            addLabel.text = passiveShut ? Loc.F("moves.passiveLocked", MoveSet.PassiveLevel) : Loc.T("newmove.add");
            screen.Show();
            transform.SetAsLastSibling();
        }

        private void Add()
        {
            string name = moveKey.Substring(7);
            if (moveKey.StartsWith("move.s.") && System.Enum.TryParse(name, out SpecialKind s)) MoveSet.Special = s;
            else if (moveKey.StartsWith("move.u.") && System.Enum.TryParse(name, out UltiKind u)) MoveSet.Ulti = u;
            else if (moveKey.StartsWith("move.p.") && System.Enum.TryParse(name, out PassiveKind p)) MoveSet.Passive = p;
            Audio.AudioManager.PlaySfx(Audio.Sfx.Win, 0.8f, 1.2f);
            screen.Hide();
        }
    }
}
