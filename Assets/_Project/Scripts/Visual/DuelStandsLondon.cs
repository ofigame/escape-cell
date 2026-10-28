using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The Big Ben stage (Parliament Square, as the player drew it from above): the fight on the mown lawn, the blue fans
    /// (left) and the red (right) just behind it; across the lawn's far railing a road with a red bus, and beyond it the
    /// Palace of Westminster with Big Ben at its left end; to the left a pavement with lamps, a road with another bus.
    /// </summary>
    public partial class DuelStands
    {
        /// <summary>A landmark the duel camera keeps inside as it slides (Big Ben's left side to the palace's right end): its
        /// front (x) and its two ends (z).</summary>
        public bool HasLandmark { get; private set; }
        public float LandmarkX { get; private set; }
        public float LandmarkLeft { get; private set; }
        public float LandmarkRight { get; private set; }

        private static readonly Look LondonLook = new Look
        {
            pebbles = new Color(0.5f, 0.5f, 0.5f),
            folder = "Duel/London/london_", baseFolder = "Duel/Euro/euro_", sky = "Duel/London/london_sky", cover = 1.22f, street = false, city = "london", rim = 1.03f, standRise = 0.6f, paving = "Duel/London/london_ground", crystals = false, backTree = false,
            thick = 1f,
            glow = new Color(1f, 0.9f, 0.7f), selfLight = new Color(0.3f, 0.3f, 0.3f),
            ground = new Color(0.32f, 0.5f, 0.22f), mound = new Color(0.36f, 0.55f, 0.25f), bank = new Color(0.3f, 0.46f, 0.2f), grassColour = Color.white,
            crystal = Color.white, mist = new Color(0.86f, 0.88f, 0.9f, 0.06f), lamp = new Color(1f, 0.9f, 0.75f), lampPower = 0.4f,
            haze = new Color(0.8f, 0.82f, 0.85f, 0f), skyTop = new Color(0.58f, 0.64f, 0.72f), skyLow = new Color(0.85f, 0.87f, 0.89f), // (London's grey sky)
            sun = new Color(0.96f, 0.96f, 0.98f), sunPower = 1.05f, ambSky = new Color(0.74f, 0.77f, 0.82f), ambHorizon = new Color(0.72f, 0.73f, 0.74f), ambGround = new Color(0.32f, 0.34f, 0.3f),
        };

        /// <summary>The palace's width (Big Ben at its left end rises about 0.6 of it) and how far its middle is behind the
        /// floor's room (far: the low fight camera sees Big Ben whole); the roads' width; the pavement's.</summary>
        private const float PalaceWide = 52f, PalaceSpan = 58f, LondonBack = 100f, RoadWide = 14f, PavementWide = 5f;

        private void BigBenSquare(Transform p, Look look, float ground, float platR, int southTeam, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            crowdSize = MadeCrowd;
            float k = crowdSize;
            var asphalt = Matte(new Color(0.22f, 0.22f, 0.24f), 0.12f);
            var line = Matte(new Color(0.92f, 0.92f, 0.88f), 0.3f);
            var slab = Matte(new Color(0.7f, 0.69f, 0.66f), 0.15f);
            var rail = Matte(new Color(0.3f, 0.38f, 0.32f), 0.12f);

            // the palace with Big Ben at its left end (its long side across the view)
            var palace = MadeModel(p, look, "palace", false);
            var pb = BoundsOf(palace);
            if (pb.size.x > pb.size.z) palace.rotation = Quaternion.Euler(0f, 90f, 0f) * palace.rotation;
            pb = BoundsOf(palace);
            palace.localScale = Vector3.one * (PalaceWide / Mathf.Max(0.01f, pb.size.z));
            pb = BoundsOf(palace);
            if (HighEnd(palace, pb) > 0f) palace.rotation = Quaternion.Euler(0f, 180f, 0f) * palace.rotation; // (Big Ben to the left)
            pb = BoundsOf(palace);
            // as wide as the fight camera's view out there (its height kept): Big Ben at the view's left edge, the palace to its right one
            palace.localScale = new Vector3(palace.localScale.x, palace.localScale.y, palace.localScale.z * PalaceSpan / Mathf.Max(0.01f, pb.size.z));
            pb = BoundsOf(palace);
            float px = centre.x - platR - LondonBack;
            palace.position += new Vector3(px - pb.center.x, ground - pb.min.y, centre.z - pb.center.z);
            pb = BoundsOf(palace);
            float benZ = pb.min.z + pb.size.z * 0.07f;
            HasLandmark = true; LandmarkX = pb.max.x; LandmarkLeft = pb.min.z; LandmarkRight = pb.max.z; // (the duel camera keeps inside it)

            // the road in front of it, a red bus on it to the right of Big Ben
            float roadNear = pb.max.x + 2f + RoadWide; // (the lawn's far edge)
            float roadMid = roadNear - RoadWide * 0.5f;
            float leftEdge = look.street ? centre.z - hd - 3f : centre.z - platR - 16f; // (the lawn's left edge: the pavement beyond it, clear of a stand)
            bool clear(Vector3 at) => look.street || new Vector2(at.x - centre.x, at.z - centre.z).magnitude > platR + 15f; // (nothing in the stands)
            Shapes.Rounded("Road", p, new Vector3(roadMid, ground + 0.02f, centre.z + 40f), new Vector3(RoadWide, 0.04f, 260f), 0.01f, asphalt);
            for (float z = leftEdge - 140f; z < centre.z + 170f; z += 6f)
                Shapes.Rounded("Dash", p, new Vector3(roadMid, ground + 0.05f, z), new Vector3(0.25f, 0.02f, 3f), 0.01f, line);
            var bus1 = Bus(p, look, new Vector3(roadMid + RoadWide * 0.2f, ground, centre.z + 18f), 90f);
            Drive(bus1, Vector3.forward, BusSpeed, centre.z - 90f, centre.z + 120f); // (along the road, left to right)

            // the pavement along the lawn's left side with its lamps, the road beyond it with a bus
            float pavMid = leftEdge - PavementWide * 0.5f;
            Shapes.Rounded("Pavement", p, new Vector3((centre.x + 60f + roadNear) * 0.5f, ground + 0.08f, pavMid), new Vector3(centre.x + 60f - roadNear + RoadWide, 0.16f, PavementWide), 0.03f, slab);
            float sideRoad = leftEdge - PavementWide - RoadWide * 0.5f;
            Shapes.Rounded("SideRoad", p, new Vector3((centre.x + 60f + roadNear) * 0.5f, ground + 0.02f, sideRoad), new Vector3(centre.x + 60f - roadNear + RoadWide, 0.04f, RoadWide), 0.01f, asphalt);
            for (float x = centre.x + 58f; x > roadNear; x -= 6f)
                Shapes.Rounded("Dash", p, new Vector3(x, ground + 0.05f, sideRoad), new Vector3(3f, 0.02f, 0.25f), 0.01f, line);
            var bus2 = Bus(p, look, new Vector3(centre.x - 60f, ground, sideRoad + RoadWide * 0.2f), 0f);
            Drive(bus2, Vector3.left, BusSpeed, -(centre.x + 40f), -(roadNear - RoadWide - 20f)); // (up the side road, away from the camera)
            for (float x = centre.x + 20f; x > roadNear + 4f; x -= 22f)
            {
                var lamp = MadeModel(p, GalataLook, "lamp", false);
                var lb = BoundsOf(lamp);
                lamp.localScale = Vector3.one * (4.8f / Mathf.Max(0.01f, lb.size.y));
                lb = BoundsOf(lamp);
                lamp.position += new Vector3(x - lb.center.x, ground + 0.16f - lb.min.y, leftEdge - 0.6f - lb.center.z);
            }

            // low green railings: along the lawn's far edge and its left edge; a hedge in a stone planter by the pavement
            Railing(p, new Vector3(roadNear + 0.6f, ground, leftEdge), new Vector3(roadNear + 0.6f, ground, centre.z + 90f), rail);
            Railing(p, new Vector3(centre.x + 40f, ground, leftEdge + 0.4f), new Vector3(roadNear + 0.6f, ground, leftEdge + 0.4f), rail);
            var hedge = Matte(new Color(0.2f, 0.36f, 0.16f), 0.15f);
            foreach (float hx in new[] { centre.x - 18f, centre.x - 46f })
            {
                if (!clear(new Vector3(hx, ground, leftEdge + 2.2f))) continue;
                Shapes.Rounded("Planter", p, new Vector3(hx, ground + 0.35f, leftEdge + 2.2f), new Vector3(6f, 0.7f, 2.4f), 0.06f, slab);
                Shapes.Rounded("Hedge", p, new Vector3(hx, ground + 1.1f, leftEdge + 2.2f), new Vector3(5.6f, 1.1f, 2f), 0.45f, hedge);
            }

            // the fans back on the lawn behind the fight (none where the stands stand: they sit in them)
            if (look.street)
            for (int side = -1; side <= 1; side += 2)
            {
                int team = side < 0 ? southTeam : 1 - southTeam;
                for (int r = 0; r < FanRowsBack; r++)
                    for (float z = 1.2f + (r % 2) * 1.1f; z < 17f; z += FanGap * k * 0.8f)
                        AddFan(new Vector3(centre.x - hw - FanBack - r * 1.8f + R(-0.3f, 0.3f), ground, centre.z + side * (z + R(-0.3f, 0.3f))), team, rng);
            }

            // short grass over the lawn (the mown picture under it), and London planes and old stone buildings to the left
            // of Big Ben, across the road and behind the side road
            var tufts = Tufts((x, z) => new Vector3(x, 0f, z), leftEdge + 0.5f, centre.z + 70f, roadNear + 1f, centre.x + 40f, 3200, rng);
            bentMeshes.Add(tufts);
            var grassGo = new GameObject("LawnGrass");
            grassGo.transform.SetParent(p, false);
            grassGo.transform.position = new Vector3(0f, ground, 0f);
            grassGo.transform.localScale = new Vector3(1f, 0.32f, 1f); // (mown: short blades)
            grassGo.AddComponent<MeshFilter>().sharedMesh = tufts;
            grassGo.AddComponent<MeshRenderer>().sharedMaterial = Matte(new Color(0.3f, 0.5f, 0.2f), 0.3f);
            float farSide = roadNear - RoadWide - 9f; // (the planes' crowns clear of the road: the buses pass)
            for (float z = benZ - 9f; z > benZ - 80f; z -= R(6f, 9f))
                PlaneTree(p, new Vector3(farSide - R(0f, 14f), ground, z), R(13f, 17f), rng);
            for (float x = centre.x + 10f; x > roadNear; x -= R(10f, 14f))
                PlaneTree(p, new Vector3(x, ground, sideRoad - RoadWide * 0.5f - 9f - R(0f, 3f)), R(12f, 16f), rng);
            // slender young trees round the lawn: along its left edge, and along its far edge left of Big Ben (its front kept clear)
            for (float x = centre.x + 26f; x > roadNear + 3f; x -= R(7f, 9f))
                if (clear(new Vector3(x, ground, leftEdge + 1.4f))) SlenderTree(p, new Vector3(x, ground, leftEdge + 1.4f), rng);
            for (float z = leftEdge + 4f; z < benZ - 7f; z += R(7f, 9f))
                if (clear(new Vector3(roadNear + 1.6f, ground, z))) SlenderTree(p, new Vector3(roadNear + 1.6f, ground, z), rng);
            var sand = Matte(new Color(0.8f, 0.72f, 0.56f), 0.2f);
            var grey = Matte(new Color(0.66f, 0.66f, 0.68f), 0.2f);
            var roof = Matte(new Color(0.32f, 0.34f, 0.38f), 0.15f);
            for (float z = benZ - 14f; z > benZ - 110f; z -= R(12f, 18f))
            {
                float hgt = R(11f, 18f), w = R(10f, 16f), bx = farSide - R(18f, 30f);
                Shapes.Rounded("Building", p, new Vector3(bx, ground + hgt * 0.5f, z), new Vector3(12f, hgt, w), 0.2f, rng.NextDouble() < 0.6 ? sand : grey);
                Shapes.Rounded("BuildingRoof", p, new Vector3(bx, ground + hgt + 0.8f, z), new Vector3(11f, 1.6f, w - 0.6f), 0.5f, roof);
            }
        }

        /// <summary>Which end of the model along z rises highest (Big Ben): its sign from the middle.</summary>
        private static float HighEnd(Transform model, Bounds b)
        {
            float best = float.MinValue, at = 0f;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                foreach (var v in mesh.vertices)
                {
                    var w = mf.transform.TransformPoint(v);
                    if (w.y > best) { best = w.y; at = w.z - b.center.z; }
                }
            }
            return at;
        }

        /// <summary>The fans' distance behind the floor's far edge and their rows; the buses' speed.</summary>
        private const float FanBack = 14f, BusSpeed = 7f;
        private const int FanRowsBack = 4;

        private Material[] leafMats;

        /// <summary>A slender young tree: a thin trunk, a tall narrow crown (a few greens).</summary>
        private void SlenderTree(Transform p, Vector3 at, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            if (leafMats == null)
                leafMats = new[] { Matte(new Color(0.24f, 0.44f, 0.18f), 0.2f), Matte(new Color(0.3f, 0.5f, 0.2f), 0.2f), Matte(new Color(0.2f, 0.38f, 0.16f), 0.2f), Matte(new Color(0.38f, 0.32f, 0.24f), 0.1f) };
            float trunk = R(2.6f, 3.4f), crown = R(4f, 5.5f), wide = R(1.8f, 2.4f);
            Shapes.Primitive(PrimitiveType.Cylinder, "TreeTrunk", p, at + Vector3.up * trunk * 0.5f, new Vector3(0.22f, trunk * 0.5f, 0.22f), leafMats[3]);
            Shapes.Primitive(PrimitiveType.Sphere, "TreeCrown", p, at + Vector3.up * (trunk + crown * 0.42f), new Vector3(wide, crown, wide), leafMats[rng.Next(3)]);
            Shapes.Primitive(PrimitiveType.Sphere, "TreeCrownTop", p, at + new Vector3(R(-0.3f, 0.3f), trunk + crown * 0.75f, R(-0.3f, 0.3f)), new Vector3(wide * 0.7f, crown * 0.5f, wide * 0.7f), leafMats[rng.Next(3)]);
        }

        private void PlaneTree(Transform p, Vector3 at, float height, System.Random rng)
        {
            var tree = MadeModel(p, EuropeLook, "tree_1", false);
            var tb = BoundsOf(tree);
            tree.localScale = Vector3.one * (height / Mathf.Max(0.01f, tb.size.y));
            tree.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * tree.rotation;
            tb = BoundsOf(tree);
            tree.position += new Vector3(at.x - tb.center.x, at.y - tb.min.y - 0.2f, at.z - tb.center.z);
        }

        private Transform Bus(Transform p, Look look, Vector3 at, float turn)
        {
            var bus = MadeModel(p, look, "bus", false);
            var bb = BoundsOf(bus);
            bus.localScale = Vector3.one * (11f / Mathf.Max(0.01f, Mathf.Max(bb.size.x, bb.size.z)));
            bb = BoundsOf(bus);
            if (bb.size.z > bb.size.x) bus.rotation = Quaternion.Euler(0f, 90f, 0f) * bus.rotation; // (along x first)
            bus.rotation = Quaternion.Euler(0f, turn, 0f) * bus.rotation;
            bb = BoundsOf(bus);
            bus.position += new Vector3(at.x - bb.center.x, at.y - bb.min.y, at.z - bb.center.z);
            return bus;
        }

        /// <summary>A low railing of posts and two rails from <paramref name="a"/> to <paramref name="b"/>.</summary>
        private static void Railing(Transform p, Vector3 a, Vector3 b, Material m)
        {
            var d = b - a; float len = d.magnitude; var dir = d / Mathf.Max(0.01f, len);
            var rot = Quaternion.LookRotation(dir);
            foreach (float h in new[] { 0.45f, 0.9f })
                Shapes.Rounded("Rail", p, (a + b) * 0.5f + Vector3.up * h, new Vector3(0.06f, 0.06f, len), 0.02f, m).transform.rotation = rot;
            for (float t = 0f; t <= len; t += 2.5f)
                Shapes.Rounded("RailPost", p, a + dir * t + Vector3.up * 0.47f, new Vector3(0.08f, 0.94f, 0.08f), 0.02f, m);
        }
    }
}
