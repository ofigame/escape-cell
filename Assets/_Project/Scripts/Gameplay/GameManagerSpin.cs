using System.Collections;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The spin attack: a quick circle drawn with the finger and foi whirls round once with its weapon, striking every
    /// enemy on the eight tiles around it (a whirl weapon reaches as far as it always does). A short rest follows.
    /// </summary>
    public partial class GameManager
    {
        private const float SpinCooldown = 0.28f; // short: circling on keeps spinning
        private float spinReadyAt;

        private void SpinAttack()
        {
            if (level == null || level.mission != MissionType.Hunt || roadPhase != RoadPhase.None || !robot.IsAlive) return;
            if (Time.time < spinReadyAt) return;
            spinReadyAt = Time.time + SpinCooldown;
            var w = weapon ?? Armory.Equipped;
            int damage = Challenge.RankBoost(Armory.Damage(w)) * (superLeft > 0f ? 2 : 1); // the rank reached adds its strength
            int reach = w.kind == WeaponKind.Whirl ? Mathf.Max(1, w.reach) : 1;
            if (heldWeapon != null) heldWeapon.Spin();
            StartCoroutine(SpinBody(0.32f));
            var glow = WeaponModels.Glow(w.tier);
            var centre = robot.transform.position;
            Shockwave.Create(centre, 1.4f + reach * 0.9f, glow);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                SlashFx.Create(centre, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.8f + reach * 0.8f), glow, 1f + 0.1f * damage);
            }
            AudioManager.PlaySfx(Sfx.Hop, 0.9f, 0.55f);
            int hits = 0;
            for (int x = -reach; x <= reach; x++)
                for (int y = -reach; y <= reach; y++)
                {
                    if (x == 0 && y == 0) continue;
                    var p = robot.Position + new GridPos(x, y);
                    if (hunt.Strike(p, damage)) hits++;
                    else if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) == 1 && hazards.Shatter(p)) hits++;
                }
            if (hits > 0) NoteFightBlow();
            lastStrikeTap = Time.time; // the fight view comes in as for any blow
            strikeCooldown = Mathf.Max(strikeCooldown, 0.25f);
            cameraRig.Shake(0.25f + 0.06f * hits);
            Haptics.Medium();
        }

        // ---------- The fury ----------

        /// <summary>Kills in a row (without being hit) that set off the fury, and the rest after one.</summary>
        private const int FuryStreak = 10;
        private const float FuryRest = 40f, FurySeconds = 3f;
        private float furyReadyAt, furyUntil;
        public bool FuryOn => Time.time < furyUntil;

        /// <summary>
        /// A long kill streak sets off the fury on its own: for three seconds foi whirls round and round at speed in a
        /// golden storm, striking every robot within two tiles again and again.
        /// </summary>
        private void StartFury()
        {
            if (level == null || level.mission != MissionType.Hunt || !robot.IsAlive) return;
            furyReadyAt = Time.time + FuryRest;
            furyUntil = Time.time + FurySeconds;
            ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.7f), Loc.T("streak.fury"), new Color(1f, 0.75f, 0.2f), 120f);
            ui.Flash(new Color(1f, 0.8f, 0.3f), 0.15f);
            AudioManager.PlaySfx(Sfx.Win, 0.9f, 0.7f);
            Haptics.Medium();
            StartCoroutine(Fury());
        }

        private IEnumerator Fury()
        {
            var v = robot.Visual;
            var start = v != null ? v.localRotation : Quaternion.identity;
            var gold = new Color(1f, 0.78f, 0.25f);
            float nextBlow = 0f, angle = 0f;
            while (Time.time < furyUntil && robot.IsAlive && level != null)
            {
                angle += Time.deltaTime * 1440f; // four turns a second
                if (v != null) v.localRotation = start * Quaternion.Euler(0f, angle, 0f);
                if (Time.time >= nextBlow)
                {
                    nextBlow = Time.time + 0.22f;
                    var w = weapon ?? Armory.Equipped;
                    int damage = Challenge.RankBoost(Armory.Damage(w)) * (superLeft > 0f ? 2 : 1); // the rank reached adds its strength
                    var centre = robot.transform.position;
                    Shockwave.Create(centre, 3.2f, gold);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f + angle * Mathf.Deg2Rad;
                        SlashFx.Create(centre, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.4f, gold, 1.4f);
                    }
                    int hits = 0;
                    for (int x = -2; x <= 2; x++)
                        for (int y = -2; y <= 2; y++)
                            if ((x != 0 || y != 0) && hunt.Strike(robot.Position + new GridPos(x, y), damage)) hits++;
                    if (hits > 0) { NoteFightBlow(); cameraRig.Shake(0.2f + 0.05f * hits); }
                    AudioManager.PlaySfx(Sfx.Hop, 0.6f, 0.5f);
                    lastStrikeTap = Time.time;
                }
                yield return null;
            }
            if (v != null) v.localRotation = start;
        }

        /// <summary>foi's body whirls round once (the robot itself keeps facing where it did).</summary>
        private IEnumerator SpinBody(float seconds)
        {
            var v = robot.Visual;
            if (v == null) yield break;
            var start = v.localRotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                if (v == null) yield break;
                float k = 1f - (1f - t) * (1f - t); // fast out of the wind-up, easing round
                v.localRotation = start * Quaternion.Euler(0f, k * 360f, 0f);
                yield return null;
            }
            if (v != null) v.localRotation = start;
        }
    }
}
