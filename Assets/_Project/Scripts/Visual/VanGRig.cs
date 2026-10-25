using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A vanG made as a model (Resources/VanG/vang_N: a rigged body, no animation of its own). Its moves are made here,
    /// on its bones, every frame from the rest pose: the arms brought down from the T, a breath, a walk while it moves
    /// (legs striding, arms swinging, a bob), and its blows: a two-handed overhead slam (stomp), a sweep of one arm
    /// (swipe), both arms back over the head and forward (throw), and a jolt when hit.
    /// </summary>
    public class VanGRig : MonoBehaviour
    {
        /// <summary>The vanG of world <paramref name="world"/> (0-based): four of them, harder ones further on.</summary>
        public static int ModelFor(int world) => world < 0 ? 0 : 1 + Mathf.Clamp(world * 4 / Data.LevelCatalog.WorldCount, 0, 3);

        /// <summary>
        /// The vanG of a level (0-based), spread over the 250: the four models take turns through every world (each world
        /// starts on another one), every world tints them in its own colour (deeper the further on), and their size tells
        /// what they are: the guard chiefs of levels 1–6 small and growing, the strong chiefs of 8–9 bigger, the world's
        /// own monster on level 10 the biggest of all, glowing, its world's model.
        /// </summary>
        public static (int model, Color tint, float size, bool boss) LookFor(int level)
        {
            if (level < 0) return (1, Color.white, 1f, false);
            int world = level / 10, slot = level % 10;
            bool boss = slot == 9;
            int model = boss ? 1 + world % 4 : 1 + (world * 3 + slot) % 4;
            float hue = Mathf.Repeat(0.03f + world * 0.137f, 1f); // (worlds a good way round the colour wheel apart)
            float depth = Mathf.Lerp(0.22f, 0.5f, world / 24f) + (boss ? 0.15f : slot >= 7 ? 0.07f : 0f);
            var tint = Color.Lerp(Color.white, Color.HSVToRGB(hue, 0.85f, 1f), depth);
            float bossSize = 1.25f + 0.15f * (world / 24f);
            if (boss) return (model, tint, bossSize, true);
            // floors 1-9: vanG's guardian — grey metal with only a hint of the world's colour, a good deal smaller
            var metal = Color.Lerp(new Color(0.6f, 0.64f, 0.7f), Color.HSVToRGB(hue, 0.5f, 1f), 0.15f);
            return (model, metal, bossSize * Data.FloorTuning.Current.guardianScale, false);
        }

        /// <summary>Each model's turn so it faces +Z.</summary>
        public static float[] Yaw = { 0f, 0f, 0f, 0f };

        private const float Height = 2.8f; // (in the monster's body: about 4 tall in the world, more than twice foi)

        /// <summary>Builds vanG <paramref name="n"/> under <paramref name="parent"/>; null if its model is not there.</summary>
        public static VanGRig Build(Transform parent, int n, out Material paint)
        {
            paint = null;
            var model = Resources.Load<GameObject>("VanG/vang_" + n);
            if (model == null) return null;
            var go = Instantiate(model, parent, false);
            go.name = "VanG";
            var tex = Resources.Load<Texture2D>("VanG/vang_" + n + "_tex");
            paint = MaterialFactory.CreateSurface(Color.white, Color.black, 0.45f, 0.2f);
            if (tex != null) { paint.SetTexture("_BaseMap", tex); paint.SetTexture("_MainTex", tex); }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = paint;
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            // standing on the ground, the right height, facing +Z (the monster turns its body to the robot)
            var b = Measure(go);
            go.transform.localScale *= Height * parent.lossyScale.y / Mathf.Max(0.01f, b.size.y);
            b = Measure(go);
            go.transform.localRotation = Quaternion.Euler(0f, Yaw[Mathf.Clamp(n - 1, 0, 3)], 0f) * go.transform.localRotation;
            b = Measure(go);
            go.transform.position += new Vector3(parent.position.x - b.center.x, parent.position.y - b.min.y, parent.position.z - b.center.z);
            var rig = go.AddComponent<VanGRig>();
            rig.Setup();
            return rig;
        }

        private static Bounds Measure(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        private Transform hips, spine, chest, head, armL, armR, foreL, foreR, legL, legR, kneeL, kneeR;
        private Vector3 hipsRest, lastPos;
        private float time, stride, walk;
        private float slamT = -1f, sweepT = -1f, throwT = -1f, hitT = -1f;

        private Transform Bone(string end)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(":" + end) || t.name == end) return t;
            return null;
        }

        private void Setup()
        {
            hips = Bone("Hips"); spine = Bone("Spine"); chest = Bone("Spine2") ?? Bone("Spine1"); head = Bone("Head");
            armL = Bone("LeftArm"); armR = Bone("RightArm"); foreL = Bone("LeftForeArm"); foreR = Bone("RightForeArm");
            legL = Bone("LeftUpLeg"); legR = Bone("RightUpLeg"); kneeL = Bone("LeftLeg"); kneeR = Bone("RightLeg");
            foreach (var t in new[] { hips, spine, chest, head, armL, armR, foreL, foreR, legL, legR, kneeL, kneeR })
                if (t != null && !rest.ContainsKey(t)) rest[t] = t.localRotation;
            if (hips != null) hipsRest = hips.localPosition;
            lastPos = Frame.position;
        }

        /// <summary>The vanG's own frame: the monster's body (turned to face the robot, +Z ahead).</summary>
        private Transform Frame => transform.parent != null ? transform.parent : transform;

        public void Slam() { slamT = 0f; }
        public void Sweep() { sweepT = 0f; }
        public void Hurl() { throwT = 0f; }
        public void Jolt() { hitT = 0f; }

        /// <summary>Turns a bone about an axis given in the vanG's own frame (right, up, forward), after its parent.</summary>
        private void Turn(Transform bone, Vector3 axis, float degrees)
        {
            if (bone == null || Mathf.Abs(degrees) < 0.01f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, Frame.TransformDirection(axis)) * bone.rotation;
        }

        /// <summary>Points an upper arm along a direction in the vanG's frame (from its rest, wherever that was).</summary>
        private void Aim(Transform arm, Transform fore, Vector3 dir)
        {
            if (arm == null || fore == null) return;
            var now = fore.position - arm.position;
            var want = Frame.TransformDirection(dir.normalized);
            arm.rotation = Quaternion.FromToRotation(now, want) * arm.rotation;
        }

        private float Side(Transform t) => t == null ? 1f : Mathf.Sign(Vector3.Dot(t.position - Frame.position, Frame.right));

        private void LateUpdate()
        {
            if (hips == null) return;
            float dt = Time.deltaTime;
            time += dt;
            // walking: from how fast it travels (a leap counts as a walk only on the ground)
            var p = Frame.position;
            var v = p - lastPos; v.y = 0f;
            lastPos = p;
            float speed = dt > 0.0001f ? v.magnitude / dt : 0f;
            walk = Mathf.MoveTowards(walk, Mathf.Clamp01(speed / 1.5f), dt * 4f);
            stride += dt * Mathf.Lerp(3f, 9f, Mathf.Clamp01(speed / 3f));

            foreach (var kv in rest) kv.Key.localRotation = kv.Value;
            hips.localPosition = hipsRest;

            float sL = Side(armL), sR = Side(armR);
            // arms down at its sides, a little out and forward (from the T or wherever the model held them)
            Aim(armL, foreL, new Vector3(sL * 0.42f, -1f, 0.18f));
            Aim(armR, foreR, new Vector3(sR * 0.42f, -1f, 0.18f));
            Turn(foreL, Vector3.right, -25f); Turn(foreR, Vector3.right, -25f); // elbows a little bent, fists up front

            // breath
            Turn(chest, Vector3.right, Mathf.Sin(time * 2f) * 2f);
            Turn(head, Vector3.right, Mathf.Sin(time * 2f + 0.6f) * 2f);

            // walk: legs stride, knees bend on the way through, arms swing the other way, a bob
            float s = Mathf.Sin(stride), c = Mathf.Cos(stride);
            Turn(legL, Vector3.right, -s * 32f * walk);
            Turn(legR, Vector3.right, s * 32f * walk);
            Turn(kneeL, Vector3.right, Mathf.Max(0f, c) * 38f * walk);
            Turn(kneeR, Vector3.right, Mathf.Max(0f, -c) * 38f * walk);
            Turn(armL, Vector3.right, s * 26f * walk);
            Turn(armR, Vector3.right, -s * 26f * walk);
            Turn(spine, Vector3.up, s * 6f * walk);
            hips.position += Frame.up * Mathf.Abs(s) * 0.035f * walk * Frame.lossyScale.y;

            // the slam: both arms up over the head, then down hard in front, the body bending into it
            if (slamT >= 0f)
            {
                slamT += dt;
                float up = slamT < 0.45f ? Mathf.SmoothStep(0f, 1f, slamT / 0.45f) : 0f;
                float down = slamT >= 0.45f ? Mathf.Clamp01(1f - (slamT - 0.6f) / 0.35f) : 0f;
                Turn(armL, Vector3.right, -165f * up + 60f * down); Turn(armR, Vector3.right, -165f * up + 60f * down);
                Turn(chest, Vector3.right, -12f * up + 22f * down);
                Turn(kneeL, Vector3.right, 20f * down); Turn(kneeR, Vector3.right, 20f * down);
                if (slamT > 0.95f) slamT = -1f;
            }
            // the sweep: the right arm swings back, then across in front
            if (sweepT >= 0f)
            {
                sweepT += dt;
                float back = sweepT < 0.25f ? sweepT / 0.25f : 0f;
                float across = sweepT >= 0.25f ? Mathf.Clamp01((sweepT - 0.25f) / 0.2f) * Mathf.Clamp01(1f - (sweepT - 0.45f) / 0.25f) : 0f;
                Turn(armR, Vector3.up, sR * (40f * back - 95f * across));
                Turn(armR, Vector3.forward, sR * -70f * Mathf.Max(back, across));
                Turn(chest, Vector3.up, sR * (15f * back - 25f * across));
                if (sweepT > 0.7f) sweepT = -1f;
            }
            // the throw: both arms back over the head, then thrown forward
            if (throwT >= 0f)
            {
                throwT += dt;
                float wind = throwT < 0.35f ? throwT / 0.35f : Mathf.Clamp01(1f - (throwT - 0.35f) / 0.12f);
                float release = throwT >= 0.35f ? Mathf.Clamp01((throwT - 0.35f) / 0.15f) * Mathf.Clamp01(1f - (throwT - 0.5f) / 0.25f) : 0f;
                Turn(armL, Vector3.right, -190f * wind - 100f * release); Turn(armR, Vector3.right, -190f * wind - 100f * release);
                Turn(chest, Vector3.right, -18f * wind + 20f * release);
                if (throwT > 0.75f) throwT = -1f;
            }
            // hit: a jolt back
            if (hitT >= 0f)
            {
                hitT += dt;
                float k = Mathf.Sin(Mathf.Clamp01(hitT / 0.3f) * Mathf.PI);
                Turn(chest, Vector3.right, -16f * k); Turn(head, Vector3.right, -12f * k);
                if (hitT > 0.3f) hitT = -1f;
            }
        }
    }
}
