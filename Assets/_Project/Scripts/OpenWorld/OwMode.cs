using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace SquashBot.OpenWorld
{
    /// <summary>
    /// The open world, played: its own camera (the isometric view upright, or over foi's shoulder sideways), its own
    /// copy of foi walking on the land, its own screen. The game's scene and screens step aside while it runs and come
    /// back when the player leaves. Everything foi can act on is found by walking up to it: the big button says what
    /// (sow, harvest, pick, feed, collect, build, the barn, the co-op).
    /// </summary>
    public class OwMode : MonoBehaviour
    {
        public static bool Active { get; private set; }

        private const float Speed = 5.5f;

        private OwSave save;
        private OwWorld world;
        private OwFarm farm;
        private OwUi ui;
        private Camera cam;
        private Transform foi;
        private Vector2 pos;
        private float facing, yaw = 45f, pitch = 22f, bob;
        private bool tps;
        private Target target;
        private float streamT, nearT;
        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private readonly List<Camera> hiddenCameras = new List<Camera>();
        private readonly List<Vector3> blockers = new List<Vector3>();
        private Action left;

        /// <summary>The open world is open: the escape is complete (level 250 won), or the build opens everything for testing.</summary>
        public static bool Open(int levelCount) => SaveData.TestMode || Progress.Stars(levelCount - 1) > 0 || SaveData.UnlockedLevel >= levelCount;

        public static OwMode Enter(Transform foiVisual, Action onLeave)
        {
            var go = new GameObject("OpenWorld");
            var m = go.AddComponent<OwMode>();
            m.left = onLeave;
            m.Begin(foiVisual);
            return m;
        }

        private void Begin(Transform foiVisual)
        {
            Active = true;
            save = OwSave.Current;
            save.RefreshDay();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.enabled && c.isRootCanvas) { c.enabled = false; hiddenCanvases.Add(c); }
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.enabled) { c.enabled = false; hiddenCameras.Add(c); }

            OwWorld.Site = save.site;
            world = OwWorld.Create(transform);
            farm = OwFarm.Create(transform, world, save);
            fx = new GameObject("OwFx").AddComponent<FxSystem>();
            fx.transform.SetParent(transform, false);
            ui = OwUi.Create();
            ui.transform.SetParent(transform, false);
            ui.ActionPressed += () => Act(false);
            ui.Action2Pressed += () => Act(true);
            ui.BackPressed += Leave;
            ui.CameraPressed += () => SetTps(!tps);
            ui.DefencePressed += ShowDefences;
            ui.ArmyPressed += ShowArmy;
            ui.Zoom += d => isoDist = Mathf.Clamp(isoDist + d, MinZoom, MaxZoom);
            foiMax = Mathf.Max(100, Challenge.FoiMaxHp);
            foiHp = foiMax;
            save.FeedSoldiers();
            StartCoroutine(OwNotify.Init());
            OwNotify.Clear();
            battle = OwBattle.Create(transform, save, farm, fx, () => pos, HurtFoi);
            battle.Ended += RaidEnded;

            var camGo = new GameObject("OwCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.98f, 0.72f, 0.55f);
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 260f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = cam.backgroundColor;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 130f;

            foi = Instantiate(foiVisual.gameObject, transform).transform;
            foi.name = "OwFoi";
            foi.localScale = foiVisual.lossyScale;
            foi.gameObject.SetActive(true);
            foreach (var mb in foi.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;

            pos = save.px > 0f ? new Vector2(save.px, save.pz) : OwWorld.Arrival;
            world.Around(pos);
            SetTps(save.tps);
            ui.SetMoney(save.money);
            ui.SetBarn(save.Stored, save.BarnSize);

            if (!save.converted)
            {
                var (gold, money) = save.ConvertFirst();
                ui.SetMoney(save.money);
                ui.Toast(Loc.F("ow.converted", gold, money));
            }
            else ui.Toast(Loc.T("ow.welcome"));

            // A raid that came while away is fought out by the farm alone; its report is shown now.
            if (save.raidAt > 0 && OwSave.Now >= save.raidAt) RaidWhileAway();
            save.PlanRaid();
        }

        private FxSystem fx;
        private OwBattle battle;
        private float foiHp, foiMax, strikeCd, raidT;
        private bool choosing;

        private void HurtFoi(float damage)
        {
            if (foiHp <= 0f) return;
            foiHp -= damage;
            if (foiHp > 0f) return;
            foiHp = 0f;
            ui.Toast(Loc.T("ow.raid.foiDown"));
            battle.FoiDown();
        }

        private void Leave()
        {
            save.px = pos.x;
            save.pz = pos.y;
            save.tps = tps;
            save.Save();
            OwSync.Upload();
            OwNotify.Schedule(save);
            Destroy(gameObject);
        }


        private bool quitting;

        /// <summary>Closing the app (or sending it away) keeps where foi stood.</summary>
        private void OnApplicationQuit() { quitting = true; KeepPlace(); OwNotify.Schedule(save); }
        private void OnApplicationPause(bool paused) { if (paused) { KeepPlace(); OwSync.Upload(); OwNotify.Schedule(save); } else OwNotify.Clear(); }

        private void KeepPlace()
        {
            if (save == null) return;
            save.px = pos.x; save.pz = pos.y; save.tps = tps;
            save.Save();
        }

        private void OnDestroy()
        {
            Active = false;
            OwIcons.Clear();
            RenderSettings.fog = false;
            foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
            foreach (var c in hiddenCameras) if (c != null) c.enabled = true;
            Screen.orientation = ScreenOrientation.Portrait;
            if (!quitting) left?.Invoke();
        }

        private void SetTps(bool on)
        {
            tps = on;
            ui.SetCameraLabel(on);
            Screen.orientation = on ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            if (on) yaw = facing;
        }

        // ---------- Each frame ----------

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { if (ui.ListOpen) ui.CloseList(); else Leave(); return; }

            // The camera turns with a drag on the right (TPS), or keys.
            var look = ui.TakeLook();
            if (tps)
            {
                yaw += look.x * 260f;
                pitch = Mathf.Clamp(pitch - look.y * 120f, 6f, 50f);
            }

            // Walking: the stick (or WASD), relative to the camera.
            var move = ui.ListOpen ? Vector2.zero : ui.Move;
            var k = Keyboard.current;
            if (k != null)
            {
                var km = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                                     (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
                if (km.sqrMagnitude > 0f) move = km.normalized;
                if (k.spaceKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame) Act(false);
            }
            float camYaw = tps ? yaw : 45f;
            if (move.sqrMagnitude > 0.01f)
            {
                var fwd = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(move.x, 0f, move.y);
                var step = new Vector2(fwd.x, fwd.z) * Speed * Mathf.Clamp01(move.magnitude * 1.3f) * dt;
                pos = Collide(pos + step);
                facing = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                bob += dt * 12f;
            }
            else bob = Mathf.MoveTowards(bob, Mathf.Round(bob / Mathf.PI) * Mathf.PI, dt * 6f);
            var p = OwWorld.World(pos);
            foi.position = p + Vector3.up * Mathf.Abs(Mathf.Sin(bob)) * 0.12f;
            foi.rotation = Quaternion.Slerp(foi.rotation, Quaternion.Euler(0f, facing, 0f), dt * 12f);

            streamT -= dt;
            if (streamT <= 0f)
            {
                streamT = 0.4f;
                world.Around(pos);
                blockers.Clear();
                blockers.AddRange(world.Blockers);
                farm.AddBlockers(blockers);
            }
            nearT -= dt;
            if (nearT <= 0f) { nearT = 0.15f; RefreshAction(); }
            strikeCd -= dt;
            raidT -= dt;
            syncT -= dt;
            if (syncT <= 0f) { syncT = 45f; OwSync.Upload(); }
            if (raidT <= 0f) { raidT = 1f; RaidClock(); }
            ui.SetFarmButtons(!battle.Raiding && (pos - OwWorld.FarmCentre).magnitude < OwBattleData.Reach(save.House.land) + 6f);
            ui.SetZoomButtons(!tps);
            if (!tps) PinchAndWheel();
            PlaceCamera(p, dt);
        }

        private const float MinZoom = 7f, MaxZoom = 42f;
        private float isoDist = 13f, pinchLast = -1f;
        private float syncT = 45f;

        /// <summary>Two fingers apart or together (when not walking), or the mouse wheel: the isometric zoom.</summary>
        private void PinchAndWheel()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float s = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(s) > 0.01f) isoDist = Mathf.Clamp(isoDist - Mathf.Sign(s) * 1.5f, MinZoom, MaxZoom);
            }
            var touch = Touchscreen.current;
            if (touch == null || ui.Move.sqrMagnitude > 0.001f) { pinchLast = -1f; return; }
            int down = 0;
            Vector2 a = Vector2.zero, b = Vector2.zero;
            foreach (var t in touch.touches)
            {
                if (!t.press.isPressed) continue;
                if (down == 0) a = t.position.ReadValue(); else if (down == 1) b = t.position.ReadValue();
                down++;
            }
            if (down < 2) { pinchLast = -1f; return; }
            float gap = (a - b).magnitude / Mathf.Max(1f, Screen.height);
            if (pinchLast > 0f) isoDist = Mathf.Clamp(isoDist - (gap - pinchLast) * 40f, MinZoom, MaxZoom);
            pinchLast = gap;
        }

        // ---------- Raids ----------

        /// <summary>Once a second: the warning before a raid, the raid when its time comes, the bar during one.</summary>
        private void RaidClock()
        {
            if (battle.Raiding)
            {
                int t = Mathf.CeilToInt(battle.TimeLeft);
                ui.SetHint(Loc.F("ow.raid.bar", battle.Left, t / 60, (t % 60).ToString("00"), Mathf.CeilToInt(foiHp), Mathf.CeilToInt(foiMax)), true);
                return;
            }
            long now = OwSave.Now;
            if (save.raidAt <= 0) return;
            long left = save.raidAt - now;
            if (left <= 0 && !choosing) { RaidChoice(); return; }
            if (left > 0 && left < save.WarningHours * 3600 && !ui.ListOpen) ui.SetHint(Loc.F("ow.raid.warning", Dur(left)), true);
        }

        /// <summary>The bandits are here: stay and fight, or sell everything at half its worth and move to a safe spot.</summary>
        private void RaidChoice()
        {
            choosing = true;
            long half = save.FarmValue() / 2;
            var rows = new List<OwRow>
            {
                new OwRow { text = Loc.T("ow.raid.fight"), sub = Loc.F("ow.raid.fightLine", battle.RaidSize), icon = BanditIcon(), button = Loc.T("ow.raid.fightButton"),
                    onClick = () => { choosing = false; ui.CloseList(); foiHp = foiMax; battle.StartRaid(); } },
                new OwRow { text = Loc.T("ow.raid.move"), sub = Loc.F("ow.raid.moveLine", half), icon = farm.HouseIcon(save.house), button = Loc.T("ow.raid.moveButton"),
                    onClick = () => { choosing = false; ui.CloseList(); SellAndMove(); } },
            };
            ui.ShowList(Loc.T("ow.raid.title"), rows);
            AudioManager.PlaySfx(Sfx.Warning, 1f, 0.9f);
        }

        private void SellAndMove()
        {
            long got = save.SellAndMove();
            OwWorld.Site = save.site;
            world.Reload();
            farm.Rebuild();
            battle.Garrison();
            pos = OwWorld.FarmCentre + new Vector2(0f, -save.House.land * 0.5f - 4f);
            streamT = 0f;
            Changed(false);
            ui.Toast(Loc.F("ow.raid.moved", got));
        }

        private void RaidEnded(bool won)
        {
            string report;
            if (won)
            {
                save.money += battle.Reward;
                report = Loc.F("ow.raid.won", battle.Kills, battle.Reward);
                AudioManager.PlaySfx(Sfx.Win, 1f, 1f);
            }
            else
            {
                var (items, crops) = save.Loot(new System.Random());
                report = Loc.F("ow.raid.lost", battle.Kills, items + battle.Stolen, crops);
                AudioManager.PlaySfx(Sfx.Warning, 0.8f, 0.7f);
            }
            save.RaidDone();
            foiHp = foiMax;
            battle.Garrison();
            Changed(true);
            ShowReport(report);
        }

        private void RaidWhileAway()
        {
            var (won, kills, count) = OwBattle.Simulate(save, (int)(save.raidAt % 100000));
            string report;
            if (won)
            {
                long reward = kills * 15;
                save.money += reward;
                report = Loc.F("ow.raid.awayWon", count, reward);
            }
            else
            {
                var (items, crops) = save.Loot(new System.Random((int)(save.raidAt % 9999)));
                int fallen = 0;
                for (int g = 0; g < save.soldiers.Length; g++) { int k = save.soldiers[g] / 3; save.soldiers[g] -= k; fallen += k; }
                report = Loc.F("ow.raid.awayLost", count, kills, items, crops, fallen);
            }
            save.RaidDone();
            Changed(true);
            battle.Garrison();
            ShowReport(report);
        }

        private void ShowReport(string report)
        {
            ui.ShowList(Loc.T("ow.raid.report"), new List<OwRow> { new OwRow { text = report, button = Loc.T("ow.ok"), onClick = ui.CloseList, tall = true } });
        }

        // ---------- Defences and the army ----------

        private void ShowDefences()
        {
            var rows = new List<OwRow>();
            var dir = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
            var spot = pos + new Vector2(dir.x, dir.z) * 2.6f;
            foreach (var d in OwBattleData.Defences)
            {
                var def = d;
                bool open = def.house <= save.house;
                bool fits = farm.DefenceFits(spot, def.kind);
                rows.Add(new OwRow
                {
                    text = Loc.T("ow.def." + def.kind),
                    sub = !open ? Loc.F("ow.needsHouse", Loc.T("ow.house." + def.house)) : !fits ? Loc.T("ow.def.noRoom") : Loc.T("ow.def.line." + def.kind),
                    icon = farm.IconOf(def.kind),
                    button = def.price.ToString(),
                    enabled = open && fits,
                    onClick = () =>
                    {
                        if (!save.Spend(def.price)) { NoMoney(); return; }
                        float rot = Mathf.Round(facing / 45f) * 45f + (def.blocks ? 0f : 180f);
                        save.defences.Add(new DefenceState { kind = (int)def.kind, x = spot.x, z = spot.y, rot = rot, level = 1, hp = def.hp });
                        ui.CloseList();
                        AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1f);
                        Changed(true);
                    },
                });
            }
            ui.ShowList(Loc.T("ow.defence"), rows);
        }

        private void ShowArmy()
        {
            var rows = new List<OwRow>();
            int cap = save.SoldierCap;
            for (int g = 0; g < Army.Groups; g++)
            {
                int group = g;
                var kind = Army.KindOf(g);
                var st = Army.StatsOf(g);
                bool room = save.soldiers[g] < cap;
                rows.Add(new OwRow
                {
                    text = Loc.T("army.kind." + kind) + "  ×" + save.soldiers[g],
                    sub = Loc.F("ow.army.line", st.hp, st.hit.ToString("0.#"), cap),
                    icon = SoldierIcon(group),
                    button = "+1 (" + OwData.SoldierPrice + ")",
                    enabled = room,
                    onClick = () =>
                    {
                        if (save.soldiers[group] >= cap) return;
                        if (!save.Spend(OwData.SoldierPrice)) { NoMoney(); return; }
                        save.soldiers[group]++;
                        AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1.2f);
                        Changed(false);
                        battle.Garrison();
                        ShowArmy();
                    },
                });
            }
            if (save.hungry) rows.Insert(0, new OwRow { text = Loc.T("ow.army.hungry"), sub = Loc.T("ow.army.hungryLine"), icon = farm.IconOf(Item.Wheat) });
            else rows.Insert(0, new OwRow { text = Loc.F("ow.army.food", save.SoldierCount), sub = Loc.T("ow.army.foodLine"), icon = farm.IconOf(Item.Milk) });
            ui.ShowList(Loc.T("ow.army"), rows);
        }

        private static Texture SoldierIcon(int group) => OwIcons.Get("soldier" + group, t =>
        {
            var kind = Army.KindOf(group);
            SoldierModel.Build(t, kind, Army.Heavy(kind) ? new Color(0.25f, 0.5f, 1f) : new Color(0.45f, 0.65f, 1f));
        });

        private static Texture BanditIcon() => OwIcons.Get("bandit", t => HumanoidBot.Build(t, new Color(0.85f, 0.5f, 0.2f)));

        private int MaxHp(DefenceState d) => Mathf.RoundToInt(OwBattleData.Of((DefenceKind)d.kind).hp * OwBattleData.LevelScale(d.level));

        private int RepairPrice(DefenceState d) => Mathf.CeilToInt(OwBattleData.Of((DefenceKind)d.kind).price * d.level * 0.5f * (1f - d.hp / (float)MaxHp(d)));

        private Vector2 Collide(Vector2 to)
        {
            to.x = Mathf.Clamp(to.x, 2f, OwWorld.Size - 2f);
            to.y = Mathf.Clamp(to.y, 2f, OwWorld.Size - 2f);
            foreach (var b in blockers)
            {
                var c = new Vector2(b.x, b.y);
                float r = b.z + 0.45f;
                var d = to - c;
                if (d.sqrMagnitude < r * r && d.sqrMagnitude > 0.0001f) to = c + d.normalized * r;
            }
            return to;
        }

        private void PlaceCamera(Vector3 p, float dt)
        {
            Vector3 want; Vector3 lookAt;
            if (tps)
            {
                var dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
                lookAt = p + Vector3.up * 1f;
                want = lookAt - dir * 6f + Quaternion.Euler(0f, yaw, 0f) * Vector3.right * 0.6f;
                float ground = OwWorld.Height(want.x - OwWorld.Origin.x, want.z - OwWorld.Origin.z) + OwWorld.Origin.y + 0.6f;
                if (want.y < ground) want.y = ground;
                cam.fieldOfView = 58f;
            }
            else
            {
                lookAt = p + Vector3.up * 0.6f;
                // Further out the view climbs: from a low three-quarter look to nearly straight down.
                float k = Mathf.InverseLerp(MinZoom, MaxZoom, isoDist);
                float a = Mathf.Lerp(46f, 70f, k) * Mathf.Deg2Rad;
                want = lookAt + Quaternion.Euler(0f, 45f, 0f) * new Vector3(0f, 0f, -Mathf.Cos(a) * isoDist) + Vector3.up * Mathf.Sin(a) * isoDist;
                RenderSettings.fogStartDistance = 70f + isoDist * 2f;
                RenderSettings.fogEndDistance = 130f + isoDist * 3f;
                cam.fieldOfView = CameraFit.Fov(46f, cam.aspect);
            }
            cam.transform.position = Vector3.Lerp(cam.transform.position, want, 1f - Mathf.Exp(-dt * 10f));
            if ((cam.transform.position - want).sqrMagnitude > 400f) cam.transform.position = want;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - cam.transform.position);
        }

        // ---------- Acting ----------

        private static string Dur(long secs)
        {
            if (secs >= 3600) return Loc.F("ow.time.h", secs / 3600, secs % 3600 / 60);
            if (secs >= 60) return Loc.F("ow.time.m", secs / 60, secs % 60);
            return Loc.F("ow.time.s", secs);
        }

        private static string Name(Item i) => Loc.T("ow.item." + i);

        private void RefreshAction()
        {
            if (ui.ListOpen) return;
            target = farm.Nearest(pos);
            string main = null, second = null;
            bool enabled = true;
            switch (target.kind)
            {
                case TargetKind.Plot:
                {
                    var pl = save.plots[target.index];
                    float g = farm.Growth(pl);
                    if (g < 0f) main = Loc.T("ow.act.sow");
                    else if (g >= 1f) main = Loc.F("ow.act.harvest", Name(OwData.Of((Crop)pl.crop).item));
                    else { main = Name(OwData.Of((Crop)pl.crop).item) + " · " + Dur(OwData.Of((Crop)pl.crop).seconds - (OwSave.Now - pl.planted)); enabled = false; }
                    break;
                }
                case TargetKind.Tree:
                {
                    var t = save.trees[target.index];
                    var def = OwData.Of((TreeKind)t.kind);
                    if (farm.FruitReady(t)) main = Loc.F("ow.act.pick", Name(def.item));
                    else { main = Name(def.item) + " · " + Dur(farm.FruitIn(t)); enabled = false; }
                    break;
                }
                case TargetKind.Building:
                {
                    var b = save.buildings[target.index];
                    var def = OwData.Of((BuildingKind)b.kind);
                    bool hive = def.kind == BuildingKind.Hive;
                    if (farm.ProduceReady(b)) main = Loc.F("ow.act.collect", Name(def.product));
                    else if (!hive && b.animals <= 0) { main = Loc.T("ow.b." + def.kind); enabled = false; }
                    else if (!hive && b.fed <= 0) main = Loc.F("ow.act.feed", b.animals * def.feedPer, Name(def.feed));
                    else { main = Name(def.product) + " · " + Dur(b.fed + def.seconds - OwSave.Now); enabled = false; }
                    if (!hive && b.animals < def.capacity) second = Loc.F("ow.act.buyAnimal", Loc.T("ow.animal." + def.kind), def.animalPrice);
                    break;
                }
                case TargetKind.House:
                    if (save.house < OwData.Houses.Length - 1) main = Loc.F("ow.act.upgradeHouse", OwData.Houses[save.house + 1].price);
                    else { main = Loc.T("ow.house." + save.house); enabled = false; }
                    break;
                case TargetKind.Barn: main = Loc.T("ow.act.barn"); break;
                case TargetKind.Coop: main = Loc.T("ow.act.coop"); break;
                case TargetKind.Empty: main = Loc.T("ow.act.build"); break;
                case TargetKind.Defence:
                {
                    var d = save.defences[target.index];
                    var def = OwBattleData.Of((DefenceKind)d.kind);
                    if (d.hp < MaxHp(d)) main = Loc.F("ow.act.repair", RepairPrice(d));
                    else if (d.level < OwBattleData.MaxLevel) main = Loc.F("ow.act.upgradeDef", OwBattleData.UpgradePrice(def, d.level), d.level + 1);
                    else { main = Loc.T("ow.def." + def.kind) + " · " + Loc.T("shop.max"); enabled = false; }
                    second = Loc.F("ow.act.remove", def.price * d.level / 2);
                    break;
                }
            }
            // In a raid the big button strikes when a bandit is near.
            if (battle != null && battle.Raiding && battle.BanditNear(pos, 3.2f)) { main = Loc.T("ow.act.strike"); enabled = true; second = null; }
            ui.SetAction(main, main != null, enabled);
            ui.SetAction2(second);
            if (battle == null || !battle.Raiding) ui.SetHint(target.kind == TargetKind.None && Vector2.Distance(pos, OwWorld.FarmCentre) > 30f ? Loc.T("ow.hint.farm") : null);
        }

        private void Act(bool second)
        {
            if (ui.ListOpen) return;
            if (!second && battle.Raiding && battle.BanditNear(pos, 3.2f))
            {
                if (strikeCd > 0f) return;
                var w = Armory.Equipped;
                strikeCd = Armory.Cooldown(w) * 2f;
                var dir = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
                int hits = battle.Strike(pos, new Vector2(dir.x, dir.z), Armory.Damage(w) * 1.5f);
                SlashFx.Create(foi.position, foi.position + dir * 2f, WeaponModels.GlowOf(w), 1.2f);
                AudioManager.PlaySfx(Sfx.Impact, hits > 0 ? 0.9f : 0.4f, 1f);
                return;
            }
            switch (target.kind)
            {
                case TargetKind.Defence:
                {
                    var d = save.defences[target.index];
                    var def = OwBattleData.Of((DefenceKind)d.kind);
                    if (second)
                    {
                        save.money += def.price * d.level / 2;
                        save.defences.RemoveAt(target.index);
                        Changed(true);
                        break;
                    }
                    if (d.hp < MaxHp(d))
                    {
                        if (!save.Spend(RepairPrice(d))) { NoMoney(); break; }
                        d.hp = MaxHp(d);
                        Changed(true);
                    }
                    else if (d.level < OwBattleData.MaxLevel)
                    {
                        if (!save.Spend(OwBattleData.UpgradePrice(def, d.level))) { NoMoney(); break; }
                        d.level++;
                        d.hp = MaxHp(d);
                        AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.2f);
                        Changed(true);
                    }
                    break;
                }
                case TargetKind.Plot:
                {
                    var pl = save.plots[target.index];
                    float g = farm.Growth(pl);
                    if (g < 0f) ShowSow(pl);
                    else if (g >= 1f) Harvest(pl);
                    break;
                }
                case TargetKind.Tree:
                {
                    var t = save.trees[target.index];
                    if (!farm.FruitReady(t)) break;
                    var def = OwData.Of((TreeKind)t.kind);
                    if (Store(def.item, def.yield)) { t.picked = OwSave.Now; Changed(false); }
                    break;
                }
                case TargetKind.Building:
                {
                    var b = save.buildings[target.index];
                    var def = OwData.Of((BuildingKind)b.kind);
                    bool hive = def.kind == BuildingKind.Hive;
                    if (second)
                    {
                        if (hive || b.animals >= def.capacity) break;
                        if (!save.Spend(def.animalPrice)) { NoMoney(); break; }
                        b.animals++;
                        AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1.2f);
                        Changed(true);
                        break;
                    }
                    if (farm.ProduceReady(b))
                    {
                        int n = hive ? 2 : b.animals;
                        if (Store(def.product, n)) { b.fed = hive ? OwSave.Now : 0; Changed(false); }
                    }
                    else if (!hive && b.animals > 0 && b.fed <= 0)
                    {
                        int need = b.animals * def.feedPer;
                        if (!save.Take(def.feed, need)) { ui.Toast(Loc.F("ow.noFeed", need, Name(def.feed))); AudioManager.PlaySfx(Sfx.Bump, 0.6f); break; }
                        b.fed = OwSave.Now;
                        AudioManager.PlaySfx(Sfx.Hop, 0.7f, 1.3f);
                        Changed(false);
                    }
                    break;
                }
                case TargetKind.House:
                    if (save.house >= OwData.Houses.Length - 1) break;
                    if (!save.Spend(OwData.Houses[save.house + 1].price)) { NoMoney(); break; }
                    save.house++;
                    AudioManager.PlaySfx(Sfx.Win, 0.9f, 1f);
                    ui.Toast(Loc.F("ow.houseUp", Loc.T("ow.house." + save.house)));
                    save.RefreshDay();
                    Changed(true);
                    break;
                case TargetKind.Barn: ShowBarn(); break;
                case TargetKind.Coop: ShowCoop(); break;
                case TargetKind.Empty: ShowBuild(target.cell); break;
            }
        }

        private void Changed(bool rebuild)
        {
            save.Save();
            ui.SetMoney(save.money);
            ui.SetBarn(save.Stored, save.BarnSize);
            if (rebuild) { farm.Rebuild(); streamT = 0f; }
            nearT = 0f;
        }

        private void NoMoney()
        {
            ui.Toast(Loc.T("ow.noMoney"));
            AudioManager.PlaySfx(Sfx.Bump, 0.6f);
        }

        private bool Store(Item i, int n)
        {
            if (save.Room < n) { ui.Toast(Loc.T("ow.barnFull")); AudioManager.PlaySfx(Sfx.Bump, 0.6f); return false; }
            save.Add(i, n);
            ui.Toast("+" + n + " " + Name(i));
            AudioManager.PlaySfx(Sfx.Coin, 0.7f, 1.4f);
            return true;
        }

        private void Harvest(PlotState pl)
        {
            var def = OwData.Of((Crop)pl.crop);
            if (!Store(def.item, def.yield)) return;
            pl.crop = -1;
            Changed(false);
        }

        // ---------- Lists ----------

        private void ShowSow(PlotState pl)
        {
            var rows = new List<OwRow>();
            foreach (var c in OwData.Crops)
            {
                var def = c;
                bool open = def.house <= save.house;
                rows.Add(new OwRow
                {
                    text = Name(def.item),
                    sub = open ? Loc.F("ow.sow.line", Dur(def.seconds), def.yield, def.price) : Loc.F("ow.needsHouse", Loc.T("ow.house." + def.house)),
                    icon = farm.IconOf(def.item),
                    button = open ? Loc.F("ow.sow.button", def.seed) : "—",
                    enabled = open,
                    onClick = () =>
                    {
                        if (!save.Spend(def.seed)) { NoMoney(); return; }
                        pl.crop = (int)def.crop;
                        pl.planted = OwSave.Now;
                        ui.CloseList();
                        AudioManager.PlaySfx(Sfx.Hop, 0.7f, 1.4f);
                        Changed(false);
                    },
                });
            }
            ui.ShowList(Loc.T("ow.sow.title"), rows);
        }

        private void ShowBuild(Vector2Int cell)
        {
            var h = save.House;
            var rows = new List<OwRow>();
            const int plotPrice = 10;
            rows.Add(new OwRow
            {
                text = Loc.T("ow.build.plot"),
                sub = Loc.F("ow.build.count", save.plots.Count, h.plots),
                icon = farm.PlotIcon(),
                button = plotPrice.ToString(),
                enabled = save.plots.Count < h.plots,
                onClick = () =>
                {
                    if (!save.Spend(plotPrice)) { NoMoney(); return; }
                    save.plots.Add(new PlotState { x = cell.x, z = cell.y });
                    ui.CloseList();
                    Changed(true);
                },
            });
            foreach (var t in OwData.Trees)
            {
                var def = t;
                bool open = def.house <= save.house && save.trees.Count < h.trees;
                rows.Add(new OwRow
                {
                    text = Loc.T("ow.tree." + def.kind),
                    sub = def.house > save.house ? Loc.F("ow.needsHouse", Loc.T("ow.house." + def.house)) : Loc.F("ow.tree.line", Dur(def.growSeconds), def.yield, Name(def.item), Dur(def.fruitSeconds)),
                    icon = farm.IconOf(def.kind),
                    button = def.sapling.ToString(),
                    enabled = open,
                    onClick = () =>
                    {
                        if (!save.Spend(def.sapling)) { NoMoney(); return; }
                        save.trees.Add(new TreeState { x = cell.x, z = cell.y, kind = (int)def.kind, planted = OwSave.Now });
                        ui.CloseList();
                        Changed(true);
                    },
                });
            }
            bool room = farm.FreeForBuilding(cell);
            foreach (var b in OwData.Buildings)
            {
                var def = b;
                bool open = def.house <= save.house && save.buildings.Count < h.buildings && room;
                rows.Add(new OwRow
                {
                    text = Loc.T("ow.b." + def.kind),
                    sub = def.house > save.house ? Loc.F("ow.needsHouse", Loc.T("ow.house." + def.house)) : !room ? Loc.T("ow.build.noRoom") : Loc.F("ow.b.line", Name(def.product), Dur(def.seconds)),
                    icon = farm.IconOf(def.kind),
                    button = def.price.ToString(),
                    enabled = open,
                    onClick = () =>
                    {
                        if (!save.Spend(def.price)) { NoMoney(); return; }
                        save.buildings.Add(new BuildingState { x = cell.x, z = cell.y, kind = (int)def.kind, fed = def.kind == BuildingKind.Hive ? OwSave.Now : 0 });
                        ui.CloseList();
                        Changed(true);
                    },
                });
            }
            ui.ShowList(Loc.T("ow.build.title"), rows);
        }

        private void ShowBarn()
        {
            var rows = new List<OwRow>();
            if (save.barn < OwData.BarnSizes.Length - 1)
            {
                int price = OwData.BarnPrices[save.barn + 1];
                rows.Add(new OwRow
                {
                    text = Loc.F("ow.barn.upgrade", OwData.BarnSizes[save.barn + 1]),
                    sub = Loc.F("ow.barn.now", save.Stored, save.BarnSize),
                    icon = farm.BarnIcon(),
                    button = price.ToString(),
                    enabled = save.money >= price,
                    onClick = () => { if (!save.Spend(price)) { NoMoney(); return; } save.barn++; Changed(true); ShowBarn(); },
                });
            }
            if (SaveData.TestMode)
                rows.Add(new OwRow { text = Loc.T("ow.test.raid"), swatch = new Color(1f, 0.3f, 0.3f), button = Loc.T("ow.raid.fightButton"),
                    onClick = () => { save.raidAt = OwSave.Now; ui.CloseList(); raidT = 0f; } });
            int coins = SaveData.Coins;
            rows.Add(new OwRow
            {
                text = Loc.F("ow.carry.title", coins),
                sub = Loc.F("ow.carry.line", OwData.LaterRate),
                swatch = new Color(1f, 0.8f, 0.25f),
                button = "+" + coins / OwData.LaterRate,
                enabled = coins >= OwData.LaterRate,
                onClick = () => { var (g, m) = save.CarryGold(); ui.Toast(Loc.F("ow.carried", g, m)); Changed(false); ShowBarn(); },
            });
            foreach (Item i in Enum.GetValues(typeof(Item)))
            {
                int n = save.Count(i);
                if (n <= 0) continue;
                rows.Add(new OwRow { text = Name(i) + "  ×" + n, sub = Loc.F("ow.barn.worth", save.PriceToday(i)), icon = farm.IconOf(i) });
            }
            ui.ShowList(Loc.T("ow.barn.title"), rows);
        }

        private void ShowCoop()
        {
            save.RefreshDay();
            var rows = new List<OwRow>();
            foreach (var o in save.orders)
            {
                var order = o;
                string want = order.count + " " + Name((Item)order.item) + (order.count2 > 0 ? " + " + order.count2 + " " + Name((Item)order.item2) : "");
                rows.Add(new OwRow
                {
                    text = want,
                    sub = order.done ? Loc.T("ow.order.done") : Loc.F("ow.order.reward", order.reward),
                    icon = farm.IconOf((Item)order.item),
                    button = order.done ? "✓" : Loc.T("ow.order.give"),
                    enabled = save.CanFill(order),
                    onClick = () => { if (save.Fill(order)) { AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.2f); ui.Toast("+" + order.reward); Changed(false); ShowCoop(); } },
                });
            }
            foreach (Item i in Enum.GetValues(typeof(Item)))
            {
                var item = i;
                int n = save.Count(item);
                if (n <= 0) continue;
                long all = 0;
                { int sold = save.soldToday[(int)item]; for (int k = 0; k < n; k++) all += Mathf.Max(1, Mathf.RoundToInt(OwData.Price(item) * (1f - Mathf.Clamp01((sold + k) / 200f) * 0.2f))); }
                rows.Add(new OwRow
                {
                    text = Name(item) + "  ×" + n,
                    sub = Loc.F("ow.coop.price", save.PriceToday(item)),
                    icon = farm.IconOf(item),
                    button = Loc.F("ow.coop.sellAll", all),
                    onClick = () => { long made = save.Sell(item, save.Count(item)); AudioManager.PlaySfx(Sfx.Coin, 1f, 1.1f); ui.Toast("+" + made); Changed(false); ShowCoop(); },
                });
            }
            if (rows.Count == save.orders.Count) rows.Add(new OwRow { text = Loc.T("ow.coop.empty"), sub = Loc.T("ow.coop.emptyLine") });
            ui.ShowList(Loc.T("ow.coop.title"), rows);
        }
    }
}
