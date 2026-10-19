using System.Collections.Generic;
using UnityEngine;

namespace BrickMuseum.Core
{
    /// <summary>Short sound effects synthesised at startup (no audio files).</summary>
    public static class Sfx
    {
        private static AudioSource source;
        private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private const int Rate = 44100;

        public static void Init(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            clips["click"] = Make(0.09f, t => Tone(t, 2200, 0.02f) * 0.35f + Tone(t, 1100, 0.06f, 0.015f) * 0.6f);
            clips["stick"] = Make(0.14f, t => Tone(t, 900, 0.07f) * 0.5f + Tone(t, 1400, 0.06f, 0.05f) * 0.35f);
            clips["pick"] = Make(0.06f, t => Tone(t, 700, 0.05f) * 0.4f);
            clips["tick"] = Make(0.03f, t => Square(t, 1300, 0.025f) * 0.15f);
            clips["wrong"] = Make(0.22f, t => Saw(t, 150, 0.2f) * 0.3f);
            clips["win"] = Make(0.75f, t => Tone(t, 523, 0.22f) + Tone(t, 659, 0.22f, 0.11f) + Tone(t, 784, 0.22f, 0.22f) + Tone(t, 1047, 0.3f, 0.33f), 0.5f);
            clips["place"] = Make(0.22f, t => Tone(t, 500, 0.08f) * 0.5f + Tone(t, 750, 0.12f, 0.08f) * 0.45f);
            clips["fail"] = Make(0.6f, t => Tone(t, 392, 0.2f) * 0.5f + Tone(t, 330, 0.2f, 0.18f) * 0.5f + Tone(t, 262, 0.25f, 0.36f) * 0.5f);
            clips["rustle"] = MakeNoise(0.3f, 0.22f);
        }

        public static void Play(string name, float volume = 1f)
        {
            if (source != null && clips.TryGetValue(name, out var c)) source.PlayOneShot(c, volume);
        }

        private static AudioClip Make(float length, System.Func<float, float> f, float gain = 1f)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate) * gain, -1f, 1f);
            var clip = AudioClip.Create("sfx", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeNoise(float length, float gain)
        {
            var rnd = new System.Random(7);
            float prev = 0f;
            return Make(length, t =>
            {
                float white = (float)(rnd.NextDouble() * 2 - 1);
                float high = white - prev; prev = white; // crude high-pass: rustly
                return high * (1f - t / length) * gain;
            });
        }

        private static float Env(float t, float dur, float delay) => t < delay || t > delay + dur ? 0f : Mathf.Exp(-6f * (t - delay) / dur);
        private static float Tone(float t, float f, float dur, float delay = 0f) => Mathf.Sin(2 * Mathf.PI * f * (t - delay)) * Env(t, dur, delay);
        private static float Square(float t, float f, float dur) => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * t)) * Env(t, dur, 0f);
        private static float Saw(float t, float f, float dur) => (2f * (t * f - Mathf.Floor(t * f + 0.5f))) * Env(t, dur, 0f);

        public static void Haptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
