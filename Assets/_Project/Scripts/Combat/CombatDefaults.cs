using SquashBot.Audio;
using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// The design's starting values (frames at 60 per second, the freeze table, the reactions), made into the
    /// ScriptableObjects the duel reads: used when there are no saved assets under Resources/Combat, and by the editor
    /// to write those assets the first time. One place for every starting number.
    /// </summary>
    public static class CombatDefaults
    {
        private const float Frame = 1000f / 60f;

        public static CombatFeelSettings Settings() => ScriptableObject.CreateInstance<CombatFeelSettings>();
        public static HitReactionProfile Reactions() => ScriptableObject.CreateInstance<HitReactionProfile>();

        /// <summary>An ultimate's final blow: the longest freeze (200 ms), the strongest knock of the camera.</summary>
        public static AttackMotionData UltiFinal()
        {
            var a = Blow(WeaponFamily.Heavy, 10, 2, 2, 10, HitDirection.Overhead, HitHeight.Mid);
            a.name = "UltiFinal";
            a.strength = HitStrength.Finisher; a.hitstopMs = 200f; a.cameraImpulse = CameraImpulseLevel.Max; a.visualKnockback = 0.4f;
            return a;
        }

        /// <summary>A family's four-blow chain, POWER HIT and KNOCKDOWN as the design gives them.</summary>
        public static WeaponFamilyMotionProfile Family(WeaponFamily f)
        {
            var p = ScriptableObject.CreateInstance<WeaponFamilyMotionProfile>();
            p.family = f;
            p.name = "Family_" + f;
            switch (f)
            {
                case WeaponFamily.Sword:
                    p.chain = new[]
                    {
                        Blow(f, 1, 2, 1, 4, HitDirection.Horizontal_RightToLeft, HitHeight.Mid),
                        Blow(f, 2, 1, 1, 4, HitDirection.Horizontal_LeftToRight, HitHeight.Mid),
                        Blow(f, 3, 2, 1, 5, HitDirection.Overhead, HitHeight.High),
                        Blow(f, 4, 2, 1, 5, HitDirection.Thrust, HitHeight.Mid),
                    };
                    p.trail = new Color(0.8f, 0.92f, 1f);
                    break;
                case WeaponFamily.Spear:
                    p.chain = new[]
                    {
                        Blow(f, 1, 3, 1, 6, HitDirection.Thrust, HitHeight.Mid),
                        Blow(f, 2, 3, 1, 6, HitDirection.Thrust, HitHeight.Mid),
                        Blow(f, 3, 4, 1, 6, HitDirection.Horizontal_LeftToRight, HitHeight.Mid),
                        Blow(f, 4, 4, 1, 6, HitDirection.Overhead, HitHeight.High),
                    };
                    p.trail = new Color(0.9f, 0.95f, 1f);
                    break;
                case WeaponFamily.Axe:
                    p.chain = new[]
                    {
                        Blow(f, 1, 6, 2, 9, HitDirection.Overhead, HitHeight.High),
                        Blow(f, 2, 6, 2, 9, HitDirection.Horizontal_RightToLeft, HitHeight.Mid),
                        Blow(f, 3, 5, 2, 9, HitDirection.Upward, HitHeight.Mid),
                        Blow(f, 4, 7, 2, 9, HitDirection.Spin, HitHeight.Mid),
                    };
                    p.trail = new Color(1f, 0.6f, 0.3f);
                    break;
                default:
                    p.chain = new[]
                    {
                        Blow(f, 1, 7, 2, 9, HitDirection.Overhead, HitHeight.High),
                        Blow(f, 2, 6, 2, 9, HitDirection.Horizontal_LeftToRight, HitHeight.Mid),
                        Blow(f, 3, 6, 2, 9, HitDirection.Upward, HitHeight.High),
                        Blow(f, 4, 8, 2, 10, HitDirection.Overhead, HitHeight.Mid),
                    };
                    p.trail = new Color(1f, 0.82f, 0.45f);
                    break;
            }
            p.powerHit = Blow(f, 5, 8, 2, 10, HitDirection.Overhead, HitHeight.High);
            p.powerHit.name = f + "_PowerHit";
            p.powerHit.strength = HitStrength.Heavy; p.powerHit.hitstopMs = 90f; p.powerHit.cameraImpulse = CameraImpulseLevel.Medium; p.powerHit.visualKnockback = 0.5f;
            p.knockdown = Blow(f, 6, 8, 2, 12, HitDirection.Upward, HitHeight.Mid);
            p.knockdown.name = f + "_Knockdown";
            p.knockdown.strength = HitStrength.Launch; p.knockdown.hitstopMs = 140f; p.knockdown.cameraImpulse = CameraImpulseLevel.Heavy; p.knockdown.visualKnockback = 0f;
            // FLY's blow by family: a sword slashes across in flight, a spear runs it through, an axe spins into it, a heavy one crashes down
            var flyDir = f == WeaponFamily.Sword ? HitDirection.Horizontal_RightToLeft : f == WeaponFamily.Spear ? HitDirection.Thrust : f == WeaponFamily.Axe ? HitDirection.Spin : HitDirection.Overhead;
            p.flyStrike = Blow(f, 7, 3, 2, 9, flyDir, HitHeight.Mid);
            p.flyStrike.name = f + "_Fly";
            p.flyStrike.strength = HitStrength.Heavy; p.flyStrike.hitstopMs = 100f; p.flyStrike.cameraImpulse = CameraImpulseLevel.Medium; p.flyStrike.visualKnockback = 0.5f;
            p.airStrike = Blow(f, 8, 3, 2, 8, HitDirection.Overhead, HitHeight.High);
            p.airStrike.name = f + "_Air";
            p.airStrike.strength = HitStrength.Medium; p.airStrike.hitstopMs = 70f; p.airStrike.cameraImpulse = CameraImpulseLevel.Medium;
            p.skyStrike = Blow(f, 9, 4, 2, 10, HitDirection.Overhead, HitHeight.High);
            p.skyStrike.name = f + "_Sky";
            p.skyStrike.strength = HitStrength.Heavy; p.skyStrike.hitstopMs = 120f; p.skyStrike.cameraImpulse = CameraImpulseLevel.Heavy; p.skyStrike.visualKnockback = 0.5f;
            return p;
        }

        /// <summary>One blow (wind-up / contact / recovery in frames), with the family's strength, freeze and sounds.</summary>
        private static AttackMotionData Blow(WeaponFamily f, int n, int windFrames, int hitFrames, int backFrames, HitDirection dir, HitHeight height)
        {
            var a = ScriptableObject.CreateInstance<AttackMotionData>();
            a.name = f + "_" + n;
            a.anticipationMs = windFrames * Frame;
            a.activeMs = hitFrames * Frame;
            a.recoveryMs = backFrames * Frame;
            a.direction = dir;
            a.height = height;
            bool heavy = f == WeaponFamily.Axe || f == WeaponFamily.Heavy;
            a.strength = heavy ? HitStrength.Medium : HitStrength.Light;
            a.hitstopMs = f == WeaponFamily.Sword ? 20f : f == WeaponFamily.Spear ? 30f : f == WeaponFamily.Axe ? 50f : 65f;
            a.cameraImpulse = CameraImpulseLevel.Light;
            a.visualKnockback = heavy ? 0.2f : 0.08f;
            // each family its own swing and blow (the game's synthesized sounds; pitch varies a little each time)
            a.whooshSfx = f == WeaponFamily.Sword ? Sfx.SwishSword : f == WeaponFamily.Spear ? Sfx.SwishSpear : f == WeaponFamily.Axe ? Sfx.SwishAxe : Sfx.SwishHeavy;
            a.whooshPitch = 1f;
            a.impactSfx = f == WeaponFamily.Sword ? Sfx.HitSword : f == WeaponFamily.Spear ? Sfx.HitSpear : f == WeaponFamily.Axe ? Sfx.HitAxe : Sfx.HitHeavy;
            a.impactPitch = 1f;
            return a;
        }
    }
}
