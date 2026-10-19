using BrickMuseum.Character;
using BrickMuseum.City;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrickMuseum.Core
{
    /// <summary>
    /// Sitting on the town's benches: next to a park bench "OTUR" sits the walker down on it, facing the way the bench
    /// faces, and the camera turns to show them and the view; walking off (or "KALK") stands them up again.
    /// </summary>
    public sealed partial class Game
    {
        private bool sitting;
        private CityMap.Obstacle? benchNear;
        private Vector3 standAt;

        private static bool IsBench(CityMap.Obstacle o) => o.model != null && o.model.id == "bank";

        /// <summary>The OTUR / KALK button; true while sitting (nothing else to show then).</summary>
        private bool ShowBenchNearby(CityMap.Obstacle? best, float bestD)
        {
            if (sitting)
            {
                benchNear = null;
                tu.SetHomeLabel("KALK");
                tu.homeBtn.gameObject.SetActive(true);
                tu.carBtn.gameObject.SetActive(false);
                tu.nearChip.SetActive(true);
                tu.nearText.text = "Bankta oturuyorsun";
                return true;
            }
            benchNear = roofNear == null && best != null && bestD < 1.8f && IsBench(best.Value) ? best : null;
            if (benchNear == null) return false;
            tu.SetHomeLabel("OTUR");
            tu.homeBtn.gameObject.SetActive(true);
            return false;
        }

        private void SitOn(CityMap.Obstacle b)
        {
            sitting = true;
            standAt = walkPos;
            facingYaw = b.yaw;
            // the seat: a little in front of the backrest, at the seat's height (4/7 of the bench's height)
            var front = Quaternion.Euler(0, b.yaw, 0) * Vector3.forward;
            float seat = b.ground + (b.top - b.ground) * 4f / 7f;
            walkPos = new Vector3(b.center.x, seat - BrickPerson.HipHeight + 0.12f, b.center.y) + front * 0.15f;
            camYaw = b.yaw + 180f - 35f;     // a three-quarter view from the front
            camPitch = 12f;
            walkCamDist = 5.5f;
            Sfx.Play("pick", 0.5f);
        }

        private void StandUp()
        {
            if (!sitting) return;
            sitting = false;
            walkPos = standAt;
            walkPos.y = GroundAt(walkPos);
            walkVy = 0f; grounded = true;
            walkCamDist = 6.5f;
            camYaw = facingYaw;
        }

        private void SitUpdate(float dt)
        {
            // pushing the stick (or a key) gets up again
            var move = stickVec + testMove;
            var kb = Keyboard.current;
            if (kb != null && (kb.wKey.isPressed || kb.sKey.isPressed || kb.aKey.isPressed || kb.dKey.isPressed)) move = Vector2.one;
            if (move.sqrMagnitude > 0.25f) { StandUp(); return; }
            person.Animate(0f, false, dt);
            person.SitPose();
            person.root.SetPositionAndRotation(walkPos, Quaternion.Euler(0, facingYaw, 0));
            var target = walkPos + Vector3.up * (BrickPerson.HipHeight + 0.5f);
            var rot = Quaternion.Euler(camPitch, camYaw, 0);
            var pos = ClampCam(target, target - rot * Vector3.forward * walkCamDist);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(target - pos);
            city.people.Tick(dt, walkPos, 90f, walkPos);
            TickHouses(dt);
            ShowNearby();
        }
    }
}
