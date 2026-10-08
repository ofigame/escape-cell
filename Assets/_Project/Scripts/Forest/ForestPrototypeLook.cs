using UnityEngine;

namespace SquashBot.Forest
{
    /// <summary>
    /// The journey's light and air: sun, sky, fog and ambient while in it (the stage's look restored on leaving),
    /// drifting leaves around the camera, footstep dust, and the daylight dimming inside caves.
    /// </summary>
    public partial class ForestPrototype
    {
        private Light sun;
        private Quaternion sunRot;
        private Color sunColor, ambientSky;
        private float sunIntensity;
        private LightShadows sunShadows;
        private bool fog;
        private Material skybox;
        private UnityEngine.Rendering.AmbientMode ambientMode;
        private ParticleSystem leaves;
        private float stepDust;

        private void SetLook(bool on)
        {
            if (on)
            {
                sun = FindAnyObjectByType<Light>();
                sunRot = sun.transform.rotation;
                sunColor = sun.color;
                sunIntensity = sun.intensity;
                sunShadows = sun.shadows;
                fog = RenderSettings.fog;
                skybox = RenderSettings.skybox;
                ambientMode = RenderSettings.ambientMode;
                ambientSky = RenderSettings.ambientLight;

                sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                sun.color = new Color(1f, 0.94f, 0.84f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.75f;
                QualitySettings.shadowDistance = 45f;
                RenderSettings.skybox = Resources.Load<Material>("Forest/Sky");
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.56f, 0.64f, 0.72f);
                RenderSettings.ambientEquatorColor = new Color(0.42f, 0.47f, 0.4f);
                RenderSettings.ambientGroundColor = new Color(0.24f, 0.23f, 0.19f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.74f, 0.8f, 0.84f);
                RenderSettings.fogStartDistance = 40f;
                RenderSettings.fogEndDistance = 190f;
                rig.SetOutdoor(true);
            }
            else
            {
                sun.transform.rotation = sunRot;
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.shadows = sunShadows;
                RenderSettings.fog = fog;
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientSky;
                rig.SetOutdoor(false);
                rig.EndChase();
            }
        }

        /// <summary>Leaves and pollen drifting down through the light around the camera.</summary>
        private void BuildAmbience()
        {
            var go = new GameObject("DriftingLeaves");
            go.transform.SetParent(transform, false);
            leaves = go.AddComponent<ParticleSystem>();
            leaves.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = leaves.main;
            main.startLifetime = 10f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.75f, 0.35f, 0.9f), new Color(0.55f, 0.75f, 0.3f, 0.9f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = leaves.emission;
            emission.rateOverTime = 14f;
            var shape = leaves.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 9f, 26f);
            var vel = leaves.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.45f, -0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            var noise = leaves.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.35f;
            var rot = leaves.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Resources.Load<Material>("SquashBot_ParticleAlpha");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            leaves.Play();
        }

        private void UpdateAmbience(float dt, bool walking)
        {
            if (leaves != null) leaves.transform.position = rig.Cam.transform.position + rig.Cam.transform.forward * 9f;
            if (!walking || !grounded) return;
            stepDust -= dt;
            if (stepDust > 0f) return;
            stepDust = 0.32f;
            bool deck = pos.y > ForestWorld.DeckHeight - 0.5f;
            fx.Dust(world.ToWorld(pos) + Vector3.up * 0.05f, deck ? new Color(0.6f, 0.62f, 0.55f) : new Color(0.45f, 0.36f, 0.26f), 3, 0.7f);
        }

        /// <summary>
        /// The light follows the camera through the passages: a cave swallows the daylight a few metres in and gives
        /// it back at the far mouth; a gorge only shades it. The camera's place in the land it left and in the land
        /// ahead tells how deep in it is.
        /// </summary>
        private void UpdateDaylight()
        {
            if (sun == null || rig == null) return;
            var camPos = rig.Cam.transform.position;
            float depth = 0f;
            var style = PassageStyle.None;
            if (world != null)
            {
                float zIn = camPos.z - world.Origin.z;
                depth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(world.TunnelZ - 1f, world.TunnelZ + 9f, zIn));
                style = world.Exit;
                if (world.Entry != PassageStyle.None && !suspended)
                {
                    depth = Mathf.Max(depth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ForestWorld.EntryMouthZ + 1f, ForestWorld.EntryMouthZ - 9f, zIn)));
                    if (zIn < world.TunnelZ - 20f) style = world.Entry;
                }
            }
            if (nextWorld != null)
            {
                float zOut = camPos.z - nextWorld.Origin.z;
                depth = Mathf.Min(depth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ForestWorld.EntryMouthZ + 1f, ForestWorld.EntryMouthZ - 9f, zOut)));
            }
            float dark = depth * (style == PassageStyle.Cave ? 0.93f : style == PassageStyle.Gorge ? 0.4f : 0f);
            sun.intensity = Mathf.Lerp(1.35f, 0.08f, dark);
            float a = Mathf.Lerp(1f, 0.16f, dark);
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.64f, 0.72f) * a;
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.47f, 0.4f) * a;
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.23f, 0.19f) * a;
            RenderSettings.fogColor = Color.Lerp(new Color(0.74f, 0.8f, 0.84f), new Color(0.05f, 0.05f, 0.06f), dark);
            if (leaves != null)
            {
                var em = leaves.emission;
                em.rateOverTime = 14f * (1f - depth);
            }
        }
    }
}
