using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The main menu's painted look (Resources/MenuArt): the top bar's pills, the profile badge, the logo, the level card
    /// and its two buttons, the dock bar and its icons, the side orbs. The pictures carry no words: the game writes its
    /// own (in the player's language) over them.
    /// </summary>
    public static class MenuSkin
    {
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (sprites.TryGetValue(name, out var s) && s != null) return s;
            var tex = Resources.Load<Texture2D>("MenuArt/" + name);
            s = tex != null ? Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
            sprites[name] = s;
            return s;
        }

        /// <summary>A picture laid on <paramref name="target"/>, filling it (touches pass through it).</summary>
        public static Image Paint(RectTransform target, string name)
        {
            var img = target.GetComponent<Image>();
            if (img == null) img = target.gameObject.AddComponent<Image>();
            img.sprite = Get(name);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A new picture box.</summary>
        public static RectTransform Picture(string name, Transform parent, string sprite, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var box = UiFactory.Box(name, parent, anchor, position, size);
            Paint(box, sprite);
            return box;
        }

        /// <summary>A side button's orb wears its painted ball; <paramref name="keepIcon"/> keeps the game's own icon on it
        /// (for the plain balls the design has no picture for).</summary>
        public static void Orb(Button button, string sprite, bool keepIcon = false, float grow = 1.12f)
        {
            var orb = (RectTransform)button.transform.Find("Orb");
            if (orb == null) return;
            var img = orb.GetComponent<Image>();
            if (img != null) { img.sprite = Get(sprite); img.type = Image.Type.Simple; img.color = Color.white; img.preserveAspect = true; }
            orb.sizeDelta *= grow;
            var rim = orb.Find("Rim");
            if (rim != null) rim.gameObject.SetActive(false);
            var icon = orb.Find("Icon");
            if (icon != null) icon.gameObject.SetActive(keepIcon);
        }

        /// <summary>A dock button: its icon standing free (no ball), larger, rising out of its tile.</summary>
        public static void DockIcon(Button button, string sprite, float size, float lift)
        {
            var orb = (RectTransform)button.transform.Find("Orb");
            if (orb == null) return;
            var img = orb.GetComponent<Image>();
            if (img != null) { img.sprite = Get(sprite); img.type = Image.Type.Simple; img.color = Color.white; img.preserveAspect = true; }
            orb.sizeDelta = new Vector2(size, size);
            orb.anchoredPosition += new Vector2(0f, lift);
            foreach (var n in new[] { "Rim", "Icon" }) { var c = orb.Find(n); if (c != null) c.gameObject.SetActive(false); }
        }

        /// <summary>A big button's face becomes the painted glossy bar; its word in white with a dark edge.</summary>
        public static void Face(Button button, string sprite, Color edge)
        {
            if (button.targetGraphic is Image img)
            {
                img.sprite = Get(sprite);
                img.type = Image.Type.Simple;
                img.color = Color.white;
                var e = img.transform.Find("Edge");
                if (e != null) e.gameObject.SetActive(false);
            }
            foreach (var t in button.GetComponentsInChildren<TextMeshProUGUI>(true)) Word(t, edge);
        }

        /// <summary>The menu's lettering: the display font, a dark edge round it.</summary>
        public static void Word(TextMeshProUGUI t, Color edge)
        {
            t.font = UiFactory.TitleFont;
            t.fontSharedMaterial = UiFactory.OutlinedTitle(edge);
        }
    }
}
