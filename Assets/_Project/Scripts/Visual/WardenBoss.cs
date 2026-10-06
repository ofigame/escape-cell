using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// WARDEN in the flesh (well, in the screen): a big grumpy monitor hovering over the arena's far corner.
    /// Its red eye follows the robot; every button the robot hits makes it flinch, flash and lose a health light;
    /// the last one sends it tumbling down in a shower of sparks.
    /// </summary>
    public class WardenBoss : MonoBehaviour
    {
        private Transform body, eye, pupil, mouth;
        private Material screenMaterial, eyeMaterial;
        private Material[] pipMaterials;
        private Vector3 home;
        private float time, hitT = 1f, defeatT = -1f;
        private int health;
        private Transform target;

        private static readonly Color EyeColor = new Color(1f, 0.25f, 0.3f);
        private static readonly Color EyeGlow = new Color(2.6f, 0.3f, 0.35f);

        public static WardenBoss Create(Vector3 position, int health, Transform lookAt)
        {
            var go = new GameObject("WARDEN");
            go.transform.position = position;
            var boss = go.AddComponent<WardenBoss>();
            boss.home = position;
            boss.health = health;
            boss.target = lookAt;
            boss.Build();
            return boss;
        }

        private void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            var shell = MaterialFactory.Create(new Color(0.2f, 0.17f, 0.32f), Color.black);
            var rim = MaterialFactory.Create(new Color(1f, 0.4f, 0.48f), new Color(1.4f, 0.25f, 0.3f));
            screenMaterial = MaterialFactory.Create(new Color(0.32f, 0.06f, 0.12f), new Color(0.25f, 0.02f, 0.05f));
            eyeMaterial = MaterialFactory.Create(EyeColor, EyeGlow);
            var white = MaterialFactory.Create(Color.white, new Color(1.5f, 1.3f, 1.3f));

            Shapes.Rounded("Shell", body, Vector3.zero, new Vector3(1.7f, 1.25f, 0.36f), 0.16f, shell);
            Shapes.Rounded("Rim", body, new Vector3(0f, 0f, -0.02f), new Vector3(1.56f, 1.1f, 0.36f), 0.12f, rim);
            Shapes.Rounded("Screen", body, new Vector3(0f, 0f, -0.05f), new Vector3(1.42f, 0.96f, 0.34f), 0.1f, screenMaterial);
            Shapes.Rounded("Brow", body, new Vector3(0f, 0.32f, -0.24f), new Vector3(0.7f, 0.09f, 0.04f), 0.03f, rim)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            eye = Shapes.Rounded("Eye", body, new Vector3(0f, 0.05f, -0.24f), new Vector3(0.46f, 0.46f, 0.06f), 0.2f, eyeMaterial).transform;
            pupil = Shapes.Rounded("Pupil", eye, new Vector3(0f, 0f, -0.04f), new Vector3(0.15f, 0.15f, 0.03f), 0.07f, white).transform;
            mouth = Shapes.Rounded("Mouth", body, new Vector3(0f, -0.3f, -0.24f), new Vector3(0.52f, 0.06f, 0.04f), 0.025f, rim).transform;
            Shapes.Rounded("Antenna", body, new Vector3(0.55f, 0.8f, 0f), new Vector3(0.05f, 0.4f, 0.05f), 0.02f, shell);
            Shapes.Rounded("Tip", body, new Vector3(0.55f, 1.02f, 0f), new Vector3(0.12f, 0.12f, 0.12f), 0.06f, eyeMaterial);

            // Health lights under the screen.
            pipMaterials = new Material[health];
            for (int i = 0; i < health; i++)
            {
                pipMaterials[i] = MaterialFactory.Create(new Color(0.5f, 1f, 0.6f), new Color(0.4f, 1.8f, 0.6f));
                float x = (i - (health - 1) * 0.5f) * 0.22f;
                Shapes.Rounded("Pip", body, new Vector3(x, -0.72f, -0.1f), new Vector3(0.14f, 0.08f, 0.08f), 0.03f, pipMaterials[i]);
            }
        }

        /// <summary>A button was hit: flinch, flash, one health light goes dark.</summary>
        public void Hit(int remaining)
        {
            hitT = 0f;
            for (int i = 0; i < pipMaterials.Length; i++)
                if (i >= remaining) MaterialFactory.SetColors(pipMaterials[i], new Color(0.25f, 0.22f, 0.3f), Color.black);
        }

        /// <summary>The final button: WARDEN short-circuits and drops out of the sky.</summary>
        public void Defeat()
        {
            Hit(0);
            defeatT = 0f;
        }

        private void Update()
        {
            time += Time.deltaTime;
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);

            // Hover and bob; flinch backwards when hit.
            hitT = Mathf.Min(1f, hitT + Time.deltaTime * 2.2f);
            float flinch = Mathf.Sin(hitT * Mathf.PI) * (1f - hitT);
            var shake = hitT < 1f ? Random.insideUnitSphere * 0.08f * (1f - hitT) : Vector3.zero;
            var pos = home + Vector3.up * (Mathf.Sin(time * 1.6f) * 0.12f) + shake;

            if (defeatT >= 0f)
            {
                defeatT += Time.deltaTime;
                pos += Vector3.down * (defeatT * defeatT * 5f);
                body.localRotation = Quaternion.Euler(defeatT * 90f, 0f, defeatT * 160f);
                if (defeatT > 2.5f) gameObject.SetActive(false);
            }
            else
            {
                body.localRotation = Quaternion.Euler(-flinch * 25f, Mathf.Sin(time * 0.9f) * 6f, Mathf.Sin(time * 1.3f) * 3f);
            }
            transform.position = pos;

            // The eye glares at the robot; it flashes white on hits and blinks now and then.
            if (target != null)
            {
                var local = body.InverseTransformPoint(target.position);
                pupil.localPosition = new Vector3(Mathf.Clamp(local.x * 0.04f, -0.1f, 0.1f), Mathf.Clamp(local.y * 0.04f, -0.1f, 0.1f), -0.04f);
            }
            bool blink = Mathf.Repeat(time, 3.7f) < 0.12f;
            eye.localScale = new Vector3(1f, blink ? 0.12f : 1f, 1f);
            var flash = Color.Lerp(Color.white, EyeColor, hitT);
            MaterialFactory.SetColors(eyeMaterial, flash, Color.Lerp(new Color(3f, 3f, 3f), EyeGlow, hitT));
            MaterialFactory.SetColors(screenMaterial, Color.Lerp(new Color(1f, 0.6f, 0.6f), new Color(0.32f, 0.06f, 0.12f), hitT), new Color(0.25f, 0.02f, 0.05f));
            mouth.localScale = new Vector3(1f, 1f + Mathf.Abs(Mathf.Sin(time * 3f)) * 0.6f, 1f);
        }
    }

    /// <summary>A floor button that lights up for the boss fight: a pulsing red pad that sinks when stepped on.</summary>
    public class BossButton : MonoBehaviour
    {
        private Transform top, ring;
        private float time, popT;

        public static BossButton Create(Vector3 position)
        {
            var go = new GameObject("BossButton");
            go.transform.position = position;
            var button = go.AddComponent<BossButton>();
            button.Build();
            return button;
        }

        private void Build()
        {
            var baseMat = MaterialFactory.Create(new Color(0.22f, 0.2f, 0.32f), Color.black);
            var topMat = MaterialFactory.Create(new Color(1f, 0.35f, 0.4f), new Color(2.2f, 0.35f, 0.4f));
            var glow = MaterialFactory.CreateTransparent(new Color(1f, 0.4f, 0.45f, 0.35f), new Color(1.6f, 0.3f, 0.35f));
            Shapes.Rounded("Base", transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.66f, 0.1f, 0.66f), 0.05f, baseMat);
            top = Shapes.Rounded("Top", transform, new Vector3(0f, 0.13f, 0f), new Vector3(0.46f, 0.1f, 0.46f), 0.05f, topMat).transform;
            ring = Shapes.Primitive(PrimitiveType.Cylinder, "Glow", transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.004f, 0.9f), glow).transform;
            popT = 0f;
        }

        public void MoveTo(Vector3 position)
        {
            transform.position = position;
            popT = 0f;
        }

        private void Update()
        {
            time += Time.deltaTime;
            popT = Mathf.Min(1f, popT + Time.deltaTime * 3f);
            float pop = popT < 1f ? 1f + Mathf.Sin(popT * Mathf.PI) * 0.4f : 1f;
            transform.localScale = Vector3.one * pop;
            float pulse = 1f + Mathf.Sin(time * 6f) * 0.15f;
            ring.localScale = new Vector3(0.9f * pulse, 0.004f, 0.9f * pulse);
            top.localPosition = new Vector3(0f, 0.13f + Mathf.Abs(Mathf.Sin(time * 3f)) * 0.03f, 0f);
        }
    }
}
