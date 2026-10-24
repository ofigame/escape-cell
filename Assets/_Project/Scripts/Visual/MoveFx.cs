using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The duel moves' own big pictures, each one living by itself and gone when done: a thick jagged lightning bolt,
    /// ice spikes bursting out of the ground in a line, a chain thrown and drawn back, a meteor falling with its fire,
    /// a whirlwind round a body, a shell of light round a body, an ice block round a body, a column of light.
    /// </summary>
    public class MoveFx : MonoBehaviour
    {
        private System.Action<float> step;
        private float life, t;

        private void Start() => OwnedMaterials.OwnAllUnder(gameObject);

        private void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            step?.Invoke(k);
            if (t >= life) Destroy(gameObject);
        }

        private static MoveFx Make(string name, float life, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.position = at;
            var m = go.AddComponent<MoveFx>();
            m.life = Mathf.Max(0.05f, life);
            return m;
        }

        private static LineRenderer Line(Transform parent, Material mat, float width, int points)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.positionCount = points;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 1;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.useWorldSpace = true;
            return lr;
        }

        /// <summary>A lightning bolt from <paramref name="from"/> to <paramref name="to"/>: a white-hot core in a wide glow,
        /// jagged and flickering (its path new every few frames), with forks; <paramref name="width"/> its core's thickness.</summary>
        public static void Bolt(Vector3 from, Vector3 to, Color colour, float width = 0.25f, float seconds = 0.45f)
        {
            var m = Make("Bolt", seconds, from);
            var hot = Color.Lerp(colour, Color.white, 0.6f);
            var core = Line(m.transform, MaterialFactory.Create(hot, hot * 6f), width, 14);
            var glow = Line(m.transform, MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.35f), colour * 3f), width * 3.2f, 14);
            var forks = new List<LineRenderer>();
            for (int i = 0; i < 3; i++) forks.Add(Line(m.transform, MaterialFactory.Create(hot, hot * 4f), width * 0.35f, 5));
            float len = Vector3.Distance(from, to);
            var dir = (to - from).normalized;
            var side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var side2 = Vector3.Cross(dir, side);
            Vector3[] pts = new Vector3[14];
            int frame = 0;
            void Jag()
            {
                for (int i = 0; i < pts.Length; i++)
                {
                    float f = i / (float)(pts.Length - 1);
                    float amp = (i == 0 || i == pts.Length - 1) ? 0f : len * 0.07f * Mathf.Sin(f * Mathf.PI);
                    pts[i] = Vector3.Lerp(from, to, f) + side * Random.Range(-amp, amp) + side2 * Random.Range(-amp, amp);
                }
                core.SetPositions(pts);
                glow.SetPositions(pts);
                foreach (var fk in forks)
                {
                    int s = Random.Range(2, pts.Length - 3);
                    var p = pts[s];
                    var off = (side * Random.Range(-1f, 1f) + side2 * Random.Range(-1f, 1f)).normalized * len * 0.18f + dir * len * 0.08f;
                    for (int j = 0; j < 5; j++) fk.SetPosition(j, p + off * (j / 4f) + side * Random.Range(-0.08f, 0.08f) * len * 0.1f);
                }
            }
            Jag();
            m.step = k =>
            {
                if (++frame % 3 == 0) Jag();
                float w = k < 0.15f ? 1f : 1f - (k - 0.15f) / 0.85f; // (full, then thinning out)
                core.widthMultiplier = width * w;
                glow.widthMultiplier = width * 3.2f * w;
                foreach (var fk in forks) fk.widthMultiplier = width * 0.35f * w;
            };
        }

        /// <summary>Ice spikes bursting out of the ground one after another from <paramref name="from"/> to
        /// <paramref name="to"/> (on the floor), standing a while, then sinking.</summary>
        public static void IceSpikes(Vector3 from, Vector3 to, float stand = 2f)
        {
            float len = Vector3.Distance(from, to);
            int n = Mathf.Max(4, Mathf.RoundToInt(len / 0.45f));
            float total = 0.35f + stand + 0.4f;
            var m = Make("IceSpikes", total, from);
            var ice = MaterialFactory.CreateSurface(new Color(0.65f, 0.9f, 1f), new Color(0.25f, 0.6f, 1f), 0.95f, 0.1f);
            var spikes = new List<(Transform t, float at, float h)>();
            var dir = to - from; dir.y = 0f;
            var right = Vector3.Cross(Vector3.up, dir.normalized);
            for (int i = 0; i < n; i++)
            {
                float f = (i + 0.5f) / n;
                for (int s = 0; s < 3; s++)
                {
                    var p = Vector3.Lerp(from, to, f) + right * Random.Range(-0.45f, 0.45f);
                    var sp = Shapes.Primitive(PrimitiveType.Cube, "Spike", m.transform, Vector3.zero, Vector3.one, ice).transform;
                    sp.position = p;
                    sp.rotation = Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0f, 90f), Random.Range(-18f, 18f)) * Quaternion.Euler(0f, 0f, 0f);
                    spikes.Add((sp, f * 0.35f, Random.Range(1.4f, 2.6f) * (s == 0 ? 1.2f : 0.8f)));
                }
            }
            m.step = k =>
            {
                float tt = k * total;
                foreach (var (sp, at, h) in spikes)
                {
                    float grow = Mathf.Clamp01((tt - at) / 0.12f);
                    float sink = Mathf.Clamp01((tt - (total - 0.4f)) / 0.4f);
                    float y = h * grow * (1f - sink);
                    sp.localScale = new Vector3(0.38f * (1f - 0.5f * sink), Mathf.Max(0.001f, y), 0.38f * (1f - 0.5f * sink));
                    sp.gameObject.SetActive(y > 0.01f);
                }
            };
        }

        /// <summary>An ice block round a body (frozen) for <paramref name="seconds"/>, cracking away at the end.</summary>
        public static void IceBlock(Transform body, float seconds)
        {
            if (body == null) return;
            var m = Make("IceBlock", seconds, body.position);
            var ice = MaterialFactory.CreateTransparent(new Color(0.6f, 0.88f, 1f, 0.72f), new Color(0.35f, 0.75f, 1.4f));
            var block = Shapes.Primitive(PrimitiveType.Cube, "Block", m.transform, new Vector3(0f, 0.75f, 0f), new Vector3(1.1f, 1.6f, 1.1f), ice).transform;
            block.localRotation = Quaternion.Euler(0f, 20f, 0f);
            var shard = Shapes.Primitive(PrimitiveType.Cube, "Shard", m.transform, new Vector3(0.35f, 1.5f, 0.2f), new Vector3(0.3f, 0.6f, 0.3f), ice).transform;
            shard.localRotation = Quaternion.Euler(20f, 30f, 15f);
            m.step = k =>
            {
                if (body != null) m.transform.position = body.position;
                float s = k < 0.08f ? k / 0.08f : k > 0.9f ? 1f - (k - 0.9f) / 0.1f : 1f;
                m.transform.localScale = Vector3.one * Mathf.Max(0.001f, s);
            };
        }

        /// <summary>A chain thrown from the mover to the other and drawn back, bringing it along (links of metal).</summary>
        public static void Chain(Transform from, Transform to, float seconds = 0.55f)
        {
            if (from == null || to == null) return;
            var m = Make("Chain", seconds, from.position);
            var metal = MaterialFactory.CreateSurface(new Color(0.75f, 0.72f, 0.65f), new Color(0.35f, 0.3f, 0.15f), 0.8f, 0.9f);
            var hookMat = MaterialFactory.Create(new Color(1f, 0.8f, 0.35f), new Color(1.5f, 1f, 0.3f));
            var links = new List<Transform>();
            for (int i = 0; i < 16; i++) links.Add(Shapes.Primitive(PrimitiveType.Sphere, "Link", m.transform, Vector3.zero, new Vector3(0.12f, 0.12f, 0.2f), metal).transform);
            var hook = Shapes.Primitive(PrimitiveType.Cube, "Hook", m.transform, Vector3.zero, new Vector3(0.25f, 0.25f, 0.35f), hookMat).transform;
            m.step = k =>
            {
                if (from == null || to == null) return;
                var a = from.position + Vector3.up * 0.9f;
                var b = to.position + Vector3.up * 0.9f;
                float reach = k < 0.4f ? k / 0.4f : 1f; // (out, then it holds while the other comes)
                var end = Vector3.Lerp(a, b, reach);
                var d = end - a;
                var rot = d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d) : Quaternion.identity;
                for (int i = 0; i < links.Count; i++)
                {
                    float f = (i + 1f) / links.Count;
                    links[i].position = Vector3.Lerp(a, end, f) + Vector3.up * Mathf.Sin(f * Mathf.PI) * 0.15f * (1f - reach);
                    links[i].rotation = rot * Quaternion.Euler(0f, 0f, i % 2 == 0 ? 0f : 90f);
                }
                hook.position = end;
                hook.rotation = rot;
            };
        }

        /// <summary>A meteor: a burning rock falling from high up to <paramref name="to"/> in <paramref name="seconds"/>,
        /// with a fire trail (the impact is the caller's).</summary>
        public static void Meteor(Vector3 to, float seconds, float size = 1f)
        {
            var from = to + new Vector3(-5f, 16f, 3f);
            var m = Make("Meteor", seconds, from);
            var rock = MaterialFactory.Create(new Color(0.35f, 0.2f, 0.12f), new Color(2.2f, 0.7f, 0.1f));
            var fire = MaterialFactory.CreateTransparent(new Color(1f, 0.45f, 0.1f, 0.5f), new Color(3f, 1.2f, 0.2f));
            var core = Shapes.Primitive(PrimitiveType.Sphere, "Rock", m.transform, Vector3.zero, Vector3.one * 1.1f * size, rock).transform;
            var halo = Shapes.Primitive(PrimitiveType.Sphere, "Fire", m.transform, Vector3.zero, Vector3.one * 1.8f * size, fire).transform;
            var trail = Line(m.transform, fire, 1.4f * size, 2);
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            m.step = k =>
            {
                var p = Vector3.Lerp(from, to, k * k);
                m.transform.position = p;
                core.localPosition = halo.localPosition = Vector3.zero;
                core.Rotate(200f * Time.deltaTime, 90f * Time.deltaTime, 0f);
                halo.localScale = Vector3.one * (1.6f + 0.3f * Mathf.Sin(Time.time * 30f)) * size;
                trail.SetPosition(0, p);
                trail.SetPosition(1, p + (from - to).normalized * 5f * size);
            };
        }

        /// <summary>A whirlwind round a body: rings of wind turning fast, rising and widening, for <paramref name="seconds"/>.</summary>
        public static void Whirl(Transform body, Color colour, float seconds)
        {
            if (body == null) return;
            var m = Make("Whirl", seconds, body.position);
            var wind = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.6f), colour * 3.5f);
            var rings = new List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                var r = Shapes.Primitive(PrimitiveType.Cylinder, "Wind", m.transform, new Vector3(0f, 0.2f + i * 0.45f, 0f), Vector3.one, wind).transform;
                rings.Add(r);
            }
            m.step = k =>
            {
                if (body != null) m.transform.position = body.position;
                float fade = k < 0.1f ? k / 0.1f : k > 0.85f ? 1f - (k - 0.85f) / 0.15f : 1f;
                for (int i = 0; i < rings.Count; i++)
                {
                    float w = (1.4f + i * 0.5f) * fade;
                    rings[i].localScale = new Vector3(w * 1.2f, 0.3f, w);
                    rings[i].Rotate(0f, (900f + i * 150f) * Time.deltaTime, 0f, Space.Self);
                    rings[i].localRotation = Quaternion.Euler(8f * Mathf.Sin(Time.time * 6f + i), rings[i].localEulerAngles.y, 8f * Mathf.Cos(Time.time * 5f + i));
                }
            };
        }

        /// <summary>A shell of light round a body (the iron body, the mirror) for <paramref name="seconds"/>, pulsing.</summary>
        public static void Shell(Transform body, Color colour, float seconds, float alpha = 0.3f)
        {
            if (body == null) return;
            var m = Make("Shell", seconds, body.position);
            var mat = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, alpha), colour * 2.5f);
            var shell = Shapes.Primitive(PrimitiveType.Sphere, "Shell", m.transform, new Vector3(0f, 0.85f, 0f), new Vector3(1.5f, 2f, 1.5f), mat).transform;
            m.step = k =>
            {
                if (body != null) m.transform.position = body.position;
                float s = k < 0.06f ? k / 0.06f : k > 0.92f ? 1f - (k - 0.92f) / 0.08f : 1f;
                float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 9f);
                shell.localScale = new Vector3(1.5f, 2f, 1.5f) * Mathf.Max(0.001f, s * pulse);
            };
        }

        /// <summary>A column of light standing on <paramref name="at"/> (healing, a strike landing), flaring and fading.</summary>
        public static void Pillar(Vector3 at, Color colour, float radius = 0.7f, float height = 6f, float seconds = 0.8f)
        {
            var m = Make("Pillar", seconds, at);
            var mat = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.45f), colour * 3f);
            var col = Shapes.Primitive(PrimitiveType.Cylinder, "Column", m.transform, new Vector3(0f, height * 0.5f, 0f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat).transform;
            m.step = k =>
            {
                float w = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
                col.localScale = new Vector3(radius * 2f * w, height * 0.5f, radius * 2f * w);
            };
        }

        /// <summary>A ball of light growing at a point and bursting outward (the core's blast): grows for
        /// <paramref name="charge"/> seconds, then flies out to <paramref name="radius"/>.</summary>
        public static void Blast(Vector3 at, Color colour, float radius, float charge = 0.5f)
        {
            float total = charge + 0.45f;
            var m = Make("Blast", total, at);
            var mat = MaterialFactory.CreateTransparent(new Color(colour.r, colour.g, colour.b, 0.5f), colour * 4f);
            var ball = Shapes.Primitive(PrimitiveType.Sphere, "Ball", m.transform, Vector3.zero, Vector3.one * 0.2f, mat).transform;
            m.step = k =>
            {
                float tt = k * total;
                float s = tt < charge ? Mathf.Lerp(0.2f, 1.2f, tt / charge) + 0.1f * Mathf.Sin(tt * 60f) : Mathf.Lerp(1.2f, radius * 2f, (tt - charge) / 0.45f);
                ball.localScale = Vector3.one * s;
                var c = mat.color; c.a = tt < charge ? 0.55f : 0.55f * (1f - (tt - charge) / 0.45f); mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            };
        }
    }
}
