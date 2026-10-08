using UnityEngine;

namespace SquashBot.Journey
{
    /// <summary>
    /// Every feel value of the journey's third-person controls in one place, editable in the Inspector
    /// (Resources/Journey/JourneyTuning.asset; built with these defaults when missing).
    /// </summary>
    [CreateAssetMenu(menuName = "Squash Bot/Journey Tuning", fileName = "JourneyTuning")]
    public class JourneyTuning : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Top walking speed, metres per second.")]
        public float moveSpeed = 4.2f;
        [Tooltip("Speed while the sprint lock is on (stick pushed past its top edge).")]
        public float sprintSpeed = 6f;
        [Tooltip("How fast the robot gets up to speed, m/s².")]
        public float acceleration = 40f;
        [Tooltip("How fast it stops when the stick is let go, m/s².")]
        public float deceleration = 50f;
        [Tooltip("How fast the robot turns to face where it walks, degrees per second.")]
        public float rotationSpeed = 720f;
        public float jumpSpeed = 5.8f;
        public float gravity = 16f;

        [Header("Joystick")]
        [Range(0f, 0.5f)] public float joystickDeadZone = 0.1f;
        [Tooltip("Knob travel at full tilt, UI units (canvas is 1080 high).")]
        public float joystickRadius = 130f;
        [Tooltip("Dragging this far above the base (in radii) offers the sprint lock.")]
        public float sprintLockReach = 1.6f;

        [Header("Camera")]
        [Tooltip("Degrees of yaw for a drag across the full screen height.")]
        public float lookSensitivityX = 260f;
        [Tooltip("Degrees of pitch for a drag across the full screen height.")]
        public float lookSensitivityY = 150f;
        public bool invertY;
        public float cameraDistance = 4.4f;
        [Tooltip("The point the camera orbits: this high above the robot's feet.")]
        public float cameraHeight = 1.45f;
        [Tooltip("Over-the-shoulder offset to the right (negative = left).")]
        public float shoulderOffset = 0.55f;
        public float defaultPitch = 12f;
        public float minPitch = -30f;
        public float maxPitch = 60f;
        [Tooltip("Follow sharpness of the orbit point (higher = tighter). Rotation is never smoothed.")]
        public float followSharpness = 22f;
        [Tooltip("Optional smoothing of look input (0 = none, raw and instant).")]
        [Range(0f, 0.9f)] public float rotationSmoothing = 0f;
        public float fieldOfView = 58f;
        [Tooltip("How close the camera may come when a wall is behind the robot.")]
        public float minCameraDistance = 0.9f;
        public float cameraCollisionRadius = 0.25f;

        [Header("Combat")]
        public float attackCooldown = 0.38f;
        public float attackDuration = 0.3f;
        [Tooltip("The swing lands this far into the attack (seconds).")]
        public float attackHitTime = 0.12f;
        [Range(0f, 1f)] public float attackMoveMultiplier = 1f;
        public float attackRange = 2.3f;
        public float attackArc = 80f;
        [Tooltip("Aim assist: snaps the swing to a robot within this range and angle of the camera's aim.")]
        public float aimAssistRange = 4f;
        public float aimAssistArc = 55f;

        [Header("Skills")]
        public float dashDistance = 4.2f;
        public float dashDuration = 0.2f;
        public float dashCooldown = 1.4f;
        public float slamRadius = 3.8f;
        public float slamCooldown = 8f;
        public int slamDamage = 2;
        [Range(0f, 1f)] public float slamMoveMultiplier = 0.3f;
        public float throwRange = 11f;
        public float throwSpeed = 18f;
        public float throwCooldown = 6f;
        [Tooltip("Legs from which each skill is open.")]
        public int slamFromLeg = 2, throwFromLeg = 3;

        private static JourneyTuning loaded;

        public static JourneyTuning Get()
        {
            if (loaded == null) loaded = Resources.Load<JourneyTuning>("Journey/JourneyTuning");
            if (loaded == null) loaded = CreateInstance<JourneyTuning>();
            return loaded;
        }
    }
}
