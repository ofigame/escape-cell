using System;
using System.Collections;
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
        /// <summary>The shows (section 5.3, 5.4) play on this layer over the card; a tap skips them.</summary>
        private RectTransform showLayer;
        private bool skip;
        private int showing;
        private TutorialHand hand;
        /// <summary>The grid's tiles as last drawn, with what each holds (the tutorial points at one).</summary>
        private readonly List<(RectTransform tile, ItemKind kind, ItemRarity rarity)> tiles = new List<(RectTransform, ItemKind, ItemRarity)>();
        private const string TutBagKey = "sb_tut_bag", TutLevelKey = "sb_tut_levelup";

        private static readonly RobotPart[] Parts = { RobotPart.Body, RobotPart.Arms, RobotPart.Shell };

        /// <summary>Makes a copy of foi as it looks now under a holder (the game sets it): the close look turns it.</summary>
        public Func<Transform, GameObject> ModelMaker;

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
            // foi itself: its body by its parts and its battle wear, turned by the finger in the close look
            UiFactory.MakeButton(card, Loc.T("ibag.foi"), Kind.Secondary, new Vector2(0f, 1f), new Vector2(22f, -22f), new Vector2(170f, 100f),
                () => ItemViewer.Show(transform, Loc.T("ibag.foiTitle"), () => ItemPreview.ViewHero(ModelMaker)), 34f);
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

            // the shows' layer (catches the finger only while one plays: a tap skips it)
            showLayer = UiFactory.Stretch("Shows", card);
            UiFactory.Fill(showLayer, new Color(0f, 0f, 0f, 0.001f));
            var skipButton = showLayer.gameObject.AddComponent<Button>();
            skipButton.transition = Selectable.Transition.None;
            skipButton.onClick.AddListener(() => skip = true);
            showLayer.gameObject.SetActive(false);
            hand = TutorialHand.Create(card);
        }

        public void Show()
        {
            screen.Show();
            transform.SetAsLastSibling();
            chosen = null;
            showing = 0; showLayer.gameObject.SetActive(false);
            ItemEvents.BagOpened();
            Refresh();
        }

        public void Hide() { hand.Show(TutorialHand.Gesture.None, Vector2.zero); screen.Hide(); }

        public bool Visible => screen.IsVisible;

        private void Say(string text, float seconds = 3f)
        {
            toast.text = text;
            toastUntil = Time.unscaledTime + seconds;
        }

        /// <summary>A point of <paramref name="target"/>'s middle in the card's own frame (where the hand and the shows go).</summary>
        private Vector2 CardPoint(RectTransform target) => card.InverseTransformPoint(target.TransformPoint(target.rect.center));

        private void Update()
        {
            if (toastUntil > 0f && Time.unscaledTime > toastUntil) { toastUntil = 0f; toast.text = ""; }
        }

        private void LevelUp(int i)
        {
            var p = Parts[i];
            int before = RobotParts.Level(p);
            int cores = RobotParts.NextCores(p);
            if (RobotParts.TryUpgrade(p))
            {
                AudioManager.PlaySfx(Sfx.Coin, 1f, 1.3f);
                Haptics.Medium();
                Say(Loc.F("part.up", Loc.T("part." + p), RobotParts.Value(p, before), RobotParts.Value(p, before + 1)));
                Prefs.SetInt(TutLevelKey, 1);
                Changed?.Invoke();
                StartCoroutine(LevelShow(i, before + 1, cores > 0));
            }
            else AudioManager.PlaySfx(Sfx.Bump, 0.6f);
            Refresh();
        }

        private void Repair()
        {
            var k = ItemBag.Count(ItemKind.RepairSmall) > 0 ? ItemKind.RepairSmall : ItemKind.RepairLarge;
            if (ItemBag.Count(k) == 0 && BattleWear.Service()) { AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1.5f); Say(Loc.T("wear.serviceDone")); Changed?.Invoke(); } // (no potion: Bip's daily service)
            else if (BattleWear.Level > 0 && ItemBag.Use(k, ItemRarity.Common) > 0)
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
            var r = chosenRarity;
            var fills = BarFills();
            var from = TileOf(k, r);
            int used = ItemBag.Use(k, r, n);
            if (used > 0)
            {
                AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1.2f + 0.1f * (int)r);
                Haptics.Light();
                Say(Effect(k, ItemCatalog.Current.Value(k, r) * used));
                if (Prefs.GetInt(TutBagKey, 0) == 0) { Prefs.SetInt(TutBagKey, 1); Say(Loc.T("tut.bag.bar"), 5f); hand.Show(TutorialHand.Gesture.None, Vector2.zero); }
                Changed?.Invoke();
                pendingShow = UseShow(new List<(ItemKind, ItemRarity)> { (k, r) }, from, fills, used);
            }
            else
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                var why = ItemBag.Blocked(k);
                if (why != null) Say(Loc.T(why));
            }
            if (ItemBag.Count(k, chosenRarity) == 0) chosen = null;
            Refresh();
            if (pendingShow != null) { StartCoroutine(pendingShow); pendingShow = null; } // (after the refresh: the show fills the bars from where they were to where they are now)
        }

        /// <summary>A use's show, started once the screen shows the result.</summary>
        private IEnumerator pendingShow;

        private void UseAll()
        {
            // what is there to use, for the show (the rarest last)
            var list = new List<(ItemKind, ItemRarity)>();
            foreach (var k in ItemBag.Kinds)
                if (ItemBag.HasRarity(k))
                    foreach (var r in ItemBag.Rarities) if (ItemBag.Count(k, r) > 0 && ItemBag.Blocked(k) == null) list.Add((k, r));
            list.Sort((a, b) => ((int)a.Item2).CompareTo((int)b.Item2));
            var fills = BarFills();
            int used = ItemBag.UseAll();
            AudioManager.PlaySfx(used > 0 ? Sfx.Coin : Sfx.Bump, 0.9f, 1.1f);
            if (used > 0) { Haptics.Medium(); Say(Loc.F("ibag.usedAll", used)); Changed?.Invoke(); pendingShow = UseShow(list, null, fills, used); }
            chosen = null;
            Refresh();
            if (pendingShow != null) { StartCoroutine(pendingShow); pendingShow = null; }
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
            repair.gameObject.SetActive(wear > 0 && ((ItemBag.Count(ItemKind.RepairSmall) + ItemBag.Count(ItemKind.RepairLarge)) > 0 || BattleWear.ServiceReady));

            // the grid: every kind and rarity there is one of
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            tiles.Clear();
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
                    tiles.Add((tile, k, r));
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
            Tutorial();
            useAll.interactable = ItemBag.Count(ItemKind.Body) + ItemBag.Count(ItemKind.Arms) + ItemBag.Count(ItemKind.Shell) + ItemBag.Count(ItemKind.Whet) + ItemBag.Count(ItemKind.Xp) > 0;
        }

        // ---------- The tutorial (section 10) ----------

        /// <summary>The first bag: pick an item, USE it (the hand shows where); the first full bar: LEVEL UP.</summary>
        private void Tutorial()
        {
            if (showing > 0) return;
            if (Prefs.GetInt(TutBagKey, 0) == 0 && tiles.Count > 0)
            {
                if (!chosen.HasValue)
                {
                    var first = tiles.Find(t => ItemBag.PartOf(t.kind).HasValue && ItemBag.Blocked(t.kind) == null);
                    if (first.tile == null) first = tiles[0];
                    hand.Show(TutorialHand.Gesture.Tap, CardPoint(first.tile));
                    Say(Loc.T("tut.bag.pick"), 30f);
                }
                else { hand.Show(TutorialHand.Gesture.Tap, CardPoint((RectTransform)useOne.transform)); Say(Loc.T("tut.bag.use"), 30f); }
                return;
            }
            if (Prefs.GetInt(TutLevelKey, 0) == 0)
                for (int i = 0; i < levelUp.Length; i++)
                    if (levelUp[i].gameObject.activeSelf && levelUp[i].interactable)
                    {
                        hand.Show(TutorialHand.Gesture.Tap, CardPoint((RectTransform)levelUp[i].transform));
                        Say(Loc.T("tut.levelUp"), 30f);
                        return;
                    }
            hand.Show(TutorialHand.Gesture.None, Vector2.zero);
        }

        // ---------- The shows (sections 5.3 and 5.4) ----------

        private float[] BarFills()
        {
            var f = new float[Parts.Length];
            for (int i = 0; i < f.Length; i++) f[i] = partFill[i].rectTransform.anchorMax.x;
            return f;
        }

        private RectTransform TileOf(ItemKind k, ItemRarity r)
        {
            foreach (var t in tiles) if (t.kind == k && t.rarity == r) return t.tile;
            return null;
        }

        /// <summary>Waits <paramref name="seconds"/>, or less once skipped.</summary>
        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !skip; t += Time.unscaledDeltaTime) yield return null;
        }

        /// <summary>
        /// Items used: each pops up big in the middle and flies into what it fills — a part item into its bar (the bar
        /// filling from where it was), the others into the line above — "+50" where it lands; an epic one with a gold
        /// flash, a beat slower and its own sound. Many at once (USE ALL): a quick run of them. A tap skips it.
        /// </summary>
        private IEnumerator UseShow(List<(ItemKind kind, ItemRarity rarity)> items, RectTransform from, float[] fillsBefore, int used)
        {
            showing++;
            skip = false;
            showLayer.gameObject.SetActive(true);
            showLayer.SetAsLastSibling();
            var after = BarFills();
            for (int i = 0; i < Parts.Length; i++) UiFactory.SetBar(partFill[i], fillsBefore[i]);
            bool many = items.Count > 1;
            int shown = 0;
            foreach (var (kind, rarity) in items)
            {
                if (shown++ >= 6) break;
                bool epic = rarity == ItemRarity.Epic;
                var icon = UiFactory.Box("Used", showLayer, new Vector2(0.5f, 0.5f), from != null ? CardPoint(from) : Vector2.zero, new Vector2(220f, 220f));
                icon.pivot = new Vector2(0.5f, 0.5f);
                ItemIcon.Draw(icon, kind, rarity);
                foreach (var g in icon.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
                Image flash = null;
                if (epic)
                {
                    flash = UiFactory.Fill(UiFactory.Stretch("Flash", showLayer), new Color(1f, 0.82f, 0.3f, 0.55f));
                    flash.raycastTarget = false;
                    flash.transform.SetSiblingIndex(0);
                    AudioManager.PlaySfx(Sfx.Win, 0.9f, 1.2f);
                }
                // up big in the middle
                var start = icon.anchoredPosition;
                float pop = many ? 0.12f : epic ? 0.45f : 0.25f;
                for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / pop)
                {
                    icon.anchoredPosition = Vector2.Lerp(start, new Vector2(0f, 120f), t);
                    icon.localScale = Vector3.one * (t < 0.7f ? Mathf.Lerp(0.3f, 1.25f, t / 0.7f) : Mathf.Lerp(1.25f, 1f, (t - 0.7f) / 0.3f));
                    if (flash != null) flash.color = new Color(1f, 0.82f, 0.3f, 0.55f * (1f - t));
                    yield return null;
                }
                if (epic) yield return Wait(0.3f); // (the slow beat: the big moment)
                // into what it fills
                var part = ItemBag.PartOf(kind);
                int pi = part.HasValue ? Array.IndexOf(Parts, part.Value) : -1;
                var to = pi >= 0 ? CardPoint((RectTransform)partFill[pi].transform.parent) : CardPoint(toast.rectTransform);
                var at = icon.anchoredPosition;
                float fly = many ? 0.15f : 0.3f;
                for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / fly)
                {
                    icon.anchoredPosition = Vector2.Lerp(at, to, t * t);
                    icon.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t);
                    yield return null;
                }
                Destroy(icon.gameObject);
                if (flash != null) Destroy(flash.gameObject);
                if (!many && !skip)
                {
                    int amount = ItemCatalog.Current.Value(kind, rarity) * used;
                    var plus = UiFactory.TextBox("Plus", showLayer, new Vector2(0.5f, 0.5f), to + new Vector2(0f, 40f), new Vector2(300f, 70f), "+" + amount, 46f, epic ? Palette.UiGold : Palette.UiCyan, title: true);
                    plus.raycastTarget = false;
                    StartCoroutine(RiseAndGo(plus.rectTransform));
                }
                AudioManager.PlaySfx(Sfx.Coin, 0.6f, 1.3f + 0.1f * (int)rarity);
            }
            // the bars fill from where they were (a bar that went past full: up to full, then its new level's)
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 0.3f)
            {
                for (int i = 0; i < Parts.Length; i++) UiFactory.SetBar(partFill[i], after[i] >= fillsBefore[i] ? Mathf.Lerp(fillsBefore[i], after[i], t) : Mathf.Lerp(fillsBefore[i], 1f, t));
                yield return null;
            }
            for (int i = 0; i < Parts.Length; i++) UiFactory.SetBar(partFill[i], after[i]);
            EndShow();
        }

        private IEnumerator RiseAndGo(RectTransform r)
        {
            var at = r.anchoredPosition;
            var text = r.GetComponentInChildren<TextMeshProUGUI>();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.7f)
            {
                if (r == null) yield break;
                r.anchoredPosition = at + new Vector2(0f, 60f * t);
                if (text != null) text.alpha = 1f - t * t;
                yield return null;
            }
            if (r != null) Destroy(r.gameObject);
        }

        /// <summary>
        /// LEVEL UP: gold (and the core parts) flowing from the top into the part, its row lit, its level popping; at
        /// levels 5, 10, 15 and 20 the big one: "NEW LOOK!" and foi shown close up, turned by the finger.
        /// </summary>
        private IEnumerator LevelShow(int i, int level, bool withCores)
        {
            showing++;
            skip = false;
            showLayer.gameObject.SetActive(true);
            showLayer.SetAsLastSibling();
            var row = CardPoint((RectTransform)partFill[i].transform.parent);
            var dots = new List<RectTransform>();
            int count = withCores ? 10 : 6;
            for (int d = 0; d < count; d++)
            {
                bool core = d >= 6;
                var dot = UiFactory.Box("Coin", showLayer, new Vector2(0.5f, 0.5f), new Vector2(core ? 300f : -300f, 820f), new Vector2(44f, 44f));
                dot.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Fill(dot, core ? Palette.UiCyan : Palette.UiGold, UiSprites.Circle).raycastTarget = false;
                dots.Add(dot);
            }
            for (float t = 0f; t < 1.2f && !skip; t += Time.unscaledDeltaTime)
            {
                for (int d = 0; d < dots.Count; d++)
                {
                    float k = Mathf.Clamp01((t - d * 0.06f) / 0.45f);
                    var start = new Vector2(d >= 6 ? 300f : -300f, 820f);
                    dots[d].anchoredPosition = Vector2.Lerp(start, row, k * k) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * 80f);
                    dots[d].gameObject.SetActive(k < 1f);
                }
                yield return null;
            }
            foreach (var d in dots) Destroy(d.gameObject);
            // the row lit, the level popping
            var glow = UiFactory.Box("Glow", showLayer, new Vector2(0.5f, 0.5f), row + new Vector2(-180f, -20f), new Vector2(960f, 120f));
            glow.pivot = new Vector2(0.5f, 0.5f);
            var gi = UiFactory.Fill(glow, new Color(1f, 1f, 1f, 0.5f), UiSprites.Rounded, 2f);
            gi.raycastTarget = false;
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.1f);
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 0.4f)
            {
                gi.color = new Color(1f, 1f, 1f, 0.5f * (1f - t));
                partLevel[i].transform.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            partLevel[i].transform.localScale = Vector3.one;
            Destroy(glow.gameObject);
            if (level % 5 == 0)
            {
                // the big one: a new look
                var big = UiFactory.TextBox("NewLook", showLayer, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 160f), Loc.T("ibag.newLook"), 86f, Palette.UiGold, title: true);
                big.raycastTarget = false;
                Haptics.Medium();
                for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 0.35f) { big.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, 1f - (1f - t) * (1f - t)); yield return null; }
                big.transform.localScale = Vector3.one;
                yield return Wait(0.7f);
                Destroy(big.gameObject);
                EndShow();
                ItemViewer.Show(transform, Loc.T("ibag.newLook"), () => ItemPreview.ViewHero(ModelMaker)); // (foi close up: the new body)
                yield break;
            }
            EndShow();
        }

        private void EndShow()
        {
            showing = Mathf.Max(0, showing - 1);
            if (showing > 0) return;
            showLayer.gameObject.SetActive(false);
            skip = false;
            Refresh();
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
