using System;
using SquashBot.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// A small flat design kit for the newer screens (the army, the challenge's pickers and waiting room): one dark
    /// background, solid rounded cards, clear type sizes and plain colour buttons with no gradients, glows or lips —
    /// and fixed gaps, so texts never run into each other.
    /// </summary>
    public static class Flat
    {
        public static readonly Color Back = new Color(0.051f, 0.055f, 0.102f);
        public static readonly Color Card = new Color(0.094f, 0.102f, 0.169f);
        public static readonly Color CardHi = new Color(0.13f, 0.14f, 0.22f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Text = Color.white;
        public static readonly Color Muted = new Color(0.62f, 0.65f, 0.76f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.3f);
        public static readonly Color Blue = new Color(0.3f, 0.6f, 1f);
        public static readonly Color Red = new Color(1f, 0.36f, 0.34f);

        /// <summary>The space between things: one step is 8 units; most gaps are 2 or 3 steps.</summary>
        public const float Gap = 16f, Pad = 24f;

        /// <summary>A solid rounded panel. A larger <paramref name="round"/> rounds less.</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color colour, float round = 1.6f)
        {
            var r = UiFactory.Box(name, parent, anchor, position, size);
            UiFactory.Fill(r, colour, UiSprites.Rounded, round).raycastTarget = false;
            return r;
        }

        /// <summary>A text in a box of its own (it shrinks to fit rather than spill), left, centred or right.</summary>
        public static TextMeshProUGUI Label(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color colour,
            TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false, bool wrap = false)
        {
            var t = UiFactory.TextBox("Label", parent, anchor, position, size, text, fontSize, colour, bold ? FontStyles.Bold : FontStyles.Normal, title: bold, align: align);
            t.enableAutoSizing = true;
            t.fontSizeMax = fontSize;
            t.fontSizeMin = fontSize * 0.6f;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        /// <summary>A plain flat button: a solid rounded face, its label, a slight press.</summary>
        public static Button Button(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, Color face, Color textColour, Action onClick, float fontSize = 34f)
        {
            var root = UiFactory.Box("Button " + text, parent, anchor, position, size);
            var img = UiFactory.Fill(root, face, UiSprites.Rounded, 2.2f);
            var label = UiFactory.Text(root, text, fontSize, textColour, title: true);
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
            label.rectTransform.offsetMax = new Vector2(-10f, 0f);
            var b = root.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var c = b.colors;
            c.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
            c.pressedColor = new Color(0.82f, 0.82f, 0.86f);
            c.disabledColor = new Color(1f, 1f, 1f, 0.28f);
            c.fadeDuration = 0.06f;
            b.colors = c;
            b.onClick.AddListener(() => { AudioManager.PlaySfx(Sfx.Click, 0.7f); onClick?.Invoke(); });
            root.gameObject.AddComponent<ButtonPress>();
            return b;
        }

        /// <summary>A full-screen opaque background (over the safe area's edges too).</summary>
        public static void Background(Transform parent)
        {
            UiFactory.Dim(parent, Back);
        }
    }
}
