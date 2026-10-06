using UnityEngine;

namespace SquashBot.Data
{
    public enum Speaker
    {
        Narrator,
        Robot,
        Warden
    }

    /// <summary>
    /// The story: robot number 47 climbs out of the Escape Cell prison floor by floor (one world = one floor),
    /// teased all the way by WARDEN, the prison's grumpy AI. Scene 0 opens the game, scene w plays before world w,
    /// <see cref="Ending"/> plays after the last level. Lines live in Loc as "story.{scene}.{line}".
    /// </summary>
    public static class Story
    {
        public const int Ending = LevelCatalog.WorldCount;
        public const int LoseQuips = 8;

        private const Speaker N = Speaker.Narrator, R = Speaker.Robot, W = Speaker.Warden;

        private static readonly Speaker[][] Scenes =
        {
            new[] { N, W, R, W }, // 0: arrival
            new[] { W, R, W },    // 1: bombs
            new[] { W, R, W },    // 2: fire and block lines
            new[] { R, W, R },    // 3: jet boosters (hover)
            new[] { W, R },
            new[] { W, R },
            new[] { W, R, W },
            new[] { R, W },       // 7: the map piece
            new[] { W, R },
            new[] { W, R, W },    // 9: the sky
            new[] { W, R },
            new[] { W, R },
            new[] { W, R },
            new[] { W, R },
            new[] { W, R },       // 14: the last floor
            new[] { N, R, W, N }, // ending: the roof
        };

        public static int LineCount(int scene) => Scenes[scene].Length;
        public static Speaker SpeakerOf(int scene, int line) => Scenes[scene][line];
        public static string Line(int scene, int line) => Loc.T($"story.{scene}.{line}");

        public static bool Seen(int scene) => PlayerPrefs.GetInt("sb_story_" + scene, 0) == 1;

        public static void MarkSeen(int scene)
        {
            PlayerPrefs.SetInt("sb_story_" + scene, 1);
            PlayerPrefs.Save();
        }

        /// <summary>A random WARDEN taunt for the result card after a loss.</summary>
        public static string LoseQuip() => Loc.T("warden.lose." + Random.Range(0, LoseQuips));
    }
}
