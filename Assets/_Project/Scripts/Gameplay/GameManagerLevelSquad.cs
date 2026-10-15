using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The army's soldiers on the floors: picked on the card before a level (BRING SOLDIERS: how many of each group,
    /// and the stage each group joins foi in), they appear beside foi when it first sets foot in their stage and fight
    /// the robots as foi's allies. One who falls is gone from the pool for a while. Four may come in the first world, two more in each next one.
    /// </summary>
    public partial class GameManager
    {
        private SquadPickScreen squadPick;
        /// <summary>The stages whose soldiers have come already (null: no soldiers on this floor).</summary>
        private bool[] squadDeployed;

        private void ShowSquadPick(int index)
        {
            if (squadPick == null)
            {
                squadPick = SquadPickScreen.Create(ui.transform.Find("Canvas/SafeArea") ?? ui.transform.Find("Canvas") ?? ui.transform);
                squadPick.Closed += () => ui.RefreshPrelevelSquad(true);
            }
            int i = Mathf.Clamp(index, 0, LevelCount - 1);
            LevelSquad.Limit = LevelSquad.LimitFor(LevelCatalog.WorldOf(i));
            squadPick.Show(Mathf.Max(1, levelSet.levels[i].fortressStages));
        }

        /// <summary>A floor starts: its soldiers are waiting for foi in their stages, at the army's strength (on the floors and in
        /// the free-battle arenas; not on the roads or in the duels, which have their own). The world sets how many may come.</summary>
        private void PrepareLevelSquad()
        {
            LevelSquad.Limit = LevelSquad.LimitFor(arenaRun ? arenaIndex : World);
            bool floor = level != null && level.mission == MissionType.Hunt && !challengeRun && !trailRun;
            squadDeployed = floor && LevelSquad.Total > 0 ? new bool[Mathf.Max(1, level.fortressStages)] : null;
        }

        /// <summary>Each frame of a floor: foi in a stage for the first time — that stage's soldiers come in beside it.</summary>
        private void UpdateLevelSquad()
        {
            if (squadDeployed == null || State != GameState.Playing || previewing || roadPhase != RoadPhase.None || !robot.IsAlive) return;
            if (glassCell != null && !glassCell.Broken) return; // foi is still in its glass cell
            int s = Mathf.Clamp(hunt.StageCount > 1 ? hunt.RobotStage : 0, 0, squadDeployed.Length - 1);
            if (squadDeployed[s]) return;
            squadDeployed[s] = true;
            int came = 0;
            for (int g = 0; g < Army.Groups; g++)
            {
                if (LevelSquad.Stage(g, squadDeployed.Length) != s) continue;
                int n = LevelSquad.Count(g);
                for (int k = 0; k < n; k++)
                    if (hunt.AddLevelSoldier(g, robot.Position + new GridPos(k % 3 - 1, -1 - k / 3), Army.StatsOf(g))) came++;
            }
            if (came > 0)
            {
                FloatAt(robot.transform.position + Vector3.up * 1.2f, Loc.F("squad.arrived", came), Palette.UiCyan);
                Audio.AudioManager.PlaySfx(Audio.Sfx.Win, 0.6f, 1.2f);
            }
        }
    }
}
