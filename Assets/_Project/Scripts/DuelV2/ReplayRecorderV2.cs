using UnityEngine;

namespace SquashBot.DuelV2
{
    /// <summary>
    /// The match-ending knock-out's replay (Docs/DUELLO_DOVUS_CEKIRDEGI_V2.md 6.2): a second camera, close and at a three
    /// quarter angle on the two, keeps the last few seconds in a ring of small pictures; after the knock-out's slow
    /// motion the last two seconds play again, slower (a tap skips them).
    /// </summary>
    public class ReplayRecorderV2 : MonoBehaviour
    {
        private const int Fps = 30;
        private RenderTexture[] ring;
        private int head, count;
        private float nextAt;
        private Camera cam;
        private Vector3 side, line;
        private float height = 6f;
        // playing
        private int playFrom, playLen;
        private float playStart, playSeconds;
        public bool Playing { get; private set; }

        public static ReplayRecorderV2 Create(Vector3 side, Vector3 line, float fighterHeight, float seconds)
        {
            var go = new GameObject("ReplayRecorderV2");
            var r = go.AddComponent<ReplayRecorderV2>();
            r.cam = go.AddComponent<Camera>();
            r.cam.enabled = false;
            r.cam.fieldOfView = 34f;
            r.cam.nearClipPlane = 0.3f; r.cam.farClipPlane = 600f;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            r.side = side.normalized; r.line = line.normalized; r.height = fighterHeight;
            int n = Mathf.CeilToInt((seconds + 0.5f) * Fps);
            r.ring = new RenderTexture[n];
            int w = 360, h = Mathf.RoundToInt(360f * Screen.height / Mathf.Max(1f, Screen.width));
            for (int i = 0; i < n; i++) { r.ring[i] = new RenderTexture(w, h, 16); r.ring[i].Create(); }
            return r;
        }

        /// <summary>Each frame: the close camera on the two, a picture kept now and then (30 a second of real time).</summary>
        public void Record(Vector3 a, Vector3 b)
        {
            if (Playing || ring == null || Time.unscaledTime < nextAt) return;
            nextAt = Time.unscaledTime + 1f / Fps;
            var mid = (a + b) * 0.5f;
            float apart = Vector3.Distance(a, b);
            var dir = (Quaternion.AngleAxis(32f, Vector3.up) * side).normalized;
            var look = mid + Vector3.up * height * 0.45f;
            cam.transform.position = look + dir * (height * 1.3f + apart * 0.6f) + Vector3.up * height * 0.1f;
            cam.transform.LookAt(look);
            cam.targetTexture = ring[head];
            cam.Render();
            cam.targetTexture = null;
            head = (head + 1) % ring.Length;
            count = Mathf.Min(count + 1, ring.Length);
        }

        /// <summary>The last <paramref name="seconds"/> again over <paramref name="playFor"/> real seconds.</summary>
        public void Play(float seconds, float playFor)
        {
            playLen = Mathf.Min(count, Mathf.CeilToInt(seconds * Fps));
            if (playLen < 4) return;
            playFrom = (head - playLen + ring.Length) % ring.Length;
            playStart = Time.unscaledTime;
            playSeconds = playFor;
            Playing = true;
        }

        public void Stop() => Playing = false;

        /// <summary>The picture to show now (null when it is over).</summary>
        public Texture Frame()
        {
            if (!Playing) return null;
            float k = (Time.unscaledTime - playStart) / Mathf.Max(0.1f, playSeconds);
            if (k >= 1f) { Playing = false; return null; }
            int i = Mathf.Clamp(Mathf.FloorToInt(k * playLen), 0, playLen - 1);
            return ring[(playFrom + i) % ring.Length];
        }

        private void OnDestroy()
        {
            if (ring == null) return;
            foreach (var r in ring) if (r != null) { r.Release(); Destroy(r); }
        }
    }
}
