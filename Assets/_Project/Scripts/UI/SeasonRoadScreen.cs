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
            Flat.Label(p, Loc.T("sr.title"), new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(960f, 80f), 50f, Flat.Text, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            head = Flat.Label(p, "", new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(960f, 60f), 30f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            head.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var bar = UiFactory.Box("Bar", p, new Vector2(0.5f, 1f), new Vector2(0f, -225f), new Vector2(900f, 30f));
            bar.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(bar, new Color(0f, 0f, 0f, 0.45f), UiSprites.Rounded, 15f).raycastTarget = false;
            barFill = UiFactory.Box("Fill", bar, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            barFill.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(barFill, new Color(1f, 0.7f, 0.25f), UiSprites.Rounded, 15f).raycastTarget = false;
            var viewport = UiFactory.Rect("Viewport", p, Vector2.zero, Vector2.one, new Vector2(40f, Monetization.Ads.BannerReserve + 160f), new Vector2(-40f, -270f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;
            var close = Flat.Button(p, Loc.T("btn.close"), new Vector2(0.5f, 0f), new Vector2(0f, Monetization.Ads.BannerReserve + 70f), new Vector2(440f, 104f), Flat.CardHi, Flat.Text, () => screen.Hide(), 34f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        public void Show()
        {
            transform.SetAsLastSibling();
            screen.Show();
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
                var row = Flat.Panel(content, "Step" + s, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(960f, big ? 120f : 100f), taken ? new Color(0.12f, 0.2f, 0.14f) : got ? Flat.CardHi : Flat.Card);
                row.pivot = new Vector2(0.5f, 1f);
                y -= (big ? 120f : 100f) + 10f;
                Flat.Label(row, s.ToString(), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(90f, 80f), big ? 46f : 38f, got ? Flat.Gold : Flat.Muted, bold: true).rectTransform.pivot = new Vector2(0f, 0.5f);
                Flat.Label(row, GiftText(s), new Vector2(0f, 0.5f), new Vector2(130f, 0f), new Vector2(560f, 80f), big ? 34f : 30f, got ? Flat.Text : Flat.Muted, bold: big).rectTransform.pivot = new Vector2(0f, 0.5f);
                if (taken) Flat.Label(row, Loc.T("comp.taken"), new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(200f, 80f), 28f, new Color(0.4f, 1f, 0.5f), TextAlignmentOptions.Center, true);
                else if (got)
                {
                    var b = Flat.Button(row, Loc.T("comp.claim"), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(200f, 76f), new Color(1f, 0.7f, 0.2f), Color.white, () =>
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
