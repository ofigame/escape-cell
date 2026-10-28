using System;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// The 7-day login calendar card: the seven days' gifts (the ones taken dimmed, today's lit in gold), the button for
    /// today's gift, and a countdown to tomorrow's once it is taken.
    /// </summary>
    public class LoginCalendarScreen : MonoBehaviour
    {
        /// <summary>Today's gift was taken (the day, 0-based).</summary>
        public event Action<int> Claimed;

        private UiScreen screen;
        private readonly Image[] tiles = new Image[LoginCalendar.Days];
        private readonly TextMeshProUGUI[] states = new TextMeshProUGUI[LoginCalendar.Days];
        private Button claim;
        private TextMeshProUGUI countdown;
        private float tick;

        public static LoginCalendarScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("LoginCalendar", canvasRoot, out var root);
            screen.BackAction = () => screen.Hide(); // (the Android back key: the same on every screen)
            var d = root.gameObject.AddComponent<LoginCalendarScreen>();
            d.screen = screen;
            d.Build(root);
            return d;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.06f, 0.05f, 0.18f, 0.55f));
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1240f));
            card.pivot = new Vector2(0.5f, 0.5f);
            screen.SetPopTarget(card);
            UiFactory.MakeButton(card, "X", Kind.Icon, new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(100f, 100f), Hide, 48f);
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(720f, 100f), Loc.T("login.title"), 60f, Palette.UiGold, title: true);

            // Four days on the first row, three on the second (the seventh, the gold chest, a little wider).
            for (int i = 0; i < LoginCalendar.Days; i++)
            {
                bool top = i < 4;
                float x = top ? -321f + i * 214f : -214f + (i - 4) * 214f;
                float y = top ? -170f : -480f;
                var size = new Vector2(i == 6 ? 220f : 196f, 290f);
                var tile = UiFactory.Box("Day" + i, card, new Vector2(0.5f, 1f), new Vector2(x, y), size);
                tiles[i] = UiFactory.Fill(tile, Color.white, UiSprites.Rounded, 1.4f);
                tiles[i].raycastTarget = false;
                UiFactory.TextBox("Day", tile, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(size.x, 50f), Loc.F("login.day", i + 1), 30f, Color.white, title: true);
                Gift(tile, LoginCalendar.Gifts[i]);
                states[i] = UiFactory.TextBox("State", tile, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(size.x, 44f), "", 26f, Color.white, title: true);
            }

            claim = UiFactory.MakeButton(card, Loc.T("login.claim"), Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(680f, 150f), Claim, 46f);
            countdown = UiFactory.TextBox("Countdown", card, new Vector2(0.5f, 0f), new Vector2(0f, 290f), new Vector2(820f, 80f), "", 36f, new Color(0.85f, 0.86f, 1f, 0.9f), FontStyles.Normal);
            var note = UiFactory.TextBox("Note", card, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(820f, 120f), Loc.T("login.note"), 28f, new Color(1f, 1f, 1f, 0.6f), FontStyles.Normal);
            note.textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>A day's gift on its tile: a coin, a chest in its strap colour or a core, and how much.</summary>
        private static void Gift(RectTransform tile, (LoginCalendar.Kind kind, int amount, ChestTier tier) g)
        {
            var disc = UiFactory.Box("Gift", tile, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(110f, 110f));
            disc.pivot = new Vector2(0.5f, 0.5f);
            var colour = g.kind == LoginCalendar.Kind.Gold ? Palette.Coin
                : g.kind == LoginCalendar.Kind.Cores ? new Color(0.35f, 0.9f, 1f)
                : ChestModels.StrapColour(g.tier);
            UiFactory.Fill(disc, colour, g.kind == LoginCalendar.Kind.Chest ? UiSprites.Rounded : UiSprites.Circle, 1.4f).raycastTarget = false;
            if (g.kind == LoginCalendar.Kind.Chest)
            {
                // a chest's lid line and lock on the box
                var lid = UiFactory.Box("Lid", disc, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(110f, 8f));
                lid.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(lid, new Color(0f, 0f, 0f, 0.35f)).raycastTarget = false;
                var lockBox = UiFactory.Box("Lock", disc, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(26f, 30f));
                lockBox.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(lockBox, Palette.UiGold, UiSprites.Rounded, 4f).raycastTarget = false;
            }
            string what = g.kind == LoginCalendar.Kind.Gold ? Loc.F("login.gold", g.amount)
                : g.kind == LoginCalendar.Kind.Cores ? Loc.F("login.cores", g.amount)
                : DuelChestScreen.TierName(g.tier);
            var t = UiFactory.TextBox("What", tile, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(tile.sizeDelta.x - 12f, 60f), what, 28f, Color.white, title: true);
            t.enableAutoSizing = true;
            t.fontSizeMin = 16f;
            t.fontSizeMax = 28f;
        }

        public void Show()
        {
            screen.Show();
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Hide() => screen.Hide();

        public bool Visible => screen.IsVisible;

        private void Claim()
        {
            int day = LoginCalendar.Claim();
            if (day < 0) return;
            Refresh();
            Claimed?.Invoke(day);
        }

        public void Refresh()
        {
            int next = LoginCalendar.Day;
            bool ready = LoginCalendar.Ready;
            for (int i = 0; i < tiles.Length; i++)
            {
                // the days before the next one were taken this round (all of them when the round has just come full circle)
                bool taken = i < next || (!ready && next == 0 && LoginCalendar.Started);
                bool today = ready && i == next;
                tiles[i].color = today ? new Color(0.95f, 0.66f, 0.15f, 0.95f)
                    : taken ? new Color(0.12f, 0.45f, 0.25f, 0.55f)
                    : new Color(0.04f, 0.04f, 0.09f, 0.6f);
                states[i].text = taken ? Loc.T("login.taken") : today ? Loc.T("login.got") : "";
                states[i].color = taken ? new Color(0.6f, 1f, 0.7f) : Color.white;
            }
            claim.gameObject.SetActive(ready);
            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            if (LoginCalendar.Ready) { countdown.text = ""; return; }
            var t = TimeSpan.FromSeconds(GameClock.SecondsToMidnight);
            countdown.text = Loc.F("login.come", t.Hours.ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00"));
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
