namespace SquashBot.Monetization
{
    /// <summary>
    /// AdMob ad unit ids. Store builds (scripting define REAL_ADS) use the game's own units (publisher
    /// pub-2558895513508689, apps "foi | cell" on Android and iOS); every other build (the test APKs and IPAs
    /// sideloaded onto our phones) keeps Google's public TEST units, which always fill with labelled test ads and are
    /// safe to click, so testing never shows our own live ads (clicking those can get the AdMob account closed). The app ids live in
    /// Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset.
    /// </summary>
    public static class AdIds
    {
#if !REAL_ADS
#if UNITY_IOS
        public const string Rewarded = "ca-app-pub-3940256099942544/1712485313";
        public const string Interstitial = "ca-app-pub-3940256099942544/4411468910";
        public const string Banner = "ca-app-pub-3940256099942544/2934735716";
#else
        public const string Rewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string Interstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string Banner = "ca-app-pub-3940256099942544/6300978111";
#endif
#else
#if UNITY_IOS
        public const string Rewarded = "ca-app-pub-2558895513508689/2296068765";
        public const string Interstitial = "ca-app-pub-2558895513508689/2240228440";
        public const string Banner = "ca-app-pub-2558895513508689/5018520741";
#else
        public const string Rewarded = "ca-app-pub-2558895513508689/9971464529";
        public const string Interstitial = "ca-app-pub-2558895513508689/5113803794";
        public const string Banner = "ca-app-pub-2558895513508689/1118718465";
#endif
#endif

        /// <summary>True while the test units are in use (shown in logs so a test build is never shipped by mistake).</summary>
        public static bool AreTestIds => Rewarded.StartsWith("ca-app-pub-3940256099942544");
    }
}
