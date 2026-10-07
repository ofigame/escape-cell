using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SquashBot.Audio
{
    /// <summary>
    /// Plays the right music and follows the game's tension. Each theme's three layers (<see cref="MusicSynth"/>) start
    /// together on the audio clock and loop in sync; the calm layer always plays, the groove layer fades in as the
    /// tension rises and the intense layer on top of it. Changing theme crossfades over a second. Themes are written on
    /// a worker thread the first time they are needed (the old one keeps playing meanwhile) and a few are kept.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        private const float Volume = 0.5f;
        private const float FadeSeconds = 1.2f;
        private const int KeepThemes = 3;

        private class Deck
        {
            public MusicTheme theme;
            public AudioSource[] sources;
            public float fade; // 0..1 deck volume
            public bool live;
        }

        private readonly Deck[] decks = new Deck[2];
        private int current;
        private readonly Dictionary<MusicTheme, AudioClip[]> clips = new Dictionary<MusicTheme, AudioClip[]>();
        private readonly List<MusicTheme> recent = new List<MusicTheme>();
        private readonly Dictionary<MusicTheme, Task<float[][]>> rendering = new Dictionary<MusicTheme, Task<float[][]>>();
        private MusicTheme wanted = MusicTheme.Menu;
        private bool hasWanted;
        private float tension, tensionTarget;
        private bool on = true;

        public static MusicDirector Create(GameObject host)
        {
            var d = host.AddComponent<MusicDirector>();
            for (int k = 0; k < 2; k++)
            {
                var deck = new Deck { sources = new AudioSource[MusicSynth.Layers] };
                for (int i = 0; i < MusicSynth.Layers; i++)
                {
                    var s = host.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.loop = true;
                    s.spatialBlend = 0f;
                    s.volume = 0f;
                    deck.sources[i] = s;
                }
                d.decks[k] = deck;
            }
            return d;
        }

        /// <summary>The theme to play (switches with a crossfade once it is ready).</summary>
        public void Play(MusicTheme theme)
        {
            wanted = theme;
            hasWanted = true;
            Request(theme);
        }

        /// <summary>0 = calm, 1 = all-out. Smoothed, so it can be set every frame.</summary>
        public void SetTension(float value) => tensionTarget = Mathf.Clamp01(value);

        public void SetEnabled(bool enabled)
        {
            on = enabled;
            if (!enabled)
                foreach (var d in decks)
                {
                    foreach (var s in d.sources) s.Stop();
                    d.live = false;
                    d.fade = 0f;
                }
        }

        /// <summary>Slow motion bends the music too.</summary>
        public void SetPitch(float pitch)
        {
            foreach (var d in decks)
                foreach (var s in d.sources) s.pitch = pitch;
        }

        private void Request(MusicTheme theme)
        {
            if (clips.ContainsKey(theme) || rendering.ContainsKey(theme)) return;
            rendering[theme] = Task.Run(() => MusicSynth.Render(theme));
        }

        private void Update()
        {
            // Finished renders become clips (AudioClip must be made on the main thread).
            if (rendering.Count > 0)
            {
                MusicTheme? done = null;
                foreach (var kv in rendering)
                    if (kv.Value.IsCompleted) { done = kv.Key; break; }
                if (done.HasValue)
                {
                    var task = rendering[done.Value];
                    rendering.Remove(done.Value);
                    if (task.Status == TaskStatus.RanToCompletion) Store(done.Value, task.Result);
                    else Debug.LogWarning("Music render failed: " + task.Exception);
                }
            }

            float dt = Time.unscaledDeltaTime;
            tension = Mathf.MoveTowards(tension, tensionTarget, dt * (tensionTarget > tension ? 0.9f : 0.35f));

            var cur = decks[current];
            if (on && hasWanted && (!cur.live || cur.theme != wanted) && clips.TryGetValue(wanted, out var set)) Switch(set);

            // Layer mix: the groove comes in from a little tension, the intense layer near the top.
            float[] layer = { 1f, Smooth(0.15f, 0.45f, tension), Smooth(0.55f, 0.85f, tension) };
            for (int k = 0; k < 2; k++)
            {
                var d = decks[k];
                if (!d.live) continue;
                d.fade = Mathf.MoveTowards(d.fade, k == current ? 1f : 0f, dt / FadeSeconds);
                for (int i = 0; i < d.sources.Length; i++) d.sources[i].volume = Volume * d.fade * layer[i] * (i == 0 ? 0.85f : 1f);
                if (k != current && d.fade <= 0f)
                {
                    foreach (var s in d.sources) s.Stop();
                    d.live = false;
                }
            }
        }

        private void Switch(AudioClip[] set)
        {
            current = 1 - current;
            var d = decks[current];
            double at = AudioSettings.dspTime + 0.08;
            for (int i = 0; i < d.sources.Length; i++)
            {
                d.sources[i].Stop();
                d.sources[i].clip = set[i];
                d.sources[i].volume = 0f;
                d.sources[i].PlayScheduled(at);
            }
            d.theme = wanted;
            d.live = true;
            d.fade = decks[1 - current].live ? 0f : 0.6f; // the very first theme comes in quicker
        }

        private void Store(MusicTheme theme, float[][] layers)
        {
            var set = new AudioClip[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                set[i] = AudioClip.Create("music_" + theme + "_" + i, layers[i].Length, 1, MusicSynth.Rate, false);
                set[i].SetData(layers[i], 0);
            }
            clips[theme] = set;
            recent.Remove(theme);
            recent.Add(theme);
            // Keep a few themes; drop the oldest one that isn't playing.
            while (recent.Count > KeepThemes)
            {
                var old = recent[0];
                recent.RemoveAt(0);
                if (old == wanted || (decks[0].live && decks[0].theme == old) || (decks[1].live && decks[1].theme == old))
                {
                    recent.Add(old);
                    if (recent.Count <= KeepThemes + 1) break;
                    continue;
                }
                foreach (var c in clips[old]) Destroy(c);
                clips.Remove(old);
            }
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
