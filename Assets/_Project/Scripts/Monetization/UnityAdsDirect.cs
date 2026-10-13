using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Monetization
{
    /// <summary>
    /// The Huawei AppGallery build (scripting define HUAWEI): Huawei phones without Google services cannot run AdMob,
    /// so the ads come straight from Unity Ads, whose Android SDK the AdMob mediation adapter already brings into the
    /// APK (com.unity3d.ads:unity-ads), called through JNI — no extra package. Rewarded, interstitial and a bottom
    /// banner, on the project's default ad units. There is no Google consent form here: Unity is told the player gave
    /// no consent (gdpr/privacy), so only non-personalised ads are served. Test builds (no REAL_ADS) run Unity's
    /// test mode, which serves labelled test ads.
    /// </summary>
    public static class UnityAdsDirect
    {
        public const string GameId = "800394184";
        public const string RewardedUnit = "Rewarded_Android", InterstitialUnit = "Interstitial_Android", BannerUnit = "Banner_Android";
#if REAL_ADS
        private const bool TestMode = false;
#else
        private const bool TestMode = true;
#endif

        private static bool started;

        public static void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (started) return;
            started = true;
            MainThread.Ensure();
            try
            {
                var activity = Activity;
                // Non-personalised ads only (no consent form in this build).
                foreach (var key in new[] { "gdpr.consent", "privacy.consent" })
                {
                    var meta = new AndroidJavaObject("com.unity3d.ads.metadata.MetaData", activity);
                    meta.Call<bool>("set", key, new AndroidJavaObject("java.lang.Boolean", false));
                    meta.Call("commit");
                }
                Units.CallStatic("initialize", activity, GameId, TestMode, new InitListener());
            }
            catch (Exception e) { Debug.LogWarning("[Ads] Unity Ads start failed: " + e.Message); }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass units;
        internal static AndroidJavaClass Units => units ??= new AndroidJavaClass("com.unity3d.ads.UnityAds");
        internal static AndroidJavaObject Activity => new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");

        private class InitListener : AndroidJavaProxy
        {
            public InitListener() : base("com.unity3d.ads.IUnityAdsInitializationListener") { }

            public void onInitializationComplete() => MainThread.Run(() =>
            {
                Debug.Log("[Ads] Unity Ads initialized" + (TestMode ? " (test mode)." : "."));
                var rewarded = new UnityFullScreen(RewardedUnit);
                var interstitial = new UnityFullScreen(InterstitialUnit);
                Ads.Rewarded = new UnityRewarded(rewarded);
                Ads.Interstitial = new UnityInterstitial(interstitial);
                var banner = new UnityBanner();
                Ads.Banner = banner;
                rewarded.Load();
                interstitial.Load();
                if (Ads.BannerWanted) banner.Show();
            });

            public void onInitializationFailed(AndroidJavaObject error, string message) =>
                MainThread.Run(() => Debug.LogWarning("[Ads] Unity Ads init failed: " + message));
        }

        /// <summary>One full-screen ad unit: loads, shows, reloads after each show; reports whether it was watched to the end.</summary>
        private class UnityFullScreen
        {
            private readonly string unit;
            private bool loading;
            public bool Ready { get; private set; }
            private Action<bool> onDone;

            public UnityFullScreen(string unit) { this.unit = unit; }

            public void Load()
            {
                if (loading || Ready) return;
                loading = true;
                Units.CallStatic("load", unit, new LoadListener(this));
            }

            public void Show(Action<bool> done)
            {
                if (!Ready) { done?.Invoke(false); Load(); return; }
                Ready = false;
                onDone = done;
                Units.CallStatic("show", Activity, unit, new ShowListener(this));
            }

            private void Finished(bool watched)
            {
                var d = onDone;
                onDone = null;
                d?.Invoke(watched);
                Load();
            }

            private class LoadListener : AndroidJavaProxy
            {
                private readonly UnityFullScreen ad;
                public LoadListener(UnityFullScreen ad) : base("com.unity3d.ads.IUnityAdsLoadListener") { this.ad = ad; }
                public void onUnityAdsAdLoaded(string placementId) => MainThread.Run(() => { ad.loading = false; ad.Ready = true; });
                public void onUnityAdsFailedToLoad(string placementId, AndroidJavaObject error, string message) => MainThread.Run(() =>
                {
                    ad.loading = false;
                    Debug.LogWarning($"[Ads] {placementId} failed to load: {message}");
                    MainThread.After(30f, ad.Load); // try again in a while
                });
            }

            private class ShowListener : AndroidJavaProxy
            {
                private readonly UnityFullScreen ad;
                public ShowListener(UnityFullScreen ad) : base("com.unity3d.ads.IUnityAdsShowListener") { this.ad = ad; }
                public void onUnityAdsShowFailure(string placementId, AndroidJavaObject error, string message) => MainThread.Run(() => ad.Finished(false));
                public void onUnityAdsShowStart(string placementId) { }
                public void onUnityAdsShowClick(string placementId) { }
                public void onUnityAdsShowComplete(string placementId, AndroidJavaObject state)
                {
                    bool completed = state != null && state.Call<string>("name") == "COMPLETED";
                    MainThread.Run(() => ad.Finished(completed));
                }
            }
        }

        private class UnityRewarded : IRewardedAds
        {
            private readonly UnityFullScreen ad;
            public UnityRewarded(UnityFullScreen ad) { this.ad = ad; }
            public bool IsReady => ad.Ready;
            public void Show(Action<bool> onFinished)
            {
                Ads.MarkFullScreenShown();
                ad.Show(onFinished);
            }
        }

        private class UnityInterstitial : IInterstitialAds
        {
            private readonly UnityFullScreen ad;
            public UnityInterstitial(UnityFullScreen ad) { this.ad = ad; }
            public bool IsReady => ad.Ready;
            public void Show(Action onClosed) => ad.Show(_ => onClosed?.Invoke());
        }

        /// <summary>A 320×50 banner at the bottom of the screen (built and shown on Android's UI thread).</summary>
        private class UnityBanner : IBannerAds
        {
            private AndroidJavaObject view;
            private bool wanted;
            public bool IsPlaceholder => false;

            public void Show()
            {
                wanted = true;
                var activity = Activity;
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        if (view == null)
                        {
                            var size = new AndroidJavaObject("com.unity3d.services.banners.UnityBannerSize", 320, 50);
                            view = new AndroidJavaObject("com.unity3d.services.banners.BannerView", activity, BannerUnit, size);
                            var gravity = new AndroidJavaClass("android.view.Gravity");
                            int g = gravity.GetStatic<int>("BOTTOM") | gravity.GetStatic<int>("LEFT"); // bottom-left (the wide screen's free corner)
                            var lp = new AndroidJavaObject("android.widget.FrameLayout$LayoutParams", -2, -2, g); // wrap content
                            activity.Call("addContentView", view, lp);
                            view.Call("load");
                        }
                        view.Call("setVisibility", wanted ? 0 : 8); // VISIBLE / GONE
                    }
                    catch (Exception e) { Debug.LogWarning("[Ads] Unity banner: " + e.Message); }
                }));
            }

            public void Hide()
            {
                wanted = false;
                if (view == null) return;
                Activity.Call("runOnUiThread", new AndroidJavaRunnable(() => view.Call("setVisibility", 8)));
            }
        }
#endif
    }

    /// <summary>Runs callbacks from Java threads on Unity's main thread (and a delayed call or two).</summary>
    public class MainThread : MonoBehaviour
    {
        private static MainThread instance;
        private static readonly Queue<Action> queue = new Queue<Action>();
        private static readonly List<(float at, Action a)> later = new List<(float, Action)>();

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("MainThread");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MainThread>();
        }

        public static void Run(Action a) { lock (queue) queue.Enqueue(a); }

        public static void After(float seconds, Action a) => later.Add((Time.realtimeSinceStartup + seconds, a));

        private void Update()
        {
            while (true)
            {
                Action a;
                lock (queue) { if (queue.Count == 0) break; a = queue.Dequeue(); }
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
            for (int i = later.Count - 1; i >= 0; i--)
            {
                if (Time.realtimeSinceStartup < later[i].at) continue;
                var a = later[i].a;
                later.RemoveAt(i);
                a();
            }
        }
    }
}
