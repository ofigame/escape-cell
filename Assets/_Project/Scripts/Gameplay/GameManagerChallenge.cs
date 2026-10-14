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
    /// The challenge (main menu → CHALLENGE): the first time, the player picks a name; the waiting room asks the server
    /// (<see cref="FoiNet"/>) for a rival close in trophies and army power — a real player's army played by the AI, or a
    /// bot when nobody fits or there is no network (then a practice match: no trophies) — then the board — an isometric courtyard with the two armies of six groups along its ends and the two foi in
    /// the middle of them. Each turn both sides pick a group in secret (ten seconds); the picks are shown and the two
    /// groups clash live in the middle; the survivors walk back. When one side has no soldiers left the duel begins: foi
    /// is the player's to move and strike, the rival foi fights back, and the soldiers still standing fight on beside
    /// their foi. Beating the rival foi wins trophies and gold; falling loses trophies.
    /// </summary>
    public partial class GameManager
    {
        private enum ChPhase { Off, Lobby, Pick, Reveal, Clash, Battle, Break, Duel, Done }

        /// <summary>The armies' point: +1 mine won the battle (1-0), -1 theirs did (0-1), 0 neither (a level battle).</summary>
        private int chLead;
        private GlassCell myCell, theirCell;
        private Colosseum colosseum;
        private float tourNext, zoom = 1f;
        private Vector3 tourSpot;

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
        /// <summary>Bumped by every waiting room, so an old one that is still waiting knows it is over.</summary>
        private int chLobbyToken;
        /// <summary>The server's challenge being played (0: none, or a practice match).</summary>
        private long chMatchId;
        private bool chOnline;
        /// <summary>How far the player has slid the board view.</summary>
        private Vector3 boardPan;
        private float boardDraggedAt = -99f;

        private const int BoardW = 25, BoardH = 21, PickSeconds = 10;

        private bool ChallengeArmyPhase => challengeRun && chPhase != ChPhase.Duel && chPhase != ChPhase.Off && chPhase != ChPhase.Done;

        private void InitChallenge()
        {
            chUi = ChallengeScreen.Create(ui.transform.Find("Canvas/SafeArea") ?? ui.transform.Find("Canvas") ?? ui.transform);
            chUi.GroupPicked += g => { if (chPhase == ChPhase.Pick) { chMyPick = g; chUi.SetStatus(Loc.T("ch.waitRival")); } };
            chUi.Leave += () => { chCancelled = true; chUi.HideAll(); };
            chUi.NameSubmitted += OnNameSubmitted;
            chUi.NameClosed += () => { chCancelled = true; chPhase = ChPhase.Off; chUi.HideAll(); };
            chUi.RenamePressed += () => { chCancelled = true; chPhase = ChPhase.Off; chUi.ShowName(Challenge.PlayerName); };
            chUi.BoardTapped += OnBoardTap;
            chUi.BoardDragged += d =>
            {
                // The finger drags the board: the view slides the other way, kept over the courtyard.
                var cam = cameraRig.Cam.transform;
                var right = cam.right; right.y = 0f; right.Normalize();
                var fwd = cam.forward; fwd.y = 0f; fwd.Normalize();
                float scale = 34f / Mathf.Max(1f, Screen.height);
                boardPan -= (right * d.x + fwd * d.y) * scale;
                boardDraggedAt = Time.unscaledTime;
                boardPan = new Vector3(Mathf.Clamp(boardPan.x, -BoardW * 0.45f, BoardW * 0.45f), 0f, Mathf.Clamp(boardPan.z, -BoardH * 0.48f, BoardH * 0.48f));
            };
            var go = new GameObject("Clash");
            clash = go.AddComponent<ClashSystem>();
            clash.Init(fx);
            clash.ClashOver += OnClashOver;
            clash.BattleOver += OnBattleOver;
        }

        /// <summary>CHALLENGE on the menu.</summary>
        private void StartChallenge()
        {
            if (!Challenge.Open)
            {
                ui.ShowIntro(Loc.T("ch.title"), Loc.T("ch.locked"));
                return;
            }
            if (!Challenge.Enabled)
            {
                ui.ShowIntro(Loc.T("ch.title"), Loc.T("ch.disabled"));
                return;
            }
            Notifications.AskOnce(token => cameraRig.StartCoroutine(FoiNet.PutSettings(Challenge.Enabled, token, FoiNet.LangCode, _ => { })));
            if (chUi == null) InitChallenge();
            ui.ChallengeStats.Hide();
            ui.ArmyView.Hide();
            if (State != GameState.Menu) ShowMenu();
            if (!Challenge.NameChosen)
            {
                chPhase = ChPhase.Off;
                chUi.ShowName(Challenge.PlayerName);
                return;
            }
            chCancelled = false;
            chPhase = ChPhase.Lobby;
            cameraRig.StartCoroutine(Lobby());
        }

        private string SideText(string name, int rank, int trophies, int power) =>
            name + "\n" + Challenge.RankName(rank) + " · " + Loc.F("ch.trophies", trophies) + " · " + Loc.F("ch.power", power);

        /// <summary>A name typed on the name screen: checked here, then claimed on the server (offline: kept for later).</summary>
        private void OnNameSubmitted(string name)
        {
            if (Challenge.NameProblem(name) is string why) { chUi.NameRefused(Loc.T("ch.name." + why)); return; }
            chUi.NameChecking();
            cameraRig.StartCoroutine(FoiNet.SetName(name, (err, profile) =>
            {
                if (err != null && err != "offline") { chUi.NameRefused(Loc.T("ch.name." + err)); return; }
                Challenge.PlayerName = name;
                Challenge.NameChosen = true;
                Challenge.Sync(profile);
                chUi.HideName();
                StartChallenge();
            }));
        }

        private bool LobbyOver(int token) => chCancelled || token != chLobbyToken;

        /// <summary>Tells the server whether challenges are on, the phone's notification token and the language.</summary>
        private void SyncChallengeSettings()
        {
            ui.RefreshSettings();
            ui.RefreshMenuRank(ui.MenuDefenseLine);
            cameraRig.StartCoroutine(FoiNet.PutSettings(Challenge.Enabled, Notifications.Token, FoiNet.LangCode, _ => { }));
        }

        private float menuWatchNext;

        /// <summary>
        /// While the menu is up, every half minute: is the player's army being fought right now (by whom), and what
        /// happened to it since last time — shown on the menu under the rank card. The record is refreshed too.
        /// </summary>
        private void WatchArmyFromMenu()
        {
            if (Time.unscaledTime < menuWatchNext || !ui.MenuVisible || !FoiNet.Registered || !Challenge.Enabled) return;
            menuWatchNext = Time.unscaledTime + 30f;
            cameraRig.StartCoroutine(FoiNet.Defense((live, items) =>
            {
                string line = null;
                if (live.Length > 0) line = Loc.F("ch.live", live[0]);
                else if (items.Length > 0)
                {
                    int held = 0, change = 0;
                    int fell = 0;
                    foreach (var a in items) { if (a.held) held++; else if (!a.draw) fell++; change += a.change; }
                    line = Loc.F("ch.defense", items.Length, held, fell, (change >= 0 ? "+" : "") + change);
                    PlayerPrefs.SetString(DefenseNewsKey, line);
                }
                else line = PlayerPrefs.GetString(DefenseNewsKey, "");
                ui.MenuDefenseLine = line;
                if (items.Length > 0 || live.Length > 0)
                    cameraRig.StartCoroutine(FoiNet.Me(p => { Challenge.Sync(p); ui.RefreshMenuRank(ui.MenuDefenseLine); }));
                else ui.RefreshMenuRank(line);
            }));
        }

        private const string DefenseNewsKey = "sb_ch_defnews";

        /// <summary>The rank card was tapped: the challenge's overall results (the server's numbers first, if it answers).</summary>
        private void ShowChallengeStats()
        {
            PlayerPrefs.DeleteKey(DefenseNewsKey); // the news has been seen
            ui.MenuDefenseLine = null;
            ui.RefreshMenuRank(null);
            if (!FoiNet.Registered) { ui.ChallengeStats.Show(); return; }
            cameraRig.StartCoroutine(FoiNet.Me(p => { Challenge.Sync(p); ui.ChallengeStats.Show(); }));
        }

        /// <summary>
        /// The waiting room: "looking for a rival" while the server gets the player's army and finds a rival (and tells
        /// of the attacks on the player's army since last time), the rival found, then onto the board.
        /// </summary>
        private IEnumerator Lobby()
        {
            int token = ++chLobbyToken;
            chMatchId = 0;
            chOnline = false;
            chUi.ShowLobby(SideText(Challenge.PlayerName, Challenge.Rank, Challenge.Trophies, Army.PowerScore));
            float started = Time.realtimeSinceStartup;
            int world = LevelCatalog.WorldOf(Mathf.Max(0, SaveData.UnlockedLevel - 1));

            bool ok = false;
            yield return FoiNet.EnsurePlayer(r => ok = r);
            if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
            if (ok && FoiNet.SyncedName != Challenge.PlayerName)
            {
                // A name chosen offline goes up now; taken meanwhile: pick another.
                string err = "offline";
                yield return FoiNet.SetName(Challenge.PlayerName, (e, p) => { err = e; Challenge.Sync(p); });
                if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
                if (err != null && err != "offline")
                {
                    chPhase = ChPhase.Off;
                    Challenge.NameChosen = false;
                    chUi.ShowName(Challenge.PlayerName, Loc.T("ch.name." + err));
                    yield break;
                }
                if (err == "offline") ok = false;
            }
            if (ok)
            {
                var points = new int[Army.Groups];
                for (int g = 0; g < Army.Groups; g++) points[g] = Army.GroupPoints(g);
                yield return FoiNet.PutArmy(points, Army.EarnedPoints, world, Challenge.FoiHpFor(world), Challenge.FoiHitFor(world), 0.5f,
                    Armory.Equipped.id, Armory.Level(Armory.Equipped), Shop.ArmorShare, r => ok = r);
                if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
            }
            FoiNet.Match match = null;
            if (ok)
            {
                FoiNet.Attack[] attacks = null;
                yield return FoiNet.Defense((_, a) => attacks = a);
                if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
                // From here the challenge counts: no leaving the room.
                chUi.LockLobby();
                yield return FoiNet.Find(m => match = m);
                if (match != null)
                {
                    yield return FoiNet.Me(Challenge.Sync);
                    chUi.SetMyCard(SideText(Challenge.PlayerName, Challenge.Rank, Challenge.Trophies, Army.PowerScore));
                }
                if (attacks != null && attacks.Length > 0)
                {
                    int held = 0, change = 0;
                    int fell = 0;
                    foreach (var a in attacks) { if (a.held) held++; else if (!a.draw) fell++; change += a.change; }
                    chUi.SetLobbyStatus(Loc.F("ch.defense", attacks.Length, held, fell, (change >= 0 ? "+" : "") + change));
                    yield return new WaitForSecondsRealtime(2.6f);
                }
            }
            float waited = Time.realtimeSinceStartup - started;
            if (waited < 1.6f) yield return new WaitForSecondsRealtime(1.6f - waited);
            if (match == null && LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }

            chOnline = match != null;
            chMatchId = match != null ? match.matchId : 0;
            rival = match != null && match.HasRival
                ? Challenge.FromRival(match.rival)
                : Challenge.MakeBot(match != null ? match.seed : Random.Range(0, 1000000), Army.EarnedPoints, world, match != null ? match.trophies : -1);
            int rivalPower = 0;
            for (int g = 0; g < Army.Groups; g++) rivalPower += Mathf.RoundToInt(Army.Soldiers * rival.Power(g) * 10f);
            chUi.ShowRival(SideText(rival.name, rival.Rank, rival.trophies, rivalPower));
            if (!chOnline) chUi.SetLobbyStatus(Loc.T("ch.offline"));
            yield return new WaitForSecondsRealtime(chOnline ? 1.8f : 2.4f);
            if (token != chLobbyToken || (match == null && chCancelled)) { if (token == chLobbyToken) chPhase = ChPhase.Off; yield break; }
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
            ui.SetChallengeHud(true);
            ui.SkillBar.Refresh(false);
            hazards.Freeze();
            fifiReadyAt = Time.time + 99999f; // Fifi stays out of challenges
            introPending = false;
            // The armies: mine along the south end, the rival's along the north, three groups either side of each foi.
            var mine = new Vector3[Army.Groups];
            var theirs = new Vector3[Army.Groups];
            var myPower = new float[Army.Groups];
            var theirPower = new float[Army.Groups];
            // Each army in two lines of three: the swords and the light spears in front (towards the middle), the heavy
            // spears and the archers behind them; foi's cell behind its army.
            float[] xs = { 5f, 12f, 19f, 5f, 12f, 19f };
            float[] lineZ = { 5.2f, 5.2f, 5.2f, 2.4f, 2.4f, 2.4f };
            for (int g = 0; g < Army.Groups; g++)
            {
                mine[g] = GridView.ToWorld(new GridPos(0, 0)) + new Vector3(xs[g], GridView.SurfaceY, lineZ[g]);
                theirs[g] = GridView.ToWorld(new GridPos(0, 0)) + new Vector3(xs[g], GridView.SurfaceY, BoardH - 1 - lineZ[g]);
                myPower[g] = Army.Power(g);
                theirPower[g] = rival.Power(g);
                myLeft[g] = theirLeft[g] = Army.Soldiers;
            }
            boardCentre = GridView.ToWorld(new GridPos(BoardW / 2, BoardH / 2)) + Vector3.up * GridView.SurfaceY;
            boardPan = Vector3.zero;
            // The arena round the courtyard: tiers, gates, torches, banners and the crowd.
            if (colosseum != null) Destroy(colosseum.gameObject);
            colosseum = Colosseum.Build(transform, GridView.ToWorld(new GridPos(BoardW / 2, BoardH / 2)), BoardW * 0.5f, BoardH * 0.5f, GridView.SurfaceY - 0.02f, rival.name.GetHashCode());
            // foi among its soldiers at the south end, the rival foi facing it at the north.
            // Both foi stand in front of the far stand (opposite the camera), either side of the halfway line.
            robot.Spawn(grid, new GridPos(0, BoardH / 2 - 4));
            robot.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // facing the camera across the courtyard
            if (rivalLook != null) Destroy(rivalLook.gameObject);
            rivalLook = RivalLook();
            rivalLook.position = GridView.ToWorld(new GridPos(0, BoardH / 2 + 4)) + Vector3.up * GridView.SurfaceY;
            rivalLook.rotation = Quaternion.Euler(0f, 90f, 0f); // facing the camera across the courtyard
            clash.Begin(boardCentre, mine, theirs, myPower, theirPower, hunt.FoiHeight * 0.8f);
            // The clashes' ground: between the two front lines (5 rows of soldiers deep each), a little short of them.
            float origin = GridView.ToWorld(new GridPos(0, 0)).z;
            clash.SetClashZone(origin + 5.2f + 1.15f + 0.4f, origin + (BoardH - 1) - 5.2f - 1.15f - 0.4f);
            clash.SetBounds(GridView.ToWorld(new GridPos(1, 1)) - new Vector3(0.3f, 0f, 0.3f), GridView.ToWorld(new GridPos(BoardW - 2, BoardH - 2)) + new Vector3(0.3f, 0f, 0.3f));
            // Both foi wait in glass cells while their armies fight; the cells break for the duel.
            chLead = 0;
            DestroyCells();
            float cellScale = Mathf.Max(robot.transform.localScale.x, robot.transform.localScale.y) * 0.62f;
            myCell = GlassCell.Create(GridView.ToWorld(robot.Position) + Vector3.up * GridView.SurfaceY, cellScale);
            theirCell = GlassCell.Create(rivalLook.position, cellScale);
            chUi.ShowBattle();
            ui.SetHudTitle(Loc.T("ch.title"));
            BeginTurn();
            AudioManager.PlaySfx(Sfx.Win, 0.6f, 0.9f);
        }

        /// <summary>A copy of foi in the rival's red, for the rival's foi.</summary>
        private Transform RivalLook()
        {
            var root = new GameObject("RivalLook").transform;
            var copy = robot.BuildLookalike(root).transform;
            root.localScale = robot.Visual.lossyScale; // foi's own size
            // Not foi's weapon: the rival's.
            var drop = new System.Collections.Generic.List<GameObject>();
            foreach (Transform c in copy.GetComponentsInChildren<Transform>(true)) if (c.name == "HeldWeapon") drop.Add(c.gameObject);
            foreach (var g in drop) DestroyImmediate(g);
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var m = r.material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), ClashSystem.Theirs, 0.45f));
            }
            rivalHeld = HeldWeapon.Attach(copy, Armory.Get(rival.weapon));
            return root;
        }

        private int Total(int[] left)
        {
            int n = 0;
            foreach (var v in left) n += v;
            return n;
        }

        private void RefreshSides() => chUi.SetSides(Challenge.PlayerName, Challenge.Rank, Total(myLeft), rival.name, rival.Rank, Total(theirLeft));

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
            chUi.SetStatus(""); // the pick card says it
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
                    chUi.SetPickTime(chTimer / PickSeconds);
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
                    chUi.SetClock(ScoreText());
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
            if (colosseum != null) colosseum.Cheer(mineLeftInGroup > theirsLeftInGroup ? 0 : mineLeftInGroup < theirsLeftInGroup ? 1 : -1, 0.8f);
            cameraRig.StartCoroutine(AfterClash());
        }

        private IEnumerator AfterClash()
        {
            yield return new WaitForSeconds(1.6f); // the survivors walk back
            if (!challengeRun || State != GameState.Playing) yield break;
            // Whoever's army is down to half first: every soldier still standing on both sides charges.
            int half = Army.Groups * Army.Soldiers / 2;
            if (Total(myLeft) > half && Total(theirLeft) > half) { BeginTurn(); yield break; }
            StartBattle();
        }

        /// <summary>An army is down to half: the all-out battle of every soldier left (the foi still in their cells).</summary>
        private void StartBattle()
        {
            chPhase = ChPhase.Battle;
            chUi.HideGroups();
            chUi.SetStatus(Loc.T("ch.battle"));
            ui.ShowIntro(Loc.T("ch.battle"), Loc.T("ch.battleText"));
            tourNext = 0f;
            clash.StartBattle();
            if (colosseum != null) { colosseum.Wave(true); colosseum.Cheer(-1, 0.5f); }
        }

        /// <summary>The battle is over: the winning army's foi goes 1-0 up (a level end: 0-0), the cells break, the soldiers leave.</summary>
        private void OnBattleOver(int mine, int theirs)
        {
            if (!challengeRun || chPhase != ChPhase.Battle) return;
            chLead = mine > theirs ? 1 : theirs > mine ? -1 : 0;
            if (colosseum != null) { colosseum.Wave(false); colosseum.Cheer(chLead > 0 ? 0 : chLead < 0 ? 1 : -1, 1f); }
            for (int g = 0; g < Army.Groups; g++) { myLeft[g] = 0; theirLeft[g] = 0; }
            myLeft[0] = mine; theirLeft[0] = theirs;
            RefreshSides();
            chPhase = ChPhase.Break;
            chUi.SetClock(ScoreText());
            chUi.SetStatus(Loc.T(chLead > 0 ? "ch.armyWon" : chLead < 0 ? "ch.armyLost" : "ch.armyEven"));
            AudioManager.PlaySfx(chLead >= 0 ? Sfx.Win : Sfx.Warning, 0.8f, 1f);
            cameraRig.StartCoroutine(Break());
        }

        private IEnumerator Break()
        {
            yield return new WaitForSeconds(1.4f);
            if (!challengeRun || chPhase != ChPhase.Break) yield break;
            clash.Retreat();
            // While the soldiers march off, the two cells glide to the middle with their foi inside.
            var myTile = new GridPos(BoardW / 2, BoardH / 2 - 2);
            var myFrom = robot.transform.position;
            var myTo = GridView.ToWorld(myTile) + Vector3.up * (myFrom.y - GridView.ToWorld(robot.Position).y);
            var cellOffset = myCell != null ? myCell.transform.position - myFrom : Vector3.zero;
            var theirFrom = rivalLook != null ? rivalLook.position : Vector3.zero;
            var theirTo = GridView.ToWorld(RivalTile) + Vector3.up * GridView.SurfaceY;
            var theirCellOffset = theirCell != null ? theirCell.transform.position - theirFrom : Vector3.zero;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 2.4f)
            {
                if (!challengeRun || chPhase != ChPhase.Break) yield break;
                float k = Mathf.SmoothStep(0f, 1f, t);
                robot.transform.position = Vector3.Lerp(myFrom, myTo, k);
                if (myCell != null) myCell.transform.position = robot.transform.position + cellOffset;
                if (rivalLook != null) rivalLook.position = Vector3.Lerp(theirFrom, theirTo, k);
                if (theirCell != null && rivalLook != null) theirCell.transform.position = rivalLook.position + theirCellOffset;
                yield return null;
            }
            robot.Spawn(grid, myTile);
            robot.transform.rotation = Quaternion.identity; // facing the rival across the middle
            if (rivalLook != null) { rivalLook.position = theirTo; rivalLook.rotation = Quaternion.Euler(0f, 180f, 0f); }
            yield return new WaitForSeconds(0.4f);
            if (myCell != null) myCell.Shatter();
            if (theirCell != null) theirCell.Shatter();
            if (colosseum != null) colosseum.Cheer(-1, 1f);
            AudioManager.PlaySfx(Sfx.Impact, 0.9f, 1.2f);
            yield return new WaitForSeconds(1.2f);
            if (!challengeRun || chPhase != ChPhase.Break) yield break;
            StartChallengeDuel();
        }

        /// <summary>Where the rival foi starts the duel: across the middle from foi.</summary>
        private static GridPos RivalTile => new GridPos(BoardW / 2, BoardH / 2 + 2);

        private HeldWeapon rivalHeld;

        /// <summary>The score as the board shows it (mine first).</summary>
        private string ScoreText() => (chLead > 0 ? 1 : 0) + " - " + (chLead < 0 ? 1 : 0);

        /// <summary>The cells are broken, the field is clear: the two foi fight it out alone.</summary>
        private void StartChallengeDuel()
        {
            chPhase = ChPhase.Duel;
            chUi.SetStatus(Loc.T("ch.duel"));
            clash.Clear();
            if (rivalLook != null) { rivalLook.SetParent(null, true); }
            var look = rivalLook;
            rivalLook = null;
            hunt.ChallengeBattle = true;
            var w = Armory.Get(rival.weapon);
            int hp = Mathf.RoundToInt(rival.foiHp / Mathf.Max(0.4f, 1f - rival.armor * 0.8f));
            float hit = Mathf.Clamp(rival.foiHit * (0.7f + 0.12f * Armory.Damage(w, Mathf.Max(1, rival.weaponLevel))), 0.05f, 0.35f);
            hunt.FoeBoldness = rival.boldness;
            hunt.AddChallengeFoe(RivalTile, hp, hit, look, rivalHeld);
            ui.ShowIntro(Loc.T("ch.duel"), Loc.T("ch.duelText"));
            AudioManager.PlaySfx(Sfx.Win, 0.8f, 0.8f);
        }

        private void DestroyCells()
        {
            if (myCell != null) Destroy(myCell.gameObject);
            if (theirCell != null) Destroy(theirCell.gameObject);
            myCell = theirCell = null;
        }

        /// <summary>The duel is decided (the rival foi down, or foi down): the final score — 2-0, 1-1 (a draw) or 0-2.</summary>
        private void ChallengeEnd(bool wonDuel)
        {
            int me = (chLead > 0 ? 1 : 0) + (wonDuel ? 1 : 0), them = (chLead < 0 ? 1 : 0) + (wonDuel ? 0 : 1);
            ChallengeFinish(me > them ? 1 : me < them ? -1 : 0, me + " - " + them);
        }

        /// <param name="outcome">1 won, 0 a draw (nobody gains anything), -1 lost.</param>
        private void ChallengeFinish(int outcome, string score)
        {
            if (chPhase == ChPhase.Done) return;
            chPhase = ChPhase.Done;
            bool won = outcome > 0, draw = outcome == 0;
            if (colosseum != null) colosseum.Cheer(won ? 0 : draw ? -1 : 1, 1f);
            // Online the server keeps the score (the same sum, shown at once, then its numbers taken); offline: practice.
            int change = 0;
            if (chOnline)
            {
                change = draw ? Challenge.RecordDraw() : Challenge.Record(won, rival.trophies);
                SendChallengeResult(won, draw);
            }
            int gold = won ? Challenge.WinGold : 0;
            SaveData.Coins += draw ? 0 : gold + Earned;
            if (won)
            {
                State = GameState.Result;
                hunt.Freeze();
                robot.Cheer();
                AudioManager.PlaySfx(Sfx.Win);
                fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 40, 6f);
            }
            chUi.HideAll();
            string line = score + "  ·  " + Loc.F(won ? "ch.wonLine" : draw ? "ch.drawLine" : "ch.lostLine", rival.name) + "\n"
                          + (chOnline ? (change >= 0 ? "+" : "") + Loc.F("ch.trophies", change) : Loc.T("ch.offline")) + (gold > 0 ? " · +" + gold + " " + Loc.T("ch.gold") : "")
                          + "\n" + Challenge.RankName(Challenge.Rank) + " · " + Loc.F("ch.trophies", Challenge.Trophies);
            StartCoroutine(ShowResultDelayed(new UIController.ResultInfo
            {
                won = won,
                bonusRound = true,
                arena = true,
                title = Loc.T(won ? "ch.victory" : draw ? "ch.draw" : "ch.defeat"),
                hasNext = false,
                canDouble = won,
                subtitle = line,
                coins = draw ? 0 : gold + Earned,
            }));
        }

        /// <summary>Leaving a challenge (the menu): the board's armies and screens go.</summary>
        private void SendChallengeResult(bool won, bool draw = false)
        {
            long id = chMatchId;
            chMatchId = 0;
            if (id == 0) return;
            cameraRig.StartCoroutine(FoiNet.SendResult(id, won, draw, r => { if (r != null) Challenge.Sync(r.profile); }));
        }

        private void EndChallenge()
        {
            // Walking out of a challenge the server knows of: it is lost (the server would count it so anyway).
            if (chMatchId != 0 && chPhase != ChPhase.Done)
            {
                Challenge.Record(false, rival != null ? rival.trophies : Challenge.Trophies);
                SendChallengeResult(false);
            }
            if (!challengeRun && chPhase == ChPhase.Off) return;
            challengeRun = false;
            chPhase = ChPhase.Off;
            if (clash != null) clash.Clear();
            if (chUi != null) chUi.HideAll();
            if (rivalLook != null) Destroy(rivalLook.gameObject);
            rivalLook = null;
            DestroyCells();
            if (colosseum != null) Destroy(colosseum.gameObject);
            colosseum = null;
            hunt.ClearChallengeFoe();
        }

        /// <summary>
        /// The armies' battle filmed like a football match: the camera sits high on the east stand at the halfway line
        /// (the player's army on the left, the rival's on the right), turning left and right to follow the fighting and
        /// closing in on a clash of two groups; in the all-out battle it pans slowly from fight to fight. A finger on the
        /// board turns it by hand; a little after the finger lifts it goes back to following.
        /// </summary>
        private void BoardPose(out Vector3 pos, out Quaternion rot)
        {
            bool duel = chPhase == ChPhase.Duel;
            if (InputReader.PointerHeld && !duel) boardDraggedAt = Time.unscaledTime; // in the duel the finger moves foi, not the camera
            bool free = duel || Time.unscaledTime - boardDraggedAt > 2.5f;
            bool groupClash = chPhase == ChPhase.Clash && clash != null && clash.Clashing && !clash.InBattle;
            zoom = Mathf.Lerp(zoom, groupClash && free ? 0.65f : 1f, Time.deltaTime * 1.4f);
            Vector3? want = null;
            if (duel) want = hunt.ChallengeFoeAt is Vector3 foe ? (robot.transform.position + foe) * 0.5f : robot.transform.position;
            else if (groupClash && clash.FightCentre is Vector3 fight) want = fight;
            else if (chPhase == ChPhase.Battle && clash != null)
            {
                if (Time.unscaledTime >= tourNext && clash.SomeFight() is Vector3 spot) { tourSpot = spot; tourNext = Time.unscaledTime + 4.5f; }
                want = tourSpot;
            }
            if (free && want.HasValue)
            {
                var w = want.Value - boardCentre;
                w = new Vector3(Mathf.Clamp(w.x, -BoardW * 0.45f, BoardW * 0.45f), 0f, Mathf.Clamp(w.z, -BoardH * 0.48f, BoardH * 0.48f));
                boardPan = Vector3.Lerp(boardPan, w, Time.deltaTime * (chPhase == ChPhase.Battle ? 0.7f : duel ? 3f : 1.6f));
            }
            // The camera's seat: high on the east stand, level with the middle of the courtyard.
            var seat = boardCentre + new Vector3(BoardW * 0.5f + 11f, 12.5f, 0f);
            var focus = boardCentre + boardPan;
            // Closing in: the camera leans forward along its line of sight (the seat stays where it is in spirit).
            pos = Vector3.Lerp(seat, focus, (1f - zoom) * 0.9f);
            rot = Quaternion.LookRotation(focus - pos);
        }
    }
}
