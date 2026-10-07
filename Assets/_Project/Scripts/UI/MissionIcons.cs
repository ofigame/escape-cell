using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// Little badges on the map for levels with a special goal, so the player sees what is coming before starting:
    /// the princess, a monster (both when the monster guards the princess), WARDEN, and the quest pieces
    /// (energy core, cage, lantern, gem). Drawn from the UI's rounded shapes.
    /// </summary>
    public static class MissionIcons
    {
        public enum Icon { None, Princess, Monster, Boss, Cores, Cages, Lanterns, Treasure }

        /// <summary>The badges a level gets (at most two).</summary>
        public static (Icon a, Icon b) For(LevelData level)
        {
            if (level == null) return (Icon.None, Icon.None);
            switch (level.mission)
            {
                case MissionType.Boss: return (Icon.Boss, Icon.None);
                case MissionType.Monster: return (Icon.Monster, level.guardsPrincess ? Icon.Princess : Icon.None);
                case MissionType.Quest:
                    switch (level.quest)
                    {
                        case QuestKind.Princess: return (Icon.Princess, Icon.None);
                        case QuestKind.Cores: return (Icon.Cores, Icon.None);
                        case QuestKind.Cages: return (Icon.Cages, Icon.None);
                        case QuestKind.Lanterns: return (Icon.Lanterns, Icon.None);
                        default: return (Icon.Treasure, Icon.None);
                    }
                default: return (Icon.None, Icon.None);
            }
        }

        /// <summary>A round white badge with the icon, anchored to <paramref name="parent"/>.</summary>
        public static void Badge(RectTransform parent, Icon icon, Vector2 anchor, Vector2 position, float size)
        {
            if (icon == Icon.None) return;
            var badge = UiFactory.Box("Badge " + icon, parent, anchor, position, new Vector2(size, size));
            badge.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Fill(badge, new Color(0.08f, 0.06f, 0.18f, 0.9f), UiSprites.Circle).raycastTarget = false;
            UiFactory.Fill(UiFactory.Stretch("Rim", badge), Color.white, UiSprites.Ring, 0.6f).raycastTarget = false;
            Draw(badge, icon, size * 0.7f);
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

        private static void Draw(RectTransform box, Icon icon, float s)
        {
            var dark = new Color(0.1f, 0.08f, 0.16f);
            var gold = new Color(1f, 0.82f, 0.3f);
            switch (icon)
            {
                case Icon.Princess:
                    // A pink face with a golden crown.
                    Part(box, new Vector2(0f, -s * 0.08f), new Vector2(s * 0.62f, s * 0.56f), new Color(1f, 0.7f, 0.85f), UiSprites.Rounded);
                    foreach (float x in new[] { -0.12f, 0.12f }) Part(box, new Vector2(s * x, -s * 0.06f), new Vector2(s * 0.09f, s * 0.12f), new Color(0.8f, 0.3f, 0.6f), UiSprites.Rounded);
                    Part(box, new Vector2(0f, s * 0.26f), new Vector2(s * 0.5f, s * 0.1f), gold, UiSprites.Rounded);
                    foreach (float x in new[] { -0.2f, 0f, 0.2f }) Part(box, new Vector2(s * x, s * (x == 0f ? 0.38f : 0.34f)), new Vector2(s * 0.1f, s * 0.18f), gold, UiSprites.Rounded);
                    break;
                case Icon.Monster:
                    // A green blob with two big eyes and little horns.
                    Part(box, new Vector2(0f, -s * 0.05f), new Vector2(s * 0.72f, s * 0.62f), new Color(0.45f, 0.85f, 0.4f), UiSprites.Circle);
                    foreach (float x in new[] { -0.15f, 0.15f })
                    {
                        Part(box, new Vector2(s * x, s * 0.02f), new Vector2(s * 0.2f, s * 0.2f), Color.white, UiSprites.Circle);
                        Part(box, new Vector2(s * x, s * 0.02f), new Vector2(s * 0.09f, s * 0.09f), dark, UiSprites.Circle);
                        Part(box, new Vector2(s * x * 1.6f, s * 0.3f), new Vector2(s * 0.08f, s * 0.16f), Color.white, UiSprites.Rounded, -x * 120f);
                    }
                    Part(box, new Vector2(0f, -s * 0.2f), new Vector2(s * 0.3f, s * 0.06f), dark, UiSprites.Rounded);
                    break;
                case Icon.Boss:
                    // WARDEN: a red screen with one glaring eye.
                    Part(box, Vector2.zero, new Vector2(s * 0.8f, s * 0.58f), new Color(0.9f, 0.3f, 0.35f), UiSprites.Rounded);
                    Part(box, Vector2.zero, new Vector2(s * 0.64f, s * 0.42f), new Color(0.3f, 0.06f, 0.1f), UiSprites.Rounded);
                    Part(box, Vector2.zero, new Vector2(s * 0.24f, s * 0.24f), new Color(1f, 0.35f, 0.4f), UiSprites.Circle);
                    Part(box, Vector2.zero, new Vector2(s * 0.08f, s * 0.08f), Color.white, UiSprites.Circle);
                    break;
                case Icon.Cores:
                    Part(box, Vector2.zero, new Vector2(s * 0.42f, s * 0.42f), new Color(0.5f, 1f, 0.6f), UiSprites.Circle);
                    foreach (float y in new[] { -0.28f, 0.28f }) Part(box, new Vector2(0f, s * y), new Vector2(s * 0.46f, s * 0.1f), new Color(0.5f, 0.52f, 0.6f), UiSprites.Rounded);
                    break;
                case Icon.Cages:
                    Part(box, new Vector2(0f, -s * 0.05f), new Vector2(s * 0.3f, s * 0.28f), new Color(0.65f, 0.8f, 1f), UiSprites.Rounded);
                    for (int i = 0; i < 4; i++) Part(box, new Vector2(s * (-0.24f + i * 0.16f), 0f), new Vector2(s * 0.05f, s * 0.62f), new Color(1f, 0.75f, 0.35f), UiSprites.Rounded);
                    Part(box, new Vector2(0f, s * 0.3f), new Vector2(s * 0.6f, s * 0.08f), new Color(1f, 0.75f, 0.35f), UiSprites.Rounded);
                    break;
                case Icon.Lanterns:
                    Part(box, new Vector2(0f, -s * 0.05f), new Vector2(s * 0.34f, s * 0.42f), new Color(1f, 0.85f, 0.45f), UiSprites.Rounded);
                    Part(box, new Vector2(0f, -s * 0.05f), new Vector2(s * 0.14f, s * 0.2f), new Color(1f, 0.5f, 0.2f), UiSprites.Circle);
                    Part(box, new Vector2(0f, s * 0.22f), new Vector2(s * 0.42f, s * 0.08f), dark, UiSprites.Rounded);
                    break;
                case Icon.Treasure:
                    Part(box, Vector2.zero, new Vector2(s * 0.42f, s * 0.42f), new Color(1f, 0.45f, 0.85f), UiSprites.Rounded, 45f);
                    Part(box, new Vector2(s * 0.06f, s * 0.08f), new Vector2(s * 0.1f, s * 0.1f), Color.white, UiSprites.Circle);
                    break;
            }
        }
    }
}
