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
        /// <summary>The whole music down to this share (the challenge's arena: a murmur of a crowd over it).</summary>
        public static float Level = 1f;
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

        /// <summary>
        /// The game's songs, cut into seamless loops by mood (Resources/Music): everything outside play (menu, garage, map,
        /// shop) the calm opening and close of The Iron Conflict; from Tungsten Edge the floors its main part, the roads its
        /// opening, duels, monsters and bosses its climax. Where a theme has one, it
        /// plays instead of the synthesized layers (and the fight layer stays out).
        /// </summary>
        private static string SongOf(MusicTheme t) =>
            t == MusicTheme.Menu ? "menu" : t == MusicTheme.Tunnel ? "road" :
            t == MusicTheme.Duel || t == MusicTheme.Monster || t == MusicTheme.Boss ? "duel" : "level";

        private readonly Dictionary<string, AudioClip> songs = new Dictionary<string, AudioClip>();

        private AudioClip[] SongSet(MusicTheme theme)
        {
            string key = SongOf(theme);
            if (!songs.TryGetValue(key, out var clip)) songs[key] = clip = Resources.Load<AudioClip>(key == "menu" ? "Music/iron_menu" : "Music/tungsten_" + key);
            return clip == null ? null : new[] { clip, null, null };
        }

        private bool SongPlaying => decks[current].live && decks[current].sources[1].clip == null;

        // The fight layer: a hard-rock loop over the world's music, as loud as the danger (see SetDanger).
        private const float RockVolume = 0.3f;
        private AudioSource rock;
        private Task<float[]> rockRender;
        private float danger, dangerTarget;

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
            d.rock = host.AddComponent<AudioSource>();
            d.rock.playOnAwake = false;
            d.rock.loop = true;
            d.rock.spatialBlend = 0f;
            d.rock.volume = 0f;
            d.rockRender = Task.Run(() => MusicSynth.RenderRock());
            return d;
        }

        /// <summary>
        /// 0 = no fight .. 1 = surrounded: the rock layer comes in with it and the world's music steps back a little.
        /// Kept under the effects. Smoothed, so it can be set every frame.
        /// </summary>
        public void SetDanger(float value) => dangerTarget = Mathf.Clamp01(value);

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
            if (!enabled && rock != null) rock.Stop();
        }

        /// <summary>Slow motion bends the music too.</summary>
        public void SetPitch(float pitch)
        {
            foreach (var d in decks)
                foreach (var s in d.sources) s.pitch = pitch;
            if (rock != null) rock.pitch = pitch;
        }

        private void Request(MusicTheme theme)
        {
            if (clips.ContainsKey(theme) || rendering.ContainsKey(theme)) return;
            var song = SongSet(theme);
            if (song != null) { clips[theme] = song; return; }
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

            // The fight layer: made into a clip once rendered, started in time with nothing (it is its own loop).
            if (rockRender != null && rockRender.IsCompleted)
            {
                if (rockRender.Status == TaskStatus.RanToCompletion)
                {
                    var data = rockRender.Result;
                    rock.clip = AudioClip.Create("music_rock", data.Length, 1, MusicSynth.Rate, false);
                    rock.clip.SetData(data, 0);
                }
                rockRender = null;
            }
            danger = Mathf.MoveTowards(danger, dangerTarget, dt * (dangerTarget > danger ? 1.4f : 0.3f));
            float rockMix = SongPlaying ? 0f : Smooth(0.05f, 0.7f, danger); // a song carries the fight itself
            if (rock.clip != null)
            {
                if (on && rockMix > 0.01f && !rock.isPlaying) rock.Play();
                else if (rockMix <= 0.01f && rock.isPlaying && danger < 0.02f) rock.Stop();
                rock.volume = on ? RockVolume * rockMix * Level : 0f;
            }
            float duck = 1f - 0.45f * rockMix;

            // Layer mix: the groove comes in from a little tension, the intense layer near the top.
            float[] layer = { 1f, Smooth(0.15f, 0.45f, tension), Smooth(0.55f, 0.85f, tension) };
            const float MenuSongGain = 1.15f, PlaySongGain = 0.45f; // in play the song stays in the background, under the fight
            for (int k = 0; k < 2; k++)
            {
                var d = decks[k];
                if (!d.live) continue;
                d.fade = Mathf.MoveTowards(d.fade, k == current ? 1f : 0f, dt / FadeSeconds);
                bool song = d.sources[1].clip == null;
                for (int i = 0; i < d.sources.Length; i++) d.sources[i].volume = song ? (i == 0 ? Volume * (d.theme == MusicTheme.Menu ? MenuSongGain : PlaySongGain) * Level * d.fade : 0f) : Volume * Level * duck * d.fade * layer[i] * (i == 0 ? 0.85f : 1f);
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
                if (set[i] != null) d.sources[i].PlayScheduled(at);
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
                if (clips.TryGetValue(old, out var oc) && oc.Length > 1 && oc[1] == null) { recent.RemoveAt(0); continue; } // a song: kept (it belongs to Resources)
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
