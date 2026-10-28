using SquashBot.Data;
using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>
    /// The workshop is part of the story: Bip talks about what was just bought, vanG's eye opens in the ceiling when
    /// the robot gets more colourful (softer after level 200), and after level 211 Kuzgun drops by now and then.
    /// </summary>
    public static class WorkshopTalk
    {
        public static readonly Color BipColor = UiKit.Style.successLight;
        public static readonly Color VangColor = UiKit.Style.theirs;
        public static readonly Color KuzgunColor = UiKit.Style.gold;

        private const string IntroKey = "sb_ws_intro";
        /// <summary>The workshop opens after the escape from the test cell (level 10).</summary>
        public const int OpensAt = 10;

        /// <summary>Levels beaten so far (the 0-based index of the next level).</summary>
        private static int Reached => SaveData.UnlockedLevel;

        public static bool IntroDue => Reached >= OpensAt && Prefs.GetInt(IntroKey, 0) == 0;

        /// <summary>The first visit: Bip shows the hidden workshop and explains the gold (the paint tube it gave is gone with the paints).</summary>
        public static void Welcome(BipTip tip)
        {
            Prefs.SetInt(IntroKey, 1);
            tip.Queue(Loc.T("ws.bip.welcome"));
            tip.Queue(Loc.T("ws.bip.gold"));
        }

        public static void Poor(BipTip tip) => tip.Queue(Loc.T("ws.bip.poor"));

        public static void ToolBought(BipTip tip, Tool t, bool first) =>
            tip.Queue(Loc.T(first ? "ws.bip.tool." + t : "ws.bip.upgraded"));

        public static void UpgradeBought(BipTip tip, Upgrade u)
        {
            string key = "ws.bip.up." + u;
            tip.Queue(Loc.T(Loc.Has(key) ? key : "ws.bip.upgraded"));
        }

        public static void CounterBought(BipTip tip) => tip.Queue(Loc.T("ws.bip.counter"));

        /// <summary>A look was bought: Bip is delighted, vanG is not, Kuzgun has an opinion.</summary>
        public static void CosmeticBought(BipTip tip, Cosmetic c)
        {
            tip.Queue(Loc.T(Random.value < 0.5f ? "ws.bip.look1" : "ws.bip.look2"));

            if (Random.value < 0.6f)
            {
                string vang = Reached >= 200 ? "ws.vang.soft" : Random.value < 0.5f ? "ws.vang.grey" : "ws.vang.error";
                tip.Queue(Loc.T(vang), Loc.T("story.warden"), VangColor);
            }

            if (Reached >= 211 && Random.value < 0.35f)
                tip.Queue(Loc.T("ws.kuzgun.prices"), Loc.T("story.kuzgun"), KuzgunColor);
        }

        /// <summary>A piece was put on: Kuzgun notices his own seal.</summary>
        public static void Worn(BipTip tip, Cosmetic c)
        {
            if (c.id == "badge.raven") tip.Queue(Loc.T("ws.kuzgun.seal"), Loc.T("story.kuzgun"), KuzgunColor);
        }
    }
}
