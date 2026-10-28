using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The player's profile (the menu's profile button and rank card open it), the whole screen: the name, the rank
    /// insignia and the league's trophy, the rank points; the matches won and lost; the cups won (the season cups and
    /// the weekly cup's steps, each with its trophy and how many); and the ladder of all thirteen ranks — the ones reached
    /// lit, the current one marked. Under it SETTINGS, CHANGE NAME and CLOSE.
    /// </summary>
    public class ChallengeStatsScreen : MonoBehaviour
    {
        /// <summary>CHANGE NAME on the profile.</summary>
        public event System.Action RenamePressed;
        /// <summary>SETTINGS on the profile (the settings open over it).</summary>
        public event System.Action SettingsPressed;

        private UiScreen screen;
        private TMPro.TextMeshProUGUI headerCoins;
        private RectTransform content;
        private ScrollRect scroll;
        private float y;
        private readonly List<GameObject> built = new List<GameObject>();

        public static ChallengeStatsScreen Create(Transform parent)
        {
            var s = UiScreen.Create("ChallengeStats", parent, out var root);
            var c = root.gameObject.AddComponent<ChallengeStatsScreen>();
            c.screen = s;
            c.Build(root);
            return c;
        }

        private void Build(RectTransform p)
        {
            Flat.Background(p);
            headerCoins = UiKit.ScreenHeader(p, screen, Loc.T("profile.title"), null);
            // The buttons stay above the banner's place; the rest scrolls between them and the title.
            float buttons = Monetization.Ads.BannerReserve + 70f;
            var viewport = UiFactory.Rect("Viewport", p, Vector2.zero, Vector2.one, new Vector2(30f, buttons + 80f), new Vector2(-30f, -200f)); // clear below the header
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;
            Bottom(p, Loc.T("settings.title"), -200f, Flat.CardHi, () => SettingsPressed?.Invoke());
            Bottom(p, Loc.T("profile.rename"), 200f, Flat.CardHi, () => RenamePressed?.Invoke()); // (the way out: the header's back)
        }

        private static void Bottom(RectTransform p, string text, float x, Color face, System.Action onClick)
        {
            var b = Flat.Button(p, text, new Vector2(0.5f, 0f), new Vector2(x, Monetization.Ads.BannerReserve + 70f), new Vector2(320f, 110f), face, Flat.Text, onClick, 34f);
            ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        public void Hide() => screen.Hide(true);

        public bool IsOpen => screen.IsVisible;

        public void Show()
        {
            Refresh();
            screen.Show();
            UiKit.RefreshCurrency(headerCoins);
            transform.SetAsLastSibling();
        }

        /// <summary>Builds the page again (the server's latest record came).</summary>
        public void Refresh()
        {
            foreach (var g in built) if (g != null) Destroy(g);
            built.Clear();
            y = 0f;
            Head();
            Titles();
            Record();
            Cups();
            Ladder();
            content.sizeDelta = new Vector2(0f, -y + 30f);
            content.anchoredPosition = Vector2.zero;
            scroll.velocity = Vector2.zero;
        }

        // ---------- The parts ----------

        private RectTransform Card(float height, Color colour)
        {
            var c = Flat.Panel(content, "Row", new Vector2(0.5f, 1f), new Vector2(0f, y - height * 0.5f), new Vector2(1000f, height - 16f), colour);
            c.pivot = new Vector2(0.5f, 0.5f);
            built.Add(c.gameObject);
            y -= height;
            return c;
        }

        private static TextMeshProUGUI Text(RectTransform parent, string text, Vector2 anchor, Vector2 at, Vector2 size, float fontSize, Color colour, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
        {
            var t = Flat.Label(parent, text, anchor, at, size, fontSize, colour, align, bold: bold);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return t;
        }

        /// <summary>The name, the rank's insignia and the league's trophy, the rank points and the league.</summary>
        private void Head()
        {
            int rank = Challenge.Rank, league = Challenge.League;
            var head = Card(380f, new Color(0.22f, 0.16f, 0.45f, 0.85f));
            RankArt.Badge(head, rank, new Vector2(0f, 0.5f), new Vector2(130f, 0f), 210f);
            RankArt.Trophy(head, league, new Vector2(1f, 0.5f), new Vector2(-130f, 0f), 200f);
            string name = string.IsNullOrEmpty(Challenge.PlayerName) ? "foi" : Challenge.PlayerName;
            Text(head, name, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(520f, 80f), 60f, Palette.UiCyan);
            Text(head, Challenge.RankName(rank), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(520f, 64f), 46f, Color.white);
            Text(head, Loc.F("ch.trophies", Challenge.Trophies), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(520f, 56f), 40f, Palette.UiGold);
            Text(head, Loc.F("ch.league", Challenge.LeagueName(league)), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(520f, 44f), 30f, new Color(1f, 0.85f, 0.5f), bold: false);
        }

        /// <summary>The title on the player's card: the button shows the next one earned (a world's conqueror, a master…).</summary>
        private void Titles()
        {
            var r = Card(160f, Flat.Card);
            var earned = TitleBook.Earned();
            var label = Text(r, TitleBook.Shown ?? Loc.T("profile.noTitle"), new Vector2(0.5f, 0.5f), new Vector2(-130f, 0f), new Vector2(660f, 70f), 38f, Palette.UiGold);
            label.enableAutoSizing = true; label.fontSizeMin = 22f; label.fontSizeMax = 38f;
            if (earned.Count < 2) return; // (nothing to choose between)
            var b = Flat.Button(r, Loc.T("profile.changeTitle"), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(250f, 96f), Flat.CardHi, Flat.Text, () => label.text = TitleBook.PickNext() ?? label.text, 28f);
            ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>The matches: played, won and lost (all kinds together), the details under them.</summary>
        private void Record()
        {
            var o = Challenge.OtherRecord;
            int won = Challenge.Wins + o.aiWins + o.friendWins;
            int played = Challenge.Played + o.aiPlayed + o.friendPlayed;
            int lost = Mathf.Max(0, played - won);
            var r = Card(300f, Flat.Card);
            Stat(r, -320f, played, Loc.T("profile.played"), Color.white);
            Stat(r, 0f, won, Loc.T("profile.won"), new Color(0.45f, 1f, 0.55f));
            Stat(r, 320f, lost, Loc.T("profile.lost"), Flat.Red);
            Text(r, Loc.F("ch.record", Challenge.Wins, Challenge.Played) + "   ·   " + Loc.F("ch.defRecord", Challenge.DefWins, Challenge.DefLosses)
                + "\n" + Loc.F("ch.otherRecord", o.aiWins, o.aiPlayed, o.friendWins, o.friendPlayed),
                new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(940f, 72f), 24f, Flat.Muted, bold: false).textWrappingMode = TextWrappingModes.Normal;
        }

        private static void Stat(RectTransform parent, float x, int value, string label, Color colour)
        {
            var box = Flat.Panel(parent, "Stat", new Vector2(0.5f, 1f), new Vector2(x, -95f), new Vector2(300f, 150f), Flat.CardHi);
            box.pivot = new Vector2(0.5f, 0.5f);
            Text(box, value.ToString(), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(280f, 80f), 66f, colour);
            Text(box, label, new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(280f, 44f), 28f, Flat.Muted);
        }

        /// <summary>The trophy case: every cup won, its trophy and how many (or a word on how to win one).</summary>
        private void Cups()
        {
            var cups = new List<(int art, string name, int count)>();
            for (int s = TrophyCase.WeekSteps - 1; s >= 0; s--)
                if (TrophyCase.Week(s) > 0) cups.Add((TrophyCase.WeekArt[s], Loc.T("profile.week." + s), TrophyCase.Week(s)));
            for (int r = Challenge.RankAt.Length - 1; r >= 0; r--)
                if (TrophyCase.Season(r) > 0) cups.Add((r, Loc.F("profile.seasonCup", Challenge.LeagueName(r)), TrophyCase.Season(r)));
            const int perRow = 4;
            int rows = Mathf.CeilToInt(cups.Count / (float)perRow);
            var c = Card(cups.Count == 0 ? 210f : 110f + rows * 285f, Flat.Card);
            Text(c, Loc.T("profile.cups"), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(900f, 60f), 40f, Palette.UiGold);
            if (cups.Count == 0)
            {
                var t = Text(c, Loc.T("profile.noCups"), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(900f, 90f), 28f, Flat.Muted, bold: false);
                t.textWrappingMode = TextWrappingModes.Normal;
                return;
            }
            for (int i = 0; i < cups.Count; i++)
            {
                var (art, name, count) = cups[i];
                float x = (i % perRow - (perRow - 1) * 0.5f) * 240f, top = -110f - (i / perRow) * 285f;
                var cell = Flat.Panel(c, "Cup", new Vector2(0.5f, 1f), new Vector2(x, top - 132f), new Vector2(228f, 270f), Flat.CardHi);
                cell.pivot = new Vector2(0.5f, 0.5f);
                RankArt.Trophy(cell, art, new Vector2(0.5f, 1f), new Vector2(0f, -64f), 100f);
                Text(cell, "×" + count, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(200f, 44f), 36f, Palette.UiGold);
                var n = Text(cell, name, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(214f, 60f), 22f, Color.white);
                n.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        /// <summary>The ladder of the thirteen ranks, the highest on top.</summary>
        private void Ladder()
        {
            int rank = Challenge.Rank;
            var head = Card(80f, new Color(0f, 0f, 0f, 0f));
            Text(head, Loc.T("ch.ladder"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 56f), 34f, Palette.UiCyan);
            for (int i = 0; i < RankArt.Count; i++)
            {
                int r = RankArt.Count - 1 - i;
                bool reached = r <= rank, current = r == rank;
                var row = Card(84f, current ? new Color(1f, 0.8f, 0.3f, 0.28f) : reached ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0.03f));
                var badge = RankArt.Badge(row, r, new Vector2(0f, 0.5f), new Vector2(50f, 0f), 62f);
                var cup = RankArt.Trophy(row, r, new Vector2(1f, 0.5f), new Vector2(-50f, 0f), 58f);
                if (!reached) { Dim(badge); Dim(cup); }
                var label = Flat.Label(row, Challenge.RankName(r) + (current ? "  (" + Loc.T("ch.you") + ")" : ""), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(360f, 60f), 32f,
                    reached ? Color.white : new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Left, bold: true);
                label.rectTransform.pivot = new Vector2(0f, 0.5f);
                var needs = Flat.Label(row, Loc.F("ch.needs", Challenge.RankAt[r]), new Vector2(1f, 0.5f), new Vector2(-104f, 13f), new Vector2(380f, 34f), 26f,
                    reached ? Palette.UiGold : new Color(1f, 0.85f, 0.4f, 0.4f), TextAlignmentOptions.Right, bold: true);
                needs.rectTransform.pivot = new Vector2(1f, 0.5f);
                var lg = Flat.Label(row, Loc.F("ch.league", Challenge.LeagueName(r)), new Vector2(1f, 0.5f), new Vector2(-104f, -15f), new Vector2(380f, 28f), 20f,
                    reached ? new Color(1f, 0.9f, 0.7f, 0.85f) : new Color(1f, 1f, 1f, 0.3f), TextAlignmentOptions.Right);
                lg.rectTransform.pivot = new Vector2(1f, 0.5f);
            }
        }

        private static void Dim(RectTransform art)
        {
            foreach (var img in art.GetComponentsInChildren<Image>()) img.color = new Color(img.color.r * 0.5f, img.color.g * 0.5f, img.color.b * 0.55f, img.color.a * 0.45f);
        }
    }
}
