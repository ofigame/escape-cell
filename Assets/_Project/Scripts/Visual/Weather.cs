using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Each floor's sky: sparkles, petals, snow, neon rain, embers, fireflies, sandstorms, bubbles, confetti, toxic drizzle,
    /// thunderstorms with lightning, data rain, cosmic dust, crystal glints, smoke and sparks. The weather follows the camera
    /// and builds up with the level: calm at the start, a storm by the end (<see cref="SetIntensity"/>).
    /// </summary>
    public class Weather : MonoBehaviour
    {
        public enum Kind { None, Sparkle, Petals, Snow, NeonRain, Embers, Fireflies, Sand, Bubbles, Confetti, Toxic, Thunder, DataRain, Cosmic, Crystal, Smoke }

        private static Material addMat, alphaMat;
        private static Texture2D dot;

        private Kind kind;
        private ParticleSystem a, b;
        private float rateA, rateB;
        private float intensity = 0.5f, shown = 0.5f;
        private bool lightning;
        private Color boltColor = new Color(0.8f, 0.9f, 1f);
        private float nextBolt, flash;
        private Light sun;
        private float sunBase = 1f;
        private Transform bolt;
        private Material boltMat;
        private Camera cam;
        private FxSystem fx;
        private float fireworkTimer;

        public static Weather Create(FxSystem fxSystem)
        {
            var w = new GameObject("Weather").AddComponent<Weather>();
            w.fx = fxSystem;
            return w;
        }

        /// <summary>The weather for a world (0-based).</summary>
        public static Kind ForWorld(int world)
        {
            switch (WorldTheme.ForWorld(world).key)
            {
                case "world.lavender": return Kind.Sparkle;
                case "world.sunset": return Kind.Petals;
                case "world.ice": return Kind.Snow;
                case "world.neon": return Kind.NeonRain;
                case "world.lava": return Kind.Embers;
                case "world.forest": return Kind.Fireflies;
                case "world.desert": return Kind.Sand;
                case "world.ocean": return Kind.Bubbles;
                case "world.candy": return Kind.Confetti;
                case "world.toxic": return Kind.Toxic;
                case "world.midnight": return Kind.Thunder;
                case "world.aurora": return Kind.Snow;
                case "world.storm": return Kind.Thunder;
                case "world.cyber": return Kind.DataRain;
                case "world.galaxy": return Kind.Cosmic;
                case "world.crystal": return Kind.Crystal;
                case "world.festival": return Kind.Confetti;
                case "world.factory": return Kind.Smoke;
                case "world.funfair": return Kind.Confetti;
                case "world.roof": return Kind.Thunder;
                default: return Kind.Sparkle;
            }
        }

        // ---------- Setup ----------

        /// <summary>Soft additive glow for particles and trails (also used by the tunnels).</summary>
        public static Material AddMaterial { get { EnsureAssets(); return addMat; } }

        private static void EnsureAssets()
        {
            if (dot != null) return;
            dot = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
                    float alpha = Mathf.Clamp01(1f - d);
                    dot.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            dot.Apply();
            addMat = Make("SquashBot_ParticleAdd");
            alphaMat = Make("SquashBot_ParticleAlpha");
        }

        private static Material Make(string resource)
        {
            var baseMat = Resources.Load<Material>(resource);
            var m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", dot);
            m.SetTexture("_MainTex", dot);
            m.SetColor("_BaseColor", Color.white);
            return m;
        }

        public void Apply(int world) => Apply(ForWorld(world), WorldTheme.ForWorld(world).accent);

        public void Apply(Kind k, Color accent)
        {
            EnsureAssets();
            if (k == kind && a != null) return;
            Clear();
            kind = k;
            lightning = false;
            boltColor = new Color(0.8f, 0.9f, 1f);
            switch (k)
            {
                case Kind.Sparkle:
                    a = Particles("Sparkle", addMat, 130, Color.Lerp(accent, Color.white, 0.3f) * 1.6f, 0.2f, 5f, new Vector3(16f, 3f, 16f), 1f, Vector3.up * 0.4f, 0f);
                    Twinkle(a);
                    break;
                case Kind.Petals:
                    a = Particles("Petals", alphaMat, 110, new Color(1f, 0.6f, 0.65f, 0.95f), 0.22f, 7f, new Vector3(18f, 1f, 18f), 8f, new Vector3(0.8f, -0.9f, 0.3f), 0f);
                    Noise(a, 0.6f);
                    break;
                case Kind.Snow:
                    a = Particles("Snow", alphaMat, 320, new Color(1f, 1f, 1f, 0.95f), 0.17f, 6f, new Vector3(18f, 1f, 18f), 8f, new Vector3(1.2f, -1.6f, 0.4f), 0f);
                    Noise(a, 0.8f);
                    break;
                case Kind.NeonRain:
                    a = Rain("NeonRain", addMat, 260, new Color(0.4f, 1f, 1.3f), 14f);
                    b = Rain("NeonRainPink", addMat, 140, new Color(1.3f, 0.4f, 1.1f), 14f);
                    lightning = true;
                    boltColor = new Color(0.6f, 0.9f, 1.4f);
                    break;
                case Kind.Embers:
                    a = Particles("Embers", addMat, 300, new Color(3f, 1f, 0.25f), 0.15f, 3.5f, new Vector3(16f, 0.2f, 16f), 0f, Vector3.up * 2.2f, 0f);
                    Noise(a, 1.4f);
                    b = Particles("Ash", alphaMat, 120, new Color(0.3f, 0.26f, 0.28f, 0.8f), 0.12f, 6f, new Vector3(18f, 1f, 18f), 8f, new Vector3(0.4f, -0.8f, 0f), 0f);
                    lightning = true;
                    boltColor = new Color(1.6f, 0.6f, 0.2f);
                    break;
                case Kind.Fireflies:
                    a = Particles("Fireflies", addMat, 110, new Color(1.6f, 2.2f, 0.5f), 0.16f, 6f, new Vector3(14f, 2f, 14f), 0.8f, Vector3.zero, 0f);
                    Noise(a, 1f);
                    Twinkle(a);
                    b = Rain("Drizzle", alphaMat, 120, new Color(0.7f, 0.85f, 1f, 0.45f), 10f);
                    break;
                case Kind.Sand:
                    a = Particles("Sand", alphaMat, 320, new Color(0.95f, 0.75f, 0.45f, 0.55f), 0.1f, 2.5f, new Vector3(4f, 3f, 20f), 1.5f, new Vector3(9f, 0.3f, 2f), 0f, -8f);
                    Stretch(a, 0.08f);
                    b = Particles("Dust", alphaMat, 30, new Color(0.9f, 0.7f, 0.45f, 0.25f), 1.6f, 4f, new Vector3(4f, 2f, 20f), 1f, new Vector3(5f, 0.2f, 1f), 0f, -8f);
                    break;
                case Kind.Bubbles:
                    a = Rain("Rain", alphaMat, 160, new Color(0.7f, 0.9f, 1f, 0.5f), 12f);
                    b = Particles("Bubbles", alphaMat, 40, new Color(0.7f, 0.95f, 1f, 0.5f), 0.14f, 4f, new Vector3(14f, 0.2f, 14f), 0f, Vector3.up * 1.5f, 0f);
                    Noise(b, 0.5f);
                    break;
                case Kind.Confetti:
                    a = Particles("Confetti", alphaMat, 240, Color.white, 0.17f, 6f, new Vector3(18f, 1f, 18f), 9f, new Vector3(0.3f, -1.4f, 0.2f), 0f);
                    RandomColors(a, new Color(1f, 0.4f, 0.6f), new Color(0.4f, 0.8f, 1f), new Color(1f, 0.9f, 0.3f));
                    Noise(a, 1f);
                    break;
                case Kind.Toxic:
                    a = Rain("ToxicRain", addMat, 200, new Color(0.5f, 1.4f, 0.3f), 9f);
                    b = Particles("ToxicBubbles", addMat, 50, new Color(0.6f, 1.6f, 0.4f), 0.12f, 3f, new Vector3(14f, 0.2f, 14f), 0f, Vector3.up * 1.6f, 0f);
                    Noise(b, 0.6f);
                    lightning = true;
                    boltColor = new Color(0.6f, 1.8f, 0.4f);
                    break;
                case Kind.Thunder:
                    a = Rain("StormRain", alphaMat, 520, new Color(0.75f, 0.85f, 1f, 0.55f), 22f, 4f);
                    b = Particles("Mist", alphaMat, 24, new Color(0.7f, 0.75f, 0.9f, 0.12f), 3f, 5f, new Vector3(18f, 1f, 18f), 1.5f, new Vector3(2.5f, 0f, 0.5f), 0f);
                    lightning = true;
                    break;
                case Kind.DataRain:
                    a = Rain("Data", addMat, 180, new Color(0.3f, 1.6f, 0.9f), 7f);
                    b = Rain("DataBlue", addMat, 90, new Color(0.3f, 0.9f, 1.8f), 9f);
                    lightning = true;
                    boltColor = new Color(0.4f, 1.6f, 1.6f);
                    break;
                case Kind.Cosmic:
                    a = Particles("Cosmic", addMat, 420, new Color(1.2f, 0.6f, 2.2f), 0.15f, 6f, new Vector3(18f, 4f, 18f), 2f, Vector3.zero, 0f);
                    var orbit = a.velocityOverLifetime;
                    orbit.enabled = true;
                    orbit.space = ParticleSystemSimulationSpace.World;
                    orbit.orbitalY = 0.35f;
                    Twinkle(a);
                    lightning = true;
                    boltColor = new Color(1.2f, 0.6f, 1.8f);
                    break;
                case Kind.Crystal:
                    a = Particles("Glints", addMat, 260, new Color(0.9f, 2f, 2.3f), 0.2f, 3f, new Vector3(16f, 3f, 16f), 1.2f, Vector3.up * 0.2f, 0f);
                    Twinkle(a);
                    b = Particles("Shards", addMat, 40, new Color(1.6f, 0.7f, 1.6f), 0.1f, 5f, new Vector3(18f, 1f, 18f), 8f, new Vector3(0f, -0.7f, 0f), 0f);
                    break;
                case Kind.Smoke:
                    a = Particles("Smoke", alphaMat, 30, new Color(0.45f, 0.4f, 0.4f, 0.22f), 2.2f, 5f, new Vector3(16f, 0.2f, 16f), 0f, Vector3.up * 0.9f, 0f);
                    Noise(a, 0.4f);
                    b = Particles("Sparks", addMat, 200, new Color(3f, 1.5f, 0.4f), 0.09f, 1.2f, new Vector3(16f, 0.2f, 16f), 0f, Vector3.up * 3f, 1.2f);
                    break;
            }
            if (a != null) rateA = a.emission.rateOverTime.constant;
            if (b != null) rateB = b.emission.rateOverTime.constant;
            ApplyIntensity(shown);
        }

        public void Clear()
        {
            if (a != null) Destroy(a.gameObject);
            if (b != null) Destroy(b.gameObject);
            a = b = null;
            kind = Kind.None;
            if (bolt != null) bolt.gameObject.SetActive(false);
            flash = 0f;
            RestoreSun();
        }

        /// <summary>0 = calm, 1 = full storm (the level's last moments). Changes are eased.</summary>
        public void SetIntensity(float k) => intensity = Mathf.Clamp01(Force >= 0f ? Force : k);

        /// <summary>Test hooks: pin the weather at this strength (negative = off).</summary>
        public static float Force = -1f;

        /// <summary>Hides or shows the effect (tunnels have their own).</summary>
        public void SetVisible(bool on)
        {
            if (a != null) a.gameObject.SetActive(on);
            if (b != null) b.gameObject.SetActive(on);
            if (!on && bolt != null) bolt.gameObject.SetActive(false);
            hidden = !on;
        }

        private bool hidden;

        // ---------- Particle helpers ----------

        private ParticleSystem Particles(string name, Material mat, float rate, Color color, float size, float life, Vector3 box, float height,
            Vector3 velocity, float gravity, float sideOffset = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(sideOffset, height, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.3f);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(velocity.x * 0.8f, velocity.x * 1.2f);
            vel.y = new ParticleSystem.MinMaxCurve(velocity.y * 0.8f, velocity.y * 1.2f);
            vel.z = new ParticleSystem.MinMaxCurve(velocity.z * 0.8f, velocity.z * 1.2f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>Falling streaks from above (rain of any colour).</summary>
        private ParticleSystem Rain(string name, Material mat, float rate, Color color, float speed, float wind = 1.5f)
        {
            var ps = Particles(name, mat, rate * 1.8f, color, 0.08f, 1.6f, new Vector3(20f, 1f, 20f), 10f, new Vector3(wind, -speed, wind * 0.3f), 0f);
            Stretch(ps, 0.07f);
            return ps;
        }

        private static void Stretch(ParticleSystem ps, float scale)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = scale;
            r.lengthScale = 1f;
        }

        private static void Noise(ParticleSystem ps, float strength)
        {
            var n = ps.noise;
            n.enabled = true;
            n.strength = strength;
            n.frequency = 0.4f;
            n.scrollSpeed = 0.3f;
        }

        private static void Twinkle(ParticleSystem ps)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.5f, 0.4f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
        }

        private static void RandomColors(ParticleSystem ps, Color c1, Color c2, Color c3)
        {
            var main = ps.main;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c1, 0f), new GradientColorKey(c2, 0.5f), new GradientColorKey(c3, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
        }

        private void ApplyIntensity(float k)
        {
            // A calm sky still has a little going on; the storm at full strength has about three times as much.
            float m = Mathf.Lerp(0.35f, 1.25f, k);
            if (a != null) { var e = a.emission; e.rateOverTime = rateA * m; }
            if (b != null) { var e = b.emission; e.rateOverTime = rateB * m; }
        }

        // ---------- Every frame ----------

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || kind == Kind.None) return;

            // Stay over the part of the world the camera looks at.
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.45f, 0f));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d) && d < 200f)
            {
                var p = ray.GetPoint(d);
                transform.position = new Vector3(p.x, 0f, p.z);
            }
            transform.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);

            float dt = Time.deltaTime;
            shown = Mathf.MoveTowards(shown, intensity, dt * 0.3f);
            ApplyIntensity(shown);
            if (hidden) return;

            if (lightning) UpdateLightning(dt);
            if (kind == Kind.Confetti && shown > 0.5f && fx != null)
            {
                // Festival skies: fireworks pop now and then once the level heats up.
                fireworkTimer -= dt;
                if (fireworkTimer <= 0f)
                {
                    fireworkTimer = Random.Range(0.6f, 2f) / shown;
                    var at = transform.position + new Vector3(Random.Range(-6f, 6f), Random.Range(5f, 8f), Random.Range(-6f, 6f));
                    var c = Color.HSVToRGB(Random.value, 0.6f, 1f);
                    fx.Burst(at, c, c * 2f, 26, 5f);
                }
            }
        }

        private void UpdateLightning(float dt)
        {
            if (sun == null) { sun = FindAnyObjectByType<Light>(); if (sun != null) sunBase = sun.intensity; }
            nextBolt -= dt;
            if (nextBolt <= 0f)
            {
                // Rare in calm weather, every few seconds in the storm.
                nextBolt = Random.Range(2.5f, 6f) * Mathf.Lerp(2.2f, 0.6f, shown);
                if (shown > 0.25f) Strike();
            }
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - dt * 4f);
                if (sun != null) sun.intensity = sunBase + flash * 2.2f;
                if (bolt != null)
                {
                    bolt.gameObject.SetActive(flash > 0.35f);
                    MaterialFactory.SetColors(boltMat, Color.white, boltColor * (2f + flash * 4f));
                }
                if (flash <= 0f) RestoreSun();
            }
        }

        private void RestoreSun()
        {
            if (sun != null) sun.intensity = sunBase;
        }

        /// <summary>A lightning bolt: a jagged streak from the sky into the distance, a flash of light, a rumble.</summary>
        public void Strike()
        {
            if (bolt == null)
            {
                bolt = new GameObject("Bolt").transform;
                boltMat = MaterialFactory.Create(Color.white, boltColor * 4f);
            }
            foreach (Transform c in bolt) Destroy(c.gameObject);
            bolt.SetParent(null);
            bolt.position = Vector3.zero;
            var start = transform.position + Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * transform.forward * Random.Range(9f, 14f) + Vector3.up * 14f;
            var p = start;
            for (int i = 0; i < 9; i++)
            {
                var next = p + new Vector3(Random.Range(-1.4f, 1.4f), -Random.Range(1.3f, 2.1f), Random.Range(-1.4f, 1.4f));
                var seg = Shapes.Rounded("Seg", bolt, (p + next) * 0.5f, new Vector3(0.09f, (next - p).magnitude + 0.1f, 0.09f), 0.03f, boltMat).transform;
                seg.up = (next - p).normalized;
                if (i == 4)
                {
                    // A short branch off the middle.
                    var branch = next + new Vector3(Random.Range(-2f, 2f), -1.6f, Random.Range(-2f, 2f));
                    var bs = Shapes.Rounded("Branch", bolt, (next + branch) * 0.5f, new Vector3(0.06f, (branch - next).magnitude, 0.06f), 0.02f, boltMat).transform;
                    bs.up = (branch - next).normalized;
                }
                p = next;
            }
            flash = 1f;
            Audio.AudioManager.PlaySfx(Audio.Sfx.Impact, 0.35f, 0.45f, 0.1f);
        }
    }
}
