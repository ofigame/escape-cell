using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Audio
{
    /// <summary>
    /// A region's quiet sound under the music (the fixes design's job 7): made here, no files — soft water with a
    /// bubble now and then, wind rising and falling, a crystal hum, the hush of space. Off with the game's sound.
    /// </summary>
    public class AmbientLoop : MonoBehaviour
    {
        private const int Rate = 22050;
        private const float Seconds = 6f, Volume = 0.16f;

        private static AmbientLoop instance;
        private AudioSource source;
        private AmbientKind playing;

        /// <summary>Plays a region's sound (None: silence).</summary>
        public static void Play(AmbientKind kind)
        {
            if (instance == null)
            {
                if (kind == AmbientKind.None) return;
                var go = new GameObject("AmbientLoop");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AmbientLoop>();
                instance.source = go.AddComponent<AudioSource>();
                instance.source.loop = true;
                instance.source.playOnAwake = false;
                instance.source.spatialBlend = 0f;
            }
            if (instance.playing == kind) return;
            instance.playing = kind;
            if (kind == AmbientKind.None) { instance.source.Stop(); return; }
            instance.source.clip = Make(kind);
            instance.source.volume = SaveData.Sound ? Volume : 0f;
            instance.source.Play();
        }

        private void Update()
        {
            if (source != null) source.volume = SaveData.Sound && playing != AmbientKind.None ? Volume : 0f;
        }

        private static AudioClip Make(AmbientKind kind)
        {
            int n = (int)(Rate * Seconds);
            var data = new float[n];
            var rng = new System.Random((int)kind * 977);
            float low = 0f, low2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                // the loop's ends meet: a slow swell that is the same at 0 and at the end
                float swell = 0.5f + 0.5f * Mathf.Sin(t / Seconds * Mathf.PI * 2f);
                float v;
                switch (kind)
                {
                    case AmbientKind.Water:
                        low += (noise - low) * 0.04f;
                        v = low * 0.8f + (i % 9000 < 600 ? Mathf.Sin(t * Mathf.Lerp(900f, 400f, (i % 9000) / 600f)) * 0.25f * (1f - (i % 9000) / 600f) : 0f);
                        break;
                    case AmbientKind.Wind:
                        low += (noise - low) * (0.01f + 0.03f * swell);
                        v = low * (0.6f + 0.6f * swell);
                        break;
                    case AmbientKind.Hum:
                        low2 += (noise - low2) * 0.02f;
                        v = (Mathf.Sin(t * 2f * Mathf.PI * 110f) * 0.25f + Mathf.Sin(t * 2f * Mathf.PI * 165f) * 0.15f) * (0.7f + 0.3f * swell) + low2 * 0.2f;
                        break;
                    default:
                        low += (noise - low) * 0.006f;
                        v = low * 1.2f + Mathf.Sin(t * 2f * Mathf.PI * 55f) * 0.08f * swell;
                        break;
                }
                data[i] = Mathf.Clamp(v, -1f, 1f) * 0.7f;
            }
            // a short fade at both ends so the loop never clicks
            int fade = Rate / 20;
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] *= k; data[n - 1 - i] *= k; }
            var clip = AudioClip.Create("ambient_" + kind, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
