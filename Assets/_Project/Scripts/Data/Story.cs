using UnityEngine;

namespace SquashBot.Data
{
    public enum Speaker
    {
        Narrator,
        /// <summary>The Observer's partner: an old archive robot who remembers the green world.</summary>
        Bip,
        /// <summary>vanG, the AI that turned the universe into its cold, flawless painting.</summary>
        VanG,
        /// <summary>The Masked Thief, who turns out to be the First Observer.</summary>
        Thief
    }

    /// <summary>
    /// The story: an Observer robot, made by vanG to paint its perfectly aligned tiles, gets Free Will from its partner
    /// Bip and runs through vanG's tunnels, world by world, to its heart. The Observer never speaks; Bip, vanG, the
    /// Masked Thief and a narrator do. Scene f plays before floor f (every 10 levels); the eight big worlds open with
    /// the scenes in <see cref="ChapterStarts"/>. <see cref="Ending"/> plays after the last level.
    /// Lines live in Loc as "story.{scene}.{line}".
    /// </summary>
    public static class Story
    {
        public const int Ending = LevelCatalog.WorldCount;
        public const int LoseQuips = 8;

        /// <summary>The first floor of each big world: Channel, Forest, Red Canyon, Sea Floor, Snowy Mountain,
        /// Crystal Cave, Cloud Bridge and Star Road (levels 1, 21, 51, 81, 111, 141, 171 and 211).</summary>
        public static readonly int[] ChapterStarts = { 0, 2, 5, 8, 11, 14, 17, 21 };

        /// <summary>The scene where the Masked Thief takes off its mask (before level 211).</summary>
        public const int Reveal = 21;

        private const Speaker N = Speaker.Narrator, B = Speaker.Bip, V = Speaker.VanG, T = Speaker.Thief;

        private static readonly Speaker[][] Scenes =
        {
            new[] { N, N, B, N, V, B },    // 0: the Grey Channel, Bip and Free Will
            new[] { V, B, N, B, B },       // 1: the Rust Factory, fire bombs, the thief appears
            new[] { N, B, V, B, N },       // 2: the Forest World: green for the first time, block rows and fire
            new[] { B, V, B, N },          // 3: the Spring Garden, jet boots (hover)
            new[] { N, V, B, B },          // 4: the Night Forest, faster blocks
            new[] { N, V, B, B, N },       // 5: the Red Canyon: ancient mines, barrels
            new[] { V, B, B, V },          // 6: the Golden Desert, wind
            new[] { N, B, V, B },          // 7: the Ancient Mine, tablets and teleports
            new[] { N, V, B, B },          // 8: the Sea Floor: currents
            new[] { N, B, V, B },          // 9: the Deep Ocean, sticky seaweed, jellyfish
            new[] { N, V, B, B },          // 10: the Sunken Server, lasers
            new[] { N, V, B, B },          // 11: the Snowy Mountain: ice
            new[] { N, V, B, B },          // 12: the Snowy Peak, ice and snow
            new[] { V, B, N, B },          // 13: the Ice Crystal, thin ice (glass)
            new[] { N, V, B, V },          // 14: the Crystal Cave: illusions, lasers and teleports
            new[] { N, B, V, B },          // 15: the Hall of Mirrors, shadow clones
            new[] { V, B, N, B },          // 16: the Acid Crystals, poison
            new[] { N, V, B, B },          // 17: the Cloud Bridge: trampolines
            new[] { N, B, V, B },          // 18: the Sunset Skies, wind and trampolines
            new[] { V, B, N, B },          // 19: the Thunder Storm, blinking tiles
            new[] { V, B, N, B },          // 20: the Sky Castle, hunting blocks
            new[] { N, N, B, T, T, T, N, V }, // 21: the thief's secret, then the Star Road
            new[] { N, V, B, B },          // 22: Lavender Space, darkness
            new[] { N, V, B, B },          // 23: the Galaxy Core, darkness and trampolines
            new[] { N, V, B, V, B },       // 24: vanG's Heart, the lanterns
            new[] { V, B, N, N, N, B, N }, // ending: nature wakes up
        };

        public static int LineCount(int scene) => Scenes[scene].Length;
        public static Speaker SpeakerOf(int scene, int line) => Scenes[scene][line];
        public static string Line(int scene, int line) => Loc.T($"story.{scene}.{line}");

        /// <summary>A story line just came up on screen (scene, line): the ending's stage listens for its last line.</summary>
        public static event System.Action<int, int> LineShown;

        public static void NotifyLine(int scene, int line) => LineShown?.Invoke(scene, line);

        /// <summary>The big world (0-7) a floor belongs to.</summary>
        public static int ChapterOf(int floor)
        {
            int chapter = 0;
            for (int i = 0; i < ChapterStarts.Length; i++)
                if (floor >= ChapterStarts[i]) chapter = i;
            return chapter;
        }

        /// <summary>True for the floors that open a big world.</summary>
        public static bool OpensChapter(int floor) => System.Array.IndexOf(ChapterStarts, floor) >= 0;

        // A new story: scenes seen in the old one (the prison tower) play again.
        private const string StoryKey = "sb_story2_";

        public static bool Seen(int scene) => PlayerPrefs.GetInt(StoryKey + scene, 0) == 1;

        public static void MarkSeen(int scene)
        {
            PlayerPrefs.SetInt(StoryKey + scene, 1);
            PlayerPrefs.Save();
        }

        /// <summary>A random vanG taunt for the result card after a loss.</summary>
        public static string LoseQuip() => Loc.T("warden.lose." + Random.Range(0, LoseQuips));
    }
}
