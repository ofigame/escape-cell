using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Build;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.UI;
using BrickMuseum.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BrickMuseum.Core
{
    /// <summary>The city seen from above: pan / zoom, tap a slot to build, place from the inventory or take a model away.</summary>
    public sealed partial class Game
    {
        private CityMap city;
        private readonly CityCam cityCam = new CityCam();
        private readonly CityUi cu = new CityUi();
        private Slot panelSlot;
        private readonly Dictionary<int, Vector2> cityTouches = new Dictionary<int, Vector2>();
        private float pressMoved;
        private float levelUpUntil;

        private void BuildCity()
        {
            // models standing on slots that no longer exist (the city plan changed) go back to the inventory
            foreach (var p in Progress.Data.placements.Where(p => CityLayout.Find(p.slot) == null).ToList())
            {
                Progress.Data.placements.Remove(p);
                Progress.Data.inventory.Add(p.id);
            }
            Progress.Save();
            FreeBuild.Wallet.EnsureStarted();
            city = new CityMap(transform);
            BuildFarm();
            BuildHouse();
            var start = CityLayout.AreaNamed("Türk Mahallesi").center;
            cityCam.Focus(city.WorldAt(start, 0f), 150f);
        }

        private void WireCity()
        {
            cu.touch.Down = e => { cityTouches[e.pointerId] = e.position; if (cityTouches.Count == 1) pressMoved = 0f; };
            cu.touch.Drag = CityDrag;
            cu.touch.Up = e =>
            {
                cityTouches.Remove(e.pointerId);
                if (cityTouches.Count == 0 && pressMoved < 14f * cu.canvas.scaleFactor) CityTap(e.position);
            };
            cu.touch.Scroll = s => cityCam.Zoom(s > 0 ? 0.88f : 1.12f);
            cu.inventoryBtn.onClick.AddListener(ShowInventory);
            cu.collectionBtn.onClick.AddListener(ShowCollection);
            cu.langBtn.onClick.AddListener(ShowLanguages);
            cu.resumeBtn.onClick.AddListener(ResumeBuild);
            cu.scoreBtn.onClick.AddListener(ShowScore);
            cu.bonusBtn.onClick.AddListener(ShowBonus);
            cu.farmBtn.onClick.AddListener(() => { FocusFarm(); OpenFarm(); });
            cu.farBtn.onClick.AddListener(() => { FocusFar(); ShowDistrict(); });
            Loc.Changed += () =>
            {
                // every text on screen (and hidden) is drawn again in the new language
                foreach (var t in FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.ForceMeshUpdate(true, true);
            };
        }

        private void EnterCity(Slot focus)
        {
            mode = Mode.City;
            bench.Clear();
            current = null;
            CloseBag(true);
            wu.canvas.gameObject.SetActive(false);
            cu.canvas.gameObject.SetActive(true);
            cam.fieldOfView = Screen.width < Screen.height ? 50f : 40f;
            ShadowDistance(650f);
            ClosePanel();
            city.RefreshAll();
            if (focus != null) cityCam.Focus(city.WorldOf(focus), 95f);
            UpdateCityHud();
            if (Progress.Data.placements.Count > 0) StartCoroutine(Notify.Init());   // after the first build: ask once for notifications
        }

        private void UpdateCityHud()
        {
            var (from, to) = Progress.LevelSpan;
            cu.SetLevel(Progress.Level, Progress.Stars, from, to);
            cu.inventoryBtn.SetLabel(Progress.Data.inventory.Count > 0 ? $"Envanter ({Progress.Data.inventory.Count})" : "Envanter");
            cu.help.transform.parent.gameObject.SetActive(!cu.panel.activeSelf);
            cu.resumeBtn.gameObject.SetActive(Pending.Has && !cu.panel.activeSelf);
            cu.scoreBtn.SetLabel($"Puan {CityScore.Total}");
            cu.bonusBtn.gameObject.SetActive(Progress.Data.bonusPending > 0);
            if (Progress.Data.bonusPending > 0) cu.bonusBtn.SetLabel($"Şehir bonusu ({Progress.Data.bonusPending})");
            cu.help.text = Progress.Data.placements.Count == 0 ? "Bir EV arsasına dokun ve ilk evini yap" : "Bir alana dokun";
        }

        // ---------- input ----------

        private void CityDrag(PointerEventData e)
        {
            pressMoved += e.delta.magnitude;
            cityTouches[e.pointerId] = e.position;
            if (cityTouches.Count >= 2)
            {
                var other = cityTouches.First(kv => kv.Key != e.pointerId).Value;
                float before = Vector2.Distance(e.position - e.delta, other), after = Vector2.Distance(e.position, other);
                if (after > 1f) cityCam.Zoom(before / after);
                return;
            }
            cityCam.Pan(cam, e.delta);
        }

        private void CityTap(Vector2 screen)
        {
            var s = city.Hit(cam.ScreenPointToRay(screen));
            if (s == null && FarmHitBy(cam.ScreenPointToRay(screen))) { OpenFarm(); return; }
            if (s == null)
            {
                // the player's own lot: build the house or the car
                var ray = cam.ScreenPointToRay(screen);
                float lotY = CityMap.Center.y + CityMap.Lawn;
                if (ray.direction.y < 0f)
                {
                    var p = ray.origin + ray.direction * ((lotY - ray.origin.y) / ray.direction.y) - CityMap.Center;
                    int lot = CityLayout.LotAt(CityMap.PlanOf(p + CityMap.Center));
                    if (lot >= 0) { OpenLot(lot); return; }
                }
                ClosePanel();
                return;
            }
            OpenSlot(s);
        }

        // ---------- slot panel ----------

        private void ClosePanel()
        {
            farmPanel = false;
            panelSlot = null;
            city.Select(null);
            cu.panel.SetActive(false);
            cu.help.transform.parent.gameObject.SetActive(true);
        }

        private static readonly Color Neutral = new Color(0.92f, 0.93f, 0.95f);

        private void OpenSlot(Slot s)
        {
            farmPanel = false;
            panelSlot = s;
            city.Select(s);
            Sfx.Play("pick", 0.6f);
            cu.help.transform.parent.gameObject.SetActive(false);
            string where = s.area.name;
            if (s.area.far && !District.IsOpen) { ShowDistrict(); return; }
            if (Progress.Level < s.level)
            {
                int need = Progress.LevelStars[Mathf.Min(s.level, Progress.LevelStars.Length) - 1];
                cu.ShowPanel(CityLayout.SlotName(s), $"{where}\nSeviye {s.level}'de açılır.\nYıldızların: {Progress.Stars}/{need}", null,
                    ("Kapat", Neutral, ClosePanel));
                return;
            }
            string id = Progress.At(s.id);
            if (id != null)
            {
                var m = ModelCatalog.Find(id);
                int stars = Progress.Of(id).stars;
                bool vehicle = s.kind == "vehicle";
                bool mine = vehicle && Progress.Data.myCar == id && !HasBuiltCar;
                string about = $"{CityLayout.SlotName(s)} · {where}\n{stars} yıldız · {m.pieces.Count} parça"
                    + (mine ? "\nSenin araban · Şehri gez'de binebilirsin" : "");
                if (CityScore.CanUpgrade(s))
                {
                    // the city score opens the gardens: of houses first, then of the bigger buildings
                    int need = CityScore.NeededFor(s), score = CityScore.Total;
                    bool open = score >= need;
                    cu.ShowPanel(m.name, about + (open ? "" : $"\nGeliştirme şehir puanı {need} olunca açılır (şimdi {score})"), Previews.Model(m),
                        open ? ("Bahçeyi geliştir", new Color(0.7f, 0.92f, 0.6f), () => { ClosePanel(); EnterGarden(s); }) : ("Envantere al", new Color(0.8f, 0.9f, 1f), () => TakeAway(s)),
                        open ? ("Envantere al", new Color(0.8f, 0.9f, 1f), () => TakeAway(s)) : ("Kapat", Neutral, ClosePanel),
                        ("Kapat", Neutral, ClosePanel));
                    return;
                }
                if (vehicle && !mine && !HasBuiltCar)
                    cu.ShowPanel(m.name, about, Previews.Model(m),
                        ("Arabam olsun", Ui.Yellow, () =>
                        {
                            Progress.Data.myCar = id;
                            Progress.Save();
                            Toast($"{m.name} artık senin araban");
                            OpenSlot(s);
                        }),
                        ("Envantere al", new Color(0.8f, 0.9f, 1f), () => TakeAway(s)),
                        ("Kapat", Neutral, ClosePanel));
                else
                    cu.ShowPanel(m.name, about, Previews.Model(m),
                        ("Envantere al", new Color(0.8f, 0.9f, 1f), () => TakeAway(s)),
                        ("Kapat", Neutral, ClosePanel));
                return;
            }
            var pool = Progress.PoolFor(s);
            var inv = Progress.InventoryFor(s);
            string kindLine = s.kind == "house" ? $"{CityLayout.StyleName(s.style)} evleri" : CityLayout.SlotName(s);
            string info;
            if (pool.Count > 0) info = $"{where}\n{Progress.ChoicesFor(s).Count} {kindLine} modeli seçilebilir";
            else
            {
                var wait = Progress.NextUnlock(s);
                info = $"{where}\n" + (wait != null ? $"Yeni model {Progress.LockText(wait.Value)} sonra" : "Bu alan için yeni modeller yakında");
            }
            var buttons = new List<(string, Color, System.Action)>();
            var resting = Progress.KindResting(s.kind);
            if (Pending.Has && Pending.slot == s.id) buttons.Add(("İnşaya devam et", Ui.Yellow, ResumeBuild));
            else if (pool.Count > 0 && resting != null)
            {
                info += $"\nYeni yapım {Progress.LockText(resting.Value)} sonra";
                buttons.Add(("Reklam izle: hemen aç", Ui.Yellow, () => StartCoroutine(WakeKindAd(s))));
            }
            else if (pool.Count > 0) buttons.Add(("Yeni yap", Ui.Yellow, () => BuildFor(s)));
            if (inv.Count > 0) buttons.Add(($"Envanterden ({inv.Count})", new Color(0.8f, 0.9f, 1f), () => PickFromInventory(s)));
            buttons.Add(("Kapat", Neutral, ClosePanel));
            cu.ShowPanel(s.kind == "house" ? $"{CityLayout.StyleName(s.style)} ev arsası" : CityLayout.SlotName(s), info, null, buttons.ToArray());
        }

        private void OpenHomeLot() => OpenLot(0);

        // ---------- the city score and the level-up bonuses ----------

        private void ShowScore()
        {
            cu.ClearRows();
            var parts = CityScore.Parts();
            cu.listTitle.text = $"Şehir puanı {parts.Sum(p => p.Points)}";
            foreach (var p in parts) cu.AddRow(ModelCatalog.Find(p.pic) is ModelDef pm ? Previews.Model(pm, 160) : null, $"{p.name} · {p.count}", $"{p.count} × {p.each} = {p.Points} puan\n{p.hint}", null, null);
            cu.AddRow(ModelCatalog.All.FirstOrDefault(x => x.kind == "house") is ModelDef hm ? Previews.Model(hm, 160) : null, "Neden gerekli?", $"Puan {CityScore.HouseGardens}: evlerin bahçeleri geliştirilir\nPuan {CityScore.BuildingGardens}: büyük binaların çevresi geliştirilir", null, null);
            cu.list.SetActive(true);
        }

        // ---------- the far district ----------

        private void FocusFar() => cityCam.Focus(city.WorldAt(CityLayout.FarCenter, 0f), 260f);

        /// <summary>What the far district needs, and the button that opens it.</summary>
        private void ShowDistrict()
        {
            panelSlot = null;
            city.Select(null);
            cu.help.transform.parent.gameObject.SetActive(false);
            if (District.IsOpen)
            {
                cu.ShowPanel(CityLayout.FarName, "İmara açık\nHer türden ev, bina ve kamu binası yapılabilir\nTaksiyle de gidilir", null,
                    ("Oraya bak", Ui.Yellow, () => { ClosePanel(); FocusFar(); }), ("Kapat", Neutral, ClosePanel));
                return;
            }
            var lines = new List<string> { "Uzak semt imara kapalı" };
            foreach (var id in District.Needed)
            {
                var m = ModelCatalog.Find(id);
                bool have = !District.Missing().Any(x => x.id == id);
                lines.Add($"{m?.name ?? id} · {(have ? "hazır" : "eksik")}");
            }
            lines.Add($"İmar bedeli: {District.Price} lira (paran: {FreeBuild.Wallet.Money})");
            cu.ShowPanel(CityLayout.FarName, string.Join("\n", lines), null,
                ($"İmara aç · {District.Price} lira", District.Ready && FreeBuild.Wallet.Money >= District.Price ? Ui.Yellow : Neutral, OpenDistrict),
                ("Kapat", Neutral, ClosePanel));
        }

        private void OpenDistrict()
        {
            if (!District.Ready) { Toast("Önce okul, hastane ve polis karakolu yapılmalı"); return; }
            if (FreeBuild.Wallet.Money < District.Price) { Toast($"Yeterli para yok · {District.Price} lira gerekli"); return; }
            if (!District.Open()) return;
            ClosePanel();
            Sfx.Play("win");
            city.RefreshAll();
            UpdateCityHud();
            FocusFar();
            Toast($"{CityLayout.FarName} imara açıldı!", 2.5f);
        }

        private static readonly string[] BonusPool = { "bank", "konteyner", "lamba", "trafiklambasi", "workers", "police", "staff" };

        private void ShowBonus()
        {
            panelSlot = null;
            city.Select(null);
            cu.help.transform.parent.gameObject.SetActive(false);
            cu.ShowPanel("Şehir bonusu", $"Reklam izle, şehir bonusunu topla\n{Progress.Data.bonusPending} bonus seni bekliyor · her biri 2 hediye", null,
                ("Reklam izle, topla", Ui.Yellow, () => StartCoroutine(CollectBonus())),
                ("Kapat", Neutral, ClosePanel));
        }

        /// <summary>Two gifts per bonus: street furniture into the inventory, or staff that start walking the town.</summary>
        private IEnumerator CollectBonus()
        {
            ClosePanel();
            yield return TestAd();
            var picks = BonusPool.OrderBy(_ => Random.value).Take(2).ToList();
            var names = new List<string>();
            foreach (var g in picks)
            {
                switch (g)
                {
                    case "workers": Progress.Data.bonusWorkers += 2; names.Add("2 belediye işçisi"); break;
                    case "police": Progress.Data.bonusPolice += 3; names.Add("3 polis"); break;
                    case "staff": Progress.Data.bonusStaff += 2; names.Add("2 belediye personeli"); break;
                    default:
                        Progress.ToInventory(g);
                        names.Add(ModelCatalog.Find(g)?.name ?? g);
                        break;
                }
            }
            Progress.Data.bonusPending = Mathf.Max(0, Progress.Data.bonusPending - 1);
            Progress.Save();
            city.people.Sync();
            Sfx.Play("win");
            UpdateCityHud();
            cu.ShowPanel("Bonus toplandı", string.Join("\n", names) + $"\nŞehir puanı: {CityScore.Total}", null, ("Tamam", Ui.Yellow, ClosePanel));
        }

        /// <summary>A lot by the west road: build on it when it is the player's, else buy it.</summary>
        private void OpenLot(int lot)
        {
            if (!FreeBuild.Lots.Owned(lot))
            {
                panelSlot = null;
                city.Select(null);
                Sfx.Play("pick", 0.6f);
                cu.help.transform.parent.gameObject.SetActive(false);
                int price = FreeBuild.Lots.Price(lot);
                cu.ShowPanel("Satılık arsa", $"{price} lira · paran {FreeBuild.Wallet.Money} lira\nSatın alınca üstüne yeni bir ev yapabilirsin", null,
                    ("Satın al", Ui.Yellow, () => BuyLot(lot)),
                    ("Kapat", Neutral, ClosePanel));
                return;
            }
            if (lot != 0)
            {
                panelSlot = null;
                city.Select(null);
                Sfx.Play("pick", 0.6f);
                cu.help.transform.parent.gameObject.SetActive(false);
                cu.ShowPanel(FreeBuild.Lots.Name(lot), $"Senin arsan · {FreeBuild.Lots.Pieces(lot).Count} parçalık ev", null,
                    ("Evimi yap", Ui.Yellow, () => { ClosePanel(); buildLot = lot; EnterBuild(false); }),
                    ("Kapat", Neutral, ClosePanel));
                return;
            }
            OpenFirstLot();
        }

        private void BuyLot(int lot)
        {
            if (!FreeBuild.Lots.Buy(lot)) { Toast("Yeterli paran yok"); Sfx.Play("wrong", 0.5f); return; }
            city.RefreshLots();
            BuildHouse();
            Sfx.Play("win");
            Toast("Arsa senin! Evimi yap ile üstüne ev kur", 2.6f);
            OpenLot(lot);
        }

        private void OpenFirstLot()
        {
            panelSlot = null;
            city.Select(null);
            Sfx.Play("pick", 0.6f);
            cu.help.transform.parent.gameObject.SetActive(false);
            cu.ShowPanel("Evim", $"Senin arsan · {Progress.Data.build.Count} parçalık ev" + (HasBuiltCar ? " · araban hazır" : ""), null,
                ("Evimi yap", Ui.Yellow, () => { ClosePanel(); buildLot = 0; EnterBuild(false); }),
                ("Arabamı yap", new Color(0.8f, 0.9f, 1f), () => { ClosePanel(); EnterBuild(true); }),
                ("Kapat", Neutral, ClosePanel));
        }

        /// <summary>"Yeni yap": the models this slot can take, the player picks one (no roulette).</summary>
        private void BuildFor(Slot s)
        {
            var choices = Progress.ChoicesFor(s);
            if (choices.Count == 0) return;
            cu.ClearRows();
            cu.listTitle.text = "Hangisini yapalım?";
            foreach (var m in choices)
            {
                var model = m;
                cu.AddRow(Previews.Model(m, 160), m.name, $"{m.pieces.Count} parça · {m.Hearts} hata hakkı", "Yap", () => StartChosen(s, model));
            }
            cu.list.SetActive(true);
        }

        private void StartChosen(Slot s, ModelDef m)
        {
            cu.list.SetActive(false);
            if (Pending.Has) { ClearPending(); Toast("Yarım kalan yapım bırakıldı"); }
            buildSlot = s;
            ClosePanel();
            EnterWorkshop();
            StartLevel(m);
        }

        /// <summary>The ad that wakes a resting kind at once.</summary>
        private System.Collections.IEnumerator WakeKindAd(Slot s)
        {
            ClosePanel();
            yield return TestAd();
            Progress.WakeKind(s.kind);
            Toast($"{CityLayout.SlotName(s)} açıldı");
            OpenSlot(s);
        }

        private void TakeAway(Slot s)
        {
            var id = Progress.Remove(s.id);
            city.Refresh(s);
            Sfx.Play("place");
            Toast($"{ModelCatalog.Find(id)?.name} envantere alındı");
            UpdateCityHud();
            OpenSlot(s);
        }

        private void PickFromInventory(Slot s)
        {
            cu.ClearRows();
            cu.listTitle.text = "Buraya ne koyalım?";
            foreach (var m in Progress.InventoryFor(s))
                cu.AddRow(Previews.Model(m, 160), m.name, $"{Progress.Of(m.id).stars} yıldız · {m.pieces.Count} parça", "Koy", () => PlaceHere(s, m));
            cu.list.SetActive(true);
        }

        private void PlaceHere(Slot s, ModelDef m)
        {
            cu.list.SetActive(false);
            Progress.Place(s.id, m.id);
            city.Refresh(s);
            city.Pop(s);
            Sfx.Play("place");
            UpdateCityHud();
            OpenSlot(s);
        }

        private void ShowLanguages()
        {
            cu.ClearRows();
            cu.listTitle.text = "Dil";
            foreach (var l in Loc.Order)
            {
                var lang = l;
                cu.AddRow(null, Loc.NameOf(l), l == Loc.Current ? "Seçili" : "", l == Loc.Current ? null : "Seç", () => { Loc.Set(lang); cu.list.SetActive(false); });
            }
            cu.AddRow(null, "Eğitimler", "Tüm ipuçlarını yeniden göster", "Baştan göster", () => { ReplayTips(); cu.list.SetActive(false); });
            cu.list.SetActive(true);
        }

        private void ShowInventory()
        {
            cu.ClearRows();
            cu.listTitle.text = "Envanter";
            var items = Progress.Data.inventory.Select(ModelCatalog.Find).Where(m => m != null).ToList();
            if (items.Count == 0) cu.AddRow(null, "Envanter boş", "Şehirdeki bir modeli \"Envantere al\" ile buraya alabilirsin", null, null);
            foreach (var m in items)
            {
                string kind = CityLayout.KindName(m.kind) + (m.style != "" ? $" · {CityLayout.StyleName(m.style)}" : "");
                cu.AddRow(Previews.Model(m, 160), m.name, $"{kind}\nUygun boş bir alana dokunup yerleştir", null, null);
            }
            cu.list.SetActive(true);
        }

        // ---------- finished models ----------

        /// <summary>The win card's main button: the model goes onto the slot it was built for.</summary>
        private void PlaceFinished()
        {
            var s = buildSlot;
            var id = current.id;
            if (s != null && Progress.At(s.id) == null) Progress.Place(s.id, id); else Progress.ToInventory(id);
            EnterCity(s);
            if (s != null) city.Pop(s);
            Sfx.Play("place");
            CheckLevelUp();
        }

        private void KeepFinished()
        {
            Progress.ToInventory(current.id);
            EnterCity(buildSlot);
            Toast("Envantere kondu");
            CheckLevelUp();
        }

        private void CheckLevelUp()
        {
            int now = Progress.Level;
            if (now <= levelBefore) return;
            int opened = CityLayout.Slots.Count(s => s.level > levelBefore && s.level <= now);
            var areas = CityLayout.Areas.Where(a => a.level > levelBefore && a.level <= now).Select(a => a.name).ToList();
            cu.levelUpText.text = $"Seviye {now}!\n{opened} yeni alan açıldı" + (areas.Count > 0 ? $"\n{string.Join(", ", areas)}" : "");
            cu.levelUp.SetActive(true);
            levelUpUntil = Time.unscaledTime + 3f;
            Sfx.Play("win");
            Progress.Data.bonusPending += now - levelBefore;   // a city bonus for every level
            Progress.Save();
            levelBefore = now;
            city.RefreshAll();
            UpdateCityHud();
        }

        private void CityUpdate(float dt)
        {
            cityCam.Apply(cam);
            city.Tick(dt);
            FarmTick(dt);
            city.people.Tick(dt, cityCam.target, cityCam.distance < 220f ? 130f : 0f);   // people are too small to see from high up
            if (cu.levelUp.activeSelf && Time.unscaledTime > levelUpUntil) cu.levelUp.SetActive(false);
            cu.resumeBtn.gameObject.SetActive(Pending.Has && !cu.panel.activeSelf && !cu.list.activeSelf);
            cu.money.text = $"{FreeBuild.Wallet.Money} lira";
        }
    }
}
