using System;
using System.Collections.Generic;
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
    /// The level map: the prison tower in its utopian city, one floor per world, with the level path climbing its face (level 1 at the bottom).
    /// Completed levels are filled, the next level pulses with the robot marker on it, the rest are locked.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        private const float NodeSpacing = 200f;
        private const float SectionPadding = 330f;
        private const float SectionHeight = SectionPadding + LevelCatalog.LevelsPerWorld * NodeSpacing;
        private const float PathAmplitude = 180f; // keeps the path on the tower's face

        public event Action<int> LevelChosen;
        public event Action BackPressed;
        public event Action BonusPressed;
        public event Action<int> StoryPressed;

        private UiScreen screen;
        private ScrollRect scroll;
        private RectTransform content;
        private RectTransform marker;
        private TextMeshProUGUI coinsText;
        private TextMeshProUGUI livesText;
        private Button bonusButton;
        private TextMeshProUGUI bonusCount;
        private int builtStars = -1;
        private bool openAll, builtOpenAll;
        private readonly List<RectTransform> nodes = new List<RectTransform>();
        private readonly List<Texture2D> textures = new List<Texture2D>();

        private int levelCount;
        private int unlocked = -1;
        private int markerFrom, markerTo;
        private float markerT = 1f;
        private float scrollTarget = -1f;

        public UiScreen Screen => screen;

        public static MapScreen Create(Transform canvasRoot, int levelCount)
        {
            var screen = UiScreen.Create("Map", canvasRoot, out var root);
            var map = root.gameObject.AddComponent<MapScreen>();
            map.screen = screen;
            map.levelCount = levelCount;
            map.BuildFrame(root);
            return map;
        }

        private void BuildFrame(RectTransform root)
        {
            UiFactory.Fill(UiFactory.Stretch("Backdrop", root), new Color(0.08f, 0.07f, 0.18f, 1f)).gameObject.AddComponent<IgnoreSafeArea>();

            // Scrolling area
            // The scroll area sits between the top bar and the banner strip, so nothing shows through either.
            var viewport = UiFactory.Rect("Viewport", root, Vector2.zero, Vector2.one, new Vector2(0f, Monetization.Ads.BannerReserve), new Vector2(0f, -190f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f)); // catches drags

            content = UiFactory.Rect("Content", viewport, Vector2.zero, new Vector2(1f, 0f));
            content.pivot = new Vector2(0.5f, 0f);

            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;

            // Fixed top bar over the map
            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -190f), Vector2.zero);
            UiFactory.Fill(bar, new Color(0.08f, 0.07f, 0.18f, 0.97f)).raycastTarget = false;
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(124f, 124f), () => BackPressed?.Invoke(), 64f);
            var lives = UiFactory.Pill("Lives", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 100f), UiFactory.PillColor);
            lives.pivot = new Vector2(0.5f, 0.5f);
            lives.anchoredPosition = Vector2.zero;
            UIController.HeartIcon(lives, new Vector2(58f, 0f), 56f);
            livesText = UiFactory.TextBox("Value", lives, new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(190f, 90f),
                "", 44f, Palette.UiText, align: TextAlignmentOptions.Left);

            // A gift button next to the back arrow whenever bonus rounds are waiting.
            bonusButton = UiFactory.MakeButton(bar, "", Kind.Gold, new Vector2(0f, 0.5f), new Vector2(176f, 0f), new Vector2(124f, 124f), () => BonusPressed?.Invoke());
            GiftIcon(bonusButton.transform.Find("Face"));
            var badge = UiFactory.Box("Count", bonusButton.transform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(56f, 56f));
            badge.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(badge, Palette.UiRed, UiSprites.Circle).raycastTarget = false;
            bonusCount = UiFactory.Text(badge, "1", 34f, Color.white);
            bonusButton.gameObject.AddComponent<Pulse>();

            var coins = UiFactory.Pill("Coins", bar, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(240f, 100f), UiFactory.PillColor);
            UIController.CoinIcon(coins, new Vector2(56f, 0f));
            coinsText = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(130f, 90f),
                "0", 50f, Palette.UiGold, align: TextAlignmentOptions.Left);
        }

        /// <summary>(Re)builds the nodes for the current progress and scrolls to <paramref name="focusLevel"/>.</summary>
        public void Show(int unlockedLevel, int coins, int focusLevel, int animateFrom = -1)
        {
            int stars = Progress.TotalStars(levelCount);
            openAll = SaveData.TestMode;
            if (unlockedLevel != unlocked || stars != builtStars || openAll != builtOpenAll) Rebuild(unlockedLevel);
            builtOpenAll = openAll;
            builtStars = stars;
            int tokens = Progress.BonusTokens;
            bonusButton.gameObject.SetActive(tokens > 0);
            bonusCount.text = tokens.ToString();
            coinsText.text = coins.ToString();
            screen.Show();

            markerTo = Mathf.Clamp(unlockedLevel, 0, levelCount - 1);
            markerFrom = animateFrom >= 0 ? animateFrom : markerTo;
            markerT = animateFrom >= 0 ? -0.35f : 1f; // short pause before the hop
            PlaceMarker(markerFrom < markerTo ? 0f : 1f);
            scrollTarget = NodeY(Mathf.Clamp(focusLevel, 0, levelCount - 1));
            Canvas.ForceUpdateCanvases();
            JumpScrollTo(scrollTarget);
        }

        public void Hide() => screen.Hide();

        public void SetLives(string text) => livesText.text = text;

        private void Rebuild(int unlockedLevel)
        {
            unlocked = unlockedLevel;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            foreach (var t in textures) Destroy(t);
            textures.Clear();
            nodes.Clear();

            int worlds = Mathf.CeilToInt(levelCount / (float)LevelCatalog.LevelsPerWorld);
            content.sizeDelta = new Vector2(0f, worlds * SectionHeight + 120f);

            for (int w = 0; w < worlds; w++) BuildSection(w);
            BuildPathDots();
            for (int i = 0; i < levelCount; i++) nodes.Add(BuildNode(i));

            marker = BuildMarker();
        }

        // ---------- Sections ----------

        private void BuildSection(int world)
        {
            var theme = WorldTheme.ForWorld(world);
            var section = UiFactory.Rect($"World {world + 1}", content, Vector2.zero, new Vector2(1f, 0f));
            section.pivot = new Vector2(0.5f, 0f);
            section.anchoredPosition = new Vector2(0f, world * SectionHeight);
            section.sizeDelta = new Vector2(0f, SectionHeight);

            // This floor of the prison tower, rising out of the utopian city (cached, shared by every rebuild).
            int worldCount = Mathf.CeilToInt(levelCount / (float)LevelCatalog.LevelsPerWorld);
            var art = UtopiaArt.Floor(world, worldCount, 360, Mathf.RoundToInt(360 * SectionHeight / 1080f));
            var raw = UiFactory.Stretch("Art", section).gameObject.AddComponent<RawImage>();
            raw.texture = art;
            raw.raycastTarget = false;

            bool locked = world * LevelCatalog.LevelsPerWorld > unlocked && !openAll;
            if (locked)
            {
                var shade = UiFactory.Fill(UiFactory.Stretch("Locked", section), new Color(0.05f, 0.04f, 0.12f, 0.4f));
                shade.raycastTarget = false;
            }

            // Banner at the start of the world.
            var banner = UiFactory.Pill("Banner", section, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(820f, 150f),
                new Color(0.1f, 0.08f, 0.22f, 0.8f));
            var title = UiFactory.TextBox("Name", banner, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(780f, 70f),
                Loc.F("world", world + 1, Loc.T(theme.key)), 46f, theme.accent);
            title.characterSpacing = 4f;
            string sub = locked ? Loc.F("map.locked", world) : $"{world * LevelCatalog.LevelsPerWorld + 1} - {(world + 1) * LevelCatalog.LevelsPerWorld}";
            UiFactory.TextBox("Sub", banner, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(780f, 56f), sub, 34f,
                new Color(1f, 1f, 1f, 0.75f), FontStyles.Normal);
            if (locked) Lock(banner, new Vector2(0f, 0.5f), new Vector2(60f, 0f), 0.8f);
            else
            {
                // Tapping an open world's banner replays its story scene.
                var bannerImage = banner.GetComponent<Image>();
                bannerImage.raycastTarget = true;
                var replay = banner.gameObject.AddComponent<Button>();
                replay.targetGraphic = bannerImage;
                int captured = world;
                replay.onClick.AddListener(() =>
                {
                    AudioManager.PlaySfx(Sfx.Click, 0.7f);
                    StoryPressed?.Invoke(captured);
                });
                banner.gameObject.AddComponent<ButtonPress>();
            }
        }

        // ---------- Path ----------

        private static float NodeY(int level)
        {
            int world = level / LevelCatalog.LevelsPerWorld;
            int i = level % LevelCatalog.LevelsPerWorld;
            return world * SectionHeight + SectionPadding + i * NodeSpacing;
        }

        private static float NodeX(int level) => Mathf.Sin(level * 0.85f) * PathAmplitude;

        private static Vector2 NodePos(int level) => new Vector2(NodeX(level), NodeY(level));

        private void BuildPathDots()
        {
            for (int i = 0; i < levelCount - 1; i++)
            {
                var a = NodePos(i);
                var b = NodePos(i + 1);
                int dots = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(a, b) / 40f));
                bool done = i + 1 <= unlocked;
                for (int d = 1; d < dots; d++)
                {
                    var p = Vector2.Lerp(a, b, d / (float)dots);
                    var dot = UiFactory.Box("Dot", content, new Vector2(0.5f, 0f), p - new Vector2(0f, 9f), new Vector2(18f, 18f));
                    dot.pivot = new Vector2(0.5f, 0f);
                    UiFactory.Fill(dot, done ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0.25f), UiSprites.Circle).raycastTarget = false;
                }
            }
        }

        private RectTransform BuildNode(int level)
        {
            var theme = WorldTheme.ForWorld(level / LevelCatalog.LevelsPerWorld);
            bool completed = level < unlocked;
            bool current = level == unlocked;
            bool locked = level > unlocked && !openAll;
            float size = current ? 180f : 150f;

            var node = UiFactory.Box($"Level {level + 1}", content, new Vector2(0.5f, 0f), NodePos(level), new Vector2(size, size));
            node.pivot = new Vector2(0.5f, 0.5f);

            var shadow = UiFactory.Box("Shadow", node, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(size + 30f, size + 30f));
            UiFactory.Fill(shadow, new Color(0.03f, 0.02f, 0.1f, 0.4f), UiSprites.Shadow, 0.5f).raycastTarget = false;

            Color fill = locked ? new Color(0.12f, 0.1f, 0.25f, 0.85f) : current ? Color.white : theme.accent;
            var face = UiFactory.Fill(node, fill, UiSprites.Circle);
            var rim = UiFactory.Fill(UiFactory.Stretch("Rim", node), locked ? new Color(1f, 1f, 1f, 0.25f) : new Color(1f, 1f, 1f, 0.9f), UiSprites.Ring, 0.5f);
            rim.raycastTarget = false;

            if (locked)
            {
                Lock(node, new Vector2(0.5f, 0.5f), Vector2.zero, 1f);
            }
            else
            {
                UiFactory.Text(node, (level + 1).ToString(), current ? 72f : 60f, UiFactory.TextDark);
                if (completed) NodeStars(node, Progress.Stars(level));
                if (completed)
                {
                    var badge = UiFactory.Box("Done", node, new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(48f, 48f));
                    badge.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Fill(badge, Palette.UiGold, UiSprites.Circle).raycastTarget = false;
                    var inner = UiFactory.Box("Inner", badge, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
                    UiFactory.Fill(inner, new Color(1f, 0.95f, 0.7f), UiSprites.Circle).raycastTarget = false;
                }

                var button = node.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                int captured = level;
                button.onClick.AddListener(() =>
                {
                    AudioManager.PlaySfx(Sfx.Click, 0.7f);
                    LevelChosen?.Invoke(captured);
                });
                node.gameObject.AddComponent<ButtonPress>();
            }

            if (current) node.gameObject.AddComponent<Pulse>();
            return node;
        }

        /// <summary>Three little stars under a finished level, the earned ones gold.</summary>
        private static void NodeStars(RectTransform node, int stars)
        {
            for (int i = 0; i < 3; i++)
            {
                var star = UiFactory.Box("Star", node, new Vector2(0.5f, 0f), new Vector2((i - 1) * 46f, i == 1 ? -26f : -18f), new Vector2(50f, 50f));
                star.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(star, i < stars ? Palette.UiGold : new Color(0.1f, 0.08f, 0.22f, 0.75f), UiSprites.Star).raycastTarget = false;
            }
        }

        /// <summary>A gift box drawn from UI shapes.</summary>
        private static void GiftIcon(Transform face)
        {
            var dark = UiFactory.TextDark;
            var box = UiFactory.Box("Box", face, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(62f, 44f));
            UiFactory.Fill(box, dark, UiSprites.Rounded, 6f).raycastTarget = false;
            var lid = UiFactory.Box("Lid", face, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(74f, 18f));
            UiFactory.Fill(lid, dark, UiSprites.Rounded, 8f).raycastTarget = false;
            var ribbon = UiFactory.Box("Ribbon", face, new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(12f, 64f));
            UiFactory.Fill(ribbon, Palette.UiGold, UiSprites.Rounded, 12f).raycastTarget = false;
            foreach (float x in new[] { -14f, 14f })
            {
                var bow = UiFactory.Box("Bow", face, new Vector2(0.5f, 0.5f), new Vector2(x, 34f), new Vector2(24f, 18f));
                UiFactory.Fill(bow, dark, UiSprites.Circle).raycastTarget = false;
            }
        }

        /// <summary>A small padlock drawn from UI shapes.</summary>
        private static void Lock(Transform parent, Vector2 anchor, Vector2 position, float scale)
        {
            var root = UiFactory.Box("Lock", parent, anchor, position, new Vector2(70f, 80f) * scale);
            root.pivot = new Vector2(0.5f, 0.5f);
            var shackle = UiFactory.Box("Shackle", root, new Vector2(0.5f, 1f), new Vector2(0f, 4f * scale), new Vector2(46f, 50f) * scale);
            shackle.pivot = new Vector2(0.5f, 1f);
            UiFactory.Fill(shackle, new Color(1f, 1f, 1f, 0.75f), UiSprites.Ring, 0.35f / scale).raycastTarget = false;
            var body = UiFactory.Box("Body", root, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(70f, 50f) * scale);
            body.pivot = new Vector2(0.5f, 0f);
            UiFactory.Fill(body, new Color(1f, 1f, 1f, 0.85f), UiSprites.Rounded, 2.5f / scale).raycastTarget = false;
            var hole = UiFactory.Box("Hole", body, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 18f) * scale);
            UiFactory.Fill(hole, new Color(0.15f, 0.12f, 0.3f, 0.9f), UiSprites.Rounded, 6f).raycastTarget = false;
        }

        // ---------- Robot marker ----------

        private RectTransform BuildMarker()
        {
            var root = UiFactory.Box("Robot", content, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(110f, 110f));
            root.pivot = new Vector2(0.5f, 0f);

            var head = UiFactory.Box("Head", root, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(96f, 84f));
            head.pivot = new Vector2(0.5f, 0f);
            // The marker wears the color of the world the robot is currently in.
            int markerWorld = Mathf.Clamp(unlocked, 0, levelCount - 1) / LevelCatalog.LevelsPerWorld;
            UiFactory.Fill(head, RobotLooks.BodyColor(markerWorld) * 1.12f, UiSprites.Rounded, 3f).raycastTarget = false;
            var visor = UiFactory.Box("Visor", head, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(72f, 44f));
            UiFactory.Fill(visor, new Color(0.24f, 0.27f, 0.4f), UiSprites.Rounded, 4f).raycastTarget = false;
            foreach (float x in new[] { -16f, 16f })
            {
                var eye = UiFactory.Box("Eye", visor, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(14f, 16f));
                UiFactory.Fill(eye, Palette.UiCyan, UiSprites.Rounded, 12f).raycastTarget = false;
            }
            foreach (float x in new[] { -18f, 18f })
            {
                var leg = UiFactory.Box("Leg", root, new Vector2(0.5f, 0f), new Vector2(x, 4f), new Vector2(18f, 26f));
                leg.pivot = new Vector2(0.5f, 0f);
                UiFactory.Fill(leg, new Color(0.62f, 0.67f, 0.82f), UiSprites.Rounded, 10f).raycastTarget = false;
            }
            return root;
        }

        private void PlaceMarker(float t)
        {
            if (marker == null) return;
            var a = NodePos(markerFrom);
            var b = NodePos(markerTo);
            var p = Vector2.Lerp(a, b, t);
            float hop = Mathf.Sin(t * Mathf.PI) * 120f;
            float sizeOffset = 90f; // stand on top of the node circle
            marker.anchoredPosition = p + new Vector2(0f, sizeOffset + hop);
            marker.SetAsLastSibling();
        }

        // ---------- Scrolling ----------

        private void JumpScrollTo(float y)
        {
            float viewport = ((RectTransform)scroll.viewport).rect.height;
            float range = content.sizeDelta.y - viewport;
            if (range <= 0f) return;
            scroll.verticalNormalizedPosition = Mathf.Clamp01((y - viewport * 0.45f) / range);
            scroll.velocity = Vector2.zero;
        }

        private void Update()
        {
            if (markerT < 1f)
            {
                markerT += Time.unscaledDeltaTime / 0.7f;
                float t = Mathf.Clamp01(markerT);
                PlaceMarker(t * t * (3f - 2f * t));
            }
            else if (marker != null)
            {
                // Idle bob on the current node.
                PlaceMarker(1f);
                marker.anchoredPosition += new Vector2(0f, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)) * 14f);
            }
        }

        private void OnDestroy()
        {
            foreach (var t in textures) Destroy(t);
        }
    }

    /// <summary>Gentle breathing scale for the "play me next" node.</summary>
    public class Pulse : MonoBehaviour
    {
        private void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.06f;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
