using System;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The challenge's phone notifications ("your profile is being fought right now", the result): on Android, asked
    /// for once on the first challenge (Android 13+ asks the player), then Firebase gives the phone's token, which the
    /// server keeps for the player. Off until the Firebase project's settings are filled in below; never on Huawei.
    /// </summary>
    public static class Notifications
    {
        // The Firebase project's Android app (from its google-services.json; not secret: they ship in every app).
        private const string AppId = "", ApiKey = "", ProjectId = "", SenderId = "";

        private const string TokenKey = "sb_push_token", AskedKey = "sb_push_asked";

        public static bool Configured => AppId.Length > 0 && ApiKey.Length > 0 && ProjectId.Length > 0 && SenderId.Length > 0;

        /// <summary>The phone's notification token (null until Firebase has given one).</summary>
        public static string Token
        {
            get { var t = PlayerPrefs.GetString(TokenKey, ""); return t.Length > 0 ? t : null; }
            private set { PlayerPrefs.SetString(TokenKey, value ?? ""); PlayerPrefs.Save(); }
        }

        private static Action<string> onToken;

        /// <summary>Starts notifications (asking the player the first time); <paramref name="tokenReady"/> gets a new token.</summary>
        public static void AskOnce(Action<string> tokenReady)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !HUAWEI
            if (!Configured) return;
            onToken = tokenReady;
            const string Permission = "android.permission.POST_NOTIFICATIONS";
            if (PlayerPrefs.GetInt(AskedKey, 0) == 0 && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(Permission))
            {
                PlayerPrefs.SetInt(AskedKey, 1);
                PlayerPrefs.Save();
                UnityEngine.Android.Permission.RequestUserPermission(Permission);
            }
            Start();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR && !HUAWEI
        private static void Start()
        {
            try
            {
                Receiver.Ensure();
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var push = new AndroidJavaClass("com.ofigame.foi.FoiPush");
                push.CallStatic("init", activity, AppId, ApiKey, ProjectId, SenderId, Receiver.Name, nameof(Receiver.OnPushToken));
            }
            catch (Exception e) { Debug.LogWarning("[Push] " + e.Message); }
        }
#endif

        /// <summary>Takes the token from Java (UnitySendMessage needs a GameObject).</summary>
        private class Receiver : MonoBehaviour
        {
            public const string Name = "FoiPushReceiver";
            private static Receiver instance;

            public static void Ensure()
            {
                if (instance != null) return;
                var go = new GameObject(Name);
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Receiver>();
            }

            public void OnPushToken(string token)
            {
                if (string.IsNullOrEmpty(token) || token == Token) return;
                Token = token;
                onToken?.Invoke(token);
            }
        }
    }
}
