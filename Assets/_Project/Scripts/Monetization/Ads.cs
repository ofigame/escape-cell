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
        public static IRewardedAds Rewarded { get; set; } = new TestRewardedAds();
        public static IBannerAds Banner { get; set; } = new TestBannerAds();
        public static IInterstitialAds Interstitial { get; set; } = new NoInterstitialAds();

        /// <summary>The menus want the banner right now (a real banner that loads later shows up if so).</summary>
        public static bool BannerWanted;

        // Pacing of the ads between levels: never in the first levels, at most every few levels and minutes, and
        // never right after the player chose to watch a rewarded ad.
        private const int FreeLevels = 10, LevelsBetween = 3;
        private const float SecondsBetween = 90f, AfterRewarded = 60f;
        private static int levelsSince;
        private static float lastFullScreen = -999f;

        /// <summary>A rewarded or interstitial ad was just watched.</summary>
        public static void MarkFullScreenShown() => lastFullScreen = Time.realtimeSinceStartup;

        /// <summary>
        /// After a level is won and the player moves on: maybe an interstitial first, then <paramref name="then"/>.
        /// </summary>
        public static void AfterLevel(int levelIndex, Action then)
        {
            levelsSince++;
            float since = Time.realtimeSinceStartup - lastFullScreen;
            bool due = levelIndex >= FreeLevels && levelsSince >= LevelsBetween && since >= SecondsBetween && since >= AfterRewarded;
            if (!due || !Interstitial.IsReady)
            {
                then?.Invoke();
                return;
            }
            levelsSince = 0;
            MarkFullScreenShown();
            Interstitial.Show(then);
        }

        /// <summary>Height (in UI units of the 1080-wide reference canvas) kept free at the bottom of menu screens for the banner.</summary>
        public const float BannerReserve = 170f;
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
            scaler.matchWidthOrHeight = Screen.width < Screen.height ? 0f : 1f;
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
