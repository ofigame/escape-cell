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
        private const float Size = 92f, Gap = 10f;

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
            var rt = UiFactory.Box("SkillBar", parent, new Vector2(0.5f, 0f), new Vector2(0f, 172f), new Vector2(640f, Size));
            rt.pivot = new Vector2(0.5f, 0f);
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
            s.root = UiFactory.Box(t.ToString(), root, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(Size, Size));
            s.root.pivot = new Vector2(0.5f, 0f);
            var bg = UiFactory.Fill(s.root, ColorOf(t), UiSprites.Circle);
            UiFactory.Fill(UiFactory.Stretch("Rim", s.root), new Color(1f, 1f, 1f, 0.85f), UiSprites.Ring, 0.35f).raycastTarget = false;
            var icon = UiFactory.Box("Icon", s.root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Size, Size));
            icon.pivot = new Vector2(0.5f, 0.5f);
            DrawIcon(icon, t);
            var badge = UiFactory.Box("Badge", s.root, new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(48f, 48f));
            badge.pivot = new Vector2(1f, 1f);
            UiFactory.Fill(badge, new Color(0.12f, 0.1f, 0.24f, 0.95f), UiSprites.Circle).raycastTarget = false;
            s.count = UiFactory.TextBox("Count", badge, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f), "", 30f, Color.white, title: true);
            s.count.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            s.count.raycastTarget = false;
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
                    UIController.HeartIcon(icon, new Vector2(Size * 0.5f, 0f), 58f);
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
                shown[i].root.anchoredPosition = new Vector2((i - (shown.Count - 1) * 0.5f) * (Size + Gap), 0f);
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
