using System;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using TMPro;
using UnityEngine;

namespace SquashBot.Monetization
{
    /// <summary>A rewarded video provider (AdMob, Unity LevelPlay, ...).</summary>
    public interface IRewardedAds
    {
        bool IsReady { get; }

        /// <summary>Shows an ad; the callback receives true only if the player earned the reward.</summary>
        void Show(Action<bool> onFinished);
    }

    /// <summary>A full-screen ad between levels.</summary>
    public interface IInterstitialAds
    {
        bool IsReady { get; }

        /// <summary>Shows the ad (or nothing if none is loaded); <paramref name="onClosed"/> runs either way.</summary>
        void Show(Action onClosed);
    }

    /// <summary>A small banner anchored to the bottom of the screen, shown only outside gameplay.</summary>
    public interface IBannerAds
    {
        /// <summary>True while this is the in-game placeholder rather than a real network's banner.</summary>
        bool IsPlaceholder { get; }

        void Show();
        void Hide();
    }

    /// <summary>
    /// Entry point for ads. The game only talks to <see cref="Rewarded"/> and <see cref="Banner"/>; swapping in a real
    /// ad network means writing those implementations and assigning them here at startup.
    /// </summary>
    public static class Ads
    {
#if REAL_ADS
        // A store build never shows the test stand-in: until the network is up there is simply no ad to watch.
        public static IRewardedAds Rewarded { get; set; } = new NoRewardedAds();
#else
        public static IRewardedAds Rewarded { get; set; } = new TestRewardedAds();
#endif
        public static IBannerAds Banner { get; set; } = new TestBannerAds();
        public static IInterstitialAds Interstitial { get; set; } = new NoInterstitialAds();

        /// <summary>The real banner took the placeholder's place (the UI drops its labelled strip).</summary>
        public static event Action BannerChanged;
        public static void RaiseBannerChanged() => BannerChanged?.Invoke();

        /// <summary>The menus want the banner right now (a real banner that loads later shows up if so).</summary>
        public static bool BannerWanted;

        // The ad plan (the scenario's §11): interstitials only at natural breaks, paced and capped.
        //  - after a level, back to the map: from level 11, every 2nd level, at least 120 s after the last one;
        //  - after a duel, back to the menu: every 3rd duel (the first 5 are free), at least 180 s after the last one;
        //  - never within 90 s of a rewarded ad; never after 3 losses in a row; at most 3 in 30 minutes and 20 a day.
        private const int FreeLevels = 10, LevelsBetween = 2, FreeDuels = 5, DuelsBetween = 3, PerHalfHour = 3, PerDay = 20;
        private const float LevelGap = 120f, DuelGap = 180f, AfterRewarded = 90f;
        private static int levelsSince, duelsSince, lossesInRow;
        private static float lastInterstitial = -999f, lastRewarded = -999f;
        private static readonly System.Collections.Generic.List<float> recent = new System.Collections.Generic.List<float>();

        /// <summary>A rewarded ad was just watched (no interstitial for a while after it).</summary>
        public static void MarkFullScreenShown() => lastRewarded = Time.realtimeSinceStartup;

        /// <summary>A level or duel ended in a loss (or not): three losses in a row skip the next interstitial.</summary>
        public static void NoteResult(bool won) => lossesInRow = won ? 0 : lossesInRow + 1;

        private static int ShownToday
        {
            get => Prefs.GetString("sb_ads_day", "") == DateTime.Now.ToString("yyyyMMdd") ? Prefs.GetInt("sb_ads_today", 0) : 0;
            set { Prefs.SetString("sb_ads_day", DateTime.Now.ToString("yyyyMMdd")); Prefs.SetInt("sb_ads_today", value); }
        }

        private static int DuelsPlayed { get => Prefs.GetInt("sb_ads_duels", 0); set => Prefs.SetInt("sb_ads_duels", value); }

        /// <summary>Whether an interstitial may come now (the caps and gaps common to every break).</summary>
        private static bool Allowed(float gap)
        {
            float now = Time.realtimeSinceStartup;
            recent.RemoveAll(t => now - t > 1800f);
            return Interstitial.IsReady && lossesInRow < 3 && now - lastInterstitial >= gap && now - lastRewarded >= AfterRewarded
                   && recent.Count < PerHalfHour && ShownToday < PerDay;
        }

        private static void Show(Action then)
        {
            lastInterstitial = Time.realtimeSinceStartup;
            recent.Add(lastInterstitial);
            ShownToday = ShownToday + 1;
            Interstitial.Show(then);
        }

        /// <summary>After a level, the player going back to the map: maybe an interstitial first, then <paramref name="then"/>.</summary>
        public static void AfterLevel(int levelIndex, Action then)
        {
            levelsSince++;
            if (levelIndex < FreeLevels || levelsSince < LevelsBetween || !Allowed(LevelGap)) { then?.Invoke(); return; }
            levelsSince = 0;
            Show(then);
        }

        /// <summary>After a duel, the player going back to the menu: maybe an interstitial first, then <paramref name="then"/>.</summary>
        public static void AfterDuel(Action then)
        {
            DuelsPlayed = DuelsPlayed + 1;
            duelsSince++;
            if (DuelsPlayed <= FreeDuels || duelsSince < DuelsBetween || !Allowed(DuelGap)) { then?.Invoke(); return; }
            duelsSince = 0;
            Show(then);
        }

        /// <summary>Height (in UI units of the 1080-wide reference canvas) kept free at the bottom of menu screens for the banner.</summary>
        public const float BannerReserve = 170f;
    }

    /// <summary>A store build before the ad network is up: no rewarded ad to watch (never a free test one).</summary>
    public class NoRewardedAds : IRewardedAds
    {
        public bool IsReady => false;
        public void Show(Action<bool> onFinished) => onFinished?.Invoke(false);
    }

    /// <summary>No interstitials off the phones (editor, Windows).</summary>
    public class NoInterstitialAds : IInterstitialAds
    {
        public bool IsReady => false;
        public void Show(Action onClosed) => onClosed?.Invoke();
    }

    /// <summary>Placeholder banner: the UI draws a labelled strip in the reserved area so the layout can be checked.</summary>
    public class TestBannerAds : IBannerAds
    {
        public bool IsPlaceholder => true;
        public void Show() { }
        public void Hide() { }
    }

    /// <summary>Stand-in used until a real ad network is connected: a clearly labelled 5-second "test ad" screen.</summary>
    public class TestRewardedAds : IRewardedAds
    {
        public bool IsReady => true;

        public void Show(Action<bool> onFinished) => TestAdScreen.Open(onFinished);
    }

    public class TestAdScreen : MonoBehaviour
    {
        private const float Duration = 5f;

        private Action<bool> done;
        private TextMeshProUGUI countdown;
        private float left = Duration;

        public static void Open(Action<bool> onFinished)
        {
            var canvas = UiFactory.CreateCanvas("TestAd", out var scaler);
            canvas.sortingOrder = 100;
            scaler.matchWidthOrHeight = UiFactory.MatchFor(Screen.width, Screen.height);
            var screen = canvas.gameObject.AddComponent<TestAdScreen>();
            screen.done = onFinished;
            screen.Build(canvas.transform);
        }

        private void Build(Transform root)
        {
            UiFactory.Dim(root, new Color(0.03f, 0.02f, 0.08f, 0.97f));
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 520f));
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(760f, 120f),
                Loc.T("ad.test"), 84f, Palette.UiGold, title: true);
            countdown = UiFactory.TextBox("Countdown", card, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(760f, 90f),
                "", 52f, Palette.UiText, FontStyles.Normal);
        }

        private void Update()
        {
            left -= Time.unscaledDeltaTime;
            countdown.text = Loc.F("ad.wait", Mathf.CeilToInt(Mathf.Max(0f, left)));
            if (left > 0f) return;

            var callback = done;
            done = null;
            Destroy(gameObject);
            callback?.Invoke(true);
        }
    }
}
