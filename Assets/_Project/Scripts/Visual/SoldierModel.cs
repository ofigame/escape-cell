using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A challenge soldier, light enough for a hundred and eighty a side on a phone: one combined body mesh per kind
    /// (legs, torso, head with a glowing visor; heavy ones add shoulder plates, a crest and a shield) and a swinging
    /// weapon arm (sword, spear or bow), both shared by every soldier of that kind, in the side's colours. It walks with a
    /// bob, raises its weapon to strike (<see cref="SetRaise"/>) and flashes white when hit. One metre tall before
    /// scaling.
    /// </summary>
    public class SoldierModel : MonoBehaviour
    {
        private const int Slots = 5; // plates (team), frame, light metal, visor (team glow), wood

        private static readonly Dictionary<GroupKind, (Mesh body, Mesh arm)> meshes = new Dictionary<GroupKind, (Mesh, Mesh)>();
        private static readonly Dictionary<Color, Material[]> materials = new Dictionary<Color, Material[]>();
        private static Material flashMat;

        private Transform arm, bodyT;
        private MeshRenderer bodyR, armR;
        private Material[] own;
        private float flash, raise, raiseShown;
        private Vector3 lastPos;
        private float walk;

        public static SoldierModel Build(Transform parent, GroupKind kind, Color team)
        {
            if (!meshes.TryGetValue(kind, out var m)) { m = MakeMeshes(kind); meshes[kind] = m; }
            if (!materials.TryGetValue(team, out var mats))
            {
                mats = new[]
                {
                    MaterialFactory.Create(team, team * 0.15f),
                    MaterialFactory.Create(new Color(0.16f, 0.17f, 0.22f), Color.black),
                    MaterialFactory.Create(new Color(0.82f, 0.85f, 0.9f), new Color(0.05f, 0.05f, 0.06f)),
                    MaterialFactory.Create(Color.Lerp(team, Color.white, 0.5f), team * 2.2f),
                    MaterialFactory.Create(new Color(0.55f, 0.36f, 0.2f), Color.black),
                };
                materials[team] = mats;
            }
            if (flashMat == null) flashMat = MaterialFactory.Create(Color.white, new Color(2.5f, 2.5f, 2.5f));

            var root = new GameObject("Soldier").transform;
            root.SetParent(parent, false);
            var s = root.gameObject.AddComponent<SoldierModel>();
            s.own = mats;
            s.bodyT = new GameObject("Body").transform;
            s.bodyT.SetParent(root, false);
            s.bodyT.gameObject.AddComponent<MeshFilter>().sharedMesh = m.body;
            s.bodyR = s.bodyT.gameObject.AddComponent<MeshRenderer>();
            s.bodyR.sharedMaterials = Pick(mats, m.body.subMeshCount);
            s.bodyR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.arm = new GameObject("Arm").transform;
            s.arm.SetParent(s.bodyT, false);
            s.arm.localPosition = new Vector3(0.25f, 0.74f, 0f);
            s.arm.gameObject.AddComponent<MeshFilter>().sharedMesh = m.arm;
            s.armR = s.arm.gameObject.AddComponent<MeshRenderer>();
            s.armR.sharedMaterials = Pick(mats, m.arm.subMeshCount);
            s.armR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.lastPos = root.position;
            return s;
        }

        private static Material[] Pick(Material[] mats, int n)
        {
            var a = new Material[n];
            for (int i = 0; i < n; i++) a[i] = mats[Mathf.Min(i, mats.Length - 1)];
            return a;
        }

        /// <summary>The weapon arm raised for a blow: 0 down … 1 up (above 1, a bigger swing).</summary>
        public void SetRaise(float k) => raise = k;

        public void Flash() => flash = 0.12f;

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            // Walking: a bob and a little sway while it moves.
            var p = transform.position;
            float moved = new Vector2(p.x - lastPos.x, p.z - lastPos.z).magnitude;
            lastPos = p;
            if (moved > 0.0005f) walk += moved * 14f;
            else walk = Mathf.MoveTowards(walk, Mathf.Round(walk / Mathf.PI) * Mathf.PI, dt * 6f);
            bodyT.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(walk)) * 0.05f, 0f);
            bodyT.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(walk) * 4f);
            // The weapon: back over the shoulder as it rises, then down through the strike.
            raiseShown = Mathf.Lerp(raiseShown, raise, dt * 18f);
            arm.localRotation = Quaternion.Euler(-raiseShown * 110f, 0f, 0f);
            if (flash > 0f)
            {
                flash -= dt;
                bool on = flash > 0f;
                var body = bodyR.sharedMaterials;
                if (on != (body[0] == flashMat))
                {
                    bodyR.sharedMaterials = on ? Fill(flashMat, body.Length) : Pick(own, body.Length);
                    armR.sharedMaterials = on ? Fill(flashMat, armR.sharedMaterials.Length) : Pick(own, armR.sharedMaterials.Length);
                }
            }
        }

        private static Material[] Fill(Material m, int n)
        {
            var a = new Material[n];
            for (int i = 0; i < n; i++) a[i] = m;
            return a;
        }

        // ---------- The shared meshes ----------

        private static (Mesh body, Mesh arm) MakeMeshes(GroupKind kind)
        {
            bool heavy = Army.Heavy(kind);
            int fam = Army.Family(kind);
            var parts = new List<(Mesh mesh, Matrix4x4 at, int slot)>();
            void Box(Vector3 pos, Vector3 size, int slot, float r = 0.03f, Quaternion? rot = null) =>
                parts.Add((MeshFactory.RoundedBox(size, r), Matrix4x4.TRS(pos, rot ?? Quaternion.identity, Vector3.one), slot));
            void Prim(PrimitiveType type, Vector3 pos, Vector3 scale, int slot, Quaternion? rot = null) =>
                parts.Add((PrimitiveMesh(type), Matrix4x4.TRS(pos, rot ?? Quaternion.identity, scale), slot));

            float w = heavy ? 1.15f : 1f;
            // Legs and feet.
            foreach (float x in new[] { -0.09f, 0.09f })
            {
                Box(new Vector3(x * w, 0.2f, 0f), new Vector3(0.12f * w, 0.36f, 0.14f), 1);
                Box(new Vector3(x * w, 0.04f, 0.03f), new Vector3(0.14f * w, 0.08f, 0.2f), 1);
            }
            // Torso: plates over a dark frame, a belt.
            Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.4f * w, 0.4f, 0.26f * w), 0, 0.06f);
            Box(new Vector3(0f, 0.42f, 0f), new Vector3(0.42f * w, 0.06f, 0.28f * w), 1);
            Box(new Vector3(0f, 0.62f, 0.135f * w), new Vector3(0.22f * w, 0.2f, 0.02f), 2, 0.01f);
            // Left arm, hanging.
            Box(new Vector3(-0.25f * w, 0.58f, 0f), new Vector3(0.1f, 0.32f, 0.1f), 1);
            // Head and visor.
            Box(new Vector3(0f, 0.93f, 0f), new Vector3(0.28f, 0.24f, 0.26f), 2, 0.07f);
            Box(new Vector3(0f, 0.94f, 0.13f), new Vector3(0.21f, 0.06f, 0.02f), 3, 0.01f);
            if (heavy)
            {
                // Shoulder plates, a crest and a round shield on the left arm.
                foreach (float x in new[] { -0.24f, 0.24f }) Box(new Vector3(x * w, 0.79f, 0f), new Vector3(0.16f, 0.08f, 0.2f), 0, 0.03f);
                Box(new Vector3(0f, 1.08f, -0.02f), new Vector3(0.05f, 0.1f, 0.22f), 0, 0.02f);
                Prim(PrimitiveType.Cylinder, new Vector3(-0.34f * w, 0.56f, 0.06f), new Vector3(0.34f, 0.02f, 0.34f), 0, Quaternion.Euler(0f, 0f, 90f));
                Prim(PrimitiveType.Cylinder, new Vector3(-0.355f * w, 0.56f, 0.06f), new Vector3(0.14f, 0.022f, 0.14f), 2, Quaternion.Euler(0f, 0f, 90f));
            }
            else
            {
                // An antenna.
                Box(new Vector3(0.08f, 1.1f, -0.04f), new Vector3(0.02f, 0.12f, 0.02f), 1, 0.008f);
            }
            var body = Combine(parts);

            // The weapon arm (its pivot at the shoulder): the arm down, the weapon in the hand pointing forward/up.
            parts.Clear();
            Box(new Vector3(0f, -0.16f, 0f), new Vector3(0.1f, 0.32f, 0.1f), 1);
            var hand = new Vector3(0f, -0.32f, 0.06f);
            if (fam == 0)
            {
                Box(hand + new Vector3(0f, 0f, 0.02f), new Vector3(0.06f, 0.04f, 0.16f), 4, 0.01f);
                Box(hand + new Vector3(0f, 0f, 0.1f), new Vector3(0.16f, 0.04f, 0.04f), 1, 0.01f);
                Box(hand + new Vector3(0f, 0f, 0.36f), new Vector3(0.05f, heavy ? 0.1f : 0.07f, 0.46f), 2, 0.012f);
            }
            else if (fam == 1)
            {
                Prim(PrimitiveType.Cylinder, hand + new Vector3(0f, 0f, 0.32f), new Vector3(0.035f, 0.55f, 0.035f), 4, Quaternion.Euler(90f, 0f, 0f));
                Box(hand + new Vector3(0f, 0f, 0.9f), new Vector3(0.06f, 0.03f, 0.16f), 2, 0.01f);
            }
            else
            {
                for (int i = -2; i <= 2; i++)
                    Box(hand + new Vector3(0f, i * 0.1f, 0.12f - Mathf.Abs(i) * 0.035f), new Vector3(0.035f, 0.12f, 0.035f), 4, 0.01f, Quaternion.Euler(i * -16f, 0f, 0f));
                Box(hand + new Vector3(0f, 0f, 0.02f), new Vector3(0.01f, 0.46f, 0.01f), 2, 0.004f);
            }
            var arm = Combine(parts);
            return (body, arm);
        }

        private static readonly Dictionary<PrimitiveType, Mesh> primitives = new Dictionary<PrimitiveType, Mesh>();

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            if (primitives.TryGetValue(type, out var m)) return m;
            var go = GameObject.CreatePrimitive(type);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Destroy(go);
            primitives[type] = m;
            return m;
        }

        /// <summary>The parts merged into one mesh, one sub-mesh per material slot used (in slot order, empty ones kept).</summary>
        private static Mesh Combine(List<(Mesh mesh, Matrix4x4 at, int slot)> parts)
        {
            int maxSlot = 0;
            foreach (var p in parts) maxSlot = Mathf.Max(maxSlot, p.slot);
            var perSlot = new List<CombineInstance>();
            for (int slot = 0; slot <= maxSlot; slot++)
            {
                var list = new List<CombineInstance>();
                foreach (var p in parts) if (p.slot == slot) list.Add(new CombineInstance { mesh = p.mesh, transform = p.at });
                var m = new Mesh();
                if (list.Count > 0) m.CombineMeshes(list.ToArray(), true, true);
                else m.vertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero }; // a slot with nothing: a degenerate triangle
                if (list.Count == 0) m.triangles = new[] { 0, 1, 2 };
                perSlot.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
            }
            var mesh = new Mesh { name = "Soldier" };
            mesh.CombineMeshes(perSlot.ToArray(), false, false);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
