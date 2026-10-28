using SquashBot.Combat;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The duel's moves played from the animation map (<see cref="DuelAnimationMap"/>): each move its own clip's section,
    /// timed to the game (at its own speed, fitted to the move's time, or its contact frame on the game's contact). A
    /// section that follows another is blended in over CrossFade from the pose it left (no snap). No allocation per frame.
    /// </summary>
    public partial class SkinnedHero
    {
        private const float CrossFade = 0.05f;

        private float playingFrom, playingTo;
        private DuelAction? actionNow;
        private Quaternion[] snapRot;
        private Vector3[] snapPos;
        private float snapLeft;
        private int chainTurn;
        private float actionContact = -1f;

        /// <summary>The mapped move playing now (null: none).</summary>
        public DuelAction? Acting => playing != null ? actionNow : null;

        /// <summary>The clip playing now (its name; null: none).</summary>
        public string PlayingClip => playing != null ? playing.name : null;

        /// <summary>How tall it stands, its feet to the top of its head (its hair or hat included; its weapon not).</summary>
        public float TopHeight
        {
            get
            {
                float baseY = transform.parent != null ? transform.parent.position.y : transform.position.y;
                float top = baseY;
                if (head != null) foreach (var r in head.GetComponentsInChildren<Renderer>()) if (r.enabled) top = Mathf.Max(top, r.bounds.max.y);
                if (top <= baseY + 0.01f) top = HeadPoint.y;
                return top - baseY;
            }
        }

        /// <summary>The clip of <paramref name="a"/> in the map (null: none).</summary>
        private static string MapClip(DuelAction a)
        {
            var row = DuelAnimationMap.Current.Get(a);
            return row != null && row.clips != null && row.clips.Length > 0 ? row.clips[0] : null;
        }

        /// <summary>Whether <paramref name="a"/> has its clip (else the move keeps its coded pose).</summary>
        public static bool HasAction(DuelAction a) { var n = MapClip(a); return n != null && ClipOf(n) != null; }

        /// <summary>
        /// Plays move <paramref name="a"/>'s section: fitted to <paramref name="seconds"/> (a Fit row), or its contact
        /// <paramref name="untilContact"/> seconds from now (a Contact row; faster than the row allows, only the part round
        /// the contact plays). False if the move has no clip.
        /// </summary>
        public bool PlayAction(DuelAction a, float seconds = 0f, float untilContact = -1f)
        {
            if (!ready) Setup();
            var row = DuelAnimationMap.Current.Get(a);
            if (row == null) return false;
            string name = row.Pick();
            var c = name != null ? ClipOf(name) : null;
            if (c == null) return false;
            float from = Mathf.Clamp(row.start, 0f, c.length), to = row.end > 0f ? Mathf.Min(row.end, c.length) : c.length;
            if (to - from < 0.05f) { from = 0f; to = c.length; }
            float rate = row.speed;
            if (row.timing == DuelAnimTiming.Fit && seconds > 0.01f) rate = (to - from) / seconds;
            else if (row.timing == DuelAnimTiming.Contact && untilContact >= 0f)
            {
                float lead = row.contact - from;
                if (lead > 0.01f)
                {
                    rate = lead / Mathf.Max(0.01f, untilContact);
                    if (rate > row.maxSpeed) { from = Mathf.Clamp(row.contact - untilContact * row.maxSpeed, from, row.contact); rate = row.maxSpeed; } // (only round the contact)
                }
            }
            rate = Mathf.Clamp(rate, 0.3f, Mathf.Max(0.3f, row.maxSpeed));
            Snapshot();
            if (!PlayMove(name, rate, false, row.loop)) return false;
            playingTime = playingFrom = from; playingTo = to;
            playingHold = row.hold; playingStay = true;
            actionContact = row.timing == DuelAnimTiming.Contact && row.contact > from ? row.contact : -1f; // (a rush stopped on the body jumps here)
            actionNow = a;
            return true;
        }

        /// <summary>A given blow of the plain chain (the cut, the thrust, a family's finish), its contact
        /// <paramref name="untilContact"/> seconds from now; never over a bigger move playing.</summary>
        public bool PlayChainAction(DuelAction a, float untilContact)
        {
            if (playing != null && actionNow.HasValue && !IsChainAction(actionNow.Value)) return false;
            return PlayAction(a, 0f, untilContact);
        }

        private static bool IsChainAction(DuelAction a) => a == DuelAction.ChainSlash || a == DuelAction.ChainStab || a >= DuelAction.ChainFinishSword;

        /// <summary>Whether the plain chain is played by clips (then the body's coded blows are not).</summary>
        public static bool ChainByClips => HasAction(DuelAction.ChainSlash) && HasAction(DuelAction.ChainStab);

        /// <summary>A mapped attack plays (the weapon then rides in the fist: the body swings it, not its own stroke).</summary>
        private bool AttackPlaying => playing != null && actionNow.HasValue && actionNow.Value >= DuelAction.ChainSlash;

        /// <summary>The pose now kept, to blend the next section in from it (a section cut in on another).</summary>
        private void Snapshot()
        {
            KnowBones();
            if (bones == null || playing == null) { snapLeft = 0f; return; }
            if (snapRot == null || snapRot.Length != bones.Length) { snapRot = new Quaternion[bones.Length]; snapPos = new Vector3[bones.Length]; }
            for (int i = 0; i < bones.Length; i++) { snapRot[i] = bones[i].localRotation; snapPos[i] = bones[i].localPosition; }
            snapLeft = CrossFade;
        }

        /// <summary>After the clip is laid on the bones: blended from the kept pose while the cross-fade lasts.</summary>
        private void BlendFromSnapshot(float dt)
        {
            if (snapLeft <= 0f || snapRot == null || bones == null) return;
            snapLeft -= dt;
            float k = 1f - Mathf.Clamp01(snapLeft / CrossFade);
            for (int i = 0; i < bones.Length && i < snapRot.Length; i++)
            {
                bones[i].localRotation = Quaternion.Slerp(snapRot[i], bones[i].localRotation, k);
                bones[i].localPosition = Vector3.Lerp(snapPos[i], bones[i].localPosition, k);
            }
        }
    }
}
