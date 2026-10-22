using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>On the floors too, foi's own blue ring glows under it (as in the duels): always plain which one is foi.</summary>
    public partial class GameManager
    {
        private FootRing levelRing;

        private void UpdateLevelRing()
        {
            bool show = State == GameState.Playing && !challengeRun && robot != null && robot.IsAlive && robot.Visual != null && robot.Visual.gameObject.activeInHierarchy;
            if (levelRing == null)
            {
                if (!show) return;
                levelRing = FootRing.Make(new Color(0.25f, 0.6f, 1f), 2.1f);
            }
            levelRing.Follow(show ? robot.transform.position : (Vector3?)null);
        }
    }
}
