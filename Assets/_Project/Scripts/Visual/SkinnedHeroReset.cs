using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The body back to its clean state (Docs/ARAYUZ_HATA_VE_TEMIZLIK.md, section 2.3): every reaction, move, clip and
    /// procedural lean let go, the head on its neck as made, the bones at rest. Called by <see cref="FoiPoseReset"/>
    /// (each round, the match's start and end, every screen foi shows on, the app's return) and when the body is hidden
    /// (its coroutines stop there: nothing they set may stay).
    /// </summary>
    public partial class SkinnedHero
    {
        private Quaternion headMadeRot;
        private Vector3 headMadePos;
        private bool headMadeKnown;

        /// <summary>Everything the blows, moves and clips left on the body undone, now.</summary>
        public void CleanSlate()
        {
            // the falls, staggers, hops and their coroutines; the held clip; the springs
            StandUp();
            if (jolt != null) { StopCoroutine(jolt); jolt = null; }
            if (moveLift != null) { StopCoroutine(moveLift); moveLift = null; }
            // the clips, the upper body, the stance, the crouch and guard, the moves and body blows
            ResetPose();
            // the coded leans and wobbles
            swayT = wobbleT = shudderT = 1f;
            kneelLean = flail = flat = rise = push = reel = 0f;
            crouchUntil = 0f; crouchAmount = 0f;
            if (scaleKnown) transform.localScale = Vector3.one * moveScaleRest;
            // the head on its neck as made, the face at rest
            if (head != null && headMadeKnown) { head.localRotation = headMadeRot; head.localPosition = headMadePos; }
            FaceLid = 0f; FaceWink = false;
            var face = GetComponentInParent<FaceExpressionController>();
            if (face != null) face.Clear();
            if (ready) Rest();
        }

        /// <summary>Lying on the ground as the picture shows it (laid flat, or a fall's clip held on its last frame).</summary>
        public bool LooksDown => flat > 0.5f || (playingHold && playing != null && ClipDone);

        /// <summary>Times a body was found lying while the rules had it up, and stood up (the duel's safety net; the
        /// tests count it).</summary>
        public static int StuckDownFixes { get; private set; }

        /// <summary>Up at once: the rules say it stands but the picture kept lying (a fall's coroutine cut short).</summary>
        public void StandUpStuck()
        {
            StuckDownFixes++;
            CleanSlate();
        }

        /// <summary>
        /// What is not clean on the body now (null: all of it), the check of section 2.4: no reaction or move under way,
        /// no clip held, no procedural lean, the head where it was made, its picture home and at its own pace.
        /// </summary>
        public string PoseViolations()
        {
            var sb = new System.Text.StringBuilder();
            void No(bool bad, string what) { if (bad) sb.Append(what).Append(' '); }
            No(knock != null || stagger != null || hop != null || pop != null || jolt != null || moveLift != null, "coroutine");
            No(playingHold, "held-clip");
            No(upperPlaying != null, "upper-clip");
            No(fadingClip != null, "fading-clip");
            No(kneelLean > 0.001f || flail > 0.001f || flat > 0.001f || rise > 0.001f || push > 0.001f || reel > 0.001f, "lean");
            No(swayT < 1f, "sway"); No(wobbleT < 1f, "wobble"); No(shudderT < 1f, "shudder");
            No(crouchHeld || guarding, "crouch-guard");
            No(moveNow != BodyMove.None || strikeNow != Combat.BodyStrike.Weapon, "move");
            No(bodyKnown && (Quaternion.Angle(transform.localRotation, bodyRot) > 1f || (transform.localPosition - bodyPos).sqrMagnitude > 1e-4f), "body-place");
            No(head != null && headMadeKnown && Quaternion.Angle(head.localRotation, headMadeRot) > 1f, "head");
            var root = transform.parent;
            if (root != null)
            {
                var vr = root.GetComponent<Combat.VisualRoot>();
                No(vr != null && !vr.AtRest, "visual-root");
                var vt = root.GetComponent<Combat.VisualTime>();
                No(vt != null && Mathf.Abs(vt.Scale - 1f) > 0.001f, "visual-time");
            }
            return sb.Length == 0 ? null : sb.ToString().TrimEnd();
        }
    }
}
