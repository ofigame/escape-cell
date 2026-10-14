using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// The challenge's overall results (the menu's rank card opens it): the player's rank insignia and the trophy of the
    /// league they are in, their record in attack and defence, and the ladder of all thirteen ranks — each with its own
    /// insignia, the trophies it takes and its league's trophy — the ones reached lit, the current one marked.
    /// </summary>
    public class ChallengeStatsScreen : MonoBehaviour
    {
        private UiScreen screen;
        private RectTransform card, head;
        private readonly List<GameObject> built = new List<GameObject>();

        public static ChallengeStatsScreen Create(Transform parent)
        {
            var s = UiScreen.Create("ChallengeStats", parent, out var root);
            var c = root.gameObject.AddComponent<ChallengeStatsScreen>();
            c.screen = s;
            UiFactory.Dim(root, new Color(0.02f, 0.02f, 0.07f, 1f));
            c.card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 1720f));
            c.card.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.TextBox("Title", c.card, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 90f), Loc.T("ch.title"), 64f, Palette.UiGold, title: true);
            var close = UiFactory.MakeButton(c.card, Loc.T("btn.close"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(520f, 110f), () => s.Hide(), 46f);
            return c;
        }

        public void Hide() => screen.Hide(true);

        public void Show()
        {
            foreach (var g in built) if (g != null) Destroy(g);
            built.Clear();
            int rank = Challenge.Rank, league = Challenge.League;

            // The head: insignia, rank, trophies and the league's trophy, the record.
            head = UiFactory.Box("Head", card, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(940f, 330f));
            built.Add(head.gameObject);
            UiFactory.Fill(head, new Color(0.3f, 0.2f, 0.6f, 0.25f), UiSprites.Rounded, 1f).raycastTarget = false;
            RankArt.Badge(head, rank, new Vector2(0f, 0.5f), new Vector2(120f, 20f), 200f);
            RankArt.Trophy(head, league, new Vector2(1f, 0.5f), new Vector2(-120f, 20f), 200f);
            var name = UiFactory.TextBox("Rank", head, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(500f, 70f), Challenge.RankName(rank), 52f, Color.white, title: true);
            var name2 = UiFactory.TextBox("Name", head, new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(500f, 50f), Challenge.PlayerName, 34f, Palette.UiCyan, title: true);
            var cups = UiFactory.TextBox("Trophies", head, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(500f, 60f), Loc.F("ch.trophies", Challenge.Trophies), 44f, Palette.UiGold, title: true);
            var lg = UiFactory.TextBox("League", head, new Vector2(0.5f, 1f), new Vector2(0f, -208f), new Vector2(500f, 44f), Loc.F("ch.league", Challenge.LeagueName(league)), 30f, new Color(1f, 0.85f, 0.5f), title: true);
            var rec = UiFactory.TextBox("Record", head, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(900f, 40f),
                Loc.F("ch.record", Challenge.Wins, Challenge.Played) + "   ·   " + Loc.F("ch.defRecord", Challenge.DefWins, Challenge.DefLosses), 26f, new Color(1f, 1f, 1f, 0.85f), FontStyles.Normal);

            // The ladder.
            var title = UiFactory.TextBox("Ladder", card, new Vector2(0.5f, 1f), new Vector2(0f, -490f), new Vector2(900f, 50f), Loc.T("ch.ladder"), 32f, Palette.UiCyan, title: true);
            built.Add(title.transform.parent.gameObject);
            const float rowH = 70f;
            for (int i = 0; i < RankArt.Count; i++)
            {
                int r = RankArt.Count - 1 - i; // the highest on top
                bool reached = r <= rank, current = r == rank;
                var row = UiFactory.Box("Row" + r, card, new Vector2(0.5f, 1f), new Vector2(0f, -540f - i * rowH), new Vector2(920f, rowH - 6f));
                built.Add(row.gameObject);
                UiFactory.Fill(row, current ? new Color(1f, 0.8f, 0.3f, 0.28f) : reached ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0.03f), UiSprites.Rounded, 1.5f).raycastTarget = false;
                var badge = RankArt.Badge(row, r, new Vector2(0f, 0.5f), new Vector2(50f, 0f), 62f);
                var cup = RankArt.Trophy(row, r, new Vector2(1f, 0.5f), new Vector2(-50f, 0f), 58f);
                if (!reached) { Dim(badge); Dim(cup); }
                var label = UiFactory.TextBox("Name", row, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(330f, 60f),
                    Challenge.RankName(r) + (current ? "  (" + Loc.T("ch.you") + ")" : ""), 32f, reached ? Color.white : new Color(1f, 1f, 1f, 0.45f), title: true, align: TextAlignmentOptions.Left);
                UiFactory.TextBox("Needs", row, new Vector2(1f, 0.5f), new Vector2(-104f, 13f), new Vector2(380f, 34f), Loc.F("ch.needs", Challenge.RankAt[r]), 26f,
                    reached ? Palette.UiGold : new Color(1f, 0.85f, 0.4f, 0.4f), title: true, align: TextAlignmentOptions.Right);
                UiFactory.TextBox("League", row, new Vector2(1f, 0.5f), new Vector2(-104f, -15f), new Vector2(380f, 28f), Loc.F("ch.league", Challenge.LeagueName(r)), 20f,
                    reached ? new Color(1f, 0.9f, 0.7f, 0.85f) : new Color(1f, 1f, 1f, 0.3f), FontStyles.Normal, align: TextAlignmentOptions.Right);
            }
            screen.Show();
            transform.SetAsLastSibling();
        }

        private static void Dim(RectTransform art)
        {
            foreach (var img in art.GetComponentsInChildren<Image>()) img.color = new Color(img.color.r * 0.5f, img.color.g * 0.5f, img.color.b * 0.55f, img.color.a * 0.45f);
        }
    }
}
