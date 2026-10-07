using System;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A Shadow Clone from vanG's Hall of Mirrors: a dark, see-through copy of the robot with glowing eyes and a
    /// shadow pool under it. It hops when the robot hops (mirrored), and a block knocks it flat for a while.
    /// </summary>
    public class ShadowClone : MonoBehaviour
    {
        private static Material shade, pool, eye;

        private Transform body;
        private Vector3 from, to;
        private float hopT = 1f, hopTime = 0.16f, time;
        private bool down;

        public bool Hopping => hopT < 1f;

        /// <summary>A clone at <paramref name="position"/>, built by <paramref name="robotLook"/> (the robot's own model).</summary>
        public static ShadowClone Create(Vector3 position, Func<Transform, GameObject> robotLook)
        {
            if (shade == null)
            {
                shade = MaterialFactory.CreateTransparent(new Color(0.16f, 0.12f, 0.26f, 0.82f), new Color(0.25f, 0.1f, 0.45f));
                pool = MaterialFactory.CreateTransparent(new Color(0.1f, 0.05f, 0.2f, 0.45f), Color.black);
                eye = MaterialFactory.Create(new Color(0.75f, 0.55f, 1f), new Color(1.6f, 0.6f, 2.4f));
            }
            var go = new GameObject("ShadowClone");
            go.transform.position = position;
            var c = go.AddComponent<ShadowClone>();
            c.from = c.to = position;
            c.body = new GameObject("Body").transform;
            c.body.SetParent(go.transform, false);
            var look = robotLook?.Invoke(c.body);
            if (look != null)
                foreach (var r in look.GetComponentsInChildren<Renderer>())
                {
                    // The eyes keep a cold violet glow; everything else turns to shadow.
                    bool glows = r.name.StartsWith("Eye");
                    r.sharedMaterial = glows ? eye : shade;
                }
            Shapes.Primitive(PrimitiveType.Cylinder, "Pool", go.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.8f, 0.01f, 0.8f), pool);
            return c;
        }

        /// <summary>Hops to <paramref name="position"/>, facing the way it goes.</summary>
        public void HopTo(Vector3 position)
        {
            from = transform.position;
            to = position;
            hopT = 0f;
            var dir = to - from;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) body.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>Knocked flat by a block (true) or back up again (false).</summary>
        public void SetDown(bool value)
        {
            down = value;
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
            float targetY = down ? 0.15f : 1f + Mathf.Sin(time * 3f) * 0.03f;
            var s = body.localScale;
            body.localScale = new Vector3(Mathf.Lerp(s.x, down ? 1.25f : 1f, dt * 10f), Mathf.Lerp(s.y, targetY, dt * 10f), Mathf.Lerp(s.z, down ? 1.25f : 1f, dt * 10f));
        }
    }
}
