using System;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The struck body's reaction library (the choreography design's section 3), over the coded pose and the springs:
    /// the stagger's rocking, the heavy blow's drive back onto one knee, the leg sweep, the launch into the air with a back
    /// flip (hit again up there: a spin, a little higher), the landings' loose arms and head, and the two ways up again
    /// (from its back, from its face). Pictures only: the place on the board is the rules'. Each reaction is named so a
    /// made clip can take its place later (<see cref="ReactionClip"/>).
    /// </summary>
    public partial class SkinnedHero
    {
        /// <summary>Feet dragged along the ground (a heavy blow's slide, a landing): where, for the game's dust.</summary>
        public static event Action<Vector3> FootDust;

        /// <summary>The reaction a made clip would replace (Docs/Koreografi_GerekenKlipler.md): its name, while it plays.</summary>
        public string ReactionClip { get; private set; } = "";

        /// <summary>How it lay when it landed: true on its face (it gets up from its face), false on its back.</summary>
        public bool LandedOnFace { get; private set; }

        /// <summary>The last way up it took ("faceUp", "faceDown"), for the lab's checks.</summary>
        public string LastGetUp { get; private set; } = "";

        private float swayT = 1f, swayPow, wobbleT = 1f, wobblePow, kneelLean;
        private Vector3 swayAxis;
        private int airHits;
        private float airExtra, airSpin;

        // ---------- The rocking stagger (medium) ----------

        /// <summary>A medium blow: a step back (the stagger), the arms out for balance, the body rocking back and forth.</summary>
        public void Rock(Vector3 from, float strength)
        {
            Stagger(from, Mathf.Clamp01(0.45f * strength));
            var away = transform.position - from; away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            swayAxis = Vector3.Cross(Vector3.up, away.normalized);
            swayPow = Mathf.Clamp01(strength);
            ReactionClip = "stagger";
            if (ClipInstead("stagger")) return; // (a made clip: it rocks by itself)
            swayT = 0f;
        }

        // ---------- Driven back (heavy) ----------

        /// <summary>A heavy blow: driven two steps back, the feet sliding (dust), down onto one knee, up again (0.8 s).</summary>
        public void DriveBack(Vector3 from)
        {
            if (knock != null) return;
            KnowRest();
            if (stagger != null) { StopCoroutine(stagger); stagger = null; if (staggerRestKnown) transform.localPosition = staggerRest; }
            knock = StartCoroutine(DrivenBack(from));
        }

        private System.Collections.IEnumerator DrivenBack(Vector3 from)
        {
            ReactionClip = "knockback";
            if (ClipInstead("knockback")) { FootDust?.Invoke(transform.position); yield return WhileClip(); ReactionClip = ""; knock = null; yield break; }
            var away = transform.position - from; away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            away.Normalize();
            var axis = Vector3.Cross(Vector3.up, away);
            var restRot = bodyRot; var restPos = bodyPos;
            var slide = away * 0.75f; // (world units: two steps)
            const float Slide = 0.28f, Kneel = 0.24f, Rise = 0.26f;
            bool dust1 = false, dust2 = false;
            for (float e = 0f; e < Slide; e += VDt)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / Slide);
                var parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
                transform.rotation = Quaternion.AngleAxis(12f * Mathf.Sin(k * Mathf.PI), axis) * parentRot * restRot; // (thrown back, then over the feet again)
                transform.localPosition = restPos + Local(slide) * k;
                reel = 1f; reelStep = e / Slide; flail = 0.45f * (1f - k * 0.5f);
                if (!dust1 && k > 0.2f) { dust1 = true; FootDust?.Invoke(transform.position); }
                if (!dust2 && k > 0.7f) { dust2 = true; FootDust?.Invoke(transform.position); }
                yield return null;
            }
            reel = 0f;
            // down onto one knee: the hips drop, the body leans over its knee, the head down
            for (float e = 0f; e < Kneel; e += VDt)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / Kneel);
                Crouch(k, 0.2f);
                kneelLean = k;
                flail = 0.25f * (1f - k);
                transform.localPosition = restPos + Local(slide);
                yield return null;
            }
            for (float e = 0f; e < Rise; e += VDt)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / Rise);
                Crouch(1f - k, 0.15f);
                kneelLean = 1f - k;
                transform.localPosition = restPos + Local(slide) * (1f - k); // (back onto its own place as it rises)
                yield return null;
            }
            kneelLean = 0f; flail = 0f;
            transform.localRotation = restRot; transform.localPosition = restPos;
            ReactionClip = "";
            knock = null;
        }

        // ---------- The leg sweep ----------

        /// <summary>
        /// Swept off its feet: the legs fly out along the sweep (<paramref name="sweepDir"/>), the body hangs flat in the air a
        /// moment, crashes onto its back away from the sweeper (<paramref name="from"/>), bounces once, lies 0.9 s, gets up (0.5 s).
        /// </summary>
        public void SweptOff(Vector3 from, Vector3 sweepDir, float lie = 0.9f)
        {
            if (knock != null) return;
            KnowRest();
            if (stagger != null) { StopCoroutine(stagger); stagger = null; if (staggerRestKnown) transform.localPosition = staggerRest; }
            knock = StartCoroutine(Swept(from, sweepDir, lie));
        }

        private System.Collections.IEnumerator Swept(Vector3 from, Vector3 sweepDir, float lie)
        {
            ReactionClip = "sweep_fall";
            if (ClipInstead("sweep_fall")) { Face(FaceMoment.Surprise); yield return WhileClip(); yield return Lying(lie); yield return GetUp(Vector3.Cross(Vector3.up, -transform.forward), 82f, false); ReactionClip = ""; knock = null; yield break; }
            var away = transform.position - from; away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            away.Normalize();
            sweepDir.y = 0f;
            // tipping over backwards (its top away from the sweeper) and a little along the sweep: the feet go with it
            var fall = (away + (sweepDir.sqrMagnitude > 1e-4f ? -sweepDir.normalized * 0.35f : Vector3.zero)).normalized;
            var axis = Vector3.Cross(Vector3.up, fall);
            float lift = Lift;
            float up = 0.45f / ParentScale;
            Face(FaceMoment.Surprise);
            Squint(0f, 0.01f);
            const float Out = 0.12f, Hang = 0.15f, Drop = 0.14f;
            for (float e = 0f; e < Out; e += VDt) { float k = e / Out; Tilt(axis, 88f * Mathf.SmoothStep(0f, 1f, k), up * k); flail = k; yield return null; }
            for (float e = 0f; e < Hang; e += VDt) { Tilt(axis, 88f, up); flail = 1f; yield return null; } // (flat in the air a moment)
            for (float e = 0f; e < Drop; e += VDt) { float k = e / Drop; Tilt(axis, 88f - 6f * k, up * (1f - k * k) + lift * k); yield return null; }
            yield return Landing(axis, 82f, lie, false);
            yield return GetUp(axis, 82f, false);
            ReactionClip = "";
            knock = null;
        }

        // ---------- The launch (uppercut, KNOCKDOWN) and its juggle ----------

        /// <summary>
        /// Thrown into the air (an uppercut, the knockdown): up, a slow back flip, about <paramref name="air"/> seconds aloft;
        /// hits up there (<see cref="AirHit"/>) spin it and keep it a little longer; down onto its back (or its face, if the
        /// spins turned it over), a bounce, a roll, up again from where it lies.
        /// </summary>
        public void LaunchUp(Vector3 from, float air = 1f, float lie = 0.35f)
        {
            if (knock != null) return;
            KnowRest();
            if (hop != null) { StopCoroutine(hop); hop = null; }
            if (stagger != null) { StopCoroutine(stagger); stagger = null; if (staggerRestKnown) transform.localPosition = staggerRest; }
            airHits = 0; airExtra = 0f; airSpin = 0f;
            knock = StartCoroutine(Launching(from, Mathf.Max(0.4f, air), lie));
        }

        /// <summary>In the air and hit again (the juggle): a spin, a little higher and longer aloft (true: it was up there).</summary>
        public bool AirHit(Vector3 from)
        {
            if (!InAir) return false;
            airHits++;
            airExtra += 0.15f;
            airSpin += 180f;
            Face(FaceMoment.HeavyHit);
            return true;
        }

        /// <summary>Up in the air from a launch (hits there are the juggle's).</summary>
        public bool InAir { get; private set; }

        private System.Collections.IEnumerator Launching(Vector3 from, float air, float lie)
        {
            ReactionClip = "launch_air";
            if (ClipInstead("launch_air")) { Face(FaceMoment.Launched); InAir = true; yield return WhileClip(); InAir = false; LandedOnFace = Mathf.RoundToInt(airSpin / 180f) % 2 == 1; yield return Lying(lie); yield return GetUp(Vector3.Cross(Vector3.up, -transform.forward), LandedOnFace ? -82f : 82f, LandedOnFace); ReactionClip = ""; knock = null; yield break; }
            var away = transform.position - from; away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            away.Normalize();
            var axis = Vector3.Cross(Vector3.up, away);
            float lift = Lift;
            float height = 2.2f / ParentScale;
            Face(FaceMoment.Launched);
            InAir = true;
            float e = 0f, top = 0f;
            while (e < air + airExtra)
            {
                float total = air + airExtra;
                float k = e / total;
                float y = 4f * k * (1f - k) * height + 0.6f * airExtra / Mathf.Max(0.01f, total) * Mathf.Sin(k * Mathf.PI) * height; // (each juggle hit a little higher)
                float flip = 360f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.08f) / 0.75f)); // (a slow back flip)
                float angle = flip + airSpin + 82f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.7f) / 0.3f));
                float landing = Mathf.Clamp01((k - 0.7f) / 0.3f);
                Tilt(axis, angle, y + lift * landing, 1f - landing); // (round its middle in the air, onto its feet's pivot as it comes down)
                flail = Mathf.Min(1f, k * 4f);
                top = Mathf.Max(top, y);
                e += VDt;
                yield return null;
            }
            InAir = false;
            // how it lies: the turns so far (an even number of half spins: on its back; odd: on its face)
            bool onFace = Mathf.RoundToInt(airSpin / 180f) % 2 == 1;
            float lay = onFace ? -82f : 82f;
            float landed = 360f + airSpin + 82f;
            Tilt(axis, landed, lift);
            yield return Landing(axis, lay, lie, onFace, true);
            yield return GetUp(axis, lay, onFace);
            ReactionClip = "";
            knock = null;
        }

        // ---------- Landing, lying, getting up ----------

        /// <summary>Down on the ground: a bounce, the arms and head going loose (the short physics blend), lying still.</summary>
        private System.Collections.IEnumerator Landing(Vector3 axis, float lay, float lie, bool onFace, bool roll = false)
        {
            LandedOnFace = onFace;
            ReactionClip = onFace ? "land_face" : "land_back";
            FootDust?.Invoke(transform.position);
            Squash(1f);
            wobbleT = 0f; wobblePow = 1f;
            Face(FaceMoment.HeavyHit);
            float lift = Lift;
            var restPos = bodyPos;
            var rollBy = Local(Vector3.ProjectOnPlane(Vector3.Cross(axis, Vector3.up), Vector3.up).normalized * (roll ? 0.35f : 0f)); // (a short roll on away)
            for (float e = 0f; e < 0.3f; e += VDt)
            {
                float bounce = Mathf.Sin(e * 18f) * Mathf.Exp(-e * 9f);
                Tilt(axis, lay + (onFace ? -1f : 1f) * 6f * bounce, lift + Mathf.Max(0f, bounce) * 0.12f / ParentScale);
                transform.localPosition += rollBy * Mathf.SmoothStep(0f, 1f, e / 0.3f);
                flail = Mathf.Max(0f, 0.8f - e * 3f);
                flat = Mathf.Min(1f, e * 4f);
                yield return null;
            }
            var lyingAt = transform.localPosition;
            for (float e = 0f; e < lie; e += VDt) { Tilt(axis, lay, lift); transform.localPosition = lyingAt; flat = 1f; flail = 0f; yield return null; }
            bodyRollOffset = lyingAt - (restPos + Vector3.up * lift);
        }

        private Vector3 bodyRollOffset;

        /// <summary>Lies still a while after a made clip's fall (the landing's dust and squash; the clip left it lying).</summary>
        private System.Collections.IEnumerator Lying(float lie)
        {
            FootDust?.Invoke(transform.position);
            if (ClipInstead(LandedOnFace ? "land_face" : "land_back")) yield return WhileClip();
            for (float e = 0f; e < lie; e += VDt) yield return null;
        }

        /// <summary>Up again from how it lies: from its back, sitting up with a hand behind it; from its face, pushing up
        /// onto its knees first; then standing (0.5 s).</summary>
        private System.Collections.IEnumerator GetUp(Vector3 axis, float lay, bool fromFace)
        {
            LastGetUp = fromFace ? "faceDown" : "faceUp";
            ReactionClip = fromFace ? "getup_face_down" : "getup_face_up";
            if (ClipInstead(ReactionClip)) { yield return WhileClip(); flail = flat = rise = push = 0f; kneelLean = 0f; transform.localRotation = bodyRot; transform.localPosition = bodyPos; bodyRollOffset = Vector3.zero; yield break; }
            float lift = Lift;
            var restPos = bodyPos;
            var offset = bodyRollOffset;
            const float First = 0.24f, Then = 0.26f;
            float mid = fromFace ? -38f : 38f;
            for (float e = 0f; e < First; e += VDt)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / First);
                Tilt(axis, Mathf.Lerp(lay, mid, k), lift * (1f - k * 0.5f));
                transform.localPosition += offset;
                flat = 1f - k;
                rise = k; push = fromFace ? 0f : k; kneelLean = fromFace ? k : 0f; // (from its face: up onto its knees, leaning over them)
                yield return null;
            }
            for (float e = 0f; e < Then; e += VDt)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / Then);
                Tilt(axis, Mathf.Lerp(mid, 0f, k), lift * 0.5f * (1f - k));
                transform.localPosition += offset * (1f - k);
                rise = 1f - k; push = fromFace ? 0f : Mathf.Max(0f, 1f - k * 2.5f); kneelLean = fromFace ? 1f - k : 0f;
                yield return null;
            }
            flail = flat = rise = push = 0f; kneelLean = 0f;
            transform.localRotation = bodyRot; transform.localPosition = restPos;
            bodyRollOffset = Vector3.zero;
            reel = 0f;
        }

        // ---------- Made clips in the reactions' place ----------

        /// <summary>A made clip for reaction <paramref name="name"/> (Resources/Heroes/Foi/hero_&lt;name&gt;) starts playing in its
        /// place: true if there is one (the procedural reaction then waits for it).</summary>
        private bool ClipInstead(string name) => ClipOf(name) != null && PlayMove(name);

        /// <summary>Waits while a reaction's made clip plays.</summary>
        private System.Collections.IEnumerator WhileClip()
        {
            while (playing != null) yield return null;
        }

        // ---------- Helpers ----------

        private float ParentScale => Mathf.Max(0.01f, transform.parent != null ? transform.parent.lossyScale.y : 1f);

        /// <summary>Its back's thickness (lying on the floor, not sunk in it), in its parent's units.</summary>
        private float Lift => 0.22f / ParentScale;

        private Vector3 Local(Vector3 world) => transform.parent != null ? transform.parent.InverseTransformVector(world) : world;

        /// <summary>The body turned <paramref name="degrees"/> about <paramref name="axis"/> (world) from its standing turn, raised <paramref name="up"/>.</summary>
        private void Tilt(Vector3 axis, float degrees, float up, float aboutMiddle = 0f)
        {
            var parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            var turn = Quaternion.AngleAxis(degrees, axis);
            transform.rotation = turn * parentRot * bodyRot;
            transform.localPosition = bodyPos + Vector3.up * up;
            if (aboutMiddle > 0f)
            {
                // turned round its middle, not its feet (a flip in the air: the head never swings down into the floor)
                var mid = Vector3.up * MiddleHeight;
                var localTurn = transform.parent != null ? Quaternion.Inverse(transform.parent.rotation) * turn * transform.parent.rotation : turn;
                transform.localPosition += (mid - localTurn * mid) * aboutMiddle;
            }
        }

        /// <summary>Its middle's height above its feet, in its parent's units (where it turns round in a flip).</summary>
        private float MiddleHeight => (head != null ? Mathf.Max(0.3f, head.position.y - transform.position.y) : 1f) * 0.5f / ParentScale;

        private void Face(FaceMoment m)
        {
            var model = transform.parent != null ? transform.parent : transform;
            var f = FaceExpressionController.On(model);
            if (f != null) f.Play(m);
        }

        /// <summary>Each frame, over the coded pose: the stagger's rocking, a kneel's lean, a landing's loose arms and head.</summary>
        private void ReactionPose(float dt)
        {
            var right = Vector3.right; var fwd = Vector3.forward;
            if (swayT < 1f)
            {
                swayT = Mathf.Min(1f, swayT + dt / 0.45f);
                float a = (6f + 6f * swayPow) * Mathf.Sin(swayT * Mathf.PI * 3f) * (1f - swayT); // (back and forth twice, settling)
                var bend = Quaternion.AngleAxis(a, swayAxis);
                if (spine != null) spine.rotation = bend * spine.rotation;
                Turn(armL, fwd, -downL * 30f * (1f - swayT)); Turn(armR, fwd, -downR * 30f * (1f - swayT)); // (the arms out for balance)
            }
            if (kneelLean > 0.001f)
            {
                Turn(spine, right, forward * 16f * kneelLean); Turn(chest, right, forward * 10f * kneelLean);
                Turn(neck, right, forward * 12f * kneelLean);
            }
            if (wobbleT < 1f)
            {
                // the short physics blend at a landing: the arms and head flop and settle (spring-damped, the trunk still)
                wobbleT = Mathf.Min(1f, wobbleT + dt / 0.35f);
                float w = wobblePow * (1f - wobbleT) * (1f - wobbleT);
                float s = Mathf.Sin(wobbleT * Mathf.PI * 5f);
                Turn(armL, fwd, -downL * 35f * w * s); Turn(armR, fwd, downR * 35f * w * s);
                Turn(foreL, fwd, downL * 25f * w * s); Turn(foreR, fwd, -downR * 25f * w * s);
                Turn(neck, right, forward * 18f * w * s);
            }
        }
    }
}
