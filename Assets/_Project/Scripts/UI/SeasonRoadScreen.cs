using System;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>The season road: the month's points and its 30 steps, each with its gift and a take button once reached.</summary>
    public class SeasonRoadScreen : MonoBehaviour
    {
        public event Action Claimed;

        private UiScreen screen;
        private TMPro.TextMeshProUGUI headerCoins;
        private RectTransform content;
        private TextMeshProUGUI head;
        private RectTransform barFill;

        public bool IsOpen => screen.IsVisible;
        public void Hide() => screen.Hide(true);

        public static SeasonRoadScreen Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("SeasonRoad", canvasRoot, out var root);
            var r = root.gameObject.AddComponent<SeasonRoadScreen>();
            r.screen = s;
            r.Build(root);
            return r;
        }

        private void Build(RectTransform p)
        {
            Flat.Background(p);
            headerCoins = UiKit.ScreenHeader(p, screen, Loc.T("sr.title"), null);
            head = Flat.Label(p, "", new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(960f, 60f), 30f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            head.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var bar = UiFactory.Box("Bar", p, new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(900f, 30f));
            bar.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(bar, UiKit.A(UiKit.Style.black, 0.45f), UiSprites.Rounded, 15f).raycastTarget = false;
            barFill = UiFactory.Box("Fill", bar, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            barFill.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(barFill, UiKit.Style.warning, UiSprites.Rounded, 15f).raycastTarget = false;
            var viewport = UiFactory.Rect("Viewport", p, Vector2.zero, Vector2.one, new Vector2(40f, Monetization.Ads.BannerReserve + 30f), new Vector2(-40f, -305f));
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

        public void Show()
        {
            transform.SetAsLastSibling();
            screen.Show();
            UiKit.RefreshCurrency(headerCoins);
            Refresh();
        }

        private static string GiftText(int step)
        {
            var g = SeasonRoad.Gift(step);
            switch (g.kind)
            {
                case SeasonRoad.Kind.Gold: return Loc.F("login.gold", g.amount);
                case SeasonRoad.Kind.Basic: return DuelChestScreen.TierName(ChestTier.Basic);
                case SeasonRoad.Kind.Bronze: return DuelChestScreen.TierName(ChestTier.Mid);
                case SeasonRoad.Kind.Cores: return Loc.F("login.cores", g.amount);
                default: return Loc.T("sr." + g.kind);
            }
        }

        private void Refresh()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            int points = SeasonRoad.Points, reached = SeasonRoad.Reached;
            head.text = Loc.F("sr.head", reached, SeasonRoad.Steps, points % SeasonRoad.PerStep, SeasonRoad.PerStep, SeasonRoad.DaysLeft);
            barFill.sizeDelta = new Vector2(Mathf.Max(30f, 900f * points / (float)(SeasonRoad.Steps * SeasonRoad.PerStep)), 30f);
            float y = -10f;
            for (int s = 1; s <= SeasonRoad.Steps; s++)
            {
                int step = s;
                bool got = reached >= s, taken = SeasonRoad.Taken(s);
                bool big = s % 5 == 0;
                var row = Flat.Panel(content, "Step" + s, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(960f, big ? 120f : 100f), taken ? UiKit.Style.panelSuccess : got ? Flat.CardHi : Flat.Card);
                row.pivot = new Vector2(0.5f, 1f);
                y -= (big ? 120f : 100f) + 10f;
                Flat.Label(row, s.ToString(), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(90f, 80f), big ? 46f : 38f, got ? Flat.Gold : Flat.Muted, bold: true).rectTransform.pivot = new Vector2(0f, 0.5f);
                Flat.Label(row, GiftText(s), new Vector2(0f, 0.5f), new Vector2(130f, 0f), new Vector2(560f, 80f), big ? 34f : 30f, got ? Flat.Text : Flat.Muted, bold: big).rectTransform.pivot = new Vector2(0f, 0.5f);
                // the pass's gift on the step (taken with the month's pass)
                if (got && Monetization.Store.HasPass && !SeasonRoad.PassTaken(s))
                {
                    var pb = Flat.Button(row, Loc.T("sr.pass"), new Vector2(1f, 0.5f), new Vector2(-250f, 0f), new Vector2(150f, 76f), UiKit.Style.purple, Color.white, () =>
                    {
                        if (SeasonRoad.TakePass(step)) { AudioManager.PlaySfx(Sfx.Coin, 1f, 1.3f); Claimed?.Invoke(); }
                        Refresh();
                    }, 24f);
                    ((RectTransform)pb.transform).pivot = new Vector2(1f, 0.5f);
                }
                if (taken) Flat.Label(row, Loc.T("comp.taken"), new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(200f, 80f), 28f, UiKit.Style.successLight, TextAlignmentOptions.Center, true);
                else if (got)
                {
                    var b = Flat.Button(row, Loc.T("comp.claim"), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(200f, 76f), UiKit.Style.warning, Color.white, () =>
                    {
                        if (SeasonRoad.Take(step)) { AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f); Haptics.Medium(); Claimed?.Invoke(); }
                        Refresh();
                    }, 28f);
                    ((RectTransform)b.transform).pivot = new Vector2(1f, 0.5f);
                }
            }
            content.sizeDelta = new Vector2(0f, -y + 20f);
        }
    }
}
