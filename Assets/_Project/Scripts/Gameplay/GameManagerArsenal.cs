using System.Collections;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The special gear of the core loop: the whirl blades (every blow is a full turn that hits everything around foi
    /// at once) and bombs (from level 5, four blast bombs a floor, six once bought: thrown at the thickest knot of
    /// enemies within six tiles, they blow up the 3x3 square where they land, or 5x5 at the top level).
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Bombs of each kind left this floor, and the kind the bomb button throws.</summary>
        private readonly int[] bombsLeft = new int[Bombs.Kinds];
        private BombKind bombKind;

        private void SetupBombs()
        {
            ResetPowers(); // a new floor: the ultimate's bar empty, the crowd skills counted out
            bool carry = levelIndex >= Bombs.FromLevel && !bonusRun;
            // Every floor from the fifth level brings blast bombs (more once bought); ice and fire once bought.
            for (int k = 0; k < Bombs.Kinds; k++)
                bombsLeft[k] = !carry ? 0 : k == (int)BombKind.Blast ? Bombs.PerFloor + (Bombs.Owned ? 2 : 0) : Bombs.Has((BombKind)k) ? Bombs.PerFloor : 0;
            if (bombsLeft[(int)bombKind] <= 0) NextBombKind(quiet: true);
            ShowBombs();
        }

        private void HideBombs()
        {
            for (int k = 0; k < Bombs.Kinds; k++) bombsLeft[k] = 0;
            ui.SetBombs(0, false, bombKind, false);
            if (gunSight != null) gunSight.Hide();
            sightOn = false;
            ui.SetFire(false, false);
        }

        private void ShowBombs()
        {
            int kinds = 0;
            for (int k = 0; k < Bombs.Kinds; k++) if (bombsLeft[k] > 0) kinds++;
            ui.SetBombs(bombsLeft[(int)bombKind], true, bombKind, kinds > 1);
        }

        /// <summary>The small button beside the bomb: the next kind foi still has.</summary>
        private void NextBombKind(bool quiet = false)
        {
            for (int i = 1; i <= Bombs.Kinds; i++)
            {
                var k = (BombKind)(((int)bombKind + i) % Bombs.Kinds);
                if (bombsLeft[(int)k] <= 0) continue;
                bombKind = k;
                break;
            }
            if (!quiet) { AudioManager.PlaySfx(Sfx.Click, 0.8f, 1.2f); ShowBombs(); }
        }

        /// <summary>A whirl: foi spins with its blades and every enemy within reach takes the blow.</summary>
        private void WhirlStrike(WeaponDef w, int damage)
        {
            if (heldWeapon != null) heldWeapon.Spin();
            var glow = WeaponModels.GlowOf(w);
            var centre = robot.transform.position;
            Shockwave.Create(centre, 2.8f, glow);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                SlashFx.Create(centre, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.6f, glow, 0.9f + 0.01f * damage);
            }
            AudioManager.PlaySfx(Sfx.Hop, 0.9f, 0.6f);
            int hits = 0;
            for (int x = -w.reach; x <= w.reach; x++)
                for (int y = -w.reach; y <= w.reach; y++)
                {
                    if (x == 0 && y == 0) continue;
                    var p = robot.Position + new GridPos(x, y);
                    if (hunt.Strike(p, damage)) hits++;
                    else if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) == 1 && hazards.Shatter(p)) hits++;
                }
            cameraRig.Shake(0.2f + 0.06f * hits);
        }

        /// <summary>The bomb button: one bomb flies to the best spot and goes off when it lands.</summary>
        private void ThrowBomb()
        {
            var kind = bombKind;
            if (State != GameState.Playing || level == null || level.mission != MissionType.Hunt || roadPhase != RoadPhase.None || bombsLeft[(int)kind] <= 0) return;
            int radius = Bombs.RadiusOf(kind);
            if (!hunt.BombSpot(robot.Position, Bombs.ThrowRange, radius, out var at))
            {
                FloatAt(robot.transform.position + Vector3.up * 1.2f, Loc.T("float.noBombTarget"), UI.UiKit.Style.gold);
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                return;
            }
            bombsLeft[(int)kind]--;
            QuestProgress(DailyGoal.Bombs);
            if (bombsLeft[(int)kind] <= 0) NextBombKind(quiet: true);
            ShowBombs();
            var from = robot.transform.position + Vector3.up * 1f;
            var to = GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY;
            const float flight = 0.6f;
            ThrownRock.Create(from, to, flight, kind == BombKind.Blast ? new Color(0.15f, 0.12f, 0.16f) : Bombs.ColourOf(kind));
            AudioManager.PlaySfx(Sfx.Hop, 0.8f, 1.3f);
            for (int x = -radius; x <= radius; x++)
                for (int y = -radius; y <= radius; y++)
                    gridView.SetWarning(at + new GridPos(x, y), 0.7f);
            StartCoroutine(kind == BombKind.Ice ? IceUp(at, flight, radius) : kind == BombKind.Fire ? FireUp(at, flight, radius) : BlowUp(at, flight));
        }

        private void ClearBombWarning(GridPos at, int r)
        {
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                    gridView.SetWarning(at + new GridPos(x, y), 0f);
        }

        private bool BombLands => State == GameState.Playing && level != null && level.mission == MissionType.Hunt;

        /// <summary>The ice bomb: a cold burst, and every enemy round it stands frozen for a few seconds.</summary>
        private IEnumerator IceUp(GridPos at, float delay, int r)
        {
            yield return new WaitForSeconds(delay);
            ClearBombWarning(at, r);
            if (!BombLands) yield break;
            var c = GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY;
            var ice = Bombs.ColourOf(BombKind.Ice);
            fx.Burst(c + Vector3.up * 0.4f, ice, new Color(1.2f, 1.8f, 2.6f), 60, 7f);
            Shockwave.Create(c, 1.6f + r * 1.2f, ice);
            cameraRig.Shake(0.5f);
            AudioManager.PlaySfx(Sfx.Blocked, 1f, 1.6f);
            Haptics.Medium();
            int caught = hunt.IceAround(at, r, Bombs.FreezeSeconds, Bombs.DamageOf(BombKind.Ice) * 4);
            if (caught > 0) FloatAt(c + Vector3.up * 1.4f, "❄ " + caught, ice);
        }

        /// <summary>The fire bomb: the tiles round it burn for a few seconds, every enemy on them hurt each half second.</summary>
        private IEnumerator FireUp(GridPos at, float delay, int r)
        {
            yield return new WaitForSeconds(delay);
            ClearBombWarning(at, r);
            if (!BombLands) yield break;
            var c = GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY;
            var fire = Bombs.ColourOf(BombKind.Fire);
            fx.Burst(c + Vector3.up * 0.4f, fire, new Color(3f, 1.2f, 0.3f), 50, 6f);
            Shockwave.Create(c, 1.6f + r * 1.2f, fire);
            cameraRig.Shake(0.6f);
            AudioManager.PlaySfx(Sfx.Impact, 0.9f, 0.75f);
            Haptics.Medium();
            int perTick = Mathf.Max(1, Bombs.DamageOf(BombKind.Fire) * 4 / 2); // half a second's burn (four health points a point, two ticks a second)
            var floor = level;
            for (float t = 0f; t < Bombs.BurnSeconds; t += 0.5f)
            {
                if (!BombLands || level != floor) yield break;
                for (int x = -r; x <= r; x++)
                    for (int y = -r; y <= r; y++)
                    {
                        var p = at + new GridPos(x, y);
                        if (!grid.InBounds(p)) continue;
                        gridView.SetWarning(p, 0.35f);
                        if ((x + y + Mathf.RoundToInt(t * 2f)) % 2 == 0)
                            fx.Burst(GridView.ToWorld(p) + Vector3.up * (GridView.SurfaceY + 0.2f), fire, new Color(2.6f, 1f, 0.2f), 4, 2.2f);
                        hunt.Strike(p, perTick);
                    }
                yield return new WaitForSeconds(0.5f);
                while (State == GameState.Paused) yield return null;
            }
            ClearBombWarning(at, r);
        }

        private IEnumerator BlowUp(GridPos at, float delay)
        {
            yield return new WaitForSeconds(delay);
            int r = Bombs.Radius;
            ClearBombWarning(at, r);
            if (!BombLands) yield break;
            var c = GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY;
            var fire = new Color(1f, 0.55f, 0.2f);
            fx.Burst(c + Vector3.up * 0.4f, fire, new Color(3f, 1.4f, 0.4f), 70, 8f);
            fx.Dust(c, new Color(0.4f, 0.36f, 0.34f), 30, 5f);
            Shockwave.Create(c, 1.6f + r * 1.2f, fire);
            cameraRig.Shake(1f);
            cameraRig.Punch(0.8f);
            AudioManager.PlaySfx(Sfx.Impact, 1f, 0.55f);
            Haptics.Medium();
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                {
                    var p = at + new GridPos(x, y);
                    hunt.Strike(p, Bombs.Damage * 4); // (health points: four a point of the bomb's strength)
                    hazards.Shatter(p);
                }
        }
    }
}
