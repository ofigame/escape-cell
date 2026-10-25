using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The Maiden's Tower stage: the fight on Üsküdar's stone quay, its rope railing at the water's edge behind the fight,
    /// the fans along it either side; the Bosphorus beyond with the tower on its islet, a ferry and a little lighthouse, and
    /// across the water Topkapı Palace on its hill with the old shore stretching away either side.
    /// </summary>
    public partial class DuelStands
    {
        private static readonly Look KizLook = new Look
        {
            pebbles = new Color(0.7f, 0.68f, 0.62f),
            folder = "Duel/Kiz/kiz_", cover = 0f, street = true, city = "kiz", paving = "Duel/Kiz/kiz_ground", paveFar = 0f, crystals = false, backTree = false,
            thick = 1f, rim = 1f, standRise = 0f,
            glow = new Color(1f, 0.9f, 0.7f), selfLight = new Color(0.32f, 0.31f, 0.3f),
            ground = new Color(0.8f, 0.77f, 0.7f), mound = new Color(0.82f, 0.79f, 0.72f), bank = new Color(0.7f, 0.68f, 0.62f), grassColour = Color.white,
            crystal = Color.white, mist = new Color(0.9f, 0.93f, 0.97f, 0.05f), lamp = new Color(1f, 0.9f, 0.75f), lampPower = 0.4f,
            haze = new Color(0.8f, 0.86f, 0.92f, 0f), skyTop = new Color(0.42f, 0.6f, 0.84f), skyLow = new Color(0.86f, 0.9f, 0.93f),
            sun = new Color(1f, 0.96f, 0.9f), sunPower = 1.2f, ambSky = new Color(0.72f, 0.78f, 0.88f), ambHorizon = new Color(0.76f, 0.78f, 0.8f), ambGround = new Color(0.36f, 0.36f, 0.36f),
        };

        /// <summary>The quay's edge behind the floor, how far the water lies under it; the tower's height and distance past
        /// the edge (far: the low fight camera sees it whole over the water); the palace's width and distance.</summary>
        private const float QuayBack = 4f, QuayDrop = 1.3f, KizHeight = 26f, KizBack = 85f, PalaceWidth = 54f, PalaceBack = 145f, PalaceSide = 16f;
        /// <summary>Which way the tower model is turned (its annex towards the camera's right, as seen from Üsküdar).</summary>
        private const float KizTurn = 0f;

        private Material seaMat;

        /// <summary>The quay, the railing, the fans along it, the sea and all that stands in it and beyond it.</summary>
        private void MaidenQuay(Transform p, Look look, float ground, float platR, int southTeam, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            crowdSize = MadeCrowd;
            float k = crowdSize;
            float edge = centre.x - hw - QuayBack;   // (the quay's edge: the water beyond it)
            float water = ground - QuayDrop;

            // the quay's face down to the water, its lighter kerb along the top
            var stone = Matte(new Color(0.66f, 0.63f, 0.57f), 0.15f);
            var kerb = Matte(new Color(0.86f, 0.83f, 0.76f), 0.15f);
            Shapes.Rounded("QuayWall", p, new Vector3(edge + 0.3f, water + (ground - water) * 0.5f - 0.5f, centre.z), new Vector3(0.6f, ground - water + 1f, 300f), 0.05f, stone);
            Shapes.Rounded("QuayKerb", p, new Vector3(edge + 0.45f, ground + 0.06f, centre.z), new Vector3(0.9f, 0.12f, 300f), 0.03f, kerb);

            // the railing: square posts, two ropes hanging between them
            var post = Matte(new Color(0.46f, 0.5f, 0.42f), 0.12f);
            var rope = Matte(new Color(0.78f, 0.7f, 0.54f), 0.15f);
            const float Gap = 3.4f, PostH = 1.2f;
            for (float z = -60f; z <= 60f; z += Gap)
            {
                var at = new Vector3(edge + 0.5f, ground, centre.z + z);
                Shapes.Rounded("Post", p, at + Vector3.up * PostH * 0.5f, new Vector3(0.26f, PostH, 0.26f), 0.03f, post);
                Shapes.Rounded("PostCap", p, at + Vector3.up * (PostH + 0.03f), new Vector3(0.32f, 0.06f, 0.32f), 0.02f, post);
                if (z + Gap > 60f) break;
                var next = at + Vector3.forward * Gap;
                foreach (float hgt in new[] { PostH - 0.15f, PostH * 0.5f })
                {
                    var a = at + Vector3.up * hgt; var b = next + Vector3.up * hgt;
                    var mid = (a + b) * 0.5f + Vector3.down * 0.22f; // (the rope's sag)
                    Rope(p, a, mid, rope); Rope(p, mid, b, rope);
                }
            }

            // the fans along the railing either side of the fight (the middle kept clear: the tower over the water)
            for (int side = -1; side <= 1; side += 2)
            {
                int team = side < 0 ? southTeam : 1 - southTeam;
                for (float z = 4.5f; z < 20f; z += FanGap * k * 0.85f)
                    AddFan(new Vector3(edge + 1.2f + R(-0.2f, 0.3f), ground, centre.z + side * (z + R(-0.3f, 0.3f))), team, rng);
            }

            // a simit cart on the quay, to the left
            var cart = MadeModel(p, GalataLook, "prop", false);
            var cb = BoundsOf(cart);
            cart.localScale = Vector3.one * (5.2f / Mathf.Max(0.01f, cb.size.y));
            cart.rotation = Quaternion.Euler(0f, 20f, 0f) * cart.rotation;
            cb = BoundsOf(cart);
            var cartAt = new Vector3(centre.x + 3f, ground, centre.z - hd - 4f);
            cart.position += new Vector3(cartAt.x - cb.center.x, ground - cb.min.y, cartAt.z - cb.center.z);

            // the sea
            Sea(p, water, edge);

            // the tower on its islet
            var tower = MadeModel(p, look, "tower", false);
            var tb = BoundsOf(tower);
            tower.localScale = Vector3.one * (KizHeight / Mathf.Max(0.01f, tb.size.y));
            tower.rotation = Quaternion.Euler(0f, KizTurn, 0f) * tower.rotation;
            tb = BoundsOf(tower);
            float tx = edge - KizBack;
            tower.position += new Vector3(tx - tb.center.x, water - tb.min.y - 0.6f, centre.z - 2f - tb.center.z);

            // a ferry crossing to the left of it, a little lighthouse to its right
            var ferry = MadeModel(p, look, "ferry", false);
            var fb = BoundsOf(ferry);
            ferry.localScale = Vector3.one * (30f / Mathf.Max(0.01f, Mathf.Max(fb.size.x, fb.size.z)));
            fb = BoundsOf(ferry);
            if (fb.size.x > fb.size.z) ferry.rotation = Quaternion.Euler(0f, 90f, 0f) * ferry.rotation; // (along the shore)
            fb = BoundsOf(ferry);
            ferry.position += new Vector3(tx + 18f - fb.center.x, water - fb.min.y - 1.2f, centre.z - 30f - fb.center.z);
            var white = Matte(new Color(0.95f, 0.95f, 0.93f), 0.3f);
            var red = Matte(new Color(0.85f, 0.15f, 0.12f), 0.3f);
            var rock = Matte(new Color(0.5f, 0.48f, 0.45f), 0.15f);
            var lh = new Vector3(tx + 6f, water, centre.z + 24f);
            Shapes.Rounded("LightRock", p, lh + Vector3.up * 0.4f, new Vector3(6f, 1.4f, 6f), 0.6f, rock);
            Shapes.Primitive(PrimitiveType.Cylinder, "Lighthouse", p, lh + Vector3.up * 3.6f, new Vector3(1.8f, 2.6f, 1.8f), white);
            Shapes.Primitive(PrimitiveType.Cylinder, "LighthouseBand", p, lh + Vector3.up * 5.4f, new Vector3(1.84f, 0.7f, 1.84f), red);
            Shapes.Primitive(PrimitiveType.Cylinder, "LighthouseTop", p, lh + Vector3.up * 6.6f, new Vector3(1.2f, 0.5f, 1.2f), red);

            // across the water: Topkapı Palace on its hill (shallow: a backdrop), the old shore either side, hazy
            var palace = MadeModel(p, look, "palace", false);
            var pb = BoundsOf(palace);
            float kp = PalaceWidth / Mathf.Max(0.01f, Mathf.Max(pb.size.x, pb.size.z));
            if (pb.size.z < pb.size.x) palace.rotation = Quaternion.Euler(0f, 90f, 0f) * palace.rotation; // (its width across the view)
            palace.localScale = Vector3.one * kp;
            pb = BoundsOf(palace);
            Squash(palace, 30f / Mathf.Max(0.01f, pb.size.x)); // (its depth along the view to about 30: inside the camera's reach)
            pb = BoundsOf(palace);
            palace.position += new Vector3(edge - PalaceBack - pb.center.x, water - pb.min.y - pb.size.y * 0.18f, centre.z + PalaceSide - pb.center.z); // (its rocky shore down in the water: only the palace and its trees over the horizon)
            foreach (var r in palace.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial.SetColor("_BaseColor", new Color(0.74f, 0.8f, 0.92f)); // (the distance's blue)
                r.sharedMaterial.SetColor("_EmissionColor", new Color(0.46f, 0.5f, 0.58f));
            }
            var shore = Matte(new Color(0.6f, 0.67f, 0.72f), 0.7f);
            var houses = Matte(new Color(0.84f, 0.84f, 0.84f), 0.5f);
            float sx = edge - PalaceBack - 6f;
            // (seen from Üsküdar the old city runs on to the right of the palace; to its left the open sea)
            for (float z = PalaceSide + PalaceWidth * 0.42f; z <= 180f; z += R(14f, 22f))
            {
                float hgt = R(2.5f, 6f), w = R(18f, 28f);
                Shapes.Rounded("Shore", p, new Vector3(sx - R(0f, 8f), water + hgt * 0.5f - 0.5f, centre.z + z), new Vector3(16f, hgt, w), hgt * 0.4f, shore);
                for (int i = 0; i < 3; i++)
                    Shapes.Rounded("ShoreHouse", p, new Vector3(sx + 5f, water + R(1f, hgt * 0.7f), centre.z + z + R(-w * 0.4f, w * 0.4f)), new Vector3(3f, R(2f, 4f), R(2.5f, 5f)), 0.2f, houses);
            }
        }

        /// <summary>A model's depth along the view (x) squashed, about its middle (a backdrop: its face kept, its depth not needed).</summary>
        private static void Squash(Transform model, float k)
        {
            var holder = new GameObject(model.name + "Squash").transform;
            holder.SetParent(model.parent, false);
            var b = BoundsOf(model);
            holder.position = b.center;
            model.SetParent(holder, true);
            holder.localScale = new Vector3(Mathf.Clamp(k, 0.1f, 1f), 1f, 1f);
        }

        /// <summary>A length of rope between two points.</summary>
        private static void Rope(Transform p, Vector3 a, Vector3 b, Material m)
        {
            var go = Shapes.Rounded("Rope", p, (a + b) * 0.5f, new Vector3(0.07f, 0.07f, (b - a).magnitude + 0.04f), 0.03f, m);
            go.transform.rotation = Quaternion.LookRotation(b - a);
        }

        /// <summary>The Bosphorus: a big plane from the quay out past the far shore, small waves drifting on it.</summary>
        private void Sea(Transform p, float water, float edge)
        {
            const int N = 128;
            var seaTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Waves", wrapMode = TextureWrapMode.Repeat };
            marbles.Add(seaTex); // (destroyed with the stage)
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N * Mathf.PI * 2f, v = y / (float)N * Mathf.PI * 2f;
                    float w = Mathf.Sin(u * 3f + Mathf.Sin(v * 2f) * 1.5f) * 0.5f + Mathf.Sin(v * 5f + u * 2f) * 0.3f + Mathf.Sin((u + v) * 7f) * 0.2f;
                    float c = 0.86f + w * 0.1f + (w > 0.7f ? 0.12f : 0f); // (light crests)
                    var b = (byte)(Mathf.Clamp01(c) * 255f);
                    px[y * N + x] = new Color32(b, b, b, 255);
                }
            seaTex.SetPixels32(px);
            seaTex.Apply(true, false);
            seaMat = MaterialFactory.CreateSurface(new Color(0.22f, 0.44f, 0.56f), Color.black, 0.75f, 0f);
            seaMat.SetTexture("_BaseMap", seaTex); seaMat.SetTexture("_MainTex", seaTex);
            seaMat.SetTextureScale("_BaseMap", new Vector2(70f, 70f));
            seaMat.EnableKeyword("_EMISSION");
            seaMat.SetTexture("_EmissionMap", seaTex);
            seaMat.SetColor("_EmissionColor", new Color(0.08f, 0.16f, 0.22f));
            mats.Add(seaMat);
            var mesh = new Mesh { name = "Sea" };
            float x0 = edge + 0.4f, x1 = edge - 400f, z0 = centre.z - 260f, z1 = centre.z + 260f;
            mesh.vertices = new[] { new Vector3(x1, water, z0), new Vector3(x1, water, z1), new Vector3(x0, water, z1), new Vector3(x0, water, z0) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            bentMeshes.Add(mesh);
            var go = new GameObject("Sea");
            go.transform.SetParent(transform, false); // (not merged with the decor: its waves drift)
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = seaMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Each frame: the sea's small waves drift.</summary>
        private void DriftSea(float time)
        {
            if (seaMat == null) return;
            seaMat.SetTextureOffset("_BaseMap", new Vector2(time * 0.004f, time * 0.0025f));
        }
    }
}
