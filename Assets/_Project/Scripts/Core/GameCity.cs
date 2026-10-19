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
            city = new CityMap(transform);
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
        }

        private void UpdateCityHud()
        {
            var (from, to) = Progress.LevelSpan;
            cu.SetLevel(Progress.Level, Progress.Stars, from, to);
            cu.inventoryBtn.SetLabel(Progress.Data.inventory.Count > 0 ? $"Envanter ({Progress.Data.inventory.Count})" : "Envanter");
            cu.help.transform.parent.gameObject.SetActive(!cu.panel.activeSelf);
            cu.resumeBtn.gameObject.SetActive(Pending.Has && !cu.panel.activeSelf);
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
            if (s == null)
            {
                // the player's own lot: build the house or the car
                var ray = cam.ScreenPointToRay(screen);
                float lotY = CityMap.Center.y + CityMap.Lawn;
                if (ray.direction.y < 0f)
                {
                    var p = ray.origin + ray.direction * ((lotY - ray.origin.y) / ray.direction.y) - CityMap.Center;
                    if (CityLayout.OnHomeLot(CityMap.PlanOf(p + CityMap.Center))) { OpenHomeLot(); return; }
                }
                ClosePanel();
                return;
            }
            OpenSlot(s);
        }

        // ---------- slot panel ----------

        private void ClosePanel()
        {
            panelSlot = null;
            city.Select(null);
            cu.panel.SetActive(false);
            cu.help.transform.parent.gameObject.SetActive(true);
        }

        private static readonly Color Neutral = new Color(0.92f, 0.93f, 0.95f);

        private void OpenSlot(Slot s)
        {
            panelSlot = s;
            city.Select(s);
            Sfx.Play("pick", 0.6f);
            cu.help.transform.parent.gameObject.SetActive(false);
            string where = s.area.name;
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
            if (pool.Count > 0) info = $"{where}\nÇarkta {pool.Count} {kindLine} modeli var";
            else
            {
                var wait = Progress.NextUnlock(s);
                info = $"{where}\n" + (wait != null ? $"Yeni model {Progress.LockText(wait.Value)} sonra" : "Bu alan için yeni modeller yakında");
            }
            var buttons = new List<(string, Color, System.Action)>();
            if (Pending.Has && Pending.slot == s.id) buttons.Add(("İnşaya devam et", Ui.Yellow, ResumeBuild));
            else if (pool.Count > 0) buttons.Add(("Yeni yap", Ui.Yellow, () => BuildFor(s)));
            if (inv.Count > 0) buttons.Add(($"Envanterden ({inv.Count})", new Color(0.8f, 0.9f, 1f), () => PickFromInventory(s)));
            buttons.Add(("Kapat", Neutral, ClosePanel));
            cu.ShowPanel(s.kind == "house" ? $"{CityLayout.StyleName(s.style)} ev arsası" : CityLayout.SlotName(s), info, null, buttons.ToArray());
        }

        private void OpenHomeLot()
        {
            panelSlot = null;
            city.Select(null);
            Sfx.Play("pick", 0.6f);
            cu.help.transform.parent.gameObject.SetActive(false);
            cu.ShowPanel("Evim", $"Senin arsan · {Progress.Data.build.Count} parçalık ev" + (HasBuiltCar ? " · araban hazır" : ""), null,
                ("Evimi yap", Ui.Yellow, () => { ClosePanel(); EnterBuild(false); }),
                ("Arabamı yap", new Color(0.8f, 0.9f, 1f), () => { ClosePanel(); EnterBuild(true); }),
                ("Kapat", Neutral, ClosePanel));
        }

        private void BuildFor(Slot s)
        {
            var pool = Progress.PoolFor(s);
            if (pool.Count == 0) return;
            if (Pending.Has) { ClearPending(); Toast("Yarım kalan yapım bırakıldı"); }
            buildSlot = s;
            ClosePanel();
            EnterWorkshop();
            StartCoroutine(Spin(pool));
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
            levelBefore = now;
            city.RefreshAll();
            UpdateCityHud();
        }

        private void CityUpdate(float dt)
        {
            cityCam.Apply(cam);
            city.Tick(dt);
            city.people.Tick(dt, cityCam.target, cityCam.distance < 220f ? 130f : 0f);   // people are too small to see from high up
            if (cu.levelUp.activeSelf && Time.unscaledTime > levelUpUntil) cu.levelUp.SetActive(false);
            cu.resumeBtn.gameObject.SetActive(Pending.Has && !cu.panel.activeSelf && !cu.list.activeSelf);
            cu.money.text = $"{FreeBuild.Wallet.Money} lira";
        }
    }
}
