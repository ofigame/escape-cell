using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// Bip, the Observer's partner: an old archive robot with a cracked screen face, two green eyes, a rusty wheel, a
    /// bouncing antenna and a soft glow under it. It rides in the robot's backpack on the roads (see <see cref="Ride"/>). It hops after the player; hit by a block it sits dazed inside a bubble for a moment
    /// (it is never destroyed), and it cheers when it reaches the door.
    /// </summary>
    public class Buddy : MonoBehaviour
    {
        private Transform body, antenna, bubble, stars;
        private Vector3 from, to;
        private float hopT = 1f, hopTime = 0.2f, dazeLeft, time, cheer = -1f;
        private Material shell;
        private static readonly Color ShellColor = new Color(0.93f, 0.86f, 0.7f);

        public bool Dazed => dazeLeft > 0f;
        public bool Hopping => hopT < 1f;

        public static Buddy Create(Vector3 position)
        {
            var go = new GameObject("Buddy");
            go.transform.position = position;
            var b = go.AddComponent<Buddy>();
            b.from = b.to = position;
            b.Build();
            return b;
        }

        private void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            shell = MaterialFactory.Create(ShellColor, ShellColor * 0.2f);
            var dark = MaterialFactory.Create(new Color(0.15f, 0.13f, 0.25f), Color.black);
            var screen = MaterialFactory.Create(new Color(0.12f, 0.24f, 0.2f), new Color(0.02f, 0.08f, 0.05f));
            var eye = MaterialFactory.Create(new Color(0.55f, 1f, 0.6f), new Color(0.7f, 2.2f, 0.8f));
            var crack = MaterialFactory.Create(new Color(0.75f, 0.9f, 0.85f), new Color(0.3f, 0.4f, 0.35f));
            var rust = MaterialFactory.Create(new Color(0.62f, 0.4f, 0.28f), Color.black);
            var glow = MaterialFactory.CreateTransparent(new Color(1f, 0.8f, 0.4f, 0.35f), new Color(1.6f, 1.1f, 0.4f));

            Shapes.Primitive(PrimitiveType.Cylinder, "Glow", transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.6f, 0.004f, 0.6f), glow);
            // An old archive unit: a boxy shell with a cracked screen for a face, two green eyes, and one rusty wheel.
            Shapes.Rounded("Shell", body, new Vector3(0f, 0.32f, 0f), new Vector3(0.44f, 0.36f, 0.36f), 0.1f, shell);
            Shapes.Rounded("Screen", body, new Vector3(0f, 0.33f, 0.17f), new Vector3(0.32f, 0.22f, 0.04f), 0.05f, screen);
            foreach (float x in new[] { -0.065f, 0.065f })
                Shapes.Primitive(PrimitiveType.Sphere, "Eye", body, new Vector3(x, 0.34f, 0.195f), Vector3.one * 0.075f, eye);
            Shapes.Rounded("Crack", body, new Vector3(0.09f, 0.37f, 0.192f), new Vector3(0.012f, 0.12f, 0.01f), 0.004f, crack).transform.localRotation = Quaternion.Euler(0f, 0f, -50f);
            var wheel = Shapes.Primitive(PrimitiveType.Cylinder, "Wheel", body, new Vector3(0f, 0.09f, 0f), new Vector3(0.18f, 0.06f, 0.18f), rust).transform;
            wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Shapes.Rounded("Axle", body, new Vector3(0f, 0.15f, 0f), new Vector3(0.2f, 0.06f, 0.16f), 0.03f, dark);
            antenna = new GameObject("Antenna").transform;
            antenna.SetParent(body, false);
            antenna.localPosition = new Vector3(-0.1f, 0.5f, 0f);
            Shapes.Rounded("Stalk", antenna, new Vector3(0f, 0.08f, 0f), new Vector3(0.03f, 0.16f, 0.03f), 0.012f, dark);
            Shapes.Primitive(PrimitiveType.Sphere, "Tip", antenna, new Vector3(0f, 0.18f, 0f), Vector3.one * 0.08f, eye);

            bubble = Shapes.Primitive(PrimitiveType.Sphere, "Bubble", transform, new Vector3(0f, 0.32f, 0f), Vector3.one * 0.85f,
                MaterialFactory.CreateTransparent(new Color(0.6f, 0.85f, 1f, 0.25f), new Color(0.6f, 1.2f, 1.8f))).transform;
            bubble.gameObject.SetActive(false);
            stars = new GameObject("Stars").transform;
            stars.SetParent(transform, false);
            stars.localPosition = new Vector3(0f, 0.8f, 0f);
            var gold = MaterialFactory.Create(Palette.UiGold, Palette.UiGold);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Shapes.Rounded("Star", stars, new Vector3(Mathf.Cos(a) * 0.2f, 0f, Mathf.Sin(a) * 0.2f), new Vector3(0.07f, 0.07f, 0.07f), 0.02f, gold);
            }
            stars.gameObject.SetActive(false);
        }

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

        /// <summary>Puts the buddy straight down somewhere (back to safety after a daze).</summary>
        public void Place(Vector3 position)
        {
            from = to = position;
            hopT = 1f;
            transform.position = position;
        }

        public void Daze(float seconds)
        {
            dazeLeft = seconds;
        }

        public void Cheer() => cheer = 0f;

        /// <summary>Rides on <paramref name="back"/> (the robot's back): small, no glow under it, bobbing along.</summary>
        public void Ride(Transform back)
        {
            transform.SetParent(back, false);
            transform.localPosition = new Vector3(0f, 0.36f, -0.3f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * 0.5f;
            var glow = transform.Find("Glow");
            if (glow != null) glow.gameObject.SetActive(false);
            from = to = transform.position;
            hopT = 1f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            if (hopT < 1f)
            {
                hopT = Mathf.Min(1f, hopT + dt / hopTime);
                transform.position = Vector3.Lerp(from, to, hopT) + Vector3.up * Mathf.Sin(hopT * Mathf.PI) * 0.25f;
            }
            dazeLeft -= dt;
            bool dazed = dazeLeft > 0f;
            bubble.gameObject.SetActive(dazed);
            stars.gameObject.SetActive(dazed);
            if (dazed)
            {
                stars.localRotation = Quaternion.Euler(0f, time * 280f, 0f);
                bubble.localScale = Vector3.one * (0.85f + Mathf.Sin(time * 8f) * 0.04f);
            }
            if (cheer >= 0f)
            {
                cheer += dt;
                body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(cheer * 9f)) * 0.25f, 0f);
                body.localRotation = Quaternion.Euler(0f, cheer * 500f, 0f);
            }
            else body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(time * 4f)) * 0.03f, 0f);
            antenna.localRotation = Quaternion.Euler(Mathf.Sin(time * 7f) * 12f, 0f, Mathf.Cos(time * 5f) * 10f);
            MaterialFactory.SetColors(shell, dazed ? Color.Lerp(ShellColor, Color.white, 0.4f + 0.3f * Mathf.Sin(time * 12f)) : ShellColor, ShellColor * 0.2f);
        }
    }
}
