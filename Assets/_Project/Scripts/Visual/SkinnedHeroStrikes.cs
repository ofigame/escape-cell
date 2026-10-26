using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The chain's blows by the body (the choreography design's 4.2), the weapon still in hand: the sword's spinning heel
    /// kick, the spear's front kick, the axe's shoulder, the heavy weapon's headbutt. Wound up, the strike at the blow's
    /// contact, carried through and back, timed by the blow's own wind-up / strike / recovery. A made clip named
    /// strike_&lt;name&gt; takes its place when added.
    /// </summary>
    public partial class SkinnedHero
    {
        private Combat.BodyStrike strikeNow = Combat.BodyStrike.Weapon;
        private float strikeT, strikeWind, strikeHit, strikeBack;

        private bool crouchHeld;

        /// <summary>A made strike clip's impact, seconds from its start (bl_mixamo_strikes.py cuts each so).</summary>
        private const float ClipImpact = 0.2f;

        /// <summary>The duel's crouch held (the game sets it each frame from the player's finger, or the rival's AI).</summary>
        public bool CrouchHeld { get => crouchHeld; set { crouchHeld = value; if (value) { crouchAt = Time.time; Crouch(0.75f, 0.12f); } } }
        private float crouchAt = -9f;

        /// <summary>Crouched now: held, and told so within the last moment (the duel sets it each frame; when it stops
        /// telling, a round's end, the body stands rather than staying crouched).</summary>
        private bool Crouched => crouchHeld && Time.time - crouchAt < 0.3f;

        /// <summary>The body's blow playing now (Weapon: none).</summary>
        public Combat.BodyStrike Striking => strikeNow;

        /// <summary>The body strikes with <paramref name="part"/>: wound up for <paramref name="wind"/> seconds, the strike
        /// for <paramref name="hit"/> (its middle the blow's contact), back in <paramref name="back"/>; facing <paramref name="toward"/>.</summary>
        public void StrikeWithBody(Combat.BodyStrike part, float wind, float hit, float back, Vector3 toward)
        {
            if (!ready) Setup();
            string clip = "strike_" + part.ToString().ToLowerInvariant();
            // a made clip: played at the rate that puts its impact (ClipImpact in) on the blow's contact
            if (ClipOf(clip) != null && PlayClip(clip, Mathf.Clamp(ClipImpact / Mathf.Max(0.02f, wind + hit * 0.5f), 0.7f, ReadableRate), false, true)) { ReactionClip = clip; return; }
            strikeNow = part;
            strikeT = 0f;
            strikeWind = Mathf.Max(0.04f, wind); strikeHit = Mathf.Max(0.02f, hit); strikeBack = Mathf.Max(0.08f, back);
            ReactionClip = clip;
        }

        /// <summary>The limb that strikes in the body's blow now (its foot, its shoulder, its head), for the contact checks.</summary>
        public Transform StrikingLimb
        {
            get
            {
                switch (strikeNow)
                {
                    case Combat.BodyStrike.HeelKick:
                    case Combat.BodyStrike.FrontKick:
                    case Combat.BodyStrike.Sweep: return kneeR != null && kneeR.childCount > 0 ? kneeR.GetChild(0) : kneeR;
                    case Combat.BodyStrike.Uppercut: return foreL != null && foreL.childCount > 0 ? foreL.GetChild(0) : foreL;
                    case Combat.BodyStrike.FlyingHeadbutt: return head;
                    case Combat.BodyStrike.ShoulderCharge: goto case Combat.BodyStrike.Shoulder;
                    case Combat.BodyStrike.Shoulder:
                    {
                        // the shoulder leading into the blow (whichever is further ahead)
                        var ahead = transform.parent != null ? transform.parent.forward : transform.forward;
                        if (armL == null || armR == null) return chest;
                        return Vector3.Dot(armL.position - armR.position, ahead) > 0f ? armL : armR;
                    }
                    case Combat.BodyStrike.Headbutt: return head;
                    default: return null;
                }
            }
        }

        /// <summary>Each frame, over the coded pose: the body's blow by its progress (0 .. 1 the wind-up, 1 .. 2 the strike,
        /// 2 .. 3 back).</summary>
        private void StrikePose(float dt)
        {
            if (strikeNow == Combat.BodyStrike.Weapon) return;
            strikeT += dt;
            float total = strikeWind + strikeHit + strikeBack;
            float p = strikeT < strikeWind ? strikeT / strikeWind
                : strikeT < strikeWind + strikeHit ? 1f + (strikeT - strikeWind) / strikeHit
                : 2f + (strikeT - strikeWind - strikeHit) / strikeBack;
            // how far out: wound up (negative: drawn back), out at the strike, held a moment, back to rest
            float wind = p < 1f ? Mathf.SmoothStep(0f, 1f, p) : 0f;
            float strike = p < 1f ? 0f : p < 2f ? Mathf.SmoothStep(0f, 1f, (p - 1f) * 1.6f) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 2.15f) / 0.85f));
            var right = Vector3.right; var up = Vector3.up; var fwd = Vector3.forward;
            switch (strikeNow)
            {
                case Combat.BodyStrike.HeelKick:
                    // turned away (the wind-up), then round with the leg swung high and straight from the right across to the left
                    Turn(spine, up, (40f * wind - 70f * strike) * forward);
                    Turn(legR, right, -kneeBack * (25f * wind + 95f * strike)); Turn(kneeR, right, kneeBack * (60f * wind - 50f * strike));
                    Turn(legR, fwd, -20f * strike); // (out to the side: a heel coming round)
                    Turn(spine, fwd, 14f * strike); Turn(armL, fwd, -downL * 45f * strike); // (leaning away for balance)
                    break;
                case Combat.BodyStrike.FrontKick:
                    // the knee drawn up (the wind-up), the leg snapped straight out ahead
                    Turn(legR, right, -kneeBack * (70f * wind + 85f * strike)); Turn(kneeR, right, kneeBack * (90f * wind * (1f - strike)));
                    Turn(spine, right, -forward * 12f * strike); // (leaning back over the standing leg)
                    Turn(armL, fwd, -downL * 30f * strike);
                    break;
                case Combat.BodyStrike.Shoulder:
                    // drawn back and down, then the shoulder driven in, the body low and turned side-on
                    Turn(spine, up, (-25f * wind + 65f * strike) * forward);
                    Turn(spine, right, forward * (10f * wind + 42f * strike)); Turn(chest, right, forward * 18f * strike); // (thrown in behind the shoulder)
                    Turn(armL, fwd, -downL * 10f * strike); Turn(armL, right, -forward * 25f * strike);
                    Crouch(0.35f * Mathf.Max(wind, strike), 0.06f);
                    if (hips != null && strike > 0f)
                    {
                        // a step into it: the body carried in behind the shoulder
                        var fr = transform.parent != null ? transform.parent : transform;
                        float hgt = head != null ? Mathf.Max(0.3f, head.position.y - transform.position.y) : 1f;
                        hips.position += fr.forward * 0.32f * hgt * strike;
                    }
                    break;
                case Combat.BodyStrike.Headbutt:
                    // the big head drawn back (the eyes squeezed, the teeth set), then thrown forward forehead first
                    Turn(spine, right, forward * (-18f * wind + 30f * strike));
                    Turn(chest, right, forward * (-10f * wind + 16f * strike));
                    Turn(neck, right, forward * (-22f * wind + 28f * strike));
                    Turn(armL, right, -forward * 35f * Mathf.Max(wind, strike)); // (the arms back)
                    if (wind > 0.5f) Squint(1f, 0.15f);
                    if (hips != null && strike > 0f)
                    {
                        var fr = transform.parent != null ? transform.parent : transform;
                        float hgt = head != null ? Mathf.Max(0.3f, head.position.y - transform.position.y) : 1f;
                        hips.position += fr.forward * 0.3f * hgt * strike; // (the forehead thrown in with the body behind it)
                    }
                    break;
            }
            var frame = transform.parent != null ? transform.parent : transform;
            float height = head != null ? Mathf.Max(0.3f, head.position.y - transform.position.y) : 1f;
            switch (strikeNow)
            {
                case Combat.BodyStrike.Sweep:
                {
                    // down on one knee (the wind-up), the back leg swept round low at the ankles, the body turning with it
                    float low = Mathf.Max(wind, strike);
                    Crouch(0.9f * low, 0.06f);
                    Turn(legR, fwd, -30f * low); Turn(legR, right, kneeBack * 35f * strike); // (straight out, near the ground)
                    Turn(kneeR, right, kneeBack * 20f * (1f - strike) * low);
                    Turn(spine, up, (25f * wind - 60f * strike) * forward); // (round with the sweep)
                    Turn(armL, fwd, -downL * 45f * low); // (the free arm out for balance)
                    break;
                }
                case Combat.BodyStrike.Uppercut:
                {
                    // gathered low (the wind-up), then springing up, the free fist driving up from below to the chin
                    Crouch(0.85f * wind * (1f - strike), 0.06f);
                    Turn(armL, right, forward * (-20f * wind + 150f * strike)); Turn(foreL, fwd, -downL * 70f * strike);
                    Turn(spine, right, forward * (18f * wind - 15f * strike));
                    if (hips != null) hips.position += Vector3.up * 0.22f * height * strike; // (off the ground with it)
                    break;
                }
                case Combat.BodyStrike.FlyingHeadbutt:
                {
                    // drawn back in the air, the arms back (the wind-up), then thrown forward head first, flying low at the other
                    Turn(spine, right, forward * (-25f * wind + 55f * strike)); Turn(neck, right, forward * (-20f * wind + 25f * strike));
                    Turn(armL, right, -forward * 60f * Mathf.Max(wind, strike)); Turn(armR, right, -forward * 40f * Mathf.Max(wind, strike));
                    Turn(legL, right, kneeBack * 30f * strike); Turn(legR, right, kneeBack * 30f * strike); // (the legs trailing)
                    if (hips != null) hips.position += (Vector3.up * 0.35f * (wind + strike * 0.3f) + frame.forward * 0.3f * strike) * height;
                    if (wind > 0.5f) Squint(1f, 0.2f);
                    break;
                }
                case Combat.BodyStrike.ShoulderCharge:
                {
                    Turn(spine, up, 60f * strike * forward); Turn(spine, right, forward * 30f * strike);
                    if (hips != null) hips.position += frame.forward * 0.25f * height * strike;
                    break;
                }
            }
            if (strikeT >= total) { strikeNow = Combat.BodyStrike.Weapon; ReactionClip = ""; }
        }
    }
}
