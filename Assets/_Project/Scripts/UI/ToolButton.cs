using System;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// One on-screen tool button in a bottom corner of the HUD: the tool's icon, its charges, a TRY badge for
    /// free trials, and a bar that drains while the tool is working.
    /// </summary>
    public class ToolButton : MonoBehaviour
    {
        public event Action Pressed;

        private RectTransform root, iconRoot;
        private Image face, activeFill;
        private TextMeshProUGUI charges, trialBadge;
        private GameObject activeBar, badge;
        private Tool? shown;
        private float punch;

        public RectTransform Rect => root;

        public static ToolButton Create(Transform parent, Vector2 anchor, Vector2 position)
        {
            var rt = UiFactory.Box("Tool", parent, anchor, position, new Vector2(170f, 170f));
            var tb = rt.gameObject.AddComponent<ToolButton>();
            tb.root = rt;
            tb.Build();
            return tb;
        }

        private void Build()
        {
            face = UiFactory.Chunky(root, UiFactory.GoldStyle);
            iconRoot = UiFactory.Stretch("Icon", root);

            badge = UiFactory.Box("Charges", root, new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(64f, 64f)).gameObject;
            ((RectTransform)badge.transform).pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(UiFactory.Rect("BadgeOutline", (RectTransform)badge.transform, Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f)), new Color(0.3f, 0.02f, 0.08f), UiSprites.Circle).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("BadgeFace", (RectTransform)badge.transform), new Color(1f, 0.3f, 0.36f), UiSprites.Circle).raycastTarget = false;
            charges = UiFactory.Text(badge.transform, "1", 38f, Color.white, title: true);
            charges.fontSharedMaterial = UiFactory.OutlinedTitle(new Color(0.3f, 0.02f, 0.08f));

            trialBadge = UiFactory.TextBox("Trial", root, new Vector2(0.5f, 0f), new Vector2(0f, -36f), new Vector2(170f, 40f), Loc.T("tool.try"), 28f, Palette.UiGold);
            activeBar = UiFactory.Bar(root, new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(150f, 12f), Palette.UiCyan, out activeFill).gameObject;

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Pressed?.Invoke());
            root.gameObject.SetActive(false);
        }

        /// <summary>null hides the button. <paramref name="active"/> 0..1 shows the tool still working.</summary>
        public void Set(Tool? tool, int count, bool trial, float active)
        {
            bool on = tool.HasValue;
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (!on) return;
            if (shown != tool)
            {
                shown = tool;
                for (int i = iconRoot.childCount - 1; i >= 0; i--) Destroy(iconRoot.GetChild(i).gameObject);
                DrawIcon(iconRoot, tool.Value);
            }
            charges.text = count.ToString();
            trialBadge.gameObject.SetActive(trial);
            bool working = active > 0f;
            activeBar.SetActive(working);
            if (working) UiFactory.SetBar(activeFill, active);
            face.color = count > 0 ? UiFactory.GoldStyle.face : new Color(0.45f, 0.42f, 0.55f);
            float s = 1f + punch * 0.2f;
            root.localScale = new Vector3(s, s, 1f);
        }

        public void Punch() => punch = 1f;

        private void Update() => punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 4f);

        /// <summary>A sword (the weapon bag's icon), drawn for a 110-unit box.</summary>
        public static void DrawSword(RectTransform parent)
        {
            var dark = UiFactory.TextDark;
            var root = UiFactory.Box("Sword", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
            root.pivot = new Vector2(0.5f, 0.5f);
            root.localRotation = Quaternion.Euler(0f, 0f, -40f);
            void Part(Vector2 pos, Vector2 size, Color c)
            {
                var r = UiFactory.Box("Part", root, new Vector2(0.5f, 0.5f), pos, size);
                r.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(r, c, UiSprites.Rounded, 8f).raycastTarget = false;
            }
            Part(new Vector2(0f, 14f), new Vector2(16f, 70f), Color.white);
            Part(new Vector2(0f, -24f), new Vector2(44f, 10f), dark);
            Part(new Vector2(0f, -38f), new Vector2(10f, 22f), dark);
            Part(new Vector2(0f, -50f), new Vector2(14f, 14f), dark);
        }

        /// <summary>Simple shape icons: a clock (slow motion), planks (bridge), a pulse ring (EMP).</summary>
        public static void DrawIcon(RectTransform parent, Tool tool)
        {
            var dark = UiFactory.TextDark;
            Vector2 c = new Vector2(0.5f, 0.5f);
            switch (tool)
            {
                case Tool.SlowMo:
                {
                    var ring = UiFactory.Box("Ring", parent, c, Vector2.zero, new Vector2(96f, 96f));
                    ring.pivot = c;
                    UiFactory.Fill(ring, dark, UiSprites.Ring, 0.45f).raycastTarget = false;
                    var hand = UiFactory.Box("Hand", parent, c, new Vector2(0f, 16f), new Vector2(12f, 38f));
                    hand.pivot = c;
                    UiFactory.Fill(hand, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    var hand2 = UiFactory.Box("Hand2", parent, c, new Vector2(12f, 0f), new Vector2(30f, 12f));
                    hand2.pivot = c;
                    UiFactory.Fill(hand2, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    break;
                }
                case Tool.Bridge:
                    for (int i = -1; i <= 1; i++)
                    {
                        var plank = UiFactory.Box("Plank", parent, c, new Vector2(0f, i * 26f), new Vector2(96f, 18f));
                        plank.pivot = c;
                        UiFactory.Fill(plank, dark, UiSprites.Rounded, 6f).raycastTarget = false;
                    }
                    foreach (float x in new[] { -38f, 38f })
                    {
                        var rail = UiFactory.Box("Rail", parent, c, new Vector2(x, 0f), new Vector2(10f, 84f));
                        rail.pivot = c;
                        UiFactory.Fill(rail, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    }
                    break;
                case Tool.Freeze:
                    // A snowflake: three crossed bars.
                    for (int k = 0; k < 3; k++)
                    {
                        var bar = UiFactory.Box("Flake", parent, c, Vector2.zero, new Vector2(14f, 96f));
                        bar.pivot = c;
                        bar.localRotation = Quaternion.Euler(0f, 0f, k * 60f);
                        UiFactory.Fill(bar, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    }
                    break;
                case Tool.Blast:
                {
                    var star = UiFactory.Box("Burst", parent, c, Vector2.zero, new Vector2(100f, 100f));
                    star.pivot = c;
                    UiFactory.Fill(star, dark, UiSprites.Star).raycastTarget = false;
                    break;
                }
                default:
                {
                    foreach (float size in new[] { 104f, 64f })
                    {
                        var ring = UiFactory.Box("Pulse", parent, c, Vector2.zero, new Vector2(size, size));
                        ring.pivot = c;
                        UiFactory.Fill(ring, dark, UiSprites.Ring, size > 80f ? 0.6f : 0.4f).raycastTarget = false;
                    }
                    var dot = UiFactory.Box("Core", parent, c, Vector2.zero, new Vector2(26f, 26f));
                    dot.pivot = c;
                    UiFactory.Fill(dot, dark, UiSprites.Circle).raycastTarget = false;
                    break;
                }
            }
        }
    }
}
