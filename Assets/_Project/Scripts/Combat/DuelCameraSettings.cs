using UnityEngine;

namespace SquashBot.Combat
{
    /// <summary>
    /// The duel's camera from the front (the design's §2.4): close enough that the two foi fill the band from about 30% to
    /// 60% of the screen's height (the bottom third belongs to the controls). The distance fits both foi side by side
    /// (with room round them), never nearer or further than the limits; the camera looks at a point this share of its
    /// distance above their feet, which sets how low they stand on the screen. Resources/Duel/DuelCameraSettings.
    /// </summary>
    [CreateAssetMenu(menuName = "foi/Duel/Camera Settings", fileName = "DuelCameraSettings")]
    public class DuelCameraSettings : ScriptableObject
    {
        /// <summary>Room either side of the outermost foi (tiles: an arm, a weapon, a fall).</summary>
        public float sideRoom = 3.1f;
        /// <summary>The nearest and furthest the camera stands (the old nearest was 15.5: 18% nearer now).</summary>
        public float minDistance = 12.7f, maxDistance = 46f;
        /// <summary>The camera's height: this much plus this share of its distance.</summary>
        public float heightBase = 2.2f, heightPerDistance = 0.2f;
        /// <summary>The point looked at stands this share of the distance above the feet (higher: the foi lower on the screen).</summary>
        public float focusPerDistance = 0.07f;
        /// <summary>The vertical field of view (degrees) the fit is worked out for.</summary>
        public float fov = 46f;

        private static DuelCameraSettings loaded;

        public static DuelCameraSettings Current
        {
            get
            {
                if (loaded == null) loaded = Resources.Load<DuelCameraSettings>("Duel/DuelCameraSettings");
                if (loaded == null) loaded = CreateInstance<DuelCameraSettings>();
                return loaded;
            }
        }
    }
}
