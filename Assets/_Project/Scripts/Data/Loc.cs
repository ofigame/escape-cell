using System.Collections.Generic;
using UnityEngine;

namespace SquashBot.Data
{
    public enum Language
    {
        English,
        Turkish
    }

    /// <summary>Tiny localization table (English / Turkish). Missing keys fall back to English, then to the key.</summary>
    public static class Loc
    {
        private static Language? current;

        // Read lazily: PlayerPrefs may only be touched from the main thread after startup.
        public static Language Current => current ?? (current = SaveData.Language).Value;

        public static void Set(Language language)
        {
            current = language;
            SaveData.Language = language;
        }

        public static string T(string key)
        {
            var table = Current == Language.Turkish ? Turkish : English;
            if (table.TryGetValue(key, out var value)) return value;
            return English.TryGetValue(key, out value) ? value : key;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        /// <summary>Default for first launch: Turkish on Turkish devices, English elsewhere.</summary>
        public static Language SystemDefault => Application.systemLanguage == SystemLanguage.Turkish ? Language.Turkish : Language.English;

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            ["menu.play"] = "PLAY",
            ["menu.tag"] = "NEON DODGE",
            ["menu.hint"] = "Swipe to jump. Dodge the red blocks!",
            ["level"] = "LEVEL {0}",
            ["world"] = "WORLD {0}  ·  {1}",
            ["world.lavender"] = "LAVENDER SPACE",
            ["world.sunset"] = "SUNSET CORAL",
            ["world.ice"] = "ICE CRYSTAL",
            ["world.neon"] = "NEON NIGHT",
            ["world.lava"] = "LAVA CORE",
            ["world.forest"] = "MINT FOREST",
            ["world.desert"] = "GOLDEN DESERT",
            ["world.ocean"] = "DEEP OCEAN",
            ["world.candy"] = "CANDY POP",
            ["world.toxic"] = "TOXIC LAB",
            ["world.midnight"] = "MIDNIGHT",
            ["world.aurora"] = "AURORA",
            ["world.storm"] = "THUNDER STORM",
            ["world.cyber"] = "CYBER GRID",
            ["world.galaxy"] = "GALAXY CORE",
            ["float.closeCount"] = "CLOSE CALL {0}/{1}",
            ["float.armor"] = "ARMOR!",
            ["hint.title"] = "TIP",
            ["hint.jump"] = "SWIPE INTO A HOLE TO LEAP OVER IT",
            ["float.smash"] = "SMASH!",
            ["lives.title"] = "OUT OF LIVES",
            ["lives.next"] = "Next life in {0}",
            ["lives.full"] = "Lives are full",
            ["lives.watchAd"] = "WATCH AD  +1",
            ["lives.left"] = "Lives left: {0}",
            ["ad.test"] = "TEST AD",
            ["ad.wait"] = "Reward in {0}...",
            ["ad.unavailable"] = "No ad available right now",
            ["ad.banner"] = "TEST AD AREA",
            ["mission.collect"] = "Collect {0} coins",
            ["mission.survive"] = "Survive {0} seconds",
            ["mission.collect.up"] = "COLLECT {0} COINS",
            ["mission.survive.up"] = "SURVIVE {0} SECONDS",
            ["hud.coins"] = "{0} / {1} coins",
            ["hud.survive"] = "Survive  {0}s",
            ["hud.shield"] = "SHIELD  {0}s",
            ["pause.title"] = "PAUSED",
            ["btn.resume"] = "RESUME",
            ["btn.restart"] = "RESTART",
            ["btn.menu"] = "MENU",
            ["btn.retry"] = "RETRY",
            ["btn.next"] = "NEXT",
            ["btn.map"] = "MAP",
            ["btn.close"] = "CLOSE",
            ["result.win"] = "LEVEL CLEAR!",
            ["result.lose"] = "SQUASHED!",
            ["result.next"] = "Ready for the next one?",
            ["result.allDone"] = "You beat every level!",
            ["result.newWorld"] = "New world unlocked! Your robot evolved.",
            ["lose.block"] = "A block got you.",
            ["lose.fall"] = "You fell through the floor.",
            ["float.close"] = "CLOSE CALL!",
            ["float.shield"] = "SHIELD!",
            ["float.blocked"] = "BLOCKED!",
            ["settings.title"] = "SETTINGS",
            ["settings.sound"] = "Sound",
            ["settings.music"] = "Music",
            ["settings.vibration"] = "Vibration",
            ["settings.language"] = "Language",
            ["settings.camera"] = "Camera",
            ["on"] = "ON",
            ["off"] = "OFF",
            ["lang.name"] = "ENGLISH",
            ["view.iso"] = "ISO",
            ["view.3d"] = "3D",
            ["map.title"] = "MAP",
            ["map.locked"] = "Finish world {0} to unlock",
        };

        private static readonly Dictionary<string, string> Turkish = new Dictionary<string, string>
        {
            ["menu.play"] = "OYNA",
            ["menu.tag"] = "NEON KAÇIŞ",
            ["menu.hint"] = "Zıplamak için kaydır. Kırmızı bloklardan kaç!",
            ["level"] = "BÖLÜM {0}",
            ["world"] = "DÜNYA {0}  ·  {1}",
            ["world.lavender"] = "LAVANTA UZAY",
            ["world.sunset"] = "GÜN BATIMI",
            ["world.ice"] = "BUZ KRİSTALİ",
            ["world.neon"] = "NEON GECE",
            ["world.lava"] = "LAV ÇEKİRDEĞİ",
            ["world.forest"] = "NANE ORMANI",
            ["world.desert"] = "ALTIN ÇÖL",
            ["world.ocean"] = "DERİN OKYANUS",
            ["world.candy"] = "ŞEKER DİYARI",
            ["world.toxic"] = "ZEHİRLİ LAB",
            ["world.midnight"] = "GECE YARISI",
            ["world.aurora"] = "KUTUP IŞIKLARI",
            ["world.storm"] = "FIRTINA",
            ["world.cyber"] = "SİBER IZGARA",
            ["world.galaxy"] = "GALAKSİ ÇEKİRDEĞİ",
            ["float.closeCount"] = "KIL PAYI {0}/{1}",
            ["float.armor"] = "ZIRH!",
            ["hint.title"] = "İPUCU",
            ["hint.jump"] = "ÇUKURA DOĞRU KAYDIR, ÜSTÜNDEN ATLA",
            ["float.smash"] = "PAT!",
            ["lives.title"] = "CANIN KALMADI",
            ["lives.next"] = "Sonraki can: {0}",
            ["lives.full"] = "Canların dolu",
            ["lives.watchAd"] = "REKLAM İZLE  +1",
            ["lives.left"] = "Kalan can: {0}",
            ["ad.test"] = "TEST REKLAMI",
            ["ad.wait"] = "Ödül {0} sn sonra...",
            ["ad.unavailable"] = "Şu an reklam yok",
            ["ad.banner"] = "TEST REKLAM ALANI",
            ["mission.collect"] = "{0} altın topla",
            ["mission.survive"] = "{0} saniye hayatta kal",
            ["mission.collect.up"] = "{0} ALTIN TOPLA",
            ["mission.survive.up"] = "{0} SANİYE HAYATTA KAL",
            ["hud.coins"] = "{0} / {1} altın",
            ["hud.survive"] = "Dayan  {0} sn",
            ["hud.shield"] = "KALKAN  {0} sn",
            ["pause.title"] = "DURAKLATILDI",
            ["btn.resume"] = "DEVAM",
            ["btn.restart"] = "YENİDEN",
            ["btn.menu"] = "MENÜ",
            ["btn.retry"] = "TEKRAR",
            ["btn.next"] = "SONRAKİ",
            ["btn.map"] = "HARİTA",
            ["btn.close"] = "KAPAT",
            ["result.win"] = "BÖLÜM TAMAM!",
            ["result.lose"] = "EZİLDİN!",
            ["result.next"] = "Sıradakine hazır mısın?",
            ["result.allDone"] = "Tüm bölümleri bitirdin!",
            ["result.newWorld"] = "Yeni dünya açıldı! Robotun gelişti.",
            ["lose.block"] = "Bir blok seni ezdi.",
            ["lose.fall"] = "Zeminden aşağı düştün.",
            ["float.close"] = "KIL PAYI!",
            ["float.shield"] = "KALKAN!",
            ["float.blocked"] = "ENGELLENDİ!",
            ["settings.title"] = "AYARLAR",
            ["settings.sound"] = "Ses",
            ["settings.music"] = "Müzik",
            ["settings.vibration"] = "Titreşim",
            ["settings.language"] = "Dil",
            ["settings.camera"] = "Kamera",
            ["on"] = "AÇIK",
            ["off"] = "KAPALI",
            ["lang.name"] = "TÜRKÇE",
            ["view.iso"] = "ISO",
            ["view.3d"] = "3D",
            ["map.title"] = "HARİTA",
            ["map.locked"] = "Açmak için {0}. dünyayı bitir",
        };
    }
}
