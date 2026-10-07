using System.Collections.Generic;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The studio splash: the OFIGAME logo on plain white with faint clear ice crystals, carrying on seamlessly from the native launch screen
    /// (which shows the same logo and ice). Ice crystals twinkle around the logo, and the whole thing
    /// stays until the menu is ready (and at least a moment), then fades into the menu. A tap skips the wait.
    /// </summary>
    public class SplashScreen : MonoBehaviour, IPointerClickHandler
    {
        private const float MinHold = 1.4f;
        private const float FadeOut = 0.5f;

        private CanvasGroup group;
        private RectTransform logo;
        private RawImage logoImage;
        private readonly List<(RectTransform rt, Image img, float phase, float speed)> sparkles = new List<(RectTransform, Image, float, float)>();
        private float t, outT = -1f;
        private bool skipping;

        /// <summary>Set by the game once the menu stands; the splash then fades out (after its minimum time).</summary>
        public static bool Ready;

        public static void Show()
        {
            var texture = Resources.Load<Texture2D>("OfigameLogo");
            if (texture == null) return;
            var ice = Resources.Load<Texture2D>("IceBackground");

            var canvas = UiFactory.CreateCanvas("Splash", out var scaler);
            canvas.sortingOrder = 200;
            scaler.matchWidthOrHeight = Screen.width < Screen.height ? 0f : 1f;

            var splash = canvas.gameObject.AddComponent<SplashScreen>();
            splash.group = canvas.gameObject.AddComponent<CanvasGroup>();

            // White ice, filling the screen (cropped, never stretched).
            var bgRect = UiFactory.Stretch("Ice", canvas.transform);
            if (ice != null)
            {
                var bg = bgRect.gameObject.AddComponent<RawImage>();
                bg.texture = ice;
                var fitter = bgRect.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = ice.width / (float)ice.height;
            }
            else UiFactory.Fill(bgRect, Color.white);
            UiFactory.Fill(UiFactory.Stretch("Tap", canvas.transform), new Color(1f, 1f, 1f, 0.001f)); // catches the skip tap

            // Twinkling ice crystals scattered over the frost.
            var rng = new System.Random(5);
            for (int i = 0; i < 34; i++)
            {
                float size = 10f + (float)rng.NextDouble() * 26f;
                var rt = UiFactory.Box("Sparkle", canvas.transform, new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()), Vector2.zero, new Vector2(size, size));
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var img = UiFactory.Fill(rt, new Color(1f, 1f, 1f, 0f), UiSprites.Rounded, 6f);
                img.raycastTarget = false;
                splash.sparkles.Add((rt, img, (float)rng.NextDouble() * 6f, 1.5f + (float)rng.NextDouble() * 2.5f));
            }

            float width = 860f;
            float height = width * texture.height / texture.width;
            splash.logo = UiFactory.Box("Logo", canvas.transform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(width, height));
            splash.logo.pivot = new Vector2(0.5f, 0.5f);
            splash.logoImage = splash.logo.gameObject.AddComponent<RawImage>();
            splash.logoImage.texture = texture;
            splash.logoImage.raycastTarget = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (t > 0.3f) skipping = true;
        }

        private void Update()
        {
            // Clamp: the first frames after loading can report a huge delta.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            t += dt;

            // The logo settles in with a tiny ease (it already sits there from the launch screen).
            float scale = Mathf.Lerp(0.97f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 1.2f), 3f));
            logo.localScale = new Vector3(scale, scale, 1f);
            foreach (var s in sparkles)
            {
                float a = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * s.speed + s.phase)), 6f);
                s.img.color = new Color(0.72f, 0.8f, 0.9f, a * 0.45f); // faint glassy glints on the white
                s.rt.localScale = Vector3.one * (0.6f + a * 0.6f);
            }

            if (outT < 0f && (skipping || (Ready && t >= MinHold))) outT = 0f;
            if (outT < 0f) return;
            outT += dt;
            group.alpha = 1f - Mathf.Clamp01(outT / FadeOut);
            if (outT >= FadeOut) Destroy(gameObject);
        }
    }
}
