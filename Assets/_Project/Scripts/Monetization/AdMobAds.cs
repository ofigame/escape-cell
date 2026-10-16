using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace SquashBot.Monetization
{
    /// <summary>
    /// Google AdMob (with Unity Ads mediated through the AdMob console).
    /// Start-up order: the consent form (UMP: GDPR and similar, shown only where required) → the SDK → the first ads
    /// are loaded. Only on phones; the editor and the Windows build keep the test stand-ins.
    /// </summary>
    public static class AdMob
    {
        private static bool started, initialized;

        /// <summary>What the ads are doing, for the hidden line in the settings (five taps on the title): started,
        /// the consent step's answer and any error, initialised. Lets a store build be checked on the phone itself.</summary>
        public static string Status { get; private set; } = "not started";

        /// <summary>Called once at start-up: asks for consent if needed, then starts AdMob and swaps the real ads in.</summary>
        public static void Start()
        {
#if HUAWEI
            UnityAdsDirect.Start(); // the AppGallery build: no Google services on those phones, Unity Ads directly
            return;
#endif
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (started) return;
            started = true;
            MainThread.Ensure();
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            if (AdIds.AreTestIds) Debug.Log("[Ads] AdMob runs with Google's TEST ad ids.");
            Status = "consent: asking";
            // Whatever the consent step does (no answer, an exception on some phones), AdMob starts within 12 s: without
            // a consent answer Google itself serves only limited, non-personalised ads where consent is required.
            MainThread.After(12f, () =>
            {
                if (initialized) return;
                Status += " · no answer in 12 s";
                Initialize();
            });
            try
            {
            var request = new ConsentRequestParameters();
            // The consent step answers on another thread on iOS: everything it says is handled back on Unity's main thread.
            ConsentInformation.Update(request, updateError => MainThread.Run(() =>
            {
                if (updateError != null) Debug.LogWarning("[Ads] Consent info: " + updateError.Message);
                Status = updateError != null ? "consent info error: " + updateError.Message : "consent: " + ConsentInformation.ConsentStatus;
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => MainThread.Run(() =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] Consent form: " + formError.Message);
                    if (formError != null) Status += " · form error: " + formError.Message;
                    Status += " · can request: " + ConsentInformation.CanRequestAds();
                    if (ConsentInformation.CanRequestAds()) Initialize();
                }));
            }));
            // A returning player who already answered can get ads at once.
            if (ConsentInformation.CanRequestAds()) Initialize();
            }
            catch (Exception e)
            {
                Status = "consent exception: " + e.GetType().Name + ": " + e.Message;
                Debug.LogWarning("[Ads] " + Status);
                Initialize();
            }
#endif
        }

        private static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Status += " · initialising";
            // Ads fit for a cartoon game played by all ages: nothing above PG (parental guidance); never marked as
            // child-directed or under the age of consent (the game is not aimed at children only).
            try
            {
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                MaxAdContentRating = MaxAdContentRating.PG,
                TagForChildDirectedTreatment = TagForChildDirectedTreatment.Unspecified,
            });
            MobileAds.Initialize(status => MainThread.Run(() =>
            {
                Debug.Log("[Ads] AdMob initialized.");
                Status = "AdMob ready (" + (AdIds.AreTestIds ? "test ids" : "live ids") + ")";
                var rewarded = new AdMobRewarded();
                var interstitial = new AdMobInterstitial();
                var banner = new AdMobBanner();
                Ads.Rewarded = rewarded;
                Ads.Interstitial = interstitial;
                Ads.Banner = banner;
                rewarded.Load();
                interstitial.Load();
                if (Ads.BannerWanted) banner.Show();
                Ads.RaiseBannerChanged(); // the placeholder strip goes
            }));
            }
            catch (Exception e)
            {
                Status += " · AdMob exception: " + e.GetType().Name + ": " + e.Message;
                Debug.LogWarning("[Ads] " + Status);
            }
        }

        /// <summary>The privacy options entry (settings screen) is required where the consent form applies.</summary>
        public static bool PrivacyOptionsRequired
        {
            get
            {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && !HUAWEI
                return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
#else
                return false;
#endif
            }
        }

        public static void ShowPrivacyOptions()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            ConsentForm.ShowPrivacyOptionsForm(error => MainThread.Run(() =>
            {
                if (error != null) Debug.LogWarning("[Ads] Privacy options: " + error.Message);
                if (ConsentInformation.CanRequestAds()) Initialize();
            }));
#endif
        }
    }

    /// <summary>A rewarded video, always one loaded ahead; a failed load retries with a growing pause.</summary>
    public class AdMobRewarded : IRewardedAds
    {
        private RewardedAd ad;
        private bool loading;
        private float retry = 2f;

        public bool IsReady => ad != null && ad.CanShowAd();

        public void Load()
        {
            if (loading || IsReady) return;
            loading = true;
            RewardedAd.Load(AdIds.Rewarded, new AdRequest(), (loaded, error) =>
            {
                loading = false;
                if (error != null || loaded == null)
                {
                    Debug.LogWarning("[Ads] Rewarded failed to load: " + error);
                    AdRetry.After(retry, Load);
                    retry = Mathf.Min(retry * 2f, 60f);
                    return;
                }
                retry = 2f;
                ad = loaded;
            });
        }

        public void Show(Action<bool> onFinished)
        {
            if (!IsReady)
            {
                onFinished?.Invoke(false);
                Load();
                return;
            }
            var showing = ad;
            ad = null;
            bool earned = false;
            showing.OnAdFullScreenContentClosed += () =>
            {
                showing.Destroy();
                Ads.MarkFullScreenShown();
                onFinished?.Invoke(earned);
                Load();
            };
            showing.OnAdFullScreenContentFailed += error =>
            {
                showing.Destroy();
                onFinished?.Invoke(false);
                Load();
            };
            showing.Show(reward => earned = true);
        }
    }

    /// <summary>An interstitial between levels (the pacing rules live in <see cref="Ads.AfterLevel"/>).</summary>
    public class AdMobInterstitial : IInterstitialAds
    {
        private InterstitialAd ad;
        private bool loading;
        private float retry = 2f;

        public bool IsReady => ad != null && ad.CanShowAd();

        public void Load()
        {
            if (loading || IsReady) return;
            loading = true;
            InterstitialAd.Load(AdIds.Interstitial, new AdRequest(), (loaded, error) =>
            {
                loading = false;
                if (error != null || loaded == null)
                {
                    Debug.LogWarning("[Ads] Interstitial failed to load: " + error);
                    AdRetry.After(retry, Load);
                    retry = Mathf.Min(retry * 2f, 60f);
                    return;
                }
                retry = 2f;
                ad = loaded;
            });
        }

        public void Show(Action onClosed)
        {
            if (!IsReady)
            {
                onClosed?.Invoke();
                Load();
                return;
            }
            var showing = ad;
            ad = null;
            showing.OnAdFullScreenContentClosed += () =>
            {
                showing.Destroy();
                onClosed?.Invoke();
                Load();
            };
            showing.OnAdFullScreenContentFailed += error =>
            {
                showing.Destroy();
                onClosed?.Invoke();
                Load();
            };
            showing.Show();
        }
    }

    /// <summary>An adaptive banner at the bottom of the menu screens (never during play).</summary>
    public class AdMobBanner : IBannerAds
    {
        private BannerView view;
        private bool visible;

        public bool IsPlaceholder => false;

        public void Show()
        {
            visible = true;
            if (view == null)
            {
                var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
                view = new BannerView(AdIds.Banner, size, AdPosition.Bottom);
                view.OnBannerAdLoadFailed += error => Debug.LogWarning("[Ads] Banner failed to load: " + error);
                view.LoadAd(new AdRequest());
            }
            view.Show();
        }

        public void Hide()
        {
            visible = false;
            view?.Hide();
        }
    }

    /// <summary>Runs a retry after a pause (ad loads that failed for lack of fill or network).</summary>
    public class AdRetry : MonoBehaviour
    {
        private float left;
        private Action action;

        public static void After(float seconds, Action action)
        {
            var go = new GameObject("AdRetry");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<AdRetry>();
            r.left = seconds;
            r.action = action;
        }

        private void Update()
        {
            left -= Time.unscaledDeltaTime;
            if (left > 0f) return;
            var a = action;
            Destroy(gameObject);
            a?.Invoke();
        }
    }
}
