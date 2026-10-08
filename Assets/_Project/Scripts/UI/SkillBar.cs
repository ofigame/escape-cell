using System;
using System.Collections.Generic;
using SquashBot.Data;
using SquashBot.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The skill bar over the bottom of the play screen: one round button per skill in the bag, with how many are
    /// left. A tap fires it. Only skills the bag holds are shown; the row stays centred.
    /// </summary>
    public class SkillBar : MonoBehaviour
    {
        private const float Size = 150f, Gap = 64f, IconBase = 92f;

        public event Action<PowerUpType> Pressed;

        private class Slot
        {
            public PowerUpType type;
            public RectTransform root;
            public TextMeshProUGUI count;
            public float punch;
        }

        private RectTransform root;
        private readonly List<Slot> slots = new List<Slot>();
        private bool visible;

        public static SkillBar Create(Transform parent)
        {
            // A column down the right edge, big enough to hit with a thumb, each skill named under its button.
            var rt = UiFactory.Box("SkillBar", parent, new Vector2(1f, 0.5f), new Vector2(-118f, 60f), new Vector2(Size, 900f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var bar = rt.gameObject.AddComponent<SkillBar>();
            bar.root = rt;
            foreach (var t in SkillBag.Order) bar.slots.Add(bar.MakeSlot(t));
            bar.Refresh(false);
            return bar;
        }

        public static Color ColorOf(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Super: return new Color(1f, 0.8f, 0.3f);
                case PowerUpType.Shield: return new Color(0.45f, 0.75f, 1f);
                case PowerUpType.Heart: return new Color(1f, 0.5f, 0.62f);
                case PowerUpType.Freeze: return new Color(0.6f, 0.9f, 1f);
                case PowerUpType.Blast: return new Color(1f, 0.6f, 0.35f);
                default: return new Color(1f, 0.45f, 0.5f);
            }
        }

        private Slot MakeSlot(PowerUpType t)
        {
            var s = new Slot { type = t };
            s.root = UiFactory.Box(t.ToString(), root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Size, Size));
            s.root.pivot = new Vector2(0.5f, 0.5f);
            // A modern round button: a soft glow in the skill's colour, a gradient face and a fine bright edge.
            var c = ColorOf(t);
            var style = UiFactory.StyleOf(c);
            var bg = UiFactory.Chunky(s.root, style, 1f, 0f, UiSprites.Circle);
            var icon = UiFactory.Box("Icon", s.root, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(IconBase, IconBase));
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.localScale = Vector3.one * (Size / IconBase * 0.92f);
            DrawIcon(icon, t);
            // The name on a small glass tag across the bottom of the button, edged in the skill's colour.
            var ribbon = UiFactory.Box("Ribbon", s.root, new Vector2(0.5f, 0f), new Vector2(0f, -20f), new Vector2(176f, 48f));
            ribbon.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(UiFactory.Stretch("RibbonFace", ribbon), new Color(0.03f, 0.03f, 0.08f, 0.72f), UiSprites.Rounded, 1.8f).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("RibbonRim", ribbon), new Color(c.r, c.g, c.b, 0.7f), UiSprites.Ring, 1.8f).raycastTarget = false;
            var name = UiFactory.OutlinedText("Name", ribbon, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(166f, 44f), Loc.T("skillName." + t), 28f, new Color(0.05f, 0.04f, 0.14f));
            name.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // How many are left: a red badge with a white number.
            var badge = UiFactory.Box("Badge", s.root, new Vector2(1f, 1f), new Vector2(14f, 14f), new Vector2(62f, 62f));
            badge.pivot = new Vector2(1f, 1f);
            UiFactory.Fill(UiFactory.Stretch("BadgeFace", badge), new Color(1f, 0.3f, 0.36f, 1f), UiSprites.Circle).raycastTarget = false;
            s.count = UiFactory.OutlinedText("Count", badge, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(60f, 56f), "", 38f, new Color(0.3f, 0.02f, 0.08f));
            s.count.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var button = s.root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => Pressed?.Invoke(t));
            s.root.gameObject.AddComponent<ButtonPress>();
            return s;
        }

        private static void DrawIcon(RectTransform icon, PowerUpType t)
        {
            var dark = UiFactory.TextDark;
            var c = new Vector2(0.5f, 0.5f);
            switch (t)
            {
                case PowerUpType.Super:
                {
                    var star = UiFactory.Box("Star", icon, c, Vector2.zero, new Vector2(72f, 72f));
                    star.pivot = c;
                    UiFactory.Fill(star, Color.white, UiSprites.Star).raycastTarget = false;
                    break;
                }
                case PowerUpType.Shield:
                {
                    var ring = UiFactory.Box("Ring", icon, c, Vector2.zero, new Vector2(64f, 64f));
                    ring.pivot = c;
                    UiFactory.Fill(ring, dark, UiSprites.Ring, 0.6f).raycastTarget = false;
                    break;
                }
                case PowerUpType.Heart:
                    UIController.HeartIcon(icon, new Vector2(IconBase * 0.5f, 0f), 58f);
                    break;
                case PowerUpType.Freeze:
                    for (int k = 0; k < 3; k++)
                    {
                        var bar = UiFactory.Box("Flake", icon, c, Vector2.zero, new Vector2(12f, 64f));
                        bar.pivot = c;
                        bar.localRotation = Quaternion.Euler(0f, 0f, k * 60f);
                        UiFactory.Fill(bar, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    }
                    break;
                case PowerUpType.Blast:
                {
                    var star = UiFactory.Box("Burst", icon, c, Vector2.zero, new Vector2(66f, 66f));
                    star.pivot = c;
                    UiFactory.Fill(star, dark, UiSprites.Star).raycastTarget = false;
                    break;
                }
                default:
                    foreach (float x in new[] { -13f, 13f })
                    {
                        var leg = UiFactory.Box("Leg", icon, c, new Vector2(x, 5f), new Vector2(13f, 36f));
                        leg.pivot = c;
                        UiFactory.Fill(leg, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    }
                    var bridge = UiFactory.Box("Bridge", icon, c, new Vector2(0f, -14f), new Vector2(40f, 13f));
                    bridge.pivot = c;
                    UiFactory.Fill(bridge, dark, UiSprites.Rounded, 8f).raycastTarget = false;
                    break;
            }
        }

        /// <summary>Shows the bag's skills (or hides the bar off the play screen).</summary>
        public void Refresh(bool show)
        {
            visible = show;
            var shown = new List<Slot>();
            foreach (var s in slots)
            {
                int n = SkillBag.Count(s.type);
                bool on = show && n > 0;
                s.root.gameObject.SetActive(on);
                if (!on) continue;
                s.count.text = n.ToString();
                shown.Add(s);
            }
            for (int i = 0; i < shown.Count; i++)
                shown[i].root.anchoredPosition = new Vector2(0f, ((shown.Count - 1) * 0.5f - i) * (Size + Gap));
        }

        /// <summary>A little pop on the slot (a skill was added or fired).</summary>
        public void Punch(PowerUpType t)
        {
            foreach (var s in slots) if (s.type == t) s.punch = 1f;
        }

        /// <summary>The screen point is on one of the skill buttons (the play area ignores it).</summary>
        public bool IsOver(Vector2 screen)
        {
            if (!visible) return false;
            foreach (var s in slots)
                if (s.root.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(s.root, screen, null)) return true;
            return false;
        }

        private void Update()
        {
            foreach (var s in slots)
            {
                if (s.punch <= 0f) continue;
                s.punch = Mathf.MoveTowards(s.punch, 0f, Time.unscaledDeltaTime * 3f);
                s.root.localScale = Vector3.one * (1f + Mathf.Sin(s.punch * Mathf.PI) * 0.25f);
            }
        }
    }
}
