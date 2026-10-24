using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// Sharing (the scenario's §10.4): a ready line — rank, score, the game's name and the player's friend code — through
    /// the phone's own share sheet (Android); elsewhere a WhatsApp link opens with it.
    /// </summary>
    public static class Share
    {
        /// <summary>The link a share carries: the invitation page with the friend code (it opens the game, or the store).</summary>
        public static string Link => MyCode.Length == 6 ? "https://ofigame.com/foi/davet.html?k=" + MyCode : "https://ofigame.com";

        /// <summary>The player's friend code as last heard from the server ("" until then).</summary>
        public static string MyCode
        {
            get => Prefs.GetString("sb_friend_code", "");
            set { Prefs.SetString("sb_friend_code", value ?? ""); Prefs.Save(); }
        }

        public static void Text(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "foi | cell");
                activity.Call("startActivity", chooser);
                return;
            }
            catch (System.Exception e) { Debug.LogWarning("[Share] " + e.Message); }
#endif
            Application.OpenURL("https://wa.me/?text=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(text));
        }
    }
}
