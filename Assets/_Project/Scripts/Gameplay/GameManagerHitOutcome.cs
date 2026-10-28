using SquashBot.Audio;
using SquashBot.Combat;
using SquashBot.Data;
using SquashBot.Visual;
using TMPro;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Every blow's outcome shown (Docs/ARAYUZ_HATA_VE_TEMIZLIK.md, section 3): a short word over the rival's head in the
    /// outcome's colour (the damage number for a hit: white, big and yellow for a strong one), its own sound; the blow
    /// log beside it for developers; the "not ready" tremble in foi's hand for a tap too early for the input buffer.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The input buffer (section 3.3): a tap this long before the weapon is ready is kept and struck then.</summary>
        private static float InputBuffer => (CombatLibrary.Settings != null ? CombatLibrary.Settings.inputBufferMs : 120f) / 1000f;

        /// <summary>A blow's outcome shown and logged (<paramref name="damage"/>: what went through; <paramref name="strong"/>:
        /// a power hit or a knockdown's).</summary>
        private void ShowOutcome(HitOutcome outcome, int damage, string reason, bool strong = false)
        {
            HitLog.Add(outcome, reason);
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            var at = (hunt.ChallengeFoeAt ?? robot.transform.position) + Vector3.up * 2.1f; // (over the rival's head)
            switch (outcome)
            {
                case HitOutcome.Hit:
                    if (damage > 0) FloatSized(at, damage.ToString(), strong ? Palette.UiGold : Color.white, strong ? 78f : 56f);
                    break;
                case HitOutcome.Blocked:
                    FloatSized(at, Loc.T("hit.blocked") + (damage > 0 ? "  " + damage : ""), UI.UiKit.Style.info, 50f);
                    AudioManager.PlaySfx(Sfx.Shield, 0.9f, 0.8f); // (the metal "dong")
                    break;
                case HitOutcome.Parried:
                    break; // (its big "PARRIED!" and foi's reel are the guard's own: GameManagerDuelSkills)
                case HitOutcome.Dodged:
                    FloatSized(at, Loc.T("hit.dodged"), UI.UiKit.Style.cyan, 50f); // (its air sound and puff: DodgedShown)
                    break;
                case HitOutcome.Missed:
                    FloatSized(at, Loc.T("hit.missed"), UI.UiKit.A(UI.UiKit.Style.white, 0.55f), 44f);
                    break;
                case HitOutcome.Immune:
                    FloatSized(at, Loc.T("hit.immune"), UI.UiKit.Style.silver, 46f);
                    AudioManager.PlaySfx(Sfx.Blocked, 0.6f, 1.6f); // (the metal ricochet)
                    break;
                case HitOutcome.Reflected:
                    FloatSized(at, Loc.T("hit.reflected"), UI.UiKit.Style.cyan, 54f);
                    break;
            }
        }

        private void FloatSized(Vector3 world, string text, Color color, float size)
        {
            if (ui == null || cameraRig == null) return;
            ui.Float(cameraRig.Cam.WorldToScreenPoint(world), text, color, size);
        }

        /// <summary>A tap too early for the buffer: a small shake of the weapon in foi's hand, no words.</summary>
        private void NotReady(string why)
        {
            HitLog.Add(HitOutcome.Missed, "NOT READY: " + why);
            if (heldWeapon != null) heldWeapon.Tremble();
        }

        // ---------- the developer's blow log (section 3.4) ----------

        private TextMeshProUGUI hitLogText;

        /// <summary>The blow log panel shown or hidden (test builds: the duel lab, or F9 on a computer).</summary>
        private void ShowHitLog(bool on)
        {
#if REAL_ADS
            on = false; // (never in a store build)
#endif
            if (!on) { if (hitLogText != null) hitLogText.transform.parent.gameObject.SetActive(false); return; }
            if (hitLogText == null)
            {
                var panel = UI.UiFactory.Box("HitLog", ui.transform, new Vector2(0f, 1f), new Vector2(12f, -260f), new Vector2(560f, 330f));
                panel.pivot = new Vector2(0f, 1f);
                UI.UiFactory.Fill(panel, new Color(0f, 0f, 0f, 0.55f)).raycastTarget = false;
                hitLogText = UI.UiFactory.TextBox("Lines", panel, new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(540f, 314f), "", 20f, Color.white, FontStyles.Normal, align: TextAlignmentOptions.TopLeft);
                hitLogText.raycastTarget = false;
                HitLog.Changed += () => { if (hitLogText != null) hitLogText.text = HitLog.Text(); };
            }
            hitLogText.transform.parent.gameObject.SetActive(true);
            hitLogText.text = HitLog.Text();
        }
    }
}
