using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// The duel's defence seen: a glowing hexagon on the shield where a blocked blow meets it, the perfect guard's
    /// gold-white ring of sparks, the dodge's blur. Lines drawn with the additive particle material, fading by
    /// themselves (on the real clock: they show through a freeze).
    /// </summary>
    public class GuardFx : MonoBehaviour
    {
        private LineRenderer line;
        private float life, age;
        private Color colour;
        private float grow;

        /// <summary>A hexagon flashing on a shield at <paramref name="at"/>, facing <paramref name="facing"/>.</summary>
        public static void Hex(Vector3 at, Vector3 facing, Color c, float size = 0.55f)
        {
            var fx = Make("GuardHex", c, 0.3f, 0.06f, 7, 0.25f);
            fx.center = at;
            var up = Vector3.up;
            var f = facing; f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            var right = Vector3.Cross(up, f.normalized);
            for (int i = 0; i <= 6; i++)
            {
                float a = (i % 6) * Mathf.PI / 3f + Mathf.PI / 6f;
                fx.line.SetPosition(i, at + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * size);
            }
        }

        /// <summary>The perfect guard: a wide gold-white ring opening round <paramref name="at"/> (flat to the camera's view).</summary>
        public static void Ring(Vector3 at, Color c, float radius = 0.9f)
        {
            const int N = 33;
            var fx = Make("GuardRing", c, 0.45f, 0.1f, N, 0.6f);
            var cam = Camera.main != null ? Camera.main.transform : null;
            var right = cam != null ? cam.right : Vector3.right;
            var up = cam != null ? cam.up : Vector3.up;
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)(N - 1) * Mathf.PI * 2f;
                fx.line.SetPosition(i, at + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
            }
            fx.center = at;
        }

        /// <summary>A short blurred streak from <paramref name="from"/> to <paramref name="to"/> (the dodge's quick step).</summary>
        public static void Streak(Vector3 from, Vector3 to, Color c)
        {
            var fx = Make("DodgeStreak", c, 0.25f, 0.5f, 2, 0f);
            fx.line.SetPosition(0, from);
            fx.line.SetPosition(1, to);
            fx.line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(1f, 1f));
        }

        private Vector3 center;

        private static GuardFx Make(string name, Color c, float life, float width, int points, float grow)
        {
            var go = new GameObject(name);
            var fx = go.AddComponent<GuardFx>();
            fx.line = go.AddComponent<LineRenderer>();
            fx.line.sharedMaterial = Weather.AddMaterial;
            fx.line.positionCount = points;
            fx.line.widthMultiplier = width;
            fx.line.numCapVertices = 2;
            fx.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fx.line.receiveShadows = false;
            fx.life = life;
            fx.colour = c;
            fx.grow = grow;
            fx.line.startColor = fx.line.endColor = c;
            return fx;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(age / life);
            var c = colour; c.a *= 1f - k * k;
            line.startColor = line.endColor = c;
            if (grow > 0f && line.positionCount > 2)
            {
                // the ring opens out as it fades
                float s = 1f + grow * Time.unscaledDeltaTime / life;
                for (int i = 0; i < line.positionCount; i++) line.SetPosition(i, center + (line.GetPosition(i) - center) * s);
            }
            if (age >= life) Destroy(gameObject);
        }
    }
}
