using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BrickMuseum.UI
{
    /// <summary>Small uGUI factory: canvas, rounded panels, pill buttons, text. Reference resolution 1080x1920 (portrait).</summary>
    public static class Ui
    {
        public static readonly Color Ink = new Color(0.12f, 0.13f, 0.18f);
        public static readonly Color Yellow = new Color(1f, 0.84f, 0.1f);
        public static readonly Color Paper = new Color(1f, 1f, 1f, 0.95f);
        private static Sprite rounded;

        /// <summary>White rounded rectangle, 9-sliced.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int s = 96, r = 40;
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (s - r))), dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (s - r)));
                        float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                t.SetPixels32(px); t.Apply();
                return rounded = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            }
        }

        public static Canvas MakeCanvas(Transform parent, int order)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(parent, false);
            }
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors a rect: anchor point (0..1) and pivot both at `at`, offset in reference pixels.</summary>
        public static RectTransform Place(this RectTransform r, Vector2 at, Vector2 size, Vector2 offset)
        {
            r.anchorMin = r.anchorMax = r.pivot = at;
            r.sizeDelta = size;
            r.anchoredPosition = offset;
            return r;
        }

        public static RectTransform Fill(this RectTransform r, float margin = 0f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(margin, margin); r.offsetMax = new Vector2(-margin, -margin);
            return r;
        }

        public static Image Panel(Transform parent, string name, Color color, bool raycast = true)
        {
            var r = Rect(parent, name);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Plain(Transform parent, string name, Color color, bool raycast = true)
        {
            var r = Rect(parent, name);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, bool bold = false, bool title = false)
        {
            var r = Rect(parent, "Text");
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = title ? Fonts.Title : bold ? Fonts.Bold : Fonts.Body;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            LocPreprocessor.Attach(t);
            t.enableAutoSizing = true;
            t.fontSizeMin = size * 0.5f;
            t.fontSizeMax = size;
            return t;
        }

        public static Button Button(Transform parent, string label, Color bg, Color fg, float fontSize, UnityAction onClick)
        {
            var img = Panel(parent, "Button " + label, bg);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            b.colors = colors;
            b.onClick.AddListener(() => { Core.Sfx.Play("pick", 0.6f); onClick(); });
            var t = Text(img.transform, label, fontSize, fg, true);
            t.rectTransform.Fill(12f);
            ButtonIcon.Add(img.gameObject, t, Icons.ForLabel(label));
            return b;
        }

        /// <summary>New label, and the icon that goes with it.</summary>
        public static void SetLabel(this Button b, string text)
        {
            b.GetComponentInChildren<TextMeshProUGUI>().text = text;
            var bi = b.GetComponent<ButtonIcon>();
            if (bi != null) bi.Set(Icons.ForLabel(text));
        }

        /// <summary>Full-screen dim layer with a centred white card; returns the card.</summary>
        public static RectTransform Modal(Transform parent, string name, float width, float height, out GameObject root)
        {
            var dim = Plain(parent, name, new Color(0, 0, 0, 0.55f));
            dim.rectTransform.Fill();
            root = dim.gameObject;
            var card = Panel(dim.transform, "Card", Color.white);
            return card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(width, height), Vector2.zero);
        }

        /// <summary>Lays children out top to bottom inside a card.</summary>
        public static void Stack(RectTransform card, float pad, float gap)
        {
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset((int)pad, (int)pad, (int)pad, (int)pad);
            v.spacing = gap;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;
        }

        public static T Height<T>(this T c, float h) where T : Component
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h; le.minHeight = h;
            return c;
        }
    }

    /// <summary>Forwards pointer events to delegates (drag areas, the bag, the gallery sticks).</summary>
    public sealed class PointerArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        public Action<PointerEventData> Down, Drag, Up;
        public Action<float> Scroll;
        public void OnPointerDown(PointerEventData e) => Down?.Invoke(e);
        public void OnDrag(PointerEventData e) => Drag?.Invoke(e);
        public void OnPointerUp(PointerEventData e) => Up?.Invoke(e);
        public void OnScroll(PointerEventData e) => Scroll?.Invoke(e.scrollDelta.y);
    }
}
