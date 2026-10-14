using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The challenge's second kind: Meydan Savaşı (the stage battle). Three stages in a line like the floors' fortress
    /// stages: the player's stage B at the south end, the round middle stage, the rival's stage A at the north end, each
    /// side joined to the middle by a gated bridge. On stage B foi fights two soldiers of each of the rival's six groups;
    /// on stage A the rival foi (its AI) fights two of each of the player's at the same time. Whoever clears their stage
    /// first goes 1-0 up; both gates open, the soldiers still standing leave, and the two foi fight it out in the middle
    /// (2-0, or a 1-1 draw). foi falling on its own stage gives the rival the point.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Which kind of challenge is being played.</summary>
        private enum ChMode { Arena, Stages }

        private ChMode chMode = ChMode.Arena;
        private bool StageMode => challengeRun && chMode == ChMode.Stages;
        private float stageSidesNext;

        private const int StW = 15;
        // Rows from the south (y = 0): stage B, its bridge (gate on the middle row), the round middle, A's bridge, stage A.
        private const int B0 = 0, B1 = 13, MidC = 23, A0 = 33, A1 = 46, StH = A1 + 1;

        private static string[] StageLayout()
        {
            var rows = new string[StH];
            for (int y = 0; y < StH; y++)
            {
                var row = new char[StW];
                for (int x = 0; x < StW; x++)
                {
                    char c = '.';
                    if ((y >= B0 && y <= B1) || (y >= A0 && y <= A1)) c = '#';
                    else if ((y > B1 && y < MidC - 6) || (y > MidC + 6 && y < A0)) c = x >= 6 && x <= 8 ? (y == B1 + 2 || y == A0 - 2 ? 'G' : '#') : '.';
                    else
                    {
                        float dx = (x - 7) / 7.4f, dy = (y - MidC) / 6.6f;
                        if (dx * dx + dy * dy <= 1f) c = '#';
                    }
                    if (x == 7 && y == 2) c = 'S';
                    row[x] = c;
                }
                rows[StH - 1 - y] = new string(row); // the layout's first row is the north end
            }
            return rows;
        }

        /// <summary>The stage battle's floor, the two crowds and the rival foi on its stage.</summary>
        private void BuildStageBoard(int world)
        {
            levelIndex = Mathf.Clamp(world * LevelCatalog.LevelsPerWorld + 5, 0, LevelCount - 1);
            var l = levelSet.levels[levelIndex].Clone();
            l.layout = StageLayout();
            l.stageBounds = new[] { 0, 0, StW - 1, StH - 1 };
            l.fortressStages = 1;
            l.gridWidth = StW;
            l.gridHeight = StH;
            l.terrain = TerrainKind.Flat;
            l.mission = MissionType.Hunt;
            l.hard = false;
            l.marathon = false;
            l.final = false;
            l.robots = 0; l.robotsAtOnce = 0; l.elites = 0; l.hardAtOnce = 0; l.brutes = 0; l.towers = 0; l.bugs = 0;
            l.monsterHp = 1;
            Calm(l);
            level = l;
            challengeRun = true;
            bonusRun = true;
            arenaRun = false;
            trailRun = false;
            dailyRun = false;
            BeginRun();
            ui.SetChallengeHud(true);
            ui.SkillBar.Refresh(false);
            hazards.Freeze();
            fifiReadyAt = Time.time + 99999f;
            introPending = false;
            chLead = 0;
            DestroyCells();
            if (colosseum != null) { Destroy(colosseum.gameObject); colosseum = null; }

            // Stage B: the rival's soldiers, two of each group, in two lines across its north half.
            int[] xs = { 2, 4, 6, 8, 10, 12 };
            for (int g = 0; g < Army.Groups; g++)
                for (int k = 0; k < 2; k++)
                {
                    var kind = Army.KindOf(g);
                    var tile = new GridPos(xs[(g * 2 + k) % 6], k == 0 ? B1 - 2 : B1 - 4 - (g % 2));
                    var model = SoldierModel.Build(hunt.transform, kind, Army.Heavy(kind) ? ClashSystem.Theirs : Color.Lerp(ClashSystem.Theirs, Color.white, 0.3f));
                    model.transform.localScale = Vector3.one * hunt.FoiHeight * 0.8f * (Army.Heavy(kind) ? 1.12f : 1f);
                    model.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    hunt.AddChallengeSoldier(GridView.ToWorld(tile), SoldierHp(kind, rival.Power(g)) + 1, model.transform, model, kind);
                }
            hunt.ChallengeBattle = false; // a few at a time come at foi, as on the floors
            hunt.UseStreetMap = true;

            // Stage A: the player's soldiers against the rival foi.
            var spots = new Vector3[Army.Groups * 2];
            var kinds = new GroupKind[spots.Length];
            var powers = new float[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                int g = i / 2;
                spots[i] = GridView.ToWorld(new GridPos(xs[i % 6], A0 + 3 + (i / 6) * 2)) + Vector3.up * GridView.SurfaceY;
                kinds[i] = Army.KindOf(g);
                powers[i] = Army.Power(g);
            }
            if (rivalLook != null) Destroy(rivalLook.gameObject);
            rivalLook = RivalLook();
            var w = Armory.Get(rival.weapon);
            // About three blows a soldier (more against heavy ones), a little more for a stronger weapon: a stage takes
            // the rival foi about as long as it takes foi.
            float champDamage = 2f + 0.5f * Armory.Damage(w, Mathf.Max(1, rival.weaponLevel));
            clash.BeginSkirmish(spots, kinds, powers, rivalLook, rivalHeld, GridView.ToWorld(new GridPos(7, A1 - 3)) + Vector3.up * GridView.SurfaceY,
                champDamage, 999f, hunt.FoiHeight * 0.8f);
            rivalLook = null; // the stage battle has it now
            clash.SetBounds(GridView.ToWorld(new GridPos(0, A0)) + new Vector3(0.2f, 0f, 0.2f), GridView.ToWorld(new GridPos(StW - 1, A1)) - new Vector3(0.2f, 0f, 0.2f));

            robot.transform.rotation = Quaternion.identity; // facing north, at the rival's soldiers
            // The opening: the camera high over the whole field, from the rival's stage (its foi and the player's soldiers in
            // place) down to foi's; then the word to go.
            chPhase = ChPhase.StageIntro;
            stageIntroT = 0f;
            hunt.Freeze();
            chUi.ShowBattle();
            chUi.HideGroups();
            chUi.SetClock("0 - 0");
            chUi.SetStatus(Loc.T("ch.stagesIntro"));
            ui.SetHudTitle(Loc.T("ch.mode.stages"));
            RefreshStageSides();
            AudioManager.PlaySfx(Sfx.Win, 0.6f, 0.9f);
        }

        /// <summary>A soldier's blows to fell on a stage: heavy ones take more, development adds.</summary>
        private static int SoldierHp(GroupKind kind, float power) => Mathf.Max(1, Mathf.RoundToInt((Army.Heavy(kind) ? 4f : 2.5f) * power));

        private float stageIntroT;
        private const float StageIntroTime = 5.2f;

        /// <summary>The opening fly-over's camera: over stage A (held), sweeping down the field to stage B.</summary>
        private void StageIntroPose(out Vector3 pos, out Quaternion rot)
        {
            stageIntroT += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.4f, StageIntroTime - 0.6f, stageIntroT));
            var a = GridView.ToWorld(new GridPos(StW / 2, (A0 + A1) / 2)) + Vector3.up * GridView.SurfaceY;
            var b = GridView.ToWorld(new GridPos(StW / 2, (B0 + B1) / 2)) + Vector3.up * GridView.SurfaceY;
            var focus = Vector3.Lerp(a, b, k);
            // High and steep over the stages; a little lower as it comes down to foi's.
            pos = focus + new Vector3(0f, Mathf.Lerp(24f, 18f, k), -Mathf.Lerp(10f, 9f, k));
            rot = Quaternion.LookRotation(focus - pos);
            if (stageIntroT >= StageIntroTime && chPhase == ChPhase.StageIntro) BeginStageFight();
        }

        /// <summary>The fly-over is done: both stages' fights begin.</summary>
        private void BeginStageFight()
        {
            chPhase = ChPhase.Stages;
            hunt.Resume();
            clash.StartSkirmish();
            chUi.SetStatus(Loc.T("ch.stagesGo"));
            ui.ShowIntro(Loc.T("ch.mode.stages"), Loc.T("ch.stagesIntro"));
            AudioManager.PlaySfx(Sfx.Warning, 0.8f, 1f);
        }

        private void RefreshStageSides() =>
            chUi.SetSides(Challenge.PlayerName, Challenge.Rank, clash.Alive(0), rival.name, rival.Rank, hunt.ChallengeSoldiersLeft);

        /// <summary>The stage battle, every frame: the sides' counts, and who clears their stage first.</summary>
        private void UpdateStageBattle()
        {
            if (Time.unscaledTime >= stageSidesNext) { stageSidesNext = Time.unscaledTime + 0.4f; RefreshStageSides(); }
            if (hunt.ChallengeSoldiersLeft == 0) StageWon(1);
        }

        /// <summary>
        /// A stage is cleared (+1: foi's, -1: the rival's): that side goes 1-0 up, the soldiers left on the other stage
        /// go, both gates open and the rival foi steps down from its stage for the duel in the middle.
        /// </summary>
        private void StageWon(int lead)
        {
            if (chPhase != ChPhase.Stages) return;
            chLead = lead;
            hunt.RemoveChallengeSoldiers();
            var (look, held) = clash.TakeChampion();
            clash.Retreat();
            OpenChallengeGates();
            chPhase = ChPhase.Duel;
            chUi.SetClock(ScoreText());
            chUi.SetStatus(Loc.T(lead > 0 ? "ch.stageWon" : "ch.stageLost"));
            AudioManager.PlaySfx(lead > 0 ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
            if (look == null) return;
            // The rival foi comes down off its stage towards the middle.
            look.SetParent(null, true);
            var at = new GridPos(Mathf.Clamp(Mathf.RoundToInt(look.position.x), 0, StW - 1), Mathf.Clamp(Mathf.RoundToInt(look.position.z), A0, A1));
            if (!grid.IsStandable(at) || grid.IsOccupied(at)) at = new GridPos(7, A0 + 1);
            var w = Armory.Get(rival.weapon);
            int hp = Mathf.RoundToInt(rival.foiHp / Mathf.Max(0.4f, 1f - rival.armor * 0.8f));
            float hit = Mathf.Clamp(rival.foiHit * (0.85f + 0.12f * Armory.Damage(w, Mathf.Max(1, rival.weaponLevel))), 0.06f, 0.35f);
            hunt.FoeBoldness = rival.boldness;
            hunt.ChallengeBattle = true;
            hunt.AddChallengeFoe(at, hp, hit, look, held, rival.shield);
            ui.ShowIntro(Loc.T("ch.duel"), Loc.T("ch.gatesOpen"));
        }

        /// <summary>Both bridge gates open (their bars rise), and the way to the middle is free.</summary>
        private void OpenChallengeGates()
        {
            foreach (var t in grid.GateTiles)
            {
                grid.SetOccupied(t, false);
                if (stageGates.TryGetValue(t, out var g) && g != null) g.Open();
                fx.Burst(GridView.ToWorld(t) + Vector3.up * 0.6f, new Color(1f, 0.7f, 0.45f), new Color(2.4f, 1.4f, 0.6f), 30, 6f);
            }
            AudioManager.PlaySfx(Sfx.Shield, 1f, 0.8f);
            cameraRig.Shake(0.4f);
        }
    }
}
