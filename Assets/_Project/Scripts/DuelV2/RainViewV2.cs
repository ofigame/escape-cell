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
        private class Falling { public Transform t; public Vector3 from, to; public float start, end; public ElementKind kind; public LineRenderer bolt, branch, glow; public bool last; }

        private readonly GameManager game;
        private readonly System.Func<float, Vector3> world;
        private readonly float scale;
        private readonly List<Shown> shown = new List<Shown>();
        private readonly List<Falling> falling = new List<Falling>();
        private Material snowMat, fireMat, boltMat, ringMat;
        private const float FallSeconds = 0.55f; // (long enough to be seen coming)

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
                if (!sim.Rains.Exists(r => r.id == shown[i].rain.id)) { if (shown[i].ring != null) Object.Destroy(shown[i].ring.gameObject); shown.RemoveAt(i); }
            Tint = Color.clear;
            foreach (var r in sim.Rains)
            {
                var s = shown.Find(x => x.rain.id == r.id);
                if (s != null) s.rain = r; // (a live duel: the same rain, its newest state)
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
            }
            // the falling drops
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                var f = falling[i];
                if (f.t == null) { falling.RemoveAt(i); continue; }
                float k = Mathf.InverseLerp(f.start, f.end, sim.Time);
                if (f.kind == ElementKind.Lightning)
                {
                    // the bolt: nothing, then a jagged white-gold line sky to ground, re-drawn each frame (it flickers)
                    bool on = k >= 0.82f;
                    f.bolt.enabled = f.glow.enabled = on; f.branch.enabled = on;
                    if (on)
                    {
                        Jag(f.bolt, f.from, f.to, f.last ? 2.2f : 1.6f);
                        for (int p = 0; p < f.bolt.positionCount; p++) f.glow.SetPosition(p, f.bolt.GetPosition(p)); // (the halo round the same path)
                        Jag(f.branch, f.bolt.GetPosition(f.bolt.positionCount / 3), f.to + new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-1f, 1f)) * scale / 3.5f, 1.2f);
                    }
                }
                else
                {
                    f.t.position = Vector3.Lerp(f.from, f.to, k * k); // (falling faster and faster)
                    f.t.Rotate(Vector3.one * 600f * Time.deltaTime, Space.Self);
                }
                if (k >= 1f) { Object.Destroy(f.t.gameObject, f.kind == ElementKind.Lightning ? 0.12f : 0f); falling.RemoveAt(i); }
            }
        }

        private void Spawn(ElementRain r, int i, Vector3 at, float now, float land)
        {
            bool last = r.Last(i);
            float u = scale / 3.5f;
            var to = at + Vector3.up * 0.4f * u + Vector3.forward * Random.Range(-0.3f, 0.3f) * r.radius;
            if (r.kind == ElementKind.Lightning)
            {
                var root = new GameObject("Bolt").transform;
                var from = to + new Vector3(Random.Range(-1.2f, 1.2f), 16f, Random.Range(-0.6f, 0.6f)) * u;
                // a white-hot core, a wide yellow halo round it, a branch splitting off
                var glow = Line(root, ColourOf(r.kind), (last ? 2.4f : 1.6f) * u, 18, 0.35f);
                var bolt = Line(root, Color.white, (last ? 0.6f : 0.4f) * u, 18);
                var branch = Line(root, ColourOf(r.kind), 0.22f * u, 10);
                bolt.enabled = branch.enabled = glow.enabled = false;
                falling.Add(new Falling { t = root, from = from, to = to, start = land - FallSeconds, end = land, kind = r.kind, bolt = bolt, branch = branch, glow = glow, last = last });
                return;
            }
            // a snowball or a fireball: a glowing core, a long tail behind it, coming in at a slant from the stands' side
            float size = (r.kind == ElementKind.Snow ? 0.45f : 0.7f) * (last ? 1.8f : 1f) * u;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Mat(r.kind);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = Vector3.one * size;
            var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(halo.GetComponent<Collider>());
            halo.transform.SetParent(go.transform, false);
            halo.transform.localScale = Vector3.one * 1.6f;
            var hr = halo.GetComponent<Renderer>();
            hr.sharedMaterial = Glow(ColourOf(r.kind), 0.35f);
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.28f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = size * 1.1f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.sharedMaterial = RingMat;
            var c = ColourOf(r.kind);
            trail.colorGradient = new Gradient { colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(c, 0.3f), new GradientColorKey(c * 0.6f, 1f) }, alphaKeys = new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) } };
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var start = to + new Vector3(Random.Range(2f, 4f), 15f, Random.Range(-1.5f, 1.5f)) * u;
            go.transform.position = start;
            falling.Add(new Falling { t = go.transform, from = start, to = to, start = land - FallSeconds, end = land, kind = r.kind, last = last });
        }

        private Material RingMat { get { Mat(ElementKind.Snow); return ringMat; } }

        private readonly Dictionary<Color, Material> glows = new Dictionary<Color, Material>();
        private Material Glow(Color c, float alpha)
        {
            c.a = alpha;
            if (glows.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(RingMat) { color = c };
            glows[c] = m;
            return m;
        }

        private LineRenderer Line(Transform parent, Color c, float width, int points, float alpha = 1f)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = points;
            lr.useWorldSpace = true;
            lr.sharedMaterial = RingMat;
            lr.widthMultiplier = width;
            lr.widthCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 1f);
            lr.colorGradient = new Gradient { colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(c, 1f) }, alphaKeys = new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) } };
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        /// <summary>A lightning line from <paramref name="a"/> to <paramref name="b"/>: zigzags off the straight line, wider in its middle.</summary>
        private void Jag(LineRenderer lr, Vector3 a, Vector3 b, float wild)
        {
            int n = lr.positionCount;
            var side = Vector3.Cross((b - a).normalized, Vector3.forward);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            for (int p = 0; p < n; p++)
            {
                float k = p / (float)(n - 1);
                float off = (p == 0 || p == n - 1) ? 0f : Random.Range(-1f, 1f) * wild * Mathf.Sin(k * Mathf.PI) * scale / 3.5f;
                lr.SetPosition(p, Vector3.Lerp(a, b, k) + side * off + Vector3.forward * off * 0.4f);
            }
        }

        /// <summary>A drop landed (the rules said so): its burst and sound, bigger for the last.</summary>
        public void OnDrop(DuelEvent e, Vector3 at)
        {
            bool last = e.why == "last";
            var c = ColourOf(e.element);
            int n = last ? 40 : 14;
            game.V2Burst(at + Vector3.up * 0.5f, c, c * 3f, n, last ? 8f : 4f);
            if (last || e.element == ElementKind.Fire) game.V2Ring(new Vector3(at.x, at.y + 0.05f, at.z), last ? 3f : 1.4f, c);
            DuelSoundsV2.Play(e.element == ElementKind.Lightning ? DuelSoundV2.Lightning : e.element == ElementKind.Fire ? DuelSoundV2.Fire : DuelSoundV2.Snow, last ? 1f : 0.55f, 0.08f);
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
