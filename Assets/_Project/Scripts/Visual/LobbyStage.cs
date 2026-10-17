using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SquashBot.Visual
{
    /// <summary>
    /// The main menu's showroom: the robot (in its current outfit) on a raised star dial, a teal beam of light pouring
    /// down on it from above with lightning flickering inside, a great star wheel (gold rings, constellations) spread
    /// over the floor round it, planets hanging in the violet space behind, sparks drifting up. It has its own camera
    /// and only draws while the menu is open, so the menu looks like a polished lobby rather than a paused level.
    /// </summary>
    public class LobbyStage : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(6000f, 0f, -6000f);

        public Camera Cam { get; private set; }

        private Transform robotHolder;
        private Func<Transform, GameObject> robotLook;
        private readonly List<Transform> sparkList = new List<Transform>();
        private readonly List<GameObject> bolts = new List<GameObject>();
        private Material beamMat, coreMat, haloMat;
        private Color beamColour, coreColour, haloColour;
        private float time, boltClock;
        private int boltOn = -1;
        private readonly System.Random flicker = new System.Random(3);

        private static readonly Color Teal = new Color(0.3f, 0.95f, 0.9f);

        public static LobbyStage Create(Func<Transform, GameObject> robotLook)
        {
            var go = new GameObject("LobbyStage");
            go.transform.position = Origin;
            var s = go.AddComponent<LobbyStage>();
            s.robotLook = robotLook;
            s.Build();
            go.SetActive(false);
            return s;
        }

        private void Build()
        {
            var camGo = new GameObject("LobbyCamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.fieldOfView = 34f;
            Cam.nearClipPlane = 0.3f;
            Cam.farClipPlane = 60f;
            Cam.depth = 4f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = Application.isMobilePlatform ? AntialiasingMode.None : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.localPosition = new Vector3(0f, 3.0f, -13f);
            camGo.transform.LookAt(Origin + new Vector3(0f, 0.57f, 0f)); // low enough to see the sky and the beam; foi stays under the rank card, above the level card

            var gold = MaterialFactory.Create(new Color(1f, 0.78f, 0.36f), new Color(0.55f, 0.38f, 0.12f));
            var goldBright = MaterialFactory.Create(new Color(1f, 0.85f, 0.45f), new Color(1.4f, 1f, 0.4f));
            var lines = MaterialFactory.Create(new Color(0.6f, 0.95f, 1f), new Color(0.9f, 1.8f, 2.2f));
            var starGold = MaterialFactory.Create(new Color(1f, 0.85f, 0.45f), new Color(2.4f, 1.8f, 0.7f));
            var starWhite = MaterialFactory.Create(Color.white, new Color(1.8f, 2f, 2.2f));
            var cyanGlow = MaterialFactory.Create(new Color(0.4f, 1f, 0.95f), new Color(0.6f, 2.4f, 2.3f));

            // Deep violet space behind everything, a teal haze where the beam comes from, planets hanging in it.
            Shapes.Primitive(PrimitiveType.Quad, "Galaxy", transform, new Vector3(0f, 6f, 34f), new Vector3(46f, 100f, 1f), MaterialFactory.CreateUnlit(SquashBot.UI.UiSprites.Galaxy.texture));
            var soft = SoftDisc();
            var haze = MaterialFactory.CreateTransparent(new Color(0.2f, 0.8f, 0.8f, 0.22f), new Color(0.1f, 0.5f, 0.5f));
            haze.SetTexture("_BaseMap", soft);
            Shapes.Primitive(PrimitiveType.Quad, "Haze", transform, new Vector3(0f, 6f, 24f), new Vector3(18f, 14f, 1f), haze);
            var violet = MaterialFactory.CreateTransparent(new Color(0.55f, 0.25f, 0.85f, 0.35f), new Color(0.3f, 0.1f, 0.45f));
            violet.SetTexture("_BaseMap", soft);
            Shapes.Primitive(PrimitiveType.Quad, "Nebula", transform, new Vector3(-9f, 5f, 26f), new Vector3(20f, 14f, 1f), violet);
            Shapes.Primitive(PrimitiveType.Quad, "Nebula", transform, new Vector3(10f, 2.5f, 25f), new Vector3(18f, 12f, 1f), violet);

            // The great star wheel spread over the floor: gold rings, sector lines and a constellation in each field.
            var floor = MaterialFactory.Create(new Color(0.07f, 0.06f, 0.18f), new Color(0.025f, 0.02f, 0.07f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Floor", transform, new Vector3(0f, -0.42f, 0f), new Vector3(60f, 0.02f, 60f), floor);
            var wheel = new GameObject("Wheel").transform;
            wheel.SetParent(transform, false);
            wheel.localPosition = new Vector3(0f, -0.395f, 0f);
            // Faint, half see-through lines: foi and its platform stay in front.
            var wheelGold = MaterialFactory.CreateTransparent(new Color(0.95f, 0.78f, 0.45f, 0.22f), new Color(0.16f, 0.11f, 0.05f));
            var dimStar = MaterialFactory.CreateTransparent(new Color(1f, 0.88f, 0.6f, 0.3f), new Color(0.3f, 0.22f, 0.1f));
            var dimStarB = MaterialFactory.CreateTransparent(new Color(0.85f, 0.95f, 1f, 0.28f), new Color(0.2f, 0.26f, 0.3f));
            var dimLine = MaterialFactory.CreateTransparent(new Color(0.6f, 0.9f, 1f, 0.18f), new Color(0.08f, 0.16f, 0.2f));
            FlatRing(wheel, 2.75f, 0.07f, cyanGlow, 0f, 72, 0.01f);
            FlatRing(wheel, 4.0f, 0.07f, wheelGold, 0f, 96, 0.01f);
            FlatRing(wheel, 4.28f, 0.035f, wheelGold, 0f, 96, 0.01f);
            FlatRing(wheel, 7.0f, 0.07f, wheelGold, 0f, 120, 0.01f);
            FlatRing(wheel, 7.32f, 0.035f, wheelGold, 0f, 120, 0.01f);
            FlatRing(wheel, 11.2f, 0.09f, wheelGold, 0f, 160, 0.01f);
            FlatRing(wheel, 11.6f, 0.04f, wheelGold, 0f, 160, 0.01f);
            FlatRing(wheel, 16f, 0.08f, wheelGold, 0f, 180, 0.01f);
            for (int i = 0; i < 72; i++)
            {
                float a = i * Mathf.PI * 2f / 72f;
                float len = i % 6 == 0 ? 0.3f : 0.14f;
                Line(wheel, Dir(a) * (4.0f - len), Dir(a) * 4.0f, 0.03f, wheelGold);
            }
            var rng = new System.Random(7);
            for (int s = 0; s < 12; s++)
            {
                float a = s * Mathf.PI * 2f / 12f;
                Line(wheel, Dir(a) * 4.28f, Dir(a) * 7.0f, 0.04f, wheelGold);
                Line(wheel, Dir(a) * 7.32f, Dir(a) * 11.2f, 0.05f, wheelGold);
                Line(wheel, Dir(a + Mathf.PI / 12f) * 11.6f, Dir(a + Mathf.PI / 12f) * 16f, 0.06f, wheelGold);
                Constellation(wheel, Dir(a + Mathf.PI / 12f) * 5.65f, 0.95f, rng, dimStar, dimStarB, dimLine);
                Constellation(wheel, Dir(a + Mathf.PI / 12f) * 9.2f, 1.6f, rng, dimStar, dimStarB, dimLine);
                Constellation(wheel, Dir(a) * 13.8f, 2.3f, rng, dimStar, dimStarB, dimLine);
            }
            var floorGlow = MaterialFactory.CreateTransparent(new Color(0.3f, 1f, 0.95f, 0.3f), new Color(0.15f, 0.6f, 0.55f));
            floorGlow.SetTexture("_BaseMap", soft);
            var pool = Shapes.Primitive(PrimitiveType.Quad, "FloorGlow", transform, new Vector3(0f, -0.38f, 0f), new Vector3(6.5f, 6.5f, 1f), floorGlow).transform;
            pool.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // The star dial foi stands on: a violet disc in gold rings with a glowing teal rim, spokes dividing it into
            // eight fields, a softly glowing centre, and a faint constellation drawn in each field.
            var dial = new GameObject("Dial").transform;
            dial.SetParent(transform, false);
            dial.localScale = new Vector3(0.56f, 1f, 0.56f); // under foi, whole in the view
            var plum = MaterialFactory.Create(new Color(0.17f, 0.1f, 0.32f), new Color(0.05f, 0.025f, 0.1f));
            var deep = MaterialFactory.Create(new Color(0.08f, 0.05f, 0.18f), Color.black);
            Shapes.Primitive(PrimitiveType.Cylinder, "Base", dial, new Vector3(0f, -0.2f, 0f), new Vector3(7.4f, 0.2f, 7.4f), deep);
            Shapes.Primitive(PrimitiveType.Cylinder, "RimWall", dial, new Vector3(0f, -0.08f, 0f), new Vector3(7.3f, 0.12f, 7.3f), cyanGlow);
            Shapes.Primitive(PrimitiveType.Cylinder, "RimOuter", dial, new Vector3(0f, 0.02f, 0f), new Vector3(7.2f, 0.05f, 7.2f), gold);
            Shapes.Primitive(PrimitiveType.Cylinder, "Dial", dial, new Vector3(0f, 0.05f, 0f), new Vector3(6.9f, 0.05f, 6.9f), plum);
            Shapes.Primitive(PrimitiveType.Cylinder, "RimInner", dial, new Vector3(0f, 0.075f, 0f), new Vector3(6.5f, 0.012f, 6.5f), gold);
            Shapes.Primitive(PrimitiveType.Cylinder, "DialInner", dial, new Vector3(0f, 0.08f, 0f), new Vector3(6.36f, 0.012f, 6.36f), plum);
            var core = MaterialFactory.Create(new Color(0.15f, 0.55f, 0.6f), new Color(0.2f, 0.9f, 0.95f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Core", dial, new Vector3(0f, 0.09f, 0f), new Vector3(2.1f, 0.012f, 2.1f), core);
            Shapes.Primitive(PrimitiveType.Cylinder, "CoreRim", dial, new Vector3(0f, 0.086f, 0f), new Vector3(2.3f, 0.012f, 2.3f), gold);
            var studs = MaterialFactory.Create(new Color(1f, 0.85f, 0.5f), new Color(0.9f, 0.65f, 0.25f));
            for (int i = 0; i < 32; i++)
            {
                float ang = (i + 0.5f) * Mathf.PI * 2f / 32f;
                Shapes.Primitive(PrimitiveType.Sphere, "Stud", dial, new Vector3(Mathf.Sin(ang) * 3.58f, 0.03f, Mathf.Cos(ang) * 3.58f), new Vector3(0.12f, 0.08f, 0.12f), studs);
            }
            // The medallion: rings round an eight-pointed gold star that glows.
            Shapes.Primitive(PrimitiveType.Cylinder, "MedalRing", dial, new Vector3(0f, 0.095f, 0f), new Vector3(1.5f, 0.01f, 1.5f), gold);
            Shapes.Primitive(PrimitiveType.Cylinder, "Medal", dial, new Vector3(0f, 0.1f, 0f), new Vector3(1.36f, 0.01f, 1.36f), core);
            for (int i = 0; i < 4; i++)
            {
                var ray = Shapes.Primitive(PrimitiveType.Cube, "Ray", dial, new Vector3(0f, 0.105f, 0f), new Vector3(0.09f, 0.01f, i % 2 == 0 ? 1.25f : 0.8f), starGold).transform;
                ray.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
            }
            // Tick marks round the rim, spokes between the fields.
            for (int i = 0; i < 48; i++)
            {
                float ang = i * Mathf.PI * 2f / 48f;
                var tick = Shapes.Primitive(PrimitiveType.Cube, "Tick", dial, new Vector3(Mathf.Sin(ang) * 3.33f, 0.085f, Mathf.Cos(ang) * 3.33f), new Vector3(0.025f, 0.01f, i % 6 == 0 ? 0.16f : 0.08f), gold).transform;
                tick.localRotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
            }
            for (int s = 0; s < 8; s++)
            {
                float ang = (s + 0.5f) * Mathf.PI * 2f / 8f;
                var spoke = Shapes.Primitive(PrimitiveType.Cube, "Spoke", dial, new Vector3(Mathf.Sin(ang) * 2.15f, 0.09f, Mathf.Cos(ang) * 2.15f), new Vector3(0.03f, 0.01f, 2.1f), gold).transform;
                spoke.localRotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
                float mid = s * Mathf.PI * 2f / 8f;
                Constellation(dial, new Vector3(Mathf.Sin(mid) * 2.15f, 0.095f, Mathf.Cos(mid) * 2.15f), 0.9f, rng, starGold, starWhite, lines, 0.03f);
            }

            // The beam of light pouring down on foi from a glowing ring overhead (only its far half: foi stands in front of it).
            var beamTex = BeamTexture();
            beamColour = new Color(0.35f, 1f, 0.95f, 0.62f);
            beamMat = MaterialFactory.CreateTransparent(beamColour, new Color(0.25f, 0.85f, 0.8f));
            beamMat.SetTexture("_BaseMap", beamTex);
            coreColour = new Color(0.75f, 1f, 1f, 0.7f);
            coreMat = MaterialFactory.CreateTransparent(coreColour, new Color(0.5f, 1.1f, 1.1f));
            coreMat.SetTexture("_BaseMap", beamTex);
            HalfTube("Beam", 0.95f, 1.55f, 0.1f, 8.5f, beamMat);
            HalfTube("BeamCore", 0.45f, 0.8f, 0.1f, 8.5f, coreMat);
            var portal = new GameObject("Portal").transform;
            portal.SetParent(transform, false);
            portal.localPosition = new Vector3(0f, 5.8f, 0.3f);
            FlatRing(portal, 1.4f, 0.16f, cyanGlow, 0f, 64, 0.06f);
            FlatRing(portal, 1.75f, 0.05f, goldBright, 0f, 64, 0.04f);
            haloColour = new Color(0.4f, 1f, 0.95f, 0.55f);
            haloMat = MaterialFactory.CreateTransparent(haloColour, new Color(0.4f, 1.4f, 1.35f));
            haloMat.SetTexture("_BaseMap", soft);
            var halo = Shapes.Primitive(PrimitiveType.Quad, "PortalGlow", portal, new Vector3(0f, -0.05f, 0f), new Vector3(4.6f, 4.6f, 1f), haloMat).transform;
            halo.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Lightning inside it: a few jagged bolts, one flashing at a time.
            var boltMat = MaterialFactory.Create(new Color(0.85f, 1f, 1f), new Color(2.2f, 3.4f, 3.4f));
            for (int b = 0; b < 5; b++)
            {
                var bolt = new GameObject("Bolt").transform;
                bolt.SetParent(transform, false);
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * 1.4f, 6.2f, 0.75f + (float)rng.NextDouble() * 0.5f);
                while (p.y > 1.2f)
                {
                    var q = p + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.7f, -0.45f - (float)rng.NextDouble() * 0.4f, 0f);
                    q.x = Mathf.Clamp(q.x, -1.1f, 1.1f);
                    Line(bolt, p, q, 0.035f, boltMat);
                    if (rng.NextDouble() < 0.3) Line(bolt, q, q + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.8f, -0.35f, 0f), 0.022f, boltMat);
                    p = q;
                }
                bolt.gameObject.SetActive(false);
                bolts.Add(bolt.gameObject);
            }

            StaticMerge.Combine(dial.gameObject, _ => false);
            StaticMerge.Combine(wheel.gameObject, _ => false);

            // Sparks drifting up through the light.
            var sparks = new GameObject("Sparks").transform;
            sparks.SetParent(transform, false);
            var spark = MaterialFactory.Create(Color.white, new Color(1.4f, 2f, 2f));
            for (int i = 0; i < 22; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 9f - 4.5f, (float)rng.NextDouble() * 6f - 0.5f, (float)rng.NextDouble() * 6f - 1f);
                var sp = Shapes.Primitive(PrimitiveType.Cube, "Spark", sparks, p, Vector3.one * (0.03f + (float)rng.NextDouble() * 0.04f), spark).transform;
                sparkList.Add(sp);
            }

            robotHolder = new GameObject("Robot").transform;
            robotHolder.SetParent(transform, false);
            robotHolder.localPosition = new Vector3(0f, 0.16f, 0f);
            robotHolder.localScale = Vector3.one * 1.8f;
        }

        private static Vector3 Dir(float a) => new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));

        /// <summary>A thin straight bar from a to b (constellation lines, wheel spokes, lightning).</summary>
        private static void Line(Transform parent, Vector3 a, Vector3 b, float width, Material mat)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var t = Shapes.Primitive(PrimitiveType.Cube, "Line", parent, a + d * 0.5f, new Vector3(width, width * 0.3f + 0.004f, d.magnitude + width * 0.5f), mat).transform;
            t.localRotation = Quaternion.LookRotation(d);
        }

        /// <summary>A flat ring of short segments lying in the parent's plane.</summary>
        private static void FlatRing(Transform parent, float radius, float width, Material mat, float y, int parts, float thick)
        {
            if (parts <= 0 || mat == null) return;
            float seg = 2f * Mathf.PI * radius / parts * 1.06f;
            for (int i = 0; i < parts; i++)
            {
                float a = i * Mathf.PI * 2f / parts;
                var p = Shapes.Primitive(PrimitiveType.Cube, "Arc", parent, new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius), new Vector3(width, thick, seg), mat).transform;
                p.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg + 90f, 0f);
            }
        }

        /// <summary>A few stars joined by thin lines, scattered round a point on the floor.</summary>
        private static void Constellation(Transform parent, Vector3 centre, float spread, System.Random rng, Material starA, Material starB, Material line, float width = 0f)
        {
            float k = spread / 0.9f;
            if (width <= 0f) width = 0.028f * k;
            var stars = new List<Vector3>();
            int count = 4 + rng.Next(3);
            for (int i = 0; i < count; i++)
                stars.Add(centre + new Vector3(((float)rng.NextDouble() - 0.5f) * spread, 0f, ((float)rng.NextDouble() - 0.5f) * spread));
            for (int i = 0; i < stars.Count; i++)
            {
                float s = (i == 0 ? 0.15f : 0.1f) * k;
                Shapes.Primitive(PrimitiveType.Sphere, "Star", parent, stars[i] + Vector3.up * 0.005f, new Vector3(s, 0.03f, s), i % 2 == 0 ? starA : starB);
                if (i > 0) Line(parent, stars[i - 1], stars[i], width, line);
            }
        }

        /// <summary>The far half of an open tube, wider at the bottom, with its own UVs (u round it, v up it).</summary>
        private void HalfTube(string name, float rTop, float rBottom, float y0, float y1, Material mat)
        {
            const int Sides = 24;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= Sides; i++)
            {
                float a = Mathf.PI * i / Sides; // 0..pi: the half on +Z, away from the camera
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(dir * rBottom + Vector3.up * y0); n.Add(-dir); uv.Add(new Vector2(i / (float)Sides, 0f));
                v.Add(dir * rTop + Vector3.up * y1); n.Add(-dir); uv.Add(new Vector2(i / (float)Sides, 1f));
            }
            for (int i = 0; i < Sides; i++)
            {
                int k = i * 2;
                t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k + 1); t.Add(k + 3); t.Add(k + 2);
                t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k + 1); t.Add(k + 2); t.Add(k + 3);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Beam alpha: dense down the middle, soft at the sides, fading out toward the top.</summary>
        private static Texture2D BeamTexture()
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, h = (y + 0.5f) / S;
                    float side = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.9f);
                    float up = Mathf.Lerp(1f, 0.45f, h) * Mathf.SmoothStep(0f, 1f, Mathf.Min(h * 12f, 1f));
                    float streak = 0.82f + 0.18f * Mathf.Sin(u * 40f + Mathf.Sin(u * 13f) * 2f);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(side * up * streak) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>A soft round blot (glows, haze): white, alpha falling off from the centre.</summary>
        private static Texture2D SoftDisc()
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Shows the stage with the robot's current look, tinted for the world the player is in.</summary>
        public void Open(WorldTheme theme)
        {
            gameObject.SetActive(true);
            Cam.fieldOfView = CameraFit.Fov(34f, Cam.aspect);
            for (int i = robotHolder.childCount - 1; i >= 0; i--) Destroy(robotHolder.GetChild(i).gameObject);
            robotLook?.Invoke(robotHolder);
            Cam.backgroundColor = new Color(0.04f, 0.03f, 0.1f);
        }

        public void Close() => gameObject.SetActive(false);

        public bool IsOpen => gameObject.activeSelf;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            time += dt;
            // The robot turns a little this way and that, as if showing off, with a slow breath.
            robotHolder.localRotation = Quaternion.Euler(0f, 180f + Mathf.Sin(time * 0.5f) * 28f, 0f); // the model faces +Z, the camera is on -Z
            robotHolder.localPosition = new Vector3(0f, 0.16f + Mathf.Abs(Mathf.Sin(time * 1.6f)) * 0.03f, 0f);
            // The light breathes; now and then a bolt of lightning flashes through it.
            float pulse = 0.85f + 0.15f * Mathf.Sin(time * 2.1f);
            boltClock -= dt;
            if (boltClock <= 0f)
            {
                if (boltOn >= 0) bolts[boltOn].SetActive(false);
                bool flash = boltOn < 0 && flicker.NextDouble() < 0.7;
                boltOn = flash ? flicker.Next(bolts.Count) : -1;
                if (boltOn >= 0) bolts[boltOn].SetActive(true);
                boltClock = flash ? 0.07f + (float)flicker.NextDouble() * 0.1f : 0.15f + (float)flicker.NextDouble() * 0.9f;
            }
            float boost = boltOn >= 0 ? 1.25f : 1f;
            SetAlpha(beamMat, beamColour, pulse * boost);
            SetAlpha(coreMat, coreColour, pulse * boost);
            SetAlpha(haloMat, haloColour, pulse * boost);
            foreach (var s in sparkList)
            {
                var p = s.localPosition;
                p.y += dt * 0.35f;
                if (p.y > 5.5f) p.y = -0.4f;
                s.localPosition = p;
                s.localRotation = Quaternion.Euler(time * 40f, time * 60f, 0f);
            }
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static void SetAlpha(Material m, Color c, float k) => m.SetColor(BaseColorId, new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * k)));
    }
}
