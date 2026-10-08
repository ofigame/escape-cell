using System;
using UnityEngine;

namespace SquashBot.Journey
{
    /// <summary>
    /// The robot's movement and the only authority over its position and facing. Movement is camera-relative on the
    /// ground plane (the camera's pitch never tilts it): stick up always walks where the camera looks, whatever the
    /// robot faces. The robot turns towards where it walks, quickly (degrees per second), independent of the camera.
    /// Speeds up and slows down at set rates; jumps, falls and dashes. The world says where it may stand
    /// (<see cref="Collide"/>, <see cref="Ground"/>), so the motor knows nothing of decks, cliffs or trees.
    /// Coordinates are local to the current stretch of land.
    /// </summary>
    public class PlayerMotor
    {
        private readonly JourneyTuning t;

        public Vector3 Position;
        /// <summary>Facing, degrees around y (0 = +z).</summary>
        public float Yaw;
        public float VerticalSpeed;
        public bool Grounded = true;
        /// <summary>Horizontal velocity this frame (m/s).</summary>
        public Vector3 Velocity { get; private set; }
        /// <summary>0..1 of top walking speed (for the animation).</summary>
        public float Speed01 => Velocity.magnitude / Mathf.Max(0.01f, t.moveSpeed);
        public bool Dashing => dashLeft > 0f;
        public Vector3 Forward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;

        /// <summary>The world's rule for a step from a point to another (returns where it may go).</summary>
        public Func<Vector3, Vector3, Vector3> Collide;
        /// <summary>The ground height under (x, z) for a walker now at height y.</summary>
        public Func<float, float, float, float> Ground;
        /// <summary>Fires on landing from a fall (the impact speed).</summary>
        public event Action<float> Landed;
        public event Action Jumped;

        private Vector3 dashDir;
        private float dashLeft;
        private Vector3? faceTarget;

        public PlayerMotor(JourneyTuning tuning) => t = tuning;

        /// <summary>The direction the stick means right now, on the ground plane, from the camera's yaw.</summary>
        public static Vector3 CameraRelative(Vector2 stick, float cameraYaw)
        {
            var rot = Quaternion.Euler(0f, cameraYaw, 0f);
            var forward = rot * Vector3.forward; // yaw only: flat by construction (pitch never enters)
            var right = rot * Vector3.right;
            return forward * stick.y + right * stick.x;
        }

        /// <summary>Turns the robot to face <paramref name="dir"/> this frame (a swing aimed at a target), at the turn rate ×2.</summary>
        public void FaceTowards(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) faceTarget = dir;
        }

        public void Dash(Vector3 dir)
        {
            dir.y = 0f;
            dashDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Forward;
            dashLeft = t.dashDuration;
            Yaw = Mathf.Atan2(dashDir.x, dashDir.z) * Mathf.Rad2Deg;
        }

        public void Jump()
        {
            if (!Grounded) return;
            VerticalSpeed = t.jumpSpeed;
            Grounded = false;
            Jumped?.Invoke();
        }

        /// <param name="stick">Movement input (dead zone already applied).</param>
        /// <param name="cameraYaw">The camera's yaw, degrees.</param>
        /// <param name="sprint">Sprint lock on: runs on (straight where the camera looks) when the stick is let go.</param>
        /// <param name="speedScale">From combat: 1 = full speed (a heavy move slows the walk, never a blanket stop).</param>
        public void Tick(float dt, Vector2 stick, float cameraYaw, bool sprint, float speedScale)
        {
            Vector3 wish;
            float top;
            if (sprint && stick.sqrMagnitude < 0.01f) { wish = CameraRelative(Vector2.up, cameraYaw); top = t.sprintSpeed; }
            else { wish = CameraRelative(stick, cameraYaw); top = sprint ? t.sprintSpeed : t.moveSpeed; }

            Vector3 vel = Velocity;
            if (dashLeft > 0f)
            {
                dashLeft -= dt;
                vel = dashDir * (t.dashDistance / Mathf.Max(0.01f, t.dashDuration));
            }
            else
            {
                var want = wish * (top * speedScale);
                // Accelerate towards the wanted velocity (a sharp turn of the stick answers at once: the change is
                // applied as a vector, not by first slowing down).
                float rate = want.sqrMagnitude > 0.0001f ? t.acceleration : t.deceleration;
                vel = Vector3.MoveTowards(vel, want, rate * dt);
            }

            // Facing: towards where it walks; a swing's target may take over for a moment.
            Vector3 face = faceTarget ?? (wish.sqrMagnitude > 0.01f ? wish : Vector3.zero);
            if (dashLeft > 0f) face = dashDir;
            if (face.sqrMagnitude > 0.0001f)
            {
                float target = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
                float speed = t.rotationSpeed * (faceTarget.HasValue ? 2f : 1f);
                Yaw = Mathf.MoveTowardsAngle(Yaw, target, speed * dt);
            }
            faceTarget = null;

            // Move, with the world's say on where it may go.
            var next = Position + vel * dt;
            if (Collide != null) next = Collide(Position, next);
            var moved = next - Position;
            moved.y = 0f;
            Velocity = dt > 0f ? moved / dt : Vector3.zero;
            Position.x = next.x;
            Position.z = next.z;

            // Height: gravity, landing.
            float ground = Ground != null ? Ground(Position.x, Position.z, Position.y) : 0f;
            VerticalSpeed -= t.gravity * dt;
            Position.y += VerticalSpeed * dt;
            if (Position.y <= ground)
            {
                Position.y = ground;
                if (!Grounded) Landed?.Invoke(-VerticalSpeed);
                VerticalSpeed = 0f;
                Grounded = true;
            }
            else if (Position.y - ground > 0.05f) Grounded = false;
        }

        /// <summary>Puts the robot somewhere at once (a checkpoint, a new stretch of land).</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
            Velocity = Vector3.zero;
            VerticalSpeed = 0f;
            Grounded = true;
            dashLeft = 0f;
        }

        public void Stop()
        {
            Velocity = Vector3.zero;
            dashLeft = 0f;
        }
    }
}
