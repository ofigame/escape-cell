using SquashBot.Data;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SquashBot.Audio
{
    /// <summary>
    /// Feel-the-game feedback. iPhone: the Taptic Engine through a small native plugin (Plugins/iOS/EscapeCellHaptics.mm).
    /// Android: short VibrationEffect pulses with amplitude (API 26+), falling back to Handheld.Vibrate
    /// (whose presence also makes Unity add the VIBRATE permission).
    /// </summary>
    public static class Haptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void EscapeCell_Impact(int style, float intensity);
        [DllImport("__Internal")] private static extern void EscapeCell_Notify(int type);
#endif

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
                Debug.LogWarning("[EscapeCell] Vibrator unavailable: " + e.Message);
                vibrator = null;
            }
        }

        private static void AndroidOneShot(int milliseconds, float strength)
        {
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
        }

        private static void AndroidPattern(long[] timings, int[] amplitudes)
        {
            if (!initialized) Init();
            if (vibrator == null)
            {
                Handheld.Vibrate();
                return;
            }
            try
            {
                if (!hasAmplitude)
                    for (int i = 0; i < amplitudes.Length; i++) amplitudes[i] = amplitudes[i] > 0 ? 255 : 0;
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1))
                {
                    vibrator.Call("vibrate", effect);
                }
            }
            catch (System.Exception)
            {
                Handheld.Vibrate();
            }
        }
#endif

        /// <param name="milliseconds">Length of the pulse (Android).</param>
        /// <param name="strength">0..1: on Android the amplitude, on iPhone it picks the impact style and intensity.</param>
        public static void Pulse(int milliseconds, float strength)
        {
            if (!SaveData.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidOneShot(milliseconds, strength);
#elif UNITY_IOS && !UNITY_EDITOR
            int style = strength < 0.4f ? 0 : strength < 0.75f ? 1 : 2;
            EscapeCell_Impact(style, Mathf.Clamp01(0.5f + strength * 0.5f));
#endif
        }

        /// <summary>A soft tick: every safe step.</summary>
        public static void Light() => Pulse(14, 0.3f);

        /// <summary>A firm knock: pickups, jumps, blocks landing nearby.</summary>
        public static void Medium() => Pulse(28, 0.6f);

        /// <summary>A heavy hit.</summary>
        public static void Heavy() => Pulse(60, 1f);

        /// <summary>Squashed, burned or fallen: a strong double jolt you can't miss.</summary>
        public static void Death()
        {
            if (!SaveData.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidPattern(new long[] { 0, 110, 70, 180 }, new[] { 0, 255, 0, 220 });
#elif UNITY_IOS && !UNITY_EDITOR
            EscapeCell_Impact(2, 1f);
            EscapeCell_Notify(2);
#endif
        }
    }
}
