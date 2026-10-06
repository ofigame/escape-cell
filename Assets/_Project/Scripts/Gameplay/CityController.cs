using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Runs city mode: the building view (tray, placing with a ghost, selecting, panning and pinch zoom on the
    /// isometric plot) and the walking view (stick, camera turning, repairs in person, mine pickups, the training
    /// ground), plus the residents' wishes and the first-time tips.
    /// </summary>
    public class CityController : MonoBehaviour
    {
        /// <summary>The training ground was used: play a daily bonus game.</summary>
        public event Action TrainingPressed;
        /// <summary>A coin pickup or reward at a world position (for the flying coins and floating numbers).</summary>
        public event Action<Vector3, string, Color> Floated;
        /// <summary>Coins flew to the counter from this world position.</summary>
        public event Action<Vector3> CoinFlown;

        private const float TurnSpeed = 0.25f;  // degrees per pixel dragged
        private const float Reach = 0.8f;
        // The tray covers the lower quarter: the plot sits a little higher and closer than a level would.
        private const float BuildLift = 0.14f, BuildZoom = 0.74f;

        private CityScene scene;
        private CityWalker walker;
        private CityScreen ui;
        private CameraRig rig;
        private Robot robot;
        private GridView gridView;
        private FxSystem fx;

        // Building view state.
        private CityPiece ghostPiece;
        private int ghostRot, movingUid, selectedUid;
        private Vector2Int ghostCell;
        private bool draggingGhost, panning, cardDrag;
        private Vector2 lastPointer;
        private Vector3 pan;
        private float zoom = 1f, pinchStart, pinchZoom;
        private bool wasPinching;
        private Vector2 pressStart;
        private bool pressed, pressMoved;
        private float refreshTimer;
        private CityPlaced interactTarget;
        private bool interactTraining;

        public bool Active { get; private set; }

        public void Init(Robot robotRef, GridView view, CameraRig rigRef, FxSystem fxRef)
        {
            robot = robotRef;
            gridView = view;
            rig = rigRef;
            fx = fxRef;
            scene = gameObject.AddComponent<CityScene>();
            scene.Init(view, robotRef.transform);
            walker = gameObject.AddComponent<CityWalker>();
            walker.Init(robotRef, rigRef);
        }

        /// <summary>The UI is rebuilt when the language changes.</summary>
        public void SetScreen(CityScreen screen)
        {
            ui = screen;
            ui.ModePressed += ToggleMode;
            ui.UndoPressed += Undo;
            ui.RepairAllPressed += RepairAll;
            ui.PieceTapped += OnPieceTapped;
            ui.PieceDragStarted += (p, s) => { cardDrag = true; StartGhost(p, s); };
            ui.PieceDragged += s => { if (ghostPiece != null) MoveGhostTo(s); };
            ui.PieceDropped += s => { cardDrag = false; if (ghostPiece != null) { MoveGhostTo(s); TipOnce("ghost"); } };
            ui.GhostRotate += () => { if (ghostPiece == null) return; ghostRot = (ghostRot + 1) % 4; UpdateGhost(); AudioManager.PlaySfx(Sfx.Click, 0.6f, 1.3f); };
            ui.GhostConfirm += ConfirmGhost;
            ui.GhostCancel += CancelGhost;
            ui.SelMove += MoveSelected;
            ui.SelRotate += RotateSelected;
            ui.SelSell += SellSelected;
            ui.SelRepair += () => RepairPiece(selectedUid, inPerson: false);
            ui.SelClose += Deselect;
            ui.InteractPressed += Interact;
            ui.ClaimPressed += Claim;
        }

        // ---------- Open / close ----------

        public void Open()
        {
            Active = true;
            // The plot uses the main game's cells.
            var grid = new GridModel(City.Size, City.Size);
            gridView.Build(grid, fx);
            rig.Frame(City.Size, City.Size);
            rig.SetStyle(CameraStyle.Still);
            rig.SetMenuFocus(false);
            rig.Showcase(null, 0f);
            pan = Vector3.zero;
            zoom = 1f;
            ApplyView();
            rig.FrameUpper(BuildLift, BuildZoom);

            City.RefreshRequests();
            bool welcome = City.Visit();
            City.ClearUndo();
            // Pictures for the tray (made once).
            foreach (var p in CityCatalog.All) CityThumbs.Get(p);
            scene.Show();
            robot.gameObject.SetActive(false);
            ghostPiece = null;
            selectedUid = movingUid = 0;
            ui.Show(walking: false);

            if (welcome) ui.ShowTip(Loc.T("city.welcomeBack"), () => TipOnce("open"));
            else TipOnce("open");
        }

        public void Close()
        {
            if (!Active) return;
            Active = false;
            walker.Stop();
            scene.Hide();
            ui.Hide();
            robot.gameObject.SetActive(true);
            City.ClearUndo();
        }

        private void ApplyView() => rig.SetUserView(pan, zoom);

        private void ToggleMode()
        {
            if (ghostPiece != null) CancelGhost();
            Deselect();
            if (walker.Active)
            {
                walker.Stop();
                scene.ClearView(Vector3.zero, Vector3.zero);
                robot.gameObject.SetActive(false);
                rig.Frame(City.Size, City.Size);
                rig.SetStyle(CameraStyle.Still);
                rig.PlayIntro();
                ApplyView();
                rig.FrameUpper(BuildLift, BuildZoom);
                ui.SetWalking(false);
            }
            else
            {
                robot.gameObject.SetActive(true);
                var start = new Vector3(Mathf.Clamp(pan.x + 3.5f, 0f, City.Size - 1), 0f, Mathf.Clamp(pan.z + 3.5f, 0f, City.Size - 1));
                walker.Begin(start);
                ui.SetWalking(true);
                TipOnce("walk");
            }
        }

        // ---------- Every frame ----------

        private void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;

            refreshTimer -= dt;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 1f;
                City.RefreshRequests();
                ui.Refresh();
                if (City.NeedingCare > 0) TipOnce("care");
                if (City.Requests.Count > 0) TipOnce("wish");
            }

            if (walker.Active) UpdateWalking(dt);
            else UpdateBuilding();
            UpdateBubbles();
        }

        private void UpdateBubbles()
        {
            var cam = rig.Cam;
            for (int i = 0; i < ui.BubbleCount; i++)
            {
                CityRequest r = i < City.Requests.Count ? City.Requests[i] : null;
                Vector2? point = null;
                if (r != null && scene.ResidentHead(r.resident, out var head))
                {
                    var sp = cam.WorldToScreenPoint(head);
                    if (sp.z > 0f) point = new Vector2(sp.x, sp.y);
                }
                ui.SetBubble(i, r, point, null);
            }
        }

        // ---------- Walking ----------

        private void UpdateWalking(float dt)
        {
            // Dragging anywhere off the controls turns the camera.
            if (ReadPointers(out var p0, out _, out int count) && count >= 1)
            {
                if (!pressed)
                {
                    pressed = true;
                    panning = !ui.IsOverUi(p0);
                    lastPointer = p0;
                }
                else if (panning)
                {
                    walker.Turn((p0.x - lastPointer.x) * TurnSpeed);
                    lastPointer = p0;
                }
            }
            else
            {
                pressed = false;
                panning = false;
            }

            walker.Tick(ui.Stick.Value, dt);
            scene.ClearView(rig.Cam.transform.position, walker.Position);

            // Gold mines hand over their coins when the robot walks by.
            foreach (var p in City.Pieces)
            {
                if (p.Piece.perk != CityPerk.Mine || City.MineCoins(p) <= 0 || Distance(p, walker.Position) > Reach + 0.3f) continue;
                var at = scene.PieceTop(p.uid);
                int coins = City.CollectMine(p.uid);
                if (coins <= 0) continue;
                for (int i = 0; i < Mathf.Min(8, 1 + coins / 8); i++) CoinFlown?.Invoke(at);
                Floated?.Invoke(at, "+" + coins, Palette.UiGold);
                AudioManager.PlaySfx(Sfx.Coin, 0.9f, 1.1f);
                Haptics.Light();
                ui.Refresh();
            }

            // Residents with a finished wish hand over the reward when the robot comes up to them.
            foreach (var r in new List<CityRequest>(City.Requests))
                if (City.RequestDone(r) && scene.ResidentHead(r.resident, out var head) && (new Vector3(head.x, 0f, head.z) - walker.Position).sqrMagnitude < 1f)
                    Claim(r);

            // The action button: repair the building in reach, or play at the training ground.
            interactTarget = null;
            interactTraining = false;
            float best = Reach;
            foreach (var p in City.Pieces)
            {
                float d = Distance(p, walker.Position);
                if (d > best) continue;
                if (City.NeedsAttention(p)) { interactTarget = p; interactTraining = false; best = d; }
                else if (p.Piece.perk == CityPerk.Training && City.Stage(p) != CareStage.NeedsCare) { interactTarget = p; interactTraining = true; best = d; }
            }
            if (walker.Repairing) ui.SetInteract(null);
            else if (interactTarget == null) ui.SetInteract(null);
            else if (interactTraining) ui.SetInteract(Loc.T("city.play"));
            else
            {
                int fee = City.CareFee(interactTarget);
                int back = fee > 0 ? Mathf.Max(1, Mathf.RoundToInt(fee * 0.2f)) : 0;
                ui.SetInteract(fee > 0 ? Loc.F("city.repairWalk", fee, back) : Loc.T("city.repairFree"));
            }
        }

        /// <summary>Distance from a point to a piece's footprint (0 inside it).</summary>
        private static float Distance(CityPlaced p, Vector3 at)
        {
            var s = p.Piece.Size(p.rot);
            float dx = Mathf.Max(p.x - 0.5f - at.x, 0f, at.x - (p.x + s.x - 0.5f));
            float dz = Mathf.Max(p.y - 0.5f - at.z, 0f, at.z - (p.y + s.y - 0.5f));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void Interact()
        {
            if (interactTarget == null) return;
            if (interactTraining)
            {
                TrainingPressed?.Invoke();
                return;
            }
            if (RepairPiece(interactTarget.uid, inPerson: true)) walker.PlayRepair();
        }

        private bool RepairPiece(int uid, bool inPerson)
        {
            if (!City.Repair(uid, inPerson, out int paid, out int refund))
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                Floated?.Invoke(scene.PieceTop(uid), Loc.T("city.noCoins"), Palette.UiRed);
                return false;
            }
            var at = scene.PieceTop(uid);
            fx.Burst(at, Palette.UiCyan, Palette.ShieldPickupGlow, 30, 4f);
            AudioManager.PlaySfx(Sfx.Shield, 0.9f, 1.2f);
            Haptics.Medium();
            Floated?.Invoke(at, refund > 0 ? Loc.F("city.repairedBack", refund) : Loc.T("city.repaired"), Palette.UiCyan);
            scene.RefreshAll();
            if (selectedUid == uid) ui.ShowSelection(City.Get(uid));
            ui.Refresh();
            return true;
        }

        private void RepairAll()
        {
            if (!City.RepairAll())
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                return;
            }
            AudioManager.PlaySfx(Sfx.Shield, 1f, 1.1f);
            Haptics.Medium();
            rig.Punch(0.5f);
            foreach (var p in City.Pieces) fx.Burst(scene.PieceTop(p.uid), Palette.UiCyan, Palette.ShieldPickupGlow, 8, 3f);
            scene.RefreshAll();
            ui.Refresh();
        }

        private void Claim(CityRequest r)
        {
            int resident = r.resident;
            int coins = City.Claim(r, out bool bestFriend);
            if (coins <= 0) return;
            scene.Dance(resident);
            if (bestFriend) scene.MakeBestFriend(resident);
            var at = scene.ResidentHead(resident, out var head) ? head : new Vector3(3.5f, 1f, 3.5f);
            for (int i = 0; i < 8; i++) CoinFlown?.Invoke(at);
            Floated?.Invoke(at, "+" + coins, Palette.UiGold);
            fx.Burst(at, Palette.Coin, Palette.CoinGlow, 30, 5f);
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.2f);
            Haptics.Medium();
            if (bestFriend) ui.ShowTip(Loc.F("city.bestFriendNow", Residents.Names[resident], City.BestFriendReward));
            ui.Refresh();
        }

        // ---------- Building ----------

        private void UpdateBuilding()
        {
            if (ui.TipShown) return;
            bool down = ReadPointers(out var p0, out var p1, out int count);

            // Two fingers: pinch to zoom.
            if (down && count >= 2)
            {
                float dist = (p0 - p1).magnitude;
                if (!wasPinching) { pinchStart = dist; pinchZoom = zoom; wasPinching = true; }
                zoom = Mathf.Clamp(pinchZoom * pinchStart / Mathf.Max(1f, dist), 0.45f, 1.25f);
                ApplyView();
                panning = draggingGhost = false;
                pressMoved = true;
                return;
            }
            if (wasPinching && !down) wasPinching = false;
            if (wasPinching) return;

            float scroll = MouseScroll();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                zoom = Mathf.Clamp(zoom * (scroll > 0f ? 0.9f : 1.1f), 0.45f, 1.25f);
                ApplyView();
            }

            if (cardDrag) return; // the tray card drives the ghost while it is being dragged out

            if (down && !pressed)
            {
                pressed = true;
                pressMoved = false;
                pressStart = lastPointer = p0;
                if (ui.IsOverUi(p0)) { pressed = false; pressStart = new Vector2(-9999f, -9999f); return; }
                // Pressing on the ghost carries it; anywhere else pans the view.
                draggingGhost = ghostPiece != null && ScreenToCell(p0, out var cell) && InsideGhost(cell);
                panning = !draggingGhost;
                return;
            }
            if (down && pressed)
            {
                if ((p0 - pressStart).magnitude > DragThreshold) pressMoved = true;
                if (draggingGhost) MoveGhostTo(p0);
                else if (panning && pressMoved && ScreenToGround(lastPointer, out var a) && ScreenToGround(p0, out var b))
                {
                    pan += a - b;
                    pan.x = Mathf.Clamp(pan.x, -4f, 4f);
                    pan.z = Mathf.Clamp(pan.z, -4f, 4f);
                    ApplyView();
                }
                lastPointer = p0;
                return;
            }
            if (!down && pressed)
            {
                pressed = false;
                bool tap = !pressMoved;
                panning = draggingGhost = false;
                if (tap) OnTap(pressStart);
            }
        }

        private static float DragThreshold => 0.12f * (Screen.dpi > 0 ? Screen.dpi : 160f);

        private void OnTap(Vector2 screen)
        {
            if (!ScreenToCell(screen, out var cell)) { Deselect(); return; }
            if (ghostPiece != null)
            {
                // Tapping a cell moves the ghost there.
                ghostCell = ClampCell(cell, ghostPiece, ghostRot);
                UpdateGhost();
                return;
            }
            var at = City.At(cell.x, cell.y);
            if (at == null) { Deselect(); return; }
            Select(at.uid);
        }

        private void Select(int uid)
        {
            selectedUid = uid;
            scene.Select(uid);
            var p = City.Get(uid);
            ui.ShowSelection(p);
            AudioManager.PlaySfx(Sfx.Click, 0.6f, 1.2f);
        }

        private void Deselect()
        {
            if (selectedUid == 0) return;
            selectedUid = 0;
            scene.Select(0);
            ui.HideSelection();
        }

        private void OnPieceTapped(CityPiece p)
        {
            if (!City.Unlocked(p))
            {
                // Locked pieces can be made the goal shown on the result card.
                Goal.Toggle(Goal.ForCity(p));
                AudioManager.PlaySfx(Sfx.Click, 0.7f, Goal.Is(Goal.ForCity(p)) ? 1.3f : 0.9f);
                Floated?.Invoke(new Vector3(3.5f, 1.5f, 3.5f) + pan, Goal.Is(Goal.ForCity(p)) ? Loc.T("city.goalSet") : Loc.T("city.goalOff"), Palette.UiGold);
                ui.Refresh();
                return;
            }
            if (City.Full) { Floated?.Invoke(new Vector3(3.5f, 1.5f, 3.5f), Loc.T("city.full"), Palette.UiRed); return; }
            // A tap drops the piece's ghost on the nearest free spot in the middle of the view.
            ghostPiece = p;
            ghostRot = 0;
            movingUid = 0;
            Deselect();
            var mid = new Vector2Int(Mathf.RoundToInt(3.5f + pan.x - (p.w - 1) * 0.5f), Mathf.RoundToInt(3.5f + pan.z - (p.h - 1) * 0.5f));
            ghostCell = City.FindSpot(p, 0, mid.x, mid.y, out var spot) ? spot : ClampCell(mid, p, 0);
            UpdateGhost();
            AudioManager.PlaySfx(Sfx.Hop, 0.6f, 1.2f);
            TipOnce("ghost");
        }

        private void StartGhost(CityPiece p, Vector2 screen)
        {
            if (City.Full) return;
            ghostPiece = p;
            ghostRot = 0;
            movingUid = 0;
            Deselect();
            MoveGhostTo(screen);
        }

        private void MoveGhostTo(Vector2 screen)
        {
            if (!ScreenToCell(screen, out var cell)) return;
            var s = ghostPiece.Size(ghostRot);
            // The finger holds the piece by its middle.
            var target = new Vector2Int(cell.x - (s.x - 1) / 2, cell.y - (s.y - 1) / 2);
            target = ClampCell(target, ghostPiece, ghostRot);
            if (target != ghostCell)
            {
                ghostCell = target;
                AudioManager.PlaySfx(Sfx.Click, 0.25f, 1.6f);
            }
            UpdateGhost();
        }

        private static Vector2Int ClampCell(Vector2Int c, CityPiece p, int rot)
        {
            var s = p.Size(rot);
            return new Vector2Int(Mathf.Clamp(c.x, 0, City.Size - s.x), Mathf.Clamp(c.y, 0, City.Size - s.y));
        }

        private bool InsideGhost(Vector2Int cell)
        {
            var s = ghostPiece.Size(ghostRot);
            return cell.x >= ghostCell.x - 1 && cell.x <= ghostCell.x + s.x && cell.y >= ghostCell.y - 1 && cell.y <= ghostCell.y + s.y;
        }

        private void UpdateGhost()
        {
            ghostCell = ClampCell(ghostCell, ghostPiece, ghostRot);
            scene.ShowGhost(ghostPiece, ghostCell, ghostRot, movingUid);
            bool fits = City.Fits(ghostPiece, ghostCell.x, ghostCell.y, ghostRot, movingUid);
            ui.ShowGhostBar(ghostPiece, fits, movingUid != 0);
        }

        private void ConfirmGhost()
        {
            if (ghostPiece == null) return;
            if (movingUid != 0)
            {
                if (!City.Move(movingUid, ghostCell.x, ghostCell.y, ghostRot)) { AudioManager.PlaySfx(Sfx.Bump, 0.6f); return; }
                scene.SetHidden(movingUid, false);
                scene.Refresh(movingUid);
                PlacedFx(movingUid, false);
            }
            else
            {
                var placed = City.Place(ghostPiece, ghostCell.x, ghostCell.y, ghostRot);
                if (placed == null) { AudioManager.PlaySfx(Sfx.Bump, 0.6f); return; }
                scene.Refresh(placed.uid);
                PlacedFx(placed.uid, true);
                if (Goal.Is(Goal.ForCity(ghostPiece))) Goal.Toggle(Goal.ForCity(ghostPiece));
            }
            EndGhost();
            ui.Refresh();
        }

        private void PlacedFx(int uid, bool bought)
        {
            var at = scene.PieceTop(uid) - Vector3.up * 0.8f;
            fx.Dust(at, Palette.TileTop, 14, 2f);
            if (bought) fx.Burst(at + Vector3.up * 0.4f, Palette.Coin, Palette.CoinGlow, 12, 3f);
            AudioManager.PlaySfx(Sfx.Impact, 0.6f, 1.2f);
            Haptics.Light();
            rig.Punch(0.3f);
        }

        private void CancelGhost()
        {
            if (ghostPiece == null) return;
            if (movingUid != 0) scene.SetHidden(movingUid, false);
            EndGhost();
        }

        private void EndGhost()
        {
            ghostPiece = null;
            movingUid = 0;
            scene.HideGhost();
            ui.HideGhostBar();
        }

        private void MoveSelected()
        {
            var p = City.Get(selectedUid);
            if (p == null) return;
            int uid = selectedUid;
            Deselect();
            ghostPiece = p.Piece;
            ghostRot = p.rot;
            ghostCell = new Vector2Int(p.x, p.y);
            movingUid = uid;
            scene.SetHidden(uid, true);
            UpdateGhost();
        }

        private void RotateSelected()
        {
            var p = City.Get(selectedUid);
            if (p == null) return;
            int rot = (p.rot + 1) % 4;
            // Turning a long piece may need it to shift so it still fits.
            var cell = ClampCell(new Vector2Int(p.x, p.y), p.Piece, rot);
            if (!City.Move(p.uid, cell.x, cell.y, rot))
            {
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                Floated?.Invoke(scene.PieceTop(p.uid), Loc.T("city.noRoom"), Palette.UiRed);
                return;
            }
            scene.Refresh(p.uid);
            scene.Select(p.uid);
            AudioManager.PlaySfx(Sfx.Click, 0.6f, 1.3f);
            ui.Refresh();
        }

        private void SellSelected()
        {
            int uid = selectedUid;
            var at = scene.PieceTop(uid);
            int refund = City.Sell(uid);
            Deselect();
            scene.Remove(uid);
            fx.Dust(at - Vector3.up * 0.8f, Palette.TileTop, 16, 2f);
            Floated?.Invoke(at, "+" + refund, Palette.UiGold);
            AudioManager.PlaySfx(Sfx.Coin, 0.9f, 0.9f);
            ui.Refresh();
        }

        private void Undo()
        {
            if (ghostPiece != null) CancelGhost();
            Deselect();
            int uid = City.Undo();
            if (uid == 0) { AudioManager.PlaySfx(Sfx.Bump, 0.5f); return; }
            scene.RefreshAll();
            AudioManager.PlaySfx(Sfx.Hop, 0.6f, 0.8f);
            ui.Refresh();
        }

        // ---------- Tips ----------

        private void TipOnce(string key)
        {
            string pref = "sb_city_tip_" + key;
            if (PlayerPrefs.GetInt(pref, 0) == 1 || ui.TipShown) return;
            PlayerPrefs.SetInt(pref, 1);
            ui.ShowTip(Loc.T("city.tip." + key));
        }

        // ---------- Input ----------

        private bool ScreenToGround(Vector2 screen, out Vector3 hit)
        {
            var ray = rig.Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, GridView.SurfaceY, 0f));
            hit = default;
            if (!plane.Raycast(ray, out float d)) return false;
            hit = ray.GetPoint(d);
            return true;
        }

        private bool ScreenToCell(Vector2 screen, out Vector2Int cell)
        {
            cell = default;
            if (!ScreenToGround(screen, out var hit)) return false;
            cell = new Vector2Int(Mathf.RoundToInt(hit.x), Mathf.RoundToInt(hit.z));
            return cell.x >= -1 && cell.y >= -1 && cell.x <= City.Size && cell.y <= City.Size;
        }

        /// <summary>The first two touches (or the mouse); false when nothing is pressed.</summary>
        private static bool ReadPointers(out Vector2 first, out Vector2 second, out int count)
        {
            first = second = Vector2.zero;
            count = 0;
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (count == 0) first = t.position.ReadValue();
                    else if (count == 1) second = t.position.ReadValue();
                    count++;
                }
                if (count > 0) return true;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                first = mouse.position.ReadValue();
                count = 1;
                return true;
            }
#else
            for (int i = 0; i < Input.touchCount && i < 2; i++)
            {
                if (i == 0) first = Input.GetTouch(0).position; else second = Input.GetTouch(1).position;
                count++;
            }
            if (count > 0) return true;
            if (Input.GetMouseButton(0)) { first = Input.mousePosition; count = 1; return true; }
#endif
            return false;
        }

        private static float MouseScroll()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null ? mouse.scroll.ReadValue().y : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        // ---------- Test hooks ----------

        public void TestPlaceGhost(string id, int x, int y)
        {
            var p = CityCatalog.Find(id);
            if (p == null) return;
            ghostPiece = p;
            ghostRot = 0;
            movingUid = 0;
            ghostCell = new Vector2Int(x, y);
            UpdateGhost();
        }

        public void TestSelect(int x, int y)
        {
            var at = City.At(x, y);
            if (at != null) Select(at.uid);
        }

        public void TestWalk(Vector3 at, float yaw)
        {
            if (!walker.Active) ToggleMode();
            walker.Begin(at);
            walker.Turn(yaw - walker.CameraYaw);
        }
    }
}
