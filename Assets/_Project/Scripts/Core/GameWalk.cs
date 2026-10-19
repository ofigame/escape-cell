using System.Collections.Generic;
using BrickMuseum.Character;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BrickMuseum.Core
{
    /// <summary>
    /// Walking through the city in third person, in landscape: the brick person walks / runs / jumps, steps up kerbs, can't
    /// walk through the models, and the name of the model next to them is shown.
    /// </summary>
    public sealed partial class Game
    {
        private readonly TpsUi tu = new TpsUi();
        private BrickPerson person;
        private string personKey;
        private Vector3 walkPos;
        private float walkVy, facingYaw, camYaw, camPitch = 14f, walkCamDist = 6.5f;
        private bool grounded, running, jumpQueued;
        private int stickId = int.MinValue, lookId = int.MinValue;
        private Vector2 stickOrigin, stickVec;
        private List<CityMap.Obstacle> obstacles = new List<CityMap.Obstacle>();
        private Vector2 testMove;
        // one pace only: the walker always moves at running speed (user wish: no separate walk mode)
        private const float WalkSpeed = 7.5f, RunSpeed = 7.5f, Radius = 0.6f, JumpSpeed = 8.5f, Gravity = 24f;

        private void WireWalk()
        {
            tu.Build(transform);
            tu.backBtn.onClick.AddListener(ExitWalk);
            tu.jumpBtn.onClick.AddListener(() => jumpQueued = true);
            tu.runBtn.onClick.AddListener(() => { running = !running; tu.SetRun(running); });
            tu.carBtn.onClick.AddListener(() => { if (driving) GetOutOfCar(); else GetIntoCar(); });
            tu.touch.Down = WalkDown;
            tu.touch.Drag = WalkDrag;
            tu.touch.Up = WalkUp;
            cu.walkBtn.onClick.AddListener(EnterWalk);
            WireHome();
            WireTaxi();
            WireFishing();
        }

        private static void SetLandscape(bool on)
        {
            if (Application.isMobilePlatform) Screen.orientation = on ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            else Screen.SetResolution(on ? 960 : 540, on ? 540 : 960, FullScreenMode.Windowed);
        }

        private void EnterWalk()
        {
            mode = Mode.Walk;
            ClosePanel();
            cu.canvas.gameObject.SetActive(false);
            tu.canvas.gameObject.SetActive(true);
            SetLandscape(true);
            if (person != null && personKey != Progress.Outfit.Key) { Destroy(person.root.gameObject); person = null; }
            if (person == null) { person = new BrickPerson(transform, Progress.Outfit); personKey = Progress.Outfit.Key; }
            person.root.gameObject.SetActive(true);
            // start on the street in front of the Turkish neighbourhood, looking into the city
            var start = CityLayout.AreaNamed("Türk Mahallesi").center;   // on the street in front of it
            walkPos = city.WorldAt(start + new Vector2(0, CityLayout.Block / 2f + CityLayout.Road / 2f), 0f);
            walkPos.y = city.GroundAt(walkPos);
            facingYaw = camYaw = 180f;
            if (Progress.Data.build.Count > 0)
            {
                // with a house of your own you start on the road in front of it, looking at it
                walkPos = city.WorldAt(new Vector2(CityLayout.MinX + 5f, CityLayout.HomeLotCenter.y + 3f), 0f);
                walkPos.y = city.GroundAt(walkPos);
                facingYaw = camYaw = 270f;
            }
            walkVy = 0f; grounded = true; running = false; tu.SetRun(false);
            obstacles = city.Obstacles();
            SpawnCar();
            if (house != null) obstacles.AddRange(FurnitureObstacles());
            decorating = false;
            tu.SetHomeLabel("GİR");
            tu.homeBtn.gameObject.SetActive(false);
            tu.decorBtn.gameObject.SetActive(false);
            cam.fieldOfView = 60f;
            ShadowDistance(90f);
            stickId = lookId = int.MinValue;
            tu.stickBase.gameObject.SetActive(false);
        }

        private void ExitWalk()
        {
            if (decorating) EndDecor();
            CancelTaxi(false);
            StopFishing();
            if (driving) GetOutOfCar();
            RemoveCar();
            SetLandscape(false);
            tu.canvas.gameObject.SetActive(false);
            if (person != null) person.root.gameObject.SetActive(false);
            var at = walkPos;
            EnterCity(null);
            cityCam.Focus(new Vector3(at.x, CityMap.Center.y, at.z), 100f);
        }

        // ---------- touch ----------

        private void WalkDown(PointerEventData e)
        {
            if (e.position.x < Screen.width * 0.45f && stickId == int.MinValue)
            {
                stickId = e.pointerId;
                stickOrigin = e.position;
                stickVec = Vector2.zero;
                tu.stickBase.gameObject.SetActive(true);
                tu.stickBase.anchoredPosition = e.position / tu.canvas.scaleFactor;
                tu.stickKnob.anchoredPosition = Vector2.zero;
            }
            else if (lookId == int.MinValue) lookId = e.pointerId;
        }

        private void WalkDrag(PointerEventData e)
        {
            if (e.pointerId == stickId)
            {
                float radius = 140f * tu.canvas.scaleFactor;
                stickVec = Vector2.ClampMagnitude((e.position - stickOrigin) / radius, 1f);
                tu.stickKnob.anchoredPosition = stickVec * 140f;
            }
            else if (e.pointerId == lookId)
            {
                float k = 0.18f / Mathf.Max(0.5f, tu.canvas.scaleFactor);
                camYaw += e.delta.x * k;
                camPitch = Mathf.Clamp(camPitch - e.delta.y * k, -8f, 55f);
            }
        }

        private void WalkUp(PointerEventData e)
        {
            if (e.pointerId == stickId) { stickId = int.MinValue; stickVec = Vector2.zero; tu.stickBase.gameObject.SetActive(false); }
            if (e.pointerId == lookId) lookId = int.MinValue;
        }

        // ---------- movement ----------

        private void WalkUpdate(float dt)
        {
            tu.money.text = $"{FreeBuild.Wallet.Money} lira";
            if (!driving) tu.taxiBtn.gameObject.SetActive(false);
            if (decorating) { DecorUpdate(dt); return; }
            if (driving) { CarUpdate(dt); FishHud(); return; }
            FishHud();
            if (fishing != FishState.Off) { FishUpdate(dt); return; }
            var move = stickVec + testMove;
            var kb = Keyboard.current;
            bool runKey = false;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                if (kb.spaceKey.wasPressedThisFrame) jumpQueued = true;
                runKey = kb.leftShiftKey.isPressed;
            }
            move = Vector2.ClampMagnitude(move, 1f);
            var camRot = Quaternion.Euler(0, camYaw, 0);
            var dir = camRot * new Vector3(move.x, 0, move.y);
            float speed = (running || runKey ? RunSpeed : WalkSpeed) * move.magnitude;
            var next = walkPos + dir.normalized * speed * dt;
            next = HouseCollide(Collide(next));
            next = ClampWalk(next);
            float ground = GroundAt(next);
            if (ground - walkPos.y > 1.1f) { next.x = walkPos.x; next.z = walkPos.z; ground = GroundAt(next); } // too high to step up
            float moved = new Vector2(next.x - walkPos.x, next.z - walkPos.z).magnitude / Mathf.Max(dt, 0.0001f);

            if (jumpQueued && grounded) { walkVy = JumpSpeed; grounded = false; Sfx.Play("pick", 0.5f); }
            jumpQueued = false;
            walkVy -= Gravity * dt;
            next.y = walkPos.y + walkVy * dt;
            if (next.y <= ground) { next.y = ground; walkVy = 0f; grounded = true; }
            else if (next.y - ground > 0.05f) grounded = false;
            walkPos = next;

            if (move.sqrMagnitude > 0.01f) facingYaw = Mathf.MoveTowardsAngle(facingYaw, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 540f * dt);
            person.root.SetPositionAndRotation(walkPos, Quaternion.Euler(0, facingYaw, 0));
            person.Animate(moved, !grounded, dt);

            // camera behind and above the person, kept above the ground
            var target = walkPos + Vector3.up * 2.0f;
            var rot = Quaternion.Euler(camPitch, camYaw, 0);
            var pos = target - rot * Vector3.forward * walkCamDist;
            pos = ClampCam(target, pos);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(target - pos);

            city.people.Tick(dt, walkPos, 90f, walkPos);
            house?.Tick(walkPos, dt);
            ShowNearby();
        }

        /// <summary>Pushes the walker (a circle) out of every model box taller than a step.</summary>
        private Vector3 Collide(Vector3 p)
        {
            foreach (var o in obstacles)
            {
                if (walkPos.y > o.top - 0.3f) continue;
                if (o.bottom != 0f && walkPos.y + 1.8f < o.bottom) continue; // on top of something low (a bench, a car roof after a jump)
                float dx = p.x - o.center.x, dz = p.z - o.center.y;
                float cx = Mathf.Clamp(dx, -o.half.x, o.half.x), cz = Mathf.Clamp(dz, -o.half.y, o.half.y);
                var d = new Vector2(dx - cx, dz - cz);
                float dist = d.magnitude;
                if (dist >= Radius) continue;
                if (dist < 0.0001f)
                {
                    // inside the box: out through the nearest side
                    float px = o.half.x - Mathf.Abs(dx), pz = o.half.y - Mathf.Abs(dz);
                    if (px < pz) p.x = o.center.x + Mathf.Sign(dx) * (o.half.x + Radius); else p.z = o.center.y + Mathf.Sign(dz) * (o.half.y + Radius);
                }
                else { var push = d / dist * (Radius - dist); p.x += push.x; p.z += push.y; }
            }
            return p;
        }

        private void ShowNearby()
        {
            CityMap.Obstacle? best = null;
            float bestD = float.MaxValue;
            foreach (var o in obstacles)
            {
                if (o.model.kind == "furniture") continue;
                // distance from the walker to the model's box
                var off = new Vector2(walkPos.x, walkPos.z) - o.center;
                var outside = new Vector2(Mathf.Max(0, Mathf.Abs(off.x) - o.half.x), Mathf.Max(0, Mathf.Abs(off.y) - o.half.y));
                float d = outside.magnitude;
                if (d < 9f && d < bestD) { bestD = d; best = o; }
            }
            if (ShowHomeNearby()) { tu.carBtn.gameObject.SetActive(false); return; }
            bool nearCar = car != null && NearCar();
            tu.carBtn.gameObject.SetActive(nearCar);
            if (nearCar) { tu.nearChip.SetActive(true); tu.nearText.text = $"Araban: {carName}"; return; }
            string who = city.people.Nearest(walkPos, 3f, out _);
            if (who != null) { tu.nearChip.SetActive(true); tu.nearText.text = who; return; }
            tu.nearChip.SetActive(best != null);
            if (best != null)
            {
                int stars = Progress.Of(best.Value.model.id).stars;
                tu.nearText.text = $"{best.Value.model.name}  ·  {stars} yıldız";
            }
        }
    }
}
