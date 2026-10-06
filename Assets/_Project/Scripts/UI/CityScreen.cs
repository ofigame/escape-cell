using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Kind = SquashBot.UI.UiFactory.ButtonKind;

namespace SquashBot.UI
{
    /// <summary>
    /// The city's controls. Building view: the material tray (tabs Build / Decor / Garden, drag a piece onto the plot),
    /// the bar for the piece being placed (rotate, place, cancel), the bar for a selected piece (move, rotate, sell,
    /// repair), undo and "repair all". Walking view: the stick, the action button (repair, play) and a camera hint.
    /// Both: the residents' speech bubbles, the wishes panel and first-time tips.
    /// </summary>
    public class CityScreen : MonoBehaviour
    {
        public event Action BackPressed, HelpPressed, ModePressed, UndoPressed, RepairAllPressed;
        public event Action<CityPiece> PieceTapped;
        public event Action<CityPiece, Vector2> PieceDragStarted;
        public event Action<Vector2> PieceDragged, PieceDropped;
        public event Action GhostRotate, GhostConfirm, GhostCancel;
        public event Action SelMove, SelRotate, SelSell, SelRepair, SelClose;
        public event Action InteractPressed;
        public event Action<CityRequest> ClaimPressed;

        private UiScreen screen;
        private RectTransform root;
        private TextMeshProUGUI coinsText, starsText, titleText, modeLabel, undoLabel, repairAllLabel, wishesLabel;
        private Button undoButton, repairAllButton;
        private RectTransform buildGroup, walkGroup, tray, trayContent, ghostBar, selBar;
        private ScrollRect trayScroll;
        private TextMeshProUGUI ghostPrice, selName, selInfo, selSellLabel, selRepairLabel, interactLabel, tipText;
        private Button ghostConfirm, selRepair, interactButton;
        private RectTransform tipCard, wishesPanel, wishesList;
        private readonly Button[] tabs = new Button[3];
        private CityCategory tab = CityCategory.Build;
        private readonly List<Action> trayRefresh = new List<Action>();
        private readonly List<Bubble> bubbles = new List<Bubble>();
        private Action tipDone;

        public Joystick Stick { get; private set; }
        public bool Walking { get; private set; }
        public UiScreen Screen => screen;

        private class Bubble
        {
            public RectTransform rect;
            public TextMeshProUGUI text;
            public Image face;
            public CityRequest request;
        }

        public static CityScreen Create(Transform canvasRoot)
        {
            var screen = UiScreen.Create("City", canvasRoot, out var root);
            var city = root.gameObject.AddComponent<CityScreen>();
            city.screen = screen;
            city.root = root;
            city.Build();
            return city;
        }

        // ---------- Building the UI ----------

        private void Build()
        {
            var bubbleLayer = UiFactory.Stretch("Bubbles", root);
            for (int i = 0; i < City.MaxRequests; i++) bubbles.Add(MakeBubble(bubbleLayer));

            var bar = UiFactory.Rect("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -150f), Vector2.zero);
            UiFactory.MakeButton(bar, "<", Kind.Icon, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(116f, 116f), () => BackPressed?.Invoke(), 60f);
            titleText = UiFactory.TextBox("Title", bar, new Vector2(0.5f, 0.5f), new Vector2(-90f, 0f), new Vector2(400f, 100f), Loc.T("city.title"), 54f, Palette.UiText, title: true);
            titleText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var coins = UiFactory.Pill("Coins", bar, new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(230f, 96f), UiFactory.PillColor);
            UIController.CoinIcon(coins, new Vector2(52f, 0f));
            coinsText = UiFactory.TextBox("Value", coins, new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(130f, 86f), "0", 46f, Palette.UiGold, align: TextAlignmentOptions.Left);
            UiFactory.MakeButton(bar, "?", Kind.Icon, new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(104f, 104f), () => HelpPressed?.Invoke(), 60f);

            // Second row: wishes, stars, mode switch.
            var row = UiFactory.Rect("Row", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -270f), new Vector2(0f, -160f));
            var wishes = UiFactory.MakeButton(row, "", Kind.Secondary, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(390f, 100f), ToggleWishes, 32f);
            wishesLabel = wishes.GetComponentInChildren<TextMeshProUGUI>();
            var stars = UiFactory.Pill("Stars", row, new Vector2(0.5f, 0.5f), new Vector2(10f, 0f), new Vector2(170f, 90f), UiFactory.PillColor);
            stars.pivot = new Vector2(0.5f, 0.5f);
            var starIcon = UiFactory.Box("Star", stars, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(54f, 54f));
            starIcon.pivot = new Vector2(0f, 0.5f);
            UiFactory.Fill(starIcon, Palette.UiGold, UiSprites.Star).raycastTarget = false;
            starsText = UiFactory.TextBox("Value", stars, new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(100f, 80f), "0", 40f, Palette.UiGold, align: TextAlignmentOptions.Left);
            var mode = UiFactory.MakeButton(row, "", Kind.Primary, new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(300f, 100f), () => ModePressed?.Invoke(), 42f);
            modeLabel = mode.GetComponentInChildren<TextMeshProUGUI>();

            BuildBuildGroup();
            BuildWalkGroup();
            BuildWishesPanel();
            BuildTip();
        }

        private void BuildBuildGroup()
        {
            buildGroup = UiFactory.Stretch("Build", root);

            undoButton = UiFactory.MakeButton(buildGroup, "", Kind.Icon, new Vector2(0f, 1f), new Vector2(30f, -290f), new Vector2(250f, 90f), () => UndoPressed?.Invoke(), 34f);
            undoLabel = undoButton.GetComponentInChildren<TextMeshProUGUI>();
            repairAllButton = UiFactory.MakeButton(buildGroup, "", Kind.Gold, new Vector2(1f, 1f), new Vector2(-30f, -290f), new Vector2(430f, 90f), () => RepairAllPressed?.Invoke(), 28f);
            repairAllLabel = repairAllButton.GetComponentInChildren<TextMeshProUGUI>();

            // The tray.
            tray = UiFactory.Card("Tray", buildGroup, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1040f, 470f));
            string[] tabKeys = { "city.tab.build", "city.tab.decor", "city.tab.garden" };
            for (int i = 0; i < 3; i++)
            {
                var cat = (CityCategory)i;
                tabs[i] = UiFactory.MakeButton(tray, Loc.T(tabKeys[i]), Kind.Secondary, new Vector2(0f, 1f), new Vector2(24f + i * 336f, -18f), new Vector2(320f, 84f), () => SetTab(cat), 36f);
            }
            var viewport = UiFactory.Rect("Viewport", tray, Vector2.zero, Vector2.one, new Vector2(16f, 16f), new Vector2(-16f, -116f));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Fill(viewport, new Color(0f, 0f, 0f, 0.001f));
            trayContent = UiFactory.Rect("Content", viewport, Vector2.zero, new Vector2(0f, 1f));
            trayContent.pivot = new Vector2(0f, 0.5f);
            trayScroll = tray.gameObject.AddComponent<ScrollRect>();
            trayScroll.viewport = viewport;
            trayScroll.content = trayContent;
            trayScroll.vertical = false;
            trayScroll.movementType = ScrollRect.MovementType.Elastic;
            trayScroll.decelerationRate = 0.1f;

            // The piece being placed.
            ghostBar = UiFactory.Card("GhostBar", buildGroup, new Vector2(0.5f, 0f), new Vector2(0f, 510f), new Vector2(1000f, 150f));
            UiFactory.MakeButton(ghostBar, Loc.T("city.rotate"), Kind.Secondary, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(290f, 110f), () => GhostRotate?.Invoke(), 38f);
            ghostConfirm = UiFactory.MakeButton(ghostBar, "", Kind.Primary, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(330f, 110f), () => GhostConfirm?.Invoke(), 40f);
            ((RectTransform)ghostConfirm.transform).pivot = new Vector2(0.5f, 0.5f);
            ghostPrice = ghostConfirm.GetComponentInChildren<TextMeshProUGUI>();
            UiFactory.MakeButton(ghostBar, Loc.T("city.cancel"), Kind.Secondary, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(290f, 110f), () => GhostCancel?.Invoke(), 38f);

            // A placed piece that was tapped.
            selBar = UiFactory.Card("SelectBar", buildGroup, new Vector2(0.5f, 0f), new Vector2(0f, 510f), new Vector2(1040f, 250f));
            selName = UiFactory.TextBox("Name", selBar, new Vector2(0f, 1f), new Vector2(30f, -16f), new Vector2(640f, 60f), "", 40f, Palette.UiText, align: TextAlignmentOptions.Left);
            selInfo = UiFactory.TextBox("Info", selBar, new Vector2(0f, 1f), new Vector2(30f, -72f), new Vector2(880f, 46f), "", 30f, Palette.UiCyan, FontStyles.Normal, align: TextAlignmentOptions.Left);
            UiFactory.MakeButton(selBar, "X", Kind.Icon, new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(90f, 90f), () => SelClose?.Invoke(), 44f);
            float x = 22f;
            UiFactory.MakeButton(selBar, Loc.T("city.move"), Kind.Secondary, new Vector2(0f, 0f), new Vector2(x, 20f), new Vector2(230f, 100f), () => SelMove?.Invoke(), 34f);
            UiFactory.MakeButton(selBar, Loc.T("city.rotate"), Kind.Secondary, new Vector2(0f, 0f), new Vector2(x + 250f, 20f), new Vector2(230f, 100f), () => SelRotate?.Invoke(), 34f);
            var sell = UiFactory.MakeButton(selBar, "", Kind.Secondary, new Vector2(0f, 0f), new Vector2(x + 500f, 20f), new Vector2(240f, 100f), () => SelSell?.Invoke(), 32f);
            selSellLabel = sell.GetComponentInChildren<TextMeshProUGUI>();
            selRepair = UiFactory.MakeButton(selBar, "", Kind.Gold, new Vector2(0f, 0f), new Vector2(x + 760f, 20f), new Vector2(236f, 100f), () => SelRepair?.Invoke(), 32f);
            selRepairLabel = selRepair.GetComponentInChildren<TextMeshProUGUI>();

            ghostBar.gameObject.SetActive(false);
            selBar.gameObject.SetActive(false);
        }

        private void BuildWalkGroup()
        {
            walkGroup = UiFactory.Stretch("Walk", root);
            Stick = Joystick.Create(walkGroup, new Vector2(0f, 0f), new Vector2(30f, 40f), 440f);
            interactButton = UiFactory.MakeButton(walkGroup, "", Kind.Gold, new Vector2(1f, 0f), new Vector2(-50f, 110f), new Vector2(300f, 220f), () => InteractPressed?.Invoke(), 46f);
            interactLabel = interactButton.GetComponentInChildren<TextMeshProUGUI>();
            interactButton.gameObject.AddComponent<Pulse>();
            var hint = UiFactory.Pill("Hint", walkGroup, new Vector2(0.5f, 0f), new Vector2(0f, 470f), new Vector2(820f, 64f), UiFactory.PillColor);
            UiFactory.Text(hint, Loc.T("city.walkHint"), 28f, new Color(1f, 1f, 1f, 0.85f), style: FontStyles.Normal);
            walkGroup.gameObject.SetActive(false);
        }

        private Bubble MakeBubble(Transform parent)
        {
            var rect = UiFactory.Box("Bubble", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 112f));
            rect.pivot = new Vector2(0.5f, 0f);
            var bg = UiFactory.Fill(rect, new Color(1f, 1f, 1f, 0.94f), UiSprites.Rounded, 1.2f);
            var tail = UiFactory.Box("Tail", rect, new Vector2(0.5f, 0f), new Vector2(0f, -14f), new Vector2(30f, 30f));
            tail.pivot = new Vector2(0.5f, 0.5f);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UiFactory.Fill(tail, new Color(1f, 1f, 1f, 0.94f)).raycastTarget = false;
            var face = UiFactory.Box("Face", rect, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(62f, 62f));
            face.pivot = new Vector2(0f, 0.5f);
            var faceImg = UiFactory.Fill(face, Color.white, UiSprites.Rounded, 3f);
            faceImg.raycastTarget = false;
            var text = UiFactory.TextBox("Text", rect, new Vector2(0f, 0.5f), new Vector2(86f, 0f), new Vector2(280f, 96f), "", 26f, UiFactory.TextDark, align: TextAlignmentOptions.Left);
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.textWrappingMode = TextWrappingModes.Normal;
            var b = new Bubble { rect = rect, text = text, face = faceImg };
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => { AudioManager.PlaySfx(Sfx.Click, 0.6f); OpenWishes(); });
            rect.gameObject.SetActive(false);
            return b;
        }

        private void BuildWishesPanel()
        {
            wishesPanel = UiFactory.Stretch("Wishes", root);
            UiFactory.Dim(wishesPanel, new Color(0.06f, 0.05f, 0.18f, 0.55f));
            var card = UiFactory.Card("Card", wishesPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 1060f));
            card.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.TextBox("Title", card, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(760f, 90f), Loc.T("city.wishes"), 56f, Palette.UiText, title: true);
            UiFactory.TextBox("Sub", card, new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(880f, 50f), Loc.T("city.wishesSub"), 28f, Palette.UiCyan, FontStyles.Normal);
            UiFactory.MakeButton(card, "X", Kind.Icon, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(96f, 96f), CloseWishes, 46f);
            wishesList = UiFactory.Rect("List", card, Vector2.zero, Vector2.one, new Vector2(30f, 30f), new Vector2(-30f, -190f));
            wishesPanel.gameObject.SetActive(false);
        }

        private void BuildTip()
        {
            tipCard = UiFactory.Card("Tip", root, new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(960f, 300f));
            UiFactory.TextBox("Badge", tipCard, new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(300f, 50f), Loc.T("city.tipTitle"), 32f, Palette.UiGold, align: TextAlignmentOptions.Left);
            tipText = UiFactory.TextBox("Text", tipCard, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(900f, 120f), "", 34f, Palette.UiText, FontStyles.Normal);
            tipText.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.MakeButton(tipCard, Loc.T("city.ok"), Kind.Primary, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(300f, 84f), () =>
            {
                tipCard.gameObject.SetActive(false);
                var done = tipDone;
                tipDone = null;
                done?.Invoke();
            }, 38f);
            tipCard.gameObject.SetActive(false);
        }

        // ---------- Tray ----------

        private void SetTab(CityCategory c)
        {
            tab = c;
            for (int i = 0; i < 3; i++)
            {
                var img = tabs[i].targetGraphic as Image;
                if (img != null) img.color = i == (int)c ? new Color(0.45f, 0.85f, 1f, 0.55f) : new Color(0.62f, 0.62f, 1f, 0.2f);
            }
            BuildCards();
        }

        private void BuildCards()
        {
            for (int i = trayContent.childCount - 1; i >= 0; i--) Destroy(trayContent.GetChild(i).gameObject);
            trayRefresh.Clear();
            const float w = 200f, gap = 14f;
            int n = 0;
            // Unlocked pieces first, then the locked ones in the order they open.
            var list = new List<CityPiece>();
            foreach (var p in CityCatalog.All) if (p.category == tab && City.Unlocked(p)) list.Add(p);
            foreach (var p in CityCatalog.All) if (p.category == tab && !City.Unlocked(p)) list.Add(p);
            foreach (var p in list)
            {
                var piece = p;
                var card = UiFactory.Box(p.id, trayContent, new Vector2(0f, 0.5f), new Vector2(n * (w + gap), 0f), new Vector2(w, 316f));
                card.pivot = new Vector2(0f, 0.5f);
                var bg = UiFactory.Fill(card, new Color(0.2f, 0.19f, 0.4f, 0.95f), UiSprites.Rounded, 1.4f);
                var ring = UiFactory.Fill(UiFactory.Stretch("Ring", card), Palette.UiGold, UiSprites.Ring, 1.4f);
                ring.raycastTarget = false;
                var pic = UiFactory.Box("Pic", card, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(170f, 170f));
                var raw = pic.gameObject.AddComponent<RawImage>();
                raw.texture = CityThumbs.Get(p);
                raw.raycastTarget = false;
                var lockShade = UiFactory.Fill(UiFactory.Stretch("Shade", pic), new Color(0.08f, 0.07f, 0.18f, 0.6f));
                lockShade.raycastTarget = false;
                UiFactory.TextBox("Name", card, new Vector2(0.5f, 1f), new Vector2(0f, -184f), new Vector2(190f, 44f), Loc.T("city." + p.id), 26f, Palette.UiText);
                var info = UiFactory.TextBox("Info", card, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(190f, 80f), "", 26f, Palette.UiGold);
                info.textWrappingMode = TextWrappingModes.Normal;
                if (p.w > 1 || p.h > 1)
                    UiFactory.TextBox("Size", pic, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(70f, 34f), p.w + "x" + p.h, 24f, Color.white).rectTransform.pivot = new Vector2(1f, 1f);
                var drag = card.gameObject.AddComponent<TrayCard>();
                drag.Init(this, piece, trayScroll);
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                button.onClick.AddListener(() => { if (!drag.Dragged) PieceTapped?.Invoke(piece); });
                trayRefresh.Add(() =>
                {
                    string locked = City.LockText(piece);
                    lockShade.gameObject.SetActive(locked != null);
                    info.text = locked ?? piece.price.ToString();
                    info.color = locked != null ? new Color(1f, 1f, 1f, 0.6f) : SaveData.Coins >= piece.price ? Palette.UiGold : Palette.UiRed;
                    info.fontSize = locked != null ? 20f : 34f;
                    ring.gameObject.SetActive(Goal.Is(Goal.ForCity(piece)));
                });
                n++;
            }
            trayContent.sizeDelta = new Vector2(n * (w + gap), 0f);
            trayContent.anchoredPosition = Vector2.zero;
            foreach (var r in trayRefresh) r();
        }

        internal void BeginCardDrag(CityPiece p, Vector2 screen) => PieceDragStarted?.Invoke(p, screen);
        internal void CardDrag(Vector2 screen) => PieceDragged?.Invoke(screen);
        internal void EndCardDrag(Vector2 screen) => PieceDropped?.Invoke(screen);

        // ---------- Showing ----------

        public void Show(bool walking)
        {
            screen.Show();
            SetTab(tab);
            SetWalking(walking);
            CloseWishes();
            tipCard.gameObject.SetActive(false);
        }

        public void Hide()
        {
            screen.Hide(true);
            Stick?.Release();
        }

        public void SetWalking(bool walking)
        {
            Walking = walking;
            buildGroup.gameObject.SetActive(!walking);
            walkGroup.gameObject.SetActive(walking);
            modeLabel.text = Loc.T(walking ? "city.build" : "city.walk");
            titleText.text = Loc.T(walking ? "city.street" : "city.title");
            if (walking) { ghostBar.gameObject.SetActive(false); selBar.gameObject.SetActive(false); }
            Refresh();
        }

        /// <summary>Coins, stars, undo and repair counters, the tray's prices and locks.</summary>
        public void Refresh()
        {
            coinsText.text = SaveData.Coins.ToString();
            starsText.text = Progress.TotalStars(LevelCatalog.LevelCount).ToString();
            undoLabel.text = Loc.F("city.undo", City.UndoCount);
            undoButton.interactable = City.UndoCount > 0;
            int need = City.NeedingCare;
            repairAllButton.gameObject.SetActive(need > 0 && !Walking);
            int cost = City.RepairAllCost;
            repairAllLabel.text = cost > 0 ? Loc.F("city.repairAll", need, cost) : Loc.F("city.repairAllFree", need);
            int ready = 0;
            foreach (var r in City.Requests) if (City.RequestDone(r)) ready++;
            wishesLabel.text = ready > 0 ? Loc.F("city.wishesReady", ready) : Loc.F("city.wishesCount", City.Requests.Count);
            foreach (var r in trayRefresh) r();
            if (wishesPanel.gameObject.activeSelf) FillWishes();
        }

        public void ShowGhostBar(CityPiece piece, bool fits, bool moving)
        {
            selBar.gameObject.SetActive(false);
            ghostBar.gameObject.SetActive(true);
            bool afford = moving || SaveData.Coins >= piece.price;
            ghostPrice.text = moving ? Loc.T("city.put") : Loc.F("city.buy", piece.price);
            ghostConfirm.interactable = fits && afford;
            tray.gameObject.SetActive(false);
        }

        public void HideGhostBar()
        {
            ghostBar.gameObject.SetActive(false);
            tray.gameObject.SetActive(!Walking);
        }

        public bool GhostBarShown => ghostBar.gameObject.activeSelf;

        public void ShowSelection(CityPlaced p)
        {
            ghostBar.gameObject.SetActive(false);
            tray.gameObject.SetActive(false);
            selBar.gameObject.SetActive(true);
            var piece = p.Piece;
            selName.text = Loc.T("city." + piece.id);
            var stage = City.Stage(p);
            string care = piece.CareDays <= 0f ? Loc.T("city.noCare")
                : stage == CareStage.Fresh ? Loc.T("city.fresh") : stage == CareStage.Dusty ? Loc.T("city.dusty") : Loc.T("city.needsCare");
            string perk = piece.perk != CityPerk.None ? "  ·  " + Loc.T("city.perk." + piece.perk) : "";
            selInfo.text = care + perk;
            selInfo.color = stage == CareStage.NeedsCare ? Palette.UiRed : stage == CareStage.Dusty ? Palette.UiGold : Palette.UiCyan;
            selSellLabel.text = Loc.F("city.sell", City.SellValue(p));
            bool needs = City.NeedsAttention(p);
            selRepair.gameObject.SetActive(needs);
            int fee = City.CareFee(p);
            selRepairLabel.text = fee > 0 ? Loc.F("city.repair", fee) : Loc.T("city.repairFree");
        }

        public void HideSelection()
        {
            selBar.gameObject.SetActive(false);
            tray.gameObject.SetActive(!Walking);
        }

        /// <summary>The walking view's action button: null hides it.</summary>
        public void SetInteract(string label)
        {
            bool on = label != null;
            if (interactButton.gameObject.activeSelf != on) interactButton.gameObject.SetActive(on);
            if (on) interactLabel.text = label;
        }

        // ---------- Bubbles ----------

        /// <summary>Places the speech bubbles over the residents with wishes (screen points; null = not visible).</summary>
        public void SetBubble(int slot, CityRequest r, Vector2? screenPoint, Canvas canvas)
        {
            var b = bubbles[slot];
            bool on = r != null && screenPoint.HasValue && !wishesPanel.gameObject.activeSelf;
            if (b.rect.gameObject.activeSelf != on) b.rect.gameObject.SetActive(on);
            if (!on) return;
            if (b.request != r)
            {
                b.request = r;
                b.face.color = RobotLooks.BodyColor(Residents.World(r.resident)) * 1.1f;
            }
            bool done = City.RequestDone(r);
            b.text.text = (done ? "<color=#1E9E5A>" + Loc.T("city.wishDone") + "</color>\n" : "") + City.RequestText(r);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)b.rect.parent, screenPoint.Value, null, out var local);
            b.rect.anchoredPosition = local;
        }

        public int BubbleCount => bubbles.Count;

        // ---------- Wishes panel ----------

        private void ToggleWishes()
        {
            if (wishesPanel.gameObject.activeSelf) CloseWishes();
            else OpenWishes();
        }

        public void OpenWishes()
        {
            wishesPanel.gameObject.SetActive(true);
            wishesPanel.SetAsLastSibling();
            FillWishes();
        }

        private void CloseWishes() => wishesPanel.gameObject.SetActive(false);

        private void FillWishes()
        {
            for (int i = wishesList.childCount - 1; i >= 0; i--) Destroy(wishesList.GetChild(i).gameObject);
            if (City.Requests.Count == 0)
            {
                UiFactory.TextBox("Empty", wishesList, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(860f, 120f), Loc.T("city.noWishes"), 34f, Palette.UiText, FontStyles.Normal)
                    .textWrappingMode = TextWrappingModes.Normal;
                return;
            }
            float y = 0f;
            foreach (var r in City.Requests)
            {
                var req = r;
                var row = UiFactory.Box("Wish", wishesList, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(880f, 250f));
                UiFactory.Fill(row, new Color(0.12f, 0.1f, 0.26f, 0.9f), UiSprites.Rounded, 1.4f).raycastTarget = false;
                var face = UiFactory.Box("Face", row, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(80f, 80f));
                UiFactory.Fill(face, RobotLooks.BodyColor(Residents.World(r.resident)) * 1.1f, UiSprites.Rounded, 3f).raycastTarget = false;
                string best = City.IsBestFriend(r.resident) ? Loc.T("city.bestFriend") : Loc.F("city.friendship", City.Data.granted[r.resident], City.WishesForBestFriend);
                UiFactory.TextBox("Name", row, new Vector2(0f, 1f), new Vector2(116f, -18f), new Vector2(720f, 50f), Residents.Names[r.resident] + "  ·  " + best, 30f, Palette.UiCyan, align: TextAlignmentOptions.Left);
                var text = UiFactory.TextBox("Text", row, new Vector2(0f, 1f), new Vector2(116f, -66f), new Vector2(740f, 90f), City.RequestText(r), 32f, Palette.UiText, FontStyles.Normal, align: TextAlignmentOptions.Left);
                text.textWrappingMode = TextWrappingModes.Normal;
                UiFactory.Bar(row, new Vector2(0f, 0f), new Vector2(116f, 36f), new Vector2(380f, 30f), Palette.UiGold, out var fill);
                UiFactory.SetBar(fill, City.RequestProgress(r));
                bool done = City.RequestDone(r);
                var claim = UiFactory.MakeButton(row, done ? Loc.F("city.claim", r.reward) : Loc.F("city.reward", r.reward), done ? Kind.Gold : Kind.Secondary,
                    new Vector2(1f, 0f), new Vector2(-20f, 18f), new Vector2(300f, 90f), () => ClaimPressed?.Invoke(req), 32f);
                claim.interactable = done;
                if (done) claim.gameObject.AddComponent<Pulse>();
                y -= 270f;
            }
        }

        // ---------- Tips ----------

        public void ShowTip(string text, Action done = null)
        {
            tipText.text = text;
            tipDone = done;
            tipCard.gameObject.SetActive(true);
            tipCard.SetAsLastSibling();
        }

        public bool TipShown => tipCard.gameObject.activeSelf;

        /// <summary>True when the screen point is on one of this screen's controls (so the plot underneath ignores it).</summary>
        public bool IsOverUi(Vector2 screenPoint)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPoint };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            return hits.Count > 0;
        }
    }

    /// <summary>
    /// A tray card: a mostly sideways drag scrolls the tray, a drag upward lifts the piece out onto the plot.
    /// </summary>
    public class TrayCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private CityScreen owner;
        private CityPiece piece;
        private ScrollRect scroll;
        private bool lifting;

        public bool Dragged { get; private set; }

        public void Init(CityScreen screen, CityPiece p, ScrollRect tray)
        {
            owner = screen;
            piece = p;
            scroll = tray;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            Dragged = true;
            lifting = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x) && e.delta.y > 0f && City.Unlocked(piece);
            if (lifting) owner.BeginCardDrag(piece, e.position);
            else scroll.OnBeginDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (lifting) owner.CardDrag(e.position);
            else scroll.OnDrag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (lifting) owner.EndCardDrag(e.position);
            else scroll.OnEndDrag(e);
            lifting = false;
            Invoke(nameof(ClearDragged), 0.05f);
        }

        private void ClearDragged() => Dragged = false;
    }
}
