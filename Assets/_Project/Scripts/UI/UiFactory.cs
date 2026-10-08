using System.Collections.Generic;
using System;
using SquashBot.Audio;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace SquashBot.UI
{
    /// <summary>Builds the UI from code: crisp TextMeshPro text, rounded glass cards and animated buttons.</summary>
    public static class UiFactory
    {
        public static readonly Color CardColor = new Color(0.05f, 0.05f, 0.1f, 0.8f);
        public static readonly Color PillColor = new Color(0.03f, 0.03f, 0.08f, 0.5f);
        public static readonly Color TextDark = new Color(0.13f, 0.12f, 0.29f);

        private static TMP_FontAsset font, titleFont;
        private static Material titleMaterial;

        /// <summary>
        /// Body text: Nunito (rounded and very readable; SemiBold for normal text, ExtraBold for bold). Titles and the logo:
        /// Paytone One. Both are open-licence (OFL) fonts in Resources/Fonts, turned into dynamic SDF fonts at startup so every
        /// Turkish letter is drawn on demand; the old built-in font stays as the last fallback for rare symbols.
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (font != null) return font;
                var builtIn = TMP_Settings.defaultFontAsset;
                if (builtIn == null) builtIn = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                font = Dynamic("Fonts/Nunito-SemiBold");
                var bold = Dynamic("Fonts/Nunito-ExtraBold");
                if (font == null) return font = builtIn;
                if (bold != null)
                {
                    if (font.fontWeightTable != null && font.fontWeightTable.Length > 7) font.fontWeightTable[7].regularTypeface = bold;
                    AddFallback(bold, builtIn);
                }
                // Scripts Nunito doesn't draw (Korean, Arabic) come from open-licence Noto fonts, when they are in Resources/Fonts.
                foreach (var extra in ScriptFonts) AddFallback(font, extra);
                AddFallback(font, builtIn);
                return font;
            }
        }

        /// <summary>The display font for titles, big numbers and the logo (falls back to the body font for missing letters).</summary>
        public static TMP_FontAsset TitleFont
        {
            get
            {
                if (titleFont != null) return titleFont;
                titleFont = Dynamic("Fonts/PaytoneOne-Regular");
                if (titleFont == null) return titleFont = Font;
                AddFallback(titleFont, Font);
                foreach (var extra in ScriptFonts) AddFallback(titleFont, extra);
                return titleFont;
            }
        }

        private static List<TMP_FontAsset> scriptFonts;

        /// <summary>Fallback fonts for other scripts: Korean (Noto Sans KR Bold) and Arabic (Noto Sans Arabic), if present.</summary>
        private static List<TMP_FontAsset> ScriptFonts
        {
            get
            {
                if (scriptFonts != null) return scriptFonts;
                scriptFonts = new List<TMP_FontAsset>();
                foreach (var path in new[] { "Fonts/NotoSansKR", "Fonts/NotoSansArabic" })
                {
                    var asset = Dynamic(path);
                    if (asset != null) scriptFonts.Add(asset);
                }
                return scriptFonts;
            }
        }

        private static void AddFallback(TMP_FontAsset asset, TMP_FontAsset fallback)
        {
            if (fallback == null || asset == fallback) return;
            if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            asset.fallbackFontAssetTable.Add(fallback);
        }

        private static TMP_FontAsset Dynamic(string path)
        {
            var source = Resources.Load<UnityEngine.Font>(path);
            if (source == null) return null;
            var asset = TMP_FontAsset.CreateFontAsset(source, 72, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset != null) asset.name = source.name;
            return asset;
        }

        /// <summary>Font material with a soft drop shadow (underlay) for big titles over the scene.</summary>
        private static Material TitleMaterial
        {
            get
            {
                if (titleMaterial != null) return titleMaterial;
                titleMaterial = new Material(TitleFont.material) { name = "Title Underlay" };
                titleMaterial.EnableKeyword("UNDERLAY_ON");
                titleMaterial.SetColor("_UnderlayColor", new Color(0.04f, 0.04f, 0.09f, 0.6f) /* glass */);
                titleMaterial.SetFloat("_UnderlayOffsetX", 0.6f);
                titleMaterial.SetFloat("_UnderlayOffsetY", -0.8f);
                titleMaterial.SetFloat("_UnderlaySoftness", 0.35f);
                // A thin dark outline keeps every title crisp over cards and the floor alike.
                titleMaterial.EnableKeyword("OUTLINE_ON");
                titleMaterial.SetFloat("_OutlineWidth", 0.08f);
                titleMaterial.SetColor("_OutlineColor", new Color(0.07f, 0.05f, 0.2f));
                titleMaterial.SetFloat("_FaceDilate", 0.1f);
                return titleMaterial;
            }
        }

        public static Canvas CreateCanvas(string name, out CanvasScaler scaler)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.pixelPerfect = false;

            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;

            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ---------- Layout ----------

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent) => Rect(name, parent, Vector2.zero, Vector2.one);

        /// <summary>A fixed-size box anchored at <paramref name="anchor"/> (also its pivot), offset by <paramref name="position"/>.</summary>
        public static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = Rect(name, parent, anchor, anchor);
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        public static Image Fill(RectTransform rt, Color color, Sprite sprite = null, float pixelsPerUnit = 1f)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = pixelsPerUnit;
            image.color = color;
            return image;
        }

        public static Image Dim(Transform parent, Color color)
        {
            var image = Fill(Stretch("Dim", parent), color);
            image.raycastTarget = true; // blocks clicks to whatever is behind
            image.gameObject.AddComponent<IgnoreSafeArea>(); // dim the whole screen, notch included
            return image;
        }

        /// <summary>A rounded glass card with a soft drop shadow and a faint rim.</summary>
        public static RectTransform Card(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var root = Box(name, parent, anchor, position, size);

            var shadow = Rect("Shadow", root, Vector2.zero, Vector2.one, new Vector2(-40f, -60f), new Vector2(40f, 24f));
            Fill(shadow, new Color(0f, 0f, 0.04f, 0.45f), UiSprites.Shadow, 0.6f).raycastTarget = false;

            // Frosted glass: a smoky see-through body (the scene glows through it), a light wash that fades down from the
            // top edge, and a fine bright rim. No flat colour slab.
            var bg = Stretch("Background", root);
            Fill(bg, CardColor, UiSprites.Rounded, 0.6f);
            var wash = Rect("Wash", root, new Vector2(0f, 0.55f), Vector2.one, new Vector2(2f, 0f), new Vector2(-2f, -2f));
            Fill(wash, new Color(1f, 1f, 1f, 0.07f), UiSprites.RoundedGradient, 0.7f).raycastTarget = false;
            var rim = Stretch("Rim", root);
            Fill(rim, new Color(1f, 1f, 1f, 0.3f), UiSprites.Ring, 0.6f).raycastTarget = false;
            var glint = Rect("Glint", root, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -3f), new Vector2(0f, -1f));
            Fill(glint, new Color(1f, 1f, 1f, 0.35f), UiSprites.Rounded, 6f).raycastTarget = false;
            return root;
        }

        public static RectTransform Pill(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var root = Box(name, parent, anchor, position, size);
            // Glass HUD chips: a see-through body, fully rounded ends and a fine bright rim.
            float ppu = Mathf.Clamp(88f / Mathf.Max(1f, Mathf.Min(size.x, size.y)), 0.25f, 3f);
            var body = Stretch("Body", root);
            Fill(body, color, UiSprites.Rounded, ppu).raycastTarget = false;
            var rim = Stretch("Rim", root);
            Fill(rim, new Color(1f, 1f, 1f, 0.22f), UiSprites.Ring, ppu).raycastTarget = false;
            return root;
        }

        // ---------- Text ----------

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Bold, bool title = false)
        {
            var rt = Stretch("Text", parent);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = title ? TitleFont : Font;
            if (title)
            {
                label.fontSharedMaterial = TitleMaterial;
                // The display font is heavy already; faux bold would blur it. Arabic comes from the lighter Noto
                // fallback, which does need the extra weight on titles and buttons (Korean ships as Noto Sans KR Bold).
                if (Data.Loc.Current != Data.Language.Arabic) style &= ~FontStyles.Bold;
            }
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            // Shrink to fit the box instead of spilling out (long Turkish words, big numbers, narrow phones).
            label.enableAutoSizing = true;
            label.fontSizeMax = size;
            label.fontSizeMin = size * 0.4f;
            // Arabic is shaped and laid out right to left on the fly; other text passes through untouched.
            label.textPreprocessor = new RtlTextPreprocessor(label);
            if (Data.Loc.IsRightToLeft)
            {
                if (align == TextAlignmentOptions.Left) label.alignment = TextAlignmentOptions.Right;
                else if (align == TextAlignmentOptions.TopLeft) label.alignment = TextAlignmentOptions.TopRight;
            }
            label.raycastTarget = false;
            return label;
        }

        public static TextMeshProUGUI TextBox(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            string text, float fontSize, Color color, FontStyles style = FontStyles.Bold, bool title = false,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var box = Box(name, parent, anchor, position, size);
            return Text(box, text, fontSize, color, align, style, title);
        }

        // ---------- Buttons ----------

        public enum ButtonKind
        {
            Primary,
            Secondary,
            Icon,
            /// <summary>Gold call to action (bonus rounds).</summary>
            Gold
        }

        /// <summary>
        /// A button colour family: the face colour (tinted over a top-to-bottom gradient), its glow, and the dark shade its
        /// label's soft shadow uses. Glass faces are frosted, see-through white instead of a colour.
        /// </summary>
        public struct ChunkyStyle
        {
            public Color face, lip, outline;
            public bool glass;
            public ChunkyStyle(string face, string glow, string shade, bool glass = false)
            {
                this.face = Hex(face);
                lip = Hex(glow);
                outline = Hex(shade);
                this.glass = glass;
            }
        }

        public static readonly ChunkyStyle CyanStyle = new ChunkyStyle("#22C3FF", "#22C3FF", "#05304E");
        public static readonly ChunkyStyle GoldStyle = new ChunkyStyle("#FFB424", "#FFA51A", "#5C3200");
        public static readonly ChunkyStyle PurpleStyle = new ChunkyStyle("#FFFFFF24", "#00000000", "#0A0820", glass: true);
        public static readonly ChunkyStyle GreenStyle = new ChunkyStyle("#2FD07A", "#2FD07A", "#073A1E");
        public static readonly ChunkyStyle RedStyle = new ChunkyStyle("#FF4F6A", "#FF4F6A", "#4E0A16");
        public static readonly ChunkyStyle DarkStyle = new ChunkyStyle("#0A0A1499", "#00000000", "#05050C", glass: true);

        /// <summary>A style from any colour (skills, worlds): that colour's face and glow, a deep shade of it for text.</summary>
        public static ChunkyStyle StyleOf(Color c) => new ChunkyStyle { face = c, lip = c, outline = new Color(c.r * 0.22f, c.g * 0.2f, c.b * 0.3f, 1f) };

        public static ChunkyStyle Style(ButtonKind kind) =>
            kind == ButtonKind.Primary ? CyanStyle : kind == ButtonKind.Gold ? GoldStyle : kind == ButtonKind.Icon ? DarkStyle : PurpleStyle;

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        /// <summary>
        /// A modern pressable surface inside the root: fully rounded (a pill, or a circle with the circle sprite), a soft
        /// glow in its own colour beneath it, the face over a gentle top-to-bottom gradient, and a fine bright edge. Glass
        /// styles are frosted see-through white with a brighter edge and no glow. Returns the face image (the button's
        /// target graphic; put content under it). A ppu of 0 or less rounds the ends fully.
        /// </summary>
        public static Image Chunky(RectTransform root, ChunkyStyle s, float ppu = 0f, float lip = 0f, Sprite sprite = null)
        {
            bool circle = sprite == UiSprites.Circle;
            float w = root.rect.width > 0f ? root.rect.width : root.sizeDelta.x;
            float h = root.rect.height > 0f ? root.rect.height : root.sizeDelta.y;
            if (ppu <= 0f) ppu = Mathf.Clamp(88f / Mathf.Max(1f, Mathf.Min(w, h)), 0.25f, 3f); // radius = half the height: pill ends
            if (!s.glass)
            {
                var glow = Rect("Glow", root, Vector2.zero, Vector2.one, new Vector2(-26f, -34f), new Vector2(26f, 12f));
                Fill(glow, new Color(s.lip.r, s.lip.g, s.lip.b, 0.42f), UiSprites.Shadow, 0.8f).raycastTarget = false;
            }
            else
            {
                var drop = Rect("Shadow", root, Vector2.zero, Vector2.one, new Vector2(-18f, -26f), new Vector2(18f, 6f));
                Fill(drop, new Color(0f, 0f, 0.05f, 0.25f), UiSprites.Shadow, 0.8f).raycastTarget = false;
            }
            var face = Stretch("Face", root);
            Sprite faceSprite = s.glass ? (circle ? UiSprites.Circle : UiSprites.Rounded) : (circle ? UiSprites.CircleGradient : UiSprites.RoundedGradient);
            var image = Fill(face, s.face, faceSprite, circle ? 1f : ppu);
            // A top sheen and a fine edge: light catching a polished (or frosted) surface.
            var sheen = Rect("Sheen", face, new Vector2(circle ? 0.18f : 0f, 0.5f), new Vector2(circle ? 0.82f : 1f, 1f), new Vector2(3f, 0f), new Vector2(-3f, -2f));
            Fill(sheen, new Color(1f, 1f, 1f, s.glass ? 0.07f : 0.16f), circle ? UiSprites.Circle : UiSprites.Rounded, circle ? 1f : ppu * 1.9f).raycastTarget = false;
            Fill(Stretch("Edge", face), new Color(1f, 1f, 1f, s.glass ? 0.38f : 0.3f), UiSprites.Ring, circle ? 0.35f : ppu).raycastTarget = false;
            return image;
        }

        private static readonly Dictionary<Color, Material> outlined = new Dictionary<Color, Material>();

        /// <summary>The display font with a fine outline in the given colour and a soft shadow under it.</summary>
        public static Material OutlinedTitle(Color colour)
        {
            if (outlined.TryGetValue(colour, out var m) && m != null) return m;
            m = new Material(TitleFont.material) { name = "Title Outline " + ColorUtility.ToHtmlStringRGB(colour) };
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.1f);
            m.SetColor("_OutlineColor", new Color(colour.r, colour.g, colour.b, 0.8f));
            m.SetFloat("_FaceDilate", 0.05f);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(colour.r * 0.5f, colour.g * 0.5f, colour.b * 0.5f, 0.55f));
            m.SetFloat("_UnderlayOffsetY", -0.7f);
            m.SetFloat("_UnderlayDilate", 0.15f);
            m.SetFloat("_UnderlaySoftness", 0.45f);
            outlined[colour] = m;
            return m;
        }

        private static Material softBody;

        /// <summary>The body font with a soft drop shadow (modern button labels).</summary>
        public static Material SoftBody
        {
            get
            {
                if (softBody != null) return softBody;
                softBody = new Material(Font.material) { name = "Body Soft Shadow" };
                softBody.EnableKeyword("UNDERLAY_ON");
                softBody.SetColor("_UnderlayColor", new Color(0f, 0f, 0.08f, 0.45f));
                softBody.SetFloat("_UnderlayOffsetY", -0.6f);
                softBody.SetFloat("_UnderlaySoftness", 0.5f);
                return softBody;
            }
        }

        /// <summary>White display text with a fine outline (labels over pictures and the floor: skill names, badges, floating words).</summary>
        public static TextMeshProUGUI OutlinedText(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string text, float fontSize, Color outline)
        {
            var t = TextBox(name, parent, anchor, position, size, text, fontSize, Color.white, title: true);
            t.fontSharedMaterial = OutlinedTitle(outline);
            return t;
        }

        public static Button MakeButton(Transform parent, string text, ButtonKind kind, Vector2 anchor, Vector2 position, Vector2 size, Action onClick, float fontSize = 64f)
        {
            var root = Box("Button " + text, parent, anchor, position, size);

            var style = Style(kind);
            var image = Chunky(root, style);
            var face = (RectTransform)image.transform;

            // A clean, heavy sans label in white with a soft shadow.
            var label = Text(face, text, fontSize * 0.86f, Color.white, style: FontStyles.Bold);
            label.fontSharedMaterial = SoftBody;
            label.characterSpacing = 2f;
            label.rectTransform.offsetMin = new Vector2(14f, 0f);
            label.rectTransform.offsetMax = new Vector2(-14f, 0f);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.9f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
            colors.fadeDuration = 0.08f;
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

        public static Image Bar(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, out Image fill)
        {
            var root = Box("Bar", parent, anchor, position, size);
            var bg = Fill(root, new Color(1f, 1f, 1f, 0.16f), UiSprites.Rounded, 4f);
            bg.raycastTarget = false;

            var fillRt = Stretch("Fill", root);
            fill = Fill(fillRt, color, UiSprites.Rounded, 4f);
            fill.raycastTarget = false;
            fillRt.anchorMax = new Vector2(0f, 1f);
            return bg;
        }

        public static void SetBar(Image fill, float value)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }
    }

    /// <summary>Squishes a button slightly while it is held, for tactile feedback.</summary>
    public class ButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private float target = 1f;

        public void OnPointerDown(PointerEventData eventData) => target = 0.93f;
        public void OnPointerUp(PointerEventData eventData) => target = 1f;
        public void OnPointerExit(PointerEventData eventData) => target = 1f;

        private void OnDisable()
        {
            target = 1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, target, Time.unscaledDeltaTime * 20f);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Fades and pops a screen in or out using unscaled time (works while the game is paused).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UiScreen : MonoBehaviour
    {
        private CanvasGroup group;
        private RectTransform content;
        private float t;
        private bool showing;

        public bool IsVisible => showing;

        public static UiScreen Create(string name, Transform parent, out RectTransform root)
        {
            root = UiFactory.Stretch(name, parent);
            root.gameObject.AddComponent<CanvasGroup>();
            var screen = root.gameObject.AddComponent<UiScreen>();
            screen.group = root.GetComponent<CanvasGroup>();
            screen.content = root;
            root.gameObject.SetActive(false);
            return screen;
        }

        /// <summary>The element that scales during the pop (defaults to the whole screen).</summary>
        public void SetPopTarget(RectTransform target) => content = target;

        public void Show()
        {
            gameObject.SetActive(true);
            showing = true;
            t = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;
            Apply();
        }

        public void Hide(bool instant = false)
        {
            showing = false;
            group.interactable = false;
            group.blocksRaycasts = false;
            if (instant || !gameObject.activeSelf)
            {
                gameObject.SetActive(false);
                return;
            }
            t = 1f;
        }

        private void Update()
        {
            if (showing)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / 0.3f);
            }
            else
            {
                t = Mathf.Max(0f, t - Time.unscaledDeltaTime / 0.15f);
                if (t <= 0f) gameObject.SetActive(false);
            }
            Apply();
        }

        private void Apply()
        {
            group.alpha = Mathf.Clamp01(t * 1.6f);
            float s = showing ? 0.9f + 0.1f * EaseOutBack(t) : 0.96f + 0.04f * t;
            content.localScale = new Vector3(s, s, 1f);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
