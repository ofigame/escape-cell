using UnityEngine;
using UnityEngine.InputSystem;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The prototype's keys and mouse through the Input System package (the project reads no old UnityEngine.Input):
    /// pressed this frame, held, let go; the mouse's left button and its place in screen pixels (bottom-left origin).
    /// </summary>
    public static class KeysV2
    {
        public static bool Down(Key k) { var kb = Keyboard.current; return kb != null && kb[k].wasPressedThisFrame; }
        public static bool Held(Key k) { var kb = Keyboard.current; return kb != null && kb[k].isPressed; }
        public static bool Up(Key k) { var kb = Keyboard.current; return kb != null && kb[k].wasReleasedThisFrame; }
        public static bool MouseDown { get { var m = Mouse.current; return m != null && m.leftButton.wasPressedThisFrame; } }
        public static bool MouseHeld { get { var m = Mouse.current; return m != null && m.leftButton.isPressed; } }
        public static Vector2 MousePos { get { var m = Mouse.current; return m != null ? m.position.ReadValue() : Vector2.zero; } }
    }
}
