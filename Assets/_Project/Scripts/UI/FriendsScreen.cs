using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// Friends: add one by user name (they accept), the requests to answer, and the friends — each with their rank, whether
    /// they are in the game now, and a duel invitation (3, 5 or 10 minutes). Over everything, a friend's invitation that
    /// came in: ACCEPT or NO.
    /// </summary>
    public class FriendsScreen : MonoBehaviour
    {
        public event Action<string> AddPressed;
        public event Action<string> AcceptPressed, RemovePressed;
        /// <summary>A friend invited to a duel: their id, name and the minutes.</summary>
        public event Action<string, string, int> InvitePressed;
        public event Action<FoiNet.Invitation> InviteAccepted, InviteDeclined;

        private UiScreen screen, invitePanel;
        private RectTransform content;
        private TMP_InputField nameInput;
        private TextMeshProUGUI status, inviteText;
        private FoiNet.Invitation shownInvite;
        private const float RowH = 150f;

        public bool IsOpen => screen.IsVisible;
        public bool InviteOpen => invitePanel.IsVisible;

        public static FriendsScreen Create(Transform canvasRoot)
        {
            var s = new GameObject("FriendsUi", typeof(RectTransform)).AddComponent<FriendsScreen>();
            var rt = (RectTransform)s.transform;
            rt.SetParent(canvasRoot, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            s.Build(rt);
            return s;
        }

        private void Build(RectTransform parent)
        {
            screen = UiScreen.Create("Friends", parent, out var root);
            Flat.Background(root);
            Flat.Label(root, Loc.T("friends.title"), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(960f, 80f), 56f, Flat.Text, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // Adding a friend by name.
            var box = UiFactory.Box("Input", root, new Vector2(0.5f, 1f), new Vector2(-150f, -270f), new Vector2(620f, 110f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var bg = UiFactory.Fill(box, Flat.CardHi, UiSprites.Rounded, 1.6f);
            var area = UiFactory.Rect("TextArea", box, Vector2.zero, Vector2.one, new Vector2(28f, 10f), new Vector2(-28f, -10f));
            area.gameObject.AddComponent<RectMask2D>();
            var text = UiFactory.Text(area, "", 44f, Color.white);
            text.enableAutoSizing = false;
            text.textPreprocessor = null;
            var placeholder = UiFactory.Text(area, Loc.T("friends.namePlaceholder"), 40f, new Color(1f, 1f, 1f, 0.3f));
            placeholder.enableAutoSizing = false;
            nameInput = box.gameObject.AddComponent<TMP_InputField>();
            nameInput.textViewport = area;
            nameInput.textComponent = text;
            nameInput.placeholder = placeholder;
            nameInput.targetGraphic = bg;
            nameInput.characterLimit = 16;
            nameInput.lineType = TMP_InputField.LineType.SingleLine;
            nameInput.onSubmit.AddListener(_ => Add());
            var add = Flat.Button(root, Loc.T("friends.add"), new Vector2(0.5f, 1f), new Vector2(330f, -270f), new Vector2(300f, 110f), Flat.Blue, Flat.Text, Add, 36f);
            ((RectTransform)add.transform).pivot = new Vector2(0.5f, 0.5f);
            status = Flat.Label(root, "", new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(940f, 60f), 28f, Flat.Gold, TextAlignmentOptions.Center, wrap: true);
            status.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // The list.
            var viewport = UiFactory.Rect("Viewport", root, Vector2.zero, Vector2.one, new Vector2(40f, 220f), new Vector2(-40f, -410f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;
            var close = Flat.Button(root, Loc.T("btn.close"), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(440f, 104f), Flat.CardHi, Flat.Text, Hide, 34f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);

            // A friend's invitation that came in.
            invitePanel = UiScreen.Create("FriendInvite", parent, out var ip);
            UiFactory.Dim(ip, new Color(0.02f, 0.02f, 0.07f, 0.85f)).gameObject.AddComponent<IgnoreSafeArea>();
            var card = Flat.Panel(ip, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 560f), Flat.Card);
            card.pivot = new Vector2(0.5f, 0.5f);
            Flat.Label(card, Loc.T("friends.inviteTitle"), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(860f, 70f), 48f, Flat.Gold, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            inviteText = Flat.Label(card, "", new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(840f, 160f), 38f, Flat.Text, TextAlignmentOptions.Center, wrap: true);
            inviteText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var yes = Flat.Button(card, Loc.T("friends.accept"), new Vector2(0.5f, 0f), new Vector2(-210f, 100f), new Vector2(380f, 110f), Flat.Blue, Flat.Text, () =>
            {
                AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.2f);
                invitePanel.Hide(true);
                if (shownInvite != null) InviteAccepted?.Invoke(shownInvite);
                shownInvite = null;
            }, 38f);
            ((RectTransform)yes.transform).pivot = new Vector2(0.5f, 0.5f);
            var no = Flat.Button(card, Loc.T("friends.decline"), new Vector2(0.5f, 0f), new Vector2(210f, 100f), new Vector2(380f, 110f), Flat.CardHi, Flat.Text, () =>
            {
                invitePanel.Hide(true);
                if (shownInvite != null) InviteDeclined?.Invoke(shownInvite);
                shownInvite = null;
            }, 38f);
            ((RectTransform)no.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        private void Add()
        {
            var name = (nameInput.text ?? "").Trim();
            if (name.Length < 3) { status.text = Loc.T("friends.err.notfound"); return; }
            AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.1f);
            status.text = Loc.T("ch.name.wait");
            AddPressed?.Invoke(name);
        }

        public void SetStatus(string text) => status.text = text;
        public void ClearInput() => nameInput.text = "";

        public void Show()
        {
            status.text = Loc.T("ch.name.wait");
            screen.Show();
            transform.SetAsLastSibling();
        }

        public void Hide() => screen.Hide(true);

        /// <summary>A friend's invitation: who, how many minutes (shown until answered, or until it is gone).</summary>
        public void ShowInvite(FoiNet.Invitation inv)
        {
            if (shownInvite != null && shownInvite.code == inv.code && invitePanel.IsVisible) return;
            shownInvite = inv;
            inviteText.text = Loc.F("friends.inviteText", inv.from, inv.min);
            invitePanel.Show();
            transform.SetAsLastSibling();
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.3f);
        }

        /// <summary>The invitation is gone (taken back, or too old): its card closes.</summary>
        public void HideInvite() { invitePanel.Hide(true); shownInvite = null; }

        /// <summary>The list, from the server's answer (null: no network).</summary>
        public void Fill(FoiNet.FriendList list)
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            if (list == null) { status.text = Loc.T("live.offline"); content.sizeDelta = Vector2.zero; return; }
            status.text = "";
            float y = -10f;
            void Header(string text)
            {
                var l = Flat.Label(content, text, new Vector2(0.5f, 1f), new Vector2(0f, y - 30f), new Vector2(980f, 50f), 30f, Flat.Muted, TextAlignmentOptions.Left, bold: true);
                l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                y -= 64f;
            }
            if (list.incoming != null && list.incoming.Length > 0)
            {
                Header(Loc.T("friends.requests"));
                foreach (var f in list.incoming) { Row(f, y, 1); y -= RowH; }
            }
            Header(Loc.F("friends.list", list.friends?.Length ?? 0));
            if (list.friends == null || list.friends.Length == 0)
            {
                var none = Flat.Label(content, Loc.T("friends.none"), new Vector2(0.5f, 1f), new Vector2(0f, y - 50f), new Vector2(940f, 100f), 30f, Flat.Muted, TextAlignmentOptions.Center, wrap: true);
                none.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                y -= 110f;
            }
            else
            {
                var sorted = new List<FoiNet.Friend>(list.friends);
                sorted.Sort((a, b) => a.online != b.online ? (a.online ? -1 : 1) : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                foreach (var f in sorted) { Row(f, y, 0); y -= RowH; }
            }
            if (list.outgoing != null && list.outgoing.Length > 0)
            {
                Header(Loc.T("friends.sent"));
                foreach (var f in list.outgoing) { Row(f, y, 2); y -= RowH; }
            }
            content.sizeDelta = new Vector2(0f, -y + 20f);
            content.anchoredPosition = Vector2.zero;
        }

        /// <param name="kind">0 a friend (invite, remove), 1 a request to answer (accept, no), 2 a request sent (take back).</param>
        private void Row(FoiNet.Friend f, float y, int kind)
        {
            var row = Flat.Panel(content, "Row", new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1000f, RowH - 14f), Flat.Card);
            row.pivot = new Vector2(0.5f, 1f);
            RankArt.Badge(row, Mathf.Clamp(f.rank, 0, RankArt.Count - 1), new Vector2(0f, 0.5f), new Vector2(66f, 0f), 96f);
            var dot = UiFactory.Box("Online", row, new Vector2(0f, 0.5f), new Vector2(130f, 26f), new Vector2(22f, 22f));
            dot.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(dot, f.online ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 1f, 1f, 0.2f), UiSprites.Circle).raycastTarget = false;
            var name = Flat.Label(row, f.name, new Vector2(0f, 0.5f), new Vector2(150f, 22f), new Vector2(380f, 56f), 38f, Flat.Text, bold: true);
            name.rectTransform.pivot = new Vector2(0f, 0.5f);
            var info = Flat.Label(row, Challenge.RankName(f.rank) + " · " + Loc.F("ch.trophies", f.trophies) + (f.online ? " · " + Loc.T("friends.online") : ""), new Vector2(0f, 0.5f), new Vector2(150f, -28f), new Vector2(420f, 40f), 24f, Flat.Muted);
            info.rectTransform.pivot = new Vector2(0f, 0.5f);
            Button Small(string text, float x, float w, Color face, Action onClick)
            {
                var b = Flat.Button(row, text, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(w, 86f), face, Flat.Text, () => { AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.2f); onClick(); }, 26f);
                ((RectTransform)b.transform).pivot = new Vector2(1f, 0.5f);
                return b;
            }
            switch (kind)
            {
                case 0:
                    int[] lengths = { 10, 5, 3 };
                    for (int i = 0; i < 3; i++)
                    {
                        int minutes = lengths[i];
                        Small(Loc.F("ch.minutes", minutes), -20f - i * 118f, 108f, new Color(1f, 0.6f, 0.25f), () => InvitePressed?.Invoke(f.id, f.name, minutes));
                    }
                    Small(Loc.T("friends.remove"), -380f, 100f, Flat.CardHi, () => RemovePressed?.Invoke(f.id));
                    break;
                case 1:
                    Small(Loc.T("friends.accept"), -20f, 200f, Flat.Blue, () => AcceptPressed?.Invoke(f.id));
                    Small(Loc.T("friends.decline"), -236f, 160f, Flat.CardHi, () => RemovePressed?.Invoke(f.id));
                    break;
                default:
                    Small(Loc.T("friends.takeBack"), -20f, 220f, Flat.CardHi, () => RemovePressed?.Invoke(f.id));
                    break;
            }
        }
    }
}
