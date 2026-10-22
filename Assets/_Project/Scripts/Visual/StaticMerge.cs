using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SquashBot.Visual
{
    /// <summary>
    /// Static batching for built scenery that leaves out the pieces that move (flames, banners): a merged piece no
    /// longer follows its transform, so merging those froze them while their animation still ran for nothing.
    /// </summary>
    public static class StaticMerge
    {
        /// <param name="moves">True for a renderer that animates (it stays as it is).</param>
        /// <param name="max">Past this many pieces the still ones are baked instead (see <see cref="Bake"/>): static
        /// batching keeps every piece's renderer, and thousands of them cost the phone every frame.</param>
        /// <param name="maxVerts">Past this many corners in all they are baked too: static batching keeps a copy of
        /// everything it merged in memory as well as on the graphics card (tens of MB on a big fortress).</param>
        public static void Combine(GameObject root, Func<Transform, bool> moves, int max = 6000, int maxVerts = int.MaxValue)
        {
            var still = new List<GameObject>();
            long verts = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (!moves(r.transform))
                {
                    still.Add(r.gameObject);
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) verts += mf.sharedMesh.vertexCount;
                }
            if (still.Count == 0) return;
            if (still.Count < max && verts < maxVerts) Batch(root, still.ToArray());
            else Bake(root, moves);
        }

        /// <summary>
        /// Truly merges the still pieces under <paramref name="root"/>: by material, in squares of the floor (so each
        /// merged piece is still culled and lit by what is near it), into a few big meshes; the pieces' own renderers are
        /// switched off (their objects stay, so nothing that looks them up breaks). For huge floors.
        /// </summary>
        public static void Bake(GameObject root, Func<Transform, bool> moves, float cell = 10f)
        {
            var groups = new Dictionary<(Material, int, ShadowCastingMode, bool, int, int), List<MeshRenderer>>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || moves(r.transform)) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable || r.sharedMaterials.Length != 1 || mf.sharedMesh.subMeshCount != 1) continue;
                var c = r.bounds.center;
                var key = (r.sharedMaterial, r.gameObject.layer, r.shadowCastingMode, r.receiveShadows, Mathf.FloorToInt(c.x / cell), Mathf.FloorToInt(c.z / cell));
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshRenderer>();
                list.Add(r);
            }
            var toRoot = root.transform.worldToLocalMatrix;
            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2) continue;
                var parts = new List<CombineInstance>();
                int verts = 0;
                void Flush()
                {
                    if (parts.Count == 0) return;
                    var mesh = new Mesh { name = "Baked " + kv.Key.Item1.name, indexFormat = verts > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    mesh.CombineMeshes(parts.ToArray(), true, true);
                    mesh.UploadMeshData(true); // (no copy kept on the CPU)
                    var go = new GameObject("Baked");
                    go.layer = kv.Key.Item2;
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = kv.Key.Item1;
                    mr.shadowCastingMode = kv.Key.Item3;
                    mr.receiveShadows = kv.Key.Item4;
                    go.AddComponent<BakedMesh>().mesh = mesh;
                    parts.Clear();
                    verts = 0;
                }
                foreach (var r in kv.Value)
                {
                    var m = r.GetComponent<MeshFilter>().sharedMesh;
                    if (verts + m.vertexCount > 200000) Flush();
                    parts.Add(new CombineInstance { mesh = m, transform = toRoot * r.localToWorldMatrix });
                    verts += m.vertexCount;
                    r.enabled = false;
                }
                Flush();
            }
        }

        /// <summary>
        /// Static batching (all of <paramref name="root"/>, or just <paramref name="pieces"/>) whose merged meshes go with
        /// the root: left to themselves they stay in memory until the next sweep, a few floors later.
        /// </summary>
        public static void Batch(GameObject root, GameObject[] pieces = null)
        {
            if (pieces == null) StaticBatchingUtility.Combine(root);
            else StaticBatchingUtility.Combine(pieces, root);
            var merged = new HashSet<Mesh>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Combined Mesh")) merged.Add(mf.sharedMesh);
            if (merged.Count > 0) root.AddComponent<BatchedMeshes>().meshes = new List<Mesh>(merged);
        }

        /// <summary>Whether a transform is one of these or under one of them.</summary>
        public static bool Under(Transform t, HashSet<Transform> roots)
        {
            for (; t != null; t = t.parent) if (roots.Contains(t)) return true;
            return false;
        }
    }

    /// <summary>A root's statically batched meshes go with it.</summary>
    public class BatchedMeshes : MonoBehaviour
    {
        public List<Mesh> meshes;
        private void OnDestroy() { if (meshes != null) foreach (var m in meshes) if (m != null) Destroy(m); }
    }

    /// <summary>A baked mesh goes with its object.</summary>
    public class BakedMesh : MonoBehaviour
    {
        public Mesh mesh;
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
