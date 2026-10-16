using System;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.Kingdom
{
    /// <summary>
    /// The kingdom's screen (landscape): back on the left, the four resources and the population across the top as in
    /// Age of Empires IV, the walk-about (TPS) button on the right, a toast for news.
    /// </summary>
    public class KmUi : MonoBehaviour
    {
        public event Action BackPressed, TpsPressed;

        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform safe, toast;
        private TextMeshProUGUI food, wood, stone, gold, pop, toastText;
        private CanvasGroup toastGroup;
        private float toastLeft;
        private Vector2Int screenSeen;

        public static KmUi Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("KmUi", out var scaler);
            canvas.transform.SetParent(parent, false);
            canvas.sortingOrder = 30;
            var ui = canvas.gameObject.AddComponent<KmUi>();
            ui.canvas = canvas;
            ui.scaler = scaler;
            ui.Build();
            return ui;
        }

        private void Build()
        {
            UiFactory.Fit(scaler);
            safe = UiFactory.Stretch("Safe", canvas.transform);
            safe.gameObject.AddComponent<SquashBot.UI.SafeArea>();

            var back = Flat.Button(safe, "←", new Vector2(0f, 1f), new Vector2(80f, -64f), new Vector2(120f, 100f), Flat.CardHi, Flat.Text, () => { Click(); BackPressed?.Invoke(); }, 52f);
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0.5f);

            // The resources, as one bar across the top.
            var bar = Flat.Panel(safe, "Resources", new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(1040f, 96f), new Color(0.05f, 0.06f, 0.1f, 0.82f));
            bar.pivot = new Vector2(0.5f, 0.5f);
            food = Resource(bar, 0, new Color(0.92f, 0.3f, 0.25f));
            wood = Resource(bar, 1, new Color(0.62f, 0.42f, 0.22f));
            stone = Resource(bar, 2, new Color(0.7f, 0.7f, 0.72f));
            gold = Resource(bar, 3, new Color(1f, 0.8f, 0.25f));
            pop = Resource(bar, 4, new Color(0.45f, 0.7f, 1f));

            var tps = Flat.Button(safe, "TPS", new Vector2(1f, 1f), new Vector2(-100f, -64f), new Vector2(160f, 100f), Flat.CardHi, Flat.Text, () => { Click(); TpsPressed?.Invoke(); }, 34f);
            ((RectTransform)tps.transform).pivot = new Vector2(0.5f, 0.5f);

            toast = Flat.Panel(safe, "Toast", new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(900f, 84f), new Color(0.05f, 0.06f, 0.1f, 0.88f));
            toast.pivot = new Vector2(0.5f, 0.5f);
            toastText = Flat.Label(toast, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 80f), 32f, Flat.Text, TextAlignmentOptions.Center, bold: true);
            toastText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toast.gameObject.AddComponent<Canvas>(); // its fading redraws only itself
        }

        /// <summary>One resource: a coloured badge and its amount.</summary>
        private TextMeshProUGUI Resource(RectTransform bar, int i, Color colour)
        {
            float x = -416f + i * 208f;
            var dot = UiFactory.Box("Icon", bar, new Vector2(0.5f, 0.5f), new Vector2(x - 56f, 0f), new Vector2(46f, 46f));
            dot.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(dot, colour, i == 1 ? UiSprites.Rounded : UiSprites.Circle, 2f).raycastTarget = false;
            var t = Flat.Label(bar, "0", new Vector2(0.5f, 0.5f), new Vector2(x + 30f, 0f), new Vector2(130f, 80f), 36f, Flat.Text, TextAlignmentOptions.Left, bold: true);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.enableAutoSizing = false;
            return t;
        }

        private int[] shown = { -1, -1, -1, -1, -1, -1 };

        /// <summary>The bar (texts only touched when a number changed).</summary>
        public void SetResources(int f, int w, int s, int g, int people, int room)
        {
            Set(food, 0, f); Set(wood, 1, w); Set(stone, 2, s); Set(gold, 3, g);
            if (shown[4] != people || shown[5] != room) { shown[4] = people; shown[5] = room; pop.text = people + "/" + room; }
        }

        private void Set(TextMeshProUGUI t, int i, int v)
        {
            if (shown[i] == v) return;
            shown[i] = v;
            t.text = v.ToString("N0");
        }

        public void Toast(string text)
        {
            toastText.text = text;
            toastLeft = 3f;
            toastGroup.alpha = 1f;
        }

        private static void Click() => AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.1f);

        private void Update()
        {
            // The screen turned (or the window changed): the layout follows.
            var now = new Vector2Int(Screen.width, Screen.height);
            if (now != screenSeen) { screenSeen = now; UiFactory.Fit(scaler); }
            if (toastLeft > 0f)
            {
                toastLeft -= Time.unscaledDeltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastLeft / 0.4f);
            }
        }
    }
}
