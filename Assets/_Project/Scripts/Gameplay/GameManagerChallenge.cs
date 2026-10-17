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
        private enum ChPhase { Off, Lobby, Duel, Break, Wait, Done }

        /// <summary>The duel under way (fighting, or the countdown after a knock-down): it may be fought in TPS.</summary>
        private bool DuelTps => challengeRun && (chPhase == ChPhase.Duel || chPhase == ChPhase.Break);

        private DuelStands colosseum;
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

        private const int BoardW = 25, BoardH = 11; // (11 across the screen: the stands round its two ends in view)
        private bool liveHooksWired;

        private void InitChallenge()
        {
            chUi = ChallengeScreen.Create(ui.transform.Find("Canvas/SafeArea") ?? ui.transform.Find("Canvas") ?? ui.transform);
            chUi.CoinsChanged += ui.RefreshMenuCoins;
            // (No group tags in the duel: the soldiers all fight from the start and are never called back.)
            chUi.HeartPressed += UseDuelHeart;
            chUi.ShieldPressed += UseDuelShield;
            chUi.FriendHost += minutes => StartFriend(1, null, minutes);
            chUi.FriendJoin += code => StartFriend(2, code, 3);
            chUi.FriendClose += CloseFriend;
            chUi.FriendsPressed += ShowFriends;
            chUi.Leave += () => { chCancelled = true; chUi.HideAll(); };
            chUi.NameSubmitted += OnNameSubmitted;
            chUi.NameClosed += () => { profileNaming = false; chCancelled = true; chPhase = ChPhase.Off; chUi.HideAll(); };
            chUi.RenamePressed += () => { chCancelled = true; chPhase = ChPhase.Off; chUi.ShowName(Challenge.PlayerName); };
            chUi.ModePicked += (_, minutes) => { chDuelMinutes = minutes is 3 or 5 or 10 ? minutes : 3; chDuelSquads = Challenge.DuelSquads; chUi.HideModes(); StartChallenge(false); };
            if (liveHooksWired) return;
            // The hunt's links (made once: the hunt outlives a language change, the challenge screens do not).
            liveHooksWired = true;
            hunt.RemoteFoeStruck += () => { if (LiveActive) LiveNet.Hit(); };
            hunt.SquadHitRemote += (id, damage, byFoi) => { if (LiveActive) LiveNet.SquadHit(id, damage, byFoi); };
            hunt.SquadHitFoe += damage => { if (LiveActive) LiveNet.FoeHit(damage); };
            hunt.RivalSentGroup += group =>
            {
                if (!challengeRun || chPhase != ChPhase.Duel) return;
                chUi.SetStatus(Loc.F("ch.rivalSends", rival.name, Loc.T("army.kind." + Army.KindOf(group))));
                AudioManager.PlaySfx(Sfx.Warning, 0.5f, 1.2f);
            };
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
            Armory.RevokeLostRanks(); // a rank weapon whose rank fell is gone before the duel is built
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
                CloudSave.Paused = false; // a name picked: the online side is on again (the copy goes up)
                Challenge.Sync(profile);
                if (profileNaming) { ProfileNamed(); return; }
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
            var p = Prefs.GetString(DefenseNewsKey, "").Split('|');
            if (p.Length != 4 || !int.TryParse(p[0], out int n) || !int.TryParse(p[1], out int held) || !int.TryParse(p[2], out int fell) || !int.TryParse(p[3], out int change))
                return ""; // none yet, or an old text-shaped entry (in the language of its day)
            return Loc.F("ch.defense", n, held, fell, (change >= 0 ? "+" : "") + change);
        }

        /// <summary>While the menu is up: a friend's duel invitation is looked for now and then (see GameManagerFriends).</summary>
        private void WatchArmyFromMenu() => WatchInvites();

        private const string DefenseNewsKey = "sb_ch_defnews";

        /// <summary>The rank card was tapped: the challenge's overall results (the server's numbers first, if it answers).</summary>
        private void ShowChallengeStats()
        {
            Prefs.DeleteKey(DefenseNewsKey); // the news has been seen
            ui.MenuDefenseLine = null;
            ui.RefreshMenuRank(null);
            // At once with what the phone knows; the server's word (if it answers) refreshes it.
            ui.ChallengeStats.Show();
            if (!FoiNet.Registered) return;
            cameraRig.StartCoroutine(FoiNet.Me(p => { if (p == null) return; Challenge.Sync(p); if (ui.ChallengeStats.IsOpen) ui.ChallengeStats.Refresh(); }));
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
            if (chFriend == 0 || chFriend == 3) chUi.ShowLobby(SideText(Challenge.PlayerName, Challenge.Rank, Challenge.Trophies, Army.PowerScore), Challenge.Rank, ModeLabel);
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
                yield return FoiNet.PutArmy(points, Army.EarnedPoints, world, Challenge.FoiMaxHp, Challenge.FoiHitFor(world), 0.5f,
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
            if (chFriend == 3)
            {
                // The rival did not come back for the rematch: back to the menu.
                chUi.SetLobbyStatus(Loc.T("live.noRematch"));
                yield return new WaitForSecondsRealtime(1.8f);
                chUi.HideLobby();
                chFriend = 0;
                chPhase = ChPhase.Off;
                ShowMenu();
                yield break;
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
                chUi.SetLobbyStatus(Loc.T("ch.searching"));
                yield return FoiNet.Find(m => match = m);
                if (match != null)
                {
                    yield return FoiNet.Me(Challenge.Sync);
                    chUi.SetMyCard(SideText(Challenge.PlayerName, Challenge.Rank, Challenge.Trophies, Army.PowerScore), Challenge.Rank);
                }
            }
            float waited = Time.realtimeSinceStartup - started;
            if (waited < 1.6f) yield return new WaitForSecondsRealtime(1.6f - waited);
            if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
            // From here the match is on: no leaving the room.
            chUi.LockLobby();
            chOnline = match != null;
            chMatchId = match != null ? match.matchId : 0;
            // A virtual rival, made from the server's seed (at about the player's trophies): no real player behind it.
            int seed = match != null ? match.seed : Random.Range(0, 1000000);
            rival = Challenge.MakeBot(seed, match != null ? match.trophies : -1);
            if (!chDuelSquads) for (int g = 0; g < rival.squad.Length; g++) rival.squad[g] = 0; // foi against foi
            int rivalPower = 0;
            for (int g = 0; g < Army.Groups; g++) rivalPower += Mathf.RoundToInt(Army.Soldiers * rival.Power(g) * 10f);
            chUi.ShowRival(SideText(rival.name, rival.Rank, rival.trophies, rivalPower), rival.Rank);
            chUi.SetLobbyStatus(Loc.T(chOnline ? "ch.found" : "ch.offline"));
            yield return new WaitForSecondsRealtime(2.2f);
            if (token != chLobbyToken) yield break;
            chUi.HideLobby();
            BuildDuelBoard(Mathf.Abs(seed) % DuelStageCount);
        }

        /// <summary>The duel's name as the waiting room shows it (with its minutes).</summary>
        private string ModeLabel => Loc.T("ch.mode.duel") + " · " + Loc.F("ch.minutes", chDuelMinutes);

        /// <summary>
        /// The duel's floor: the stage's shape cut from the board (the colosseum's square, or a hexagon, octagon, circle,
        /// diamond or cross), in the look of the stage's world; the colosseum's stands round the first stage, low walls
        /// crowned with flames, ice or light round the others. No crates, no vanG, no cage.
        /// </summary>
        /// <summary>The columns where the floor's ends run whole (the stands stand along them).</summary>
        private int standFrom, standTo;

        private void BuildCourtyard()
        {
            var st = Stage;
            var rows = new string[BoardH];
            // The shape laid across the view: 21 deep (away from the camera), 15 across, cut at the camera's end and the
            // far end — so its two ends (the screen's left and right) run straight and whole, and the stands meet them.
            const int Deep = 21;
            int off = (BoardW - Deep) / 2;
            standFrom = BoardW; standTo = -1;
            for (int r = 0; r < BoardH; r++)
            {
                var row = new char[BoardW];
                for (int x = 0; x < BoardW; x++)
                {
                    bool inside = st.shape == BattleArenas.Shape.Square;
                    if (!inside && x >= off && x < off + Deep)
                    {
                        int insetZ = Mathf.RoundToInt(BattleArenas.Inset(st.shape, Deep, x - off) * BoardH / (float)Deep);
                        inside = r >= insetZ && r < BoardH - insetZ;
                    }
                    row[x] = inside ? '#' : '.';
                    if (inside && r == 0) { standFrom = Mathf.Min(standFrom, x); standTo = Mathf.Max(standTo, x); }
                }
                if (r == BoardH - 3) row[BoardW / 2] = 'S';
                rows[r] = new string(row);
            }
            levelIndex = Mathf.Clamp(st.world * LevelCatalog.LevelsPerWorld + 5, 0, LevelCount - 1);
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
            // The stands at the floor's two ends (the player's fans behind the player's mark, the rival's behind the rival's), the
            // stage's backdrop across the floor (the camera's side open).
            if (colosseum != null) Destroy(colosseum.gameObject);
            var (mark, other) = DuelMarks;
            var floorCells = new bool[BoardW, BoardH];
            for (int y = 0; y < BoardH; y++) for (int x = 0; x < BoardW; x++) floorCells[x, y] = l.layout[y][x] != '.';
            var cell0 = GridView.ToWorld(new GridPos(0, 0));
            colosseum = DuelStands.Build(transform, floorCells, new Vector3(cell0.x, 0f, cell0.z), GridView.SurfaceY - 0.02f, rival.name.GetHashCode(), mark.y < other.y, st.theme);
        }

        /// <summary>The rival's foi: foi as the rival dresses it (its own outfit; a made-up one for a computer rival), with the rival's weapon.</summary>
        private Transform RivalLook()
        {
            var root = new GameObject("RivalLook").transform;
            var outfit = !string.IsNullOrEmpty(rival.look) ? Cosmetics.OutfitFrom(rival.look) : Cosmetics.OutfitFor(rival.name);
            robot.ApplyOutfit(outfit);
            var copy = robot.BuildLookalike(root).transform;
            robot.ApplyOutfit(Cosmetics.Outfit()); // foi back in its own
            root.localScale = robot.Visual.lossyScale; // foi's own size
            // Not foi's weapon: the rival's.
            var drop = new System.Collections.Generic.List<GameObject>();
            foreach (Transform c in copy.GetComponentsInChildren<Transform>(true)) if (c.name == "HeldWeapon") drop.Add(c.gameObject);
            foreach (var g in drop) DestroyImmediate(g);
            rivalHeld = HeldWeapon.Attach(copy, Armory.Get(rival.weapon));
            return root;
        }

        private HeldWeapon rivalHeld;

        /// <summary>Each frame of a challenge: the live link, the duel being fought.</summary>
        private void UpdateChallenge(float dt)
        {
            UpdateLive();
            if (!challengeRun || State != GameState.Playing) return;
            if (chPhase == ChPhase.Duel) UpdateDuel(dt);
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
            if (levelTps) { levelTps = false; ExitTps(); } // the result card upright
            bool won = outcome > 0, draw = outcome == 0;
            // The challenge's quests: a duel played, won, won alone or against a live rival.
            ChallengeQuests.Report(DuelGoal.Play);
            if (won) { ChallengeQuests.Report(DuelGoal.Win); if (!chDuelSquads) ChallengeQuests.Report(DuelGoal.SoloWin); if (chLive) ChallengeQuests.Report(DuelGoal.LiveWin); }
            if (colosseum != null) colosseum.Cheer(won ? 0 : draw ? -1 : 1, 1f);
            hunt.Freeze();
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
            cameraRig.StartCoroutine(ShowChallengeResult(won, draw, score, gold));
        }

        /// <summary>
        /// The result card, with its trophies: a random live match's from the server (sent with its result), a match
        /// against a virtual rival's once the server has taken the result (half a live match's), a friend's: none.
        /// </summary>
        private IEnumerator ShowChallengeResult(bool won, bool draw, string score, int gold)
        {
            string trophies;
            if (chLive && liveLost) trophies = Loc.T("live.lost");
            else if (chLive && liveRanked)
            {
                if (liveProfile != null) Challenge.Sync(liveProfile);
                trophies = TrophyLine(liveChange);
            }
            else if (chLive)
            {
                if (liveProfile != null) Challenge.Sync(liveProfile);
                trophies = Loc.T("ch.friendMatch");
            }
            else if (chOnline && chMatchId != 0)
            {
                long id = chMatchId;
                chMatchId = 0;
                FoiNet.Result result = null;
                bool back = false;
                cameraRig.StartCoroutine(FoiNet.SendResult(id, won, draw, r => { result = r; back = true; }));
                float until = Time.realtimeSinceStartup + 6f;
                while (!back && Time.realtimeSinceStartup < until) yield return null;
                if (result != null) Challenge.Sync(result.profile);
                trophies = result != null ? TrophyLine(result.change) : Loc.T("ch.resultLater");
            }
            else trophies = Loc.T("ch.offline");
            string line = score + "  ·  " + Loc.F(won ? "ch.wonLine" : draw ? "ch.drawLine" : "ch.lostLine", rival.name)
                          + (chLive && liveLeft ? "\n" + Loc.T("live.rivalLeftWin") : "")
                          + "\n" + trophies + (gold > 0 ? " · +" + gold + " " + Loc.T("ch.gold") : "")
                          + "\n" + Challenge.RankName(Challenge.Rank) + " · " + Loc.F("ch.trophies", Challenge.Trophies);
            yield return ShowResultDelayed(new UIController.ResultInfo
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
                rematch = chLive && rematchMatch != 0,
            });
        }

        private static string TrophyLine(int change) => (change >= 0 ? "+" : "") + Loc.F("ch.trophies", change);

        /// <summary>A match the server knows of goes to its record (a loss when the player leaves).</summary>
        private void SendChallengeResult(bool won, bool draw = false, bool left = false)
        {
            long id = chMatchId;
            chMatchId = 0;
            if (id == 0) return;
            cameraRig.StartCoroutine(FoiNet.SendResult(id, won, draw, r => { if (r != null) Challenge.Sync(r.profile); }, left));
        }

        /// <summary>Leaving a challenge (the menu): the board and its screens go; a match the server knows of is lost.</summary>
        private void EndChallenge()
        {
            // Walked out of a duel under way: the leaver's penalty (rank points); before it began, a plain loss.
            if (chMatchId != 0 && chPhase != ChPhase.Done) SendChallengeResult(false, left: chPhase == ChPhase.Duel || chPhase == ChPhase.Break);
            chLobbyToken++; // a waiting room still searching stops there
            if (levelTps) { levelTps = false; ExitTps(); }
            EndLive();
            if (!challengeRun && chPhase == ChPhase.Off) return;
            challengeRun = false;
            chPhase = ChPhase.Off;
            ClearDuel();
            if (robot != null && robot.Visual != null) robot.Visual.gameObject.SetActive(true);
            if (chUi != null) chUi.HideAll();
            if (rivalLook != null) Destroy(rivalLook.gameObject);
            rivalLook = null;
            if (colosseum != null) Destroy(colosseum.gameObject);
            colosseum = null;
            hunt.ClearChallengeFoe();
        }

        /// <summary>
        /// The duel filmed like a football match: the camera sits high to the east at the halfway line (the player's side
        /// on the left, the rival's on the right), never turning; it keeps both foi in view (their middle, further back the
        /// further apart they are), with a still zone round the middle of the view so small steps do not shake it.
        /// </summary>
        private void BoardPose(out Vector3 pos, out Quaternion rot)
        {
            var seat = boardCentre + new Vector3(BoardW * 0.5f + 11f, 12.5f, 0f);
            var look = (boardCentre - seat).normalized;
            var me = robot.transform.position;
            var foe = hunt.ChallengeFoeAt ?? me;
            var aim = (me + foe) * 0.5f - boardCentre;
            aim.y = 0f;
            var gap = aim - duelPan;
            if (gap.magnitude > 1f) duelPan += gap.normalized * (gap.magnitude - 1f) * Mathf.Min(1f, Time.deltaTime * 2.5f);
            float apart = new Vector2(me.x - foe.x, me.z - foe.z).magnitude;
            duelBack = Mathf.Lerp(duelBack, ultimateOn ? 13f : 29f + Mathf.Clamp(apart - 3f, 0f, 14f) * 1.2f, Time.unscaledDeltaTime * (ultimateOn ? 5f : 2f)); // (the ultimate: close in)
            pos = boardCentre + duelPan - look * duelBack;
            rot = Quaternion.LookRotation(look);
            OrbitPose(ref pos, ref rot, me, foe);
        }

        // ---------- The slow-motion camera ----------

        /// <summary>Until when (unscaled time) a big moment's slow motion is filmed close, and round what.</summary>
        private float slowCamUntil, slowCamWeight, slowCamAngle;
        private Vector3 slowCamCentre;

        /// <summary>The camera goes in close and circles the two foi while the duel is in slow motion (a big blow, the ultimate).</summary>
        private bool SlowCamOn => challengeRun && (ultimateOn || Time.unscaledTime < slowCamUntil || slowCamWeight > 0f);

        /// <summary>
        /// A big moment is playing (the slow motion and the camera circling the two): until it is over nobody lands a
        /// blow — only the move itself — and a live duel's health changes wait for its end.
        /// </summary>
        private bool DuelCinematic => challengeRun && chPhase == ChPhase.Duel && (ultimateOn || Time.unscaledTime < slowCamUntil);

        /// <summary>A big moment: film it close, circling <paramref name="centre"/>, for <paramref name="seconds"/>.</summary>
        private void SlowCam(Vector3 centre, float seconds)
        {
            slowCamCentre = centre;
            slowCamUntil = Mathf.Max(slowCamUntil, Time.unscaledTime + seconds);
        }

        /// <summary>Blends the board's view into the circling close one (and back out) while the slow motion lasts.</summary>
        private void OrbitPose(ref Vector3 pos, ref Quaternion rot, Vector3 me, Vector3 foe)
        {
            float dt = Time.unscaledDeltaTime;
            bool on = ultimateOn || Time.unscaledTime < slowCamUntil;
            var centre = Time.unscaledTime < slowCamUntil ? Vector3.Lerp(slowCamCentre, (me + foe) * 0.5f, 0.35f) : (me + foe) * 0.5f;
            if (slowCamWeight <= 0f && on)
            {
                // It starts circling from where the board's camera stands.
                var from = pos - centre;
                slowCamAngle = Mathf.Atan2(from.z, from.x) * Mathf.Rad2Deg;
            }
            slowCamWeight = Mathf.MoveTowards(slowCamWeight, on ? 1f : 0f, dt * (on ? 2.6f : 1.8f));
            if (slowCamWeight <= 0f) return;
            slowCamAngle += dt * 70f;
            float a = slowCamAngle * Mathf.Deg2Rad;
            float apart = Mathf.Clamp(new Vector2(me.x - foe.x, me.z - foe.z).magnitude, 0f, 6f);
            float radius = 6.4f + apart * 0.5f; // far enough to see both whole, and the moment round them
            var orbit = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * (4.3f + apart * 0.25f);
            // Never into the stands: it keeps over the floor (the stands rise along its long sides).
            orbit.x = Mathf.Clamp(orbit.x, boardCentre.x - BoardW * 0.5f, boardCentre.x + BoardW * 0.5f);
            orbit.z = Mathf.Clamp(orbit.z, boardCentre.z - BoardH * 0.5f + 0.5f, boardCentre.z + BoardH * 0.5f - 0.5f);
            var orbitRot = Quaternion.LookRotation(centre + Vector3.up * 0.55f - orbit);
            float w = Mathf.SmoothStep(0f, 1f, slowCamWeight);
            pos = Vector3.Lerp(pos, orbit, w);
            rot = Quaternion.Slerp(rot, orbitRot, w);
        }
    }
}
