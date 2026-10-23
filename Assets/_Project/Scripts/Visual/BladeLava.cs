using UnityEngine;
using UnityEngine.Rendering;

namespace SquashBot.Visual
{
    /// <summary>
    /// Lava welling out of a blade: its glow surges in slow waves, and glowing drops ooze from all over it — they swell
    /// out a little, then sag and drip, cooling from white-hot through the lava's colour to dark as they fall.
    /// </summary>
    public class BladeLava : MonoBehaviour
    {
        private Color hot;
        private float length;
        private Renderer[] blade;
        private MaterialPropertyBlock block;
        private Color baseGlow;
        private float phase;

        /// <summary>The lava's colour (red-orange, or blue) and the weapon's length (sizes the drops).</summary>
        public void Set(Color colour, float weaponLength)
        {
            hot = colour;
            length = Mathf.Max(0.2f, weaponLength);
        }

        private void Start()
        {
            phase = Random.value * 10f;
            block = new MaterialPropertyBlock();
            var all = GetComponentsInChildren<MeshRenderer>(true);
            blade = all;
            MeshRenderer biggest = null;
            float size = -1f;
            foreach (var r in all)
            {
                float s = r.bounds.size.sqrMagnitude;
                if (s > size) { size = s; biggest = r; }
            }
            if (biggest != null && biggest.sharedMaterial != null && biggest.sharedMaterial.HasProperty("_EmissionColor"))
                baseGlow = biggest.sharedMaterial.GetColor("_EmissionColor");
            if (biggest != null) Drops(biggest);
        }

        private void Drops(MeshRenderer from)
        {
            var go = new GameObject("Lava");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f * length, 0.12f * length);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f * length, 0.06f * length);
            main.gravityModifier = 0.12f; // (they sag and drip)
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 120;
            var emission = ps.emission;
            emission.rateOverTime = 65f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.MeshRenderer;
            shape.meshRenderer = from;
            shape.normalOffset = 0.004f;
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.15f)));
            var cool = ps.colorOverLifetime;
            cool.enabled = true;
            var g = new Gradient();
            var white = Color.Lerp(hot, Color.white, 0.7f);
            var deep = new Color(hot.r * 0.5f, hot.g * 0.35f, hot.b * 0.5f);
            g.SetKeys(new[] { new GradientColorKey(white, 0f), new GradientColorKey(hot, 0.35f), new GradientColorKey(deep, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
            cool.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Weather.AddMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }

        private void Update()
        {
            if (blade == null) return;
            // the glow in slow surges, as if the lava inside rose and fell
            float t = Time.time + phase;
            float surge = 0.75f + 0.35f * Mathf.Sin(t * 2.1f) + 0.18f * Mathf.Sin(t * 5.3f + 1.7f);
            var glow = Color.Lerp(baseGlow, hot * 1.3f, 0.55f) * surge;
            foreach (var r in blade)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", glow);
                r.SetPropertyBlock(block);
            }
        }
    }
}
