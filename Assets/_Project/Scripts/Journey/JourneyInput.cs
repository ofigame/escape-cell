using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Journey
{
    /// <summary>
    /// The journey's logical input layer: Move, Look, Attack, Jump, Dash, Skill1, Skill2 and the sprint lock, whatever
    /// they come from. Keyboard, mouse and gamepad arrive through Input System actions; the touch controls
    /// (<see cref="TouchStick"/>, <see cref="TouchLookArea"/>, <see cref="TouchButton"/>) write into the same layer.
    /// Gameplay reads it once a frame and never touches devices or transforms through it.
    /// </summary>
    public class JourneyInput
    {
        /// <summary>Movement, -1..1 on both axes, dead zone applied.</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Camera turn wanted this frame, degrees (x = yaw, y = pitch up).</summary>
        public Vector2 Look { get; private set; }
        public bool Sprint { get; set; }
        public bool AttackPressed { get; private set; }
        public bool AttackHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool DashPressed { get; private set; }
        public bool Skill1Pressed { get; private set; }
        public bool Skill2Pressed { get; private set; }

        // Written by the touch controls between frames.
        private Vector2 touchMove, touchLook;
        private bool touchAttack, touchAttackHeld, touchJump, touchDash, touchSkill1, touchSkill2;

        /// <summary>Test hooks and scripted moments can steer here (replaces the stick while set).</summary>
        public Vector2? ScriptedMove;

#if ENABLE_INPUT_SYSTEM
        private readonly InputAction move, look, attack, jump, dash, skill1, skill2, sprint;
#endif
        private readonly JourneyTuning tuning;

        public JourneyInput(JourneyTuning tuning)
        {
            this.tuning = tuning;
#if ENABLE_INPUT_SYSTEM
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            look = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            look.AddBinding("<Gamepad>/rightStick");
            attack = Button("Attack", "<Keyboard>/f", "<Gamepad>/buttonWest");
            jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            dash = Button("Dash", "<Keyboard>/leftShift", "<Gamepad>/buttonEast");
            skill1 = Button("Skill1", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            skill2 = Button("Skill2", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            sprint = Button("Sprint", "<Keyboard>/r", "<Gamepad>/leftStickPress");
            foreach (var a in new[] { move, look, attack, jump, dash, skill1, skill2, sprint }) a.Enable();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static InputAction Button(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            return a;
        }
#endif

        public void Dispose()
        {
#if ENABLE_INPUT_SYSTEM
            foreach (var a in new[] { move, look, attack, jump, dash, skill1, skill2, sprint }) a.Dispose();
#endif
        }

        // ---------- Touch controls write here ----------

        public void SetTouchMove(Vector2 v) => touchMove = v;
        public void AddTouchLook(Vector2 degrees) => touchLook += degrees;
        public void PressAttack() { touchAttack = true; touchAttackHeld = true; }
        public void ReleaseAttack() => touchAttackHeld = false;
        public void PressJump() => touchJump = true;
        public void PressDash() => touchDash = true;
        public void PressSkill1() => touchSkill1 = true;
        public void PressSkill2() => touchSkill2 = true;

        /// <summary>Gathers this frame's input from every source (call once, at the start of the frame's gameplay).</summary>
        public void Poll()
        {
            var m = touchMove;
            var lk = touchLook;
            bool atk = touchAttack, jmp = touchJump, dsh = touchDash, s1 = touchSkill1, s2 = touchSkill2;
#if ENABLE_INPUT_SYSTEM
            var km = move.ReadValue<Vector2>();
            if (km.sqrMagnitude > m.sqrMagnitude) m = km;
            // A gamepad's right stick turns at a steady rate.
            lk += look.ReadValue<Vector2>() * (180f * Time.unscaledDeltaTime);
            // The mouse looks while the right button is held (desktop testing).
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
                lk += mouse.delta.ReadValue() / Mathf.Max(1f, Screen.height) * new Vector2(tuning.lookSensitivityX, tuning.lookSensitivityY);
            atk |= attack.WasPressedThisFrame();
            jmp |= jump.WasPressedThisFrame();
            dsh |= dash.WasPressedThisFrame();
            s1 |= skill1.WasPressedThisFrame();
            s2 |= skill2.WasPressedThisFrame();
            if (sprint.WasPressedThisFrame()) Sprint = !Sprint;
            AttackHeld = touchAttackHeld || attack.IsPressed();
#else
            AttackHeld = touchAttackHeld;
#endif
            if (ScriptedMove.HasValue) m = ScriptedMove.Value;
            // Dead zone, rescaled so movement starts smoothly from its edge.
            float mag = m.magnitude;
            float dz = tuning.joystickDeadZone;
            Move = mag <= dz ? Vector2.zero : m / mag * Mathf.Clamp01((mag - dz) / (1f - dz));
            Look = tuning.invertY ? new Vector2(lk.x, -lk.y) : lk;
            AttackPressed = atk;
            JumpPressed = jmp;
            DashPressed = dsh;
            Skill1Pressed = s1;
            Skill2Pressed = s2;
            touchLook = Vector2.zero;
            touchAttack = touchJump = touchDash = touchSkill1 = touchSkill2 = false;
        }

        /// <summary>Clears everything (on entering a ride, pausing...).</summary>
        public void Clear()
        {
            touchMove = touchLook = Vector2.zero;
            touchAttack = touchAttackHeld = touchJump = touchDash = touchSkill1 = touchSkill2 = false;
            Sprint = false;
            Move = Look = Vector2.zero;
            AttackPressed = JumpPressed = DashPressed = Skill1Pressed = Skill2Pressed = false;
        }
    }
}
