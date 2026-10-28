using System;
using SquashBot.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// A round duel button: a glossy face in its colour, a drawn icon, a ring round it (the power, the shield's
    /// durability, a move's time left), a grey sweep while it waits (filling clockwise) and a gold glow when ready. It
    /// acts the moment it is pressed (no waiting for the lift): it shrinks a little, ticks and buzzes. <see cref="Down"/>
    /// and <see cref="Up"/> tell a held press (the shield).
    /// </summary>
    public class DuelButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action Down, Up;

        private RectTransform rect;
        private Image face, icon, ringBack, ring, wait, glow;
        private Image[] parts;
        private TextMeshProUGUI badge;
        private CanvasGroup group;
        private bool usable = true, held, pulse;
        private float pressT, glowT, idleAlpha = 0.85f;
        private Color colour;

        public RectTransform Rect => rect;
        public bool Held => held;
        public bool Usable => usable;

        public static DuelButton Create(RectTransform parent, string name, float size, Color colour, DuelIcon icon, int ringParts = 0)
        {
            var rt = UiFactory.Box(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var b = rt.gameObject.AddComponent<DuelButton>();
            b.rect = rt;
            b.colour = colour;
            b.group = rt.gameObject.AddComponent<CanvasGroup>();
            // the glow behind (a ready ultimate), the ring's track and fill, the face, the icon, the waiting sweep
            b.glow = Img("Glow", rt, UiSprites.Circle, UiKit.A(UiKit.Style.gold, 0f), 1.32f);
            b.ringBack = Img("RingBack", rt, DuelIcons.Ring, UiKit.A(UiKit.Style.black, 0.45f), 1.18f);
            if (ringParts > 1)
            {
                b.parts = new Image[ringParts];
                float share = 1f / ringParts, gap = 0.025f;
                for (int i = 0; i < ringParts; i++)
                {
                    var p = Img("Part" + i, rt, DuelIcons.Ring, Color.white, 1.18f);
                    p.type = Image.Type.Filled;
                    p.fillMethod = Image.FillMethod.Radial360;
                    p.fillOrigin = (int)Image.Origin360.Top;
                    p.fillClockwise = true;
                    p.fillAmount = 0f;
                    p.rectTransform.localEulerAngles = new Vector3(0f, 0f, -360f * share * i - 360f * gap * 0.5f);
                    b.parts[i] = p;
                }
                b.ringShare = share - gap;
            }
            else
            {
                b.ring = Img("Ring", rt, DuelIcons.Ring, Color.white, 1.18f);
                b.ring.type = Image.Type.Filled;
                b.ring.fillMethod = Image.FillMethod.Radial360;
                b.ring.fillOrigin = (int)Image.Origin360.Top;
                b.ring.fillClockwise = true;
                b.ring.fillAmount = 0f;
            }
            var faceRt = UiFactory.Stretch("Face", rt);
            b.face = UiFactory.Chunky(faceRt, UiFactory.StyleOf(colour), 1f, 0f, UiSprites.Circle);
            b.face.raycastTarget = true;
            b.icon = Img("Icon", rt, DuelIcons.Get(icon), Color.white, 0.68f);
            b.wait = Img("Wait", rt, UiSprites.Circle, UiKit.A(UiKit.Style.backDeep, 0.55f), 1f);
            b.wait.type = Image.Type.Filled;
            b.wait.fillMethod = Image.FillMethod.Radial360;
            b.wait.fillOrigin = (int)Image.Origin360.Top;
            b.wait.fillClockwise = false;
            b.wait.fillAmount = 0f;
            return b;
        }

        private float ringShare = 1f;

        private static Image Img(string name, RectTransform parent, Sprite sprite, Color c, float scale)
        {
            var r = UiFactory.Box(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, parent.sizeDelta * scale);
            r.pivot = new Vector2(0.5f, 0.5f);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = c;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>A small count in the corner (the hearts left), or none.</summary>
        public void SetBadge(string text)
        {
            if (badge == null)
            {
                var b = UiFactory.Box("Badge", rect, new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(46f, 46f));
                b.pivot = new Vector2(1f, 1f);
                UiFactory.Fill(UiFactory.Stretch("Face", b), UiKit.A(UiKit.Style.back, 0.95f), UiSprites.Circle).raycastTarget = false;
                badge = UiFactory.OutlinedText("Count", b, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(46f, 42f), "", 28f, UiKit.Style.backDeep);
                badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            badge.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
            badge.text = text;
        }

        public void SetIcon(DuelIcon i) => icon.sprite = DuelIcons.Get(i);

        public void SetColour(Color c)
        {
            if (c == colour) return;
            colour = c;
            face.color = c;
        }

        /// <summary>The ring round it (0..1) in a colour.</summary>
        public void SetRing(float k, Color c)
        {
            if (ring == null) return;
            ring.fillAmount = Mathf.Clamp01(k);
            ring.color = c;
        }

        /// <summary>The ring in parts (the power bar's three): each part filled by its share of <paramref name="value"/>.</summary>
        public void SetParts(float value, Color full, Color filling)
        {
            if (parts == null) return;
            for (int i = 0; i < parts.Length; i++)
            {
                float k = Mathf.Clamp01(value - i);
                parts[i].fillAmount = k * ringShare;
                parts[i].color = k >= 1f ? full : filling;
            }
        }

        /// <summary>The grey sweep of a wait (1: just started .. 0: ready).</summary>
        public void SetWait(float k) => wait.fillAmount = Mathf.Clamp01(k);

        /// <summary>Usable or faded (and not taking presses).</summary>
        public void SetUsable(bool on)
        {
            if (on == usable) return;
            usable = on;
            Refresh();
        }

        /// <summary>The slow gold pulse of a ready move (never a fast blink).</summary>
        public void SetPulse(bool on) => pulse = on;

        /// <summary>How solid the button stands while not in use (the setting), and its size.</summary>
        public void SetLook(float alpha, float scale)
        {
            idleAlpha = alpha;
            rect.localScale = Vector3.one * scale;
            baseScale = scale;
            Refresh();
        }

        private float baseScale = 1f;

        private void Refresh()
        {
            if (group == null) return;
            group.alpha = usable ? (held ? 1f : idleAlpha) : idleAlpha * 0.5f;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!usable) return;
            held = true;
            pressT = 1f;
            Refresh();
            AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.35f);
            Haptics.Light();
            Down?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!held) return;
            held = false;
            Refresh();
            Up?.Invoke();
        }

        /// <summary>Let go from outside (the round over, the button hidden): the held press ends.</summary>
        public void Release()
        {
            if (!held) return;
            held = false;
            Refresh();
            Up?.Invoke();
        }

        private void OnDisable() => Release();

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var s = DuelControlsSettings.Current;
            float k = held ? 1f : Mathf.Clamp01(pressT);
            if (pressT > 0f) pressT -= dt / Mathf.Max(0.01f, s.pressSeconds * 2f);
            float scale = baseScale * Mathf.Lerp(1f, s.pressScale, held ? 1f : k);
            if (Mathf.Abs(rect.localScale.x - scale) > 0.0005f) rect.localScale = new Vector3(scale, scale, 1f);
            if (pulse)
            {
                glowT += dt * 2.2f;
                var c = glow.color; c.a = 0.35f + 0.25f * Mathf.Sin(glowT);
                glow.color = c;
            }
            else if (glow.color.a > 0f) { var c = glow.color; c.a = Mathf.Max(0f, c.a - dt * 3f); glow.color = c; }
        }
    }
}
