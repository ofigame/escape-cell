using System;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// A short comic-style dialogue between robot 47 and WARDEN over the blurred level scene.
    /// Lines type out with little voice blips; a tap finishes the line, the next tap moves on. SKIP ends it.
    /// </summary>
    public class StoryScreen : MonoBehaviour
    {
        private const float CharsPerSecond = 40f;

        private UiScreen screen;
        private TextMeshProUGUI chapter, speakerName, line, tapHint;
        private RectTransform bubble, robotAvatar, wardenAvatar, wardenMouth;
        private CanvasGroup robotGroup, wardenGroup;
        private Image bubbleRim, robotFace, wardenEye;

        private int scene, index;
        private float typed;
        private int lastBlip;
        private Action done;
        private float time, lineTime;

        public UiScreen Screen => screen;

        public static StoryScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("Story", canvasRoot, out var root);
            var story = root.gameObject.AddComponent<StoryScreen>();
            story.screen = screen;
            story.Build(root);
            return story;
        }

        private void Build(RectTransform root)
        {
            // The whole screen is the "next" button.
            var dim = UiFactory.Dim(root, new Color(0.05f, 0.04f, 0.14f, 0.55f));
            var tap = dim.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(Advance);

            chapter = UiFactory.TextBox("Chapter", root, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(960f, 90f), "", 56f, Palette.UiCyan, title: true);
            chapter.characterSpacing = 6f;
            chapter.raycastTarget = false;

            UiFactory.MakeButton(root, Loc.T("story.skip"), Kind.Secondary, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(220f, 100f), Finish, 44f);

            // Speech bubble
            bubble = UiFactory.Card("Bubble", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(940f, 520f));
            bubbleRim = UiFactory.Fill(UiFactory.Stretch("Rim", bubble), Palette.UiCyan, UiSprites.Ring, 0.9f);
            bubbleRim.raycastTarget = false;
            var namePill = UiFactory.Pill("Name", bubble, new Vector2(0.5f, 1f), new Vector2(0f, 40f), new Vector2(420f, 90f), new Color(0.1f, 0.08f, 0.22f, 0.95f));
            speakerName = UiFactory.Text(namePill, "", 44f, Palette.UiCyan);
            speakerName.characterSpacing = 4f;
            line = UiFactory.TextBox("Line", bubble, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(840f, 380f), "", 54f, Palette.UiText, FontStyles.Bold);
            line.textWrappingMode = TextWrappingModes.Normal;
            line.enableAutoSizing = true;
            line.fontSizeMin = 36f;
            line.fontSizeMax = 54f;
            line.raycastTarget = false;
            tapHint = UiFactory.TextBox("Tap", bubble, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(800f, 50f), Loc.T("story.tap"), 32f,
                new Color(1f, 1f, 1f, 0.6f), FontStyles.Normal);
            tapHint.raycastTarget = false;

            robotAvatar = BuildRobot(root);
            wardenAvatar = BuildWarden(root);
            robotGroup = robotAvatar.gameObject.AddComponent<CanvasGroup>();
            wardenGroup = wardenAvatar.gameObject.AddComponent<CanvasGroup>();
            robotGroup.blocksRaycasts = wardenGroup.blocksRaycasts = false;
        }

        /// <summary>Robot 47: the cube head with its visor and two cyan eyes, plus an antenna.</summary>
        private RectTransform BuildRobot(Transform root)
        {
            var avatar = UiFactory.Box("Robot", root, new Vector2(0.5f, 0.5f), new Vector2(-250f, -420f), new Vector2(300f, 300f));
            avatar.pivot = new Vector2(0.5f, 0.5f);
            var head = UiFactory.Box("Head", avatar, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(260f, 230f));
            head.pivot = new Vector2(0.5f, 0.5f);
            robotFace = UiFactory.Fill(head, Color.white, UiSprites.Rounded, 1.2f);
            robotFace.raycastTarget = false;
            var visor = UiFactory.Box("Visor", head, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(200f, 120f));
            visor.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(visor, new Color(0.2f, 0.22f, 0.36f), UiSprites.Rounded, 1.6f).raycastTarget = false;
            foreach (float x in new[] { -44f, 44f })
            {
                var eye = UiFactory.Box("Eye", visor, new Vector2(0.5f, 0.5f), new Vector2(x, 4f), new Vector2(38f, 46f));
                eye.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(eye, Palette.UiCyan, UiSprites.Rounded, 4f).raycastTarget = false;
            }
            var stick = UiFactory.Box("Antenna", avatar, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(14f, 50f));
            stick.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(stick, new Color(0.75f, 0.78f, 0.9f), UiSprites.Rounded, 10f).raycastTarget = false;
            var tip = UiFactory.Box("Tip", avatar, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(34f, 34f));
            tip.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(tip, Palette.UiGold, UiSprites.Circle).raycastTarget = false;
            return avatar;
        }

        /// <summary>WARDEN: a grumpy security monitor with one red eye and a mouth that flickers as it talks.</summary>
        private RectTransform BuildWarden(Transform root)
        {
            var avatar = UiFactory.Box("Warden", root, new Vector2(0.5f, 0.5f), new Vector2(250f, -420f), new Vector2(320f, 300f));
            avatar.pivot = new Vector2(0.5f, 0.5f);
            var stand = UiFactory.Box("Stand", avatar, new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(40f, 60f));
            stand.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(stand, new Color(0.2f, 0.18f, 0.3f), UiSprites.Rounded, 6f).raycastTarget = false;
            var monitor = UiFactory.Box("Monitor", avatar, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(300f, 230f));
            monitor.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(monitor, new Color(0.18f, 0.15f, 0.28f), UiSprites.Rounded, 1.2f).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", monitor), new Color(1f, 0.35f, 0.45f, 0.9f), UiSprites.Ring, 1.2f).raycastTarget = false;
            var glass = UiFactory.Box("Screen", monitor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 180f));
            glass.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(glass, new Color(0.3f, 0.06f, 0.12f), UiSprites.Rounded, 1.6f).raycastTarget = false;
            var brow = UiFactory.Box("Brow", glass, new Vector2(0.5f, 0.5f), new Vector2(0f, 62f), new Vector2(120f, 16f));
            brow.pivot = new Vector2(0.5f, 0.5f);
            brow.localRotation = Quaternion.Euler(0f, 0f, -8f);
            UiFactory.Fill(brow, new Color(1f, 0.4f, 0.45f), UiSprites.Rounded, 12f).raycastTarget = false;
            var eye = UiFactory.Box("Eye", glass, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(84f, 84f));
            eye.pivot = new Vector2(0.5f, 0.5f);
            wardenEye = UiFactory.Fill(eye, new Color(1f, 0.3f, 0.35f), UiSprites.Circle);
            wardenEye.raycastTarget = false;
            var pupil = UiFactory.Box("Pupil", eye, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            pupil.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(pupil, new Color(1f, 0.9f, 0.9f), UiSprites.Circle).raycastTarget = false;
            wardenMouth = UiFactory.Box("Mouth", glass, new Vector2(0.5f, 0.5f), new Vector2(0f, -54f), new Vector2(110f, 12f));
            wardenMouth.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(wardenMouth, new Color(1f, 0.4f, 0.45f), UiSprites.Rounded, 12f).raycastTarget = false;
            return avatar;
        }

        /// <summary>Plays a scene; <paramref name="onDone"/> runs after the last line (or SKIP).</summary>
        public void Play(int sceneIndex, int world, Action onDone)
        {
            scene = sceneIndex;
            done = onDone;
            robotFace.color = RobotLooks.BodyColor(Mathf.Min(world, LevelCatalog.WorldCount - 1)) * 1.12f;
            chapter.text = scene == Story.Ending ? Loc.T("story.end") : Loc.F("story.floor", scene + 1);
            screen.Show();
            ShowLine(0);
        }

        public void Hide() => screen.Hide(true);

        private void ShowLine(int i)
        {
            index = i;
            typed = 0f;
            lastBlip = 0;
            lineTime = 0f;
            var who = Story.SpeakerOf(scene, i);
            line.text = Story.Line(scene, i);
            line.maxVisibleCharacters = 0;
            line.fontStyle = who == Speaker.Narrator ? FontStyles.Italic : FontStyles.Bold;

            Color accent = who == Speaker.Warden ? new Color(1f, 0.45f, 0.5f) : who == Speaker.Robot ? Palette.UiCyan : Palette.UiGold;
            speakerName.text = who == Speaker.Warden ? Loc.T("story.warden") : who == Speaker.Robot ? Loc.T("story.robot") : "";
            speakerName.transform.parent.gameObject.SetActive(who != Speaker.Narrator);
            speakerName.color = accent;
            bubbleRim.color = accent;
            tapHint.gameObject.SetActive(false);
        }

        private void Advance()
        {
            if (!screen.IsVisible) return;
            int length = line.text.Length;
            if (typed < length)
            {
                typed = length; // first tap: show the whole line
                return;
            }
            AudioManager.PlaySfx(Sfx.Click, 0.5f);
            if (index + 1 < Story.LineCount(scene)) ShowLine(index + 1);
            else Finish();
        }

        private void Finish()
        {
            if (!screen.IsVisible) return;
            screen.Hide();
            var callback = done;
            done = null;
            callback?.Invoke();
        }

        private void Update()
        {
            if (!screen.IsVisible) return;
            float dt = Time.unscaledDeltaTime;
            time += dt;
            lineTime += dt;

            var who = Story.SpeakerOf(scene, index);
            int length = line.text.Length;
            if (typed < length)
            {
                typed = Mathf.Min(length, typed + dt * CharsPerSecond);
                int shown = Mathf.FloorToInt(typed);
                // Voice blips: robot chirps high, WARDEN grumbles low, the narrator stays quiet.
                if (who != Speaker.Narrator && shown / 3 > lastBlip)
                {
                    lastBlip = shown / 3;
                    AudioManager.PlaySfx(Sfx.Click, 0.22f, who == Speaker.Robot ? 1.6f : 0.6f, 0.1f);
                }
            }
            line.maxVisibleCharacters = Mathf.FloorToInt(typed);
            bool talking = typed < length;
            tapHint.gameObject.SetActive(!talking);
            if (!talking) tapHint.alpha = 0.4f + 0.3f * Mathf.Sin(time * 4f);

            // The speaker steps forward and bounces while talking; the other one waits in the shade.
            Pose(robotAvatar, robotGroup, who == Speaker.Robot, talking, -250f);
            Pose(wardenAvatar, wardenGroup, who == Speaker.Warden, talking, 250f);
            float mouth = who == Speaker.Warden && talking ? 12f + Mathf.Abs(Mathf.Sin(time * 22f)) * 30f : 12f;
            wardenMouth.sizeDelta = new Vector2(110f, mouth);
            wardenEye.rectTransform.localScale = Vector3.one * (Mathf.Repeat(time, 3.2f) < 0.12f ? 0.15f : 1f); // blink

            float pop = lineTime < 0.18f ? 1f + Mathf.Sin(lineTime / 0.18f * Mathf.PI) * 0.03f : 1f;
            bubble.localScale = new Vector3(pop, pop, 1f);
        }

        private void Pose(RectTransform avatar, CanvasGroup group, bool active, bool talking, float x)
        {
            float target = active ? 1.08f : 0.86f;
            float s = Mathf.Lerp(avatar.localScale.x, target, Time.unscaledDeltaTime * 10f);
            avatar.localScale = new Vector3(s, s, 1f);
            group.alpha = Mathf.Lerp(group.alpha, active ? 1f : 0.45f, Time.unscaledDeltaTime * 10f);
            float bob = active && talking ? Mathf.Abs(Mathf.Sin(time * 11f)) * 14f : Mathf.Sin(time * 2f) * 4f;
            avatar.anchoredPosition = new Vector2(x, -420f + bob);
        }
    }
}
