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
        private AnimationClip idleClip, crouchClip, blockClip, walkClip, stanceClip;
        private bool stanceSwapped, walkBack;

        /// <summary>Behind its shield now (the duel sets it each frame): the block's stance.</summary>
        public bool Guarding { get; set; }
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
            if (!idleLoaded)
            {
                idleLoaded = true;
                idleClip = ClipOf(IdleClip);
                crouchClip = ClipOf("crouch_idle"); blockClip = ClipOf("block"); walkClip = ClipOf("walk_fight");
                stanceClip = idleClip;
            }
            KnowBones();
            if (bones == null) return false;
            // the stance: a duel, nothing else moving the body; crouched, behind the shield, walking or standing still
            var want = crouchHeld && crouchClip != null ? crouchClip : Guarding && blockClip != null ? blockClip : walking ? walkClip : idleClip;
            bool stance = want != null && HeldWeapon.DuelStance && upperPlaying == null && knock == null && !Flailing && moveNow == BodyMove.None && strikeNow == Combat.BodyStrike.Weapon;
            // another stance wanted: the one now eased out quickly, the new one in (each from its start)
            bool swap = want != stanceClip;
            idleWeight = Mathf.MoveTowards(idleWeight, stance && !swap ? 1f : 0f, dt / (swap ? 0.07f : 0.15f));
            if (swap && idleWeight <= 0.001f) { stanceClip = want; idleTime = 0f; stanceSwapped = true; }
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
            if (stanceClip != null)
            {
                if (stanceSwapped || !l.Playing(Combat.HeroAnimLayers.Base)) { l.Play(Combat.HeroAnimLayers.Base, stanceClip); stanceSwapped = false; }
                // the walk's steps by the body's pace; the block held raised (its last frame); the others round and round
                float rate = stanceClip == walkClip ? Mathf.Clamp(speed / 1.4f, 0.6f, 1.8f) * (walkBack ? -1f : 1f) : 1f;
                idleTime = stanceClip == blockClip ? Mathf.Min(idleTime + dt, stanceClip.length) : Mathf.Repeat(idleTime + dt * rate, Mathf.Max(0.1f, stanceClip.length));
                l.SetTime(Combat.HeroAnimLayers.Base, idleTime);
            }
            var full = playing != null ? playing : fadingClip;
            float fullW = playing != null ? clipWeight : fadingClip != null ? fadeOutLeft / ClipFade : 0f;
            if (full != null)
            {
                l.SetTime(Combat.HeroAnimLayers.FullBody, playing != null ? playingTime : fadingTime);
                l.Weight(Combat.HeroAnimLayers.FullBody, stanceClip != null ? fullW : 1f);
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

        /// <summary>A quick step back out of a blow's way (the dodge's clip, about <paramref name="seconds"/> long); false if
        /// there is none.</summary>
        public bool DodgeClip(float seconds = 0.6f) => ClipOf("dodge") != null && PlayClip("dodge", Mathf.Clamp(MoveLength("dodge") / Mathf.Max(0.2f, seconds), 1f, 3.5f), false, true);

        /// <summary>The duel lost: down on its knees, beaten (the clip; false if there is none).</summary>
        public bool Defeated() => ClipOf("defeated") != null && PlayClip("defeated", 1f, false, true);

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
