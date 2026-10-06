using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>Procedural meshes with soft, rounded edges (cached by size), so nothing looks like a raw primitive.</summary>
    public static class MeshFactory
    {
        private static readonly Dictionary<(Vector3, float), Mesh> RoundedBoxes = new Dictionary<(Vector3, float), Mesh>();

        /// <summary>
        /// A box of the given size whose edges and corners are rounded with the given radius.
        /// Built from a subdivided cube whose vertices are pushed onto a rounded shell, giving smooth normals.
        /// </summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int segments = 6)
        {
            radius = Mathf.Min(radius, size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
            var key = (size, radius);
            if (RoundedBoxes.TryGetValue(key, out var cached) && cached != null) return cached;

            var half = size * 0.5f;
            var inner = half - Vector3.one * radius;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float AxisHalf(Vector3 axis) => Mathf.Abs(Vector3.Dot(axis, half));

            void Face(Vector3 normal, Vector3 axisU, Vector3 axisV)
            {
                // Each face is a grid whose rows sit inside the rounded bands at its edges.
                var stepsU = BuildSteps(AxisHalf(axisU), radius, segments);
                var stepsV = BuildSteps(AxisHalf(axisV), radius, segments);
                int start = vertices.Count;
                int nu = stepsU.Length, nv = stepsV.Length;
                for (int j = 0; j < nv; j++)
                {
                    for (int i = 0; i < nu; i++)
                    {
                        // Point on the unit cube face in [-1, 1]
                        var p = normal + axisU * stepsU[i] + axisV * stepsV[j];
                        var onBox = Vector3.Scale(p, half);
                        var clamped = new Vector3(
                            Mathf.Clamp(onBox.x, -inner.x, inner.x),
                            Mathf.Clamp(onBox.y, -inner.y, inner.y),
                            Mathf.Clamp(onBox.z, -inner.z, inner.z));
                        var dir = onBox - clamped;
                        var nrm = dir.sqrMagnitude > 1e-8f ? dir.normalized : normal;
                        vertices.Add(clamped + nrm * radius);
                        normals.Add(nrm);
                        uvs.Add(new Vector2((stepsU[i] + 1f) * 0.5f, (stepsV[j] + 1f) * 0.5f));
                    }
                }

                for (int j = 0; j < nv - 1; j++)
                {
                    for (int i = 0; i < nu - 1; i++)
                    {
                        int a = start + j * nu + i;
                        int b = a + 1;
                        int c = a + nu;
                        int d = c + 1;
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                }
            }

            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.forward, Vector3.right);
            Face(Vector3.right, Vector3.forward, Vector3.up);
            Face(Vector3.left, Vector3.up, Vector3.forward);
            Face(Vector3.forward, Vector3.up, Vector3.right);
            Face(Vector3.back, Vector3.right, Vector3.up);

            var mesh = new Mesh { name = $"RoundedBox {size} r{radius}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            RoundedBoxes[key] = mesh;
            return mesh;
        }

        /// <summary>Grid coordinates in [-1, 1] along one axis: <paramref name="segments"/> rows inside each rounded band.</summary>
        private static float[] BuildSteps(float halfAxis, float radius, int segments)
        {
            float band = halfAxis > 0f ? Mathf.Clamp01(radius / halfAxis) : 1f;
            var list = new List<float>();
            for (int i = 0; i <= segments; i++) list.Add(-1f + band * i / segments);
            for (int i = segments; i >= 0; i--) list.Add(1f - band * i / segments);
            // When the band covers the whole half, the two middles meet: drop the duplicate.
            for (int i = list.Count - 1; i > 0; i--)
                if (list[i] - list[i - 1] < 1e-4f) list.RemoveAt(i);
            return list.ToArray();
        }
    }
}
