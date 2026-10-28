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
    /// they are in the game now, and a duel invitation (a 3-minute duel). The friends (and the player among them) are ranked by
    /// rank or by power. Over everything, a friend's invitation that came in: ACCEPT or NO.
    /// </summary>
    public class FriendsScreen : MonoBehaviour
    {
        public event Action<string> AddPressed;
        public event Action<string> AcceptPressed, RemovePressed;
        /// <summary>A friend invited to a duel: their id, name and the minutes.</summary>
        public event Action<string, string, int> InvitePressed;
        /// <summary>A copy duel with an away friend (their name).</summary>
        public event Action<string> CopyPressed;
        /// <summary>A friend code typed in; the copy setting switched.</summary>
        public event Action<string> CodePressed;
        public event Action<bool> CopySettingPressed;
        public event Action<FoiNet.Invitation> InviteAccepted, InviteDeclined;

        private UiScreen screen, invitePanel;
        private TMPro.TextMeshProUGUI headerCoins;
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
            headerCoins = UiKit.ScreenHeader(root, screen, Loc.T("friends.title"), Hide);
            // Adding a friend by name.
            var box = UiFactory.Box("Input", root, new Vector2(0.5f, 1f), new Vector2(-150f, -270f), new Vector2(620f, 110f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var bg = UiFactory.Fill(box, Flat.CardHi, UiSprites.Rounded, 1.6f);
            var area = UiFactory.Rect("TextArea", box, Vector2.zero, Vector2.one, new Vector2(28f, 10f), new Vector2(-28f, -10f));
            area.gameObject.AddComponent<RectMask2D>();
            var text = UiFactory.Text(area, "", 44f, Color.white);
            text.enableAutoSizing = false;
            text.textPreprocessor = null;
            var placeholder = UiFactory.Text(area, Loc.T("friends.namePlaceholder"), 40f, UiKit.A(UiKit.Style.white, 0.3f));
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
            // Ranked by rank or by power.
            byRank = Flat.Button(root, Loc.T("friends.byRank"), new Vector2(0.5f, 1f), new Vector2(-250f, -455f), new Vector2(480f, 84f), Flat.CardHi, Flat.Text, () => SortBy(false), 28f);
            ((RectTransform)byRank.transform).pivot = new Vector2(0.5f, 0.5f);
            byPower = Flat.Button(root, Loc.T("friends.byPower"), new Vector2(0.5f, 1f), new Vector2(250f, -455f), new Vector2(480f, 84f), Flat.CardHi, Flat.Text, () => SortBy(true), 28f);
            ((RectTransform)byPower.transform).pivot = new Vector2(0.5f, 0.5f);
            byRank.gameObject.SetActive(false); byPower.gameObject.SetActive(false); // (ranked by rank; the row shows the friend code and the copy setting)
            BuildCodeRow(root, -455f);
            // The list.
            var viewport = UiFactory.Rect("Viewport", root, Vector2.zero, Vector2.one, new Vector2(40f, Monetization.Ads.BannerReserve + 140f), new Vector2(-40f, -510f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, UiKit.A(UiKit.Style.black, 0.001f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;

            // A friend's invitation that came in.
            invitePanel = UiScreen.Create("FriendInvite", parent, out var ip);
            UiFactory.Dim(ip, UiKit.A(UiKit.Style.backDeep, 0.85f)).gameObject.AddComponent<IgnoreSafeArea>();
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

        private Button byRank, byPower;
        private bool rankByPower;
        private FoiNet.FriendList shown;

        /// <summary>The friends ranked by rank (then trophies) or by power: the lit button.</summary>
        private void SortBy(bool power)
        {
            AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.2f);
            rankByPower = power;
            if (shown != null) Fill(shown);
            else LightSort();
        }

        private void LightSort()
        {
            ((Image)byRank.targetGraphic).color = rankByPower ? Flat.CardHi : UiKit.Style.orange;
            ((Image)byPower.targetGraphic).color = rankByPower ? UiKit.Style.orange : Flat.CardHi;
        }

        private void Add()
        {
            var name = (nameInput.text ?? "").Trim();
            if (name.Length < 3) { status.text = Loc.T("friends.err.notfound"); return; }
            AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.1f);
            status.text = Loc.T("ch.name.wait");
            // a friend code (six of the code letters, typed in capitals or with a #): added by code
            string code = name.TrimStart('#').ToUpperInvariant();
            bool isCode = (name.StartsWith("#") || name == code) && code.Length == 6 && System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-HJ-NP-Z2-9]{6}$");
            if (isCode) CodePressed?.Invoke(code); else AddPressed?.Invoke(name);
        }

        public void SetStatus(string text) => status.text = text;

        private TextMeshProUGUI codeLine;
        private Button copyToggle;

        /// <summary>The player's own friend code, to share (null: not known yet).</summary>
        public void SetMyCode(string code)
        {
            if (codeLine != null) codeLine.text = string.IsNullOrEmpty(code) ? "" : Loc.F("friends.myCode", code) + "  ↗";
        }

        public void SetCopySetting(bool on)
        {
            if (copyToggle == null) return;
            copyToggle.GetComponentInChildren<TextMeshProUGUI>().text = Loc.T(on ? "friends.copyOn" : "friends.copyOff");
            copyAllowed = on;
        }

        private bool copyAllowed = true;

        /// <summary>The friend code line and its field (add by code), and the copy setting: built under the name field.</summary>
        private void BuildCodeRow(RectTransform p, float y)
        {
            codeLine = Flat.Label(p, "", new Vector2(0.5f, 1f), new Vector2(-170f, y), new Vector2(620f, 50f), 28f, Flat.Gold, TextAlignmentOptions.Left, bold: true);
            codeLine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // tapped: the invitation shared (the code and its link)
            codeLine.raycastTarget = true;
            var share = codeLine.gameObject.AddComponent<Button>();
            share.transition = Selectable.Transition.None;
            share.onClick.AddListener(() =>
            {
                if (Share.MyCode.Length != 6) return;
                AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.2f);
                Share.Text(Loc.F("share.invite", Share.MyCode) + " " + Share.Link);
            });
            copyToggle = Flat.Button(p, Loc.T("friends.copyOn"), new Vector2(0.5f, 1f), new Vector2(330f, y), new Vector2(330f, 64f), Flat.CardHi, Flat.Text, () =>
            {
                AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.2f);
                CopySettingPressed?.Invoke(!copyAllowed);
            }, 22f);
            ((RectTransform)copyToggle.transform).pivot = new Vector2(0.5f, 0.5f);
        }
        public void ClearInput() => nameInput.text = "";

        public void Show()
        {
            status.text = Loc.T("ch.name.wait");
            screen.Show();
            UiKit.RefreshCurrency(headerCoins);
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
            shown = list;
            LightSort();
            if (list == null) { status.text = Loc.T("live.offline"); content.sizeDelta = Vector2.zero; return; }
            status.text = "";
            float y = -10f;
            void Header(string text)
            {
                var l = Flat.Label(content, text, new Vector2(0.5f, 1f), new Vector2(0f, y - 6f), new Vector2(980f, 50f), 30f, Flat.Muted, TextAlignmentOptions.Left, bold: true);
                l.rectTransform.pivot = new Vector2(0.5f, 1f);
                l.rectTransform.anchoredPosition = new Vector2(0f, y - 6f);
                y -= 76f; // the rows start well under the heading
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
                // ranked, the player among them (by rank then trophies, or by power)
                var sorted = new List<FoiNet.Friend>(list.friends);
                if (list.me != null && !string.IsNullOrEmpty(list.me.name)) sorted.Add(list.me);
                sorted.Sort((a, b) => rankByPower
                    ? (b.power != a.power ? b.power.CompareTo(a.power) : b.trophies.CompareTo(a.trophies))
                    : (b.rank != a.rank ? b.rank.CompareTo(a.rank) : b.trophies != a.trophies ? b.trophies.CompareTo(a.trophies) : b.power.CompareTo(a.power)));
                for (int i = 0; i < sorted.Count; i++) { Row(sorted[i], y, sorted[i] == list.me ? 3 : 0, i + 1); y -= RowH; }
            }
            if (list.outgoing != null && list.outgoing.Length > 0)
            {
                Header(Loc.T("friends.sent"));
                foreach (var f in list.outgoing) { Row(f, y, 2); y -= RowH; }
            }
            content.sizeDelta = new Vector2(0f, -y + 20f);
            content.anchoredPosition = Vector2.zero;
        }

        /// <param name="kind">0 a friend (invite, remove), 1 a request to answer (accept, no), 2 a request sent (take back), 3 the player.</param>
        /// <param name="place">Its place in the ranking (0: none).</param>
        private void Row(FoiNet.Friend f, float y, int kind, int place = 0)
        {
            var row = Flat.Panel(content, "Row", new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1000f, RowH - 14f), kind == 3 ? Flat.CardHi : Flat.Card);
            row.pivot = new Vector2(0.5f, 1f);
            if (kind == 3) UiFactory.Fill(UiFactory.Stretch("Rim", row), Flat.Gold, UiSprites.Ring, 1.6f).raycastTarget = false;

            RankArt.Badge(row, Mathf.Clamp(f.rank, 0, RankArt.Count - 1), new Vector2(0f, 0.5f), new Vector2(66f, 0f), 96f);
            var dot = UiFactory.Box("Online", row, new Vector2(0f, 0.5f), new Vector2(130f, 26f), new Vector2(22f, 22f));
            dot.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(dot, f.online ? UiKit.Style.successLight : UiKit.A(UiKit.Style.white, 0.2f), UiSprites.Circle).raycastTarget = false;
            var name = Flat.Label(row, (place > 0 ? place + ". " : "") + (kind == 3 ? f.name + " (" + Loc.T("friends.you") + ")" : f.name), new Vector2(0f, 0.5f), new Vector2(150f, 22f), new Vector2(450f, 56f), 38f, kind == 3 ? Flat.Gold : Flat.Text, bold: true);
            name.rectTransform.pivot = new Vector2(0f, 0.5f);
            string score = kind == 0 ? Rivalry.Text(f.name) : "";
            var info = Flat.Label(row, Challenge.RankName(f.rank) + " · " + Loc.F("ch.trophies", f.trophies) + (score.Length > 0 ? " · " + score : "") + (f.online && kind != 3 ? " · " + Loc.T("friends.online") : ""), new Vector2(0f, 0.5f), new Vector2(150f, -28f), new Vector2(450f, 40f), 24f, Flat.Muted);
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
                    // online: a live duel; away: a duel with its copy
                    if (f.online) Small(Loc.T("friends.duel"), -20f, 240f, UiKit.Style.orange, () => InvitePressed?.Invoke(f.id, f.name, 3));
                    else Small(Loc.T("friends.copy"), -20f, 240f, UiKit.Style.purple, () => CopyPressed?.Invoke(f.name));
                    Small(Loc.T("friends.remove"), -276f, 110f, Flat.CardHi, () => RemovePressed?.Invoke(f.id));
                    break;
                case 3:
                    break; // the player: no buttons
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
