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

        private UiScreen lobby, hud;
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
            UiFactory.Dim(l, new Color(0.03f, 0.03f, 0.1f, 0.9f)).gameObject.AddComponent<IgnoreSafeArea>();
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
            var leave = UiFactory.MakeButton(l, Loc.T("bag.back"), Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(420f, 110f), () => Leave?.Invoke(), 42f);
            ((RectTransform)leave.transform).pivot = new Vector2(0.5f, 0.5f);

            // The battle's overlay.
            hud = UiScreen.Create("ChallengeHud", root, out var h);
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
        }

        private TextMeshProUGUI Card(RectTransform parent, Vector2 at, Color colour)
        {
            var card = UiFactory.Box("Card", parent, new Vector2(0.5f, 0.5f), at, new Vector2(820f, 260f));
            card.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(card, new Color(colour.r, colour.g, colour.b, 0.18f), UiSprites.Rounded, 30f).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", card), new Color(colour.r, colour.g, colour.b, 0.8f), UiSprites.Ring, 30f).raycastTarget = false;
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
                groups[i].button.gameObject.SetActive(true);
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
