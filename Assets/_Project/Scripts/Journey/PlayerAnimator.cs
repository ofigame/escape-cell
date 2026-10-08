using SquashBot.Gameplay;
using UnityEngine;

namespace SquashBot.Journey
{
    /// <summary>
    /// The robot's animation, driven like an Animator would be — a speed value for the walk, the swing's progress,
    /// the slam's wind-up — but procedural, as the robot is built from parts in code. It only moves the robot's own
    /// parts (bob and squash, the hammer's swing); where the robot stands and faces belongs to the motor. If a rigged
    /// model with an Animator arrives later, this is the one class to swap.
    /// </summary>
    public class PlayerAnimator
    {
        private readonly Robot robot;
        private Transform hammer;

        public PlayerAnimator(Robot robot) => this.robot = robot;

        public void SetHammer(Transform held) => hammer = held;

        public void Tick(PlayerMotor motor, PlayerCombat combat)
        {
            robot.ArenaBob(Mathf.Clamp01(motor.Speed01) * (motor.Grounded ? 1f : 0.2f));
            if (hammer == null) return;
            float a;
            if (combat.SlamWind01 > 0f) a = Mathf.Lerp(0f, -150f, combat.SlamWind01); // raised high, about to come down
            else if (combat.IsAttacking)
            {
                float s = combat.Swing01;
                a = s < 0.35f ? Mathf.Lerp(0f, -60f, s / 0.35f) : s < 0.6f ? Mathf.Lerp(-60f, 110f, (s - 0.35f) / 0.25f) : Mathf.Lerp(110f, 0f, (s - 0.6f) / 0.4f);
            }
            else a = Mathf.Sin(Time.time * 9f) * 6f * Mathf.Clamp01(motor.Speed01); // swinging gently with the walk
            hammer.localRotation = Quaternion.Euler(20f + a, 0f, -15f);
        }
    }
}
