using System.Collections;
using System.IO;
using System.Linq;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.Workshop;
using UnityEngine;

namespace BrickMuseum.Core
{
    /// <summary>
    /// Development test tour (`-shots <folder>`): plays the city loop by itself and saves screenshots of every screen,
    /// then quits. Only in development builds and the editor.
    /// </summary>
    public sealed partial class Game
    {
        private IEnumerator Tour(string dir)
        {
            if (!Debug.isDebugBuild) yield break;
            Directory.CreateDirectory(dir);
            Progress.Reset();
            EnterCity(null);
            yield return new WaitForSeconds(1.5f);
            yield return Shot(dir, "01_city");

            // tap the first Turkish house plot
            var plot = CityLayout.Slots.First(s => s.kind == "house" && s.style == "tr");
            cityCam.Focus(city.WorldOf(plot), 95f);
            yield return null;
            OpenSlot(plot);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "02_plot_panel");

            BuildFor(plot);
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "03_roulette");
            while (spinning) yield return null;
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "04_target");
            wu.refModal.SetActive(false);
            while (!bench.Complete) { PlaceNext(); yield return null; }
            yield return new WaitForSeconds(1.2f);
            yield return Shot(dir, "05_win");
            wu.win.SetActive(false);
            PlaceFinished();
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "06_house_in_city");

            // a parking spot: build a vehicle and keep it in the inventory
            var parking = CityLayout.Slots.First(s => s.kind == "vehicle" && s.level <= Progress.Level);
            BuildFor(parking);
            while (spinning) yield return null;
            wu.refModal.SetActive(false);
            while (!bench.Complete) { PlaceNext(); yield return null; }
            yield return new WaitForSeconds(0.8f);
            wu.win.SetActive(false);
            PlaceFinished();
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "07_car_parked");

            // grow the city: every model finished with stars and placed on the first free slot it fits
            foreach (var m in ModelCatalog.All)
            {
                if (Progress.Owned(m.id)) continue;
                Progress.Finished(m.id, 1 + m.index % 3);
                var free = CityLayout.Slots.FirstOrDefault(s => Progress.At(s.id) == null && Progress.Fits(m, s));
                if (free != null) Progress.Place(free.id, m.id); else Progress.ToInventory(m.id);
            }
            levelBefore = 1;
            EnterCity(null);
            CheckLevelUp();
            cityCam.Focus(CityMap.Center, 300f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "08_level_up_city");
            cu.levelUp.SetActive(false);
            cityCam.Focus(AreaPoint("Türk Mahallesi"), 110f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "09_turkish_block");
            cityCam.Focus(AreaPoint("Yalı Boyu"), 110f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "10_park");
            cityCam.Focus(AreaPoint("Eserler Bahçesi"), 130f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "11_landmarks");
            cityCam.Focus(AreaPoint("Merkez"), 150f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "11b_center");
            cityCam.Focus(AreaPoint("Sahil Parkı") + new Vector3(0, 0, 30f), 330f);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "11c_lake_city");
            yield return SizeLineup(dir);

            // take a model away, then put it on another free slot from the inventory
            var filled = CityLayout.Slots.First(s => s.kind == "vehicle" && Progress.At(s.id) != null);
            cityCam.Focus(city.WorldOf(filled), 90f);
            OpenSlot(filled);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "12_filled_panel");
            TakeAway(filled);
            var empty = CityLayout.Slots.First(s => s.kind == "vehicle" && Progress.At(s.id) == null && s.level <= Progress.Level);
            OpenSlot(empty);
            yield return new WaitForSeconds(0.2f);
            yield return Shot(dir, "13_empty_with_inventory");
            PickFromInventory(empty);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "14_inventory_pick");
            cu.list.SetActive(false);
            ClosePanel();

            // a locked slot and the collection
            var locked = CityLayout.Slots.OrderByDescending(s => s.level).First();
            OpenSlot(locked);
            yield return new WaitForSeconds(0.2f);
            yield return Shot(dir, "15_locked_slot");
            ClosePanel();
            ShowCollection();
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "16_collection");
            wu.collection.SetActive(false);
            ShowInventory();
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "17_inventory");
            cu.list.SetActive(false);
            cityCam.Focus(AreaPoint("Kamu Binaları") + new Vector3(0, 0, 26f), 38f);
            yield return new WaitForSeconds(2f);
            Debug.Log($"[Tour] townspeople: {city.people.Count}");
            yield return Shot(dir, "17b_people_city");

            // walking (landscape TPS); the sports car is the player's own car
            if (Progress.Data.placements.Any(p => p.id == "sport")) Progress.Data.myCar = "sport";
            EnterWalk();
            yield return new WaitForSeconds(1.2f);
            yield return Shot(dir, "18_walk_start");
            testMove = new Vector2(0, 1);
            yield return new WaitForSeconds(1.1f);
            yield return Shot(dir, "19_walking");
            running = true;
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "20_running");
            testMove = Vector2.zero; running = false;
            // the people of the public buildings and the houses on their pavements
            foreach (var (area, shot) in new[] { ("Kamu Binaları", "20b_people_civic"), ("Türk Mahallesi", "20c_people_houses") })
            {
                walkPos = AreaPoint(area) + new Vector3(26f, 0, CityLayout.Block / 2f + 1.2f);
                walkPos.y = city.GroundAt(walkPos);
                facingYaw = camYaw = 270f; camPitch = 9f; walkCamDist = 5f;
                yield return new WaitForSeconds(2.5f);
                yield return Shot(dir, shot);
            }
            walkCamDist = 6.5f;
            // my own car: walk up to it, get in, drive off down the street and round a corner
            if (car != null)
            {
                walkPos = car.position + car.rotation * Vector3.left * (carHalfWid + 1.6f);
                walkPos.y = city.GroundAt(walkPos);
                facingYaw = camYaw = carYaw + 90f; camPitch = 12f; walkCamDist = 7f;
                yield return new WaitForSeconds(0.6f);
                yield return Shot(dir, "20d_my_car");
                GetIntoCar();
                testMove = new Vector2(0, 1);
                yield return new WaitForSeconds(2.2f);
                yield return Shot(dir, "20e_driving");
                testMove = new Vector2(0.8f, 0.6f);
                yield return new WaitForSeconds(1.2f);
                yield return Shot(dir, "20f_turning");
                testMove = Vector2.zero;
                yield return new WaitForSeconds(1.5f);
                GetOutOfCar();
                yield return new WaitForSeconds(0.5f);
                yield return Shot(dir, "20g_got_out");
                Debug.Log($"[Tour] car {carName} at {car.position}");
            }
            else Debug.Log("[Tour] no car");

            walkCamDist = 6.5f; camPitch = 14f;
            // stand in front of the first Turkish house, looking at it
            var house = CityLayout.Slots.First(s => s.kind == "house" && Progress.At(s.id) != null);
            var front = Quaternion.Euler(0, house.yaw, 0) * Vector3.forward;
            walkPos = city.WorldOf(house) + front * (house.d / 2f + 3f);
            walkPos.y = city.GroundAt(walkPos);
            facingYaw = camYaw = Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg;
            camPitch = 12f;
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "21_near_house");
            jumpQueued = true;
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "22_jump");
            yield return new WaitForSeconds(0.8f);
            // close-up of the person's face
            camYaw = facingYaw + 180f; camPitch = 6f; walkCamDist = 3.2f;
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "23_face");
            testMove = new Vector2(0, -1); // walk toward the camera
            yield return new WaitForSeconds(0.45f);
            yield return Shot(dir, "24_walk_front");
            testMove = Vector2.zero; walkCamDist = 6.5f;
            ExitWalk();
            yield return new WaitForSeconds(1f);
            yield return Shot(dir, "25_back_to_city");

            // character workshop
            OpenCharacter();
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "26_character");
            editing.hat = "top"; editing.shirt = "red"; editing.pattern = "tie"; editing.pants = "black"; editing.hairStyle = "long"; editing.hair = "black";
            studio.Show(editing);
            ShowWardrobeTab(3);
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "27_character_hat");
            editing.hat = "helmet"; editing.shirt = "orange"; editing.pattern = "stripe"; editing.skin = "brown"; editing.hairStyle = "bangs";
            studio.Show(editing);
            ShowWardrobeTab(4);
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "28_character_worker");
            editing.hat = "crown"; editing.shirt = "gold"; editing.pattern = "plain"; editing.skin = "cream"; editing.hair = "yellow";
            CloseCharacter();
            EnterWalk();
            camYaw = facingYaw + 180f; camPitch = 6f; walkCamDist = 3.4f;
            yield return new WaitForSeconds(1.2f);
            yield return Shot(dir, "29_walk_new_outfit");
            walkCamDist = 6.5f;
            ExitWalk();
            yield return new WaitForSeconds(0.6f);
            yield return BuildTour(dir);
            Application.Quit();
        }

        /// <summary>Development: `-pictures <folder>` saves a picture of every model (as the roulette shows them) and quits.</summary>
        private static void SavePictures(string dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var m in ModelCatalog.All)
                File.WriteAllBytes(Path.Combine(dir, $"{m.index:000}_{m.id}.png"), Build.Previews.Model(m, 320).EncodeToPNG());
            Debug.Log($"[Pictures] {ModelCatalog.All.Count} saved");
            Application.Quit();
        }

        private static Vector3 AreaPoint(string name) { var c = CityLayout.AreaNamed(name).center; return CityMap.Center + new Vector3(c.x, 0, c.y); }

        private bool tourCam;

        /// <summary>Size check: a person and models of every kind side by side at their city size, with their real size written above.</summary>
        private IEnumerator SizeLineup(string dir)
        {
            SetLandscape(true);
            yield return new WaitForSeconds(0.6f);
            tourCam = true;
            cam.fieldOfView = 40f;
            yield return Lineup(dir, "11d_sizes_small", -60f, new[] { "moto", "car", "bus", "at", "giraffe", "cam", "tr_1" }, 1.0f);
            yield return Lineup(dir, "11e_sizes_big", 60f, new[] { "tr_1", "apartman", "gokdelen", "piramit", "eiffel", "bigben" }, 2.2f);
            tourCam = false;
            SetLandscape(false);
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>A person and the given models in a row on the lake at their city size, real heights written above them.</summary>
        private IEnumerator Lineup(string dir, string shot, float startX, string[] ids, float camFar)
        {
            var root = new GameObject("SizeLineup").transform;
            float x = startX, z = -100f, y = World.Landscape.Ground + 0.08f;
            void Label(float cx, float top, string text, float size)
            {
                var t = new GameObject("Label").AddComponent<TMPro.TextMeshPro>();
                t.transform.SetParent(root, false);
                t.transform.position = new Vector3(cx, top + size * 0.25f, z);
                t.transform.rotation = Quaternion.Euler(0, 180, 0);                 // faces the camera in the south
                t.font = UI.Fonts.Bold; t.text = text; t.fontSize = size * 3f; t.alignment = TMPro.TextAlignmentOptions.Center; t.color = Color.black;
                t.rectTransform.sizeDelta = new Vector2(size * 3f, size);
            }
            var p = new Character.BrickPerson(root, Progress.Outfit);
            p.root.position = new Vector3(x, y, z); p.root.rotation = Quaternion.Euler(0, 180, 0);
            Label(x, 2.2f, "İnsan\n1,8 m", camFar * 1.6f); x += 3f;
            foreach (var id in ids)
            {
                var m = ModelCatalog.Find(id); if (m == null) continue;
                float w = m.sx * m.scale;
                var go = Build.PieceFactory.BuildModel(m);
                go.transform.SetParent(root, false);
                go.transform.localScale = Vector3.one * m.scale;
                go.transform.position = new Vector3(x + w / 2f, y, z);
                Label(x + w / 2f, m.height * m.scale, $"{m.name}\n{m.height * m.scale * 0.9f:0.#} m", camFar * 1.6f);
                x += w + 3f;
            }
            float mid = (startX + x) / 2f, width = x - startX;
            cam.transform.position = new Vector3(mid, y + 6f * camFar, z + width * 0.85f);
            cam.transform.LookAt(new Vector3(mid, y + 4f * camFar, z));
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, shot);
            Destroy(root.gameObject);
        }

        private Vector2 ScreenOf(Target t) { var s = cam.WorldToScreenPoint(t.center); return new Vector2(s.x, s.y); }

        private void PlaceNext()
        {
            var t = bench.targets.First(x => x.available);
            var it = wu.bag.items.First(i => i.kind.key == t.p.key);
            BeginHold(it);
            HoldMove(ScreenOf(t));
            if (snapped == null || snapped.p.key != t.p.key) Debug.LogWarning($"[Tour] no snap for {t.p.key}");
            HoldRelease();
        }

        private static IEnumerator Shot(string dir, string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null;
            yield return null;
            Debug.Log("[Tour] " + name);
        }
    }
}
