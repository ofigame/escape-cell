using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The magic that hurts a monster: a swirling violet orb on a tile, marked by a column of light. Picking it up wraps
    /// the robot in a glowing aura (<see cref="CreateAura"/>) until it hits the monster.
    /// </summary>
    public class MagicOrb : MonoBehaviour
    {
        private static readonly Color Violet = new Color(0.75f, 0.45f, 1f);
        private static readonly Color VioletGlow = new Color(1.8f, 0.9f, 2.8f);

        private Transform core, ring1, ring2, beam;
        private Material beamMat;
        private float time, popT;

        /// <summary>True in the orb's last seconds on a tile: it flickers before jumping to another one.</summary>
        public bool Leaving { get; set; }

        public static MagicOrb Create(Vector3 position)
        {
            var go = new GameObject("MagicOrb");
            go.transform.position = position;
            var orb = go.AddComponent<MagicOrb>();
            orb.Build();
            return orb;
        }

        private void Build()
        {
            var glow = MaterialFactory.Create(Violet, VioletGlow);
            var gold = MaterialFactory.Create(new Color(1f, 0.85f, 0.4f), new Color(2.2f, 1.6f, 0.4f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Disc", transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.8f, 0.004f, 0.8f),
                MaterialFactory.CreateTransparent(new Color(0.75f, 0.45f, 1f, 0.4f), VioletGlow * 0.6f));
            core = Shapes.Primitive(PrimitiveType.Sphere, "Core", transform, new Vector3(0f, 0.6f, 0f), Vector3.one * 0.32f, glow).transform;
            ring1 = new GameObject("Ring1").transform;
            ring1.SetParent(transform, false);
            ring1.localPosition = core.localPosition;
            ring2 = new GameObject("Ring2").transform;
            ring2.SetParent(transform, false);
            ring2.localPosition = core.localPosition;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Shapes.Rounded("Bit", ring1, new Vector3(Mathf.Cos(a) * 0.3f, 0f, Mathf.Sin(a) * 0.3f), new Vector3(0.06f, 0.06f, 0.06f), 0.02f, gold);
                Shapes.Rounded("Bit", ring2, new Vector3(Mathf.Cos(a) * 0.38f, Mathf.Sin(a) * 0.38f, 0f), new Vector3(0.05f, 0.05f, 0.05f), 0.02f, glow);
            }
            beamMat = MaterialFactory.CreateTransparent(new Color(0.75f, 0.45f, 1f, 0.3f), VioletGlow);
            beam = Shapes.Primitive(PrimitiveType.Cylinder, "Beam", transform, new Vector3(0f, 2.2f, 0f), new Vector3(0.34f, 2.2f, 0.34f), beamMat).transform;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            time += Time.deltaTime;
            popT = Mathf.Min(1f, popT + Time.deltaTime * 3f);
            transform.localScale = Vector3.one * (popT < 1f ? Mathf.Sin(popT * Mathf.PI * 0.5f) * (1f + Mathf.Sin(popT * Mathf.PI) * 0.3f) : 1f);
            if (Leaving) transform.localScale *= 0.8f + 0.2f * Mathf.Sin(time * 18f);
            core.localPosition = new Vector3(0f, 0.6f + Mathf.Sin(time * 3f) * 0.08f, 0f);
            ring1.localPosition = ring2.localPosition = core.localPosition;
            ring1.localRotation = Quaternion.Euler(20f, time * 140f, 0f);
            ring2.localRotation = Quaternion.Euler(0f, time * -90f, 30f);
            core.localScale = Vector3.one * (0.32f + Mathf.Sin(time * 8f) * 0.03f);
            MaterialFactory.SetColors(beamMat, new Color(0.75f, 0.45f, 1f, 0.24f + Mathf.Sin(time * 4f) * 0.08f), VioletGlow);
        }

        /// <summary>The glow around a robot carrying the magic.</summary>
        public static GameObject CreateAura(Transform robot)
        {
            var aura = new GameObject("MagicAura");
            aura.transform.SetParent(robot, false);
            aura.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            Shapes.Primitive(PrimitiveType.Sphere, "Glow", aura.transform, Vector3.zero, Vector3.one * 1.05f,
                MaterialFactory.CreateTransparent(new Color(0.75f, 0.45f, 1f, 0.28f), VioletGlow));
            var gold = MaterialFactory.Create(new Color(1f, 0.85f, 0.4f), new Color(2.2f, 1.6f, 0.4f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Shapes.Rounded("Spark", aura.transform, new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f), new Vector3(0.07f, 0.07f, 0.07f), 0.02f, gold);
            }
            aura.AddComponent<Spin>();
            return aura;
        }

        /// <summary>Keeps the aura's sparks circling.</summary>
        private class Spin : MonoBehaviour
        {
            private void Update() => transform.Rotate(0f, Time.deltaTime * 200f, 0f, Space.Self);
        }
    }
}
