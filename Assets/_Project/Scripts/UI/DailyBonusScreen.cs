using System;
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
    /// The daily bonus card: today's three games (ticked off as they are played), the PLAY button with the plays left,
    /// the once-a-day ad for one more, and a countdown to tomorrow's plays.
    /// </summary>
    public class DailyBonusScreen : MonoBehaviour
    {
        /// <summary>Play: null for the day's next game.</summary>
        public event Action<BonusGame?> PlayPressed;
        public event Action AdPressed;

        private UiScreen screen;
        private TextMeshProUGUI subtitle, countdown, playLabel;
        private Button play, ad;
        private readonly TextMeshProUGUI[] rows = new TextMeshProUGUI[DailyBonus.FreePlays];
        private float tick;

        public static DailyBonusScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("DailyBonus", canvasRoot, out var root);
            var d = root.gameObject.AddComponent<DailyBonusScreen>();
            d.screen = screen;
            d.Build(root);
            return d;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.06f, 0.05f, 0.18f, 0.55f));
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 1180f));
            card.pivot = new Vector2(0.5f, 0.5f);
            screen.SetPopTarget(card);
            UiFactory.MakeButton(card, "X", Kind.Icon, new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(100f, 100f), Hide, 48f);
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(700f, 100f), Loc.T("daily.bonusTitle"), 64f, Palette.UiGold, title: true);
            subtitle = UiFactory.TextBox("Sub", card, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(820f, 90f), "", 32f, Palette.UiText, FontStyles.Normal);
            subtitle.textWrappingMode = TextWrappingModes.Normal;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = UiFactory.Box("Row" + i, card, new Vector2(0.5f, 1f), new Vector2(0f, -270f - i * 120f), new Vector2(780f, 104f));
                UiFactory.Fill(row, new Color(0.04f, 0.04f, 0.09f, 0.6f) /* glass */, UiSprites.Rounded, 1.4f).raycastTarget = false;
                rows[i] = UiFactory.TextBox("Text", row, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(740f, 90f), "", 38f, Palette.UiText);
                rows[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            play = UiFactory.MakeButton(card, "", Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(620f, 150f), () => PlayPressed?.Invoke(null), 60f);
            playLabel = play.GetComponentInChildren<TextMeshProUGUI>();
            ad = UiFactory.MakeButton(card, Loc.T("daily.watchAd"), Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(620f, 150f), () => AdPressed?.Invoke(), 46f);
            countdown = UiFactory.TextBox("Countdown", card, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(820f, 80f), "", 34f, new Color(0.85f, 0.86f, 1f, 0.85f), FontStyles.Normal);
            UiFactory.TextBox("Note", card, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(820f, 60f), Loc.T("daily.note"), 26f, new Color(1f, 1f, 1f, 0.55f), FontStyles.Normal);
        }

        public void Show()
        {
            screen.Show();
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Hide() => screen.Hide();

        public bool Visible => screen.IsVisible;

        public void Refresh()
        {
            var games = DailyBonus.TodaysGames();
            int used = DailyBonus.Used;
            for (int i = 0; i < rows.Length; i++)
            {
                bool done = i < used;
                rows[i].text = done ? "<s>" + (i + 1) + ".  " + Loc.T("bonusGame." + games[i]) + "</s>" : (i + 1) + ".  " + Loc.T("bonusGame." + games[i]);
                rows[i].color = done ? new Color(1f, 1f, 1f, 0.4f) : Palette.UiText;
            }
            int left = DailyBonus.Left;
            subtitle.text = Loc.F("daily.left", left, DailyBonus.FreePlays);
            play.gameObject.SetActive(left > 0);
            playLabel.text = Loc.F("daily.play", left);
            ad.gameObject.SetActive(left == 0 && DailyBonus.CanWatchAd);
            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            int left = DailyBonus.Left;
            if (left > 0) { countdown.text = ""; return; }
            var t = TimeSpan.FromSeconds(GameClock.SecondsToMidnight);
            countdown.text = Loc.F("daily.tomorrow", t.Hours.ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00"));
        }

        private void Update()
        {
            tick -= Time.unscaledDeltaTime;
            if (tick > 0f) return;
            tick = 1f;
            UpdateCountdown();
        }
    }
}
