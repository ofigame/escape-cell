using System;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// One face for one moment: its priority (a lower one never cuts a higher one short) and how long it holds, the
    /// mouth and eyes drawn, the lids (0 open .. 1 shut; a wink shuts one), the brows (-1 down and angry .. 1 up and
    /// worried or surprised), a tint over the face (blue when frozen), a sweat drop, the hair standing up.
    /// </summary>
    [Serializable]
    public class FaceExpression
    {
        public FaceMoment moment;
        public int priority;
        public float seconds = 1f;
        public MouthShape mouth;
        public EyeShape eyes;
        [Range(0f, 1f)] public float lids;
        public bool wink;
        [Range(-1f, 1f)] public float brows;
        public Color tint = new Color(0.6f, 0.8f, 1f);
        [Range(0f, 1f)] public float tintAmount;
        public bool sweat, hairUp;

        public FaceExpression() { }

        public FaceExpression(FaceMoment m, int priority, float seconds, MouthShape mouth = MouthShape.None, EyeShape eyes = EyeShape.None, float lids = 0f, float brows = 0f)
        {
            moment = m; this.priority = priority; this.seconds = seconds; this.mouth = mouth; this.eyes = eyes; this.lids = lids; this.brows = brows;
        }
    }
}
