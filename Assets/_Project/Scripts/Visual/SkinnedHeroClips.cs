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

        private readonly List<Renderer> headRenderers = new List<Renderer>();
        private Transform headSeen;
        private float groundLift;
        /// <summary>The most the body is lifted off the floor (in its own units: about a lying body's thickness).</summary>
        private const float MaxGroundLift = 0.25f;
        // the body's bones as round limbs: each bone to its child, as thick as the armour round it (measured once from the
        // mesh as it was made); the lowest of them is how low the body reaches now
        private Transform[] limbFrom, limbTo;
        private float[] limbRadius;
        private bool limbsKnown;

        /// <summary>
        /// The body's limbs measured once: for each bone the vertices it carries most, how far they lie round the bone's
        /// line (to its child), at the mesh's own pose; the radius is the bulk of them (most, not the furthest few).
        /// </summary>
        private void KnowLimbs()
        {
            limbsKnown = true;
            SkinnedMeshRenderer body = null;
            foreach (var sm in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (sm.sharedMesh != null && sm.rootBone != null && (sm.rootBone == hips || sm.rootBone.IsChildOf(hips)) && (body == null || sm.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = sm;
            if (body == null || !body.sharedMesh.isReadable) return;
            var mesh = body.sharedMesh;
            var verts = mesh.vertices;
            var weights = mesh.boneWeights;
            var binds = mesh.bindposes;
            var bonesOf = body.bones;
            if (weights.Length != verts.Length || binds.Length != bonesOf.Length) return;
            var near = new List<float>[bonesOf.Length];
            var childOf = new int[bonesOf.Length];
            for (int b = 0; b < bonesOf.Length; b++)
            {
                childOf[b] = -1;
                if (bonesOf[b] == null) continue;
                for (int c = 0; c < bonesOf.Length; c++) if (bonesOf[c] != null && bonesOf[c].parent == bonesOf[b]) { childOf[b] = c; break; }
            }
            for (int v = 0; v < verts.Length; v++)
            {
                int b = weights[v].boneIndex0;
                if (b < 0 || b >= bonesOf.Length || bonesOf[b] == null || weights[v].weight0 < 0.5f) continue;
                var p = binds[b].MultiplyPoint3x4(verts[v]); // (in the bone's own frame: the bone at its origin)
                var end = childOf[b] >= 0 ? binds[b].MultiplyPoint3x4(binds[childOf[b]].inverse.MultiplyPoint3x4(Vector3.zero)) : Vector3.zero;
                float d = DistanceToSegment(p, Vector3.zero, end);
                (near[b] ??= new List<float>()).Add(d);
            }
            var from = new List<Transform>(); var to = new List<Transform>(); var radius = new List<float>();
            for (int b = 0; b < bonesOf.Length; b++)
            {
                if (near[b] == null || near[b].Count < 12) continue;
                near[b].Sort();
                from.Add(bonesOf[b]); to.Add(childOf[b] >= 0 ? bonesOf[childOf[b]] : bonesOf[b]);
                radius.Add(near[b][(int)(near[b].Count * 0.9f)]); // (the bulk of the armour round it)
            }
            limbFrom = from.ToArray(); limbTo = to.ToArray(); limbRadius = radius.ToArray();
        }

        /// <summary>How wide its trunk is, as a radius in the world (its hips and spine as thick as their armour, measured from
        /// the mesh); its big head left out. 0 until known.</summary>
        public float TrunkRadius
        {
            get
            {
                if (!limbsKnown) { if (!ready) Setup(); KnowLimbs(); }
                float r = 0f;
                if (limbFrom != null)
                    for (int i = 0; i < limbFrom.Length; i++)
                    {
                        var n = limbFrom[i] != null ? limbFrom[i].name : "";
                        if (n.EndsWith(":Hips") || n.EndsWith(":Spine") || n.EndsWith(":Spine1") || n.EndsWith(":Spine2")) r = Mathf.Max(r, limbRadius[i] * limbFrom[i].lossyScale.x);
                    }
                return r;
            }
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float k = ab.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * k);
        }

        /// <summary>
        /// Never into the ground: while it is down (falling, lying, getting up) the body's lowest point — its limbs as round
        /// as their armour, its head — kept on the floor it stands on, the whole body lifted from the hips by what went
        /// under; standing it is never lifted (its feet are on the floor). Measured on this frame's pose, so it neither
        /// lags nor feeds on itself. No allocation per frame.
        /// </summary>
        private void KeepAboveGround(float dt)
        {
            if (hips == null || transform.parent == null) return;
            if (!limbsKnown) KnowLimbs();
            if (headSeen != head) { headSeen = head; headRenderers.Clear(); if (head != null) head.GetComponentsInChildren(true, headRenderers); }
            bool down = knock != null || playingHold || Flailing;
            float need = 0f;
            if (down)
            {
                float ground = transform.parent.position.y;
                float low = float.MaxValue;
                if (limbFrom != null)
                    for (int i = 0; i < limbFrom.Length; i++)
                    {
                        var a = limbFrom[i]; var b = limbTo[i];
                        if (a == null || b == null) continue;
                        float r = limbRadius[i] * a.lossyScale.x;
                        low = Mathf.Min(low, Mathf.Min(a.position.y, b.position.y) - r);
                    }
                for (int i = 0; i < headRenderers.Count; i++)
                    if (headRenderers[i] != null && headRenderers[i].enabled && headRenderers[i].gameObject.activeInHierarchy) low = Mathf.Min(low, headRenderers[i].bounds.min.y);
                if (low != float.MaxValue) need = Mathf.Clamp(ground + 0.004f - low, 0f, MaxGroundLift * ParentScale); // (never more than a lying body's thickness)
            }
            groundLift = need > groundLift ? need : Mathf.MoveTowards(groundLift, need, dt * 3f); // (up at once, down quickly)
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
