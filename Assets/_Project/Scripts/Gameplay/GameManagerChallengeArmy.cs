using System.Collections;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The challenge's Army Battle: three matches, each begun with both armies whole. The player leads one group at a time
    /// (the bar of six at the bottom picks it): the stick moves it, a tap shows where to attack. A match is won by the
    /// side that leaves the other without a soldier; after three minutes the side with more soldiers standing takes it
    /// (level: nobody). After three matches the higher score wins. Against the AI the matches run here; live, the server
    /// starts and ends them (<see cref="GameManager.UpdateLive"/>).
    /// </summary>
    public partial class GameManager
    {
        private ArmyBattle army;
        private int chArmyMe, chArmyThem, chArmyRound;
        private float chArmyLeft;
        private bool chArmyWiped;
        private int armyWorld;

        private const int ArmyRounds = 3;
        private const float ArmyRoundSeconds = 180f;

        /// <summary>The Army Battle's board: the courtyard, the two armies along its ends (foi is not on it).</summary>
        private void BuildArmyBoard(int world)
        {
            armyWorld = world;
            BuildCourtyard(world);
            chArmyMe = chArmyThem = 0;
            chArmyRound = 0;
            ClearDuel();
            robot.Spawn(grid, new GridPos(BoardW / 2, 1));
            robot.Visual.gameObject.SetActive(false); // the armies fight; foi watches from the stands
            hunt.Freeze();
            SetUpArmies();
            chUi.ShowBattle();
            chUi.ShowArmyBar(army.AliveByGroup(0), 0);
            RefreshArmySides();
            ui.SetHudTitle(Loc.T("ch.mode.army"));
            chPhase = ChPhase.Lobby; // the briefing first
            cameraRig.StartCoroutine(ChallengeBriefing("army", 3, () =>
            {
                if (LiveActive) LiveWaitFor(LiveWait.Start); // the server starts the first match
                else StartArmyRound(1);
            }));
            AudioManager.PlaySfx(Sfx.Win, 0.6f, 0.9f);
        }

        /// <summary>Both armies whole in their places: mine along the south end, the rival's along the north.</summary>
        private void SetUpArmies()
        {
            var mine = new Vector3[Army.Groups];
            var theirs = new Vector3[Army.Groups];
            var myPower = new float[Army.Groups];
            var theirPower = new float[Army.Groups];
            // Each army in two lines of three: the swords and the light spears in front, the heavy spears and the archers behind.
            float[] xs = { 5f, 12f, 19f, 5f, 12f, 19f };
            float[] lineZ = { 5.2f, 5.2f, 5.2f, 2.4f, 2.4f, 2.4f };
            var origin = GridView.ToWorld(new GridPos(0, 0)) + Vector3.up * GridView.SurfaceY;
            // A live match: the same arena on both screens — the server's side 0 at the south end, side 1 at the north.
            bool north = LiveActive && liveSide == 1;
            for (int g = 0; g < Army.Groups; g++)
            {
                var south = origin + new Vector3(xs[g], 0f, lineZ[g]);
                var far = origin + new Vector3(xs[g], 0f, BoardH - 1 - lineZ[g]);
                mine[g] = north ? far : south;
                theirs[g] = north ? south : far;
                myPower[g] = Army.Power(g);
                theirPower[g] = rival.Power(g);
            }
            army.RivalBoldness = rival.boldness;
            army.Begin(mine, theirs, myPower, theirPower, hunt.FoiHeight * 0.8f, LiveActive,
                GridView.ToWorld(new GridPos(1, 1)) - new Vector3(0.3f, 0f, 0.3f), GridView.ToWorld(new GridPos(BoardW - 2, BoardH - 2)) + new Vector3(0.3f, 0f, 0.3f), north);
        }

        /// <summary>A match begins (both armies whole again after the first).</summary>
        private void StartArmyRound(int n)
        {
            if (!challengeRun) return;
            chArmyRound = n;
            if (n > 1) SetUpArmies();
            chArmyLeft = ArmyRoundSeconds;
            chArmyWiped = false;
            chPhase = ChPhase.Army;
            army.Go();
            chUi.ShowArmyBar(army.AliveByGroup(0), army.Active(0));
            chUi.SetStatus(Loc.F("ch.armyRound", n, ArmyRounds));
            ui.ShowIntro(Loc.F("ch.armyRound", n, ArmyRounds), Loc.T("ch.armyHint"));
            if (colosseum != null) colosseum.Cheer(-1, 0.6f);
            AudioManager.PlaySfx(Sfx.Warning, 0.7f, 0.9f);
            RefreshArmySides();
        }

        /// <summary>The stick and taps lead the active group (the Army Battle has no foi to move).</summary>
        private void ArmyInput(InputCommand command)
        {
            if (army == null || chPhase != ChPhase.Army) { if (army != null) army.Move(Vector3.zero); return; }
            var vel = Vector3.zero;
            if (command.stick.HasValue)
            {
                var cam = cameraRig.Cam.transform;
                var f = cam.forward; f.y = 0f;
                var r = cam.right; r.y = 0f;
                var s = command.stick.Value;
                float push = s.magnitude;
                if (push >= 0.2f) vel = (r.normalized * s.x + f.normalized * s.y).normalized * Mathf.Lerp(1.4f, 2.6f, Mathf.InverseLerp(0.2f, 1f, push));
            }
            army.Move(vel);
            if (command.tap.HasValue)
            {
                var ray = cameraRig.Cam.ScreenPointToRay(command.tap.Value);
                var ground = new Plane(Vector3.up, new Vector3(0f, GridView.SurfaceY, 0f));
                if (ground.Raycast(ray, out float d))
                {
                    var at = ray.GetPoint(d);
                    army.AttackAt(at);
                    Shockwave.Create(at, 0.8f, new Color(1f, 0.85f, 0.3f)); // where the attack goes
                    AudioManager.PlaySfx(Sfx.Click, 0.6f, 1.3f);
                }
                input.CancelTap();
            }
        }

        /// <summary>Each frame of a match: the clock, the counts, a side wiped out.</summary>
        private void UpdateArmy(float dt)
        {
            chArmyLeft = Mathf.Max(0f, chArmyLeft - dt);
            int s = Mathf.CeilToInt(chArmyLeft);
            chUi.SetClock(ScoreText() + "   " + s / 60 + ":" + (s % 60).ToString("00"));
            chUi.ShowArmyBar(army.AliveByGroup(0), army.Active(0));
            RefreshArmySides();
            int mine = army.Alive(0), theirs = army.Alive(1);
            if (LiveActive)
            {
                // The server ends the match: this side only tells it when it has no soldier left.
                if (mine == 0 && !chArmyWiped) { chArmyWiped = true; LiveNet.Wiped(); }
                return;
            }
            if (mine == 0) ArmyRoundOver(-1);
            else if (theirs == 0) ArmyRoundOver(1);
            else if (chArmyLeft <= 0f) ArmyRoundOver(mine > theirs ? 1 : theirs > mine ? -1 : 0);
        }

        private void RefreshArmySides()
        {
            if (army == null) return;
            chUi.SetSides(Challenge.PlayerName, Challenge.Rank, army.Alive(0), rival.name, rival.Rank, army.Alive(1));
        }

        /// <summary>A match is over (+1 mine, -1 theirs, 0 nobody's): the score, then the next match or the end.</summary>
        private void ArmyRoundOver(int winner)
        {
            if (chPhase != ChPhase.Army) return;
            if (winner > 0) chArmyMe++;
            else if (winner < 0) chArmyThem++;
            ShowArmyRoundEnd(winner);
            if (LiveActive) return; // the server starts the next match (or ends the battle)
            cameraRig.StartCoroutine(NextArmyRound());
        }

        private void ShowArmyRoundEnd(int winner)
        {
            chPhase = ChPhase.Break;
            army.Halt();
            chUi.SetClock(ScoreText());
            chUi.SetStatus(Loc.T(winner > 0 ? "ch.matchWon" : winner < 0 ? "ch.matchLost" : "ch.matchEven"));
            if (colosseum != null) colosseum.Cheer(winner > 0 ? 0 : winner < 0 ? 1 : -1, 1f);
            AudioManager.PlaySfx(winner >= 0 ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
        }

        private IEnumerator NextArmyRound()
        {
            yield return new WaitForSeconds(3f);
            while (challengeRun && State == GameState.Paused) yield return null;
            if (!challengeRun || chPhase != ChPhase.Break || State != GameState.Playing) yield break;
            if (chArmyRound >= ArmyRounds) { ChallengeEnd(); yield break; }
            StartArmyRound(chArmyRound + 1);
        }
    }
}
