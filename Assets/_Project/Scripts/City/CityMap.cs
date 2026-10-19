using System.Collections.Generic;
using BrickMuseum.Build;
using BrickMuseum.Data;
using BrickMuseum.UI;
using BrickMuseum.World;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrickMuseum.City
{
    /// <summary>
    /// The city in the scene: winding streets over hills, pavements, neighbourhood lawns, the creek with its bridges, the
    /// park's footpaths, the slots (coloured pads on little plinths, their kind written on them, grey and numbered while
    /// locked) and the models standing on them. Everything is made on the straight plan and shaped by
    /// <see cref="CityShape"/>. Units are studs, like the models.
    /// </summary>
    public sealed class CityMap
    {
        public const float Lawn = 0.4f;                      // top of the blocks (pavement) above the road
        public const float RoadTop = 0.1f;
        public const float CreekBed = -2.4f, WaterTop = -1.9f;
        public static Vector3 Center => Landscape.CityCenter;
        public readonly Transform root;
        public readonly Townsfolk people;

        private sealed class SlotView
        {
            public Slot slot;
            public GameObject go, pad, model, partial;
            public string partialKey;
            public Renderer padRenderer;
            public TextMeshPro label;
            public BoxCollider hit;
        }

        private readonly Dictionary<Slot, SlotView> views = new Dictionary<Slot, SlotView>();
        private readonly Dictionary<Area, (Renderer lawn, TextMeshPro sign)> areaViews = new Dictionary<Area, (Renderer, TextMeshPro)>();
        private readonly Dictionary<string, Material> padMats = new Dictionary<string, Material>();
        private Material padLocked, padSelected;
        private Slot selected;
        private readonly List<(Transform t, float scale, float time)> pops = new List<(Transform, float, float)>();
        private static readonly Color PavementColour = new Color(0.8f, 0.79f, 0.76f);

        public CityMap(Transform parent)
        {
            root = new GameObject("City").transform;
            root.SetParent(parent, false);
            root.position = Center;
            BuildGround();
            BuildCreek();
            BuildLots();
            BuildFar();
            BuildMega();
            foreach (var a in CityLayout.Areas) BuildArea(a);
            people = new Townsfolk(root);
        }

        // ---------- static scenery ----------

        /// <summary>A mesh made on the plan, bent onto the land.</summary>
        private Renderer Add(string name, MeshBuilder b, Material m, bool shadows = false, float maxEdge = 5f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = CityShape.Bend(b.ToMesh(name), maxEdge);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return r;
        }

        private void BuildGround()
        {
            float x0 = CityLayout.MinX, x1 = CityLayout.MaxX, z0 = CityLayout.MinZ, zShore = CityLayout.ZOf(0) + CityLayout.Block / 2f;
            float cx = CityLayout.CreekX, ch = CityLayout.Creek / 2f;
            // the streets: asphalt everywhere but in the creek valley (the blocks stand on it), bridges over the creek
            var asphalt = new MeshBuilder();
            asphalt.Box(new Vector3((x0 + cx - ch) / 2f, RoadTop / 2f, (z0 + zShore) / 2f), new Vector3(cx - ch - x0, RoadTop, zShore - z0));
            asphalt.Box(new Vector3((cx + ch + x1) / 2f, RoadTop / 2f, (z0 + zShore) / 2f), new Vector3(x1 - cx - ch, RoadTop, zShore - z0));
            foreach (float z in CityLayout.StreetZs())
                asphalt.Box(new Vector3(cx, RoadTop - 0.4f, z), new Vector3(CityLayout.Creek + 0.2f, 0.8f, CityLayout.Road));   // the bridge decks
            Add("Roads", asphalt, Mats.Lit(new Color(0.33f, 0.35f, 0.38f), 0.1f), false, 6f);

            var lines = new MeshBuilder();
            foreach (float x in CityLayout.StreetXs())                         // north-south streets: dashed centre lines
                for (float t = z0 + 3f; t < zShore - 4f; t += 8f)
                    if (!CityLayout.OnStreetZ(t + 2f)) lines.Box(new Vector3(x, RoadTop + 0.02f, t + 2f), new Vector3(0.5f, 0.04f, 4f));
            foreach (float z in CityLayout.StreetZs())                         // east-west streets: centre line and parking lanes
            {
                for (float t = x0 + 3f; t < x1 - 4f; t += 8f) lines.Box(new Vector3(t + 2f, RoadTop + 0.02f, z), new Vector3(4f, 0.04f, 0.5f));
                foreach (float side in new[] { -1f, 1f })
                    lines.Box(new Vector3(0, RoadTop + 0.02f, z + side * (CityLayout.Road / 2f - CityLayout.ParkingLane)), new Vector3(x1 - x0, 0.04f, 0.25f));
            }
            Add("RoadLines", lines, Mats.Lit(new Color(0.95f, 0.93f, 0.85f), 0.1f), false, 3f);

            // the bridges' railings
            var rail = new MeshBuilder();
            foreach (float z in CityLayout.StreetZs())
                foreach (float side in new[] { -1f, 1f })
                {
                    float rz = z + side * (CityLayout.Road / 2f - 0.15f);
                    for (float x = cx - ch; x <= cx + ch; x += 3f) rail.Box(new Vector3(x, RoadTop + 0.5f, rz), new Vector3(0.25f, 1f, 0.25f));
                    rail.Box(new Vector3(cx, RoadTop + 1.0f, rz), new Vector3(CityLayout.Creek, 0.15f, 0.2f));
                }
            // the stone quay along the lake and its railing
            var quay = new MeshBuilder();
            quay.Box(new Vector3(0, Lawn / 2f - 1f, zShore + CityLayout.Quay / 2f), new Vector3(x1 - x0, Lawn + 2f, CityLayout.Quay));
            Add("Quay", quay, Mats.Lit(new Color(0.72f, 0.7f, 0.66f), 0.1f));
            float zr = CityLayout.MaxZ - 0.3f;
            for (float x = x0 + 1f; x < x1; x += 4f) rail.Box(new Vector3(x, Lawn + 0.5f, zr), new Vector3(0.25f, 1f, 0.25f));
            rail.Box(new Vector3(0, Lawn + 1.0f, zr), new Vector3(x1 - x0, 0.15f, 0.2f));
            Add("Railing", rail, Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.4f), true, 4f);
        }

        /// <summary>The creek: grassy banks down to the water, from the forest to the lake (under the quay).</summary>
        private void BuildCreek()
        {
            float cx = CityLayout.CreekX, ch = CityLayout.Creek / 2f, w = CityLayout.CreekWater;
            float z0 = CityLayout.MinZ, z1 = CityLayout.MaxZ + 4f;
            // the valley's cross-section, west to east: grass down to the water's edge, a muddy bed under the water
            var bank = new[] { new Vector2(cx - ch, RoadTop), new Vector2(cx - w - 1.2f, CreekBed + 0.4f) };
            var bankE = new[] { new Vector2(cx + w + 1.2f, CreekBed + 0.4f), new Vector2(cx + ch, RoadTop) };
            var bed = new[] { new Vector2(cx - w - 1.5f, CreekBed + 0.4f), new Vector2(cx - w + 1f, CreekBed - 0.6f), new Vector2(cx + w - 1f, CreekBed - 0.6f), new Vector2(cx + w + 1.5f, CreekBed + 0.4f) };
            AddStrip("CreekBanks", new[] { bank, bankE }, z0, z1, Grass.Lawn(new Color(0.5f, 0.72f, 0.42f)));
            AddStrip("CreekBed", new[] { bed }, z0, z1, Mats.Lit(new Color(0.12f, 0.26f, 0.42f), 0.15f));   // a deep blue bed: the water reads blue
            var water = new MeshBuilder();
            water.Box(new Vector3(cx, WaterTop - 0.05f, (z0 + z1) / 2f), new Vector3(2f * w, 0.1f, z1 - z0));
            Add("CreekWater", water, Water.Creek, false, 6f);
            // stones along the water's edge
            var stones = new MeshBuilder();
            var rnd = new System.Random(7);
            for (float z = z0 + 4f; z < z1 - 6f; z += 7f)
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = cx + side * (w + 0.3f + (float)rnd.NextDouble() * 1.2f), r = 0.5f + (float)rnd.NextDouble() * 0.6f;
                    stones.Cylinder(new Vector3(x, CreekBed + 0.1f, z + (float)rnd.NextDouble() * 4f), r, r * 0.6f, r * 1.1f, 6);
                }
            Add("CreekStones", stones, Mats.Lit(new Color(0.6f, 0.6f, 0.58f), 0.1f), true, 50f);
        }

        private TextMeshPro farState;
        private GameObject farBarrier;

        /// <summary>The far district's streets, the country road out to it, a road sign and (while closed) a barrier.</summary>
        private void BuildFar()
        {
            float x0 = CityLayout.FarMinX, x1 = CityLayout.FarMaxX, z0 = CityLayout.FarNorth, z1 = CityLayout.FarSouth, cz = CityLayout.CountryZ;
            var asphalt = new MeshBuilder();
            asphalt.Box(new Vector3((x0 + x1) / 2f, RoadTop / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, RoadTop, z1 - z0));
            asphalt.Box(new Vector3((CityLayout.MaxX + x0) / 2f, RoadTop / 2f, cz), new Vector3(x0 - CityLayout.MaxX + 0.2f, RoadTop, CityLayout.CountryRoad));
            Add("FarRoads", asphalt, Mats.Lit(new Color(0.33f, 0.35f, 0.38f), 0.1f), false, 6f);
            var lines = new MeshBuilder();
            for (float t = CityLayout.MaxX + 3f; t < x0 - 4f; t += 8f) lines.Box(new Vector3(t + 2f, RoadTop + 0.02f, cz), new Vector3(4f, 0.04f, 0.5f));   // the country road
            foreach (float side in new[] { -1f, 1f })                                                                                                      // and its edge lines
                lines.Box(new Vector3((CityLayout.MaxX + x0) / 2f, RoadTop + 0.02f, cz + side * (CityLayout.CountryRoad / 2f - 0.6f)), new Vector3(x0 - CityLayout.MaxX, 0.04f, 0.25f));
            foreach (float x in CityLayout.FarStreetXs())
                for (float t = z0 + 3f; t < z1 - 4f; t += 8f) lines.Box(new Vector3(x, RoadTop + 0.02f, t + 2f), new Vector3(0.5f, 0.04f, 4f));
            foreach (float z in CityLayout.FarStreetZs())
                for (float t = x0 + 3f; t < x1 - 4f; t += 8f) lines.Box(new Vector3(t + 2f, RoadTop + 0.02f, z), new Vector3(4f, 0.04f, 0.5f));
            Add("FarRoadLines", lines, Mats.Lit(new Color(0.95f, 0.93f, 0.85f), 0.1f), false, 3f);

            // a big green road sign at the town's edge: where the road goes, and whether it is open
            var at = new Vector2(CityLayout.MaxX + 14f, cz - CityLayout.CountryRoad / 2f - 3f);
            var sign = new GameObject("FarSign");
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = CityShape.Point(at, RoadTop);
            sign.transform.localRotation = Quaternion.Euler(0, 90f + CityShape.Angle(at), 0);
            var posts = new MeshBuilder();
            posts.Box(new Vector3(-2.6f, 2f, 0), new Vector3(0.3f, 4f, 0.3f));
            posts.Box(new Vector3(2.6f, 2f, 0), new Vector3(0.3f, 4f, 0.3f));
            var panel = new MeshBuilder();
            panel.Box(new Vector3(0, 3.7f, 0.05f), new Vector3(6.4f, 2.6f, 0.15f));
            AddChild(sign.transform, posts.ToMesh("posts"), Mats.Lit(new Color(0.75f, 0.76f, 0.78f), 0.3f));
            AddChild(sign.transform, panel.ToMesh("panel"), Mats.Lit(new Color(0.1f, 0.5f, 0.28f), 0.3f));
            foreach (float face in new[] { 0f, 180f })
            {
                var name = FlatText(sign.transform, Vector3.zero, CityLayout.FarName + " →", 0.7f, 6f);
                name.transform.localPosition = Quaternion.Euler(0, face, 0) * new Vector3(0, 4.3f, 0.14f);
                name.transform.localRotation = Quaternion.Euler(0, 180f + face, 0);
                name.color = Color.white;
                var state = FlatText(sign.transform, Vector3.zero, "", 0.45f, 6f);
                state.transform.localPosition = Quaternion.Euler(0, face, 0) * new Vector3(0, 3.2f, 0.14f);
                state.transform.localRotation = Quaternion.Euler(0, 180f + face, 0);
                state.color = new Color(1f, 0.95f, 0.6f);
                if (face == 0f) farState = state; else farStateBack = state;
            }
            // a red and white barrier across the road at the district's entrance
            var bar = new MeshBuilder();
            var barRed = new MeshBuilder();
            float bx = x0 - 3f;
            foreach (float side in new[] { -1f, 1f }) bar.Box(new Vector3(bx, 0.6f, cz + side * (CityLayout.CountryRoad / 2f + 0.4f)), new Vector3(0.4f, 1.2f, 0.4f));
            for (int i = 0; i < 6; i++)
                (i % 2 == 0 ? barRed : bar).Box(new Vector3(bx, 1.1f, cz - CityLayout.CountryRoad / 2f + (i + 0.5f) * CityLayout.CountryRoad / 6f), new Vector3(0.25f, 0.35f, CityLayout.CountryRoad / 6f));
            farBarrier = new GameObject("FarBarrier");
            farBarrier.transform.SetParent(root, false);
            var bw = Add("FarBarrierWhite", bar, Mats.Lit(Color.white, 0.3f), true, 3f);
            var br = Add("FarBarrierRed", barRed, Mats.Lit(new Color(0.85f, 0.15f, 0.12f), 0.3f), true, 3f);
            bw.transform.SetParent(farBarrier.transform, true);
            br.transform.SetParent(farBarrier.transform, true);
        }

        private TextMeshPro farStateBack, megaState, megaStateBack;
        private GameObject megaBarrier;

        /// <summary>The highway from the village to Gökkent and Gökkent's streets, a sign, a barrier while it is closed.</summary>
        private void BuildMega()
        {
            float x0 = CityLayout.MegaMinX, x1 = CityLayout.MegaMaxX, z0 = CityLayout.MegaNorth, z1 = CityLayout.MegaSouth, cz = CityLayout.CountryZ;
            float h0 = CityLayout.FarMaxX, hw = CityLayout.Highway;
            var asphalt = new MeshBuilder();
            asphalt.Box(new Vector3((x0 + x1) / 2f, RoadTop / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, RoadTop, z1 - z0));
            asphalt.Box(new Vector3((h0 + x0) / 2f, RoadTop / 2f, cz), new Vector3(x0 - h0 + 0.2f, RoadTop, hw));
            Add("MegaRoads", asphalt, Mats.Lit(new Color(0.3f, 0.32f, 0.35f), 0.1f), false, 6f);
            var lines = new MeshBuilder();
            foreach (float side in new[] { -0.3f, 0.3f })                       // the highway: a double middle line
                lines.Box(new Vector3((h0 + x0) / 2f, RoadTop + 0.02f, cz + side), new Vector3(x0 - h0, 0.04f, 0.2f));
            foreach (float side in new[] { -1f, 1f })                           // its lanes and edges
            {
                for (float t = h0 + 3f; t < x0 - 4f; t += 8f) lines.Box(new Vector3(t + 2f, RoadTop + 0.02f, cz + side * hw / 4f), new Vector3(4f, 0.04f, 0.3f));
                lines.Box(new Vector3((h0 + x0) / 2f, RoadTop + 0.02f, cz + side * (hw / 2f - 0.6f)), new Vector3(x0 - h0, 0.04f, 0.25f));
            }
            foreach (float x in CityLayout.MegaStreetXs())
                for (float t = z0 + 3f; t < z1 - 4f; t += 8f) lines.Box(new Vector3(x, RoadTop + 0.02f, t + 2f), new Vector3(0.5f, 0.04f, 4f));
            foreach (float z in CityLayout.MegaStreetZs())
                for (float t = x0 + 3f; t < x1 - 4f; t += 8f) lines.Box(new Vector3(t + 2f, RoadTop + 0.02f, z), new Vector3(4f, 0.04f, 0.5f));
            Add("MegaRoadLines", lines, Mats.Lit(new Color(0.95f, 0.93f, 0.85f), 0.1f), false, 3f);

            // a big blue motorway sign where the highway leaves the village
            var at = new Vector2(h0 + 16f, cz - hw / 2f - 3f);
            var sign = new GameObject("MegaSign");
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = CityShape.Point(at, RoadTop);
            sign.transform.localRotation = Quaternion.Euler(0, 90f + CityShape.Angle(at), 0);
            var posts = new MeshBuilder();
            posts.Box(new Vector3(-3.4f, 2.5f, 0), new Vector3(0.35f, 5f, 0.35f));
            posts.Box(new Vector3(3.4f, 2.5f, 0), new Vector3(0.35f, 5f, 0.35f));
            var panel = new MeshBuilder();
            panel.Box(new Vector3(0, 4.6f, 0.05f), new Vector3(8.4f, 3f, 0.15f));
            AddChild(sign.transform, posts.ToMesh("posts"), Mats.Lit(new Color(0.75f, 0.76f, 0.78f), 0.3f));
            AddChild(sign.transform, panel.ToMesh("panel"), Mats.Lit(new Color(0.1f, 0.3f, 0.75f), 0.3f));
            foreach (float face in new[] { 0f, 180f })
            {
                var name = FlatText(sign.transform, Vector3.zero, Mega.Name + " →", 0.9f, 8f);
                name.transform.localPosition = Quaternion.Euler(0, face, 0) * new Vector3(0, 5.2f, 0.14f);
                name.transform.localRotation = Quaternion.Euler(0, 180f + face, 0);
                name.color = Color.white;
                var state = FlatText(sign.transform, Vector3.zero, "", 0.5f, 8f);
                state.transform.localPosition = Quaternion.Euler(0, face, 0) * new Vector3(0, 3.9f, 0.14f);
                state.transform.localRotation = Quaternion.Euler(0, 180f + face, 0);
                state.color = new Color(1f, 0.95f, 0.6f);
                if (face == 0f) megaState = state; else megaStateBack = state;
            }
            var bar = new MeshBuilder();
            var barRed = new MeshBuilder();
            float bx = x0 - 3f;
            foreach (float side in new[] { -1f, 1f }) bar.Box(new Vector3(bx, 0.6f, cz + side * (hw / 2f + 0.4f)), new Vector3(0.4f, 1.2f, 0.4f));
            for (int i = 0; i < 8; i++)
                (i % 2 == 0 ? barRed : bar).Box(new Vector3(bx, 1.1f, cz - hw / 2f + (i + 0.5f) * hw / 8f), new Vector3(0.25f, 0.35f, hw / 8f));
            megaBarrier = new GameObject("MegaBarrier");
            megaBarrier.transform.SetParent(root, false);
            Add("MegaBarrierWhite", bar, Mats.Lit(Color.white, 0.3f), true, 3f).transform.SetParent(megaBarrier.transform, true);
            Add("MegaBarrierRed", barRed, Mats.Lit(new Color(0.85f, 0.15f, 0.12f), 0.3f), true, 3f).transform.SetParent(megaBarrier.transform, true);
        }

        /// <summary>The road sign's line and the barrier, for the district's state.</summary>
        public void RefreshDistrict()
        {
            string t = District.IsOpen ? "İmara açık" : "İmara kapalı";
            farState.text = t; farStateBack.text = t;
            farBarrier.SetActive(!District.IsOpen);
            string m = Mega.IsOpen ? "İmara açık" : "İmara kapalı";
            megaState.text = m; megaStateBack.text = m;
            megaBarrier.SetActive(!Mega.IsOpen);
        }

        private readonly TextMeshPro[] lotSigns = new TextMeshPro[CityLayout.LotCount];
        private readonly GameObject[] saleBoards = new GameObject[CityLayout.LotCount];

        /// <summary>Open surfaces along z made from cross-section lines (no end caps), bent onto the land.</summary>
        private void AddStrip(string name, Vector2[][] lines, float z0, float z1, Material m)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var line in lines)
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    int k = verts.Count;
                    verts.Add(new Vector3(line[i].x, line[i].y, z0)); verts.Add(new Vector3(line[i + 1].x, line[i + 1].y, z0));
                    verts.Add(new Vector3(line[i].x, line[i].y, z1)); verts.Add(new Vector3(line[i + 1].x, line[i + 1].y, z1));
                    tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = CityShape.Bend(mesh, 6f);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            m.SetFloat("_Cull", 0f);
        }

        /// <summary>The player's lots: raised blocks with a pavement and their name by the road; a red "for sale" board on those not bought.</summary>
        private void BuildLots()
        {
            var kerb = new MeshBuilder();
            var lawn = new MeshBuilder();
            for (int i = 0; i < CityLayout.LotCount; i++)
            {
                var c = CityLayout.LotCenter(i);
                float size = CityLayout.LotSize(i);
                kerb.Box(new Vector3(c.x, Lawn / 2f - 1f, c.y), new Vector3(size, Lawn + 2f, size - 0.6f));
                lawn.Box(new Vector3(c.x, Lawn + 0.03f, c.y), new Vector3(size - 2f * CityLayout.Pavement, 0.06f, size - 2f * CityLayout.Pavement));
                lotSigns[i] = FlatText(root, c + new Vector2(0, size / 2f - CityLayout.Pavement / 2f), Lawn + 0.08f, "", 1.9f, 22f);
                lotSigns[i].color = new Color(0.25f, 0.27f, 0.3f);
                // the board: two posts and a red panel, by the road
                var board = new MeshBuilder();
                board.Box(new Vector3(-1.6f, 1.6f, 0), new Vector3(0.25f, 3.2f, 0.25f));
                board.Box(new Vector3(1.6f, 1.6f, 0), new Vector3(0.25f, 3.2f, 0.25f));
                var panel = new MeshBuilder();
                panel.Box(new Vector3(0, 2.6f, 0.05f), new Vector3(4f, 1.6f, 0.15f));
                var go = new GameObject("ForSale " + i);
                go.transform.SetParent(root, false);
                bool villa = CityLayout.IsVilla(i);
                var at = c + new Vector2(villa ? -size / 2f + 6f : size / 2f - 6f, 0);
                go.transform.localPosition = CityShape.Point(at, Lawn);
                go.transform.localRotation = Quaternion.Euler(0, (villa ? 270f : 90f) + CityShape.Angle(at), 0);
                AddChild(go.transform, board.ToMesh("posts"), Mats.Lit(Color.white, 0.3f));
                AddChild(go.transform, panel.ToMesh("panel"), Mats.Lit(new Color(0.85f, 0.18f, 0.15f), 0.3f));
                var t = FlatText(go.transform, new Vector3(0, 2.6f, 0.14f), "SATILIK", 0.5f, 3.6f);
                t.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                t.color = Color.white;
                saleBoards[i] = go;
            }
            Add("Pavement Lots", kerb, Mats.Lit(PavementColour, 0.1f));
            Add("Lawn Lots", lawn, Grass.Lawn(new Color(0.58f, 0.8f, 0.5f)));
            RefreshLots();
        }

        private static void AddChild(Transform parent, Mesh mesh, Material m)
        {
            var go = new GameObject("part");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
        }

        /// <summary>Names on the lots bought, prices and boards on the rest.</summary>
        public void RefreshLots()
        {
            for (int i = 0; i < CityLayout.LotCount; i++)
            {
                bool owned = FreeBuild.Lots.Owned(i);
                lotSigns[i].text = owned ? FreeBuild.Lots.Name(i) : FreeBuild.Lots.ForSale(i) ? $"Satılık · {FreeBuild.Lots.Price(i)} lira" : $"Villa arsası · İmara kapalı";
                saleBoards[i].SetActive(!owned);
            }
        }

        private void BuildArea(Area a)
        {
            if (a.creek) { foreach (var s in a.slots) BuildSlot(s); return; }
            var kerb = new MeshBuilder();
            kerb.Box(new Vector3(a.center.x, Lawn / 2f - 1f, a.center.y), new Vector3(CityLayout.Block, Lawn + 2f, CityLayout.Block));   // a kerb on every side, also downhill
            Add("Pavement " + a.name, kerb, Mats.Lit(PavementColour, 0.1f));
            var lawn = new MeshBuilder();
            float inner = CityLayout.Block - 2f * CityLayout.Pavement;   // a pavement all round the block
            lawn.Box(new Vector3(a.center.x, Lawn + 0.03f, a.center.y), new Vector3(inner, 0.06f, inner));
            var r = Add("Lawn " + a.name, lawn, Grass.Lawn(a.ground));
            // winding gravel footpaths (the park)
            if (a.paths.Count > 0)
            {
                var gravel = new MeshBuilder();
                foreach (var path in a.paths) Ribbon(gravel, path, 2.6f, Lawn + 0.07f);
                Add("Paths " + a.name, gravel, Mats.Lit(new Color(0.86f, 0.8f, 0.66f), 0.05f), false, 2.5f);
            }
            // the neighbourhood's name on the pavement along its front street
            var sign = FlatText(root, a.center + new Vector2(0, CityLayout.Block / 2f - CityLayout.Pavement / 2f), Lawn + 0.08f, a.name, 1.9f, 26f);
            sign.color = new Color(0.25f, 0.27f, 0.3f);
            areaViews[a] = (r, sign);
            foreach (var s in a.slots) BuildSlot(s);
        }

        /// <summary>A flat strip along a line of plan points (a footpath), as thin boxes between the points.</summary>
        private static void Ribbon(MeshBuilder b, Vector2[] pts, float width, float y)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i]; var c = pts[i + 1];
                var mid = (a + c) / 2f;
                float len = (c - a).magnitude + width * 0.5f;
                float yaw = Mathf.Atan2(c.x - a.x, c.y - a.y) * Mathf.Rad2Deg;
                var save = b.Xf;
                b.Xf = Matrix4x4.TRS(new Vector3(mid.x, y, mid.y), Quaternion.Euler(0, yaw, 0), Vector3.one);
                b.Box(Vector3.zero, new Vector3(width, 0.04f, len));
                b.Xf = save;
            }
        }

        /// <summary>Text lying on the ground at a plan point, turned with the street.</summary>
        private TextMeshPro FlatText(Transform parent, Vector2 plan, float y, string text, float size, float width)
        {
            var t = FlatText(parent, Vector3.zero, text, size, width);
            t.transform.localPosition = CityShape.Point(plan, y);
            t.transform.localRotation = Quaternion.Euler(0, CityShape.Angle(plan), 0) * Quaternion.Euler(90f, 180f, 0f);
            return t;
        }

        private static TextMeshPro FlatText(Transform parent, Vector3 localPos, string text, float size, float width)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.rotation = Quaternion.Euler(90f, 180f, 0f); // lying face up, reads upright for the camera looking north
            var t = go.AddComponent<TextMeshPro>();
            LocPreprocessor.Attach(t);
            t.font = Fonts.Bold;
            t.text = text;
            t.fontSize = size * 10f;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(width, size * 1.6f);
            t.enableAutoSizing = true; t.fontSizeMin = 1f; t.fontSizeMax = size * 10f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>
        /// A slot stands level on a little plinth (its top at the ground height under its middle, its foot below the
        /// lowest corner); parking spaces lie on the street's slope instead. Turned with the street.
        /// </summary>
        private void BuildSlot(Slot s)
        {
            var v = new SlotView { slot = s };
            v.go = new GameObject("Slot " + s.id);
            v.go.transform.SetParent(root, false);
            v.go.transform.localPosition = SlotPoint(s);
            var turn = Quaternion.Euler(0f, s.yaw + CityShape.Angle(s.local), 0f);
            v.go.transform.localRotation = s.onRoad ? Quaternion.FromToRotation(Vector3.up, CityShape.Normal(s.local)) * turn : turn;
            float drop = s.onRoad || s.only == "bridge" ? 0f : Plinth(s);
            var pad = new MeshBuilder();
            if (s.only == "bridge")
                pad.Box(new Vector3(0, 0.03f, 0), new Vector3(6f, 0.06f, 4f));                       // only a marker on the bridge deck
            else if (s.onRoad)
                pad.Box(new Vector3(0, 0.02f, 0), new Vector3(s.w - 0.4f, 0.04f, s.d - 0.4f));
            else
                pad.Box(new Vector3(0, 0.075f, 0), new Vector3(s.w - 0.4f, 0.15f, s.d - 0.4f));
            if (drop > 0.45f)
            {
                var stone = new MeshBuilder();
                stone.Box(new Vector3(0, -drop / 2f + 0.001f, 0), new Vector3(s.w - 0.4f, drop, s.d - 0.4f));
                var (n, rise, ground) = Stairs(s);
                float z0 = (s.d - 0.4f) / 2f;
                for (int i = 0; i < n; i++)
                {
                    float top = 0.15f - (i + 1) * rise / (n + 1);
                    stone.Box(new Vector3(0, (top + ground - 0.4f) / 2f, z0 + (i + 0.5f) * StepDepth), new Vector3(StairWidth, top - ground + 0.4f, StepDepth));
                }
                var plinth = new GameObject("Plinth");
                plinth.transform.SetParent(v.go.transform, false);
                plinth.AddComponent<MeshFilter>().sharedMesh = stone.ToMesh("plinth");
                var pr = plinth.AddComponent<MeshRenderer>();
                pr.sharedMaterial = stoneMat ??= Mats.Lit(new Color(0.74f, 0.72f, 0.68f), 0.1f);
                pr.shadowCastingMode = ShadowCastingMode.On;
            }
            v.pad = new GameObject("Pad");
            v.pad.transform.SetParent(v.go.transform, false);
            v.pad.AddComponent<MeshFilter>().sharedMesh = pad.ToMesh("pad");
            v.padRenderer = v.pad.AddComponent<MeshRenderer>();
            v.padRenderer.shadowCastingMode = ShadowCastingMode.Off;
            v.label = FlatText(v.go.transform, new Vector3(0, 0.26f, 0), "", Mathf.Min(s.w, s.d, 16f) * 0.2f, Mathf.Min(s.WorldSize.x, 30f) - 1.5f);
            v.hit = v.go.AddComponent<BoxCollider>();
            views[s] = v;
            if (s.kind == "house") RefreshGarden(s);
        }

        /// <summary>Highest and lowest ground under a slot (its middle, corners and edge midpoints).</summary>
        private static (float high, float low) GroundUnder(Slot s)
        {
            float high = float.MinValue, low = float.MaxValue;
            var ws = s.WorldSize / 2f;
            for (int i = -1; i <= 1; i++)
                for (int k = -1; k <= 1; k++)
                {
                    float h = CityShape.Height(s.local + new Vector2(i * ws.x, k * ws.y));
                    high = Mathf.Max(high, h); low = Mathf.Min(low, h);
                }
            return (high, low);
        }

        /// <summary>
        /// The ground height a slot stands at: a level pad on top of the highest ground under it (so it is never half
        /// buried on a slope); parking spaces and the bridge follow the street under their middle.
        /// </summary>
        private static float SlotGround(Slot s) => s.onRoad || s.only == "bridge" ? CityShape.Height(s.local) : GroundUnder(s).high;

        private static Vector3 SlotPoint(Slot s)
        {
            var w = CityShape.Warp(s.local);
            return new Vector3(w.x, SlotGround(s) + Base(s), w.y);
        }

        private const float StepDepth = 0.55f, StairWidth = 3.2f;
        private static Material stoneMat;

        /// <summary>
        /// Steps from a raised plot down to the ground in front of it (toward its street): how many, how high the pad
        /// stands over that ground, and that ground's height in the slot's own frame. None for a pad close to the ground.
        /// </summary>
        private static (int n, float rise, float ground) Stairs(Slot s)
        {
            if (s.onRoad || s.only == "bridge" || s.kind == "tree" || s.w < 6f) return (0, 0f, 0f);
            var f = Quaternion.Euler(0, s.yaw, 0) * Vector3.forward;
            var outside = s.local + new Vector2(f.x, f.z) * (s.d / 2f + StepDepth * 2f);
            float ground = CityShape.Height(outside) - SlotGround(s) + 0.03f;   // the lawn in front, from the pad's foot
            float rise = 0.15f - ground;
            if (rise < 0.45f) return (0, 0f, 0f);
            return (Mathf.CeilToInt(rise / 0.35f), rise, ground);
        }

        /// <summary>The walkable height on a plot's steps (above the bare ground at p), or NaN when p is not on them.</summary>
        private static float OnStairs(Slot s, Vector2 p)
        {
            var (n, rise, ground) = Stairs(s);
            if (n == 0) return float.NaN;
            var f = Quaternion.Euler(0, s.yaw, 0) * Vector3.forward;
            var r = Quaternion.Euler(0, s.yaw, 0) * Vector3.right;
            var d = p - s.local;
            float lz = d.x * f.x + d.y * f.z, lx = d.x * r.x + d.y * r.z, z0 = (s.d - 0.4f) / 2f;
            if (Mathf.Abs(lx) > StairWidth / 2f || lz < z0 || lz > z0 + n * StepDepth) return float.NaN;
            float t = (lz - z0) / (n * StepDepth);
            return SlotGround(s) + Lawn + Mathf.Lerp(0.15f, ground, t) - CityShape.Height(p);
        }

        /// <summary>How far a slot's plinth reaches below its top: down past the lowest ground under it.</summary>
        private static float Plinth(Slot s)
        {
            var (high, low) = GroundUnder(s);
            return high - low + 0.4f;
        }

        // ---------- state ----------

        private Material PadMat(string kind)
        {
            if (padMats.TryGetValue(kind, out var m)) return m;
            Color c;
            switch (kind)
            {
                case "house": c = new Color(0.86f, 0.72f, 0.92f); break;
                case "big": c = new Color(0.72f, 0.7f, 0.94f); break;
                case "sport": c = new Color(0.98f, 0.6f, 0.42f); break;
                case "hotel": c = new Color(0.98f, 0.8f, 0.45f); break;
                case "golf": c = new Color(0.62f, 0.9f, 0.5f); break;
                case "civic": c = new Color(0.62f, 0.8f, 0.94f); break;
                case "historic": c = new Color(0.9f, 0.78f, 0.6f); break;
                case "vehicle": c = new Color(0.42f, 0.45f, 0.5f); break;
                case "tree": c = new Color(0.45f, 0.62f, 0.32f); break;
                case "animal": c = new Color(0.86f, 0.74f, 0.5f); break;
                case "statue": c = new Color(0.88f, 0.86f, 0.8f); break;
                case "landmark": c = new Color(0.96f, 0.84f, 0.45f); break;
                default: c = new Color(0.55f, 0.84f, 0.84f); break;
            }
            return padMats[kind] = Mats.Lit(c, 0.2f);
        }

        /// <summary>Redraws every slot and lawn for the player's level and what stands where.</summary>
        public void RefreshAll()
        {
            int level = Progress.Level;
            foreach (var kv in areaViews)
            {
                var a = kv.Key;
                bool open = District.Opened(a, level);
                kv.Value.lawn.sharedMaterial.SetColor("_BaseColor", open ? a.ground : Color.Lerp(a.ground, new Color(0.6f, 0.6f, 0.6f), 0.75f));
                kv.Value.sign.text = open ? a.name : District.Closed(a) ? $"{a.name}  ·  İmara kapalı" : $"{a.name}  ·  Seviye {a.level}";
            }
            foreach (var v in views.Values) Refresh(v, level);
            RefreshDistrict();
            people.Sync();
        }

        private readonly Dictionary<Slot, FreeBuild.BuildSite> gardens = new Dictionary<Slot, FreeBuild.BuildSite>();

        /// <summary>(Re)builds a house plot's garden on its pad from the saved pieces.</summary>
        public void RefreshGarden(Slot s)
        {
            if (gardens.TryGetValue(s, out var old)) { Object.Destroy(old.root.gameObject); gardens.Remove(s); }
            var pieces = FreeBuild.Gardens.Pieces(s.id, false);
            if (pieces == null || pieces.Count == 0 || !views.TryGetValue(s, out var v)) return;
            int n = FreeBuild.Gardens.Size(s);
            var g = new FreeBuild.BuildSite(v.go.transform, pieces, n, n, Vector3.zero, 0f, 1f, false, null, false);
            g.root.localPosition = new Vector3(-n / 2f, 0.15f, -n / 2f);
            g.root.localRotation = Quaternion.identity;
            gardens[s] = g;
        }

        /// <summary>The model standing on a slot (null when empty).</summary>
        public GameObject ModelOn(Slot s) => views.TryGetValue(s, out var v) ? v.model : null;

        public void Refresh(Slot s) { if (views.TryGetValue(s, out var v)) Refresh(v, Progress.Level); people.Sync(); }

        /// <summary>Height of the bridge model's road deck above its foot (the model's third brick layer), unscaled.</summary>
        private const float BridgeDeck = 9 * Piece.Plate, StudHeight = 0.135f;   // (its studs go just under the asphalt deck)

        private void Refresh(SlotView v, int level)
        {
            var s = v.slot;
            bool open = District.Opened(s, level);
            string id = Progress.At(s.id);
            var m = id != null ? ModelCatalog.Find(id) : null;
            string shown = v.model != null ? v.model.name : null;
            if (m == null && v.model != null) { Object.Destroy(v.model); v.model = null; }
            if (m != null && shown != m.name)
            {
                if (v.model != null) Object.Destroy(v.model);
                v.model = PieceFactory.BuildModel(m);
                v.model.transform.SetParent(v.go.transform, false);
                float k = FitScale(m, s);
                // the big bridge stands in the creek with its deck just under the street's deck; the rest stand on their pads
                v.model.transform.localPosition = new Vector3(0, s.only == "bridge" ? -0.05f - (BridgeDeck + StudHeight) * k : s.onRoad ? 0.04f : 0.15f, 0);
                v.model.transform.localScale = Vector3.one * k;
                if (s.only == "bridge")
                    foreach (Transform c in v.model.transform) if (c.name.Contains(":blue:")) Object.Destroy(c.gameObject);   // the toy's water: the real creek flows here
            }
            // a half-finished build stands on its plot as far as it got
            var site = m == null ? Progress.Site(s.id) : null;
            string want = site != null ? site.model + ":" + site.placed.Count : null;
            if (want != v.partialKey)
            {
                if (v.partial != null) Object.Destroy(v.partial);
                v.partial = null;
                v.partialKey = want;
                var pm = site != null ? ModelCatalog.Find(site.model) : null;
                if (pm != null)
                {
                    v.partial = PieceFactory.BuildModel(pm);
                    var on = new HashSet<int>(site.placed);
                    for (int i = 0; i < v.partial.transform.childCount; i++) v.partial.transform.GetChild(i).gameObject.SetActive(on.Contains(i));
                    v.partial.transform.SetParent(v.go.transform, false);
                    float pk = FitScale(pm, s);
                    v.partial.transform.localPosition = new Vector3(0, s.onRoad ? 0.04f : 0.15f, 0);
                    v.partial.transform.localScale = Vector3.one * pk;
                }
            }
            v.padRenderer.sharedMaterial = s == selected ? (padSelected ??= Mats.Lit(new Color(1f, 0.86f, 0.2f), 0.3f)) : open ? PadMat(s.kind) : (padLocked ??= Mats.Lit(new Color(0.7f, 0.7f, 0.7f), 0.1f));
            v.pad.SetActive(m == null || s.only != "bridge" || s == selected);
            v.label.gameObject.SetActive(m == null && v.partial == null);
            v.label.text = open ? CityLayout.SlotShort(s) : District.Closed(s.area) ? "KAPALI" : $"Sv {s.level}";
            v.label.color = open ? new Color(0.2f, 0.22f, 0.28f, 0.85f) : new Color(0.35f, 0.35f, 0.38f, 0.9f);
            float h = m != null ? Mathf.Max(2f, m.height * FitScale(m, s)) : 2f;
            v.hit.center = new Vector3(0, h / 2f, 0);
            v.hit.size = new Vector3(s.only == "bridge" ? 30f : s.w, h, s.d);
        }

        /// <summary>Largest scale at which the model fits its slot (a little room left round it).</summary>
        public static float SlotFit(ModelDef m, Slot s)
        {
            float margin = s.kind == "vehicle" ? 0.4f : s.kind == "landmark" ? 0.6f : 1.2f;
            return Mathf.Min((s.w - margin) / Mathf.Max(1, m.sx), (s.d - margin) / Mathf.Max(1, m.sz));
        }

        /// <summary>
        /// The scale a model stands at: its real-world size (ModelDef.scale), shrunk only when the slot is too small.
        /// Trees may spread over the pavement and are never shrunk.
        /// </summary>
        public static float FitScale(ModelDef m, Slot s) => s.kind == "tree" ? m.scale : Mathf.Min(m.scale, SlotFit(m, s));

        private static float Base(Slot s) => s.onRoad || s.only == "bridge" ? RoadTop : Lawn;

        public void Select(Slot s)
        {
            var old = selected;
            selected = s;
            if (old != null) Refresh(old);
            if (s != null) Refresh(s);
        }

        public Slot Hit(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 5000f)) return null;
            foreach (var v in views.Values) if (v.hit == hit.collider) return v.slot;
            return null;
        }

        public Vector3 WorldOf(Slot s) => root.TransformPoint(SlotPoint(s));

        /// <summary>A plan point (relative to the city centre) at height y above the ground, in the world.</summary>
        public Vector3 WorldAt(Vector2 plan, float y) => root.TransformPoint(CityShape.Point(plan, y));

        /// <summary>The plan point under a world position.</summary>
        public static Vector2 PlanOf(Vector3 world) => CityShape.ToPlan(new Vector2(world.x - Center.x, world.z - Center.z));

        // ---------- walking and driving ----------

        /// <summary>Height of the walkable surface: streets, bridges, the raised blocks, the slot pads, the creek's banks, the quay.</summary>
        public float GroundAt(Vector3 world)
        {
            var p = PlanOf(world);
            return Center.y + CityShape.Height(p) + SurfaceAt(p);
        }

        /// <summary>The surface's height above the bare ground at a plan point.</summary>
        private float SurfaceAt(Vector2 p)
        {
            float half = CityLayout.Block / 2f;
            if (CityLayout.OnHomeLot(p)) return Lawn + 0.03f;
            if (p.y > CityLayout.ZOf(0) + half) return Lawn;                                     // the quay
            if (CityLayout.InCreek(p))
            {
                if (CityLayout.OnStreetZ(p.y)) return RoadTop;                                  // a bridge
                float d = Mathf.Abs(p.x - CityLayout.CreekX), w = CityLayout.CreekWater + 1.2f, ch = CityLayout.Creek / 2f;
                return d <= w ? WaterTop : Mathf.Lerp(CreekBed + 0.3f, RoadTop, (d - w) / (ch - w));   // wading, or on the bank
            }
            foreach (var a in CityLayout.Areas)
            {
                if (a.creek || Mathf.Abs(p.x - a.center.x) > half || Mathf.Abs(p.y - a.center.y) > half) continue;
                foreach (var s in a.slots)
                {
                    if (s.onRoad) continue;
                    float steps = OnStairs(s, p);
                    if (!float.IsNaN(steps)) return steps;
                    var ws = s.WorldSize;
                    if (Mathf.Abs(p.x - s.local.x) <= ws.x / 2f - 0.2f && Mathf.Abs(p.y - s.local.y) <= ws.y / 2f - 0.2f)
                        return SlotGround(s) - CityShape.Height(p) + Lawn + 0.15f;   // a level pad
                }
                return Lawn + 0.03f;
            }
            return RoadTop;
        }

        /// <summary>Is this world point on a street or a bridge (where a car may drive)?</summary>
        public bool IsRoad(Vector3 world)
        {
            var p = PlanOf(world);
            if (CityLayout.OnCountryRoad(p) || CityLayout.OnHighway(p)) return true;          // the country road and the highway out east
            if (CityLayout.InMega(p)) return Mathf.Abs(SurfaceAt(p) - RoadTop) < 0.01f;         // Gökkent's streets
            if (CityLayout.InFar(p)) return Mathf.Abs(SurfaceAt(p) - RoadTop) < 0.01f;          // the far district's streets
            if (p.x < CityLayout.MinX || p.x > CityLayout.MaxX || p.y < CityLayout.MinZ || p.y > CityLayout.ZOf(0) + CityLayout.Block / 2f) return false;
            if (CityLayout.InCreek(p)) return CityLayout.OnStreetZ(p.y);
            return Mathf.Abs(SurfaceAt(p) - RoadTop) < 0.01f;
        }

        /// <summary>A placed model as a box the walker can't enter (world x/z centre and half size, top height).</summary>
        public struct Obstacle
        {
            public Vector2 center, half;
            public float top;
            public float bottom;      // 0 = standing on the ground; else only in the way at this height and above
            public ModelDef model;
            public float yaw, ground; // which way its front faces (world yaw), the height it stands on
        }

        public List<Obstacle> Obstacles()
        {
            var list = new List<Obstacle>();
            foreach (var v in views.Values)
            {
                string id = Progress.At(v.slot.id);
                var m = id != null ? ModelCatalog.Find(id) : null;
                if (m == null) continue;
                var s = v.slot;
                if (s.only == "bridge") continue;              // you walk and drive over the bridge
                float k = FitScale(m, s);
                var size = new Vector2(m.sx, m.sz) * k;
                if (Mathf.Abs(Mathf.Repeat(s.yaw + CityShape.Angle(s.local), 180f) - 90f) < 45f) size = new Vector2(size.y, size.x);
                var at = WorldOf(s);
                list.Add(new Obstacle
                {
                    center = new Vector2(at.x, at.z), half = size / 2f,
                    top = at.y + 0.15f + m.height * k, model = m, yaw = s.yaw + CityShape.Angle(s.local), ground = at.y + 0.15f,
                });
            }
            return list;
        }

        /// <summary>The new model bounces once when it lands.</summary>
        public void Pop(Slot s)
        {
            if (views.TryGetValue(s, out var v) && v.model != null) pops.Add((v.model.transform, v.model.transform.localScale.x, 0f));
        }

        public void Tick(float dt)
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (t, scale, time) = pops[i];
                if (t == null) { pops.RemoveAt(i); continue; }
                time += dt;
                float k = Mathf.Min(1f, time / 0.5f);
                t.localScale = Vector3.one * scale * (1f + 0.3f * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                if (k >= 1f) { t.localScale = Vector3.one * scale; pops.RemoveAt(i); } else pops[i] = (t, scale, time);
            }
        }
    }

    /// <summary>Bird's-eye camera over the city: one finger pans, pinch / wheel zooms (far enough to see the whole town on a phone).</summary>
    public sealed class CityCam
    {
        public Vector3 target;
        public float distance = 120f, pitch = 55f;
        public const float MinDist = 25f, MaxDist = 1000f;

        public void Focus(Vector3 at, float dist) { target = at; distance = Mathf.Clamp(dist, MinDist, MaxDist); }

        public void Pan(Camera cam, Vector2 deltaPx)
        {
            float k = distance * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            // the camera looks north, so screen right is world -x: the ground follows the finger
            target += new Vector3(deltaPx.x * k, 0f, deltaPx.y * k / Mathf.Sin(pitch * Mathf.Deg2Rad) * 0.85f);
            var c = CityMap.Center;
            target.x = Mathf.Clamp(target.x, c.x + CityLayout.MinX - CityLayout.HomeLot - 20f, c.x + CityLayout.MegaMaxX + 20f);   // (the lots lie west of the town, the village and Gökkent east)
            target.z = Mathf.Clamp(target.z, c.z + CityLayout.MinZ - 20f, c.z + CityLayout.MaxZ + 60f);
        }

        public void Zoom(float factor) => distance = Mathf.Clamp(distance * factor, MinDist, MaxDist);

        public void Apply(Camera cam)
        {
            var rot = Quaternion.Euler(pitch, 180f, 0f); // looking north (-z) from the south
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }
    }
}
