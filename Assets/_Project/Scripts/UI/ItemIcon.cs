using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace SquashBot.UI
{
    /// <summary>
    /// An item's picture (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 1.1), drawn from the UI's own shapes inside its
    /// rarity's frame: a thick chest plate, a geared servo, a crystal coating, a sparking whetstone, a glowing blue bottle,
    /// a green bottle with a wrench (the large one bigger and glowing).
    /// </summary>
    public static class ItemIcon
    {
        public static Color RarityColour(ItemRarity r) =>
            r == ItemRarity.Epic ? new Color(1f, 0.78f, 0.22f) : r == ItemRarity.Rare ? new Color(0.35f, 0.65f, 1f) : new Color(0.68f, 0.7f, 0.76f);

        /// <summary>The frame (its rarity's colour) and the item's shape inside, filling <paramref name="box"/>.</summary>
        public static void Draw(RectTransform box, ItemKind k, ItemRarity r)
        {
            float s = Mathf.Min(box.rect.width > 1f ? box.rect.width : box.sizeDelta.x, box.rect.height > 1f ? box.rect.height : box.sizeDelta.y);
            var rim = RarityColour(ItemBag.HasRarity(k) ? r : ItemRarity.Common);
            Raw(UiFactory.Fill(box, rim, UiSprites.Rounded, 1.6f));
            var inner = UiFactory.Rect("Inner", box, Vector2.zero, Vector2.one, new Vector2(7f, 7f), new Vector2(-7f, -7f));
            Raw(UiFactory.Fill(inner, new Color(0.07f, 0.1f, 0.22f), UiSprites.Rounded, 1.6f));
            if (r == ItemRarity.Epic && ItemBag.HasRarity(k)) Raw(UiFactory.Fill(Shape("Glow", inner, 0f, 0f, s * 0.8f, s * 0.8f), new Color(1f, 0.8f, 0.3f, 0.25f), UiSprites.Circle));
            var steel = new Color(0.72f, 0.76f, 0.84f);
            var dark = new Color(0.35f, 0.38f, 0.45f);
            switch (k)
            {
                case ItemKind.Body:
                    Raw(UiFactory.Fill(Shape("Plate", inner, 0f, 2f, s * 0.6f, s * 0.56f), steel, UiSprites.Rounded, 3f));
                    Raw(UiFactory.Fill(Shape("Line", inner, 0f, 2f, s * 0.05f, s * 0.46f), dark));
                    Raw(UiFactory.Fill(Shape("Neck", inner, 0f, s * 0.26f, s * 0.24f, s * 0.08f), new Color(0.07f, 0.1f, 0.22f), UiSprites.Rounded, 6f));
                    break;
                case ItemKind.Arms:
                    var gear = Shape("Gear", inner, -s * 0.06f, 0f, s * 0.5f, s * 0.5f);
                    for (int i = 0; i < 4; i++)
                    {
                        var tooth = Shape("Tooth", gear, 0f, 0f, s * 0.6f, s * 0.12f);
                        tooth.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
                        Raw(UiFactory.Fill(tooth, dark));
                    }
                    Raw(UiFactory.Fill(Shape("Disc", gear, 0f, 0f, s * 0.44f, s * 0.44f), steel, UiSprites.Circle));
                    Raw(UiFactory.Fill(Shape("Hub", gear, 0f, 0f, s * 0.14f, s * 0.14f), dark, UiSprites.Circle));
                    Raw(UiFactory.Fill(Shape("Motor", inner, s * 0.22f, -s * 0.12f, s * 0.24f, s * 0.3f), new Color(1f, 0.6f, 0.25f), UiSprites.Rounded, 5f));
                    break;
                case ItemKind.Shell:
                    var gem = Shape("Crystal", inner, 0f, 0f, s * 0.46f, s * 0.46f);
                    gem.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    Raw(UiFactory.Fill(gem, new Color(0.5f, 0.9f, 1f), UiSprites.Rounded, 4f));
                    var shine = Shape("Shine", gem, -s * 0.08f, s * 0.08f, s * 0.14f, s * 0.14f);
                    Raw(UiFactory.Fill(shine, new Color(1f, 1f, 1f, 0.8f), UiSprites.Rounded, 4f));
                    break;
                case ItemKind.Whet:
                    var stone = Shape("Stone", inner, 0f, -s * 0.08f, s * 0.62f, s * 0.26f);
                    stone.localRotation = Quaternion.Euler(0f, 0f, -18f);
                    Raw(UiFactory.Fill(stone, new Color(0.55f, 0.55f, 0.6f), UiSprites.Rounded, 4f));
                    for (int i = 0; i < 3; i++)
                        Raw(UiFactory.Fill(Shape("Spark", inner, s * (0.12f + 0.1f * i), s * (0.14f + 0.07f * i), s * 0.08f, s * 0.08f), new Color(1f, 0.85f, 0.3f), UiSprites.Circle));
                    break;
                default:
                    bool xp = k == ItemKind.Xp, big = k == ItemKind.RepairLarge;
                    var glass = xp ? new Color(0.35f, 0.7f, 1f) : new Color(0.35f, 0.9f, 0.45f);
                    float b = big ? 0.62f : xp ? 0.54f : 0.46f;
                    if (big || xp) Raw(UiFactory.Fill(Shape("Halo", inner, 0f, -s * 0.06f, s * (b + 0.16f), s * (b + 0.16f)), new Color(glass.r, glass.g, glass.b, 0.3f), UiSprites.Circle));
                    Raw(UiFactory.Fill(Shape("Bottle", inner, 0f, -s * 0.06f, s * b, s * b), glass, UiSprites.Circle));
                    Raw(UiFactory.Fill(Shape("Neck", inner, 0f, s * (b * 0.5f), s * 0.14f, s * 0.16f), glass, UiSprites.Rounded, 4f));
                    Raw(UiFactory.Fill(Shape("Cork", inner, 0f, s * (b * 0.5f + 0.1f), s * 0.16f, s * 0.07f), new Color(0.6f, 0.42f, 0.25f), UiSprites.Rounded, 4f));
                    if (!xp)
                    {
                        // a wrench across the bottle
                        var wrench = Shape("Wrench", inner, 0f, -s * 0.06f, s * b * 0.8f, s * 0.07f);
                        wrench.localRotation = Quaternion.Euler(0f, 0f, 40f);
                        Raw(UiFactory.Fill(wrench, Color.white));
                    }
                    else Raw(UiFactory.Fill(Shape("Spark", inner, -s * 0.08f, 0f, s * 0.1f, s * 0.1f), Color.white, UiSprites.Circle));
                    break;
            }
        }

        private static RectTransform Shape(string name, Transform parent, float x, float y, float w, float h)
        {
            var rt = UiFactory.Box(name, parent, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private static void Raw(Image i) => i.raycastTarget = false;
    }
}
