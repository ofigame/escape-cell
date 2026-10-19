using System.Collections.Generic;
using BrickMuseum.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrickMuseum.City
{
    /// <summary>
    /// A glowing strip of arrows painted along a route on the streets (it follows the bends and hills), its arrows
    /// running toward the goal; and a big arrow bobbing over the goal (a waiting passenger, a drop-off point).
    /// </summary>
    public sealed class RouteLine
    {
        private readonly Transform cityRoot;
        private readonly GameObject strip, marker;
        private readonly Mesh mesh;
        private readonly Material mat;
        private Vector3 markerAt;
        private static Texture2D chevrons;

        public RouteLine(Transform cityRoot)
        {
            this.cityRoot = cityRoot;
            mesh = new Mesh { name = "route" };
            strip = new GameObject("Route");
            strip.transform.SetParent(cityRoot, false);
            strip.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = strip.AddComponent<MeshRenderer>();
            mat = Mats.UnlitTexture(Chevrons, true);
            mat.SetFloat("_Cull", 0f);                 // seen from above whichever way the strip was wound
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;

            var b = new MeshBuilder();
            b.Cylinder(new Vector3(0, 0, 0), 0.02f, 1.1f, 1.4f, 20);                 // the head, its tip down
            b.Cylinder(new Vector3(0, 1.4f, 0), 0.42f, 0.42f, 1.5f, 16);            // the shaft
            marker = new GameObject("GoalArrow");
            marker.transform.SetParent(cityRoot, false);
            marker.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("arrow");
            var mr = marker.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Unlit(new Color(1f, 0.82f, 0.1f));
            mr.shadowCastingMode = ShadowCastingMode.Off;
            Hide();
        }

        /// <summary>Yellow chevrons on a faint band, pointing along +v (the way to go).</summary>
        private static Texture2D Chevrons
        {
            get
            {
                if (chevrons != null) return chevrons;
                const int s = 64;
                chevrons = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "chevrons" };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                        float arm = Mathf.Abs(u - 0.5f);                                   // distance from the middle
                        float d = Mathf.Abs(v - (0.62f - arm * 0.9f));                    // a V pointing to +v
                        bool chevron = d < 0.11f && arm < 0.42f;
                        float edge = Mathf.Clamp01((0.5f - arm) * 12f);
                        px[y * s + x] = chevron ? new Color(1f, 0.85f, 0.15f, 0.95f * edge) : new Color(0.25f, 0.85f, 1f, 0.32f * edge);
                    }
                chevrons.SetPixels32(px);
                chevrons.Apply();
                return chevrons;
            }
        }

        /// <summary>Lays the strip along a plan route (densified so it follows the land), 1.6 wide.</summary>
        public void Show(List<Vector2> route)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < route.Count; i++)
            {
                float len = Vector2.Distance(route[i], route[i + 1]);
                int n = Mathf.Max(1, Mathf.CeilToInt(len / 1.5f));
                for (int k = 0; k < n; k++) pts.Add(Vector2.Lerp(route[i], route[i + 1], k / (float)n));
            }
            pts.Add(route[route.Count - 1]);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float along = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                var dir = (i + 1 < pts.Count ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                var side = new Vector2(-dir.y, dir.x) * 0.8f;
                if (i > 0) along += Vector2.Distance(pts[i - 1], pts[i]);
                verts.Add(CityShape.Point(pts[i] - side, CityMap.RoadTop + 0.08f));
                verts.Add(CityShape.Point(pts[i] + side, CityMap.RoadTop + 0.08f));
                uvs.Add(new Vector2(0, along / 1.6f)); uvs.Add(new Vector2(1, along / 1.6f));
                if (i > 0)
                {
                    int a = verts.Count - 4;
                    tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            strip.SetActive(true);
        }

        /// <summary>The bobbing arrow over a plan point (height above the ground there).</summary>
        public void Mark(Vector2 plan, float groundY)
        {
            markerAt = new Vector3(CityShape.Warp(plan).x, groundY, CityShape.Warp(plan).y);
            marker.SetActive(true);
        }

        public void Hide() { strip.SetActive(false); marker.SetActive(false); }

        public string Debug => $"marker {(marker.activeSelf ? "on" : "off")} at {marker.transform.position}";

        public void Tick(float time)
        {
            mat.SetTextureOffset("_BaseMap", new Vector2(0, -time * 1.4f));
            if (marker.activeSelf)
            {
                marker.transform.localPosition = markerAt + Vector3.up * (3.2f + 0.5f * Mathf.Sin(time * 3.2f));
                marker.transform.localRotation = Quaternion.Euler(0, time * 90f, 0);
            }
        }
    }
}
