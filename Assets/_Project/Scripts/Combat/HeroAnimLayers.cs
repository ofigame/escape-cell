using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SquashBot.Combat
{
    /// <summary>
    /// A rigged foi's animation layers, on a graph of its own evaluated by hand (the body has no animator controller; its
    /// rig is the Mixamo skeleton, Generic): Base (the body's own coded pose), UpperBody (a clip on the spine and up only,
    /// so foi can strike while its legs walk), FullBody (dances, the ultimates' moves, falls) and HitReaction (additive: a
    /// reaction laid over whatever the body is doing).
    /// </summary>
    public class HeroAnimLayers
    {
        public const int Base = 0, UpperBody = 1, FullBody = 2, HitReaction = 3;
        private const int Count = 4;

        private PlayableGraph graph;
        private AnimationLayerMixerPlayable mixer;
        private readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[Count];
        private readonly List<Transform> lower = new List<Transform>();
        private readonly List<Quaternion> lowerRot = new List<Quaternion>();
        private readonly List<Vector3> lowerPos = new List<Vector3>();

        public bool Valid => graph.IsValid();

        /// <summary>The graph on <paramref name="animator"/> (on the body's root, <paramref name="root"/>); the upper layer
        /// masked to <paramref name="spine"/> and everything under it.</summary>
        public HeroAnimLayers(Animator animator, Transform root, Transform spine, string name)
        {
            graph = PlayableGraph.Create(name + " layers");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "out", animator);
            mixer = AnimationLayerMixerPlayable.Create(graph, Count);
            output.SetSourcePlayable(mixer);
            for (int i = 0; i < Count; i++) mixer.SetInputWeight(i, 0f);
            mixer.SetLayerAdditive(HitReaction, true);
            if (spine != null)
            {
                // the mask: every bone of the body by its path from the root, on only from the spine up
                var all = root.GetComponentsInChildren<Transform>(true);
                var mask = new AvatarMask { transformCount = all.Length };
                for (int i = 0; i < all.Length; i++)
                {
                    bool on = all[i] == spine || all[i].IsChildOf(spine);
                    mask.SetTransformPath(i, PathOf(all[i], root));
                    mask.SetTransformActive(i, on);
                    if (!on && all[i] != root) lower.Add(all[i]);
                }
                mixer.SetLayerMaskFromAvatarMask(UpperBody, mask);
            }
        }

        private static string PathOf(Transform t, Transform root)
        {
            if (t == root) return "";
            var path = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>Plays <paramref name="clip"/> on a layer (from its start), the layer on.</summary>
        public void Play(int layer, AnimationClip clip)
        {
            if (!Valid || clip == null) return;
            if (clips[layer].IsValid())
            {
                graph.Disconnect(mixer, layer);
                clips[layer].Destroy();
            }
            clips[layer] = AnimationClipPlayable.Create(graph, clip);
            graph.Connect(clips[layer], 0, mixer, layer);
            mixer.SetInputWeight(layer, 1f);
        }

        /// <summary>A layer off (its clip stays connected, weightless).</summary>
        public void Stop(int layer)
        {
            if (Valid) mixer.SetInputWeight(layer, 0f);
        }

        public bool Playing(int layer) => Valid && clips[layer].IsValid() && mixer.GetInputWeight(layer) > 0f;

        public void SetTime(int layer, float seconds)
        {
            if (Valid && clips[layer].IsValid()) clips[layer].SetTime(seconds);
        }

        /// <summary>The layers written onto the body now.</summary>
        public void Evaluate()
        {
            if (Valid) graph.Evaluate(0f);
        }

        /// <summary>
        /// The upper layer over the body's own coded pose: the legs and hips as the code posed them (kept round the
        /// graph's evaluation, whatever it writes there), the spine and up from the clip.
        /// </summary>
        public void EvaluateUpperOverPose()
        {
            if (!Valid) return;
            lowerRot.Clear(); lowerPos.Clear();
            foreach (var t in lower) { lowerRot.Add(t.localRotation); lowerPos.Add(t.localPosition); }
            graph.Evaluate(0f);
            for (int i = 0; i < lower.Count; i++) { lower[i].localRotation = lowerRot[i]; lower[i].localPosition = lowerPos[i]; }
        }

        public void Destroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
