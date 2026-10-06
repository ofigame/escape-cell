using System;
using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// The guide: every feature of the game explained in a sentence or two, grouped by topic. Opens from the menu's "?"
    /// button, and from the "?" on the shop, garage, map and city screens straight at their own topic.
    /// Texts are Loc keys "guide.&lt;topic&gt;.t" (title) and "guide.&lt;topic&gt;.b" (body).
    /// </summary>
    public class GuideScreen : MonoBehaviour
    {
        /// <summary>Topics in reading order; section headers start with '#'.</summary>
        public static readonly string[] Topics =
        {
            "#play", "move", "jump", "missions", "road", "stars", "lives", "armor", "rescue", "fire", "hover", "combo",
            "#floors", "rules", "boss", "events", "tools",
            "#bonus", "bonus", "tunnels", "daily", "chest",
            "#shop", "shop", "goal", "garage", "map",
            "#city", "city", "cityBuild", "cityWalk", "cityCare", "cityWishes", "cityPerks",
        };

        public event Action BackPressed;

        private UiScreen screen;
        private RectTransform content;
        private ScrollRect scroll;
        private readonly Dictionary<string, RectTransform> anchors = new Dictionary<string, RectTransform>();

        private TextMeshProUGUI Paragraph(string text, float size, Color color, FontStyles style, bool title)
        {
            var label = UiFactory.Text(content, text, size, color, TextAlignmentOptions.TopLeft, style, title);
            label.enableAutoSizing = false;
            label.fontSize = size;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private void Spacer(float height)
        {
            var gap = UiFactory.Rect("Gap", content, Vector2.zero, Vector2.one);
            gap.gameObject.AddComponent<LayoutElement>().minHeight = height;
        }

        public static GuideScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("Guide", canvasRoot, out var root);
            var guide = root.gameObject.AddComponent<GuideScreen>();
            guide.screen = screen;
            guide.Build(root);
            return guide;
        }

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.06f, 0.05f, 0.18f, 0.7f));
            // The card fills the screen under the top bar whatever its height (tablets are much shorter than phones),
            // so it never covers the back button.
            var card = UiFactory.Card("Card", root, new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            card.anchorMin = Vector2.zero;
            card.anchorMax = Vector2.one;
            card.offsetMin = new Vector2(40f, 40f);
            card.offsetMax = new Vector2(-40f, -190f);
            var viewport = UiFactory.Rect("Viewport", card, Vector2.zero, Vector2.one, new Vector2(16f, 16f), new Vector2(-16f, -16f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            content = UiFactory.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            // Paragraphs stack under each other at their own text height.
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 40);
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildBar(root);
            scroll = card.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;

            bool first = true;
            foreach (var topic in Topics)
            {
                bool section = topic[0] == '#';
                if (section && !first) Spacer(24f);
                first = false;
                string key = section ? "guide.sec." + topic.Substring(1) : "guide." + topic + ".t";
                var title = Paragraph(Loc.T(key), section ? 44f : 36f, section ? Palette.UiGold : Palette.UiCyan, FontStyles.Bold, section);
                if (section) title.characterSpacing = 4f;
                anchors[topic] = (RectTransform)title.transform;
                if (section) continue;
                var body = Paragraph(Loc.T("guide." + topic + ".b"), 31f, Palette.UiText, FontStyles.Normal, false);
                body.lineSpacing = 4f;
                Spacer(14f);
            }
        }

        private void BuildBar(RectTransform root)
        {
            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -170f), Vector2.zero);
            UiFactory.Fill(bar, new Color(0.08f, 0.07f, 0.18f, 0.96f));
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(120f, 120f), () => BackPressed?.Invoke(), 64f);
            UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 110f), Loc.T("guide.title"), 64f, Palette.UiText, title: true)
                .rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.MakeButton(bar, "X", Kind.Icon, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(120f, 120f), () => BackPressed?.Invoke(), 56f);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // The Android back button arrives as Escape.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (screen.IsVisible && kb != null && kb.escapeKey.wasPressedThisFrame) BackPressed?.Invoke();
#endif
        }

        /// <summary>Opens the guide, scrolled to <paramref name="topic"/> (null = the top).</summary>
        public void Show(string topic = null)
        {
            screen.Show();
            transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float y = topic != null && anchors.TryGetValue(topic, out var a) ? -a.anchoredPosition.y - a.rect.height * (1f - a.pivot.y) - 10f : 0f;
            float max = Mathf.Max(0f, content.sizeDelta.y - ((RectTransform)content.parent).rect.height);
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, max));
            scroll.velocity = Vector2.zero;
        }

        public void Hide() => screen.Hide(true);

        public bool Visible => screen.IsVisible;
    }
}
