using System;
using UnityEngine;

namespace SquashBot.Data
{
    /// <summary>
    /// The graphics tier. "Auto" (the default) reads the device: memory and CPU cores say roughly how new and strong a
    /// phone is (an iPhone 15 has 6 GB, an older iPhone 11 4 GB, a budget Android 3-4 GB). The player can also pick a
    /// tier in settings; the choice is saved. Desktop builds are always High.
    /// </summary>
    public static class GraphicsQuality
    {
        private const string Key = "sb_gfx"; // -1 auto, otherwise a GraphicsTier

        public static event Action Changed;

        /// <summary>The player's choice, or null for automatic.</summary>
        public static GraphicsTier? Choice
        {
            get
            {
                int v = PlayerPrefs.GetInt(Key, -1);
                return v < 0 || v > (int)GraphicsTier.High ? (GraphicsTier?)null : (GraphicsTier)v;
            }
            set
            {
                PlayerPrefs.SetInt(Key, value.HasValue ? (int)value.Value : -1);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static GraphicsTier Current => Choice ?? Detected;

        /// <summary>Auto, Low, Medium, High, Auto... for the settings button.</summary>
        public static void Next()
        {
            var c = Choice;
            Choice = !c.HasValue ? GraphicsTier.Low : c.Value == GraphicsTier.High ? (GraphicsTier?)null : c.Value + 1;
        }

        public static GraphicsTier Detected
        {
            get
            {
                if (!Application.isMobilePlatform) return GraphicsTier.High;
                int ram = SystemInfo.systemMemorySize;
                int cores = SystemInfo.processorCount;
                if (Application.platform == RuntimePlatform.IPhonePlayer)
                {
                    // iPhones are fast for their memory: 6 GB+ (iPhone 13 Pro and newer) is High, 4 GB Medium.
                    if (ram >= 5500) return GraphicsTier.High;
                    return ram >= 3500 ? GraphicsTier.Medium : GraphicsTier.Low;
                }
                if (ram >= 7500 && cores >= 8) return GraphicsTier.High;
                return ram >= 3800 && cores >= 6 ? GraphicsTier.Medium : GraphicsTier.Low;
            }
        }
    }
}
