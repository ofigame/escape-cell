using System.Collections;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The journey seen as one land: every floor stands on a plateau a flight of stairs above the land its roads run
    /// through, its ramparts kept, the world's ground, rocks, trees and peaks all round it. A road ends at the foot of
    /// the stairs with the floor ahead in full view (its first stage built there, walls and all); foi climbs up and the
    /// floor itself takes over in the same place. Out of a won floor, stairs go down past vanG's cage to the next road.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>How far the floors stand above the land (seven steps of the stairs).</summary>
        private const float PlateauDrop = 2.52f;
        /// <summary>The scenery round a journey floor, or the floor ahead seen from a road.</summary>
        private Transform journeyScene;
        /// <summary>Where foi steps onto a journey floor (the top of its stairs: the south edge below its start).</summary>
        private GridPos journeyEntry;
        /// <summary>foi is climbing the stairs up to the floor (no walking meanwhile).</summary>
        private bool climbing;

        private void ClearJourneyScene()
        {
            if (journeyScene != null) Destroy(journeyScene.gameObject);
            journeyScene = null;
        }

        /// <summary>A floor's entry: from its start spot straight south to the last floor tile (stage 1's south edge).</summary>
        private static GridPos EntryOf(GridModel g)
        {
            var p = g.StartSpot ?? g.CenterFloor();
            while (g.IsFloor(p + new GridPos(0, -1))) p += new GridPos(0, -1);
            return p;
        }

        private bool LastOfWorld => level != null && (level.final || levelIndex % LevelCatalog.LevelsPerWorld == LevelCatalog.LevelsPerWorld - 1);

        /// <summary>The gap in a journey floor's south ramparts where its stairs come up.</summary>
        private static bool EntryGap(GridPos p, GridPos entry) => p.y < entry.y && Mathf.Abs(p.x - entry.x) <= 2;

        /// <summary>A journey floor: its plateau, the land round it, the stairs up at the entry and down past the cage.</summary>
        private void SetupJourneyLevelScene()
        {
            ClearJourneyScene();
            journeyScene = new GameObject("JourneyScene").transform;
            int w = Mathf.Clamp(World, 0, BattleArenas.Count - 1);
            var look = BattleArenas.Style(w);
            Plateaus(journeyScene, grid, level.fortressStages > 1 ? level.stageBounds : null, look.wall);
            // The land reaches well beyond the floor's own box: its scenery is laid on a wider map, shifted back.
            const int Pad = 16;
            var rows = new string[grid.Height + Pad * 2];
            for (int r = 0; r < rows.Length; r++)
            {
                var row = new char[grid.Width + Pad * 2];
                int y = rows.Length - 1 - r - Pad;
                for (int x = 0; x < row.Length; x++) row[x] = grid.Exists(new GridPos(x - Pad, y)) ? '#' : '.';
                rows[r] = new string(row);
            }
            var wide = new GridModel(grid.Width + Pad * 2, grid.Height + Pad * 2, rows);
            var decor = new GameObject("Land").transform;
            decor.SetParent(journeyScene, false);
            // The ways in and out stay clear: the road up to the entry stairs, and on from the stairs down past the cage.
            var e0 = journeyEntry + new GridPos(Pad, Pad);
            var x0 = huntGate + new GridPos(Pad, Pad);
            bool exitWay = huntGateSet && !LastOfWorld;
            ArenaDecor.KeepClear = p => (Mathf.Abs(p.x - e0.x) <= 3 && p.y < e0.y) || (exitWay && Mathf.Abs(p.x - x0.x) <= 3 && p.y > x0.y);
            ArenaDecor.Trail(decor, wide, w, look.wall, look.accent, PlateauDrop, 3);
            ArenaDecor.KeepClear = null;
            decor.position = new Vector3(-Pad, 0f, -Pad);
            if (decor.GetComponentsInChildren<MeshRenderer>().Length < 6000) StaticBatchingUtility.Combine(decor.gameObject);
            // Up from the road at the entry...
            var entry = GridView.ToWorld(journeyEntry) + new Vector3(0f, GridView.SurfaceY - PlateauDrop, -4.1f);
            Stairs(journeyScene, entry, 0f, look, null);
            RoadStub(journeyScene, entry + new Vector3(0f, 0f, -0.2f), -1f, look);
            // ...and, past vanG's cage, down to the next road (a world's last floor leads into its tunnel instead).
            if (huntGateSet && !LastOfWorld)
            {
                var exit = GridView.ToWorld(huntGate) + new Vector3(0f, GridView.SurfaceY - PlateauDrop, 4.1f);
                Stairs(journeyScene, exit, 180f, look, null);
                RoadStub(journeyScene, exit + new Vector3(0f, 0f, 0.2f), 1f, look);
            }
        }

        /// <summary>
        /// The floor ahead at a road's end: its first stage (as on the menu's backdrop) with its ramparts, on its plateau,
        /// set so that its entry is exactly at the top of the road's stairs — where the floor itself will stand.
        /// </summary>
        private void BuildFloorAhead(int lvl)
        {
            ClearJourneyScene();
            var data = BackdropOf(levelSet.levels[Mathf.Clamp(lvl, 0, LevelCount - 1)]);
            var pg = BuildGrid(data); // (this sets the relief to the floor's: it is put back for the road below)
            var root = new GameObject("FloorAhead").transform;
            var view = new GameObject("Grid").AddComponent<GridView>();
            view.transform.SetParent(root, false);
            view.Build(pg, fx, data.lowWalls);
            var pe = EntryOf(pg);
            int w = Mathf.Clamp(LevelCatalog.WorldOf(lvl), 0, BattleArenas.Count - 1);
            float camp = Mathf.InverseLerp(0f, 24f, w);
            Color.RGBToHSV(WorldTheme.Current.accent, out float wh, out _, out _);
            FortressFloor.WallTint = Color.HSVToRGB(wh, 0.22f, Mathf.Lerp(0.6f, 0.48f, camp));
            FortressFloor.Fx = FortressFloor.WallFx.None;
            FortressFloor.TowerGrow = Mathf.Lerp(1f, 1.9f, camp);
            FortressFloor.WallScaleAt = null;
            FortressFloor.SkipCornerTowers = false;
            FortressFloor.SkipWall = p => EntryGap(p, pe);
            FortressFloor.Detailed = false;
            FortressFloor.WallThick = Mathf.Lerp(1.0f, 1.9f, camp);
            var walls = FortressFloor.Build(pg, view.transform, FortressFloor.StageFor(w), WorldTheme.Current.accent, pe, view.Surface, null, Mathf.Lerp(1.15f, 3.0f, camp));
            FortressFloor.SkipWall = null;
            var look = BattleArenas.Style(w);
            Plateaus(root, pg, null, look.wall);
            var entryWorld = GridView.ToWorld(pe);
            // The road's own relief again (the road is flat).
            FloorRelief.Apply(grid, TerrainKind.Flat, World, null, LevelCatalog.StageRise, LevelCatalog.BridgeRows);
            var stairsTop = GridView.ToWorld(trailGate) + new Vector3(0f, PlateauDrop, 4.1f);
            root.position = stairsTop - entryWorld;
            if (walls != null && walls.GetComponentsInChildren<MeshRenderer>().Length < 6000) StaticBatchingUtility.Combine(walls.gameObject);
            journeyScene = root;
        }

        /// <summary>Up the road's stairs onto the floor ahead: a short climb, then the floor takes over where it stood.</summary>
        private IEnumerator ClimbOntoFloor(int lvl)
        {
            climbing = true;
            var from = robot.transform.position;
            var to = GridView.ToWorld(trailGate) + new Vector3(0f, PlateauDrop, 4.1f);
            robot.ArenaFace(to - from);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                var p = Vector3.Lerp(from, to, t);
                p.y = Mathf.Lerp(from.y, to.y, Mathf.SmoothStep(0f, 1f, t)) + Mathf.Abs(Mathf.Sin(t * Mathf.PI * 7f)) * 0.05f;
                robot.transform.position = p;
                yield return null;
            }
            climbing = false;
            ExitTps();
            StartJourneyLevel(lvl);
        }

        /// <summary>Rock under a floor down to the land: one block under each stage (up to its height), or the whole floor.</summary>
        private static void Plateaus(Transform parent, GridModel g, int[] stageBounds, Color wall)
        {
            var rock = MaterialFactory.CreateSurface(Color.Lerp(wall, new Color(0.35f, 0.32f, 0.3f), 0.5f), Color.black, 0.15f, 0f);
            void Block(int x0, int y0, int x1, int y1)
            {
                var top = GridView.ToWorld(new GridPos((x0 + x1) / 2, (y0 + y1) / 2)).y;
                float h = top + PlateauDrop;
                var c = new Vector3((x0 + x1) * 0.5f, top - h * 0.5f - 0.1f, (y0 + y1) * 0.5f);
                Shapes.Rounded("Plateau", parent, c, new Vector3(x1 - x0 + 3f, h, y1 - y0 + 3f), 0.3f, rock);
            }
            if (stageBounds != null && stageBounds.Length >= 4)
            {
                for (int i = 0; i + 3 < stageBounds.Length; i += 4) Block(stageBounds[i], stageBounds[i + 1], stageBounds[i + 2], stageBounds[i + 3]);
                return;
            }
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var p in g.AllPositions())
            {
                if (!g.Exists(p)) continue;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            if (minX <= maxX) Block(minX, minY, maxX, maxY);
        }

        /// <summary>A flight of broad stairs: its foot at <paramref name="foot"/>, rising 2.5 over 3.5 tiles the way <paramref name="yaw"/> faces.</summary>
        private static Transform Stairs(Transform parent, Vector3 foot, float yaw, BattleArenas.Look look, string sign)
        {
            var root = new GameObject("Stairs").transform;
            root.SetParent(parent, false);
            root.position = foot;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var stone = MaterialFactory.Create(Color.Lerp(look.wall, Color.white, 0.35f), Color.black);
            var edge = MaterialFactory.Create(look.accent, look.accent * 1.6f);
            for (int i = 0; i < 7; i++)
            {
                float h = 0.36f * (i + 1);
                Shapes.Rounded("Step", root, new Vector3(0f, h * 0.5f, 0.25f + i * 0.5f), new Vector3(4.6f, h, 0.52f), 0.04f, stone);
                Shapes.Rounded("Edge", root, new Vector3(0f, h + 0.01f, i * 0.5f + 0.02f), new Vector3(4.6f, 0.04f, 0.08f), 0.02f, edge);
            }
            foreach (float s in new[] { -1f, 1f })
                Shapes.Rounded("Rail", root, new Vector3(s * 2.45f, 1.5f, 1.8f), new Vector3(0.25f, 3f, 3.8f), 0.08f, stone);
            if (sign != null)
            {
                var t = new GameObject("Sign").AddComponent<TMPro.TextMeshPro>();
                t.transform.SetParent(root, false);
                t.transform.localPosition = new Vector3(0f, 4.4f, 3.4f);
                t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                t.font = UiFactory.Font;
                t.text = sign;
                t.fontSize = 8f;
                t.alignment = TMPro.TextAlignmentOptions.Center;
                t.color = new Color(1f, 0.9f, 0.45f);
                t.outlineWidth = 0.25f;
                t.outlineColor = new Color(0.1f, 0.05f, 0.1f);
                t.rectTransform.sizeDelta = new Vector2(9f, 2.5f);
            }
            return root;
        }

        /// <summary>A stretch of the road on the land beyond a floor's stairs (<paramref name="dir"/> +1 north, -1 south).</summary>
        private static void RoadStub(Transform parent, Vector3 from, float dir, BattleArenas.Look look)
        {
            var paving = MaterialFactory.CreateSurface(Color.Lerp(look.wall, Color.white, 0.6f), Color.black, 0.1f, 0f);
            Shapes.Rounded("Road", parent, from + new Vector3(0f, -0.08f, dir * 5f), new Vector3(5f, 0.16f, 10f), 0.05f, paving);
        }
    }
}
