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
    /// drawn icons.
    /// </summary>
    public static class MenuArt
    {
        public enum Icon { Garage, Shop, Map, Guide, Chest, Swords, Friends, House, Profile, Calendar, Backpack }

        /// <summary>
        /// The logo in the game's card look (like the level card): a navy plate in a gold frame with a parchment on it,
        /// "foi" big in dark ink pressed into the paper, and "CELL" under it, spaced out between two thin rules.
        /// </summary>
        public static void Logo(Transform parent, Vector2 anchor, Vector2 position)
        {
            var root = UiFactory.Box("Logo", parent, anchor, position + new Vector2(0f, -22f), new Vector2(530f, 286f));
            UiFactory.Framed(root, UiFactory.CardColor, 1.1f).raycastTarget = false;
            var paper = UiFactory.Box("Parchment", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(490f, 246f));
            UiFactory.Fill(paper, Color.white, UiSprites.Parchment, 1.2f).raycastTarget = false;
            var ink = new Color(0.3f, 0.16f, 0.06f);
            var light = new Color(1f, 0.95f, 0.8f, 0.85f);
            // Pressed into the paper: a pale edge under the ink.
            Layer(paper, "foi", new Vector2(0f, 24f), 162f, light);
            var face = Layer(paper, "foi", new Vector2(0f, 28f), 162f, ink);
            face.enableVertexGradient = true;
            face.colorGradient = new VertexGradient(new Color(0.42f, 0.24f, 0.1f), new Color(0.42f, 0.24f, 0.1f), ink, ink);
            var cellLight = Layer(paper, "CELL", new Vector2(0f, -84f), 52f, light);
            cellLight.characterSpacing = 40f;
            var cell = Layer(paper, "CELL", new Vector2(0f, -81f), 52f, new Color(0.55f, 0.3f, 0.1f));
            cell.characterSpacing = 40f;
            foreach (float s in new[] { -1f, 1f })
            {
                var rule = UiFactory.Box("Rule", paper, new Vector2(0.5f, 0.5f), new Vector2(s * 160f, -84f), new Vector2(82f, 4f));
                UiFactory.Fill(rule, new Color(0.55f, 0.3f, 0.1f, 0.7f)).raycastTarget = false;
            }
        }

        private static TextMeshProUGUI Layer(Transform root, string text, Vector2 offset, float size, Color color)
        {
            var box = UiFactory.Box("Layer", root, new Vector2(0.5f, 0.5f), offset, new Vector2(1000f, size * 1.2f));
            box.pivot = new Vector2(0.5f, 0.5f);
            var t = UiFactory.Text(box, text, size, color, title: true);
            t.enableAutoSizing = false;
            t.fontSize = size;
            t.characterSpacing = text.Length > 4 ? 18f : 4f;
            return t;
        }

        // One outlined material per look, shared (one each per label was never let go, and drew each label on its own).
        private static readonly System.Collections.Generic.Dictionary<(Material, Color, float), Material> outlines = new System.Collections.Generic.Dictionary<(Material, Color, float), Material>();

        public static void Outline(TextMeshProUGUI t, Color color, float width)
        {
            var key = (t.fontSharedMaterial, color, width);
            if (!outlines.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(t.fontSharedMaterial);
                m.EnableKeyword("OUTLINE_ON");
                m.SetColor("_OutlineColor", color);
                m.SetFloat("_OutlineWidth", width);
                m.SetFloat("_FaceDilate", 0.1f);
                outlines[key] = m;
            }
            t.fontSharedMaterial = m;
        }

        /// <summary>
        /// A big glossy tile button: a coloured face over a darker lip (it looks pressable), an icon and a white label.
        /// </summary>
        public static Button Tile(Transform parent, string label, Icon icon, Color color, Vector2 anchor, Vector2 position, Vector2 size, Action onClick, float fontSize = 40f)
        {
            var root = UiFactory.Box("Tile " + label, parent, anchor, position, size);
            var face = UiFactory.Stretch("Face", root);
            var image = UiFactory.Fill(face, color, UiSprites.Rounded, 0.8f);
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

        /// <summary>
        /// A dock button: a clean coloured orb with its icon and a small label under it, for the menu's bottom bar.
        /// </summary>
        public static Button DockButton(Transform parent, string label, Icon icon, Color color, Vector2 anchor, Vector2 position, float size, Action onClick)
        {
            var root = UiFactory.Box("Dock " + label, parent, anchor, position, new Vector2(size * 1.5f, size + 56f));
            root.pivot = new Vector2(0.5f, 0.5f);
            var hit = UiFactory.Fill(root, new Color(1f, 1f, 1f, 0.001f));
            var orb = UiFactory.Box("Orb", root, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(size, size));
            orb.pivot = new Vector2(0.5f, 1f);
            OrbFace(orb, icon, color, size);
            var text = UiFactory.TextBox("Label", root, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(size * 1.5f, 50f), label, 32f, Color.white, title: true);
            Outline(text, new Color(0.05f, 0.03f, 0.15f, 0.8f), 0.2f);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                AudioManager.PlaySfx(Sfx.Click, 0.7f);
                Haptics.Light();
                onClick();
            });
            root.gameObject.AddComponent<ButtonPress>();
            return button;
        }

        /// <summary>The game look's orb: a gradient disc in the colour inside a double gold ring, the icon on it.</summary>
        private static void OrbFace(RectTransform orb, Icon icon, Color color, float size)
        {
            UiFactory.Fill(orb, color, UiSprites.CircleGradient).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", orb), Color.white, UiSprites.GoldRing).raycastTarget = false;
            var iconBox = UiFactory.Box("Icon", orb, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.52f, size * 0.52f));
            iconBox.pivot = new Vector2(0.5f, 0.5f);
            DrawIcon(iconBox, icon, color);
        }

        /// <summary>A lone orb with an icon (no label, no click): emblems on cards and banners.</summary>
        public static RectTransform Emblem(Transform parent, Icon icon, Color color, Vector2 anchor, Vector2 position, float size)
        {
            var orb = UiFactory.Box("Emblem", parent, anchor, position, new Vector2(size, size));
            orb.pivot = new Vector2(0.5f, 0.5f);
            OrbFace(orb, icon, color, size);
            return orb;
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
                case Icon.Guide:
                    // An open book with a question mark.
                    Part(box, c, new Vector2(-s * 0.21f, -s * 0.04f), new Vector2(s * 0.42f, s * 0.6f), Color.white, UiSprites.Rounded, 6f);
                    Part(box, c, new Vector2(s * 0.21f, -s * 0.04f), new Vector2(s * 0.42f, s * 0.6f), Color.white, UiSprites.Rounded, -6f);
                    Part(box, c, new Vector2(0f, -s * 0.04f), new Vector2(s * 0.05f, s * 0.6f), dark, UiSprites.Rounded);
                    Part(box, c, new Vector2(-s * 0.21f, s * 0.08f), new Vector2(s * 0.24f, s * 0.05f), dark, UiSprites.Rounded);
                    Part(box, c, new Vector2(-s * 0.21f, -s * 0.06f), new Vector2(s * 0.24f, s * 0.05f), dark, UiSprites.Rounded);
                    Part(box, c, new Vector2(s * 0.21f, s * 0.08f), new Vector2(s * 0.24f, s * 0.05f), dark, UiSprites.Rounded);
                    Part(box, c, new Vector2(s * 0.21f, -s * 0.06f), new Vector2(s * 0.24f, s * 0.05f), dark, UiSprites.Rounded);
                    break;
                case Icon.Profile:
                    // One player: a head and shoulders, a little star on the chest.
                    Part(box, c, new Vector2(0f, s * 0.2f), new Vector2(s * 0.42f, s * 0.42f), Color.white, UiSprites.Circle);
                    Part(box, c, new Vector2(0f, -s * 0.26f), new Vector2(s * 0.78f, s * 0.4f), Color.white, UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, -s * 0.24f), new Vector2(s * 0.18f, s * 0.18f), Palette.UiGold, UiSprites.Star);
                    break;
                case Icon.Friends:
                    // Two friends side by side: heads and shoulders, the one in front a touch bigger.
                    Part(box, c, new Vector2(s * 0.18f, s * 0.16f), new Vector2(s * 0.26f, s * 0.26f), new Color(0.75f, 0.85f, 1f), UiSprites.Circle);
                    Part(box, c, new Vector2(s * 0.18f, -s * 0.2f), new Vector2(s * 0.42f, s * 0.3f), new Color(0.75f, 0.85f, 1f), UiSprites.Rounded);
                    Part(box, c, new Vector2(-s * 0.12f, s * 0.12f), new Vector2(s * 0.3f, s * 0.3f), Color.white, UiSprites.Circle);
                    Part(box, c, new Vector2(-s * 0.12f, -s * 0.24f), new Vector2(s * 0.5f, s * 0.34f), Color.white, UiSprites.Rounded);
                    break;
                case Icon.House:
                    // A farmhouse: white walls, a red roof, a door, a little tree beside it.
                    Part(box, c, new Vector2(-s * 0.05f, s * 0.12f), new Vector2(s * 0.48f, s * 0.48f), new Color(0.85f, 0.26f, 0.2f), UiSprites.Rounded, 45f);
                    Part(box, c, new Vector2(-s * 0.05f, -s * 0.16f), new Vector2(s * 0.56f, s * 0.42f), new Color(0.96f, 0.93f, 0.86f), UiSprites.Rounded);
                    Part(box, c, new Vector2(-s * 0.05f, -s * 0.24f), new Vector2(s * 0.14f, s * 0.24f), new Color(0.5f, 0.32f, 0.2f), UiSprites.Rounded);
                    Part(box, c, new Vector2(s * 0.33f, -s * 0.06f), new Vector2(s * 0.26f, s * 0.26f), new Color(0.35f, 0.75f, 0.35f), UiSprites.Circle);
                    Part(box, c, new Vector2(s * 0.33f, -s * 0.28f), new Vector2(s * 0.06f, s * 0.2f), new Color(0.5f, 0.32f, 0.2f), UiSprites.Rounded);
                    break;
                case Icon.Chest:
                    // A treasure chest with a gold lock.
                    Part(box, c, new Vector2(0f, -s * 0.12f), new Vector2(s * 0.84f, s * 0.48f), new Color(0.72f, 0.42f, 0.22f), UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, s * 0.18f), new Vector2(s * 0.84f, s * 0.26f), new Color(0.85f, 0.52f, 0.28f), UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, s * 0.04f), new Vector2(s * 0.86f, s * 0.07f), Palette.UiGold, UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, -s * 0.02f), new Vector2(s * 0.18f, s * 0.22f), Palette.UiGold, UiSprites.Rounded);
                    break;
                case Icon.Calendar:
                {
                    // A calendar page: a red band on top with two rings, the days in rows, today marked in gold.
                    var red = new Color(0.9f, 0.27f, 0.25f);
                    var ink = new Color(0.3f, 0.32f, 0.45f);
                    Part(box, c, new Vector2(0f, -s * 0.06f), new Vector2(s * 0.8f, s * 0.74f), Color.white, UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, s * 0.22f), new Vector2(s * 0.8f, s * 0.2f), red, UiSprites.Rounded);
                    foreach (float x in new[] { -0.2f, 0.2f }) Part(box, c, new Vector2(s * x, s * 0.33f), new Vector2(s * 0.08f, s * 0.18f), ink, UiSprites.Rounded);
                    for (int row = 0; row < 3; row++)
                        for (int col = 0; col < 4; col++)
                        {
                            bool today = row == 1 && col == 2;
                            Part(box, c, new Vector2(s * (-0.255f + col * 0.17f), s * (0.03f - row * 0.15f)), new Vector2(s * (today ? 0.15f : 0.11f), s * (today ? 0.13f : 0.09f)),
                                today ? Palette.UiGold : new Color(ink.r, ink.g, ink.b, 0.55f), today ? UiSprites.Circle : UiSprites.Rounded);
                        }
                    break;
                }
                case Icon.Backpack:
                {
                    // A backpack: its body, the flap over the top with a gold buckle, a front pocket, the carrying loop.
                    var cloth = new Color(0.2f, 0.62f, 0.55f);
                    var deep = new Color(0.12f, 0.42f, 0.38f);
                    Part(box, c, new Vector2(0f, s * 0.36f), new Vector2(s * 0.26f, s * 0.2f), deep, UiSprites.Ring);
                    Part(box, c, new Vector2(0f, -s * 0.06f), new Vector2(s * 0.72f, s * 0.78f), cloth, UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, s * 0.17f), new Vector2(s * 0.74f, s * 0.3f), deep, UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, -s * 0.24f), new Vector2(s * 0.48f, s * 0.26f), deep, UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, -s * 0.2f), new Vector2(s * 0.36f, s * 0.04f), new Color(1f, 1f, 1f, 0.35f), UiSprites.Rounded);
                    Part(box, c, new Vector2(0f, s * 0.04f), new Vector2(s * 0.14f, s * 0.14f), Palette.UiGold, UiSprites.Rounded);
                    break;
                }
                case Icon.Swords:
                    // Two crossed swords.
                    foreach (float sgn in new[] { -1f, 1f })
                    {
                        Part(box, c, new Vector2(0f, s * 0.05f), new Vector2(s * 0.12f, s * 0.8f), new Color(0.88f, 0.9f, 0.96f), UiSprites.Rounded, sgn * 40f);
                        Part(box, c, new Vector2(sgn * s * 0.2f, -s * 0.22f), new Vector2(s * 0.32f, s * 0.09f), Palette.UiGold, UiSprites.Rounded, sgn * 40f);
                        Part(box, c, new Vector2(sgn * s * 0.27f, -s * 0.3f), new Vector2(s * 0.1f, s * 0.18f), new Color(0.5f, 0.3f, 0.2f), UiSprites.Rounded, sgn * 40f);
                    }
                    break;
            }
        }
    }
}
