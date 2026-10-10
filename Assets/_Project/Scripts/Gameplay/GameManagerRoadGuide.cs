using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// After the win, the way to the tunnel can't be missed: a tall beam of light stands on the exit (seen from
    /// anywhere on the floor), a trail of glowing chevrons runs over the tiles from foi to the exit (a wave of light
    /// travels along it, and it follows foi as it moves), an arrow circles foi pointing the way, and the words
    /// "RUN TO THE TUNNEL!" pop up over foi.
    /// </summary>
    public partial class GameManager
    {
        private Transform roadGuide, roadArrow;
        private readonly List<(GridPos pos, Transform mark)> roadTrail = new List<(GridPos, Transform)>();
        private GridPos roadTrailFrom = new GridPos(-99, -99);
        private Material roadTrailMat;
        private float roadNoteTimer;

        private static readonly Color RoadGreen = new Color(0.5f, 1f, 0.7f);

        private void ShowRoadGuide()
        {
            ClearRoadGuide();
            if (roadBeacon == null) return;
            roadGuide = new GameObject("RoadGuide").transform;

            // A beam of light on the exit, up into the sky.
            var beam = MaterialFactory.CreateTransparent(new Color(RoadGreen.r, RoadGreen.g, RoadGreen.b, 0.35f), new Color(0.6f, 2.4f, 1.1f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Beam", roadBeacon.transform, new Vector3(0f, 4f, 0f), new Vector3(0.7f, 4f, 0.7f), beam);
            Shapes.Primitive(PrimitiveType.Cylinder, "BeamCore", roadBeacon.transform, new Vector3(0f, 4f, 0f), new Vector3(0.25f, 4f, 0.25f),
                MaterialFactory.Create(RoadGreen, new Color(1f, 3f, 1.6f)));

            // The arrow circling foi.
            roadArrow = new GameObject("RoadArrow").transform;
            roadArrow.SetParent(roadGuide, false);
            var solid = MaterialFactory.Create(RoadGreen, new Color(0.5f, 2.2f, 1f));
            Shapes.Rounded("Shaft", roadArrow, new Vector3(0f, 0f, 0.05f), new Vector3(0.14f, 0.06f, 0.4f), 0.03f, solid);
            foreach (float s in new[] { -1f, 1f })
                Shapes.Rounded("Head", roadArrow, new Vector3(s * 0.1f, 0f, 0.26f), new Vector3(0.1f, 0.06f, 0.3f), 0.03f, solid)
                    .transform.localRotation = Quaternion.Euler(0f, -s * 40f, 0f);

            roadTrailMat = MaterialFactory.Create(RoadGreen, new Color(0.4f, 1.8f, 0.8f));
            roadTrailFrom = new GridPos(-99, -99);
            roadNoteTimer = 0f;
            AudioManager.PlaySfx(Sfx.Win, 0.5f, 1.3f);
        }

        private void ClearRoadGuide()
        {
            if (roadGuide != null) Destroy(roadGuide.gameObject);
            roadGuide = null;
            roadArrow = null;
            roadTrail.Clear();
        }

        private void UpdateRoadGuide()
        {
            if (roadGuide == null || roadBeacon == null) return;
            var exit = GridView.ToWorld(roadExit);

            // The arrow: a little ahead of foi on the side of the exit, bobbing.
            var to = exit - robot.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                var dir = to.normalized;
                roadArrow.gameObject.SetActive(to.magnitude > 1.5f);
                roadArrow.position = robot.transform.position + dir * 1.3f + Vector3.up * (0.35f + Mathf.Sin(Time.time * 5f) * 0.08f);
                roadArrow.rotation = Quaternion.LookRotation(dir);
                roadArrow.localScale = Vector3.one * (1.6f + Mathf.Sin(Time.time * 8f) * 0.15f);
            }

            // The trail follows foi: rebuilt whenever it reaches a new tile.
            if (robot.Position != roadTrailFrom) BuildRoadTrail();
            for (int i = 0; i < roadTrail.Count; i++)
            {
                // A wave of light running from foi towards the exit.
                float wave = Mathf.Repeat(Time.time * 6f - i, 8f);
                float k = wave < 1.5f ? 1f - wave / 1.5f : 0f;
                roadTrail[i].mark.localScale = Vector3.one * (1f + k * 0.45f);
            }

            // Say it again now and then while foi isn't on its way.
            roadNoteTimer -= Time.deltaTime;
            if (roadNoteTimer <= 0f)
            {
                roadNoteTimer = 6f;
                FloatAt(robot.transform.position + Vector3.up * 1.2f, Loc.T("float.toTunnel"), RoadGreen);
            }
        }

        /// <summary>The shortest walk from foi to the exit over free floor tiles, marked with a chevron on each.</summary>
        private void BuildRoadTrail()
        {
            roadTrailFrom = robot.Position;
            foreach (var (_, mark) in roadTrail) if (mark != null) Destroy(mark.gameObject);
            roadTrail.Clear();
            var came = new Dictionary<GridPos, GridPos> { [robot.Position] = robot.Position };
            var queue = new Queue<GridPos>();
            queue.Enqueue(robot.Position);
            bool found = false;
            while (queue.Count > 0 && !found)
            {
                var p = queue.Dequeue();
                foreach (var d in DirectionExtensions.All)
                {
                    var o = d.ToOffset();
                    var n = p + o;
                    if (!grid.IsFloor(n))
                    {
                        // A one-tile gap: foi leaps it (the trail jumps it too).
                        if (!grid.InBounds(n) || grid.IsWall(n)) continue;
                        n = n + o;
                    }
                    if (came.ContainsKey(n) || !grid.IsFloor(n)) continue; // after the win nothing stands in the way
                    came[n] = p;
                    if (n == roadExit) { found = true; break; }
                    queue.Enqueue(n);
                }
            }
            if (!found) return;
            var path = new List<GridPos>();
            for (var p = roadExit; p != robot.Position; p = came[p]) path.Add(p);
            path.Reverse();
            var prev = robot.Position;
            foreach (var p in path)
            {
                if (p == roadExit) break;
                var mark = new GameObject("Trail").transform;
                mark.SetParent(roadGuide, false);
                mark.position = GridView.ToWorld(p) + Vector3.up * (GridView.SurfaceY + 0.05f);
                var o = new Vector3(p.x - prev.x, 0f, p.y - prev.y);
                mark.rotation = Quaternion.LookRotation(o);
                foreach (float s in new[] { -1f, 1f })
                    Shapes.Rounded("Chevron", mark, new Vector3(s * 0.13f, 0f, 0f), new Vector3(0.1f, 0.04f, 0.42f), 0.02f, roadTrailMat)
                        .transform.localRotation = Quaternion.Euler(0f, -s * 40f, 0f);
                roadTrail.Add((p, mark));
                prev = p;
            }
        }
    }
}
