using System;
using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Gameplay;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The training room's controls (the design's §10): the dummy's modes (standing, attacking, defending, casting its
    /// ultimate), INFO (damage numbers and the perfect guard's cue), CHALLENGES (the list: each one's name, steps,
    /// reward and a tick when done), the challenge under way with its steps ticking green one by one, and EXIT.
    /// </summary>
    public class TrainingPanel : MonoBehaviour
    {
        public event Action<TrainingDummy> DummyPicked;
        public event Action<string> ChallengePicked;
        public event Action InfoToggled, ExitPressed;

        private RectTransform root, card, listScreen, listContent;
        private TextMeshProUGUI cardTitle, cardReward;
        private readonly List<TextMeshProUGUI> stepTexts = new List<TextMeshProUGUI>();
        private readonly Dictionary<TrainingDummy, Image> chips = new Dictionary<TrainingDummy, Image>();
        private Image infoChip;

        private static readonly Color Chip = UiKit.A(UiKit.Style.panel, 0.92f), ChipOn = UiKit.A(UiKit.Style.orange, 0.95f);

        public static TrainingPanel Create(RectTransform parent)
        {
            var r = UiFactory.Stretch("Training", parent);
            var p = r.gameObject.AddComponent<TrainingPanel>();
            p.root = r;
            p.Build();
            return p;
        }

        private void Build()
        {
            // the row of chips under the health bars
            var row = UiFactory.Box("Row", root, new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(1040f, 84f));
            row.pivot = new Vector2(0.5f, 1f);
            var modes = new[] { TrainingDummy.Standing, TrainingDummy.Attacking, TrainingDummy.Defending, TrainingDummy.Ulti };
            for (int i = 0; i < modes.Length; i++)
            {
                var m = modes[i];
                var b = Flat.Button(row, Loc.T("tr.dummy." + m), new Vector2(0f, 0.5f), new Vector2(80f + i * 142f, 0f), new Vector2(136f, 76f), Chip, Flat.Text, () => DummyPicked?.Invoke(m), 22f);
                ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
                chips[m] = (Image)b.targetGraphic;
            }
            var info = Flat.Button(row, Loc.T("tr.info"), new Vector2(0f, 0.5f), new Vector2(80f + 4 * 142f, 0f), new Vector2(136f, 76f), Chip, Flat.Text, () => InfoToggled?.Invoke(), 22f);
            ((RectTransform)info.transform).pivot = new Vector2(0.5f, 0.5f);
            infoChip = (Image)info.targetGraphic;
            var list = Flat.Button(row, Loc.T("tr.challenges"), new Vector2(0f, 0.5f), new Vector2(80f + 5 * 142f + 30f, 0f), new Vector2(170f, 76f), UiKit.Style.purple, Flat.Text, () => ShowList(true), 22f);
            ((RectTransform)list.transform).pivot = new Vector2(0.5f, 0.5f);
            var exit = Flat.Button(row, Loc.T("tr.exit"), new Vector2(1f, 0.5f), new Vector2(-65f, 0f), new Vector2(120f, 76f), UiKit.Style.steel, Flat.Text, () => ExitPressed?.Invoke(), 22f);
            ((RectTransform)exit.transform).pivot = new Vector2(0.5f, 0.5f);
            // the challenge under way: its name, its steps, its reward
            card = Flat.Panel(root, "Challenge", new Vector2(0f, 0.5f), new Vector2(24f, 260f), new Vector2(540f, 320f), UiKit.A(UiKit.Style.back, 0.88f));
            card.pivot = new Vector2(0f, 0.5f);
            cardTitle = Flat.Label(card, "", new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(500f, 56f), 34f, Flat.Gold, TextAlignmentOptions.Center, bold: true);
            cardTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cardReward = Flat.Label(card, "", new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(500f, 44f), 24f, Flat.Muted, TextAlignmentOptions.Center);
            cardReward.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            card.gameObject.SetActive(false);
            BuildList();
        }

        private void BuildList()
        {
            listScreen = UiFactory.Stretch("List", root);
            var dim = UiFactory.Fill(listScreen, UiKit.A(UiKit.Style.backDeep, 0.85f));
            dim.gameObject.AddComponent<IgnoreSafeArea>();
            var title = Flat.Label(listScreen, Loc.T("tr.challenges"), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(900f, 80f), 54f, Flat.Text, TextAlignmentOptions.Center, bold: true);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            listContent = UiFactory.Box("Content", listScreen, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(980f, 1500f));
            listContent.pivot = new Vector2(0.5f, 1f);
            var close = Flat.Button(listScreen, Loc.T("btn.close"), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(440f, 110f), Flat.CardHi, Flat.Text, () => ShowList(false), 36f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
            listScreen.gameObject.SetActive(false);
        }

        /// <summary>The list of challenges: five rows to a group (sword, spear, axe, heavy, the defence), done ones ticked.</summary>
        public void ShowList(bool on)
        {
            listScreen.gameObject.SetActive(on);
            if (!on) return;
            listScreen.SetAsLastSibling();
            for (int i = listContent.childCount - 1; i >= 0; i--) Destroy(listContent.GetChild(i).gameObject);
            var all = ComboChallenges.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                int col = i % 5, grp = i / 5;
                bool done = ComboChallenges.Done(c);
                string id = c.id;
                var b = Flat.Button(listContent, ComboChallenges.Name(c), new Vector2(0f, 1f), new Vector2(98f + col * 196f, -50f - grp * 250f), new Vector2(186f, 90f),
                    done ? UiKit.Style.green : Flat.CardHi, Flat.Text, () => { ShowList(false); ChallengePicked?.Invoke(id); }, 22f);
                ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
                var steps = new List<string>();
                foreach (var s in c.steps) steps.Add(ComboChallenges.StepText(s));
                var t = Flat.Label(listContent, string.Join(" + ", steps), new Vector2(0f, 1f), new Vector2(98f + col * 196f, -150f - grp * 250f), new Vector2(186f, 100f), 17f, Flat.Muted, TextAlignmentOptions.Top, wrap: true);
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        /// <summary>The dummy's mode chip lit, and INFO's.</summary>
        public void SetModes(TrainingDummy dummy, bool info)
        {
            foreach (var kv in chips) kv.Value.color = kv.Key == dummy ? ChipOn : Chip;
            infoChip.color = info ? ChipOn : Chip;
        }

        /// <summary>The challenge under way (null: none): its steps, the done ones green, the current one's count.</summary>
        public void SetChallenge(ComboChallengeData c, int step, int progress)
        {
            card.gameObject.SetActive(c != null);
            if (c == null) return;
            cardTitle.text = ComboChallenges.Name(c);
            var reward = new List<string>();
            if (c.gold > 0) reward.Add("+" + c.gold + " " + Loc.T("ch.gold"));
            if (c.cores > 0) reward.Add(Loc.F("login.cores", "+" + c.cores));
            if (!string.IsNullOrEmpty(c.title)) reward.Add(Loc.T(c.title));
            cardReward.text = string.Join(" · ", reward);
            while (stepTexts.Count < c.steps.Count)
            {
                var t = Flat.Label(card, "", new Vector2(0f, 1f), new Vector2(30f, -100f - stepTexts.Count * 54f), new Vector2(490f, 50f), 26f, Flat.Text, TextAlignmentOptions.Left);
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
                stepTexts.Add(t);
            }
            for (int i = 0; i < stepTexts.Count; i++)
            {
                stepTexts[i].gameObject.SetActive(i < c.steps.Count);
                if (i >= c.steps.Count) continue;
                bool done = i < step;
                stepTexts[i].text = (done ? "+ " : i == step ? "> " : "- ") + ComboChallenges.StepText(c.steps[i]) + (i == step && c.steps[i].count > 1 ? "  " + progress + "/" + c.steps[i].count : "");
                stepTexts[i].color = done ? UiKit.Style.successLight : i == step ? Flat.Text : Flat.Muted;
            }
        }

        public bool IsOver(Vector2 screen) =>
            listScreen.gameObject.activeSelf
            || RectTransformUtility.RectangleContainsScreenPoint((RectTransform)root.Find("Row"), screen, null)
            || card.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(card, screen, null);
    }
}
