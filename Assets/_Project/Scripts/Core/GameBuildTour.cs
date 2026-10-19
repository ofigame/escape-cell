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
            yield return LanguageTour(dir);
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
            RefreshBuildUi();
            Debug.Log($"[Tour] car built: {s.pieces.Count} pieces, wheels {s.CountOf("wheel")}");
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "47_car_build");
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
