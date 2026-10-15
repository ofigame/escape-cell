using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Static batching for built scenery that leaves out the pieces that move (flames, banners): a merged piece no
    /// longer follows its transform, so merging those froze them while their animation still ran for nothing.
    /// </summary>
    public static class StaticMerge
    {
        /// <param name="moves">True for a renderer that animates (it stays as it is).</param>
        /// <param name="max">Past this many pieces nothing is merged (the merged copy costs memory).</param>
        public static void Combine(GameObject root, Func<Transform, bool> moves, int max = 6000)
        {
            var still = new List<GameObject>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (!moves(r.transform)) still.Add(r.gameObject);
            if (still.Count > 0 && still.Count < max) StaticBatchingUtility.Combine(still.ToArray(), root);
        }

        /// <summary>Whether a transform is one of these or under one of them.</summary>
        public static bool Under(Transform t, HashSet<Transform> roots)
        {
            for (; t != null; t = t.parent) if (roots.Contains(t)) return true;
            return false;
        }
    }
}
