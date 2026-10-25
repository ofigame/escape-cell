using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// When the loss's tips apply (the design's §6.3): how many breakers caught the player, shields broken, ultimates
    /// taken, combo moves let run out, how long the ultimate stood ready, how many tiers the rival's weapon was ahead;
    /// and how many general tips there are. Resources/Duel/DefeatTipSettings when there is one.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Duel/Defeat Tip Settings", fileName = "DefeatTipSettings")]
    public class DefeatTipSettings : ScriptableObject
    {
        public int breakersCaught = 3, shieldsBroken = 2, ultisTaken = 2, comboMissed = 3, weaponTiersAhead = 2, generalTips = 6;
        public float ultiIdleSeconds = 20f;

        private static DefeatTipSettings loaded;

        public static DefeatTipSettings Current
        {
            get
            {
                if (loaded == null) loaded = Resources.Load<DefeatTipSettings>("Duel/DefeatTipSettings");
                if (loaded == null) loaded = CreateInstance<DefeatTipSettings>();
                return loaded;
            }
        }
    }
}
