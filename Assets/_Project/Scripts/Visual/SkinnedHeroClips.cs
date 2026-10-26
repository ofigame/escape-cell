using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The made clips over the coded pose: in a duel, standing, the fighting stance (hero_idle_fight, looping) in place of
    /// the coded idle, eased out as the body walks; a reaction's clip (hero_hit_small, hero_stagger, the get-ups) eased in
    /// and out over a tenth of a second instead of snapping. The coded pose is taken first, the clips laid over it bone by
    /// bone. No allocation per frame.
    /// </summary>
    public partial class SkinnedHero
    {
        private const float ClipFade = 0.1f;
        private const string IdleClip = "idle_fight";

        private Transform[] bones;
        private Quaternion[] poseRot;
        private Vector3[] posePos;
        private float idleTime, idleWeight, clipWeight, fadeOutLeft;
        private AnimationClip fadingClip;
        private float fadingTime;
        private bool idleLoaded;
        private AnimationClip idleClip;
        /// <summary>The clips wrote the bones this frame (the hips then hold their place: no reset to rest).</summary>
        private bool clipsWrote;

        /// <summary>The body's bones (the skeleton's, once), for laying the clips over the coded pose.</summary>
        private void KnowBones()
        {
            if (bones != null || hips == null) return;
            var list = new List<Transform>();
            foreach (var t in hips.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("mixamorig:")) list.Add(t);
            bones = list.ToArray();
            poseRot = new Quaternion[bones.Length];
            posePos = new Vector3[bones.Length];
        }

        /// <summary>
        /// After the coded pose (or instead of it, while a move plays): the stance clip and a reaction's clip laid over the
        /// pose by their weights. Returns true when the clips wrote the bones (a full move or a blend).
        /// </summary>
        private bool LayClips(float dt, bool walking)
        {
            clipsWrote = false;
            if (!idleLoaded) { idleLoaded = true; idleClip = ClipOf(IdleClip); }
            KnowBones();
            if (bones == null) return false;
            // the stance: a duel, standing still, nothing else moving the body
            bool stance = idleClip != null && HeldWeapon.DuelStance && !walking && upperPlaying == null && knock == null && !Flailing && moveNow == BodyMove.None && strikeNow == Combat.BodyStrike.Weapon;
            idleWeight = Mathf.MoveTowards(idleWeight, stance ? 1f : 0f, dt / 0.15f);
            clipWeight = Mathf.MoveTowards(clipWeight, playing != null ? 1f : 0f, dt / ClipFade);
            if (playing == null && fadingClip != null) fadeOutLeft -= dt;
            if (fadeOutLeft <= 0f) fadingClip = null;
            float top = Mathf.Max(idleWeight, Mathf.Max(clipWeight, fadingClip != null ? fadeOutLeft / ClipFade : 0f));
            if (top <= 0.001f) return false;
            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            var l = Layers(animator);
            if (!l.Valid) return false;
            // the coded pose, kept
            for (int i = 0; i < bones.Length; i++) { poseRot[i] = bones[i].localRotation; posePos[i] = bones[i].localPosition; }
            // the stance on the base layer, the reaction (or the one fading out) on the full body layer
            if (idleClip != null)
            {
                if (!l.Playing(Combat.HeroAnimLayers.Base)) l.Play(Combat.HeroAnimLayers.Base, idleClip);
                idleTime = (idleTime + dt) % Mathf.Max(0.1f, idleClip.length);
                l.SetTime(Combat.HeroAnimLayers.Base, idleTime);
            }
            var full = playing != null ? playing : fadingClip;
            float fullW = playing != null ? clipWeight : fadingClip != null ? fadeOutLeft / ClipFade : 0f;
            if (full != null)
            {
                l.SetTime(Combat.HeroAnimLayers.FullBody, playing != null ? playingTime : fadingTime);
                l.Weight(Combat.HeroAnimLayers.FullBody, idleClip != null ? fullW : 1f);
            }
            else l.Stop(Combat.HeroAnimLayers.FullBody);
            l.Evaluate();
            // over the pose by the larger weight (the stance's or the reaction's)
            float w = Mathf.Clamp01(top);
            if (w < 0.999f)
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i].localRotation = Quaternion.Slerp(poseRot[i], bones[i].localRotation, w);
                    bones[i].localPosition = Vector3.Lerp(posePos[i], bones[i].localPosition, w);
                }
            clipsWrote = true;
            return true;
        }

        /// <summary>A reaction's clip ends: held on its last frame, eased back into the pose.</summary>
        private void ClipEnded(AnimationClip c, float at)
        {
            fadingClip = c;
            fadingTime = at;
            fadeOutLeft = ClipFade;
        }

        /// <summary>
        /// A body left lying or turned over with nothing to get it up (its knock cut short: the fighter hidden a moment, the
        /// round reset): up again at once. The picture never stays on the ground once the body is free.
        /// </summary>
        private void HealPose()
        {
            if (!bodyKnown || knock != null || stagger != null || hop != null || pop != null || moveLift != null) return;
            if (Quaternion.Angle(transform.localRotation, bodyRot) < 15f && flat < 0.05f && rise < 0.05f && kneelLean < 0.05f) return;
            flail = flat = rise = push = reel = 0f; kneelLean = 0f;
            transform.localRotation = bodyRot; transform.localPosition = bodyPos;
            InAir = false;
            ReactionClip = "";
        }
    }
}
