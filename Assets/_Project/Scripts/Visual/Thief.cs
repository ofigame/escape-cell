using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The coin thief: a round little raccoon-like bandit with a black mask, a striped tail and a bulging gold sack on
    /// its back. It hops from tile to tile, ducks when caught (dizzy stars spin over its head) and leaves a coin behind.
    /// </summary>
    public class Thief : MonoBehaviour
    {
        private Transform body, sack, stars;
        private Vector3 from, to;
        private float hopT = 1f, hopTime = 0.25f, stunLeft, flash, time;
        private Material fur;
        private Color furColor;

        public bool Stunned => stunLeft > 0f;
        public bool Hopping => hopT < 1f;

        public static Thief Create(Vector3 position, Color tint)
        {
            var go = new GameObject("Thief");
            go.transform.position = position;
            var t = go.AddComponent<Thief>();
            t.furColor = tint;
            t.from = t.to = position;
            t.Build();
            return t;
        }

        private void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            fur = MaterialFactory.Create(furColor, furColor * 0.15f);
            var light = MaterialFactory.Create(Color.Lerp(furColor, Color.white, 0.6f), Color.black);
            var mask = MaterialFactory.Create(new Color(0.1f, 0.08f, 0.14f), Color.black);
            var white = MaterialFactory.Create(Color.white, new Color(0.4f, 0.4f, 0.4f));
            var sackMat = MaterialFactory.Create(new Color(0.62f, 0.45f, 0.28f), Color.black);
            var gold = MaterialFactory.Create(Palette.UiGold, Palette.UiGold * 1.2f);

            Shapes.Rounded("Torso", body, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.42f, 0.46f), 0.18f, fur);
            Shapes.Rounded("Belly", body, new Vector3(0f, 0.27f, 0.2f), new Vector3(0.32f, 0.28f, 0.1f), 0.08f, light);
            Shapes.Rounded("Head", body, new Vector3(0f, 0.68f, 0.04f), new Vector3(0.46f, 0.36f, 0.4f), 0.15f, fur);
            Shapes.Rounded("Mask", body, new Vector3(0f, 0.71f, 0.23f), new Vector3(0.44f, 0.11f, 0.05f), 0.04f, mask);
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(x, 0.715f, 0.26f), Vector3.one * 0.075f, white);
                Shapes.Rounded("Ear", body, new Vector3(x * 1.7f, 0.9f, 0f), new Vector3(0.11f, 0.14f, 0.08f), 0.04f, fur);
                Shapes.Rounded("Foot", body, new Vector3(x * 1.3f, 0.05f, 0.05f), new Vector3(0.14f, 0.1f, 0.18f), 0.04f, mask);
            }
            Shapes.Primitive(PrimitiveType.Sphere, "Nose", body, new Vector3(0f, 0.63f, 0.25f), Vector3.one * 0.06f, mask);
            // A striped tail.
            for (int i = 0; i < 4; i++)
                Shapes.Rounded("Tail", body, new Vector3(0.12f, 0.18f + i * 0.08f, -0.3f - i * 0.04f), new Vector3(0.13f, 0.1f, 0.13f), 0.05f, i % 2 == 0 ? fur : mask);
            // The sack on its back, a coin peeking out.
            sack = new GameObject("Sack").transform;
            sack.SetParent(body, false);
            sack.localPosition = new Vector3(-0.08f, 0.5f, -0.28f);
            Shapes.Primitive(PrimitiveType.Sphere, "Bag", sack, Vector3.zero, new Vector3(0.42f, 0.4f, 0.36f), sackMat);
            Shapes.Rounded("Tie", sack, new Vector3(0f, 0.2f, 0f), new Vector3(0.14f, 0.06f, 0.14f), 0.03f, mask);
            Shapes.Primitive(PrimitiveType.Cylinder, "Coin", sack, new Vector3(0.05f, 0.26f, 0f), new Vector3(0.16f, 0.02f, 0.16f), gold).transform.localRotation = Quaternion.Euler(70f, 0f, 20f);

            stars = new GameObject("Stars").transform;
            stars.SetParent(transform, false);
            stars.localPosition = new Vector3(0f, 1.05f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Shapes.Rounded("Star", stars, new Vector3(Mathf.Cos(a) * 0.25f, 0f, Mathf.Sin(a) * 0.25f), new Vector3(0.09f, 0.09f, 0.09f), 0.02f, gold);
            }
            stars.gameObject.SetActive(false);
            transform.localScale = Vector3.one * 1.15f;
        }

        /// <summary>Hops to a new tile over <paramref name="seconds"/>, facing where it goes.</summary>
        public void HopTo(Vector3 position, float seconds)
        {
            from = transform.position;
            to = position;
            hopTime = Mathf.Max(0.05f, seconds);
            hopT = 0f;
            var dir = to - from;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>Caught: a flash, dizzy stars for a moment, and it can't run.</summary>
        public void Catch(float stunSeconds)
        {
            stunLeft = stunSeconds;
            flash = 1f;
        }

        /// <summary>The last catch: it drops the sack and sits down for good.</summary>
        public void Give()
        {
            stunLeft = 999f;
            flash = 1f;
            if (sack != null) sack.gameObject.SetActive(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            if (hopT < 1f)
            {
                hopT = Mathf.Min(1f, hopT + dt / hopTime);
                transform.position = Vector3.Lerp(from, to, hopT) + Vector3.up * Mathf.Sin(hopT * Mathf.PI) * 0.35f;
            }
            stunLeft -= dt;
            stars.gameObject.SetActive(stunLeft > 0f);
            if (stunLeft > 0f) stars.localRotation = Quaternion.Euler(0f, time * 300f, 0f);
            // Squash on landing, wobble while dizzy, a sneaky bob otherwise.
            float squash = hopT < 1f ? 1f + Mathf.Sin(hopT * Mathf.PI) * 0.12f : 1f;
            body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
            body.localRotation = stunLeft > 0f ? Quaternion.Euler(0f, 0f, Mathf.Sin(time * 14f) * 12f) : Quaternion.Euler(0f, 0f, Mathf.Sin(time * 6f) * 3f);
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - dt * 3f);
                MaterialFactory.SetColors(fur, Color.Lerp(furColor, Color.white, flash), Color.Lerp(furColor * 0.15f, Color.white * 2f, flash));
            }
        }
    }
}
