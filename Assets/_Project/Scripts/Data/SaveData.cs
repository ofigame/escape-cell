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

        /// <summary>Test builds (the computer, and the developer's own test APK) can open every level and arena; Play bundles never.</summary>
#if REAL_ADS || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && !TEST_TOOLS)
        private const bool TestModeDefault = false;
#else
        private const bool TestModeDefault = true;
#endif

        /// <summary>Highest level index (0-based) the player may start. Lower levels count as completed.</summary>
        public static int UnlockedLevel
        {
            get => Prefs.GetInt(UnlockedKey, 0);
            set { Prefs.SetInt(UnlockedKey, value); Prefs.Save(); }
        }

        public static int Coins
        {
            get => Prefs.GetInt(CoinsKey, 0);
            set { Prefs.SetInt(CoinsKey, value); Prefs.Save(); }
        }

        /// <summary>foi's build: there is only the one foi (an old choice of another build reads as foi).</summary>
        public static int Hero
        {
            get => 0;
            set { Prefs.SetInt("sb_hero", value); Prefs.Save(); }
        }

        /// <summary>foi's head was picked (free) at the first start of this version.</summary>
        public static bool HeadChosen
        {
            get => Prefs.GetInt("sb_head_chosen", 0) == 1;
            set { Prefs.SetInt("sb_head_chosen", value ? 1 : 0); Prefs.Save(); }
        }

        /// <summary>The build was picked at the very first start (the picker never opens by itself again).</summary>
        public static bool HeroChosen
        {
            get => Prefs.GetInt("sb_hero_chosen", 0) == 1;
            set { Prefs.SetInt("sb_hero_chosen", value ? 1 : 0); Prefs.Save(); }
        }

        /// <summary>How far the play camera stands from the robot: 0 near … 3 far (default 2).</summary>
        /// <summary>The camera drops lower and closer while foi fights robots or vanG (not for bugs).</summary>
        /// <summary>The lane duel (on, the trial): both foi on one line across the screen, moving only left and right.</summary>
        public static bool LaneDuel
        {
            get => Prefs.GetInt("sb_lane_duel", 1) == 1;
            set { Prefs.SetInt("sb_lane_duel", value ? 1 : 0); Prefs.Save(); }
        }

        public static bool CombatCamera
        {
            get => Prefs.GetInt("sb_combat_cam", 1) == 1;
            set { Prefs.SetInt("sb_combat_cam", value ? 1 : 0); Prefs.Save(); }
        }

        public static int CameraDistance
        {
            get => Mathf.Clamp(Prefs.GetInt("sb_cam_distance7", 2), 0, 3); // FAR by default (a fresh key: everyone starts far)
            set { Prefs.SetInt("sb_cam_distance7", Mathf.Clamp(value, 0, 3)); Prefs.Save(); }
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
#if REAL_ADS || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && !TEST_TOOLS)
            get => false; // levels open one by one on every phone build
#else
            get => GetBool(TestModeKey, TestModeDefault);
#endif
            set => SetBool(TestModeKey, value);
        }

        public static Language Language
        {
            get => (Language)Prefs.GetInt(LanguageKey, (int)Loc.SystemDefault);
            set { Prefs.SetInt(LanguageKey, (int)value); Prefs.Save(); }
        }

        private static bool GetBool(string key, bool fallback) => Prefs.GetInt(key, fallback ? 1 : 0) == 1;

        private static void SetBool(string key, bool value)
        {
            Prefs.SetInt(key, value ? 1 : 0);
            Prefs.Save();
        }
    }
}
