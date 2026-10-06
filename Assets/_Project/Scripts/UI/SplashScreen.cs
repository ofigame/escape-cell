using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// Studio splash on launch: the OFIGAME logo fades in on its navy backdrop, holds, then fades out to the menu.
    /// A tap skips it.
    /// </summary>
    public class SplashScreen : MonoBehaviour, IPointerClickHandler
    {
        private const float FadeIn = 0.45f;
        private const float Hold = 1.6f;
        private const float FadeOut = 0.45f;
        private static readonly Color Backdrop = new Color32(0x1A, 0x23, 0x34, 0xFF);

        private CanvasGroup group;
        private RectTransform logo;
        private float t;
        private bool skipping;

        public static void Show()
        {
            var texture = Resources.Load<Texture2D>("OfigameLogo");
            if (texture == null) return;

            var canvas = UiFactory.CreateCanvas("Splash", out var scaler);
            canvas.sortingOrder = 200;
            scaler.matchWidthOrHeight = Screen.width < Screen.height ? 0f : 1f;

            var splash = canvas.gameObject.AddComponent<SplashScreen>();
            splash.group = canvas.gameObject.AddComponent<CanvasGroup>();

            var bg = UiFactory.Fill(UiFactory.Stretch("Backdrop", canvas.transform), Backdrop);
            bg.raycastTarget = true;

            // Wide logo: 85% of the reference width, keeping its aspect.
            float width = 920f;
            float height = width * texture.height / texture.width;
            splash.logo = UiFactory.Box("Logo", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            var raw = splash.logo.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (t > 0.3f) skipping = true;
        }

        private void Update()
        {
            // Clamp: the first frames after loading can report a huge delta that would skip the logo.
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            if (skipping && t < FadeIn + Hold) t = FadeIn + Hold;

            float logoAlpha = t < FadeIn ? t / FadeIn : 1f;
            float scale = Mathf.Lerp(0.94f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(t / (FadeIn + Hold)), 3f));
            logo.localScale = new Vector3(scale, scale, 1f);
            logo.GetComponent<RawImage>().color = new Color(1f, 1f, 1f, logoAlpha);

            float outT = t - FadeIn - Hold;
            group.alpha = outT > 0f ? 1f - Mathf.Clamp01(outT / FadeOut) : 1f;
            if (outT >= FadeOut) Destroy(gameObject);
        }
    }
}
