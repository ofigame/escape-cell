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

    /// <summary>Plays the synthesized effects through a small voice pool, plus the looping music track.</summary>
    public class AudioManager : MonoBehaviour
    {
        private const int Voices = 10;

        public static AudioManager Instance { get; private set; }

        private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private AudioSource music;
        private int next;

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

            music = gameObject.AddComponent<AudioSource>();
            music.clip = SoundSynth.MusicLoop();
            music.loop = true;
            music.volume = 0.45f;
            music.playOnAwake = false;
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
            if (SaveData.Music && !music.isPlaying) music.Play();
            else if (!SaveData.Music && music.isPlaying) music.Stop();
        }

        /// <summary>Slows the music down with the game during slow motion (but not while paused).</summary>
        private void Update()
        {
            float target = Time.timeScale <= 0f ? 1f : Mathf.Lerp(0.7f, 1f, Time.timeScale);
            music.pitch = Mathf.MoveTowards(music.pitch, target, Time.unscaledDeltaTime * 2f);
        }

        public static void PlaySfx(Sfx sfx, float volume = 1f, float pitch = 1f, float pitchJitter = 0f)
        {
            if (Instance != null) Instance.Play(sfx, volume, pitch, pitchJitter);
        }
    }
}
