using System;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Journey
{
    /// <summary>
    /// The third-person camera: an orbit of yaw, pitch and distance around a point above the robot, a little over the
    /// right shoulder. Look input turns it directly (no lag on yaw; optional, light smoothing only if tuned so); only
    /// the orbit point follows the robot with a short, tight smoothing. It is not parented to the robot and never
    /// turns with it: camera and robot face independently. Pulls in when something stands between it and the robot.
    /// It computes the pose; <see cref="CameraRig"/> is the one that moves the actual camera (a single authority).
    /// Coordinates are world space.
    /// </summary>
    public class TPSCamera
    {
        private readonly JourneyTuning t;
        private readonly CameraRig rig;

        public float Yaw;
        public float Pitch;
        private Vector3 pivot;
        private bool hasPivot;
        private Vector2 smoothLook;
        private float distance;
        private Pose blendFrom;
        private float blend = 1f;

        /// <summary>How far the camera may be from <c>from</c> towards <c>to</c> before something is in the way (phase 3: physics).</summary>
        public Func<Vector3, Vector3, float, float> Obstruction;
        /// <summary>The ground height under a world point (keeps the camera out of the ground).</summary>
        public Func<Vector3, float> GroundAt;

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        /// <summary>The camera's aim on the ground plane.</summary>
        public Vector3 FlatForward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;

        public TPSCamera(JourneyTuning tuning, CameraRig rig)
        {
            t = tuning;
            this.rig = rig;
            Pitch = t.defaultPitch;
            distance = t.cameraDistance;
        }

        /// <summary>Turn by look input (degrees). Call every frame, before <see cref="Follow"/>.</summary>
        public void Turn(Vector2 look)
        {
            if (t.rotationSmoothing > 0f) smoothLook = Vector2.Lerp(look, smoothLook, t.rotationSmoothing);
            else smoothLook = look;
            Yaw = Mathf.Repeat(Yaw + smoothLook.x, 360f);
            Pitch = Mathf.Clamp(Pitch - smoothLook.y, t.minPitch, t.maxPitch);
        }

        /// <summary>Follow the robot's feet at <paramref name="target"/> (world) and hand the pose to the rig. Call in LateUpdate.</summary>
        public void Follow(Vector3 target, float dt)
        {
            var want = target + Vector3.up * t.cameraHeight;
            pivot = hasPivot ? Vector3.Lerp(pivot, want, 1f - Mathf.Exp(-t.followSharpness * dt)) : want;
            hasPivot = true;

            Rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            var shoulder = Rotation * Vector3.right * t.shoulderOffset;
            var origin = pivot + shoulder;
            float wanted = t.cameraDistance;
            if (Obstruction != null) wanted = Mathf.Max(t.minCameraDistance, Obstruction(origin, origin - Rotation * Vector3.forward * t.cameraDistance, t.cameraCollisionRadius));
            // Pull in at once when blocked, ease back out when clear.
            distance = wanted < distance ? wanted : Mathf.MoveTowards(distance, wanted, dt * 6f);
            var pos = origin - Rotation * Vector3.forward * distance;
            if (GroundAt != null) pos.y = Mathf.Max(pos.y, GroundAt(pos) + 0.35f);
            Position = pos;
            if (blend < 1f)
            {
                // Gliding in from the pose handed over (a ride's camera).
                blend = Mathf.Min(1f, blend + dt / 0.45f);
                float k = Mathf.SmoothStep(0f, 1f, blend);
                Position = Vector3.Lerp(blendFrom.position, Position, k);
                Rotation = Quaternion.Slerp(blendFrom.rotation, Rotation, k);
            }
            rig.Chase(Position, Rotation, t.fieldOfView);
        }

        /// <summary>Takes over from another camera's pose without a jump (yaw and pitch read from it).</summary>
        public void Snap(Pose pose, Vector3 target)
        {
            var e = pose.rotation.eulerAngles;
            Yaw = e.y;
            Pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), t.minPitch, t.maxPitch);
            pivot = target + Vector3.up * t.cameraHeight;
            hasPivot = true;
            distance = t.cameraDistance;
            smoothLook = Vector2.zero;
            blendFrom = pose;
            blend = 0f;
        }

        /// <summary>Behind <paramref name="facingYaw"/> at the default pitch (a start, a respawn).</summary>
        public void Reset(float facingYaw, Vector3 target)
        {
            Yaw = facingYaw;
            Pitch = t.defaultPitch;
            pivot = target + Vector3.up * t.cameraHeight;
            hasPivot = true;
            distance = t.cameraDistance;
            smoothLook = Vector2.zero;
        }
    }
}
