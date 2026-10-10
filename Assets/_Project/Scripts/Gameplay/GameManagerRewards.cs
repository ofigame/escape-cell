using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Monetization;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// What keeps a player going: kill streaks (robots and towers knocked out within a few seconds of each other —
    /// three, five, eight, twelve and on — each called out big with a flash, a ring of light, a fanfare and bonus
    /// coins), today's quests counting up as the floor is played (a note pops when one is done), and the chest at the
    /// end of every won floor (its coins doubled for an ad).
    /// </summary>
    public partial class GameManager
    {
        private int killStreak;
        private float lastKillAt = -10f;
        private const float StreakWindow = 3f;

        private void InitRewards()
        {
            hunt.Felled += g => QuestProgress(g);
            hunt.ShieldHit += (p, broke) => { if (broke) QuestProgress(DailyGoal.Shields); };
        }

        /// <summary>A robot or tower went down: the streak grows if it came soon after the last one.</summary>
        private void CountStreak(GridPos p)
        {
            killStreak = Time.time - lastKillAt <= StreakWindow ? killStreak + 1 : 1;
            lastKillAt = Time.time;
            int n = killStreak;
            bool callout = n == 3 || n == 5 || n == 8 || n == 12 || (n > 12 && n % 4 == 0);
            if (!callout) return;
            string key = n == 3 ? "streak.3" : n == 5 ? "streak.5" : n == 8 ? "streak.8" : n == 12 ? "streak.12" : null;
            string text = key != null ? Loc.T(key) : Loc.F("streak.n", n);
            var colour = n >= 12 ? new Color(1f, 0.4f, 0.9f) : n >= 8 ? new Color(1f, 0.55f, 0.25f) : n >= 5 ? Palette.UiGold : Palette.UiCyan;
            ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.62f), text, colour, 96f + Mathf.Min(n, 16) * 2f);
            ui.Flash(colour, 0.1f);
            Shockwave.Create(robot.transform.position, 2f + Mathf.Min(n, 12) * 0.15f, colour);
            fx.Burst(robot.transform.position + Vector3.up * 0.8f, colour, colour * 2.4f, 30 + n * 2, 6f);
            AudioManager.PlaySfx(Sfx.Win, 0.55f, 1.1f + Mathf.Min(n, 16) * 0.03f);
            Haptics.Medium();
            int bonus = n;
            coinsThisRun += bonus;
            FloatAt(robot.transform.position + Vector3.up * 1.3f, "+" + bonus, Palette.UiGold);
            if (n >= 5) QuestProgress(DailyGoal.Streak);
        }

        private void QuestProgress(DailyGoal goal, int amount = 1)
        {
            if (bonusRun || !DailyQuests.Report(goal, amount)) return;
            if (robot != null && State == GameState.Playing)
                FloatAt(robot.transform.position + Vector3.up * 1.6f, Loc.T("quests.complete"), new Color(0.55f, 0.85f, 1f));
            AudioManager.PlaySfx(Sfx.Coin, 0.8f, 1.6f);
        }

        /// <summary>The chest after a won floor, over the result card.</summary>
        private void OfferChest()
        {
            if (bonusRun || dailyRun || level == null) return;
            var reward = LevelChest.Roll(levelIndex);
            ui.Chest.Show(reward.rarity, () =>
            {
                LevelChest.Grant(reward);
                ui.RefreshMenuCoins();
                return ChestLine(reward.coins, reward.lives);
            }, Ads.Rewarded.IsReady ? () =>
            {
                Ads.Rewarded.Show(rewarded =>
                {
                    if (!rewarded) return;
                    SaveData.Coins += reward.coins;
                    ui.RefreshMenuCoins();
                    ui.Chest.ShowDoubled(ChestLine(reward.coins * 2, reward.lives));
                });
            } : (System.Action)null);
        }

        private static string ChestLine(int coins, int lives) =>
            Loc.F("chest.coins", coins) + (lives > 0 ? "\n" + Loc.F("chest.lives", lives) : "");
    }
}
