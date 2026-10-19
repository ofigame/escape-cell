using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrickMuseum.UI
{
    /// <summary>The workshop screen's widgets (layout only; the game wires the behaviour).</summary>
    public sealed class WorkshopUi
    {
        public Canvas canvas, overlay;
        public PointerArea touch;
        public RectTransform hud;
        public TextMeshProUGUI modelName, progress, help, hintText, bagCount, toast;
        public CanvasGroup toastGroup;
        public RectTransform heartsRow;
        public readonly List<Image> hearts = new List<Image>();
        public Button cityBtn, hintBtn, bagBtn;
        public RectTransform refCard, hintBar;
        public RawImage refImage;
        public RectTransform bagSheet;
        public PointerArea bagArea;
        public BagView bag;
        public Button spreadBtn, bagClose;
        public GameObject roulette;
        public RawImage rouImage;
        public TextMeshProUGUI rouName, rouKicker;
        public Image rouCard;
        public GameObject refModal;
        public RawImage refBigImage;
        public PointerArea refDrag;
        public TextMeshProUGUI refKicker, refName, refInfo;
        public Button refGo;
        public GameObject win;
        public TextMeshProUGUI winTitle, winInfo;
        public readonly List<Image> winStars = new List<Image>();
        public Button exhibitBtn, newBtn;
        public GameObject fail;
        public TextMeshProUGUI failInfo;
        public Button failAdBtn, failOkBtn;
        public GameObject ad;
        public TextMeshProUGUI adCount;
        public GameObject collection;
        public RectTransform collectionList;
        public Button collectionClose;

        public static readonly Color BagBrown = new Color(0.55f, 0.34f, 0.17f);

        public void Build(Transform parent)
        {
            canvas = Ui.MakeCanvas(parent, 10);
            var root = canvas.transform;

            var t = Ui.Plain(root, "Touch", new Color(0, 0, 0, 0));
            t.rectTransform.Fill();
            touch = t.gameObject.AddComponent<PointerArea>();

            hud = Ui.Rect(root, "Hud"); hud.Fill();

            // top bar
            cityBtn = Ui.Button(hud, "Şehir", Color.white, Ui.Ink, 34, () => { });
            ((RectTransform)cityBtn.transform).Place(new Vector2(0, 1), new Vector2(200, 96), new Vector2(24, -40));
            var nameChip = Ui.Panel(hud, "NameChip", Ui.Paper, false);
            nameChip.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(420, 110), new Vector2(-30, -33));
            modelName = Ui.Text(nameChip.transform, "Atölye", 36, Ui.Ink, true);
            modelName.rectTransform.Fill(0);
            modelName.rectTransform.offsetMin = new Vector2(12, 44);
            modelName.rectTransform.offsetMax = new Vector2(-12, -6);
            progress = Ui.Text(nameChip.transform, "", 28, new Color(0.35f, 0.38f, 0.45f), true);
            progress.rectTransform.Fill(0);
            progress.rectTransform.offsetMin = new Vector2(12, 6);
            progress.rectTransform.offsetMax = new Vector2(-12, -66);
            hintBtn = Ui.Button(hud, "İpucu", new Color(1f, 0.93f, 0.55f), Ui.Ink, 34, () => { });
            ((RectTransform)hintBtn.transform).Place(new Vector2(1, 1), new Vector2(200, 96), new Vector2(-24, -40));

            heartsRow = Ui.Rect(hud, "Hearts");
            heartsRow.Place(new Vector2(0.5f, 1), new Vector2(480, 60), new Vector2(0, -172));
            var hl = heartsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10; hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = false; hl.childControlHeight = false;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

            // target card
            var card = Ui.Panel(hud, "TargetCard", Color.white);
            refCard = card.rectTransform.Place(new Vector2(0, 1), new Vector2(250, 290), new Vector2(24, -224));
            refImage = Ui.Rect(card.transform, "Live").gameObject.AddComponent<RawImage>();
            refImage.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(230, 230), new Vector2(0, -10));
            refImage.raycastTarget = false;
            var hedef = Ui.Text(card.transform, "HEDEF  (büyüt)", 26, new Color(0.3f, 0.36f, 0.42f), true);
            hedef.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(240, 44), new Vector2(0, 6));

            // hint bar
            var hb = Ui.Panel(hud, "HintBar", new Color(1f, 0.95f, 0.69f), false);
            hintBar = hb.rectTransform.Place(new Vector2(1, 1), new Vector2(760, 170), new Vector2(-24, -224));
            hintText = Ui.Text(hb.transform, "", 34, Ui.Ink, true);
            hintText.rectTransform.Fill(18);
            hintBar.gameObject.SetActive(false);

            help = Ui.Text(hud, "", 34, Ui.Ink, true);
            var hp = Ui.Panel(hud, "HelpBg", new Color(1, 1, 1, 0.88f), false);
            hp.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(900, 120), new Vector2(0, 300));
            help.transform.SetParent(hp.transform, false);
            help.rectTransform.Fill(14);

            // bag button
            var bb = Ui.Panel(hud, "BagButton", BagBrown);
            bb.sprite = Icons.Circle; bb.type = Image.Type.Simple;
            bb.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(230, 230), new Vector2(0, 50));
            bagBtn = bb.gameObject.AddComponent<Button>();
            var bl = Ui.Text(bb.transform, "ÇANTA", 40, Color.white, false, true);
            bl.rectTransform.Fill(20);
            ButtonIcon.Add(bb.gameObject, bl, Icons.Bag, 40f);
            var badge = Ui.Panel(bb.transform, "Badge", new Color(0.9f, 0.22f, 0.21f), false);
            badge.rectTransform.Place(new Vector2(1, 1), new Vector2(90, 64), new Vector2(10, 4));
            bagCount = Ui.Text(badge.transform, "0", 36, Color.white, true);
            bagCount.rectTransform.Fill(4);

            BuildBag(root);
            // modals and the toast live on their own canvas, shown over the city too
            overlay = Ui.MakeCanvas(parent, 20);
            root = overlay.transform;
            BuildModals(root);

            var tg = Ui.Panel(root, "Toast", new Color(0.08f, 0.08f, 0.1f, 0.88f), false);
            tg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(900, 110), new Vector2(0, -250));
            toast = Ui.Text(tg.transform, "", 34, Color.white, true);
            toast.rectTransform.Fill(16);
            toastGroup = tg.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        }

        private void BuildBag(Transform root)
        {
            var sheet = Ui.Panel(root, "Bag", new Color(0.48f, 0.3f, 0.14f));
            bagSheet = sheet.rectTransform;
            bagSheet.anchorMin = new Vector2(0, 0); bagSheet.anchorMax = new Vector2(1, 0.6f);
            bagSheet.offsetMin = new Vector2(0, -60); bagSheet.offsetMax = Vector2.zero;
            var title = Ui.Text(sheet.transform, "ÇANTA", 46, Color.white, false, true);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(400, 80), new Vector2(0, -12));
            var tip = Ui.Text(sheet.transform, "Boş yere parmağını sürt: parçalar dağılır", 26, new Color(1, 0.92f, 0.78f), true);
            tip.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(700, 40), new Vector2(0, -86));
            spreadBtn = Ui.Button(sheet.transform, "Dağıt", new Color(0, 0, 0, 0.28f), Color.white, 32, () => { });
            ((RectTransform)spreadBtn.transform).Place(new Vector2(0, 1), new Vector2(180, 84), new Vector2(24, -18));
            bagClose = Ui.Button(sheet.transform, "Kapat", new Color(0, 0, 0, 0.28f), Color.white, 32, () => { });
            ((RectTransform)bagClose.transform).Place(new Vector2(1, 1), new Vector2(180, 84), new Vector2(-24, -18));
            var inner = Ui.Panel(sheet.transform, "Inside", new Color(0.32f, 0.19f, 0.08f));
            inner.rectTransform.Fill(24);
            inner.rectTransform.offsetMax = new Vector2(-24, -140);
            inner.rectTransform.offsetMin = new Vector2(24, 84);
            inner.gameObject.AddComponent<RectMask2D>();
            bagArea = inner.gameObject.AddComponent<PointerArea>();
            bag = new BagView(inner.rectTransform);
            sheet.gameObject.SetActive(false);
        }

        private void BuildModals(Transform root)
        {
            // roulette
            var card = Ui.Modal(root, "Roulette", 820, 1040, out roulette);
            rouCard = card.GetComponent<Image>();
            rouKicker = Ui.Text(card, "Model seçiliyor…", 36, new Color(0.35f, 0.38f, 0.45f), true);
            rouKicker.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(760, 60), new Vector2(0, -30));
            var bg = Ui.Panel(card, "PicBg", new Color(0.9f, 0.93f, 0.95f), false);
            bg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(740, 740), new Vector2(0, -100));
            rouImage = Ui.Rect(bg.transform, "Pic").gameObject.AddComponent<RawImage>();
            rouImage.rectTransform.Fill(10);
            rouName = Ui.Text(card, "?", 64, Ui.Ink, false, true);
            rouName.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(760, 110), new Vector2(0, 50));
            roulette.SetActive(false);

            // target model (big, turnable)
            card = Ui.Modal(root, "Target", 860, 1360, out refModal);
            refKicker = Ui.Text(card, "BUNU YAPACAKSIN", 34, new Color(0.35f, 0.38f, 0.45f), true);
            refKicker.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(800, 56), new Vector2(0, -26));
            refName = Ui.Text(card, "", 68, Ui.Ink, false, true);
            refName.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(800, 96), new Vector2(0, -82));
            var vbg = Ui.Panel(card, "ViewBg", new Color(0.9f, 0.93f, 0.95f));
            vbg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(780, 780), new Vector2(0, -186));
            refBigImage = Ui.Rect(vbg.transform, "Live").gameObject.AddComponent<RawImage>();
            refBigImage.rectTransform.Fill(6);
            refBigImage.raycastTarget = false;
            refDrag = vbg.gameObject.AddComponent<PointerArea>();
            var turn = Ui.Text(card, "Parmağınla çevirerek her yönden bak", 28, new Color(0.35f, 0.38f, 0.45f), true);
            turn.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(800, 50), new Vector2(0, 300));
            refInfo = Ui.Text(card, "", 32, new Color(0.25f, 0.28f, 0.34f), true);
            refInfo.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(800, 90), new Vector2(0, 200));
            refGo = Ui.Button(card, "Başla", Ui.Yellow, Ui.Ink, 52, () => { });
            ((RectTransform)refGo.transform).Place(new Vector2(0.5f, 0), new Vector2(780, 140), new Vector2(0, 40));
            refModal.SetActive(false);

            // win
            card = Ui.Modal(root, "Win", 860, 900, out win);
            winTitle = Ui.Text(card, "", 60, Ui.Ink, false, true);
            winTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(800, 110), new Vector2(0, -40));
            var stars = Ui.Rect(card, "Stars").Place(new Vector2(0.5f, 1), new Vector2(600, 150), new Vector2(0, -170));
            for (int i = 0; i < 3; i++)
            {
                var s = Ui.Plain(stars, "Star", Color.white, false);
                s.sprite = Icons.Star;
                s.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(140, 140), new Vector2((i - 1) * 170, 0));
                winStars.Add(s);
            }
            winInfo = Ui.Text(card, "", 34, new Color(0.3f, 0.33f, 0.4f), true);
            winInfo.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(800, 100), new Vector2(0, -340));
            exhibitBtn = Ui.Button(card, "Şehre yerleştir", Ui.Yellow, Ui.Ink, 50, () => { });
            ((RectTransform)exhibitBtn.transform).Place(new Vector2(0.5f, 0), new Vector2(780, 150), new Vector2(0, 210));
            newBtn = Ui.Button(card, "Envantere koy", new Color(0.92f, 0.93f, 0.95f), Ui.Ink, 44, () => { });
            ((RectTransform)newBtn.transform).Place(new Vector2(0.5f, 0), new Vector2(780, 130), new Vector2(0, 50));
            win.SetActive(false);

            // out of hearts
            card = Ui.Modal(root, "Fail", 860, 820, out fail);
            var ft = Ui.Text(card, "Hakların bitti!", 60, new Color(0.85f, 0.2f, 0.2f), false, true);
            ft.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(800, 110), new Vector2(0, -40));
            failInfo = Ui.Text(card, "", 36, new Color(0.3f, 0.33f, 0.4f), true);
            failInfo.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(780, 260), new Vector2(0, -170));
            failAdBtn = Ui.Button(card, "Reklam izle: +2 hak", Ui.Yellow, Ui.Ink, 46, () => { });
            ((RectTransform)failAdBtn.transform).Place(new Vector2(0.5f, 0), new Vector2(780, 150), new Vector2(0, 200));
            failOkBtn = Ui.Button(card, "Şehre dön", new Color(0.92f, 0.93f, 0.95f), Ui.Ink, 44, () => { });
            ((RectTransform)failOkBtn.transform).Place(new Vector2(0.5f, 0), new Vector2(780, 130), new Vector2(0, 40));
            fail.SetActive(false);

            // test ad
            card = Ui.Modal(root, "Ad", 700, 600, out ad);
            card.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f);
            var at = Ui.Text(card, "REKLAM (test)", 40, new Color(1, 1, 1, 0.7f), true);
            at.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(640, 80), new Vector2(0, -40));
            adCount = Ui.Text(card, "3", 160, Color.white, false, true);
            adCount.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(400, 260), new Vector2(0, -20));
            ad.SetActive(false);

            // collection
            card = Ui.Modal(root, "Collection", 960, 1640, out collection);
            var ct = Ui.Text(card, "Koleksiyon", 60, Ui.Ink, false, true);
            ct.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(900, 100), new Vector2(0, -24));
            var cs = Ui.Text(card, "Her alanın çarkı, o türden sıradaki 3 modelden birini seçer", 28, new Color(0.35f, 0.38f, 0.45f), true);
            cs.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(900, 50), new Vector2(0, -120));
            var scroll = Ui.Rect(card, "Scroll");
            scroll.anchorMin = new Vector2(0, 0); scroll.anchorMax = new Vector2(1, 1);
            scroll.offsetMin = new Vector2(24, 180); scroll.offsetMax = new Vector2(-24, -180);
            scroll.gameObject.AddComponent<RectMask2D>();
            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            var img = scroll.gameObject.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0.001f);
            collectionList = Ui.Rect(scroll, "List");
            collectionList.anchorMin = new Vector2(0, 1); collectionList.anchorMax = new Vector2(1, 1); collectionList.pivot = new Vector2(0.5f, 1);
            collectionList.offsetMin = collectionList.offsetMax = Vector2.zero;
            var vl = collectionList.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 14; vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = false;
            collectionList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = collectionList; sr.horizontal = false; sr.viewport = scroll;
            collectionClose = Ui.Button(card, "Kapat", new Color(0.92f, 0.93f, 0.95f), Ui.Ink, 44, () => { });
            ((RectTransform)collectionClose.transform).Place(new Vector2(0.5f, 0), new Vector2(880, 130), new Vector2(0, 30));
            collection.SetActive(false);
        }

        public void SetHearts(int total, int left)
        {
            while (hearts.Count < total)
            {
                var h = Ui.Plain(heartsRow, "Heart", Color.white, false);
                h.sprite = Icons.Heart;
                h.rectTransform.sizeDelta = new Vector2(56, 56);
                hearts.Add(h);
            }
            for (int i = 0; i < hearts.Count; i++)
            {
                hearts[i].gameObject.SetActive(i < total);
                hearts[i].color = i < left ? new Color(0.92f, 0.25f, 0.3f) : new Color(0.75f, 0.75f, 0.78f, 0.7f);
            }
        }
    }
}
