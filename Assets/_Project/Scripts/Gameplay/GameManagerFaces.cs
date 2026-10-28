using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>The duel's faces (the design's §2.1): a fighter's face answers the moment (see <see cref="FaceExpressionController"/>).</summary>
    public partial class GameManager
    {
        /// <summary>The face of <paramref name="who"/> (foi's or the rival's model) answers <paramref name="moment"/>.</summary>
        private void FaceFor(Transform who, FaceMoment moment)
        {
            var face = FaceExpressionController.On(who);
            if (face == null) return;
            face.Play(moment);
            face.ApplyNow();
        }

        /// <summary>What sits on <paramref name="who"/>'s head answers a blow (1 a medium one, 2 a heavy one or a throw, 3 a
        /// knock-out): always a light hop, it stays on the head (the crown never falls: the old hats' flying is gone).</summary>
        private static void HatFor(Transform who, int how, Vector3 from)
        {
            var skin = who != null ? who.GetComponentInChildren<SkinnedHero>() : null;
            if (skin == null || skin.HatMount == null || skin.HatMount.childCount == 0) return;
            HatBounce.On(skin.HatMount).Hop();
        }

        /// <summary>
        /// The knock-down's taunt: foi turns to the camera with a sly grin, flourishes its weapon (the family's: sheathed,
        /// shouldered, planted, struck on the ground), hops; the crowd answers. Two seconds of show, no rules.
        /// </summary>
        private void KnockdownTaunt()
        {
            if (!challengeRun || robot.Visual == null) return;
            FaceFor(robot.Visual, FaceMoment.SlyGrin);
            var held = robot.Visual.GetComponentInChildren<HeldWeapon>();
            if (held != null) held.Flourish();
            var skin = robot.Visual.GetComponentInChildren<SkinnedHero>();
            if (skin != null) skin.Hop(0.5f, 0.4f);
            if (colosseum != null) colosseum.Cheer(0, 0.8f);
            Audio.AudioManager.PlaySfx(Audio.Sfx.Win, 0.5f, 1.5f);
            if (LiveActive) LiveNet.Emote(FaceTaunts.Emote(3)); // (the rival sees the sly grin)
        }

        /// <summary>Both hats back on their heads (a new round).</summary>
        private void HatsBack()
        {
            foreach (var m in new[] { robot != null ? robot.Visual : null, hunt.ChallengeFoeModel })
            {
                var skin = m != null ? m.GetComponentInChildren<SkinnedHero>() : null;
                var hat = skin != null && skin.HatMount != null ? skin.HatMount.GetComponent<HatBounce>() : null;
                if (hat != null) hat.Restore();
            }
        }

        /// <summary>The face under the moments for <paramref name="who"/> (worried at low health, else set).</summary>
        private void FaceRest(Transform who, FaceMoment moment)
        {
            var face = FaceExpressionController.On(who);
            if (face != null && face.Resting != moment) face.Rest(moment);
        }

        /// <summary>Both faces plain again (a new round).</summary>
        private void FacesClear()
        {
            foreach (var m in new[] { robot != null ? robot.Visual : null, hunt.ChallengeFoeModel })
            {
                var s = m != null ? m.GetComponentInChildren<SkinnedHero>() : null;
                var f = s != null ? s.GetComponent<FaceExpressionController>() : null;
                if (f != null) f.Clear();
            }
        }

        /// <summary>The duel over: foi's face its own again (the menu shows it plain).</summary>
        private void FacesOff()
        {
            var s = robot != null && robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            var f = s != null ? s.GetComponent<FaceExpressionController>() : null;
            if (f != null) Destroy(f);
        }
    }
}
