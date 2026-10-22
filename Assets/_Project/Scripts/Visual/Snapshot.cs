using UnityEngine;

namespace SquashBot.Visual
{
    /// <summary>
    /// A still picture of what a camera sees, kept light: drawn smooth (with anti-aliasing, into a borrowed target),
    /// then copied into a plain picture with no depth and no extra samples. A kept 512 picture costs 1 MB this way
    /// instead of 13 (the shop, the workshop and the chests keep dozens of them).
    /// </summary>
    public static class Snapshot
    {
        public static RenderTexture Take(Camera cam, int size, string name, int samples = 4)
        {
            var draw = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, samples);
            var keep = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32) { name = name, useMipMap = false };
            keep.Create();
            var was = cam.targetTexture;
            cam.targetTexture = draw;
            cam.Render();
            cam.targetTexture = was;
            Graphics.Blit(draw, keep);
            RenderTexture.ReleaseTemporary(draw);
            return keep;
        }
    }
}
