using UnityEngine;

namespace SquashBot.Audio
{
    /// <summary>
    /// The duel's music by the match (the design's §11.3): the base layer always; the tension layer while the score is
    /// close (at most <see cref="closeScore"/> apart) in the last <see cref="lastMinute"/> seconds; the peak (drums) in the
    /// last <see cref="lastSeconds"/>, through an ultimate and on a level score's golden knock-down. The music dips a
    /// moment at a knock-down (the crowd to the front). Resources/Duel/DuelMusicSettings when there is one.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Duel/Music Settings", fileName = "DuelMusicSettings")]
    public class DuelMusicSettings : ScriptableObject
    {
        /// <summary>The music's tension (0..1) for each: the base only, the tension layer, the peak.</summary>
        public float baseTension = 0.1f, closeTension = 0.55f, peakTension = 1f;
        public int closeScore = 1;
        public float lastMinute = 60f, lastSeconds = 10f;
        public float knockDip = 0.4f;

        private static DuelMusicSettings loaded;

        public static DuelMusicSettings Current
        {
            get
            {
                if (loaded == null) loaded = Resources.Load<DuelMusicSettings>("Duel/DuelMusicSettings");
                if (loaded == null) loaded = CreateInstance<DuelMusicSettings>();
                return loaded;
            }
        }
    }
}
