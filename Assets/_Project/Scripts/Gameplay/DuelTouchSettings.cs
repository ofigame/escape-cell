using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// How the duel tells a tap from a walk and a swipe (in dp and milliseconds): a tap moves less than
    /// <see cref="tapMaxDp"/> and lifts within <see cref="tapMaxMs"/>; a walk starts past <see cref="walkStartDp"/>
    /// sideways; a swipe goes <see cref="swipeMinDp"/> up or down within <see cref="swipeMaxMs"/>, its upright share at
    /// least <see cref="swipeRatio"/> times its sideways one. The touch sensitivity setting scales them (calm, normal,
    /// eager). Resources/Duel/DuelTouchSettings when there is one.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Duel/Touch Settings", fileName = "DuelTouchSettings")]
    public class DuelTouchSettings : ScriptableObject
    {
        public float tapMaxDp = 10f, tapMaxMs = 200f;
        public float walkStartDp = 10f;
        public float swipeMinDp = 40f, swipeMaxMs = 250f, swipeRatio = 1.5f;
        /// <summary>The floating stick's reach (dp).</summary>
        public float stickRadiusDp = 72f;
        /// <summary>After a swipe up, how long to watch for a curl at its end (a somersault), and how much it must turn (degrees).</summary>
        public float curlWaitMs = 250f, curlTurn = 110f;
        /// <summary>The sensitivity steps: the swipe's length and its upright share are multiplied by these (calm, normal, eager).</summary>
        public float[] swipeLengthSteps = { 1.3f, 1f, 0.75f };
        public float[] swipeRatioSteps = { 1.25f, 1f, 0.75f };

        private static DuelTouchSettings loaded;

        public static DuelTouchSettings Current
        {
            get
            {
                if (loaded == null) loaded = Resources.Load<DuelTouchSettings>("Duel/DuelTouchSettings");
                if (loaded == null) loaded = CreateInstance<DuelTouchSettings>();
                return loaded;
            }
        }
    }
}
