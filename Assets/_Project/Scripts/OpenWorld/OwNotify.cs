using System;
using System.Collections;
using SquashBot.Data;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using Unity.Notifications.iOS;
#endif

namespace SquashBot.OpenWorld
{
    /// <summary>
    /// The farm's notes on the phone (local notifications, no server needed): the first crop ready, the first animal
    /// produce ready, the bandits' warning two hours before a raid, and the raid itself. Set when the player leaves the
    /// open world or sends the game away; cleared when they come back. Permission is asked once, on the first visit.
    /// </summary>
    public static class OwNotify
    {
        private const string AskedKey = "sb_ow_notify_asked";
        private const string Channel = "ow_farm";
        private static bool ready;

        /// <summary>The first visit: the channel, and the player is asked (Android 13+, iOS).</summary>
        public static IEnumerator Init()
        {
            if (ready) yield break;
            ready = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(Channel, Loc.T("ow.notif.channel"), Loc.T("ow.notif.channelLine"), Importance.Default));
            if (Prefs.GetInt(AskedKey, 0) == 0)
            {
                Prefs.SetInt(AskedKey, 1);
                Prefs.Save();
                var request = new PermissionRequest();
                while (request.Status == PermissionStatus.RequestPending) yield return null;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (Prefs.GetInt(AskedKey, 0) == 0)
            {
                Prefs.SetInt(AskedKey, 1);
                Prefs.Save();
                using var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false);
                while (!req.IsFinished) yield return null;
            }
#endif
            yield break;
        }

        /// <summary>Back in the open world: what was waiting is cleared (the player is here).</summary>
        public static void Clear()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { AndroidNotificationCenter.CancelAllScheduledNotifications(); AndroidNotificationCenter.CancelAllDisplayedNotifications(); } catch (Exception e) { Debug.LogWarning("[OwNotify] " + e.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { iOSNotificationCenter.RemoveAllScheduledNotifications(); iOSNotificationCenter.RemoveAllDeliveredNotifications(); iOSNotificationCenter.ApplicationBadge = 0; } catch (Exception e) { Debug.LogWarning("[OwNotify] " + e.Message); }
#endif
        }

        /// <summary>Leaving: notes for what will happen while the player is away.</summary>
        public static void Schedule(OwSave save)
        {
            Clear();
            long now = OwSave.Now;
            // The first crop to ripen, the first produce to collect.
            long crop = long.MaxValue;
            foreach (var p in save.plots)
                if (p.crop >= 0) { long at = p.planted + OwData.Of((Crop)p.crop).seconds; if (at > now && at < crop) crop = at; }
            if (crop != long.MaxValue) Send("crop", Loc.T("ow.notif.cropTitle"), Loc.T("ow.notif.cropBody"), crop - now);
            long produce = long.MaxValue;
            foreach (var b in save.buildings)
                if (b.fed > 0) { long at = b.fed + OwData.Of((BuildingKind)b.kind).seconds; if (at > now && at < produce) produce = at; }
            if (produce != long.MaxValue && Math.Abs(produce - crop) > 600) Send("produce", Loc.T("ow.notif.produceTitle"), Loc.T("ow.notif.produceBody"), produce - now);
            // The bandits.
            if (save.raidAt > now)
            {
                long warn = save.raidAt - (long)(save.WarningHours * 3600) - now;
                if (warn > 60) Send("warn", Loc.T("ow.notif.warnTitle"), Loc.F("ow.notif.warnBody", Mathf.RoundToInt(save.WarningHours * 60f)), warn);
                Send("raid", Loc.T("ow.notif.raidTitle"), Loc.T("ow.notif.raidBody"), save.raidAt - now);
            }
        }

        private static void Send(string id, string title, string body, long inSeconds)
        {
            if (inSeconds <= 0) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var n = new AndroidNotification(title, body, DateTime.Now.AddSeconds(inSeconds));
                AndroidNotificationCenter.SendNotification(n, Channel);
            }
            catch (Exception e) { Debug.LogWarning("[OwNotify] " + e.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                iOSNotificationCenter.ScheduleNotification(new iOSNotification
                {
                    Identifier = "ow_" + id,
                    Title = title,
                    Body = body,
                    ShowInForeground = false,
                    Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = TimeSpan.FromSeconds(inSeconds), Repeats = false },
                });
            }
            catch (Exception e) { Debug.LogWarning("[OwNotify] " + e.Message); }
#else
            Debug.Log("[OwNotify] " + id + " in " + inSeconds + " s: " + title + " — " + body);
#endif
        }
    }
}
