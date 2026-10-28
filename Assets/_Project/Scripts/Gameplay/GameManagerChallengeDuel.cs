using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The challenge's Duel: foi against the rival foi for 3, 5 or 10 minutes, each with its squad behind it — up to two
    /// soldiers of every group, taken from the army's pool (a soldier who falls is away for an hour; one lost in this duel
    /// stays lost for the rest of it). The six tags at the bottom send a group at the rival foi (tap again: back to
    /// guard); left alone the soldiers guard their foi. Each side has three hearts (a third of its health back) and three
    /// shields (three seconds, two blows) for the whole duel. Whoever puts the other foi down scores a point and both
    /// start again from their marks; when time is up the higher score wins (level: a draw). A foi's blow takes 1% of the
    /// other's health per point of its weapon's damage (armour takes some off). The duel is fought on one of six stages.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The duel's length in minutes (3, 5 or 10).</summary>
        private int chDuelMinutes = 3;

        /// <summary>This duel has squads (the player's choice; in a live match, the match's).</summary>
        private bool chDuelSquads = false;

        /// <summary>Knock-downs scored by each side, and the time left (seconds).</summary>
        private int chDuelMe, chDuelThem;
        private float chDuelLeft;
        private NameTag myTag, theirTag;

        /// <summary>Hearts and shields left this duel: the player's and the rival's.</summary>
        private int duelHearts, duelShields, rivalHearts, rivalShields;
        private float rivalShieldUntil, rivalPowerUpAt;
        // (the duel table's: the same as the server's)
        private static int DuelHearts => DuelBalanceTable.Current.hearts;
        private static int DuelShields => DuelBalanceTable.Current.shields;
        private static float DuelHeartHeal => DuelBalanceTable.Current.heartHeal;
        private static float DuelShieldSeconds => DuelBalanceTable.Current.shieldSeconds;
        private static int DuelShieldHits => DuelBalanceTable.Current.shieldHits;

        // ---------- The six stages ----------

        /// <summary>
        /// A duel stage: its name, the world whose look it takes, the floor's shape, what crowns its low walls (none,
        /// flames, ice, glow) and whether the colosseum's stands rise round it instead.
        /// </summary>
        private struct DuelStage
        {
            public string key;
            public int world;
            public BattleArenas.Shape shape;
            public int fx;
            public bool colosseum;
            /// <summary>The backdrop across the floor, facing the camera.</summary>
            public DuelStands.Theme theme;
            public Color wall, fxColour;
        }

        private static readonly DuelStage[] DuelStages =
        {
            new DuelStage { key = "duel.stage.6", world = 23, shape = BattleArenas.Shape.Round, fx = 0, colosseum = true, theme = DuelStands.Theme.Grove, wall = new Color(0.3f, 0.4f, 0.45f), fxColour = new Color(0.35f, 0.85f, 1f) },  // the blue grove
            new DuelStage { key = "duel.stage.7", world = 13, shape = BattleArenas.Shape.Round, fx = 0, colosseum = true, theme = DuelStands.Theme.Frost, wall = new Color(0.8f, 0.9f, 1f), fxColour = new Color(0.55f, 0.9f, 1f) },     // the glacier peak
            new DuelStage { key = "duel.stage.8", world = 0, shape = BattleArenas.Shape.Round, fx = 0, colosseum = true, theme = DuelStands.Theme.Mars, wall = new Color(0.6f, 0.3f, 0.2f), fxColour = new Color(1f, 0.6f, 0.25f) },        // the red planet
            new DuelStage { key = "duel.stage.9", world = 5, shape = BattleArenas.Shape.Round, fx = 0, colosseum = true, theme = DuelStands.Theme.Cappadocia, wall = new Color(0.86f, 0.7f, 0.52f), fxColour = new Color(1f, 0.82f, 0.45f) }, // Cappadocia
            new DuelStage { key = "duel.stage.10", world = 9, shape = BattleArenas.Shape.Round, fx = 0, colosseum = true, theme = DuelStands.Theme.Asia, wall = new Color(0.7f, 0.3f, 0.25f), fxColour = new Color(1f, 0.8f, 0.4f) },      // the temple of blossoms
            new DuelStage { key = "duel.stage.11", world = 2, shape = BattleArenas.Shape.Round, fx = 0, colosseum = true, theme = DuelStands.Theme.Europe, wall = new Color(0.6f, 0.6f, 0.6f), fxColour = new Color(1f, 0.82f, 0.4f) },   // the knights' court
            new DuelStage { key = "duel.stage.12", world = 5, shape = BattleArenas.Shape.Round, fx = 0, colosseum = true, theme = DuelStands.Theme.Arabia, wall = new Color(0.86f, 0.68f, 0.48f), fxColour = new Color(0.4f, 0.9f, 1f) },  // the desert palace
            new DuelStage { key = "duel.stage.13", world = 2, shape = BattleArenas.Shape.Round, fx = 0, colosseum = true, theme = DuelStands.Theme.Istanbul, wall = new Color(0.5f, 0.5f, 0.5f), fxColour = new Color(1f, 0.82f, 0.5f) },  // Galata
            new DuelStage { key = "duel.stage.14", world = 2, shape = BattleArenas.Shape.Round, fx = 0, colosseum = true, theme = DuelStands.Theme.Bosphorus, wall = new Color(0.8f, 0.77f, 0.7f), fxColour = new Color(1f, 0.9f, 0.7f) },  // the Maiden's Tower
            new DuelStage { key = "duel.stage.15", world = 2, shape = BattleArenas.Shape.Round, fx = 0, colosseum = true, theme = DuelStands.Theme.London, wall = new Color(0.32f, 0.5f, 0.22f), fxColour = new Color(1f, 0.9f, 0.7f) },  // Big Ben
        };

        /// <summary>The stage wheel before the duel: every stage flashes by and it stops on <paramref name="stage"/>.</summary>
        private System.Collections.IEnumerator SpinToStage(int stage)
        {
            var names = new string[DuelStages.Length];
            var pics = new Texture[DuelStages.Length];
            for (int i = 0; i < DuelStages.Length; i++)
            {
                names[i] = Loc.T(DuelStages[i].key);
                pics[i] = Resources.Load<Texture2D>("DuelStages/stage_" + i);
            }
            yield return chUi.StageWheel(names, pics, stage);
        }

        /// <summary>The stages a match is drawn from (all of them: only the made stages are left).</summary>
        private static int DrawnStages => DuelStages.Length;

        /// <summary>The stage being fought on (an index into the list).</summary>
        private int duelStage;
        private DuelStage Stage => DuelStages[Mathf.Clamp(duelStage, 0, DuelStages.Length - 1)];
        public static int DuelStageCount => DrawnStages;

        /// <summary>Test builds: the duel stage picked in the settings (-1: drawn as usual). Never a live match's (both
        /// screens draw the same one).</summary>
        public static int TestStage
        {
#if REAL_ADS || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && !TEST_TOOLS)
            get => -1; // (a store build: always drawn; a tester's old pick never sticks)
#else
            get { int s = Prefs.GetInt("sb_test_stage", -1); return s >= 0 && s < DuelStages.Length ? s : -1; }
#endif
            set { Prefs.SetInt("sb_test_stage", value); Prefs.Save(); }
        }

        /// <summary>The picked test stage's name for the settings (or "random").</summary>
        public static string TestStageLabel => TestStage < 0 ? Loc.T("settings.stageRandom") : Loc.T(DuelStages[TestStage].key);

        /// <summary>The stage a duel is fought on: the test pick, if one is set, else <paramref name="drawn"/>.</summary>
        private static int PickedStage(int drawn) => TestStage >= 0 ? TestStage : drawn;

        /// <summary>The marks the two foi start from: either side of the middle, the player's to the south (left on screen).</summary>
        private (GridPos mine, GridPos theirs) DuelMarks
        {
            get
            {
                // A live match: the same arena on both screens — the server's side 0 to the south (left), side 1 to the north.
                GridPos south = new GridPos(BoardW / 2, BoardH / 2 - 3), north = new GridPos(BoardW / 2, BoardH / 2 + 3);
                return chLive && liveSide == 1 ? (north, south) : (south, north);
            }
        }

        /// <summary>Both sides' duel strength this match (under their league ceilings, or equal power): set as the foi are placed.</summary>
        private DuelPower.Stats duelMine, duelTheirs;

        /// <summary>A ranked duel (each side under its own ceiling); a friend's match is equal power.</summary>
        private bool DuelRanked => !(chLive && !liveRanked) && !copyMatch && !EventMatch;

        /// <summary>The player's weapon mastery in its blow (the bot duels; the live server does not know mastery yet).</summary>
        private float MyMasteryTimes(WeaponDef w) => LiveActive ? 1f : DuelBalanceTable.Current.MasteryTimes(Mastery.Level(w), DuelPower.TierOf(Challenge.Rank), !DuelRanked);

        /// <summary>The player's blow: the duel's (its weapon's tier under the ceiling, the arms' share), the floors' otherwise.</summary>
        private int MyBlow(WeaponDef w) => challengeRun
            ? Mathf.Max(1, Mathf.RoundToInt(DuelPower.Blow(w, duelMine.weaponTier) * (1f + duelMine.damage) * MyMasteryTimes(w)))
            : Challenge.RankBoost(Armory.Damage(w));

        /// <summary>The Duel's board: the stage, the two foi on their marks with their squads, the clock.</summary>
        /// <summary>The lane duel's line: the board column both foi stand on (the screen's left-right runs along it).</summary>
        private int laneX;

        /// <summary>The lane duel is on now: the setting, a one-on-one duel against the AI (not 2v2, not live: the server
        /// keeps a live duel's places).</summary>
        private bool LaneOn => SaveData.LaneDuel && challengeRun && !LiveActive;

        private void BuildDuelBoard(int stage)
        {
            duelStage = Mathf.Clamp(stage, 0, DuelStages.Length - 1);
            ResetPhysical(); // (nobody down, in the air or crouched)
            levelTps = false; // (the duel is always seen from the board's camera)
            zoneRadius = 99f; zoneDrawn = -1; zoneHurtMe = zoneHurtFoe = 0f; zoneAnnounced = false;
            hunt.Unsafe = null;
            BuildCourtyard();
            chDuelMe = chDuelThem = 0;
            ResetOvertime();
            chDuelLeft = chDuelMinutes * 60f;
            duelHearts = rivalHearts = DuelHearts;
            lastBeat = -1;
            BeginMatchProgress(rival != null ? (rival.seed != 0 ? rival.seed : rival.name.GetHashCode()) : 0);
            TuneBot();
            duelShields = rivalShields = DuelShields;
            rivalShieldUntil = 0f;
            rivalPowerUpAt = Time.time + 20f;
            if (robot.Visual != null) robot.Visual.gameObject.SetActive(true);
            chUi.ShowBattle();
            BeginDuelSkills();
            LoadMoveSets();
            ApplyDuelRule(); // (the weekly mode's rule)
            ui.SetHudTitle(Loc.T("ch.mode.duel") + " · " + Loc.T(Stage.key));
            PlaceDuelists(first: true);
            RefreshSquadBar();
            hunt.Freeze(); // nobody moves while it counts down
            chPhase = ChPhase.Lobby; // the count first
            cameraRig.StartCoroutine(DuelOpening());
            AudioManager.PlaySfx(Sfx.Win, 0.6f, 0.9f);
        }

        private void BeginDuel()
        {
            if (!challengeRun || chPhase == ChPhase.Done || chPhase == ChPhase.Duel) return;
            hunt.DelayBotOrders(10f + Random.value * 8f);
            chPhase = ChPhase.Duel;
            duelPan = robot.transform.position - boardCentre; duelPan.y = 0f;
            hunt.Resume();
            FirstSession.DuelStarted(); // (time_to_first_duel)
            chUi.SetStatus(Loc.T("ch.duel") + "  ·  " + GoalText(matchGoal)); // the match's small challenge
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 0.8f);
            RefreshDuelClock();
        }

        /// <summary>
        /// Both foi on their marks, full health, a fresh rival foi (its look, weapon and health from its profile). The
        /// first time the squads come out of the pool; after a knock-down the ones left stand back in their places.
        /// </summary>
        private void PlaceDuelists(bool first)
        {
            danceAt = null; danceCam = 0f;
            hunt.RemoveChallengeFoe();
            health = 1f;
            Shields.Ready(); // (each round: the shield ready)
            RefreshHealthBar();
            RebootShown(first);
            var (mine, theirs) = DuelMarks;
            laneX = mine.x;
            theirs = new GridPos(laneX, theirs.y); // (the lane duel: both on the one line; the marks already share it)
            robot.Spawn(grid, mine);
            bool north = theirs.y < mine.y;
            robot.transform.rotation = Quaternion.identity;
            robot.Visual.rotation = Quaternion.Euler(0f, north ? 180f : 0f, 0f); // facing the rival across the middle
            var look = RivalLook();
            look.position = GridView.ToWorld(theirs) + Vector3.up * GridView.SurfaceY;
            look.rotation = Quaternion.Euler(0f, north ? 0f : 180f, 0f);
            hunt.FoeRemote = LiveActive; // a live rival: its own game moves it, the server keeps its health
            var w = Armory.Get(rival.weapon);
            hunt.FoeBoldness = rival.boldness;
            hunt.FoeSkill = Mathf.Clamp01(0.55f + 0.035f * rival.Rank + 0.2f * rival.boldness); // (every rival fights back; higher ranks parry sooner)
            hunt.ChallengeBattle = true;
            // Both sides' strength: each under its own rank's ceiling in a ranked duel, both at the third tier's in a
            // friend's match (as the server counts it). The duel's health is twice the body's.
            bool ranked = DuelRanked;
            duelMine = DuelPower.Mine(ranked, Armory.Equipped);
            duelTheirs = DuelPower.For(rival.Body, rival.Arms, rival.Shell, w.tier, ranked ? DuelPower.TierOf(rival.Rank) : DuelPower.EqualTier, !ranked);
            hunt.FoeSoldierDamage = DuelPower.Blow(w, duelTheirs.weaponTier);
            hunt.FoiHp = duelMine.hp;
            // Its health points behind its shell (live: a share of 100, the server keeps the points).
            int hp = LiveActive ? 100 : Mathf.RoundToInt(duelTheirs.hp / Mathf.Max(0.5f, 1f - duelTheirs.armor) * (botTuning != null ? botTuning.hpScale : 1f));
            // Its blow: its weapon's (tier under its ceiling) with its arms' share, less foi's shell, over foi's health.
            float myArmor = rivalPassive == PassiveKind.Piercer ? duelMine.armor * 0.75f : duelMine.armor; // (its piercer: a quarter of foi's armour ignored)
            float share = DuelPower.Blow(w, duelTheirs.weaponTier) * (1f + duelTheirs.damage) * (1f - myArmor) / Mathf.Max(1, duelMine.hp)
                * DuelBalanceTable.Current.MasteryTimes(rival.mastery, DuelPower.TierOf(rival.Rank), !ranked); // (its weapon's mastery too, as foi's)
            hunt.AddChallengeFoe(theirs, hp, share, look, rivalHeld, 0);
            // both bodies back to standing at rest: nothing of the round before (a crouch, a guard, a fall) stays on them
            ResetPhysical();
            FoiPoseReset.CleanAll(robot.Visual, hunt.ChallengeFoeModel); // (the pose reset: every round and the match's start)
            var mySkin = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            var foeSkin = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
            SkinnedHero.PairForDuel(mySkin, foeSkin); // (the two bodies kept out of each other)
            { var lo = GridView.ToWorld(new GridPos(0, 0)); var hi = GridView.ToWorld(new GridPos(BoardW - 1, BoardH - 1)); SkinnedHero.DuelArea = Rect.MinMaxRect(Mathf.Min(lo.x, hi.x) - 0.5f, Mathf.Min(lo.z, hi.z) - 0.5f, Mathf.Max(lo.x, hi.x) + 0.5f, Mathf.Max(lo.z, hi.z) + 0.5f); } // (the platform: none of them off it)
            // each one's weapon mastery: how rich its plain chain looks (the player's own, the rival's card's)
            if (heldWeapon != null) heldWeapon.MasteryLevel = Mastery.Level(Armory.Equipped);
            var foeHeld = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<HeldWeapon>() : null;
            if (foeHeld != null) foeHeld.MasteryLevel = rival != null ? rival.mastery : 0;
            hunt.LaneX = LaneOn ? laneX : (int?)null; // (the lane duel: the rival keeps to the line)
            // (no name tags over the two foi: their names and ranks are over the health bars)
            if (myTag != null) { Destroy(myTag.gameObject); myTag = null; }
            if (theirTag != null) { Destroy(theirTag.gameObject); theirTag = null; }
            int back = north ? 1 : -1; // behind each foi, away from the other
            if (first)
            {
                hunt.ClearSquads();
                hunt.ResetRemoteSquad();
                int myForm = Challenge.DuelFormation, theirForm = Mathf.Abs(rival.name.GetHashCode()) % 3;
                for (int g = 0; g < Army.Groups && chDuelSquads; g++) // foi against foi: no squads at all
                {
                    int n = SoldierPool.ForDuel(g);
                    for (int s = 0; s < n; s++) hunt.AddSquadSoldier(true, g * SoldierPool.PerDuel + s, mine + FormationSpot(myForm, g, s, back), Army.StatsOf(g));
                    if (LiveActive) continue; // the live rival's soldiers come with its state
                    for (int s = 0; s < rival.squad[g]; s++) hunt.AddSquadSoldier(false, g * SoldierPool.PerDuel + s, theirs + FormationSpot(theirForm, g, s, -back), Army.StatsFromTotal(g, rival.points[g]));
                }
                // Every group charges from the start (a tag tapped calls its group back to guard, tapped again: back in).
                // (A guarding squad stays by its foi until a tag sends it in.)
                for (int g = 0; g < Army.Groups; g++) { hunt.SetSquadOrder(true, g, myForm != 1); hunt.SetSquadOrder(false, g, theirForm != 1); }
            }
            // After a knock-down the soldiers left line up behind their foi again (no new ones and no healing until the duel is over).
            else hunt.RegroupSquads();
            RoundPassives(); // (each round: the master warrior's bar part, the sturdy's spare life)
            GuardRound(); // (each round: both shields whole)

            RefreshSquadBar();
        }

        /// <summary>
        /// Where soldier <paramref name="s"/> of group <paramref name="g"/> starts, from its foi (<paramref name="back"/>: the
        /// way away from the rival): attack, a row in front; guard, a ring round foi; flanks, two wings at its sides.
        /// </summary>
        private static GridPos FormationSpot(int form, int g, int s, int back)
        {
            int i = g * 2 + s; // 0..11
            switch (form)
            {
                case 1:
                {
                    var ring = new[] { new GridPos(-1, 0), new GridPos(1, 0), new GridPos(0, back), new GridPos(0, -back), new GridPos(-1, back), new GridPos(1, back),
                                       new GridPos(-1, -back), new GridPos(1, -back), new GridPos(-2, 0), new GridPos(2, 0), new GridPos(-2, back), new GridPos(2, back) };
                    return ring[i % ring.Length];
                }
                case 2:
                {
                    int side = i % 2 == 0 ? -1 : 1, k = i / 2;
                    return new GridPos(side * (3 + k % 3), -back * (k / 3));
                }
                default:
                    return new GridPos((g - 3) * 2 + s, -back);
            }
        }

        /// <summary>A knock-down: the one left standing scores, both start again from the middle.</summary>
        private void DuelKnockdown(bool iWon)
        {
            if (!challengeRun || chPhase != ChPhase.Duel) return;
            if (iWon) chDuelMe++; else chDuelThem++;
            if (!iWon) { var koSkin = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null; if (koSkin != null) koSkin.PlayAction(Combat.DuelAction.Knockout); } // (foi down: its knockout clip, held lying until the next round)
            if (!iWon && mcHearts == 0 && duelHearts > 0) stats.mine.downWithHeartsUnused++; // (the tip: hearts left unused)
            PowerSlow(0.4f, 0.3f); // (the knock-down: slow motion a moment, the crowd up)
            AudioManager.DipMusic(DuelMusicSettings.Current.knockDip); // (the music dips, the crowd to the front)
            KnockoutShown(iWon);
            if (SuddenDeath) { RefreshDuelClock(); chPhase = ChPhase.Break; hunt.Freeze(); ChallengeEnd(); return; } // (sudden death: one knock-down wins)
            if (overtime) { OvertimeKnockdown(iWon); return; } // (the golden knock-down: the first one wins)
            if (iWon) OfferFinish(lastFoeAt);
            if (iWon) skillBar?.ShowTaunt(1.5f); // (the taunt offered a moment)
            cameraRig.StartCoroutine(RivalEmote(!iWon));
            chPhase = ChPhase.Break;
            hunt.Freeze();
            RefreshDuelClock();
            chUi.SetStatus(Loc.T(iWon ? "ch.koYou" : "ch.koThem"));
            if (colosseum != null) colosseum.Cheer(iWon ? 0 : 1, 0.9f);
            AudioManager.PlaySfx(iWon ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
            if (iWon) fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 24, 5f);
            cameraRig.StartCoroutine(AfterKnockdown());
        }

        /// <summary>
        /// The knock-down seen: the pictures in slow motion a moment (the game's clock never), the one down with its eyes
        /// shut and electric arcs crackling over it.
        /// </summary>
        private void KnockoutShown(bool iWon)
        {
            Combat.VisualTime.SlowAll(0.3f, 0.4f);
            Haptics.Pulse(120, 1f);
            var down = iWon ? hunt.ChallengeFoeModel : robot.Visual;
            var skin = down != null ? down.GetComponentInChildren<SkinnedHero>() : null;
            if (skin == null) return;
            FaceFor(down, FaceMoment.KnockedOut); // (spiral eyes, the tongue out; stars over the head)
            HatFor(down, 3, (iWon ? robot.Visual : hunt.ChallengeFoeModel) is Transform kb && kb != null ? kb.position : down.position - down.forward);
            Combat.StatusFx.On(Combat.CombatFeel.RootOf(down)).Stun(2.5f);
            skin.Squash(1f);
            var c = skin.ChestPoint;
            float s = Mathf.Max(0.3f, down.lossyScale.y) * 0.5f;
            for (int i = 0; i < 3; i++) MoveFx.Bolt(c + Random.insideUnitSphere * s, c + Random.insideUnitSphere * s, new Color(0.6f, 0.85f, 1f), 0.05f, 0.35f);
            Combat.ScrapBits.Spawn(c, down.position - robot.transform.position, 3, Mathf.Max(0.3f, down.lossyScale.y), down.position.y);
        }

        /// <summary>Back on its mark after a knock-down: foi's eyes open again with a glow (starting up anew).</summary>
        private void RebootShown(bool first)
        {
            FacesClear();
            HatsBack();
            var skin = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null;
            if (skin == null) return;
            skin.EyesShut(0f);
            if (first) return;
            Combat.StatusFx.On(Combat.CombatFeel.RootOf(robot.Visual)).Flash(0.18f);
            fx.Burst(skin.HeadPoint, new Color(0.6f, 0.9f, 1f), new Color(1.5f, 2.4f, 3f), 14, 2.5f);
        }

        /// <summary>A live knock-down, as the server tells it: its score; both start again when it says.</summary>
        private void LiveKnockdown(bool iWon, int mine, int theirs)
        {
            if (!challengeRun || (chPhase != ChPhase.Duel && chPhase != ChPhase.Wait)) return;
            if (!iWon) { var koSkin = robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null; if (koSkin != null) koSkin.PlayAction(Combat.DuelAction.Knockout); } // (foi down: its knockout clip)
            chDuelMe = mine;
            AudioManager.DipMusic(DuelMusicSettings.Current.knockDip);
            chDuelThem = theirs;
            chPhase = ChPhase.Break;
            hunt.Freeze();
            if (iWon) { OfferFinish(hunt.ChallengeFoeAt ?? lastFoeAt); hunt.RemoveChallengeFoe(); } else { health = 0f; RefreshHealthBar(); robot.Squash(); }
            RefreshDuelClock();
            chUi.SetStatus(Loc.T(iWon ? "ch.koYou" : "ch.koThem"));
            if (colosseum != null) colosseum.Cheer(iWon ? 0 : 1, 0.9f);
            AudioManager.PlaySfx(iWon ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
            if (iWon) fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 24, 5f);
        }

        /// <summary>The server's word to start (again): both on their marks, the clock as it says.</summary>
        private void LiveDuelGo(float secs)
        {
            if (!challengeRun) return;
            chDuelLeft = secs;
            PlaceDuelists(first: false);
            duelPan = robot.transform.position - boardCentre; duelPan.y = 0f;
            RefreshDuelClock();
            // 3, 2, 1: the server holds the blows and its clock for the same 2.4 s (Rules.DuelCountdown).
            cameraRig.StartCoroutine(CountdownThenGo());
        }

        /// <summary>
        /// The duel's opening, straight on the board: 5, 4, 3, 2, 1 over the two foi on their marks, then the fight.
        /// </summary>
        private System.Collections.IEnumerator DuelOpening()
        {
            duelCountdown = true;
            try
            {
                for (int i = 5; i >= 1; i--)
                {
                    if (!challengeRun || chPhase != ChPhase.Lobby) yield break; // (over, or the server started it meanwhile)
                    FaceForCountdown(true);
                    if (i == 3) CrownsShine(); // (the crowns' glint; the Marshal's ring of light)
                    chUi.SetStatus(i.ToString());
                    ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f), i.ToString(), Palette.UiGold, 220f);
                    AudioManager.PlaySfx(Sfx.Click, 1f, 0.75f + 0.08f * (5 - i));
                    yield return new WaitForSeconds(1f);
                    while (challengeRun && State == GameState.Paused) yield return null;
                }
            }
            finally { duelCountdown = false; }
            if (!challengeRun || chPhase != ChPhase.Lobby) yield break;
            FaceForCountdown(false);
            if (LiveActive) LiveWaitFor(LiveWait.Start); else BeginDuel();
        }

        /// <summary>
        /// The countdown: both foi turned to the camera (and a little toward each other), never their backs to it;
        /// <paramref name="on"/> false: face to face again for the fight.
        /// </summary>
        private void FaceForCountdown(bool on)
        {
            var foe = hunt.ChallengeFoeModel;
            if (robot.Visual == null || foe == null) return;
            var cam = Camera.main != null ? Camera.main.transform.position : robot.transform.position + Vector3.right * 10f;
            Vector3 Dir(Vector3 at, Vector3 other)
            {
                var toCam = cam - at; toCam.y = 0f;
                var toOther = other - at; toOther.y = 0f;
                if (toCam.sqrMagnitude < 1e-4f || toOther.sqrMagnitude < 1e-4f) return toOther;
                return on ? Vector3.Slerp(toCam.normalized, toOther.normalized, 0.3f) : toOther.normalized;
            }
            var me = robot.transform.position; var them = foe.position;
            robot.Face(me + Dir(me, them));
            hunt.TurnFoe(Dir(them, me));
        }

        /// <summary>3, 2, 1 over the two foi on their marks (everything still), then the duel goes on.</summary>
        /// <summary>The 3, 2, 1 is on: no moves, no blows.</summary>
        private bool duelCountdown;

        private System.Collections.IEnumerator CountdownThenGo()
        {
            hunt.Freeze();
            chPhase = ChPhase.Break;
            duelCountdown = true;
            try
            {
                for (int i = 3; i >= 1; i--)
                {
                    if (!challengeRun || chPhase == ChPhase.Done) yield break;
                    if (i == 3) CrownsShine();
                    chUi.SetStatus(i.ToString());
                    ui.Float(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f), i.ToString(), Palette.UiGold, 220f);
                    AudioManager.PlaySfx(Sfx.Click, 1f, 0.8f + 0.1f * (3 - i));
                    yield return new WaitForSeconds(0.8f);
                    while (challengeRun && State == GameState.Paused) yield return null;
                }
            }
            finally { duelCountdown = false; }
            if (!challengeRun || chPhase == ChPhase.Done || State != GameState.Playing) yield break;
            hunt.Resume();
            chPhase = ChPhase.Duel;
            chUi.SetStatus(Loc.T("ch.duel"));
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 1.1f);
        }

        private System.Collections.IEnumerator AfterKnockdown()
        {
            yield return new WaitForSeconds(1.2f);
            while (challengeRun && (Time.time < finishUntil || finishing)) yield return null; // the finish offered (or playing) first
            while (challengeRun && State == GameState.Paused) yield return null;
            if (!challengeRun || chPhase != ChPhase.Break || State != GameState.Playing) yield break;
            if (chDuelLeft <= 0f) { if (!TryStartOvertime()) ChallengeEnd(); yield break; }
            PlaceDuelists(first: false);
            yield return CountdownThenGo();
        }

        /// <summary>Each frame of the duel: the clock (it only runs while the two fight), a knock-down, time up.</summary>
        // ---------- The closing ring ----------

        /// <summary>The safe ring's radius (tiles from the board's middle) and the last one drawn.</summary>
        private float zoneRadius = 99f;
        /// <summary>The closing ring is off (the whole floor stays safe to the end).</summary>
        private static readonly bool ClosingRing = false;
        private int zoneDrawn = -1;
        private float zoneHurtMe, zoneHurtFoe;
        private bool zoneAnnounced;

        /// <summary>
        /// So nobody runs round the board for ever: from a quarter of the duel's time the safe ring closes in, down to a
        /// ring a few tiles across just before the end. The tiles outside it glow red, and whoever stands out there loses
        /// health (a live duel's health is the server's: there only the ring shows).
        /// </summary>
        private void UpdateDuelZone(float dt)
        {
            if (!ClosingRing) { zoneRadius = 99f; hunt.Unsafe = null; return; } // (taken off: the user asked)
            float total = Mathf.Max(60f, chDuelMinutes * 60f);
            float elapsed = total - chDuelLeft;
            float k = Mathf.InverseLerp(total * 0.25f, total * 0.9f, elapsed);
            if (k <= 0f) { zoneRadius = 99f; zoneAnnounced = false; hunt.Unsafe = null; return; }
            if (!zoneAnnounced)
            {
                zoneAnnounced = true;
                FloatAt(robot.transform.position + Vector3.up * 1.6f, Loc.T("duel.zone"), UI.UiKit.Style.theirs);
                AudioManager.PlaySfx(Sfx.Blocked, 0.9f, 0.6f);
            }
            zoneRadius = Mathf.Lerp(13.5f, 2.6f, k);
            var mid = new GridPos(BoardW / 2, BoardH / 2);
            // The rival (and every robot on the board) keeps inside the ring.
            hunt.SafeCentre = mid;
            if (hunt.Unsafe == null) hunt.Unsafe = p => Vector2.Distance(new Vector2(p.x, p.y), new Vector2(BoardW / 2, BoardH / 2)) > zoneRadius;
            int step = Mathf.RoundToInt(zoneRadius * 2f);
            if (step != zoneDrawn)
            {
                zoneDrawn = step;
                var red = new Color(1f, 0.3f, 0.25f);
                for (int y = 0; y < BoardH; y++)
                    for (int x = 0; x < BoardW; x++)
                    {
                        var p = new GridPos(x, y);
                        if (!grid.Exists(p)) continue;
                        bool outside = Vector2.Distance(new Vector2(x, y), new Vector2(mid.x, mid.y)) > zoneRadius;
                        gridView.SetTint(p, outside ? red : (Color?)null);
                    }
            }
            if (LiveActive) return;
            // Out in the red: foi's health drains (in small knocks, so it shows), and the rival's too.
            if (Vector2.Distance(new Vector2(robot.Position.x, robot.Position.y), new Vector2(mid.x, mid.y)) > zoneRadius)
            {
                zoneHurtMe += dt * 0.07f;
                if (zoneHurtMe >= 0.035f)
                {
                    hitShareOverride = zoneHurtMe;
                    moveHit = true;
                    OnBlockImpact(robot.Position);
                    moveHit = false;
                    hitShareOverride = -1f;
                    zoneHurtMe = 0f;
                }
            }
            var foeTile = hunt.ChallengeFoeTile;
            if (foeTile.HasValue && Vector2.Distance(new Vector2(foeTile.Value.x, foeTile.Value.y), new Vector2(mid.x, mid.y)) > zoneRadius)
            {
                zoneHurtFoe += dt;
                if (zoneHurtFoe >= 0.6f) { zoneHurtFoe = 0f; HitFoe(0.35f); }
            }
        }

        private void UpdateDuel(float dt)
        {
            UpdateTutorial(); // (the guided first duel)
            UpdateTraining(); // (the training room)
            ui.SetRivalHealth(true, hunt.ChallengeFoeHealth); // the rival's bar at the top right, over its name
            UpdateDuelZone(dt);
            UpdateDuelSkills(dt);
            chDuelLeft = Mathf.Max(0f, chDuelLeft - dt);
            // the distance the player keeps, sampled once a second (its style, for its friends' copy of it)
            if (Mathf.FloorToInt(chDuelLeft) != Mathf.FloorToInt(chDuelLeft + dt) && hunt.ChallengeFoeTile.HasValue)
            { PlayStyle.Sample(HuntSystem.Chebyshev(robot.Position, hunt.ChallengeFoeTile.Value)); stats.Distance(HuntSystem.Chebyshev(robot.Position, hunt.ChallengeFoeTile.Value)); }
            RefreshDuelClock();
            RefreshSquadBar();
            if (rivalShieldUntil > 0f && Time.time > rivalShieldUntil) { rivalShieldUntil = 0f; hunt.EndFoeShield(); }
            if (LiveActive) return; // the server keeps the score and the clock
            RivalPowerUps();
            if (hunt.ChallengeFoeDown) { DuelKnockdown(true); return; }
            if (chDuelLeft <= 0f)
            {
                if (overtime) OvertimeEnd(); // (no knock-down in the overtime: health decides)
                else if (!TryStartOvertime()) { chPhase = ChPhase.Break; hunt.Freeze(); ChallengeEnd(); }
            }
        }

        /// <summary>The AI's hearts and shields: a heart when badly hurt, a shield now and then when foi presses it.</summary>
        private void RivalPowerUps()
        {
            if (Time.time < rivalPowerUpAt) return;
            float share = hunt.ChallengeFoeHealth;
            float heartBelow = rivalHearts == 1 ? 0.25f : botTuning != null && Random.value < botTuning.earlyHeart ? 0.7f : 0.4f; // (its last heart kept for later: only when badly hurt)
            if (rivalHearts > 0 && share < heartBelow && Random.value < 0.6f)
            {
                rivalHearts--;
                hunt.HealFoe(DuelHeartHeal + (rivalPassive == PassiveKind.HeartMaster ? DuelBalanceTable.Current.heartMasterHeal : 0f));
                chUi.SetStatus(Loc.F("ch.rivalHeart", rivalHearts));
                rivalPowerUpAt = Time.time + 8f;
                return;
            }
            rivalPowerUpAt = Time.time + 1.5f;
        }

        // ---------- The player's hearts, shields and squad orders ----------

        /// <summary>A heart: a third of foi's health back (live: the server heals).</summary>
        private void UseDuelHeart()
        {
            if (chPhase != ChPhase.Duel || duelHearts <= 0 || health >= 0.999f) return;
            PlayStyle.Heart(health);
            duelHearts--;
            mcHearts++;
            stats.mine.hearts++;
            if (LiveActive) LiveNet.Heal();
            else
            {
                Heal(robot.Position);
                if (myPassive == PassiveKind.HeartMaster) { health = Mathf.Min(1f, health + DuelBalanceTable.Current.heartMasterHeal); RefreshHealthBar(); } // (36% instead of 30%)
            }
            AudioManager.PlaySfx(Sfx.Shield, 0.8f, 1.6f);
            RefreshSquadBar();
        }

        /// <summary>A shield: three seconds, two blows (live: the server counts the blows too).</summary>
        private void UseDuelShield()
        {
            if (chPhase != ChPhase.Duel || duelShields <= 0 || robot.IsShielded) return;
            duelShields--;
            PlayStyle.Shield();
            robot.GiveShield(DuelShieldSeconds, DuelShieldHits + (myPassive == PassiveKind.ShieldMaster ? DuelBalanceTable.Current.shieldMasterHits : 0)); // (the shield master: 3 blows)
            if (LiveActive) LiveNet.Shield();
            AudioManager.PlaySfx(Sfx.Shield, 1f, 1.1f);
            RefreshSquadBar();
        }

        private void RefreshSquadBar()
        {
            if (chUi == null || !challengeRun) return;
            var left = hunt.SquadAliveByGroup(true);
            var orders = new bool[Army.Groups];
            for (int g = 0; g < Army.Groups; g++) orders[g] = hunt.SquadOrder(true, g);
            chUi.ShowSquadBar(left, orders, duelHearts, duelShields);
            int mine = 0, theirs = 0;
            foreach (int n in left) mine += n;
            foreach (int n in hunt.SquadAliveByGroup(false)) theirs += n;
            chUi.SetSides(Challenge.PlayerName, Challenge.Rank, mine, rival.name, rival.Rank, theirs);
        }

        private void RefreshDuelClock()
        {
            int s = Mathf.CeilToInt(chDuelLeft);
            chUi.SetClock(s / 60 + ":" + (s % 60).ToString("00"), ScoreText()); // (the time between the names, the score between the bars)
            matchWorst = Mathf.Max(matchWorst, chDuelThem - chDuelMe);
            // The last ten seconds: the score grows with every beat, a heartbeat; level, the golden knock-down.
            if (chPhase == ChPhase.Duel && s <= 10 && s > 0 && s != lastBeat)
            {
                lastBeat = s;
                chUi.PulseScore(1.35f);
                AudioManager.PlaySfx(Sfx.Bump, 0.8f, 0.55f);
                AudioManager.SetTension(1f);
                if (s == 10 && chDuelMe == chDuelThem && !overtime && OvertimeAllowed) chUi.SetStatus(Loc.T("duel.goldenKoSoon")); // (level with ten seconds left: the overtime is coming)
            }
        }

        private int lastBeat = -1;

        /// <summary>The score as the board shows it (mine first).</summary>
        private string ScoreText() => chDuelMe + " - " + chDuelThem;

        private int ScoreMine => chDuelMe;
        private int ScoreTheirs => chDuelThem;

        private void ClearDuel()
        {
            if (myTag != null) Destroy(myTag.gameObject);
            if (theirTag != null) Destroy(theirTag.gameObject);
            myTag = theirTag = null;
            hunt.ClearSquads();
            EndDuelSkills();
        }
    }

    /// <summary>The two sides' colours in the challenge (the player's blue, the rival's red).</summary>
    public static class ArmyBattleColours
    {
        public static readonly Color Mine = new Color(0.3f, 0.62f, 1f), Theirs = new Color(1f, 0.32f, 0.3f);
    }
}
