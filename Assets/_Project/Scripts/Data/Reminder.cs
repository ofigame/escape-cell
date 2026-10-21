using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SquashBot.Data
{
    /// <summary>
    /// The daily reminder: one local notification for the next evening (19:00, at least half a day away), set again each
    /// time the game opens or goes to the background, so it only comes on days away from the game. iOS asks the player
    /// the first time; Android shows it from its own alarm (no Google services needed: Huawei too).
    /// </summary>
    public static class Reminder
    {
        private const int Hour = 19;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void EscapeCell_ScheduleReminder(string title, string body, double seconds);
#endif

        /// <summary>When the next reminder comes.</summary>
        private static DateTime Next()
        {
            var now = DateTime.Now;
            var at = now.Date.AddHours(Hour);
            while ((at - now).TotalHours < 12) at = at.AddDays(1);
            return at;
        }

        public static void Schedule()
        {
            try
            {
                var at = Next();
                string title = Loc.T("reminder.title"), body = Loc.T("reminder.body");
#if UNITY_IOS && !UNITY_EDITOR
                EscapeCell_ScheduleReminder(title, body, (at - DateTime.Now).TotalSeconds);
#elif UNITY_ANDROID && !UNITY_EDITOR
                long millis = new DateTimeOffset(at).ToUnixTimeMilliseconds();
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var reminder = new AndroidJavaClass("com.ofigame.foi.FoiReminder"))
                    reminder.CallStatic("schedule", activity, title, body, millis);
#endif
            }
            catch (Exception e) { Debug.LogWarning("[Reminder] " + e.Message); }
        }
    }
}
