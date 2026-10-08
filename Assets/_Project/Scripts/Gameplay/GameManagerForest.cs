using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Forest;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The third-person forest prototype, opened from the menu (see <see cref="ForestPrototype"/>). At the tunnel
    /// mouth the road course takes over (forest theme, water slides); out of the tunnel comes the next stretch of
    /// forest, and so on.
    /// </summary>
    public partial class GameManager
    {
        private const int ForestTunnelLevel = 36;

        private ForestPrototype forest;
        private bool forestRoad;

        private void StartForest()
        {
            if (forest != null) return;
            ResetRun();
            State = GameState.Menu; // the level loop stays idle while the prototype runs
            ui.HideScreens();
            if (lobby != null) lobby.Close();
            cameraRig.Showcase(null, 0f);
            cameraRig.SetMenuFocus(false);
            weather.gameObject.SetActive(false); // the stage's rain and sparkles stay out of the forest
            forest = ForestPrototype.Begin(robot, cameraRig, fx);
            forest.Exited += EndForest;
            forest.TunnelReached += StartForestTunnel;
        }

        private void EndForest()
        {
            if (forest == null) return;
            if (forestRoad)
            {
                forestRoad = false;
                runner.Stop();
                DuctRunner.Realistic = false;
            }
            forest.End();
            forest = null;
            weather.gameObject.SetActive(true);
            ShowMenu();
        }

        /// <summary>The tunnel: the road course from the tunnel mouth, a little harder with every leg.</summary>
        private void StartForestTunnel()
        {
            forestRoad = true;
            DuctRunner.Realistic = true;
            runner.PrepareRoad(9000 + forest.Leg * 31, forest.TunnelEntry, 0f, ForestTunnelLevel + forest.Leg * 4, 1, DuctRunner.Kind.Mine);
            runner.TakeOver();
            AudioManager.PlayMusic(MusicTheme.Tunnel);
            AudioManager.PlaySfx(Sfx.Hop, 0.8f, 1.2f);
            ui.ShowIntro(Loc.T("forest.tunnelTitle"), Loc.T("road.run"));
        }

        /// <summary>The tunnel walked: out into the next stretch of forest.</summary>
        private void ForestTunnelArrived()
        {
            forestRoad = false;
            int got = runner.Coins;
            runner.Stop();
            DuctRunner.Realistic = false;
            AudioManager.PlayMusic(MusicTheme.Menu);
            forest.NextLeg(got);
            ui.ShowIntro(Loc.F("forest.legTitle", forest.Leg), Loc.T("forest.follow"));
        }
    }
}
