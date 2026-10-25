using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The city stages (Galata first): no stands, no platform; the fight is on a cobbled street that runs up to the landmark,
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

        /// <summary>The landmark's height and its distance behind the floor's room (far: the low fight camera sees it whole, at
        /// the street's end), and the houses' heights.</summary>
        private const float TowerHeight = 30f, TowerBack = 100f, HouseMin = 16f, HouseMax = 22f;
        /// <summary>Which way the house model's front faces before it is turned (degrees about up: 0 = +z).</summary>
        private const float HouseFront = 0f;
        /// <summary>The pavements' width, how far up the street the fans stand (one row, from behind the fight), how far
        /// apart, and the lamps' spacing.</summary>
        private const float Walk = 2.6f, CrowdLength = 30f, FanGap = 1.25f, LampEvery = 32f;

        /// <summary>The ground: one big quad, its picture tiled about every 3 m.</summary>
        private void PavedGround(Transform p, Look look, float ground)
        {
            const float Size = 220f, Tile = 3.2f;
            // everywhere, or (a quay) from in front of the camera to its edge behind the floor
            float x0 = centre.x - 50f - Size * 0.5f, x1 = centre.x - 50f + Size * 0.5f;
            if (look.paveFar >= 0f) { x0 = centre.x - hw - QuayBack - look.paveFar; x1 = centre.x + 80f; }
            float z0 = centre.z - Size * 0.5f, z1 = centre.z + Size * 0.5f;
            var mesh = new Mesh { name = "Paving" };
            mesh.vertices = new[] { new Vector3(x0, ground, z0), new Vector3(x0, ground, z1), new Vector3(x1, ground, z1), new Vector3(x1, ground, z0) };
            mesh.uv = new[] { new Vector2(x0, z0) / Tile, new Vector2(x0, z1) / Tile, new Vector2(x1, z1) / Tile, new Vector2(x1, z0) / Tile };
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
        /// A narrow street (along -x, away from the camera) with the fight at its near end and the tower on its cobbled
        /// plaza at its far end: tall houses right along it either side behind thin pavements (which run on towards the
        /// camera), a row of fans on each pavement up the street from the fight (south -z: <paramref name="southTeam"/>'s), lamps here
        /// and there, a simit cart in the street; houses closing the far end behind the tower, the city's domes beyond.
        /// </summary>
        private void GalataStreet(Transform p, Look look, float ground, float platR, int southTeam, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            crowdSize = MadeCrowd;
            float k = crowdSize;
            var stone = Matte(new Color(0.5f, 0.5f, 0.5f), 0.15f);

            // the tower on its own cobbled plaza
            var tower = MadeModel(p, look, "tower", false);
            var tb = BoundsOf(tower);
            tower.localScale = Vector3.one * (TowerHeight / Mathf.Max(0.01f, tb.size.y));
            tb = BoundsOf(tower);
            float towerX = centre.x - platR - TowerBack;
            tower.position += new Vector3(towerX - tb.center.x, ground - tb.min.y - 0.05f, centre.z - tb.center.z);
            float plaza = BoundsOf(tower).extents.x;

            float street = hd + 2f;               // (the street's half width: the floor and a little room)
            float fronts = street + Walk;         // (the houses' fronts)
            float near = centre.x + 36f;          // (the pavements run on past the camera)
            float housesFrom = centre.x + hw;     // (the first houses beside the fight)
            float end = towerX - plaza;           // (the street's far end: the plaza's back)

            for (int side = -1; side <= 1; side += 2)
            {
                int team = side < 0 ? southTeam : 1 - southTeam;
                // the thin pavement along the houses, from past the camera up to the plaza
                Shapes.Rounded("Pavement", p, new Vector3((near + towerX + plaza) * 0.5f, ground + 0.1f, centre.z + side * (street + Walk * 0.5f)),
                    new Vector3(near - towerX - plaza, 0.2f, Walk), 0.04f, stone);
                // one row of fans on it up the street towards the tower (beside the fight they would stand outside the
                // low camera's view), lengthwise, each looking at the fight
                for (float x = centre.x - hw; x > centre.x - hw - CrowdLength; x -= FanGap * k)
                    AddFan(new Vector3(x + R(-0.2f, 0.2f) * k, ground + 0.2f, centre.z + side * (street + 1.1f + R(-0.1f, 0.1f))), team, rng);
                // lamps at the kerb here and there (the two sides staggered)
                for (float x = near - 6f - (side > 0 ? LampEvery * 0.5f : 0f); x > towerX + plaza; x -= LampEvery)
                {
                    var lamp = MadeModel(p, look, "lamp", false);
                    var lb = BoundsOf(lamp);
                    lamp.localScale = Vector3.one * (4.6f / Mathf.Max(0.01f, lb.size.y));
                    lb = BoundsOf(lamp);
                    lamp.position += new Vector3(x - lb.center.x, ground + 0.2f - lb.min.y, centre.z + side * (street + 0.3f) - lb.center.z);
                }
                // tall houses right along the street, fronts to it, from beside the fight up to the plaza
                float cursor = housesFrom;
                while (cursor > towerX + plaza * 0.5f)
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
                    float hh = R(HouseMin + 2f, HouseMax + 2f);
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
            float farX = end - 22f; // (inside the camera's reach)
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(farX - R(0f, 8f), ground, centre.z + Mathf.Lerp(-34f, 34f, i / 3f) + R(-4f, 4f));
                float rad = R(3.5f, 5f);
                Shapes.Primitive(PrimitiveType.Cylinder, "Drum", p, at + Vector3.up * rad * 0.9f, new Vector3(rad * 2.1f, rad * 0.9f, rad * 2.1f), far);
                Shapes.Primitive(PrimitiveType.Sphere, "Dome", p, at + Vector3.up * rad * 1.8f, new Vector3(rad * 2f, rad * 1.5f, rad * 2f), far);
                for (int m = -1; m <= 1; m += 2)
                {
                    float mh = R(13f, 17f);
                    var foot = at + new Vector3(R(-2f, 2f), 0f, m * (rad + 2.5f));
                    Shapes.Primitive(PrimitiveType.Cylinder, "Minaret", p, foot + Vector3.up * mh * 0.5f, new Vector3(0.8f, mh * 0.5f, 0.8f), far);
                    Shapes.Primitive(PrimitiveType.Cylinder, "Balcony", p, foot + Vector3.up * mh * 0.78f, new Vector3(1.3f, 0.25f, 1.3f), far);
                    Piece(p, "MinaretCap", cone, foot + Vector3.up * mh, new Vector3(0.9f, 2.8f, 0.9f), far);
                }
            }

            // a simit cart in the street between the fight and the tower, a little to the left
            var cart = MadeModel(p, look, "prop", false);
            var cb = BoundsOf(cart);
            cart.localScale = Vector3.one * (5.2f / Mathf.Max(0.01f, cb.size.y)); // (big enough to read far up the street)
            cart.rotation = Quaternion.Euler(0f, 90f + R(-20f, 20f), 0f) * cart.rotation;
            cb = BoundsOf(cart);
            var spot = new Vector3(centre.x - platR - 14f, ground, centre.z - street * 0.35f);
            cart.position += new Vector3(spot.x - cb.center.x, ground - cb.min.y, spot.z - cb.center.z);
        }
    }
}
