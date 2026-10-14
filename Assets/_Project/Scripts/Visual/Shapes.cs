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
            var probe = GameObject.CreatePrimitive(type);
            m = probe.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(probe);
            meshes[type] = m;
            return m;
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
