using System.Collections.Generic;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The four big tunnel themes, one per stretch of the campaign:
    /// neon speed tunnel (hexagonal tube of light), laser grid corridor (steel walls, sweeping red beams, sparks),
    /// crystal cavern (rock walls bristling with glowing crystals) and zero-G void (a hologram tube over a galaxy).
    /// Same running rules as every course; these only change how it looks, plus floaty movement in the void.
    /// </summary>
    public partial class DuctRunner
    {
        private bool openTop;

        private bool IsThemed => kind == Kind.Neon || kind == Kind.Laser || kind == Kind.Crystal || kind == Kind.Void;

        /// <summary>The theme a road uses for a level (0-based): a new one every 50 levels.</summary>
        public static Kind RoadTheme(int level) => level < 50 ? Kind.Neon : level < 100 ? Kind.Laser : level < 150 ? Kind.Crystal : Kind.Void;

        // Zero-G: lighter gravity and slower lane changes (less grip).
        private float GravityScale => kind == Kind.Void ? 0.62f : 1f;
        private float LaneScale => kind == Kind.Void ? 0.7f : 1f;

        // A burst of lens width on jump pads: the "warp" feeling.
        private float fovKick;
        private float CameraFov => 62f + fovKick * 18f;

        private Material neonA, neonB, panelMat, laserMat, steelMat, rockMat, holoMat, holoLineMat;
        private Material[] crystalMats;
        private ParticleSystem speedLines, themeDust;
        private TrailRenderer trail;
        private Transform sky;
        private float sparkTimer;

        private void ThemeMaterials()
        {
            switch (kind)
            {
                case Kind.Neon:
                    neonA = MaterialFactory.Create(new Color(0.3f, 0.9f, 1f), new Color(0.4f, 1.8f, 2.6f));
                    neonB = MaterialFactory.Create(new Color(0.9f, 0.4f, 1f), new Color(1.9f, 0.5f, 2.6f));
                    panelMat = MaterialFactory.Create(new Color(0.08f, 0.08f, 0.2f), new Color(0.02f, 0.02f, 0.08f));
                    frameMat = MaterialFactory.Create(new Color(0.3f, 0.9f, 1f), new Color(0.3f, 1.4f, 2f));
                    topMat = MaterialFactory.Create(new Color(0.12f, 0.12f, 0.3f), new Color(0.05f, 0.05f, 0.18f));
                    slabMat = MaterialFactory.Create(new Color(0.06f, 0.06f, 0.16f), Color.black);
                    blockMat = MaterialFactory.Create(new Color(1f, 0.3f, 0.6f), new Color(1.8f, 0.3f, 0.9f));
                    break;
                case Kind.Laser:
                    laserMat = MaterialFactory.Create(new Color(1f, 0.2f, 0.2f), new Color(4f, 0.4f, 0.35f));
                    steelMat = MaterialFactory.Create(new Color(0.32f, 0.33f, 0.38f), Color.black);
                    panelMat = MaterialFactory.Create(new Color(0.22f, 0.22f, 0.26f), Color.black);
                    frameMat = MaterialFactory.Create(new Color(0.9f, 0.25f, 0.2f), new Color(1.4f, 0.25f, 0.2f));
                    topMat = MaterialFactory.Create(new Color(0.3f, 0.3f, 0.34f), new Color(0.04f, 0.02f, 0.02f));
                    slabMat = MaterialFactory.Create(new Color(0.18f, 0.18f, 0.22f), Color.black);
                    blockMat = MaterialFactory.Create(new Color(0.45f, 0.45f, 0.5f), new Color(0.4f, 0.05f, 0.05f));
                    barMat = laserMat;
                    hurdleMat = laserMat;
                    break;
                case Kind.Crystal:
                    rockMat = MaterialFactory.Create(new Color(0.3f, 0.22f, 0.38f), Color.black);
                    crystalMats = new[]
                    {
                        MaterialFactory.Create(new Color(0.85f, 0.5f, 1f), new Color(1.6f, 0.6f, 2.4f)),
                        MaterialFactory.Create(new Color(1f, 0.55f, 0.85f), new Color(2.4f, 0.7f, 1.6f)),
                        MaterialFactory.Create(new Color(0.5f, 0.95f, 1f), new Color(0.5f, 1.8f, 2.4f)),
                    };
                    frameMat = MaterialFactory.Create(new Color(0.75f, 0.55f, 1f), new Color(0.9f, 0.5f, 1.6f));
                    topMat = MaterialFactory.Create(new Color(0.42f, 0.34f, 0.5f), new Color(0.12f, 0.06f, 0.16f));
                    slabMat = rockMat;
                    blockMat = MaterialFactory.Create(new Color(0.4f, 0.32f, 0.45f), new Color(0.12f, 0.04f, 0.16f));
                    break;
                case Kind.Void:
                    holoMat = MaterialFactory.CreateTransparent(new Color(0.3f, 0.9f, 1f, 0.35f), new Color(0.3f, 1.6f, 2.2f));
                    holoLineMat = MaterialFactory.Create(new Color(0.4f, 1f, 1f), new Color(0.5f, 2.2f, 2.6f));
                    frameMat = MaterialFactory.Create(new Color(0.4f, 1f, 1f), new Color(0.4f, 1.8f, 2.4f));
                    topMat = MaterialFactory.CreateTransparent(new Color(0.1f, 0.35f, 0.6f, 0.22f), new Color(0.05f, 0.25f, 0.45f));
                    slabMat = MaterialFactory.CreateTransparent(new Color(0.1f, 0.3f, 0.5f, 0.12f), Color.black);
                    blockMat = MaterialFactory.Create(new Color(0.45f, 0.4f, 0.42f), new Color(0.35f, 0.12f, 0.05f));
                    hurdleMat = holoLineMat;
                    barMat = MaterialFactory.Create(new Color(1f, 0.5f, 1f), new Color(2.2f, 0.8f, 2.4f));
                    break;
            }
        }

        // ---------- Rows ----------

        private void BuildThemeRow(Transform root, int r)
        {
            // A road leaving a platform starts as an open bridge: while the robot still walks to the exit, the camera looks
            // down on it from above, and a roof or tall walls right at the edge would cover the floor.
            openTop = roadMode && r < 8;
            switch (kind)
            {
                case Kind.Neon: NeonRow(root, r); break;
                case Kind.Laser: LaserRow(root, r); break;
                case Kind.Crystal: CrystalRow(root, r); break;
                case Kind.Void: VoidRow(root, r); break;
            }
        }

        /// <summary>A hexagonal tube: dark panels edged with cyan and magenta light, a bright frame every few rows.</summary>
        private void NeonRow(Transform root, int r)
        {
            const float radius = 3f;
            var centre = new Vector3(0f, 1.35f, 0f);
            float apothem = radius * Mathf.Cos(30f * Mathf.Deg2Rad);
            for (int i = 0; i < 6; i++)
            {
                float angle = 30f + i * 60f;
                if (Mathf.Approximately(angle, 270f)) continue; // the floor is the running lanes
                if (openTop && angle < 180f) continue;
                var dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f);
                var panel = Shapes.Rounded("Panel", root, centre + dir * apothem, new Vector3(radius, 0.12f, 1.04f), 0.04f, panelMat).transform;
                panel.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                // Light strips run along the tube at the corners.
                float corner = (angle + 30f) * Mathf.Deg2Rad;
                var at = centre + new Vector3(Mathf.Cos(corner), Mathf.Sin(corner), 0f) * (radius - 0.08f);
                Shapes.Rounded("Strip", root, at, new Vector3(0.07f, 0.07f, 1.06f), 0.02f, i % 2 == 0 ? neonA : neonB);
            }
            if (r % 4 == 0 && !openTop)
            {
                // A ring of light around the whole tube.
                for (int i = 0; i < 6; i++)
                {
                    float angle = 30f + i * 60f;
                    var dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f);
                    var edge = Shapes.Rounded("Ring", root, centre + dir * (apothem - 0.06f), new Vector3(radius, 0.06f, 0.08f), 0.02f, r % 8 == 0 ? neonB : neonA).transform;
                    edge.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                }
            }
        }

        /// <summary>A steel corridor with red light strips, vents and warning stripes.</summary>
        private void LaserRow(Transform root, int r)
        {
            if (openTop) return;
            // A low steel ceiling, high above the camera: it closes the corridor in without ever hiding the robot.
            Shapes.Rounded("Ceiling", root, new Vector3(0f, 3.3f, 0f), new Vector3(WallX * 2f + 0.8f, 0.2f, 1.06f), 0.04f, panelMat);
            if (r % 3 == 0)
                foreach (float s in new[] { -0.8f, 0.8f })
                    Shapes.Rounded("CeilingLight", root, new Vector3(s, 3.18f, 0f), new Vector3(0.5f, 0.05f, 0.5f), 0.02f, laserMat);
            foreach (float side in new[] { -1f, 1f })
            {
                Shapes.Rounded("Wall", root, new Vector3(side * (WallX + 0.1f), 1.2f, 0f), new Vector3(0.3f, 2.8f, 1.06f), 0.04f, steelMat);
                Shapes.Rounded("Strip", root, new Vector3(side * (WallX - 0.07f), 0.25f, 0f), new Vector3(0.04f, 0.06f, 1.06f), 0.015f, laserMat);
                if (r % 3 == 0)
                    Shapes.Rounded("Rib", root, new Vector3(side * (WallX - 0.02f), 1.3f, 0f), new Vector3(0.12f, 2.2f, 0.16f), 0.03f, panelMat);
                if (r % 6 == 0)
                {
                    Shapes.Rounded("Lamp", root, new Vector3(side * (WallX - 0.06f), 2.3f, 0f), new Vector3(0.08f, 0.1f, 0.4f), 0.03f, laserMat);
                    for (int i = 0; i < 3; i++)
                        Shapes.Rounded("Vent", root, new Vector3(side * (WallX - 0.06f), 0.9f + i * 0.18f, 0.2f), new Vector3(0.05f, 0.06f, 0.5f), 0.02f, slabMat);
                }
            }
        }

        /// <summary>A cave: lumpy rock walls, glowing crystals leaning in from the sides and hanging high above.</summary>
        private void CrystalRow(Transform root, int r)
        {
            var rng = new System.Random(r * 7717 + 3);
            foreach (float side in new[] { -1f, 1f })
            {
                var rock = Shapes.Rounded("Rock", root, new Vector3(side * (WallX + 0.3f), 0.8f + (float)rng.NextDouble() * 0.4f, 0f),
                    new Vector3(0.9f, 2f + (float)rng.NextDouble(), 1.3f), 0.35f, rockMat).transform;
                rock.localRotation = Quaternion.Euler((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 30f, side * (8f + (float)rng.NextDouble() * 10f));
                if (rng.Next(3) == 0)
                {
                    // A cluster of two or three crystals leaning towards the lanes.
                    int n = 2 + rng.Next(2);
                    for (int i = 0; i < n; i++)
                    {
                        float h = 0.8f + (float)rng.NextDouble() * 1.6f;
                        var c = Shapes.Rounded("Crystal", root, new Vector3(side * (WallX - 0.1f), h * 0.5f + 0.1f, ((float)rng.NextDouble() - 0.5f) * 0.6f),
                            new Vector3(0.22f, h, 0.22f), 0.05f, crystalMats[rng.Next(crystalMats.Length)]).transform;
                        c.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 30f, rng.Next(90), -side * (15f + (float)rng.NextDouble() * 25f));
                    }
                }
            }
            if (r % 5 == 0 && !openTop)
            {
                // Crystals hanging from the cave roof, well above the camera's line of sight.
                for (int i = 0; i < 3; i++)
                {
                    float h = 0.6f + (float)rng.NextDouble() * 0.8f;
                    var c = Shapes.Rounded("Stalactite", root, new Vector3(((float)rng.NextDouble() - 0.5f) * 4.4f, 4.2f - h * 0.5f, 0f),
                        new Vector3(0.2f, h, 0.2f), 0.05f, crystalMats[rng.Next(crystalMats.Length)]).transform;
                    c.localRotation = Quaternion.Euler(0f, 45f, ((float)rng.NextDouble() - 0.5f) * 20f);
                }
                Shapes.Rounded("Roof", root, new Vector3(0f, 4.4f, 0f), new Vector3(6f, 0.6f, 1.3f), 0.3f, rockMat);
            }
        }

        /// <summary>No walls, no ground: glass lanes inside rings of hologram light.</summary>
        private void VoidRow(Transform root, int r)
        {
            if (r % 3 != 0) return;
            const int pieces = 18;
            const float radius = 2.5f;
            var centre = new Vector3(0f, 1.2f, 0f);
            for (int i = 0; i < pieces; i++)
            {
                float a = i * Mathf.PI * 2f / pieces;
                var seg = Shapes.Rounded("Holo", root, centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius,
                    new Vector3(0.9f, 0.05f, 0.06f), 0.02f, r % 9 == 0 ? holoLineMat : holoMat).transform;
                seg.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
            }
            // Faint lines along the tube.
            foreach (float a in new[] { 20f, 160f, 60f, 120f })
            {
                var at = centre + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f) * radius;
                Shapes.Rounded("Line", root, at, new Vector3(0.03f, 0.03f, 3.1f), 0.01f, holoMat);
            }
        }

        // ---------- Obstacles ----------

        /// <summary>Restyles obstacles for the themes; false = use the standard look.</summary>
        private bool ThemedObstacle(Obstacle o, Transform go, ObstacleKind k, int l, int r, float w)
        {
            switch (kind)
            {
                case Kind.Laser when k == ObstacleKind.Bar || k == ObstacleKind.Hurdle:
                {
                    // A laser beam across the corridor between two emitters: low (jump it) or at head height (slide).
                    float y = k == ObstacleKind.Bar ? 0.95f : 0.32f;
                    Shapes.Rounded("Beam", go, new Vector3(0f, y, 0f), new Vector3(w + 0.6f, 0.07f, 0.07f), 0.03f, laserMat);
                    Shapes.Rounded("Glow", go, new Vector3(0f, y, 0f), new Vector3(w + 0.6f, 0.16f, 0.02f), 0.01f,
                        MaterialFactory.CreateTransparent(new Color(1f, 0.2f, 0.2f, 0.35f), new Color(2f, 0.2f, 0.2f)));
                    if (k == ObstacleKind.Bar)
                        Shapes.Rounded("Beam2", go, new Vector3(0f, y + 0.3f, 0f), new Vector3(w + 0.6f, 0.05f, 0.05f), 0.02f, laserMat);
                    foreach (float s in new[] { -1f, 1f })
                        Shapes.Rounded("Emitter", go, new Vector3(s * (w * 0.5f + 0.3f), y, 0f), new Vector3(0.2f, 0.26f, 0.26f), 0.05f, steelMat);
                    return true;
                }
                case Kind.Laser when k == ObstacleKind.Mover:
                    // A vertical beam sweeping from lane to lane.
                    Shapes.Rounded("Beam", go, new Vector3(0f, 1.2f, 0f), new Vector3(0.08f, 2.4f, 0.08f), 0.03f, laserMat);
                    Shapes.Rounded("Glow", go, new Vector3(0f, 1.2f, 0f), new Vector3(0.22f, 2.4f, 0.02f), 0.01f,
                        MaterialFactory.CreateTransparent(new Color(1f, 0.2f, 0.2f, 0.35f), new Color(2f, 0.2f, 0.2f)));
                    Shapes.Rounded("Base", go, new Vector3(0f, 0.05f, 0f), new Vector3(0.4f, 0.1f, 0.4f), 0.04f, steelMat);
                    return true;
                case Kind.Crystal when k == ObstacleKind.Hurdle:
                    // A row of short crystals across the floor.
                    for (int i = 0; i < 7; i++)
                    {
                        var c = Shapes.Rounded("Crystal", go, new Vector3(-w * 0.5f + (i + 0.5f) * w / 7f, 0.22f, 0f), new Vector3(0.18f, 0.44f, 0.18f), 0.04f, crystalMats[i % 3]).transform;
                        c.localRotation = Quaternion.Euler(0f, 45f, (i % 2 == 0 ? 1f : -1f) * 12f);
                    }
                    return true;
                case Kind.Crystal when k == ObstacleKind.Bar:
                    // A rock lintel with crystals hanging down: slide under.
                    Shapes.Rounded("Lintel", go, new Vector3(0f, 1.25f, 0f), new Vector3(w + 0.8f, 0.5f, 0.5f), 0.2f, rockMat);
                    for (int i = 0; i < 6; i++)
                        Shapes.Rounded("Spike", go, new Vector3(-w * 0.45f + i * w * 0.18f, 0.88f, 0f), new Vector3(0.16f, 0.4f, 0.16f), 0.04f, crystalMats[i % 3])
                            .transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    return true;
                case Kind.Void when k == ObstacleKind.Drop || k == ObstacleKind.Mover:
                {
                    // Meteors drifting in the lanes instead of falling blocks.
                    float my = k == ObstacleKind.Drop ? -0.05f : 0.45f;
                    var rock = Shapes.Rounded("Meteor", go, new Vector3(0f, my, 0f), new Vector3(0.85f, 0.75f, 0.8f), 0.3f, blockMat).transform;
                    rock.localRotation = Quaternion.Euler(r * 37f, r * 61f, r * 13f);
                    Shapes.Primitive(PrimitiveType.Sphere, "Glow", go, new Vector3(0f, my, -0.3f), Vector3.one * 0.5f,
                        MaterialFactory.CreateTransparent(new Color(1f, 0.5f, 0.2f, 0.3f), new Color(2f, 0.6f, 0.2f)));
                    o.y = 0f;
                    o.landed = true;
                    return true;
                }
                case Kind.Void when k == ObstacleKind.Hurdle || k == ObstacleKind.Bar:
                {
                    float y = k == ObstacleKind.Bar ? 1.05f : 0.3f;
                    float h = k == ObstacleKind.Bar ? 0.6f : 0.3f;
                    Shapes.Rounded("Field", go, new Vector3(0f, y, 0f), new Vector3(w + 0.2f, h, 0.06f), 0.03f,
                        MaterialFactory.CreateTransparent(k == ObstacleKind.Bar ? new Color(1f, 0.4f, 1f, 0.4f) : new Color(0.4f, 1f, 1f, 0.4f), k == ObstacleKind.Bar ? new Color(2f, 0.6f, 2.2f) : new Color(0.4f, 1.8f, 2.2f)));
                    Shapes.Rounded("Edge", go, new Vector3(0f, y + h * 0.5f, 0f), new Vector3(w + 0.2f, 0.04f, 0.08f), 0.02f, k == ObstacleKind.Bar ? barMat : holoLineMat);
                    Shapes.Rounded("Edge", go, new Vector3(0f, y - h * 0.5f, 0f), new Vector3(w + 0.2f, 0.04f, 0.08f), 0.02f, k == ObstacleKind.Bar ? barMat : holoLineMat);
                    return true;
                }
            }
            return false;
        }

        // ---------- Effects ----------

        private void StartThemeFx()
        {
            fovKick = 0f;
            // Speed lines streaming past the camera on every course; denser and brighter in the neon tunnel.
            var lineColor = kind == Kind.Laser ? new Color(2f, 0.5f, 0.4f) : kind == Kind.Crystal ? new Color(1.4f, 0.8f, 2f) : new Color(0.6f, 1.4f, 2f);
            speedLines = Stream("SpeedLines", Weather.AddMaterial, lineColor, kind == Kind.Neon ? 70f : IsThemed ? 40f : 18f, 0.04f, new Vector3(5f, 3.2f, 1f));
            var r = speedLines.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.12f;

            if (kind == Kind.Crystal)
                themeDust = Stream("Glints", Weather.AddMaterial, new Color(1.6f, 0.9f, 2.2f), 30f, 0.1f, new Vector3(6f, 4f, 1f), 0.4f);
            else if (kind == Kind.Void)
            {
                themeDust = Stream("StarDust", Weather.AddMaterial, new Color(0.8f, 0.9f, 1.6f), 40f, 0.06f, new Vector3(14f, 10f, 1f), 0.5f);
                BuildSky();
            }

            // A glowing trail behind the robot: the garage's trail colour, or the theme's light.
            var trailColor = robot.TrailColor ?? (kind == Kind.Laser ? new Color(1f, 0.3f, 0.3f) : kind == Kind.Crystal ? new Color(0.9f, 0.5f, 1f) : new Color(0.3f, 0.9f, 1f));
            trail = robot.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Weather.AddMaterial;
            trail.time = 0.35f;
            trail.minVertexDistance = 0.08f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.22f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(trailColor * 1.6f, 0f), new GradientColorKey(trailColor, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Particles born ahead of the camera and flying back past it, so they always stream at the player.</summary>
        private ParticleSystem Stream(string name, Material mat, Color color, float rate, float size, Vector3 box, float speedFactor = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig.Cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 16f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 600;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-22f * speedFactor, -30f * speedFactor);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.6f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>The void's sky: a spiral galaxy far below and ahead, and a few planets, kept at a fixed distance from the camera.</summary>
        private void BuildSky()
        {
            sky = new GameObject("VoidSky").transform;
            var ps = sky.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1500;
            var emission = ps.emission;
            emission.enabled = false;
            var r = sky.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Weather.AddMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rng = new System.Random(5);
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < 1400; i++)
            {
                // Two spiral arms with a bright core.
                float t = (float)rng.NextDouble();
                int arm = rng.Next(2);
                float angle = t * 9f + arm * Mathf.PI;
                float dist = t * 34f;
                var jitter = new Vector3((float)rng.NextDouble() - 0.5f, ((float)rng.NextDouble() - 0.5f) * 0.3f, (float)rng.NextDouble() - 0.5f) * (2f + dist * 0.25f);
                p.position = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist) + jitter;
                p.startSize = Mathf.Lerp(1.2f, 0.35f, t) * (0.6f + (float)rng.NextDouble());
                var c = Color.Lerp(new Color(1.6f, 1.2f, 1.8f), Color.Lerp(new Color(0.4f, 0.6f, 1.8f), new Color(1.4f, 0.4f, 1.4f), (float)rng.NextDouble()), Mathf.Sqrt(t));
                p.startColor = c;
                p.startLifetime = 100000f;
                ps.Emit(p, 1);
            }
            var planets = new[] { (new Vector3(-30f, 14f, 40f), 6f, new Color(0.9f, 0.5f, 0.3f)), (new Vector3(36f, 22f, 60f), 9f, new Color(0.5f, 0.6f, 1f)), (new Vector3(14f, -6f, 30f), 3f, new Color(0.8f, 0.9f, 1f)) };
            foreach (var (pos, size, color) in planets)
                Shapes.Primitive(PrimitiveType.Sphere, "Planet", sky, pos, Vector3.one * size, MaterialFactory.Create(color, color * 0.4f));
        }

        private void UpdateThemeFx(float dt)
        {
            fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 1.4f);
            if (sky != null)
            {
                // Far below and ahead of the camera, turning slowly.
                var cam = rig.Cam.transform;
                sky.position = cam.position + new Vector3(cam.forward.x, 0f, cam.forward.z).normalized * 55f + Vector3.down * 9f;
                sky.rotation = Quaternion.LookRotation(new Vector3(cam.forward.x, 0f, cam.forward.z)) * Quaternion.Euler(-28f, 0f, 0f) * Quaternion.Euler(0f, time * 3f, 0f);
            }
            if (kind == Kind.Laser && Active && !ended)
            {
                // Sparks spray from the walls now and then.
                sparkTimer -= dt;
                if (sparkTimer <= 0f)
                {
                    sparkTimer = Random.Range(0.25f, 0.8f);
                    float side = Random.value < 0.5f ? -1f : 1f;
                    var at = World(side * (WallX - 0.1f), Random.Range(0.6f, 2.2f), z + Random.Range(4f, 14f));
                    fx.Burst(at, new Color(1f, 0.7f, 0.3f), new Color(3f, 1.4f, 0.3f), 10, 3.5f);
                }
            }
            if (laserFlicker && obstacles.Count > 0 && laserMat != null)
            {
                float f = 3.2f + Mathf.Sin(time * 40f) * 0.8f;
                MaterialFactory.SetColors(laserMat, new Color(1f, 0.2f, 0.2f), new Color(f, 0.35f, 0.3f));
            }
        }

        private bool laserFlicker => kind == Kind.Laser;

        private void StopThemeFx()
        {
            if (speedLines != null) Destroy(speedLines.gameObject);
            if (themeDust != null) Destroy(themeDust.gameObject);
            if (sky != null) Destroy(sky.gameObject);
            if (trail != null) Destroy(trail);
            speedLines = themeDust = null;
            sky = null;
            trail = null;
        }
    }
}
