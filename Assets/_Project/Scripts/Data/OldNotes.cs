using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SquashBot.Data
{
    /// <summary>
    /// The removed open world's farm and raid notes, still waiting on the phones that had it: the game clears them at
    /// start. iOS keeps local notifications with the system (they came on after the update), so every start removes the
    /// pending and shown ones (the game sends none of its own any more); Android, once, cancels what is shown and deletes
    /// the farm's notification channel.
    /// </summary>
    public static class OldNotes
    {
        private const string AndroidDoneKey = "sb_old_notes_cleared"; // (this phone's own: PlayerPrefs, not the cloud copy)

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void EscapeCell_ClearNotes();
#endif

        public static void Clear()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                EscapeCell_ClearNotes();
#elif UNITY_ANDROID && !UNITY_EDITOR
                if (PlayerPrefs.GetInt(AndroidDoneKey, 0) == 1) return;
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var manager = activity.Call<AndroidJavaObject>("getSystemService", "notification"))
                {
                    manager.Call("cancelAll");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        if (version.GetStatic<int>("SDK_INT") >= 26) manager.Call("deleteNotificationChannel", "ow_farm");
                }
                PlayerPrefs.SetInt(AndroidDoneKey, 1);
                PlayerPrefs.Save();
#endif
            }
            catch (Exception e) { Debug.LogWarning("[OldNotes] " + e.Message); }
        }
    }
}
