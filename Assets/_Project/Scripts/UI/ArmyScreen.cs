using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// foi's army: the six soldier groups (their kind, development points, strength and which kind they beat), the free
    /// development points to spread (+1 / +5 on a group), how the current world is scoring towards its points, and a
    /// gold-priced reset that takes every spent point back.
    /// </summary>
    public class ArmyScreen : MonoBehaviour
    {
        private UiScreen screen;
        private TextMeshProUGUI freeText, worldText, resetLabel;
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

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.02f, 0.02f, 0.06f, 0.8f)).gameObject.AddComponent<IgnoreSafeArea>();
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 1640f));
            card.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 100f), Loc.T("army.title"), 68f, Palette.UiGold, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            freeText = UiFactory.TextBox("Free", card, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 60f), "", 40f, Palette.UiCyan, title: true);
            freeText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            worldText = UiFactory.TextBox("World", card, new Vector2(0.5f, 1f), new Vector2(0f, -212f), new Vector2(920f, 70f), "", 28f, new Color(1f, 1f, 1f, 0.8f), FontStyles.Normal);
            worldText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            worldText.textWrappingMode = TextWrappingModes.Normal;
            for (int g = 0; g < Army.Groups; g++) GroupCard(card, g);
            UiFactory.TextBox("Wheel", card, new Vector2(0.5f, 0f), new Vector2(0f, 290f), new Vector2(920f, 50f), Loc.T("army.wheel"), 28f, new Color(1f, 1f, 1f, 0.75f), FontStyles.Normal)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var reset = UiFactory.MakeButton(card, "", Kind.Secondary, new Vector2(0.5f, 0f), new Vector2(-230f, 170f), new Vector2(420f, 100f), () =>
            {
                if (Army.TryReset()) { AudioManager.PlaySfx(Sfx.Coin, 0.9f, 1f); Refresh(); }
                else AudioManager.PlaySfx(Sfx.Bump, 0.6f);
            }, 30f);
            ((RectTransform)reset.transform).pivot = new Vector2(0.5f, 0.5f);
            resetLabel = reset.GetComponentInChildren<TextMeshProUGUI>();
            var close = UiFactory.MakeButton(card, Loc.T("bag.back"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(230f, 170f), new Vector2(420f, 110f), () => screen.Hide(), 46f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        private void GroupCard(RectTransform card, int g)
        {
            var kind = Army.KindOf(g);
            var colour = FamilyColour(kind);
            int col = g % 2, row = g / 2;
            var box = UiFactory.Box("Group" + g, card, new Vector2(0.5f, 1f), new Vector2((col == 0 ? -1f : 1f) * 235f, -280f - row * 330f), new Vector2(450f, 310f));
            box.pivot = new Vector2(0.5f, 1f);
            UiFactory.Fill(box, new Color(colour.r, colour.g, colour.b, 0.16f), UiSprites.Rounded, 24f).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", box), new Color(colour.r, colour.g, colour.b, 0.6f), UiSprites.Ring, 24f).raycastTarget = false;
            // A badge: the family's colour with the group's soldier count.
            var badge = UiFactory.Box("Badge", box, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(96f, 96f));
            badge.pivot = new Vector2(0f, 1f);
            UiFactory.Fill(badge, colour, UiSprites.Circle).raycastTarget = false;
            UiFactory.Text(badge, Army.Soldiers.ToString(), 40f, new Color(0.1f, 0.06f, 0.2f));
            var name = UiFactory.TextBox("Name", box, new Vector2(0f, 1f), new Vector2(136f, -26f), new Vector2(300f, 50f), Loc.T("army.kind." + kind), 34f, Palette.UiText, title: true, align: TextAlignmentOptions.Left);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            var beats = UiFactory.TextBox("Beats", box, new Vector2(0f, 1f), new Vector2(136f, -78f), new Vector2(300f, 40f), "", 24f, new Color(1f, 1f, 1f, 0.75f), FontStyles.Normal, align: TextAlignmentOptions.Left);
            beats.rectTransform.pivot = new Vector2(0f, 1f);
            var stats = UiFactory.TextBox("Stats", box, new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(410f, 50f), "", 30f, Palette.UiGold, title: true);
            stats.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var tierRow = UiFactory.Box("Tier", box, new Vector2(1f, 1f), new Vector2(-24f, -30f), new Vector2(120f, 30f));
            tierRow.pivot = new Vector2(1f, 1f);
            var stars = new List<Image>();
            for (int s = 0; s < 4; s++)
            {
                var st = UiFactory.Box("Star", tierRow, new Vector2(1f, 0.5f), new Vector2(-s * 30f, 0f), new Vector2(28f, 28f));
                st.pivot = new Vector2(1f, 0.5f);
                var img = UiFactory.Fill(st, Color.white, UiSprites.Star);
                img.raycastTarget = false;
                stars.Add(img);
            }
            var plus1 = UiFactory.MakeButton(box, "+1", Kind.Gold, new Vector2(0.5f, 0f), new Vector2(-105f, 24f), new Vector2(190f, 84f), () => Add(g, 1), 38f);
            var plus5 = UiFactory.MakeButton(box, "+5", Kind.Gold, new Vector2(0.5f, 0f), new Vector2(105f, 24f), new Vector2(190f, 84f), () => Add(g, 5), 38f);
            ((RectTransform)plus1.transform).pivot = new Vector2(0.5f, 0f);
            ((RectTransform)plus5.transform).pivot = new Vector2(0.5f, 0f);
            refreshers.Add(() =>
            {
                int pts = Army.GroupPoints(g);
                stats.text = Loc.F("army.stats", pts, Mathf.RoundToInt((Army.Power(g) - 1f) * 100f));
                var beatKind = (GroupKind)(((Army.Family(kind) + 2) % 3) * 2); // the family this one beats
                beats.text = Loc.F("army.beats", Loc.T("army.family." + Army.Family(beatKind)));
                int tier = Army.Tier(g);
                for (int s = 0; s < stars.Count; s++) stars[s].color = s < tier ? Palette.UiGold : new Color(1f, 1f, 1f, 0.15f);
                bool any = Army.FreePoints > 0;
                plus1.interactable = any;
                plus5.interactable = any;
            });
        }

        private void Add(int g, int n)
        {
            if (Army.Spend(g, n)) AudioManager.PlaySfx(Sfx.Click, 0.9f, 1.3f);
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
            freeText.text = Loc.F("army.free", Army.FreePoints, Army.EarnedPoints);
            int score = Army.WorldScore(world);
            worldText.text = Loc.F("army.world", world + 1, score, Mathf.Min(100, score / 10));
            resetLabel.text = Loc.F("army.reset", Army.ResetPrice);
            foreach (var r in refreshers) r();
        }
    }
}
