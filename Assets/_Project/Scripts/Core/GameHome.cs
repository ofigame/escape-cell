using System.Collections.Generic;
using BrickMuseum.Build;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.FreeBuild;
using BrickMuseum.Home;
using BrickMuseum.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BrickMuseum.Core
{
    /// <summary>
    /// The player's own house standing on its lot west of the city: the free-built house itself, walked through storey by
    /// storey (doors swing open, stairs climb, the camera stops at walls), and decorated inside: "Dekore et" shows one
    /// storey from above with the floors over it lifted away; furniture is added, dragged, turned and removed.
    /// </summary>
    public sealed partial class Game
    {
        public const float FurnitureScale = 0.5f;
        /// <summary>Inside the house the walker is thinner: it fits through doors and reaches only the next stair.</summary>
        private const float WallRadius = 0.35f;

        private sealed class FurnitureView
        {
            public HomeItem data;
            public FurnitureDef def;
            public GameObject go;
            /// <summary>Half size on the lot (lot units), turned with the piece.</summary>
            public Vector2 Half => (data.rot % 2 == 1 ? new Vector2(def.model.sz, def.model.sx) : new Vector2(def.model.sx, def.model.sz)) * FurnitureScale / 2f;
        }

        private readonly DecorUi du = new DecorUi();
        private BuildSite house;
        private readonly List<FurnitureView> furniture = new List<FurnitureView>();
        private bool decorating, decorDrag;
        private int decorStorey = 1;
        private FurnitureView selected;
        private Vector3 grabOffset;
        private Transform selMarker;

        private float HouseFloorY => house.root.position.y;

        private void WireHome()
        {
            du.Build(transform);
            tu.decorBtn.onClick.AddListener(StartDecor);
            du.doneBtn.onClick.AddListener(EndDecor);
            du.wallBtn.SetLabel("Alt kat");
            du.floorBtn.SetLabel("Üst kat");
            du.wallBtn.onClick.AddListener(() => ShowStorey(decorStorey - 1));
            du.floorBtn.onClick.AddListener(() => ShowStorey(decorStorey + 1));
            du.turnBtn.onClick.AddListener(() => { if (selected != null) { selected.data.rot = (selected.data.rot + 1) % 4; PlaceFurniture(selected); Progress.Save(); } });
            du.removeBtn.onClick.AddListener(() =>
            {
                if (selected == null) return;
                Progress.Data.home.items.Remove(selected.data);
                furniture.Remove(selected);
                Destroy(selected.go);
                Select(null);
                Progress.Save();
            });
            du.touch.Down = DecorDown;
            du.touch.Drag = DecorDrag;
            du.touch.Up = _ => { if (decorDrag) Progress.Save(); decorDrag = false; };
        }

        /// <summary>(Re)builds the player's house on its lot from the saved pieces, with its furniture.</summary>
        private void BuildHouse()
        {
            if (house != null) Destroy(house.root.gameObject);
            furniture.Clear();
            selMarker = null;
            var h = Progress.Data.home;
            if (h.version < 2) { h.items.Clear(); h.version = 2; Progress.Save(); }
            var c = CityLayout.HomeLotCenter;
            // the baseplate sits on the lot's lawn; the house's front (+z when building) faces the road to the east
            var center = city.WorldAt(c, CityMap.Lawn + 0.06f + Piece.Plate);
            float yaw = 90f + CityShape.Angle(c);
            house = new BuildSite(city.root, Progress.Data.build, CityLayout.HomePlate, CityLayout.HomePlate,
                BuildSite.CornerFor(center, CityLayout.HomePlate, CityLayout.HomePlate, yaw), yaw, 1f, false, "dgreen");
            foreach (var it in h.items) SpawnFurniture(it);
        }

        private FurnitureView SpawnFurniture(HomeItem it)
        {
            var def = Furniture.Find(it.id);
            if (def == null) return null;
            var v = new FurnitureView { data = it, def = def, go = PieceFactory.BuildModel(def.model) };
            v.go.transform.SetParent(house.root, false);
            v.go.transform.localScale = Vector3.one * FurnitureScale;
            PlaceFurniture(v);
            furniture.Add(v);
            return v;
        }

        private void PlaceFurniture(FurnitureView v)
        {
            var half = v.Half;
            v.data.x = Mathf.Clamp(v.data.x, half.x, house.lx - half.x);
            v.data.z = Mathf.Clamp(v.data.z, half.y, house.lz - half.y);
            v.go.transform.localPosition = new Vector3(v.data.x, v.data.y, v.data.z);
            v.go.transform.localRotation = Quaternion.Euler(0, 90f * v.data.rot, 0);
        }

        /// <summary>The furniture as boxes in the walker's way (only on its own floor).</summary>
        private IEnumerable<CityMap.Obstacle> FurnitureObstacles()
        {
            bool turned = Mathf.RoundToInt(house.root.eulerAngles.y / 90f) % 2 == 1;
            foreach (var v in furniture)
            {
                var c = v.go.transform.position;
                var half = v.Half;
                yield return new CityMap.Obstacle
                {
                    center = new Vector2(c.x, c.z), half = turned ? new Vector2(half.y, half.x) : half,
                    bottom = c.y, top = c.y + v.def.model.height * FurnitureScale, model = v.def.model,
                };
            }
        }

        /// <summary>Everything the walker bumps into: the city's models (not the car being driven) and the furniture.</summary>
        private void RebuildObstacles()
        {
            obstacles = city.Obstacles();
            if (carSlot != null)
            {
                var at = city.WorldOf(carSlot);
                obstacles.RemoveAll(o => Mathf.Abs(o.center.x - at.x) < 0.5f && Mathf.Abs(o.center.y - at.z) < 0.5f);
            }
            if (house != null) obstacles.AddRange(FurnitureObstacles());
        }

        // ---------- walking in the city and the house ----------

        private bool InHouse(Vector3 p) => house != null && house.pieces.Count > 0 && house.Covers(p);

        private float GroundAt(Vector3 p) => InHouse(p) ? house.Ground(p) : city.GroundAt(p);

        /// <summary>The walls of the house stop the walker; sliding along them works.</summary>
        private Vector3 HouseCollide(Vector3 next)
        {
            if (house == null || !house.Covers(next, 1f) || !house.Blocked(next, WallRadius)) return next;
            var alongX = new Vector3(next.x, next.y, walkPos.z);
            if (!house.Blocked(alongX, WallRadius)) return alongX;
            var alongZ = new Vector3(walkPos.x, next.y, next.z);
            if (!house.Blocked(alongZ, WallRadius)) return alongZ;
            return new Vector3(walkPos.x, next.y, walkPos.z);
        }

        private Vector3 ClampWalk(Vector3 next)
        {
            // the bounds are on the plan: the town's edges, the lake's quay, the player's lot out to the west
            var p = CityMap.PlanOf(next);
            float lotZ = CityLayout.HomeLotCenter.y, half = CityLayout.HomeLot / 2f - 0.6f;
            bool byLot = Mathf.Abs(p.y - lotZ) < half;
            var q = p;
            q.x = Mathf.Clamp(q.x, CityLayout.MinX + (byLot ? -CityLayout.HomeLot + 0.6f : 1f), CityLayout.MaxX - 1f);
            if (q.x < CityLayout.MinX) q.y = Mathf.Clamp(q.y, lotZ - half, lotZ + half);
            q.y = Mathf.Clamp(q.y, CityLayout.MinZ + 1f, CityLayout.MaxZ - 0.8f);
            if (q == p) return next;
            var w = city.WorldAt(q, 0f);
            return new Vector3(w.x, next.y, w.z);
        }

        /// <summary>Keeps the walking camera above the ground and in front of the house's walls.</summary>
        private Vector3 ClampCam(Vector3 target, Vector3 pos)
        {
            if (house != null && house.Covers(target, 2f)) pos = house.CamClamp(target, pos);
            pos.y = Mathf.Max(pos.y, city.GroundAt(pos) + 0.4f);
            return pos;
        }

        private int StoreyOf(float y) => Mathf.FloorToInt((y - HouseFloorY + 0.2f) / (BuildSite.Storey * Piece.Plate)) + 1;

        /// <summary>Inside the house: the "Dekore et" button and which storey you're on.</summary>
        private bool ShowHomeNearby()
        {
            bool inside = InHouse(walkPos);
            tu.decorBtn.gameObject.SetActive(inside);
            tu.homeBtn.gameObject.SetActive(false);
            if (!inside) return false;
            tu.nearChip.SetActive(true);
            tu.nearText.text = $"Evin · {StoreyOf(walkPos.y)}. kat";
            return true;
        }

        // ---------- decorating ----------

        private void StartDecor()
        {
            decorating = true;
            tu.canvas.gameObject.SetActive(false);
            du.canvas.gameObject.SetActive(true);
            du.ClearCards();
            foreach (var f in Furniture.All)
            {
                var def = f;
                du.AddCard(Previews.Model(f.model, 160), f.name, Progress.Level < f.level ? $"Seviye {f.level}" : null, () => AddFurniture(def));
            }
            ShowStorey(StoreyOf(walkPos.y));
            Select(null);
        }

        private void ShowStorey(int storey)
        {
            decorStorey = Mathf.Clamp(storey, 1, house.Storeys);
            house.SetView(decorStorey);
            foreach (var v in furniture) v.go.SetActive(v.data.y < decorStorey * BuildSite.Storey * Piece.Plate - 0.5f);
            du.help.text = $"{decorStorey}. kat · aşağıdan eşya ekle, sürükleyip taşı";
            if (selected != null && !selected.go.activeSelf) Select(null);
        }

        private void EndDecor()
        {
            decorating = false;
            Select(null);
            du.canvas.gameObject.SetActive(false);
            tu.canvas.gameObject.SetActive(true);
            house.SetView(0);
            foreach (var v in furniture) v.go.SetActive(true);
            RebuildObstacles();
            Progress.Save();
        }

        private void AddFurniture(FurnitureDef def)
        {
            // dropped on the floor in the middle of the shown storey
            var from = house.root.TransformPoint(new Vector3(house.lx / 2f, decorStorey * BuildSite.Storey * Piece.Plate - 1f, house.lz / 2f));
            if (!house.FloorUnder(new Ray(from, Vector3.down), out var at)) return;
            var local = house.root.InverseTransformPoint(at);
            var item = new HomeItem { id = def.id, x = local.x, y = local.y, z = local.z, rot = 0 };
            Progress.Data.home.items.Add(item);
            var v = SpawnFurniture(item);
            Select(v);
            Sfx.Play("place", 0.7f);
            Progress.Save();
        }

        private void Select(FurnitureView v)
        {
            selected = v;
            du.selectedBar.SetActive(v != null);
            if (v != null) du.selectedName.text = v.def.name;
            if (selMarker == null)
            {
                var b = new MeshBuilder();
                b.Box(new Vector3(0, 0.02f, 0), Vector3.one);
                var go = new GameObject("Selected");
                go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("sel");
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(1f, 0.84f, 0.1f), 0.2f);
                selMarker = go.transform;
                selMarker.SetParent(house.root, false);
            }
            selMarker.gameObject.SetActive(v != null);
        }

        private FurnitureView PickFurniture(Ray ray)
        {
            FurnitureView best = null;
            float bestD = float.MaxValue;
            foreach (var v in furniture)
            {
                if (!v.go.activeSelf) continue;
                var b = Previews.Bounds(v.go);
                b.Expand(0.2f);
                if (b.IntersectRay(ray, out float d) && d < bestD) { bestD = d; best = v; }
            }
            return best;
        }

        private void DecorDown(PointerEventData e)
        {
            var ray = cam.ScreenPointToRay(e.position);
            var v = PickFurniture(ray);
            Select(v);
            decorDrag = v != null && house.FloorUnder(ray, out var p);
            if (decorDrag)
            {
                house.FloorUnder(ray, out p);
                grabOffset = v.go.transform.position - p;
                grabOffset.y = 0f;
                Sfx.Play("pick", 0.5f);
            }
        }

        private void DecorDrag(PointerEventData e)
        {
            if (!decorDrag || selected == null) return;
            if (!house.FloorUnder(cam.ScreenPointToRay(e.position), out var p)) return;
            var local = house.root.InverseTransformPoint(p + grabOffset);
            float floor = (decorStorey - 1) * BuildSite.Storey * Piece.Plate;
            if (local.y < floor - 0.05f || local.y > floor + 1.3f) return;     // not up on a wall or down a stairwell
            selected.data.x = Mathf.Round(local.x * 4f) / 4f;
            selected.data.z = Mathf.Round(local.z * 4f) / 4f;
            selected.data.y = local.y;
            PlaceFurniture(selected);
        }

        private void DecorUpdate(float dt)
        {
            // the shown storey from above, seen from the road side
            var center = house.root.TransformPoint(new Vector3(house.lx / 2f, (decorStorey - 1) * BuildSite.Storey * Piece.Plate, house.lz / 2f + 1f));
            var front = house.root.rotation * Vector3.forward;
            cam.transform.position = center + front * 10f + Vector3.up * 17f;
            cam.transform.rotation = Quaternion.LookRotation(center - cam.transform.position);
            person.Animate(0f, false, dt);
            if (selected != null)
            {
                var half = selected.Half;
                selMarker.localPosition = new Vector3(selected.data.x, selected.data.y, selected.data.z);
                selMarker.localScale = new Vector3(half.x * 2f + 0.3f, 0.04f, half.y * 2f + 0.3f);
            }
        }
    }
}
