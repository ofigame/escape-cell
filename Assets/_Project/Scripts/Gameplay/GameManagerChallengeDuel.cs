using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The challenge's Duel: foi against the rival foi alone in the arena for 3, 5 or 10 minutes. Whoever puts the other
    /// down scores a point and both start again from their marks in the middle; when time is up the higher score wins
    /// (level: a draw). Each blow takes 1% of the other's health per point of its weapon's damage (armour takes some
    /// off). The player is on the left (the south mark, as the camera on the east stand sees it), the rival on the right;
    /// names float over both.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The duel's length in minutes (3, 5 or 10).</summary>
        private int chDuelMinutes = 3;

        /// <summary>Knock-downs scored by each side, and the time left (seconds).</summary>
        private int chDuelMe, chDuelThem;
        private float chDuelLeft;
        private NameTag myTag, theirTag;

        /// <summary>The marks the two foi start from: either side of the middle, the player's to the south (left on screen).</summary>
        private (GridPos mine, GridPos theirs) DuelMarks
        {
            get
            {
                // A live match: the same arena on both screens — the server's side 0 to the south (left), side 1 to the north.
                GridPos south = new GridPos(BoardW / 2, BoardH / 2 - 3), north = new GridPos(BoardW / 2, BoardH / 2 + 3);
                return chLive && liveSide == 1 ? (north, south) : (south, north);
            }
        }

        /// <summary>The share of a foi's health one blow of this weapon takes: 1% per point of damage, less its armour.</summary>
        private static float DuelBlow(WeaponDef w, int weaponLevel, float armor) =>
            0.01f * Armory.Damage(w, Mathf.Max(1, weaponLevel)) * (1f - Mathf.Clamp01(armor));

        /// <summary>The Duel's courtyard: no armies, the two foi on their marks, the clock running.</summary>
        private void BuildDuelBoard(int world)
        {
            BuildCourtyard(world);
            chDuelMe = chDuelThem = 0;
            chDuelLeft = chDuelMinutes * 60f;
            if (army != null) army.Clear();
            if (rivalLook != null) Destroy(rivalLook.gameObject);
            rivalLook = null;
            if (robot.Visual != null) robot.Visual.gameObject.SetActive(true);
            chUi.ShowBattle();
            chUi.HideGroups();
            ui.SetHudTitle(Loc.T("ch.mode.duel"));
            PlaceDuelists();
            hunt.Freeze(); // nobody moves while the briefing is read
            chPhase = ChPhase.Lobby; // the briefing first
            cameraRig.StartCoroutine(ChallengeBriefing("duel", 3, () => { if (LiveActive) LiveWaitFor(LiveWait.Start); else BeginDuel(); }));
            AudioManager.PlaySfx(Sfx.Win, 0.6f, 0.9f);
        }

        private void BeginDuel()
        {
            if (!challengeRun) return;
            chPhase = ChPhase.Duel;
            duelPan = robot.transform.position - boardCentre; duelPan.y = 0f;
            hunt.Resume();
            chUi.SetStatus(Loc.T("ch.duel"));
            ui.ShowIntro(Loc.T("ch.duel"), Loc.F("ch.duelMinutes", chDuelMinutes));
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 0.8f);
            RefreshDuelClock();
        }

        /// <summary>Both foi on their marks, full health, a fresh rival foi (its look, weapon and health from its profile).</summary>
        private void PlaceDuelists()
        {
            hunt.RemoveChallengeFoe();
            health = 1f;
            RefreshHealthBar();
            var (mine, theirs) = DuelMarks;
            robot.Spawn(grid, mine);
            bool north = theirs.y < mine.y;
            robot.transform.rotation = Quaternion.identity;
            robot.Visual.rotation = Quaternion.Euler(0f, north ? 180f : 0f, 0f); // facing the rival across the middle
            var look = RivalLook();
            look.position = GridView.ToWorld(theirs) + Vector3.up * GridView.SurfaceY;
            look.rotation = Quaternion.Euler(0f, north ? 0f : 180f, 0f);
            hunt.FoeRemote = LiveActive; // a live rival: its own game moves it, the server keeps its health
            var w = Armory.Get(rival.weapon);
            hunt.FoeBoldness = rival.boldness;
            hunt.ChallengeBattle = true;
            // 100 health points behind its armour; foi's blows take its weapon's damage in points (1% each).
            int hp = LiveActive ? 100 : Mathf.RoundToInt(100f / Mathf.Max(0.5f, 1f - rival.armor));
            // The AI swings about once a second while a player swings as fast as the weapon allows: each of its blows
            // weighs what that weapon does in a second, so it fights as hard as a player with it would.
            float rate = Mathf.Clamp(1f / Mathf.Max(0.1f, Armory.Cooldown(w, Mathf.Max(1, rival.weaponLevel))), 1f, 5f);
            hunt.AddChallengeFoe(theirs, hp, DuelBlow(w, rival.weaponLevel, Shop.ArmorShare) * (LiveActive ? 1f : rate), look, rivalHeld, 0);
            if (myTag == null) myTag = SquashBot.Visual.NameTag.Create(robot.transform, Challenge.PlayerName, ArmyBattle.Mine);
            if (theirTag != null) Destroy(theirTag.gameObject);
            theirTag = SquashBot.Visual.NameTag.Create(look, rival.name, ArmyBattle.Theirs);
        }

        /// <summary>A knock-down: the one left standing scores, both start again from the middle.</summary>
        private void DuelKnockdown(bool iWon)
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            if (iWon) chDuelMe++; else chDuelThem++;
            chPhase = ChPhase.Break;
            hunt.Freeze();
            RefreshDuelClock();
            chUi.SetStatus(Loc.T(iWon ? "ch.koYou" : "ch.koThem"));
            if (colosseum != null) colosseum.Cheer(iWon ? 0 : 1, 0.9f);
            AudioManager.PlaySfx(iWon ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
            if (iWon) fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 24, 5f);
            cameraRig.StartCoroutine(AfterKnockdown());
        }

        /// <summary>A live knock-down, as the server tells it: its score; both start again when it says.</summary>
        private void LiveKnockdown(bool iWon, int mine, int theirs)
        {
            if (!challengeRun || (chPhase != ChPhase.Duel && chPhase != ChPhase.Wait)) return;
            chDuelMe = mine;
            chDuelThem = theirs;
            chPhase = ChPhase.Break;
            hunt.Freeze();
            if (iWon) hunt.RemoveChallengeFoe(); else { health = 0f; RefreshHealthBar(); robot.Squash(); }
            RefreshDuelClock();
            chUi.SetStatus(Loc.T(iWon ? "ch.koYou" : "ch.koThem"));
            if (colosseum != null) colosseum.Cheer(iWon ? 0 : 1, 0.9f);
            AudioManager.PlaySfx(iWon ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
            if (iWon) fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 24, 5f);
        }

        /// <summary>The server's word to start (again): both on their marks, the clock as it says.</summary>
        private void LiveDuelGo(float secs)
        {
            if (!challengeRun) return;
            chDuelLeft = secs;
            PlaceDuelists();
            hunt.Resume();
            chPhase = ChPhase.Duel;
            duelPan = robot.transform.position - boardCentre; duelPan.y = 0f;
            chUi.SetStatus(Loc.T("ch.duel"));
            RefreshDuelClock();
        }

        private System.Collections.IEnumerator AfterKnockdown()
        {
            yield return new WaitForSeconds(1.8f);
            while (challengeRun && State == GameState.Paused) yield return null;
            if (!challengeRun || chPhase != ChPhase.Break || State != GameState.Playing) yield break;
            if (chDuelLeft <= 0f) { ChallengeEnd(); yield break; }
            PlaceDuelists();
            hunt.Resume();
            chPhase = ChPhase.Duel;
            chUi.SetStatus(Loc.T("ch.duel"));
        }

        /// <summary>Each frame of the duel: the clock (it only runs while the two fight), a knock-down, time up.</summary>
        private void UpdateDuel(float dt)
        {
            chDuelLeft = Mathf.Max(0f, chDuelLeft - dt);
            RefreshDuelClock();
            if (LiveActive) return; // the server keeps the score and the clock
            if (hunt.ChallengeFoeDown) { DuelKnockdown(true); return; }
            if (chDuelLeft <= 0f) { chPhase = ChPhase.Break; hunt.Freeze(); ChallengeEnd(); }
        }

        private void RefreshDuelClock()
        {
            int s = Mathf.CeilToInt(chDuelLeft);
            chUi.SetClock(ScoreText() + "   " + s / 60 + ":" + (s % 60).ToString("00"));
        }

        /// <summary>The score as the board shows it (mine first).</summary>
        private string ScoreText() => ScoreMine + " - " + ScoreTheirs;

        private int ScoreMine => chMode == ChMode.Duel ? chDuelMe : chArmyMe;
        private int ScoreTheirs => chMode == ChMode.Duel ? chDuelThem : chArmyThem;

        private void ClearDuel()
        {
            if (myTag != null) Destroy(myTag.gameObject);
            if (theirTag != null) Destroy(theirTag.gameObject);
            myTag = theirTag = null;
        }
    }
}
