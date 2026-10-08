using UnityEngine;

namespace SquashBot.Data
{
    public static class SaveData
    {
        private const string UnlockedKey = "sb_unlocked_level";
        private const string CoinsKey = "sb_coins";
        private const string PerspectiveKey = "sb_perspective_view";
        private const string SoundKey = "sb_sound";
        private const string MusicKey = "sb_music";
        private const string VibrationKey = "sb_vibration";
        private const string LanguageKey = "sb_language";
        private const string TestModeKey = "sb_test_mode";

        /// <summary>Test builds open every level on the map (lives are spent as in the real game). SET TO false BEFORE RELEASE.</summary>
        private const bool TestModeDefault = true;

        /// <summary>Highest level index (0-based) the player may start. Lower levels count as completed.</summary>
        public static int UnlockedLevel
        {
            get => PlayerPrefs.GetInt(UnlockedKey, 0);
            set { PlayerPrefs.SetInt(UnlockedKey, value); PlayerPrefs.Save(); }
        }

        public static int Coins
        {
            get => PlayerPrefs.GetInt(CoinsKey, 0);
            set { PlayerPrefs.SetInt(CoinsKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>Which of foi's four builds the player plays (0 Classic, 1 Volt, 2 Kaya, 3 Zip).</summary>
        public static int Hero
        {
            get => PlayerPrefs.GetInt("sb_hero", 0);
            set { PlayerPrefs.SetInt("sb_hero", value); PlayerPrefs.Save(); }
        }

        /// <summary>The build was picked at the very first start (the picker never opens by itself again).</summary>
        public static bool HeroChosen
        {
            get => PlayerPrefs.GetInt("sb_hero_chosen", 0) == 1;
            set { PlayerPrefs.SetInt("sb_hero_chosen", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>How far the play camera stands from the robot: 0 near … 3 far (default 2).</summary>
        public static int CameraDistance
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("sb_cam_distance2", 2), 0, 3);
            set { PlayerPrefs.SetInt("sb_cam_distance2", Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); }
        }

        public static bool PerspectiveView
        {
            get => GetBool(PerspectiveKey, false);
            set => SetBool(PerspectiveKey, value);
        }

        public static bool Sound
        {
            get => GetBool(SoundKey, true);
            set => SetBool(SoundKey, value);
        }

        public static bool Music
        {
            get => GetBool(MusicKey, true);
            set => SetBool(MusicKey, value);
        }

        public static bool Vibration
        {
            get => GetBool(VibrationKey, true);
            set => SetBool(VibrationKey, value);
        }

        /// <summary>Testing: every level is playable from the map.</summary>
        public static bool TestMode
        {
            get => GetBool(TestModeKey, TestModeDefault);
            set => SetBool(TestModeKey, value);
        }

        public static Language Language
        {
            get => (Language)PlayerPrefs.GetInt(LanguageKey, (int)Loc.SystemDefault);
            set { PlayerPrefs.SetInt(LanguageKey, (int)value); PlayerPrefs.Save(); }
        }

        private static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) == 1;

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
