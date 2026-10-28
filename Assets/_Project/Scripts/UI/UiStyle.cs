using UnityEngine;

namespace SquashBot.UI
{
    /// <summary>
    /// The game's one design language as data (Docs/TasarimSistemi.md, taken from the main menu): its colours, type sizes
    /// and spacing. Every screen built with <see cref="UiKit"/> (and the older <see cref="Flat"/> kit, the UI colours of
    /// the palette) reads them here, so a colour changed here changes everywhere. Resources/UI/UiStyle (an asset made from
    /// this, when there is one) or these defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/UI/UI Style", fileName = "UiStyle")]
    public class UiStyle : ScriptableObject
    {
        [Header("Colours")]
        public Color back = new Color(0.04f, 0.07f, 0.16f);
        public Color panel = new Color(0.09f, 0.19f, 0.4f);
        public Color panelHi = new Color(0.15f, 0.29f, 0.55f);
        public Color line = new Color(1f, 1f, 1f, 0.08f);
        public Color text = new Color(0.933f, 0.941f, 1f);
        public Color textMuted = new Color(0.62f, 0.65f, 0.76f);
        public Color textFaint = new Color(1f, 1f, 1f, 0.6f);
        public Color gold = new Color(1f, 0.831f, 0.361f);
        public Color cyan = new Color(0.624f, 0.953f, 1f);
        public Color success = new Color(0.184f, 0.816f, 0.478f);
        public Color warning = new Color(1f, 0.706f, 0.141f);
        public Color danger = new Color(1f, 0.478f, 0.522f);
        public Color mine = new Color(0.3f, 0.6f, 1f);
        public Color theirs = new Color(1f, 0.36f, 0.34f);
        public Color dim = new Color(0.2f, 0.19f, 0.36f, 0.55f);
        public Color pill = new Color(0.05f, 0.12f, 0.3f, 0.78f);

        [Header("Type (on the 1080-wide canvas)")]
        public float titleSize = 56f;
        public float subtitleSize = 42f;
        public float bodySize = 30f;
        public float smallSize = 24f;
        public float numberSize = 46f;

        [Header("Spacing")]
        public float headerHeight = 190f;
        public float backButton = 124f;
        public float gap = 16f;
        public float pad = 24f;
        public float edge = 36f;

        private static UiStyle current;

        /// <summary>The style in use (the asset in Resources/UI, else the defaults).</summary>
        public static UiStyle Current
        {
            get
            {
                if (current == null) current = Resources.Load<UiStyle>("UI/UiStyle");
                if (current == null) { current = CreateInstance<UiStyle>(); current.hideFlags = HideFlags.DontSave; }
                return current;
            }
        }
    }
}
