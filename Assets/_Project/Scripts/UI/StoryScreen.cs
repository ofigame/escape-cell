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
    /// A short comic-style story scene over the blurred level: Bip (left) and vanG (right) talk, the silent Observer
    /// stands between them, and in the reveal scene the Masked Thief takes vanG's place and takes off its mask.
    /// Lines type out with little voice blips; a tap finishes the line, the next tap moves on. SKIP ends it.
    /// </summary>
    public class StoryScreen : MonoBehaviour
    {
        private const float CharsPerSecond = 40f;

        private UiScreen screen;
        private TextMeshProUGUI chapter, speakerName, line, tapHint;
        private RectTransform bubble, robotAvatar, wardenAvatar, wardenMouth, bipAvatar, thiefAvatar, thiefMask;
        private CanvasGroup robotGroup, wardenGroup, bipGroup, thiefGroup, lumiGroup;
        private RectTransform lumiAvatar;
        private Image bubbleRim, robotFace, wardenEye;
        private Image[] robotEyes;
        private Image dim;
        private int lastThiefLine;

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
            dim = UiFactory.Dim(root, new Color(0.05f, 0.04f, 0.14f, 0.55f));
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

            bipAvatar = BuildBip(root);
            wardenAvatar = BuildWarden(root);
            thiefAvatar = BuildThief(root);
            lumiAvatar = BuildLumi(root);
            lumiGroup = lumiAvatar.gameObject.AddComponent<CanvasGroup>();
            lumiGroup.blocksRaycasts = false;
            robotAvatar = BuildRobot(root);
            robotGroup = robotAvatar.gameObject.AddComponent<CanvasGroup>();
            wardenGroup = wardenAvatar.gameObject.AddComponent<CanvasGroup>();
            bipGroup = bipAvatar.gameObject.AddComponent<CanvasGroup>();
            thiefGroup = thiefAvatar.gameObject.AddComponent<CanvasGroup>();
            robotGroup.blocksRaycasts = wardenGroup.blocksRaycasts = bipGroup.blocksRaycasts = thiefGroup.blocksRaycasts = false;
            robotAvatar.localScale = Vector3.one * 0.62f;
        }

        /// <summary>The Observer: the cube head with its visor and two eyes, plus an antenna. It never speaks.</summary>
        private RectTransform BuildRobot(Transform root)
        {
            var avatar = UiFactory.Box("Robot", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -470f), new Vector2(300f, 300f));
            avatar.pivot = new Vector2(0.5f, 0.5f);
            var head = UiFactory.Box("Head", avatar, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(260f, 230f));
            head.pivot = new Vector2(0.5f, 0.5f);
            robotFace = UiFactory.Fill(head, Color.white, UiSprites.Rounded, 1.2f);
            robotFace.raycastTarget = false;
            var visor = UiFactory.Box("Visor", head, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(200f, 120f));
            visor.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(visor, new Color(0.2f, 0.22f, 0.36f), UiSprites.Rounded, 1.6f).raycastTarget = false;
            robotEyes = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                var eye = UiFactory.Box("Eye", visor, new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -44f : 44f, 4f), new Vector2(38f, 46f));
                eye.pivot = new Vector2(0.5f, 0.5f);
                robotEyes[i] = UiFactory.Fill(eye, Palette.UiCyan, UiSprites.Rounded, 4f);
                robotEyes[i].raycastTarget = false;
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

        /// <summary>Bip: a small, round old archive robot with a cracked screen face, green eyes and one rusty wheel.</summary>
        private RectTransform BuildBip(Transform root)
        {
            var avatar = UiFactory.Box("Bip", root, new Vector2(0.5f, 0.5f), new Vector2(-260f, -420f), new Vector2(280f, 300f));
            avatar.pivot = new Vector2(0.5f, 0.5f);
            var wheel = UiFactory.Box("Wheel", avatar, new Vector2(0.5f, 0.5f), new Vector2(0f, -112f), new Vector2(86f, 86f));
            wheel.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(wheel, new Color(0.62f, 0.4f, 0.28f), UiSprites.Circle).raycastTarget = false;
            var hub = UiFactory.Box("Hub", wheel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            hub.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(hub, new Color(0.35f, 0.27f, 0.24f), UiSprites.Circle).raycastTarget = false;
            var body = UiFactory.Box("Body", avatar, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(230f, 200f));
            body.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(body, new Color(0.93f, 0.86f, 0.7f), UiSprites.Rounded, 0.7f).raycastTarget = false;
            var screenBox = UiFactory.Box("Screen", body, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(176f, 128f));
            screenBox.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(screenBox, new Color(0.12f, 0.24f, 0.2f), UiSprites.Rounded, 1.4f).raycastTarget = false;
            foreach (float x in new[] { -34f, 34f })
            {
                var eye = UiFactory.Box("Eye", screenBox, new Vector2(0.5f, 0.5f), new Vector2(x, 6f), new Vector2(34f, 34f));
                eye.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(eye, new Color(0.55f, 1f, 0.6f), UiSprites.Circle).raycastTarget = false;
            }
            // The crack across the screen.
            foreach (var (pos, angle, len) in new[] { (new Vector2(40f, 30f), -55f, 70f), (new Vector2(58f, -4f), -20f, 40f) })
            {
                var crack = UiFactory.Box("Crack", screenBox, new Vector2(0.5f, 0.5f), pos, new Vector2(5f, len));
                crack.pivot = new Vector2(0.5f, 0.5f);
                crack.localRotation = Quaternion.Euler(0f, 0f, angle);
                UiFactory.Fill(crack, new Color(0.75f, 0.9f, 0.85f, 0.7f), UiSprites.Rounded, 20f).raycastTarget = false;
            }
            var stick = UiFactory.Box("Antenna", avatar, new Vector2(0.5f, 0.5f), new Vector2(-50f, 118f), new Vector2(10f, 44f));
            stick.pivot = new Vector2(0.5f, 0.5f);
            stick.localRotation = Quaternion.Euler(0f, 0f, 14f);
            UiFactory.Fill(stick, new Color(0.62f, 0.55f, 0.45f), UiSprites.Rounded, 10f).raycastTarget = false;
            var tip = UiFactory.Box("Tip", avatar, new Vector2(0.5f, 0.5f), new Vector2(-56f, 142f), new Vector2(26f, 26f));
            tip.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(tip, new Color(0.55f, 1f, 0.6f), UiSprites.Circle).raycastTarget = false;
            return avatar;
        }

        /// <summary>Lumi: the painter spirit, a soft glow in rings of light with a golden brush stroke across it.</summary>
        private RectTransform BuildLumi(Transform root)
        {
            var avatar = UiFactory.Box("Lumi", root, new Vector2(0.5f, 0.5f), new Vector2(-260f, -420f), new Vector2(280f, 280f));
            avatar.pivot = new Vector2(0.5f, 0.5f);
            foreach (var (size, alpha) in new[] { (260f, 0.18f), (190f, 0.35f), (120f, 0.9f) })
            {
                var ring = UiFactory.Box("Glow", avatar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                ring.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(ring, new Color(1f, 0.82f, 0.95f, alpha), UiSprites.Circle).raycastTarget = false;
            }
            var stroke = UiFactory.Box("Brush", avatar, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(170f, 18f));
            stroke.pivot = new Vector2(0.5f, 0.5f);
            stroke.localRotation = Quaternion.Euler(0f, 0f, 18f);
            UiFactory.Fill(stroke, Palette.UiGold, UiSprites.Rounded, 12f).raycastTarget = false;
            avatar.gameObject.SetActive(false);
            return avatar;
        }

        /// <summary>The Masked Thief: an old, scratched Observer head with a dark mask across its eyes.</summary>
        private RectTransform BuildThief(Transform root)
        {
            var avatar = UiFactory.Box("Thief", root, new Vector2(0.5f, 0.5f), new Vector2(250f, -420f), new Vector2(300f, 300f));
            avatar.pivot = new Vector2(0.5f, 0.5f);
            var head = UiFactory.Box("Head", avatar, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(260f, 230f));
            head.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(head, new Color(0.62f, 0.6f, 0.66f), UiSprites.Rounded, 1.2f).raycastTarget = false;
            var visor = UiFactory.Box("Visor", head, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(200f, 120f));
            visor.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(visor, new Color(0.18f, 0.18f, 0.26f), UiSprites.Rounded, 1.6f).raycastTarget = false;
            foreach (float x in new[] { -44f, 44f })
            {
                var eye = UiFactory.Box("Eye", visor, new Vector2(0.5f, 0.5f), new Vector2(x, 4f), new Vector2(38f, 46f));
                eye.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(eye, Palette.UiGold, UiSprites.Rounded, 4f).raycastTarget = false;
            }
            // Scratches of a long life.
            foreach (var (pos, angle) in new[] { (new Vector2(-96f, 70f), 30f), (new Vector2(92f, -78f), -40f), (new Vector2(70f, 84f), 70f) })
            {
                var scratch = UiFactory.Box("Scratch", head, new Vector2(0.5f, 0.5f), pos, new Vector2(6f, 46f));
                scratch.pivot = new Vector2(0.5f, 0.5f);
                scratch.localRotation = Quaternion.Euler(0f, 0f, angle);
                UiFactory.Fill(scratch, new Color(0.38f, 0.36f, 0.42f), UiSprites.Rounded, 20f).raycastTarget = false;
            }
            thiefMask = UiFactory.Box("Mask", head, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(250f, 70f));
            thiefMask.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(thiefMask, new Color(0.08f, 0.07f, 0.12f), UiSprites.Rounded, 2f).raycastTarget = false;
            foreach (float x in new[] { -44f, 44f })
            {
                var hole = UiFactory.Box("Hole", thiefMask, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(40f, 22f));
                hole.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(hole, new Color(1f, 0.85f, 0.4f), UiSprites.Rounded, 8f).raycastTarget = false;
            }
            return avatar;
        }

        /// <summary>Plays a scene; <paramref name="onDone"/> runs after the last line (or SKIP).</summary>
        public void Play(int sceneIndex, int world, Action onDone)
        {
            scene = sceneIndex;
            done = onDone;
            robotFace.color = RobotLooks.BodyColor(Mathf.Min(world, LevelCatalog.WorldCount - 1)) * 1.12f;
            // "GRİ KANAL · Test Hücresi": the floor and the scene's own title from the scenario.
            chapter.text = scene == Story.Ending ? Loc.T("story.end")
                : Loc.T(LevelCatalog.WorldKey(scene)) + "  ·  " + Loc.T("story.title." + scene);
            // The ending plays over its own scene (nature waking up), so the shade over it is light.
            dim.color = new Color(0.05f, 0.04f, 0.14f, scene == Story.Ending ? 0.12f : 0.55f);
            lastThiefLine = -1;
            for (int i = 0; i < Story.LineCount(scene); i++)
                if (Story.SpeakerOf(scene, i) == Speaker.Thief) lastThiefLine = i;
            screen.Show();
            ShowLine(0);
        }

        public void Hide() => screen.Hide(true);

        /// <summary>The Masked Thief stands where vanG usually does, until its part of the scene is over.</summary>
        private bool ThiefOnStage => lastThiefLine >= 0 && index <= lastThiefLine;

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

            Color accent = who == Speaker.VanG ? new Color(1f, 0.45f, 0.5f)
                : who == Speaker.Bip ? new Color(0.55f, 1f, 0.6f)
                : who == Speaker.Thief ? Palette.UiGold
                : who == Speaker.Lumi ? new Color(1f, 0.8f, 0.95f)
                : Palette.UiCyan;
            speakerName.text = who == Speaker.VanG ? Loc.T("story.warden")
                : who == Speaker.Bip ? Loc.T("story.bip")
                : who == Speaker.Thief ? Loc.T(scene >= Story.Reveal ? "story.firstObserver" : "story.kuzgun")
                : who == Speaker.Lumi ? Loc.T("story.lumi")
                : "";
            speakerName.transform.parent.gameObject.SetActive(who != Speaker.Narrator);
            speakerName.color = accent;
            bubbleRim.color = accent;
            tapHint.gameObject.SetActive(false);

            // Kuzgun wears his mask until the floor where he takes it off (its first line still shows it). Lumi speaks from
            // Bip's place.
            thiefMask.gameObject.SetActive(scene < Story.Reveal || (scene == Story.Reveal && i == 0));
            wardenAvatar.gameObject.SetActive(!ThiefOnStage);
            thiefAvatar.gameObject.SetActive(ThiefOnStage);
            bipAvatar.gameObject.SetActive(who != Speaker.Lumi);
            lumiAvatar.gameObject.SetActive(who == Speaker.Lumi);
            foreach (var eye in robotEyes) eye.color = Palette.UiCyan;
            Story.NotifyLine(scene, i);
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
                // Voice blips: Bip chirps high, the thief hums, vanG grumbles low, the narrator stays quiet.
                if (who != Speaker.Narrator && shown / 3 > lastBlip)
                {
                    lastBlip = shown / 3;
                    float pitch = who == Speaker.Bip ? 1.7f : who == Speaker.Lumi ? 1.3f : who == Speaker.Thief ? 1.0f : 0.6f;
                    AudioManager.PlaySfx(Sfx.Click, 0.22f, pitch, 0.1f);
                }
            }
            line.maxVisibleCharacters = Mathf.FloorToInt(typed);
            bool talking = typed < length;
            tapHint.gameObject.SetActive(!talking);
            if (!talking) tapHint.alpha = 0.4f + 0.3f * Mathf.Sin(time * 4f);

            // The speaker steps forward and bounces while talking; the others wait in the shade. The Observer only listens.
            Pose(bipAvatar, bipGroup, who == Speaker.Bip, talking, -260f, -420f, 1f);
            Pose(lumiAvatar, lumiGroup, who == Speaker.Lumi, talking, -260f, -420f, 1f);
            Pose(wardenAvatar, wardenGroup, who == Speaker.VanG, talking, 250f, -420f, 1f);
            Pose(thiefAvatar, thiefGroup, who == Speaker.Thief, talking, 250f, -420f, 1f);
            Pose(robotAvatar, robotGroup, who == Speaker.Narrator, false, 0f, -470f, 0.62f);
            float mouth = who == Speaker.VanG && talking ? 12f + Mathf.Abs(Mathf.Sin(time * 22f)) * 30f : 12f;
            wardenMouth.sizeDelta = new Vector2(110f, mouth);
            wardenEye.rectTransform.localScale = Vector3.one * (Mathf.Repeat(time, 3.2f) < 0.12f ? 0.15f : 1f); // blink

            float pop = lineTime < 0.18f ? 1f + Mathf.Sin(lineTime / 0.18f * Mathf.PI) * 0.03f : 1f;
            bubble.localScale = new Vector3(pop, pop, 1f);
        }

        private void Pose(RectTransform avatar, CanvasGroup group, bool active, bool talking, float x, float y, float size)
        {
            float target = (active ? 1.08f : 0.86f) * size;
            float s = Mathf.Lerp(avatar.localScale.x, target, Time.unscaledDeltaTime * 10f);
            avatar.localScale = new Vector3(s, s, 1f);
            group.alpha = Mathf.Lerp(group.alpha, active ? 1f : 0.45f, Time.unscaledDeltaTime * 10f);
            float bob = active && talking ? Mathf.Abs(Mathf.Sin(time * 11f)) * 14f : Mathf.Sin(time * 2f) * 4f;
            avatar.anchoredPosition = new Vector2(x, y + bob);
        }
    }
}
