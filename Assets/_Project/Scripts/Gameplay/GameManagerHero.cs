using System.Collections;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// foi's build (one for everyone now: the picker no longer opens). The robot rebuilds its body and keeps its outfit; the menu's showcase follows.
    /// </summary>
    public partial class GameManager
    {
        private void OnHeroPicked(int hero)
        {
            robot.SetHero(hero);
            robot.ApplyOutfit(Cosmetics.Outfit());
            if (lobby != null)
            {
                Destroy(lobby.gameObject);
                lobby = null;
            }
            if (State == GameState.Menu && !ui.Garage.Screen.IsVisible) ShowMenu();
        }
    }
}
