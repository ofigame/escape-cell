using System.Collections;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The duel's events: the weekly rule mode (Monday to Thursday) and the weekend tournament (Saturday and Sunday), both
    /// equal power, against the AI, without rank points. The week's rule is put on the duel as it is built.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The duel being played: 1 the usual, 2 the weekly rule mode, 3 the weekend tournament.</summary>
        private int chMode = 1;
        private WeeklyMode.Rule? duelRule;
        private WeaponDef weaponBeforeRule;

        private bool EventMatch => chMode != 1;

        /// <summary>The weekly rule duel or the tournament's next round: a bot rival at the player's trophies, then the board.</summary>
        private IEnumerator EventDuel(int mode)
        {
            if (chUi == null) InitChallenge();
            if (challengeRun) yield break;
            if (mode == 3 && (Tournament.Out || Tournament.Champion)) { ui.ShowIntro(Loc.T("tn.title"), Loc.T(Tournament.Champion ? "tn.done" : "tn.out")); yield break; }
            chMode = mode;
            twoVsTwo = mode == 4;
            duelRule = mode == 2 ? WeeklyMode.ThisWeek : (WeeklyMode.Rule?)null;
            chCancelled = false; chFriend = 0; chOnline = false; chLive = false; chMatchId = 0; copyMatch = false;
            chDuelMinutes = mode == 3 ? 2 : duelRule == WeeklyMode.Rule.SuddenDeath ? 1 : 3;
            int seed = mode == 3 ? Tournament.SeedFor(Tournament.Round) : Random.Range(0, 1000000);
            rival = Challenge.MakeBot(seed, -1);
            chPhase = ChPhase.Lobby;
            int token = ++chLobbyToken;
            string label = mode == 3 ? Loc.T("tn.title") + " · " + Loc.T("tn.round" + Tournament.Round) : mode == 4 ? Loc.T("tvt.title") : Loc.T("wk.title") + " · " + Loc.T("wk.rule." + duelRule);
            chUi.ShowLobby(MyCard(), Challenge.Rank, label);
            chUi.ShowRival(RivalCard(rival), rival.Rank);
            chUi.LockLobby();
            chUi.SetLobbyStatus(mode == 3 ? Loc.T("tn.round" + Tournament.Round) : mode == 4 ? Loc.T("tvt.text") : Loc.T("wk.rule." + duelRule + ".d"));
            yield return new WaitForSecondsRealtime(1.8f);
            if (token != chLobbyToken) yield break;
            int stage = Mathf.Abs(seed) % DuelStageCount;
            yield return SpinToStage(stage);
            if (token != chLobbyToken) yield break;
            chUi.HideLobby();
            BuildDuelBoard(stage);
        }

        /// <summary>The week's rule on the duel (called as the board is built, after the move sets are loaded).</summary>
        private void ApplyDuelRule()
        {
            if (duelRule == null) return;
            switch (duelRule.Value)
            {
                case WeeklyMode.Rule.OneWeapon:
                    weaponBeforeRule = weapon;
                    weapon = WeeklyMode.Weapon;
                    rival.weapon = WeeklyMode.Weapon.id;
                    ShowHeld(weapon);
                    break;
                case WeeklyMode.Rule.Heartless:
                    duelHearts = rivalHearts = 0;
                    break;
                case WeeklyMode.Rule.ShieldWar:
                    duelShields = rivalShields = 6;
                    break;
                case WeeklyMode.Rule.RandomSet:
                    mySpecial = (SpecialKind)Random.Range(0, 8); myUlti = (UltiKind)Random.Range(0, 9); myPassive = (PassiveKind)Random.Range(1, 10);
                    rivalSpecial = (SpecialKind)Random.Range(0, 8); rivalUlti = (UltiKind)Random.Range(0, 9); rivalPassive = (PassiveKind)Random.Range(1, 10);
                    break;
                case WeeklyMode.Rule.GiantFoi:
                    robot.SetSize(1.5f);
                    break;
            }
        }

        /// <summary>The duel is over: the rule's changes undone.</summary>
        private void ClearDuelRule()
        {
            if (duelRule == WeeklyMode.Rule.OneWeapon) { weapon = weaponBeforeRule; weaponBeforeRule = null; }
            duelRule = null;
            chMode = 1;
            if (twoVsTwo) { twoVsTwo = false; hunt.ClearSquads(); }
        }

        // The rules in the numbers
        private float UltiCost => duelRule == WeeklyMode.Rule.QuickUlti ? 2f : 3f;
        private float RuleComboGap => duelRule == WeeklyMode.Rule.ComboFrenzy ? 2f : 1f;
        private float RuleDamageTimes => duelRule == WeeklyMode.Rule.GiantFoi ? 2f : 1f;
        private bool SuddenDeath => duelRule == WeeklyMode.Rule.SuddenDeath;

        /// <summary>An event duel's end: the week's wins counted, the tournament moved on. Returns a line for the card (or null).</summary>
        private string SettleEvent(bool won)
        {
            if (chMode == 2 && won)
            {
                int step = WeeklyMode.RecordWin();
                return step >= 0 ? Loc.T("wk.gift" + step) : Loc.F("wk.progress", WeeklyMode.Wins);
            }
            if (chMode == 3)
            {
                string gift = Tournament.Record(won);
                return gift != null ? Loc.T(gift) : Loc.T("tn.out");
            }
            return null;
        }
    }
}
