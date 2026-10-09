using SquashBot.Audio;
using SquashBot.Data;
using SquashBot.Monetization;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Rewarded ads the player picks besides the end-of-run offers: a health refill when it runs low in a level (a
    /// pulsing button over the bag, once a level), and the daily chest doubled. The workshop's coin ad lives in the
    /// shop screen (see AdRewards).
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The health refill was used this level.</summary>
        private bool adHealUsed;

        /// <summary>Health below this share offers the refill.</summary>
        private const float AdHealBelow = 0.3f;

        private bool AdHealOffered =>
            !adHealUsed && level != null && level.mission == MissionType.Hunt && State == GameState.Playing
            && roadPhase == RoadPhase.None && !previewing && HealthEnabled && health < AdHealBelow && Ads.Rewarded.IsReady;

        /// <summary>The refill button: the game waits during the ad, then carries on with a full health bar.</summary>
        private void OnAdHealPressed()
        {
            if (!AdHealOffered) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            ui.SetAdHealVisible(false);
            Ads.Rewarded.Show(rewarded =>
            {
                if (State == GameState.Paused)
                {
                    State = GameState.Playing;
                    Time.timeScale = slowMoLeft > 0f ? SlowMoScale : 1f;
                }
                if (!rewarded) return;
                adHealUsed = true;
                health = 1f;
                hurtCooldown = 0f;
                RefreshHealthBar();
                var at = robot.transform.position + Vector3.up * 0.6f;
                fx.Burst(at, new Color(0.4f, 1f, 0.6f), new Color(0.5f, 2.2f, 0.8f), 36, 5f);
                FloatAt(at + Vector3.up * 0.4f, Loc.T("float.potion"), new Color(0.5f, 1f, 0.65f));
                AudioManager.PlaySfx(Sfx.Shield, 1f, 1.3f);
                Haptics.Medium();
            });
        }

        /// <summary>After the daily chest: offer to double it for an ad (when one is ready).</summary>
        private void OfferDailyDouble(int got)
        {
            if (got <= 0 || !Ads.Rewarded.IsReady) return;
            ui.AdOffer.Show(Loc.T("ad.dailyTitle"), Loc.F("ad.dailyBody", got), () =>
            {
                Ads.Rewarded.Show(rewarded =>
                {
                    if (!rewarded) return;
                    SaveData.Coins += got;
                    FloatAt(robot.transform.position + Vector3.up * 0.8f, "+" + got, Palette.UiGold);
                    fx.Burst(robot.transform.position + Vector3.up * 0.8f, Palette.Coin, Palette.CoinGlow, 40, 6f);
                    AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
                    ui.RefreshMenuCoins();
                });
            });
        }
    }
}
