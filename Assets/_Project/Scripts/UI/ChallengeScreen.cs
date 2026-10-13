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
    /// The challenge's screens over the board: the waiting room (the player's card, "looking for a rival…", then the
    /// rival's card), and in the battle the two names and ranks with their soldiers left, the turn's clock and status,
    /// and the player's six group buttons (kind, soldiers left; the picked one lit) for the secret pick.
    /// </summary>
    public class ChallengeScreen : MonoBehaviour
    {
        public event Action<int> GroupPicked;
        public event Action Leave;
        /// <summary>A tap on the board (screen point): the game finds the group under it.</summary>
        public event Action<Vector2> BoardTapped;

        /// <summary>A drag across the board (screen pixels): the view slides over it.</summary>
        public event Action<Vector2> BoardDragged;

        /// <summary>A name typed and confirmed on the name screen; closing it without one.</summary>
        public event Action<string> NameSubmitted;
        public event Action NameClosed;
        /// <summary>"Change name" in the waiting room.</summary>
        public event Action RenamePressed;

        private class TapArea : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler, UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IDragHandler
        {
            public Action<Vector2> tapped, dragged;
            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) { if (!e.dragging) tapped?.Invoke(e.position); }
            public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData e) { }
            public void OnDrag(UnityEngine.EventSystems.PointerEventData e) => dragged?.Invoke(e.delta);
        }

        private UiScreen lobby, hud, namePanel;
        private TMP_InputField nameInput;
        private TextMeshProUGUI nameError;
        private Button nameOk, leaveButton, renameButton;
        private TextMeshProUGUI myCard, theirCard, lobbyStatus, myBar, theirBar, clock, status;
        private RectTransform searchBar;
        private readonly List<(Button button, TextMeshProUGUI count, Image face, Color colour)> groups = new List<(Button, TextMeshProUGUI, Image, Color)>();
        private int picked = -1;
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
            UiFactory.Dim(l, new Color(0.03f, 0.03f, 0.1f, 1f)).gameObject.AddComponent<IgnoreSafeArea>();
            UiFactory.TextBox("Title", l, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(960f, 110f), Loc.T("ch.title"), 76f, Palette.UiGold, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            myCard = Card(l, new Vector2(0f, 330f), ClashSystem.Mine);
            theirCard = Card(l, new Vector2(0f, -330f), ClashSystem.Theirs);
            UiFactory.TextBox("Vs", l, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 120f), "VS", 96f, Color.white, title: true).rectTransform.pivot = new Vector2(0.5f, 0.5f);
            lobbyStatus = UiFactory.TextBox("Status", l, new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(900f, 70f), "", 40f, Palette.UiCyan, title: true);
            lobbyStatus.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            searchBar = UiFactory.Box("Search", l, new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(160f, 14f));
            searchBar.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(searchBar, Palette.UiCyan, UiSprites.Rounded, 4f).raycastTarget = false;
            var leave = UiFactory.MakeButton(l, Loc.T("btn.close"), Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(420f, 110f), () => Leave?.Invoke(), 42f);
            ((RectTransform)leave.transform).pivot = new Vector2(0.5f, 0.5f);
            leaveButton = leave;
            renameButton = UiFactory.MakeButton(l, Loc.T("ch.rename"), Kind.Secondary, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(360f, 84f), () => RenamePressed?.Invoke(), 30f);
            ((RectTransform)renameButton.transform).pivot = new Vector2(0.5f, 0.5f);

            BuildNamePanel(root);

            // The battle's overlay.
            hud = UiScreen.Create("ChallengeHud", root, out var h);
            var tap = UiFactory.Stretch("BoardTap", h);
            UiFactory.Fill(tap, new Color(0f, 0f, 0f, 0f));
            var area = tap.gameObject.AddComponent<TapArea>();
            area.tapped = p => BoardTapped?.Invoke(p);
            area.dragged = d => BoardDragged?.Invoke(d);
            var top = UiFactory.Rect("Top", h, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -380f), new Vector2(0f, -240f)); // under the HUD's top row
            UiFactory.Fill(top, new Color(0.03f, 0.03f, 0.1f, 0.55f)).raycastTarget = false;
            myBar = UiFactory.TextBox("Me", top, new Vector2(0f, 0.5f), new Vector2(30f, -20f), new Vector2(420f, 120f), "", 32f, ClashSystem.Mine, title: true, align: TextAlignmentOptions.Left);
            myBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            theirBar = UiFactory.TextBox("Them", top, new Vector2(1f, 0.5f), new Vector2(-30f, -20f), new Vector2(420f, 120f), "", 32f, ClashSystem.Theirs, title: true, align: TextAlignmentOptions.Right);
            theirBar.rectTransform.pivot = new Vector2(1f, 0.5f);
            clock = UiFactory.TextBox("Clock", top, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(200f, 110f), "", 72f, Color.white, title: true);
            clock.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            status = UiFactory.TextBox("Status", h, new Vector2(0.5f, 0f), new Vector2(0f, 470f), new Vector2(980f, 70f), "", 38f, Palette.UiGold, title: true);
            status.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            for (int g = 0; g < Army.Groups; g++) GroupButton(h, g);
            foreach (var g in groups) g.button.gameObject.SetActive(false); // the groups are picked on the board itself
        }

        // ---------- The name ----------

        private void BuildNamePanel(RectTransform root)
        {
            namePanel = UiScreen.Create("ChallengeName", root, out var n);
            UiFactory.Dim(n, new Color(0.03f, 0.03f, 0.1f, 1f)).gameObject.AddComponent<IgnoreSafeArea>();
            UiFactory.TextBox("Title", n, new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(900f, 110f), Loc.T("ch.nameTitle"), 72f, Palette.UiGold, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var hint = UiFactory.TextBox("Hint", n, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(900f, 90f), Loc.T("ch.nameHint"), 34f, new Color(1f, 1f, 1f, 0.75f));
            hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            hint.textWrappingMode = TextWrappingModes.Normal;

            var box = UiFactory.Box("Input", n, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(780f, 140f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var bg = UiFactory.Fill(box, new Color(1f, 1f, 1f, 0.12f), UiSprites.Rounded, 1.2f);
            UiFactory.Fill(UiFactory.Stretch("Rim", box), new Color(ClashSystem.Mine.r, ClashSystem.Mine.g, ClashSystem.Mine.b, 0.9f), UiSprites.Ring, 1.2f).raycastTarget = false;
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

            nameError = UiFactory.TextBox("Error", n, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(900f, 70f), "", 34f, new Color(1f, 0.45f, 0.5f), title: true);
            nameError.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            nameOk = UiFactory.MakeButton(n, Loc.T("ch.nameOk"), Kind.Primary, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(460f, 130f), SubmitName, 48f);
            ((RectTransform)nameOk.transform).pivot = new Vector2(0.5f, 0.5f);
            var back = UiFactory.MakeButton(n, Loc.T("btn.close"), Kind.Secondary, new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(360f, 100f), () => NameClosed?.Invoke(), 38f);
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0.5f);
        }

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
            namePanel.Show();
            transform.SetAsLastSibling();
        }

        /// <summary>The name is being checked (the button waits), or the answer: an error, or "" to close the screen.</summary>
        public void NameChecking() { nameOk.interactable = false; nameError.text = Loc.T("ch.name.wait"); }
        public void NameRefused(string error) { nameOk.interactable = true; nameError.text = error; }
        public void HideName() => namePanel.Hide(true);
        public bool NameShowing => namePanel.IsVisible;

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
            float x = (g - 2.5f) * 172f;
            var b = UiFactory.MakeButton(parent, "", Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(x, 210f), new Vector2(160f, 220f), () => Pick(g), 26f);
            ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
            var face = (Image)b.targetGraphic;
            var label = b.GetComponentInChildren<TextMeshProUGUI>();
            label.text = Loc.T("army.kind." + kind);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true; label.fontSizeMin = 16f; label.fontSizeMax = 26f;
            label.rectTransform.offsetMin = new Vector2(8f, 70f);
            label.rectTransform.offsetMax = new Vector2(-8f, -8f);
            var badge = UiFactory.Box("Count", b.transform, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(76f, 76f));
            badge.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(badge, colour, UiSprites.Circle).raycastTarget = false;
            var count = UiFactory.Text(badge, "10", 36f, new Color(0.1f, 0.06f, 0.2f));
            groups.Add((b, count, face, colour));
        }

        /// <summary>A group picked on the board.</summary>
        public void PickFromBoard(int g)
        {
            if (picked >= 0) return;
            picked = g;
            AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.2f);
            GroupPicked?.Invoke(g);
        }

        private void Pick(int g)
        {
            if (picked >= 0 || !groups[g].button.interactable) return;
            picked = g;
            AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.2f);
            for (int i = 0; i < groups.Count; i++) groups[i].face.color = i == g ? groups[i].colour : new Color(1f, 1f, 1f, 0.12f);
            GroupPicked?.Invoke(g);
        }

        // ---------- The waiting room ----------

        public void ShowLobby(string me)
        {
            hud.Hide(true);
            myCard.text = me;
            theirCard.text = "?";
            lobbyStatus.text = Loc.T("ch.searching");
            leaveButton.gameObject.SetActive(true);
            renameButton.gameObject.SetActive(true);
            searchBar.gameObject.SetActive(true);
            searching = true;
            searchT = 0f;
            lobby.Show();
            transform.SetAsLastSibling();
        }

        public void ShowRival(string them)
        {
            theirCard.text = them;
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

        public void SetMyCard(string me) => myCard.text = me;
        public void SetLobbyStatus(string text) => lobbyStatus.text = text;

        // ---------- The battle ----------

        public void ShowBattle()
        {
            lobby.Hide(true);
            hud.Show();
            transform.SetAsLastSibling();
        }

        public void HideAll()
        {
            lobby.Hide(true);
            hud.Hide(true);
            namePanel.Hide(true);
        }

        public void SetSides(string me, int myLeft, string them, int theirLeft)
        {
            myBar.text = me + "\n" + Loc.F("ch.soldiers", myLeft);
            theirBar.text = them + "\n" + Loc.F("ch.soldiers", theirLeft);
        }

        public void SetClock(string text) => clock.text = text;
        public void SetStatus(string text) => status.text = text;

        /// <summary>A new turn: the groups with soldiers left can be picked.</summary>
        public void OpenPick(int[] left)
        {
            picked = -1;
            for (int i = 0; i < groups.Count; i++)
            {
                bool alive = left[i] > 0;
                groups[i].button.interactable = alive;
                groups[i].count.text = left[i].ToString();
                groups[i].face.color = alive ? new Color(groups[i].colour.r, groups[i].colour.g, groups[i].colour.b, 0.45f) : new Color(1f, 1f, 1f, 0.06f);
            }
        }

        public void ClosePick()
        {
            foreach (var g in groups) g.button.interactable = false;
        }

        /// <summary>The duel: the group bar goes away.</summary>
        public void HideGroups()
        {
            foreach (var g in groups) g.button.gameObject.SetActive(false);
            clock.text = "";
        }

        public int Picked => picked;

        private void Update()
        {
            if (!searching || searchBar == null) return;
            searchT += Time.unscaledDeltaTime;
            searchBar.anchoredPosition = new Vector2(Mathf.Sin(searchT * 3f) * 260f, 270f);
        }
    }
}
