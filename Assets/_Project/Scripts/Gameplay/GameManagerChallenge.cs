using System.Collections;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using SquashBot.Visual;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// The challenge (main menu → CHALLENGE): the waiting room finds a rival close in trophies and army power (a bot for
    /// now), then the board — an isometric courtyard with the two armies of six groups along its ends and the two foi in
    /// the middle of them. Each turn both sides pick a group in secret (ten seconds); the picks are shown and the two
    /// groups clash live in the middle; the survivors walk back. When one side has no soldiers left the duel begins: foi
    /// is the player's to move and strike, the rival foi fights back, and the soldiers still standing fight on beside
    /// their foi. Beating the rival foi wins trophies and gold; falling loses trophies.
    /// </summary>
    public partial class GameManager
    {
        private enum ChPhase { Off, Lobby, Pick, Reveal, Clash, Duel, Done }

        private bool challengeRun;
        private ChPhase chPhase = ChPhase.Off;
        private Challenge.Opponent rival;
        private ClashSystem clash;
        private ChallengeScreen chUi;
        private float chTimer, chBotPickAt;
        private int chMyPick = -1, chTheirPick = -1;
        private readonly int[] myLeft = new int[Army.Groups], theirLeft = new int[Army.Groups];
        private Transform rivalLook;
        private Vector3 boardCentre;
        private bool chCancelled;
        /// <summary>How far the player has slid the board view.</summary>
        private Vector3 boardPan;

        private const int BoardW = 25, BoardH = 21, PickSeconds = 10;

        private bool ChallengeArmyPhase => challengeRun && chPhase != ChPhase.Duel && chPhase != ChPhase.Off && chPhase != ChPhase.Done;

        private void InitChallenge()
        {
            chUi = ChallengeScreen.Create(ui.transform.Find("Canvas/SafeArea") ?? ui.transform.Find("Canvas") ?? ui.transform);
            chUi.GroupPicked += g => { if (chPhase == ChPhase.Pick) { chMyPick = g; chUi.SetStatus(Loc.T("ch.waitRival")); } };
            chUi.Leave += () => { chCancelled = true; chUi.HideAll(); };
            chUi.BoardTapped += OnBoardTap;
            chUi.BoardDragged += d =>
            {
                // The finger drags the board: the view slides the other way, kept over the courtyard.
                var cam = cameraRig.Cam.transform;
                var right = cam.right; right.y = 0f; right.Normalize();
                var fwd = cam.forward; fwd.y = 0f; fwd.Normalize();
                float scale = 26f / Mathf.Max(1f, Screen.height);
                boardPan -= (right * d.x + fwd * d.y) * scale;
                boardPan = new Vector3(Mathf.Clamp(boardPan.x, -BoardW * 0.45f, BoardW * 0.45f), 0f, Mathf.Clamp(boardPan.z, -BoardH * 0.45f, BoardH * 0.45f));
            };
            var go = new GameObject("Clash");
            clash = go.AddComponent<ClashSystem>();
            clash.Init(fx);
            clash.ClashOver += OnClashOver;
        }

        /// <summary>CHALLENGE on the menu.</summary>
        private void StartChallenge()
        {
            if (!Challenge.Open)
            {
                ui.ShowIntro(Loc.T("ch.title"), Loc.T("ch.locked"));
                return;
            }
            if (chUi == null) InitChallenge();
            if (State != GameState.Menu) ShowMenu();
            chCancelled = false;
            chPhase = ChPhase.Lobby;
            cameraRig.StartCoroutine(Lobby());
        }

        private string SideText(string name, int rank, int trophies, int power) =>
            name + "\n" + Challenge.RankName(rank) + " · " + Loc.F("ch.trophies", trophies) + " · " + Loc.F("ch.power", power);

        /// <summary>The waiting room: "looking for a rival", the rival found, then onto the board.</summary>
        private IEnumerator Lobby()
        {
            chUi.ShowLobby(SideText(Challenge.PlayerName, Challenge.Rank, Challenge.Trophies, Army.PowerScore));
            yield return new WaitForSecondsRealtime(Random.Range(1.6f, 3f));
            if (chCancelled) { chPhase = ChPhase.Off; yield break; }
            int world = LevelCatalog.WorldOf(Mathf.Max(0, SaveData.UnlockedLevel - 1));
            rival = Challenge.MakeBot(Random.Range(0, 1000000), Army.EarnedPoints, world);
            int rivalPower = 0;
            for (int g = 0; g < Army.Groups; g++) rivalPower += Mathf.RoundToInt(Army.Soldiers * rival.Power(g) * 10f);
            chUi.ShowRival(SideText(rival.name, rival.Rank, rival.trophies, rivalPower));
            yield return new WaitForSecondsRealtime(1.8f);
            if (chCancelled) { chPhase = ChPhase.Off; yield break; }
            chUi.HideLobby();
            BuildBoard(world);
        }

        /// <summary>The courtyard: a plain floor in the world's look, ramparts round it, no crates, no vanG, no cage.</summary>
        private void BuildBoard(int world)
        {
            var rows = new string[BoardH];
            for (int r = 0; r < BoardH; r++)
            {
                var row = new char[BoardW];
                for (int x = 0; x < BoardW; x++) row[x] = '#';
                if (r == BoardH - 3) row[BoardW / 2] = 'S'; // foi: in the middle of its army (the south end)
                rows[r] = new string(row);
            }
            levelIndex = Mathf.Clamp(world * LevelCatalog.LevelsPerWorld + 5, 0, LevelCount - 1);
            var l = levelSet.levels[levelIndex].Clone();
            l.layout = rows;
            l.stageBounds = new[] { 0, 0, BoardW - 1, BoardH - 1 };
            l.fortressStages = 1;
            l.gridWidth = BoardW;
            l.gridHeight = BoardH;
            l.terrain = TerrainKind.Flat;
            l.mission = MissionType.Hunt;
            l.hard = false;
            l.marathon = false;
            l.final = false;
            l.robots = 0; l.robotsAtOnce = 0; l.elites = 0; l.hardAtOnce = 0; l.brutes = 0; l.towers = 0; l.bugs = 0;
            l.monsterHp = 1;
            Calm(l);
            level = l;
            challengeRun = true;
            bonusRun = true;
            arenaRun = false;
            trailRun = false;
            dailyRun = false;
            BeginRun();
            hazards.Freeze();
            fifiReadyAt = Time.time + 99999f; // Fifi stays out of challenges
            introPending = false;
            // The armies: mine along the south end, the rival's along the north, three groups either side of each foi.
            var mine = new Vector3[Army.Groups];
            var theirs = new Vector3[Army.Groups];
            var myPower = new float[Army.Groups];
            var theirPower = new float[Army.Groups];
            int[] xs = { 3, 7, 10, 14, 17, 21 };
            for (int g = 0; g < Army.Groups; g++)
            {
                mine[g] = GridView.ToWorld(new GridPos(xs[g], 2)) + Vector3.up * GridView.SurfaceY;
                theirs[g] = GridView.ToWorld(new GridPos(xs[g], BoardH - 3)) + Vector3.up * GridView.SurfaceY;
                myPower[g] = Army.Power(g);
                theirPower[g] = rival.Power(g);
                myLeft[g] = theirLeft[g] = Army.Soldiers;
            }
            boardCentre = GridView.ToWorld(new GridPos(BoardW / 2, BoardH / 2)) + Vector3.up * GridView.SurfaceY;
            boardPan = Vector3.zero;
            // foi among its soldiers at the south end, the rival foi facing it at the north.
            robot.Spawn(grid, new GridPos(BoardW / 2, 1));
            if (rivalLook != null) Destroy(rivalLook.gameObject);
            rivalLook = RivalLook();
            rivalLook.position = GridView.ToWorld(new GridPos(BoardW / 2, BoardH - 2)) + Vector3.up * GridView.SurfaceY;
            rivalLook.rotation = Quaternion.Euler(0f, 180f, 0f);
            clash.Begin(boardCentre, mine, theirs, myPower, theirPower, hunt.FoiHeight * 0.8f);
            chUi.ShowBattle();
            ui.SetHudTitle(Loc.T("ch.title"));
            BeginTurn();
            AudioManager.PlaySfx(Sfx.Win, 0.6f, 0.9f);
        }

        /// <summary>A copy of foi in the rival's red, for the rival's foi.</summary>
        private Transform RivalLook()
        {
            var root = new GameObject("RivalLook").transform;
            robot.BuildLookalike(root);
            root.localScale = HeroScale;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var m = r.material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), ClashSystem.Theirs, 0.45f));
            }
            return root;
        }

        private int Total(int[] left)
        {
            int n = 0;
            foreach (var v in left) n += v;
            return n;
        }

        private void RefreshSides() => chUi.SetSides(Challenge.PlayerName, Total(myLeft), rival.name, Total(theirLeft));

        /// <summary>A tap on the board while picking: my group nearest the finger (on the screen) is sent.</summary>
        private void OnBoardTap(Vector2 screen)
        {
            if (chPhase != ChPhase.Pick || chMyPick >= 0) return;
            var cam = cameraRig.Cam;
            int best = -1;
            float bestD = Screen.height * 0.09f;
            for (int g = 0; g < Army.Groups; g++)
            {
                if (myLeft[g] <= 0) continue;
                var p = cam.WorldToScreenPoint(clash.Spot(0, g) + Vector3.up * 0.4f);
                if (p.z <= 0f) continue;
                float d = Vector2.Distance(screen, p);
                if (d < bestD) { bestD = d; best = g; }
            }
            if (best < 0) return;
            chUi.PickFromBoard(best);
            clash.SetPickable(null, best);
        }

        private void BeginTurn()
        {
            chPhase = ChPhase.Pick;
            chTimer = PickSeconds;
            chMyPick = chTheirPick = -1;
            chBotPickAt = PickSeconds - Random.Range(1.5f, 6.5f);
            RefreshSides();
            chUi.OpenPick(myLeft);
            var can = new bool[Army.Groups];
            for (int g = 0; g < Army.Groups; g++) can[g] = myLeft[g] > 0;
            clash.SetPickable(can, -1);
            chUi.SetStatus(Loc.T("ch.pick"));
        }

        /// <summary>The bot's pick, by its boldness: a bold one sends its strongest groups first, a careful one saves them.</summary>
        private int BotPick()
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int g = 0; g < Army.Groups; g++)
            {
                if (theirLeft[g] <= 0) continue;
                float strength = theirLeft[g] * rival.Power(g);
                float s = (rival.boldness - 0.5f) * strength + Random.value * 6f;
                if (s > bestScore) { bestScore = s; best = g; }
            }
            return best;
        }

        /// <summary>Each frame of a challenge: the turn's clock, the reveal, the end of the duel.</summary>
        private void UpdateChallenge(float dt)
        {
            if (!challengeRun || State != GameState.Playing) return;
            switch (chPhase)
            {
                case ChPhase.Pick:
                    chTimer -= dt;
                    chUi.SetClock(Mathf.CeilToInt(Mathf.Max(0f, chTimer)).ToString());
                    if (chTheirPick < 0 && chTimer <= chBotPickAt) chTheirPick = BotPick();
                    if (chTimer > 0f && (chMyPick < 0 || chTheirPick < 0)) break;
                    // Time's up (or both have picked): any group left for whoever hasn't.
                    if (chMyPick < 0)
                    {
                        int start = Random.Range(0, Army.Groups);
                        for (int k = 0; k < Army.Groups; k++) if (myLeft[(start + k) % Army.Groups] > 0) { chMyPick = (start + k) % Army.Groups; break; }
                    }
                    if (chTheirPick < 0) chTheirPick = BotPick();
                    chUi.ClosePick();
                    clash.SetPickable(null, -1);
                    chPhase = ChPhase.Reveal;
                    chTimer = 1.2f;
                    chUi.SetClock("");
                    chUi.SetStatus(Loc.T("army.kind." + Army.KindOf(chMyPick)) + "  VS  " + Loc.T("army.kind." + Army.KindOf(chTheirPick)));
                    AudioManager.PlaySfx(Sfx.Warning, 0.7f, 1.1f);
                    break;
                case ChPhase.Reveal:
                    chTimer -= dt;
                    if (chTimer > 0f) break;
                    chPhase = ChPhase.Clash;
                    clash.StartClash(chMyPick, chTheirPick);
                    break;
                case ChPhase.Duel:
                    if (hunt.ChallengeFoeDown) ChallengeEnd(true);
                    break;
            }
        }

        private void OnClashOver(int mineLeftInGroup, int theirsLeftInGroup)
        {
            if (!challengeRun) return;
            myLeft[chMyPick] = mineLeftInGroup;
            theirLeft[chTheirPick] = theirsLeftInGroup;
            clash.Label(0, chMyPick, mineLeftInGroup);
            clash.Label(1, chTheirPick, theirsLeftInGroup);
            RefreshSides();
            chUi.SetStatus(mineLeftInGroup > theirsLeftInGroup ? Loc.T("ch.clashWon") : mineLeftInGroup < theirsLeftInGroup ? Loc.T("ch.clashLost") : Loc.T("ch.clashEven"));
            cameraRig.StartCoroutine(AfterClash());
        }

        private IEnumerator AfterClash()
        {
            yield return new WaitForSeconds(1.6f); // the survivors walk back
            if (!challengeRun || State != GameState.Playing) yield break;
            if (Total(myLeft) > 0 && Total(theirLeft) > 0) { BeginTurn(); yield break; }
            StartChallengeDuel();
        }

        /// <summary>One army is gone: the two foi fight it out, the other army's survivors at their foi's side.</summary>
        private void StartChallengeDuel()
        {
            chPhase = ChPhase.Duel;
            chUi.HideGroups();
            chUi.SetStatus(Loc.T("ch.duel"));
            // The soldiers still standing go into the duel as they are (their own models), the rest of the board clears.
            var mineLeft = clash.TakeSurvivors(0);
            var theirsLeft = clash.TakeSurvivors(1);
            clash.Clear();
            if (rivalLook != null) { rivalLook.SetParent(null, true); }
            var look = rivalLook;
            rivalLook = null;
            hunt.AddChallengeFoe(new GridPos(BoardW / 2, BoardH - 2), rival.foiHp, rival.foiHit, look);
            foreach (var (body, bot, group, kind) in mineLeft)
                hunt.AddChallengeAlly(body.position, Mathf.RoundToInt(3f * Army.Power(group)), body, bot);
            foreach (var (body, bot, group, kind) in theirsLeft)
                hunt.AddChallengeSoldier(body.position, Mathf.RoundToInt(3f * rival.Power(group)), body, bot);
            ui.ShowIntro(Loc.T("ch.duel"), Loc.T("ch.duelText"));
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 0.8f);
        }

        /// <summary>The challenge is decided: trophies, gold for a win, and the result card (again, or the menu).</summary>
        private void ChallengeEnd(bool won)
        {
            if (chPhase == ChPhase.Done) return;
            chPhase = ChPhase.Done;
            int change = Challenge.Record(won, rival.trophies);
            int gold = won ? Challenge.WinGold : 0;
            SaveData.Coins += gold + Earned;
            if (won)
            {
                State = GameState.Result;
                hunt.Freeze();
                robot.Cheer();
                AudioManager.PlaySfx(Sfx.Win);
                fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 40, 6f);
            }
            chUi.HideAll();
            string line = Loc.F(won ? "ch.wonLine" : "ch.lostLine", rival.name) + "\n"
                          + (change >= 0 ? "+" : "") + Loc.F("ch.trophies", change) + (gold > 0 ? " · +" + gold + " " + Loc.T("ch.gold") : "")
                          + "\n" + Challenge.RankName(Challenge.Rank) + " · " + Loc.F("ch.trophies", Challenge.Trophies);
            StartCoroutine(ShowResultDelayed(new UIController.ResultInfo
            {
                won = won,
                bonusRound = true,
                arena = true,
                title = Loc.T(won ? "ch.victory" : "ch.defeat"),
                hasNext = false,
                canDouble = won,
                subtitle = line,
                coins = gold + Earned,
            }));
        }

        /// <summary>Leaving a challenge (the menu): the board's armies and screens go.</summary>
        private void EndChallenge()
        {
            if (!challengeRun && chPhase == ChPhase.Off) return;
            challengeRun = false;
            chPhase = ChPhase.Off;
            if (clash != null) clash.Clear();
            if (chUi != null) chUi.HideAll();
            if (rivalLook != null) Destroy(rivalLook.gameObject);
            rivalLook = null;
            hunt.ClearChallengeFoe();
        }

        /// <summary>The board seen whole from above its south end, while the armies fight.</summary>
        private void BoardPose(out Vector3 pos, out Quaternion rot)
        {
            // Far enough for the whole board on a tall phone, aimed a little south so it sits above the group buttons.
            float aspect = Mathf.Max(0.4f, cameraRig.Cam.aspect);
            float k = Mathf.Clamp(0.86f / aspect, 1f, 2.1f);
            pos = boardCentre + boardPan + new Vector3(-7f, 21f, -17f) * k;
            rot = Quaternion.LookRotation(boardCentre + boardPan + new Vector3(0f, 0f, -4f) - pos);
        }
    }
}
