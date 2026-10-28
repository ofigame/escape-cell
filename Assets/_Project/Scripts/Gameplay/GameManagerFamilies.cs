using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The weapon families' own ways on the floors (the fixes design's job 10; the numbers in FloorTuning): within a tier
    /// every family does the same damage a second, the difference is how. The sword's every fourth blow in a row hits
    /// half again as hard; the spear reaches a tile farther; the axe cuts the tiles beside its target; the heavy ones shake
    /// the tiles round it and make it reel a moment. In the duel the families keep their duel table's ways (reach).
    /// </summary>
    public partial class GameManager
    {
        private int swordChain;
        private float swordChainAt = -9f;

        /// <summary>A sword's blow in a row (each within the chain's gap of the last): every fourth harder.</summary>
        private int SwordChain(WeaponDef w, int damage)
        {
            if (w.kind == WeaponKind.Gun || Armory.Family(w) != WeaponFamily.Sword) { swordChain = 0; return damage; }
            var t = FloorTuning.Current;
            swordChain = Time.time - swordChainAt <= t.swordChainGap ? swordChain + 1 : 1;
            swordChainAt = Time.time;
            if (swordChain % Mathf.Max(1, t.swordEvery) != 0) return damage;
            FloatAt(robot.transform.position + Vector3.up * 1.3f, "×" + MoveTexts.N(t.swordTimes), UI.UiKit.Style.gold);
            return Mathf.Max(1, Mathf.RoundToInt(damage * t.swordTimes));
        }

        /// <summary>A blow landed on the floors: the rank and chest weapons' effects, then the family's own.</summary>
        private void AfterHit(WeaponDef w, GridPos at, int damage)
        {
            SpecialHit(w, at, damage);
            if (w == null || hunt == null || challengeRun || w.kind == WeaponKind.Gun) return;
            var t = FloorTuning.Current;
            switch (Armory.Family(w))
            {
                case WeaponFamily.Axe:
                {
                    // a wide cut: the tiles beside the target that foi reaches
                    int side = Mathf.Max(1, Mathf.RoundToInt(damage * t.axeSideShare));
                    Around(at, 1, p => { if (HuntSystem.Chebyshev(p, robot.Position) <= StrikeReach && p != robot.Position && hunt.IsFighter(p)) hunt.Strike(p, side); });
                    break;
                }
                case WeaponFamily.Heavy:
                {
                    // the ground shakes round the target; the target reels
                    int splash = Mathf.Max(1, Mathf.RoundToInt(damage * t.heavySplashShare));
                    Around(at, 1, p => { if (p != robot.Position && hunt.IsFighter(p)) hunt.Strike(p, splash); });
                    hunt.StaggerAt(at, t.heavyStagger);
                    Shockwave.Create(GridView.ToWorld(at) + Vector3.up * GridView.SurfaceY, 1.4f, new Color(0.9f, 0.8f, 0.6f));
                    break;
                }
            }
        }

        /// <summary>How far a duel blow reaches with this weapon (the gap kept; the spear's family one tile more).</summary>
        private static int DuelReach(WeaponDef w) => DuelBalanceTable.Current.Reach(Armory.Family(w));
    }
}
