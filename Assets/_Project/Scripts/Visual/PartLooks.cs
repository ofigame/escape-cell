using System.Collections.Generic;
using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// foi's parts seen on its body (the rival sees them too), a step at levels 5, 10, 15 and 20 of each:
    /// the body a chest plate → shoulder plates → a glowing core → a gold-trimmed body; the arms wrist rings → thick
    /// forearms → energy veins → glowing fists; the shell rivets → … → a crystal coat → a mirror finish on every plate.
    /// Pieces ride the bones (they move with the pose); dressed again, the old ones go first.
    /// </summary>
    public static class PartLooks
    {
        private const string Holder = "PartLook";

        private static Transform Bone(Transform root, string bone)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == bone || t.name.EndsWith(":" + bone)) return t;
            return null;
        }

        /// <summary>Puts the parts' look on the skin (<paramref name="skin"/>: the hero's body root) for these levels.</summary>
        public static void Apply(Transform skin, int body, int arms, int shell)
        {
            if (skin == null) return;
            var old = new List<GameObject>();
            foreach (var t in skin.GetComponentsInChildren<Transform>(true)) if (t.name == Holder) old.Add(t.gameObject);
            foreach (var g in old) Object.DestroyImmediate(g); // (at once: a lookalike may be copied right after)
            int b = RobotParts.LookStep(body), a = RobotParts.LookStep(arms), s = RobotParts.LookStep(shell);
            if (b == 0 && a == 0 && s == 0) return;

            var chest = Bone(skin, "Spine2");
            var armL = Bone(skin, "LeftArm");
            var armR = Bone(skin, "RightArm");
            if (chest == null || armL == null || armR == null) return;
            float u = Vector3.Distance(armL.position, armR.position); // the shoulders' width: every size is a share of it
            if (u < 1e-4f) return;
            var fwd = skin.forward;
            var up = skin.up;

            // The plates' metal: steel, brighter with the shell, gold at the body's last step, a mirror at the shell's.
            var metal = s >= 4 ? new Color(0.92f, 0.95f, 1f) : b >= 4 ? new Color(1f, 0.8f, 0.35f) : new Color(0.72f, 0.76f, 0.84f);
            var plate = MaterialFactory.CreateSurface(metal, b >= 4 ? new Color(0.25f, 0.16f, 0.02f) : Color.black, s >= 4 ? 0.95f : 0.5f + 0.1f * s, s >= 4 ? 1f : 0.6f);
            var glow = MaterialFactory.Create(new Color(0.4f, 0.95f, 1f), new Color(0.8f, 2.4f, 3f));
            var gold = MaterialFactory.CreateSurface(new Color(1f, 0.78f, 0.3f), new Color(0.3f, 0.2f, 0.03f), 0.8f, 1f);

            // ---- the body ----
            var chestAt = chest.position + fwd * (0.4f * u) - up * (0.02f * u);
            if (b >= 1)
            {
                // a rounded breastplate over the suit's chest
                Piece(PrimitiveType.Sphere, chest, chestAt, Quaternion.LookRotation(fwd, up), new Vector3(0.86f * u, 0.64f * u, 0.2f * u), plate);
                if (b >= 4) // a gold rim behind the core
                    Piece(PrimitiveType.Cylinder, chest, chestAt + fwd * (0.1f * u), Quaternion.LookRotation(up, -fwd), new Vector3(0.42f * u, 0.015f * u, 0.42f * u), gold);
            }
            if (b >= 2)
                foreach (var arm in new[] { armL, armR })
                    Piece(PrimitiveType.Sphere, arm, arm.position + up * (0.13f * u) + fwd * (0.03f * u), Quaternion.LookRotation(fwd, up), new Vector3(0.66f * u, 0.32f * u, 0.66f * u), plate);
            if (b >= 3)
                Piece(PrimitiveType.Cylinder, chest, chestAt + fwd * (0.12f * u), Quaternion.LookRotation(up, -fwd), new Vector3(0.3f * u, 0.02f * u, 0.3f * u), glow);

            // ---- the arms ----
            foreach (var side in new[] { "Left", "Right" })
            {
                var fore = Bone(skin, side + "ForeArm");
                var hand = Bone(skin, side + "Hand");
                if (fore == null || hand == null || a == 0) continue;
                var axis = hand.position - fore.position;
                float len = axis.magnitude;
                if (len < 1e-4f) continue;
                var along = Quaternion.FromToRotation(Vector3.up, axis / len);
                var ringMat = a >= 3 ? glow : plate;
                // the wrist ring (thick at step 2: a forearm guard)
                float guard = a >= 2 ? 0.55f : 0.14f;
                Piece(PrimitiveType.Cylinder, fore, fore.position + axis * (0.85f - guard * 0.5f), along, new Vector3(0.4f * u, len * guard * 0.5f, 0.4f * u), plate);
                Piece(PrimitiveType.Cylinder, fore, fore.position + axis * 0.88f, along, new Vector3(0.44f * u, 0.03f * u, 0.44f * u), ringMat);
                if (a >= 3) Piece(PrimitiveType.Cylinder, fore, fore.position + axis * (0.86f - guard * 0.8f), along, new Vector3(0.44f * u, 0.03f * u, 0.44f * u), glow);
                if (a >= 4) Piece(PrimitiveType.Sphere, hand, hand.position + axis * 0.3f, Quaternion.identity, Vector3.one * (0.36f * u), Glow(new Color(1f, 0.75f, 0.3f)));
            }

            // ---- the shell ----
            if (s >= 2)
            {
                // rivets round the chest
                for (int i = 0; i < 6; i++)
                {
                    float ang = i * Mathf.PI / 3f + Mathf.PI / 6f;
                    Piece(PrimitiveType.Sphere, chest, chestAt + skin.right * (Mathf.Cos(ang) * 0.36f * u) + up * (Mathf.Sin(ang) * 0.25f * u) + fwd * (0.07f * u), Quaternion.identity, Vector3.one * (0.07f * u), gold);
                }
            }
            if (s >= 3)
            {
                // crystal spikes on the shoulders
                var crystal = MaterialFactory.CreateTransparent(new Color(0.55f, 0.9f, 1f, 0.75f), new Color(0.3f, 0.8f, 1.1f));
                foreach (var arm in new[] { armL, armR })
                    for (int i = 0; i < 3; i++)
                    {
                        var at = arm.position + up * ((0.24f + 0.04f * i) * u) + fwd * ((i - 1) * 0.16f * u);
                        Piece(PrimitiveType.Cube, arm, at, Quaternion.LookRotation(fwd, up) * Quaternion.Euler(45f, 0f, 45f), new Vector3(0.12f, 0.12f, 0.2f) * u, crystal);
                    }
            }
        }

        private static Material Glow(Color c) => MaterialFactory.Create(c, c * 2.5f);

        private static Transform Piece(PrimitiveType kind, Transform bone, Vector3 at, Quaternion rot, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(kind);
            go.name = Holder;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var t = go.transform;
            t.position = at;
            t.rotation = rot;
            t.localScale = size;
            t.SetParent(bone, true);
            return t;
        }
    }
}
