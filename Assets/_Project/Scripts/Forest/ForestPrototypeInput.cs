using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.UI;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Forest
{
    /// <summary>
    /// Touch controls of the journey: a finger that drags anywhere walks (the stick opens under it); a quick tap with
    /// any finger strikes, also a second finger while the first one walks; a fast flick up starts the auto-run (a
    /// little faster than walking, steered by dragging, a flick up jumps), a flick down stops it.
    /// </summary>
    public partial class ForestPrototype
    {
        private const float AutoBoost = 1.3f;

        /// <summary>Test hook: the robot walks the path by itself (screenshots of the whole leg).</summary>
        public static bool AutoWalk;

        private class Finger
        {
            public Vector2 start, last;
            public float t0;
            public bool moved;
            public readonly List<(float t, Vector2 p)> trail = new List<(float, Vector2)>();
        }

        private readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
        private readonly List<int> gone = new List<int>();
        private readonly HashSet<int> pressed = new HashSet<int>();
        private bool autoRun;
        private float autoBlocked;
        private RectTransform autoBadge;

        private void BuildAutoBadge()
        {
            autoBadge = UiFactory.Pill("Auto", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 420f), new Vector2(620f, 70f), new Color(0.08f, 0.2f, 0.12f, 0.7f));
            autoBadge.pivot = new Vector2(0.5f, 0.5f);
            var t = UiFactory.TextBox("Text", autoBadge, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 62f), Loc.T("forest.auto"), 30f, new Color(0.7f, 1f, 0.75f));
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            autoBadge.gameObject.SetActive(false);
        }

        private void StartAutoRun()
        {
            if (autoRun) return;
            autoRun = true;
            autoBlocked = 0f;
            AudioPing(1.25f);
        }

        private void StopAutoRun()
        {
            if (!autoRun) return;
            autoRun = false;
            AudioPing(0.8f);
        }

        private static void AudioPing(float pitch) => Audio.AudioManager.PlaySfx(Audio.Sfx.Hop, 0.35f, pitch);

        private void ReadInput()
        {
            joyInput = Vector2.zero;
            if (AutoWalk)
            {
                float aheadZ = pos.z + 4f;
                var target = new Vector3(world.PathX(aheadZ), 0f, aheadZ);
                var to = target - pos;
                float rel = Mathf.DeltaAngle(camYaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                joyInput = new Vector2(Mathf.Sin(rel), Mathf.Cos(rel));
                if (grounded && (InGap(pos.x, pos.z + 0.35f) || rollers.Exists(r => r.localPosition.z > pos.z && r.localPosition.z - pos.z < 1.6f))) jumpQueued = true;
                return;
            }
            float scale = canvas.GetComponent<RectTransform>().rect.width / Screen.width;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                var k = new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0), (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
                if (k != Vector2.zero) joyInput = k.normalized;
                if (kb.spaceKey.wasPressedThisFrame) jumpQueued = true;
                if (kb.fKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame) attackQueued = true;
                if (kb.rKey.wasPressedThisFrame) { if (autoRun) StopAutoRun(); else StartAutoRun(); }
            }

            // Every finger (or the mouse, as finger 0) is followed from where it lands to where it lifts.
            float now = Time.unscaledTime;
            pressed.Clear();
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var touch in ts.touches)
                {
                    if (!touch.press.isPressed) continue;
                    int id = touch.touchId.ReadValue();
                    Track(id, touch.position.ReadValue(), touch.press.wasPressedThisFrame, now, scale);
                }
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                Track(0, Mouse.current.position.ReadValue(), Mouse.current.leftButton.wasPressedThisFrame, now, scale);

            gone.Clear();
            foreach (var kv in fingers) if (!pressed.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone)
            {
                Lifted(fingers[id], now);
                fingers.Remove(id);
                if (id == joyFinger) joyFinger = -1;
            }

            // The stick: the walking finger, once it has moved.
            bool stick = joyFinger >= 0 && fingers.TryGetValue(joyFinger, out var jf) && jf.moved;
            joyBase.gameObject.SetActive(stick);
            if (stick)
            {
                var f = fingers[joyFinger];
                float radius = 80f / scale;
                var d = Vector2.ClampMagnitude(f.last - f.start, radius);
                joyInput = d / radius;
                joyBase.anchoredPosition = f.start * scale;
                joyKnob.anchoredPosition = d * scale;
            }
#endif
            if (autoBadge != null) autoBadge.gameObject.SetActive(autoRun);
        }

        private void Track(int id, Vector2 p, bool justPressed, float now, float scale)
        {
            pressed.Add(id);
            if (!fingers.TryGetValue(id, out var f))
            {
                // A finger landing on a button belongs to the button.
                if (!justPressed || !OnStickArea(p)) return;
                f = new Finger { start = p, last = p, t0 = now };
                fingers[id] = f;
                if (joyFinger < 0) joyFinger = id;
            }
            f.last = p;
            f.trail.Add((now, p));
            while (f.trail.Count > 2 && now - f.trail[0].t > 0.12f) f.trail.RemoveAt(0);
            if ((p - f.start).magnitude > 16f / scale) f.moved = true;
        }

        /// <summary>A finger lifted: a tap strikes, a fast flick up runs (or jumps while running), a flick down stops.</summary>
        private void Lifted(Finger f, float now)
        {
            if (!f.moved)
            {
                if (now - f.t0 < 0.35f) attackQueued = true;
                return;
            }
            var first = f.trail[0];
            float span = now - first.t;
            if (span < 0.016f) return;
            var v = (f.last - first.p) / span;
            if (v.magnitude < Screen.height * 0.9f || Mathf.Abs(v.y) < Mathf.Abs(v.x)) return;
            if (v.y > 0f)
            {
                if (autoRun) jumpQueued = true;
                else StartAutoRun();
            }
            else StopAutoRun();
        }

        /// <summary>The stick starts wherever a thumb lands, except on the buttons (bottom right) and the top bar.</summary>
        private static bool OnStickArea(Vector2 p) =>
            p.y < Screen.height * 0.82f && !(p.x > Screen.width * 0.55f && p.y < Screen.height * 0.42f);

        /// <summary>
        /// The walker's heading and speed this frame. Walking: towards the stick (relative to the camera), the path's
        /// bends followed when pushing roughly forward. Auto-running: straight on along the path, a little faster;
        /// dragging sideways steers.
        /// </summary>
        private bool Steer(float dt, out float speed)
        {
            var input = joyInput;
            bool held = input.sqrMagnitude > 0.02f;
            bool assist = !InFight() && pos.y < ForestWorld.DeckHeight - 0.5f;
            if (autoRun)
            {
                if (held) yaw += input.x * 95f * dt;
                else if (assist) yaw = Mathf.SmoothDampAngle(yaw, AssistYaw(yaw, 0f), ref yawVel, 0.35f, TurnSpeed * 0.6f, dt);
                speed = WalkSpeed * AutoBoost;
                return true;
            }
            speed = 0f;
            if (!held) return false;
            float stickYaw = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
            float target = camYaw + stickYaw;
            if (assist && Mathf.Abs(stickYaw) < 40f) target = AssistYaw(target, Mathf.Abs(stickYaw) / 40f);
            // Turning eases in and out (a smooth arc, not a snap).
            yaw = Mathf.SmoothDampAngle(yaw, target, ref yawVel, 0.2f, TurnSpeed, dt);
            speed = WalkSpeed * Mathf.Clamp01(input.magnitude);
            return true;
        }

        /// <summary>Leans a heading towards the path ahead (fully at <paramref name="off"/> = 0, not at all at 1).</summary>
        private float AssistYaw(float target, float off)
        {
            float aheadZ = pos.z + 4f;
            float pathYaw = Mathf.Atan2(world.PathX(aheadZ) - pos.x, aheadZ - pos.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(target, pathYaw)) > 60f) return target;
            return Mathf.LerpAngle(target, pathYaw, 0.75f * (1f - off));
        }

        /// <summary>The auto-run stops by itself against a wall it can't pass.</summary>
        private void CheckAutoBlocked(float wanted, float moved, float dt)
        {
            if (!autoRun) return;
            autoBlocked = moved < wanted * 0.3f ? autoBlocked + dt : 0f;
            if (autoBlocked > 0.6f) StopAutoRun();
        }
    }
}
