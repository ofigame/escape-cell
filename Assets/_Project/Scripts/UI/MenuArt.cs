using System;
using SquashBot.Audio;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The main menu's showpieces: the chunky 3D logo, the bright shortcut tiles (garage, shop, map) with little
    /// drawn icons, and the city mode card that stands on its own.
    /// </summary>
    public static class MenuArt
    {
        public enum Icon { Garage, Shop, Map, City }

        /// <summary>"ESCAPE CELL" in the display font: a dark extruded back, a bright gradient face with a thick outline.</summary>
        public static void Logo(Transform parent, Vector2 anchor, Vector2 position)
        {
            var root = UiFactory.Box("Logo", parent, anchor, position, new Vector2(1000f, 340f));
            root.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
            // The extrusion: a few dark copies stepping down make the letters look like thick blocks.
            for (int i = 4; i >= 1; i--)
            {
                var depth = Layer(root, new Vector2(0f, -i * 5f), Color.Lerp(new Color(0.07f, 0.32f, 0.45f), new Color(0.04f, 0.12f, 0.25f), i / 4f));
                Outline(depth, new Color(0.04f, 0.1f, 0.22f), 0.22f);
            }
            var face = Layer(root, Vector2.zero, Color.white);
            face.enableVertexGradient = true;
            face.colorGradient = new VertexGradient(Color.white, Color.white, new Color(0.45f, 0.95f, 1f), new Color(0.6f, 1f, 0.85f));
            Outline(face, new Color(0.05f, 0.14f, 0.3f), 0.22f);
        }

        private static TextMeshProUGUI Layer(Transform root, Vector2 offset, Color color)
        {
            var box = UiFactory.Box("Layer", root, new Vector2(0.5f, 0.5f), offset, new Vector2(1000f, 340f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var t = UiFactory.Text(box, "ESCAPE\nCELL", 168f, color, title: true);
            t.enableAutoSizing = false;
            t.fontSize = 168f;
            t.lineSpacing = -30f;
            t.characterSpacing = 2f;
            return t;
        }

        private static void Outline(TextMeshProUGUI t, Color color, float width)
        {
            var m = new Material(t.fontSharedMaterial);
            m.EnableKeyword("OUTLINE_ON");
            m.SetColor("_OutlineColor", color);
            m.SetFloat("_OutlineWidth", width);
            m.SetFloat("_FaceDilate", 0.1f);
            t.fontSharedMaterial = m;
        }

        /// <summary>
        /// A big glossy tile button: a coloured face over a darker lip (it looks pressable), an icon and a white label.
        /// </summary>
        public static Button Tile(Transform parent, string label, Icon icon, Color color, Vector2 anchor, Vector2 position, Vector2 size, Action onClick, float fontSize = 40f)
        {
            var root = UiFactory.Box("Tile " + label, parent, anchor, position, size);
            var shadow = UiFactory.Rect("Shadow", root, Vector2.zero, Vector2.one, new Vector2(-24f, -40f), new Vector2(24f, 10f));
            UiFactory.Fill(shadow, new Color(0.05f, 0.03f, 0.15f, 0.5f), UiSprites.Shadow, 0.8f).raycastTarget = false;
            var lip = UiFactory.Rect("Lip", root, Vector2.zero, Vector2.one, new Vector2(0f, -12f), Vector2.zero);
            UiFactory.Fill(lip, color * 0.62f + new Color(0f, 0f, 0f, 0.38f), UiSprites.Rounded, 0.8f).raycastTarget = false;
            var face = UiFactory.Stretch("Face", root);
            var image = UiFactory.Fill(face, color, UiSprites.Rounded, 0.8f);
            var gloss = UiFactory.Rect("Gloss", face, new Vector2(0f, 0.55f), Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, -8f));
            UiFactory.Fill(gloss, new Color(1f, 1f, 1f, 0.22f), UiSprites.Rounded, 1.2f).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", face), new Color(1f, 1f, 1f, 0.45f), UiSprites.Ring, 0.8f).raycastTarget = false;

            bool wide = size.x > size.y * 1.6f;
            var iconBox = wide
                ? UiFactory.Box("Icon", face, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(size.y * 0.62f, size.y * 0.62f))
                : UiFactory.Box("Icon", face, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(size.y * 0.42f, size.y * 0.42f));
            if (wide) iconBox.pivot = new Vector2(0f, 0.5f);
            DrawIcon(iconBox, icon, color);

            var text = wide
                ? UiFactory.TextBox("Label", face, new Vector2(0f, 0.5f), new Vector2(size.y * 0.62f + 44f, 0f), new Vector2(size.x - size.y * 0.62f - 64f, size.y * 0.8f), label, fontSize, Color.white, title: true, align: TextAlignmentOptions.Left)
                : UiFactory.TextBox("Label", face, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(size.x - 16f, size.y * 0.36f), label, fontSize, Color.white, title: true);
            if (wide) text.rectTransform.pivot = new Vector2(0f, 0.5f);
            Outline(text, color * 0.45f + new Color(0f, 0f, 0f, 0.55f), 0.25f);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.9f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.7f, 0.7f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                AudioManager.PlaySfx(Sfx.Click, 0.7f);
                Haptics.Light();
                onClick();
            });
            root.gameObject.AddComponent<ButtonPress>();
            return button;
        }

        // ---------- Icons, drawn from the UI's rounded shapes ----------

        private static Image Part(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color c, Sprite sprite, float rot = 0f)
        {
            var r = UiFactory.Box("Part", parent, anchor, pos, size);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.localRotation = Quaternion.Euler(0f, 0f, rot);
            var img = UiFactory.Fill(r, c, sprite, sprite == UiSprites.Rounded ? 2.5f : 1f);
            img.raycastTarget = false;
            return img;
        }

        private static void DrawIcon(RectTransform box, Icon icon, Color tint)
        {
            var c = new Vector2(0.5f, 0.5f);
            float s = box.sizeDelta.y;
            var dark = tint * 0.45f + new Color(0f, 0f, 0f, 0.55f);
            switch (icon)
            {
                case Icon.Garage:
                    // A robot head with glowing eyes.
                    Part(box, c, new Vector2(0f, -s * 0.02f), new Vector2(s * 0.86f, s * 0.74f), Color.white, UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, -s * 0.02f), new Vector2(s * 0.64f, s * 0.4f), dark, UiSprites.Rounded);
                    foreach (float x in new[] { -0.14f, 0.14f }) Part(box, c, new Vector2(s * x, -s * 0.02f), new Vector2(s * 0.12f, s * 0.16f), Palette.UiCyan, UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, s * 0.42f), new Vector2(s * 0.06f, s * 0.16f), Color.white, UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, s * 0.5f), new Vector2(s * 0.14f, s * 0.14f), Palette.UiGold, UiSprites.Circle);
                    break;
                case Icon.Shop:
                    // A shopping bag with a coin on it.
                    Part(box, c, new Vector2(0f, s * 0.3f), new Vector2(s * 0.42f, s * 0.42f), Color.white, UiSprites.Ring);
                    Part(box, c, new Vector2(0f, -s * 0.1f), new Vector2(s * 0.8f, s * 0.66f), Color.white, UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, -s * 0.1f), new Vector2(s * 0.36f, s * 0.36f), Palette.UiGold, UiSprites.Circle);
                    Part(box, c, new Vector2(0f, -s * 0.1f), new Vector2(s * 0.2f, s * 0.2f), new Color(1f, 0.93f, 0.6f), UiSprites.Circle);
                    break;
                case Icon.Map:
                    // A map pin over a folded map.
                    Part(box, c, new Vector2(0f, -s * 0.24f), new Vector2(s * 0.9f, s * 0.36f), new Color(1f, 1f, 1f, 0.85f), UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, s * 0.02f), new Vector2(s * 0.34f, s * 0.34f), Color.white, UiSprites.Rounded, 45f);
                    Part(box, c, new Vector2(0f, s * 0.2f), new Vector2(s * 0.56f, s * 0.56f), Color.white, UiSprites.Circle);
                    Part(box, c, new Vector2(0f, s * 0.2f), new Vector2(s * 0.24f, s * 0.24f), dark, UiSprites.Circle);
                    break;
                case Icon.City:
                    // A little skyline with lit windows.
                    float[] heights = { 0.5f, 0.86f, 0.64f, 0.98f, 0.56f };
                    for (int i = 0; i < heights.Length; i++)
                    {
                        float x = (i - 2) * s * 0.19f, h = heights[i] * s * 0.9f;
                        Part(box, new Vector2(0.5f, 0f), new Vector2(x, h * 0.5f), new Vector2(s * 0.17f, h), Color.white, UiSprites.Rounded);
                        for (int w = 0; w < (int)(h / (s * 0.17f)); w++)
                            Part(box, new Vector2(0.5f, 0f), new Vector2(x, s * 0.1f + w * s * 0.16f), new Vector2(s * 0.07f, s * 0.07f), (i + w) % 3 == 0 ? Palette.UiGold : dark, UiSprites.Rounded);
                    }
                    Part(box, new Vector2(0.5f, 0f), new Vector2(0f, s * 0.02f), new Vector2(s * 1.04f, s * 0.06f), Color.white, UiSprites.Rounded);
                    break;
            }
        }
    }
}
