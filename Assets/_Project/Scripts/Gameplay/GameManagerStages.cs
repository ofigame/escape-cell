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
            foreach (var g in grid.GateTiles)
                stageGates[g] = StageGate.Create(GridView.ToWorld(g) + Vector3.up * GridView.SurfaceY, WorldTheme.Current.accent);
        }

        private void ClearStageGates()
        {
            foreach (var g in stageGates.Values) if (g != null) Destroy(g.gameObject);
            stageGates.Clear();
        }

        private void OnGateOpened(int stage, List<GridPos> tiles)
        {
            foreach (var t in tiles)
            {
                if (stageGates.TryGetValue(t, out var g) && g != null) g.Open();
                var at = GridView.ToWorld(t) + Vector3.up * 0.6f;
                fx.Burst(at, new Color(1f, 0.7f, 0.45f), new Color(2.4f, 1.4f, 0.6f), 40, 6f);
                Shockwave.Create(GridView.ToWorld(t), 2f, new Color(1f, 0.6f, 0.35f));
                FloatAt(GridView.ToWorld(t) + Vector3.up * 0.8f, Loc.F("float.gateOpen", stage + 2), Palette.UiGold);
            }
            if (tiles.Count > 0) FloatAt(robot.transform.position + Vector3.up * 0.9f, Loc.F("float.gateOpen", stage + 2), Palette.UiGold);
            AudioManager.PlaySfx(Sfx.Shield, 1f, 0.8f);
            AudioManager.PlaySfx(Sfx.Impact, 0.6f, 1.3f);
            cameraRig.Shake(0.4f);
            Haptics.Medium();
        }

        /// <summary>foi bumped into a shut gate: say what opens it (not on every bump).</summary>
        private void OnStageGateBumped(GridPos p)
        {
            if (!grid.IsGate(p) || Time.unscaledTime - lastGateNote < 2f) return;
            lastGateNote = Time.unscaledTime;
            FloatAt(GridView.ToWorld(p) + Vector3.up * 1.1f, Loc.T("float.gateShut"), new Color(1f, 0.6f, 0.4f));
        }
        private float lastGateNote = -10f;

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
