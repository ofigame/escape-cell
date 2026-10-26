using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The special moves' and ultimates' body choreography (the choreography design's section 5): the body carries the
    /// move, the effects only dress it. A move plays for its rules' seconds over the coded pose: bone turns by its progress,
    /// and the body's own lift or step where the move has one. A made clip named move_&lt;name&gt; takes its place when
    /// added (Docs/Koreografi_GerekenKlipler.md).
    /// </summary>
    public partial class SkinnedHero
    {
        private BodyMove moveNow = BodyMove.None;
        private float moveT, moveFor = 1f, moveScale = 1f;
        private Vector3 moveToward;
        private Coroutine moveLift;

        /// <summary>The quake clip's slam, seconds from its start (bl_mixamo_strikes.py cuts it so).</summary>
        private const float QuakeSlam = 0.53f;

        /// <summary>The body move playing now (None: none).</summary>
        public BodyMove Performing => moveNow;

        /// <summary>
        /// Plays <paramref name="move"/> for <paramref name="seconds"/> (its rules' time), facing <paramref name="toward"/>
        /// (the other fighter, in the world).
        /// </summary>
        public void Perform(BodyMove move, float seconds, Vector3 toward)
        {
            if (!ready) Setup();
            moveNow = move;
            moveT = 0f;
            moveFor = Mathf.Max(0.1f, seconds);
            moveToward = toward;
            ReactionClip = "move_" + move.ToString().ToLowerInvariant();
            // a made clip does the move: the quake's slam (QuakeSlam in) on the coded slam's moment, any other over its time
            float rate = move == BodyMove.Quake ? QuakeSlam / (0.6f * moveFor) : MoveLength(ReactionClip) / moveFor;
            if (ClipOf(ReactionClip) != null && PlayClip(ReactionClip, Mathf.Clamp(rate, 0.5f, 2.5f), false, true)) { moveNow = BodyMove.None; return; }
            KnowRest();
            if (moveLift != null) { StopCoroutine(moveLift); moveLift = null; transform.localPosition = bodyPos; }
            var root = transform.parent != null ? transform.parent : transform;
            var ahead = toward - root.position; ahead.y = 0f;
            switch (move)
            {
                case BodyMove.Quake: moveLift = StartCoroutine(Lifted(0.35f, 0.6f, 0.9f)); break; // (a jump, the slam at its end)
                case BodyMove.CoreBurst: moveLift = StartCoroutine(Lifted(0.25f, 0.75f, 1.1f)); break; // (rising as the arms open)
                case BodyMove.ShockPush: Combat.VisualRoot.On(root).Push(-ahead, 0.35f, 0.3f); break; // (the recoil: a step back)
                case BodyMove.ShoulderPush: Combat.VisualRoot.On(root).Lunge(ahead, Combat.VisualRoot.KeptApart(root.position, toward, 0.7f), 0.08f, 0.06f, 0.25f); break;
            }
        }

        /// <summary>The body lifted off the ground between two points of the move (a jump, a rise) and back down.</summary>
        private System.Collections.IEnumerator Lifted(float from, float to, float height)
        {
            float up = height / ParentScale;
            while (moveT < 1f && moveNow != BodyMove.None)
            {
                float k = Mathf.InverseLerp(from, to, moveT);
                float y = k > 0f && k < 1f ? Mathf.Sin(k * Mathf.PI) * up : 0f;
                transform.localPosition = bodyPos + Vector3.up * y;
                yield return null;
            }
            transform.localPosition = bodyPos;
            moveLift = null;
        }

        /// <summary>The move ends now (the duel over, the move cut short).</summary>
        public void StopPerforming()
        {
            moveNow = BodyMove.None;
            if (moveLift != null) { StopCoroutine(moveLift); moveLift = null; transform.localPosition = bodyPos; }
            transform.localScale = Vector3.one * moveScaleRest;
        }

        private float moveScaleRest = 1f;
        private bool scaleKnown;

        /// <summary>Each frame, over the coded pose: the move's bone turns by its progress.</summary>
        private void MovePose(float dt)
        {
            if (!scaleKnown) { scaleKnown = true; moveScaleRest = transform.localScale.x; }
            if (moveNow == BodyMove.None) return;
            moveT = Mathf.Min(1f, moveT + dt / moveFor);
            float k = moveT;
            var right = Vector3.right; var up = Vector3.up; var fwd = Vector3.forward;
            float In(float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, k));
            float Bell(float a, float b) => k <= a || k >= b ? 0f : Mathf.Sin(Mathf.InverseLerp(a, b, k) * Mathf.PI);
            float hold = Mathf.Min(In(0f, 0.12f), 1f - In(0.88f, 1f)); // (into the pose and out of it)
            switch (moveNow)
            {
                case BodyMove.Whirl:
                    // on one foot: the other knee up, the free arm straight out, leaning back a little against the spin
                    Turn(legL, right, -kneeBack * 40f * hold); Turn(kneeL, right, kneeBack * 70f * hold);
                    Turn(armL, fwd, -downL * 70f * hold);
                    Turn(spine, right, -forward * 8f * hold);
                    break;
                case BodyMove.Quake:
                {
                    // both arms up with the weapon (to the slam at 0.6), the knees giving under the landing
                    float raise = In(0.05f, 0.3f) * (1f - In(0.58f, 0.66f));
                    Turn(armL, fwd, -downL * 130f * raise);
                    Turn(armL, right, forward * 70f * In(0.58f, 0.66f) * (1f - In(0.8f, 1f))); // (the slam: the arms down in front)
                    Turn(spine, right, forward * (-10f * raise + 28f * Bell(0.58f, 1f)));
                    if (k < 0.3f) Crouch(0.5f * Bell(0f, 0.35f), 0.08f); // (crouched to jump)
                    else if (k > 0.6f) Crouch(Bell(0.6f, 1f), 0.08f); // (landed: the knees bent)
                    break;
                }
                case BodyMove.SkyRaise:
                    // the free arm up to the sky with the weapon; each bolt brings the weapon down (the weapon's own swing)
                    Turn(armL, fwd, -downL * 150f * hold);
                    Turn(spine, right, -forward * 10f * hold); Turn(neck, right, -forward * 15f * hold);
                    break;
                case BodyMove.PullDown:
                {
                    // pointing at the other (to 0.45), then both arms up and dragged down out of the sky
                    float point = In(0f, 0.12f) * (1f - In(0.4f, 0.5f));
                    Turn(armL, fwd, -downL * 58f * point); Turn(armL, right, forward * 75f * point);
                    float up2 = In(0.5f, 0.7f) * (1f - In(0.82f, 0.95f));
                    Turn(armL, fwd, -downL * 150f * up2);
                    Turn(armL, right, forward * 60f * In(0.82f, 0.95f) * (1f - In(0.95f, 1f)));
                    Turn(spine, right, forward * 22f * Bell(0.8f, 1f));
                    break;
                }
                case BodyMove.CoreBurst:
                {
                    // a fist on its chest (to 0.2), the arms flung open as it rises, down onto one knee
                    float fist = In(0f, 0.08f) * (1f - In(0.2f, 0.28f));
                    Turn(armL, right, forward * 85f * fist); Turn(foreL, fwd, -downL * 90f * fist);
                    float open = In(0.25f, 0.4f) * (1f - In(0.75f, 0.85f));
                    Turn(armL, fwd, -downL * 120f * open);
                    Turn(chest, right, -forward * 14f * open);
                    if (k > 0.75f) { kneelLean = Bell(0.75f, 1.1f); Crouch(Bell(0.75f, 1.1f), 0.08f); }
                    break;
                }
                case BodyMove.IronFlex:
                {
                    // the chest beaten twice, then the arms up, bent, the muscles swelling (the body 5% bigger)
                    float beat = Mathf.Max(Bell(0.05f, 0.2f), Bell(0.22f, 0.37f));
                    Turn(armL, right, forward * 80f * beat); Turn(foreL, fwd, -downL * 80f * beat);
                    float flex = In(0.4f, 0.55f) * (1f - In(0.9f, 1f));
                    Turn(armL, fwd, -downL * 95f * flex); Turn(foreL, right, forward * 100f * flex);
                    transform.localScale = Vector3.one * moveScaleRest * (1f + 0.05f * In(0.4f, 0.55f) * (1f - In(0.95f, 1f)));
                    break;
                }
                case BodyMove.MirrorGuard:
                    // the arms crossed in front, the palms out
                    Turn(armL, right, forward * 80f * hold); Turn(armL, fwd, -downL * 40f * hold);
                    Turn(foreL, up, 50f * hold);
                    break;
                case BodyMove.HookHaul:
                {
                    // the arm flung forward (the hook flies, to 0.3), then hauling on the chain, leaning back
                    float fling = In(0f, 0.1f) * (1f - In(0.3f, 0.4f));
                    Turn(armL, right, forward * 95f * fling); Turn(armL, fwd, -downL * 30f * fling);
                    float haul = In(0.3f, 0.4f) * (1f - In(0.85f, 1f));
                    float tug = 0.5f + 0.5f * Mathf.Sin(k * Mathf.PI * 8f); // (hand over hand)
                    Turn(armL, right, forward * (70f - 50f * tug) * haul);
                    Turn(spine, right, -forward * 18f * haul); Turn(neck, right, forward * 8f * haul);
                    break;
                }
                case BodyMove.ShockPush:
                {
                    float push = In(0f, 0.15f) * (1f - In(0.6f, 1f));
                    Turn(armL, right, forward * 90f * push); Turn(foreL, fwd, downL * -10f * push); // (the palm pushed straight out)
                    Turn(spine, right, forward * 10f * push);
                    break;
                }
                case BodyMove.GroundPunch:
                {
                    float punch = In(0f, 0.3f) * (1f - In(0.75f, 1f));
                    Turn(armL, right, forward * 55f * punch); Turn(armL, fwd, downL * 20f * punch);
                    Turn(spine, right, forward * 30f * punch); Turn(neck, right, forward * 10f * punch);
                    Crouch(punch, 0.08f);
                    break;
                }
                case BodyMove.ChestPatch:
                {
                    // a hand at its chest turning a part in, then a shrug
                    float at = In(0f, 0.15f) * (1f - In(0.7f, 0.8f));
                    Turn(armL, right, forward * 75f * at); Turn(foreL, fwd, -downL * 85f * at);
                    Turn(foreL, up, 25f * at * Mathf.Sin(k * Mathf.PI * 10f)); // (screwing it in)
                    float shrug = Bell(0.78f, 1f);
                    Turn(armL, fwd, -downL * 12f * shrug); Turn(armR, fwd, -downR * 12f * shrug);
                    Turn(neck, right, -forward * 8f * shrug);
                    break;
                }
                case BodyMove.ShoulderPush:
                {
                    float sh = Bell(0f, 1f);
                    Turn(chest, up, 25f * sh); Turn(spine, right, forward * 12f * sh);
                    break;
                }
            }
            if (moveT >= 1f)
            {
                moveNow = BodyMove.None;
                kneelLean = 0f;
                transform.localScale = Vector3.one * moveScaleRest;
                ReactionClip = "";
            }
        }
    }
}
