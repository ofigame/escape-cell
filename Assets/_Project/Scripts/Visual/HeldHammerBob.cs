using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>The carried Thunder Hammer bobbing on the robot's shoulder.</summary>
    public class HeldHammerBob : MonoBehaviour
    {
        private float t;
        private Vector3 basePos;

        private void Start() => basePos = transform.localPosition;

        private void Update()
        {
            t += Time.deltaTime;
            transform.localPosition = basePos + new Vector3(0f, Mathf.Sin(t * 5f) * 0.03f, 0f);
        }
    }
}
