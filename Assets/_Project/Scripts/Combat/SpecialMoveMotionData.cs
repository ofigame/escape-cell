using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>A special move's or ultimate's feel: its phases, the freeze at its peak, the camera, the picture's slow motion.</summary>
    [CreateAssetMenu(menuName = "foi/Combat/Special Move Motion", fileName = "Special")]
    public class SpecialMoveMotionData : ScriptableObject
    {
        public string moveKey;
        public float startMs = 300f;
        public float finalHitstopMs = 200f;
        public CameraImpulseLevel finalImpulse = CameraImpulseLevel.Max;
        /// <summary>How slow the picture runs while it starts (1: not at all); the game's clock never changes.</summary>
        public float visualSlow = 0.3f;
        public float whiteFlashMs = 40f;
    }
}
