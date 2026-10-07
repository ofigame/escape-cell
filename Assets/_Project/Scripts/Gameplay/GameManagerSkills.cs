using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Skills go into the bag when they are picked up and are fired from the skill bar: as many as were collected,
    /// whenever the player chooses. The rarest is the super skill (the hardest floors only): a few seconds of being
    /// untouchable and fast.
    /// </summary>
    public partial class GameManager
    {
        private const float SuperSkillSeconds = 7f;
        /// <summary>The robot hops this much faster while the super skill lasts.</summary>
        private const float SuperSkillSpeed = 1.6f;

        private float superSkillLeft;
        private GameObject superSkillAura;

        /// <summary>An orb was picked up on the floor.</summary>
        private void OnPowerUpCollected(PowerUpType type, GridPos p)
        {
            // The first time a skill is picked up, a banner says what it does (and that it waits in the bag).
            if (PlayerPrefs.GetInt("sb_seen_skill." + type, 0) == 0)
            {
                PlayerPrefs.SetInt("sb_seen_skill." + type, 1);
                ui.ShowIntro(Loc.T("skill.title"), Loc.T("skillIntro." + type));
                if (SkillBag.Bagged(type) && PlayerPrefs.GetInt("sb_seen_skillbag", 0) == 0)
                {
                    PlayerPrefs.SetInt("sb_seen_skillbag", 1);
                    ui.Bip.Queue(Loc.T("bip.skillBag"));
                }
            }
            if (!SkillBag.Bagged(type))
            {
                ApplySkill(type, p);
                return;
            }
            SkillBag.Add(type);
            ui.SkillBar.Refresh(true);
            ui.SkillBar.Punch(type);
            fx.Burst(GridView.ToWorld(p) + Vector3.up * 0.5f, SkillBar.ColorOf(type), SkillBar.ColorOf(type) * 2f, 16, 3f);
            AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1.5f);
            FloatAt(GridView.ToWorld(p), Loc.F("float.bagged", Loc.T("skillName." + type), SkillBag.Count(type)), SkillBar.ColorOf(type));
            Haptics.Light();
        }

        /// <summary>A skill button was tapped.</summary>
        private void OnSkillPressed(PowerUpType type)
        {
            if (State != GameState.Playing || previewing || runner.Active) return;
            if (type == PowerUpType.Heart && (!HealthEnabled || health >= 0.999f))
            {
                FloatAt(robot.transform.position + Vector3.up * 0.6f, Loc.T("float.healthFull"), Palette.UiCyan);
                return;
            }
            if (!SkillBag.TryUse(type)) return;
            ApplySkill(type, robot.Position);
            ui.SkillBar.Refresh(true);
            ui.SkillBar.Punch(type);
            Haptics.Medium();
        }

        /// <summary>Untouchable and fast: a golden aura, blocks break on the robot, hops come quicker.</summary>
        private void StartSuperSkill()
        {
            superSkillLeft = SuperSkillSeconds;
            robot.GiveShield(SuperSkillSeconds, 999);
            robot.TimeBoost = Mathf.Max(robot.TimeBoost, SuperSkillSpeed);
            if (superSkillAura == null) superSkillAura = MakeSuperAura(robot.transform);
            fx.Burst(robot.transform.position + Vector3.up * 0.6f, SuperCrate.Gold, SuperCrate.GoldGlow, 50, 7f);
            Shockwave.Create(robot.transform.position, 2.2f, new Color(1f, 0.85f, 0.35f));
            cameraRig.Punch(1.2f);
            cameraRig.Shake(0.5f);
            AudioManager.PlaySfx(Sfx.Win, 0.7f, 1.4f);
            FloatAt(robot.transform.position + Vector3.up * 0.8f, Loc.F("float.superPower", (int)SuperSkillSeconds), SuperCrate.Gold);
        }

        private void UpdateSuperSkill(float dt)
        {
            if (superSkillLeft <= 0f) return;
            superSkillLeft -= dt;
            if (superSkillLeft > 0f) return;
            EndSuperSkill();
        }

        private void EndSuperSkill()
        {
            superSkillLeft = 0f;
            if (toolSlowLeft <= 0f) robot.TimeBoost = 1f;
            if (superSkillAura != null) Destroy(superSkillAura);
            superSkillAura = null;
        }
    }
}
