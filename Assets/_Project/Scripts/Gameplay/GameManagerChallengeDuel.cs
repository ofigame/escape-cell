using System.Collections;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The challenge's duel after the armies: three rounds, foi against the rival foi, every round played. The armies'
    /// winner has 1 point, each round 1 more (4 in all; 2-2 is a draw). Between rounds both foi go back to their marks,
    /// foi's health is full again and a fresh rival foi steps in.
    /// </summary>
    public partial class GameManager
    {
        private const int DuelRounds = 3;

        /// <summary>Hearts each foi has for the whole duel (each fills <see cref="HeartHeal"/> of the health).</summary>
        private const int DuelHearts = 5;
        private int chHearts;

        /// <summary>The heart button: part of foi's health back (only in a round, and only when some is missing).</summary>
        private void OnDuelHeart()
        {
            if (!challengeRun || chPhase != ChPhase.Duel || State != GameState.Playing || chHearts <= 0 || health >= 0.999f) return;
            chHearts--;
            health = Mathf.Min(1f, health + HeartHeal);
            RefreshHealthBar();
            fx.Burst(robot.transform.position + Vector3.up * 0.6f, new Color(1f, 0.35f, 0.45f), new Color(2.4f, 0.6f, 0.8f), 26, 4f);
            AudioManager.PlaySfx(Sfx.Coin, 0.9f, 1.4f);
            Haptics.Light();
            ui.SetDuelHearts(true, chHearts);
        }

        /// <summary>The round being fought (1..3), and the rounds won by each side so far.</summary>
        private int chRound, chDuelMe, chDuelThem;

        /// <summary>Where the two foi stand when a round starts: the courtyard's middle, or the stage battle's oval.</summary>
        private (GridPos mine, GridPos theirs) DuelMarks =>
            chMode == ChMode.Stages ? (new GridPos(StMid, MidC - 2), new GridPos(StMid, MidC + 2)) : (new GridPos(BoardW / 2, BoardH / 2 - 2), RivalTile);

        /// <summary>The rival foi for a round: its look and weapon, its health and blows from its profile.</summary>
        private void AddRivalFoe(GridPos at, Transform look, HeldWeapon held)
        {
            var w = Armory.Get(rival.weapon);
            int hp = Mathf.RoundToInt(rival.foiHp / Mathf.Max(0.4f, 1f - rival.armor * 0.8f));
            float hit = Mathf.Clamp(rival.foiHit * (0.85f + 0.12f * Armory.Damage(w, Mathf.Max(1, rival.weaponLevel))), 0.06f, 0.35f);
            hunt.FoeBoldness = rival.boldness;
            hunt.ChallengeBattle = true;
            hunt.AddChallengeFoe(at, hp, hit, look, held, rival.shield);
        }

        /// <summary>The duel's first round begins (the score so far: the armies').</summary>
        private void BeginDuelRounds()
        {
            chRound = 1;
            chDuelMe = chDuelThem = 0;
            chHearts = DuelHearts;
            hunt.FoeHearts = DuelHearts;
            hunt.FoeHeartHeal = HeartHeal;
            ui.SetDuelHearts(true, chHearts);
            liveDuelStart = Time.time;
            chUi.SetClock(ScoreText());
        }

        /// <summary>A round is over (the rival foi down, or foi's health gone): the next one, or the final score.</summary>
        private void DuelRoundOver(bool won)
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            if (won) chDuelMe++; else chDuelThem++;
            if (LiveActive) LiveNet.Duel(chRound, won, Time.time - liveDuelStart);
            chPhase = ChPhase.Break;
            hunt.Freeze();
            ui.SetDuelHearts(chRound < DuelRounds, chHearts);
            chUi.SetClock(ScoreText());
            chUi.SetStatus(Loc.F(won ? "ch.roundWon" : "ch.roundLost", chRound));
            if (colosseum != null) colosseum.Cheer(won ? 0 : 1, 0.9f);
            AudioManager.PlaySfx(won ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
            if (won) fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 24, 5f);
            if (chRound >= DuelRounds)
            {
                if (LiveActive) LiveAwaitResult();
                else ChallengeEnd();
                return;
            }
            cameraRig.StartCoroutine(NextRound());
        }

        private IEnumerator NextRound()
        {
            yield return new WaitForSeconds(2.2f);
            while (challengeRun && State == GameState.Paused) yield return null;
            if (!challengeRun || chPhase != ChPhase.Break || State != GameState.Playing) yield break;
            hunt.RemoveChallengeFoe(); // a rival foi still standing leaves the ring
            chRound++;
            health = 1f;
            RefreshHealthBar();
            var (mine, theirs) = DuelMarks;
            if (!grid.IsStandable(mine) || grid.IsOccupied(mine)) mine = SafeTileNear(mine, mine);
            robot.Spawn(grid, mine);
            robot.transform.rotation = Quaternion.identity; // facing the rival across the middle
            var look = RivalLook();
            look.position = GridView.ToWorld(theirs) + Vector3.up * GridView.SurfaceY;
            look.rotation = Quaternion.Euler(0f, 180f, 0f);
            look.SetParent(null, true);
            AddRivalFoe(theirs, look, rivalHeld);
            hunt.Resume();
            chPhase = ChPhase.Duel;
            liveDuelStart = Time.time;
            chUi.SetClock(ScoreText());
            chUi.SetStatus(Loc.F("ch.round", chRound, DuelRounds));
            ui.ShowIntro(Loc.F("ch.round", chRound, DuelRounds), Loc.T("ch.duelText"));
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 0.9f);
        }

        /// <summary>The score as the board shows it (mine first): the armies' point and the rounds won.</summary>
        private string ScoreText() => ScoreMine + " - " + ScoreTheirs;

        private int ScoreMine => (chLead > 0 ? 1 : 0) + chDuelMe;
        private int ScoreTheirs => (chLead < 0 ? 1 : 0) + chDuelThem;
    }
}
