using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Fortress stages on the game's side: the energy gates on the bridges (they drop when the stage before is beaten),
    /// the stage in the HUD, and a burn in a later stage costing a heart and going on from that stage's entrance rather
    /// than the whole floor. Hard floors: Fifi falls with foi and comes back with it; a rescue gives half the health back.
    /// </summary>
    public partial class GameManager
    {
        private readonly Dictionary<GridPos, StageGate> stageGates = new Dictionary<GridPos, StageGate>();

        private void InitStages()
        {
            hunt.GateOpened += OnGateOpened;
        }

        private void SetupStageGates()
        {
            ClearStageGates();
            if (level == null || level.mission != MissionType.Hunt) return;
            // One gate across each bridge (its gate tiles side by side in a row); every tile of it opens it.
            var done = new HashSet<GridPos>();
            foreach (var g in grid.GateTiles)
            {
                if (done.Contains(g)) continue;
                var row = grid.GateTiles.FindAll(o => o.y == g.y && Mathf.Abs(o.x - g.x) <= 3);
                Vector3 centre = Vector3.zero;
                foreach (var o in row) { centre += GridView.ToWorld(o); done.Add(o); }
                centre /= row.Count;
                var gate = StageGate.Create(centre + Vector3.up * GridView.SurfaceY, WorldTheme.Current.accent, row.Count);
                foreach (var o in row) stageGates[o] = gate;
            }
        }

        private void ClearStageGates()
        {
            foreach (var g in new HashSet<StageGate>(stageGates.Values)) if (g != null) Destroy(g.gameObject);
            stageGates.Clear();
            ClearBridgeGuide();
        }

        // ---------- The way over the bridge ----------
        // When a gate opens, arrows on the floor run from foi to the next stage's entrance until foi gets there.
        private GridPos? bridgeGoal;
        private int bridgeStage = -1;
        private GridPos bridgeFrom = new GridPos(-99, -99);
        private readonly List<Transform> bridgeMarks = new List<Transform>();
        private Material bridgeMat;
        private static readonly Color BridgeGold = new Color(1f, 0.78f, 0.3f);

        private void StartBridgeGuide(int stage)
        {
            ClearBridgeGuide();
            if (stage + 1 >= hunt.StageCount) return;
            bridgeStage = stage;
            bridgeGoal = hunt.StageEntry(stage + 1);
            bridgeMat ??= MaterialFactory.Create(BridgeGold, new Color(2.2f, 1.5f, 0.45f));
        }

        private void ClearBridgeGuide()
        {
            foreach (var m in bridgeMarks) if (m != null) Destroy(m.gameObject);
            bridgeMarks.Clear();
            bridgeGoal = null;
            bridgeStage = -1;
            bridgeFrom = new GridPos(-99, -99);
        }

        private void UpdateBridgeGuide()
        {
            if (!bridgeGoal.HasValue || hunt == null) return;
            if (hunt.RobotStage > bridgeStage) { ClearBridgeGuide(); return; } // over the bridge: done
            if (robot.Position != bridgeFrom) BuildBridgeTrail();
            for (int i = 0; i < bridgeMarks.Count; i++)
            {
                // A wave of light running along the arrows towards the bridge.
                float wave = Mathf.Repeat(Time.time * 7f - i, 9f);
                float k = wave < 1.6f ? 1f - wave / 1.6f : 0f;
                if (bridgeMarks[i] != null) bridgeMarks[i].localScale = Vector3.one * (1.6f + k * 0.6f);
            }
        }

        /// <summary>The shortest walk from foi to the next stage's entrance over the floor, an arrow on each tile.</summary>
        private void BuildBridgeTrail()
        {
            bridgeFrom = robot.Position;
            foreach (var m in bridgeMarks) if (m != null) Destroy(m.gameObject);
            bridgeMarks.Clear();
            var goal = bridgeGoal.Value;
            var came = new Dictionary<GridPos, GridPos> { [robot.Position] = robot.Position };
            var queue = new Queue<GridPos>();
            queue.Enqueue(robot.Position);
            bool found = false;
            while (queue.Count > 0 && !found)
            {
                var p = queue.Dequeue();
                foreach (var d in DirectionExtensions.All)
                {
                    var n = p + d.ToOffset();
                    if (came.ContainsKey(n) || !grid.IsFloor(n) || grid.IsLava(n) || grid.IsCage(n)) continue;
                    came[n] = p;
                    if (n == goal) { found = true; break; }
                    queue.Enqueue(n);
                }
            }
            if (!found) return;
            var path = new List<GridPos>();
            for (var p = goal; p != robot.Position; p = came[p]) path.Add(p);
            path.Reverse();
            var prev = robot.Position;
            foreach (var p in path)
            {
                var dir = GridView.ToWorld(p) - GridView.ToWorld(prev);
                dir.y = 0f;
                var mark = new GameObject("BridgeArrow").transform;
                mark.position = GridView.ToWorld(p) + Vector3.up * (GridView.SurfaceY + 0.08f);
                if (dir.sqrMagnitude > 0.001f) mark.rotation = Quaternion.LookRotation(dir);
                Shapes.Rounded("Shaft", mark, new Vector3(0f, 0f, -0.06f), new Vector3(0.12f, 0.03f, 0.34f), 0.02f, bridgeMat);
                foreach (float s in new[] { -1f, 1f })
                    Shapes.Rounded("Head", mark, new Vector3(s * 0.09f, 0f, 0.14f), new Vector3(0.09f, 0.03f, 0.26f), 0.02f, bridgeMat)
                        .transform.localRotation = Quaternion.Euler(0f, -s * 40f, 0f);
                bridgeMarks.Add(mark);
                prev = p;
            }
        }

        private void OnGateOpened(int stage, List<GridPos> tiles)
        {
            foreach (var t in tiles)
            {
                if (stageGates.TryGetValue(t, out var g) && g != null) g.Open();
                var at = GridView.ToWorld(t) + Vector3.up * 0.6f;
                fx.Burst(at, new Color(1f, 0.7f, 0.45f), new Color(2.4f, 1.4f, 0.6f), 40, 6f);
                Shockwave.Create(GridView.ToWorld(t), 2f, new Color(1f, 0.6f, 0.35f));
            }
            if (tiles.Count > 0) FloatAt(robot.transform.position + Vector3.up * 0.9f, Loc.F("float.gateOpen", stage + 2), Palette.UiGold);
            StartBridgeGuide(stage);
            AudioManager.PlaySfx(Sfx.Shield, 1f, 0.8f);
            AudioManager.PlaySfx(Sfx.Impact, 0.6f, 1.3f);
            cameraRig.Shake(0.4f);
            Haptics.Medium();
        }

        /// <summary>foi bumped into a shut gate: say what opens it (not on every bump).</summary>
        private void OnStageGateBumped(GridPos p)
        {
            if ((!grid.IsGate(p) && !StageLocked(p)) || Time.unscaledTime - lastGateNote < 2f) return;
            lastGateNote = Time.unscaledTime;
            FloatAt(GridView.ToWorld(p) + Vector3.up * 1.1f, Loc.T("float.gateShut"), new Color(1f, 0.6f, 0.4f));
        }
        private float lastGateNote = -10f;

        /// <summary>The fortress stage a tile belongs to, from the level's own layout (a bridge counts with the stage it
        /// starts from until its gate, then with the next).</summary>
        private int StageAt(GridPos p)
        {
            var b = level != null ? level.stageBounds : null;
            if (b == null || b.Length < 8) return 0;
            int n = b.Length / 4;
            for (int s = 0; s < n; s++)
            {
                if (p.y <= b[s * 4 + 3]) return s;
                if (s < n - 1 && p.y < b[(s + 1) * 4 + 1]) return p.y > b[s * 4 + 3] + 1 + LevelCatalog.BridgeGateRow ? s + 1 : s;
            }
            return n - 1;
        }

        /// <summary>A tile behind a shut stage gate (fortress floors of the core loop only).</summary>
        private bool StageLocked(GridPos p) =>
            State == GameState.Playing && level != null && level.mission == MissionType.Hunt && hunt != null && hunt.StageLocked(p);

        /// <summary>"Stage 2/4 · ..." in the HUD on fortress floors.</summary>
        private string StagePrefix() =>
            hunt != null && hunt.StageCount > 1 ? Loc.F("hud.stage", hunt.RobotStage + 1, hunt.StageCount) + "  ·  " : "";

        /// <summary>
        /// foi went down in a stage past the first: a heart is spent and it carries on from that stage's entrance, with a
        /// full health bar and a moment of shield (Fifi, down with it on a hard floor, comes back too). False: the first
        /// stage, no heart left — a real loss.
        /// </summary>
        private bool StageRespawn()
        {
            if (State != GameState.Playing || level == null || level.mission != MissionType.Hunt || hunt.StageCount <= 1) return false;
            int s = hunt.RobotStage;
            if (s < 1 || hunt.MonsterDown || !Lives.TryConsume()) return false;
            var entry = hunt.StageEntry(s);
            var at = SafeTileNear(entry, entry);
            robot.Spawn(grid, at);
            robot.GiveShield(3f);
            health = 1f;
            hurtCooldown = 0f;
            RefreshHealthBar();
            if (level.hard) KnockOutFifi();
            ReviveFifi();
            cameraRig.Shake(0.6f);
            fx.Burst(robot.transform.position + Vector3.up * 0.5f, new Color(0.5f, 0.9f, 1f), new Color(0.6f, 1.6f, 2.4f), 30, 5f);
            ui.ShowIntro(Loc.F("stage.back", s + 1), Loc.T("stage.backText") + " · " + Loc.T("road.lifeLost") + " · " + Loc.F("lives.left", Lives.Count));
            AudioManager.PlaySfx(Sfx.Shield, 1f, 1.1f);
            return true;
        }
    }
}
