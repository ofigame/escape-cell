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
    /// The level map. The place itself is 3D (<see cref="MapWorld"/>): floating stepping stones winding up into the sky,
    /// a themed island at each world's start and the robot on the next level. This screen lays a thin layer on top:
    /// each visible stone gets its number, stars, goal badges or padlock and a button, following the stone on screen
    /// as the map is dragged; each island gets its world's sign (tap it to replay the world's story).
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        public event Action<int> LevelChosen;
        public event Action BackPressed;
        public event Action BonusPressed;
        public event Action<int> StoryPressed;

        /// <summary>The level data behind a node (for the special-goal badges), set by the game.</summary>
        public Func<int, LevelData> LevelOf;
        /// <summary>Builds the robot's look under a transform (the map's robot wears the player's outfit).</summary>
        public Func<Transform, GameObject> RobotLook;

        private UiScreen screen;
        private RectTransform overlay;
        private TextMeshProUGUI coinsText;
        private TextMeshProUGUI livesText;
        private Button bonusButton;
        private TextMeshProUGUI bonusCount;
        private MapWorld world;
        private int builtStars = -1;
        private bool openAll, builtOpenAll;
        private readonly List<RectTransform> nodes = new List<RectTransform>();
        private readonly List<RectTransform> banners = new List<RectTransform>();

        private int levelCount;
        private int unlocked = -1;

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
            // The map's 3D camera draws the background; this layer only catches drags and holds the buttons.
            overlay = UiFactory.Rect("Overlay", root, Vector2.zero, Vector2.one, new Vector2(0f, Monetization.Ads.BannerReserve), new Vector2(0f, -190f));
            UiFactory.Fill(overlay, new Color(0f, 0f, 0f, 0.001f));
            var drag = overlay.gameObject.AddComponent<MapDragArea>();
            drag.Dragged += px => world?.Drag(px);
            drag.Released += () => world?.EndDrag();

            // Fixed top bar over the map
            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -190f), Vector2.zero);
            UiFactory.Fill(bar, new Color(0.08f, 0.07f, 0.18f, 0.88f)).raycastTarget = false;
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

        /// <summary>(Re)builds the map for the current progress and shows it around <paramref name="focusLevel"/>.</summary>
        public void Show(int unlockedLevel, int coins, int focusLevel, int animateFrom = -1)
        {
            if (world == null) world = MapWorld.Create(levelCount, RobotLook);
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
            world.Open(focusLevel, animateFrom);
            LateUpdate();
        }

        public void Hide()
        {
            screen.Hide();
            if (world != null) world.Close();
        }

        /// <summary>True while the 3D map is on screen (the game's own camera can rest).</summary>
        public bool IsOpen => screen.IsVisible;

        public void SetLives(string text) => livesText.text = text;

        private void Rebuild(int unlockedLevel)
        {
            unlocked = unlockedLevel;
            foreach (var n in nodes) Destroy(n.gameObject);
            foreach (var b in banners) Destroy(b.gameObject);
            nodes.Clear();
            banners.Clear();
            world.Build(unlockedLevel, openAll);
            int worlds = Mathf.CeilToInt(levelCount / (float)LevelCatalog.LevelsPerWorld);
            for (int w = 0; w < worlds; w++) banners.Add(BuildBanner(w));
            for (int i = 0; i < levelCount; i++) nodes.Add(BuildNode(i));
        }

        /// <summary>A world's sign on its island: name and level range (or the stars it takes), tap to replay its story.</summary>
        private RectTransform BuildBanner(int w)
        {
            var theme = WorldTheme.ForWorld(w);
            bool locked = w * LevelCatalog.LevelsPerWorld > unlocked && !openAll;
            var banner = UiFactory.Pill("Banner " + (w + 1), overlay, new Vector2(0f, 0f), Vector2.zero, new Vector2(680f, 130f), new Color(0.04f, 0.04f, 0.09f, 0.6f) /* glass */);
            banner.pivot = new Vector2(0.5f, 0.5f);
            var title = UiFactory.TextBox("Name", banner, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(640f, 64f),
                Loc.F("world", w + 1, Loc.T(theme.key)), 42f, theme.accent, title: true);
            title.characterSpacing = 3f;
            string sub = locked ? Loc.F("map.locked", w) : $"{w * LevelCatalog.LevelsPerWorld + 1} - {(w + 1) * LevelCatalog.LevelsPerWorld}";
            UiFactory.TextBox("Sub", banner, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(640f, 48f), sub, 30f,
                new Color(1f, 1f, 1f, 0.75f), FontStyles.Normal);
            if (locked) Lock(banner, new Vector2(0f, 0.5f), new Vector2(54f, 0f), 0.7f);
            else
            {
                var image = banner.Find("Body").GetComponent<Image>();
                image.raycastTarget = true;
                var replay = banner.gameObject.AddComponent<Button>();
                replay.targetGraphic = image;
                int captured = w;
                replay.onClick.AddListener(() =>
                {
                    AudioManager.PlaySfx(Sfx.Click, 0.7f);
                    StoryPressed?.Invoke(captured);
                });
                banner.gameObject.AddComponent<ButtonPress>();
            }
            return banner;
        }

        /// <summary>The layer over one stone: its number (or padlock), stars, goal badges and a button.</summary>
        private RectTransform BuildNode(int level)
        {
            bool completed = level < unlocked;
            bool current = level == unlocked;
            bool locked = level > unlocked && !openAll;
            var node = UiFactory.Box($"Level {level + 1}", overlay, new Vector2(0f, 0f), Vector2.zero, new Vector2(180f, 150f));
            node.pivot = new Vector2(0.5f, 0.5f);
            var hit = UiFactory.Fill(node, new Color(1f, 1f, 1f, 0.001f), UiSprites.Circle);

            if (locked)
            {
                Lock(node, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), 0.75f);
            }
            else
            {
                if (current)
                {
                    // The robot stands on it: the number rides above in a bright tag.
                    var tag = UiFactory.Pill("Tag", node, new Vector2(0.5f, 1f), new Vector2(0f, 150f), new Vector2(150f, 74f), Color.white);
                    tag.pivot = new Vector2(0.5f, 0.5f);
                    UiFactory.Text(tag, (level + 1).ToString(), 50f, UiFactory.TextDark);
                    tag.gameObject.AddComponent<Pulse>();
                }
                else
                {
                    // Big glossy numbers: white fading to gold, a dark outline and a soft shadow, readable on any stone.
                    var num = UiFactory.Text(node, (level + 1).ToString(), 66f, Color.white, title: true);
                    num.rectTransform.anchoredPosition = new Vector2(0f, 8f);
                    num.fontSharedMaterial = NumberMaterial;
                    num.enableVertexGradient = true;
                    var gold = new Color(1f, 0.86f, 0.45f);
                    num.colorGradient = new VertexGradient(Color.white, Color.white, gold, gold);
                }
                if (completed) NodeStars(node, Progress.Stars(level), -26f);
                // Special goals (princess, monster, WARDEN, quest pieces, thief, Bip) show as badges by the stone.
                var (iconA, iconB) = MissionIcons.For(LevelOf?.Invoke(level));
                MissionIcons.Badge(node, iconA, new Vector2(0f, 1f), new Vector2(-6f, 10f), 70f);
                MissionIcons.Badge(node, iconB, new Vector2(0f, 1f), new Vector2(-62f, -22f), 60f);

                var button = node.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                int captured = level;
                button.onClick.AddListener(() =>
                {
                    AudioManager.PlaySfx(Sfx.Click, 0.7f);
                    LevelChosen?.Invoke(captured);
                });
                node.gameObject.AddComponent<ButtonPress>();
            }
            return node;
        }

        private static Material numberMaterial;

        /// <summary>The title font with a thick dark outline and a drop shadow, shared by every number on the map.</summary>
        private static Material NumberMaterial
        {
            get
            {
                if (numberMaterial != null) return numberMaterial;
                numberMaterial = new Material(UiFactory.TitleFont.material) { name = "Map Numbers" };
                numberMaterial.EnableKeyword("OUTLINE_ON");
                numberMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
                numberMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.14f, 0.08f, 0.3f));
                numberMaterial.EnableKeyword("UNDERLAY_ON");
                numberMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.05f, 0.02f, 0.12f, 0.7f));
                numberMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
                numberMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.8f);
                numberMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
                numberMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
                return numberMaterial;
            }
        }

        /// <summary>Keeps every label on its stone (and hides the ones off screen).</summary>
        private void LateUpdate()
        {
            if (world == null || !screen.IsVisible) return;
            for (int i = 0; i < nodes.Count; i++) Follow(nodes[i], world.ScreenPoint(i), 13f);
            for (int w = 0; w < banners.Count; w++) Follow(banners[w], world.IslandScreenPoint(w), 15f);
        }

        private void Follow(RectTransform rt, Vector3 sp, float refDistance)
        {
            bool on = sp.z > 0.5f && sp.y > -250f && sp.y < UnityEngine.Screen.height + 250f;
            if (rt.gameObject.activeSelf != on) rt.gameObject.SetActive(on);
            if (!on) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, sp, null, out var local);
            // Overlay children are anchored at its bottom-left corner.
            rt.anchoredPosition = local + overlay.rect.size * overlay.pivot;
            float s = Mathf.Clamp(refDistance / sp.z, 0.6f, 1.4f);
            rt.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Three little stars under a finished level, the earned ones gold.</summary>
        private static void NodeStars(RectTransform node, int stars, float y = -18f)
        {
            for (int i = 0; i < 3; i++)
            {
                var star = UiFactory.Box("Star", node, new Vector2(0.5f, 0f), new Vector2((i - 1) * 46f, i == 1 ? y - 8f : y), new Vector2(50f, 50f));
                star.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(star, i < stars ? Palette.UiGold : new Color(0.04f, 0.04f, 0.09f, 0.6f) /* glass */, UiSprites.Star).raycastTarget = false;
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
            UiFactory.Fill(hole, new Color(0.04f, 0.04f, 0.09f, 0.6f) /* glass */, UiSprites.Rounded, 6f).raycastTarget = false;
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
