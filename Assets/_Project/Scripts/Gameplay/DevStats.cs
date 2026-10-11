using UnityEngine;
using UnityEngine.Profiling;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// Test builds only (development builds; never in the store build): a small read-out in the top-left corner with
    /// the frame rate (smoothed, and the worst frame of the last second) and the memory in use, to check on real
    /// phones that the game stays smooth and light.
    /// </summary>
    public class DevStats : MonoBehaviour
    {
        private float smoothed = 1f / 60f, worst, worstShown, timer;
        private long memory;
        private GUIStyle style;
        private string text = "";

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            smoothed = Mathf.Lerp(smoothed, dt, 0.08f);
            worst = Mathf.Max(worst, dt);
            timer += dt;
            if (timer < 0.5f) return;
            timer = 0f;
            worstShown = worst;
            worst = 0f;
            memory = Profiler.GetTotalAllocatedMemoryLong() / 1048576;
            text = $"FPS {1f / Mathf.Max(0.0001f, smoothed):0}  (en kötü {worstShown * 1000f:0} ms)\nBellek {memory} MB";
        }

        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(18, Screen.height / 60), fontStyle = FontStyle.Bold };
                style.normal.textColor = Color.white;
            }
            var r = new Rect(12f, Screen.height * 0.21f, Screen.width * 0.6f, style.fontSize * 3f); // under the HUD's health bar
            var shadow = new Rect(r.x + 2f, r.y + 2f, r.width, r.height);
            var c = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(shadow, text, style);
            style.normal.textColor = memory > 600 || worstShown > 0.05f ? new Color(1f, 0.55f, 0.4f) : new Color(0.6f, 1f, 0.7f);
            GUI.Label(r, text, style);
            style.normal.textColor = c;
        }
    }
}
