using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The journey: the whole game as one way on. Each floor is reached by a road (TrailMap: a shape of its own, one
    /// chest of allies guarded by robots, spitters and a vanG) ending at a staircase straight onto the floor, which is
    /// played as it always was — with the allies fighting alongside. A floor won opens its way out onto the next road.
    /// After a world's tenth floor vanG's tunnel follows, then a short road to the world's battle arena; the arena won,
    /// a real door opens in its wall, and through it (a white flash) lies the next world's first road — the allies stay
    /// behind, the next world has its own. Dying restarts the part foi was on (the floor from its stairs, a road from its
    /// start) and loses the allies won in it; only dying on a floor costs a life. Where foi is is kept between sessions.
    /// </summary>
    public partial class GameManager
    {
        private enum JPhase { Road = 0, Level = 1, GateRoad = 2, Arena = 3 }

        /// <summary>This run is part of the journey (a road, a floor, the gate road or the arena).</summary>
        private bool journey;
        /// <summary>The floor just won leads out onto the next road (not into the tunnel).</summary>
        private bool journeyExit;
        /// <summary>The tunnel after a world's last floor leads on to the gate road (not to a result card).</summary>
        private bool journeyTunnel;
        /// <summary>The level the player tried to start with no lives left was the journey's.</summary>
        private bool pendingJourney;

        private const string JLevelKey = "sb_j_level", JPhaseKey = "sb_j_phase", JSegAlliesKey = "sb_j_seg";

        /// <summary>The floor the journey is at (old saves pick up at the newest floor open).</summary>
        private int JourneyLevel => Mathf.Clamp(PlayerPrefs.GetInt(JLevelKey, SaveData.UnlockedLevel), 0, LevelCount - 1);
        private JPhase JourneyPhase => (JPhase)PlayerPrefs.GetInt(JPhaseKey, 0);

        private void SaveJourney(int level, JPhase phase)
        {
            PlayerPrefs.SetInt(JLevelKey, Mathf.Clamp(level, 0, LevelCount - 1));
            PlayerPrefs.SetInt(JPhaseKey, (int)phase);
            PlayerPrefs.Save();
        }

        /// <summary>A part of the journey begins: the allies brought into it are what a death there falls back to.</summary>
        private void BeginSegment() => PlayerPrefs.SetInt(JSegAlliesKey, AlliesKept);

        /// <summary>Died in a part: the allies won in it are lost, those brought into it stay.</summary>
        private void SegmentLost() => KeepAllies(Mathf.Min(AlliesKept, PlayerPrefs.GetInt(JSegAlliesKey, 0)));

        /// <summary>PLAY: on from where the journey stands.</summary>
        private void ContinueJourney()
        {
            ui.Arenas.Hide();
            int at = JourneyLevel;
            switch (JourneyPhase)
            {
                case JPhase.Level: StartJourneyLevel(at); break;
                case JPhase.GateRoad:
                case JPhase.Arena: StartJourneyRoad(at, toGate: true); break; // the arena is fought again from its road
                default: StartJourneyRoad(at, toGate: false); break;
            }
        }

        /// <summary>
        /// A road of the journey: to floor <paramref name="lvl"/> (its stairs at the end), or, after a world's last floor
        /// <paramref name="lvl"/>, the short road to that world's arena gate.
        /// </summary>
        private void StartJourneyRoad(int lvl, bool toGate)
        {
            journey = true;
            trailRunMode = false;
            journeyExit = journeyTunnel = false;
            SaveJourney(lvl, toGate ? JPhase.GateRoad : JPhase.Road);
            BeginSegment();
            int world = LevelCatalog.WorldOf(lvl);
            bonusRun = true; // a road is never lost for good: no life, the result card offers it again
            dailyRun = false;
            arenaRun = true;
            trailRun = true;
            trailEntering = false;
            trailEnd = toGate ? TrailEnd.Gate : TrailEnd.Stairs;
            trailNextLevel = lvl;
            trailTarget = Mathf.Clamp(world, 0, BattleArenas.Count - 1);
            arenaIndex = trailTarget;
            arenaBestStreak = 0;
            arenaGiftsPhase = arenaGiftsDone = false;
            levelIndex = lvl;
            var shape = toGate ? TrailMap.Shape.Gentle : TrailMap.ShapeFor(lvl);
            int size = toGate ? 3 : 4 + (lvl % LevelCatalog.LevelsPerWorld) / 3 + world / 6;
            var layout = TrailMap.Layout(9100 + lvl * 131 + (toGate ? 7 : 0), shape, size, toGate ? 0 : 1, out var bounds);
            level = BuildTrailLevel(trailTarget, layout, bounds);
            BeginRun();
            EnterTps();
            SetupTrail();
            SpawnKeptAllies();
            ui.ShowIntro(toGate ? Loc.F("trail.title", Loc.T(BattleArenas.NameKey(trailTarget))) : Loc.F("journey.toLevel", lvl + 1),
                toGate ? Loc.T("journey.toArenaText") : Loc.T("journey.roadText"));
            AudioManager.PlaySfx(Sfx.Win, 0.5f, 1.1f);
            RefreshHud();
            ui.SkillBar.Refresh(true);
        }

        /// <summary>Up the stairs: floor <paramref name="lvl"/> as it always was, straight away (no card, no fly-over), allies alongside.</summary>
        private void StartJourneyLevel(int lvl)
        {
            if (Lives.Count <= 0)
            {
                pendingLevel = lvl;
                pendingJourney = true;
                ExitTps();
                ShowMenu();
                ui.ShowNoLives();
                return;
            }
            journey = true;
            journeyExit = journeyTunnel = false;
            SaveJourney(lvl, JPhase.Level);
            BeginSegment();
            trailRun = false;
            bonusRun = false;
            arenaRun = false;
            dailyRun = false;
            levelIndex = Mathf.Clamp(lvl, 0, LevelCount - 1);
            level = levelSet.levels[levelIndex].Clone();
            ApplyAssist(level);
            ApplyCurve(level, AlliesKept);
            BeginRun();
            SpawnKeptAllies();
            ui.ShowIntro(Loc.F("level", levelIndex + 1), MissionText(level, upper: true));
            AudioManager.PlaySfx(Sfx.Win, 0.6f, 1.2f);
            RefreshHud();
            ui.SkillBar.Refresh(true);
        }

        /// <summary>
        /// The floors grow harder slowly: the first two worlds as they are, from the third on every world adds about a
        /// tenth to the robots' health and numbers (and vanG's), the last two floors of a world a little more, and a floor
        /// faced with allies brings a few more robots to meet them.
        /// </summary>
        private void ApplyCurve(LevelData l, int allies)
        {
            int w = LevelCatalog.WorldOf(levelIndex);
            if (allies > 0) l.robots += allies / 2;
            if (w < 2) return;
            float k = (w - 1) * 0.09f;
            l.robotHp = Mathf.CeilToInt(l.robotHp * (1f + k));
            l.robots += Mathf.RoundToInt(l.robots * k * 0.6f);
            l.elites += Mathf.RoundToInt(l.elites * k * 0.5f);
            l.monsterHp = Mathf.CeilToInt(l.monsterHp * (1f + k * 0.8f));
            if (levelIndex % LevelCatalog.LevelsPerWorld >= LevelCatalog.LevelsPerWorld - 2)
            {
                l.robotHp += 1;
                l.robots += 2 + w / 4;
            }
        }

        /// <summary>A journey floor won: banked at once; out onto the next road, or (a world's last floor) into the tunnel.</summary>
        private void JourneyLevelWon(int stars)
        {
            CommitWin(stars, Earned);
            bool last = level.final || levelIndex % LevelCatalog.LevelsPerWorld == LevelCatalog.LevelsPerWorld - 1;
            if (last)
            {
                SaveJourney(levelIndex, JPhase.GateRoad);
                journeyTunnel = true;
            }
            else
            {
                SaveJourney(levelIndex + 1, JPhase.Road);
                journeyExit = true;
            }
            StartCoroutine(OpenRoadSoon());
        }

        /// <summary>Out of a won floor onto the next road (instead of the tunnel).</summary>
        private void JourneyOut()
        {
            journeyExit = false;
            roadPhase = RoadPhase.None;
            if (roadBeacon != null) Destroy(roadBeacon);
            ClearRoadGuide();
            StartJourneyRoad(levelIndex + 1, toGate: false);
        }

        /// <summary>The arena won: its door to the next world swung open; through it with a white flash onto the next road.</summary>
        private void JourneyThroughDoor()
        {
            ui.Flash(Color.white, 0.6f);
            AudioManager.PlaySfx(Sfx.Win, 1f, 1.4f);
            KeepAllies(0); // the allies stay behind: the next world has its own
            int next = (arenaIndex + 1) * LevelCatalog.LevelsPerWorld;
            if (next >= LevelCount)
            {
                ExitTps();
                ShowBonusResult(Loc.T("trail.allDone"));
                return;
            }
            SaveJourney(next, JPhase.Road);
            StartJourneyRoad(next, toGate: false);
        }
    }
}
