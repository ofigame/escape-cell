using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SquashBot.Visual
{
    /// <summary>
    /// Keeps phones cool: 60 frames a second while something moves that needs it (a level being played, the open world
    /// while foi walks or a fight goes on, the opening film, a finger on the screen and a moment after), 30 when the
    /// screen only waits (menus, the map, lists read without touching). Half the frames is about half the heat.
    /// </summary>
    public class FramePacer : MonoBehaviour
    {
        public const int Smooth = 60, Calm = 30;
        private const float Linger = 2.5f;

        /// <summary>The game says whether what is on screen now needs every frame.</summary>
        public static Func<bool> Busy;

        /// <summary>When the rate last changed (the frame governor does not judge the frames round a change).</summary>
        public static float ChangedAt { get; private set; } = -100f;

        private float lastTouch = -100f;

        public static void Install()
        {
            Application.targetFrameRate = Smooth;
            if (FindAnyObjectByType<FramePacer>() != null) return;
            var go = new GameObject("FramePacer");
            DontDestroyOnLoad(go);
            go.AddComponent<FramePacer>();
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (Touched()) lastTouch = now;
            bool busy = now - lastTouch < Linger || (Busy?.Invoke() ?? true);
            int want = busy ? Smooth : Calm;
            if (Application.targetFrameRate == want) return;
            Application.targetFrameRate = want;
            ChangedAt = now;
        }

        private static bool Touched()
        {
            var t = Touchscreen.current;
            if (t != null && t.primaryTouch.press.isPressed) return true;
            var m = Mouse.current;
            if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed || Mathf.Abs(m.scroll.ReadValue().y) > 0.01f)) return true;
            var k = Keyboard.current;
            return k != null && k.anyKey.isPressed;
        }
    }
}
