using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The Shadow Clones of vanG's Hall of Mirrors (the Crystal Cave, then the Star Road, where they take the place of
    /// the Masked Thief): two dark copies of the robot copy every hop it makes, mirrored, one left-right and one
    /// up-down. Touching one hurts like a block. Collect the energy cores spread over the floor to win; a falling block
    /// knocks a clone flat for a few seconds, so the clones can be lured under them.
    /// </summary>
    public partial class GameManager
    {
        private const float CloneDownSeconds = 4f;

        private class Clone
        {
            public ShadowClone view;
            public GridPos pos;
            /// <summary>True: mirrors left-right (copies up-down moves as they are); false: mirrors up-down.</summary>
            public bool flipX;
            public float down;
        }

        private readonly List<Clone> clones = new List<Clone>();
        private readonly List<(GridPos pos, QuestItem item)> cores = new List<(GridPos, QuestItem)>();
        private GridPos cloneLastTile;

        private void SetupClones()
        {
            objectivesDone = 0;
            cloneLastTile = robot.Position;
            doorPos = robot.Position; // the pieces spread away from the start
            if (level.cloneKnockouts > 0)
            {
                // This card asks to knock the clones out (lure them under blocks), not to collect cores.
                objectivesTotal = level.cloneKnockouts;
            }
            else
            {
                foreach (var p in SpreadTiles(Mathf.Max(3, level.keys)))
                    cores.Add((p, QuestItem.Create(QuestKind.Cores, GridView.ToWorld(p) + Vector3.up * GridView.SurfaceY)));
                objectivesTotal = cores.Count;
            }

            // Each clone starts at the robot's mirror image across the floor (or the nearest free tile to it).
            var r = robot.Position;
            foreach (bool flipX in new[] { true, false })
            {
                var want = flipX ? new GridPos(grid.Width - 1 - r.x, r.y) : new GridPos(r.x, grid.Height - 1 - r.y);
                var at = CloneStart(want);
                clones.Add(new Clone
                {
                    pos = at,
                    flipX = flipX,
                    view = ShadowClone.Create(GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY, t => robot.BuildLookalike(t)),
                });
            }
            hazards.IsProtected = p => cores.Exists(c => c.pos == p);
        }

        /// <summary>The free tile closest to <paramref name="want"/> that is at least four steps from the robot and the other clone.</summary>
        private GridPos CloneStart(GridPos want)
        {
            GridPos best = want;
            int bestScore = int.MaxValue;
            foreach (var t in grid.AllPositions())
            {
                if (!grid.IsStandable(t) || t.Manhattan(robot.Position) < 4 || clones.Exists(c => c.pos == t) || cores.Exists(c => c.pos == t)) continue;
                int score = t.Manhattan(want);
                if (score < bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        /// <summary>The robot landed on <paramref name="p"/>: the clones copy its move, and a core there is picked up.</summary>
        private void ClonesFollow(GridPos p)
        {
            int dx = System.Math.Sign(p.x - cloneLastTile.x), dy = System.Math.Sign(p.y - cloneLastTile.y);
            cloneLastTile = p;

            int core = cores.FindIndex(c => c.pos == p);
            if (core >= 0) TakeCore(core);
            if (State != GameState.Playing) return;

            if (dx != 0 || dy != 0)
                foreach (var c in clones)
                {
                    if (c.down > 0f) continue;
                    var step = c.flipX ? new GridPos(-dx, dy) : new GridPos(dx, -dy);
                    var target = c.pos + step;
                    if (!grid.IsStandable(target) || clones.Exists(o => o != c && o.pos == target)) continue;
                    c.pos = target;
                    c.view.HopTo(GridView.ToWorld(target) + Vector3.up * GridView.SurfaceY);
                }
            CheckCloneTouch();
        }

        private void UpdateClones(float dt)
        {
            if (previewing) return;
            foreach (var c in clones)
            {
                if (c.down <= 0f) continue;
                c.down -= dt;
                if (c.down > 0f) continue;
                c.view.SetDown(false);
                fx.Dust(GridView.ToWorld(c.pos) + Vector3.up * 0.1f, new Color(0.4f, 0.3f, 0.6f), 10, 2f);
            }
            CheckCloneTouch();
        }

        /// <summary>A clone on the robot's tile hurts it like a block (a shield knocks the clone flat instead).</summary>
        private void CheckCloneTouch()
        {
            if (State != GameState.Playing || !robot.IsAlive || robot.IsHovering) return;
            foreach (var c in clones)
            {
                if (c.down > 0f || c.pos != robot.Position || c.view.Hopping) continue;
                if (robot.IsShielded)
                {
                    robot.AbsorbHit();
                    KnockClone(c);
                    return;
                }
                KnockClone(c);
                if (TryRescue(robot.Position, false)) return;
                robot.Squash();
                cameraRig.Shake(1.2f);
                fx.Burst(robot.transform.position + Vector3.up * 0.3f, new Color(0.3f, 0.2f, 0.5f), new Color(1.2f, 0.4f, 2f), 24, 5f);
                AudioManager.PlaySfx(Sfx.Squash);
                Haptics.Death();
                Lose(Loc.T("lose.clone"));
                return;
            }
        }

        /// <summary>A block landed on <paramref name="p"/>: any clone there is knocked flat for a while.</summary>
        private void KnockClonesAt(GridPos p)
        {
            foreach (var c in clones.ToArray())
                if (c.pos == p && c.down <= 0f && clones.Contains(c))
                {
                    KnockClone(c, counts: true);
                    FloatAt(GridView.ToWorld(p), Loc.T("float.cloneDown"), Palette.UiCyan);
                }
        }

        /// <summary>A clone knocked flat; <paramref name="counts"/>: it counts towards a knock-out goal (a block did it).</summary>
        private void KnockClone(Clone c, bool counts = false)
        {
            c.down = CloneDownSeconds;
            c.view.SetDown(true);
            fx.Burst(GridView.ToWorld(c.pos) + Vector3.up * 0.3f, new Color(0.3f, 0.2f, 0.5f), new Color(1.2f, 0.4f, 2f), 16, 4f);
            AudioManager.PlaySfx(Sfx.Blocked, 0.8f, 0.7f);
            if (!counts || level.cloneKnockouts <= 0 || State != GameState.Playing) return;
            objectivesDone++;
            if (objectivesDone < objectivesTotal) return;
            FloatAt(c.view.transform.position + Vector3.up, Loc.T("float.clonesGone"), Palette.UiGold);
            foreach (var o in clones) fx.Burst(o.view.transform.position + Vector3.up * 0.4f, new Color(0.3f, 0.2f, 0.5f), new Color(1.2f, 0.4f, 2f), 20, 4f);
            ClearClones();
            Win();
        }

        private void TakeCore(int index)
        {
            var (pos, item) = cores[index];
            cores.RemoveAt(index);
            if (item != null) Destroy(item.gameObject);
            objectivesDone++;
            var at = GridView.ToWorld(pos);
            fx.Burst(at + Vector3.up * 0.5f, Palette.UiCyan, Palette.ShieldPickupGlow, 22, 4f);
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.3f);
            cameraRig.Punch(0.5f);
            Haptics.Medium();
            int left = objectivesTotal - objectivesDone;
            if (left > 0)
            {
                FloatAt(at, Loc.F("quest.left.Cores", left), Palette.UiCyan);
                return;
            }
            FloatAt(at, Loc.T("float.clonesGone"), Palette.UiGold);
            foreach (var c in clones) fx.Burst(c.view.transform.position + Vector3.up * 0.4f, new Color(0.3f, 0.2f, 0.5f), new Color(1.2f, 0.4f, 2f), 20, 4f);
            ClearClones();
            Win();
        }

        private void ClearClones()
        {
            foreach (var c in clones) if (c.view != null) Destroy(c.view.gameObject);
            clones.Clear();
            foreach (var (_, item) in cores) if (item != null) Destroy(item.gameObject);
            cores.Clear();
        }
    }
}
