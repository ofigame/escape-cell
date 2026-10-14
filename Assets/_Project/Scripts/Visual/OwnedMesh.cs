using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>A mesh made at run time for one object: destroyed with it (else it would stay in memory).</summary>
    public class OwnedMesh : MonoBehaviour
    {
        public Mesh mesh;
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
