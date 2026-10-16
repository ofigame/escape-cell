using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The battle arenas' landscape TPS controls: the left of the screen moves foi (a stick that comes to the thumb),
    /// the right turns the camera (a drag anywhere there), with the strike, spin and jump buttons at the bottom right.
    /// Each part keeps only the finger that started on it, so moving, looking and striking work together. Its canvas
    /// sits under the HUD's, so the HUD's own buttons (pause, skills) still win their touches.
    /// </summary>
    public class TpsControls : MonoBehaviour
    {
        public const float StickRadius = 100f;

        public Vector2 Move { get; private set; }
        /// <summary>The strike button is held (a gun post fires while it is).</summary>
        public bool StrikeHeld => strikeButton != null && strikeButton.Held;
        private PressButton strikeButton;
        private RectTransform stickArea, spinButton, jumpButton, crosshair;
        private TMPro.TextMeshProUGUI ammoText;
        public event Action Strike, Spin, Jump;
        /// <summary>The arena's map in the bottom-right corner.</summary>
        public ArenaMinimap Minimap { get; private set; }

        private Vector2 look;
        private Canvas canvas;
        private CanvasScaler scaler;

        /// <summary>The camera turn gathered since the last call (screen heights dragged, x right, y up).</summary>
        public Vector2 TakeLook()
        {
            var l = look;
            look = Vector2.zero;
            return l;
        }

        public static TpsControls Create()
        {
            var canvas = UiFactory.CreateCanvas("TpsControls", out var scaler);
            canvas.sortingOrder = 9;
            var c = canvas.gameObject.AddComponent<TpsControls>();
            c.canvas = canvas;
            c.scaler = scaler;
            // Everything inside the screen's safe area (clear of an iPhone's notch and home bar).
            var root = UiFactory.Stretch("Safe", canvas.transform);
            root.gameObject.AddComponent<SafeArea>();
            // The look area first (under everything else), the stick's area over its left part, the buttons on top.
            Area<LookArea>("LookArea", root, new Vector2(0.4f, 0f), new Vector2(1f, 0.82f)).owner = c;
            var stick = Area<Stick>("MoveArea", root, new Vector2(0f, 0f), new Vector2(0.4f, 0.82f));
            stick.Build(c);
            c.stickArea = (RectTransform)stick.transform;
            // The gun post's aim: a crosshair in the middle and the bullets left (shown only up on a post).
            c.crosshair = UiFactory.Box("Crosshair", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));
            c.crosshair.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(UiFactory.Stretch("Ring", c.crosshair), new Color(1f, 0.35f, 0.25f, 0.95f), UiSprites.Ring, 0.35f).raycastTarget = false;
            var dot = UiFactory.Box("Dot", c.crosshair, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
            dot.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(dot, new Color(1f, 0.35f, 0.25f), UiSprites.Circle).raycastTarget = false;
            foreach (var (pos, size) in new[] { (new Vector2(0f, 62f), new Vector2(6f, 30f)), (new Vector2(0f, -62f), new Vector2(6f, 30f)), (new Vector2(62f, 0f), new Vector2(30f, 6f)), (new Vector2(-62f, 0f), new Vector2(30f, 6f)) })
            {
                var tick = UiFactory.Box("Tick", c.crosshair, new Vector2(0.5f, 0.5f), pos, size);
                tick.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(tick, new Color(1f, 0.35f, 0.25f, 0.95f), UiSprites.Rounded, 3f).raycastTarget = false;
            }
            c.ammoText = UiFactory.TextBox("Ammo", root, new Vector2(1f, 0f), new Vector2(-190f, 330f), new Vector2(260f, 70f), "100", 56f, Color.white);
            c.ammoText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            c.ammoText.raycastTarget = false;
            c.strikeButton = Button(root, "Strike", new Vector2(-190f, 200f), 200f, new Color(1f, 0.55f, 0.25f, 0.85f), 0, () => c.Strike?.Invoke()).GetComponent<PressButton>();
            c.spinButton = Button(root, "Spin", new Vector2(-440f, 150f), 135f, new Color(0.45f, 0.75f, 1f, 0.8f), 1, () => c.Spin?.Invoke());
            c.jumpButton = Button(root, "Jump", new Vector2(-420f, 360f), 130f, new Color(0.55f, 1f, 0.6f, 0.8f), 2, () => c.Jump?.Invoke());
            c.Minimap = ArenaMinimap.Create(root);
            c.SetGunMode(false, 0);
            c.Show(false);
            return c;
        }

        public void Show(bool on)
        {
            canvas.gameObject.SetActive(on);
            if (!on) { Move = Vector2.zero; look = Vector2.zero; }
        }

        private static T Area<T>(string name, Transform parent, Vector2 min, Vector2 max) where T : Component
        {
            var area = UiFactory.Rect(name, parent, min, max);
            area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            return area.gameObject.AddComponent<T>();
        }

        private bool aimOn;

        /// <summary>A gun in foi's hand: the crosshair in the middle of the screen (no bullet count: it never runs dry).</summary>
        public void SetAim(bool on)
        {
            if (aimOn == on || ammoText.gameObject.activeSelf) return;
            aimOn = on;
            crosshair.gameObject.SetActive(on);
        }

        /// <summary>Puts the crosshair at a screen point (the hand gun's aim), or back in the middle.</summary>
        public void PlaceCrosshair(Vector2 screen)
        {
            var parent = (RectTransform)crosshair.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local);
            crosshair.anchoredPosition = local;
        }

        /// <summary>Up on a gun post: the crosshair and bullets show, the stick and spin go, the jump button climbs down.</summary>
        public void SetGunMode(bool on, int ammo)
        {
            crosshair.gameObject.SetActive(on || aimOn); // a gun in hand keeps its crosshair after the post
            if (on) crosshair.anchoredPosition = Vector2.zero; // the gun post aims through the middle of the screen
            ammoText.gameObject.SetActive(on);
            ammoText.text = ammo.ToString();
            ammoText.color = ammo <= 15 ? new Color(1f, 0.45f, 0.35f) : Color.white;
            stickArea.gameObject.SetActive(!on);
            spinButton.gameObject.SetActive(!on);
            jumpButton.localRotation = Quaternion.Euler(0f, 0f, on ? 180f : 0f); // the chevron points down: climb down
            // On foot a tap on the right side strikes, so the strike button is only there on a gun (held: a burst).
            strikeButton.gameObject.SetActive(on);
            jumpButton.gameObject.SetActive(on);
            // On the gun the right thumb aims, so the fire button moves to the left thumb (and the bullets with it).
            var fire = (RectTransform)strikeButton.transform;
            fire.anchorMin = fire.anchorMax = on ? Vector2.zero : new Vector2(1f, 0f);
            fire.anchoredPosition = on ? new Vector2(260f, 260f) : new Vector2(-190f, 200f);
            strikeButton.baseScale = on ? 1.35f : 1f; // a big fire button for the left thumb
            jumpButton.GetComponent<PressButton>().baseScale = on ? 1.3f : 1f;
            ammoText.fontSize = on ? 72f : 56f;
            var ammoRect = ammoText.rectTransform;
            ammoRect.anchorMin = ammoRect.anchorMax = on ? Vector2.zero : new Vector2(1f, 0f);
            ammoRect.anchoredPosition = on ? new Vector2(260f, 470f) : new Vector2(-190f, 330f);
            if (on) Move = Vector2.zero;
        }

        private static RectTransform Button(Transform parent, string name, Vector2 fromBottomRight, float size, Color colour, int symbol, Action down)
        {
            var r = UiFactory.Box(name, parent, new Vector2(1f, 0f), fromBottomRight, new Vector2(size, size));
            r.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(r, colour, UiSprites.Circle);
            UiFactory.Fill(UiFactory.Stretch("Rim", r), new Color(1f, 1f, 1f, 0.6f), UiSprites.Ring, 0.4f).raycastTarget = false;
            // Drawn symbols (no font glyphs needed): a sword, a ring for the spin, a chevron for the jump.
            if (symbol == 0)
            {
                Bar(r, new Vector2(0f, 8f), new Vector2(size * 0.14f, size * 0.56f), 45f);
                Bar(r, new Vector2(-size * 0.12f, -size * 0.1f), new Vector2(size * 0.34f, size * 0.09f), 45f);
            }
            else if (symbol == 1)
            {
                var ring = UiFactory.Box("Ring", r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.5f, size * 0.5f));
                ring.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(ring, Color.white, UiSprites.Ring, 0.25f).raycastTarget = false;
            }
            else
            {
                Bar(r, new Vector2(-size * 0.1f, 0f), new Vector2(size * 0.1f, size * 0.36f), -40f);
                Bar(r, new Vector2(size * 0.1f, 0f), new Vector2(size * 0.1f, size * 0.36f), 40f);
            }
            r.gameObject.AddComponent<PressButton>().down = down;
            return r;
        }

        private static void Bar(RectTransform parent, Vector2 at, Vector2 size, float angle)
        {
            var b = UiFactory.Box("Bar", parent, new Vector2(0.5f, 0.5f), at, size);
            b.pivot = new Vector2(0.5f, 0.5f);
            b.localRotation = Quaternion.Euler(0f, 0f, angle);
            UiFactory.Fill(b, Color.white, UiSprites.Rounded, 4f).raycastTarget = false;
        }

        // Made while the phone was still upright: the scale follows the screen as it turns (landscape fits the height),
        // or everything would come out sized for the long side.
        private void Update() => UiFactory.Fit(scaler);

        private void OnDisable() { Move = Vector2.zero; look = Vector2.zero; }

        private class LookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
        {
            public TpsControls owner;
            private int pointer = int.MinValue;
            private float downAt, travelled;
            public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
            public void OnPointerDown(PointerEventData e)
            {
                if (pointer != int.MinValue) return;
                pointer = e.pointerId;
                downAt = Time.unscaledTime;
                travelled = 0f;
            }
            public void OnDrag(PointerEventData e)
            {
                if (e.pointerId != pointer) return;
                owner.look += e.delta / Mathf.Max(1f, Screen.height);
                travelled += e.delta.magnitude / Mathf.Max(1f, Screen.height);
            }
            public void OnPointerUp(PointerEventData e)
            {
                if (e.pointerId != pointer) return;
                pointer = int.MinValue;
                // A quick tap on the camera side (not a turn): a strike — or a shot, up on a gun.
                if (Time.unscaledTime - downAt < 0.28f && travelled < 0.025f) owner.Strike?.Invoke();
            }
            private void OnDisable() => pointer = int.MinValue;
        }

        private class Stick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
        {
            private TpsControls owner;
            private RectTransform area, baseRect, knob;
            private Vector2 home = new Vector2(200f, 200f);
            private int pointer = int.MinValue;

            public void Build(TpsControls c)
            {
                owner = c;
                area = (RectTransform)transform;
                float r = StickRadius;
                baseRect = UiFactory.Box("Base", area, Vector2.zero, home, new Vector2(r * 2.3f, r * 2.3f));
                baseRect.pivot = new Vector2(0.5f, 0.5f);
                baseRect.anchoredPosition = home;
                UiFactory.Fill(baseRect, new Color(1f, 1f, 1f, 0.14f), UiSprites.Circle).raycastTarget = false;
                UiFactory.Fill(UiFactory.Stretch("Rim", baseRect), new Color(1f, 1f, 1f, 0.45f), UiSprites.Ring, 0.2f).raycastTarget = false;
                knob = UiFactory.Box("Knob", baseRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(r * 0.95f, r * 0.95f));
                knob.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(knob, new Color(1f, 1f, 1f, 0.75f), UiSprites.Circle).raycastTarget = false;
            }

            public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

            public void OnPointerDown(PointerEventData e)
            {
                if (pointer != int.MinValue) return;
                pointer = e.pointerId;
                var local = Local(e.position);
                if ((local - baseRect.anchoredPosition).magnitude > StickRadius * 1.1f) baseRect.anchoredPosition = local; // the stick comes to the thumb
                Track(local);
            }

            public void OnDrag(PointerEventData e) { if (e.pointerId == pointer) Track(Local(e.position)); }

            public void OnPointerUp(PointerEventData e)
            {
                if (e.pointerId != pointer) return;
                Release();
            }

            private void Release()
            {
                pointer = int.MinValue;
                if (owner != null) owner.Move = Vector2.zero;
                if (baseRect != null) { baseRect.anchoredPosition = home; knob.anchoredPosition = Vector2.zero; }
            }

            private void Track(Vector2 local)
            {
                var clamped = Vector2.ClampMagnitude(local - baseRect.anchoredPosition, StickRadius);
                knob.anchoredPosition = clamped;
                owner.Move = clamped / StickRadius;
            }

            private Vector2 Local(Vector2 screen)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
                return local + area.rect.size * area.pivot;
            }

            private void OnDisable() => Release();
        }

        private class PressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public Action down;
            private float pressed;
            private bool held;
            public bool Held => held;
            /// <summary>Its size (bigger up on a gun); the press squash is on top.</summary>
            public float baseScale = 1f;
            public void OnPointerDown(PointerEventData e) { held = true; pressed = 1f; down?.Invoke(); }
            public void OnPointerUp(PointerEventData e) => held = false;
            private void Update()
            {
                pressed = Mathf.MoveTowards(pressed, held ? 0.6f : 0f, Time.unscaledDeltaTime * 6f);
                transform.localScale = Vector3.one * (baseScale * (1f - pressed * 0.12f));
            }
            private void OnDisable() { held = false; pressed = 0f; transform.localScale = Vector3.one * baseScale; }
        }
    }
}
