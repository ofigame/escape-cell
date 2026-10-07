using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Audio
{
    public enum Sfx
    {
        Hop,
        Bump,
        Coin,
        Warning,
        Impact,
        Squash,
        Fall,
        Shield,
        Blocked,
        CloseCall,
        Win,
        Lose,
        Click
    }

    /// <summary>Plays the synthesized effects through a small voice pool, plus the layered music (<see cref="MusicDirector"/>).</summary>
    public class AudioManager : MonoBehaviour
    {
        private const int Voices = 10;

        public static AudioManager Instance { get; private set; }

        private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private MusicDirector music;
        private int next;
        private float pitch = 1f;

        public static AudioManager Create()
        {
            var manager = new GameObject("Audio").AddComponent<AudioManager>();
            Instance = manager;
            manager.Build();
            return manager;
        }

        private void Build()
        {
            clips[Sfx.Hop] = SoundSynth.Hop();
            clips[Sfx.Bump] = SoundSynth.Bump();
            clips[Sfx.Coin] = SoundSynth.Coin();
            clips[Sfx.Warning] = SoundSynth.Warning();
            clips[Sfx.Impact] = SoundSynth.Impact();
            clips[Sfx.Squash] = SoundSynth.Squash();
            clips[Sfx.Fall] = SoundSynth.Fall();
            clips[Sfx.Shield] = SoundSynth.Shield();
            clips[Sfx.Blocked] = SoundSynth.Blocked();
            clips[Sfx.CloseCall] = SoundSynth.CloseCall();
            clips[Sfx.Win] = SoundSynth.Win();
            clips[Sfx.Lose] = SoundSynth.Lose();
            clips[Sfx.Click] = SoundSynth.Click();

            for (int i = 0; i < Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                pool.Add(source);
            }

            music = MusicDirector.Create(gameObject);
            music.Play(MusicTheme.Menu);
            RefreshMusic();
        }

        /// <param name="pitchJitter">Random pitch spread so repeated sounds (hops, coins) don't feel robotic.</param>
        public void Play(Sfx sfx, float volume = 1f, float pitch = 1f, float pitchJitter = 0f)
        {
            if (!SaveData.Sound || !clips.TryGetValue(sfx, out var clip)) return;
            var source = pool[next];
            next = (next + 1) % pool.Count;
            source.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clip, volume);
        }

        public void RefreshMusic()
        {
            music.SetEnabled(SaveData.Music);
        }

        /// <summary>Slows the music down with the game during slow motion (but not while paused).</summary>
        private void Update()
        {
            float target = Time.timeScale <= 0f ? 1f : Mathf.Lerp(0.7f, 1f, Time.timeScale);
            pitch = Mathf.MoveTowards(pitch, target, Time.unscaledDeltaTime * 2f);
            music.SetPitch(pitch);
        }

        /// <summary>The music for what is on screen now (menu, a world, tunnel, monster, WARDEN).</summary>
        public static void PlayMusic(MusicTheme theme)
        {
            if (Instance != null) Instance.music.Play(theme);
        }

        /// <summary>How tense the moment is, 0-1: the music adds drums and then its lead as it rises.</summary>
        public static void SetTension(float tension)
        {
            if (Instance != null) Instance.music.SetTension(tension);
        }

        public static void PlaySfx(Sfx sfx, float volume = 1f, float pitch = 1f, float pitchJitter = 0f)
        {
            if (Instance != null) Instance.Play(sfx, volume, pitch, pitchJitter);
        }
    }
}
