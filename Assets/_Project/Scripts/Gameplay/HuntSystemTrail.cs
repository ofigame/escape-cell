using System;
using System.Collections.Generic;
using SquashBot.Core;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The open world's roads (see GameManagerTrail): no vanG at the end, robots placed in bands by the clearings, and
    /// the chests' guardians — poison spitters. A spitter keeps close to its chest; when foi comes within seven tiles it
    /// winds up (the tiles round foi glow red) and lobs a green glob that leaves a poison puddle there for a while. When
    /// the last guardian of a chest falls, the chest is free to open.
    /// </summary>
    public partial class HuntSystem
    {
        /// <summary>No monster on this floor (the roads): clearing the robots never raises vanG.</summary>
        public bool NoMonster;

        /// <summary>The battle arenas and their roads: enemy robots wear red (foi's allies are blue).</summary>
        public bool RedFoes;

        /// <summary>A spitter's glob landed: the tiles of its poison puddle.</summary>
        public event Action<List<GridPos>> Poisoned;
        /// <summary>The last guardian of a chest fell: the chest's number.</summary>
        public event Action<int> ChestFreed;

        private static readonly Color PoisonColour = new Color(0.45f, 0.95f, 0.3f);

        /// <summary>A robot of a road's band, or (spitter) a chest's guardian kept near its chest.</summary>
        public void AddTrailGuard(GridPos p, bool spitter, GridPos home, int chestId, int rank = 0)
        {
            if (!grid.IsStandable(p) || grid.IsOccupied(p)) return;
            AddGuard(p, brute: spitter, rank: spitter ? 97 : rank);
            var g = guards[guards.Count - 1];
            g.spitter = spitter;
            g.home = home;
            g.chestId = chestId;
            if (spitter) { g.hp = g.maxHp = level.robotHp * 3 + 3; g.cooldown = 1.5f; }
            Total++;
        }

        /// <summary>
        /// A chest's vanG: the monster's model, fighting like a warlord (an area smash) and keeping by its chest. Its
        /// health grows road by road.
        /// </summary>
        public void AddChestBoss(GridPos p, GridPos home, int chestId, int hp)
        {
            if (CreateMonster == null || !grid.IsStandable(p) || grid.IsOccupied(p)) return;
            var g = new Guard { pos = p, brute = true, rank = 99, hp = hp, maxHp = hp, home = home, chestId = chestId, step = 0.5f, cooldown = 1.5f };
            g.root = new GameObject("ChestVanG").transform;
            g.root.SetParent(transform, false);
            g.root.position = At(p);
            g.mon = CreateMonster(p, hp);
            g.mon.transform.SetParent(g.root, true);
            g.mon.transform.localPosition = Vector3.zero;
            g.from = g.to = At(p);
            grid.SetOccupied(p, true);
            guards.Add(g);
            Total++;
        }

        private Guard challengeFoe;

        /// <summary>
        /// The challenge's rival foi for the duel: <paramref name="look"/> (a copy of foi in the rival's colours) fighting
        /// like a warlord (an area smash), taking <paramref name="hp"/> blows and each of its blows taking
        /// <paramref name="hitShare"/> of foi's health.
        /// </summary>
        public void AddChallengeFoe(GridPos p, int hp, float hitShare, Transform look)
        {
            if (!grid.IsStandable(p)) return;
            var g = new Guard { pos = p, brute = true, rank = 98, hp = hp, maxHp = hp, step = 0.5f, cooldown = 1.2f, share = hitShare };
            g.root = new GameObject("RivalFoi").transform;
            g.root.SetParent(transform, false);
            g.root.position = At(p);
            look.SetParent(g.root, false);
            look.localPosition = Vector3.zero;
            g.model = look;
            AddHealthBar(g);
            g.from = g.to = At(p);
            grid.SetOccupied(p, true);
            guards.Add(g);
            Total++;
            challengeFoe = g;
        }

        /// <summary>The rival foi has fallen (or there is none).</summary>
        public bool ChallengeFoeDown => challengeFoe == null || challengeFoe.dead;

        /// <summary>A free tile near <paramref name="p"/> (no robot, no ally, not foi's), or null.</summary>
        private GridPos? FreeNear(GridPos p)
        {
            for (int r = 0; r <= 4; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var q = p + new GridPos(dx, dy);
                        if (grid.IsStandable(q) && !grid.IsOccupied(q) && !AllyAt(q) && q != robot.Position) return q;
                    }
            return null;
        }

        /// <summary>
        /// A soldier of the rival's army carried into the duel: its own model (<paramref name="body"/>, the soldier as it
        /// stood) moved onto the nearest free tile, fighting as a robot of the given health.
        /// </summary>
        public void AddChallengeSoldier(Vector3 at, int hp, Transform body, GuardBot bot)
        {
            var tile = FreeNear(new GridPos(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.z)));
            if (!tile.HasValue) { Destroy(body.gameObject); return; }
            var g = new Guard { pos = tile.Value, hp = Mathf.Max(1, hp), maxHp = Mathf.Max(1, hp), rank = 1, step = 0.4f, cooldown = 0.8f };
            g.root = body;
            g.root.SetParent(transform, true);
            g.root.position = At(tile.Value);
            g.bot = bot;
            AddHealthBar(g);
            g.from = g.to = At(tile.Value);
            grid.SetOccupied(tile.Value, true);
            guards.Add(g);
            Total++;
        }

        /// <summary>One of the player's soldiers carried into the duel as an ally (its own model).</summary>
        public void AddChallengeAlly(Vector3 at, int hp, Transform body, GuardBot bot)
        {
            var tile = FreeNear(new GridPos(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.z)));
            if (!tile.HasValue) { Destroy(body.gameObject); return; }
            var a = new Guard { pos = tile.Value, hp = Mathf.Max(1, hp), maxHp = Mathf.Max(1, hp), rank = -1, step = 0.3f, ally = true };
            a.root = body;
            a.root.SetParent(transform, true);
            a.root.position = At(tile.Value);
            a.bot = bot;
            AddHealthBar(a);
            a.from = a.to = At(tile.Value);
            allies.Add(a);
        }

        public void ClearChallengeFoe() => challengeFoe = null;

        /// <summary>A spitter's turn: back towards its chest if it strayed, else face foi and lob poison when in range.</summary>
        private void UpdateSpitter(Guard g, float dt)
        {
            var toFoi = robot.transform.position - g.root.position;
            toFoi.y = 0f;
            if (toFoi.sqrMagnitude > 0.01f) g.root.rotation = Quaternion.Slerp(g.root.rotation, Quaternion.LookRotation(toFoi), dt * 6f);
            int near = Chebyshev(robot.Position, g.pos);
            if (g.windup >= 0f)
            {
                g.windup += dt;
                float k = g.windup / 0.9f;
                g.SetRaise(k * 1.3f);
                foreach (var t in g.area) view.SetWarning(t, 0.4f + 0.6f * k);
                if (k < 1f) return;
                g.windup = -1f;
                g.cooldown = 2.4f;
                g.SetRaise(0f);
                foreach (var t in g.area) view.SetWarning(t, 0f);
                var tiles = new List<GridPos>(g.area);
                g.area.Clear();
                ThrownRock.Create(g.root.position + Vector3.up * 1.6f, At(g.target), 0.45f, PoisonColour);
                AudioPlay(0.8f);
                StartCoroutine(Splash(tiles, 0.45f));
                return;
            }
            if (g.cooldown <= 0f && near <= 7 && !PlayerAway)
            {
                // Wind up: foi's tile and a couple round it.
                g.windup = 0f;
                g.target = robot.Position;
                g.area.Clear();
                g.area.Add(g.target);
                foreach (var d in DirectionExtensions.All)
                    if (rng.Next(2) == 0 && grid.IsFloor(g.target + d.ToOffset())) g.area.Add(g.target + d.ToOffset());
                return;
            }
            // Keep near the chest: step back when more than three tiles off.
            if (g.moveT < 1f) return;
            g.step -= dt;
            if (g.step > 0f || Chebyshev(g.pos, g.home) <= 3) return;
            g.step = level.robotStep * 1.2f;
            GridPos best = g.pos;
            int bestD = g.home.Manhattan(g.pos);
            foreach (var d in DirectionExtensions.All)
            {
                var n = g.pos + d.ToOffset();
                if (!CanStep(g.pos, n)) continue;
                int nd = g.home.Manhattan(n);
                if (nd < bestD) { bestD = nd; best = n; }
            }
            if (best != g.pos) MoveGuard(g, best, level.robotStep);
        }

        private System.Collections.IEnumerator Splash(List<GridPos> tiles, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!running) yield break;
            foreach (var t in tiles) fx.Burst(At(t) + Vector3.up * 0.2f, PoisonColour, PoisonColour * 2f, 10, 3f);
            Poisoned?.Invoke(tiles);
        }

        private void AudioPlay(float pitch) => Audio.AudioManager.PlaySfx(Audio.Sfx.Hop, 0.7f, pitch);

        /// <summary>A guard fell: if it was a chest's last guardian, the chest is free.</summary>
        private void CheckChestFreed(Guard dead)
        {
            if (dead.chestId < 0) return;
            foreach (var o in guards) if (!o.dead && o.chestId == dead.chestId) return;
            ChestFreed?.Invoke(dead.chestId);
        }
    }
}
