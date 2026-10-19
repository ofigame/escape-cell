using System.Linq;
using BrickMuseum.Build;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.FreeBuild;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrickMuseum.Core
{
    /// <summary>
    /// The player's own car: the one they built freely on the chassis (once it has four wheels), or else one of the city's
    /// vehicles chosen with "Arabam olsun". In walk mode it waits
    /// on its parking space (or next to the start when it is in the inventory); walk up to it, get in and drive it round
    /// the streets. It stays on the roads (kerbs stop it), bumps into models and parked cars, and goes back to its
    /// space when the walk ends.
    /// </summary>
    public sealed partial class Game
    {
        private Transform car;
        private ModelDef carModel;
        private string carName;
        public const float BuiltCarScale = 0.5f;
        public const int ChassisX = 8, ChassisZ = 12;

        /// <summary>The free-built car can be driven once it has at least four wheels.</summary>
        private static bool HasBuiltCar => Progress.Data.carBuild.Count(p => p.kind.StartsWith("wheel")) >= 4;
        private Slot carSlot;
        private bool driving;
        private float carYaw, carSpeed, carHalfLen, carHalfWid;
        private const float CarAccel = 9f, CarBrake = 18f, CarTop = 20f, CarReverse = 6f, CarTurn = 75f;

        /// <summary>The car stands ready when walking starts; with no car chosen, the first parked vehicle becomes it.</summary>
        private void SpawnCar()
        {
            RemoveCar();
            var data = Progress.Data;
            if (HasBuiltCar) { SpawnBuiltCar(); return; }
            if (string.IsNullOrEmpty(data.myCar) || !Progress.Owned(data.myCar))
            {
                // no car chosen yet: the first parked car (a four-wheeler before a scooter or a motorbike)
                var first = data.placements.Where(p => ModelCatalog.Find(p.id)?.kind == "vehicle").OrderBy(p => p.id == "scooter" || p.id == "moto" ? 1 : 0).FirstOrDefault();
                data.myCar = first != null ? first.id : "";
                if (first == null) return;
                Progress.Save();
            }
            carModel = ModelCatalog.Find(data.myCar);
            if (carModel == null) return;
            carName = carModel.name;
            var placed = data.placements.FirstOrDefault(p => p.id == carModel.id);
            carSlot = placed != null ? CityLayout.Find(placed.slot) : null;

            car = new GameObject("MyCar").transform;
            var body = PieceFactory.BuildModel(carModel).transform;
            body.SetParent(car, false);
            body.localRotation = Quaternion.Euler(0, -90f, 0);           // the vehicles' fronts point to model +x
            body.localScale = Vector3.one * carModel.scale;
            carHalfLen = carModel.sx * carModel.scale / 2f;
            carHalfWid = carModel.sz * carModel.scale / 2f;

            if (carSlot != null)
            {
                // drive it off its parking space: hide the parked copy and leave its box out of the collisions
                var parked = city.ModelOn(carSlot);
                if (parked != null) parked.SetActive(false);
                var at = city.WorldOf(carSlot);
                obstacles.RemoveAll(o => Mathf.Abs(o.center.x - at.x) < 0.5f && Mathf.Abs(o.center.y - at.z) < 0.5f);
                car.position = new Vector3(at.x, CityMap.Center.y + CityMap.RoadTop, at.z);
                carYaw = carSlot.yaw + 90f;
            }
            else
            {
                car.position = new Vector3(walkPos.x - 7f, CityMap.Center.y + CityMap.RoadTop, walkPos.z + 2.5f);
                carYaw = 270f;
            }
            car.rotation = Quaternion.Euler(0, carYaw, 0);
            carSpeed = 0f;
        }

        /// <summary>The player's own free-built car, on the road by the start (beside the house when there is one).</summary>
        private void SpawnBuiltCar()
        {
            carModel = null; carSlot = null; carName = "Arabam";
            var list = Progress.Data.carBuild;
            int minX = int.MaxValue, maxX = 0, minZ = int.MaxValue, maxZ = 0;
            foreach (var p in list)
            {
                var k = Stock.Find(p.kind);
                if (k == null) continue;
                int fw = p.r % 2 == 1 ? k.d : k.w, fd = p.r % 2 == 1 ? k.w : k.d;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x + fw);
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z + fd);
            }
            car = new GameObject("MyCar").transform;
            // the pieces centred on the car's origin, front (+z of the chassis) forward
            var corner = -new Vector3((minX + maxX) / 2f, 0, (minZ + maxZ) / 2f) * BuiltCarScale;
            new BuildSite(car, list, ChassisX, ChassisZ, corner, 0f, BuiltCarScale, false, null, false);
            carHalfLen = (maxZ - minZ) * BuiltCarScale / 2f;
            carHalfWid = (maxX - minX) * BuiltCarScale / 2f;
            if (Progress.Data.build.Count > 0)
            {
                car.position = CityMap.Center + new Vector3(CityLayout.MinX + 13f, CityMap.RoadTop, CityLayout.HomeLotCenter.y - 6f);
                carYaw = 180f;
            }
            else
            {
                car.position = new Vector3(walkPos.x - 7f, CityMap.Center.y + CityMap.RoadTop, walkPos.z + 2.5f);
                carYaw = 270f;
            }
            car.rotation = Quaternion.Euler(0, carYaw, 0);
            carSpeed = 0f;
        }

        private void RemoveCar()
        {
            if (car == null) return;
            Destroy(car.gameObject);
            car = null;
            if (carSlot != null) { var parked = city.ModelOn(carSlot); if (parked != null) parked.SetActive(true); }
            carSlot = null;
        }

        private bool NearCar()
        {
            var off = Quaternion.Inverse(car.rotation) * (walkPos - car.position);
            return Mathf.Abs(off.x) < carHalfWid + 2.5f && Mathf.Abs(off.z) < carHalfLen + 2.5f;
        }

        private void GetIntoCar()
        {
            if (car == null) return;
            driving = true;
            person.root.gameObject.SetActive(false);
            tu.SetCarLabel("İN");
            tu.carBtn.gameObject.SetActive(true);
            tu.jumpBtn.gameObject.SetActive(false);
            tu.runBtn.gameObject.SetActive(false);
            tu.help.text = "Sol başparmak: gaz, fren ve direksiyon   ·   Sağ başparmak: etrafına bak";
            camYaw = carYaw; camPitch = 14f;
            Sfx.Play("place", 0.6f);
        }

        private void GetOutOfCar()
        {
            driving = false;
            carSpeed = 0f;
            // step out on the driver's side, or the other side when that is the kerb
            var side = car.rotation * Vector3.left * (carHalfWid + 1.2f);
            var spot = car.position + side;
            if (city.GroundAt(spot) - car.position.y > 1.1f) spot = car.position - side;
            walkPos = new Vector3(spot.x, city.GroundAt(spot), spot.z);
            facingYaw = carYaw;
            person.root.gameObject.SetActive(true);
            person.root.SetPositionAndRotation(walkPos, Quaternion.Euler(0, facingYaw, 0));
            tu.SetCarLabel("BİN");
            tu.jumpBtn.gameObject.SetActive(true);
            tu.runBtn.gameObject.SetActive(true);
            tu.help.text = "Sol başparmak: yürü   ·   Sağ başparmak: etrafına bak";
            walkCamDist = 6.5f;
        }

        private void CarUpdate(float dt)
        {
            var input = stickVec + testMove;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1;
            }
            input = Vector2.ClampMagnitude(input, 1f);

            // gas / brake / reverse, then rolling to a stop
            float gas = input.y;
            if (gas > 0.05f) carSpeed += (carSpeed < 0 ? CarBrake : CarAccel) * gas * dt;
            else if (gas < -0.05f) carSpeed += (carSpeed > 0 ? CarBrake : CarAccel * 0.6f) * gas * dt;
            else carSpeed = Mathf.MoveTowards(carSpeed, 0f, 4f * dt);
            carSpeed = Mathf.Clamp(carSpeed, -CarReverse, CarTop);

            // steering turns more as the car gets going, and the other way round when reversing
            float grip = Mathf.Clamp(carSpeed / 5f, -1f, 1f);
            float yaw = carYaw + input.x * CarTurn * grip * dt;
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var next = car.position + fwd * carSpeed * dt;
            if (CarBlocked(next, yaw))
            {
                if (Mathf.Abs(carSpeed) > 6f) Sfx.Play("pick", 0.4f);
                carSpeed = -carSpeed * 0.25f;   // a small bounce back
            }
            else { car.position = next; carYaw = yaw; }
            car.rotation = Quaternion.Euler(0, carYaw, 0);
            walkPos = car.position;

            // the camera follows behind, unless the player is looking round
            if (lookId == int.MinValue) camYaw = Mathf.MoveTowardsAngle(camYaw, carYaw, 120f * dt);
            var target = car.position + Vector3.up * 2.2f;
            var rot = Quaternion.Euler(camPitch, camYaw, 0);
            var pos = target - rot * Vector3.forward * (carHalfLen * 2f + 7f);
            pos.y = Mathf.Max(pos.y, city.GroundAt(pos) + 0.4f);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(target - pos);

            city.people.Tick(dt, car.position, 90f, car.position);
            tu.nearChip.SetActive(true);
            tu.nearText.text = $"{carName}  ·  {Mathf.RoundToInt(Mathf.Abs(carSpeed) * 0.9f * 3.6f)} km/sa";
        }

        /// <summary>The car (a box) would leave the road, run into a model or leave the town.</summary>
        private bool CarBlocked(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var c = CityMap.Center;
            if (p.x < c.x + CityLayout.MinX + carHalfWid || p.x > c.x + CityLayout.MaxX - carHalfWid) return true;
            if (p.z < c.z + CityLayout.MinZ + carHalfWid || p.z > c.z + CityLayout.ZOf(0) + CityLayout.Block / 2f - carHalfWid) return true;
            float road = c.y + CityMap.RoadTop + 0.15f;
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f), (0f, 1f), (0f, -1f), (-1f, 0f), (1f, 0f) })
            {
                var q = p + rot * new Vector3(sx * carHalfWid, 0, sz * carHalfLen);
                if (city.GroundAt(q) > road) return true;                                // a kerb
                foreach (var o in obstacles)
                    if (Mathf.Abs(q.x - o.center.x) < o.half.x && Mathf.Abs(q.z - o.center.y) < o.half.y) return true;
            }
            return false;
        }
    }
}
