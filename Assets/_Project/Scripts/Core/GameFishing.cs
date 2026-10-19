using System.Linq;
using BrickMuseum.Build;
using BrickMuseum.City;
using BrickMuseum.Data;
using BrickMuseum.FreeBuild;
using BrickMuseum.UI;
using BrickMuseum.World;
using UnityEngine;

namespace BrickMuseum.Core
{
    /// <summary>
    /// Fishing from the lake's quay or the creek's banks. OLTA: the rod comes into the hand. FIRLAT (hold): the power
    /// swings up and down, the rod leans back; let go: the rod whips forward and the float flies out in an arc. Then
    /// wait: the float bobs; a bite pulls it under (and the phone buzzes) — ÇEK now! Reeling: hold to pull, let go to
    /// give line; the fish pulls back; keep the needle in the green and the fish comes in; too tight snaps the line,
    /// too slack lets it go. Caught fish go into the cooler and are sold at the Fish Hall (if the town has built one).
    /// </summary>
    public sealed partial class Game
    {
        private enum FishState { Off, Ready, Charging, Flying, Waiting, Bite, Reeling, Landing }

        private FishState fishing;
        private bool fishCreek;
        private Transform rod, bobber, caughtFish;
        private LineRenderer line;
        private float power, castT, stateTime, biteAt, tension, landed, slack, tight, rodLean, castDist;
        private Vector3 castFrom, castTo, bobberRest;
        private FishKind hooked;
        private bool holdDown;
        private readonly System.Random fishRnd = new System.Random();
        private const float RodLength = 3.2f;

        private void WireFishing()
        {
            tu.fishBtn.onClick.AddListener(() => { if (fishing == FishState.Off) StartFishing(); else StopFishing(); });
            tu.hold.Down = _ => holdDown = true;
            tu.hold.Up = _ => holdDown = false;
            tu.sellBtn.onClick.AddListener(SellFish);
        }

        // ---------- where you can fish ----------

        /// <summary>On the quay (facing the lake) or on a creek bank (facing the water): true, and which way to face.</summary>
        private bool FishingSpot(out float yaw, out bool creek)
        {
            var p = CityMap.PlanOf(walkPos);
            yaw = 0f; creek = false;
            if (p.y > CityLayout.ZOf(0) + CityLayout.Block / 2f - 0.5f && p.x > CityLayout.MinX && p.x < CityLayout.MaxX) return true;   // the quay: the lake is south
            if (CityLayout.InCreek(p) && !CityLayout.OnStreetZ(p.y))
            {
                float d = Mathf.Abs(p.x - CityLayout.CreekX);
                if (d > CityLayout.CreekWater - 1f && d < CityLayout.CreekWater + 7f)
                {
                    creek = true;
                    yaw = p.x < CityLayout.CreekX ? 90f : 270f;
                    return true;
                }
            }
            return false;
        }

        // ---------- the rod ----------

        private void StartFishing()
        {
            if (!FishingSpot(out float yaw, out fishCreek)) return;
            fishing = FishState.Ready;
            facingYaw = camYaw = yaw; camPitch = 16f;
            power = 0f; rodLean = 0f; holdDown = false;
            if (rod == null) MakeRod();
            rod.gameObject.SetActive(true);
            bobber.gameObject.SetActive(false);
            line.enabled = false;
            tu.fishBtn.SetLabel("BIRAK");
            tu.holdBtn.SetLabel("FIRLAT");
            tu.holdBtn.gameObject.SetActive(true);
            tu.jumpBtn.gameObject.SetActive(false);
            tu.help.text = "FIRLAT'ı basılı tut, bırak · balık vurunca ÇEK";
            Sfx.Play("pick");
        }

        private void StopFishing()
        {
            if (fishing == FishState.Off) return;
            fishing = FishState.Off;
            if (rod != null) { rod.gameObject.SetActive(false); bobber.gameObject.SetActive(false); line.enabled = false; }
            if (caughtFish != null) { Destroy(caughtFish.gameObject); caughtFish = null; }
            tu.fishBtn.SetLabel("OLTA");
            tu.holdBtn.gameObject.SetActive(false);
            tu.powerBar.SetActive(false);
            tu.fishPanel.SetActive(false);
            tu.jumpBtn.gameObject.SetActive(true);
            tu.help.text = "Sol başparmak: yürü   ·   Sağ başparmak: etrafına bak";
        }

        private void MakeRod()
        {
            var b = new MeshBuilder();
            b.Cylinder(Vector3.zero, 0.07f, 0.07f, 0.7f, 8);                        // the handle
            b.Cylinder(new Vector3(0, 0.7f, 0), 0.045f, 0.018f, RodLength - 0.7f, 8);
            rod = new GameObject("Rod").transform;
            rod.SetParent(person.root, false);
            rod.localPosition = new Vector3(0.48f, 1.05f, 0.35f);
            rod.gameObject.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("rod");
            rod.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.25f, 0.2f, 0.16f), 0.4f);
            var reel = new MeshBuilder();
            reel.Xf = Matrix4x4.TRS(new Vector3(0, 0.45f, 0.1f), Quaternion.Euler(0, 0, 90), Vector3.one);
            reel.Cylinder(new Vector3(0, -0.06f, 0), 0.12f, 0.12f, 0.12f, 12);
            var rg = new GameObject("Reel");
            rg.transform.SetParent(rod, false);
            rg.AddComponent<MeshFilter>().sharedMesh = reel.ToMesh("reel");
            rg.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.7f, 0.72f, 0.75f), 0.7f);

            var f = new MeshBuilder();
            f.Cylinder(Vector3.zero, 0.16f, 0.16f, 0.16f, 12);
            var fl = new MeshBuilder();
            fl.Dome(new Vector3(0, 0.16f, 0), 0.16f, 0.16f, 12);
            bobber = new GameObject("Float").transform;
            bobber.SetParent(transform, false);
            bobber.gameObject.AddComponent<MeshFilter>().sharedMesh = f.ToMesh("float");
            bobber.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(Color.white, 0.5f);
            var top = new GameObject("Top");
            top.transform.SetParent(bobber, false);
            top.AddComponent<MeshFilter>().sharedMesh = fl.ToMesh("floattop");
            top.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.9f, 0.15f, 0.12f), 0.5f);

            line = rod.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 12;
            line.widthMultiplier = 0.03f;
            line.sharedMaterial = Mats.Unlit(new Color(0.95f, 0.95f, 0.95f));
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
        }

        private Vector3 RodTip => rod.TransformPoint(new Vector3(0, RodLength, 0));

        /// <summary>The water's surface height (world) under a point: the lake, or the creek.</summary>
        private float WaterY(Vector3 world) => fishCreek ? CityMap.Center.y + CityShape.Height(CityMap.PlanOf(world)) + CityMap.WaterTop : Landscape.Ground + 0.1f;

        private void DrawLine(float sag)
        {
            var a = RodTip; var b = bobber.position + Vector3.up * 0.16f;
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (line.positionCount - 1f);
                line.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * sag);
            }
        }

        // ---------- the loop ----------

        private void FishUpdate(float dt)
        {
            stateTime += dt;
            var fwd = Quaternion.Euler(0, facingYaw, 0) * Vector3.forward;
            person.root.SetPositionAndRotation(walkPos, Quaternion.Euler(0, facingYaw, 0));
            person.Animate(0f, false, dt);
            person.HoldRightArm(fishing == FishState.Charging ? 120f : 60f);
            tu.powerBar.SetActive(fishing == FishState.Charging);
            tu.fishPanel.SetActive(fishing == FishState.Reeling);
            switch (fishing)
            {
                case FishState.Ready:
                    rodLean = Mathf.MoveTowards(rodLean, 0f, dt * 200f);
                    if (holdDown) { fishing = FishState.Charging; stateTime = 0f; }
                    break;
                case FishState.Charging:
                    power = Mathf.PingPong(stateTime * 1.1f, 1f);
                    tu.SetPower(power);
                    rodLean = Mathf.MoveTowards(rodLean, -70f, dt * 260f);               // the rod goes back over the shoulder
                    if (!holdDown) Cast(fwd);
                    break;
                case FishState.Flying:
                {
                    rodLean = Mathf.MoveTowards(rodLean, 25f, dt * 900f);                // and whips forward
                    castT = Mathf.Min(1f, stateTime / 0.9f);
                    var p = Vector3.Lerp(castFrom, castTo, castT) + Vector3.up * Mathf.Sin(castT * Mathf.PI) * (2f + castDist * 0.25f);
                    bobber.position = p;
                    DrawLine(0.05f);
                    if (castT >= 1f)
                    {
                        fishing = FishState.Waiting; stateTime = 0f;
                        biteAt = 2f + (float)fishRnd.NextDouble() * 5f;
                        bobberRest = castTo;
                        Sfx.Play("rustle", 0.5f);
                    }
                    break;
                }
                case FishState.Waiting:
                    rodLean = Mathf.MoveTowards(rodLean, 10f, dt * 80f);
                    bobber.position = bobberRest + Vector3.up * Mathf.Sin(time * 2.2f) * 0.05f;
                    DrawLine(0.6f);
                    if (holdDown) { ReelInEmpty(); break; }                               // pulled in before a bite
                    if (stateTime > biteAt)
                    {
                        fishing = FishState.Bite; stateTime = 0f;
                        hooked = Fish.Pick(fishCreek, fishRnd);
                        tu.holdBtn.SetLabel("ÇEK!");
                        Sfx.Play("wrong", 0.4f); Sfx.Haptic();
                    }
                    break;
                case FishState.Bite:
                    // the float is pulled under, again and again
                    bobber.position = bobberRest + Vector3.down * (0.25f + 0.15f * Mathf.Abs(Mathf.Sin(time * 14f))) + fwd * Mathf.Sin(time * 9f) * 0.12f;
                    DrawLine(0.2f);
                    tu.holdBtn.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(time * 18f));
                    if (holdDown)
                    {
                        fishing = FishState.Reeling; stateTime = 0f; tension = 0.5f; landed = 0f; slack = 0f; tight = 0f;
                        tu.holdBtn.transform.localScale = Vector3.one;
                        tu.holdBtn.SetLabel("ÇEK");
                        tu.fishText.text = "Gerginliği yeşilde tut";
                    }
                    else if (stateTime > 2.2f)
                    {
                        tu.holdBtn.transform.localScale = Vector3.one;
                        tu.holdBtn.SetLabel("FIRLAT");
                        Toast("Balık yemi kaptı, kaçtı");
                        fishing = FishState.Waiting; stateTime = 0f; biteAt = 2f + (float)fishRnd.NextDouble() * 4f;
                    }
                    break;
                case FishState.Reeling:
                    Reel(dt, fwd);
                    break;
                case FishState.Landing:
                    // the fish dangles from the line for a moment
                    if (caughtFish != null)
                    {
                        caughtFish.position = RodTip + Vector3.down * 1.1f;
                        caughtFish.rotation = Quaternion.Euler(0, time * 120f, Mathf.Sin(time * 12f) * 25f);
                        bobber.position = caughtFish.position + Vector3.up * 0.4f;
                        DrawLine(0f);
                    }
                    rodLean = Mathf.MoveTowards(rodLean, -20f, dt * 120f);
                    if (stateTime > 1.8f)
                    {
                        if (caughtFish != null) { Destroy(caughtFish.gameObject); caughtFish = null; }
                        bobber.gameObject.SetActive(false); line.enabled = false;
                        fishing = FishState.Ready;
                        tu.holdBtn.SetLabel("FIRLAT");
                    }
                    break;
            }
            rod.localRotation = Quaternion.Euler(55f - rodLean - (fishing == FishState.Reeling ? tension * 25f : 0f), 0, 0);
            // the camera behind, looking out over the water
            var target = walkPos + Vector3.up * 2.0f + fwd * 3f;
            var rot = Quaternion.Euler(camPitch, camYaw, 0);
            var pos = target - rot * Vector3.forward * (walkCamDist + 2f);
            pos = ClampCam(target, pos);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(target - pos);
            city.people.Tick(dt, walkPos, 90f, walkPos);
        }

        private void Cast(Vector3 fwd)
        {
            castDist = Mathf.Lerp(5f, fishCreek ? 9f : 22f, power);
            castFrom = RodTip;
            var to = walkPos + fwd * castDist;
            if (fishCreek)
            {
                // land in the creek's water, not on the far bank
                var p = CityMap.PlanOf(to);
                p.x = Mathf.Clamp(p.x, CityLayout.CreekX - CityLayout.CreekWater + 1f, CityLayout.CreekX + CityLayout.CreekWater - 1f);
                to = city.WorldAt(p, 0f);
            }
            to.y = WaterY(to);
            castTo = to;
            bobber.gameObject.SetActive(true);
            bobber.position = castFrom;
            line.enabled = true;
            fishing = FishState.Flying; stateTime = 0f;
            Sfx.Play("pick", 0.8f);
        }

        private void ReelInEmpty()
        {
            bobber.gameObject.SetActive(false); line.enabled = false;
            fishing = FishState.Ready; holdDown = false;
            Toast("Olta boş geldi");
        }

        /// <summary>The fight: holding pulls (tension up), letting go gives line; the fish pulls in bursts.</summary>
        private void Reel(float dt, Vector3 fwd)
        {
            float s = hooked.strength;
            // gentle: small runs, a slow needle, a wide green; the line snaps only after staying at the top a while
            float pull = (Mathf.PerlinNoise(time * 0.8f, 3.1f) - 0.5f) * 0.7f * s;
            tension += (holdDown ? 0.45f : -0.4f) * dt + pull * dt;
            tension = Mathf.Clamp(tension, 0f, 1f);
            bool inGreen = tension >= 0.25f && tension <= 0.8f;
            landed = Mathf.Clamp01(landed + (inGreen ? 0.32f / Mathf.Sqrt(s) : -0.03f) * dt);
            slack = tension < 0.06f ? slack + dt : 0f;
            tight = tension > 0.97f ? tight + dt : 0f;
            tu.SetReel(tension, landed);
            // the float comes in as the fish does, splashing about
            bobber.position = Vector3.Lerp(bobberRest, walkPos + fwd * 2.5f + Vector3.up * (WaterY(walkPos + fwd * 2.5f) - walkPos.y), landed)
                              + new Vector3(Mathf.Sin(time * 11f), 0, Mathf.Cos(time * 13f)) * 0.15f;
            bobber.position = new Vector3(bobber.position.x, WaterY(bobber.position) - 0.2f, bobber.position.z);
            DrawLine(0.4f * (1f - tension));
            if (holdDown && Mathf.Repeat(time, 0.25f) < dt) Sfx.Play("tick", 0.4f);
            if (tight > 0.8f) { LoseFish("Misina koptu!"); return; }
            if (slack > 3f) { LoseFish("Balık kaçtı"); return; }
            if (landed >= 1f) Landed();
        }

        private void LoseFish(string why)
        {
            Toast(why);
            Sfx.Play("fail", 0.6f);
            bobber.gameObject.SetActive(false); line.enabled = false;
            tu.holdBtn.SetLabel("FIRLAT");
            fishing = FishState.Ready; holdDown = false;
        }

        private void Landed()
        {
            var item = Fish.Catch(hooked, fishRnd);
            Progress.Data.fish.Add(item);
            Progress.Save();
            fishing = FishState.Landing; stateTime = 0f; holdDown = false;
            // a little brick fish on the line
            var b = new MeshBuilder();
            float len = Mathf.Lerp(0.5f, 1.4f, (item.cm - 9f) / 70f);
            b.Box(new Vector3(0, 0, 0), new Vector3(len, len * 0.38f, len * 0.16f));
            b.Box(new Vector3(len * 0.55f, 0, 0), new Vector3(len * 0.22f, len * 0.42f, len * 0.06f));
            caughtFish = new GameObject("CaughtFish").transform;
            caughtFish.SetParent(transform, false);
            caughtFish.gameObject.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("fish");
            caughtFish.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(hooked.creek ? new Color(0.55f, 0.6f, 0.45f) : new Color(0.72f, 0.78f, 0.85f), 0.8f);
            bool hall = HasFishHall;
            Toast($"{hooked.name} · {item.cm} cm · {item.price} lira" + (hall ? "" : "\nSatmak için şehre Balık Hali yap"), 2.8f);
            Sfx.Play("win");
        }

        // ---------- selling ----------

        private bool HasFishHall => Progress.Data.placements.Any(p => p.id == "balikhali");

        /// <summary>Beside the Fish Hall: true (the sell button shows when there is fish in the cooler).</summary>
        private bool NearFishHall()
        {
            foreach (var o in obstacles)
            {
                if (o.model == null || o.model.id != "balikhali") continue;
                var off = new Vector2(walkPos.x, walkPos.z) - o.center;
                var outside = new Vector2(Mathf.Max(0, Mathf.Abs(off.x) - o.half.x), Mathf.Max(0, Mathf.Abs(off.y) - o.half.y));
                if (outside.magnitude < 12f) return true;
            }
            return false;
        }

        private void SellFish()
        {
            var list = Progress.Data.fish;
            if (list.Count == 0) return;
            int total = list.Sum(f => f.price), n = list.Count;
            list.Clear();
            Wallet.Earn(total);
            Toast($"{n} balık satıldı · +{total} lira", 2.6f);
            Sfx.Play("win");
        }

        /// <summary>The fishing and selling buttons and the cooler, while walking.</summary>
        private void FishHud()
        {
            int n = Progress.Data.fish.Count;
            tu.cooler.text = $"Balık: {n}";
            bool spot = fishing == FishState.Off && !driving && FishingSpot(out _, out _);
            tu.fishBtn.gameObject.SetActive(spot || fishing != FishState.Off);
            tu.sellBtn.gameObject.SetActive(!driving && fishing == FishState.Off && n > 0 && NearFishHall());
        }
    }
}
