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
    /// The challenge (main menu → CHALLENGE): the first time, the player picks a name; then the kind — the Army Battle or
    /// the Duel (3, 5 or 10 minutes). The waiting room looks for a live rival who picked the same for a while; nobody
    /// there: a real player's army or foi played by the AI (or a bot) — a match for the record only, no trophies. Only a
    /// random live match moves trophies; a friend's match (by code) is for the record too. Both kinds are fought in the
    /// arena: the player on the left (the south end, as the camera on the east stand sees it), the rival on the right.
    /// </summary>
    public partial class GameManager
    {
        private enum ChPhase { Off, Lobby, Army, Duel, Break, Wait, Done }

        /// <summary>Which kind of challenge is being played: the armies' battle, or the foi's duel.</summary>
        private enum ChMode { Army, Duel }

        private ChMode chMode = ChMode.Army;
        private Colosseum colosseum;
        private float zoom = 1f;

        private bool challengeRun;
        private ChPhase chPhase = ChPhase.Off;
        private Challenge.Opponent rival;
        private ChallengeScreen chUi;
        private Transform rivalLook;
        private Vector3 boardCentre;
        private bool chCancelled;
        /// <summary>Bumped by every waiting room, so an old one that is still waiting knows it is over.</summary>
        private int chLobbyToken;
        /// <summary>The server's challenge being played against the AI (0: none, or a practice match).</summary>
        private long chMatchId;
        private bool chOnline;
        /// <summary>How far the view has slid over the courtyard.</summary>
        private Vector3 boardPan;
        private Vector3 duelPan;
        private float duelBack = 21f;

        private const int BoardW = 25, BoardH = 21;
        private bool liveHooksWired;

        /// <summary>The Army Battle is on (the stick and taps lead the active group, not foi).</summary>
        private bool ChallengeArmyPhase => challengeRun && chMode == ChMode.Army && chPhase != ChPhase.Off && chPhase != ChPhase.Done;

        private void InitChallenge()
        {
            chUi = ChallengeScreen.Create(ui.transform.Find("Canvas/SafeArea") ?? ui.transform.Find("Canvas") ?? ui.transform);
            chUi.GroupPicked += g => { if (ChallengeArmyPhase && army != null) army.SetActive(0, g); };
            chUi.FriendHost += (mode, minutes) => StartFriend(1, null, mode, minutes);
            chUi.FriendJoin += code => StartFriend(2, code, 0, 3);
            chUi.FriendClose += CloseFriend;
            chUi.Leave += () => { chCancelled = true; chUi.HideAll(); };
            chUi.NameSubmitted += OnNameSubmitted;
            chUi.NameClosed += () => { chCancelled = true; chPhase = ChPhase.Off; chUi.HideAll(); };
            chUi.RenamePressed += () => { chCancelled = true; chPhase = ChPhase.Off; chUi.ShowName(Challenge.PlayerName); };
            chUi.ModePicked += (m, minutes) => { chMode = m == 1 ? ChMode.Duel : ChMode.Army; chDuelMinutes = minutes; chUi.HideModes(); StartChallenge(false); };
            if (army != null) Destroy(army.gameObject); // built again after a language change: one board only
            army = new GameObject("ArmyBattle").AddComponent<ArmyBattle>();
            army.Init(fx);
            army.HitRemote += (index, damage) => { if (LiveActive) LiveNet.ArmyHit(index, damage); };
            if (!liveHooksWired) { liveHooksWired = true; hunt.RemoteFoeStruck += () => { if (LiveActive) LiveNet.Hit(); }; }
        }

        /// <summary>CHALLENGE on the menu.</summary>
        private void StartChallenge() => StartChallenge(true);

        /// <param name="chooseMode">Ask which kind; false: the kind already picked (playing again).</param>
        private void StartChallenge(bool chooseMode)
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
            if (chooseMode)
            {
                chPhase = ChPhase.Off;
                chUi.ShowModes();
                return;
            }
            chCancelled = false;
            chFriend = 0;
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

        /// <summary>True while the challenge's briefing cards are up (the match waits).</summary>
        private bool chBriefing;

        /// <summary>
        /// The challenge's briefing: the floors' tap-through cards over the kind's own picture (ch.brief.KIND.0 …), then
        /// <paramref name="then"/>. Leaving the challenge meanwhile drops it.
        /// </summary>
        private IEnumerator ChallengeBriefing(string kind, int cards, System.Action then)
        {
            var texts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < cards; i++) texts.Add(Loc.T("ch.brief." + kind + "." + i));
            bool done = false;
            void OnDone() => done = true;
            chBriefing = true;
            ui.Briefing.Finished += OnDone;
            try
            {
                ui.Briefing.Show(texts, Resources.Load<Texture2D>("ChallengeModes/" + kind));
                while (!done && challengeRun) yield return null;
            }
            finally
            {
                ui.Briefing.Finished -= OnDone;
                chBriefing = false;
            }
            if (challengeRun) then();
        }

        /// <summary>Tells the server whether challenges are on, the phone's notification token and the language.</summary>
        private void SyncChallengeSettings()
        {
            ui.RefreshSettings();
            ui.RefreshMenuRank(ui.MenuDefenseLine);
            cameraRig.StartCoroutine(FoiNet.PutSettings(Challenge.Enabled, Notifications.Token, FoiNet.LangCode, _ => { }));
        }

        private float menuWatchNext;

        /// <summary>The last defence news, in the current language (empty when there is none).</summary>
        private static string DefenseNews()
        {
            var p = PlayerPrefs.GetString(DefenseNewsKey, "").Split('|');
            if (p.Length != 4 || !int.TryParse(p[0], out int n) || !int.TryParse(p[1], out int held) || !int.TryParse(p[2], out int fell) || !int.TryParse(p[3], out int change))
                return ""; // none yet, or an old text-shaped entry (in the language of its day)
            return Loc.F("ch.defense", n, held, fell, (change >= 0 ? "+" : "") + change);
        }

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
                    // Kept as numbers (written out in whatever language is on when it is shown again).
                    PlayerPrefs.SetString(DefenseNewsKey, items.Length + "|" + held + "|" + fell + "|" + change);
                }
                else line = DefenseNews();
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
        /// The waiting room: the player's army and foi go up to the server, a live rival is looked for (a friend waited
        /// for), else a rival played by the AI is found; the rival's card, then onto the board.
        /// </summary>
        private IEnumerator Lobby()
        {
            int token = ++chLobbyToken;
            chMatchId = 0;
            chOnline = false;
            chLive = false;
            // (Playing a friend, the friend panel stays up until the friend is there.)
            if (chFriend == 0) chUi.ShowLobby(SideText(Challenge.PlayerName, Challenge.Rank, Challenge.Trophies, Army.PowerScore), Challenge.Rank, ModeLabel);
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
                    chUi.HideFriend();
                    chUi.ShowName(Challenge.PlayerName, Loc.T("ch.name." + err));
                    yield break;
                }
                if (err == "offline") ok = false;
            }
            if (ok)
            {
                // A result lost to the network goes up first; then the settings (on / off, language) the server pushes by.
                yield return FoiNet.SendPending(r => { if (r != null) Challenge.Sync(r.profile); });
                yield return FoiNet.PutSettings(Challenge.Enabled, Notifications.Token, FoiNet.LangCode, _ => { });
                if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
                ok = FoiNet.Registered;
            }
            if (ok)
            {
                var points = new int[Army.Groups];
                for (int g = 0; g < Army.Groups; g++) points[g] = Army.GroupPoints(g);
                yield return FoiNet.PutArmy(points, Army.EarnedPoints, world, Challenge.FoiHpFor(world), Challenge.FoiHitFor(world), 0.5f,
                    Armory.Equipped.id, Armory.Level(Armory.Equipped), Shop.ArmorShare, Shop.Level(Upgrade.Shield) > 0 ? Shop.ShieldHits : 0, r => ok = r);
                if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
            }
            // A live rival first (a friend's code: only that).
            if (ok)
            {
                LiveNet.Msg matched = null;
                yield return LiveSearch(token, m => matched = m);
                if (LobbyOver(token)) { LiveNet.Close(); chPhase = ChPhase.Off; yield break; }
                if (matched != null) { yield return StartLive(matched, world, token); yield break; }
            }
            if (chFriend != 0)
            {
                // A friend's match only: no AI rival instead (the panel says what happened).
                if (!ok) { chUi.SetFriendStatus(Loc.T("live.offline")); chUi.SetFriendBusy(false); }
                chPhase = ChPhase.Off;
                yield break;
            }
            FoiNet.Match match = null;
            if (ok)
            {
                FoiNet.Attack[] attacks = null;
                yield return FoiNet.Defense((_, a) => attacks = a);
                if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
                chUi.SetLobbyStatus(Loc.T("live.aiRival"));
                yield return FoiNet.Find(m => match = m);
                if (match != null)
                {
                    yield return FoiNet.Me(Challenge.Sync);
                    chUi.SetMyCard(SideText(Challenge.PlayerName, Challenge.Rank, Challenge.Trophies, Army.PowerScore), Challenge.Rank);
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
            if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
            // From here the match is on: no leaving the room.
            chUi.LockLobby();
            chOnline = match != null;
            chMatchId = match != null ? match.matchId : 0;
            rival = match != null && match.HasRival
                ? Challenge.FromRival(match.rival)
                : Challenge.MakeBot(match != null ? match.seed : Random.Range(0, 1000000), Army.EarnedPoints, world, match != null ? match.trophies : -1);
            int rivalPower = 0;
            for (int g = 0; g < Army.Groups; g++) rivalPower += Mathf.RoundToInt(Army.Soldiers * rival.Power(g) * 10f);
            chUi.ShowRival(SideText(rival.name, rival.Rank, rival.trophies, rivalPower), rival.Rank);
            chUi.SetLobbyStatus(Loc.T(chOnline ? "ch.aiMatch" : "ch.offline"));
            yield return new WaitForSecondsRealtime(2.2f);
            if (token != chLobbyToken) yield break;
            chUi.HideLobby();
            if (chMode == ChMode.Duel) BuildDuelBoard(world);
            else BuildArmyBoard(world);
        }

        /// <summary>The mode's name as the waiting room shows it (the duel with its minutes).</summary>
        private string ModeLabel => chMode == ChMode.Duel ? Loc.T("ch.mode.duel") + " · " + Loc.F("ch.minutes", chDuelMinutes) : Loc.T("ch.mode.army");

        /// <summary>The courtyard: a plain floor in the world's look, ramparts round it, no crates, no vanG, no cage.</summary>
        private void BuildCourtyard(int world)
        {
            var rows = new string[BoardH];
            for (int r = 0; r < BoardH; r++)
            {
                var row = new char[BoardW];
                for (int x = 0; x < BoardW; x++) row[x] = '#';
                if (r == BoardH - 3) row[BoardW / 2] = 'S';
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
            l.reactor = false; // no reactor tower (and its repair drones) from the late worlds on the board
            l.armor = 0; // every blow counts in full (the duel's damage is the weapon's own)
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
            coins.Stop(); // no coins or power-up crates on the challenge's floor
            powerUps.Stop();
            fifiReadyAt = Time.time + 99999f; // Fifi stays out of challenges
            introPending = false;
            boardCentre = GridView.ToWorld(new GridPos(BoardW / 2, BoardH / 2)) + Vector3.up * GridView.SurfaceY;
            boardPan = Vector3.zero;
            zoom = 1f;
            // The arena round the courtyard: tiers, gates, torches, banners and the crowd.
            if (colosseum != null) Destroy(colosseum.gameObject);
            colosseum = Colosseum.Build(transform, GridView.ToWorld(new GridPos(BoardW / 2, BoardH / 2)), BoardW * 0.5f, BoardH * 0.5f, GridView.SurfaceY - 0.02f, rival.name.GetHashCode());
        }

        /// <summary>A copy of foi in the rival's red, with the rival's weapon, for the rival's foi.</summary>
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
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), ArmyBattle.Theirs, 0.45f));
            }
            rivalHeld = HeldWeapon.Attach(copy, Armory.Get(rival.weapon));
            return root;
        }

        private HeldWeapon rivalHeld;

        /// <summary>Each frame of a challenge: the live link, the match being played.</summary>
        private void UpdateChallenge(float dt)
        {
            UpdateLive();
            if (!challengeRun || State != GameState.Playing) return;
            switch (chPhase)
            {
                case ChPhase.Army: UpdateArmy(dt); break;
                case ChPhase.Duel: UpdateDuel(dt); break;
            }
        }

        /// <summary>The match is decided: the final score.</summary>
        private void ChallengeEnd()
        {
            int me = ScoreMine, them = ScoreTheirs;
            ChallengeFinish(me > them ? 1 : me < them ? -1 : 0, me + " - " + them);
        }

        /// <param name="outcome">1 won, 0 a draw, -1 lost.</param>
        private void ChallengeFinish(int outcome, string score)
        {
            if (chPhase == ChPhase.Done) return;
            chPhase = ChPhase.Done;
            bool won = outcome > 0, draw = outcome == 0;
            if (colosseum != null) colosseum.Cheer(won ? 0 : draw ? -1 : 1, 1f);
            if (army != null) army.Halt();
            hunt.Freeze();
            // Trophies only in a random live match (the server's numbers); against the AI or a friend: the record only.
            string trophies;
            if (chLive && liveLost) trophies = Loc.T("live.lost");
            else if (chLive && liveRanked)
            {
                if (liveProfile != null) Challenge.Sync(liveProfile);
                trophies = (liveChange >= 0 ? "+" : "") + Loc.F("ch.trophies", liveChange);
            }
            else if (chLive)
            {
                if (liveProfile != null) Challenge.Sync(liveProfile);
                trophies = Loc.T("ch.friendMatch");
            }
            else if (chOnline)
            {
                SendChallengeResult(won, draw);
                trophies = Loc.T("ch.aiMatch");
            }
            else trophies = Loc.T("ch.offline");
            int gold = won && (chOnline || chLive) ? Challenge.WinGold : 0; // offline practice pays no prize
            SaveData.Coins += draw ? 0 : gold + Earned;
            State = GameState.Result;
            if (won)
            {
                robot.Cheer();
                AudioManager.PlaySfx(Sfx.Win);
                fx.Burst(robot.transform.position + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 40, 6f);
            }
            chUi.HideAll();
            string line = score + "  ·  " + Loc.F(won ? "ch.wonLine" : draw ? "ch.drawLine" : "ch.lostLine", rival.name)
                          + (chLive && liveLeft ? "\n" + Loc.T("live.rivalLeftWin") : "")
                          + "\n" + trophies + (gold > 0 ? " · +" + gold + " " + Loc.T("ch.gold") : "")
                          + "\n" + Challenge.RankName(Challenge.Rank) + " · " + Loc.F("ch.trophies", Challenge.Trophies);
            StartCoroutine(ShowResultDelayed(new UIController.ResultInfo
            {
                won = won,
                bonusRound = true,
                arena = true,
                title = Loc.T(won ? "ch.victory" : draw ? "ch.draw" : "ch.defeat"),
                hasNext = false,
                canDouble = false,
                challenge = true,
                subtitle = line,
                coins = draw ? 0 : gold + Earned,
            }));
        }

        /// <summary>A match against the AI goes to the server's record (no trophies there).</summary>
        private void SendChallengeResult(bool won, bool draw = false)
        {
            long id = chMatchId;
            chMatchId = 0;
            if (id == 0) return;
            cameraRig.StartCoroutine(FoiNet.SendResult(id, won, draw, r => { if (r != null) Challenge.Sync(r.profile); }));
        }

        /// <summary>Leaving a challenge (the menu): the board and its screens go; a match the server knows of is lost.</summary>
        private void EndChallenge()
        {
            if (chMatchId != 0 && chPhase != ChPhase.Done) SendChallengeResult(false);
            chLobbyToken++; // a waiting room still searching stops there
            EndLive();
            if (!challengeRun && chPhase == ChPhase.Off) return;
            challengeRun = false;
            chPhase = ChPhase.Off;
            ClearDuel();
            if (army != null) army.Clear();
            if (robot != null && robot.Visual != null) robot.Visual.gameObject.SetActive(true);
            if (chUi != null) chUi.HideAll();
            if (rivalLook != null) Destroy(rivalLook.gameObject);
            rivalLook = null;
            if (colosseum != null) Destroy(colosseum.gameObject);
            colosseum = null;
            hunt.ClearChallengeFoe();
        }

        /// <summary>
        /// The challenge filmed like a football match: the camera sits high on the east stand at the halfway line (the
        /// player's side on the left, the rival's on the right). In the Army Battle it follows the player's active group
        /// (closing in when the two active groups meet); in the Duel it slides gently after foi.
        /// </summary>
        private void BoardPose(out Vector3 pos, out Quaternion rot)
        {
            var seat = boardCentre + new Vector3(BoardW * 0.5f + 11f, 12.5f, 0f);
            var look = (boardCentre - seat).normalized;
            if (chMode == ChMode.Duel)
            {
                // Never turning: it keeps both foi in view (their middle, further back the further apart they are), with a
                // still zone round the middle of the view so small steps do not shake it.
                var me = robot.transform.position;
                var foe = hunt.ChallengeFoeAt ?? me;
                var aim = (me + foe) * 0.5f - boardCentre;
                aim.y = 0f;
                var gap = aim - duelPan;
                if (gap.magnitude > 1f) duelPan += gap.normalized * (gap.magnitude - 1f) * Mathf.Min(1f, Time.deltaTime * 2.5f);
                float apart = new Vector2(me.x - foe.x, me.z - foe.z).magnitude;
                duelBack = Mathf.Lerp(duelBack, 17f + Mathf.Clamp(apart - 3f, 0f, 14f) * 1.4f, Time.deltaTime * 2f);
                pos = boardCentre + duelPan - look * duelBack;
                rot = Quaternion.LookRotation(look);
                return;
            }
            Vector3 want = boardCentre;
            bool close = false;
            if (army != null && chPhase == ChPhase.Army)
            {
                var mine = army.GroupCentre(0, army.Active(0));
                var theirs = army.GroupCentre(1, army.Active(1));
                close = (mine - theirs).sqrMagnitude < 7f * 7f;
                want = close ? (mine + theirs) * 0.5f : mine;
            }
            var w = want - boardCentre;
            w = new Vector3(Mathf.Clamp(w.x, -BoardW * 0.45f, BoardW * 0.45f), 0f, Mathf.Clamp(w.z, -BoardH * 0.48f, BoardH * 0.48f));
            boardPan = Vector3.Lerp(boardPan, w, Time.deltaTime * 1.6f);
            zoom = Mathf.Lerp(zoom, close ? 0.7f : 0.85f, Time.deltaTime * 1.2f);
            var focus = boardCentre + boardPan;
            // The seat slides along the stand only (never into it); closing in is along the line of sight.
            pos = Vector3.Lerp(seat + new Vector3(0f, 0f, boardPan.z), focus, (1f - zoom) * 0.9f);
            // Aimed a little short (towards the camera), so the fight sits above the group bar at the bottom.
            rot = Quaternion.LookRotation(focus + new Vector3(0.9f, 0f, 0f) - pos);
        }
    }
}
