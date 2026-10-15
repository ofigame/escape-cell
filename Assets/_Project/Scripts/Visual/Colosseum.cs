using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SquashBot.Visual
{
    /// <summary>
    /// The challenge's arena, built round the courtyard like the Colosseum: a podium wall with two great barred gates
    /// (north and south), stepped stone tiers rising all round, a tall back wall with rows of arches, torches on the
    /// podium, the two sides' banners on the top, the emperor's box on the rival's side — and the crowd: a thousand-odd
    /// spectators in the sides' colours (the player's south, the rival's north) drawn instanced in a dozen draw calls,
    /// bobbing in their seats, on their feet when their side does well, rolling a wave round the arena in the battle.
    /// A soft crowd murmur plays under it and swells with the cheers.
    /// </summary>
    public class Colosseum : MonoBehaviour
    {
        private const int Tiers = 6;
        private const float PodiumH = 1.3f, TierStep = 1.0f, TierRise = 0.62f;

        private Vector3 centre;
        /// <summary>No stand on the east side (where the camera sits: it would look through the tiers).</summary>
        private bool openEast;
        private bool Open(Vector3 p) => openEast && p.x - centre.x > hw * 0.75f;
        private float hw, hd, corner, floorY;

        // The crowd, in batches by look (body colour × skin), drawn instanced.
        private class Batch { public Mesh mesh; public Material mat; public readonly List<Vector3> pos = new List<Vector3>(); public readonly List<float> phase = new List<float>(); public readonly List<float> angle = new List<float>(); public readonly List<int> side = new List<int>(); public readonly List<float> facing = new List<float>(); public Matrix4x4[] m; public bool isHead; }
        private readonly List<Batch> batches = new List<Batch>();
        private readonly float[] excite = new float[2];
        private bool wave;
        private float waveT;
        private readonly List<(Transform t, float seed)> flames = new List<(Transform, float)>();
        private readonly List<(Transform t, float seed)> banners = new List<(Transform, float)>();
        private AudioSource murmur;

        public static Colosseum Build(Transform parent, Vector3 centre, float halfWidth, float halfDepth, float floorY, int seed, bool openEast = false)
        {
            var go = new GameObject("Colosseum");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Colosseum>();
            c.centre = centre;
            c.hw = halfWidth;
            c.hd = halfDepth;
            c.corner = 2.5f;
            c.floorY = floorY;
            c.openEast = openEast;
            c.Make(seed);
            return c;
        }

        /// <summary>A side's stand cheers (0 the player's, 1 the rival's): 0..1, it settles back by itself.</summary>
        public void Cheer(int side, float strength)
        {
            if (side < 0) { excite[0] = Mathf.Max(excite[0], strength); excite[1] = Mathf.Max(excite[1], strength); }
            else excite[side] = Mathf.Max(excite[side], strength);
        }

        /// <summary>The wave rolling round the whole arena (the all-out battle), on or off.</summary>
        public void Wave(bool on) => wave = on;

        // ---------- The shape: a rounded rectangle round the courtyard, pushed out by an offset ----------

        /// <summary>A point on the loop <paramref name="off"/> out from the courtyard's edge, at <paramref name="t"/> (0..1 round), and its outward normal.</summary>
        private Vector3 Loop(float t, float off, out Vector3 normal)
        {
            float a = hw - corner, b = hd - corner, r = corner + off;
            float straightX = 2f * a, straightZ = 2f * b, arc = Mathf.PI * 0.5f * r;
            float total = 2f * straightX + 2f * straightZ + 4f * arc;
            float d = Mathf.Repeat(t, 1f) * total;
            // South edge (left to right), SE corner, east edge, NE, north (right to left), NW, west, SW.
            Vector3 p;
            if ((d -= 0f) < straightX) { p = new Vector3(-a + d, 0f, -b - r); normal = Vector3.back; }
            else if ((d -= straightX) < arc) { float ang = -90f + d / arc * 90f; p = Corner(a, -b, r, ang, out normal); }
            else if ((d -= arc) < straightZ) { p = new Vector3(a + r, 0f, -b + d); normal = Vector3.right; }
            else if ((d -= straightZ) < arc) { float ang = d / arc * 90f; p = Corner(a, b, r, ang, out normal); }
            else if ((d -= arc) < straightX) { p = new Vector3(a - d, 0f, b + r); normal = Vector3.forward; }
            else if ((d -= straightX) < arc) { float ang = 90f + d / arc * 90f; p = Corner(-a, b, r, ang, out normal); }
            else if ((d -= arc) < straightZ) { p = new Vector3(-a - r, 0f, b - d); normal = Vector3.left; }
            else { d -= straightZ; float ang = 180f + d / arc * 90f; p = Corner(-a, -b, r, ang, out normal); }
            return centre + p;
        }

        private static Vector3 Corner(float cx, float cz, float r, float deg, out Vector3 normal)
        {
            float rad = deg * Mathf.Deg2Rad;
            normal = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            return new Vector3(cx, 0f, cz) + normal * r;
        }

        private float Perimeter(float off) => 4f * (hw - corner) + 4f * (hd - corner) + 2f * Mathf.PI * (corner + off);

        private void Make(int seed)
        {
            var rng = new System.Random(seed);
            var stoneLight = MaterialFactory.CreateSurface(new Color(0.86f, 0.78f, 0.64f), Color.black, 0.15f, 0f);
            var stoneMid = MaterialFactory.CreateSurface(new Color(0.72f, 0.63f, 0.5f), Color.black, 0.1f, 0f);
            var stoneDark = MaterialFactory.CreateSurface(new Color(0.5f, 0.42f, 0.32f), Color.black, 0.1f, 0f);
            var shadow = MaterialFactory.Create(new Color(0.12f, 0.09f, 0.07f), Color.black);

            // The stone: podium, tiers (seat tops light, risers mid), back wall — one mesh, three materials.
            const int N = 220;
            var verts = new List<Vector3>();
            var tris = new[] { new List<int>(), new List<int>(), new List<int>() };
            void Band(float offA, float yA, float offB, float yB, int sub)
            {
                int start = verts.Count;
                for (int i = 0; i <= N; i++)
                {
                    float t = i / (float)N;
                    var pa = Loop(t, offA, out _); var pb = Loop(t, offB, out _);
                    verts.Add(new Vector3(pa.x, yA, pa.z));
                    verts.Add(new Vector3(pb.x, yB, pb.z));
                }
                for (int i = 0; i < N; i++)
                {
                    int k = start + i * 2;
                    if (Open(verts[k]) && Open(verts[k + 2])) continue; // the camera's side: no stand
                    tris[sub].AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                }
            }
            float y0 = floorY;
            float off = 0.35f;
            Band(off, y0, off, y0 + PodiumH, 1); // the podium's face
            Band(off, y0 + PodiumH, off + 0.6f, y0 + PodiumH, 0); // its walkway
            off += 0.6f;
            float top = y0 + PodiumH;
            for (int i = 0; i < Tiers; i++)
            {
                Band(off, top, off, top + TierRise, 1); // the riser
                top += TierRise;
                Band(off, top, off + TierStep, top, 0); // the seat
                off += TierStep;
            }
            float backH = 2.2f;
            Band(off, top, off, top + backH, 2); // the back wall's face
            Band(off, top + backH, off + 0.7f, top + backH, 1); // its top
            Band(off + 0.7f, top + backH, off + 0.7f, y0 - 0.5f, 2); // the outside
            var mesh = new Mesh { name = "ColosseumStone", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 3;
            for (int s = 0; s < 3; s++) mesh.SetTriangles(tris[s], s);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var stone = new GameObject("Stone");
            stone.transform.SetParent(transform, false);
            stone.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = stone.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { stoneLight, stoneMid, stoneDark };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            float backOff = off, topY = top;

            var decor = new GameObject("Decor").transform;
            decor.SetParent(transform, false);
            // Arches along the back wall (two rows), facing in.
            float per = Perimeter(backOff);
            int arches = Mathf.RoundToInt(per / 1.6f);
            for (int i = 0; i < arches; i++)
            {
                var p = Loop(i / (float)arches, backOff - 0.02f, out var n);
                if (Open(p)) continue;
                var rot = Quaternion.LookRotation(-n);
                for (int row = 0; row < 2; row++)
                {
                    var a = Shapes.Rounded("Arch", decor, new Vector3(p.x, topY + 0.35f + row * 0.95f, p.z), new Vector3(0.62f, 0.78f, 0.06f), 0.28f, shadow).transform;
                    a.rotation = rot;
                }
            }
            // The gates: dark arches through the podium, north and south, with bars and the sides' banners beside them.
            var bars = MaterialFactory.CreateSurface(new Color(0.35f, 0.3f, 0.26f), Color.black, 0.4f, 0.6f);
            var blue = MaterialFactory.Create(new Color(0.18f, 0.42f, 0.9f), new Color(0.05f, 0.12f, 0.3f));
            var red = MaterialFactory.Create(new Color(0.85f, 0.18f, 0.16f), new Color(0.3f, 0.04f, 0.03f));
            foreach (int side in new[] { -1, 1 })
            {
                var g = centre + new Vector3(0f, y0, side * (hd + 0.33f));
                var rot = Quaternion.LookRotation(new Vector3(0f, 0f, -side));
                var arch = Shapes.Rounded("Gate", decor, g + Vector3.up * 0.62f, new Vector3(2.4f, 1.3f, 0.08f), 0.5f, shadow).transform;
                arch.rotation = rot;
                for (int b = -4; b <= 4; b++)
                {
                    var bar = Shapes.Rounded("Bar", decor, g + new Vector3(b * 0.26f, 0.6f, -side * 0.05f), new Vector3(0.05f, 1.15f, 0.05f), 0.02f, bars).transform;
                    bar.rotation = rot;
                }
                var flag = side > 0 ? red : blue;
                foreach (int s in new[] { -1, 1 })
                {
                    var bn = Shapes.Rounded("GateBanner", decor, g + new Vector3(s * 1.55f, 0.75f, -side * 0.04f), new Vector3(0.5f, 1.2f, 0.03f), 0.02f, flag).transform;
                    bn.rotation = rot;
                }
            }
            // Torches on the podium wall.
            var wood = MaterialFactory.Create(new Color(0.3f, 0.22f, 0.15f), Color.black);
            var fire = MaterialFactory.Create(new Color(1f, 0.6f, 0.15f), new Color(3f, 1.4f, 0.3f));
            var core = MaterialFactory.Create(new Color(1f, 0.95f, 0.6f), new Color(3f, 2.6f, 1.2f));
            int torches = Mathf.RoundToInt(Perimeter(0.35f) / 5f);
            for (int i = 0; i < torches; i++)
            {
                var p = Loop((i + 0.5f) / torches, 0.3f, out var n);
                if (Open(p)) continue;
                if (Mathf.Abs(p.x - centre.x) < 2.2f) continue; // not in front of the gates
                Shapes.Primitive(PrimitiveType.Cylinder, "Torch", decor, new Vector3(p.x, y0 + PodiumH + 0.2f, p.z) - n * 0.05f, new Vector3(0.08f, 0.25f, 0.08f), wood);
                var f = Shapes.Primitive(PrimitiveType.Sphere, "Flame", decor, new Vector3(p.x, y0 + PodiumH + 0.55f, p.z) - n * 0.05f, new Vector3(0.22f, 0.32f, 0.22f), fire).transform;
                Shapes.Primitive(PrimitiveType.Sphere, "Core", f, new Vector3(0f, -0.1f, 0f), new Vector3(0.55f, 0.55f, 0.55f), core);
                flames.Add((f, (float)rng.NextDouble() * 10f));
            }
            // Banners on the top of the back wall: blue over the player's half, red over the rival's.
            var pole = MaterialFactory.Create(new Color(0.85f, 0.7f, 0.3f), new Color(0.2f, 0.15f, 0.02f));
            int flags = Mathf.RoundToInt(Perimeter(backOff) / 4f);
            for (int i = 0; i < flags; i++)
            {
                var p = Loop((i + 0.25f) / flags, backOff + 0.35f, out var n);
                if (Open(p)) continue;
                Shapes.Primitive(PrimitiveType.Cylinder, "Pole", decor, new Vector3(p.x, topY + backH + 0.7f, p.z), new Vector3(0.05f, 0.7f, 0.05f), pole);
                var holder = new GameObject("Banner").transform;
                holder.SetParent(decor, false);
                holder.position = new Vector3(p.x, topY + backH + 1.3f, p.z);
                holder.rotation = Quaternion.LookRotation(-n);
                Shapes.Rounded("Cloth", holder, new Vector3(0.28f, -0.25f, 0f), new Vector3(0.55f, 0.5f, 0.02f), 0.01f, p.z > centre.z ? red : blue);
                banners.Add((holder, (float)rng.NextDouble() * 6f));
            }
            // The emperor's box: over the rival's gate, a red box with gold trim and a canopy.
            var gold = MaterialFactory.Create(new Color(0.95f, 0.75f, 0.25f), new Color(0.4f, 0.28f, 0.05f));
            var boxAt = Loop(0.625f, 2.6f, out _); // the middle of the north edge
            boxAt.y = y0 + PodiumH + TierRise * 2f;
            Shapes.Rounded("Box", decor, boxAt + Vector3.up * 0.35f, new Vector3(3.2f, 0.7f, 1.6f), 0.06f, red);
            Shapes.Rounded("BoxTrim", decor, boxAt + new Vector3(0f, 0.72f, -0.78f), new Vector3(3.3f, 0.08f, 0.08f), 0.02f, gold);
            foreach (int s in new[] { -1, 1 }) Shapes.Primitive(PrimitiveType.Cylinder, "Column", decor, boxAt + new Vector3(s * 1.5f, 1.4f, -0.7f), new Vector3(0.12f, 0.7f, 0.12f), gold);
            Shapes.Rounded("Canopy", decor, boxAt + new Vector3(0f, 2.15f, -0.2f), new Vector3(3.5f, 0.12f, 1.9f), 0.04f, red);
            Shapes.Rounded("Throne", decor, boxAt + new Vector3(0f, 0.95f, 0.2f), new Vector3(0.6f, 0.5f, 0.5f), 0.05f, gold);
            // The torches' flames and the banners move: left out of the merge.
            var moving = new HashSet<Transform>();
            foreach (var (t, _) in flames) if (t != null) moving.Add(t);
            foreach (var (t, _) in banners) if (t != null) moving.Add(t);
            StaticMerge.Combine(decor.gameObject, t => StaticMerge.Under(t, moving));
            foreach (var r in decor.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

            MakeCrowd(rng, top: topY);
            MakeMurmur();
        }

        // ---------- The crowd ----------

        private void MakeCrowd(System.Random rng, float top)
        {
            var blues = new[] { new Color(0.22f, 0.5f, 1f), new Color(0.35f, 0.62f, 1f), new Color(0.16f, 0.38f, 0.82f), new Color(0.55f, 0.75f, 1f) };
            var reds = new[] { new Color(1f, 0.32f, 0.3f), new Color(0.88f, 0.22f, 0.2f), new Color(1f, 0.5f, 0.42f), new Color(0.75f, 0.16f, 0.15f) };
            var skins = new[] { new Color(0.95f, 0.78f, 0.64f), new Color(0.85f, 0.64f, 0.48f), new Color(0.62f, 0.44f, 0.3f), new Color(0.4f, 0.28f, 0.2f) };
            // A spectator: a body and a head, one mesh per skin (the body's colour is the batch's material).
            // Plain boxes (24 corners each): a thousand of them stay cheap on a phone, and suit the robots' blocky world.
            var bodyMesh = Cube();
            var headMesh = Cube();
            Material Mat(Color c) { var m = MaterialFactory.Create(c, c * 0.08f); m.enableInstancing = true; return m; }
            var skinMats = new Material[skins.Length];
            for (int i = 0; i < skins.Length; i++) skinMats[i] = Mat(skins[i]);
            var bodyMats = new Material[2, 4];
            for (int i = 0; i < 4; i++) { bodyMats[0, i] = Mat(blues[i]); bodyMats[1, i] = Mat(reds[i]); }
            var headBatches = new Batch[skins.Length];
            for (int i = 0; i < skins.Length; i++) { headBatches[i] = new Batch { mesh = headMesh, mat = skinMats[i], isHead = true }; batches.Add(headBatches[i]); }
            var bodyBatches = new Batch[2, 4];
            for (int t = 0; t < 2; t++) for (int i = 0; i < 4; i++) { bodyBatches[t, i] = new Batch { mesh = bodyMesh, mat = bodyMats[t, i] }; batches.Add(bodyBatches[t, i]); }

            float off = 0.35f + 0.6f;
            float y = floorY + PodiumH;
            for (int tier = 0; tier < Tiers; tier++)
            {
                y += TierRise;
                float seatOff = off + TierStep * 0.45f;
                float per = Perimeter(seatOff);
                int n = Mathf.RoundToInt(per / 0.42f);
                for (int i = 0; i < n; i++)
                {
                    float t = (i + (tier % 2) * 0.5f) / n;
                    var p = Loop(t, seatOff, out var nrm);
                    if (Open(p)) continue;
                    // Gaps: the gates' fronts and the emperor's box, and here and there an empty seat.
                    if (Mathf.Abs(p.x - centre.x) < 1.8f && tier < 2) continue;
                    if (p.z > centre.z && Mathf.Abs(p.x - centre.x) < 2f && tier >= 1 && tier <= 3) continue;
                    if (rng.NextDouble() < 0.06) continue;
                    int team = p.z > centre.z ? 1 : 0;
                    if (Mathf.Abs(p.z - centre.z) < hd * 0.4f && rng.NextDouble() < 0.35) team = 1 - team; // the long sides mixed a little
                    int shade = rng.Next(4), skin = rng.Next(skins.Length);
                    float ang = Mathf.Atan2(p.z - centre.z, p.x - centre.x);
                    var facing = Quaternion.LookRotation(-nrm).eulerAngles.y;
                    var b = bodyBatches[team, shade];
                    b.pos.Add(new Vector3(p.x, y + 0.18f, p.z)); b.phase.Add((float)rng.NextDouble() * 10f); b.angle.Add(ang); b.side.Add(team);
                    var h = headBatches[skin];
                    h.pos.Add(new Vector3(p.x, y + 0.46f, p.z)); h.phase.Add(b.phase[b.phase.Count - 1]); h.angle.Add(ang); h.side.Add(team);
                    // Facing the courtyard (kept as the y rotation in the batch's own list).
                    b.facing.Add(facing); h.facing.Add(facing);
                }
                off += TierStep;
            }
            foreach (var bt in batches) bt.m = new Matrix4x4[bt.pos.Count];
        }

        private static Mesh cube;

        private static Mesh Cube()
        {
            if (cube != null) return cube;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = go.GetComponent<MeshFilter>().sharedMesh;
            Destroy(go);
            return cube;
        }

        private readonly Matrix4x4[] chunk = new Matrix4x4[1023];

        private void LateUpdate()
        {
            float dt = Time.deltaTime, time = Time.time;
            for (int s = 0; s < 2; s++) excite[s] = Mathf.MoveTowards(excite[s], 0f, dt * 0.25f);
            if (wave) waveT += dt * 1.6f;
            var headScale = Vector3.one * 0.2f;
            var bodyScale = new Vector3(0.3f, 0.36f, 0.22f);
            foreach (var b in batches)
            {
                bool head = b.isHead;
                for (int i = 0; i < b.pos.Count; i++)
                {
                    float ex = excite[b.side[i]];
                    float jump = Mathf.Max(0f, Mathf.Sin(time * (6f + ex * 4f) + b.phase[i])) * (0.02f + ex * 0.22f);
                    if (wave) jump += Mathf.Pow(Mathf.Max(0f, Mathf.Cos(b.angle[i] - waveT)), 8f) * 0.3f; // the wave rolling round
                    var rot = Quaternion.Euler(0f, b.facing[i], Mathf.Sin(time * 2f + b.phase[i]) * (2f + ex * 8f));
                    b.m[i] = Matrix4x4.TRS(b.pos[i] + Vector3.up * jump, rot, head ? headScale : bodyScale);
                }
                for (int k = 0; k < b.m.Length; k += 1023)
                {
                    int n = Mathf.Min(1023, b.m.Length - k);
                    System.Array.Copy(b.m, k, chunk, 0, n);
                    Graphics.DrawMeshInstanced(b.mesh, 0, b.mat, chunk, n, null, ShadowCastingMode.Off, true);
                }
            }
            foreach (var (t, seed) in flames)
            {
                if (t == null) continue;
                float f = 1f + Mathf.Sin(time * 13f + seed) * 0.08f + Mathf.Sin(time * 23f + seed * 2f) * 0.06f;
                t.localScale = new Vector3(0.22f * f, 0.32f * (2f - f), 0.22f * f);
            }
            foreach (var (t, seed) in banners)
                if (t != null) t.localRotation = Quaternion.Euler(0f, t.localEulerAngles.y, Mathf.Sin(time * 1.7f + seed) * 6f);
            if (murmur != null)
            {
                float target = (0.1f + Mathf.Max(excite[0], excite[1]) * 0.35f + (wave ? 0.12f : 0f)) * (Data.SaveData.Sound ? 1f : 0f);
                murmur.volume = Mathf.MoveTowards(murmur.volume, target, dt * 0.6f);
                murmur.pitch = 1f + Mathf.Max(excite[0], excite[1]) * 0.08f;
            }
        }

        // ---------- The crowd's murmur: soft filtered noise that rises and falls ----------

        private void MakeMurmur()
        {
            const int rate = 22050;
            int len = rate * 6;
            var data = new float[len];
            var r = new System.Random(5);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < len; i++)
            {
                float white = (float)r.NextDouble() * 2f - 1f;
                lp1 += (white - lp1) * 0.08f; // voices: a low hum of many
                lp2 += (lp1 - lp2) * 0.2f;
                float t = i / (float)rate;
                float swell = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 2f / 6f) * Mathf.Sin(t * 1.3f + 1f);
                data[i] = lp2 * 2.2f * swell;
            }
            // Loop seamlessly: cross-fade the last half second into the start.
            int fade = rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = Mathf.Lerp(data[len - fade + i], data[i], k);
            }
            var clip = AudioClip.Create("CrowdMurmur", len - fade, 1, rate, false);
            var trimmed = new float[len - fade];
            System.Array.Copy(data, trimmed, len - fade);
            clip.SetData(trimmed, 0);
            murmur = gameObject.AddComponent<AudioSource>();
            murmur.clip = clip;
            murmur.loop = true;
            murmur.volume = 0f;
            murmur.spatialBlend = 0f;
            murmur.Play();
        }
    }
}
