using SquashBot.Forest;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>The third-person forest prototype, opened from the menu (see <see cref="ForestPrototype"/>).</summary>
    public partial class GameManager
    {
        private ForestPrototype forest;

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
        }

        private void EndForest()
        {
            if (forest == null) return;
            forest.End();
            forest = null;
            weather.gameObject.SetActive(true);
            ShowMenu();
        }
    }
}
