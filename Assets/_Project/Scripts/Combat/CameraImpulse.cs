using SquashBot.Data;
using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// The camera knocked by a moment, along the blow (a quick shove that rings down), laid over wherever the camera rig
    /// put the camera this frame. Off when the player turned screen shake off.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class CameraImpulse : MonoBehaviour
    {
        private static CameraImpulse instance;
        private Vector3 dir;
        private float amp, t = 1f;

        /// <summary>A knock of <paramref name="level"/> along <paramref name="worldDirection"/> (scaled by the player's shake setting).</summary>
        public static void Fire(CameraImpulseLevel level, Vector3 worldDirection, float multiplier = 1f)
        {
            if (level == CameraImpulseLevel.None || !SaveData.ScreenShake) return;
            var s = CombatLibrary.Settings;
            if (s.lowQualityTrims && GraphicsQuality.Current == GraphicsTier.Low && level < CameraImpulseLevel.Medium) return;
            var a = s.impulseStrength;
            float strength = a == null || a.Length == 0 ? 0f : a[Mathf.Clamp((int)level, 0, a.Length - 1)] * multiplier;
            if (strength <= 0f) return;
            if (instance == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                instance = cam.GetComponent<CameraImpulse>();
                if (instance == null) instance = cam.gameObject.AddComponent<CameraImpulse>();
            }
            if (instance.t < 1f && instance.amp > strength) return; // (a bigger knock still ringing)
            worldDirection.y = 0f;
            instance.dir = worldDirection.sqrMagnitude > 1e-6f ? worldDirection.normalized : Vector3.right;
            instance.amp = strength;
            instance.t = 0f;
        }

        private void LateUpdate()
        {
            if (t >= 1f) return;
            float fade = Mathf.Max(0.05f, CombatLibrary.Settings.impulseFade);
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / fade);
            float ring = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * (1f - t);
            var side = Vector3.Cross(Vector3.up, dir);
            transform.position += (dir * 0.8f + side * 0.35f + Vector3.up * 0.25f) * (amp * ring);
        }
    }
}
