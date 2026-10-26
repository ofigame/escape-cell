using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// One move's clip: which clip (or its few variants of the same kind), the section of it played (seconds), how it is
    /// timed, its contact frame. The clips are the body's own (Resources/Heroes/Foi/hero_&lt;name&gt;), made with their travel
    /// on the ground taken out: no root motion, the fighter stays on its place.
    /// </summary>
    [System.Serializable]
    public class DuelAnimationRow
    {
        public DuelAction action;
        /// <summary>The clip's name (hero_&lt;name&gt;); more than one: variants of the same move, taken in turn.</summary>
        public string[] clips = new string[0];
        /// <summary>The section played, seconds into the clip (end 0: to its end).</summary>
        public float start, end;
        public DuelAnimTiming timing = DuelAnimTiming.Speed;
        /// <summary>The speed (Speed timing), or the speed a Fit or Contact timing starts from when the game gives no time.</summary>
        public float speed = 1f;
        /// <summary>The contact frame, seconds into the clip (Contact timing; also the moment the struck one reacts).</summary>
        public float contact;
        /// <summary>The fastest it plays: faster than this, only the part round the contact is played.</summary>
        public float maxSpeed = 2.5f;
        public bool loop, hold;
        /// <summary>A stand-in until the move's own clip comes (the rules' section 6: one row changes then).</summary>
        public bool temporary;
        [TextArea] public string note;

        [System.NonSerialized] private int turn;

        /// <summary>The clip this time (the variants in turn).</summary>
        public string Pick()
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[(turn++) % clips.Length];
        }

        public static DuelAnimationRow Of(DuelAction a, string clip, float start, float end, DuelAnimTiming timing, float speed = 1f, float contact = 0f,
            bool loop = false, bool hold = false, bool temporary = false, float maxSpeed = 2.5f, string note = "") =>
            new DuelAnimationRow { action = a, clips = new[] { clip }, start = start, end = end, timing = timing, speed = speed, contact = contact,
                loop = loop, hold = hold, temporary = temporary, maxSpeed = maxSpeed, note = note };
    }
}
