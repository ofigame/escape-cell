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
        /// <summary>The low quality setting: no camera knock below Medium, no flash.</summary>
        public bool lowQualityTrims = true;
    }
}
