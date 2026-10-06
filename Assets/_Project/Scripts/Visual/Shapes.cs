using UnityEngine;

namespace SquashBot.Visual
{
    public static class Shapes
    {
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
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
