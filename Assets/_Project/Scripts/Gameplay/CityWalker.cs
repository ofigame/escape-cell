using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Walking around the city in third person: the virtual stick moves the robot relative to the camera, dragging
    /// elsewhere turns the camera around it. The robot walks around buildings (never into them) and stays on the plot.
    /// While walking, the camera follows from behind and a little above, high enough that buildings rarely hide the robot.
    /// </summary>
    public class CityWalker : MonoBehaviour
    {
        private const float Speed = 2.4f;
        private const float Radius = 0.26f;
        // The camera sits on a sphere round the robot: turn (yaw), tilt (pitch) and distance are all the player's to change.
        private const float DefaultPitch = 47f, MinPitch = 12f, MaxPitch = 78f;
        private const float DefaultDistance = 4.5f, MinDistance = 2.4f, MaxDistance = 8f;
        private float pitch = DefaultPitch, distance = DefaultDistance;
        private const float BlendTime = 0.6f;

        private Robot robot;
        private CameraRig rig;
        private Transform visual;
        private Transform legL, legR, armL, armR;
        private readonly List<(Transform t, Vector3 pos, Quaternion rot)> limbs = new List<(Transform, Vector3, Quaternion)>();
        private Vector3 pos;
        private float yaw, heading, phase, blend;
        private Pose blendFrom;
        private float repairT = -1f;
        private Transform wrench;

        public bool Active { get; private set; }
        public Vector3 Position => pos;
        public float CameraYaw => yaw;

        public void Init(Robot robotRef, CameraRig rigRef)
        {
            robot = robotRef;
            rig = rigRef;
        }

        public void Begin(Vector3 start)
        {
            Active = true;
            pos = FreeSpotNear(start);
            heading = 45f;
            yaw = 45f; // looking into the plot from its front corner, like the building view
            blend = 0f;
            blendFrom = rig.CurrentPose;
            robot.gameObject.SetActive(true);
            robot.enabled = false;
            visual = robot.Visual;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            limbs.Clear();
            foreach (var name in new[] { "LegL", "LegR", "FootL", "FootR", "ArmL", "ArmR" })
            {
                var t = visual.Find(name);
                if (t != null) limbs.Add((t, t.localPosition, t.localRotation));
            }
            legL = visual.Find("LegL");
            legR = visual.Find("LegR");
            armL = visual.Find("ArmL");
            armR = visual.Find("ArmR");
            PoseRobot(0f, 0f);
            UpdateCamera(0f);
        }

        public void Stop()
        {
            if (!Active) return;
            Active = false;
            foreach (var (t, p, r) in limbs)
            {
                t.localPosition = p;
                t.localRotation = r;
            }
            limbs.Clear();
            if (wrench != null) Destroy(wrench.gameObject);
            wrench = null;
            repairT = -1f;
            robot.enabled = true;
            rig.EndChase();
        }

        /// <summary>Turns the camera (dragging on the screen): degrees per screen pixel are applied by the caller.</summary>
        public void Turn(float degrees, float tilt = 0f)
        {
            yaw += degrees;
            pitch = Mathf.Clamp(pitch + tilt, MinPitch, MaxPitch);
            lookAround = 1.2f;
        }

        /// <summary>Moves the camera in (<1) or out (>1).</summary>
        public void Zoom(float factor) => distance = Mathf.Clamp(distance * factor, MinDistance, MaxDistance);

        // After the player turns the camera by hand, it waits a moment before swinging back behind the robot.
        private float lookAround;
        private const float FollowSpeed = 160f; // degrees per second

        /// <summary>The robot swings a wrench for a moment (repairing a building in person).</summary>
        public void PlayRepair()
        {
            repairT = 0f;
            if (wrench == null)
            {
                var hand = armR != null ? armR : visual;
                wrench = new GameObject("Wrench").transform;
                wrench.SetParent(hand, false);
                wrench.localPosition = new Vector3(0f, -0.1f, 0.06f);
                var m = MaterialFactory.Create(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.4f, 0.05f));
                Shapes.Rounded("Handle", wrench, Vector3.zero, new Vector3(0.04f, 0.2f, 0.03f), 0.01f, m);
                Shapes.Rounded("Head", wrench, new Vector3(0f, -0.11f, 0f), new Vector3(0.1f, 0.05f, 0.03f), 0.01f, m);
            }
            wrench.gameObject.SetActive(true);
        }

        public bool Repairing => repairT >= 0f;

        /// <summary>Moves the robot by the stick (x = right, y = forward, relative to the camera).</summary>
        public void Tick(Vector2 stick, float dt)
        {
            if (!Active) return;
            if (repairT >= 0f)
            {
                repairT += dt;
                stick = Vector2.zero;
                if (repairT >= 2f)
                {
                    repairT = -1f;
                    if (wrench != null) wrench.gameObject.SetActive(false);
                }
            }

            var forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            var move = (right * stick.x + forward * stick.y);
            float amount = Mathf.Clamp01(move.magnitude);
            if (amount > 0.05f)
            {
                move = move.normalized * (Speed * amount * dt);
                // Slide along walls: try each axis on its own.
                var next = pos + new Vector3(move.x, 0f, 0f);
                if (Free(next)) pos = next;
                next = pos + new Vector3(0f, 0f, move.z);
                if (Free(next)) pos = next;
                heading = Mathf.MoveTowardsAngle(heading, Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg, 720f * dt);
                phase += dt * 9f * amount;
                // The camera keeps swinging round behind the robot, so walking and turning always look forward.
                // (Not while backing up: the camera would chase the robot round in circles.)
                if (lookAround <= 0f && stick.y > -0.35f) yaw = Mathf.MoveTowardsAngle(yaw, heading, FollowSpeed * amount * dt);
            }
            lookAround = Mathf.Max(0f, lookAround - dt);
            PoseRobot(amount, dt);
            UpdateCamera(dt);
        }

        private static bool Free(Vector3 p)
        {
            float min = -0.45f, max = City.Size - 0.55f;
            if (p.x < min || p.z < min || p.x > max || p.z > max) return false;
            // Check the robot's footprint corners against the pieces standing there.
            for (int i = 0; i < 4; i++)
            {
                float x = p.x + ((i & 1) == 0 ? -Radius : Radius);
                float z = p.z + ((i & 2) == 0 ? -Radius : Radius);
                var at = City.At(Mathf.RoundToInt(x), Mathf.RoundToInt(z));
                if (at != null && !CityScene.IsFlat(at.Piece)) return false;
            }
            return true;
        }

        private static Vector3 FreeSpotNear(Vector3 start)
        {
            if (Free(start)) return start;
            for (int r = 1; r < City.Size * 2; r++)
                for (int x = -r; x <= r; x++)
                    for (int z = -r; z <= r; z++)
                    {
                        var p = new Vector3(Mathf.Round(start.x) + x, 0f, Mathf.Round(start.z) + z);
                        if (Free(p)) return p;
                    }
            return new Vector3(-0.4f, 0f, -0.4f);
        }

        private void PoseRobot(float amount, float dt)
        {
            robot.transform.position = pos;
            robot.transform.rotation = Quaternion.Euler(0f, heading, 0f);
            float swing = Mathf.Sin(phase) * amount;
            if (legL != null)
            {
                legL.localRotation = Quaternion.Euler(swing * 35f, 0f, 0f);
                legR.localRotation = Quaternion.Euler(-swing * 35f, 0f, 0f);
            }
            if (armL != null)
            {
                armL.localRotation = Quaternion.Euler(-swing * 40f, 0f, 0f);
                float hammer = repairT >= 0f ? -60f - Mathf.Abs(Mathf.Sin(repairT * 9f)) * 60f : swing * 40f;
                armR.localRotation = Quaternion.Euler(hammer, 0f, 0f);
            }
            visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase)) * 0.04f * amount, 0f);
        }

        private void UpdateCamera(float dt)
        {
            var camPos = pos + Vector3.up * 0.45f + Quaternion.Euler(pitch, yaw, 0f) * Vector3.back * distance;
            var look = Quaternion.LookRotation(pos + Vector3.up * 0.45f - camPos);
            // Glide from wherever the camera was (the building view) into the walking view.
            blend = Mathf.Min(1f, blend + dt / BlendTime);
            float k = blend * blend * (3f - 2f * blend);
            rig.Chase(Vector3.Lerp(blendFrom.position, camPos, k), Quaternion.Slerp(blendFrom.rotation, look, k), 55f);
        }
    }
}
