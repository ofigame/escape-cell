using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>
    /// The duel's buttons: their sizes and gaps (in dp, the phone's density-free unit: 48 dp is about a fingertip),
    /// the dead ring round each, and the size and solidity steps of the settings. Resources/Duel/DuelControlsSettings
    /// when there is one, else these values.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Duel/Controls Settings", fileName = "DuelControlsSettings")]
    public class DuelControlsSettings : ScriptableObject
    {
        /// <summary>Canvas units per dp (the 1080-wide canvas over a phone about 411 dp wide).</summary>
        public float unitsPerDp = 2.63f;
        public float shieldDp = 76f, ultiDp = 82f, specialDp = 62f, contextDp = 62f, heartDp = 50f, jumpDp = 56f, pauseDp = 40f, emoteDp = 44f;
        /// <summary>Between buttons, and from the safe area's edges.</summary>
        public float gapDp = 10f, edgeDp = 16f;
        /// <summary>The ring round every button where a touch does nothing (a finger just missing a button never strikes).</summary>
        public float deadZoneDp = 11f;
        /// <summary>The settings' button sizes and the solidity of buttons not in use.</summary>
        public float[] sizeSteps = { 0.85f, 1f, 1.15f };
        public float[] opacitySteps = { 0.7f, 0.85f, 1f };
        /// <summary>A pressed button shrinks to this for this long (seconds).</summary>
        public float pressScale = 0.9f, pressSeconds = 0.06f;
        /// <summary>The power bars under the health bars: height (canvas units) and the gap below the bar.</summary>
        public float meterHeight = 20f, meterGap = 10f;

        private static DuelControlsSettings loaded;

        public static DuelControlsSettings Current
        {
            get
            {
                if (loaded == null) loaded = Resources.Load<DuelControlsSettings>("Duel/DuelControlsSettings");
                if (loaded == null) loaded = CreateInstance<DuelControlsSettings>();
                return loaded;
            }
        }

        public float Units(float dp) => dp * unitsPerDp;
    }
}
