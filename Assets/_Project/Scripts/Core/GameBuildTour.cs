using System.Collections;
using System.Linq;
using BrickMuseum.FreeBuild;
using BrickMuseum.UI;
using UnityEngine;

namespace BrickMuseum.Core
{
    /// <summary>Test tour part: the free-building lot, one real tap, then a scripted two-storey house with a balcony.</summary>
    public sealed partial class Game
    {
        private IEnumerator BuildTour(string dir)
        {
            EnterBuild();
            yield return new WaitForSeconds(0.8f);
            int before = site.pieces.Count;
            buildKind = Stock.Find("brick:4x2x3");
            BuildTap(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Debug.Log($"[Tour] build tap: {before} -> {site.pieces.Count} pieces, box {Stock.Total}");
            yield return Shot(dir, "30_build_start");
            site.Undo();

            foreach (var k in Stock.Kinds) Stock.Add(k.key, 400);   // the test house needs a full box
            yield return null;
            ScriptedHouse(1);
            RefreshBuildUi();
            buildCam.distance = 40f; buildCam.yaw = 205f; buildCam.pitch = 40f;
            buildCam.target = BuildSite.HouseOrigin + new Vector3(0, 4f, 0);
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "31a_next_storey_guides");
            ScriptedHouse(2);
            RefreshBuildUi();
            buildCam.distance = 40f; buildCam.yaw = 205f; buildCam.pitch = 30f;
            buildCam.target = BuildSite.HouseOrigin + new Vector3(0, 4f, 0);
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "31_build_house");
            buildCam.yaw = 25f;
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "32_build_back");
            buildCam.yaw = 205f; buildCam.pitch = 50f;
            site.SetView(1);
            bu.storeyBtn.SetLabel("1. kata kadar");
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "33_build_storey1");
            site.SetView(0);
            bu.storeyBtn.SetLabel("Tüm katlar");
            ShowBuildTab("Özel");
            buildKind = Stock.Find("door:3x1x9");
            testGhost = true;
            site.ShowGhost(cam.ScreenPointToRay(new Vector2(Screen.width * 0.3f, Screen.height * 0.5f)), buildKind, "white", 0);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "34_build_special");
            testGhost = false;
            bu.colours.SetActive(true);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "35_build_colours");
            bu.colours.SetActive(false);
            Debug.Log($"[Tour] house built: {site.pieces.Count} pieces");
            ExitBuild();
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "36_city_home");
            yield return HouseWalkTour(dir);
            yield return CarBuildTour(dir);
            yield return FishTour(dir);
            yield return LotTour(dir);
            yield return ScoreTour(dir);
            yield return RoofTour(dir);
            yield return GardenTour(dir);
            yield return LanguageTour(dir);
        }

        /// <summary>The city score list, then a level-up bonus collected with the ad.</summary>
        private IEnumerator ScoreTour(string dir)
        {
            EnterCity(null);
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[Tour] city score {City.CityScore.Total}, bonuses waiting {Data.Progress.Data.bonusPending}");
            yield return Shot(dir, "53a_score_pill");
            ShowScore();
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "53b_score_list");
            cu.list.SetActive(false);
            if (Data.Progress.Data.bonusPending == 0) Data.Progress.Data.bonusPending = 1;
            ShowBonus();
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "53c_bonus_panel");
            int before = City.CityScore.Total;
            yield return CollectBonus();
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "53d_bonus_collected");
            Debug.Log($"[Tour] bonus: score {before} -> {City.CityScore.Total}, inventory {Data.Progress.Data.inventory.Count}, police {Data.Progress.Data.bonusPolice} workers {Data.Progress.Data.bonusWorkers} staff {Data.Progress.Data.bonusStaff}");
            ClosePanel();
        }

        /// <summary>Walking to a skyscraper's door, up to its roof, the city from above, and back down.</summary>
        private IEnumerator RoofTour(string dir)
        {
            EnterWalk();
            yield return new WaitForSeconds(0.4f);
            var towers = obstacles.Where(IsTower).OrderByDescending(o => o.top).ToList();
            Debug.Log($"[Tour] towers: {towers.Count}");
            if (towers.Count == 0) { ExitWalk(); yield break; }
            var t = towers[0];
            walkPos = new Vector3(t.center.x, 0f, t.center.y - t.half.y - 1.2f);
            walkPos.y = GroundAt(walkPos);
            facingYaw = camYaw = 0f; camPitch = 14f;
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "54a_tower_door");
            Debug.Log($"[Tour] roof button: {tu.homeBtn.gameObject.activeSelf} near {(roofNear != null ? roofNear.Value.model.id : "-")}");
            ClimbRoof(t);
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "54b_on_roof");
            testMove = new Vector2(0, 1);
            camYaw = 180f;
            yield return new WaitForSeconds(1.2f);
            testMove = Vector2.zero;
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "54c_roof_view");
            Debug.Log($"[Tour] roof: y {walkPos.y:F1} top {t.top:F1}, inside {Mathf.Abs(walkPos.x - t.center.x) <= t.half.x && Mathf.Abs(walkPos.z - t.center.y) <= t.half.y}");
            LeaveRoof();
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "54d_back_down");
            ExitWalk();
        }

        /// <summary>A city house's garden: a picket fence with a gate, a pool, a tree, flowers, a stone path, a lamp.</summary>
        private IEnumerator GardenTour(string dir)
        {
            var gs = City.CityLayout.Slots.First(x => x.kind == "house" && Data.Progress.At(x.id) != null);
            cityCam.Focus(city.WorldOf(gs), 60f);
            OpenSlot(gs);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "52a_garden_button");
            foreach (var k in Stock.Kinds) if (k.group == "Bahçe") Stock.Add(k.key, 60);
            EnterGarden(gs);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "52b_garden_empty");
            var g = site;
            int n = Gardens.Size(gs);
            for (int x = 0; x + 1 < n; x += 2)
            {
                g.PlaceAt("picket:2x1x3", "white", x, 0, 0, 0);
                if (Mathf.Abs(x + 1 - n / 2f) > 2f) g.PlaceAt("picket:2x1x3", "white", x, 0, n - 1, 0);    // a gate in the front
            }
            for (int z = 1; z + 1 < n - 1; z += 2) { g.PlaceAt("hedge:2x1x3", "green", 0, 0, z, 1); g.PlaceAt("hedge:2x1x3", "green", n - 1, 0, z, 1); }
            g.PlaceAt("pool:4x4x1", "white", n - 6, 0, 1, 0);
            g.PlaceAt("gtree:2x2x8", "green", 1, 0, 1, 0);
            g.PlaceAt("gtree:2x2x8", "rose", 1, 0, n - 4, 0);
            for (int x = n - 6; x < n - 2; x++) g.PlaceAt("flowers:1x1x1", x % 2 == 0 ? "yellow" : "rose", x, 0, n - 2, 0);
            for (int z = n - 4; z < n; z += 2) g.PlaceAt("stones:2x2x1", "gray", n / 2 - 1, 0, z, 0);
            g.PlaceAt("glamp:1x1x6", "dgray", n / 2 + 2, 0, n - 3, 0);
            RefreshBuildUi();
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "52c_garden_built");
            Debug.Log($"[Tour] garden: {g.pieces.Count} pieces on a {n}x{n} plot");
            ExitBuild();
            cityCam.Focus(city.WorldOf(gs), 55f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "52d_garden_in_city");
        }

        /// <summary>Buying the lot north of the house, building a second house on it, walking into it.</summary>
        private IEnumerator LotTour(string dir)
        {
            Wallet.Earn(2000);
            cityCam.Focus(city.WorldAt(City.CityLayout.LotCenter(1), 0f), 120f);
            OpenLot(1);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "51a_lot_for_sale");
            BuyLot(1);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "51b_lot_bought");
            ClosePanel();
            buildLot = 1;
            EnterBuild(false);
            yield return new WaitForSeconds(0.3f);
            ScriptedHouse(1);
            ScriptedHouse(2);
            RefreshBuildUi();
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "51c_second_house_build");
            ExitBuild();
            cityCam.Focus(city.WorldAt(City.CityLayout.LotCenter(0) + new Vector2(0, -16f), 0f), 150f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "51d_two_houses");
            EnterWalk();
            yield return new WaitForSeconds(0.4f);
            var h2 = houses[1];
            walkPos = h2.root.TransformPoint(new Vector3(11.5f, 0f, 19f));
            walkPos.y = GroundAt(walkPos);
            var face = h2.root.rotation * Vector3.back;
            facingYaw = camYaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg; camPitch = 10f; walkCamDist = 5f;
            testMove = new Vector2(0, 0.6f);
            yield return new WaitForSeconds(1.2f);
            testMove = Vector2.zero;
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "51e_in_second_house");
            Debug.Log($"[Tour] lots owned {Lots.OwnedCount}, money {Wallet.Money}, chip '{tu.nearText.text}'");
            walkCamDist = 6.5f;
            ExitWalk();
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>Fishing on the quay (cast, bite, reel, catch), then selling the catch at the Fish Hall.</summary>
        private IEnumerator FishTour(string dir)
        {
            EnterWalk();
            yield return new WaitForSeconds(0.5f);
            walkPos = city.WorldAt(new Vector2(-60f, City.CityLayout.ZOf(0) + City.CityLayout.Block / 2f + 1.6f), 0f);
            walkPos.y = city.GroundAt(walkPos);
            facingYaw = camYaw = 0f; camPitch = 14f;
            yield return new WaitForSeconds(0.4f);
            StartFishing();
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "50a_fishing_ready");
            holdDown = true;
            yield return new WaitForSeconds(0.7f);
            yield return Shot(dir, "50b_charging");
            holdDown = false;
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "50c_cast");
            yield return new WaitForSeconds(0.6f);
            biteAt = 0.3f;
            while (fishing != FishState.Bite) yield return null;
            yield return new WaitForSeconds(0.2f);
            yield return Shot(dir, "50d_bite");
            holdDown = true;
            float shotAt = Time.time + 1.2f;
            bool shotTaken = false;
            while (fishing == FishState.Reeling || fishing == FishState.Bite)
            {
                holdDown = tension < 0.52f;                    // a steady hand: pull when slack, give when tight
                if (!shotTaken && Time.time > shotAt) { shotTaken = true; yield return Shot(dir, "50e_reeling"); }
                yield return null;
            }
            holdDown = false;
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "50f_caught");
            Debug.Log($"[Tour] fish in the cooler: {Data.Progress.Data.fish.Count}, state {fishing}");
            StopFishing();
            // the Fish Hall on a public building plot, the catch sold there
            var hallSlot = City.CityLayout.Slots.First(s => s.kind == "civic" && s.level <= Data.Progress.Level);
            if (Data.Progress.At(hallSlot.id) != null) Data.Progress.Remove(hallSlot.id);
            Data.Progress.Place(hallSlot.id, "balikhali");
            city.Refresh(hallSlot);
            RebuildObstacles();
            var hf = Quaternion.Euler(0, hallSlot.yaw + City.CityShape.Angle(hallSlot.local), 0) * Vector3.forward;
            walkPos = city.WorldOf(hallSlot) + hf * (hallSlot.d / 2f - 2f);
            walkPos.y = city.GroundAt(walkPos);
            facingYaw = camYaw = Mathf.Atan2(-hf.x, -hf.z) * Mathf.Rad2Deg; camPitch = 10f;
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "50g_fish_hall");
            int before = Wallet.Money;
            SellFish();
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "50h_sold");
            Debug.Log($"[Tour] fish sold: {before} -> {Wallet.Money}");
            ExitWalk();
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>The city with a panel open in every language, the walking and building screens in a few; back to Turkish.</summary>
        private IEnumerator LanguageTour(string dir)
        {
            var slot = City.CityLayout.Slots.First(s => s.kind == "house" && Data.Progress.At(s.id) != null);
            ShowLanguages();
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "50_languages");
            cu.list.SetActive(false);
            foreach (var l in new[] { Data.Language.English, Data.Language.Japanese, Data.Language.Arabic, Data.Language.German, Data.Language.French,
                         Data.Language.Italian, Data.Language.Spanish, Data.Language.Korean, Data.Language.Azerbaijani })
            {
                Data.Loc.Set(l);
                cityCam.Focus(city.WorldOf(slot), 90f);
                OpenSlot(slot);
                yield return new WaitForSeconds(0.4f);
                yield return Shot(dir, $"51_city_{l}");
                ClosePanel();
                if (l == Data.Language.Japanese || l == Data.Language.Arabic || l == Data.Language.German)
                {
                    EnterBuild(false);
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot(dir, $"52_build_{l}");
                    ExitBuild();
                    EnterWalk();
                    yield return new WaitForSeconds(0.8f);
                    yield return Shot(dir, $"53_walk_{l}");
                    ExitWalk();
                    yield return new WaitForSeconds(0.4f);
                }
            }
            Data.Loc.Set(Data.Language.Turkish);
        }

        /// <summary>World yaw of a direction given on the house's lot.</summary>
        private float LotYaw(Vector3 localDir) { var w = house.root.rotation * localDir; return Mathf.Atan2(w.x, w.z) * Mathf.Rad2Deg; }

        private void StandOnLot(float x, float y, float z, Vector3 heading)
        {
            walkPos = house.root.TransformPoint(new Vector3(x, y, z));
            walkPos.y = GroundAt(walkPos);
            walkVy = 0f; grounded = true;
            facingYaw = camYaw = LotYaw(heading);
        }

        /// <summary>Into the house through the front door, up the stairs, out onto the balcony; decorating two storeys.</summary>
        private IEnumerator HouseWalkTour(string dir)
        {
            EnterWalk();
            yield return new WaitForSeconds(1f);
            yield return Shot(dir, "37_walk_home");
            StandOnLot(11.5f, 0f, 19f, Vector3.back);
            camPitch = 10f; walkCamDist = 5f;
            testMove = new Vector2(0, 0.7f);
            yield return new WaitForSeconds(1.1f);
            yield return Shot(dir, "38_door_open");
            yield return new WaitForSeconds(1.2f);
            testMove = Vector2.zero;
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "39_inside_ground");
            // up the stairs: step on from the side at the low end, then climb toward -x
            StandOnLot(16.4f, 0f, 9.8f, Vector3.back);
            testMove = new Vector2(0, 0.5f);
            yield return new WaitForSeconds(0.9f);
            camYaw = facingYaw = LotYaw(Vector3.left);
            running = true; testMove = new Vector2(0, 1f);
            yield return new WaitForSeconds(0.7f);
            yield return Shot(dir, "40_stairs");
            yield return new WaitForSeconds(0.6f);
            running = false;
            camYaw = LotYaw(Vector3.forward);
            yield return new WaitForSeconds(0.8f);
            testMove = Vector2.zero;
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[Tour] after the stairs: storey {StoreyOf(walkPos.y)}, y {walkPos.y - HouseFloorY:0.0}");
            yield return Shot(dir, "41_upstairs");
            // out through the upper door onto the balcony
            StandOnLot(11.5f, 4f, 13.2f, Vector3.forward);
            testMove = new Vector2(0, 0.6f);
            yield return new WaitForSeconds(1.6f);
            testMove = Vector2.zero;
            camYaw = LotYaw(Vector3.back); camPitch = 16f;
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[Tour] balcony: y {walkPos.y - HouseFloorY:0.0}");
            yield return Shot(dir, "42_balcony");

            // decorate: the ground floor, then upstairs
            StandOnLot(13f, 0f, 11.5f, Vector3.back);
            camPitch = 14f; walkCamDist = 6.5f;
            yield return null;
            StartDecor();
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "43_decor_storey1");
            AddFurniture(Home.Furniture.Find("kanepe"));
            selected.data.x = 13f; selected.data.z = 12.5f; selected.data.rot = 2; PlaceFurniture(selected);
            AddFurniture(Home.Furniture.Find("tv"));
            selected.data.x = 13f; selected.data.z = 10f; PlaceFurniture(selected);
            AddFurniture(Home.Furniture.Find("masa"));
            selected.data.x = 8.5f; selected.data.z = 12.5f; PlaceFurniture(selected);
            AddFurniture(Home.Furniture.Find("saksi"));
            selected.data.x = 16f; selected.data.z = 14f; PlaceFurniture(selected);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "44_decor_added");
            ShowStorey(2);
            AddFurniture(Home.Furniture.Find("yatak"));
            selected.data.x = 14.5f; selected.data.z = 12f; PlaceFurniture(selected);
            AddFurniture(Home.Furniture.Find("dolap"));
            selected.data.x = 8f; selected.data.z = 14f; selected.data.rot = 2; PlaceFurniture(selected);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "45_decor_storey2");
            EndDecor();
            StandOnLot(15.5f, 0f, 14f, new Vector3(-0.6f, 0, -1f));
            camPitch = 18f; walkCamDist = 5.5f;
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "46_inside_furnished");
            walkCamDist = 6.5f; camPitch = 14f;
            ExitWalk();
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>A small car built on the chassis (wheels, a floor plate, a bonnet, a windscreen, a roof), then driven.</summary>
        private IEnumerator CarBuildTour(string dir)
        {
            EnterBuild(true);
            yield return new WaitForSeconds(0.5f);
            var s = site;
            foreach (var (x, z) in new[] { (1, 2), (6, 2), (1, 8), (6, 8) }) s.PlaceAt("wheel:2x1x3", "black", x, 0, z, 1);
            for (int x = 1; x <= 5; x += 2) for (int z = 1; z <= 9; z += 2) s.PlaceAt("brick:2x2x1", "dgray", x, 3, z, 0);
            for (int x = 1; x <= 5; x += 2) { s.PlaceAt("brick:2x2x3", "blue", x, 4, 9, 0); s.PlaceAt("brick:2x2x3", "blue", x, 4, 1, 0); }
            s.PlaceAt("window:4x1x6", "white", 2, 4, 7, 0);
            foreach (int y in new[] { 4, 7 })
            {
                foreach (int z in new[] { 3, 5 }) { s.PlaceAt("brick:2x1x3", "blue", 1, y, z, 1); s.PlaceAt("brick:2x1x3", "blue", 6, y, z, 1); }
                s.PlaceAt("brick:1x1x3", "blue", 1, y, 7, 0); s.PlaceAt("brick:1x1x3", "blue", 6, y, 7, 0);
            }
            for (int x = 1; x <= 5; x += 2) for (int z = 3; z <= 7; z += 2) s.PlaceAt("brick:2x2x1", "white", x, 10, z, 0);
            s.PlaceAt("taxisign:2x1x3", "yellow", 3, 11, 5, 0);                           // it's a taxi
            RefreshBuildUi();
            Debug.Log($"[Tour] car built: {s.pieces.Count} pieces, wheels {s.CountOf("wheel")}");
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "47_car_build");
            // a second car in the garage: a little red runabout
            SwitchCar(true);
            yield return new WaitForSeconds(0.3f);
            s = site;
            foreach (var (x, z) in new[] { (1, 2), (6, 2), (1, 8), (6, 8) }) s.PlaceAt("wheel:2x1x3", "black", x, 0, z, 1);
            for (int x = 1; x <= 5; x += 2) for (int z = 1; z <= 9; z += 2) s.PlaceAt("brick:2x2x1", "red", x, 3, z, 0);
            for (int x = 1; x <= 5; x += 2) { s.PlaceAt("brick:2x2x3", "red", x, 4, 1, 0); s.PlaceAt("slope:2x2x3", "red", x, 4, 8, 3); }
            RefreshBuildUi();
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "47b_second_car");
            Debug.Log($"[Tour] garage: {Garage.Cars.Count} cars, money {Wallet.Money}");
            ExitBuild();
            yield return new WaitForSeconds(0.4f);
            EnterWalk();
            yield return new WaitForSeconds(0.6f);
            if (car != null)
            {
                walkPos = car.position + car.rotation * Vector3.left * (carHalfWid + 1.6f);
                walkPos.y = city.GroundAt(walkPos);
                facingYaw = camYaw = carYaw + 60f; camPitch = 12f; walkCamDist = 6f;
                yield return new WaitForSeconds(0.6f);
                yield return Shot(dir, "48_my_built_car");
                GetIntoCar();
                testMove = new Vector2(0, 1);
                yield return new WaitForSeconds(2f);
                yield return Shot(dir, "49_driving_built");
                testMove = Vector2.zero;
                // a taxi fare: the strip to the passenger, pick them up, the strip to their stop, get paid
                yield return new WaitForSeconds(0.6f);
                Debug.Log($"[Tour] taxi: {carName} taxi={carIsTaxi}");
                int before = Wallet.Money;
                StartTaxiJob();
                yield return new WaitForSeconds(1f);
                camPitch = 30f;
                yield return Shot(dir, "49b_taxi_route");
                Debug.Log($"[Tour] {route.Debug}, passenger at {fare.root.position}, car {car.position}");
                car.position = city.WorldAt(fareStreet, 0f); car.position = new Vector3(car.position.x, city.GroundAt(car.position), car.position.z);
                carSpeed = 0f;
                yield return new WaitForSeconds(1f);
                yield return Shot(dir, "49c_passenger_in");
                car.position = city.WorldAt(dropStreet, 0f); car.position = new Vector3(car.position.x, city.GroundAt(car.position), car.position.z);
                carSpeed = 0f;
                yield return new WaitForSeconds(1f);
                yield return Shot(dir, "49d_paid");
                Debug.Log($"[Tour] taxi paid: {before} -> {Wallet.Money}");
                camPitch = 14f;
                GetOutOfCar();
            }
            Debug.Log($"[Tour] built car: {(car != null ? carName : "none")}");
            walkCamDist = 6.5f;
            ExitWalk();
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>A test house: two storeys with a door, windows, stairs inside, a balcony with a railing and a flat roof.</summary>
        private void ScriptedHouse(int stage)
        {
            const int x0 = 6, x1 = 17, z0 = 6, z1 = 15;
            var s = site;
            if (stage == 2) goto upper;
            // openings and the stairs first, the walls fill round them
            s.PlaceAt("door:3x1x9", "white", 10, 0, z1, 0);
            s.PlaceAt("window:2x1x6", "white", 7, 3, z1, 0);
            s.PlaceAt("window:2x1x6", "white", 14, 3, z1, 0);
            s.PlaceAt("window:4x1x6", "white", 9, 3, z0, 0);
            s.PlaceAt("window:2x1x6", "white", x0, 3, 10, 1);
            s.PlaceAt("window:2x1x6", "white", x1, 3, 10, 1);
            s.PlaceAt("stairs:10x2x10", "gray", 7, 0, 7, 0);
            foreach (int y in new[] { 0, 3, 6 }) Ring(x0, z0, x1, z1, y, "terra");
            // the upper floor (a hole over the stairs) and the balcony in front
            for (int x = x0; x <= x1; x += 2)
                for (int z = z0; z <= z1; z += 2)
                    if (!(z + 1 >= 7 && z <= 8 && x + 1 >= 7 && x <= 16)) s.PlaceAt("brick:2x2x1", "brown", x, 9, z, 0);
            if (stage == 1) return;
            upper:
            foreach (int x in new[] { 8, 10, 12 }) { s.PlaceAt("brick:2x2x1", "gray", x, 9, 16, 0); s.PlaceAt("fence:2x1x3", "white", x, 10, 17, 0); }
            s.PlaceAt("door:3x1x9", "white", 10, 10, z1, 0);
            s.PlaceAt("window:2x1x6", "white", 14, 13, z1, 0);
            s.PlaceAt("window:2x1x6", "white", 7, 13, z1, 0);
            s.PlaceAt("window:4x1x6", "white", 9, 13, z0, 0);
            foreach (int y in new[] { 10, 13, 16 }) Ring(x0, z0, x1, z1, y, "cream");
            for (int x = x0; x <= x1; x += 2)
                for (int z = z0; z <= z1; z += 2) s.PlaceAt("brick:2x2x1", "dred", x, 19, z, 0);
            for (int x = x0; x <= x1 - 1; x += 2) { s.PlaceAt("fence:2x1x3", "white", x, 20, z1, 0); s.PlaceAt("fence:2x1x3", "white", x, 20, z0, 0); }
        }

        /// <summary>One course of bricks round a rectangle (running bond: odd courses start half a brick on).</summary>
        private void Ring(int x0, int z0, int x1, int z1, int y, string colour)
        {
            bool odd = y / 3 % 2 == 1;
            Course(x0, z0, x1, z0, y, colour, odd);
            Course(x0, z1, x1, z1, y, colour, odd);
            Course(x0, z0 + 1, x0, z1 - 1, y, colour, !odd);
            Course(x1, z0 + 1, x1, z1 - 1, y, colour, !odd);
        }

        private void Course(int xa, int za, int xb, int zb, int y, string colour, bool shift)
        {
            bool alongX = za == zb;
            int len = alongX ? xb - xa + 1 : zb - za + 1;
            for (int i = 0; i < len;)
            {
                int x = alongX ? xa + i : xa, z = alongX ? za : za + i;
                bool firstHalf = shift && i == 0;
                if (!site.IsFree(x, y, z)) { i++; continue; }
                if (!firstHalf && i + 1 < len && site.PlaceAt("brick:2x1x3", colour, x, y, z, alongX ? 0 : 1)) { i += 2; continue; }
                site.PlaceAt("brick:1x1x3", colour, x, y, z, 0);
                i++;
            }
        }
    }
}
