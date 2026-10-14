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

        private const int StW = 25, StMid = StW / 2;
        // Rows from the south (y = 0): stage B (its fighting ground, then the waiting groups' ground behind a line), its
        // bridge (gate on the middle row), the round middle, A's bridge, stage A (fighting ground, then its waiting ground).
        private const int B0 = 0, BLine = 12, B1 = 16, MidC = 28, A0 = 40, ALine = 52, A1 = 56, StH = A1 + 1;
        /// <summary>The rival's groups still waiting on foi's stage (their soldiers' models, in their blocks).</summary>
        private readonly List<List<SoldierModel>> waitingB = new List<List<SoldierModel>>();
        private int stageGroup;
        private float nextGroupAt = -1f;

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
                    else if ((y > B1 && y < MidC - 8) || (y > MidC + 8 && y < A0)) c = x >= StMid - 1 && x <= StMid + 1 ? (y == B1 + 2 || y == A0 - 2 ? 'G' : '#') : '.';
                    else
                    {
                        float dx = (x - StMid) / 12.4f, dy = (y - MidC) / 8.6f;
                        if (dx * dx + dy * dy <= 1f) c = '#';
                    }
                    if (x == StMid && y == 3) c = 'S';
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
            l.reactor = false; // no reactor tower (and its repair drones) from the late worlds on the board
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

            // Stage B: the rival's whole army, six blocks waiting behind the line (foi may not go there); the groups step
            // up one after another.
            foreach (var grp in waitingB) foreach (var m in grp) if (m != null) Destroy(m.gameObject);
            waitingB.Clear();
            for (int g = 0; g < Army.Groups; g++)
            {
                var kind = Army.KindOf(g);
                var grp = new List<SoldierModel>();
                var centre = GridView.ToWorld(new GridPos(0, 0)) + new Vector3(2.5f + 4f * g, GridView.SurfaceY, (BLine + B1) * 0.5f + 0.6f);
                for (int i = 0; i < Army.Soldiers; i++)
                {
                    var model = SoldierModel.Build(hunt.transform, kind, Army.Heavy(kind) ? ClashSystem.Theirs : Color.Lerp(ClashSystem.Theirs, Color.white, 0.3f));
                    model.transform.localScale = Vector3.one * hunt.FoiHeight * 0.8f * (Army.Heavy(kind) ? 1.12f : 1f);
                    model.transform.position = centre + new Vector3((i % 6 - 2.5f) * 0.42f, 0f, (i / 6 - 2f) * 0.5f);
                    model.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    grp.Add(model);
                }
                waitingB.Add(grp);
            }
            for (int y = BLine; y <= B1; y++) for (int x = 0; x < StW; x++) grid.SetOccupied(new GridPos(x, y), true); // the waiting ground
            stageGroup = 0;
            nextGroupAt = -1f;
            hunt.ChallengeBattle = false; // a few at a time come at foi, as on the floors
            hunt.UseStreetMap = true;

            // Stage A: the player's soldiers against the rival foi.
            var spots = new Vector3[Army.Groups * Army.Soldiers];
            var kinds = new GroupKind[spots.Length];
            var powers = new float[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                int g = i / Army.Soldiers, k = i % Army.Soldiers;
                var centre = GridView.ToWorld(new GridPos(0, 0)) + new Vector3(2.5f + 4f * g, GridView.SurfaceY, (ALine + A1) * 0.5f - 0.6f);
                spots[i] = centre + new Vector3((k % 6 - 2.5f) * 0.42f, 0f, (k / 6 - 2f) * 0.5f);
                kinds[i] = Army.KindOf(g);
                powers[i] = Army.Power(g);
            }
            if (rivalLook != null) Destroy(rivalLook.gameObject);
            rivalLook = RivalLook();
            var w = Armory.Get(rival.weapon);
            // About three blows a soldier (more against heavy ones), a little more for a stronger weapon: a stage takes
            // the rival foi about as long as it takes foi.
            float champDamage = 2.4f + 0.6f * Armory.Damage(w, Mathf.Max(1, rival.weaponLevel));
            clash.BeginSkirmish(spots, kinds, powers, rivalLook, rivalHeld, GridView.ToWorld(new GridPos(StMid, (A0 + ALine) / 2)) + Vector3.up * GridView.SurfaceY,
                champDamage, 999f, hunt.FoiHeight * 0.8f);
            rivalLook = null; // the stage battle has it now
            clash.SetBounds(GridView.ToWorld(new GridPos(0, A0)) + new Vector3(0.2f, 0f, 0.2f), GridView.ToWorld(new GridPos(StW - 1, A1)) - new Vector3(0.2f, 0f, 0.2f));
            // (The rival foi may go anywhere on its stage: its blows only land on the group whose turn it is.)

            robot.transform.rotation = Quaternion.identity; // facing north, at the rival's soldiers
            // The opening: the camera high over the whole field, from the rival's stage (its foi and the player's soldiers in
            // place) down to foi's; then the word to go.
            chPhase = ChPhase.StageIntro;
            stageIntroT = 0f;
            hunt.Freeze();
            cameraRig.StartCoroutine(ChallengeBriefing("stages", 3, () => { }));
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
            if (!chBriefing) stageIntroT += Time.deltaTime; // the fly-over waits for the briefing cards
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
            SendStageGroup();
            chUi.SetStatus(Loc.T("ch.stagesGo"));
            ui.ShowIntro(Loc.T("ch.mode.stages"), Loc.T("ch.stagesIntro"));
            AudioManager.PlaySfx(Sfx.Warning, 0.8f, 1f);
        }

        /// <summary>The groups still waiting behind the line go (the challenge was left).</summary>
        private void ClearStageWaiting()
        {
            foreach (var grp in waitingB) foreach (var m in grp) if (m != null) Destroy(m.gameObject);
            waitingB.Clear();
        }

        private int WaitingOnB { get { int n = 0; foreach (var g in waitingB) n += g.Count; return n; } }

        private void RefreshStageSides() =>
            chUi.SetSides(Challenge.PlayerName, Challenge.Rank, clash.Alive(0), rival.name, rival.Rank, hunt.ChallengeSoldiersLeft + WaitingOnB);

        /// <summary>The next of the rival's groups steps out of its block onto foi's fighting ground.</summary>
        private void SendStageGroup()
        {
            while (stageGroup < waitingB.Count && waitingB[stageGroup].Count == 0) stageGroup++;
            if (stageGroup >= waitingB.Count) return;
            var kind = Army.KindOf(stageGroup);
            int hp = Mathf.Max(1, Mathf.RoundToInt((Army.Heavy(kind) ? 2f : 1f) * rival.Power(stageGroup)));
            foreach (var m in waitingB[stageGroup])
            {
                if (m == null) continue;
                // Out of the block to the front of the fighting ground, under the line.
                var at = m.transform.position;
                hunt.AddChallengeSoldier(new Vector3(at.x, at.y, GridView.ToWorld(new GridPos(0, BLine - 2)).z), hp, m.transform, m, kind);
            }
            waitingB[stageGroup].Clear();
            chUi.SetStatus(Loc.F("ch.groupUp", Loc.T("army.kind." + kind)));
            stageGroup++;
        }

        /// <summary>The stage battle, every frame: the sides' counts, the next group up, and who clears their stage first.</summary>
        private void UpdateStageBattle()
        {
            if (Time.unscaledTime >= stageSidesNext) { stageSidesNext = Time.unscaledTime + 0.4f; RefreshStageSides(); }
            if (hunt.ChallengeSoldiersLeft > 0) return;
            if (WaitingOnB == 0) { StageWon(1); return; }
            // A group beaten: a breath, then the next one steps up.
            if (nextGroupAt < 0f) nextGroupAt = Time.time + 1.2f;
            else if (Time.time >= nextGroupAt) { nextGroupAt = -1f; SendStageGroup(); }
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
            // The waiting blocks leave too, and their ground is free for the duel.
            foreach (var grp in waitingB) { foreach (var m in grp) if (m != null) Destroy(m.gameObject); grp.Clear(); }
            for (int y = BLine; y <= B1; y++) for (int x = 0; x < StW; x++) grid.SetOccupied(new GridPos(x, y), false);
            var (look, held) = clash.TakeChampion();
            clash.Retreat();
            OpenChallengeGates();
            chPhase = ChPhase.Duel;
            chDuelMe = chDuelThem = 0;
            chUi.SetClock(ScoreText());
            chUi.SetStatus(Loc.T(lead > 0 ? "ch.stageWon" : "ch.stageLost"));
            AudioManager.PlaySfx(lead > 0 ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
            if (look == null) return;
            // The rival foi comes down off its stage towards the middle.
            look.SetParent(null, true);
            var at = new GridPos(Mathf.Clamp(Mathf.RoundToInt(look.position.x), 0, StW - 1), Mathf.Clamp(Mathf.RoundToInt(look.position.z), A0, A1));
            if (!grid.IsStandable(at) || grid.IsOccupied(at)) at = new GridPos(StMid, A0 + 1);
            AddRivalFoe(at, look, held);
            BeginDuelRounds();
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
