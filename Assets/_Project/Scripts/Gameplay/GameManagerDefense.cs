using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The player's defences on the game's side: setting them up before a level (the fortress from above, stage by
    /// stage; tap a glowing spot to place the picked defence, tap one to take it back), raising the saved ones at the
    /// start of every try, the extra robots archers bring on the early floors, half coins for what they fell, and
    /// using them up when the level is won. The first archers are a gift, taught on level 1.
    /// </summary>
    public partial class GameManager
    {
        private DefenseSystem defenses;
        private bool defenseEditing, defenseEditPending, defenseStrike;
        private int editStage;
        private DefenseKind editKind = DefenseKind.Archer;
        private int[] defenseRects;
        private List<DefenseSlots.Slot> defenseSlots = new List<DefenseSlots.Slot>();
        private readonly List<GameObject> defenseMarkers = new List<GameObject>();
        private Material markerMat;
        /// <summary>The set-up view, free like a farm game's: where it looks and how far back it stands.</summary>
        private Vector3 editFocus;
        private float editDist = 20f, pinchPrev = -1f;

        private void InitDefense()
        {
            defenses = new GameObject("Defenses").AddComponent<DefenseSystem>();
            defenses.Init(hunt, fx);
            defenses.Strike = DefenseStrike;
            defenses.RobotStage = () => hunt.RobotStage;
            defenses.Running = () => State == GameState.Playing && !defenseEditing && !IntroRunning;
        }

        /// <summary>Hooks the set-up screen and the before-level card's button (again whenever the UI is rebuilt).</summary>
        private void WireDefenseUi()
        {
            ui.PrelevelDefense += (index, boosts) =>
            {
                ui.HidePrelevel();
                defenseEditPending = true;
                StartLevel(index, boosts);
            };
            var e = ui.DefenseEditor;
            e.KindSelected += k => { editKind = k; RefreshEditor(); };
            e.StageStep += d => FocusStage(Mathf.Clamp(editStage + d, 0, defenseRects.Length / 4 - 1));
            e.Dragged += PanEditor;
            e.StartPressed += CloseDefenseEditor;
            e.BackPressed += () => { defenseEditing = false; ClearMarkers(); e.Hide(); ui.SetHudHidden(false); ShowMap(); };
            e.Tapped += OnEditorTap;
        }

        /// <summary>A defence's blow: like any strike, but what it fells brings half the coins.</summary>
        private void DefenseStrike(GridPos p, int damage)
        {
            if (State != GameState.Playing || !grid.InBounds(p)) return;
            if (hunt.MonsterUp && p == hunt.MonsterTile) return; // vanG is foi's fight
            defenseStrike = true;
            hunt.Strike(p, damage);
            defenseStrike = false;
        }

        /// <summary>The fortress's stages (x0, y0, x1, y1 each); level 1's single floor is boxed from its tiles.</summary>
        private int[] FortressRects()
        {
            if (level.stageBounds != null && level.stageBounds.Length >= 4) return level.stageBounds;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsFloor(p) || grid.IsLava(p) || grid.IsCage(p) || grid.IsBridge(p)) continue;
                x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
            }
            return x0 > x1 ? new[] { 0, 0, grid.Width - 1, grid.Height - 1 } : new[] { x0, y0, x1, y1 };
        }

        /// <summary>At the start of a try: the saved defences rise in their spots (and set-up opens if asked for).</summary>
        private void SetupDefenses()
        {
            defenses.Clear();
            ClearMarkers();
            defenseEditing = false;
            if (level == null || level.mission != MissionType.Hunt || bonusRun) { defenseEditPending = false; return; }
            defenseRects = FortressRects();
            defenseSlots = DefenseSlots.Fixed(grid, defenseRects);
            var start = grid.StartSpot ?? grid.CenterFloor();
            var placed = Defenses.Placed(levelIndex);
            var kept = new List<Defenses.Placement>();
            foreach (var pl in placed)
            {
                if (!DefenseSlots.Resolve(grid, defenseRects, defenseSlots, start, pl, out var slot) || grid.IsOccupied(slot.tile) && pl.kind == DefenseKind.Tower) continue;
                defenses.Spawn(grid, pl, slot, Defenses.Tier(pl.kind, levelIndex));
                kept.Add(pl);
            }
            if (kept.Count != placed.Count) Defenses.Save(levelIndex, kept);
            // Level 1 the first time: three archers as a gift, and set-up opens to show how.
            bool gift = levelIndex == 0 && Defenses.GrantStarterOnce();
            if (defenseEditPending || gift) OpenDefenseEditor(gift);
            else AddDefenseRobots(kept);
            defenseEditPending = false;
        }

        /// <summary>The robots defences bring along on the early floors (three per archer), to their stages.</summary>
        private void AddDefenseRobots(List<Defenses.Placement> placed)
        {
            foreach (var pl in placed)
            {
                if (!DefenseSlots.Resolve(grid, defenseRects, defenseSlots, grid.StartSpot ?? grid.CenterFloor(), pl, out var slot)) continue;
                hunt.AddRobots(slot.stage, Defenses.ExtraEnemies(pl.kind, levelIndex));
            }
        }

        // ---------- Set-up ----------

        private void OpenDefenseEditor(bool gift = false)
        {
            defenseEditing = true;
            editKind = DefenseKind.Archer;
            markerMat ??= MaterialFactory.CreateTransparent(new Color(0.4f, 0.95f, 1f, 0.45f), new Color(0.6f, 2.2f, 3f));
            ui.DefenseEditor.Show();
            FocusStage(0);
            if (gift) ui.DefenseEditor.SetHint(Loc.T("def.gift"));
        }

        private void CloseDefenseEditor()
        {
            if (!defenseEditing) return;
            defenseEditing = false;
            ClearMarkers();
            ui.DefenseEditor.Hide();
            ui.SetHudHidden(false);
            AddDefenseRobots(Defenses.Placed(levelIndex));
            introPending = true; // the opening shot, then the cell breaks
            AudioManager.PlaySfx(Sfx.Win, 0.5f, 1.4f);
        }

        private void RefreshEditor()
        {
            int stages = defenseRects.Length / 4;
            ui.DefenseEditor.SetStage(editStage, stages);
            foreach (var k in Defenses.All)
            {
                bool locked = !Defenses.Unlocked(k);
                string count = locked ? Loc.F("def.lockedAt", Defenses.UnlockAt(k) + 1) : Loc.F("def.ready", Defenses.Available(k));
                ui.DefenseEditor.SetKind(k, count, locked, k == editKind);
            }
            string hint = Loc.T("def.hint") + "\n" + Loc.T("def.move");
            if (levelIndex < 29) hint += "\n" + Loc.T("def.extra");
            ui.DefenseEditor.SetHint(hint);
            BuildMarkers();
        }

        /// <summary>Glowing rings on the free spots for the picked defence in the stage on show.</summary>
        private void BuildMarkers()
        {
            ClearMarkers();
            if (!Defenses.Unlocked(editKind)) return;
            if (editKind == DefenseKind.Tower)
            {
                var start = grid.StartSpot ?? grid.CenterFloor();
                for (int x = defenseRects[editStage * 4]; x <= defenseRects[editStage * 4 + 2]; x++)
                    for (int y = defenseRects[editStage * 4 + 1]; y <= defenseRects[editStage * 4 + 3]; y++)
                    {
                        var p = new GridPos(x, y);
                        if (!DefenseSlots.TowerOk(grid, defenseRects, p, start, out _) || grid.IsOccupied(p)) continue;
                        Marker(GridView.ToWorld(p) + Vector3.up * (GridView.SurfaceY + 0.06f), 0.55f);
                    }
                return;
            }
            foreach (var s in defenseSlots)
                if (s.kind == editKind && s.stage == editStage && !defenses.Has(s.Placement)) Marker(s.world + Vector3.up * 0.05f, s.kind == DefenseKind.Archer ? 0.9f : 0.75f);
        }

        private void Marker(Vector3 at, float size)
        {
            var m = Shapes.Primitive(PrimitiveType.Cylinder, "DefenseSpot", null, at, new Vector3(size, 0.02f, size), markerMat);
            m.AddComponent<Pulse3D>();
            defenseMarkers.Add(m);
        }

        private void ClearMarkers()
        {
            foreach (var m in defenseMarkers) if (m != null) Destroy(m);
            defenseMarkers.Clear();
        }

        /// <summary>A tap in set-up: on a defence, take it back; on a free spot of the picked kind, place one.</summary>
        private void OnEditorTap(Vector2 screen)
        {
            if (!defenseEditing) return;
            var cam = cameraRig.Cam;
            float reach = Mathf.Max(60f, Screen.height * 0.05f);
            var list = Defenses.Placed(levelIndex);
            // Taking one back: the placed defence nearest the finger.
            int hitIndex = -1;
            float best = reach;
            var start = grid.StartSpot ?? grid.CenterFloor();
            for (int i = 0; i < list.Count; i++)
            {
                if (!DefenseSlots.Resolve(grid, defenseRects, defenseSlots, start, list[i], out var s)) continue;
                var sp = cam.WorldToScreenPoint(s.world + Vector3.up * 0.4f);
                float d = Vector2.Distance(sp, screen);
                if (sp.z > 0f && d < best) { best = d; hitIndex = i; }
            }
            if (hitIndex >= 0)
            {
                defenses.Remove(list[hitIndex]);
                list.RemoveAt(hitIndex);
                Defenses.Save(levelIndex, list);
                AudioManager.PlaySfx(Sfx.Click, 0.8f, 0.8f);
                RefreshEditor();
                return;
            }
            if (!Defenses.Unlocked(editKind)) return;
            // Placing one: the free spot of the picked kind nearest the finger (a tower on the tapped tile).
            DefenseSlots.Slot slot = default;
            bool found = false;
            if (editKind == DefenseKind.Tower)
            {
                if (TryScreenToGrid(screen, out var cell) && DefenseSlots.TowerOk(grid, defenseRects, cell, start, out int st) && !grid.IsOccupied(cell))
                {
                    slot = new DefenseSlots.Slot { kind = DefenseKind.Tower, stage = st, tile = cell, world = GridView.ToWorld(cell) + Vector3.up * GridView.SurfaceY };
                    found = true;
                }
            }
            else
            {
                best = reach * 1.4f;
                foreach (var s in defenseSlots)
                {
                    if (s.kind != editKind || defenses.Has(s.Placement)) continue;
                    var sp = cam.WorldToScreenPoint(s.world);
                    float d = Vector2.Distance(sp, screen);
                    if (sp.z > 0f && d < best) { best = d; slot = s; found = true; }
                }
            }
            if (!found) return;
            if (Defenses.Available(editKind) <= 0) { Note(Loc.T("def.none")); return; }
            if (defenses.CountIn(slot.stage, editKind) >= Defenses.PerStage(editKind)) { Note(Loc.T("def.full")); return; }
            var pl = slot.Placement;
            list.Add(pl);
            Defenses.Save(levelIndex, list);
            defenses.Spawn(grid, pl, slot, Defenses.Tier(pl.kind, levelIndex));
            fx.Burst(slot.world + Vector3.up * 0.4f, new Color(0.5f, 0.9f, 1f), new Color(0.8f, 2f, 3f), 18, 3f);
            AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1.3f);
            RefreshEditor();
        }

        private void Note(string text)
        {
            AudioManager.PlaySfx(Sfx.Bump, 0.6f);
            ui.DefenseEditor.SetHint(text);
        }

        private static readonly Vector3 EditDir = new Vector3(-0.32f, 1f, -0.55f).normalized;

        /// <summary>The set-up view: from above and behind its focus, at its distance.</summary>
        private void EditorPose(out Vector3 pos, out Quaternion rot)
        {
            pos = editFocus + EditDir * editDist;
            rot = Quaternion.LookRotation(editFocus - pos);
        }

        /// <summary>Jumps the view to a stage, all of it in the frame.</summary>
        private void FocusStage(int s)
        {
            var a = new GridPos(defenseRects[s * 4], defenseRects[s * 4 + 1]);
            var b = new GridPos(defenseRects[s * 4 + 2], defenseRects[s * 4 + 3]);
            editFocus = (GridView.ToWorld(a) + GridView.ToWorld(b)) * 0.5f;
            float w = b.x - a.x + 3f, h = b.y - a.y + 3f;
            editDist = Mathf.Max(w * 1.25f, h) * 0.5f / Mathf.Tan(IntroFov * 0.5f * Mathf.Deg2Rad) + 4f;
            editStage = s;
            RefreshEditor();
        }

        /// <summary>A finger dragged over the scene: the view slides with it, over the whole floor.</summary>
        private void PanEditor(Vector2 delta)
        {
            if (!defenseEditing || TouchCount() >= 2) return;
            var cam = cameraRig.Cam;
            float perPixel = 2f * editDist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            var right = cam.transform.right; right.y = 0f; right.Normalize();
            var fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
            editFocus -= (right * delta.x + fwd * (delta.y / 0.8f)) * perPixel;
            editFocus.x = Mathf.Clamp(editFocus.x, -2f, grid.Width + 1f);
            editFocus.z = Mathf.Clamp(editFocus.z, -2f, grid.Height + 1f);
            int near = NearestStage(editFocus);
            if (near != editStage) { editStage = near; RefreshEditor(); }
        }

        /// <summary>Two fingers pinching (or the mouse wheel): the view comes closer or stands back.</summary>
        private void UpdateEditorZoom()
        {
            float maxDist = Mathf.Max(grid.Width, grid.Height) * 1.2f + 10f;
#if ENABLE_INPUT_SYSTEM
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null && ts.touches.Count >= 2 && ts.touches[0].press.isPressed && ts.touches[1].press.isPressed)
            {
                float d = Vector2.Distance(ts.touches[0].position.ReadValue(), ts.touches[1].position.ReadValue());
                if (pinchPrev > 0f && d > 1f) editDist = Mathf.Clamp(editDist * pinchPrev / d, 6f, maxDist);
                pinchPrev = d;
            }
            else pinchPrev = -1f;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) editDist = Mathf.Clamp(editDist * (wheel > 0f ? 0.9f : 1.1f), 6f, maxDist);
            }
#endif
        }

        private static int TouchCount()
        {
#if ENABLE_INPUT_SYSTEM
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts == null) return 0;
            int n = 0;
            foreach (var t in ts.touches) if (t.press.isPressed) n++;
            return n;
#else
            return Input.touchCount;
#endif
        }

        /// <summary>The stage nearest a point of the floor (by its rows).</summary>
        private int NearestStage(Vector3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int s = 0; s < defenseRects.Length / 4; s++)
            {
                float y0 = defenseRects[s * 4 + 1], y1 = defenseRects[s * 4 + 3];
                float d = p.z < y0 ? y0 - p.z : p.z > y1 ? p.z - y1 : 0f;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        private int archerAdLevel = -1;

        /// <summary>Back at a level just lost: a short ad for one archer (once per lost level).</summary>
        private void OfferArcherAd(int index)
        {
            if (index != levelIndex || archerAdLevel == index || !Monetization.Ads.Rewarded.IsReady || PlayerPrefs.GetInt(FailKey(index), 0) <= 0) return;
            archerAdLevel = index;
            ui.AdOffer.Show(Loc.T("def.adTitle"), Loc.T("def.adBody"), () =>
            {
                Monetization.Ads.Rewarded.Show(rewarded =>
                {
                    if (!rewarded) return;
                    Defenses.AddStock(DefenseKind.Archer, 1);
                    AudioManager.PlaySfx(Sfx.Coin, 1f, 1.3f);
                    ui.SetPrelevelDefense(PrelevelDefenseLabel(index));
                });
            });
        }

        /// <summary>The DEFENCES button on the before-level card says how many stand ready in that level.</summary>
        private string PrelevelDefenseLabel(int index)
        {
            int n = Defenses.Placed(index).Count;
            return n > 0 ? Loc.F("def.buttonPlaced", n) : Loc.T("def.button");
        }
    }

    /// <summary>A gentle breathing scale for 3D markers.</summary>
    public class Pulse3D : MonoBehaviour
    {
        private Vector3 base0;
        private void Start() => base0 = transform.localScale;
        private void Update()
        {
            float s = 1f + Mathf.Sin(Time.time * 5f) * 0.12f;
            transform.localScale = new Vector3(base0.x * s, base0.y, base0.z * s);
        }
    }
}
