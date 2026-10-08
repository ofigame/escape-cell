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
    /// The mission briefing at the start of a level: the floor is blurred behind, and a big card walks through what to
    /// do one step at a time, each with a little 3D scene (the briefing stage) above its sentence. Nothing moves on
    /// by itself: a tap (or NEXT) goes to the next step, the last one says START. SKIP jumps straight in.
    /// </summary>
    public class BriefingScreen : MonoBehaviour
    {
        /// <summary>Step i is now on show (the game puts its scene on the stage).</summary>
        public event Action<int> StepShown;
        public event Action Finished;

        private UiScreen screen;
        private RectTransform card;
        private RawImage view;
        private TextMeshProUGUI title, text, nextLabel;
        private RectTransform dots;
        private readonly List<Image> dotImages = new List<Image>();
        private List<string> steps = new List<string>();
        private int index;
        private float pop = 1f, shownAt;

        public bool IsOpen => screen.IsVisible;

        public static BriefingScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("Briefing", canvasRoot, out var root);
            var b = root.gameObject.AddComponent<BriefingScreen>();
            b.screen = screen;
            b.Build(root);
            return b;
        }

        private void Build(RectTransform root)
        {
            // Tapping anywhere moves on, like the NEXT button.
            var dim = UiFactory.Dim(root, new Color(0.04f, 0.03f, 0.12f, 0.35f));
            var tap = dim.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(Next);

            card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(940f, 1540f));
            card.pivot = new Vector2(0.5f, 0.5f);
            screen.SetPopTarget(card);
            var cardTap = card.gameObject.AddComponent<Button>();
            cardTap.transition = Selectable.Transition.None;
            cardTap.onClick.AddListener(Next);

            title = UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(620f, 90f), "", 54f, Palette.UiCyan, title: true);
            title.characterSpacing = 3f;
            UiFactory.MakeButton(card, Loc.T("brief.skip"), Kind.Secondary, new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(190f, 86f), Finish, 36f);

            // The stage: a soft glowing well the 3D scene sits in.
            var well = UiFactory.Box("Well", card, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(860f, 820f));
            UiFactory.Fill(well, new Color(0.04f, 0.04f, 0.09f, 0.66f) /* glass */, UiSprites.Rounded, 0.8f).raycastTarget = false;
            var viewRect = UiFactory.Box("View", well, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 820f));
            viewRect.pivot = new Vector2(0.5f, 0.5f);
            view = viewRect.gameObject.AddComponent<RawImage>();
            view.raycastTarget = false;

            text = UiFactory.TextBox("Text", card, new Vector2(0.5f, 1f), new Vector2(0f, -990f), new Vector2(840f, 260f), "", 50f, Palette.UiText);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 34f;
            text.fontSizeMax = 52f;

            dots = UiFactory.Box("Dots", card, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(600f, 30f));
            var next = UiFactory.MakeButton(card, "", Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(640f, 160f), Next, 70f);
            nextLabel = next.GetComponentInChildren<TextMeshProUGUI>();
        }

        /// <summary>Opens the briefing on its first step, showing the stage's picture.</summary>
        public void Show(IList<string> stepTexts, Texture stageTexture)
        {
            steps = new List<string>(stepTexts);
            view.texture = stageTexture;
            foreach (var d in dotImages) Destroy(d.gameObject);
            dotImages.Clear();
            for (int i = 0; i < steps.Count && steps.Count > 1; i++)
            {
                var d = UiFactory.Box("Dot", dots, new Vector2(0.5f, 0.5f), new Vector2((i - (steps.Count - 1) * 0.5f) * 48f, 0f), new Vector2(24f, 24f));
                d.pivot = new Vector2(0.5f, 0.5f);
                var img = UiFactory.Fill(d, Color.white, UiSprites.Circle);
                img.raycastTarget = false;
                dotImages.Add(img);
            }
            screen.Show();
            transform.SetAsLastSibling();
            index = -1;
            Next();
        }

        private void Next()
        {
            if (!screen.IsVisible) return;
            // A stray double tap must not race through the steps.
            if (index >= 0 && Time.unscaledTime - shownAt < 0.35f) return;
            if (index + 1 >= steps.Count)
            {
                Finish();
                return;
            }
            index++;
            shownAt = Time.unscaledTime;
            title.text = steps.Count > 1 ? Loc.F("brief.title.step", index + 1, steps.Count) : Loc.T("brief.title");
            text.text = steps[index];
            nextLabel.text = Loc.T(index == steps.Count - 1 ? "brief.start" : "brief.next");
            for (int i = 0; i < dotImages.Count; i++)
                dotImages[i].color = i == index ? Palette.UiCyan : new Color(1f, 1f, 1f, i < index ? 0.6f : 0.2f);
            pop = 0f;
            Audio.AudioManager.PlaySfx(Audio.Sfx.Click, 0.7f, 1f + index * 0.08f);
            StepShown?.Invoke(index);
        }

        private void Finish()
        {
            if (!screen.IsVisible) return;
            screen.Hide();
            Finished?.Invoke();
        }

        public void Hide() => screen.Hide(true);

        private void Update()
        {
            if (pop >= 1f) return;
            pop = Mathf.Min(1f, pop + Time.unscaledDeltaTime * 4f);
            float s = 0.92f + 0.08f * Mathf.Sin(pop * Mathf.PI * 0.5f);
            text.transform.localScale = new Vector3(s, s, 1f);
            text.alpha = pop;
            view.color = new Color(1f, 1f, 1f, Mathf.Clamp01(pop * 1.5f));
        }
    }
}
