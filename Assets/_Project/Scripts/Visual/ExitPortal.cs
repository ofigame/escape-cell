using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The exit door of Exit missions: a dim, slowly turning gate while closed, then it bursts open into
    /// spinning rings with a beam of light and rising sparks.
    /// </summary>
    public class ExitPortal : MonoBehaviour
    {
        private const int RingPieces = 12;

        private Transform ring, innerRing, beam, sparks, arrow;
        private Material ringMaterial, coreMaterial, beamMaterial;
        private Transform[] sparkPieces;
        private float openT = -1f; // < 0: closed
        private float time;

        public bool IsOpen => openT >= 0f;

        public static ExitPortal Create(Vector3 position)
        {
            var go = new GameObject("ExitPortal");
            go.transform.position = position;
            var portal = go.AddComponent<ExitPortal>();
            portal.Build();
            return portal;
        }

        private void Build()
        {
            ringMaterial = MaterialFactory.Create(new Color(0.5f, 0.9f, 1f), new Color(0.35f, 0.9f, 1.2f));
            coreMaterial = MaterialFactory.CreateTransparent(new Color(0.55f, 0.95f, 1f, 0.35f), new Color(0.3f, 0.8f, 1.1f));
            beamMaterial = MaterialFactory.CreateTransparent(new Color(0.6f, 0.95f, 1f, 0.0f), new Color(0.5f, 1.4f, 1.8f));

            Shapes.Primitive(PrimitiveType.Cylinder, "Core", transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.62f, 0.005f, 0.62f), coreMaterial);

            ring = MakeRing("Ring", 0.36f, 0.09f);
            innerRing = MakeRing("InnerRing", 0.22f, 0.06f);

            beam = Shapes.Primitive(PrimitiveType.Cylinder, "Beam", transform, new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 1.5f, 0.5f), beamMaterial).transform;
            beam.gameObject.SetActive(false);

            // A bobbing arrow above the closed door: "the way out is here".
            arrow = new GameObject("Arrow").transform;
            arrow.SetParent(transform, false);
            var arrowMaterial = MaterialFactory.Create(new Color(0.6f, 1f, 1f), new Color(0.6f, 1.8f, 2.2f));
            Shapes.Rounded("L", arrow, new Vector3(-0.07f, 0f, 0f), new Vector3(0.2f, 0.06f, 0.06f), 0.025f, arrowMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
            Shapes.Rounded("R", arrow, new Vector3(0.07f, 0f, 0f), new Vector3(0.2f, 0.06f, 0.06f), 0.025f, arrowMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, 40f);

            sparks = new GameObject("Sparks").transform;
            sparks.SetParent(transform, false);
            sparkPieces = new Transform[8];
            var sparkMaterial = MaterialFactory.Create(Color.white, new Color(1.2f, 2.2f, 2.6f));
            for (int i = 0; i < sparkPieces.Length; i++)
                sparkPieces[i] = Shapes.Rounded("Spark", sparks, Vector3.zero, Vector3.one * 0.05f, 0.02f, sparkMaterial).transform;
            sparks.gameObject.SetActive(false);
        }

        private Transform MakeRing(string name, float radius, float pieceSize)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 0.06f, 0f);
            for (int i = 0; i < RingPieces; i++)
            {
                float a = i * Mathf.PI * 2f / RingPieces;
                var piece = Shapes.Rounded("Piece", root, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius),
                    new Vector3(pieceSize, pieceSize * 0.7f, pieceSize * 1.6f), pieceSize * 0.3f, ringMaterial);
                piece.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
            return root;
        }

        /// <summary>Burst open (with a little overshoot).</summary>
        public void Open()
        {
            if (IsOpen) return;
            openT = 0f;
            beam.gameObject.SetActive(true);
            sparks.gameObject.SetActive(true);
            arrow.gameObject.SetActive(false);
            MaterialFactory.SetColors(ringMaterial, new Color(0.7f, 1f, 1f), new Color(0.6f, 1.9f, 2.4f));
        }

        private void Update()
        {
            time += Time.deltaTime;

            if (!IsOpen)
            {
                // Closed: a sleepy gate that turns slowly and breathes, so the player sees where it will open.
                ring.Rotate(0f, 25f * Time.deltaTime, 0f);
                innerRing.Rotate(0f, -35f * Time.deltaTime, 0f);
                float breathe = 0.85f + Mathf.Sin(time * 3f) * 0.05f;
                ring.localScale = innerRing.localScale = new Vector3(breathe, 1f, breathe);
                arrow.localPosition = new Vector3(0f, 0.75f + Mathf.Abs(Mathf.Sin(time * 4f)) * 0.18f, 0f);
                var cam = Camera.main;
                if (cam != null) arrow.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
                return;
            }

            openT += Time.deltaTime;
            float pop = openT < 0.35f ? EaseOutBack(openT / 0.35f) : 1f;
            ring.Rotate(0f, 160f * Time.deltaTime, 0f);
            innerRing.Rotate(0f, -260f * Time.deltaTime, 0f);
            float pulse = 1f + Mathf.Sin(time * 8f) * 0.05f;
            ring.localScale = Vector3.one * (1.15f * pop * pulse);
            innerRing.localScale = Vector3.one * (1.1f * pop / pulse);
            ring.localPosition = new Vector3(0f, 0.06f + Mathf.Sin(time * 4f) * 0.03f, 0f);

            float beamAlpha = Mathf.Clamp01(openT / 0.4f) * (0.28f + Mathf.Sin(time * 6f) * 0.06f);
            MaterialFactory.SetColors(beamMaterial, new Color(0.6f, 0.95f, 1f, beamAlpha), new Color(0.5f, 1.4f, 1.8f) * beamAlpha * 2f);
            beam.localScale = new Vector3(0.5f * pop, 1.5f, 0.5f * pop);

            for (int i = 0; i < sparkPieces.Length; i++)
            {
                float t = Mathf.Repeat(time * 0.8f + i / (float)sparkPieces.Length, 1f);
                float a = i * 2.4f + time;
                sparkPieces[i].localPosition = new Vector3(Mathf.Cos(a) * 0.28f, t * 1.6f, Mathf.Sin(a) * 0.28f);
                sparkPieces[i].localScale = Vector3.one * 0.05f * (1f - t);
            }
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
