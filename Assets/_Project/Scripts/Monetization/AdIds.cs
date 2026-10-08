namespace SquashBot.Monetization
{
    /// <summary>
    /// AdMob ad unit ids. These are Google's public TEST ids: they always fill with labelled test ads and are safe to
    /// click. Replace them with the real ids from the AdMob console before release (and the app ids in
    /// Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset).
    /// </summary>
    public static class AdIds
    {
#if UNITY_IOS
        public const string Rewarded = "ca-app-pub-3940256099942544/1712485313";
        public const string Interstitial = "ca-app-pub-3940256099942544/4411468910";
        public const string Banner = "ca-app-pub-3940256099942544/2934735716";
#else
        public const string Rewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string Interstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string Banner = "ca-app-pub-3940256099942544/6300978111";
#endif

        /// <summary>True while the test ids above are in use (shown in logs so a test build is never shipped by mistake).</summary>
        public static bool AreTestIds => Rewarded.StartsWith("ca-app-pub-3940256099942544");
    }
}
