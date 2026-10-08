using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Monster levels in two parts. On the isometric floor the robot dodges the rain and collects hammer blows (ammo,
    /// up to what its hammer holds). A glowing ring marks the monster's arena: stepping inside with blows in the bag
    /// starts the fight over the robot's shoulder (<see cref="ArenaFight"/>). Out of blows, the robot runs out of the
    /// ring and the isometric view comes back. The floor's dangers wait while the fight goes on.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Monster health per "hit" on the level card; the hammer deals 10-32 per blow.</summary>
        private const int MonsterPointsPerHit = 10;
        /// <summary>The fight ring's radius (world units around the monster's tile).</summary>
        private const float ArenaRadius = 2.8f;

        private ArenaFight arena;
        private GameObject arenaZone;
        private Material zoneMat;
        private GridPos zoneCentre;
        /// <summary>The arena is every tile this many steps (or fewer) from the monster, diagonals included.</summary>
        private const int ZoneReach = 2;
        private float arenaCooldown, needAmmoShown;
        private bool arenaTaught;

        private bool ArenaActive => arena != null && arena.Active;

        private Vector3 MonsterCentre => GridView.ToWorld(monsterPos) + Vector3.up * GridView.SurfaceY;

        private bool InArenaZone(GridPos t)
        {
            if (monster == null || monster.Dead) return false;
            return Mathf.Abs(t.x - monsterPos.x) <= ZoneReach && Mathf.Abs(t.y - monsterPos.y) <= ZoneReach;
        }

        private void EnsureArena()
        {
            if (arena != null) return;
            arena = ArenaFight.Create(robot, cameraRig, fx);
            arena.Ammo = () => duel ? 99 : hammerAmmo;
            arena.SpendAmmo = () => { if (!duel) hammerAmmo = Mathf.Max(0, hammerAmmo - 1); RefreshHud(); };
            arena.CanAct = () => State == GameState.Playing;
            arena.Ignore = p => ui != null && ui.IsOverTool(p);
            arena.Struck += dmg => { if (duel) OnDuelStruck(); else OnArenaStruck(dmg); };
            arena.Hurt += weight => { if (duel) OnDuelHurt(weight); else OnArenaHurt(weight); };
            arena.Exited += at => { if (duel) DuelEscape(); else EndArenaFight(true); };
            arena.OutOfAmmo += () =>
            {
                FloatAt(robot.transform.position + Vector3.up * 0.8f, Loc.T("float.noAmmo"), Palette.UiRed);
                BipSay("noAmmo");
            };
        }

        /// <summary>The arena: the two rows of tiles around the monster glow (and follow it when it leaps).</summary>
        private void SetupArenaRing()
        {
            EnsureArena();
            arenaCooldown = 0f;
            BuildZone(monsterPos);
        }

        /// <summary>Lights the tiles within two steps of <paramref name="centre"/> (the fight starts on any of them).</summary>
        private void BuildZone(GridPos centre)
        {
            if (arenaZone == null)
            {
                arenaZone = new GameObject("ArenaZone");
                zoneMat = MaterialFactory.Create(new Color(1f, 0.62f, 0.3f), new Color(1.2f, 0.5f, 0.2f));
            }
            for (int i = arenaZone.transform.childCount - 1; i >= 0; i--) Destroy(arenaZone.transform.GetChild(i).gameObject);
            zoneCentre = centre;
            for (int dx = -ZoneReach; dx <= ZoneReach; dx++)
                for (int dy = -ZoneReach; dy <= ZoneReach; dy++)
                {
                    var t = new GridPos(centre.x + dx, centre.y + dy);
                    if ((dx == 0 && dy == 0) || !grid.InBounds(t) || !grid.IsFloor(t)) continue;
                    Shapes.Rounded("Zone", arenaZone.transform, GridView.ToWorld(t) + Vector3.up * (GridView.SurfaceY + 0.015f), new Vector3(0.8f, 0.03f, 0.8f), 0.04f, zoneMat);
                }
            arenaZone.SetActive(true);
        }

        private void ShowZone(bool on)
        {
            if (arenaZone != null) arenaZone.SetActive(on);
        }

        private void UpdateArenaRing()
        {
            if (arenaZone == null || !arenaZone.activeSelf) return;
            if (!duel && monster != null && monsterPos != zoneCentre) BuildZone(monsterPos); // the monster leapt
            // A soft glow while the bag is empty, a hot pulse when blows are ready: go in!
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (charged || duel ? 6f : 2f));
            var c = charged || duel ? new Color(1f, 0.4f, 0.25f) : new Color(1f, 0.75f, 0.4f);
            MaterialFactory.SetColors(zoneMat, c, c * (0.5f + 1.1f * pulse));
        }

        /// <summary>The monster's pose for an attack: 0 a slam, 1 a swipe, 2 a throw.</summary>
        private void MonsterPose(int kind)
        {
            if (monster == null) return;
            if (kind == 0) monster.Stomp();
            else if (kind == 1) monster.Swipe();
            else monster.Throw();
            AudioManager.PlaySfx(Sfx.Warning, 0.5f, kind == 0 ? 0.6f : 0.9f);
        }

        /// <summary>Called whenever the robot lands on a tile: a tile of the ring starts the fight.</summary>
        private void CheckArenaEntry(GridPos p)
        {
            if (level.mission != MissionType.Monster || ArenaActive || !InArenaZone(p)) return;
            TryEnterArena();
        }

        private void TryEnterArena()
        {
            if (State != GameState.Playing || previewing || monster == null || monster.Dead || ArenaActive || arenaCooldown > 0f) return;
            if (MonsterShielded)
            {
                FloatAt(MonsterCentre + Vector3.up, Loc.T("float.monsterShield"), Palette.UiCyan);
                return;
            }
            if (hammerAmmo <= 0)
            {
                if (Time.time - needAmmoShown > 2f)
                {
                    needAmmoShown = Time.time;
                    FloatAt(MonsterCentre + Vector3.up, Loc.T("float.needAmmo"), Palette.UiCyan);
                }
                return;
            }

            EnsureArena();
            // The ring is cleared for the fight, and the floor's dangers wait.
            foreach (var t in grid.AllPositions())
                if (InArenaZone(t)) hazards.Shatter(t);
            hazards.Freeze();
            floorRules.Freeze();
            enemies.Freeze();
            hunt.Freeze();
            powerUps.Freeze();
            if (aura != null) aura.SetActive(false);
            arena.Begin(() => monster == null || monster.Dead, MonsterPose, MonsterCentre, ArenaRadius, LevelCatalog.Difficulty(levelIndex), Weapons.Level, null);
            AudioManager.PlaySfx(Sfx.Warning, 0.8f, 0.8f);
            if (!arenaTaught)
            {
                arenaTaught = true;
                ui.Bip.Say(Loc.T("bip.arena"));
            }
            RefreshHud();
        }

        /// <summary>
        /// Leaves the fight. <paramref name="place"/>: the robot hops back onto the closest free tile outside the ring
        /// (it ran out, or was knocked out); otherwise it stays where it is (the level is over).
        /// </summary>
        private void EndArenaFight(bool place)
        {
            if (!ArenaActive) return;
            var from = robot.transform.position;
            arena.End();
            if (State == GameState.Playing)
            {
                hazards.Resume();
                floorRules.Resume();
                enemies.Resume();
                hunt.Resume();
                powerUps.Resume();
            }
            if (aura != null) aura.SetActive(charged);
            arenaCooldown = 1.2f;
            if (!place) return;

            GridPos best = robot.Position;
            float bestD = float.MaxValue;
            foreach (var t in grid.AllPositions())
            {
                if (!grid.IsStandable(t) || InArenaZone(t) || hazards.IsThreatened(t)) continue;
                float d = (GridView.ToWorld(t) - new Vector3(from.x, 0f, from.z)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            robot.RescueTo(best);
            RefreshHud();
        }

        /// <summary>A hammer blow landed.</summary>
        private void OnArenaStruck(int damage)
        {
            if (monster == null || monster.Dead) return;
            var at = MonsterCentre + Vector3.up * 0.9f;
            if (MonsterShielded)
            {
                FloatAt(at, Loc.T("float.monsterShield"), Palette.UiCyan);
                AudioManager.PlaySfx(Sfx.Blocked, 0.8f, 1.4f);
                return;
            }
            monsterHp = Mathf.Max(0, monsterHp - damage);
            objectivesDone = objectivesTotal - monsterHp;
            fx.Burst(at, HammerModels.Glow(Weapons.Level), ThunderHammer.ElectricGlow * 1.2f, 36, 6f);
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 0.7f);
            cameraRig.Punch(0.8f);
            Haptics.Medium();
            FloatAt(at, "-" + damage, Palette.UiGold);

            if (monsterHp <= 0)
            {
                cameraRig.StartCoroutine(FinalBlow(at));
                return;
            }
            cameraRig.StartCoroutine(HitStop(0.07f));
            monster.Hit(monsterHp);
            RefreshHud();
            // Upper floors: the monster shields up and leaps away now and then, ending the round.
            if (level.phased && Random.value < 0.35f)
            {
                EndArenaFight(true);
                MonsterPhase();
            }
        }

        /// <summary>A heavy blow lands: the world stops for a blink.</summary>
        private System.Collections.IEnumerator HitStop(float seconds)
        {
            if (Time.timeScale < 0.99f) yield break;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (State == GameState.Playing && Time.timeScale < 0.1f) Time.timeScale = 1f;
        }

        /// <summary>The last blow: a white flash, slow motion over the shoulder while the monster falls, then the win.</summary>
        private System.Collections.IEnumerator FinalBlow(Vector3 at)
        {
            robot.GiveShield(3f, 999);
            monster.Defeat();
            ui.Flash(Color.white, 0.7f);
            Shockwave.Create(MonsterCentre, 3f, new Color(1f, 0.9f, 0.6f));
            fx.Burst(at, Palette.UiGold, Palette.CoinGlow * 1.5f, 70, 9f);
            cameraRig.Shake(1.2f);
            Haptics.Medium();
            Time.timeScale = 0.3f;
            yield return new WaitForSecondsRealtime(0.9f);
            while (State == GameState.Paused) yield return null;
            Time.timeScale = 1f;
            if (State != GameState.Playing) yield break;
            {
                EndArenaFight(false);
                grid.SetOccupied(monsterPos, false);
                if (questGoal != null)
                {
                    questGoal.Complete();
                    grid.SetOccupied(princessPos, false);
                    FloatAt(GridView.ToWorld(princessPos) + Vector3.up, Loc.T("quest.done.Princess"), Palette.UiGold);
                }
                ShowZone(false);
                if (level.cage)
                {
                    // The cage bursts open and Princess Mira is free.
                    FreedMira.Create(MonsterCentre, robot.transform.position);
                    fx.Burst(at, PrincessMira.Light, new Color(2.4f, 1.8f, 2.4f), 50, 6f);
                    FloatAt(at, Loc.T("float.miraFree"), PrincessMira.Light);
                    ui.Bip.Say(Loc.T("mira.thanks"), Loc.T("story.mira"), PrincessMira.Light);
                }
                else FloatAt(at, Loc.T("float.monsterDown"), Palette.UiGold);
                AudioManager.PlaySfx(Sfx.Squash, 1f, 0.5f);
                Win();
            }
        }

        /// <summary>The monster's blow landed on the robot.</summary>
        private void OnArenaHurt(float weight)
        {
            if (!HealthEnabled)
            {
                // One-hit floors: the blow throws the robot out of the ring and knocks a hammer blow out of the bag.
                hammerAmmo = Mathf.Max(0, hammerAmmo - 1);
            }
            ArenaDamage(weight, knockOut: true);
        }

        /// <summary>Every playing frame of a monster level.</summary>
        private void UpdateArena(float dt)
        {
            if (arenaCooldown > 0f) arenaCooldown -= dt;
            UpdateArenaRing();
            UpdateMonsterThrows(dt);
            if (arena != null) arena.SpeedBoost = superSkillLeft > 0f ? 1.5f : 1f;
            // Nothing stands between the robot and the monster during the fight.
            if (ArenaActive && !duel)
                foreach (var t in grid.AllPositions())
                    if (InArenaZone(t)) hazards.Shatter(t);
        }

        // ---------- The monster attacks from afar ----------

        // Outside the ring the monster isn't idle either: now and then it hurls a boulder (the cage, an electric ball)
        // at the robot's tile. The tile flashes while it flies; whoever still stands there is hit.
        private readonly System.Collections.Generic.List<(GridPos tile, float left)> throws = new System.Collections.Generic.List<(GridPos, float)>();
        private float throwTimer = 3f;

        private void UpdateMonsterThrows(float dt)
        {
            if (level.mission != MissionType.Monster || monster == null || monster.Dead || previewing || ArenaActive)
            {
                throws.Clear();
                return;
            }
            float d = LevelCatalog.Difficulty(levelIndex);
            throwTimer -= dt;
            if (throwTimer <= 0f)
            {
                throwTimer = Mathf.Lerp(5.5f, 2.6f, d) + Random.Range(0f, 1.2f);
                var target = robot.Position;
                if (target.Manhattan(monsterPos) <= 8 && !InArenaZone(target) && grid.IsStandable(target))
                {
                    float flight = Mathf.Lerp(1.3f, 0.95f, d);
                    throws.Add((target, flight));
                    monster.Throw();
                    var tint = level.cage ? new Color(0.5f, 0.85f, 1f) : new Color(0.6f, 0.5f, 0.4f);
                    ThrownRock.Create(MonsterCentre + Vector3.up * 1.7f, GridView.ToWorld(target) + Vector3.up * (GridView.SurfaceY + 0.2f), flight, tint);
                    AudioManager.PlaySfx(Sfx.Warning, 0.6f, 0.8f);
                }
            }
            for (int i = throws.Count - 1; i >= 0; i--)
            {
                var (tile, left) = throws[i];
                left -= dt;
                gridView.SetWarning(tile, 0.55f + 0.45f * Mathf.Sin(Time.time * 24f));
                if (left > 0f)
                {
                    throws[i] = (tile, left);
                    continue;
                }
                throws.RemoveAt(i);
                var at = GridView.ToWorld(tile) + Vector3.up * GridView.SurfaceY;
                Shockwave.Create(at, 0.8f, level.cage ? new Color(0.5f, 0.85f, 1f) : new Color(0.85f, 0.7f, 0.5f));
                fx.Burst(at + Vector3.up * 0.2f, new Color(0.6f, 0.5f, 0.45f), new Color(0.8f, 0.6f, 0.4f), 18, 4f);
                cameraRig.Shake(0.4f);
                AudioManager.PlaySfx(Sfx.Impact, 0.7f, 0.9f);
                if (robot.Position == tile && !robot.IsHopping && !robot.IsShielded && !robot.IsHovering) OnBlockImpact(tile);
                if (State != GameState.Playing) return;
            }
        }

        /// <summary>The robot's hammer over its shoulder while it carries blows (the hammer of its level).</summary>
        private GameObject ShoulderHammer()
        {
            var held = new GameObject("ShoulderHammer");
            held.transform.SetParent(robot.transform, false);
            held.transform.localPosition = new Vector3(0.32f, 0.35f, -0.08f);
            held.transform.localRotation = Quaternion.Euler(-20f, 0f, -35f);
            HammerModels.Build(held.transform, Weapons.Level, 0.75f);
            held.AddComponent<HeldHammerBob>();
            return held;
        }

        /// <summary>The Thunder Hammer boost: the bag starts full.</summary>
        private void FillHammerBag()
        {
            hammerAmmo = Weapons.MaxAmmo;
            if (aura == null) aura = ShoulderHammer();
            FloatAt(robot.transform.position, Loc.F("float.ammo", hammerAmmo, Weapons.MaxAmmo), ThunderHammer.Electric);
        }

        private void ClearArena()
        {
            duel = false;
            if (ArenaActive) arena.End();
            ShowZone(false);
        }

        // ---------- The thief duel ----------

        // Catching Kuzgun starts a short duel in a small ring around him: two hammer blows crack his mask and the
        // catch counts; two of his blows (or running out of the ring) and he slips away to be chased again. Blows
        // don't run out in a duel.
        private const int DuelStrikes = 2, DuelHurts = 2;
        private const float DuelRadius = 2.3f;
        private bool duel;
        private int duelStrikes, duelHurts;

        private void StartDuel()
        {
            EnsureArena();
            BuildZone(thiefPos);
            duel = true;
            duelStrikes = duelHurts = 0;
            var centre = GridView.ToWorld(thiefPos) + Vector3.up * GridView.SurfaceY;
            foreach (var t in grid.AllPositions())
                if (Mathf.Abs(t.x - thiefPos.x) <= 2 && Mathf.Abs(t.y - thiefPos.y) <= 2) hazards.Shatter(t);
            hazards.Freeze();
            floorRules.Freeze();
            enemies.Freeze();
            hunt.Freeze();
            powerUps.Freeze();
            arena.Begin(() => thief == null, null, centre, DuelRadius, Mathf.Clamp01(LevelCatalog.Difficulty(levelIndex) + 0.2f), Weapons.Level, null);
            FloatAt(centre + Vector3.up * 1.2f, Loc.T("float.duel"), Palette.UiGold);
            AudioManager.PlaySfx(Sfx.Warning, 0.8f, 1.1f);
            BipSay("duel");
        }

        private void OnDuelStruck()
        {
            if (thief == null) return;
            duelStrikes++;
            var at = thief.transform.position + Vector3.up * 0.8f;
            fx.Burst(at, Palette.UiGold, Palette.CoinGlow, 26, 5f);
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 1.1f);
            cameraRig.Punch(0.6f);
            Haptics.Medium();
            if (duelStrikes < DuelStrikes)
            {
                FloatAt(at, Loc.T("float.maskCrack"), Palette.UiGold);
                return;
            }
            EndDuel();
            ThiefCaught();
        }

        private void OnDuelHurt(float weight)
        {
            if (!ArenaDamage(weight, knockOut: false)) return; // the robot lost the level
            if (++duelHurts >= DuelHurts) DuelEscape();
        }

        /// <summary>Kuzgun got away this time: the duel ends and he leaps off.</summary>
        private void DuelEscape()
        {
            EndDuel();
            if (thief == null) return;
            FloatAt(thief.transform.position + Vector3.up, Loc.T("float.thiefSlipped"), Palette.UiRed);
            thiefEscape = true;
            thiefTimer = 0.05f;
        }

        private void EndDuel()
        {
            if (!duel) return;
            EndArenaFight(true);
            duel = false;
            ShowZone(false);
        }

        /// <summary>
        /// A blow on the robot during a fight: health takes it (a rescue charge saves the last sliver). On one-hit floors
        /// the robot is only thrown out (<paramref name="knockOut"/>). False when the level is lost.
        /// </summary>
        private bool ArenaDamage(float weight, bool knockOut)
        {
            BreakCombo();
            ui.Flash(new Color(1f, 0.15f, 0.15f), 0.35f);
            cameraRig.Shake(1f);
            Haptics.Medium();
            fx.Burst(robot.transform.position + Vector3.up * 0.4f, new Color(1f, 0.35f, 0.35f), new Color(2.4f, 0.5f, 0.4f), 22, 5f);
            if (!HealthEnabled)
            {
                AudioManager.PlaySfx(Sfx.Squash, 0.7f, 1.3f);
                FloatAt(robot.transform.position + Vector3.up * 0.6f, Loc.T("float.knockedOut"), Palette.UiRed);
                if (knockOut) EndArenaFight(true);
                return true;
            }
            float damage = HitShare(levelIndex) * weight * (1f - Shop.ArmorShare);
            if (health - damage > 0.001f)
            {
                health -= damage;
                AudioManager.PlaySfx(Sfx.Squash, 0.7f, 1.4f);
                FloatAt(robot.transform.position + Vector3.up * 0.5f, "-" + Mathf.RoundToInt(damage * 100f) + "%", Palette.UiRed);
                if (health < 0.35f) BipSay("lowHealth");
                RefreshHealthBar();
                return true;
            }
            if ((RescueEnabled && rescues > 0) || Shop.TryUse(Boost.ExtraRescue))
            {
                if (RescueEnabled && rescues > 0) rescues--;
                health = 0.2f;
                robot.GiveShield(1.5f);
                FloatAt(robot.transform.position + Vector3.up * 0.5f, Loc.T("float.rescued"), Palette.UiCyan);
                RefreshHealthBar();
                return true;
            }
            health = 0f;
            RefreshHealthBar();
            EndArenaFight(false);
            duel = false;
            robot.Squash();
            AudioManager.PlaySfx(Sfx.Squash);
            Haptics.Death();
            Lose(Loc.T(level.mission == MissionType.Thief ? "lose.thiefDuel" : level.cage ? "lose.cage" : "lose.monster"));
            return false;
        }

        /// <summary>The HUD line of a monster level.</summary>
        private string ArenaHud()
        {
            int percent = Mathf.CeilToInt(monsterHp * 100f / Mathf.Max(1, objectivesTotal));
            string kind = level.cage ? "hud.cage" : "hud.arena";
            return ArenaActive ? Loc.F(kind + "Fight", hammerAmmo, percent) : Loc.F(kind + "Ammo", hammerAmmo, Weapons.MaxAmmo, percent);
        }
    }
}
