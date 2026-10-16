using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SquashBot.Kingdom
{
    /// <summary>
    /// The kingdom's camera, as in Age of Empires IV: looking down at a slant over a point on the land, north up until
    /// turned, following no one. One finger drags the land along (the point under it stays under it); two fingers
    /// pinch to zoom and twist to turn. A quick tap is not a drag: it is passed on (selecting, giving orders). On a
    /// computer: WASD or the arrows pan, the wheel zooms, Q and E turn, the middle or right button drags the land.
    /// </summary>
    public class KmCamera : MonoBehaviour
    {
        public const float MinDist = 14f, MaxDist = 70f, Pitch = 52f;

        public Camera Cam { get; private set; }
        public Vector2 Focus;
        public float Dist = 34f, Yaw;
        /// <summary>The land it may look over (map units).</summary>
        public Rect Bounds;

        /// <summary>A tap on the land (screen point) — not on the screen's buttons, not the end of a drag.</summary>
        public event Action<Vector2> Tapped;

        /// <summary>The view is moving now (a finger on it, or still gliding).</summary>
        public bool Moving { get; private set; }

        private const float TapSlop = 18f, TapTime = 0.35f;
        private int dragId = -1;
        private Vector2 dragLast, downAt;
        private float downTime;
        private bool dragging, overUi, twoFinger;
        private float pinchGap, pinchAngle;
        private Vector2 pinchMid;
        private Vector2 glide;

        public static KmCamera Create(Transform parent)
        {
            var go = new GameObject("KmCamera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.78f, 0.9f);
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 260f;
            cam.fieldOfView = 40f;
            var k = go.AddComponent<KmCamera>();
            k.Cam = cam;
            return k;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Touches();
            if (!Application.isMobilePlatform) Mouse(dt); // (a phone's fingers are read as touches only)
            Keys(dt);
            // A flick glides on a moment.
            if (!dragging && glide.sqrMagnitude > 0.01f)
            {
                Focus += glide * dt;
                glide = Vector2.Lerp(glide, Vector2.zero, 1f - Mathf.Exp(-dt * 5f));
            }
            Moving = dragging || twoFinger || glide.sqrMagnitude > 0.05f;
            Clamp();
            Place();
        }

        // ---------- Fingers ----------

        private bool Touches()
        {
            var ts = Touchscreen.current;
            if (ts == null) return false;
            int down = 0;
            Vector2 a = default, b = default;
            int aId = -1;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                if (down == 0) { a = t.position.ReadValue(); aId = t.touchId.ReadValue(); }
                else if (down == 1) b = t.position.ReadValue();
                down++;
            }
            if (down == 0)
            {
                if (dragId >= 0)
                {
                    // Lifted: a tap if it barely moved, quickly, and did not start on a button.
                    if (!dragging && !twoFinger && !overUi && Time.unscaledTime - downTime < TapTime) Tapped?.Invoke(dragLast);
                    dragId = -1;
                }
                dragging = twoFinger = false;
                return false;
            }
            if (down >= 2)
            {
                // Two fingers: pinch to zoom, twist to turn, the middle drags.
                var mid = (a + b) * 0.5f;
                float gap = (a - b).magnitude, angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                if (!twoFinger) { twoFinger = true; dragging = false; pinchGap = gap; pinchAngle = angle; pinchMid = mid; }
                else
                {
                    if (gap > 1f && pinchGap > 1f) Dist = Mathf.Clamp(Dist * pinchGap / gap, MinDist, MaxDist);
                    Yaw -= Mathf.DeltaAngle(pinchAngle, angle);
                    DragLand(pinchMid, mid);
                    pinchGap = gap; pinchAngle = angle; pinchMid = mid;
                }
                dragId = aId;
                return true;
            }
            twoFinger = false;
            if (dragId != aId)
            {
                dragId = aId;
                dragLast = downAt = a;
                downTime = Time.unscaledTime;
                dragging = false;
                glide = Vector2.zero;
                overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(aId);
                return true;
            }
            if (overUi) return true;
            if (!dragging && (a - downAt).magnitude > TapSlop * Mathf.Max(1f, Screen.dpi / 160f) * 0.6f) dragging = true;
            if (dragging)
            {
                var before = Focus;
                DragLand(dragLast, a);
                float dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);
                glide = Vector2.Lerp(glide, (Focus - before) / dt, 0.5f);
            }
            dragLast = a;
            return true;
        }

        // ---------- Mouse and keys (the computer) ----------

        private Vector2 mouseLast;
        private bool mouseDrag, mouseDown;
        private Vector2 mouseDownAt;

        private void Mouse(float dt)
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null) return;
            var p = m.position.ReadValue();
            bool over = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            float wheel = m.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f && !over) Dist = Mathf.Clamp(Dist * (wheel > 0f ? 0.88f : 1.14f), MinDist, MaxDist);
            if ((m.middleButton.isPressed || m.rightButton.isPressed) && (mouseDrag || !over))
            {
                if (mouseDrag) DragLand(mouseLast, p);
                mouseDrag = true;
            }
            else mouseDrag = false;
            // The left button: a click is a tap (dragging the land with it too, as a finger does).
            if (m.leftButton.wasPressedThisFrame) { mouseDown = !over; mouseDownAt = p; downTime = Time.unscaledTime; }
            if (mouseDown && m.leftButton.isPressed && (p - mouseDownAt).magnitude > TapSlop) DragLand(mouseLast, p);
            if (mouseDown && m.leftButton.wasReleasedThisFrame)
            {
                if ((p - mouseDownAt).magnitude <= TapSlop && Time.unscaledTime - downTime < TapTime * 1.5f) Tapped?.Invoke(p);
                mouseDown = false;
            }
            dragging = mouseDrag || (mouseDown && (p - mouseDownAt).magnitude > TapSlop);
            mouseLast = p;
        }

        private void Keys(float dt)
        {
            var k = Keyboard.current;
            if (k == null) return;
            var move = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                                   (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
            if (move.sqrMagnitude > 0f)
            {
                var r = Quaternion.Euler(0f, Yaw, 0f) * new Vector3(move.x, 0f, move.y);
                Focus += new Vector2(r.x, r.z) * Dist * 1.1f * dt;
            }
            if (k.qKey.isPressed) Yaw -= 90f * dt;
            if (k.eKey.isPressed) Yaw += 90f * dt;
        }

        // ---------- Placing ----------

        /// <summary>Moves the view so the land point under one screen point ends up under another.</summary>
        private void DragLand(Vector2 from, Vector2 to)
        {
            if (!Ground(from, out var a) || !Ground(to, out var b)) return;
            Focus += new Vector2(a.x - b.x, a.z - b.z);
            Place();
        }

        /// <summary>Where a screen point meets the land (a level plane at the focus's height: steady while dragging).</summary>
        public bool Ground(Vector2 screen, out Vector3 world)
        {
            float y = KmLand.World(Focus).y;
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            var ray = Cam.ScreenPointToRay(screen);
            if (plane.Raycast(ray, out float d)) { world = ray.GetPoint(d); return true; }
            world = default;
            return false;
        }

        /// <summary>Map units of a screen point on the land.</summary>
        public bool MapPoint(Vector2 screen, out Vector2 map)
        {
            map = default;
            if (!Ground(screen, out var w)) return false;
            map = new Vector2(w.x - KmLand.Origin.x, w.z - KmLand.Origin.z);
            return true;
        }

        private void Clamp()
        {
            if (Bounds.width <= 0f) return;
            Focus.x = Mathf.Clamp(Focus.x, Bounds.xMin + 4f, Bounds.xMax - 4f);
            Focus.y = Mathf.Clamp(Focus.y, Bounds.yMin + 4f, Bounds.yMax - 4f);
        }

        public void Place()
        {
            var at = KmLand.World(Focus);
            var back = Quaternion.Euler(Pitch, Yaw, 0f) * new Vector3(0f, 0f, -Dist);
            transform.position = at + back;
            transform.rotation = Quaternion.LookRotation(at - transform.position);
        }
    }
}
