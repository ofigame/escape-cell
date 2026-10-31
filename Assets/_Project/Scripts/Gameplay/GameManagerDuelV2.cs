using System.Collections;
using System.Reflection;
using SquashBot.Data;
using SquashBot.DuelV2;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The duel fight core v2's prototype (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 9): the game sets a duel up as it always
    /// does (the stage, its crowd and light, the two foi, their weapons and crowns), then the v2 core takes the two
    /// over and the game's own duel stands still (<see cref="DuelCoreV2.Running"/>). Nothing here runs outside the
    /// prototype scene.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The prototype's start: offline (the bot rival at once), no menus or screens of the game's own.</summary>
        internal void V2Boot()
        {
            var fn = typeof(FoiNet);
            fn.GetField("devServerRead", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
            fn.GetField("devServer", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, "http://127.0.0.1:9");
            SaveData.HeroChosen = true;
            Challenge.PlayerName = "foi";
            Challenge.NameChosen = true;
            Monetization.Ads.Banner.Hide();
            if (lobby != null) { Destroy(lobby.gameObject); lobby = null; }
            ShowMenu();
            if (ui != null && ui.LoginView != null) ui.LoginView.Hide();
            V2HideScreens();
        }

        /// <summary>The game's own screens out of sight (the prototype draws its own).</summary>
        internal void V2HideScreens()
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c.enabled) c.enabled = false;
        }

        /// <summary>
        /// A duel set up on <paramref name="stage"/> with these weapons (family, the league tier's weapon tier), then taken
        /// over: true when the two stand on their marks and the v2 core has them.
        /// </summary>
        internal IEnumerator V2SetupDuel(int stage, int[] family, int[] tier, System.Action<bool> done)
        {
            DuelCoreV2.Running = false;
            Time.timeScale = 1f;
            if (challengeRun) { EndChallenge(); yield return null; }
            TestStage = Mathf.Clamp(stage, 0, DuelStages.Length - 1);
            var mine = V2Weapon(family[0], tier[0]);
            if (mine != null) { PlayerPrefs.SetInt("sb_weapon_own_" + mine.id, 1); Armory.Equip(mine); }
            ShowMenu();
            yield return new WaitForSeconds(0.5f);
            if (chUi == null) InitChallenge();
            chMode = 1; chDuelMinutes = 3;
            StartChallenge(false);
            float until = Time.realtimeSinceStartup + 45f;
            while (!(chPhase == ChPhase.Lobby && duelCountdown) && Time.realtimeSinceStartup < until)
            {
                if (ui.Briefing.IsOpen) ui.Briefing.SendMessage("Finish");
                V2HideScreens();
                yield return null;
            }
            if (!(chPhase == ChPhase.Lobby && duelCountdown)) { done(false); yield break; }
            // the takeover: the game's own duel stops where it stands
            cameraRig.StopAllCoroutines();
            DuelCoreV2.Running = true;
            Time.timeScale = 1f;
            hunt.Freeze();
            robot.enabled = false;
            // the rival's weapon: the chosen family and tier; a shield on its arm as foi has
            var theirs = V2Weapon(family[1], tier[1]);
            var foeSkin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
            if (foeSkin != null && theirs != null)
            {
                var look = foeSkin.transform.parent != null ? foeSkin.transform.parent : foeSkin.transform;
                foreach (var h in look.GetComponentsInChildren<HeldWeapon>(true)) Destroy(h.gameObject);
                yield return null;
                rivalHeld = HeldWeapon.Attach(look, theirs);
                ShieldHold.Attach(look);
            }
            V2HideScreens();
            done(true);
        }

        /// <summary>The prototype gives the duel back (to the menu).</summary>
        internal void V2Release()
        {
            DuelCoreV2.Running = false;
            Time.timeScale = 1f;
            if (robot != null) robot.enabled = true;
            if (hunt != null) hunt.Resume();
            if (challengeRun) EndChallenge();
            ShowMenu();
            V2HideScreens();
        }

        /// <summary>A shop weapon of this family, at the league tier's weapon tier (2..6).</summary>
        private static WeaponDef V2Weapon(int family, int leagueTier)
        {
            int want = Mathf.Clamp(leagueTier + 1, 1, 6);
            WeaponDef best = null;
            foreach (var w in Armory.All)
            {
                if (w.chest || w.Special || (int)Armory.Family(w) != family) continue;
                if (best == null || Mathf.Abs(w.tier - want) < Mathf.Abs(best.tier - want)) best = w;
            }
            return best;
        }

        // ---------- What the v2 core works with ----------

        /// <summary>foi's root (moved along the line) and its look; the rival's.</summary>
        internal Transform V2MeRoot => robot != null ? robot.transform : null;
        internal Transform V2MeLook => robot != null ? robot.Visual : null;
        internal Transform V2FoeRoot => hunt != null ? hunt.ChallengeFoeModel : null;
        internal HeldWeapon V2MeWeapon => heldWeapon;
        internal HeldWeapon V2FoeWeapon => rivalHeld;
        /// <summary>The board's middle (on the floor), the fighters' line (the screen's left to right) and the camera's side.</summary>
        internal Vector3 V2Centre => boardCentre;
        internal Vector3 V2Line => Vector3.forward;
        internal Vector3 V2CameraSide => sideDir;
        internal Camera V2GameCamera => cameraRig != null ? cameraRig.Cam : null;
        internal Transform V2Stage => colosseum != null ? colosseum.transform : null;
        internal static int V2StageCount => DuelStages.Length;
        internal static string V2StageName(int i) => Loc.T(DuelStages[Mathf.Clamp(i, 0, DuelStages.Length - 1)].key);
        internal float V2FighterScale => robot != null && robot.Visual != null ? robot.Visual.lossyScale.y : 1f;

        /// <summary>The game's sparks and rings, for the v2 fight's pictures.</summary>
        internal void V2Burst(Vector3 at, Color color, Color glow, int count, float speed) { if (fx != null) fx.Burst(at, color, glow, count, speed); }
        internal void V2Ring(Vector3 at, float size, Color color) => Shockwave.Create(at, size, color);
        internal void V2Cheer(int side, float strength) { if (colosseum != null) colosseum.Cheer(side, strength); }
        internal void V2Dust(Vector3 at, Color color, int count, float speed) { if (fx != null) fx.Dust(at, color, count, speed); }
    }
}
