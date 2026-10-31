using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The prototype's own camera (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 6.1): from the stage's open side, both foi always in
    /// the frame, nearer as they close in and further as they part (between the settings' limits), smoothed; the two in
    /// the band 30–60% of the upright screen's height. Moments (shake, zoom, a slow push in) are laid over it.
    /// </summary>
    public class DuelCameraV2 : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        private Vector3 mid, side, line;
        private float dist = 16f, scale = 1f, height = 6f;
        private bool placed;
        // the moments laid over the follow: a zoom (share nearer), a shake, a push toward a point
        private float zoom, zoomTarget, shake, shakeTime;
        private Vector3 shakeDir;
        private Vector3 focusOverride; private float focusWeight, focusTarget;
        private float rise;

        /// <summary>A fighter's height in the world (the band on screen is worked out from it).</summary>
        public float FighterHeight { get => height; set => height = Mathf.Max(0.5f, value); }

        public static DuelCameraV2 Create(Vector3 side, Vector3 line, float fighterScale)
        {
            var go = new GameObject("DuelCameraV2");
            var c = go.AddComponent<DuelCameraV2>();
            c.Cam = go.AddComponent<Camera>();
            c.Cam.depth = 50f;
            c.Cam.fieldOfView = 46f;
            c.Cam.nearClipPlane = 0.3f;
            c.Cam.farClipPlane = 700f;
            c.Cam.clearFlags = CameraClearFlags.Skybox;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            c.side = side.normalized; c.line = line.normalized; c.scale = fighterScale;
            return c;
        }

        /// <summary>Each frame: the two fighters' feet (world).</summary>
        public void Follow(Vector3 a, Vector3 b, float dt)
        {
            var s = DuelCoreSettings.Current;
            var m = (a + b) * 0.5f;
            float apart = Mathf.Abs(Vector3.Dot(b - a, line));
            float vf = Cam.fieldOfView * Mathf.Deg2Rad;
            float hf = 2f * Mathf.Atan(Mathf.Tan(vf * 0.5f) * Mathf.Max(0.3f, Cam.aspect));
            // near enough for the fighters to fill their band of the screen, far enough for both (and room) across it
            float visible = height / Mathf.Max(0.1f, s.camBand);
            float forBand = visible * 0.5f / Mathf.Tan(vf * 0.5f);
            float forWidth = (apart * 0.5f + s.camSideRoom) / Mathf.Tan(hf * 0.5f);
            float want = Mathf.Clamp(Mathf.Max(forBand, forWidth), s.camNear, s.camFar);
            float k = placed ? 1f - Mathf.Exp(-s.camFollow * dt) : 1f;
            mid = placed ? Vector3.Lerp(mid, m, k) : m;
            dist = placed ? Mathf.Lerp(dist, want, k) : want;
            placed = true;
            zoom = Mathf.MoveTowards(zoom, zoomTarget, dt * 0.6f);
            focusWeight = Mathf.MoveTowards(focusWeight, focusTarget, dt * 3f);
            if (shakeTime > 0f) shakeTime -= dt;
            float d = dist * (1f - zoom);
            // the feet at camFeetAt of the screen: the middle of the view stands this much over them
            float shown = 2f * d * Mathf.Tan(vf * 0.5f);
            var look = mid + Vector3.up * (shown * (0.5f - s.camFeetAt) + rise);
            if (focusWeight > 0.001f) look = Vector3.Lerp(look, focusOverride, focusWeight);
            float pitch = s.camPitch * Mathf.Deg2Rad;
            var pos = look + side * d * Mathf.Cos(pitch) + Vector3.up * (d * Mathf.Sin(pitch) + rise * 0.5f);
            if (shakeTime > 0f && s.screenShake) pos += shakeDir * shake * Mathf.Sin(Time.unscaledTime * 70f) * (shakeTime / 0.25f);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(look - pos);
        }

        /// <summary>A little nearer (share of the distance) for a while; 0 back.</summary>
        public void Zoom(float share) => zoomTarget = Mathf.Clamp(share, 0f, 0.5f);
        public void Shake(Vector3 dir, float amount) { shakeDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.up; shake = amount; shakeTime = 0.25f; }
        /// <summary>The view drawn toward a point (a knock-out, the last drop), weight 0..1.</summary>
        public void Focus(Vector3 at, float weight) { focusOverride = at; focusTarget = Mathf.Clamp01(weight); }
        /// <summary>The camera raised (the sky in view for a rain).</summary>
        public void Rise(float up) => rise = up;
    }
}
