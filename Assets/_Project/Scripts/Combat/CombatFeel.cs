using System;
using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// The duel's blows felt: when a blow is known to land (the rules already applied it), its feel plays on the
    /// striker's weapon's contact (the moment the swing meets the body; at once if no swing is on its way): both bodies
    /// freeze a few milliseconds, the camera is knocked along the blow, the struck body's picture is pushed back, the
    /// family's impact sound. Only pictures and sounds: no rule, damage or place changes here. The bot and the player go
    /// through the same call.
    /// </summary>
    public static class CombatFeel
    {
        private struct Pending
        {
            public HeldWeapon weapon;
            public Transform attacker, victim;
            public AttackMotionData data;
            public float deadline;
        }

        private static readonly List<Pending> pending = new List<Pending>(8);
        private static readonly List<Vector2> lightFreezes = new List<Vector2>(32); // (when, how long ms)
        private static bool hooked;

        /// <summary>A blow's feel has played: striker, struck, the attack.</summary>
        public static event Action<Transform, Transform, AttackMotionData> Contacted;

        /// <summary>The player's (and the lab's) own scale for the hit freeze (1: as designed, 0: none).</summary>
        public static float HitstopMultiplier
        {
            get => Prefs.GetFloat("sb_cf_hitstop", 1f);
            set { Prefs.SetFloat("sb_cf_hitstop", Mathf.Clamp(value, 0f, 3f)); Prefs.Save(); }
        }

        /// <summary>The player's (and the lab's) own scale for the camera's knocks.</summary>
        public static float ShakeMultiplier
        {
            get => Prefs.GetFloat("sb_cf_shake", 1f);
            set { Prefs.SetFloat("sb_cf_shake", Mathf.Clamp(value, 0f, 3f)); Prefs.Save(); }
        }

        /// <summary>
        /// A blow landed: <paramref name="attacker"/>'s (its visual root) <paramref name="data"/> on <paramref name="victim"/>.
        /// Felt on the striker's weapon's contact if its swing is still on the way there, else at once.
        /// </summary>
        public static void Hit(Transform attacker, Transform victim, AttackMotionData data)
        {
            if (victim == null || data == null) return;
            Hook();
            var w = attacker != null ? attacker.GetComponentInChildren<HeldWeapon>() : null;
            float wait = w != null ? w.ContactIn : -1f;
            if (wait > 0f && wait < 0.35f)
            {
                pending.Add(new Pending { weapon = w, attacker = attacker, victim = victim, data = data, deadline = Time.unscaledTime + wait + 0.15f });
                CombatFeelRunner.Ensure();
                return;
            }
            Apply(attacker, victim, data);
        }

        /// <summary>A big moment: every fighter's pictures slow for a while (the game's clock does not).</summary>
        public static void BigMoment()
        {
            var s = CombatLibrary.Settings;
            VisualTime.SlowAll(s.bigMomentSlow, s.bigMomentSlowSeconds);
        }

        private static void Hook()
        {
            if (hooked) return;
            hooked = true;
            HeldWeapon.PhaseReached += OnPhase;
        }

        private static void OnPhase(HeldWeapon w, SwingPhase phase)
        {
            if (phase != SwingPhase.Contact) return;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].weapon != w) continue;
                var p = pending[i];
                pending.RemoveAt(i);
                Apply(p.attacker, p.victim, p.data);
            }
        }

        /// <summary>Each frame (while some blow waits): one whose contact never came plays now.</summary>
        internal static bool Tick()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].weapon != null && Time.unscaledTime < pending[i].deadline) continue;
                var p = pending[i];
                pending.RemoveAt(i);
                Apply(p.attacker, p.victim, p.data);
            }
            return pending.Count > 0;
        }

        /// <summary>A fighter's visual root: the parent of its rigged body (where its picture clock and layer live).</summary>
        public static Transform RootOf(Transform fighter)
        {
            if (fighter == null) return null;
            var body = fighter.GetComponentInChildren<SkinnedHero>(true);
            return body != null && body.transform.parent != null ? body.transform.parent : fighter;
        }

        private static void Apply(Transform attacker, Transform victim, AttackMotionData data)
        {
            if (victim == null) return;
            var s = CombatLibrary.Settings;
            float ms = data.hitstopMs * HitstopMultiplier;
            if (data.strength == HitStrength.Light && ms > 0f)
            {
                // light blows in a row never freeze more than the budget within its window (a sword's flurry never stutters)
                float now = Time.unscaledTime, used = 0f;
                for (int i = lightFreezes.Count - 1; i >= 0; i--)
                {
                    if (now - lightFreezes[i].x > s.hitstopWindow) lightFreezes.RemoveAt(i);
                    else used += lightFreezes[i].y;
                }
                ms = Mathf.Min(ms, Mathf.Max(0f, s.hitstopBudgetMs - used));
                if (ms > 0f) lightFreezes.Add(new Vector2(now, ms));
            }
            var attackerRoot = RootOf(attacker);
            var victimRoot = RootOf(victim);
            if (ms > 0f)
            {
                if (attackerRoot != null) VisualTime.On(attackerRoot).Freeze(ms / 1000f);
                VisualTime.On(victimRoot).Freeze(ms / 1000f);
            }
            var dir = attacker != null ? victim.position - attacker.position : -victim.forward;
            CameraImpulse.Fire(data.cameraImpulse, dir, ShakeMultiplier);
            var r = CombatLibrary.Reactions;
            float knock = data.visualKnockback > 0f ? data.visualKnockback : r.Knockback(data.strength);
            VisualRoot.On(victimRoot).Push(dir, knock, r.Recovery(data.strength));
            AudioManager.PlaySfx(data.impactSfx, 0.55f, data.impactPitch, 0.08f);
            Contacted?.Invoke(attacker, victim, data);
        }
    }
}
