using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Audio
{
    /// <summary>
    /// Short, precise vibrations on Android (VibrationEffect with amplitude, API 26+).
    /// Falls back to the stock Handheld.Vibrate (which also makes Unity add the VIBRATE permission).
    /// </summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static bool initialized;
        private static bool hasAmplitude;

        private static void Init()
        {
            initialized = true;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                hasAmplitude = vibrator != null && vibrator.Call<bool>("hasAmplitudeControl");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SquashBot] Vibrator unavailable: " + e.Message);
                vibrator = null;
            }
        }
#endif

        /// <param name="milliseconds">Length of the pulse.</param>
        /// <param name="strength">0..1 amplitude (ignored on devices without amplitude control).</param>
        public static void Pulse(int milliseconds, float strength)
        {
            if (!SaveData.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!initialized) Init();
            if (vibrator == null)
            {
                Handheld.Vibrate();
                return;
            }
            try
            {
                int amplitude = hasAmplitude ? Mathf.Clamp(Mathf.RoundToInt(strength * 255f), 1, 255) : -1; // -1 = device default
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, amplitude))
                {
                    vibrator.Call("vibrate", effect);
                }
            }
            catch (System.Exception)
            {
                Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (strength > 0.6f) Handheld.Vibrate();
#endif
        }

        public static void Light() => Pulse(12, 0.25f);
        public static void Medium() => Pulse(25, 0.55f);
        public static void Heavy() => Pulse(60, 1f);
    }
}
