using System.Collections;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The game's side of the perfect city (level 101 on): words over shielded guards and drones, and the reactor
    /// core's fall, when the whole floor lights up in rings from where it stood.
    /// </summary>
    public partial class GameManager
    {
        private float lastShieldNote = -10f;

        private void InitUtopia()
        {
            hunt.ShieldHit += (p, broke) =>
            {
                if (!broke && Time.unscaledTime - lastShieldNote < 0.8f) return;
                lastShieldNote = Time.unscaledTime;
                FloatAt(GridView.ToWorld(p) + Vector3.up * 1.1f, Loc.T(broke ? "float.shieldDown" : "float.energyShield"), new Color(0.55f, 0.9f, 1f));
            };
            hunt.ReactorDown += p =>
            {
                FloatAt(GridView.ToWorld(p) + Vector3.up * 1.6f, Loc.T("float.reactorDown"), Palette.UiGold);
                StartCoroutine(LightUpFloor(p));
            };
        }

        /// <summary>The floor lights up ring by ring from the fallen reactor: pearl tiles with a warm gold glow.</summary>
        private IEnumerator LightUpFloor(GridPos from)
        {
            var g = grid;
            int reach = Mathf.Max(g.Width, g.Height);
            for (int r = 0; r <= reach; r++)
            {
                if (grid != g || level == null || !level.utopia) yield break;
                for (int x = -r; x <= r; x++)
                    for (int y = -r; y <= r; y++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != r) continue;
                        var p = new GridPos(from.x + x, from.y + y);
                        if (!g.IsFloor(p) || floorRules.IsSpecial(p) || g.IsSafe(p)) continue;
                        gridView.SetTint(p, new Color(1f, 0.97f, 0.88f), new Color(0.75f, 0.6f, 0.25f));
                        gridView.Bounce(p, 0.5f);
                        if ((x + y) % 3 == 0) fx.Burst(GridView.ToWorld(p) + Vector3.up * 0.15f, new Color(1f, 0.9f, 0.6f), new Color(1.8f, 1.5f, 0.7f), 3, 1.5f);
                    }
                if (r % 3 == 0) AudioManager.PlaySfx(Sfx.Coin, 0.3f, 1.2f + r * 0.03f);
                yield return new WaitForSeconds(0.05f);
            }
        }
    }
}
