using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The duel's faces (the design's §2.1, §7): a <see cref="FaceExpression"/> for every moment, and how fast the lids
    /// and brows move between them (60–120 ms). Resources/Duel/FaceExpressionData when there is one, else these.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Duel/Face Expressions", fileName = "FaceExpressionData")]
    public class FaceExpressionData : ScriptableObject
    {
        /// <summary>The lids and brows reach a new face in this long (seconds).</summary>
        public float blend = 0.09f;
        public List<FaceExpression> faces = Defaults();

        private static FaceExpressionData loaded;

        public static FaceExpressionData Current
        {
            get
            {
                if (loaded == null) loaded = Resources.Load<FaceExpressionData>("Duel/FaceExpressionData");
                if (loaded == null) loaded = CreateInstance<FaceExpressionData>();
                return loaded;
            }
        }

        public FaceExpression Of(FaceMoment m)
        {
            foreach (var f in faces) if (f.moment == m) return f;
            return faces.Count > 0 ? faces[0] : new FaceExpression();
        }

        public static List<FaceExpression> Defaults() => new List<FaceExpression>
        {
            new FaceExpression(FaceMoment.Idle, 0, 99f, brows: -0.35f),                                  // set, the brows a little knit
            new FaceExpression(FaceMoment.LightHit, 3, 0.12f, lids: 0.7f, brows: -0.5f),                              // the eyes narrow a moment
            new FaceExpression(FaceMoment.HeavyHit, 5, 0.45f, MouthShape.Grit, lids: 1f, brows: -1f),                 // eyes squeezed, the mouth twisted
            new FaceExpression(FaceMoment.Launched, 7, 0.9f, MouthShape.Open, EyeShape.Wide, brows: 1f),              // mouth wide open, eyes big
            new FaceExpression(FaceMoment.Shocked, 6, 0.8f, MouthShape.Grit, EyeShape.Crossed, brows: 0.6f) { hairUp = true }, // cross-eyed, hair on end
            new FaceExpression(FaceMoment.Frozen, 6, 1.2f, MouthShape.Chatter, lids: 0.5f, brows: 0.5f) { tintAmount = 0.45f }, // teeth chattering, blue
            new FaceExpression(FaceMoment.LowHealth, 1, 99f, MouthShape.Wavy, brows: 0.8f) { sweat = true },          // worried, a drop of sweat
            new FaceExpression(FaceMoment.PerfectGuard, 4, 1f, MouthShape.Sly, lids: 0.5f, brows: -0.4f),            // a sly grin
            new FaceExpression(FaceMoment.UltiStart, 6, 1.2f, MouthShape.Grit, lids: 0.5f, brows: -1f),              // angry, teeth clenched
            new FaceExpression(FaceMoment.KnockedOut, 9, 3f, MouthShape.TongueOut, EyeShape.Spiral),                  // spiral eyes, tongue out
            new FaceExpression(FaceMoment.Victory, 8, 4f, MouthShape.Grin, brows: 0.8f),                              // a big grin, brows up
            new FaceExpression(FaceMoment.Defeat, 8, 4f, MouthShape.Pout, brows: 0.6f),                 // sulking, the lip out
            new FaceExpression(FaceMoment.Draw, 8, 4f, MouthShape.Wavy, brows: 0.45f),                                // a shrug
            new FaceExpression(FaceMoment.TongueOut, 2, 1.2f, MouthShape.TongueOut, brows: 0.2f),
            new FaceExpression(FaceMoment.BrowRaise, 2, 1.2f, MouthShape.Smile, brows: 1f),
            new FaceExpression(FaceMoment.Wink, 2, 1.2f, MouthShape.Sly, brows: -0.2f) { wink = true },
            new FaceExpression(FaceMoment.SlyGrin, 2, 1.2f, MouthShape.Sly, lids: 0.5f, brows: -0.6f),
            new FaceExpression(FaceMoment.FakeCry, 2, 1.2f, MouthShape.Cry, EyeShape.Tears, lids: 0.5f, brows: 0.9f),
            new FaceExpression(FaceMoment.Surprise, 2, 1.2f, MouthShape.Open, EyeShape.Wide, brows: 1f),
        };
    }
}
