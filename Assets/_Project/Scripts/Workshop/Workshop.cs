using System.Collections.Generic;
using System.Linq;
using BrickMuseum.Build;
using BrickMuseum.Data;
using BrickMuseum.World;
using UnityEngine;

namespace BrickMuseum.Workshop
{
    /// <summary>One spot of the model being built.</summary>
    public sealed class Target
    {
        public Piece p;
        public GameObject ghost;
        public bool placed, available;
        public List<Target> deps = new List<Target>();
        public Vector3 pos, center;
        public Quaternion rot;
    }

    /// <summary>
    /// The workbench: table on a wooden deck, a 16×16 base plate and the model being built (ghost spots that unlock as the
    /// pieces under them are placed). Handles the piece in the player's hand: snapping onto a matching free spot.
    /// </summary>
    public sealed class Bench
    {
        public readonly Transform root, modelRoot;
        public ModelDef Model { get; private set; }
        public readonly List<Target> targets = new List<Target>();
        public Target Hint { get; private set; }
        public bool Done;

        private GameObject held;
        private int heldSteps = -1;
        private Piece heldKind;
        private readonly List<Material> heldMats = new List<Material>();
        private readonly List<(Transform t, float y, float time)> drops = new List<(Transform, float, float)>();
        private readonly List<(Transform t, float time)> presses = new List<(Transform, float)>();

        public Bench(Transform parent)
        {
            root = new GameObject("Workbench").transform;
            root.SetParent(parent, false);
            modelRoot = new GameObject("Model").transform;
            modelRoot.SetParent(root, false);
            BuildFurniture();
        }

        private void BuildFurniture()
        {
            var wood = Mats.Lit(new Color(0.66f, 0.46f, 0.26f), 0.3f);
            Box("Table", new Vector3(0, -0.4f - 0.8f, 0), new Vector3(38, 1.6f, 26), wood, true);
            foreach (var (x, z) in new[] { (-17f, -11f), (17f, -11f), (-17f, 11f), (17f, 11f) })
                Box("Leg", new Vector3(x, (-2f + Landscape.Ground) / 2f, z), new Vector3(1.6f, -2f - Landscape.Ground, 1.6f), wood, true);
            // deck planks
            var plankA = Mats.Lit(new Color(0.58f, 0.42f, 0.28f), 0.15f);
            var plankB = Mats.Lit(new Color(0.53f, 0.38f, 0.25f), 0.15f);
            for (int i = 0; i < 24; i++)
                Box("Plank", new Vector3(-57.5f + i * 5f, Landscape.Ground - 0.5f + 0.4f, 10f), new Vector3(4.8f, 1f, 110f), i % 2 == 0 ? plankA : plankB, false);
            // the base plate
            var plate = PieceFactory.Create(Piece.Make("brick", "green", 0, 0, 0, 16, 16, 1), PieceLook.Solid, root);
            plate.name = "BasePlate";
            plate.transform.localPosition = new Vector3(0, -Piece.Plate, 0);
            foreach (var r in plate.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Mats.Lit(new Color(0.36f, 0.66f, 0.38f), 0.35f);
        }

        private void Box(string name, Vector3 center, Vector3 size, Material m, bool shadows)
        {
            var b = new MeshBuilder();
            b.Box(center, size);
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Clear()
        {
            foreach (Transform c in modelRoot) Object.Destroy(c.gameObject);
            modelRoot.localRotation = Quaternion.identity;
            targets.Clear();
            Model = null;
            Hint = null;
            Done = false;
            drops.Clear(); presses.Clear();
        }

        public void Begin(ModelDef m)
        {
            Clear();
            Model = m;
            foreach (var p in m.pieces)
            {
                var t = new Target { p = p };
                t.ghost = PieceFactory.Create(p, PieceLook.Ghost, modelRoot);
                PieceFactory.Place(t.ghost.transform, p, m);
                t.pos = t.ghost.transform.position;
                t.rot = t.ghost.transform.rotation;
                t.center = t.pos + t.rot * new Vector3(0, p.CenterY, p.OnWall ? -0.4f : 0f);
                targets.Add(t);
            }
            // Unlock rules: `need` = everything below that plate height first (a disc never waits for the other discs);
            // otherwise the pieces right under it; an overhang waits for the whole layer below; stickers wait for their surface.
            var solid = targets.Where(a => a.p.t != "sticker").ToList();
            foreach (var b in targets)
            {
                var p = b.p;
                if (p.need >= 0) { b.deps = solid.Where(a => a != b && a.p.y < p.need && (p.t == "sticker" || a.p.t != "disc")).ToList(); continue; }
                if (p.y == 0) continue;
                var below = solid.Where(a => a.p.y + a.p.h == p.y && a.p.t != "disc").ToList();
                var over = below.Where(a => Overlap(a.p, p)).ToList();
                b.deps = over.Count > 0 ? over : below.Count > 0 ? below : solid.Where(a => a.p.y < p.y).ToList();
            }
            Refresh();
        }

        private static bool Overlap(Piece a, Piece b) => a.x < b.x + b.fw && b.x < a.x + a.fw && a.z < b.z + b.fd && b.z < a.z + a.fd;

        private void Refresh()
        {
            foreach (var t in targets)
            {
                t.available = !t.placed && t.deps.All(d => d.placed);
                t.ghost.SetActive(t.available);
            }
        }

        public int PlacedCount => targets.Count(t => t.placed);
        public bool Complete => targets.Count > 0 && targets.All(t => t.placed);

        // ---------- the piece in hand ----------

        public void Hold(Piece kind)
        {
            Drop();
            heldKind = kind;
            heldSteps = -1;
        }

        private void SetHeld(int steps)
        {
            if (held != null && heldSteps == steps) return;
            if (held != null) Object.Destroy(held);
            held = PieceFactory.Create(heldKind, PieceLook.Drag, root);
            held.transform.rotation = PieceFactory.Rotation(steps);
            heldSteps = steps;
            heldMats.Clear();
            // only the private copies made for the hand: never shared materials (a text's font material would go too)
            foreach (var r in held.GetComponentsInChildren<MeshRenderer>()) if (r.GetComponent<TMPro.TMP_Text>() == null) heldMats.Add(r.sharedMaterial);
        }

        /// <summary>
        /// Moves the held piece under the pointer (screen px). Returns the spot it snaps to (a free spot of the same kind
        /// near the pointer), and sets wrong to a nearby free spot of another kind when there is no match.
        /// </summary>
        public Target MoveHeld(Camera cam, Vector2 screen, float snapPx, out Target wrong)
        {
            Target best = null, near = null;
            float bd = snapPx, wd = snapPx;
            foreach (var t in targets)
            {
                if (!t.available) continue;
                var s = cam.WorldToScreenPoint(t.center);
                if (s.z < 0) continue;
                float d = Vector2.Distance(new Vector2(s.x, s.y), screen);
                if (t.p.key == heldKind.key) { if (d < bd) { bd = d; best = t; } }
                else if (d < wd) { wd = d; near = t; }
            }
            wrong = best == null ? near : null;
            if (best != null)
            {
                int flip = best.p.IsSymmetric && best.p.w != heldKind.w ? 1 : 0;
                SetHeld((best.p.steps + flip) % 4);
                held.transform.position = best.pos + Vector3.up * 0.3f;
                foreach (var m in heldMats) Mats.Tint(m, false);
            }
            else
            {
                SetHeld(0);
                float y = (Model != null ? Model.height : 2f) * 0.5f + 1f;
                var ray = cam.ScreenPointToRay(screen);
                if (new Plane(Vector3.up, new Vector3(0, y, 0)).Raycast(ray, out float dist))
                {
                    var hit = ray.GetPoint(dist);
                    held.transform.position = new Vector3(Mathf.Clamp(hit.x, -18, 18), y, Mathf.Clamp(hit.z, -12, 12));
                }
                foreach (var m in heldMats) Mats.Tint(m, wrong != null);
            }
            return best;
        }

        public void Drop()
        {
            if (held != null) Object.Destroy(held);
            held = null;
            foreach (var m in heldMats) Object.Destroy(m);
            heldMats.Clear();
        }

        public void PlaceAt(Target t)
        {
            t.placed = true;
            var go = PieceFactory.Create(t.p, PieceLook.Solid, modelRoot);
            PieceFactory.Place(go.transform, t.p, Model);
            if (t.p.t == "sticker") { go.transform.localScale = Vector3.one * 1.25f; presses.Add((go.transform, 0f)); }
            else { go.transform.position += Vector3.up * 0.5f; drops.Add((go.transform, t.pos.y, 0f)); }
            if (Hint != null && Hint.p.key == t.p.key) ClearHint();
            Refresh();
        }

        // ---------- hint ----------

        public Target PickHint()
        {
            ClearHint();
            var options = targets.Where(t => t.available).ToList();
            if (options.Count == 0) return null;
            Hint = options[Random.Range(0, options.Count)];
            foreach (var r in Hint.ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Mats.Hint;
            return Hint;
        }

        public void ClearHint()
        {
            if (Hint != null && Hint.ghost != null)
            {
                Object.Destroy(Hint.ghost);
                Hint.ghost = PieceFactory.Create(Hint.p, PieceLook.Ghost, modelRoot);
                PieceFactory.Place(Hint.ghost.transform, Hint.p, Model);
                Hint.ghost.SetActive(Hint.available);
            }
            Hint = null;
        }

        public void Tick(float dt)
        {
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var (t, y, time) = drops[i];
                if (t == null) { drops.RemoveAt(i); continue; }
                time += dt;
                float k = Mathf.Min(1f, time / 0.12f);
                var p = t.position; p.y = y + 0.5f * (1f - k); t.position = p;
                if (k >= 1f) drops.RemoveAt(i); else drops[i] = (t, y, time);
            }
            for (int i = presses.Count - 1; i >= 0; i--)
            {
                var (t, time) = presses[i];
                if (t == null) { presses.RemoveAt(i); continue; }
                time += dt;
                float k = Mathf.Min(1f, time / 0.18f);
                t.localScale = Vector3.one * (1.25f - 0.25f * k);
                if (k >= 1f) presses.RemoveAt(i); else presses[i] = (t, time);
            }
            if (Done) modelRoot.Rotate(0f, 34f * dt, 0f);
        }
    }

    /// <summary>Orbit camera around the workbench (drag turns, pinch / wheel zooms).</summary>
    public sealed class OrbitCam
    {
        public float yaw = 200f, pitch = 24f, distance = 30f;
        public Vector3 target;
        public float minDist = 8f, maxDist = 90f;

        public void Fit(Camera cam, float sx, float sz, float height)
        {
            float r = Mathf.Max(sx, Mathf.Max(sz, height)) / 2f + 2.5f;
            distance = r / (Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Min(1f, cam.aspect)) * 1.05f;
            target = new Vector3(0, height * 0.4f, 0);
            yaw = 200f; pitch = 24f;
        }

        public void Turn(Vector2 deltaPx)
        {
            yaw += deltaPx.x * 0.25f;
            pitch = Mathf.Clamp(pitch - deltaPx.y * 0.2f, 6f, 78f);
        }

        public void Zoom(float factor) => distance = Mathf.Clamp(distance * factor, minDist, maxDist);

        public void Apply(Camera cam)
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.position = target + rot * Vector3.back * distance;
            cam.transform.rotation = rot;
        }
    }
}
