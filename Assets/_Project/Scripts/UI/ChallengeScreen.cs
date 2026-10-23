using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Gameplay;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// The challenge's screens: the duel and its minutes, playing a friend (a code), the name, the waiting room (the
    /// player's card, "looking for a rival…", then the rival's card), and over the board the two names with their soldiers
    /// left, the score and clock, the status line, and the squad bar (six group tags, hearts and shields).
    /// </summary>
    public class ChallengeScreen : MonoBehaviour
    {
        /// <summary>A group's tag tapped on the duel's squad bar (send it / call it back).</summary>
        public event Action<int> GroupPicked;
        /// <summary>The duel's heart and shield buttons.</summary>
        public event Action HeartPressed, ShieldPressed;
        public event Action Leave;

        /// <summary>A name typed and confirmed on the name screen; closing it without one.</summary>
        public event Action<string> NameSubmitted;
        public event Action NameClosed;
        /// <summary>A kind picked on the modes screen: 0 the Army Battle, 1 the Duel (with its minutes).</summary>
        public event Action<int, int> ModePicked;
        /// <summary>A competition's reward was taken (the menu's coins).</summary>
        public event Action CoinsChanged;
        private CompetitionScreen competitions;
        /// <summary>The friend panel's MAKE A CODE: the duel's minutes the maker picked.</summary>
        public event Action<int> FriendHost;
        public event Action FriendClose;
        public event Action<string> FriendJoin;
        /// <summary>"Change name" in the waiting room.</summary>
        public event Action RenamePressed;
        /// <summary>FRIENDS on the modes screen.</summary>
        public event Action FriendsPressed;

        private UiScreen lobby, hud, namePanel;
        private RectTransform pickPanel, pickBar, myBadge, theirBadge;
        /// <summary>The battle screen's own layer (the duel's moves are laid on it).</summary>
        public RectTransform BattleHud => (RectTransform)pickPanel.parent;
        private TextMeshProUGUI pickTitle;
        private Image pickFill;
        private float pickPop = 1f;
        private int myBadgeRank = -1, theirBadgeRank = -1;
        private TMP_InputField nameInput;
        private TextMeshProUGUI nameError;
        private Button nameOk, leaveButton, renameButton;
        private PlayerCard myCard, theirCard;
        private TextMeshProUGUI lobbyMode;
        private TextMeshProUGUI lobbyStatus, myBar, theirBar, clock, status;
        private RectTransform searchBar;
        private readonly List<(Button button, TextMeshProUGUI count, Image face, Color colour)> groups = new List<(Button, TextMeshProUGUI, Image, Color)>();
        private float searchT;
        private bool searching;

        public static ChallengeScreen Create(Transform canvasRoot)
        {
            var go = new GameObject("Challenge", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var c = go.AddComponent<ChallengeScreen>();
            c.Build(rt);
            return c;
        }

        private void Build(RectTransform root)
        {
            // The waiting room.
            lobby = UiScreen.Create("ChallengeLobby", root, out var l);
            Flat.Background(l);
            Flat.Label(l, Loc.T("ch.title"), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(960f, 80f), 58f, Flat.Text, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            lobbyMode = Flat.Label(l, "", new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(960f, 44f), 28f, Flat.Muted, TextAlignmentOptions.Center);
            lobbyMode.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            myCard = new PlayerCard(l, new Vector2(0f, 250f), Flat.Blue);
            theirCard = new PlayerCard(l, new Vector2(0f, -250f), Flat.Red);
            var vs = Flat.Panel(l, "Vs", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f), Flat.CardHi, 0.9f);
            vs.pivot = new Vector2(0.5f, 0.5f);
            Flat.Label(vs, "VS", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 80f), 52f, Flat.Text, TextAlignmentOptions.Center, bold: true).rectTransform.pivot = new Vector2(0.5f, 0.5f);
            lobbyStatus = Flat.Label(l, "", new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 235f), new Vector2(940f, 60f), 34f, Flat.Text, TextAlignmentOptions.Center, bold: true, wrap: true);
            lobbyStatus.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var track = Flat.Panel(l, "Track", new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 175f), new Vector2(600f, 10f), Flat.Line, 6f);
            track.pivot = new Vector2(0.5f, 0.5f);
            searchBar = UiFactory.Box("Search", l, new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 175f), new Vector2(160f, 10f));
            searchBar.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(searchBar, Flat.Blue, UiSprites.Rounded, 6f).raycastTarget = false;
            renameButton = Flat.Button(l, Loc.T("ch.rename"), new Vector2(0.5f, 0f), new Vector2(-240f, Monetization.Ads.BannerReserve + 75f), new Vector2(440f, 104f), Flat.CardHi, Flat.Text, () => RenamePressed?.Invoke(), 32f);
            ((RectTransform)renameButton.transform).pivot = new Vector2(0.5f, 0.5f);
            leaveButton = Flat.Button(l, Loc.T("btn.close"), new Vector2(0.5f, 0f), new Vector2(240f, Monetization.Ads.BannerReserve + 75f), new Vector2(440f, 104f), Flat.CardHi, Flat.Text, () => Leave?.Invoke(), 32f);
            ((RectTransform)leaveButton.transform).pivot = new Vector2(0.5f, 0.5f);

            BuildNamePanel(root);
            BuildModes(root);

            // The battle's overlay.
            hud = UiScreen.Create("ChallengeHud", root, out var h);
            // Each side's rank and name just over its health bar (the bars sit at the top left and right), the clock
            // between the two bars.
            var top = UiFactory.Rect("Top", h, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -280f), new Vector2(0f, -196f));
            myBar = UiFactory.TextBox("Me", top, new Vector2(0f, 0.5f), new Vector2(118f, 0f), new Vector2(330f, 60f), "", 34f, ArmyBattleColours.Mine, title: true, align: TextAlignmentOptions.Left);
            myBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            theirBar = UiFactory.TextBox("Them", top, new Vector2(1f, 0.5f), new Vector2(-118f, 0f), new Vector2(330f, 60f), "", 34f, ArmyBattleColours.Theirs, title: true, align: TextAlignmentOptions.Right);
            theirBar.rectTransform.pivot = new Vector2(1f, 0.5f);
            foreach (var bar in new[] { myBar, theirBar }) { bar.enableAutoSizing = true; bar.fontSizeMin = 20f; bar.fontSizeMax = 34f; }
            clock = UiFactory.TextBox("Clock", h, new Vector2(0.5f, 1f), new Vector2(0f, -203f), new Vector2(220f, 70f), "", 46f, Color.white, title: true); // (between the names: its box's top here, its middle on theirs)
            clock.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            score = UiFactory.TextBox("Score", h, new Vector2(0.5f, 1f), new Vector2(0f, -275f), new Vector2(260f, 84f), "", 60f, Palette.UiGold, title: true); // (between the health bars: its middle on theirs)
            score.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            status = UiFactory.TextBox("Status", h, new Vector2(0.5f, 0f), new Vector2(-60f, 380f), new Vector2(860f, 70f), "", 34f, Palette.UiGold, title: true);
            status.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // The secret pick: a pop-up card at the bottom with the six groups (picture, name, soldiers left) and the clock.
            pickPanel = UiFactory.Box("Pick", h, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(1050f, 400f));
            pickPanel.pivot = new Vector2(0.5f, 0f);
            UiFactory.Fill(pickPanel, new Color(0.05f, 0.05f, 0.12f, 0.92f), UiSprites.Rounded, 1f);
            UiFactory.Fill(UiFactory.Stretch("Rim", pickPanel), new Color(ArmyBattleColours.Mine.r, ArmyBattleColours.Mine.g, ArmyBattleColours.Mine.b, 0.8f), UiSprites.Ring, 1f).raycastTarget = false;
            pickTitle = UiFactory.TextBox("Title", pickPanel, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(980f, 56f), Loc.T("ch.pick"), 36f, Palette.UiGold, title: true);
            pickBar = UiFactory.Bar(pickPanel, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(900f, 14f), Palette.UiCyan, out pickFill).rectTransform;
            for (int g = 0; g < Army.Groups; g++) GroupButton(pickPanel, g);
            pickPanel.gameObject.SetActive(false);
        }

        // ---------- The two kinds of challenge ----------

        private UiScreen modes;

        private void BuildModes(RectTransform root)
        {
            modes = UiScreen.Create("ChallengeModes", root, out var m);
            Flat.Background(m);
            Flat.Label(m, Loc.T("ch.modeTitle"), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(960f, 80f), 56f, Flat.Text, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // The competitions: the season, the leaderboards, the weekend cup, the clan, the quests.
            competitions = CompetitionScreen.Create(root);
            competitions.CoinsChanged += () => CoinsChanged?.Invoke();
            var comp = Flat.Button(m, Loc.T("comp.button"), new Vector2(0.5f, 1f), new Vector2(0f, -265f), new Vector2(640f, 96f), new Color(0.75f, 0.45f, 1f), Flat.Text, () => { AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.1f); competitions.Show(); }, 32f);
            ((RectTransform)comp.transform).pivot = new Vector2(0.5f, 0.5f);
            ModeCard(m, 1, new Vector2(0f, 160f), "duel", "ch.mode.duel", "ch.mode.duelText", new Color(1f, 0.6f, 0.25f), tappable: false);
            // One button: a 3-minute duel, foi against foi.
            var go = Flat.Button(m, Loc.T("ch.duelGo"), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(720f, 140f),
                new Color(1f, 0.6f, 0.25f), new Color(0.12f, 0.06f, 0f), () => { AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.1f); ModePicked?.Invoke(1, 3); }, 46f);
            ((RectTransform)go.transform).pivot = new Vector2(0.5f, 0.5f);
            // Playing a friend by code, between the lengths and CLOSE (the friends list is on the main menu).
            var friend = Flat.Button(m, Loc.T("live.friend"), new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 190f), new Vector2(640f, 100f), Flat.Blue, Flat.Text, ShowFriend, 34f);
            ((RectTransform)friend.transform).pivot = new Vector2(0.5f, 0.5f);
            var back = Flat.Button(m, Loc.T("btn.close"), new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 70f), new Vector2(440f, 104f), Flat.CardHi, Flat.Text, () => modes.Hide(true), 34f);
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0.5f);
            BuildFriend(root);
        }

        // ---------- Playing a friend ----------

        private UiScreen friendPanel;
        private TMP_InputField codeInput;
        private TextMeshProUGUI friendCode, friendStatus;
        private Button hostButton, joinButton;

        private void BuildFriend(RectTransform root)
        {
            friendPanel = UiScreen.Create("ChallengeFriend", root, out var p);
            Flat.Background(p);
            Flat.Label(p, Loc.T("live.friend"), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(960f, 80f), 56f, Flat.Text, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var hint = Flat.Label(p, Loc.T("live.friendHint"), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(940f, 90f), 28f, Flat.Muted, TextAlignmentOptions.Center, wrap: true);
            hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // Making a code: the code, big.
            var hostCard = Flat.Panel(p, "Host", new Vector2(0.5f, 0.5f), new Vector2(0f, 380f), new Vector2(940f, 470f), Flat.Card);
            hostCard.pivot = new Vector2(0.5f, 0.5f);
            friendCode = Flat.Label(hostCard, "", new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(900f, 110f), 96f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            friendCode.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            friendCode.characterSpacing = 30f;
            hostButton = Flat.Button(hostCard, Loc.T("live.host"), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(560f, 100f), Flat.Blue, Flat.Text, () => FriendHost?.Invoke(hostMinutes), 34f);
            ((RectTransform)hostButton.transform).pivot = new Vector2(0.5f, 0.5f);

            // Entering a friend's code.
            var joinCard = Flat.Panel(p, "Join", new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(940f, 330f), Flat.Card);
            joinCard.pivot = new Vector2(0.5f, 0.5f);
            var box = UiFactory.Box("Input", joinCard, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(420f, 120f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var bg = UiFactory.Fill(box, Flat.CardHi, UiSprites.Rounded, 1.6f);
            var area = UiFactory.Rect("TextArea", box, Vector2.zero, Vector2.one, new Vector2(24f, 10f), new Vector2(-24f, -10f));
            area.gameObject.AddComponent<RectMask2D>();
            var text = UiFactory.Text(area, "", 64f, Color.white);
            text.enableAutoSizing = false;
            text.textPreprocessor = null;
            text.characterSpacing = 20f;
            var placeholder = UiFactory.Text(area, "0000", 64f, new Color(1f, 1f, 1f, 0.25f));
            placeholder.enableAutoSizing = false;
            codeInput = box.gameObject.AddComponent<TMP_InputField>();
            codeInput.textViewport = area;
            codeInput.textComponent = text;
            codeInput.placeholder = placeholder;
            codeInput.targetGraphic = bg;
            codeInput.characterLimit = 4;
            codeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            codeInput.lineType = TMP_InputField.LineType.SingleLine;
            codeInput.onSubmit.AddListener(_ => Join());
            joinButton = Flat.Button(joinCard, Loc.T("live.join"), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(560f, 100f), Flat.Blue, Flat.Text, Join, 34f);
            ((RectTransform)joinButton.transform).pivot = new Vector2(0.5f, 0.5f);

            friendStatus = Flat.Label(p, "", new Vector2(0.5f, 0.5f), new Vector2(0f, -320f), new Vector2(940f, 120f), 32f, Flat.Text, TextAlignmentOptions.Center, bold: true, wrap: true);
            friendStatus.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var close = Flat.Button(p, Loc.T("btn.close"), new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 70f), new Vector2(440f, 104f), Flat.CardHi, Flat.Text, () => FriendClose?.Invoke(), 34f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>Every duel lasts three minutes.</summary>
        private const int hostMinutes = 3;

        private void Join()
        {
            var code = (codeInput.text ?? "").Trim();
            if (code.Length != 4) { friendStatus.text = Loc.T("live.codeShape"); return; }
            FriendJoin?.Invoke(code);
        }

        /// <summary>The friend panel (an invitation being made or answered goes on there).</summary>
        public void ShowFriendPanel() => ShowFriend();

        private void ShowFriend()
        {
            AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.1f);
            modes.Hide(true);
            friendCode.text = "- - - -";
            codeInput.text = "";
            friendStatus.text = "";
            SetFriendBusy(false);
            friendPanel.Show();
            transform.SetAsLastSibling();
        }

        public void HideFriend() => friendPanel.Hide(true);
        public bool FriendOpen => friendPanel != null && friendPanel.IsVisible;
        public void SetFriendCode(string code) => friendCode.text = code;
        public void SetFriendStatus(string text) => friendStatus.text = text;

        /// <summary>While a code waits for a friend (or a join is on its way) the buttons rest.</summary>
        public void SetFriendBusy(bool busy)
        {
            hostButton.interactable = !busy;
            joinButton.interactable = !busy;
            codeInput.interactable = !busy;
        }

        /// <summary>A kind's card: a picture from the game itself, its name and a line about it; tapping it picks it.</summary>
        private RectTransform ModeCard(RectTransform parent, int mode, Vector2 at, string picture, string nameKey, string textKey, Color colour, bool tappable = true)
        {
            var card = UiFactory.Box("Mode" + mode, parent, new Vector2(0.5f, 0.5f), at, new Vector2(960f, 640f));
            card.pivot = new Vector2(0.5f, 0.5f);
            var face = UiFactory.Fill(card, Flat.Card, UiSprites.Rounded, 1.6f);
            var picBox = UiFactory.Rect("Picture", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(Flat.Pad, -Flat.Pad - 430f), new Vector2(-Flat.Pad, -Flat.Pad));
            picBox.gameObject.AddComponent<RectMask2D>();
            var tex = Resources.Load<Texture2D>("ChallengeModes/" + picture);
            var pic = UiFactory.Stretch("Image", picBox).gameObject.AddComponent<RawImage>();
            pic.texture = tex;
            pic.color = tex != null ? Color.white : new Color(colour.r, colour.g, colour.b, 0.35f);
            pic.raycastTarget = false;
            if (tex != null)
            {
                // Fill the frame (cropping the picture's middle band).
                float frameAspect = 912f / 430f, texAspect = tex.width / (float)tex.height;
                pic.uvRect = texAspect > frameAspect ? new Rect((1f - frameAspect / texAspect) * 0.5f, 0f, frameAspect / texAspect, 1f)
                                                     : new Rect(0f, (1f - texAspect / frameAspect) * 0.5f, 1f, texAspect / frameAspect);
            }
            // A strip of the kind's colour under the picture, then its name and what it is.
            var strip = UiFactory.Box("Strip", card, new Vector2(0.5f, 1f), new Vector2(0f, -Flat.Pad - 430f), new Vector2(912f, 8f));
            strip.pivot = new Vector2(0.5f, 1f);
            UiFactory.Fill(strip, colour).raycastTarget = false;
            var name = Flat.Label(card, Loc.T(nameKey), new Vector2(0f, 0f), new Vector2(Flat.Pad + 8f, 112f), new Vector2(900f, 60f), 44f, colour, bold: true);
            name.rectTransform.pivot = new Vector2(0f, 0.5f);
            var text = Flat.Label(card, Loc.T(textKey), new Vector2(0f, 0f), new Vector2(Flat.Pad + 8f, 48f), new Vector2(900f, 66f), 26f, Flat.Muted, wrap: true);
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            if (!tappable)
            {
                // Its own buttons start it (beside its name; the line under them runs the full width).
                name.rectTransform.sizeDelta = new Vector2(340f, 60f);
                name.rectTransform.anchoredPosition = new Vector2(Flat.Pad + 8f, 122f);
                text.rectTransform.anchoredPosition = new Vector2(Flat.Pad + 8f, 40f);
                return card;
            }
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.onClick.AddListener(() => { AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.1f); ModePicked?.Invoke(mode, 0); });
            card.gameObject.AddComponent<ButtonPress>();
            return card;
        }

        public void ShowModes()
        {
            if (friendPanel != null) friendPanel.Hide(true);
            lobby.Hide(true);
            hud.Hide(true);
            modes.Show();
            transform.SetAsLastSibling();
        }

        public void HideModes() => modes.Hide(true);

        // ---------- The name ----------

        private void BuildNamePanel(RectTransform root)
        {
            namePanel = UiScreen.Create("ChallengeName", root, out var n);
            UiFactory.Dim(n, new Color(0.03f, 0.03f, 0.1f, 1f)).gameObject.AddComponent<IgnoreSafeArea>();
            Flat.Backdrop(n); // the game look's galaxy and gold corners
            UiFactory.TextBox("Title", n, new Vector2(0.5f, 0.5f), new Vector2(0f, 760f), new Vector2(900f, 110f), Loc.T("ch.nameTitle"), 72f, Palette.UiGold, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var hint = UiFactory.TextBox("Hint", n, new Vector2(0.5f, 0.5f), new Vector2(0f, 650f), new Vector2(900f, 90f), Loc.T("ch.nameHint"), 34f, new Color(1f, 1f, 1f, 0.75f));
            hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            hint.textWrappingMode = TextWrappingModes.Normal;

            var box = UiFactory.Box("Input", n, new Vector2(0.5f, 0.5f), new Vector2(0f, 510f), new Vector2(780f, 140f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var bg = UiFactory.Fill(box, new Color(1f, 1f, 1f, 0.12f), UiSprites.Rounded, 1.2f);
            UiFactory.Fill(UiFactory.Stretch("Rim", box), new Color(ArmyBattleColours.Mine.r, ArmyBattleColours.Mine.g, ArmyBattleColours.Mine.b, 0.9f), UiSprites.Ring, 1.2f).raycastTarget = false;
            var area = UiFactory.Rect("TextArea", box, Vector2.zero, Vector2.one, new Vector2(36f, 14f), new Vector2(-36f, -14f));
            area.gameObject.AddComponent<RectMask2D>();
            var text = UiFactory.Text(area, "", 56f, Color.white);
            text.enableAutoSizing = false;
            text.textPreprocessor = null; // typed text is shown as typed
            var placeholder = UiFactory.Text(area, "foi1234", 56f, new Color(1f, 1f, 1f, 0.3f));
            placeholder.enableAutoSizing = false;
            nameInput = box.gameObject.AddComponent<TMP_InputField>();
            nameInput.textViewport = area;
            nameInput.textComponent = text;
            nameInput.placeholder = placeholder;
            nameInput.targetGraphic = bg;
            nameInput.characterLimit = 16;
            nameInput.contentType = TMP_InputField.ContentType.Standard;
            nameInput.lineType = TMP_InputField.LineType.SingleLine;
            nameInput.onSubmit.AddListener(_ => SubmitName());
            nameInput.onValueChanged.AddListener(_ => nameError.text = "");

            nameError = UiFactory.TextBox("Error", n, new Vector2(0.5f, 0.5f), new Vector2(0f, 390f), new Vector2(900f, 70f), "", 34f, new Color(1f, 0.45f, 0.5f), title: true);
            nameError.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            nameOk = UiFactory.MakeButton(n, Loc.T("ch.nameOk"), Kind.Primary, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(460f, 130f), SubmitName, 48f);
            ((RectTransform)nameOk.transform).pivot = new Vector2(0.5f, 0.5f);
            nameClose = UiFactory.MakeButton(n, Loc.T("btn.close"), Kind.Secondary, new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(360f, 100f), () => { if (Challenge.NameChosen) NameClosed?.Invoke(); }, 38f);
            ((RectTransform)nameClose.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        private Button nameClose;

        private void SubmitName()
        {
            if (!nameOk.interactable) return;
            NameSubmitted?.Invoke((nameInput.text ?? "").Trim());
        }

        /// <summary>The name screen, filled with the current name (and why the last one was refused, if it was).</summary>
        public void ShowName(string current, string error = "")
        {
            lobby.Hide(true);
            hud.Hide(true);
            nameInput.text = current;
            nameError.text = error;
            nameOk.interactable = true;
            nameClose.gameObject.SetActive(Challenge.NameChosen); // the first name is a must: no way out without one
            namePanel.Show();
            transform.SetAsLastSibling();
        }

        /// <summary>The name is being checked (the button waits), or the answer: an error, or "" to close the screen.</summary>
        public void NameChecking() { nameOk.interactable = false; nameError.text = Loc.T("ch.name.wait"); }
        public void NameRefused(string error) { nameOk.interactable = true; nameError.text = error; }
        public void HideName() => namePanel.Hide(true);
        public bool NameShowing => namePanel.IsVisible;

        /// <summary>A side's card in the waiting room: its rank insignia, the name, and rank · trophies · power under it.</summary>
        private class PlayerCard
        {
            private readonly RectTransform panel;
            private readonly TextMeshProUGUI name, detail;
            private RectTransform badge;
            private int badgeRank = -1;

            public PlayerCard(RectTransform parent, Vector2 at, Color colour)
            {
                panel = Flat.Panel(parent, "Card", new Vector2(0.5f, 0.5f), at, new Vector2(960f, 250f), Flat.Card);
                panel.pivot = new Vector2(0.5f, 0.5f);
                var stripe = UiFactory.Box("Stripe", panel, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(12f, 200f));
                stripe.pivot = new Vector2(0f, 0.5f);
                UiFactory.Fill(stripe, colour, UiSprites.Rounded, 6f).raycastTarget = false;
                name = Flat.Label(panel, "", new Vector2(0f, 0.5f), new Vector2(220f, 34f), new Vector2(700f, 64f), 48f, Flat.Text, bold: true);
                name.rectTransform.pivot = new Vector2(0f, 0.5f);
                detail = Flat.Label(panel, "", new Vector2(0f, 0.5f), new Vector2(220f, -36f), new Vector2(700f, 50f), 28f, Flat.Muted, wrap: true);
                detail.rectTransform.pivot = new Vector2(0f, 0.5f);
            }

            /// <summary>The side's text (its name on the first line) and its rank (-1: not known yet: a question mark).</summary>
            public void Set(string text, int rank)
            {
                var parts = (text ?? "").Split('\n');
                name.text = parts[0];
                detail.text = parts.Length > 1 ? parts[1] : "";
                if (rank == badgeRank) return;
                if (badge != null) UnityEngine.Object.Destroy(badge.gameObject);
                badge = rank >= 0 ? RankArt.Badge(panel, rank, new Vector2(0f, 0.5f), new Vector2(110f, 0f), 170f) : null;
                badgeRank = rank;
            }
        }

        private TextMeshProUGUI Card(RectTransform parent, Vector2 at, Color colour)
        {
            var card = UiFactory.Box("Card", parent, new Vector2(0.5f, 0.5f), at, new Vector2(820f, 260f));
            card.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(card, new Color(colour.r, colour.g, colour.b, 0.18f), UiSprites.Rounded, 1.2f).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", card), new Color(colour.r, colour.g, colour.b, 0.8f), UiSprites.Ring, 1.2f).raycastTarget = false;
            var text = UiFactory.TextBox("Text", card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 230f), "", 40f, Color.white, title: true);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private void GroupButton(RectTransform parent, int g)
        {
            var kind = Army.KindOf(g);
            var colour = ArmyScreen.FamilyColour(kind);
            float x = (g - 2.5f) * 168f;
            var b = UiFactory.MakeButton(parent, "", Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(x, 150f), new Vector2(156f, 270f), () => Pick(g), 24f);
            ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
            var face = (Image)b.targetGraphic;
            // The soldier's picture up top.
            UnitPicture(b.transform, kind, new Vector2(0f, 56f), 120f);
            var label = b.GetComponentInChildren<TextMeshProUGUI>();
            label.text = Loc.T("army.kind." + kind);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true; label.fontSizeMin = 14f; label.fontSizeMax = 24f;
            label.rectTransform.offsetMin = new Vector2(6f, 58f);
            label.rectTransform.offsetMax = new Vector2(-6f, -146f);
            var badge = UiFactory.Box("Count", b.transform, new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(64f, 64f));
            badge.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(badge, colour, UiSprites.Circle).raycastTarget = false;
            var count = UiFactory.Text(badge, Army.Soldiers.ToString(), 30f, new Color(0.1f, 0.06f, 0.2f));
            groups.Add((b, count, face, colour));
        }

        /// <summary>
        /// A soldier of a kind drawn small: legs, a body in the player's blue, a head with a glowing visor, its weapon
        /// (sword, spear or bow) and, for a heavy one, a round shield and shoulder plates.
        /// </summary>
        public static void UnitPicture(Transform parent, GroupKind kind, Vector2 at, float size)
        {
            var box = UiFactory.Box("Unit", parent, new Vector2(0.5f, 0.5f), at, new Vector2(size, size));
            box.pivot = new Vector2(0.5f, 0.5f);
            float s = size;
            bool heavy = Army.Heavy(kind);
            int fam = Army.Family(kind);
            var team = ArmyBattleColours.Mine;
            var dark = new Color(0.16f, 0.17f, 0.24f);
            var metal = new Color(0.86f, 0.89f, 0.95f);
            var wood = new Color(0.6f, 0.4f, 0.22f);
            Image P(Vector2 p, Vector2 sz, Color c, Sprite sp, float rot = 0f)
            {
                var r = UiFactory.Box("P", box, new Vector2(0.5f, 0.5f), p * s, sz * s);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.localRotation = Quaternion.Euler(0f, 0f, rot);
                var img = UiFactory.Fill(r, c, sp, sp == UiSprites.Rounded ? 5f : 1f);
                img.raycastTarget = false;
                return img;
            }
            float w = heavy ? 1.15f : 1f;
            // The weapon behind the arm.
            if (fam == 0)
            {
                P(new Vector2(0.27f, 0.12f), new Vector2(0.07f, heavy ? 0.52f : 0.44f), metal, UiSprites.Rounded, -18f);
                P(new Vector2(0.22f, -0.1f), new Vector2(0.16f, 0.04f), wood, UiSprites.Rounded, -18f);
            }
            else if (fam == 1)
            {
                P(new Vector2(0.27f, 0.05f), new Vector2(0.04f, 0.86f), wood, UiSprites.Rounded);
                P(new Vector2(0.27f, 0.47f), new Vector2(0.09f, 0.14f), metal, UiSprites.Rounded, 45f);
            }
            else
            {
                for (int i = -2; i <= 2; i++)
                    P(new Vector2(0.3f - Mathf.Abs(i) * 0.035f, i * 0.1f), new Vector2(0.04f, 0.12f), wood, UiSprites.Rounded, i * 16f);
                P(new Vector2(0.24f, 0f), new Vector2(0.012f, 0.44f), metal, UiSprites.Rounded);
            }
            // Legs, body, belt, arms, head, visor.
            P(new Vector2(-0.07f * w, -0.3f), new Vector2(0.1f * w, 0.26f), dark, UiSprites.Rounded);
            P(new Vector2(0.07f * w, -0.3f), new Vector2(0.1f * w, 0.26f), dark, UiSprites.Rounded);
            P(new Vector2(0f, -0.02f), new Vector2(0.34f * w, 0.34f), team, UiSprites.Rounded);
            P(new Vector2(0f, -0.15f), new Vector2(0.36f * w, 0.05f), dark, UiSprites.Rounded);
            P(new Vector2(0.22f * w, -0.02f), new Vector2(0.08f, 0.26f), dark, UiSprites.Rounded);
            P(new Vector2(0f, 0.27f), new Vector2(0.24f, 0.2f), metal, UiSprites.Rounded);
            P(new Vector2(0f, 0.27f), new Vector2(0.18f, 0.05f), Color.Lerp(team, Color.white, 0.6f), UiSprites.Rounded);
            if (heavy)
            {
                P(new Vector2(-0.2f, 0.14f), new Vector2(0.13f, 0.07f), team, UiSprites.Rounded);
                P(new Vector2(0.2f, 0.14f), new Vector2(0.13f, 0.07f), team, UiSprites.Rounded);
                P(new Vector2(0f, 0.4f), new Vector2(0.04f, 0.09f), team, UiSprites.Rounded);
                P(new Vector2(-0.24f, -0.06f), new Vector2(0.3f, 0.3f), Color.Lerp(team, Color.black, 0.2f), UiSprites.Circle);
                P(new Vector2(-0.24f, -0.06f), new Vector2(0.12f, 0.12f), metal, UiSprites.Circle);
            }
            else P(new Vector2(-0.22f * w, -0.02f), new Vector2(0.08f, 0.26f), dark, UiSprites.Rounded);
        }

        /// <summary>A group tapped on the bar: it leads now (the bar stays).</summary>
        private void Pick(int g)
        {
            if (!groups[g].button.interactable) return;
            AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.2f);
            GroupPicked?.Invoke(g);
        }

        // ---------- The waiting room ----------

        public void ShowLobby(string me, int myRank, string mode)
        {
            hud.Hide(true);
            myCard.Set(me, myRank);
            theirCard.Set("?", -1);
            lobbyMode.text = mode;
            lobbyStatus.text = Loc.T("ch.searching");
            leaveButton.gameObject.SetActive(true);
            renameButton.gameObject.SetActive(true);
            searchBar.gameObject.SetActive(true);
            searching = true;
            searchT = 0f;
            lobby.Show();
            transform.SetAsLastSibling();
        }

        public void ShowRival(string them, int rank)
        {
            theirCard.Set(them, rank);
            lobbyStatus.text = Loc.T("ch.found");
            searching = false;
            searchBar.gameObject.SetActive(false);
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.2f);
        }

        public void HideLobby() => lobby.Hide();

        /// <summary>Once the server is asked for a rival there is no going back (the challenge counts).</summary>
        public void LockLobby()
        {
            leaveButton.gameObject.SetActive(false);
            renameButton.gameObject.SetActive(false);
        }

        public void SetMyCard(string me, int rank) => myCard.Set(me, rank);
        public void SetLobbyStatus(string text) => lobbyStatus.text = text;

        // ---------- The battle ----------

        public void ShowBattle()
        {
            lobby.Hide(true);
            hud.Show();
            // Under the game's own HUD (and every card), not over it: its finger area (turning the camera) covers the whole
            // screen, and on top it would swallow the pause and bag buttons and the briefing cards' taps.
            transform.SetAsFirstSibling();
        }

        public void HideAll()
        {
            lobby.Hide(true);
            hud.Hide(true);
            namePanel.Hide(true);
            armyBar = false;
            if (modes != null) modes.Hide(true);
        }

        public void SetSides(string me, int myRank, int myLeft, string them, int theirRank, int theirLeft)
        {
            myBar.text = me;
            theirBar.text = them;
            // Each side's rank insignia beside its name.
            var top = (RectTransform)myBar.rectTransform.parent.parent;
            if (myRank != myBadgeRank)
            {
                if (myBadge != null) Destroy(myBadge.gameObject);
                myBadge = RankArt.Badge(top, myRank, new Vector2(0f, 0.5f), new Vector2(76f, 0f), 76f);
                myBadgeRank = myRank;
                Unplated(myBadge);
            }
            if (theirRank != theirBadgeRank)
            {
                if (theirBadge != null) Destroy(theirBadge.gameObject);
                theirBadge = RankArt.Badge(top, theirRank, new Vector2(1f, 0.5f), new Vector2(-76f, 0f), 76f);
                theirBadgeRank = theirRank;
                Unplated(theirBadge);
            }
        }

        /// <summary>A rank badge without the see-through plate behind its insignia (only the insignia itself shows).</summary>
        private static void Unplated(RectTransform badge)
        {
            if (badge == null) return;
            foreach (var img in badge.GetComponentsInChildren<Image>(true))
                if (img.transform == badge || img.color.a < 0.9f) img.enabled = img.color.a >= 0.9f && img.transform != badge;
        }

        // ---------- The stage wheel ----------

        /// <summary>
        /// After the match is found: every duel stage's picture and name flashes by, slowing down, and stops on
        /// <paramref name="pick"/> (a gold frame, a beat), then it is gone. <paramref name="names"/> and <paramref name="pictures"/>
        /// in the stages' order.
        /// </summary>
        public System.Collections.IEnumerator StageWheel(string[] names, Texture[] pictures, int pick)
        {
            var root = UiFactory.Stretch("StageWheel", transform);
            var layer = root.gameObject.AddComponent<Canvas>(); layer.overrideSorting = true; layer.sortingOrder = 400;
            root.gameObject.AddComponent<GraphicRaycaster>();
            UiFactory.Fill(UiFactory.Stretch("Dim", root), new Color(0.02f, 0.02f, 0.07f, 0.94f)).gameObject.AddComponent<IgnoreSafeArea>();
            var title = Flat.Label(root, Loc.T("ch.stagePick"), new Vector2(0.5f, 0.5f), new Vector2(0f, 520f), new Vector2(900f, 80f), 48f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var frame = UiFactory.Box("Frame", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(930f, 640f));
            frame.pivot = new Vector2(0.5f, 0.5f);
            var frameFace = UiFactory.Fill(frame, Flat.CardHi, UiSprites.Rounded, 1.6f);
            var picBox = UiFactory.Rect("Picture", frame, Vector2.zero, Vector2.one, new Vector2(15f, 15f), new Vector2(-15f, -15f));
            picBox.gameObject.AddComponent<RectMask2D>();
            var pic = UiFactory.Stretch("Image", picBox).gameObject.AddComponent<RawImage>();
            pic.raycastTarget = false;
            var name = Flat.Label(root, "", new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(900f, 90f), 58f, Flat.Text, TextAlignmentOptions.Center, bold: true);
            name.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            int n = Mathf.Max(1, names.Length);
            // round and round, slowing: two full turns and on to the pick
            int steps = n * 2 + ((pick % n) + n) % n + 1;
            for (int i = 0; i < steps; i++)
            {
                int s = i % n;
                pic.texture = pictures[s];
                name.text = names[s];
                float k = i / (float)Mathf.Max(1, steps - 1);
                frame.localScale = Vector3.one * 1.04f;
                AudioManager.PlaySfx(Sfx.Click, 0.6f, 1.5f - 0.5f * k);
                float wait = Mathf.Lerp(0.06f, 0.38f, k * k);
                for (float e = 0f; e < wait; e += Time.unscaledDeltaTime)
                {
                    frame.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, e / wait);
                    yield return null;
                }
            }
            // landed: gold, a pop, a beat
            frameFace.color = Flat.Gold;
            name.color = Flat.Gold;
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.1f);
            for (float e = 0f; e < 0.3f; e += Time.unscaledDeltaTime)
            {
                frame.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(e / 0.3f * Mathf.PI));
                yield return null;
            }
            frame.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(0.9f);
            Destroy(root.gameObject);
        }

        // ---------- The duel's squad bar ----------

        private bool armyBar;
        private int barHearts = -1, barShields = -1;
        private Button heartButton, shieldButton;
        private TextMeshProUGUI heartText, shieldText;

        /// <summary>
        /// The duel's power buttons: the heart and the shield with what is left of each. The soldiers all fight from the
        /// start and are never called back, so there are no group tags (each side's soldiers left show at the top).
        /// </summary>
        public void ShowSquadBar(int[] left, bool[] attacking, int hearts, int shields)
        {
            if (!armyBar)
            {
                armyBar = true;
                pickPanel.gameObject.SetActive(false);
                if (heartButton == null) BuildPowerButtons();
                sideways = !IsSideways; // lay them out for the screen as it is now
                Layout();
                barHearts = barShields = -1;
            }
            if (hearts != barHearts) { barHearts = hearts; heartText.text = hearts.ToString(); heartButton.interactable = hearts > 0; }
            if (shields != barShields) { barShields = shields; shieldText.text = shields.ToString(); shieldButton.interactable = shields > 0; }
        }

        /// <summary>The heart and shield: round buttons like the floors' skills (icon, a badge with what is left), at the
        /// right edge.</summary>
        private void BuildPowerButtons()
        {
            heartButton = PowerButton(PowerUpType.Heart, new Vector2(-75f, 395f), () => HeartPressed?.Invoke(), out heartText);
            shieldButton = PowerButton(PowerUpType.Shield, new Vector2(-75f, 520f), () => ShieldPressed?.Invoke(), out shieldText);
        }

        private Button PowerButton(PowerUpType type, Vector2 at, Action onClick, out TextMeshProUGUI count)
        {
            var root = UiFactory.Box(type + "Button", (RectTransform)pickPanel.parent, new Vector2(1f, 0f), at, new Vector2(110f, 110f)); // (small: the floor stays in view)
            root.pivot = new Vector2(0.5f, 0.5f);
            var colour = type == PowerUpType.Heart ? new Color(1f, 0.5f, 0.62f) : new Color(0.45f, 0.75f, 1f);
            var bg = UiFactory.Chunky(root, UiFactory.StyleOf(colour), 1f, 0f, UiSprites.Circle);
            var icon = UiFactory.Box("Icon", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(SkillBar.IconBase, SkillBar.IconBase));
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.localScale = Vector3.one * (110f / SkillBar.IconBase * 0.92f);
            SkillBar.DrawIcon(icon, type);
            var badge = UiFactory.Box("Badge", root, new Vector2(1f, 1f), new Vector2(12f, 12f), new Vector2(46f, 46f));
            badge.pivot = new Vector2(1f, 1f);
            UiFactory.Fill(UiFactory.Stretch("BadgeFace", badge), new Color(1f, 0.3f, 0.36f, 1f), UiSprites.Circle).raycastTarget = false;
            count = UiFactory.OutlinedText("Count", badge, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(46f, 42f), "", 28f, new Color(0.3f, 0.02f, 0.08f));
            count.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => { AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.3f); onClick(); });
            root.gameObject.AddComponent<ButtonPress>();
            return button;
        }

        /// <summary>A finger on the heart and shield (not a move or a tap on the field).</summary>
        public bool IsOverControls(Vector2 screen) =>
            pickPanel != null && pickPanel.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(pickPanel, screen, null)
            || heartButton != null && heartButton.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)heartButton.transform, screen, null)
            || shieldButton != null && shieldButton.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)shieldButton.transform, screen, null);

        public void SetClock(string text) => clock.text = text;

        /// <summary>The duel's time (between the names) and score (between the health bars).</summary>
        public void SetClock(string time, string scoreText) { clock.text = time; score.text = scoreText; }

        private TextMeshProUGUI score;
        public void SetStatus(string text) => status.text = text;

        private static bool IsSideways => Screen.width > Screen.height;
        private bool sideways;

        /// <summary>Upright or sideways (the duel fought in TPS): the top bar, the status line and the heart and shield
        /// move to fit the short screen and keep clear of the TPS stick and buttons.</summary>
        private void Layout()
        {
            bool now = IsSideways;
            if (now == sideways || clock == null) return;
            sideways = now;
            var top = (RectTransform)myBar.rectTransform.parent.parent; // (the text sits in its own box)
            top.offsetMin = new Vector2(0f, -280f);
            top.offsetMax = new Vector2(0f, -196f);
            status.rectTransform.anchoredPosition = now ? new Vector2(0f, 330f) : new Vector2(-60f, 380f);
            if (heartButton == null) return;
            ((RectTransform)heartButton.transform).anchoredPosition = new Vector2(-75f, 395f);
            ((RectTransform)shieldButton.transform).anchoredPosition = new Vector2(-75f, 520f);
        }

        private void Update()
        {
            if (hud != null && hud.gameObject.activeInHierarchy) Layout();
            if (pickPanel != null && pickPanel.gameObject.activeSelf && pickPop < 1f)
            {
                pickPop = Mathf.Min(1f, pickPop + Time.unscaledDeltaTime / 0.25f);
                float k = 1f + 0.08f * Mathf.Sin(pickPop * Mathf.PI) - (1f - pickPop) * 0.3f;
                pickPanel.localScale = new Vector3(k, k, 1f);
            }
            if (!searching || searchBar == null) return;
            searchT += Time.unscaledDeltaTime;
            searchBar.anchoredPosition = new Vector2(Mathf.Sin(searchT * 3f) * 220f, 270f);
        }
    }
}
