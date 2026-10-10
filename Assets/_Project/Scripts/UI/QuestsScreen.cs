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
    /// Today's quests from the menu: three rows, each with what to do, a progress bar, its coins, and a gold "take"
    /// button once it is done; how long until new ones come at the bottom.
    /// </summary>
    public class QuestsScreen : MonoBehaviour
    {
        public event Action Claimed;

        private UiScreen screen;
        private readonly List<Action> refreshers = new List<Action>();
        private TextMeshProUGUI renew;

        public bool IsOpen => screen.IsVisible;

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
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 1200f));
            card.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(820f, 100f), Loc.T("quests.title"), 64f, Palette.UiGold, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < DailyQuests.Count; i++) Row(card, i);
            renew = UiFactory.TextBox("Renew", card, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(820f, 50f), "", 30f, new Color(1f, 1f, 1f, 0.7f), FontStyles.Normal);
            renew.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var close = UiFactory.MakeButton(card, Loc.T("bag.back"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(480f, 110f), () => screen.Hide(), 46f);
            ((RectTransform)close.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        private void Row(RectTransform card, int i)
        {
            float y = -200f - i * 270f;
            var row = UiFactory.Box("Quest" + i, card, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(820f, 240f));
            row.pivot = new Vector2(0.5f, 1f);
            UiFactory.Fill(row, new Color(1f, 1f, 1f, 0.07f), UiSprites.Rounded, 24f).raycastTarget = false;
            var text = UiFactory.TextBox("Text", row, new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(520f, 100f), "", 36f, Palette.UiText, FontStyles.Normal, align: TextAlignmentOptions.TopLeft);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            var coins = UiFactory.TextBox("Coins", row, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(220f, 60f), "", 38f, Palette.UiGold, title: true, align: TextAlignmentOptions.Right);
            coins.rectTransform.pivot = new Vector2(1f, 1f);
            var back = UiFactory.Box("Bar", row, new Vector2(0f, 0f), new Vector2(30f, 40f), new Vector2(500f, 34f));
            back.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(back, new Color(0f, 0f, 0f, 0.4f), UiSprites.Rounded, 17f).raycastTarget = false;
            var fill = UiFactory.Box("Fill", back, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(500f, 34f));
            fill.pivot = new Vector2(0f, 0.5f);
            var fillImage = UiFactory.Fill(fill, new Color(0.36f, 0.85f, 0.6f), UiSprites.Rounded, 17f);
            fillImage.raycastTarget = false;
            var count = UiFactory.TextBox("Count", back, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 34f), "", 26f, Color.white, title: true);
            count.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var claim = UiFactory.MakeButton(row, Loc.T("chest.take"), Kind.Gold, new Vector2(1f, 0f), new Vector2(-130f, 56f), new Vector2(220f, 90f), () =>
            {
                if (DailyQuests.Claim(i))
                {
                    AudioManager.PlaySfx(Sfx.Coin, 1f, 1.3f);
                    Haptics.Medium();
                    Claimed?.Invoke();
                }
                Refresh();
            }, 34f);
            ((RectTransform)claim.transform).pivot = new Vector2(0.5f, 0.5f);
            refreshers.Add(() =>
            {
                var q = DailyQuests.Get(i);
                text.text = Loc.F("quest." + q.goal, q.target);
                coins.text = "+" + q.reward;
                float k = Mathf.Clamp01(q.progress / (float)Mathf.Max(1, q.target));
                fill.sizeDelta = new Vector2(Mathf.Max(34f, 500f * k), 34f);
                fill.gameObject.SetActive(k > 0f);
                count.text = q.claimed ? Loc.T("quests.done") : q.progress + " / " + q.target;
                claim.gameObject.SetActive(q.Done && !q.claimed);
            });
        }

        public void Show()
        {
            Refresh();
            screen.Show();
            transform.SetAsLastSibling();
        }

        private void Refresh()
        {
            foreach (var r in refreshers) r();
            int s = GameClock.SecondsToMidnight;
            renew.text = Loc.F("quests.renew", s / 3600, (s / 60) % 60);
        }
    }
}
