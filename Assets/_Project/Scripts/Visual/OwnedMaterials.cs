using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Materials made at run time for one object (a robot, its health bar): destroyed with it. Destroying a GameObject
    /// leaves its materials in memory, so floor after floor of robots would pile them up.
    /// </summary>
    public class OwnedMaterials : MonoBehaviour
    {
        private readonly List<Material> owned = new List<Material>();

        public static void Own(GameObject go, params Material[] mats)
        {
            var o = go.GetComponent<OwnedMaterials>() ?? go.AddComponent<OwnedMaterials>();
            foreach (var m in mats) if (m != null && !o.owned.Contains(m)) o.owned.Add(m);
        }

        /// <summary>Takes every material the renderers under <paramref name="go"/> use (only for objects whose materials are
        /// all their own, never shared or cached ones).</summary>
        public static void OwnAllUnder(GameObject go)
        {
            var o = go.GetComponent<OwnedMaterials>() ?? go.AddComponent<OwnedMaterials>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && !o.owned.Contains(m)) o.owned.Add(m);
        }

        private void OnDestroy()
        {
            foreach (var m in owned) if (m != null) Destroy(m);
            owned.Clear();
        }
    }
}
