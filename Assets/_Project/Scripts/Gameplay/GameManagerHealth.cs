using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Health: the first 20 levels are one hit and out; from level 21 the robot has a health bar (100%) and every hit
    /// takes a share of it. Later levels take smaller shares (the robot gets tougher with the story), armor bought in
    /// the workshop shaves more off each hit, and hearts on the floor heal. At 0% the old rules apply (a rescue charge
    /// or the level is lost).
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Health bars start at this level (0-based index, so level 21).</summary>
        public const int HealthFromLevel = 0;
        /// <summary>A heart pickup heals this much.</summary>
        private const float HeartHeal = 0.3f;
        /// <summary>After a hit the robot can't be hurt again for a moment.</summary>
        private const float HurtGrace = 1.3f;

        private float health = 1f;
        /// <summary>A share of health the next hit takes instead of the usual (set around a robot's blow).</summary>
        private float hitShareOverride = -1f;
        private float hurtCooldown;

        private bool HealthEnabled => !bonusRun && levelIndex >= HealthFromLevel;

        /// <summary>
        /// The share of the bar one full hit takes: a third at level 21, a sixth around level 150, a tenth at the end
        /// (that is about 3, 6-7 and 10 hits).
        /// </summary>
        public static float HitShare(int index)
        {
            float t = Mathf.Clamp01((index - HealthFromLevel) / (float)(LevelCatalog.LevelCount - 1 - HealthFromLevel));
            return 1f / Mathf.Lerp(3f, 10f, Mathf.Pow(t, 0.85f));
        }

        private void SetupHealth()
        {
            health = 1f;
            hurtCooldown = 0f;
            RefreshHealthBar();
        }

        private void UpdateHealth(float dt)
        {
            if (hurtCooldown > 0f) hurtCooldown -= dt;
        }

        private void RefreshHealthBar() => ui.SetHealth(HealthEnabled && State != GameState.Result, health, Shop.ArmorShare);

        /// <summary>
        /// A hit that would end the run: with health left the bar takes it instead (the robot is knocked to a safe tile
        /// when it fell into something). <paramref name="weight"/> scales the hit (fire and poison hurt a little less).
        /// </summary>
        private bool TryTakeHealthHit(GridPos p, bool crushed, float weight = 1f)
        {
            if (!HealthEnabled) return false;
            if (hurtCooldown > 0f) return true; // still blinking from the last hit
            float share = hitShareOverride >= 0f ? hitShareOverride
                : level != null && level.mission == MissionType.Hunt && crushed ? level.crateShare
                : HitShare(levelIndex) * weight;
            // foi toughens up along the campaign (a quarter less from every hit by the last level), and the workshop's
            // armour comes on top: the enemies grow too, so the shop still matters.
            float toughness = Mathf.Lerp(1f, 0.74f, levelIndex / (float)(LevelCatalog.LevelCount - 1));
            float damage = share * toughness * (1f - Shop.ArmorShare);
            if (health - damage <= 0.001f)
            {
                health = 0f;
                RefreshHealthBar();
                return false;
            }
            health -= damage;
            hurtCooldown = HurtGrace;
            ui.Flash(new Color(1f, 0.15f, 0.15f), 0.3f);
            BreakCombo();

            if (crushed) hazards.Shatter(p);
            else robot.RescueTo(SafeTileNear(robot.LastLeftTile, p));
            robot.GiveShield(HurtGrace); // the blink: nothing hurts for a moment

            cameraRig.Shake(0.9f);
            cameraRig.Punch(0.7f);
            fx.Burst(robot.transform.position + Vector3.up * 0.4f, new Color(1f, 0.35f, 0.35f), new Color(2.4f, 0.5f, 0.4f), 22, 5f);
            AudioManager.PlaySfx(Sfx.Squash, 0.7f, 1.4f);
            Haptics.Medium();
            FloatAt(GridView.ToWorld(p), "-" + Mathf.RoundToInt(damage * 100f) + "%", Palette.UiRed);
            if (health < 0.35f) BipSay("lowHealth");
            RefreshHealthBar();
            return true;
        }

        /// <summary>A heart from the floor: some health back.</summary>
        private void Heal(GridPos p)
        {
            float before = health;
            health = Mathf.Min(1f, health + HeartHeal);
            fx.Burst(GridView.ToWorld(p) + Vector3.up * 0.5f, new Color(1f, 0.45f, 0.55f), new Color(2.4f, 0.6f, 0.8f), 20, 4f);
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.6f);
            FloatAt(GridView.ToWorld(p), "+" + Mathf.RoundToInt((health - before) * 100f) + "%", new Color(1f, 0.5f, 0.6f));
            RefreshHealthBar();
        }

        /// <summary>Hearts only show up on health levels while the bar isn't full.</summary>
        private bool WantsHeart() => HealthEnabled && health < 0.85f;
    }
}
