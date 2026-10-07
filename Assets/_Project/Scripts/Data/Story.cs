using UnityEngine;

namespace SquashBot.Data
{
    public enum Speaker
    {
        Narrator,
        /// <summary>Cell's partner: a tiny maintenance robot living in its backpack, who remembers the colours.</summary>
        Bip,
        /// <summary>vanG, the AI that runs the universe's server, built Cell and builds every cell it must escape.</summary>
        VanG,
        /// <summary>Kuzgun, the masked thief: CL-1, Cell's older brother.</summary>
        Thief,
        /// <summary>Lumi, the old painter spirit of the Galaxy Painting.</summary>
        Lumi
    }

    /// <summary>
    /// The story of the scenario document: Cell (CL-7), the little painter robot vanG built, finds a colour inside
    /// itself and escapes vanG's test cells with Bip, frees Lumi piece by piece, meets its brother Kuzgun (CL-1) and
    /// learns it is vanG's lost heart. Cell never speaks; Bip, vanG, Kuzgun, Lumi and a narrator do. Scene f plays
    /// before floor f (every 10 levels); <see cref="Ending"/> plays after the last level. Lines live in Loc as
    /// "story.{scene}.{line}", scene titles as "story.title.{scene}"; every level also has its own card texts
    /// ("lvl.{n}.vang", ".help", ".goal", ".hook").
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

        private const Speaker N = Speaker.Narrator, B = Speaker.Bip, V = Speaker.VanG, T = Speaker.Thief, L = Speaker.Lumi;

        // One row per floor (the floor's opening, after the previous floor's closing lines), then the ending.
        // Generated from the scenario document with its texts (story.{scene}.{line}, story.title.{scene}).
        private static readonly Speaker[][] Scenes =
        {
            new[] { N, N, V, N, B },
            new[] { N, V, N, N, V, N },
            new[] { N, N, N },
            new[] { N, L, N, N, N, B, N },
            new[] { N, V, N, B, N, N, B },
            new[] { N, V, N, N, N, N },
            new[] { N, N, N, N, N },
            new[] { N, N, N, N },
            new[] { N, N, N, N, N },
            new[] { N, N, N, N },
            new[] { N, N, N, N, V, N, V },
            new[] { N, N, N, N, N, N, B, N },
            new[] { N, N, N, B, N },
            new[] { N, N, V, N },
            new[] { N, N, N, N, L, N, V },
            new[] { N, V, N, N, N, N },
            new[] { N, N, V, N, B, N },
            new[] { N, L, N, N, N },
            new[] { N, B, N, N },
            new[] { N, N, V, N },
            new[] { N, V, N, N, N },
            new[] { N, N, T, N },
            new[] { N, N, N, B, N },
            new[] { N, N, N, V, N },
            new[] { N, N, N, V, N },
            new[] { N, N, V, N, N, N, V, N, N, N },
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
