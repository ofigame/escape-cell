using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The player's profile: a user name picked when the game first reaches its menu (it can be skipped; then it is asked
    /// again next time), shown over foi everywhere — on the floors too — and changed on the profile screen (the menu's
    /// rank card) once a week.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The name screen is up for the profile (not on the way into a challenge).</summary>
        private bool profileNaming;
        private bool profilePrompted, profileWired;
        private SquashBot.Visual.NameTag levelTag;

        /// <summary>The menu is up: the first time, the name is asked for (if there is none yet).</summary>
        private void ProfileOnMenu()
        {
            if (!profileWired)
            {
                profileWired = true;
                ui.ChallengeStats.RenamePressed += RenameProfile;
                ui.SquadPressed += ShowSquadPick;
            }
            if (levelTag != null) levelTag.gameObject.SetActive(false);
            if (profilePrompted || Challenge.NameChosen) return;
            profilePrompted = true;
            if (chUi == null) InitChallenge();
            profileNaming = true;
            chUi.ShowName(Challenge.PlayerName);
        }

        /// <summary>CHANGE NAME on the profile: the name screen (or, within the week, how long until it can be changed).</summary>
        private void RenameProfile()
        {
            if (Challenge.NameChosen && Challenge.RenameDays > 0)
            {
                ui.ShowIntro(Loc.T("profile.title"), Loc.F("profile.renameWait", Challenge.RenameDays));
                return;
            }
            if (chUi == null) InitChallenge();
            ui.ChallengeStats.Hide();
            profileNaming = true;
            chUi.ShowName(Challenge.PlayerName);
        }

        /// <summary>The name screen's answer when it was opened for the profile: back to the menu (the profile shown).</summary>
        private void ProfileNamed()
        {
            profileNaming = false;
            chUi.HideName();
            if (levelTag != null) levelTag.SetText(Challenge.PlayerName);
            ui.RefreshMenuRank(ui.MenuDefenseLine);
        }

        /// <summary>A floor starts: the player's name over foi (the duel has its own tags).</summary>
        private void ShowLevelTag()
        {
            if (challengeRun) { if (levelTag != null) levelTag.gameObject.SetActive(false); return; }
            if (levelTag == null) levelTag = SquashBot.Visual.NameTag.Create(robot.transform, Challenge.PlayerName, new Color(1f, 0.9f, 0.55f), 1.25f);
            levelTag.SetText(Challenge.PlayerName);
            levelTag.gameObject.SetActive(true);
        }
    }
}
