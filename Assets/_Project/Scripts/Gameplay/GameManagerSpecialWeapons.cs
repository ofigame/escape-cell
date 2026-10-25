using System.Collections;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The rank weapons' effects, after their blow lands on a tile: fire burns there a few seconds, ice freezes the
    /// enemy, lightning jumps to two more, a quake hits the tiles round about, a cleave cuts the tiles beside, and a
    /// piercing blow goes on through two more tiles beyond.
    /// </summary>
    public partial class GameManager
    {
        private void SpecialHit(WeaponDef w, GridPos at, int damage)
        {
            // Not in the duels: the server counts plain blows only (and ice would hold the rival foi still).
            // Every weapon with an effect has it here (the rank and chest weapons, and the shop's lava axe and frost lance).
            if (w == null || !w.HasFx || hunt == null || challengeRun) return;
            var c = WeaponModels.FxColour(w.fx);
            var spot = GridView.ToWorld(at) + Vector3.up * (GridView.SurfaceY + 0.5f);
            switch (w.fx)
            {
                case SpecialFx.Burn:
                    StartCoroutine(BurnTile(at, Mathf.Max(1, damage / 5), w.fxTicks > 0 ? w.fxTicks : 3));
                    break;
                case SpecialFx.Freeze:
                    fx.Burst(spot, c, c * 2f, 18, 4f);
                    hunt.IceAround(at, 0, 1.5f, 0);
                    break;
                case SpecialFx.Chain:
                    var from = spot;
                    foreach (var p in hunt.TargetsNear(at, 3, 2))
                    {
                        var to = GridView.ToWorld(p) + Vector3.up * (GridView.SurfaceY + 0.5f);
                        ZapFx.Create(from, to, c);
                        fx.Burst(to, c, c * 2.5f, 10, 3f);
                        hunt.Strike(p, Mathf.Max(1, damage / 2));
                        from = to;
                    }
                    AudioManager.PlaySfx(Sfx.Impact, 0.5f, 2.2f);
                    break;
                case SpecialFx.Quake:
                    Shockwave.Create(GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY, 2.4f, c);
                    cameraRig.Shake(0.4f);
                    Around(at, 1, p => hunt.Strike(p, Mathf.Max(1, damage / 2)));
                    break;
                case SpecialFx.Cleave:
                    // The tiles beside the target that foi can reach: a wide arc.
                    Around(at, 1, p => { if (HuntSystem.Chebyshev(p, robot.Position) <= StrikeReach && p != robot.Position) hunt.Strike(p, Mathf.Max(1, damage * 3 / 5)); });
                    SlashFx.Create(robot.transform.position, GridView.ToWorld(at), c, 1.8f);
                    break;
                case SpecialFx.Pierce:
                    int dx = System.Math.Sign(at.x - robot.Position.x), dy = System.Math.Sign(at.y - robot.Position.y);
                    if (dx == 0 && dy == 0) break;
                    var end = at;
                    for (int k = 1; k <= 2; k++)
                    {
                        var p = at + new GridPos(dx * k, dy * k);
                        if (!grid.InBounds(p)) break;
                        hunt.Strike(p, Mathf.Max(1, damage * 7 / 10));
                        end = p;
                    }
                    Tracer.Create(spot, GridView.ToWorld(end) + Vector3.up * (GridView.SurfaceY + 0.5f), c);
                    break;
            }
        }

        private static void Around(GridPos at, int r, System.Action<GridPos> each)
        {
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                    if (x != 0 || y != 0) each(at + new GridPos(x, y));
        }

        /// <summary>A fire weapon's blow: the tile burns, hurting what stands on it each half second.</summary>
        private IEnumerator BurnTile(GridPos at, int perTick, int ticks)
        {
            var floor = level;
            var c = WeaponModels.FxColour(SpecialFx.Burn);
            for (int i = 0; i < ticks; i++)
            {
                yield return new WaitForSeconds(0.5f);
                while (State == GameState.Paused) yield return null;
                if (State != GameState.Playing || level != floor) yield break;
                fx.Burst(GridView.ToWorld(at) + Vector3.up * (GridView.SurfaceY + 0.2f), c, new Color(2.6f, 1f, 0.2f), 6, 2.4f);
                hunt.Strike(at, perTick);
            }
        }
    }
}
