using UnityEngine;

namespace SquashBot.Core
{
    /// <summary>
    /// On a computer the game opens as a phone-shaped window (it is made for portrait): a wide window, or one remembered
    /// from an earlier run, is turned upright at start, as tall as most of the screen.
    /// </summary>
    public static class DesktopWindow
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Upright()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            if (Screen.height >= Screen.width && Screen.fullScreenMode == FullScreenMode.Windowed) return;
            int h = Mathf.RoundToInt(Screen.currentResolution.height * 0.88f);
            int w = Mathf.RoundToInt(h * 9f / 19.5f);
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
#endif
        }
    }
}
