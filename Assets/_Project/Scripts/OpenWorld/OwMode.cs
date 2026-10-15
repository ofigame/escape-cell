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

            world = OwWorld.Create(transform);
            farm = OwFarm.Create(transform, world, save);
            ui = OwUi.Create();
            ui.transform.SetParent(transform, false);
            ui.ActionPressed += () => Act(false);
            ui.Action2Pressed += () => Act(true);
            ui.BackPressed += Leave;
            ui.CameraPressed += () => SetTps(!tps);

            var camGo = new GameObject("OwCamera");
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
        }

        private void Leave()
        {
            save.px = pos.x;
            save.pz = pos.y;
            save.tps = tps;
            save.Save();
            Destroy(gameObject);
        }


        private void OnDestroy()
        {
            Active = false;
            RenderSettings.fog = false;
            foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
            foreach (var c in hiddenCameras) if (c != null) c.enabled = true;
            Screen.orientation = ScreenOrientation.Portrait;
            left?.Invoke();
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
            PlaceCamera(p, dt);
        }

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
                want = lookAt + Quaternion.Euler(0f, 45f, 0f) * new Vector3(0f, 0f, -1f) * 9f + Vector3.up * 9.5f;
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
            }
            ui.SetAction(main, main != null, enabled);
            ui.SetAction2(second);
            ui.SetHint(target.kind == TargetKind.None && Vector2.Distance(pos, OwWorld.FarmCentre) > 30f ? Loc.T("ow.hint.farm") : null);
        }

        private void Act(bool second)
        {
            if (ui.ListOpen) return;
            switch (target.kind)
            {
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
                    swatch = def.item == Item.Wheat ? OwData.ColourOf(def.item) : OwData.ColourOf(def.item),
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
                swatch = new Color(0.46f, 0.3f, 0.18f),
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
                    swatch = def.colour,
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
                    swatch = def.colour,
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
                    swatch = new Color(0.7f, 0.18f, 0.14f),
                    button = price.ToString(),
                    enabled = save.money >= price,
                    onClick = () => { if (!save.Spend(price)) { NoMoney(); return; } save.barn++; Changed(true); ShowBarn(); },
                });
            }
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
                rows.Add(new OwRow { text = Name(i) + "  ×" + n, sub = Loc.F("ow.barn.worth", save.PriceToday(i)), swatch = OwData.ColourOf(i) });
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
                    swatch = new Color(1f, 0.78f, 0.3f),
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
                    swatch = OwData.ColourOf(item),
                    button = Loc.F("ow.coop.sellAll", all),
                    onClick = () => { long made = save.Sell(item, save.Count(item)); AudioManager.PlaySfx(Sfx.Coin, 1f, 1.1f); ui.Toast("+" + made); Changed(false); ShowCoop(); },
                });
            }
            if (rows.Count == save.orders.Count) rows.Add(new OwRow { text = Loc.T("ow.coop.empty"), sub = Loc.T("ow.coop.emptyLine") });
            ui.ShowList(Loc.T("ow.coop.title"), rows);
        }
    }
}
