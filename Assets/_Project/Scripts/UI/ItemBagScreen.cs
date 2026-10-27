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
    /// The BAG (Docs/ESYA_CANTA_VE_BEDEN_SISTEMI.md, section 5): the three parts' points bars (a full one's LEVEL UP lit, or
    /// what gold or core parts it still lacks), the battle wear and its repair, the items in rarity frames; touching one
    /// shows what it does with USE and ×10 USE; USE ALL uses every part item, whetstone and experience potion (never the
    /// repair potions).
    /// </summary>
    public class ItemBagScreen : MonoBehaviour
    {
        /// <summary>Something was used or a part levelled (the menu's counts, foi's look).</summary>
        public event Action Changed;

        private const int PerRow = 5;
        private const float Tile = 168f, Step = 184f;

        private UiScreen screen;
        private RectTransform card, grid;
        private TextMeshProUGUI toast;
        private float toastUntil;
        private readonly TextMeshProUGUI[] partLevel = new TextMeshProUGUI[3], partPoints = new TextMeshProUGUI[3];
        private readonly Image[] partFill = new Image[3];
        private readonly Button[] levelUp = new Button[3];
        private readonly TextMeshProUGUI[] levelUpLabel = new TextMeshProUGUI[3];
        private TextMeshProUGUI wearText;
        private Button repair;
        private RectTransform detail;
        private TextMeshProUGUI detailName, detailText;
        private Button useOne, useTen, useAll;
        private ItemKind? chosen;
        private ItemRarity chosenRarity;

        private static readonly RobotPart[] Parts = { RobotPart.Body, RobotPart.Arms, RobotPart.Shell };

        public static ItemBagScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("ItemBag", canvasRoot, out var root);
            var b = root.gameObject.AddComponent<ItemBagScreen>();
            b.screen = screen;
            b.Build(root);
            return b;
        }

        /// <summary>The part's colour (as in the workshop).</summary>
        public static Color PartColour(RobotPart p) => p == RobotPart.Body ? new Color(1f, 0.45f, 0.5f) : p == RobotPart.Arms ? new Color(1f, 0.66f, 0.25f) : new Color(0.45f, 0.75f, 1f);

        private void Build(RectTransform root)
        {
            UiFactory.Dim(root, new Color(0.04f, 0.04f, 0.12f, 0.88f));
            card = UiFactory.Card("Card", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 1800f));
            card.pivot = new Vector2(0.5f, 0.5f);
            screen.SetPopTarget(card);
            UiFactory.MakeButton(card, "X", Kind.Icon, new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(100f, 100f), Hide, 48f);
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(700f, 100f), Loc.T("ibag.title"), 64f, Palette.UiGold, title: true);
            toast = UiFactory.TextBox("Toast", card, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(900f, 56f), "", 32f, Palette.UiCyan);

            // the three parts' bars
            for (int i = 0; i < Parts.Length; i++)
            {
                var p = Parts[i];
                int idx = i;
                float y = -200f - i * 130f;
                var name = UiFactory.TextBox("Part", card, new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(220f, 60f), Loc.T("part." + p), 36f, PartColour(p), title: true, align: TextAlignmentOptions.Left);
                name.enableAutoSizing = true; name.fontSizeMin = 20f; name.fontSizeMax = 36f;
                partLevel[i] = UiFactory.TextBox("Level", card, new Vector2(0f, 1f), new Vector2(40f, y - 56f), new Vector2(220f, 44f), "", 28f, Color.white, align: TextAlignmentOptions.Left);
                UiFactory.Bar(card, new Vector2(0f, 1f), new Vector2(270f, y - 18f), new Vector2(400f, 44f), PartColour(p), out partFill[i]);
                partPoints[i] = UiFactory.TextBox("Points", card, new Vector2(0f, 1f), new Vector2(270f, y - 66f), new Vector2(400f, 40f), "", 26f, new Color(1f, 1f, 1f, 0.85f));
                levelUp[i] = UiFactory.MakeButton(card, Loc.T("ibag.levelUp"), Kind.Gold, new Vector2(1f, 1f), new Vector2(-30f, y), new Vector2(270f, 104f), () => LevelUp(idx), 30f);
                levelUpLabel[i] = levelUp[i].GetComponentInChildren<TextMeshProUGUI>();
                levelUpLabel[i].enableAutoSizing = true; levelUpLabel[i].fontSizeMin = 16f; levelUpLabel[i].fontSizeMax = 30f;
            }

            // the battle wear and its repair
            wearText = UiFactory.TextBox("Wear", card, new Vector2(0f, 1f), new Vector2(40f, -600f), new Vector2(620f, 70f), "", 32f, Color.white, align: TextAlignmentOptions.Left);
            repair = UiFactory.MakeButton(card, Loc.T("ibag.repair"), Kind.Secondary, new Vector2(1f, 1f), new Vector2(-30f, -588f), new Vector2(270f, 96f), Repair, 32f);

            grid = UiFactory.Box("Grid", card, new Vector2(0.5f, 1f), new Vector2(0f, -720f), new Vector2(PerRow * Step, 4 * Step));

            // the chosen item: its name, what it does, USE and ×10 USE
            detail = UiFactory.Box("Detail", card, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(920f, 230f));
            UiFactory.Fill(detail, new Color(0.03f, 0.06f, 0.16f, 0.85f), UiSprites.Rounded, 1.6f).raycastTarget = false;
            detailName = UiFactory.TextBox("Name", detail, new Vector2(0f, 1f), new Vector2(26f, -14f), new Vector2(560f, 60f), "", 36f, Color.white, title: true, align: TextAlignmentOptions.Left);
            detailText = UiFactory.TextBox("Text", detail, new Vector2(0f, 1f), new Vector2(26f, -78f), new Vector2(560f, 140f), "", 28f, new Color(1f, 1f, 1f, 0.85f), FontStyles.Normal, align: TextAlignmentOptions.TopLeft);
            detailText.textWrappingMode = TextWrappingModes.Normal;
            useOne = UiFactory.MakeButton(detail, Loc.T("ibag.use"), Kind.Primary, new Vector2(1f, 1f), new Vector2(-20f, -14f), new Vector2(290f, 96f), () => Use(1), 34f);
            useTen = UiFactory.MakeButton(detail, Loc.T("ibag.use10"), Kind.Secondary, new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(290f, 90f), () => Use(10), 30f);
            useAll = UiFactory.MakeButton(card, Loc.T("ibag.useAll"), Kind.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(620f, 120f), UseAll, 42f);
        }

        public void Show()
        {
            screen.Show();
            transform.SetAsLastSibling();
            chosen = null;
            Refresh();
        }

        public void Hide() => screen.Hide();

        public bool Visible => screen.IsVisible;

        private void Say(string text)
        {
            toast.text = text;
            toastUntil = Time.unscaledTime + 3f;
        }

        private void Update()
        {
            if (toastUntil > 0f && Time.unscaledTime > toastUntil) { toastUntil = 0f; toast.text = ""; }
        }

        private void LevelUp(int i)
        {
            var p = Parts[i];
            int before = RobotParts.Level(p);
            if (RobotParts.TryUpgrade(p))
            {
                AudioManager.PlaySfx(Sfx.Coin, 1f, 1.3f);
                Haptics.Medium();
                Say(Loc.F("part.up", Loc.T("part." + p), RobotParts.Value(p, before), RobotParts.Value(p, before + 1)));
                Changed?.Invoke();
            }
            else AudioManager.PlaySfx(Sfx.Bump, 0.6f);
            Refresh();
        }

        private void Repair()
        {
            var k = ItemBag.Count(ItemKind.RepairSmall) > 0 ? ItemKind.RepairSmall : ItemKind.RepairLarge;
            if (BattleWear.Level > 0 && ItemBag.Use(k, ItemRarity.Common) > 0)
            {
                AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1.5f);
                Say(Loc.T("ibag.repaired"));
                Changed?.Invoke();
            }
            Refresh();
        }

        private void Use(int n)
        {
            if (!chosen.HasValue) return;
            var k = chosen.Value;
            int used = ItemBag.Use(k, chosenRarity, n);
            if (used > 0)
            {
                AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1.2f + 0.1f * (int)chosenRarity);
                Haptics.Light();
                Say(Effect(k, ItemCatalog.Current.Value(k, chosenRarity) * used));
                Changed?.Invoke();
            }
            else
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                var why = ItemBag.Blocked(k);
                if (why != null) Say(Loc.T(why));
            }
            if (ItemBag.Count(k, chosenRarity) == 0) chosen = null;
            Refresh();
        }

        private void UseAll()
        {
            int used = ItemBag.UseAll();
            AudioManager.PlaySfx(used > 0 ? Sfx.Coin : Sfx.Bump, 0.9f, 1.1f);
            if (used > 0) { Haptics.Medium(); Say(Loc.F("ibag.usedAll", used)); Changed?.Invoke(); }
            chosen = null;
            Refresh();
        }

        /// <summary>What using items gave ("Body +50 points").</summary>
        public static string Effect(ItemKind k, int amount)
        {
            switch (k)
            {
                case ItemKind.Body:
                case ItemKind.Arms:
                case ItemKind.Shell: return Loc.F("ibag.eff.part", Loc.T("part." + ItemBag.PartOf(k).Value), amount);
                case ItemKind.Whet: return Loc.F("ibag.eff.whet", amount, Armory.Equipped != null ? Loc.T("weapon." + Armory.Equipped.id) : "");
                case ItemKind.Xp: return Loc.F("ibag.eff.xp", amount);
                case ItemKind.RepairSmall: return Loc.T("ibag.eff.repairS");
                default: return Loc.T("ibag.eff.repairL");
            }
        }

        public static string NameOf(ItemKind k) => Loc.T("item." + k);

        public static string RarityName(ItemRarity r) => Loc.T("rarity." + r);

        public void Refresh()
        {
            for (int i = 0; i < Parts.Length; i++)
            {
                var p = Parts[i];
                bool maxed = RobotParts.IsMaxed(p);
                int lv = RobotParts.Level(p), pts = PartPoints.Points(p), need = PartPoints.Need(p);
                partLevel[i].text = Loc.F("ibag.level", lv);
                UiFactory.SetBar(partFill[i], maxed ? 1f : need > 0 ? pts / (float)need : 0f);
                int bagged = ItemBag.Count(ItemBag.ItemOf(p));
                partPoints[i].text = maxed ? Loc.T("part.top") : pts + "/" + need + (PartPoints.Over(p) > 0 ? "  <color=#9FF3FF>+" + PartPoints.Over(p) + "</color>" : "") + (bagged > 0 ? "  ·  " + Loc.F("ibag.inBag", bagged) : "");
                bool full = PartPoints.Full(p);
                levelUp[i].gameObject.SetActive(full);
                if (full)
                {
                    int gold = RobotParts.NextGold(p), cores = RobotParts.NextCores(p);
                    bool afford = RobotParts.CanAfford(p);
                    levelUpLabel[i].text = afford ? Loc.T("ibag.levelUp") + "\n" + (cores > 0 ? Loc.F("part.price2", gold, cores) : gold.ToString())
                        : SaveData.Coins < gold ? Loc.F("ibag.needGold", gold - SaveData.Coins) : Loc.F("ibag.needCores", cores - CoreParts.Count);
                    levelUp[i].interactable = afford;
                }
            }
            int wear = BattleWear.Level;
            wearText.text = Loc.T("ibag.wear") + "  " + Dots(wear) + "  " + Loc.T("wear." + wear);
            repair.gameObject.SetActive(wear > 0 && (ItemBag.Count(ItemKind.RepairSmall) + ItemBag.Count(ItemKind.RepairLarge)) > 0);

            // the grid: every kind and rarity there is one of
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            int n = 0;
            foreach (var k in ItemBag.Kinds)
                for (int ri = ItemBag.HasRarity(k) ? 2 : 0; ri >= 0; ri--)
                {
                    var r = (ItemRarity)ri;
                    int count = ItemBag.Count(k, r);
                    if (count <= 0) continue;
                    var tile = UiFactory.Box("Item", grid, new Vector2(0f, 1f), new Vector2((n % PerRow) * Step + (Step - Tile) * 0.5f, -(n / PerRow) * Step), new Vector2(Tile, Tile));
                    ItemIcon.Draw(tile, k, r);
                    var kk = k; var rr = r;
                    var hit = tile.gameObject.AddComponent<Button>();
                    hit.targetGraphic = tile.GetComponent<Image>();
                    hit.onClick.AddListener(() => { chosen = kk; chosenRarity = rr; AudioManager.PlaySfx(Sfx.Click, 0.6f, 1.2f); Refresh(); });
                    var c = UiFactory.TextBox("Count", tile, new Vector2(1f, 0f), new Vector2(-8f, 4f), new Vector2(110f, 44f), "×" + count, 30f, Color.white, title: true, align: TextAlignmentOptions.Right);
                    c.raycastTarget = false;
                    if (chosen == k && chosenRarity == r) tile.localScale = Vector3.one * 1.08f;
                    n++;
                }
            if (n == 0) UiFactory.TextBox("Empty", grid, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(860f, 160f), Loc.T("ibag.empty"), 32f, new Color(1f, 1f, 1f, 0.6f), FontStyles.Normal).textWrappingMode = TextWrappingModes.Normal;

            detail.gameObject.SetActive(chosen.HasValue);
            if (chosen.HasValue)
            {
                var k = chosen.Value;
                int count = ItemBag.Count(k, chosenRarity);
                detailName.text = NameOf(k) + (ItemBag.HasRarity(k) ? "  <size=26><color=#" + ColorUtility.ToHtmlStringRGB(ItemIcon.RarityColour(chosenRarity)) + ">" + RarityName(chosenRarity) + "</color></size>" : "");
                var why = ItemBag.Blocked(k);
                detailText.text = Effect(k, ItemCatalog.Current.Value(k, chosenRarity)) + "  ·  " + Loc.F("ibag.have", count) + (why != null ? "\n<color=#FF7A85>" + Loc.T(why) + "</color>" : "");
                useOne.interactable = why == null;
                useTen.gameObject.SetActive(count > 1);
                useTen.interactable = why == null;
            }
            useAll.interactable = ItemBag.Count(ItemKind.Body) + ItemBag.Count(ItemKind.Arms) + ItemBag.Count(ItemKind.Shell) + ItemBag.Count(ItemKind.Whet) + ItemBag.Count(ItemKind.Xp) > 0;
        }

        /// <summary>The wear as steps lit of three (the font has no dots: bars).</summary>
        public static string Dots(int wear)
        {
            var s = "";
            for (int i = 0; i < BattleWear.MaxLevel; i++) s += i < wear ? "<color=#FFB060>I</color>" : "<color=#FFFFFF44>I</color>";
            return s;
        }
    }
}
