using System;
using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Journey
{
    /// <summary>
    /// The robot's fighting, on its own: the hammer swing (cooldown, duration, the moment it lands, how much it slows
    /// the walk — by default not at all), and the three skills — the dash (a quick dodge, untouchable while it lasts),
    /// the ground slam (a shockwave all round, slows the walk while it winds) and the hammer throw (flies where the
    /// camera aims and comes back, striking all in its way). It never disables movement; it only tells the motor a
    /// speed factor and, while swinging, where to face. Swings aim where the camera looks, nudged onto a robot near
    /// that line (aim assist). The world resolves the hits through the callbacks. Coordinates: local to the land.
    /// </summary>
    public class PlayerCombat
    {
        private readonly JourneyTuning t;

        /// <summary>The nearest target within a range and angle of a direction from a point, if any.</summary>
        public Func<Vector3, Vector3, float, float, Vector3?> FindTarget;
        /// <summary>A swing lands: origin, direction, range, arc (degrees), damage.</summary>
        public Action<Vector3, Vector3, float, float, int> Strike;
        /// <summary>A slam lands: centre, radius, damage.</summary>
        public Action<Vector3, float, int> Slam;
        /// <summary>The thrown hammer passes a point: point, radius, throw id (one hit per robot per throw).</summary>
        public Action<Vector3, float, int> HammerPasses;

        public bool IsAttacking => swingT < t.attackDuration;
        /// <summary>0..1 through the current swing (1 = none).</summary>
        public float Swing01 => Mathf.Clamp01(swingT / Mathf.Max(0.01f, t.attackDuration));
        public bool CanAttack => cooldown <= 0f && !HammerOut;
        public bool HammerOut => thrown != null;
        public bool Invulnerable => invulnerable > 0f;
        public float SlamWind01 => slamWind > 0f ? 1f - slamWind / 0.35f : 0f;

        public float DashCooldown01 => dashCd / Mathf.Max(0.01f, t.dashCooldown);
        public float SlamCooldown01 => slamCd / Mathf.Max(0.01f, t.slamCooldown);
        public float ThrowCooldown01 => HammerOut ? 1f : throwCd / Mathf.Max(0.01f, t.throwCooldown);

        public bool SlamOpen { get; set; }
        public bool ThrowOpen { get; set; }

        /// <summary>The walk's speed factor right now (1 = untouched).</summary>
        public float MoveMultiplier => slamWind > 0f ? t.slamMoveMultiplier : IsAttacking ? t.attackMoveMultiplier : 1f;

        private float swingT = 99f, cooldown, dashCd, slamCd, throwCd, invulnerable, slamWind;
        private bool landed = true;
        private Vector3 swingDir;
        private ThrownHammer thrown;
        private int throwId;
        private Transform worldRoot, held;

        public PlayerCombat(JourneyTuning tuning) => t = tuning;

        /// <summary>The land the hits happen in (the thrown hammer flies in it) and the hammer in the robot's hand.</summary>
        public void Attach(Transform world, Transform heldHammer)
        {
            worldRoot = world;
            held = heldHammer;
        }

        public void Tick(float dt, JourneyInput input, PlayerMotor motor, Vector3 cameraFlatForward)
        {
            cooldown -= dt;
            dashCd -= dt;
            slamCd -= dt;
            throwCd -= dt;
            invulnerable -= dt;

            // The swing: on a press (or held down: swings again as soon as it can).
            if ((input.AttackPressed || input.AttackHeld) && CanAttack)
            {
                swingT = 0f;
                landed = false;
                cooldown = t.attackCooldown;
                swingDir = Aim(motor.Position, cameraFlatForward);
                AudioManager.PlaySfx(Sfx.Hop, 0.5f, 0.7f);
            }
            if (swingT < t.attackDuration)
            {
                swingT += dt;
                if (swingT < t.attackHitTime + 0.05f) motor.FaceTowards(swingDir);
                if (!landed && swingT >= t.attackHitTime)
                {
                    landed = true;
                    Strike?.Invoke(motor.Position, swingDir, t.attackRange, t.attackArc, 1);
                }
            }

            if (input.DashPressed && dashCd <= 0f)
            {
                dashCd = t.dashCooldown;
                invulnerable = t.dashDuration + 0.1f;
                var dir = motor.Velocity.sqrMagnitude > 0.5f ? motor.Velocity : PlayerMotor.CameraRelative(input.Move.sqrMagnitude > 0.01f ? input.Move : Vector2.up, Mathf.Atan2(cameraFlatForward.x, cameraFlatForward.z) * Mathf.Rad2Deg);
                motor.Dash(dir);
                AudioManager.PlaySfx(Sfx.Hop, 0.7f, 1.4f);
            }

            if (input.Skill1Pressed && SlamOpen && slamCd <= 0f && motor.Grounded)
            {
                slamCd = t.slamCooldown;
                slamWind = 0.35f; // a short wind-up, then the blow
                AudioManager.PlaySfx(Sfx.Hop, 0.7f, 0.5f);
            }
            if (slamWind > 0f)
            {
                slamWind -= dt;
                if (slamWind <= 0f)
                {
                    slamWind = 0f;
                    Slam?.Invoke(motor.Position, t.slamRadius, t.slamDamage);
                }
            }

            if (input.Skill2Pressed && ThrowOpen && !HammerOut && throwCd <= 0f && worldRoot != null)
            {
                throwCd = t.throwCooldown;
                throwId++;
                var dir = Aim(motor.Position, cameraFlatForward, t.throwRange);
                motor.FaceTowards(dir);
                thrown = ThrownHammer.Launch(worldRoot, held, motor.Position + Vector3.up * 1f, dir, t.throwRange, t.throwSpeed);
                if (held != null) held.gameObject.SetActive(false);
                AudioManager.PlaySfx(Sfx.Hop, 0.8f, 0.9f);
            }
            if (thrown != null)
            {
                bool back = thrown.Tick(dt, motor.Position + Vector3.up * 1f);
                HammerPasses?.Invoke(thrown.LocalPosition, 0.9f, throwId);
                if (back)
                {
                    UnityEngine.Object.Destroy(thrown.gameObject);
                    thrown = null;
                    if (held != null) held.gameObject.SetActive(true);
                    AudioManager.PlaySfx(Sfx.Coin, 0.4f, 0.7f);
                }
            }
        }

        /// <summary>Where a blow goes: the camera's aim, onto a robot near that line if there is one.</summary>
        private Vector3 Aim(Vector3 from, Vector3 cameraFlatForward, float range = -1f)
        {
            var dir = cameraFlatForward;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            var target = FindTarget?.Invoke(from, dir, range > 0f ? range : t.aimAssistRange, t.aimAssistArc);
            if (target.HasValue)
            {
                var to = target.Value - from;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) dir = to.normalized;
            }
            return dir;
        }

        /// <summary>Puts everything away (a ride, a respawn).</summary>
        public void Reset()
        {
            swingT = 99f;
            slamWind = 0f;
            if (thrown != null) UnityEngine.Object.Destroy(thrown.gameObject);
            thrown = null;
            if (held != null) held.gameObject.SetActive(true);
        }
    }

    /// <summary>The thrown hammer: spins out along a line, then flies back to the robot's hand.</summary>
    public class ThrownHammer : MonoBehaviour
    {
        private Vector3 dir, start;
        private float range, speed, travelled;
        private bool returning;

        public Vector3 LocalPosition => transform.localPosition;

        public static ThrownHammer Launch(Transform world, Transform heldModel, Vector3 from, Vector3 dir, float range, float speed)
        {
            GameObject go;
            if (heldModel != null)
            {
                go = Instantiate(heldModel.gameObject, world, false);
                go.SetActive(true);
            }
            else go = new GameObject("ThrownHammer");
            go.name = "ThrownHammer";
            go.transform.SetParent(world, false);
            go.transform.localPosition = from;
            go.transform.localScale = Vector3.one * 1.3f;
            var h = go.AddComponent<ThrownHammer>();
            h.dir = dir.normalized;
            h.start = from;
            h.range = range;
            h.speed = speed;
            return h;
        }

        /// <returns>True when it is back in the hand.</returns>
        public bool Tick(float dt, Vector3 hand)
        {
            transform.localRotation *= Quaternion.Euler(0f, 0f, -900f * dt);
            if (!returning)
            {
                travelled += speed * dt;
                transform.localPosition = start + dir * travelled;
                if (travelled >= range) returning = true;
                return false;
            }
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, hand, speed * 1.15f * dt);
            return (transform.localPosition - hand).sqrMagnitude < 0.36f;
        }
    }
}
