using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>Lens helpers for tall, narrow phones, so a view never loses its sides compared with a 9:16 screen.</summary>
    public static class CameraFit
    {
        /// <summary>A vertical field of view that keeps at least the width <paramref name="fov"/> gives on a 9:16 screen.</summary>
        public static float Fov(float fov, float aspect)
        {
            float halfWidth = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * (9f / 16f);
            float needed = 2f * Mathf.Atan(halfWidth / Mathf.Max(0.1f, aspect)) * Mathf.Rad2Deg;
            return Mathf.Clamp(needed, fov, 85f);
        }
    }
}
