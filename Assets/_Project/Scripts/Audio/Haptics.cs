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
        private static AndroidJavaObject vibrator, gameAttributes;
        private static bool initialized;
        private static bool hasAmplitude, hasVibrator = true;
        private static int sdk;

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
                hasVibrator = vibrator != null && vibrator.Call<bool>("hasVibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                hasAmplitude = hasVibrator && sdk >= 26 && vibrator.Call<bool>("hasAmplitudeControl");
                // Tagged as game feedback (USAGE_GAME), so system settings that mute "other" vibrations leave it alone.
                if (sdk >= 26)
                    using (var builder = new AndroidJavaObject("android.media.AudioAttributes$Builder"))
                    {
                        builder.Call<AndroidJavaObject>("setUsage", 14).Dispose();
                        gameAttributes = builder.Call<AndroidJavaObject>("build");
                    }
                if (!hasVibrator) Debug.Log("[EscapeCell] This device has no vibration motor.");
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
            if (vibrator == null) { Handheld.Vibrate(); return; }
            if (!hasVibrator) return;
            if (sdk < 26) { vibrator.Call("vibrate", (long)milliseconds); return; }
            try
            {
                int amplitude = hasAmplitude ? Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(110f, 255f, strength)), 1, 255) : -1; // -1 = device default
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, amplitude))
                {
                    Vibrate(effect);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[EscapeCell] Vibration failed: " + e.Message);
                vibrator.Call("vibrate", (long)milliseconds);
            }
        }

        private static void Vibrate(AndroidJavaObject effect)
        {
            if (gameAttributes != null) vibrator.Call("vibrate", effect, gameAttributes);
            else vibrator.Call("vibrate", effect);
        }

        private static void AndroidPattern(long[] timings, int[] amplitudes)
        {
            if (!initialized) Init();
            if (vibrator == null) { Handheld.Vibrate(); return; }
            if (!hasVibrator) return;
            if (sdk < 26) { vibrator.Call("vibrate", timings, -1); return; }
            try
            {
                if (!hasAmplitude)
                    for (int i = 0; i < amplitudes.Length; i++) amplitudes[i] = amplitudes[i] > 0 ? 255 : 0;
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1))
                {
                    Vibrate(effect);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[EscapeCell] Vibration failed: " + e.Message);
                vibrator.Call("vibrate", timings, -1);
            }
        }
#endif

        /// <summary>False on devices without a vibration motor (most tablets): the settings say so.</summary>
        public static bool Available
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!initialized) Init();
                return hasVibrator;
#else
                return true;
#endif
            }
        }

        /// <param name="milliseconds">Length of the pulse (Android).</param>
        /// <param name="strength">0..1: on Android the amplitude, on iPhone it picks the impact style and intensity.</param>
        public static void Pulse(int milliseconds, float strength)
        {
            if (!SaveData.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidOneShot(Mathf.Max(25, milliseconds), strength);
#elif UNITY_IOS && !UNITY_EDITOR
            int style = strength < 0.4f ? 0 : strength < 0.75f ? 1 : 2;
            EscapeCell_Impact(style, Mathf.Clamp01(0.5f + strength * 0.5f));
#endif
        }

        /// <summary>A soft tick: every safe step.</summary>
        // Pulses shorter than ~25 ms barely spin up the small motors of many phones, so even the soft tick lasts 25 ms.
        public static void Light() => Pulse(25, 0.3f);

        /// <summary>A firm knock: pickups, jumps, blocks landing nearby.</summary>
        public static void Medium() => Pulse(45, 0.6f);

        /// <summary>A heavy hit.</summary>
        public static void Heavy() => Pulse(80, 1f);

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
