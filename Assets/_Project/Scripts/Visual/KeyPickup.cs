using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A key of Exit missions: a glowing cyan key (the colour of the door it opens) that spins and bobs above a
    /// light disc on its tile. Pops in when it appears, shrinks away when it moves to another tile.
    /// </summary>
    public class KeyPickup : MonoBehaviour
    {
        private Transform key, disc;
        private float time, popT;

        public static KeyPickup Create(Vector3 position)
        {
            var go = new GameObject("Key");
            go.transform.position = position;
            var pickup = go.AddComponent<KeyPickup>();
            pickup.Build();
            return pickup;
        }

        private void Build()
        {
            var body = MaterialFactory.Create(new Color(0.55f, 0.95f, 1f), new Color(0.4f, 1.3f, 1.7f));
            var glow = MaterialFactory.CreateTransparent(new Color(0.55f, 0.95f, 1f, 0.35f), new Color(0.3f, 0.9f, 1.2f));

            disc = Shapes.Primitive(PrimitiveType.Cylinder, "Disc", transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.6f, 0.004f, 0.6f), glow).transform;

            key = new GameObject("Body").transform;
            key.SetParent(transform, false);

            // The bow: a ring of small pieces.
            const int pieces = 10;
            for (int i = 0; i < pieces; i++)
            {
                float a = i * Mathf.PI * 2f / pieces;
                var piece = Shapes.Rounded("Bow", key, new Vector3(Mathf.Cos(a) * 0.11f, 0.13f + Mathf.Sin(a) * 0.11f, 0f),
                    new Vector3(0.075f, 0.075f, 0.06f), 0.025f, body);
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }
            Shapes.Rounded("Shaft", key, new Vector3(0f, -0.1f, 0f), new Vector3(0.06f, 0.3f, 0.06f), 0.025f, body);
            Shapes.Rounded("Tooth1", key, new Vector3(0.06f, -0.2f, 0f), new Vector3(0.09f, 0.05f, 0.05f), 0.02f, body);
            Shapes.Rounded("Tooth2", key, new Vector3(0.05f, -0.12f, 0f), new Vector3(0.07f, 0.045f, 0.05f), 0.02f, body);
            Place();
        }

        private void Place()
        {
            popT = 0f;
            key.localScale = Vector3.zero;
        }

        /// <summary>Jump to another tile (the old one got hit or broke), popping in again.</summary>
        public void MoveTo(Vector3 position)
        {
            transform.position = position;
            Place();
        }

        private void Update()
        {
            time += Time.deltaTime;
            popT += Time.deltaTime;
            float pop = popT < 0.35f ? EaseOutBack(popT / 0.35f) : 1f;
            key.localScale = Vector3.one * (1.7f * pop);
            key.localPosition = new Vector3(0f, 0.6f + Mathf.Sin(time * 3.2f) * 0.08f, 0f);
            key.localRotation = Quaternion.Euler(0f, time * 140f, 0f);
            float pulse = 1f + Mathf.Sin(time * 5f) * 0.12f;
            disc.localScale = new Vector3(0.6f * pulse * pop, 0.004f, 0.6f * pulse * pop);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
