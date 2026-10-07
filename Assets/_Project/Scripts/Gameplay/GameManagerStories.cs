using System.Collections;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The story missions added after the monster fight: the Masked Thief to corner and catch, Bip to guard all the way
    /// to the door, and the monster that shields itself and moves after every hit on the upper floors. Like the other
    /// stories they never kill the player by themselves: they keep the robot moving and thinking.
    /// </summary>
    public partial class GameManager
    {
        // ---------- Coin thief ----------

        // A masked bandit with a sack of coins runs around the floor, always away from the robot, preferring open
        // tiles to corners. Bumping into it while it stands still catches it: it is dazed for a moment, drops some
        // coins, and runs a little faster afterwards. Catch it enough times and it gives the whole sack back.
        private Thief thief;
        private GridPos thiefPos;
        private int thiefLeft;
        private float thiefTimer, thiefInterval;
        private bool thiefEscape;

        // In a race (thiefRace) Kuzgun doesn't run from the robot: he runs for the coins. Whoever collects the card's
        // number of coins first wins; the coins come all over the floor, faster than usual.
        private int thiefCoins;

        private void SetupThief()
        {
            objectivesTotal = thiefLeft = Mathf.Max(2, level.keys);
            objectivesDone = 0;
            thiefCoins = 0;
            if (level.thiefRace)
            {
                coins.FocusRadius = 0;
                level.maxCoins = Mathf.Max(level.maxCoins, 4);
                level.coinInterval = Mathf.Min(level.coinInterval, 1.1f);
                level.coinLifetime = Mathf.Max(level.coinLifetime, 7f);
            }
            thiefPos = FarTile() ?? robot.Position;
            grid.SetOccupied(thiefPos, true);
            var tint = Color.Lerp(new Color(0.55f, 0.55f, 0.62f), WorldTheme.Current.accent, 0.25f);
            thief = Thief.Create(GridView.ToWorld(thiefPos) + Vector3.up * GridView.SurfaceY, tint);
            thiefInterval = Mathf.Lerp(0.8f, 0.5f, LevelCatalog.Difficulty(levelIndex));
            thiefTimer = 1.5f;
            hazards.IsProtected = p => p == thiefPos;
        }

        private void UpdateThief(float dt)
        {
            if (thief == null || thiefLeft <= 0 || previewing) return;
            if (thief.Stunned || thief.Hopping) return;
            thiefTimer -= dt;
            if (thiefTimer > 0f) return;
            thiefTimer = thiefInterval * Random.Range(0.85f, 1.25f);

            if (thiefEscape)
            {
                ThiefLeap();
                return;
            }
            if (level.thiefRace)
            {
                RaceStep();
                return;
            }

            // Away from the robot (by walking distance), towards open ground, with a little unpredictability.
            var dist = WalkDistances(robot.Position);
            var best = thiefPos;
            float bestScore = float.MinValue;
            foreach (var d in DirectionExtensions.All)
            {
                var n = thiefPos + d.ToOffset();
                if (!grid.IsStandable(n) || n == robot.Position || hazards.IsThreatened(n)) continue;
                int dn = dist.TryGetValue(n, out int v) ? v : 30;
                int exits = 0;
                foreach (var e in DirectionExtensions.All)
                    if (grid.IsStandable(n + e.ToOffset())) exits++;
                float score = Mathf.Min(dn, 8) * 2f + exits * 0.8f + Random.value * 1.6f;
                if (score > bestScore) { bestScore = score; best = n; }
            }
            if (best == thiefPos) return;
            grid.SetOccupied(thiefPos, false);
            thiefPos = best;
            grid.SetOccupied(thiefPos, true);
            thief.HopTo(GridView.ToWorld(thiefPos) + Vector3.up * GridView.SurfaceY, Mathf.Min(0.3f, thiefInterval * 0.6f));
        }

        /// <summary>The race: one step towards the nearest coin (by walking distance), taking it on arrival.</summary>
        private void RaceStep()
        {
            var dist = WalkDistances(thiefPos);
            GridPos? goalTile = null;
            int best = int.MaxValue;
            foreach (var c in coins.Positions)
                if (dist.TryGetValue(c, out int s) && s < best) { best = s; goalTile = c; }
            if (!goalTile.HasValue) return;
            var toGoal = WalkDistances(goalTile.Value);
            var next = thiefPos;
            int here = toGoal.TryGetValue(thiefPos, out int h) ? h : 99;
            foreach (var d in DirectionExtensions.All)
            {
                var n = thiefPos + d.ToOffset();
                if (!grid.IsStandable(n) || n == robot.Position || hazards.IsThreatened(n)) continue;
                if (toGoal.TryGetValue(n, out int dn) && dn < here) { here = dn; next = n; }
            }
            if (next == thiefPos) return;
            grid.SetOccupied(thiefPos, false);
            thiefPos = next;
            grid.SetOccupied(thiefPos, true);
            thief.HopTo(GridView.ToWorld(thiefPos) + Vector3.up * GridView.SurfaceY, Mathf.Min(0.3f, thiefInterval * 0.6f));
            if (!coins.Take(thiefPos)) return;
            thiefCoins++;
            AudioManager.PlaySfx(Sfx.Coin, 0.6f, 0.7f);
            FloatAt(GridView.ToWorld(thiefPos) + Vector3.up, Loc.F("float.thiefScore", thiefCoins, objectivesTotal), Palette.UiRed);
            if (thiefCoins >= objectivesTotal) Lose(Loc.T("lose.thief"));
        }

        /// <summary>The race is won when the robot has the card's number of coins first.</summary>
        private void CheckRace()
        {
            if (!level.thiefRace || thief == null || State != GameState.Playing) return;
            objectivesDone = Mathf.Min(coinsThisRun, objectivesTotal);
            if (coinsThisRun < objectivesTotal) return;
            thiefLeft = 0;
            thief.Give();
            FloatAt(thief.transform.position + Vector3.up, Loc.T("float.thiefDone"), Palette.UiGold);
            Win();
        }

        private void CatchThief()
        {
            if (level.thiefRace) return; // in a race bumping into him does nothing
            // A dazed thief can't be caught again: it has to be run down anew.
            if (thief == null || thiefLeft <= 0 || thief.Hopping || thief.Stunned || thiefEscape) return;
            thiefLeft--;
            objectivesDone++;
            var at = GridView.ToWorld(thiefPos);
            fx.Burst(at + Vector3.up * 0.6f, Palette.UiGold, Palette.CoinGlow, 30, 5f);
            AudioManager.PlaySfx(Sfx.Coin, 1f, 0.8f);
            cameraRig.Shake(0.5f);
            Haptics.Medium();
            // The coins it drops lie around it for a while.
            foreach (var d in DirectionExtensions.All)
                if (Random.value < 0.7f) coins.Drop(thiefPos + d.ToOffset(), 7f);
            if (thiefLeft <= 0)
            {
                thief.Give();
                grid.SetOccupied(thiefPos, false);
                FloatAt(at + Vector3.up, Loc.T("float.thiefDone"), Palette.UiGold);
                coinsThisRun += 5;
                Win();
                return;
            }
            thief.Catch(1.2f);
            thiefTimer = 0.05f; // the leap comes right after the daze
            thiefEscape = true; // when the daze wears off it leaps away
            thiefInterval *= 0.93f;
            FloatAt(at + Vector3.up, Loc.F("float.thiefCaught", thiefLeft), Palette.UiGold);
        }

        /// <summary>After a catch: a big leap to a free tile 4-7 steps from the robot, so the chase starts over.</summary>
        private void ThiefLeap()
        {
            thiefEscape = false;
            var dist = WalkDistances(robot.Position);
            var options = new List<GridPos>();
            foreach (var kv in dist)
                if (kv.Value >= 4 && kv.Value <= 7 && grid.IsStandable(kv.Key) && !hazards.IsThreatened(kv.Key)) options.Add(kv.Key);
            if (options.Count == 0) return;
            grid.SetOccupied(thiefPos, false);
            thiefPos = options[Random.Range(0, options.Count)];
            grid.SetOccupied(thiefPos, true);
            thief.HopTo(GridView.ToWorld(thiefPos) + Vector3.up * GridView.SurfaceY, 0.5f);
            thiefTimer = 0.6f;
        }

        private void ClearThief()
        {
            thiefEscape = false;
            if (thief != null) Destroy(thief.gameObject);
            thief = null;
            thiefLeft = 0;
        }

        /// <summary>Walking steps from <paramref name="from"/> to every tile the robot could stand on.</summary>
        private Dictionary<GridPos, int> WalkDistances(GridPos from)
        {
            var dist = new Dictionary<GridPos, int> { [from] = 0 };
            var queue = new Queue<GridPos>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in DirectionExtensions.All)
                {
                    var n = p + d.ToOffset();
                    if (dist.ContainsKey(n) || !grid.IsFloor(n)) continue;
                    dist[n] = dist[p] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        // ---------- Kuzgun the ally ----------

        // After the mask comes off (level 211) Kuzgun sometimes works alongside Cell: he hops along a tile away from
        // the robot, never in its way, and points the dangers out by keeping off threatened tiles.
        private Thief ally;
        private GridPos allyPos;
        private float allyTimer;

        private void SetupAlly()
        {
            if (!level.allyKuzgun) return;
            allyPos = SafeTileNear(robot.Position + new GridPos(1, 0), robot.Position);
            if (allyPos == robot.Position) return;
            ally = Thief.Create(GridView.ToWorld(allyPos) + Vector3.up * GridView.SurfaceY, Color.Lerp(new Color(0.55f, 0.55f, 0.62f), WorldTheme.Current.accent, 0.25f));
            allyTimer = 0.5f;
        }

        private void UpdateAlly(float dt)
        {
            if (ally == null || previewing || ally.Hopping) return;
            allyTimer -= dt;
            if (allyTimer > 0f) return;
            allyTimer = 0.25f;
            if (allyPos.Manhattan(robot.Position) <= 1 && !hazards.IsThreatened(allyPos)) return;
            var dist = WalkDistances(robot.Position);
            var best = allyPos;
            int bestD = dist.TryGetValue(allyPos, out int here) ? here : 99;
            if (hazards.IsThreatened(allyPos)) bestD = 99; // get off a tile about to be hit, whatever the distance
            foreach (var d in DirectionExtensions.All)
            {
                var n = allyPos + d.ToOffset();
                if (!grid.IsStandable(n) || n == robot.Position || hazards.IsThreatened(n)) continue;
                if (dist.TryGetValue(n, out int dn) && dn < bestD) { bestD = dn; best = n; }
            }
            if (best == allyPos) return;
            allyPos = best;
            ally.HopTo(GridView.ToWorld(allyPos) + Vector3.up * GridView.SurfaceY, 0.2f);
        }

        private void ClearAlly()
        {
            if (ally != null) Destroy(ally.gameObject);
            ally = null;
        }

        // ---------- Escort ----------

        // Bip, a little lost robot, follows the player's robot a tile behind. The door is open from the start, far
        // away; the level is won when Bip steps through it. Bip waits instead of walking onto a tile that is about to be
        // hit, and a block that lands on it only dazes it for a moment inside a bubble.
        private Buddy buddy;
        private GridPos buddyPos;
        private float buddyTimer;
        private bool buddyHome;
        private int escortStart;

        // Cards may ask for more than the door: keep Bip safe for a while (escortSeconds: no door, the clock counts), or
        // take it to several spots in turn (escortStops: the glowing door moves on to the next spot each time).
        private int stopsLeft;

        private void SetupEscort()
        {
            stopsLeft = Mathf.Max(1, level.escortStops);
            objectivesTotal = stopsLeft;
            objectivesDone = 0;
            doorPos = FarTile(avoidDoor: false) ?? robot.Position;
            portal = ExitPortal.Create(GridView.ToWorld(doorPos) + Vector3.up * GridView.SurfaceY);
            portal.Open();
            hazards.IsProtected = p => p == doorPos;
            buddyPos = robot.Position;
            foreach (var d in DirectionExtensions.All)
            {
                var n = robot.Position + d.ToOffset();
                if (grid.IsStandable(n) && n != doorPos) { buddyPos = n; break; }
            }
            buddy = Buddy.Create(GridView.ToWorld(buddyPos) + Vector3.up * GridView.SurfaceY);
            buddyTimer = 0.6f;
            buddyHome = false;
            var dist = WalkDistances(doorPos);
            escortStart = dist.TryGetValue(buddyPos, out int s) ? Mathf.Max(1, s) : 10;
            if (level.escortSeconds > 0f)
            {
                // No door: just keep Bip safe until the time is up.
                Destroy(portal.gameObject);
                portal = null;
                doorPos = new GridPos(-99, -99);
            }
        }

        private bool EscortTimed => level.escortSeconds > 0f;

        private void UpdateEscort(float dt)
        {
            if (buddy == null || buddyHome || previewing) return;
            if (EscortTimed && elapsed >= level.escortSeconds)
            {
                StartCoroutine(BuddyHome());
                return;
            }
            if (buddy.Dazed || buddy.Hopping) return;
            buddyTimer -= dt;
            if (buddyTimer > 0f) return;
            buddyTimer = 0.22f;

            // Near the door Bip heads in by itself; otherwise it keeps a tile behind the robot.
            var toDoor = EscortTimed ? new Dictionary<GridPos, int>() : WalkDistances(doorPos);
            bool homeRun = toDoor.TryGetValue(buddyPos, out int doorSteps) && doorSteps <= 2;
            if (!homeRun && buddyPos.Manhattan(robot.Position) <= 1) return;
            var target = homeRun ? doorPos : robot.Position;
            var dist = WalkDistances(target);
            var best = buddyPos;
            int bestD = dist.TryGetValue(buddyPos, out int here) ? here : 99;
            foreach (var d in DirectionExtensions.All)
            {
                var n = buddyPos + d.ToOffset();
                // Bip may share the door tile with the robot, never any other.
                if (!grid.IsStandable(n) || (n == robot.Position && n != doorPos) || hazards.IsThreatened(n)) continue;
                if (dist.TryGetValue(n, out int dn) && dn < bestD) { bestD = dn; best = n; }
            }
            if (best == buddyPos) return;
            buddyPos = best;
            buddy.HopTo(GridView.ToWorld(buddyPos) + Vector3.up * GridView.SurfaceY, 0.2f);
            if (buddyPos == doorPos) StartCoroutine(BuddyHome());
        }

        private IEnumerator BuddyHome()
        {
            buddyHome = true;
            objectivesDone++;
            yield return new WaitForSeconds(0.25f);
            if (State != GameState.Playing) yield break;
            if (!EscortTimed && --stopsLeft > 0)
            {
                // A spot reached, more to go: the door moves on to the next one.
                buddy.Cheer();
                fx.Burst(buddy.transform.position + Vector3.up * 0.5f, Palette.UiCyan, Palette.ShieldPickupGlow, 20, 4f);
                FloatAt(buddy.transform.position + Vector3.up, Loc.F("float.escortStop", stopsLeft), Palette.UiCyan);
                if (portal != null) Destroy(portal.gameObject);
                var from = doorPos;
                doorPos = FarTile(avoidDoor: false) ?? doorPos;
                if (doorPos == from) doorPos = SafeTileNear(robot.Position, robot.Position);
                portal = ExitPortal.Create(GridView.ToWorld(doorPos) + Vector3.up * GridView.SurfaceY);
                portal.Open();
                escortStart = Mathf.Max(1, WalkDistances(doorPos).TryGetValue(buddyPos, out int s) ? s : 10);
                buddyHome = false;
                yield break;
            }
            buddy.Cheer();
            fx.Burst(buddy.transform.position + Vector3.up * 0.5f, Palette.UiGold, Palette.CoinGlow, 30, 5f);
            FloatAt(buddy.transform.position + Vector3.up, Loc.T("float.escortDone"), Palette.UiGold);
            Win();
        }

        /// <summary>A block (or a hole or fire) got Bip: dazed in a bubble, then back on the nearest safe tile.</summary>
        private void DazeBuddy(GridPos p)
        {
            if (buddy == null || buddyHome || buddy.Dazed) return;
            buddy.Daze(2.2f);
            BipSay("dazed");
            buddyTimer = 2.2f;
            hazards.Shatter(p);
            var safe = SafeTileNear(buddyPos, robot.Position);
            if (safe != buddyPos)
            {
                buddyPos = safe;
                buddy.Place(GridView.ToWorld(buddyPos) + Vector3.up * GridView.SurfaceY);
            }
            AudioManager.PlaySfx(Sfx.Blocked, 0.9f, 1.3f);
            FloatAt(buddy.transform.position + Vector3.up, Loc.T("float.buddyDazed"), Palette.UiCyan);
        }

        private float EscortProgress()
        {
            if (buddy == null) return 0f;
            if (EscortTimed) return Mathf.Clamp01(elapsed / level.escortSeconds);
            if (buddyHome && stopsLeft <= 1) return 1f;
            var dist = WalkDistances(doorPos);
            int left = dist.TryGetValue(buddyPos, out int s) ? s : escortStart;
            float leg = Mathf.Clamp01(1f - left / (float)escortStart);
            return (objectivesDone + leg) / Mathf.Max(1, objectivesTotal);
        }

        private int EscortStepsLeft()
        {
            if (EscortTimed) return Mathf.CeilToInt(Mathf.Max(0f, level.escortSeconds - elapsed));
            var dist = WalkDistances(doorPos);
            return dist.TryGetValue(buddyPos, out int s) ? s : 0;
        }

        private void ClearEscort()
        {
            if (buddy != null) Destroy(buddy.gameObject);
            buddy = null;
            buddyHome = false;
        }

        // ---------- The shielding monster (upper floors) ----------

        // After every hit but the last, the monster throws up a shield for a few seconds and leaps to another tile, so
        // each hit needs a new plan. While shielded, the hammer bounces off.
        private GameObject monsterShield;
        private float monsterShieldLeft;

        private bool MonsterShielded => monsterShieldLeft > 0f;

        private void MonsterPhase()
        {
            monsterShieldLeft = 3f;
            if (monsterShield == null)
            {
                monsterShield = Shapes.Primitive(PrimitiveType.Sphere, "Shield", monster.transform, new Vector3(0f, 1f, 0f), Vector3.one * 2.4f,
                    MaterialFactory.CreateTransparent(new Color(0.55f, 0.85f, 1f, 0.22f), new Color(0.8f, 1.6f, 2.4f)));
            }
            monsterShield.SetActive(true);
            // A new tile away from the robot, reachable from a few sides and not cutting the floor off.
            var reach = ReachableTiles();
            GridPos best = monsterPos;
            int bestScore = int.MinValue;
            foreach (var t in grid.AllPositions())
            {
                if (!grid.IsStandable(t) || !reach.Contains(t) || t.Manhattan(robot.Position) < 4 || hazards.IsThreatened(t)) continue;
                var without = ReachableTiles(t, princessPos);
                int sides = OpenSides(t, without);
                if (sides < 2 || reach.Count - 1 - without.Count > 0) continue;
                int score = -Mathf.Abs(t.Manhattan(robot.Position) - 6) * 3 + sides + Random.Range(0, 3);
                if (score > bestScore) { bestScore = score; best = t; }
            }
            if (best == monsterPos) return;
            grid.SetOccupied(monsterPos, false);
            fx.Dust(GridView.ToWorld(monsterPos) + Vector3.up * 0.1f, Palette.TileTop, 18, 3f);
            monsterPos = best;
            grid.SetOccupied(monsterPos, true);
            StartCoroutine(MonsterLeap(GridView.ToWorld(monsterPos) + Vector3.up * GridView.SurfaceY));
        }

        private IEnumerator MonsterLeap(Vector3 to)
        {
            var from = monster.transform.position;
            for (float t = 0f; t < 1f && monster != null; t += Time.deltaTime / 0.55f)
            {
                monster.transform.position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2.2f;
                yield return null;
            }
            if (monster == null) yield break;
            monster.transform.position = to;
            cameraRig.Shake(0.6f);
            fx.Dust(to + Vector3.up * 0.1f, Palette.TileTop, 24, 4f);
            AudioManager.PlaySfx(Sfx.Impact, 0.9f, 0.7f);
        }

        private void UpdateMonsterShield(float dt)
        {
            if (monsterShieldLeft <= 0f) return;
            monsterShieldLeft -= dt;
            if (monsterShield != null)
            {
                float s = 2.4f + Mathf.Sin(Time.time * 6f) * 0.08f;
                monsterShield.transform.localScale = Vector3.one * s;
                if (monsterShieldLeft <= 0f) monsterShield.SetActive(false);
            }
        }

        private void ClearMonsterShield()
        {
            monsterShieldLeft = 0f;
            monsterShield = null; // a child of the monster, destroyed with it
        }
    }
}
