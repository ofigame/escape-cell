using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The seventh level of every world (from the second world on) is a surprise (the scenario's §6.2): a rule of its own,
    /// told on the briefing — a time race (every enemy in 90 seconds), one life (no hearts: one knock-down ends it),
    /// combos only (only the blows after the third in a row do damage) or darkness (only foi's own light).
    /// </summary>
    public partial class GameManager
    {
        public enum LevelRule { None, TimeRace, OneLife, ComboOnly, Darkness }

        private LevelRule levelRule;
        private const float RaceSeconds = 90f;
        private int ruleCombo;
        private float ruleComboAt = -9f;
        private Light ruleLamp;
        private float ruleSunWas = -1f;
        private Color ruleAmbientWas;

        private static LevelRule RuleFor(int index) =>
            index >= LevelCatalog.LevelsPerWorld && index % LevelCatalog.LevelsPerWorld == 6 ? (LevelRule)(1 + (index / LevelCatalog.LevelsPerWorld) % 4) : LevelRule.None;

        /// <summary>A level starts: its rule (if it is a seventh level), darkness put up.</summary>
        private void BeginLevelRule()
        {
            EndLevelRule();
            levelRule = challengeRun || bonusRun || arenaRun || dailyRun ? LevelRule.None : RuleFor(levelIndex);
            ruleCombo = 0;
            if (levelRule != LevelRule.Darkness) return;
            // only foi's light: the sun nearly out, a lamp riding with foi
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.enabled) { ruleSunWas = l.intensity; l.intensity *= 0.12f; break; }
            ruleAmbientWas = RenderSettings.ambientLight;
            RenderSettings.ambientLight = ruleAmbientWas * 0.15f;
            ruleLamp = new GameObject("FoiLamp").AddComponent<Light>();
            ruleLamp.type = LightType.Point;
            ruleLamp.range = 5.5f;
            ruleLamp.intensity = 2.6f;
            ruleLamp.color = new Color(1f, 0.92f, 0.75f);
            ruleLamp.transform.SetParent(robot.transform, false);
            ruleLamp.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        }

        /// <summary>The level is over (or left): the darkness lifted.</summary>
        private void EndLevelRule()
        {
            if (ruleLamp != null) Destroy(ruleLamp.gameObject);
            ruleLamp = null;
            if (ruleSunWas >= 0f)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional && l.enabled) { l.intensity = ruleSunWas; break; }
                RenderSettings.ambientLight = ruleAmbientWas;
                ruleSunWas = -1f;
            }
            levelRule = LevelRule.None;
        }

        /// <summary>Each frame of the level: the time race's clock.</summary>
        private void UpdateLevelRule()
        {
            if (levelRule != LevelRule.TimeRace || State != GameState.Playing || roadPhase != RoadPhase.None) return;
            float left = RaceSeconds - elapsed;
            if (left <= 0f) { Lose(Loc.T("rule.timeUp")); return; }
            ui.SetHudTitle(Loc.T("rule.TimeRace") + " · " + Mathf.CeilToInt(left));
        }

        /// <summary>One life: the first blow that would take health ends the level instead.</summary>
        private bool RuleOneLife => levelRule == LevelRule.OneLife;

        /// <summary>Combos only: blows count only from the fourth in a row (the first three only build the run).</summary>
        private int RuleDamage(int damage)
        {
            if (levelRule != LevelRule.ComboOnly) return damage;
            ruleCombo = Time.time - ruleComboAt < 1.4f ? ruleCombo + 1 : 1;
            ruleComboAt = Time.time;
            if (ruleCombo <= 3) { FloatAt(robot.transform.position + Vector3.up, ruleCombo + "/3", new Color(1f, 0.85f, 0.4f)); return 0; }
            return damage;
        }

        /// <summary>The briefing's first card on a seventh level: its rule.</summary>
        private string RuleCard() => levelRule == LevelRule.None ? null : Loc.T("rule." + levelRule) + ": " + Loc.T("rule." + levelRule + ".d");
    }
}
