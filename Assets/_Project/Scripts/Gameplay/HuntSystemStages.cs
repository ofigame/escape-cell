using System;
using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Fortress stages: a floor of up to six stages joined by gated bridges (see LevelCatalog.StageLayout). Each stage
    /// has its own crowd — the first only easy robots; every later stage the robots of the stages before it plus its own
    /// tougher rank — dropping in as they fall, a few at a time, but only while foi is in that stage. A gate opens when
    /// everything in the stage before it is beaten; vanG wakes in the last stage when all of them are clear.
    /// </summary>
    public partial class HuntSystem
    {
        private int stageCount = 1;
        private int[] stageBounds;
        private int[] stageToCome;
        private bool[] gateOpen;
        private List<GridPos>[] gateGroups;

        /// <summary>A gate opened (the stage it leads into, 1-based for the player: stage <c>s + 2</c>), its tiles.</summary>
        public event Action<int, List<GridPos>> GateOpened;

        public int StageCount => stageCount;

        /// <summary>The stage a tile belongs to (a bridge counts with the stage south of it).</summary>
        public int StageOf(GridPos p)
        {
            if (stageCount <= 1 || stageBounds == null) return 0;
            for (int s = 0; s < stageCount; s++)
            {
                int x0 = stageBounds[s * 4], y0 = stageBounds[s * 4 + 1], x1 = stageBounds[s * 4 + 2], y1 = stageBounds[s * 4 + 3];
                if (p.y >= y0 && p.y <= y1 && p.x >= x0 - 1 && p.x <= x1 + 1) return s;
            }
            // A bridge: up to its gate it belongs with the stage south of it, past the gate with the next one.
            for (int s = 0; s < stageCount - 1; s++)
                if (p.y > stageBounds[s * 4 + 3] && p.y < stageBounds[(s + 1) * 4 + 1]) return p.y > stageBounds[s * 4 + 3] + 1 + Data.LevelCatalog.BridgeGateRow ? s + 1 : s;
            return p.y < stageBounds[1] ? 0 : stageCount - 1;
        }

        /// <summary>Where foi comes back in a stage after burning there: the stage's first tile off the bridge.</summary>
        public GridPos StageEntry(int s)
        {
            if (stageCount <= 1 || s <= 0) return grid.StartSpot ?? grid.CenterFloor();
            int x = (stageBounds[s * 4] + stageBounds[s * 4 + 2]) / 2;
            return new GridPos(x, stageBounds[s * 4 + 1] + 1);
        }

        /// <summary>
        /// A tile in a stage whose gate is still shut: foi may not get there any way at all — walking, leaping, flying or
        /// being rescued (see Robot.Forbidden and GameManager.SafeTileNear).
        /// </summary>
        public bool StageLocked(GridPos p)
        {
            if (stageCount <= 1 || gateOpen == null) return false;
            int open = 0;
            while (open < gateOpen.Length && gateOpen[open]) open++;
            return StageOf(p) > open;
        }

        /// <summary>The stage foi is standing in.</summary>
        public int RobotStage => StageOf(robot.Position);

        private void SetupStages()
        {
            stageBounds = level.stageBounds;
            stageCount = stageBounds != null && stageBounds.Length >= 4 ? stageBounds.Length / 4 : 1;
            stageToCome = new int[stageCount];
            gateOpen = new bool[Mathf.Max(0, stageCount - 1)];
            gateGroups = new List<GridPos>[Mathf.Max(0, stageCount - 1)];
            for (int s = 0; s < gateGroups.Length; s++) gateGroups[s] = new List<GridPos>();
            foreach (var g in grid.GateTiles)
            {
                int s = StageOf(g);
                if (s < gateGroups.Length) gateGroups[s].Add(g);
                grid.SetOccupied(g, true); // shut
            }
        }

        /// <summary>
        /// The rank of a robot of stage <paramref name="s"/>: 0 easy (one blow), up to the stage's own. Later stages keep
        /// the earlier ranks too; hard floors always keep plenty of easy ones in the mix.
        /// </summary>
        private int RankFor(int s)
        {
            if (stageCount <= 1) return -1; // single-stage floors: the classic guard
            if (s == 0) return 0;
            double r = rng.NextDouble();
            if (level.hard && r < 0.35) return 0;
            if (r < 0.75) return s;
            return rng.Next(s + 1);
        }

        /// <summary>Spawns the floor's crowd stage by stage (the multi-stage version of the classic spawn).</summary>
        private void SpawnStages()
        {
            var free = FreeTiles(Mathf.Clamp(Mathf.Min(grid.Width, grid.Height) / 2, 1, 3));
            var freeIn = new List<GridPos>[stageCount];
            int area = 0;
            for (int s = 0; s < stageCount; s++)
            {
                freeIn[s] = free.FindAll(p => StageOf(p) == s && !grid.IsGate(p) && InStage(p, s));
                area += freeIn[s].Count;
            }
            // Bugs by room; robots weighted towards the later stages (stage s gets s + 1 shares).
            int shares = stageCount * (stageCount + 1) / 2;
            guardCap = level.robotsAtOnce > 0 ? level.robotsAtOnce : level.robots;
            int robotsLeft = level.robots;
            for (int s = 0; s < stageCount; s++)
            {
                int bugsHere = Mathf.RoundToInt(level.bugs * freeIn[s].Count / (float)Mathf.Max(1, area));
                for (int i = 0; i < bugsHere && freeIn[s].Count > 0; i++) AddBug(Take(freeIn[s]));
                int robotsHere = s == stageCount - 1 ? robotsLeft : Mathf.RoundToInt(level.robots * (s + 1) / (float)shares);
                robotsLeft -= robotsHere;
                int now = Mathf.Min(robotsHere, guardCap);
                var away = freeIn[s].FindAll(p => Chebyshev(p, robot.Position) >= 3);
                int spawned = 0;
                for (int i = 0; i < now && away.Count > 0; i++) { var p = Take(away); freeIn[s].Remove(p); AddGuard(p, rank: RankFor(s)); spawned++; }
                stageToCome[s] = robotsHere - spawned;
            }
            // Enforcers in the last stage; vanG's warlords (the very strong tenth of the crowd) spread over the stages
            // after the first, more of them the further in.
            int last = stageCount - 1;
            for (int i = 0; i < level.brutes && freeIn[last].Count > 0; i++) AddGuard(Take(freeIn[last]), brute: true);
            int elitesLeft = level.elites;
            for (int s = 1; s < stageCount && elitesLeft > 0; s++)
            {
                int here = s == last ? elitesLeft : Mathf.Min(elitesLeft, Mathf.RoundToInt(level.elites * s / (float)(shares - 1)));
                var far = freeIn[s].FindAll(p => Chebyshev(p, robot.Position) >= 4);
                for (int i = 0; i < here && far.Count > 0; i++) { var p = Take(far); freeIn[s].Remove(p); AddGuard(p, brute: true, rank: 99); elitesLeft--; }
            }
            // Towers right and left of vanG's cage (the last stage), and the reactor in the last.
            for (int s = 0; s < stageCount; s++)
            {
                if (stageCount > 1 && s == 0) continue;
                int towersHere = stageCount == 1 || s == last ? level.towers : 0; // all by vanG's cage (in the last stage)
                var far = freeIn[s].FindAll(p => Chebyshev(p, robot.Position) >= 4);
                if (level.reactor && s == last && far.Count > 0) { var rp = NearestCentre(far); far.Remove(rp); freeIn[s].Remove(rp); AddTower(rp, reactor: true); }
                foreach (var tp in TowerSpots(far, towersHere)) { far.Remove(tp); freeIn[s].Remove(tp); AddTower(tp); }
            }
            guardsToCome = 0;
            foreach (int c in stageToCome) guardsToCome += c;
            nextGuardAt = Time.time + 2f;
        }

        private bool InStage(GridPos p, int s) =>
            p.x >= stageBounds[s * 4] && p.x <= stageBounds[s * 4 + 2] && p.y >= stageBounds[s * 4 + 1] && p.y <= stageBounds[s * 4 + 3];

        /// <summary>A robot fell and its stage has more to come: the next drops in, but only where foi is.</summary>
        private void UpdateStageReinforcements()
        {
            if (guardsToCome <= 0 || Time.time < nextGuardAt) return;
            int s = Mathf.Clamp(RobotStage, 0, stageCount - 1);
            if (stageToCome[s] <= 0) return;
            int alive = 0;
            foreach (var g in guards) if (!g.dead && !g.brute && g.stage == s) alive++;
            if (alive >= guardCap) return;
            var spots = FreeTiles(4);
            spots.RemoveAll(p => !InStage(p, s) || grid.IsOccupied(p) || (Avoid != null && Avoid(p)));
            if (spots.Count == 0) { nextGuardAt = Time.time + 0.5f; return; }
            var at = Take(spots);
            AddGuard(at, rank: RankFor(s));
            var ng = guards[guards.Count - 1];
            ng.from = At(at) + Vector3.up * 7f; // it falls onto its tile
            ng.root.position = ng.from;
            ng.moveT = 0f;
            ng.cooldown = 1f;
            stageToCome[s]--;
            guardsToCome--;
            Shockwave.Create(At(at), 1.2f, new Color(1f, 0.55f, 0.35f));
            fx.Burst(At(at) + Vector3.up * 0.2f, new Color(1f, 0.6f, 0.35f), new Color(2f, 0.9f, 0.4f), 16, 3f);
            Audio.AudioManager.PlaySfx(Audio.Sfx.Warning, 0.35f, 1.4f);
            nextGuardAt = Time.time + Mathf.Lerp(1.4f, 0.8f, level.score / 100f);
        }

        /// <summary>What is still to beat in a stage (robots to come included).</summary>
        private int StageLeft(int s)
        {
            int n = stageToCome[s];
            foreach (var g in guards) if (!g.dead && g.stage == s) n++;
            foreach (var t in towers) if (!t.dead && StageOf(t.pos) == s) n++;
            return n;
        }

        /// <summary>Opens every gate whose stage is now clear.</summary>
        private void CheckGates()
        {
            if (gateOpen == null) return;
            for (int s = 0; s < gateOpen.Length; s++)
            {
                if (gateOpen[s] || StageLeft(s) > 0) continue;
                gateOpen[s] = true;
                foreach (var g in gateGroups[s]) grid.SetOccupied(g, false);
                GateOpened?.Invoke(s, gateGroups[s]);
            }
        }
    }
}
