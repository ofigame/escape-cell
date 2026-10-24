using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A stand-in for a robot in the duel's big moments: a copy of its pieces (meshes and materials, where they stand)
    /// while the robot itself is hidden, free to leap and flip across the floor or to burst into its parts in slow
    /// motion and come back together — without fighting the robot's own animation. Gone when the moment is over (the
    /// robot shown again).
    /// </summary>
    public class StuntDouble : MonoBehaviour
    {
        private readonly List<Renderer> hidden = new List<Renderer>();
        private readonly List<Mesh> baked = new List<Mesh>();
        private Transform source;
        private float bornAt;
        private readonly List<(Transform t, Vector3 pos, Quaternion rot)> parts = new List<(Transform, Vector3, Quaternion)>();

        /// <summary>A copy of every mesh under <paramref name="model"/>, the model hidden meanwhile.</summary>
        public static StuntDouble Make(Transform model)
        {
            var go = new GameObject("StuntDouble");
            go.transform.SetPositionAndRotation(model.position, model.rotation);
            var d = go.AddComponent<StuntDouble>();
            d.source = model;
            d.bornAt = Time.realtimeSinceStartup;
            foreach (var sk in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!sk.enabled || sk.sharedMesh == null) continue;
                var baked = new Mesh();
                sk.BakeMesh(baked, true); // (the body's scale baked in: the part itself unscaled, never twice)
                var p = new GameObject("Part").transform;
                p.SetParent(go.transform, false);
                p.SetPositionAndRotation(sk.transform.position, sk.transform.rotation);
                p.localScale = Divide(Vector3.one, go.transform.lossyScale);
                p.gameObject.AddComponent<MeshFilter>().sharedMesh = baked;
                var copy = p.gameObject.AddComponent<MeshRenderer>();
                copy.sharedMaterials = sk.sharedMaterials;
                copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.parts.Add((p, p.localPosition, p.localRotation));
                d.baked.Add(baked);
                sk.enabled = false;
                d.hidden.Add(sk);
            }
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var p = new GameObject("Part").transform;
                p.SetParent(go.transform, false);
                p.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                p.localScale = Divide(r.transform.lossyScale, go.transform.lossyScale);
                p.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var copy = p.gameObject.AddComponent<MeshRenderer>();
                copy.sharedMaterials = r.sharedMaterials;
                copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.parts.Add((p, p.localPosition, p.localRotation));
                r.enabled = false;
                d.hidden.Add(r);
            }
            return d;
        }

        private static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / Mathf.Max(1e-4f, b.x), a.y / Mathf.Max(1e-4f, b.y), a.z / Mathf.Max(1e-4f, b.z));

        /// <summary>The robot shown again, the stand-in gone.</summary>
        public void Done()
        {
            foreach (var r in hidden) if (r != null) r.enabled = true;
            Destroy(gameObject);
        }

        /// <summary>
        /// Never left behind: gone with the robot it stands in for (a live knock-down can take the rival away mid-moment),
        /// and in any case after a few seconds (no moment lasts that long).
        /// </summary>
        private void Update()
        {
            if (source == null || Time.realtimeSinceStartup - bornAt > 6f) Done();
        }

        private void OnDestroy()
        {
            foreach (var r in hidden) if (r != null) r.enabled = true; // never leave a robot invisible
            foreach (var m in baked) if (m != null) Destroy(m);
        }

        /// <summary>
        /// A leap from where it stands to <paramref name="to"/> (an arc <paramref name="height"/> high) with a stunt on the
        /// way: 0 a front flip, 1 a double spin, 2 a big jump and stomp, 3 a side roll. Real seconds.
        /// </summary>
        public IEnumerator Leap(Vector3 to, float height, float seconds, int stunt)
        {
            var from = transform.position;
            var baseRot = transform.rotation;
            var dir = to - from; dir.y = 0f;
            var face = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : baseRot;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / seconds);
                float k = t * t * (3f - 2f * t);
                var p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(t * Mathf.PI) * (stunt == 2 ? height * 1.6f : height);
                transform.position = p;
                Quaternion spin = stunt switch
                {
                    0 => Quaternion.Euler(360f * k, 0f, 0f),
                    1 => Quaternion.Euler(0f, 720f * k, 0f),
                    3 => Quaternion.Euler(0f, 0f, -360f * k),
                    _ => Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * -20f, 0f, 0f),
                };
                transform.rotation = face * spin;
                yield return null;
            }
        }

        /// <summary>
        /// Bursts into its parts (flying out from <paramref name="centre"/>, spinning), hangs a moment, and comes back
        /// together. Real seconds: out, hold, back.
        /// </summary>
        public IEnumerator Shatter(Vector3 centre, float outSeconds = 0.3f, float hold = 0.18f, float backSeconds = 0.38f)
        {
            var rng = new System.Random(parts.Count * 7919 + (int)(Time.realtimeSinceStartup * 1000f));
            var flights = new List<(Vector3 off, Vector3 spin)>();
            foreach (var p in parts)
            {
                var away = p.t.position - centre;
                away.y = Mathf.Abs(away.y) + 0.3f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.up;
                away = away.normalized * (0.8f + (float)rng.NextDouble() * 1.2f);
                var spin = new Vector3((float)rng.NextDouble() * 540f - 270f, (float)rng.NextDouble() * 540f - 270f, (float)rng.NextDouble() * 540f - 270f);
                flights.Add((transform.InverseTransformDirection(away), spin));
            }
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / outSeconds);
                float k = 1f - (1f - t) * (1f - t);
                Pose(flights, k);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / backSeconds);
                float k = 1f - t * t * (3f - 2f * t);
                Pose(flights, k);
                yield return null;
            }
            Pose(flights, 0f);
        }

        private void Pose(List<(Vector3 off, Vector3 spin)> flights, float k)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                var (t, pos, rot) = parts[i];
                if (t == null) continue;
                t.localPosition = pos + flights[i].off * k;
                t.localRotation = Quaternion.Euler(flights[i].spin * k) * rot;
            }
        }
    }
}
