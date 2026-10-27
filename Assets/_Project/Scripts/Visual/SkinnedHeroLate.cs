using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A duel body's second step each frame, after every body has taken its pose (so the two are kept out of each other
    /// on where both stand this frame): the push boxes, then the hands' grip. Added by the body when it is paired.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class SkinnedHeroLate : MonoBehaviour
    {
        private SkinnedHero body;

        private void LateUpdate()
        {
            if (body == null) body = GetComponent<SkinnedHero>();
            if (body != null) body.FinishFrame();
        }
    }
}
