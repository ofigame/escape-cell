using System;
using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The duel's touches, every finger on its own (one can walk while another strikes or holds the shield): a finger
    /// that lifts quickly without moving is a tap (a blow); one that slides sideways walks (a floating stick, one
    /// finger at a time); a quick swipe up jumps (a loop at its end: a somersault), a quick swipe down dodges. A touch
    /// that starts on a button or in the dead ring round it does nothing here (the buttons take it). On a computer the
    /// mouse is one finger, and the keys help: A/D or the arrows walk, W/up jumps, S/down dodges, Shift holds the shield.
    /// </summary>
    public class DuelTouchReader
    {
        private class Finger
        {
            public Vector2 start, last, stickBase;
            public float pressAt;
            public bool ignored, walking, swiped, moved, crouching;
            public float downAt = -1f;
            public readonly Vector2[] trail = new Vector2[16];
            public readonly float[] trailAt = new float[16];
            public int trailN;
            // a swipe up waiting a moment for a curl at its end
            public Vector2? pendingUp;
            public float pendingAt, curl;
            public Vector2 curlDir, curlPos;
            public int crouchAt;
        }

        private readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
        private readonly List<int> seen = new List<int>(), gone = new List<int>();
        private readonly Queue<InputCommand> queued = new Queue<InputCommand>();
        private int walker = int.MinValue;

        /// <summary>Touches starting on these screen points (the buttons and their dead rings) are the buttons'.</summary>
        public Func<Vector2, bool> Ignore { get; set; }

        /// <summary>The walking finger's stick (screen pixels), for the on-screen stick.</summary>
        public bool StickActive { get; private set; }
        public Vector2 StickBase { get; private set; }
        public Vector2 StickFinger { get; private set; }

        /// <summary>The shield key (Shift) is held (computers).</summary>
        public static bool ShieldKeyHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
#else
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
            }
        }

        private static float PixelsPerDp => (Screen.dpi > 0 ? Screen.dpi : 160f) / 160f;

        /// <summary>Everything let go (a new round, the duel over): no finger walks on.</summary>
        public void Reset()
        {
            fingers.Clear();
            queued.Clear();
            walker = int.MinValue;
            StickActive = false;
        }

        public InputCommand Poll()
        {
            seen.Clear();
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                {
                    int id = t.touchId.ReadValue();
                    if (id == 0) continue;
                    bool down = t.press.isPressed;
                    if (!down && !fingers.ContainsKey(id)) continue;
                    Track(id, down, t.position.ReadValue());
                }
            var mouse = Mouse.current;
            if (mouse != null && (ts == null || seen.Count == 0) && (mouse.leftButton.isPressed || fingers.ContainsKey(-1)))
                Track(-1, mouse.leftButton.isPressed, mouse.position.ReadValue());
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                Track(t.fingerId + 1, t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled, t.position);
            }
            if (Input.touchCount == 0 && (Input.GetMouseButton(0) || fingers.ContainsKey(-1)))
                Track(-1, Input.GetMouseButton(0), Input.mousePosition);
#endif
            // fingers no longer reported: lifted where they last were
            gone.Clear();
            foreach (var kv in fingers) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) Lift(id, fingers[id].last);

            var cmd = queued.Count > 0 ? queued.Dequeue() : default;
            foreach (var kv in fingers) if (kv.Value.crouching) cmd.crouch = true; // (while any finger holds its crouch)
            if (walker != int.MinValue && fingers.TryGetValue(walker, out var w) && w.walking && !w.swiped)
            {
                float radius = DuelTouchSettings.Current.stickRadiusDp * PixelsPerDp;
                var delta = w.last - w.stickBase;
                if (delta.magnitude > radius) { w.stickBase = w.last - delta.normalized * radius; delta = w.last - w.stickBase; } // the base follows the finger
                cmd.stick = delta / radius;
                StickActive = true;
                StickBase = w.stickBase;
                StickFinger = w.last;
            }
            else StickActive = false;
            Keys(ref cmd);
            return cmd;
        }

        private void Track(int id, bool down, Vector2 pos)
        {
            seen.Add(id);
            if (!fingers.TryGetValue(id, out var f))
            {
                if (!down) return;
                f = new Finger { start = pos, last = pos, stickBase = pos, pressAt = Time.unscaledTime };
                f.ignored = Ignore != null && Ignore(pos);
                Note(f, pos);
                fingers[id] = f;
                return;
            }
            if (!down) { Lift(id, pos); return; }
            f.last = pos;
            if (f.ignored) return;
            var s = DuelTouchSettings.Current;
            float dp = PixelsPerDp;
            if ((pos - f.start).magnitude > s.tapMaxDp * dp) f.moved = true;
            Note(f, pos);
            if (f.pendingUp.HasValue)
            {
                // after the swipe up: how far the path turns (a loop at its end is a somersault)
                var seg = pos - f.curlPos;
                if (seg.magnitude > 6f * dp)
                {
                    var d = seg.normalized;
                    f.curl += Mathf.Abs(Vector2.SignedAngle(f.curlDir, d));
                    f.curlDir = d; f.curlPos = pos;
                }
                if (f.curl >= s.curlTurn || Time.unscaledTime - f.pendingAt > s.curlWaitMs / 1000f) FireUp(f);
                return;
            }
            if (f.downAt >= 0f && !f.crouching && Time.unscaledTime - f.downAt > CrouchHold) f.crouching = true; // (held: a crouch)
            if (f.crouching)
            {
                // crouched: a swipe up from here is the uppercut (once)
                if (!f.swiped && SwipeFrom(f, pos, f.crouchAt) > 0) { f.swiped = true; queued.Enqueue(new InputCommand { upper = true }); }
                return;
            }
            if (f.swiped) return;
            int side = SideFlick(f, pos);
            if (side != 0) { f.swiped = true; StopWalking(id); queued.Enqueue(new InputCommand { charge = side }); return; }
            var swipe = Swipe(f, pos);
            if (swipe > 0) { f.pendingUp = (pos - f.start).normalized; StartCurl(f, pos); StopWalking(id); return; }
            if (swipe < 0) { f.downAt = Time.unscaledTime; f.crouchAt = f.trailN; StopWalking(id); return; } // (a dodge if let go soon, else a crouch)
            if (!f.walking && walker == int.MinValue)
            {
                var d = pos - f.start;
                bool sideways = Mathf.Abs(d.x) > s.walkStartDp * dp;
                bool slow = d.magnitude > s.walkStartDp * dp && Time.unscaledTime - f.pressAt > s.swipeMaxMs / 1000f;
                if (sideways || slow) { f.walking = true; f.stickBase = f.start; walker = id; }
            }
        }

        private void StartCurl(Finger f, Vector2 pos)
        {
            f.pendingAt = Time.unscaledTime;
            f.curl = 0f;
            f.curlDir = f.pendingUp ?? Vector2.up;
            f.curlPos = pos;
        }

        private void FireUp(Finger f)
        {
            var s = DuelTouchSettings.Current;
            queued.Enqueue(new InputCommand { flick = f.pendingUp, flickCurl = f.curl >= s.curlTurn });
            f.pendingUp = null;
            f.swiped = true;
        }

        private void StopWalking(int id)
        {
            if (walker != id) return;
            walker = int.MinValue;
            if (fingers.TryGetValue(id, out var f)) f.walking = false;
        }

        private void Lift(int id, Vector2 pos)
        {
            if (!fingers.TryGetValue(id, out var f)) return;
            fingers.Remove(id);
            if (walker == id) walker = int.MinValue;
            if (f.ignored) return;
            if (f.pendingUp.HasValue) { FireUp(f); return; }
            if (f.downAt >= 0f && !f.crouching) { queued.Enqueue(new InputCommand { dodge = true }); return; } // (let go soon: a dodge)
            if (f.crouching) return; // (the crouch ends with the finger)
            if (f.swiped || f.walking) return;
            // a swipe let go before any frame saw it: judged on the lift too
            Note(f, pos);
            int swipe = Swipe(f, pos);
            if (swipe > 0) { f.pendingUp = (pos - f.start).normalized; f.curl = 0f; FireUp(f); return; }
            if (swipe < 0) { queued.Enqueue(new InputCommand { dodge = true }); return; }
            var s = DuelTouchSettings.Current;
            bool quick = Time.unscaledTime - f.pressAt <= s.tapMaxMs / 1000f;
            if (!f.moved && (pos - f.start).magnitude <= s.tapMaxDp * PixelsPerDp && quick) queued.Enqueue(new InputCommand { tap = pos });
        }

        /// <summary>How long a swipe down must be held to crouch (the sensitivity's steps scale it).</summary>
        private static float CrouchHold
        {
            get
            {
                var s = DuelTouchSettings.Current;
                int sense = SaveData.TouchSensitivity;
                return s.crouchHoldMs / 1000f * s.swipeLengthSteps[Mathf.Clamp(sense, 0, s.swipeLengthSteps.Length - 1)];
            }
        }

        /// <summary>A quick swipe since trail point <paramref name="from"/> (the crouch's start): up (1), down (-1) or none.</summary>
        private static int SwipeFrom(Finger f, Vector2 pos, int from)
        {
            var s = DuelTouchSettings.Current;
            float min = s.swipeMinDp * PixelsPerDp;
            float now = Time.unscaledTime, window = s.swipeMaxMs / 1000f;
            int n = Mathf.Min(f.trailN - from, f.trail.Length);
            for (int k = 1; k < n; k++)
            {
                int i = ((f.trailN - 1 - k) % f.trail.Length + f.trail.Length) % f.trail.Length;
                if (now - f.trailAt[i] > window) break;
                var v = pos - f.trail[i];
                if (Mathf.Abs(v.y) >= min && Mathf.Abs(v.y) >= Mathf.Abs(v.x) * s.swipeRatio) return v.y > 0f ? 1 : -1;
            }
            return 0;
        }

        /// <summary>A quick flick sideways within the swipe's time from the press (the shoulder charge): -1, 1 or 0.</summary>
        private static int SideFlick(Finger f, Vector2 pos)
        {
            var s = DuelTouchSettings.Current;
            if (Time.unscaledTime - f.pressAt > s.swipeMaxMs / 1000f) return 0; // (a slow drag walks)
            float min = s.swipeMinDp * s.chargeLengthTimes * PixelsPerDp;
            var v = pos - f.start;
            if (Mathf.Abs(v.x) >= min && Mathf.Abs(v.x) >= Mathf.Abs(v.y) * s.swipeRatio) return v.x > 0f ? 1 : -1;
            return 0;
        }

        private static void Note(Finger f, Vector2 pos)
        {
            int i = f.trailN % f.trail.Length;
            f.trail[i] = pos;
            f.trailAt[i] = Time.unscaledTime;
            f.trailN++;
        }

        /// <summary>A quick swipe within the last moment: up (1), down (-1) or none (0), wherever it started.</summary>
        private static int Swipe(Finger f, Vector2 pos)
        {
            var s = DuelTouchSettings.Current;
            int sense = SaveData.TouchSensitivity;
            float min = s.swipeMinDp * s.swipeLengthSteps[Mathf.Clamp(sense, 0, s.swipeLengthSteps.Length - 1)] * PixelsPerDp;
            float ratio = s.swipeRatio * s.swipeRatioSteps[Mathf.Clamp(sense, 0, s.swipeRatioSteps.Length - 1)];
            float now = Time.unscaledTime, window = s.swipeMaxMs / 1000f;
            int n = Mathf.Min(f.trailN, f.trail.Length);
            for (int k = 1; k < n; k++)
            {
                int i = ((f.trailN - 1 - k) % f.trail.Length + f.trail.Length) % f.trail.Length;
                if (now - f.trailAt[i] > window) break;
                var v = pos - f.trail[i];
                if (Mathf.Abs(v.y) >= min && Mathf.Abs(v.y) >= Mathf.Abs(v.x) * ratio) return v.y > 0f ? 1 : -1;
            }
            return 0;
        }

        private bool upWas, downWas;
        private float downKeyAt = -1f, sideKeyAt = -10f;
        private int sideKeyWas, lastSide;

        /// <summary>The computer's keys: A/D or the arrows walk, W/up jumps, S/down dodges.</summary>
        private void Keys(ref InputCommand cmd)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            bool up = kb.wKey.isPressed || kb.upArrowKey.isPressed, down = kb.sKey.isPressed || kb.downArrowKey.isPressed;
#else
            float x = Input.GetAxisRaw("Horizontal");
            bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow), down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
#endif
            // S held: a crouch (let go soon: a dodge); S + W: the uppercut; D/A twice quickly: the shoulder charge
            if (down && !downWas) downKeyAt = Time.unscaledTime;
            bool crouchKey = down && downKeyAt >= 0f && Time.unscaledTime - downKeyAt > CrouchHold;
            if (!down && downWas && downKeyAt >= 0f && Time.unscaledTime - downKeyAt <= CrouchHold) cmd.dodge = true;
            if (!down) downKeyAt = -1f;
            if (crouchKey) cmd.crouch = true;
            int side = x > 0f ? 1 : x < 0f ? -1 : 0;
            if (side != 0 && sideKeyWas == 0)
            {
                if (side == lastSide && Time.unscaledTime - sideKeyAt < 0.3f) cmd.charge = side; // (twice quickly)
                sideKeyAt = Time.unscaledTime; lastSide = side;
            }
            sideKeyWas = side;
            if (x != 0f && !cmd.stick.HasValue && !crouchKey) cmd.stick = new Vector2(x, 0f);
            if (up && !upWas && !cmd.flick.HasValue) { if (crouchKey) cmd.upper = true; else cmd.flick = Vector2.up; }
            upWas = up; downWas = down;
        }
    }
}
