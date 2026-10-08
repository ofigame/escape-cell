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
        private bool forestRoad, forestPassage;

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
            forest.TunnelNear += PrepareForestPassage;
        }

        private void EndForest()
        {
            if (forest == null) return;
            if (forestRoad || forestPassage)
            {
                forestRoad = forestPassage = false;
                runner.Stop();
                DuctRunner.Realistic = false;
                DuctRunner.RealisticRoof = true;
            }
            forest.End();
            forest = null;
            weather.gameObject.SetActive(true);
            ShowMenu();
        }

        /// <summary>
        /// The passage, laid while the robot is still walking up to its mouth (so the mouth shows a real way on): the
        /// road course from where the ride waits inside, a little harder with every leg, and the next land at its far
        /// end. A cave is ridden in a minecart under a rock roof; a gorge is rafted down a river between high walls
        /// open to the sky.
        /// </summary>
        private void PrepareForestPassage()
        {
            if (forestPassage) return;
            forestPassage = true;
            DuctRunner.Realistic = true;
            DuctRunner.RealisticRoof = forest.ExitStyle != PassageStyle.Gorge;
            runner.PrepareRoad(9000 + forest.Leg * 31, forest.BoardPoint, 0f, ForestTunnelLevel + forest.Leg * 4, 1,
                DuctRunner.RealisticRoof ? DuctRunner.Kind.Mine : DuctRunner.Kind.Surf);
            forest.PrepareNext(runner.EndPose.position);
        }

        /// <summary>Aboard: the ride takes the robot and the camera on from where they are.</summary>
        private void StartForestTunnel()
        {
            PrepareForestPassage();
            forestRoad = true;
            runner.TakeOver();
            AudioManager.PlayMusic(MusicTheme.Tunnel);
            AudioManager.PlaySfx(Sfx.Hop, 0.8f, 1.2f);
            ui.ShowIntro(Loc.T(DuctRunner.RealisticRoof ? "forest.tunnelTitle" : "forest.gorgeTitle"), Loc.T("road.run"));
        }

        /// <summary>Out of the passage: on foot again in the land ahead, from right where the ride stopped.</summary>
        private void ForestTunnelArrived()
        {
            forestRoad = false;
            forestPassage = false;
            int got = runner.Coins;
            var cam = cameraRig.Cam.transform;
            var pose = new Pose(cam.position, cam.rotation);
            runner.Stop();
            DuctRunner.Realistic = false;
            DuctRunner.RealisticRoof = true;
            AudioManager.PlayMusic(MusicTheme.Menu);
            forest.SwitchWorld(got, pose);
            ui.ShowIntro(Loc.F("forest.legTitle", forest.Leg), Loc.T("forest.follow"));
        }
    }
}
