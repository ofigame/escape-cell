using System;
using System.Collections;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The challenge's competitions in one screen, by tabs: the SEASON (this month's: days left, the best rank reached,
    /// the reward it is worth, last season's reward to take), the LEADERBOARDS (the world, the player's country, its
    /// friends, the clans), the weekend CUP (wins from Friday to Sunday, a reward at each step, the top fifty), the CLAN
    /// (the player's: its members; or found one, or find one and join) and the QUESTS (three a day, three a week).
    /// </summary>
    public class CompetitionScreen : MonoBehaviour
    {
        private enum Tab { Season, Top, Cup, Clan } // (the quests have their own screen, the duel's and the levels' together)

        /// <summary>Coins came in (a reward taken): the menu's purse is refreshed.</summary>
        public event Action CoinsChanged;

        private UiScreen screen;
        private TMPro.TextMeshProUGUI headerCoins;
        private RectTransform content, subBar;
        private TextMeshProUGUI status;
        private readonly Button[] tabs = new Button[4];
        private Tab tab;
        private string topScope = "world";
        private TMP_InputField input;
        private int clanBadge;

        public static CompetitionScreen Create(Transform root)
        {
            var s = UiScreen.Create("Competitions", root, out var p);
            var c = p.gameObject.AddComponent<CompetitionScreen>();
            c.screen = s;
            c.Build(p);
            return c;
        }

        public void Show()
        {
            transform.SetAsLastSibling();
            screen.Show();
            UiKit.RefreshCurrency(headerCoins);
            Open(Tab.Season);
        }

        private void Build(RectTransform p)
        {
            Flat.Background(p);
            headerCoins = UiKit.ScreenHeader(p, screen, Loc.T("comp.title"), null);
            string[] names = { "comp.season", "comp.top", "comp.cup", "comp.clan" };
            for (int i = 0; i < names.Length; i++)
            {
                var t = (Tab)i;
                tabs[i] = Flat.Button(p, Loc.T(names[i]), new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 236f, -235f), new Vector2(222f, 84f), Flat.CardHi, Flat.Text, () => { Click(); Open(t); }, 28f);
                ((RectTransform)tabs[i].transform).pivot = new Vector2(0.5f, 0.5f);
            }
            subBar = UiFactory.Box("Sub", p, new Vector2(0.5f, 1f), new Vector2(0f, -335f), new Vector2(1000f, 90f));
            subBar.pivot = new Vector2(0.5f, 0.5f);
            status = Flat.Label(p, "", new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 60f), new Vector2(940f, 60f), 28f, Flat.Gold, TextAlignmentOptions.Center, wrap: true);
            status.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var viewport = UiFactory.Rect("Viewport", p, Vector2.zero, Vector2.one, new Vector2(40f, Monetization.Ads.BannerReserve + 110f), new Vector2(-40f, -390f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, UiKit.A(UiKit.Style.black, 0f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;
        }

        private static void Click() => AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.1f);

        // ---------- Tabs ----------

        private void Open(Tab t)
        {
            tab = t;
            for (int i = 0; i < tabs.Length; i++) ((Image)tabs[i].targetGraphic).color = i == (int)t ? UiKit.Style.orange : Flat.CardHi;
            Clear();
            for (int i = subBar.childCount - 1; i >= 0; i--) Destroy(subBar.GetChild(i).gameObject);
            status.text = "";
            StopAllCoroutines();
            switch (t)
            {
                case Tab.Season: StartCoroutine(Season()); break;
                case Tab.Top: TopBar(); StartCoroutine(Top()); break;
                case Tab.Cup: StartCoroutine(Cup()); break;
                default: StartCoroutine(ClanTab()); break;
            }
        }

        private float y;

        private void Clear()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            y = -10f;
            content.anchoredPosition = Vector2.zero;
        }

        private void Fit() => content.sizeDelta = new Vector2(0f, -y + 20f);

        private void Waiting() => status.text = Loc.T("comp.loading");
        private void Offline() => status.text = Loc.T("live.offline");

        // ---------- Rows ----------

        private RectTransform Card(float height, Color colour)
        {
            var c = Flat.Panel(content, "Row", new Vector2(0.5f, 1f), new Vector2(0f, y - height * 0.5f), new Vector2(980f, height - 14f), colour);
            c.pivot = new Vector2(0.5f, 0.5f);
            y -= height;
            return c;
        }

        private TextMeshProUGUI Text(RectTransform parent, string text, Vector2 anchor, Vector2 at, Vector2 size, float fontSize, Color colour, TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false)
        {
            var t = Flat.Label(parent, text, anchor, at, size, fontSize, colour, align, bold: bold, wrap: true);
            t.rectTransform.pivot = new Vector2(anchor.x, 0.5f);
            return t;
        }

        private Button SmallButton(RectTransform parent, string text, Color face, Action onClick)
        {
            var b = Flat.Button(parent, text, new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(210f, 84f), face, Flat.Text, () => { Click(); onClick(); }, 28f);
            ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
            return b;
        }

        /// <summary>A leaderboard line: place, rank badge, name, points (the player's own lit).</summary>
        private void Line(int place, int rank, string name, string value, bool me)
        {
            var row = Card(96f, me ? UiKit.Style.panelHi : Flat.Card);
            Text(row, place + ".", new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(90f, 80f), 32f, Flat.Gold, bold: true);
            if (rank >= 0) RankArt.Badge(row, rank, new Vector2(0f, 0.5f), new Vector2(160f, 0f), 72f);
            Text(row, name, new Vector2(0f, 0.5f), new Vector2(215f, 0f), new Vector2(520f, 80f), 32f, Flat.Text, bold: me);
            Text(row, value, new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(260f, 80f), 30f, Flat.Gold, TextAlignmentOptions.Right, bold: true);
        }

        // ---------- The season ----------

        private IEnumerator Season()
        {
            Waiting();
            FoiNet.SeasonInfo s = null;
            yield return FoiNet.Season(r => s = r);
            if (s == null) { Offline(); yield break; }
            status.text = "";
            int month = s.season % 12 + 1, year = s.season / 12;
            var days = Mathf.Max(0, (int)Math.Ceiling((DateTimeOffset.FromUnixTimeSeconds(s.endsAt) - DateTimeOffset.UtcNow).TotalDays));
            var head = Card(130f, Flat.Card);
            Text(head, Loc.F("comp.seasonName", Loc.T("month." + month), year), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(900f, 60f), 40f, Flat.Gold, TextAlignmentOptions.Center, true);
            Text(head, Loc.F("comp.daysLeft", days), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(900f, 44f), 28f, Flat.Muted, TextAlignmentOptions.Center);
            var best = Card(140f, Flat.Card);
            RankArt.Badge(best, s.bestRank, new Vector2(0f, 0.5f), new Vector2(80f, 0f), 96f);
            Text(best, Loc.F("comp.best", Challenge.RankName(s.bestRank), s.best), new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(480f, 110f), 30f, Flat.Text);
            Text(best, Loc.F("comp.worth", 200 + 150 * Mathf.Clamp(s.bestRank, 0, 12)), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(300f, 90f), 30f, Flat.Gold, TextAlignmentOptions.Right, true);
            if (s.reward != null && s.reward.coins > 0)
            {
                var r = Card(150f, UiKit.Style.brown);
                int rm = s.reward.season % 12 + 1;
                Text(r, Loc.F("comp.reward", Loc.T("month." + rm), Challenge.RankName(s.reward.rank), s.reward.coins), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(680f, 130f), 30f, Flat.Text, bold: true);
                int cupRank = s.reward.rank;
                SmallButton(r, Loc.T("comp.claim"), UiKit.Style.warning, () => StartCoroutine(ClaimSeason(cupRank)));
            }
            var info = Card(220f, Flat.Card);
            Text(info, Loc.T("comp.seasonHelp"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 200f), 27f, Flat.Muted, TextAlignmentOptions.Center);
            Fit();
        }

        private IEnumerator ClaimSeason(int rank)
        {
            int coins = 0;
            yield return FoiNet.ClaimSeason(c => coins = c);
            if (coins > 0)
            {
                TrophyCase.AddSeason(rank); // the cup into the profile's trophy case
                SaveData.Coins += coins; CoinsChanged?.Invoke(); AudioManager.PlaySfx(Sfx.Coin, 1f, 1f);
            }
            Open(Tab.Season);
            if (coins > 0) status.text = Loc.F("comp.got", coins);
        }

        // ---------- The leaderboards ----------

        private void TopBar()
        {
            string[] scopes = { "world", "country", "friends", "clans" };
            for (int i = 0; i < 4; i++)
            {
                string sc = scopes[i];
                var b = Flat.Button(subBar, Loc.T("comp.top." + sc), new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 245f, 0f), new Vector2(230f, 76f), sc == topScope ? Flat.Blue : Flat.CardHi, Flat.Text, () => { Click(); topScope = sc; Open(Tab.Top); }, 26f);
                ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
            }
        }

        private IEnumerator Top()
        {
            Waiting();
            FoiNet.TopList list = null;
            if (topScope == "clans") yield return FoiNet.Clans(null, r => list = r);
            else yield return FoiNet.Top(topScope, r => list = r);
            if (list == null) { Offline(); yield break; }
            status.text = list.place > 0 ? Loc.F("comp.place", list.place) : "";
            if (list.list == null || list.list.Length == 0) { status.text = Loc.T("comp.empty"); yield break; }
            for (int i = 0; i < list.list.Length; i++)
            {
                var e = list.list[i];
                if (topScope == "clans") Line(i + 1, -1, e.name + "  (" + e.size + ")", e.score.ToString("N0"), false);
                else Line(i + 1, e.rank, e.name, e.trophies.ToString("N0"), e.me);
            }
            Fit();
        }

        // ---------- The weekend cup ----------

        private IEnumerator Cup()
        {
            yield return CupOnly();
            // the weekly rule mode's board under the cup
            FoiNet.WeeklyBoard board = null;
            yield return FoiNet.Weekly(b => board = b);
            if (board?.list == null) yield break;
            var head = Card(70f, UiKit.A(UiKit.Style.black, 0f));
            Text(head, Loc.F("wk.board", Loc.T("wk.rule." + WeeklyMode.ThisWeek), board.wins), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(900f, 60f), 30f, Flat.Gold, bold: true);
            for (int i = 0; i < board.list.Length; i++) Line(i + 1, -1, board.list[i].name, board.list[i].wins.ToString(), board.list[i].me);
            Fit();
        }

        private IEnumerator CupOnly()
        {
            Waiting();
            FoiNet.CupInfo cup = null;
            yield return FoiNet.Cup(r => cup = r);
            if (cup == null) { Offline(); yield break; }
            status.text = "";
            var left = DateTimeOffset.FromUnixTimeSeconds(cup.endsAt) - DateTimeOffset.UtcNow;
            string when = left.TotalHours >= 24 ? Loc.F("comp.days", (int)left.TotalDays) : Loc.F("comp.hours", Mathf.Max(1, (int)left.TotalHours));
            var head = Card(140f, Flat.Card);
            Text(head, Loc.T(cup.open ? "comp.cupOpen" : "comp.cupClosed"), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 60f), 36f, Flat.Gold, TextAlignmentOptions.Center, true);
            Text(head, Loc.F(cup.open ? "comp.cupEnds" : "comp.cupStarts", when) + "   ·   " + Loc.F("comp.cupWins", cup.wins), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(900f, 50f), 28f, Flat.Text, TextAlignmentOptions.Center);
            if (cup.steps != null)
                for (int i = 0; i < cup.steps.Length; i++)
                {
                    int step = i;
                    bool taken = (cup.claimed & (1 << i)) != 0, ready = cup.wins >= cup.steps[i];
                    var r = Card(110f, taken ? UiKit.Style.panelSuccess : Flat.Card);
                    Text(r, Loc.F("comp.cupStep", cup.steps[i], cup.coins[i]), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(650f, 90f), 30f, ready ? Flat.Text : Flat.Muted, bold: ready);
                    if (taken) Text(r, Loc.T("comp.taken"), new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(200f, 80f), 30f, UiKit.Style.successLight, TextAlignmentOptions.Center, true);
                    else if (ready) SmallButton(r, Loc.T("comp.claim"), UiKit.Style.warning, () => StartCoroutine(ClaimCup(step)));
                }
            if (cup.list != null && cup.list.Length > 0)
            {
                var t = Card(70f, UiKit.A(UiKit.Style.black, 0f));
                Text(t, Loc.T("comp.cupTop"), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(900f, 60f), 30f, Flat.Muted, bold: true);
                for (int i = 0; i < cup.list.Length; i++) Line(i + 1, -1, cup.list[i].name, Loc.F("comp.cupWinsShort", cup.list[i].wins), cup.list[i].me);
            }
            Fit();
        }

        private IEnumerator ClaimCup(int step)
        {
            int coins = 0;
            yield return FoiNet.ClaimCup(step, c => coins = c);
            if (coins > 0)
            {
                TrophyCase.AddWeek(step); // the cup into the profile's trophy case
                SaveData.Coins += coins; CoinsChanged?.Invoke(); AudioManager.PlaySfx(Sfx.Coin, 1f, 1f);
            }
            Open(Tab.Cup);
            if (coins > 0) status.text = Loc.F("comp.got", coins);
        }

        // ---------- The clan ----------

        private static readonly Color[] BadgeColours =
        {
            UiKit.Style.mine, UiKit.Style.theirs, UiKit.Style.success, UiKit.Style.warning,
            UiKit.Style.purple, UiKit.Style.info, UiKit.Style.danger, UiKit.Style.text,
            UiKit.Style.bronze, UiKit.Style.panelSuccess, UiKit.Style.orange, UiKit.Style.green,
        };

        private void Badge(RectTransform parent, int badge, string name, Vector2 at, float size)
        {
            var b = UiFactory.Box("Badge", parent, new Vector2(0f, 0.5f), at, new Vector2(size, size));
            b.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(b, BadgeColours[Mathf.Clamp(badge, 0, BadgeColours.Length - 1)], UiSprites.Circle).raycastTarget = false;
            var t = Flat.Label(b, string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), size * 0.5f, Color.white, TextAlignmentOptions.Center, bold: true);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private IEnumerator ClanTab()
        {
            Waiting();
            FoiNet.ClanInfo clan = null; bool ok = false;
            yield return FoiNet.Clan(null, (c, k) => { clan = c; ok = k; });
            if (!ok) { Offline(); yield break; }
            status.text = "";
            if (clan != null) { ShowClan(clan); yield break; }
            // No clan: found one (a name and a colour) or find one.
            var make = Card(270f, Flat.Card);
            Text(make, Loc.T("comp.clanNew"), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 50f), 32f, Flat.Gold, TextAlignmentOptions.Center, true);
            input = NameBox(make, new Vector2(-150f, 15f));
            for (int i = 0; i < 6; i++)
            {
                int b = i * 2;
                var chip = Flat.Button(make, "", new Vector2(0.5f, 0f), new Vector2(-325f + i * 90f, 50f), new Vector2(70f, 70f), BadgeColours[b], Color.white, () => { clanBadge = b; Open(Tab.Clan); }, 20f);
                ((RectTransform)chip.transform).pivot = new Vector2(0.5f, 0.5f);
                if (b == clanBadge) chip.transform.localScale = Vector3.one * 1.2f;
            }
            var create = Flat.Button(make, Loc.T("comp.clanCreate"), new Vector2(0.5f, 0f), new Vector2(300f, 50f), new Vector2(280f, 84f), Flat.Blue, Flat.Text, () => { Click(); StartCoroutine(CreateClan()); }, 28f);
            ((RectTransform)create.transform).pivot = new Vector2(0.5f, 0.5f);
            var find = Card(70f, UiKit.A(UiKit.Style.black, 0f));
            Text(find, Loc.T("comp.clanFind"), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(900f, 60f), 30f, Flat.Muted, bold: true);
            FoiNet.TopList list = null;
            yield return FoiNet.Clans(null, r => list = r);
            if (list?.list != null)
                foreach (var e in list.list)
                {
                    var row = Card(100f, Flat.Card);
                    Badge(row, e.badge, e.name, new Vector2(60f, 0f), 64f);
                    Text(row, e.name + "\n<size=24><color=#9EA6C2>" + Loc.F("comp.clanLine", e.size, Competitions.ClanSize, e.score.ToString("N0")) + "</color></size>", new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(560f, 90f), 30f, Flat.Text, bold: true);
                    string id = e.id;
                    if (e.size < Competitions.ClanSize) SmallButton(row, Loc.T("comp.clanJoin"), UiKit.Style.success, () => StartCoroutine(JoinClan(id)));
                }
            Fit();
        }

        private TMP_InputField NameBox(RectTransform parent, Vector2 at)
        {
            var box = UiFactory.Box("Input", parent, new Vector2(0.5f, 0.5f), at, new Vector2(560f, 90f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var bg = UiFactory.Fill(box, Flat.CardHi, UiSprites.Rounded, 1.6f);
            var area = UiFactory.Rect("TextArea", box, Vector2.zero, Vector2.one, new Vector2(24f, 8f), new Vector2(-24f, -8f));
            area.gameObject.AddComponent<RectMask2D>();
            var text = UiFactory.Text(area, "", 38f, Color.white);
            text.enableAutoSizing = false;
            text.textPreprocessor = null;
            var ph = UiFactory.Text(area, Loc.T("comp.clanName"), 34f, UiKit.A(UiKit.Style.white, 0.3f));
            ph.enableAutoSizing = false;
            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = bg;
            field.characterLimit = 16;
            field.lineType = TMP_InputField.LineType.SingleLine;
            return field;
        }

        /// <summary>A cell goal step taken: its chest (bronze; bronze and 10 core parts; gold).</summary>
        private IEnumerator TakeCellGoal(int step)
        {
            bool ok = false;
            yield return FoiNet.ClanGoal(step, r => ok = r);
            if (ok)
            {
                DuelChests.Add(step == 2 ? ChestTier.Advanced : ChestTier.Mid);
                if (step == 1) CoreParts.Add(10);
                AudioManager.PlaySfx(Sfx.Coin, 1f, 1f);
                CoinsChanged?.Invoke();
            }
            Open(Tab.Clan);
            if (ok) status.text = Loc.T("cell.gift" + step);
        }

        private void ShowClan(FoiNet.ClanInfo clan)
        {
            var head = Card(180f, Flat.Card);
            Badge(head, clan.badge, clan.name, new Vector2(90f, 0f), 110f);
            Text(head, clan.name, new Vector2(0f, 0.5f), new Vector2(170f, 25f), new Vector2(560f, 60f), 42f, Flat.Gold, bold: true);
            Text(head, Loc.F("comp.clanLine", clan.size, Competitions.ClanSize, clan.score.ToString("N0")), new Vector2(0f, 0.5f), new Vector2(170f, -35f), new Vector2(560f, 50f), 28f, Flat.Muted);
            if (clan.mine) SmallButton(head, Loc.T("comp.clanLeave"), UiKit.Style.brown, () => StartCoroutine(LeaveClan()));
            // the cell's weekly goal: its members' wins together, a cell chest for everyone at each step
            if (clan.mine && clan.goals != null && clan.goals.Length > 0)
            {
                var goal = Card(70f, UiKit.A(UiKit.Style.black, 0f));
                Text(goal, Loc.F("cell.goal", clan.weekWins), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(900f, 60f), 30f, Flat.Gold, bold: true);
                for (int s = 0; s < clan.goals.Length; s++)
                {
                    int step = s;
                    bool taken = (clan.goalClaimed & (1 << s)) != 0, reached = clan.weekWins >= clan.goals[s];
                    var r = Card(100f, taken ? UiKit.Style.panelSuccess : Flat.Card);
                    Text(r, Loc.F("cell.step", clan.goals[s]) + "\n<size=24><color=#FFC64D>" + Loc.T("cell.gift" + s) + "</color></size>", new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(680f, 90f), 30f, reached ? Flat.Text : Flat.Muted, bold: true);
                    if (taken) Text(r, Loc.T("comp.taken"), new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(200f, 80f), 30f, UiKit.Style.successLight, TextAlignmentOptions.Center, true);
                    else if (reached) SmallButton(r, Loc.T("comp.claim"), UiKit.Style.warning, () => StartCoroutine(TakeCellGoal(step)));
                }
            }
            if (clan.members != null)
                for (int i = 0; i < clan.members.Length; i++)
                {
                    var m = clan.members[i];
                    Line(i + 1, m.rank, m.name + (m.leader ? "  ★" : ""), m.trophies.ToString("N0"), m.me);
                }
            Fit();
        }

        private IEnumerator CreateClan()
        {
            string name = (input != null ? input.text : "").Trim();
            if (name.Length < 3) { status.text = Loc.T("comp.clanShort"); yield break; }
            Waiting();
            string why = null;
            yield return FoiNet.CreateClan(name, clanBadge, w => why = w);
            if (why == null) { Open(Tab.Clan); yield break; }
            status.text = Loc.T(why == "taken" ? "comp.clanTaken" : why == "net" ? "live.offline" : "comp.clanBad");
        }

        private IEnumerator JoinClan(string id)
        {
            Waiting();
            string why = null;
            yield return FoiNet.JoinClan(id, w => why = w);
            if (why == null) { Open(Tab.Clan); yield break; }
            status.text = Loc.T(why == "full" ? "comp.clanFull" : "live.offline");
        }

        private IEnumerator LeaveClan()
        {
            bool ok = false;
            yield return FoiNet.LeaveClan(r => ok = r);
            Open(Tab.Clan);
            if (!ok) Offline();
        }
    }

    /// <summary>The clan sizes the server keeps to (shown with the clans).</summary>
    public static class Competitions
    {
        public const int ClanSize = 30;
    }
}
