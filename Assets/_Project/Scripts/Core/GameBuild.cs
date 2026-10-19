using System.Collections.Generic;
using System.Linq;
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
                ? carEdit ??= new BuildSite(transform, Progress.Data.carBuild, ChassisX, ChassisZ, BuildSite.CornerFor(BuildSite.CarOrigin, ChassisX, ChassisZ, 0f), 0f, 1f, true, "gray")
                : houseEdit ??= new BuildSite(transform, Progress.Data.build, lot, lot, BuildSite.CornerFor(BuildSite.HouseOrigin, lot, lot, 0f), 0f, 1f, true, "dgreen");
            bu.title.text = forCar ? "Arabam" : "Evim";
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
            BuildHouse();
            EnterCity(null);
            var lot = CityLayout.HomeLotCenter;
            cityCam.Focus(city.WorldAt(lot, 0f), 90f);
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
                    if (Stock.Count(kind.key) <= 0) bu.Say("Bu parçadan yok · şehirde model yaparak kazan");
                    RefreshBuildUi();
                });
            }
            if (buildKind == null || buildKind.group != group) buildKind = Stock.Kinds.FirstOrDefault(k => k.group == group && Stock.Count(k.key) > 0) ?? Stock.Kinds.First(k => k.group == group);
            RefreshBuildUi();
        }

        private void RefreshBuildUi()
        {
            bu.UpdateCards(buildKind?.key, Stock.Count);
            bu.stockText.text = $"{site.pieces.Count} parça yerinde · kutuda {Stock.Total}" + (buildingCar ? $" · tekerlek {site.CountOf("wheel")}/4" : "");
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
