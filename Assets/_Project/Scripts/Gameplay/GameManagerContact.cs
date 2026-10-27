using SquashBot.Combat;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The rushing blows' touch (Docs/DUELLO_CARPISMA_VE_TEMAS.md, sections 3 and 5.2): a mover's picture rushes to the
    /// other's body and stops there (its push box), the blow landing as it touches; its hit effect on the contact point,
    /// once. The rules' places (at least three tiles apart) never move: only the pictures close the gap.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>How long a rush takes to reach the other's body (seconds; the blow lands then).</summary>
        private const float RushSeconds = 0.1f;

        /// <summary>Each side's FLY under way (leaping), and when two met in the air.</summary>
        private bool myFlying, rivalFlying;
        private float airClashAt = -9f;

        /// <summary>Both flew at each other at once (the collision rules' scenario 7): they meet in the middle, mid-leap, a
        /// clash of weapons there and both rocked; neither blow lands.</summary>
        private System.Collections.IEnumerator AirClash()
        {
            airClashAt = Time.time;
            yield return new WaitForSeconds(0.4f); // (the middle of the leaps)
            airClashAt = Time.time;
            var a = robot.Visual; var b = hunt.ChallengeFoeModel;
            if (a == null || b == null) yield break;
            var at = (a.position + b.position) * 0.5f + Vector3.up * 1.6f;
            var across = Vector3.Cross(Vector3.up, b.position - a.position);
            SkinnedHero.BothCame?.Invoke(at, across.sqrMagnitude > 1e-6f ? across.normalized : Vector3.right);
        }

        /// <summary><paramref name="mover"/>'s picture rushes to <paramref name="toward"/>'s body (the contact distance),
        /// arriving in <paramref name="seconds"/>, held a moment, then home.</summary>
        private static void RushToTouch(Transform mover, Transform toward, float seconds = RushSeconds, float hold = 0.14f, float back = 0.35f)
        {
            var me = CombatFeel.RootOf(mover);
            var them = CombatFeel.RootOf(toward);
            if (me == null || them == null) return;
            var d = them.position - me.position; d.y = 0f;
            float dist = VisualRoot.KeptApart(me.position, them.position, 99f);
            if (dist > 0.02f) VisualRoot.On(me).Lunge(d, dist, Mathf.Max(0.04f, seconds), hold, back);
        }

        /// <summary>Waits until the two bodies touch (their push boxes meet), or <paramref name="most"/> seconds: the moment
        /// a rushing blow lands.</summary>
        private static System.Collections.IEnumerator WaitForTouch(Transform a, Transform b, float most = 0.35f)
        {
            var sa = a != null ? a.GetComponentInChildren<SkinnedHero>() : null;
            var sb = b != null ? b.GetComponentInChildren<SkinnedHero>() : null;
            if (sa == null || sb == null) { yield return new WaitForSeconds(RushSeconds); yield break; }
            float touch = (sa.PushBoxRadius + sb.PushBoxRadius) * 1.04f;
            for (float e = 0f; e < most; e += Time.deltaTime)
            {
                var gap = sa.TrunkPoint - sb.TrunkPoint; gap.y = 0f;
                if (gap.magnitude <= touch) yield break;
                yield return null;
            }
        }

        /// <summary>Rushes onto the other's body, then — if the blow landed (<paramref name="mine"/>: the player's) — its
        /// effect as it touches.</summary>
        private System.Collections.IEnumerator RushThenHit(Transform strikerModel, Transform struckModel, HitHeight height, int inARow, bool mine = true)
        {
            RushToTouch(strikerModel, struckModel);
            yield return WaitForTouch(strikerModel, struckModel);
            if (LiveActive || Landed(mine)) ShowHitOn(strikerModel, struckModel, height, inARow);
        }

        /// <summary>A landed blow from <paramref name="strikerModel"/> on <paramref name="struckModel"/>: its effect where
        /// the striker's weapon (or body) meets the struck part (by <paramref name="height"/>), once.</summary>
        private void ShowHitOn(Transform strikerModel, Transform struckModel, HitHeight height, int inARow)
        {
            var skin = struckModel != null ? struckModel.GetComponentInChildren<SkinnedHero>() : null;
            if (skin == null) return;
            var held = strikerModel != null ? strikerModel.GetComponentInChildren<HeldWeapon>() : null;
            skin.HurtSpots(out var headAt, out var chestAt, out var legsAt);
            bool toHead = height == HitHeight.High;
            var part = toHead ? headAt : height == HitHeight.Low ? legsAt : chestAt;
            var weaponAt = held != null && held.Tip != null ? held.Tip.position : strikerModel != null ? strikerModel.position + Vector3.up * skin.PushBoxRadius * 2f : part;
            var at = HitEffects.ContactPoint(weaponAt, part, skin.PushBoxRadius * (toHead ? 0.55f : 0.6f));
            var along = struckModel.position - (strikerModel != null ? strikerModel.position : struckModel.position - struckModel.forward); along.y = 0f;
            bool mineStrike = strikerModel == robot.Visual;
            var wDef = mineStrike ? Armory.Equipped : rival != null ? Armory.Get(rival.weapon) : null;
            var glow = wDef != null ? WeaponModels.GlowOf(wDef) : new Color(1f, 0.8f, 0.4f);
            int id = HitEffects.NewBlow();
            HitShown(id, at, along, glow, inARow, toHead);
            if (LabLatencyMs > 0) cameraRig.StartCoroutine(HitConfirmedLater(LabLatencyMs / 1000f, id, at, along, glow, inARow, toHead));
        }
    }
}
