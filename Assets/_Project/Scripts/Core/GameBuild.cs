using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Build;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.FreeBuild;
using BrickMuseum.UI;
using BrickMuseum.Workshop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BrickMuseum.Core
{
    /// <summary>
    /// Free building of the player's own house: any piece from the material box, any colour, anywhere on the lot —
    /// walls, floors for more storeys, doors, windows, stairs, balconies with railings, a roof. Materials come from
    /// building the city (<see cref="Stock"/>).
    /// </summary>
    public sealed partial class Game
    {
        private readonly BuildUi bu = new BuildUi();
        private readonly OrbitCam buildCam = new OrbitCam { minDist = 6f, maxDist = 80f };
        private BuildSite site, houseEdit, carEdit;
        private bool buildingCar;
        private int buildLot, houseEditLot = -1;
        private Slot gardenSlot;                  // a city house's garden being built (else the player's own house or car)
        private BuildSite gardenEdit;
        private BuildKind buildKind;
        private string buildColour = "red", buildTab = "Tuğla";
        private int buildRot;
        private bool removing;
        private readonly Dictionary<int, Vector2> buildTouches = new Dictionary<int, Vector2>();
        private float buildMoved, buildSaveAt;

        private static readonly string[] BuildColours =
            { "red", "dred", "terra", "orange", "yellow", "cream", "white", "sand", "brown", "green", "dgreen", "lblue", "blue", "gray", "black" };

        private void WireBuild()
        {
            bu.Build(transform);
            cu.buildBtn.onClick.AddListener(() => EnterBuild(false));
            bu.switchBtn.onClick.AddListener(() => { if (site.dirty) { Progress.Save(); site.dirty = false; } site.HideGhost(); EnterBuild(!buildingCar); });
            // the garage: work on the next car, or start a new one
            bu.nextCarBtn.onClick.AddListener(() => { if (buildingCar) SwitchCar(false); else SwitchLot(); });
            bu.newCarBtn.onClick.AddListener(() => { SwitchCar(true); });
            bu.backBtn.onClick.AddListener(ExitBuild);
            bu.undoBtn.onClick.AddListener(() => { if (site.Undo()) { Sfx.Play("rustle", 0.6f); RefreshBuildUi(); } else bu.Say("Geri alınacak bir şey yok"); });
            bu.modeBtn.onClick.AddListener(() => { removing = !removing; bu.SetMode(removing); site.HideGhost(); });
            bu.turnBtn.onClick.AddListener(() => buildRot = (buildRot + 1) % 4);
            bu.colourBtn.onClick.AddListener(() => bu.colours.SetActive(true));
            bu.storeyBtn.onClick.AddListener(() =>
            {
                int max = site.Storeys;
                site.SetView(site.view >= max || max <= 1 ? 0 : site.view + 1);
                bu.storeyBtn.SetLabel(site.view == 0 ? "Tüm katlar" : $"{site.view}. kata kadar");
            });
            bu.BuildColours(BuildColours.Select(c => (c, Palette.Of(c))), c => { buildColour = c; bu.colourSwatch.color = Palette.Of(c); });
            bu.touch.Down = e => { buildTouches[e.pointerId] = e.position; if (buildTouches.Count == 1) buildMoved = 0f; };
            bu.touch.Drag = BuildDrag;
            bu.touch.Up = e =>
            {
                buildTouches.Remove(e.pointerId);
                if (buildTouches.Count == 0 && buildMoved < 14f * bu.canvas.scaleFactor) BuildTap(e.position);
            };
            bu.touch.Scroll = s => buildCam.Zoom(s > 0 ? 0.88f : 1.12f);
            var groups = Stock.Kinds.Select(k => k.group).Distinct().ToList();
            bu.SetTabs(groups, ShowBuildTab);
        }

        private void EnterBuild(bool forCar = false)
        {
            buildingCar = forCar;
            mode = Mode.Build;
            ClosePanel();
            Stock.EnsureStarted();
            int lot = CityLayout.HomePlate;
            site = forCar
                ? carEdit ??= new BuildSite(transform, Garage.Active.pieces, ChassisX, ChassisZ, BuildSite.CornerFor(BuildSite.CarOrigin, ChassisX, ChassisZ, 0f), 0f, 1f, true, "gray")
                : HouseEditFor(buildLot, lot);
            gardenSlot = null;
            bu.title.text = forCar ? Garage.Active.name : Lots.Name(buildLot);
            bu.nextCarBtn.SetLabel(forCar ? "Sonraki araba" : "Sonraki ev");
            bu.nextCarBtn.gameObject.SetActive(forCar ? Garage.Cars.Count > 1 : Lots.OwnedCount > 1);
            bu.newCarBtn.gameObject.SetActive(forCar);
            bu.switchBtn.SetLabel(forCar ? "Evime geç" : "Arabama geç");
            site.SetView(0);
            bu.storeyBtn.SetLabel("Tüm katlar");
            cu.canvas.gameObject.SetActive(false);
            bu.canvas.gameObject.SetActive(true);
            cam.fieldOfView = 50f;
            ShadowDistance(120f);
            buildCam.target = (forCar ? BuildSite.CarOrigin : BuildSite.HouseOrigin) + new Vector3(0, forCar ? 1f : 2f, 0);
            buildCam.yaw = 200f; buildCam.pitch = forCar ? 32f : 38f; buildCam.distance = forCar ? 20f : 42f;
            if (forCar) buildTab = "Araba"; else if (buildTab == "Araba") buildTab = "Tuğla";
            removing = false; bu.SetMode(false);
            bu.colourSwatch.color = Palette.Of(buildColour);
            ShowBuildTab(buildTab);
        }

        private void ExitBuild()
        {
            if (site.dirty) { Progress.Save(); site.dirty = false; }
            site.HideGhost();
            bu.canvas.gameObject.SetActive(false);
            if (gardenSlot != null)
            {
                // back to the city, the new garden round the house
                var s = gardenSlot;
                gardenSlot = null;
                city.RefreshGarden(s);
                EnterCity(s);
                bu.switchBtn.gameObject.SetActive(true);
                return;
            }
            BuildHouse();
            EnterCity(null);
            var lot = CityLayout.LotCenter(buildLot);
            cityCam.Focus(city.WorldAt(lot, 0f), 90f);
        }

        /// <summary>
        /// A city house's garden in the editor: its plot as the baseplate, the house standing fixed in the middle (its cells
        /// can't take pieces), the garden tab open.
        /// </summary>
        private void EnterGarden(Slot s)
        {
            var m = ModelCatalog.Find(Progress.At(s.id));
            if (m == null) return;
            EnterBuild(false);
            gardenSlot = s;
            if (gardenEdit != null) Destroy(gardenEdit.root.gameObject);
            int n = Gardens.Size(s);
            gardenEdit = new BuildSite(transform, Gardens.Pieces(s.id, true), n, n, BuildSite.CornerFor(BuildSite.GardenOrigin, n, n, 0f), 0f, 1f, true, "green");
            float k = CityMap.FitScale(m, s);
            var model = PieceFactory.BuildModel(m).transform;
            model.SetParent(gardenEdit.root, false);
            model.localPosition = new Vector3(n / 2f, 0f, n / 2f);
            model.localScale = Vector3.one * k;
            int fw = Mathf.CeilToInt(m.sx * k), fd = Mathf.CeilToInt(m.sz * k);
            gardenEdit.Block((n - fw) / 2, (n - fd) / 2, fw, fd, Mathf.CeilToInt(m.height * k / Piece.Plate) + 1);
            site = gardenEdit;
            bu.title.text = "Bahçe";
            bu.nextCarBtn.gameObject.SetActive(false);
            bu.newCarBtn.gameObject.SetActive(false);
            bu.switchBtn.gameObject.SetActive(false);
            buildCam.target = BuildSite.GardenOrigin + new Vector3(0, 1.5f, 0);
            buildCam.distance = 30f; buildCam.pitch = 45f;
            ShowBuildTab("Bahçe");
            RefreshBuildUi();
        }

        /// <summary>The editing lot for a house lot (only one at a time: another lot's is put away first).</summary>
        private BuildSite HouseEditFor(int lotIndex, int size)
        {
            if (houseEdit != null && houseEditLot != lotIndex) { Destroy(houseEdit.root.gameObject); houseEdit = null; }
            houseEditLot = lotIndex;
            return houseEdit ??= new BuildSite(transform, Lots.Pieces(lotIndex), size, size, BuildSite.CornerFor(BuildSite.HouseOrigin, size, size, 0f), 0f, 1f, true, "dgreen");
        }

        /// <summary>On to the next house lot the player owns.</summary>
        private void SwitchLot()
        {
            if (site.dirty) { Progress.Save(); site.dirty = false; }
            site.HideGhost();
            buildLot = Lots.NextOwned(buildLot);
            EnterBuild(false);
            bu.Say(Lots.Name(buildLot));
        }

        /// <summary>Saves the car on the chassis and puts another one there (the next in the garage, or a new empty one).</summary>
        private void SwitchCar(bool makeNew)
        {
            if (site.dirty) { Progress.Save(); site.dirty = false; }
            site.HideGhost();
            if (makeNew) Garage.AddNew(); else Garage.Next();
            if (carEdit != null) { Destroy(carEdit.root.gameObject); carEdit = null; }
            EnterBuild(true);
            bu.Say(Garage.Active.name);
        }

        private void ShowBuildTab(string group)
        {
            buildTab = group;
            bu.ShowTab(group);
            bu.ClearCards();
            foreach (var k in Stock.Kinds.Where(k => k.group == group))
            {
                var kind = k;
                bu.AddCard(k.key, BuildParts.Picture(k), k.name, () =>
                {
                    buildKind = kind;
                    if (removing) { removing = false; bu.SetMode(false); }
                    if (Stock.Count(kind.key) <= 0) bu.Say(kind.price > 0 ? "Bu parçadan yok · satın al" : "Bu parçadan yok · şehirde model yaparak kazan");
                    RefreshBuildUi();
                }, k.price, k.pack, () =>
                {
                    if (Stock.Buy(kind)) { Sfx.Play("place", 0.7f); buildKind = kind; bu.Say($"+{kind.pack} {kind.name}"); }
                    else { Sfx.Play("wrong", 0.5f); bu.Say("Yeterli paran yok"); }
                    RefreshBuildUi();
                });
            }
            if (buildKind == null || buildKind.group != group) buildKind = Stock.Kinds.FirstOrDefault(k => k.group == group && Stock.Count(k.key) > 0) ?? Stock.Kinds.First(k => k.group == group);
            RefreshBuildUi();
        }

        private void RefreshBuildUi()
        {
            bu.UpdateCards(buildKind?.key, Stock.Count);
            bu.stockText.text = $"{site.pieces.Count} parça yerinde · kutuda {Stock.Total} · {Wallet.Money} lira" + (buildingCar ? $" · tekerlek {site.CountOf("wheel")}/4" : "");
        }

        private void BuildDrag(PointerEventData e)
        {
            buildMoved += e.delta.magnitude;
            buildTouches[e.pointerId] = e.position;
            if (buildTouches.Count >= 2)
            {
                var other = buildTouches.First(kv => kv.Key != e.pointerId).Value;
                float before = Vector2.Distance(e.position - e.delta, other), after = Vector2.Distance(e.position, other);
                if (after > 1f) buildCam.Zoom(before / after);
                // both fingers moving together slide the view up / down
                buildCam.target.y = Mathf.Clamp(buildCam.target.y + e.delta.y * 0.01f, BuildSite.HouseOrigin.y, BuildSite.HouseOrigin.y + 20f);
                return;
            }
            buildCam.Turn(e.delta);
        }

        private void BuildTap(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            if (removing)
            {
                if (site.RemoveAt(ray)) { Sfx.Play("rustle", 0.6f); RefreshBuildUi(); }
                return;
            }
            if (buildKind == null) return;
            string why = site.Place(ray, buildKind, buildColour, buildRot);
            if (why != null) { bu.Say(why); Sfx.Play("wrong", 0.5f); return; }
            if (site.dirty) { Sfx.Play("stick", 0.7f); RefreshBuildUi(); }
        }

        private void BuildUpdate(float dt)
        {
            buildCam.Apply(cam);
            site.UpdateGuides();
            bu.Tick();
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) buildRot = (buildRot + 1) % 4;
            if (kb != null && kb.zKey.wasPressedThisFrame && kb.ctrlKey.isPressed) bu.undoBtn.onClick.Invoke();
            // a see-through preview under the mouse (desktop); on phones the piece just appears where you tap
            var mouse = Mouse.current;
            bool hover = !removing && !bu.colours.activeSelf && mouse != null && buildTouches.Count == 0 && Touchscreen.current == null;
            if (hover && IsOverTouchArea(mouse.position.ReadValue()))
                site.ShowGhost(cam.ScreenPointToRay(mouse.position.ReadValue()), buildKind, buildColour, buildRot);
            else if (!testGhost) site.HideGhost();
            if (site.dirty && Time.unscaledTime > buildSaveAt) { Progress.Save(); site.dirty = false; buildSaveAt = Time.unscaledTime + 5f; }
        }

        private bool testGhost;

        /// <summary>True when the pointer is over the empty build area (not over a button or the strip).</summary>
        private bool IsOverTouchArea(Vector2 pos)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pos }, results);
            return results.Count > 0 && results[0].gameObject == bu.touch.gameObject;
        }
    }
}
