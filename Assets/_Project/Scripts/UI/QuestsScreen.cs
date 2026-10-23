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
    /// The one QUESTS screen, the duel's and the levels' together: today's three (one duel quest, one level quest and a
    /// third that is one or the other, by the day) and this week's three duel quests; each row with what to do, its
    /// progress, its coins and a gold "take" button once it is done; how long until new ones come at the bottom.
    /// </summary>
    public class QuestsScreen : MonoBehaviour
    {
        public event Action Claimed;

        private UiScreen screen;

        /// <summary>Closed at once (another screen is taking over).</summary>
        public void Hide() => screen.Hide(true);
        private RectTransform list;
        private TextMeshProUGUI renew;

        public bool IsOpen => screen.IsVisible;

        /// <summary>One row of the screen: a duel quest (daily or weekly) or a level quest.</summary>
        private struct Entry
        {
            public bool duel, weekly;
            public int index;
        }

        /// <summary>Today's third quest is the duel's on even days, the levels' on odd ones.</summary>
        private static bool ThirdIsDuel => (DateTime.Now.DayOfYear & 1) == 0;

        private static IEnumerable<Entry> Entries(bool weekly)
        {
            if (weekly)
            {
                for (int i = 0; i < ChallengeQuests.Count; i++) yield return new Entry { duel = true, weekly = true, index = i };
                yield break;
            }
            yield return new Entry { duel = true, index = 0 };
            yield return new Entry { duel = false, index = 0 };
            yield return ThirdIsDuel ? new Entry { duel = true, index = 1 } : new Entry { duel = false, index = 1 };
        }

        private static (string text, int coins, int progress, int target, bool done, bool claimed) Read(Entry e)
        {
            if (e.duel)
            {
                var q = ChallengeQuests.Get(e.weekly, e.index);
                return (ChallengeQuests.Text(q), q.coins, q.progress, q.target, q.Done, q.claimed);
            }
            var d = DailyQuests.Get(e.index);
            return (Loc.F("quest." + d.goal, d.target), d.reward, d.progress, d.target, d.Done, d.claimed);
        }

        private static bool Take(Entry e) => e.duel ? ChallengeQuests.Claim(e.weekly, e.index) > 0 : DailyQuests.Claim(e.index);

        /// <summary>Quests on the screen done and waiting to be taken (the menu button's red dot).</summary>
        public static int Waiting
        {
            get
            {
                int n = 0;
                foreach (bool weekly in new[] { false, true })
                    foreach (var e in Entries(weekly)) { var r = Read(e); if (r.done && !r.claimed) n++; }
                return n;
            }
        }

        public static QuestsScreen Create(Transform canvasRoot)
        {
            var s = UiScreen.Create("Quests", canvasRoot, out var root);
            var q = root.gameObject.AddComponent<QuestsScreen>();
            q.screen = s;
            q.Build(root);
            return q;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.02f, 0.02f, 0.06f, 0.72f)).gameObject.AddComponent<IgnoreSafeArea>();
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1560f));
            card.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(820f, 100f), Loc.T("comp.quests"), 64f, Palette.UiGold, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            list = UiFactory.Box("List", card, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(860f, 1200f));
            list.pivot = new Vector2(0.5f, 1f);
            renew = UiFactory.TextBox("Renew", card, new Vector2(0.5f, 0f), new Vector2(0f, 175f), new Vector2(820f, 50f), "", 30f, new Color(1f, 1f, 1f, 0.7f), FontStyles.Normal);
            renew.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var close = UiFactory.MakeButton(card, Loc.T("btn.close"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(480f, 110f), () => screen.Hide(), 46f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        private float y;

        private void Heading(string text)
        {
            var t = UiFactory.TextBox("Head", list, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(860f, 60f), text, 34f, Palette.UiGold, title: true, align: TextAlignmentOptions.Left);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            y -= 66f;
        }

        private void Row(Entry e)
        {
            var q = Read(e);
            var row = UiFactory.Box("Quest", list, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(860f, 160f));
            row.pivot = new Vector2(0.5f, 1f);
            y -= 174f;
            UiFactory.Framed(row, q.claimed ? new Color(0.12f, 0.2f, 0.14f) : Flat.Card, 1.8f).raycastTarget = false;
            var text = UiFactory.TextBox("Text", row, new Vector2(0f, 1f), new Vector2(26f, -16f), new Vector2(560f, 84f), q.text, 32f, Palette.UiText, FontStyles.Normal, align: TextAlignmentOptions.TopLeft);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 22f;
            text.fontSizeMax = 32f;
            var coins = UiFactory.TextBox("Coins", row, new Vector2(1f, 1f), new Vector2(-26f, -18f), new Vector2(220f, 56f), "+" + q.coins, 36f, Palette.UiGold, title: true, align: TextAlignmentOptions.Right);
            coins.rectTransform.pivot = new Vector2(1f, 1f);
            var back = UiFactory.Box("Bar", row, new Vector2(0f, 0f), new Vector2(26f, 32f), new Vector2(540f, 32f));
            back.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(back, new Color(0f, 0f, 0f, 0.4f), UiSprites.Rounded, 16f).raycastTarget = false;
            float k = Mathf.Clamp01(q.progress / (float)Mathf.Max(1, q.target));
            if (k > 0f)
            {
                var fill = UiFactory.Box("Fill", back, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(Mathf.Max(32f, 540f * k), 32f));
                fill.pivot = new Vector2(0f, 0.5f);
                UiFactory.Fill(fill, new Color(0.36f, 0.85f, 0.6f), UiSprites.Rounded, 16f).raycastTarget = false;
            }
            UiFactory.TextBox("Count", back, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 32f), q.claimed ? Loc.T("quests.done") : q.progress + " / " + q.target, 24f, Color.white, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            if (q.done && !q.claimed)
            {
                var claim = UiFactory.MakeButton(row, Loc.T("chest.take"), Kind.Gold, new Vector2(1f, 0f), new Vector2(-126f, 50f), new Vector2(210f, 84f), () =>
                {
                    if (Take(e))
                    {
                        AudioManager.PlaySfx(Sfx.Coin, 1f, 1.3f);
                        Haptics.Medium();
                        Claimed?.Invoke();
                    }
                    Refresh();
                }, 32f);
                ((RectTransform)claim.transform).pivot = new Vector2(0.5f, 0.5f);
            }
        }

        public void Show()
        {
            Refresh();
            screen.Show();
            transform.SetAsLastSibling();
        }

        private void Refresh()
        {
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            y = 0f;
            Heading(Loc.T("comp.daily"));
            foreach (var e in Entries(false)) Row(e);
            y -= 12f;
            Heading(Loc.T("comp.weekly"));
            foreach (var e in Entries(true)) Row(e);
            int s = GameClock.SecondsToMidnight;
            renew.text = Loc.F("quests.renew", s / 3600, (s / 60) % 60);
        }
    }
}
