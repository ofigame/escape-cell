using System.Collections.Generic;
using SquashBot.Audio;
using SquashBot.Gameplay;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The elemental rains on screen (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 5.4): the target circle on the floor in the
    /// element's colour (pulsing faster as the drops near; lightning's sliding after its target), each drop falling from
    /// the sky to land at the rules' moment (a snowball, a fireball, a bolt), the burst where it lands, the sky's tint
    /// while it rains.
    /// </summary>
    public class RainViewV2
    {
        private class Shown { public ElementRain rain; public LineRenderer ring; public int spawned; }
        private class Falling { public Transform t; public Vector3 from, to; public float start, end; public ElementKind kind; }

        private readonly GameManager game;
        private readonly System.Func<float, Vector3> world;
        private readonly float scale;
        private readonly List<Shown> shown = new List<Shown>();
        private readonly List<Falling> falling = new List<Falling>();
        private Material snowMat, fireMat, boltMat, ringMat;
        private const float FallSeconds = 0.32f;

        /// <summary>The sky's tint now (the screen's overlay): the element's colour while one rains.</summary>
        public Color Tint { get; private set; }

        public RainViewV2(GameManager game, System.Func<float, Vector3> world, float scale)
        {
            this.game = game; this.world = world; this.scale = scale;
        }

        public static Color ColourOf(ElementKind k) => k == ElementKind.Snow ? new Color(0.55f, 0.85f, 1f) : k == ElementKind.Fire ? new Color(1f, 0.45f, 0.1f) : new Color(1f, 0.95f, 0.35f);

        private Material Mat(ElementKind k)
        {
            if (snowMat == null)
            {
                snowMat = MaterialFactory.Create(new Color(0.9f, 0.97f, 1f), new Color(0.6f, 0.9f, 1.4f));
                fireMat = MaterialFactory.Create(new Color(1f, 0.5f, 0.1f), new Color(3f, 1.2f, 0.2f));
                boltMat = MaterialFactory.Create(new Color(1f, 1f, 0.6f), new Color(4f, 4f, 1.5f));
                ringMat = new Material(Shader.Find("Sprites/Default"));
            }
            return k == ElementKind.Snow ? snowMat : k == ElementKind.Fire ? fireMat : boltMat;
        }

        public void Update(DuelSim sim)
        {
            // circles: one for each rain, kept on its centre
            for (int i = shown.Count - 1; i >= 0; i--)
                if (!sim.Rains.Contains(shown[i].rain)) { if (shown[i].ring != null) Object.Destroy(shown[i].ring.gameObject); shown.RemoveAt(i); }
            Tint = Color.clear;
            foreach (var r in sim.Rains)
            {
                var s = shown.Find(x => x.rain == r);
                if (s == null) { s = new Shown { rain = r, ring = Ring(ColourOf(r.kind)) }; shown.Add(s); }
                var at = world(r.centre);
                float next = r.Done ? 1f : Mathf.Clamp01((r.dropAt[r.fallen] - sim.Time) / 0.8f);
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * Mathf.Lerp(30f, 8f, next));
                DrawRing(s.ring, at + Vector3.up * 0.06f, r.radius * pulse, ColourOf(r.kind), sim.Time < r.warnEnd ? 0.55f + 0.4f * Mathf.Sin(Time.time * 14f) : 0.9f);
                // drops spawned a fall's time before they land (the rules' moment)
                while (s.spawned < r.dropAt.Count && sim.Time >= r.dropAt[s.spawned] - FallSeconds)
                {
                    Spawn(r, s.spawned, at, sim.Time, r.dropAt[s.spawned]);
                    s.spawned++;
                }
                var c = ColourOf(r.kind); c.a = r.kind == ElementKind.Lightning ? 0.18f : 0.12f;
                Tint = c;
            }
            // the falling drops
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                var f = falling[i];
                if (f.t == null) { falling.RemoveAt(i); continue; }
                float k = Mathf.InverseLerp(f.start, f.end, sim.Time);
                if (f.kind == ElementKind.Lightning) { f.t.gameObject.SetActive(k >= 0.85f); f.t.position = f.to + Vector3.up * 6f * scale / 3.5f; }
                else f.t.position = Vector3.Lerp(f.from, f.to, k * k);
                if (k >= 1f) { Object.Destroy(f.t.gameObject, f.kind == ElementKind.Lightning ? 0.08f : 0f); falling.RemoveAt(i); }
            }
        }

        private void Spawn(ElementRain r, int i, Vector3 at, float now, float land)
        {
            bool last = r.Last(i);
            float size = (r.kind == ElementKind.Snow ? 0.35f : r.kind == ElementKind.Fire ? 0.6f : 0.12f) * (last ? 1.8f : 1f) * scale / 3.5f;
            GameObject go;
            if (r.kind == ElementKind.Lightning)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.localScale = new Vector3(size * (last ? 2f : 1f), 6f * scale / 3.5f, size * (last ? 2f : 1f));
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = Vector3.one * size;
            }
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Mat(r.kind);
            var to = at + Vector3.up * 0.4f * scale / 3.5f + Vector3.forward * Random.Range(-0.3f, 0.3f) * r.radius;
            var from = to + new Vector3(Random.Range(-1f, 1f), 14f, Random.Range(-1f, 1f)) * scale / 3.5f;
            go.transform.position = from;
            falling.Add(new Falling { t = go.transform, from = from, to = to, start = land - FallSeconds, end = land, kind = r.kind });
        }

        /// <summary>A drop landed (the rules said so): its burst and sound, bigger for the last.</summary>
        public void OnDrop(DuelEvent e, Vector3 at)
        {
            bool last = e.why == "last";
            var c = ColourOf(e.element);
            int n = last ? 40 : 14;
            game.V2Burst(at + Vector3.up * 0.5f, c, c * 3f, n, last ? 8f : 4f);
            if (last || e.element == ElementKind.Fire) game.V2Ring(new Vector3(at.x, at.y + 0.05f, at.z), last ? 3f : 1.4f, c);
            AudioManager.PlaySfx(e.element == ElementKind.Lightning ? Sfx.Clang : e.element == ElementKind.Fire ? Sfx.Impact : Sfx.Squash, last ? 1f : 0.6f, e.element == ElementKind.Snow ? 1.3f : 1f, 0.1f);
        }

        public void Clear()
        {
            foreach (var s in shown) if (s.ring != null) Object.Destroy(s.ring.gameObject);
            foreach (var f in falling) if (f.t != null) Object.Destroy(f.t.gameObject);
            shown.Clear(); falling.Clear();
        }

        private LineRenderer Ring(Color c)
        {
            Mat(ElementKind.Snow);
            var go = new GameObject("RainCircle");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 48;
            lr.loop = true;
            lr.useWorldSpace = true;
            lr.sharedMaterial = ringMat;
            lr.widthMultiplier = 0.12f * scale / 3.5f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        private static void DrawRing(LineRenderer lr, Vector3 centre, float radius, Color c, float alpha)
        {
            if (lr == null) return;
            c.a = alpha;
            lr.startColor = lr.endColor = c;
            for (int i = 0; i < lr.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / lr.positionCount;
                lr.SetPosition(i, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
        }
    }
}
