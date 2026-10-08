using System.Collections;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// foi's four builds: the very first start opens the picker once (after the studio logo), and the garage can open it
    /// later to switch for coins. The robot rebuilds its body and keeps its outfit; the menu's showcase follows.
    /// </summary>
    public partial class GameManager
    {
        private IEnumerator FirstHeroPick()
        {
            yield return new WaitForSecondsRealtime(2.6f);
            while (State != GameState.Menu) yield return null;
            if (!SaveData.HeroChosen) ui.HeroPicker.Show(false);
        }

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
