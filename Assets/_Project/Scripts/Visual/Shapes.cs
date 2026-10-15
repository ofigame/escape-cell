using UnityEngine;

namespace SquashBot.Visual
{
    public static class Shapes
    {
        /// <summary>Shapes made while this is on cast real shadows (the fortress floor turns it on; off elsewhere for speed).</summary>
        public static bool CastShadows;

        public static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 scale, Material material)
        {
            return Primitive(PrimitiveType.Cube, name, parent, localPos, scale, material);
        }

        /// <summary>A soft-edged box. The size is baked into the mesh, so the transform keeps scale 1 for animations.</summary>
        public static GameObject Rounded(string name, Transform parent, Vector3 localPos, Vector3 size, float radius, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.RoundedBox(size, radius);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = CastShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static readonly System.Collections.Generic.Dictionary<PrimitiveType, Mesh> meshes = new System.Collections.Generic.Dictionary<PrimitiveType, Mesh>();

        /// <summary>Unity's built-in mesh for a primitive (taken once from a throw-away primitive).</summary>
        public static Mesh MeshOf(PrimitiveType type)
        {
            if (meshes.TryGetValue(type, out var m) && m != null) return m;
            if (type == PrimitiveType.Sphere) { m = IcoSphere(2); meshes[type] = m; return m; }
            var probe = GameObject.CreatePrimitive(type);
            m = probe.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(probe);
            meshes[type] = m;
            return m;
        }

        /// <summary>A round ball of the built-in sphere's size (0.5 across the middle) from a twice-split icosahedron: 320
        /// triangles instead of the built-in 768, the same smooth look at the sizes the game draws it (eyes, leaves, dots).</summary>
        private static Mesh IcoSphere(int splits)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var tris = new System.Collections.Generic.List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int s = 0; s < splits; s++)
            {
                var mids = new System.Collections.Generic.Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                    if (mids.TryGetValue(key, out int i)) return i;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    mids[key] = v.Count - 1;
                    return v.Count - 1;
                }
                var next = new System.Collections.Generic.List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
            var normals = new Vector3[v.Count];
            var uv = new Vector2[v.Count];
            for (int i = 0; i < v.Count; i++)
            {
                normals[i] = v[i];
                uv[i] = new Vector2(0.5f + Mathf.Atan2(v[i].z, v[i].x) / (2f * Mathf.PI), 0.5f + Mathf.Asin(v[i].y) / Mathf.PI);
                v[i] *= 0.5f;
            }
            var mesh = new Mesh { name = "IcoSphere" };
            mesh.SetVertices(v);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>
        /// A primitive shape as a plain mesh object: no collider made (and destroyed) for each of the thousands of pieces
        /// a floor or the map is built from.
        /// </summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = MeshOf(type);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = CastShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
