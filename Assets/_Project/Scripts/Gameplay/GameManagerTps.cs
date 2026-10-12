using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The battle arenas fought in landscape over foi's shoulder (TPS): the phone turns sideways when an arena starts,
    /// the left thumb moves foi (the stick's way turned by the camera, to the nearest floor direction, step after step
    /// while held), the right thumb turns the camera round foi, and the buttons strike what is ahead, spin and jump.
    /// Everything else (robots, crates, floor rules, gifts) is the arena as before. Back to portrait when it ends.
    /// On a computer: WASD moves, the arrow keys (or a drag on the right) turn, space strikes, E spins, Q jumps.
    /// </summary>
    public partial class GameManager
    {
        private const float TpsDistance = 6.2f, TpsFov = 58f, TpsTurnSpeed = 240f, TpsPitchSpeed = 120f;
        private TpsControls tps;
        private bool tpsOn, tpsStrike, tpsSpin, tpsJump;
        private float tpsYaw = 45f, tpsPitch = 27f;

        private bool TpsActive => tpsOn && State == GameState.Playing;

        private void EnterTps()
        {
            if (tps == null)
            {
                tps = TpsControls.Create();
                tps.Strike += () => tpsStrike = true;
                tps.Spin += () => tpsSpin = true;
                tps.Jump += () => tpsJump = true;
            }
            tpsOn = true;
            tpsStrike = tpsSpin = tpsJump = false;
            tpsYaw = 45f; // the usual view's way (looking along +x +z)
            tpsPitch = 27f;
            tps.Show(true);
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        private void ExitTps()
        {
            if (!tpsOn) return;
            tpsOn = false;
            if (tps != null) tps.Show(false);
            Screen.orientation = ScreenOrientation.Portrait;
        }

        /// <summary>Portrait everywhere but the arenas: the app allows landscape (for them), the game holds portrait.</summary>
        private static void LockPortrait()
        {
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        private Vector3 TpsForward => Quaternion.Euler(0f, tpsYaw, 0f) * Vector3.forward;

        /// <summary>This frame's command from the TPS controls (and keys), the camera turned by the right thumb.</summary>
        private InputCommand TpsCommand()
        {
            float dt = Time.unscaledDeltaTime;
            var look = tps.TakeLook();
            tpsYaw += look.x * 260f;
            tpsPitch = Mathf.Clamp(tpsPitch - look.y * 140f, 6f, 55f);
            var move = tps.Move;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                var km = new Vector2((k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f), (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f));
                if (km.sqrMagnitude > 0f) move = km.normalized;
                tpsYaw += ((k.rightArrowKey.isPressed ? 1f : 0f) - (k.leftArrowKey.isPressed ? 1f : 0f)) * TpsTurnSpeed * dt;
                tpsPitch = Mathf.Clamp(tpsPitch + ((k.downArrowKey.isPressed ? 1f : 0f) - (k.upArrowKey.isPressed ? 1f : 0f)) * TpsPitchSpeed * dt, 6f, 55f);
                if (k.spaceKey.wasPressedThisFrame) tpsStrike = true;
                if (k.eKey.wasPressedThisFrame) tpsSpin = true;
                if (k.qKey.wasPressedThisFrame) tpsJump = true;
            }
#endif
            var command = default(InputCommand);
            if (tpsStrike) { tpsStrike = false; TpsStrike(); }
            if (tpsSpin) { tpsSpin = false; command.spin = true; return command; }
            if (tpsJump) { tpsJump = false; command.jump = true; return command; }
            if (move.magnitude < 0.3f) return command;
            // The stick's way on the floor, turned by the camera, to the nearest of the four floor directions.
            var fwd = TpsForward;
            var right = new Vector3(fwd.z, 0f, -fwd.x);
            var want = right * move.x + fwd * move.y;
            float best = float.MinValue;
            foreach (var d in DirectionExtensions.All)
            {
                var o = d.ToOffset();
                float dot = Vector3.Dot(want, new Vector3(o.x, 0f, o.y));
                if (dot > best) { best = dot; command.move = d; }
            }
            command.held = true;
            command.pace = Mathf.Clamp01(move.magnitude);
            return command;
        }

        /// <summary>The strike button: the best target in reach ahead of the camera (or a swing at the air).</summary>
        private void TpsStrike()
        {
            if (level == null || level.mission != MissionType.Hunt || !robot.IsAlive) return;
            var fwd = TpsForward;
            if (hunt.BestInReach(robot.Position, StrikeReach, fwd, -0.35f, out var at))
            {
                if (hunt.IsFighter(at)) lastStrikeTap = Time.time;
                if (!TryStrikeNow(at, true)) pendingStrike = (at, true, Time.time);
                return;
            }
            var ahead = robot.Position + new GridPos(Mathf.RoundToInt(fwd.x), Mathf.RoundToInt(fwd.z));
            if (hazards.HasLanded(ahead)) { if (!TryStrikeNow(ahead, false)) pendingStrike = (ahead, false, Time.time); return; }
            if (strikeCooldown <= 0f && robot.Strike(robot.transform.position + fwd * 1.5f)) strikeCooldown = 0.25f;
        }

        /// <summary>The camera over foi's shoulder: behind it by the camera's turn, a little above, looking past it.</summary>
        private void TpsPose(out Vector3 pos, out Quaternion rot)
        {
            var focus = robot.transform.position + Vector3.up * 1.1f;
            var dir = Quaternion.Euler(tpsPitch, tpsYaw, 0f) * Vector3.forward;
            pos = focus - dir * TpsDistance + Quaternion.Euler(0f, tpsYaw, 0f) * Vector3.right * 0.6f; // a touch to the right
            rot = Quaternion.LookRotation(dir);
        }
    }
}
