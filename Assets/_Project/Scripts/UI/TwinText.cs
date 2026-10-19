using UnityEngine;

namespace BrickMuseum.Core
{
    /// <summary>Keeps the back face of a two-sided board saying the same as the front.</summary>
    public sealed class TwinText : MonoBehaviour
    {
        public TMPro.TextMeshPro twin;
        private TMPro.TextMeshPro self;
        private void LateUpdate()
        {
            if (self == null) self = GetComponent<TMPro.TextMeshPro>();
            if (twin != null && twin.text != self.text) twin.text = self.text;
        }
    }
}
