using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The city stages (Galata first): no stands; the platform lies in a cobbled street that runs up to the landmark,
    /// the fans standing along its pavements lengthwise on low steps, houses either side, lamps, bollards, and the city's
    /// domes and minarets far behind.
    /// </summary>
    public partial class DuelStands
    {
        private static readonly Look GalataLook = new Look
        {
            pebbles = new Color(0.42f, 0.42f, 0.42f),
            folder = "Duel/Galata/galata_", cover = 0f, street = true, paving = "Duel/Galata/galata_ground", crystals = false, backTree = false,
            thick = 1f, rim = 1f, standRise = 0f,
            glow = new Color(1f, 0.82f, 0.5f), selfLight = new Color(0.3f, 0.28f, 0.26f),
            ground = new Color(0.42f, 0.42f, 0.42f), mound = new Color(0.46f, 0.46f, 0.46f), bank = new Color(0.4f, 0.4f, 0.4f), grassColour = Color.white,
            crystal = Color.white, mist = new Color(1f, 0.88f, 0.7f, 0.07f), lamp = new Color(1f, 0.78f, 0.46f), lampPower = 0.6f,
            haze = new Color(0.94f, 0.76f, 0.55f, 0f), skyTop = new Color(0.3f, 0.5f, 0.86f), skyLow = new Color(1f, 0.82f, 0.58f),
            sun = new Color(1f, 0.84f, 0.62f), sunPower = 1.25f, ambSky = new Color(0.66f, 0.7f, 0.82f), ambHorizon = new Color(0.82f, 0.7f, 0.56f), ambGround = new Color(0.3f, 0.29f, 0.28f),
        };

        /// <summary>The landmark's height, its distance behind the platform's rim, and the houses' heights.</summary>
        private const float TowerHeight = 42f, TowerBack = 56f, HouseMin = 10f, HouseMax = 13f;
        /// <summary>Which way the house model's front faces before it is turned (degrees about up: 0 = +z).</summary>
        private const float HouseFront = 0f;
        /// <summary>How many rows of fans stand on each pavement, and how far apart (along the street and across it).</summary>
        private const int FanRows = 4;
        private const float FanRow = 1f, FanStep = 0.3f;

        /// <summary>The ground: one big quad, its picture tiled about every 3 m.</summary>
        private void PavedGround(Transform p, Look look, float ground)
        {
            const float Size = 220f, Tile = 3.2f;
            var c = new Vector3(centre.x - 50f, ground, centre.z);
            var mesh = new Mesh { name = "Paving" };
            float h = Size * 0.5f;
            mesh.vertices = new[] { c + new Vector3(-h, 0f, -h), c + new Vector3(-h, 0f, h), c + new Vector3(h, 0f, h), c + new Vector3(h, 0f, -h) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, Size / Tile), new Vector2(Size / Tile, Size / Tile), new Vector2(Size / Tile, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            bentMeshes.Add(mesh); // (destroyed with the stage)
            var m = Surface(Color.white, 0.05f);
            var tex = Resources.Load<Texture2D>(look.paving);
            if (tex != null) tex.wrapMode = TextureWrapMode.Repeat;
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", look.selfLight);
            var go = new GameObject("Paving");
            go.transform.SetParent(p, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// <summary>
        /// The street (along -x, away from the camera) from beside the platform up to the tower on its cobbled plaza: on each
        /// side (south -z: <paramref name="southTeam"/>'s fans) stepped pavements full of fans, bollards at the kerb, lamps,
        /// a row of houses; houses closing the far end behind the tower, the city's domes and minarets beyond.
        /// </summary>
        private void GalataStreet(Transform p, Look look, float ground, float platR, int southTeam, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            crowdSize = MadeCrowd;
            float k = crowdSize;
            var stone = Matte(new Color(0.5f, 0.5f, 0.5f), 0.15f);
            var iron = Surface(new Color(0.08f, 0.08f, 0.09f), 0.35f);

            // the tower on its own cobbled plaza
            var tower = MadeModel(p, look, "tower", false);
            var tb = BoundsOf(tower);
            tower.localScale = Vector3.one * (TowerHeight / Mathf.Max(0.01f, tb.size.y));
            tb = BoundsOf(tower);
            float towerX = centre.x - platR - TowerBack;
            tower.position += new Vector3(towerX - tb.center.x, ground - tb.min.y - 0.05f, centre.z - tb.center.z);
            float plaza = BoundsOf(tower).extents.x;

            float front = centre.x + platR * 0.35f; // (the street's near end: a little in front of the platform's middle)
            float end = towerX - plaza;             // (its far end: the plaza's back)
            float kerb = platR + 0.6f;              // (the first fans' row, off the platform's rim)
            float fronts = kerb + FanRows * FanRow + 0.6f; // (the houses' fronts)

            for (int side = -1; side <= 1; side += 2)
            {
                int team = side < 0 ? southTeam : 1 - southTeam;
                float stepsEnd = towerX + plaza * 0.9f; // (the pavements stop at the plaza)
                // the stepped pavement: each row a step higher, the last reaching the houses
                for (int r = 0; r < FanRows; r++)
                {
                    float top = ground + 0.2f + r * FanStep;
                    float z0 = kerb + r * FanRow - 0.4f, z1 = r == FanRows - 1 ? fronts + 0.5f : z0 + FanRow;
                    Shapes.Rounded("Step", p, new Vector3((front + stepsEnd) * 0.5f, (ground + top) * 0.5f, centre.z + side * (z0 + z1) * 0.5f),
                        new Vector3(front - stepsEnd, top - ground, z1 - z0 + 0.02f), 0.04f, stone);
                    // its fans, lengthwise, each looking at the platform
                    for (float x = front - 0.6f; x > stepsEnd + 0.6f; x -= 0.55f * k)
                        AddFan(new Vector3(x + R(-0.15f, 0.15f) * k, top, centre.z + side * (kerb + r * FanRow + R(-0.1f, 0.1f))), team, rng);
                }
                // bollards at the kerb, as in the old streets
                for (float x = front - 1f; x > stepsEnd + 1f; x -= 2.4f)
                    Shapes.Primitive(PrimitiveType.Cylinder, "Bollard", p, new Vector3(x, ground + 0.45f, centre.z + side * (kerb - 0.75f)), new Vector3(0.24f, 0.45f, 0.24f), iron);
                // lamps against the houses
                for (float x = front - 2f; x > stepsEnd; x -= 9f)
                {
                    var lamp = MadeModel(p, look, "lamp", false);
                    var lb = BoundsOf(lamp);
                    lamp.localScale = Vector3.one * (4.6f / Mathf.Max(0.01f, lb.size.y));
                    lb = BoundsOf(lamp);
                    float top = ground + 0.2f + (FanRows - 1) * FanStep;
                    lamp.position += new Vector3(x - lb.center.x, top - lb.min.y, centre.z + side * (fronts - 0.5f) - lb.center.z);
                }
                // the houses along the street, fronts to it, up past the plaza
                float cursor = front + 4f;
                while (cursor > end)
                {
                    float hh = R(HouseMin, HouseMax);
                    var house = MadeModel(p, look, "house", false);
                    var hb = BoundsOf(house);
                    house.localScale = Vector3.one * (hh / Mathf.Max(0.01f, hb.size.y));
                    house.rotation = Quaternion.Euler(0f, (side > 0 ? 180f : 0f) - HouseFront, 0f) * house.rotation;
                    hb = BoundsOf(house);
                    house.position += new Vector3(cursor - hb.max.x, ground - hb.min.y, centre.z + side * (fronts + hb.extents.z) - hb.center.z);
                    cursor -= hb.size.x + 0.05f;
                }
            }

            // houses closing the street's far end behind the tower, facing down it
            {
                float z = centre.z - 46f;
                while (z < centre.z + 46f)
                {
                    float hh = R(HouseMin + 1f, HouseMax + 3f);
                    var house = MadeModel(p, look, "house", false);
                    var hb = BoundsOf(house);
                    house.localScale = Vector3.one * (hh / Mathf.Max(0.01f, hb.size.y));
                    house.rotation = Quaternion.Euler(0f, 90f - HouseFront, 0f) * house.rotation;
                    hb = BoundsOf(house);
                    house.position += new Vector3(end - 3f - hb.max.x, ground - hb.min.y, z - hb.min.z);
                    z += hb.size.z + 0.05f;
                }
            }

            // the old city's skyline far behind: domes on drums, minarets
            var far = Matte(new Color(0.66f, 0.7f, 0.8f), 0.6f); // (hazy blue in the distance)
            var cone = Cone(12);
            float farX = end - 40f;
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(farX - R(0f, 8f), ground, centre.z + Mathf.Lerp(-34f, 34f, i / 3f) + R(-4f, 4f));
                float rad = R(4.5f, 6.5f);
                Shapes.Primitive(PrimitiveType.Cylinder, "Drum", p, at + Vector3.up * rad * 0.9f, new Vector3(rad * 2.1f, rad * 0.9f, rad * 2.1f), far);
                Shapes.Primitive(PrimitiveType.Sphere, "Dome", p, at + Vector3.up * rad * 1.8f, new Vector3(rad * 2f, rad * 1.5f, rad * 2f), far);
                for (int m = -1; m <= 1; m += 2)
                {
                    float mh = R(22f, 27f);
                    var foot = at + new Vector3(R(-2f, 2f), 0f, m * (rad + 2.5f));
                    Shapes.Primitive(PrimitiveType.Cylinder, "Minaret", p, foot + Vector3.up * mh * 0.5f, new Vector3(1.1f, mh * 0.5f, 1.1f), far);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Balcony", p, foot + Vector3.up * mh * 0.78f, new Vector3(1.8f, 0.3f, 1.8f), far);
                    Piece(p, "MinaretCap", cone, foot + Vector3.up * mh, new Vector3(1.2f, 4f, 1.2f), far);
                }
            }

            // a simit cart at the street's near end, on the south pavement's corner
            var cart = MadeModel(p, look, "prop", false);
            var cb = BoundsOf(cart);
            cart.localScale = Vector3.one * (2.6f / Mathf.Max(0.01f, cb.size.y));
            cart.rotation = Quaternion.Euler(0f, R(60f, 120f), 0f) * cart.rotation;
            cb = BoundsOf(cart);
            var spot = new Vector3(front + 2.5f, ground, centre.z - (kerb + 1.5f));
            cart.position += new Vector3(spot.x - cb.center.x, ground - cb.min.y, spot.z - cb.center.z);
        }
    }
}
