using System;
using System.Collections;
using System.Linq;
using BrickMuseum.Data;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#elif UNITY_IOS && !UNITY_EDITOR
using Unity.Notifications.iOS;
#endif

namespace BrickMuseum.Core
{
    /// <summary>
    /// Local notifications on the phone (Android, iOS; nothing on the desktop). While the game is in the background a
    /// few reminders wait: the fish farm's batch is ready, the city misses its builder after a day, and (with a taxi of
    /// your own) the townspeople can't find a taxi. They are all cancelled when the game comes back.
    /// </summary>
    public static class Notify
    {
        private const string Channel = "brickville";
        private static bool ready;

        public const string FishReady = "Balıklar toplanmayı bekliyor!";
        public const string GrowCity = "Şehrini geliştir, yenilikleri aç!";
        public const string NoTaxi = "Halk huzursuz, taksi bulamıyor.";
        public const string CropsReady = "Ekinler hasada hazır!";
        public const string BarnReady = "Çiftlikte ürünler toplanmayı bekliyor!";
        public const string HotelsFull = "Otellerinin kasası doldu, geliri topla!";

        /// <summary>Sets the channel up and asks for permission (once the player has built something).</summary>
        public static IEnumerator Init()
        {
            if (ready) yield break;
            ready = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.Initialize();
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(Channel, "Brickville", "Brickville", Importance.Default));
            var ask = new PermissionRequest();
            while (ask.Status == PermissionStatus.RequestPending) yield return null;
#elif UNITY_IOS && !UNITY_EDITOR
            using (var ask = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false))
                while (!ask.IsFinished) yield return null;
#endif
            yield break;
        }

        public static void CancelAll()
        {
            if (!ready) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
            iOSNotificationCenter.ApplicationBadge = 0;
#endif
        }

        /// <summary>The reminders that wait while the game is away (each text in the player's language).</summary>
        public static void Plan()
        {
            if (!ready) return;
            CancelAll();
            if (FishFarm.Owned)
            {
                var left = FishFarm.Left;
                Schedule(1, FishReady, left < TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(30) : left);
            }
            Schedule(2, GrowCity, TimeSpan.FromHours(24));
            var crop = Farming.NextCrop();
            if (crop != null) Schedule(4, CropsReady, crop.Value < TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(20) : crop.Value);
            var barn = Farming.NextBarn();
            if (barn != null) Schedule(5, BarnReady, barn.Value < TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(45) : barn.Value);
            if (Progress.Data.cars.Any(FreeBuild.Garage.IsTaxi)) Schedule(3, NoTaxi, TimeSpan.FromHours(8));
            if (City.Hotels.All().Count > 0) Schedule(6, HotelsFull, TimeSpan.FromHours(City.Hotels.CapHours));
        }

        private static void Schedule(int id, string text, TimeSpan after)
        {
            string body = Loc.Translate(text);
#if UNITY_ANDROID && !UNITY_EDITOR
            var n = new AndroidNotification("Brickville", body, DateTime.Now + after);
            AndroidNotificationCenter.SendNotificationWithExplicitID(n, Channel, id);
#elif UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = "bv" + id, Title = "Brickville", Body = body, Badge = 1, ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = after, Repeats = false },
            });
#endif
            Debug.Log($"[Notify] in {after.TotalMinutes:F0} min: {body}");
        }
    }
}
