using System.Collections.Generic;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// The active skills, big and see-through down the left edge of the screen: a shield, a snowflake, a magnet, the
    /// Thunder Hammer. Each one breathes (grows and shrinks) so the player always knows what they have, a ring around
    /// it runs down with the time left and it beats faster near the end. They stay faint and let touches through, so
    /// the floor behind is never hidden or blocked.
    /// </summary>
    public class SkillBadges : MonoBehaviour
    {
        private const float Size = 190f;
        private const float Alpha = 0.55f;

        private class Badge
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image ring;
            public float shown, time;
            public bool on;
        }

        private readonly Dictionary<SkillIcon, Badge> badges = new Dictionary<SkillIcon, Badge>();

        public static SkillBadges Create(Transform parent)
        {
            var rt = UiFactory.Rect("Skills", parent, new Vector2(0f, 0.3f), new Vector2(0f, 0.72f));
            rt.sizeDelta = new Vector2(Size + 40f, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(30f + Size * 0.5f, 0f);
            var s = rt.gameObject.AddComponent<SkillBadges>();
            foreach (SkillIcon icon in System.Enum.GetValues(typeof(SkillIcon))) s.badges[icon] = s.Build(rt, icon);
            return s;
        }

        private Badge Build(RectTransform parent, SkillIcon icon)
        {
            var root = UiFactory.Box(icon.ToString(), parent, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Size, Size));
            root.pivot = new Vector2(0.5f, 0.5f);
            var b = new Badge { root = root, group = root.gameObject.AddComponent<CanvasGroup>() };
            b.group.blocksRaycasts = false;
            b.group.interactable = false;
            var color = ColorOf(icon);
            UiFactory.Fill(root, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.35f, 0.75f), UiSprites.Circle).raycastTarget = false;
            var ringRect = UiFactory.Stretch("Ring", root);
            b.ring = UiFactory.Fill(ringRect, color, UiSprites.Ring);
            b.ring.raycastTarget = false;
            b.ring.type = Image.Type.Filled;
            b.ring.fillMethod = Image.FillMethod.Radial360;
            b.ring.fillOrigin = (int)Image.Origin360.Top;
            b.ring.fillClockwise = false;
            var art = UiFactory.Box("Art", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Size, Size));
            art.pivot = new Vector2(0.5f, 0.5f);
            Draw(art, icon, Size * 0.55f, color);
            root.gameObject.SetActive(false);
            return b;
        }

        private static Color ColorOf(SkillIcon icon)
        {
            switch (icon)
            {
                case SkillIcon.Shield: return new Color(0.4f, 0.8f, 1f);
                case SkillIcon.Freeze: return new Color(0.7f, 0.95f, 1f);
                case SkillIcon.Magnet: return new Color(1f, 0.4f, 0.45f);
                default: return ThunderHammer.Electric;
            }
        }

        private static Image Part(RectTransform parent, Vector2 pos, Vector2 size, Color c, Sprite sprite, float rot = 0f)
        {
            var r = UiFactory.Box("Part", parent, new Vector2(0.5f, 0.5f), pos, size);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.localRotation = Quaternion.Euler(0f, 0f, rot);
            var img = UiFactory.Fill(r, c, sprite, sprite == UiSprites.Rounded ? 3f : 1f);
            img.raycastTarget = false;
            return img;
        }

        private static void Draw(RectTransform box, SkillIcon icon, float s, Color c)
        {
            var white = Color.white;
            switch (icon)
            {
                case SkillIcon.Shield:
                    // A rounded shield with a bright stripe.
                    Part(box, new Vector2(0f, s * 0.06f), new Vector2(s * 0.7f, s * 0.6f), c, UiSprites.Rounded);
                    Part(box, new Vector2(0f, -s * 0.2f), new Vector2(s * 0.5f, s * 0.5f), c, UiSprites.Rounded, 45f);
                    Part(box, new Vector2(0f, s * 0.02f), new Vector2(s * 0.12f, s * 0.62f), white, UiSprites.Rounded);
                    break;
                case SkillIcon.Freeze:
                    // A snowflake: three crossed bars with a dot in the middle.
                    for (int i = 0; i < 3; i++) Part(box, Vector2.zero, new Vector2(s * 0.14f, s * 0.95f), white, UiSprites.Rounded, i * 60f);
                    Part(box, Vector2.zero, new Vector2(s * 0.28f, s * 0.28f), c, UiSprites.Circle);
                    break;
                case SkillIcon.Magnet:
                    // A horseshoe magnet: two legs with white tips and a bend.
                    foreach (float x in new[] { -0.24f, 0.24f })
                    {
                        Part(box, new Vector2(s * x, s * 0.05f), new Vector2(s * 0.2f, s * 0.6f), c, UiSprites.Rounded);
                        Part(box, new Vector2(s * x, s * 0.33f), new Vector2(s * 0.2f, s * 0.16f), white, UiSprites.Rounded);
                    }
                    Part(box, new Vector2(0f, -s * 0.24f), new Vector2(s * 0.68f, s * 0.24f), c, UiSprites.Rounded);
                    break;
                default:
                    // The Thunder Hammer: a tilted handle and a head with a bolt.
                    var gold = new Color(1f, 0.8f, 0.35f);
                    Part(box, new Vector2(-s * 0.08f, -s * 0.12f), new Vector2(s * 0.13f, s * 0.75f), gold, UiSprites.Rounded, 35f);
                    Part(box, new Vector2(s * 0.12f, s * 0.18f), new Vector2(s * 0.68f, s * 0.32f), c, UiSprites.Rounded, 35f);
                    Part(box, new Vector2(s * 0.12f, s * 0.18f), new Vector2(s * 0.26f, s * 0.08f), white, UiSprites.Rounded, -25f);
                    break;
            }
        }

        /// <summary>
        /// Updates one skill: <paramref name="left"/> seconds left of <paramref name="total"/> (0 or less hides it;
        /// a negative total means "held until used", shown with a full ring).
        /// </summary>
        public void Set(SkillIcon icon, float left, float total)
        {
            var b = badges[icon];
            bool on = total < 0f ? left > 0f : left > 0.01f;
            b.on = on;
            if (on && !b.root.gameObject.activeSelf)
            {
                b.root.gameObject.SetActive(true);
                b.shown = 0f;
            }
            if (on) b.ring.fillAmount = total < 0f ? 1f : Mathf.Clamp01(left / Mathf.Max(0.01f, total));
            b.time = total < 0f ? 99f : left;
        }

        public void HideAll()
        {
            foreach (var b in badges.Values)
            {
                b.on = false;
                b.root.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float y = 0f;
            foreach (SkillIcon icon in System.Enum.GetValues(typeof(SkillIcon)))
            {
                var b = badges[icon];
                if (!b.root.gameObject.activeSelf) continue;
                b.shown = Mathf.MoveTowards(b.shown, b.on ? 1f : 0f, dt * (b.on ? 4f : 3f));
                if (!b.on && b.shown <= 0f)
                {
                    b.root.gameObject.SetActive(false);
                    continue;
                }
                // Breathe; faster and stronger in the last two seconds.
                bool ending = b.time < 2f;
                float beat = Mathf.Sin(Time.unscaledTime * (ending ? 10f : 3.2f));
                float scale = (1f + beat * (ending ? 0.1f : 0.07f)) * Mathf.Lerp(0.6f, 1f, EaseOut(b.shown));
                b.root.localScale = new Vector3(scale, scale, 1f);
                b.group.alpha = Alpha * b.shown * (ending ? 0.75f + 0.25f * beat : 1f);
                b.root.anchoredPosition = new Vector2(0f, y - Size * 0.5f);
                y -= (Size + 24f) * b.shown;
            }
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
    }
}
