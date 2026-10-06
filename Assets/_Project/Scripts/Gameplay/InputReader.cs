using SquashBot.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Gameplay
{
    /// <summary>One frame of player intent: a move in a direction, or a jump forward.</summary>
    public struct InputCommand
    {
        public Direction? move;
        public bool jump;

        /// <summary>A press held still long enough (hover escape): fires once.</summary>
        public bool holdStart;
        /// <summary>Finger position while holding (screen pixels).</summary>
        public Vector2? holdPosition;
        /// <summary>The held finger was lifted.</summary>
        public bool holdEnd;

        public bool IsEmpty => !move.HasValue && !jump && !holdStart && !holdPosition.HasValue && !holdEnd;
    }

    /// <summary>
    /// Turns swipes (touch or mouse drag) and keyboard presses into grid directions, and a double tap
    /// (or the space bar) into a forward jump. Swipes are matched against where each grid axis actually
    /// points on screen, so the isometric view always moves the robot the way the finger went.
    /// </summary>
    public class InputReader
    {
        private const float SwipeThresholdInches = 0.11f; // small, so a short flick registers on the first frames of the drag
        private const float TapMaxDuration = 0.25f;
        private const float DoubleTapWindow = 0.35f;
        private const float HoldTime = 0.3f;

        private readonly Camera cam;
        private bool tracking;
        private bool consumed;
        private Vector2 startPos;
        private Vector2 lastPos;
        private float pressTime;
        private float lastTapTime = -10f;
        private bool holding;

        /// <summary>Long-press detection is only on where the hover escape is unlocked, so it never steals slow swipes elsewhere.</summary>
        public bool HoldEnabled { get; set; }

        /// <summary>
        /// Screen mode (the tunnel runner): swipes map to plain screen directions (right = PlusX, up = PlusY)
        /// and a single tap is a jump.
        /// </summary>
        public bool ScreenMode { get; set; }

        /// <summary>Presses that start on these screen points (on-screen tool buttons) are left to the UI.</summary>
        public System.Func<Vector2, bool> Ignore { get; set; }

        public InputReader(Camera camera)
        {
            cam = camera;
        }

        public InputCommand Poll(Vector3 robotWorld)
        {
            if (ReadJumpKey()) return new InputCommand { jump = true };
            var key = ReadKeyboard();
            if (key.HasValue) return new InputCommand { move = ScreenMode ? ScreenDirection(key.Value) : Resolve(Rotate45(key.Value), robotWorld) };

            bool hasPointer = ReadPointer(out bool pressed, out Vector2 pos);
            if (hasPointer && pressed) lastPos = pos;

            if (pressed && !tracking)
            {
                tracking = true;
                consumed = false;
                startPos = pos;
                pressTime = Time.unscaledTime;
                if (Ignore != null && Ignore(pos))
                {
                    consumed = true; // a button press: no swipe, tap or hover from it
                    return default;
                }

                // The second tap of a double tap fires on touch-down, without waiting for the finger to lift.
                if (pressTime - lastTapTime <= DoubleTapWindow)
                {
                    lastTapTime = -10f;
                    consumed = true;
                    return new InputCommand { jump = true };
                }
                return default;
            }

            if (!tracking) return default;

            if (holding)
            {
                if (pressed) return new InputCommand { holdPosition = pos };
                holding = false;
                tracking = false;
                return new InputCommand { holdEnd = true, holdPosition = hasPointer ? pos : lastPos };
            }

            // Held still long enough: start a hover instead of a swipe or tap.
            if (HoldEnabled && pressed && !consumed && Time.unscaledTime - pressTime >= HoldTime
                && (pos - startPos).magnitude < SwipeThresholdInches * (Screen.dpi > 0 ? Screen.dpi : 160f))
            {
                holding = true;
                consumed = true;
                lastTapTime = -10f;
                return new InputCommand { holdStart = true, holdPosition = pos };
            }

            if (!pressed)
            {
                // A quick flick can be released before any mid-drag frame saw it move, so judge it on release too.
                tracking = false;
                if (consumed) return default;
                var move = CheckSwipe(hasPointer ? pos : lastPos, robotWorld);
                if (move.HasValue) return new InputCommand { move = move };
                return new InputCommand { jump = RegisterTap() };
            }

            if (consumed) return default;
            return new InputCommand { move = CheckSwipe(pos, robotWorld) }; // triggers mid-drag for snappy response
        }

        /// <summary>A short press without movement is a tap; two taps in quick succession make a jump.</summary>
        private bool RegisterTap()
        {
            float now = Time.unscaledTime;
            if (now - pressTime > TapMaxDuration) return false;
            if (ScreenMode) return true;
            lastTapTime = now; // the next touch-down within the window completes the double tap
            return false;
        }

        private Direction? CheckSwipe(Vector2 pos, Vector3 robotWorld)
        {
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            var delta = pos - startPos;
            if (delta.magnitude < SwipeThresholdInches * dpi) return null;

            consumed = true; // one move per swipe
            lastTapTime = -10f;
            return ScreenMode ? ScreenDirection(delta) : Resolve(delta.normalized, robotWorld);
        }

        private static Direction ScreenDirection(Vector2 v) =>
            Mathf.Abs(v.x) >= Mathf.Abs(v.y) ? (v.x > 0f ? Direction.PlusX : Direction.MinusX) : (v.y > 0f ? Direction.PlusY : Direction.MinusY);
        /// <summary>Pick the grid direction whose on-screen projection best matches the screen vector.</summary>
        private Direction Resolve(Vector2 screenDir, Vector3 robotWorld)
        {
            var origin = (Vector2)cam.WorldToScreenPoint(robotWorld);
            var best = Direction.PlusX;
            float bestDot = float.MinValue;
            foreach (var d in DirectionExtensions.All)
            {
                var o = d.ToOffset();
                var axis = ((Vector2)cam.WorldToScreenPoint(robotWorld + new Vector3(o.x, 0f, o.y)) - origin).normalized;
                float dot = Vector2.Dot(axis, screenDir);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = d;
                }
            }
            return best;
        }

        // The grid axes run diagonally on screen, so arrow keys are rotated 45 degrees:
        // Up = up-right, Right = down-right, Down = down-left, Left = up-left.
        private static Vector2 Rotate45(Vector2 v)
        {
            const float c = 0.70710678f;
            return new Vector2(v.x * c + v.y * c, -v.x * c + v.y * c);
        }

        private static bool ReadJumpKey()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private static Vector2? ReadKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return null;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) return Vector2.up;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) return Vector2.down;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) return Vector2.left;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) return Vector2.right;
            return null;
#else
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return Vector2.up;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return Vector2.down;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return Vector2.left;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return Vector2.right;
            return null;
#endif
        }

        private static bool ReadPointer(out bool pressed, out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                pressed = true;
                position = touch.primaryTouch.position.ReadValue();
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                pressed = mouse.leftButton.isPressed;
                position = mouse.position.ReadValue();
                return true;
            }
#else
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                pressed = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                position = t.position;
                return true;
            }
            if (Input.mousePresent)
            {
                pressed = Input.GetMouseButton(0);
                position = Input.mousePosition;
                return true;
            }
#endif
            pressed = false;
            position = Vector2.zero;
            return false;
        }
    }
}
