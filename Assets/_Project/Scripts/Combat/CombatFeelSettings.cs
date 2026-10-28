using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// The duel's feel as a whole: how still a freeze holds, the most freezing light blows may add up to, the camera's
    /// knocks, the big moment's slow motion, what the low quality setting turns off. The player's (and the lab's) own
    /// multipliers for the freeze and the shake ride on top.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Combat/Combat Feel Settings", fileName = "CombatFeelSettings")]
    public class CombatFeelSettings : ScriptableObject
    {
        /// <summary>The pictures' speed while frozen (0.05: nearly still).</summary>
        public float hitstopSpeed = 0.05f;
        /// <summary>Light blows freeze at most this much (ms) within <see cref="hitstopWindow"/> seconds.</summary>
        public float hitstopBudgetMs = 120f;
        public float hitstopWindow = 1f;
        /// <summary>The camera's knock for each <see cref="CameraImpulseLevel"/> (in the camera rig's units) and its fade (seconds).</summary>
        public float[] impulseStrength = { 0f, 0.08f, 0.2f, 0.35f, 0.55f };
        public float impulseFade = 0.22f;
        /// <summary>A big moment's slow motion: the pictures' speed and for how long (real seconds).</summary>
        public float bigMomentSlow = 0.35f;
        public float bigMomentSlowSeconds = 0.6f;
        /// <summary>The struck body's white flash (ms).</summary>
        public float hitFlashMs = 60f;
        [Tooltip("The duel blow's visible wind-up: its contact this long after the tap (the blow's look and feel land then; the rules count it at the tap)")]
        public float contactLeadMs = 75f;
        [Tooltip("The input buffer: a tap this long (ms) before the weapon is ready is kept and struck the moment it is (earlier: the 'not ready' tremble)")]
        public float inputBufferMs = 120f;
        [Tooltip("How far the struck fighter's picture leans into a blow just before it lands (m; the design's 0.3 tile)")]
        public float catchOffset = 0.3f;
        [Tooltip("The most a striker's picture slides toward its rival (m; the design's 1.5 tiles)")]
        public float maxLunge = 1.5f;
        /// <summary>The low quality setting: no camera knock below Medium, no flash.</summary>
        public bool lowQualityTrims = true;
    }
}
