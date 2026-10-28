using System;
using SquashBot.Audio;
using SquashBot.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// Settings → save and recovery: the player's recovery code (made on the server, shown big, copied with a tap) and,
    /// on a new phone, the box to type one in and bring a game back. Everything else is saved by itself (CloudSave);
    /// the game plays the same without a network, only these two buttons need one.
    /// </summary>
    public class CloudScreen : MonoBehaviour
    {
        /// <summary>A game came back: the scene starts again on it.</summary>
        public event Action Restored;

        private UiScreen screen;
        private TextMeshProUGUI code, status;
        private TMP_InputField input;
        private Button makeButton, restoreButton;
        private TextMeshProUGUI makeText;
        private bool busy, sure;

        public static CloudScreen Create(Transform root)
        {
            var s = UiScreen.Create("Cloud", root, out var p);
            var c = p.gameObject.AddComponent<CloudScreen>();
            c.screen = s;
            c.Build(p);
            return c;
        }

        public void Show()
        {
            sure = false;
            status.text = "";
            input.text = "";
            Refresh();
            transform.SetAsLastSibling(); // over the first pick too
            screen.Show();
        }

        private void Build(RectTransform p)
        {
            Flat.Background(p);
            Flat.Label(p, Loc.T("cloud.title"), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(960f, 90f), 56f, Flat.Text, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var text = Flat.Label(p, Loc.T("cloud.text"), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(940f, 160f), 30f, Flat.Muted, TextAlignmentOptions.Center, wrap: true);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // The code, big, with its buttons.
            var card = Flat.Panel(p, "Code", new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(940f, 400f), Flat.Card);
            card.pivot = new Vector2(0.5f, 0.5f);
            code = Flat.Label(card, "", new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 120f), 72f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            code.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            code.characterSpacing = 8f;
            code.enableAutoSizing = true;
            code.fontSizeMin = 36f;
            code.fontSizeMax = 72f;
            makeButton = Flat.Button(card, "", new Vector2(0.5f, 0f), new Vector2(-220f, 90f), new Vector2(400f, 100f), Flat.Blue, Flat.Text, MakeCode, 32f);
            ((RectTransform)makeButton.transform).pivot = new Vector2(0.5f, 0.5f);
            makeText = makeButton.GetComponentInChildren<TextMeshProUGUI>();
            var copy = Flat.Button(card, Loc.T("cloud.copy"), new Vector2(0.5f, 0f), new Vector2(220f, 90f), new Vector2(400f, 100f), Flat.CardHi, Flat.Text, Copy, 32f);
            ((RectTransform)copy.transform).pivot = new Vector2(0.5f, 0.5f);

            // Bringing a game back from another phone.
            var join = Flat.Panel(p, "Restore", new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(940f, 480f), Flat.Card);
            join.pivot = new Vector2(0.5f, 0.5f);
            var head = Flat.Label(join, Loc.T("cloud.restoreTitle"), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(880f, 70f), 34f, Flat.Text, TextAlignmentOptions.Center, bold: true, wrap: true);
            head.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var warn = Flat.Label(join, Loc.T("cloud.restoreWarn"), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(880f, 60f), 26f, Flat.Muted, TextAlignmentOptions.Center, wrap: true);
            warn.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var box = UiFactory.Box("Input", join, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(760f, 120f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var bg = UiFactory.Fill(box, Flat.CardHi, UiSprites.Rounded, 1.6f);
            var area = UiFactory.Rect("TextArea", box, Vector2.zero, Vector2.one, new Vector2(24f, 10f), new Vector2(-24f, -10f));
            area.gameObject.AddComponent<RectMask2D>();
            var typed = UiFactory.Text(area, "", 54f, Color.white);
            typed.enableAutoSizing = false;
            typed.textPreprocessor = null;
            typed.characterSpacing = 6f;
            var placeholder = UiFactory.Text(area, "XXXX-XXXX-XXXX", 54f, UiKit.A(UiKit.Style.white, 0.25f));
            placeholder.enableAutoSizing = false;
            input = box.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = typed;
            input.placeholder = placeholder;
            input.targetGraphic = bg;
            input.characterLimit = 16;
            input.contentType = TMP_InputField.ContentType.Alphanumeric;
            input.characterValidation = TMP_InputField.CharacterValidation.None;
            input.onValidateInput = (s, i, ch) => char.IsLetterOrDigit(ch) || ch == '-' ? char.ToUpperInvariant(ch) : '\0';
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.onValueChanged.AddListener(_ => sure = false);
            restoreButton = Flat.Button(join, Loc.T("cloud.restore"), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(560f, 100f), UiKit.Style.bronze, Flat.Text, Restore, 34f);
            ((RectTransform)restoreButton.transform).pivot = new Vector2(0.5f, 0.5f);

            status = Flat.Label(p, "", new Vector2(0.5f, 0.5f), new Vector2(0f, -490f), new Vector2(940f, 120f), 32f, Flat.Text, TextAlignmentOptions.Center, bold: true, wrap: true);
            status.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var close = Flat.Button(p, Loc.T("btn.close"), new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 70f), new Vector2(440f, 104f), Flat.CardHi, Flat.Text, () => { if (!busy) screen.Hide(); }, 34f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        private void Refresh()
        {
            string c = CloudSave.Code;
            code.text = c.Length > 0 ? c : "—";
            makeText.text = Loc.T(c.Length > 0 ? "cloud.renew" : "cloud.make");
        }

        private void MakeCode()
        {
            if (busy) return;
            AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.1f);
            busy = true;
            status.text = Loc.T("cloud.working");
            StartCoroutine(CloudSave.NewCode(c =>
            {
                busy = false;
                status.text = c == null ? Loc.T("cloud.net") : "";
                Refresh();
            }));
        }

        private void Copy()
        {
            if (CloudSave.Code.Length == 0) return;
            AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.1f);
            GUIUtility.systemCopyBuffer = CloudSave.Code;
            status.text = Loc.T("cloud.copied");
        }

        private void Restore()
        {
            if (busy) return;
            string typed = (input.text ?? "").Replace("-", "").Replace(" ", "");
            if (typed.Length != 12) { status.text = Loc.T("cloud.bad"); return; }
            // Twice: it takes the place of this phone's game.
            if (!sure) { sure = true; status.text = Loc.T("cloud.sure"); return; }
            AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.1f);
            busy = true;
            status.text = Loc.T("cloud.working");
            StartCoroutine(CloudSave.Restore(input.text, why =>
            {
                busy = false;
                sure = false;
                if (why == null)
                {
                    status.text = Loc.T("cloud.done");
                    Invoke(nameof(Done), 1.2f);
                    busy = true;
                    return;
                }
                status.text = Loc.T(why == "code" ? "cloud.bad" : why == "wait" ? "cloud.wait" : why == "empty" ? "cloud.empty" : "cloud.net");
            }));
        }

        private void Done() => Restored?.Invoke();
    }
}
