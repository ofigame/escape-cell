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
    /// at once) and bombs (from level 31, three a floor: thrown at the thickest knot of enemies within six tiles, they
    /// blow up the 3x3 square where they land, or 5x5 at the top level).
    /// </summary>
    public partial class GameManager
    {
        private int bombsLeft;

        private void SetupBombs()
        {
            bombsLeft = Bombs.Owned && levelIndex >= Bombs.FromLevel && !bonusRun ? Bombs.PerFloor : 0;
            ui.SetBombs(bombsLeft, true);
        }

        private void HideBombs()
        {
            bombsLeft = 0;
            ui.SetBombs(0, false);
        }

        /// <summary>A whirl: foi spins with its blades and every enemy within reach takes the blow.</summary>
        private void WhirlStrike(WeaponDef w, int damage)
        {
            if (heldWeapon != null) heldWeapon.Spin();
            var glow = WeaponModels.Glow(w.tier);
            var centre = robot.transform.position;
            Shockwave.Create(centre, 2.8f, glow);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                SlashFx.Create(centre, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.6f, glow, 0.9f + 0.1f * damage);
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
            if (State != GameState.Playing || level == null || level.mission != MissionType.Hunt || roadPhase != RoadPhase.None || bombsLeft <= 0) return;
            if (!hunt.BombSpot(robot.Position, Bombs.ThrowRange, Bombs.Radius, out var at))
            {
                FloatAt(robot.transform.position + Vector3.up * 1.2f, Loc.T("float.noBombTarget"), new Color(1f, 0.75f, 0.5f));
                AudioManager.PlaySfx(Sfx.Bump, 0.6f);
                return;
            }
            bombsLeft--;
            QuestProgress(DailyGoal.Bombs);
            ui.SetBombs(bombsLeft, true);
            var from = robot.transform.position + Vector3.up * 1f;
            var to = GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY;
            const float flight = 0.6f;
            ThrownRock.Create(from, to, flight, new Color(0.15f, 0.12f, 0.16f));
            AudioManager.PlaySfx(Sfx.Hop, 0.8f, 1.3f);
            for (int x = -Bombs.Radius; x <= Bombs.Radius; x++)
                for (int y = -Bombs.Radius; y <= Bombs.Radius; y++)
                    gridView.SetWarning(at + new GridPos(x, y), 0.7f);
            StartCoroutine(BlowUp(at, flight));
        }

        private IEnumerator BlowUp(GridPos at, float delay)
        {
            yield return new WaitForSeconds(delay);
            int r = Bombs.Radius;
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                    gridView.SetWarning(at + new GridPos(x, y), 0f);
            if (State != GameState.Playing || level == null || level.mission != MissionType.Hunt) yield break;
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
                    hunt.Strike(p, Bombs.Damage);
                    hazards.Shatter(p);
                }
        }
    }
}
