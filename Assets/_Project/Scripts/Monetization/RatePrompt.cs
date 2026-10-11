using System;
using UnityEngine;

namespace SquashBot.Monetization
{
    /// <summary>
    /// Asks for a store rating with the store's own in-game card, at good moments only: first when level 10 is beaten,
    /// then when a world is finished — never within 30 days of the last ask, at most three times a year (the stores cap
    /// it themselves too: iOS shows it at most three times in 365 days, Google Play has its own quota). Nothing is ever
    /// given for a rating (both stores forbid it) and the game never learns whether one was given. The record lives on
    /// the phone (PlayerPrefs); no server needed.
    /// iOS: Unity's built-in call. Android: Google's Play In-App Review package, compiled in with the PLAY_REVIEW define.
    /// </summary>
    public static class RatePrompt
    {
        private const string AsksKey = "sb_rate_asks"; // unix days of the asks, comma separated (last year's only)
        private const int GapDays = 30, PerYear = 3;

        private static int Today => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400);

        /// <summary>A level was beaten for the first time (1-based number); <paramref name="worldDone"/> if it ended a world.</summary>
        public static bool ShouldAsk(int levelNumber, bool worldDone)
        {
            if (levelNumber < 10 || (levelNumber != 10 && !worldDone)) return false;
            var asks = Asks();
            int today = Today;
            int last = asks.Count > 0 ? asks[asks.Count - 1] : int.MinValue / 2;
            return today - last >= GapDays && asks.Count < PerYear;
        }

        public static void Ask()
        {
            var asks = Asks();
            asks.Add(Today);
            PlayerPrefs.SetString(AsksKey, string.Join(",", asks));
            PlayerPrefs.Save();
            Debug.Log("RatePrompt: asking for a rating now");
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.RequestStoreReview();
#elif UNITY_ANDROID && PLAY_REVIEW && !UNITY_EDITOR
            AskOnPlay();
#endif
        }

#if UNITY_ANDROID && PLAY_REVIEW && !UNITY_EDITOR
        private static Google.Play.Review.ReviewManager review;

        private static void AskOnPlay()
        {
            review ??= new Google.Play.Review.ReviewManager();
            var request = review.RequestReviewFlow();
            request.Completed += op =>
            {
                if (op.Error != Google.Play.Review.ReviewErrorCode.NoError) return;
                review.LaunchReviewFlow(op.GetResult());
            };
        }
#endif

        /// <summary>The asks of the last 365 days, oldest first.</summary>
        private static System.Collections.Generic.List<int> Asks()
        {
            var list = new System.Collections.Generic.List<int>();
            int today = Today;
            foreach (var s in PlayerPrefs.GetString(AsksKey, "").Split(','))
                if (int.TryParse(s, out int d) && today - d < 365) list.Add(d);
            return list;
        }
    }
}
