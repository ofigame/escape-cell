using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// foi's army, in the flat style: the free development points at the top, then the six soldier groups one row each —
    /// the soldier's picture, its name, the family it beats, its points and strength and its tier stars — with +1 / +5
    /// to put points on and −1 / −5 to take them back; the rock-paper-scissors line and CLOSE at the bottom.
    /// </summary>
    public class ArmyScreen : MonoBehaviour
    {
        private UiScreen screen;
        private TextMeshProUGUI freeValue, freeOf, worldText;
        private readonly List<Action> refreshers = new List<Action>();
        private int world;

        public bool IsOpen => screen.IsVisible;

        public static ArmyScreen Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("Army", canvasRoot, out var root);
            var a = root.gameObject.AddComponent<ArmyScreen>();
            a.screen = s;
            a.Build(root);
            return a;
        }

        /// <summary>The colour of a family: swords red, spears teal, bows gold.</summary>
        public static Color FamilyColour(GroupKind k) =>
            Army.Family(k) == 0 ? new Color(0.95f, 0.42f, 0.4f) : Army.Family(k) == 1 ? new Color(0.25f, 0.78f, 0.7f) : new Color(1f, 0.76f, 0.3f);

        private const float RowH = 330f, RowGap = 16f, Top = -8f;

        private void Build(RectTransform root)
        {
            Flat.Background(root);
            var top = new Vector2(0.5f, 1f);
            Flat.Label(root, Loc.T("army.title"), top, new Vector2(0f, -70f), new Vector2(960f, 80f), 58f, Flat.Text, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // The free points: a pill with the number big.
            var pill = Flat.Panel(root, "Free", top, new Vector2(0f, -150f), new Vector2(960f, 96f), Flat.Card);
            pill.pivot = new Vector2(0.5f, 1f);
            Flat.Label(pill, Loc.T("army.freeLabel"), new Vector2(0f, 0.5f), new Vector2(Flat.Pad, 0f), new Vector2(480f, 60f), 32f, Flat.Muted)
                .rectTransform.pivot = new Vector2(0f, 0.5f);
            freeOf = Flat.Label(pill, "", new Vector2(1f, 0.5f), new Vector2(-Flat.Pad, -2f), new Vector2(240f, 50f), 28f, Flat.Muted, TextAlignmentOptions.Right);
            freeOf.rectTransform.pivot = new Vector2(1f, 0.5f);
            freeValue = Flat.Label(pill, "", new Vector2(1f, 0.5f), new Vector2(-Flat.Pad - 250f, 0f), new Vector2(160f, 70f), 52f, Flat.Gold, TextAlignmentOptions.Right, bold: true);
            freeValue.rectTransform.pivot = new Vector2(1f, 0.5f);

            worldText = Flat.Label(root, "", top, new Vector2(0f, -262f), new Vector2(960f, 56f), 26f, Flat.Muted, TextAlignmentOptions.Center, wrap: true);
            worldText.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // The six groups in a list that scrolls (each card tall, with big buttons).
            var viewport = UiFactory.Rect("Viewport", root, Vector2.zero, Vector2.one, new Vector2(0f, Monetization.Ads.BannerReserve + 220f), new Vector2(0f, -300f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0f));
            var content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, Army.Groups * (RowH + RowGap) + 16f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;
            for (int g = 0; g < Army.Groups; g++) GroupRow(content, g);

            var bottom = new Vector2(0.5f, 0f);
            Flat.Label(root, Loc.T("army.wheel"), bottom, new Vector2(0f, Monetization.Ads.BannerReserve + 180f), new Vector2(960f, 50f), 26f, Flat.Muted, TextAlignmentOptions.Center)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var close = Flat.Button(root, Loc.T("btn.close"), bottom, new Vector2(0f, Monetization.Ads.BannerReserve + 70f), new Vector2(520f, 110f), Flat.Blue, Color.white, () => screen.Hide(), 42f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        private void GroupRow(RectTransform root, int g)
        {
            var kind = Army.KindOf(g);
            var colour = FamilyColour(kind);
            var row = Flat.Panel(root, "Group" + g, new Vector2(0.5f, 1f), new Vector2(0f, Top - g * (RowH + RowGap)), new Vector2(960f, RowH), Flat.Card);
            row.pivot = new Vector2(0.5f, 1f);
            // The family's colour along the left edge.
            var stripe = UiFactory.Box("Stripe", row, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(10f, RowH - 36f));
            stripe.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(stripe, colour, UiSprites.Rounded, 6f).raycastTarget = false;

            // The soldier's picture on a tinted tile, with the soldier count.
            var tile = Flat.Panel(row, "Tile", new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(150f, 150f), new Color(colour.r, colour.g, colour.b, 0.18f), 2f);
            tile.pivot = new Vector2(0f, 1f);
            ChallengeScreen.UnitPicture(tile, kind, new Vector2(0f, 6f), 130f);
            var count = Flat.Panel(tile, "Count", new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(72f, 36f), new Color(0f, 0f, 0f, 0.55f), 4f);
            count.pivot = new Vector2(0.5f, 0f);
            Flat.Label(count, Army.Soldiers.ToString(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 34f), 24f, Color.white, TextAlignmentOptions.Center, bold: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // The words: name, what it beats, points and strength, tier stars.
            const float textX = 30f + 150f + Flat.Pad;
            const float textW = 960f - textX - Flat.Pad;
            var name = Flat.Label(row, Loc.T("army.kind." + kind), new Vector2(0f, 1f), new Vector2(textX, -22f), new Vector2(textW, 48f), 36f, Flat.Text, bold: true);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            var beats = Flat.Label(row, "", new Vector2(0f, 1f), new Vector2(textX, -74f), new Vector2(textW, 34f), 20f, Flat.Muted);
            beats.rectTransform.pivot = new Vector2(0f, 1f);
            var stats = Flat.Label(row, "", new Vector2(0f, 1f), new Vector2(textX, -112f), new Vector2(textW, 38f), 22f, Flat.Gold, bold: true);
            stats.rectTransform.pivot = new Vector2(0f, 1f);
            var stars = new List<Image>();
            for (int s = 0; s < 4; s++)
            {
                var st = UiFactory.Box("Star", row, new Vector2(0f, 1f), new Vector2(textX + s * 38f, -150f), new Vector2(32f, 32f));
                var img = UiFactory.Fill(st, Color.white, UiSprites.Star);
                img.raycastTarget = false;
                stars.Add(img);
            }

            // Three rows on the right — power, blow, armour — each with its value and − +1 +5.
            var pad = new Vector2(0f, 0f);
            Button B(string t, float x, float y, Color face, Color text, Action a)
            {
                var b = Flat.Button(row, t, pad, new Vector2(x, y), new Vector2(92f, 74f), face, text, a, 34f);
                ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
                return b;
            }
            var dark = new Color(0.1f, 0.08f, 0.15f);
            var tracks = new[] { Army.Track.Power, Army.Track.Hit, Army.Track.Armor };
            var plus1 = new Button[3];
            var plus5 = new Button[3];
            var minus = new Button[3];
            var values = new TextMeshProUGUI[3];
            for (int r = 0; r < 3; r++)
            {
                var track = tracks[r];
                float x0 = 30f + r * 304f; // each column 300 wide
                minus[r] = B("-", x0 + 50f, 52f, Flat.CardHi, Flat.Text, () => Take(g, 1, track));
                plus1[r] = B("+1", x0 + 150f, 52f, colour, dark, () => Add(g, 1, track));
                plus5[r] = B("+5", x0 + 250f, 52f, colour, dark, () => Add(g, 5, track));
                values[r] = Flat.Label(row, "", pad, new Vector2(x0 + 150f, 122f), new Vector2(290f, 42f), 26f, Flat.Text, TextAlignmentOptions.Center, bold: true);
                values[r].rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            refreshers.Add(() =>
            {
                var st = Army.StatsOf(g);
                values[0].text = Loc.F("army.power", st.hp);
                values[1].text = Loc.F("army.hit", st.hit.ToString("0.#"));
                values[2].text = Loc.F("army.armorPct", Mathf.RoundToInt(st.armor * 100f));
                stats.text = Loc.F("army.points", Army.GroupPoints(g));
                var beatKind = (GroupKind)(((Army.Family(kind) + 2) % 3) * 2); // the family this one beats
                beats.text = Loc.F("army.beats", Loc.T("army.family." + Army.Family(beatKind))) + "  ·  " + Loc.F("army.pool", SoldierPool.Available(g), Army.Soldiers);
                int tier = Army.Tier(g);
                for (int s = 0; s < stars.Count; s++) stars[s].color = s < tier ? Flat.Gold : new Color(1f, 1f, 1f, 0.12f);
                bool any = Army.FreePoints > 0;
                for (int r = 0; r < 3; r++)
                {
                    int have = Army.Points(g, tracks[r]), max = Army.MaxPoints(g, tracks[r]);
                    plus1[r].interactable = plus5[r].interactable = any && have < max;
                    minus[r].interactable = have > 0;
                    values[r].color = have >= max ? Flat.Gold : Flat.Text;
                }
            });
        }

        private void Take(int g, int n, Army.Track t)
        {
            if (Army.Take(g, n, t)) AudioManager.PlaySfx(Sfx.Click, 0.9f, 0.9f);
            else AudioManager.PlaySfx(Sfx.Bump, 0.6f);
            Refresh();
        }

        private void Add(int g, int n, Army.Track t)
        {
            if (Army.Spend(g, n, t)) AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.3f);
            else AudioManager.PlaySfx(Sfx.Bump, 0.6f);
            Refresh();
        }

        /// <param name="currentWorld">The world being played (its score so far is shown).</param>
        public void Show(int currentWorld)
        {
            world = currentWorld;
            Refresh();
            screen.Show();
            transform.SetAsLastSibling();
        }

        public void Hide() => screen.Hide(true);

        private void Refresh()
        {
            freeValue.text = Army.FreePoints.ToString();
            freeOf.text = Loc.F("army.earnedOf", Army.EarnedPoints);
            int score = Army.WorldScore(world);
            worldText.text = Loc.F("army.world", world + 1, score, Mathf.Min(100, score / 10))
                             + "   ·   " + Loc.F("army.rankBonus", Mathf.RoundToInt(Challenge.ArmyBonus * 100f));
            foreach (var r in refreshers) r();
        }
    }
}
