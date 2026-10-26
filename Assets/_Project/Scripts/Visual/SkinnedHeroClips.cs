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
        private bool stanceSwapped, walkBack, blockLoops;

        /// <summary>Behind its shield now (the duel sets it each frame): the block's stance.</summary>
        public bool Guarding { get => guarding && Time.time - guardAt < 0.3f; set { guarding = value; if (value) guardAt = Time.time; } } // (told each frame: not told, not guarding)
        private bool guarding;
        private float guardAt = -9f;

        /// <summary>
        /// The body back to standing at rest, every move, clip, stance and reaction let go (a round starts again on the
        /// marks: nothing of the last moment of the round before stays on it).
        /// </summary>
        public void ResetPose()
        {
            StandUp();
            StopMove(); StopUpper();
            fadingClip = null; fadeOutLeft = 0f; clipWeight = 0f;
            crouchHeld = false; guarding = false;
            moveNow = BodyMove.None; strikeNow = Combat.BodyStrike.Weapon;
            if (moveLift != null) { StopCoroutine(moveLift); moveLift = null; }
            stanceClip = idleClip; idleTime = 0f; stanceSwapped = true;
            actionNow = null; snapLeft = 0f;
            if (ready) Rest();
        }
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
                idleClip = MapClip(Combat.DuelAction.Idle) is string idn ? ClipOf(idn) : null; // (the stances from the animation map)
                crouchClip = MapClip(Combat.DuelAction.Crouch) is string cn ? ClipOf(cn) : null; blockClip = MapClip(Combat.DuelAction.Block) is string bn ? ClipOf(bn) : null; walkClip = MapClip(Combat.DuelAction.Walk) is string wn ? ClipOf(wn) : null;
                var blockRow = Combat.DuelAnimationMap.Current.Get(Combat.DuelAction.Block); blockLoops = blockRow != null && blockRow.loop;
                stanceClip = idleClip;
            }
            KnowBones();
            if (bones == null) return false;
            // the stance: a duel, nothing else moving the body; crouched, behind the shield, walking or standing still
            var want = Crouched && crouchClip != null ? crouchClip : Guarding && blockClip != null ? blockClip : walking ? walkClip : idleClip;
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
                idleTime = stanceClip == blockClip && !blockLoops ? Mathf.Min(idleTime + dt, stanceClip.length) : Mathf.Repeat(idleTime + dt * rate, Mathf.Max(0.1f, stanceClip.length));
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
        public bool DodgeClip(float seconds = 0.5f) => PlayAction(Combat.DuelAction.Dodge, seconds); // (its section fitted to the dodge's time)

        /// <summary>The duel lost: down on its knees, beaten (the clip; false if there is none).</summary>
        public bool Defeated() => ClipOf("defeated") != null && PlayClip("defeated", 1f, false, true);

        private Renderer[] bodyRenderers;
        private readonly List<Renderer> headRenderers = new List<Renderer>();
        private Transform headSeen;
        private float groundLift;
        /// <summary>The most the body is lifted off the floor (in its own units: about a lying body's thickness).</summary>
        private const float MaxGroundLift = 0.25f;

        /// <summary>
        /// Never into the ground: the body's lowest point (its armour as it lies, its head) kept on the floor it stands on
        /// (its visual root's height), the whole body lifted from the hips by what went under, eased. The skinned bounds
        /// are last frame's (they carry last frame's lift); the head's are this frame's. No allocation per frame.
        /// </summary>
        private void KeepAboveGround(float dt)
        {
            if (hips == null || transform.parent == null) return;
            if (bodyRenderers == null)
            {
                // only what the hips carry (the body; not a cape or a piece hung elsewhere, which the lift does not move:
                // measured, it would ask for more lift each frame without end)
                var carried = new List<Renderer>();
                foreach (var sm in GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (sm.rootBone != null && (sm.rootBone == hips || sm.rootBone.IsChildOf(hips))) carried.Add(sm);
                bodyRenderers = carried.ToArray();
            }
            if (headSeen != head) { headSeen = head; headRenderers.Clear(); if (head != null) head.GetComponentsInChildren(true, headRenderers); }
            float ground = transform.parent.position.y;
            float low = float.MaxValue;
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null && bodyRenderers[i].enabled) low = Mathf.Min(low, bodyRenderers[i].bounds.min.y - groundLift);
            for (int i = 0; i < headRenderers.Count; i++)
                if (headRenderers[i] != null && headRenderers[i].enabled && headRenderers[i].gameObject.activeInHierarchy) low = Mathf.Min(low, headRenderers[i].bounds.min.y);
            if (low == float.MaxValue) return;
            // only while it is down (falling, lying, getting up): standing, its feet are on the floor already (and the
            // skinned bounds reach a little under them)
            bool down = knock != null || playingHold || Flailing;
            float need = down ? Mathf.Clamp(ground + 0.004f - low, 0f, MaxGroundLift * ParentScale) : 0f; // (never more than a lying body's thickness)
            groundLift = need > groundLift ? need : Mathf.MoveTowards(groundLift, need, dt * 1.5f); // (up at once, down gently)
            if (groundLift > 0.0005f) hips.position += Vector3.up * groundLift;
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
