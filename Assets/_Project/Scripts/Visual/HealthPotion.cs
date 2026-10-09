using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A healing potion on a safe island: a round glass flask of glowing red liquid with a cork, bobbing and turning
    /// over a soft ring of light. Drunk, it is gone for a while and then fills up again.
    /// </summary>
    public class HealthPotion : MonoBehaviour
    {
        private Transform flask;
        private float time, hiddenFor;

        public bool Ready => hiddenFor <= 0f;

        public static HealthPotion Create(Vector3 tile)
        {
            var go = new GameObject("HealthPotion");
            go.transform.position = tile;
            var p = go.AddComponent<HealthPotion>();
            p.Build();
            return p;
        }

        private void Build()
        {
            var ring = MaterialFactory.CreateTransparent(new Color(1f, 0.35f, 0.45f, 0.35f), new Color(1.6f, 0.4f, 0.5f));
            Shapes.Primitive(PrimitiveType.Cylinder, "Glow", transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.7f, 0.004f, 0.7f), ring);
            flask = new GameObject("Flask").transform;
            flask.SetParent(transform, false);
            var glass = MaterialFactory.CreateTransparent(new Color(0.85f, 0.95f, 1f, 0.35f), new Color(0.2f, 0.25f, 0.3f));
            var liquid = MaterialFactory.Create(new Color(1f, 0.2f, 0.3f), new Color(2.2f, 0.3f, 0.45f));
            var cork = MaterialFactory.Create(new Color(0.7f, 0.5f, 0.3f), Color.black);
            var heart = MaterialFactory.Create(Color.white, new Color(1.6f, 1.6f, 1.6f));
            Shapes.Primitive(PrimitiveType.Sphere, "Liquid", flask, new Vector3(0f, 0.28f, 0f), Vector3.one * 0.3f, liquid);
            Shapes.Primitive(PrimitiveType.Sphere, "Glass", flask, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.38f, glass);
            Shapes.Rounded("Neck", flask, new Vector3(0f, 0.53f, 0f), new Vector3(0.12f, 0.16f, 0.12f), 0.04f, glass);
            Shapes.Rounded("Cork", flask, new Vector3(0f, 0.63f, 0f), new Vector3(0.1f, 0.08f, 0.1f), 0.03f, cork);
            // A little white cross on the flask: this one heals.
            Shapes.Rounded("CrossA", flask, new Vector3(0f, 0.3f, 0.19f), new Vector3(0.12f, 0.035f, 0.02f), 0.01f, heart);
            Shapes.Rounded("CrossB", flask, new Vector3(0f, 0.3f, 0.19f), new Vector3(0.035f, 0.12f, 0.02f), 0.01f, heart);
        }

        /// <summary>Drunk: hides for <paramref name="seconds"/>, then pops back.</summary>
        public void Drink(float seconds)
        {
            hiddenFor = seconds;
            flask.gameObject.SetActive(false);
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (hiddenFor > 0f)
            {
                hiddenFor -= Time.deltaTime;
                if (hiddenFor <= 0f) flask.gameObject.SetActive(true);
                return;
            }
            flask.localPosition = new Vector3(0f, 0.08f + Mathf.Sin(time * 2.5f) * 0.05f, 0f);
            flask.localRotation = Quaternion.Euler(0f, time * 70f, 0f);
        }
    }
}
