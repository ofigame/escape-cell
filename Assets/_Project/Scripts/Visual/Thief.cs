using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The Masked Thief: an old, scratched Observer robot (the very first one vanG built) with a dark mask across its
    /// visor, a bent antenna and a bulging gold sack on its back. It hops from tile to tile, ducks when caught (dizzy
    /// stars spin over its head) and leaves a coin behind.
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
            var mask = MaterialFactory.Create(new Color(0.1f, 0.08f, 0.14f), Color.black);
            var sackMat = MaterialFactory.Create(new Color(0.62f, 0.45f, 0.28f), Color.black);
            var gold = MaterialFactory.Create(Palette.UiGold, Palette.UiGold * 1.2f);

            // An old Observer: cube head with a visor, a boxy body, worn grey metal full of scratches.
            var scratch = MaterialFactory.Create(Color.Lerp(furColor, Color.black, 0.45f), Color.black);
            var eye = MaterialFactory.Create(Palette.UiGold, Palette.UiGold * 1.6f);
            Shapes.Rounded("Torso", body, new Vector3(0f, 0.24f, 0f), new Vector3(0.4f, 0.32f, 0.3f), 0.08f, fur);
            Shapes.Rounded("Head", body, new Vector3(0f, 0.6f, 0f), new Vector3(0.46f, 0.42f, 0.44f), 0.1f, fur);
            Shapes.Rounded("Visor", body, new Vector3(0f, 0.6f, 0.222f), new Vector3(0.34f, 0.22f, 0.03f), 0.04f, mask);
            Shapes.Rounded("Mask", body, new Vector3(0f, 0.61f, 0.245f), new Vector3(0.48f, 0.12f, 0.03f), 0.03f, mask);
            foreach (float x in new[] { -0.075f, 0.075f })
            {
                Shapes.Rounded("Eye", body, new Vector3(x, 0.61f, 0.262f), new Vector3(0.07f, 0.045f, 0.01f), 0.01f, eye);
                Shapes.Rounded("Foot", body, new Vector3(x * 1.4f, 0.04f, 0.03f), new Vector3(0.12f, 0.08f, 0.16f), 0.03f, mask);
            }
            foreach (var (pos, angle) in new[] { (new Vector3(-0.2f, 0.72f, 0.2f), 35f), (new Vector3(0.18f, 0.48f, 0.2f), -30f), (new Vector3(0.12f, 0.3f, 0.152f), 60f) })
                Shapes.Rounded("Scratch", body, pos, new Vector3(0.012f, 0.1f, 0.01f), 0.004f, scratch).transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            var stalk = Shapes.Rounded("Antenna", body, new Vector3(0.06f, 0.9f, 0f), new Vector3(0.025f, 0.16f, 0.025f), 0.01f, mask).transform;
            stalk.localRotation = Quaternion.Euler(0f, 0f, -25f);
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", body, new Vector3(0.1f, 0.98f, 0f), Vector3.one * 0.06f, eye);
            // The sack on its back, a coin peeking out.
            sack = new GameObject("Sack").transform;
            sack.SetParent(body, false);
            sack.localPosition = new Vector3(-0.06f, 0.42f, -0.26f);
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
