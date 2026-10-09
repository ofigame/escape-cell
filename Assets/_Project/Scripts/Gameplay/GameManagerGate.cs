using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The core loop's gate and healing islands. The tunnel gate stands from the start in the middle of the floor's
    /// north edge with the monster asleep in front of it; beating the crowd wakes it, beating it opens the gate and
    /// the road leaves from there. Harder floors have small safe islands a leap off the edge (layout 'H' tiles): no
    /// crate falls there and no enemy reaches them, and a potion on each fills the health bar (then refills itself).
    /// </summary>
    public partial class GameManager
    {
        private GridPos huntGate;
        private bool huntGateSet;
        private TunnelGate gateView;
        private MonsterCage cageView;
        private GameObject bridgeRails;
        private readonly List<(GridPos tile, HealthPotion view)> potions = new List<(GridPos, HealthPotion)>();
        private float lastSleepNote = -10f;

        /// <summary>Seconds before a drunk potion is full again.</summary>
        private const float PotionRefill = 25f;

        /// <summary>The middle of the north edge: of the tiles with nothing beyond them northwards, the one nearest the middle.</summary>
        private GridPos NorthGate()
        {
            // With vanG's cage and bridge: the bridge's far end, where the tunnel begins.
            if (grid.BridgeTiles.Count > 0)
            {
                huntGateSet = true;
                return grid.BridgeTiles[grid.BridgeTiles.Count - 1];
            }
            int mid = grid.Width / 2;
            GridPos best = grid.CenterFloor();
            int bestScore = int.MaxValue;
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsFloor(p) || grid.IsSafe(p)) continue;
                bool edge = true;
                for (int y = p.y + 1; y < grid.Height && edge; y++) edge = !grid.Exists(new GridPos(p.x, y));
                if (!edge) continue;
                int score = Mathf.Abs(p.x - mid) * 10 + (grid.Height - 1 - p.y);
                if (score < bestScore) { bestScore = score; best = p; }
            }
            huntGateSet = true;
            return best;
        }

        /// <summary>Where the monster waits: the tile just in front of (south of) the gate, else the nearest floor tile.</summary>
        private GridPos GateGuardSpot(GridPos gate)
        {
            // In the cage: its middle, on the row nearer the floor.
            if (grid.CageTiles.Count > 0)
            {
                int bx = grid.BridgeTiles.Count > 0 ? grid.BridgeTiles[0].x : grid.CageTiles[0].x;
                GridPos inCage = grid.CageTiles[0];
                foreach (var c in grid.CageTiles)
                    if (Mathf.Abs(c.x - bx) < Mathf.Abs(inCage.x - bx) || (c.x == inCage.x && c.y < inCage.y)) inCage = c;
                return inCage;
            }
            var front = gate + new GridPos(0, -1);
            if (grid.IsStandable(front) && !grid.IsSafe(front)) return front;
            GridPos best = gate;
            int bestDistance = int.MaxValue;
            foreach (var p in grid.AllPositions())
            {
                if (p == gate || !grid.IsStandable(p) || grid.IsSafe(p)) continue;
                int d = p.Manhattan(gate);
                if (d < bestDistance) { bestDistance = d; best = p; }
            }
            return best;
        }

        /// <summary>One potion on every healing island (its tile nearest the floor).</summary>
        private void SetupPotions()
        {
            ClearPotions();
            var seen = new HashSet<GridPos>();
            foreach (var p in grid.AllPositions())
            {
                if (!grid.IsSafe(p) || seen.Contains(p)) continue;
                // Gather the island and put the potion on its tile farthest from the main floor.
                var island = new List<GridPos>();
                var queue = new Queue<GridPos>();
                queue.Enqueue(p);
                seen.Add(p);
                while (queue.Count > 0)
                {
                    var q = queue.Dequeue();
                    island.Add(q);
                    foreach (var d in DirectionExtensions.All)
                    {
                        var n = q + d.ToOffset();
                        if (grid.IsSafe(n) && seen.Add(n)) queue.Enqueue(n);
                    }
                }
                foreach (var t in island) gridView.SetTint(t, new Color(1f, 0.78f, 0.85f), new Color(1.4f, 0.45f, 0.6f)); // soft rose: safe ground
                var centre = grid.CenterFloor();
                island.Sort((a, b) => b.Manhattan(centre).CompareTo(a.Manhattan(centre)));
                var at = island[0];
                potions.Add((at, HealthPotion.Create(GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY)));
            }
        }

        private void ClearPotions()
        {
            foreach (var (_, view) in potions) if (view != null) Destroy(view.gameObject);
            potions.Clear();
        }

        /// <summary>Removes the gate and the potions (a new level, the menu).</summary>
        private void ClearGate()
        {
            if (gateView != null) Destroy(gateView.gameObject);
            gateView = null;
            huntGateSet = false;
            if (cageView != null) Destroy(cageView.gameObject);
            cageView = null;
            if (bridgeRails != null) Destroy(bridgeRails);
            bridgeRails = null;
            ClearPotions();
        }

        /// <summary>foi landed on a tile: a ready potion there fills the health bar.</summary>
        private void OnHuntArrived(GridPos p)
        {
            foreach (var (tile, view) in potions)
            {
                if (tile != p || view == null || !view.Ready) continue;
                view.Drink(PotionRefill);
                health = 1f;
                RefreshHealthBar();
                var at = GridView.ToWorld(p) + Vector3.up * 0.6f;
                fx.Burst(at, new Color(1f, 0.35f, 0.45f), new Color(2.2f, 0.4f, 0.55f), 36, 5f);
                Shockwave.Create(GridView.ToWorld(p), 1.4f, new Color(1f, 0.45f, 0.55f));
                FloatAt(at + Vector3.up * 0.4f, Loc.T("float.potion"), new Color(1f, 0.55f, 0.65f));
                AudioManager.PlaySfx(Sfx.Shield, 1f, 1.3f);
                Haptics.Medium();
            }
        }


        /// <summary>"Canavar vanG" over the monster's health bar (it turns to face the camera with the bar).</summary>
        private static void NameTag(Monster m)
        {
            var bar = m.transform.Find("HealthBar");
            if (bar == null) return;
            var go = new GameObject("NameTag");
            go.transform.SetParent(bar, false);
            go.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            var text = go.AddComponent<TMPro.TextMeshPro>();
            text.font = UiFactory.TitleFont;
            text.fontSharedMaterial = UiFactory.OutlinedTitle(new Color(0.15f, 0.02f, 0.05f));
            text.text = Loc.T("monster.vanG");
            text.fontSize = 2.6f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.42f, 0.32f);
            text.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        }
        /// <summary>
        /// Shuts vanG's cage (its tiles are closed to everyone, the monster's own tile is left for it) and the bridge
        /// (closed until vanG falls), and builds the cage, the bridge's rails and its tint.
        /// </summary>
        private void SetupCage()
        {
            if (grid.CageTiles.Count == 0) return;
            var spot = hunt.MonsterSpot ?? grid.CageTiles[0];
            foreach (var c in grid.CageTiles) if (c != spot) grid.SetOccupied(c, true);
            foreach (var b in grid.BridgeTiles) grid.SetOccupied(b, true);
            var min = grid.CageTiles[0];
            var max = grid.CageTiles[0];
            foreach (var c in grid.CageTiles)
            {
                min = new GridPos(Mathf.Min(min.x, c.x), Mathf.Min(min.y, c.y));
                max = new GridPos(Mathf.Max(max.x, c.x), Mathf.Max(max.y, c.y));
            }
            if (cageView != null) Destroy(cageView.gameObject);
            cageView = MonsterCage.Create(GridView.ToWorld(min) + Vector3.up * GridView.SurfaceY, GridView.ToWorld(max) + Vector3.up * GridView.SurfaceY, WorldTheme.Current.accent);

            // The bridge: its tiles in a stone grey, low rails along both sides.
            if (bridgeRails != null) Destroy(bridgeRails);
            bridgeRails = new GameObject("BridgeRails");
            var rail = MaterialFactory.Create(new Color(0.3f, 0.28f, 0.36f), Color.black);
            var lamp = MaterialFactory.Create(WorldTheme.Current.accent, WorldTheme.Current.accent * 1.4f);
            foreach (var b in grid.BridgeTiles)
            {
                gridView.SetTint(b, new Color(0.72f, 0.7f, 0.78f), WorldTheme.Current.accent * 0.6f);
                var at = GridView.ToWorld(b) + Vector3.up * GridView.SurfaceY;
                foreach (float s in new[] { -1f, 1f })
                {
                    Shapes.Rounded("Rail", bridgeRails.transform, at + new Vector3(s * 0.52f, 0.32f, 0f), new Vector3(0.06f, 0.06f, 1.02f), 0.02f, rail);
                    Shapes.Rounded("Post", bridgeRails.transform, at + new Vector3(s * 0.52f, 0.16f, 0f), new Vector3(0.08f, 0.32f, 0.08f), 0.02f, rail);
                    Shapes.Primitive(PrimitiveType.Sphere, "Lamp", bridgeRails.transform, at + new Vector3(s * 0.52f, 0.4f, 0f), Vector3.one * 0.1f, lamp);
                }
            }
        }

        /// <summary>The crowd is beaten and vanG wakes: the cage's bars sink and its tiles are free again.</summary>
        private void OpenCage()
        {
            if (cageView == null) return;
            cageView.Open();
            foreach (var c in grid.CageTiles)
                if (!hunt.MonsterUp || c != hunt.MonsterTile) grid.SetOccupied(c, false);
        }

        /// <summary>vanG is beaten: the bridge to the tunnel opens.</summary>
        private void OpenBridge()
        {
            foreach (var b in grid.BridgeTiles) grid.SetOccupied(b, false);
            if (gateView != null) gateView.Open();
        }

        /// <summary>A blow on the sleeping monster: say why it did nothing (not on every blow).</summary>
        private void OnSleepingHit(GridPos p)
        {
            if (Time.unscaledTime - lastSleepNote < 2.5f) return;
            lastSleepNote = Time.unscaledTime;
            FloatAt(GridView.ToWorld(p) + Vector3.up * 1.4f, Loc.T("float.asleep"), new Color(0.75f, 0.85f, 1f));
        }
    }
}
