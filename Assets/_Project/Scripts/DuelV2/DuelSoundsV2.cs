using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// Plays the prototype's fight sounds (<see cref="DuelSoundSynthV2"/>, made once): a variant picked at random (never
    /// the same one twice running), the pitch a little spread, and lower in slow motion (the moment heavier).
    /// </summary>
    public class DuelSoundsV2 : MonoBehaviour
    {
        private const int Voices = 16;
        private static DuelSoundsV2 instance;
        private static Dictionary<DuelSoundV2, AudioClip[]> clips;
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private readonly Dictionary<DuelSoundV2, int> last = new Dictionary<DuelSoundV2, int>();
        private int next;

        public static void Play(DuelSoundV2 kind, float volume = 1f, float jitter = 0.04f)
        {
            if (instance == null) instance = new GameObject("DuelSoundsV2").AddComponent<DuelSoundsV2>();
            instance.PlayOne(kind, volume, jitter);
        }

        /// <summary>Makes the sounds now (at the fight's setup), not at the first blow (it takes about a second).</summary>
        public static void Warm()
        {
            if (instance == null) instance = new GameObject("DuelSoundsV2").AddComponent<DuelSoundsV2>();
        }

        /// <summary>Every sound's variants, made the first time (about a second).</summary>
        private static void Build()
        {
            if (clips != null) return;
            clips = new Dictionary<DuelSoundV2, AudioClip[]>();
            foreach (DuelSoundV2 k in System.Enum.GetValues(typeof(DuelSoundV2)))
            {
                var vs = new AudioClip[DuelSoundSynthV2.Variants];
                for (int v = 0; v < vs.Length; v++) vs[v] = DuelSoundSynthV2.Make(k, v);
                clips[k] = vs;
            }
        }

        private void Awake()
        {
            Build();
            for (int i = 0; i < Voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f;
                pool.Add(s);
            }
        }

        private void PlayOne(DuelSoundV2 kind, float volume, float jitter)
        {
            if (!SaveData.Sound) return;
            var vs = clips[kind];
            int v = Random.Range(0, vs.Length);
            if (last.TryGetValue(kind, out int prev) && v == prev) v = (v + 1) % vs.Length;
            last[kind] = v;
            var s = pool[next]; next = (next + 1) % pool.Count;
            float slow = Time.timeScale <= 0f ? 1f : Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(Time.timeScale));
            s.pitch = slow * (1f + Random.Range(-jitter, jitter));
            s.PlayOneShot(vs[v], volume);
        }

        /// <summary>Writes every sound and variant as .wav files (the self-test: to listen to them outside the game).</summary>
        public static void WriteAll(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            foreach (DuelSoundV2 k in System.Enum.GetValues(typeof(DuelSoundV2)))
                for (int v = 0; v < DuelSoundSynthV2.Variants; v++)
                    WriteWav(System.IO.Path.Combine(dir, k + "_" + v + ".wav"), DuelSoundSynthV2.Samples(k, v));
        }

        private static void WriteWav(string path, float[] b)
        {
            using (var w = new System.IO.BinaryWriter(System.IO.File.Create(path)))
            {
                int rate = DuelSoundSynthV2.Rate, bytes = b.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (float x in b) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(x, -1f, 1f) * 32767f));
            }
        }
    }
}
