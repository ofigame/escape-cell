using SquashBot.Audio;
using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// One attack's motion and feel: its clip (none yet: the swing is made by code), its three phases (wind-up, contact,
    /// recovery), where and how hard it lands, the freeze on contact, the camera's knock, how far the struck body is
    /// thrown back (only its picture), its effect and sounds. All tunable; the starting values are the design's.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Combat/Attack Motion", fileName = "Attack")]
    public class AttackMotionData : ScriptableObject
    {
        public AnimationClip clip;
        public float anticipationMs = 33f;
        public float activeMs = 17f;
        public float recoveryMs = 67f;
        public HitDirection direction = HitDirection.Horizontal_RightToLeft;
        public HitHeight height = HitHeight.Mid;
        public HitStrength strength = HitStrength.Light;
        public float hitstopMs = 20f;
        public CameraImpulseLevel cameraImpulse = CameraImpulseLevel.None;
        /// <summary>How far the struck body's picture is pushed back (tiles); it springs back, its place never changes.</summary>
        public float visualKnockback = 0.08f;
        public GameObject impactVfx;
        /// <summary>The sounds (the game's own synthesized ones) and their pitch.</summary>
        public Sfx whooshSfx = Sfx.SwishSword;
        public float whooshPitch = 1f;
        public Sfx impactSfx = Sfx.HitSword;
        public float impactPitch = 1f;
        public bool enableTrail = true;
        [Tooltip("How far the striker's picture slides toward the rival so this blow touches its body at contact (m, at most 1.5 tiles); below 0: the family's default (CombatLibrary.ContactOffset)")]
        public float contactOffset = -1f;
        [Tooltip("What strikes: the weapon, or the body itself (a kick, a shoulder, a headbutt) with the weapon still in hand")]
        public BodyStrike body = BodyStrike.Weapon;

        /// <summary>The whole swing in seconds.</summary>
        public float TotalSeconds => (anticipationMs + activeMs + recoveryMs) / 1000f;
    }
}
