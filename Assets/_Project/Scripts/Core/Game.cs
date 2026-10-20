using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Build;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.UI;
using BrickMuseum.Workshop;
using BrickMuseum.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BrickMuseum.Core
{
    /// <summary>
    /// The whole game, built from code at start. Two places: the city seen from above (tap a slot, choose to build or to
    /// place something from the inventory) and the workbench (the roulette picks a model for that slot, find its pieces in
    /// the bag, snap them on, limited mistakes). Finished models stand in the city.
    /// </summary>
    public sealed partial class Game : MonoBehaviour
    {
        private enum Mode { City, Workshop, Walk, Build }

        private Camera cam;
        private readonly OrbitCam orbit = new OrbitCam();
        private Bench bench;
        private readonly WorkshopUi wu = new WorkshopUi();
        private Mode mode;
        private float time;

        // the model being built and the slot it is for
        private ModelDef current;
        private Slot buildSlot;
        private int heartsTotal, heartsLeft, mistakes;
        private bool adUsed, spinning, busy;

        // the piece in hand
        private BagItem held;
        private int heldPointer = int.MinValue;
        private Target snapped, wrongSpot;
        private bool stirring;
        private bool bagOpen;
        private float bagShown; // 0 closed .. 1 open
        private float toastUntil;
        private readonly Dictionary<int, Vector2> touches = new Dictionary<int, Vector2>();

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            cam = new GameObject("Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.SetParent(transform, false);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 6000f;
            cam.cullingMask = ~(1 << Previews.PictureLayer);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.gameObject.AddComponent<AudioListener>();

            Sfx.Init(gameObject);
            Landscape.Build(transform);
            bench = new Bench(transform);
            BuildCity();
            wu.Build(transform);
            cu.Build(transform);
            Wire();
            WireCity();
            WireWalk();
            WireCharacter();
            WireBuild();
            WireCoach();
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;
            EnterCity(null);
        }

        private void Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-shots");
            if (i >= 0 && i + 1 < args.Length) StartCoroutine(Tour(args[i + 1]));
            int k = System.Array.IndexOf(args, "-pictures");
            if (k >= 0 && k + 1 < args.Length && Debug.isDebugBuild) SavePictures(args[k + 1]);
        }

        private void Wire()
        {
            wu.bagBtn.onClick.AddListener(OpenBag);
            wu.bagClose.onClick.AddListener(() => CloseBag());
            wu.spreadBtn.onClick.AddListener(() => { wu.bag.Spread(); Sfx.Play("rustle"); });
            wu.hintBtn.onClick.AddListener(() => StartCoroutine(HintAd()));
            wu.cityBtn.onClick.AddListener(LeaveWorkshop);
            wu.collectionClose.onClick.AddListener(() => wu.collection.SetActive(false));
            wu.refGo.onClick.AddListener(() => wu.refModal.SetActive(false));
            wu.refCard.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowTarget(false));
            wu.refDrag.Drag = e => Previews.TurnLive(-e.delta.x * 0.4f);
            wu.exhibitBtn.onClick.AddListener(() => { wu.win.SetActive(false); PlaceFinished(); });
            wu.newBtn.onClick.AddListener(() => { wu.win.SetActive(false); KeepFinished(); });
            wu.failOkBtn.onClick.AddListener(() => { wu.fail.SetActive(false); Progress.Failed(current.id); ClearPending(); EnterCity(buildSlot); });
            wu.failAdBtn.onClick.AddListener(() => StartCoroutine(ContinueAd()));
            wu.refImage.texture = Previews.Live;
            wu.refBigImage.texture = Previews.Live;

            wu.touch.Down = e => touches[e.pointerId] = e.position;
            wu.touch.Up = e => touches.Remove(e.pointerId);
            wu.touch.Drag = OrbitDrag;
            wu.touch.Scroll = s => orbit.Zoom(s > 0 ? 0.9f : 1.1f);

            wu.bagArea.Down = BagDown;
            wu.bagArea.Drag = BagDrag;
            wu.bagArea.Up = BagUp;
        }

        private static void ShadowDistance(float d)
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset a) a.shadowDistance = d;
        }

        // ================= workshop =================

        private void EnterWorkshop()
        {
            mode = Mode.Workshop;
            cu.canvas.gameObject.SetActive(false);
            wu.canvas.gameObject.SetActive(true);
            cam.fieldOfView = Screen.width < Screen.height ? 58f : 45f;
            ShadowDistance(160f);
            bench.Clear();
            current = null;
            CloseBag(true);
            wu.hintBar.gameObject.SetActive(false);
            wu.bag.glowKey = null;
            wu.refCard.gameObject.SetActive(false);
            wu.bagBtn.gameObject.SetActive(false);
            wu.heartsRow.gameObject.SetActive(false);
            wu.help.transform.parent.gameObject.SetActive(false);
            wu.modelName.text = "Atölye";
            wu.progress.text = "";
            orbit.Fit(cam, 16, 16, 3);
        }

        /// <summary>The workshop's "Şehir" button: an unfinished build is simply dropped (no penalty).</summary>
        private void LeaveWorkshop()
        {
            if (spinning) return;
            bool building = current != null && !bench.Done;
            EnterCity(buildSlot);
            if (building) { SavePending(); Toast("Yapım kaydedildi · İnşaya devam et ile sürdür", 2.6f); }
        }

        private IEnumerator Spin(List<ModelDef> pool)
        {
            if (spinning || pool.Count == 0) yield break;
            spinning = true;
            var chosen = pool[Random.Range(0, pool.Count)];
            // the reel shows models of the same kind, so the player sees what this slot can get
            var reel = ModelCatalog.All.Where(m => m.kind == chosen.kind).ToList();
            int steps = 22 + Random.Range(0, 6);
            var seq = new List<ModelDef>();
            for (int i = 0; i < steps - 1; i++)
            {
                ModelDef m;
                do m = reel[Random.Range(0, reel.Count)]; while (seq.Count > 0 && m == seq[seq.Count - 1] && reel.Count > 1);
                seq.Add(m);
            }
            seq.Add(chosen);
            wu.roulette.SetActive(true);
            wu.rouCard.color = Color.white;
            wu.rouKicker.text = buildSlot != null ? CityLayout.SlotName(buildSlot) + " için model seçiliyor…" : "Model seçiliyor…";
            for (int n = 0; n < seq.Count; n++)
            {
                wu.rouImage.texture = Previews.Model(seq[n]);
                wu.rouName.text = seq[n].name;
                Sfx.Play("tick");
                yield return new WaitForSeconds(0.05f + Mathf.Pow((n + 1f) / seq.Count, 3f) * 0.38f);
            }
            wu.rouCard.color = new Color(1f, 0.95f, 0.6f);
            wu.rouKicker.text = "BUNU YAPACAKSIN!";
            Sfx.Play("win");
            yield return new WaitForSeconds(1.3f);
            wu.roulette.SetActive(false);
            spinning = false;
            StartLevel(chosen);
        }

        private void StartLevel(ModelDef m, PendingBuild resume = null)
        {
            current = m;
            bench.Begin(m);
            if (resume != null) RestorePending(resume);
            MakeBags();
            FillBag();
            wu.bag.glowKey = null;
            if (resume == null) { heartsTotal = heartsLeft = m.Hearts; mistakes = 0; adUsed = false; }
            Previews.ShowLive(m);
            orbit.Fit(cam, m.sx, m.sz, m.height);
            wu.refCard.gameObject.SetActive(true);
            wu.bagBtn.gameObject.SetActive(true);
            wu.heartsRow.gameObject.SetActive(true);
            Help("Çantayı aç, gereken parçayı tezgâhtaki parlak yere sürükle");
            UpdateHud();
            ShowTarget(resume == null);
            SavePending();
            if (resume != null && heartsLeft <= 0) { wu.refModal.SetActive(false); ShowOutOfHearts(); }
        }

        // ---------- the bags: a big model comes in bags of about two layers each ----------

        private readonly List<List<Target>> bags = new List<List<Target>>();
        private int bagIndex;

        /// <summary>Groups the model's pieces by layer (two brick layers a bag), merging thin groups and splitting fat ones.</summary>
        private void MakeBags()
        {
            bags.Clear();
            // a piece goes in the bag of its layer, or later when it waits for pieces higher up (a sticker on an upper wall)
            var key = new Dictionary<Target, int>();
            int Key(Target t) { if (key.TryGetValue(t, out var k)) return k; k = t.p.y / 6; foreach (var d in t.deps) k = Mathf.Max(k, Key(d)); return key[t] = k; }
            var layers = bench.targets.GroupBy(Key).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            var bag = new List<Target>();
            foreach (var layer in layers)
            {
                if (bag.Count > 0 && bag.Count + layer.Count > 32) { bags.Add(bag); bag = new List<Target>(); }
                bag.AddRange(layer);
                if (bag.Count >= 14) { bags.Add(bag); bag = new List<Target>(); }
            }
            if (bag.Count > 0) { if (bags.Count > 0 && bag.Count < 6) bags[bags.Count - 1].AddRange(bag); else bags.Add(bag); }
            // a resumed build starts at the first bag with pieces still to go on
            bagIndex = 0;
            while (bagIndex < bags.Count - 1 && bags[bagIndex].All(t => t.placed)) bagIndex++;
        }

        private bool BagDone => bagIndex < bags.Count - 1 && bags[bagIndex].All(t => t.placed);

        /// <summary>The current bag: its pieces still to go on, plus a few that belong to no spot.</summary>
        private void FillBag()
        {
            var group = bags.Count > 0 ? bags[bagIndex] : bench.targets;
            var kinds = group.Where(t => !t.placed).Select(t => t.p.Kind()).ToList();
            kinds.AddRange(Distractors(current, Mathf.Clamp(2 + kinds.Count / 6, 2, 8)));
            wu.bag.Fill(kinds);
            wu.bag.glowKey = null;
            UpdateHud();
        }

        private static List<Piece> Distractors(ModelDef m, int n)
        {
            var keys = new HashSet<string>(m.pieces.Select(p => p.Kind().key));
            var src = m.pieces.Where(p => p.t != "sticker" && p.t != "wheel" && p.t != "disc" && p.t != "spire").ToList();
            var colors = Palette.Keys.Where(c => c != "gold").ToList();
            int[][] sizes = { new[] { 1, 1 }, new[] { 1, 2 }, new[] { 1, 3 }, new[] { 1, 4 }, new[] { 2, 2 }, new[] { 2, 3 }, new[] { 2, 4 }, new[] { 1, 6 }, new[] { 2, 6 } };
            var output = new List<Piece>();
            for (int guard = 0; output.Count < n && guard < 400 && src.Count > 0; guard++)
            {
                var p = src[Random.Range(0, src.Count)];
                int w = p.w, d = p.d; string c = p.c;
                if (p.t == "brick" && Random.value < 0.5f) { var s = sizes[Random.Range(0, sizes.Length)]; w = s[0]; d = s[1]; }
                else c = colors[Random.Range(0, colors.Count)];
                var k = Piece.Make(p.t, c, 0, 0, 0, w, d, p.h, null, p.art).Kind();
                if (!keys.Add(k.key)) continue;
                output.Add(k);
            }
            return output;
        }

        private void ShowTarget(bool first)
        {
            if (current == null) return;
            wu.refKicker.text = first ? "BUNU YAPACAKSIN" : "HEDEF MODEL";
            wu.refGo.SetLabel(first ? "Başla" : "Devam");
            wu.refName.text = current.name;
            wu.refInfo.text = $"{current.cat} · {current.pieces.Count} parça · {current.Hearts} hata hakkı\nÇantada {wu.bag.Count} parça var" + (bags.Count > 1 ? $" · torba {bagIndex + 1}/{bags.Count}" : "");
            wu.refModal.SetActive(true);
        }

        private void UpdateHud()
        {
            if (current == null) return;
            wu.modelName.text = current.name;
            wu.progress.text = $"{bench.PlacedCount} / {bench.targets.Count} parça";
            wu.SetHearts(heartsTotal, heartsLeft);
            wu.bagCount.text = wu.bag.Count.ToString();
        }

        private void Help(string text)
        {
            wu.help.text = text;
            wu.help.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private void Toast(string text, float seconds = 1.8f)
        {
            wu.toast.text = text;
            toastUntil = Time.unscaledTime + seconds;
        }

        // ---------- bag ----------

        private void OpenBag()
        {
            if (current == null || bench.Done) return;
            bagOpen = true;
            wu.bagSheet.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            wu.bag.Layout();   // (silent: only shuffling the bag rustles)
        }

        private void CloseBag(bool instant = false)
        {
            bagOpen = false;
            if (instant) { bagShown = 0f; wu.bagSheet.gameObject.SetActive(false); }
        }

        private void BagDown(PointerEventData e)
        {
            if (held != null) return;
            var it = wu.bag.Hit(e.position, null);
            if (it != null) { BeginHold(it); heldPointer = e.pointerId; HoldMove(e.position); }
            else stirring = true;
        }

        private void BagDrag(PointerEventData e)
        {
            if (held != null && e.pointerId == heldPointer) HoldMove(e.position);
            else if (stirring) wu.bag.Brush(e.position, e.delta, null, wu.canvas.scaleFactor);
        }

        private void BagUp(PointerEventData e)
        {
            if (held != null && e.pointerId == heldPointer) HoldRelease();
            stirring = false;
        }

        private void BeginHold(BagItem it)
        {
            held = it;
            it.hidden = true;
            it.image.gameObject.SetActive(false);
            bench.Hold(it.kind);
            CloseBag();
            Sfx.Play("pick");
            Help("");
        }

        private void HoldMove(Vector2 screen)
        {
            if (held == null) return;
            if (Application.isMobilePlatform) screen += Vector2.up * 90f * wu.canvas.scaleFactor; // keep the piece above the finger
            snapped = bench.MoveHeld(cam, screen, 150f * wu.canvas.scaleFactor, out wrongSpot);
        }

        private void HoldRelease()
        {
            var it = held;
            held = null;
            heldPointer = int.MinValue;
            bench.Drop();
            if (it == null) return;
            if (snapped != null)
            {
                bench.PlaceAt(snapped);
                wu.bag.Remove(it);
                SavePending();
                Sfx.Play(snapped.p.t == "sticker" ? "stick" : "click");
                if (bench.Hint == null) { wu.hintBar.gameObject.SetActive(false); wu.bag.glowKey = null; }
                UpdateHud();
                if (bench.Complete) StartCoroutine(Win());
                else if (BagDone) { bagIndex++; FillBag(); Toast($"Yeni torba açıldı · {bagIndex + 1}/{bags.Count}"); }
            }
            else
            {
                it.hidden = false;
                if (wrongSpot != null) Mistake(it.kind);
                else Toast("Parça çantaya geri döndü", 1f);
            }
            snapped = wrongSpot = null;
        }

        private void Mistake(Piece kind)
        {
            mistakes++;
            heartsLeft = Mathf.Max(0, heartsLeft - 1);
            Sfx.Haptic();   // a wrong piece: only a buzz
            UpdateHud();
            SavePending();
            if (heartsLeft > 0) { Toast($"Bu {kind.Label()} buraya uymuyor"); return; }
            Sfx.Play("fail");
            wu.failInfo.text = $"{current.name} 3 gün boyunca çarkta çıkmayacak.\nAlan boş kaldı, başka bir model deneyebilirsin.";
            wu.failAdBtn.gameObject.SetActive(!adUsed);
            wu.fail.SetActive(true);
        }

        private IEnumerator ContinueAd()
        {
            wu.fail.SetActive(false);
            yield return TestAd();
            adUsed = true;
            heartsLeft += 2;
            heartsTotal = Mathf.Max(heartsTotal, heartsLeft);
            UpdateHud();
            Toast("+2 hata hakkı");
            SavePending();
        }

        private IEnumerator TestAd()
        {
            wu.ad.SetActive(true);
            for (int n = 3; n > 0; n--) { wu.adCount.text = n.ToString(); yield return new WaitForSeconds(1f); }
            wu.ad.SetActive(false);
        }

        private IEnumerator HintAd()
        {
            if (current == null || bench.Done || busy) yield break;
            busy = true;
            yield return TestAd();
            busy = false;
            var t = bench.PickHint();
            if (t == null) yield break;
            wu.bag.glowKey = t.p.key;
            wu.hintText.text = $"İpucu: {t.p.Kind().Label()} al,\ntezgâhta SARI yanıp sönen yere koy";
            wu.hintBar.gameObject.SetActive(true);
            OpenBag();
        }

        private int levelBefore;

        private IEnumerator Win()
        {
            bench.Done = true;
            ClearPending();
            yield return new WaitForSeconds(0.35f);
            int stars = mistakes == 0 ? 3 : mistakes <= heartsTotal / 2 ? 2 : 1;
            levelBefore = Progress.Level;
            FreeBuild.Stock.EnsureStarted();
            Progress.Finished(current.id, stars);
            Progress.RestKind(current.kind);
            int earned = FreeBuild.Stock.Earn(current, stars);
            Sfx.Play("win");
            wu.winTitle.text = $"{current.name} tamamlandı!";
            for (int i = 0; i < 3; i++) wu.winStars[i].color = i < stars ? new Color(1f, 0.76f, 0.15f) : new Color(0.85f, 0.86f, 0.9f);
            wu.winInfo.text = (mistakes == 0 ? "Hiç hata yapmadın!" : $"{mistakes} hata yaptın") + $"\n+{stars} yıldız · +{earned} malzeme";
            wu.hintBar.gameObject.SetActive(false);
            Help("");
            wu.win.SetActive(true);
        }

        private void OrbitDrag(PointerEventData e)
        {
            touches[e.pointerId] = e.position;
            if (touches.Count >= 2)
            {
                // pinch: compare this finger's previous and new distance to the other finger
                var other = touches.First(kv => kv.Key != e.pointerId).Value;
                float before = Vector2.Distance(e.position - e.delta, other), after = Vector2.Distance(e.position, other);
                if (after > 1f) orbit.Zoom(before / after);
                return;
            }
            orbit.Turn(e.delta / Mathf.Max(0.5f, wu.canvas.scaleFactor) * 0.6f);
        }

        // ---------- collection ----------

        private void ShowCollection()
        {
            foreach (Transform c in wu.collectionList) Destroy(c.gameObject);
            foreach (var m in ModelCatalog.All)
            {
                var s = Progress.Of(m.id);
                bool locked = Progress.IsLocked(m.id, out var left);
                bool placed = Progress.Data.placements.Any(p => p.id == m.id), stored = Progress.Data.inventory.Contains(m.id);
                var row = Ui.Panel(wu.collectionList, "Row", placed || stored ? new Color(0.88f, 0.96f, 0.88f) : new Color(0.95f, 0.96f, 0.97f), false).Height(150f);
                var pic = Ui.Rect(row.transform, "Pic").gameObject.AddComponent<RawImage>();
                pic.texture = Previews.Model(m, 160);
                pic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(130, 130), new Vector2(12, 0));
                var name = Ui.Text(row.transform, m.name, 38, Ui.Ink, true);
                name.alignment = TextAlignmentOptions.Left;
                name.rectTransform.Place(new Vector2(0, 1), new Vector2(470, 70), new Vector2(160, -12));
                string kind = CityLayout.KindName(m.kind) + (m.style != "" ? $" · {CityLayout.StyleName(m.style)}" : "");
                var sub = Ui.Text(row.transform, $"{kind} · {m.pieces.Count} parça", 28, new Color(0.4f, 0.43f, 0.5f), true);
                sub.alignment = TextAlignmentOptions.Left;
                sub.rectTransform.Place(new Vector2(0, 0), new Vector2(470, 56), new Vector2(160, 14));
                string status = placed ? "Şehirde" : stored ? "Envanterde" : locked ? "Bekliyor\n" + Progress.LockText(left) : "Yapılmadı";
                if (s.stars > 0) status += $"\n{s.stars} yıldız";
                var st = Ui.Text(row.transform, status, 30, placed || stored ? new Color(0.15f, 0.55f, 0.3f) : locked ? new Color(0.8f, 0.3f, 0.2f) : Ui.Ink, true);
                st.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(230, 130), new Vector2(-12, 0));
            }
            wu.collection.SetActive(true);
        }

        // ================= frame =================

        private void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            Mats.Pulse(time);
            Water.Tick(time);
            bench.Tick(dt);
            studio.Tick(dt, chu.canvas.gameObject.activeSelf);
            Previews.Tick(dt, mode == Mode.Workshop && current != null && (wu.refCard.gameObject.activeInHierarchy || wu.refModal.activeSelf));

            // bag slides up / down
            bagShown = Mathf.MoveTowards(bagShown, bagOpen ? 1f : 0f, dt * 6f);
            if (wu.bagSheet.gameObject.activeSelf)
            {
                float h = wu.bagSheet.rect.height + 80f;
                wu.bagSheet.anchoredPosition = new Vector2(0, -h * (1f - Mathf.SmoothStep(0, 1, bagShown)));
                if (bagShown <= 0f && held == null) wu.bagSheet.gameObject.SetActive(false);
            }
            wu.toastGroup.alpha = Mathf.MoveTowards(wu.toastGroup.alpha, Time.unscaledTime < toastUntil ? 1f : 0f, dt * 5f);
            wu.bagBtn.transform.localScale = Vector3.one * (current != null && bench.PlacedCount == 0 && !bagOpen ? 1f + 0.06f * Mathf.Sin(time * 5f) : 1f);

            CoachTick(dt);
            if (tourCam) { }
            else if (mode == Mode.Workshop) orbit.Apply(cam);
            else if (mode == Mode.Walk) WalkUpdate(dt);
            else if (mode == Mode.Build) BuildUpdate(dt);
            else CityUpdate(dt);
        }
    }
}
