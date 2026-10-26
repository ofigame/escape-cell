using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// foi's hands closed on what they hold: each finger's joints turned toward the palm (a fist round the weapon's grip,
    /// round the shield's strap), the weapon hand turned so the grip runs through the fist (index side to the blade), and
    /// the weapon put in the fist's middle. The turns are worked out once at rest (the palms face down in the T-pose).
    /// No allocation per frame.
    /// </summary>
    public partial class SkinnedHero
    {
        private struct FingerJoint { public Transform bone; public Vector3 axis; public float angle; }

        private FingerJoint[] fingerJoints;
        private Transform indexR, pinkyR, middleR3;

        // how far each joint closes (degrees): the four fingers' base, middle and tip joints; the thumb's
        private static readonly float[] FingerCurl = { 78f, 88f, 62f };
        private static readonly float[] ThumbCurl = { 22f, 32f, 26f };

        /// <summary>The finger joints' turns toward the palm, from the body at rest (called while it stands at rest).</summary>
        private void KnowFingers()
        {
            var joints = new List<FingerJoint>();
            var down = -(Frame * Vector3.up); // (the palms face down at rest)
            foreach (var side in new[] { "Left", "Right" })
                foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" })
                    for (int j = 1; j <= 3; j++)
                    {
                        var b = FindBone(transform, side + "Hand" + finger + j);
                        var c = FindBone(transform, side + "Hand" + finger + (j + 1));
                        if (b == null || c == null) continue;
                        var dir = c.position - b.position;
                        var axis = Vector3.Cross(dir, down);
                        if (axis.sqrMagnitude < 1e-8f) continue;
                        joints.Add(new FingerJoint
                        {
                            bone = b,
                            axis = Quaternion.Inverse(b.rotation) * axis.normalized, // (in the joint's own frame)
                            angle = (finger == "Thumb" ? ThumbCurl : FingerCurl)[j - 1],
                        });
                    }
            fingerJoints = joints.ToArray();
            indexR = FindBone(transform, "RightHandIndex1");
            pinkyR = FindBone(transform, "RightHandPinky1");
            middleR3 = FindBone(transform, "RightHandMiddle3");
        }

        /// <summary>Both hands closed (each joint from its rest, turned toward the palm).</summary>
        private void CloseHands()
        {
            if (fingerJoints == null) return;
            for (int i = 0; i < fingerJoints.Length; i++)
            {
                var f = fingerJoints[i];
                if (f.bone != null && restLocal.TryGetValue(f.bone, out var rest)) f.bone.localRotation = rest * Quaternion.AngleAxis(f.angle, f.axis);
            }
        }

        /// <summary>The weapon hand turned so the grip runs through the fist: the index side along the weapon's length.</summary>
        private void HandAlong(Vector3 length)
        {
            if (indexR == null || pinkyR == null || handR == null) return;
            var across = indexR.position - pinkyR.position;
            if (across.sqrMagnitude < 1e-8f || length.sqrMagnitude < 1e-8f) return;
            handR.rotation = Quaternion.FromToRotation(across, length) * handR.rotation;
        }

        /// <summary>The middle of the closed fist (where a grip goes): between the middle finger's root and its curled tip.</summary>
        private Vector3 FistMiddle => fingerR != null && middleR3 != null ? (fingerR.position + middleR3.position) * 0.5f
            : fingerR != null ? Vector3.Lerp(handR.position, fingerR.position, 0.65f) : handR.position;
    }
}
