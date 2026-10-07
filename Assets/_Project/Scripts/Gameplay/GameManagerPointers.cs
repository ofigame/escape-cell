using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Where the player should be heading right now, for the edge-of-screen arrows: the close camera on a big floor
    /// can't show far keys, quest pieces, the hammer or the door, so the HUD points at them.
    /// </summary>
    public partial class GameManager
    {
        private readonly List<(Vector3 world, Color color)> goals = new List<(Vector3, Color)>();

        private List<(Vector3 world, Color color)> Goals()
        {
            goals.Clear();
            if (level == null || grid == null || previewing) return goals;
            Vector3 At(GridPos p) => GridView.ToWorld(p) + Vector3.up * 0.5f;

            foreach (var o in objectives)
                if (o.View != null) goals.Add((At(o.pos), Palette.UiGold));
            switch (level.mission)
            {
                case MissionType.Exit:
                    if (portal != null && portal.IsOpen) goals.Add((At(doorPos), Palette.UiCyan));
                    break;
                case MissionType.Quest:
                    if (questReady) goals.Add((At(doorPos), Palette.UiCyan));
                    break;
                case MissionType.Monster:
                    if (!charged && orb != null) goals.Add((At(orbPos), ThunderHammer.Electric));
                    else if (charged && monster != null) goals.Add((At(monsterPos), Palette.UiRed));
                    break;
                case MissionType.Thief:
                    if (thief != null && thiefLeft > 0) goals.Add((At(thiefPos), Palette.UiGold));
                    break;
                case MissionType.Clone:
                    foreach (var (pos, _) in cores) goals.Add((At(pos), Palette.UiCyan));
                    break;
                case MissionType.Escort:
                    if (!EscortTimed) goals.Add((At(doorPos), Palette.UiCyan));
                    if (buddy != null && buddyPos.Manhattan(robot.Position) > 3) goals.Add((At(buddyPos), new Color(1f, 0.72f, 0.35f)));
                    break;
                case MissionType.Paint:
                {
                    // The nearest tile still to paint.
                    GridPos? best = null;
                    int bestD = int.MaxValue;
                    foreach (var t in grid.AllPositions())
                    {
                        if (!grid.IsFloor(t) || painted.Contains(t)) continue;
                        int d = t.Manhattan(robot.Position);
                        if (d < bestD) { bestD = d; best = t; }
                    }
                    if (best.HasValue) goals.Add((At(best.Value), WorldTheme.Current.accent));
                    break;
                }
            }
            if (crate != null) goals.Add((At(crateTile), SuperCrate.Gold));
            return goals;
        }
    }
}
