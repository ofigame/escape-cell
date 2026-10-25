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
        Click,
        // the duel's weapon families: swings, blows landing, two weapons meeting
        SwishSword,
        SwishSpear,
        SwishAxe,
        SwishHeavy,
        HitSword,
        HitSpear,
        HitAxe,
        HitHeavy,
        Clang
    }

    /// <summary>Plays the synthesized effects through a small voice pool, plus the layered music (<see cref="MusicDirector"/>).</summary>
    public class AudioManager : MonoBehaviour
    {
        private const int Voices = 10;

        public static AudioManager Instance { get; private set; }

        private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        /// <summary>A sound's variants (one picked each time it plays), where it has some.</summary>
        private readonly Dictionary<Sfx, AudioClip[]> variants = new Dictionary<Sfx, AudioClip[]>();
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
            // the duel's weapon families: their swings and their blows landing, and two weapons meeting
            clips[Sfx.SwishSword] = SoundSynth.SwishSword();
            clips[Sfx.SwishSpear] = SoundSynth.SwishSpear();
            clips[Sfx.SwishAxe] = SoundSynth.SwishAxe();
            clips[Sfx.SwishHeavy] = SoundSynth.SwishHeavy();
            clips[Sfx.HitSword] = SoundSynth.HitSword();
            clips[Sfx.HitSpear] = SoundSynth.HitSpear();
            clips[Sfx.HitAxe] = SoundSynth.HitAxe();
            clips[Sfx.HitHeavy] = SoundSynth.HitHeavy();
            clips[Sfx.Clang] = SoundSynth.Clang();
            // three of each of the commonest duel sounds (picked at random, the pitch varying too): no blow sounds twice the same
            variants[Sfx.SwishSword] = new[] { clips[Sfx.SwishSword], SoundSynth.SwishSword(1), SoundSynth.SwishSword(2) };
            variants[Sfx.HitSword] = new[] { clips[Sfx.HitSword], SoundSynth.HitSword(1), SoundSynth.HitSword(2) };
            variants[Sfx.HitAxe] = new[] { clips[Sfx.HitAxe], SoundSynth.HitAxe(1), SoundSynth.HitAxe(2) };
            variants[Sfx.HitHeavy] = new[] { clips[Sfx.HitHeavy], SoundSynth.HitHeavy(1), SoundSynth.HitHeavy(2) };

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
            if (variants.TryGetValue(sfx, out var vs) && vs.Length > 0) clip = vs[Random.Range(0, vs.Length)]; // (one of its variants)
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

        /// <summary>The music dips for a moment (a knock-down: the crowd to the front).</summary>
        public static void DipMusic(float seconds)
        {
            if (Instance != null) Instance.music.Dip(seconds);
        }

        /// <summary>How much of a fight is on around foi, 0-1: the hard-rock layer rises and falls with it.</summary>
        public static void SetDanger(float danger)
        {
            if (Instance != null) Instance.music.SetDanger(danger);
        }

        public static void PlaySfx(Sfx sfx, float volume = 1f, float pitch = 1f, float pitchJitter = 0f)
        {
            if (Instance != null) Instance.Play(sfx, volume, pitch, pitchJitter);
        }
    }
}
