using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// What a fighter's body shows of its state, on its visual root: the white flash of a blow, burning (little flames
    /// for a while), frozen or slowed (a blue tint, its pictures at 70%), stunned (sparks circling over its head),
    /// untouchable (a pulsing shine). One tint at a time on its body (the flash over all), by a property block, so the
    /// shared materials are never changed.
    /// </summary>
    public class StatusFx : MonoBehaviour
    {
        private Renderer[] body;
        private MaterialPropertyBlock block;
        private float flashUntil, burnUntil, freezeUntil, stunUntil, shineUntil;
        private bool tinted;
        private ParticleSystem flames, frost;
        private Transform stars;
        private Visual.SkinnedHero hero;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>The state effects on a visual root (made the first time they are asked for).</summary>
        public static StatusFx On(Transform visualRoot)
        {
            if (visualRoot == null) return null;
            var s = visualRoot.GetComponent<StatusFx>();
            return s != null ? s : visualRoot.gameObject.AddComponent<StatusFx>();
        }

        /// <summary>The body flashes white for <paramref name="seconds"/> (a blow landing).</summary>
        public void Flash(float seconds) => flashUntil = Mathf.Max(flashUntil, Time.unscaledTime + seconds);

        /// <summary>Every status ended at once: no flash, flames, frost, stars or shine (the pose reset).</summary>
        public void Clear() { flashUntil = burnUntil = freezeUntil = stunUntil = shineUntil = 0f; }

        /// <summary>Little flames on it for a while.</summary>
        public void Burn(float seconds)
        {
            burnUntil = Mathf.Max(burnUntil, Time.time + seconds);
            if (flames == null) flames = MakeParticles("Flames", new Color(1f, 0.55f, 0.15f), 0.9f, 22f);
            var e = flames.emission; e.enabled = true;
            if (!flames.isPlaying) flames.Play();
        }

        /// <summary>Frozen or slowed: blue, its pictures at 70% (the rules decide how long it really stands).</summary>
        public void Freeze(float seconds)
        {
            freezeUntil = Mathf.Max(freezeUntil, Time.time + seconds);
            VisualTime.On(transform).SlowFor(0.7f, seconds);
            if (frost == null) frost = MakeParticles("Frost", new Color(0.65f, 0.9f, 1f), 0.6f, 14f);
            var e = frost.emission; e.enabled = true;
            if (!frost.isPlaying) frost.Play();
        }

        /// <summary>Stunned: sparks circling over its head.</summary>
        public void Stun(float seconds)
        {
            stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
            if (stars == null)
            {
                stars = new GameObject("Stars").transform;
                stars.SetParent(transform, false);
                var mat = Visual.MaterialFactory.Create(new Color(1f, 0.9f, 0.4f), new Color(3f, 2.5f, 0.8f));
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f;
                    Visual.Shapes.Primitive(PrimitiveType.Sphere, "Star", stars, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.35f, Vector3.one * 0.09f, mat);
                }
                Visual.OwnedMaterials.Own(stars.gameObject, mat);
            }
            stars.gameObject.SetActive(true);
        }

        private int damageTier;
        private ParticleSystem smoke;
        private float arcAt;

        /// <summary>How hurt the body looks: 0 whole, 1 smoking now and then (under half health), 2 smoking and crackling
        /// with little electric arcs (under a quarter). Set as health changes; each tier starts once.</summary>
        public void SetDamage(int tier)
        {
            tier = Mathf.Clamp(tier, 0, 2);
            if (tier == damageTier) return;
            damageTier = tier;
            if (tier > 0 && smoke == null)
            {
                smoke = MakeParticles("Smoke", new Color(0.35f, 0.35f, 0.38f), 1.6f, 3f);
                var main = smoke.main; main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f); main.gravityModifier = -0.08f;
                var shape = smoke.shape; shape.scale = new Vector3(0.3f, 0.3f, 0.3f); shape.position = new Vector3(0f, 1.1f, -0.1f);
                smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = Visual.MaterialFactory.CreateTransparent(new Color(0.4f, 0.4f, 0.42f, 0.5f), Color.black);
                Visual.OwnedMaterials.Own(smoke.gameObject, smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial);
            }
            if (smoke != null)
            {
                var e = smoke.emission; e.enabled = tier > 0; e.rateOverTime = tier >= 2 ? 6f : 2.5f;
                if (tier > 0 && !smoke.isPlaying) smoke.Play();
            }
            arcAt = Time.time + 0.5f;
        }

        /// <summary>Untouchable for a while: a pulsing shine (the backflip, the iron body).</summary>
        public void Shine(float seconds) => shineUntil = Mathf.Max(shineUntil, Time.time + seconds);

        private ParticleSystem MakeParticles(string name, Color colour, float life, float rate)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.gravityModifier = -0.15f; // (rising)
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = colour;
            main.maxParticles = 40;
            var emission = ps.emission; emission.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(0.5f, 1.1f, 0.4f); shape.position = new Vector3(0f, 0.75f, 0f);
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(colour, 0.3f), new GradientColorKey(colour * 0.5f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Visual.Weather.AddMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        private void LateUpdate()
        {
            float now = Time.time;
            if (flames != null && now >= burnUntil && flames.emission.enabled) { var e = flames.emission; e.enabled = false; }
            if (damageTier >= 2 && now >= arcAt)
            {
                // a short electric arc crackling over the body
                arcAt = now + Random.Range(1.6f, 2.8f);
                if (hero == null) hero = GetComponentInChildren<Visual.SkinnedHero>();
                if (hero != null)
                {
                    var c = hero.ChestPoint;
                    float s = Mathf.Max(0.3f, transform.lossyScale.y) * 0.35f;
                    Visual.MoveFx.Bolt(c + Random.insideUnitSphere * s, c + Random.insideUnitSphere * s, new Color(0.6f, 0.85f, 1f), 0.04f * s / 0.35f, 0.14f);
                }
            }
            if (frost != null && now >= freezeUntil && frost.emission.enabled) { var e = frost.emission; e.enabled = false; }
            if (stars != null)
            {
                bool on = now < stunUntil;
                if (stars.gameObject.activeSelf != on) stars.gameObject.SetActive(on);
                if (on)
                {
                    if (hero == null) hero = GetComponentInChildren<Visual.SkinnedHero>();
                    if (hero != null) stars.position = hero.HeadPoint + Vector3.up * 0.25f * transform.lossyScale.y;
                    stars.Rotate(0f, 360f * Time.deltaTime, 0f, Space.World);
                }
            }
            // the body's tint: the flash over all, then frozen blue, then the untouchable shine
            Color? tint = null;
            if (Time.unscaledTime < flashUntil) tint = new Color(2.6f, 2.6f, 2.6f, 1f);
            else if (now < freezeUntil) tint = new Color(0.62f, 0.82f, 1.25f, 1f);
            else if (now < shineUntil) { float k = 1.15f + 0.35f * Mathf.Sin(now * 22f); tint = new Color(k, k, k * 1.08f, 1f); }
            if (tint.HasValue) Tint(tint.Value);
            else if (tinted) ClearTint();
        }

        private void Tint(Color c)
        {
            if (body == null || body.Length == 0)
            {
                hero = GetComponentInChildren<Visual.SkinnedHero>();
                body = hero != null ? hero.GetComponentsInChildren<SkinnedMeshRenderer>(true) : new Renderer[0];
            }
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, c);
            foreach (var r in body) if (r != null) r.SetPropertyBlock(block, 0);
            tinted = true;
        }

        private void ClearTint()
        {
            if (body != null) foreach (var r in body) if (r != null) r.SetPropertyBlock(null, 0);
            tinted = false;
        }
    }
}
