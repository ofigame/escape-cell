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
        /// <summary>A kind of challenge was picked: 0 the Arena battle, 1 the Meydan (stage) battle.</summary>
        public event Action<int> ModePicked;
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
        private RectTransform pickPanel, pickBar, myBadge, theirBadge;
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
            lobbyStatus = Flat.Label(l, "", new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(940f, 60f), 34f, Flat.Text, TextAlignmentOptions.Center, bold: true, wrap: true);
            lobbyStatus.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var track = Flat.Panel(l, "Track", new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(600f, 10f), Flat.Line, 6f);
            track.pivot = new Vector2(0.5f, 0.5f);
            searchBar = UiFactory.Box("Search", l, new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(160f, 10f));
            searchBar.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(searchBar, Flat.Blue, UiSprites.Rounded, 6f).raycastTarget = false;
            renameButton = Flat.Button(l, Loc.T("ch.rename"), new Vector2(0.5f, 0f), new Vector2(-240f, 130f), new Vector2(440f, 104f), Flat.CardHi, Flat.Text, () => RenamePressed?.Invoke(), 32f);
            ((RectTransform)renameButton.transform).pivot = new Vector2(0.5f, 0.5f);
            leaveButton = Flat.Button(l, Loc.T("btn.close"), new Vector2(0.5f, 0f), new Vector2(240f, 130f), new Vector2(440f, 104f), Flat.CardHi, Flat.Text, () => Leave?.Invoke(), 32f);
            ((RectTransform)leaveButton.transform).pivot = new Vector2(0.5f, 0.5f);

            BuildNamePanel(root);
            BuildModes(root);

            // The battle's overlay.
            hud = UiScreen.Create("ChallengeHud", root, out var h);
            var tap = UiFactory.Stretch("BoardTap", h);
            UiFactory.Fill(tap, new Color(0f, 0f, 0f, 0f));
            var area = tap.gameObject.AddComponent<TapArea>();
            area.tapped = p => BoardTapped?.Invoke(p);
            area.dragged = d => BoardDragged?.Invoke(d);
            var top = UiFactory.Rect("Top", h, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -505f), new Vector2(0f, -365f)); // under the HUD's top row and the health bar
            UiFactory.Fill(top, new Color(0.03f, 0.03f, 0.1f, 0.55f)).raycastTarget = false;
            myBar = UiFactory.TextBox("Me", top, new Vector2(0f, 0.5f), new Vector2(136f, -20f), new Vector2(380f, 120f), "", 32f, ClashSystem.Mine, title: true, align: TextAlignmentOptions.Left);
            myBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            theirBar = UiFactory.TextBox("Them", top, new Vector2(1f, 0.5f), new Vector2(-136f, -20f), new Vector2(380f, 120f), "", 32f, ClashSystem.Theirs, title: true, align: TextAlignmentOptions.Right);
            theirBar.rectTransform.pivot = new Vector2(1f, 0.5f);
            clock = UiFactory.TextBox("Clock", top, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(200f, 110f), "", 72f, Color.white, title: true);
            clock.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            status = UiFactory.TextBox("Status", h, new Vector2(0.5f, 0f), new Vector2(0f, 470f), new Vector2(980f, 70f), "", 38f, Palette.UiGold, title: true);
            status.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // The secret pick: a pop-up card at the bottom with the six groups (picture, name, soldiers left) and the clock.
            pickPanel = UiFactory.Box("Pick", h, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(1050f, 400f));
            pickPanel.pivot = new Vector2(0.5f, 0f);
            UiFactory.Fill(pickPanel, new Color(0.05f, 0.05f, 0.12f, 0.92f), UiSprites.Rounded, 1f);
            UiFactory.Fill(UiFactory.Stretch("Rim", pickPanel), new Color(ClashSystem.Mine.r, ClashSystem.Mine.g, ClashSystem.Mine.b, 0.8f), UiSprites.Ring, 1f).raycastTarget = false;
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
            ModeCard(m, 0, new Vector2(0f, 330f), "arena", "ch.mode.arena", "ch.mode.arenaText", new Color(0.62f, 0.45f, 1f));
            ModeCard(m, 1, new Vector2(0f, -360f), "stages", "ch.mode.stages", "ch.mode.stagesText", new Color(1f, 0.6f, 0.25f));
            var back = Flat.Button(m, Loc.T("btn.close"), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(440f, 104f), Flat.CardHi, Flat.Text, () => modes.Hide(true), 34f);
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>A kind's card: a picture from the game itself, its name and a line about it; tapping it picks it.</summary>
        private void ModeCard(RectTransform parent, int mode, Vector2 at, string picture, string nameKey, string textKey, Color colour)
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
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.onClick.AddListener(() => { AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.1f); ModePicked?.Invoke(mode); });
            card.gameObject.AddComponent<ButtonPress>();
        }

        public void ShowModes()
        {
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
            var team = ClashSystem.Mine;
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

        /// <summary>A group picked on the board.</summary>
        public void PickFromBoard(int g)
        {
            if (picked >= 0) return;
            picked = g;
            AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.2f);
            pickPanel.gameObject.SetActive(false);
            GroupPicked?.Invoke(g);
        }

        private void Pick(int g)
        {
            if (picked >= 0 || !groups[g].button.interactable) return;
            picked = g;
            AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.2f);
            for (int i = 0; i < groups.Count; i++) groups[i].face.color = i == g ? groups[i].colour : new Color(1f, 1f, 1f, 0.12f);
            pickPanel.gameObject.SetActive(false);
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
            transform.SetAsLastSibling();
        }

        public void HideAll()
        {
            lobby.Hide(true);
            hud.Hide(true);
            namePanel.Hide(true);
            if (modes != null) modes.Hide(true);
        }

        public void SetSides(string me, int myRank, int myLeft, string them, int theirRank, int theirLeft)
        {
            myBar.text = me + "\n" + Loc.F("ch.soldiers", myLeft);
            theirBar.text = them + "\n" + Loc.F("ch.soldiers", theirLeft);
            // Each side's rank insignia beside its name.
            var top = (RectTransform)myBar.rectTransform.parent.parent;
            if (myRank != myBadgeRank)
            {
                if (myBadge != null) Destroy(myBadge.gameObject);
                myBadge = RankArt.Badge(top, myRank, new Vector2(0f, 0.5f), new Vector2(66f, -6f), 110f);
                myBadgeRank = myRank;
            }
            if (theirRank != theirBadgeRank)
            {
                if (theirBadge != null) Destroy(theirBadge.gameObject);
                theirBadge = RankArt.Badge(top, theirRank, new Vector2(1f, 0.5f), new Vector2(-66f, -6f), 110f);
                theirBadgeRank = theirRank;
            }
        }

        /// <summary>The pick's clock bar: the share of the ten seconds left.</summary>
        public void SetPickTime(float left) { if (pickFill != null) UiFactory.SetBar(pickFill, Mathf.Clamp01(left)); }

        public void SetClock(string text) => clock.text = text;
        public void SetStatus(string text) => status.text = text;

        /// <summary>A new turn: the pick card pops up; the groups with soldiers left can be picked.</summary>
        public void OpenPick(int[] left)
        {
            picked = -1;
            pickPanel.gameObject.SetActive(true);
            pickPop = 0f;
            SetPickTime(1f);
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
            pickPanel.gameObject.SetActive(false);
        }

        /// <summary>The duel: the group bar goes away.</summary>
        public void HideGroups()
        {
            pickPanel.gameObject.SetActive(false);
            clock.text = "";
        }

        public int Picked => picked;

        private void Update()
        {
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
