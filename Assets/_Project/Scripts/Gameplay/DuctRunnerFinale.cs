using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The last road (after level 250): vanG's core flees down the Star Road and the robot chases it. The road falls
    /// apart behind the robot, pixels fly off the tunnel, and the ground shakes now and then; the core waits at the
    /// end, on the landing of vanG's main server.
    /// </summary>
    public partial class DuctRunner
    {
        private const float FinaleLength = 260f;
        /// <summary>How far ahead of the robot the core flees.</summary>
        private const float CoreLead = 11f;

        private bool finale;
        private Transform core, coreRing;
        private float debrisTimer, quakeTimer;

        private static readonly Color CoreRed = new Color(1f, 0.35f, 0.45f);

        private void BuildCore()
        {
            core = new GameObject("vanGCore").transform;
            core.SetParent(transform, false);
            Shapes.Primitive(PrimitiveType.Sphere, "Glow", core, Vector3.zero, Vector3.one * 1.6f,
                MaterialFactory.CreateTransparent(new Color(1f, 0.3f, 0.4f, 0.22f), new Color(1.6f, 0.3f, 0.5f)));
            Shapes.Primitive(PrimitiveType.Sphere, "Core", core, Vector3.zero, Vector3.one * 0.8f, MaterialFactory.Create(CoreRed, new Color(2.6f, 0.5f, 0.8f)));
            Shapes.Primitive(PrimitiveType.Sphere, "Eye", core, new Vector3(0f, 0f, -0.32f), Vector3.one * 0.3f, MaterialFactory.Create(Color.white, new Color(2.4f, 2.2f, 2.4f)));
            coreRing = new GameObject("Ring").transform;
            coreRing.SetParent(core, false);
            var ring = MaterialFactory.Create(new Color(1f, 0.6f, 0.7f), new Color(1.8f, 0.6f, 0.9f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Shapes.Rounded("Bit", coreRing, new Vector3(Mathf.Cos(a) * 0.75f, Mathf.Sin(a) * 0.75f, 0f), Vector3.one * 0.14f, 0.03f, ring);
            }
            debrisTimer = 0f;
            quakeTimer = 1.5f;
        }

        private void UpdateFinale(float dt)
        {
            if (core == null) return;
            float cz = Mathf.Min(z + CoreLead, length + 3f);
            core.position = World(Mathf.Sin(time * 1.3f) * 0.6f, 1.4f + Mathf.Sin(time * 2.7f) * 0.2f, cz);
            core.rotation = Rotation(cz) * Quaternion.Euler(0f, 180f, 0f);
            coreRing.localRotation = Quaternion.Euler(0f, 0f, time * 120f);
            if (ended) return;

            // Pixels breaking off the tunnel ahead.
            debrisTimer -= dt;
            if (debrisTimer <= 0f)
            {
                debrisTimer = 0.12f;
                float dz = z + Random.Range(4f, 20f);
                fx.Burst(World(Random.Range(-2.2f, 2.2f), Random.Range(1.5f, 3.5f), dz), Random.value < 0.5f ? CoreRed : WorldTheme.Current.accent,
                    new Color(1.4f, 0.5f, 1.2f), 5, 3f);
            }
            quakeTimer -= dt;
            if (quakeTimer <= 0f)
            {
                quakeTimer = Random.Range(1.2f, 2f);
                rig.Shake(0.35f);
            }
        }

        /// <summary>On the last road the rows behind the robot crumble and drop away.</summary>
        private float CollapseDrop(int row)
        {
            if (!finale) return 0f;
            float behind = z - 1.5f - row;
            return behind > 0f ? behind * behind * 0.35f : 0f;
        }

        private void ClearFinale()
        {
            if (core != null) Destroy(core.gameObject);
            core = coreRing = null;
        }
    }
}
