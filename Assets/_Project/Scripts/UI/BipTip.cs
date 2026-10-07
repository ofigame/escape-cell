using SquashBot.Audio;
using SquashBot.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// Bip's warnings during a level: a little speech bubble with Bip's cracked-screen face slides in at the lower left,
    /// says one short line ("A block is coming down on you, move!") and slides out again. It never blocks a touch.
    /// </summary>
    public class BipTip : MonoBehaviour
    {
        private const float ShowSeconds = 2.8f;

        private RectTransform root;
        private TextMeshProUGUI line;
        private CanvasGroup group;
        private float left, time;

        public static BipTip Create(Transform parent)
        {
            var rt = UiFactory.Box("BipTip", parent, new Vector2(0f, 0f), new Vector2(24f, 330f), new Vector2(640f, 150f));
            var tip = rt.gameObject.AddComponent<BipTip>();
            tip.root = rt;
            tip.group = rt.gameObject.AddComponent<CanvasGroup>();
            tip.group.blocksRaycasts = false;
            tip.group.interactable = false;
            tip.group.alpha = 0f;
            tip.Build();
            return tip;
        }

        private void Build()
        {
            var bubble = UiFactory.Box("Bubble", root, new Vector2(0f, 0.5f), new Vector2(118f, 0f), new Vector2(520f, 120f));
            UiFactory.Fill(bubble, new Color(0.08f, 0.16f, 0.13f, 0.88f), UiSprites.Rounded, 0.8f).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", bubble), new Color(0.55f, 1f, 0.6f, 0.9f), UiSprites.Ring, 0.8f).raycastTarget = false;
            line = UiFactory.TextBox("Line", bubble, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 104f), "", 36f, Color.white,
                FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft);
            line.enableAutoSizing = true;
            line.fontSizeMin = 24f;
            line.fontSizeMax = 36f;
            line.raycastTarget = false;

            // Bip's face: the old archive unit's screen, cracked, with two green eyes.
            var face = UiFactory.Box("Face", root, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(124f, 116f));
            UiFactory.Fill(face, new Color(0.93f, 0.86f, 0.7f), UiSprites.Rounded, 1f).raycastTarget = false;
            var screen = UiFactory.Box("Screen", face, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 76f));
            screen.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(screen, new Color(0.12f, 0.24f, 0.2f), UiSprites.Rounded, 1.6f).raycastTarget = false;
            foreach (float x in new[] { -20f, 20f })
            {
                var eye = UiFactory.Box("Eye", screen, new Vector2(0.5f, 0.5f), new Vector2(x, 4f), new Vector2(20f, 20f));
                eye.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(eye, new Color(0.55f, 1f, 0.6f), UiSprites.Circle).raycastTarget = false;
            }
            var crack = UiFactory.Box("Crack", screen, new Vector2(0.5f, 0.5f), new Vector2(26f, 14f), new Vector2(4f, 40f));
            crack.pivot = new Vector2(0.5f, 0.5f);
            crack.localRotation = Quaternion.Euler(0f, 0f, -50f);
            UiFactory.Fill(crack, new Color(0.75f, 0.9f, 0.85f, 0.7f), UiSprites.Rounded, 20f).raycastTarget = false;
            var name = UiFactory.TextBox("Name", face, new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(124f, 34f), Loc.T("story.bip"), 26f,
                new Color(0.55f, 1f, 0.6f));
            name.raycastTarget = false;
        }

        /// <summary>Bip says <paramref name="text"/> for a few seconds.</summary>
        public void Say(string text)
        {
            line.text = text;
            left = ShowSeconds;
            AudioManager.PlaySfx(Sfx.Click, 0.35f, 1.7f, 0.1f);
        }

        public void Hide() => left = 0f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            time += dt;
            left -= dt;
            bool on = left > 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, on ? 1f : 0f, dt * 6f);
            float slide = (1f - group.alpha) * -120f;
            root.anchoredPosition = new Vector2(24f + slide, 330f + (on ? Mathf.Sin(time * 5f) * 3f : 0f));
        }
    }
}
