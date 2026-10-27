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
        private bool winnerWalking;

        /// <summary>
        /// The duel is over: the winner walks to the middle of the platform and dances there to the camera; the other steps
        /// back to its own side and turns to watch.
        /// </summary>
        private IEnumerator WinnerToMiddle(bool won)
        {
            // (the rival moved by its root: its model stays on it, so turning it never swings the model off)
            var foeBody = hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.parent ?? hunt.ChallengeFoeModel : null;
            if (foeBody != null && hunt.ChallengeFoeModel != foeBody) hunt.ChallengeFoeModel.localPosition = Vector3.zero;
            var winner = won ? robot.transform : foeBody;
            var loser = won ? foeBody : robot.transform;
            var winnerFace = won ? robot.Visual : hunt.ChallengeFoeModel;
            var loserFace = won ? hunt.ChallengeFoeModel : robot.Visual;
            try
            {
                if (winner == null) yield break;
                DropRings(); // (the duel's rings go: the dance is the show)
                var mid = boardCentre; mid.y = winner.position.y;
                Vector3 loserFrom = loser != null ? loser.position : mid, loserTo = loserFrom;
                if (loser != null)
                {
                    var away = loserFrom - mid; away.y = 0f;
                    if (away.sqrMagnitude < 0.25f) away = Vector3.forward * (loserFrom.z >= mid.z ? 1f : -1f);
                    loserTo = mid + away.normalized * 4.5f; loserTo.y = loserFrom.y; // (its own side, back from the middle)
                }
                var winnerFrom = winner.position;
                var wSkin = winnerFace != null ? winnerFace.GetComponentInChildren<SkinnedHero>() : null;
                var lSkin = loserFace != null ? loserFace.GetComponentInChildren<SkinnedHero>() : null;
                if (wSkin != null) wSkin.StandUp();
                if (lSkin != null) lSkin.StandUp();
                const float T = 1.1f;
                for (float e = 0f; e < T; e += Time.unscaledDeltaTime)
                {
                    if (!challengeRun || winner == null) yield break;
                    float k = Mathf.SmoothStep(0f, 1f, e / T);
                    float bob = Mathf.Abs(Mathf.Sin(e * 14f)) * 0.06f; // (a walk's light step)
                    var wNow = Vector3.Lerp(winnerFrom, mid, k);
                    Face(winnerFace, mid - winnerFrom);
                    winner.position = wNow + Vector3.up * bob;
                    if (loser != null)
                    {
                        loser.position = Vector3.Lerp(loserFrom, loserTo, k) + Vector3.up * bob;
                        Face(loserFace, loserTo - loserFrom);
                    }
                    yield return null;
                }
                winner.position = mid;
                if (loser != null) { loser.position = loserTo; Face(loserFace, mid - loserTo); } // (it watches the dance)
                var cam = Camera.main != null ? Camera.main.transform.position : mid + Vector3.right * 10f;
                Face(winnerFace, cam - mid); // (dancing to the camera)
                fx.Burst(mid + Vector3.up * 0.6f, Palette.Coin, Palette.CoinGlow, 40, 6f);
                if (won) robot.Cheer(); // (its dance, if it has one: claps otherwise)
                else if (wSkin != null) wSkin.Celebrate(rival.bot ? "dance.d" + (1 + Mathf.Abs(rival.name.GetHashCode()) % 4) : null); // the rival's dance of its own
                if (lSkin != null) lSkin.Defeated(); // (the other down on its knees, beaten)
                winnerWalking = false;
                // through the dance: the winner kept turned to the duel's camera, the other to the winner (nothing else turns them)
                for (float e = 0f; e < 10f && challengeRun && State == GameState.Result && winner != null; e += Time.unscaledDeltaTime)
                {
                    Face(winnerFace, cam - winner.position);
                    if (loser != null) Face(loserFace, winner.position - loser.position);
                    yield return null;
                }
            }
            finally { winnerWalking = false; }
        }

        /// <summary>foi or the rival turned (flat) along a direction (the rival by its root).</summary>
        private void Face(Transform model, Vector3 dir)
        {
            dir.y = 0f;
            if (model == null || dir.sqrMagnitude < 1e-4f) return;
            if (model == robot.Visual) robot.Face(robot.transform.position + dir);
            else hunt.TurnFoe(dir);
        }

        /// <summary>The duel's winner dancing (null: none): the camera comes in closer on it.</summary>
        private Transform danceAt;
        private float danceCam;
        private float duelBack = 21f;

        private const int BoardW = 25, BoardH = 11; // (11 across the screen: the stands round its two ends in view)
        private bool liveHooksWired;

        private static bool footDustWired;

        private void InitChallenge()
        {
            // the struck bodies' dragged feet and landings raise dust (the reactions' library)
            if (!footDustWired) { footDustWired = true; SkinnedHero.FootDust += p => { if (fx != null) fx.Dust(p, new Color(0.62f, 0.58f, 0.52f), 10, 2.2f); }; }
            // the duel's lunges: each fighter's weapon aims at the other (the contact rule)
            HeldWeapon.TargetOf = h =>
            {
                if (!challengeRun || hunt == null || h == null) return null;
                var foeModel = hunt.ChallengeFoeModel;
                if (h == heldWeapon) return foeModel;
                return foeModel != null && h.transform.IsChildOf(foeModel) ? robot.Visual : null;
            };
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
            chUi.TrainingPressed += () => StartTraining(null);
            chUi.ModePicked += (mode, minutes) =>
            {
                chUi.HideModes();
                if (mode >= 2 && mode <= 3) { cameraRig.StartCoroutine(EventDuel(mode)); return; } // the weekly rule mode, the weekend tournament
                if (TutorialDue) { StartTutorial(); return; } // (a new player's first duel: the guided one)
                chMode = 1; chDuelMinutes = 3; chDuelSquads = Challenge.DuelSquads; StartChallenge(false);
            };
            if (liveHooksWired) return;
            // The hunt's links (made once: the hunt outlives a language change, the challenge screens do not).
            liveHooksWired = true;
            hunt.RemoteFoeStruck += () => { if (LiveActive) LiveNet.Hit(Time.time < myJumpUntil, heldWeapon != null && heldWeapon.Current != null ? (int)heldWeapon.Current.height : 1); }; // (in the air: the server adds the air blow's extra; its height: a crouch lets high ones by)
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
        private IEnumerator Lobby() => Lobby(null);

        /// <param name="ahead">A rival found ahead (PLAY AGAIN on the last duel's rewards): straight to it, no search.</param>
        private IEnumerator Lobby(FoiNet.Match ahead)
        {
            copyMatch = false;
            chMode = 1; duelRule = null;
            int token = ++chLobbyToken;
            chMatchId = 0;
            chOnline = false;
            chLive = false;
            // (Playing a friend, the friend panel stays up until the friend is there.)
            if (chFriend == 0 || chFriend == 3) chUi.ShowLobby(MyCard(), Challenge.Rank, ModeLabel);
            float started = Time.realtimeSinceStartup;
            int world = LevelCatalog.WorldOf(Mathf.Max(0, SaveData.UnlockedLevel - 1));

            bool ok = false;
            if (ahead != null) { yield return LobbyAhead(ahead, token, started); yield break; }
            yield return FoiNet.EnsurePlayer(r => ok = r);
            if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
            if (ok) yield return FoiNet.CancelAhead(); // (a rival found ahead and never fought: dropped, not a loss)
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
                    Armory.Equipped.id, RobotParts.Level(RobotPart.Arms), RobotParts.Armor, Shields.All.IndexOf(Shields.Equipped) + 1, r => ok = r); // (the shield field: the carried shield, 1 wooden … 4 amethyst) // (the weapon level's field carries the arms' level)
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
                    chUi.SetMyCard(MyCard(), Challenge.Rank);
                }
            }
            yield return LobbyFound(match, token, started, 1.6f);
        }

        /// <summary>The rival found (or none: offline): its card, the stage wheel, onto the board. <paramref name="pause"/>: the
        /// least time the room stays (a rival found ahead: short).</summary>
        private IEnumerator LobbyFound(FoiNet.Match match, int token, float started, float pause)
        {
            float waited = Time.realtimeSinceStartup - started;
            if (waited < pause) yield return new WaitForSecondsRealtime(pause - waited);
            if (LobbyOver(token)) { chPhase = ChPhase.Off; yield break; }
            // From here the match is on: no leaving the room.
            chUi.LockLobby();
            chOnline = match != null;
            chMatchId = match != null ? match.matchId : 0;
            // A virtual rival, made from the server's seed (at about the player's trophies): no real player behind it.
            int seed = match != null ? match.seed : Random.Range(0, 1000000);
            var again = rematchBot; // (the computer rival asked for a rematch: the same one again)
            rematchBot = null;
            rival = again ?? Challenge.MakeBot(seed, match != null ? match.trophies : -1);
            if (!chDuelSquads) for (int g = 0; g < rival.squad.Length; g++) rival.squad[g] = 0; // foi against foi
            int rivalPower = 0;
            for (int g = 0; g < Army.Groups; g++) rivalPower += Mathf.RoundToInt(Army.Soldiers * rival.Power(g) * 10f);
            chUi.ShowRival(RivalCard(rival), rival.Rank);
            chUi.SetLobbyStatus(Loc.T(chOnline ? "ch.found" : "ch.offline"));
            yield return new WaitForSecondsRealtime(pause < 1f ? 0.7f : 1.6f);
            if (token != chLobbyToken) yield break;
            int stage = again != null ? duelStage : PickedStage(Mathf.Abs(seed) % DuelStageCount);
            if (match != null) FoiNet.AheadUsed(); // (a rival found ahead is fought now)
            yield return SpinToStage(stage); // every stage flashes by, it stops on the one drawn
            if (token != chLobbyToken) yield break;
            chUi.HideLobby();
            BuildDuelBoard(stage);
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
            int Deep = DuelStands.IsMade(st.theme) ? 15 : BoardW; // every stage the board's whole length (all about the same size, big); the grove a round floor on its round platform
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
            robot.SetSize(1.58f, 0.1f); // foi big in the duels (the camera stands well back to show the whole stage)
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
            if (duelSky) { duelSky = false; cameraRig.SetBackdrop(null); }
            var (mark, other) = DuelMarks;
            var floorCells = new bool[BoardW, BoardH];
            for (int y = 0; y < BoardH; y++) for (int x = 0; x < BoardW; x++) floorCells[x, y] = l.layout[y][x] != '.';
            var cell0 = GridView.ToWorld(new GridPos(0, 0));
            // the stands: the player's filled by its rescued foi, the rival's by its own (a computer rival's about the player's)
            int mine = Rescued.Count, theirs = rival.world > 0 ? rival.world * Rescued.PerWorld : Mathf.Clamp(mine + (rival.seed % 5 - 2) * Rescued.PerWorld, 0, 1000);
            DuelStands.Fill[0] = Rescued.Fill(mine); DuelStands.FlagShare[0] = Rescued.Flags(mine);
            DuelStands.Fill[1] = Rescued.Fill(theirs); DuelStands.FlagShare[1] = Rescued.Flags(theirs);
            colosseum = DuelStands.Build(transform, floorCells, new Vector3(cell0.x, 0f, cell0.z), GridView.SurfaceY - 0.02f, rival.name.GetHashCode(), mark.y < other.y, st.theme);
            if (DuelStands.IsMade(st.theme))
            {
                // A made stage brings its own floor, land and sky: the tiles hidden; the grove's moonlit night, the glacier's
                // cold bright day.
                var light = DuelStands.LightOf(st.theme);
                gridView.Ghost();
                cameraRig.SetBackdrop(DuelStands.SkyOf(st.theme));
                duelSky = true;
                foreach (var lamp in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (lamp.type == LightType.Directional)
                    {
                        lamp.color = light.sun;
                        lamp.intensity = light.power;
                        break;
                    }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = light.sky;
                RenderSettings.ambientEquatorColor = light.horizon;
                RenderSettings.ambientGroundColor = light.ground;
            }
            // The stage's own land all round the stands and its own sky (the city stadium keeps the city).
            else if (st.theme != DuelStands.Theme.City)
            {
                var land = st.theme switch
                {
                    DuelStands.Theme.Glacier => DuctRunner.Kind.Snow,
                    DuelStands.Theme.Volcano => DuctRunner.Kind.Lava,
                    DuelStands.Theme.Space => DuctRunner.Kind.Void,
                    DuelStands.Theme.Forest => DuctRunner.Kind.Forest,
                    _ => DuctRunner.Kind.Canyon,
                };
                cameraRig.SetBackdrop(FloorBiome.Build(colosseum.transform, grid, land, rival.name.GetHashCode() & 0xffff, duel: true));
                duelSky = true;
            }
        }

        /// <summary>The duel's land put its own sky up (put back when the duel ends).</summary>
        private bool duelSky;

        /// <summary>The rival's foi: foi as the rival dresses it (its own outfit; a made-up one for a computer rival), with the rival's weapon.</summary>
        private Transform RivalLook()
        {
            var root = new GameObject("RivalLook").transform;
            var outfit = rival.bot ? Cosmetics.OutfitFor(rival.name) : Cosmetics.OutfitFrom(rival.look); // a player's own (plain if it wears nothing), a made-up one for the computer
            robot.ApplyOutfit(outfit);
            var copy = robot.BuildLookalike(root).transform;
            // The copy shares foi's paint and eye materials: give it its own before foi dresses back.
            var own = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (!own.TryGetValue(mats[i], out var m)) own[mats[i]] = m = new Material(mats[i]);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
            robot.ApplyOutfit(Cosmetics.Outfit()); // foi back in its own
            root.localScale = robot.Visual.lossyScale; // foi's own size
            // Not foi's weapon: the rival's.
            var drop = new System.Collections.Generic.List<GameObject>();
            foreach (Transform c in copy.GetComponentsInChildren<Transform>(true)) if (c.name == "HeldWeapon" || c.name == "HeldShield") drop.Add(c.gameObject); // (not foi's shield either)
            foreach (var g in drop) DestroyImmediate(g);
            foreach (var sh in copy.GetComponentsInChildren<ShieldHold>(true)) DestroyImmediate(sh); // (the rival carries no shield of foi's)
            rivalHeld = HeldWeapon.Attach(copy, Armory.Get(rival.weapon));
            var rivalSkin = copy.GetComponentInChildren<SkinnedHero>(true);
            if (rivalSkin != null) PartLooks.Apply(rivalSkin.transform, rival.Body, rival.Arms, rival.Shell); // its own parts' look
            return root;
        }

        private HeldWeapon rivalHeld;

        /// <summary>Each frame of a challenge: the live link, the duel being fought.</summary>
        private void UpdateChallenge(float dt)
        {
            UpdatePhysical();
            LandDeadline();
            UpdateLive();
            UpdateDuelLab();
            if (!challengeRun || State != GameState.Playing) return;
            if (chPhase == ChPhase.Duel) UpdateDuel(dt);
        }

        /// <summary>The match is decided: the final score.</summary>
        private void ChallengeEnd()
        {
            int me = ScoreMine, them = ScoreTheirs;
            ChallengeFinish(me > them ? 1 : me < them ? -1 : 0, me + " - " + them);
        }

        /// <summary>The chest the last duel brought (none: lost, or a draw).</summary>
        private ChestTier? chestWon;
        /// <summary>The last duel's core parts, and whether it was the day's first win.</summary>
        private int coresWon, shownChange;
        private bool shownChangeKnown;
        private bool firstWinToday;

        /// <param name="outcome">1 won, 0 a draw, -1 lost.</param>
        private void ChallengeFinish(int outcome, string score)
        {
            if (chPhase == ChPhase.Done) return;
            chPhase = ChPhase.Done;
            if (levelTps) { levelTps = false; ExitTps(); } // the result card upright
            bool won = outcome > 0, draw = outcome == 0;
            Monetization.Ads.NoteResult(won || draw);
            // The challenge's quests: a duel played, won, won alone or against a live rival.
            ChallengeQuests.Report(DuelGoal.Play);
            if (won) { ChallengeQuests.Report(DuelGoal.Win); if (!chDuelSquads) ChallengeQuests.Report(DuelGoal.SoloWin); if (chLive) ChallengeQuests.Report(DuelGoal.LiveWin); }
            // A duel won brings a chest: the better the streak and the health left, the better the chest. A friend's
            // match brings none (nor touches the streak), and pays gold only for the day's first three.
            bool friendly = (chLive && !liveRanked) || copyMatch;
            chestWon = friendly ? null : DuelChests.OnDuelEnd(won, draw, chOnline || chLive, health);
            bool paid = !friendly || FriendMatchesPaid.Take();
            if (colosseum != null) colosseum.Cheer(won ? 0 : draw ? -1 : 1, 1f);
            hunt.Freeze();
            bool prized = (chOnline || chLive) && paid; // offline practice pays no prize
            int gold = !prized ? 0 : won ? Challenge.WinGold : draw ? Challenge.DrawGold : Challenge.LossGold;
            if (!paid) { coinsThisRun = 0; comboBonus = 0; } // (nor the coins picked up in it)
            // Core parts: 3 a win, 2 a draw, 1 a loss; the day's first win triples its gold, brings 10 more and a bronze
            // chest; every fifth win in a row 5 more. (A friend's match brings none.)
            int cores = friendly || !(chOnline || chLive) ? 0 : won ? 3 : draw ? 2 : 1;
            firstWinToday = won && cores > 0 && DuelDay.TakeFirstWin();
            if (firstWinToday) { gold *= 3; cores += 10; DuelChests.Add(ChestTier.Mid); }
            if (won && cores > 0 && DuelChests.Streak > 0 && DuelChests.Streak % 5 == 0) cores += 5;
            CoreParts.Add(cores);
            coresWon = cores;
            SaveData.Coins += gold + (draw ? 0 : Earned);
            SettleProgress(won, draw, friendly, gold, cores, firstWinToday);
            PlayStyle.EndDuel(chDuelMinutes * 60f - chDuelLeft); // (its style: blows, shields, hearts)
            if (lastRewards != null) lastRewards.eventLine = SettleEvent(won); // (the week's wins, the tournament's round)
            // the score with the friend (a friend match or a copy duel); a win over a copy tells the friend
            if (friendly && rival != null)
            {
                Rivalry.Record(rival.name, won, draw);
                if (copyMatch) cameraRig.StartCoroutine(FoiNet.FriendCopyResult(rival.name, won));
            }
            State = GameState.Result;
            danceAt = won ? robot.transform : !draw ? hunt.ChallengeFoeModel : null;
            // the faces: the winner grins, the loser sulks; a draw, both shrug
            FaceFor(robot.Visual, won ? Visual.FaceMoment.Victory : draw ? Visual.FaceMoment.Draw : Visual.FaceMoment.Defeat);
            FaceFor(hunt.ChallengeFoeModel, won ? Visual.FaceMoment.Defeat : draw ? Visual.FaceMoment.Draw : Visual.FaceMoment.Victory);
            if (won) AudioManager.PlaySfx(Sfx.Win);
            if (danceAt != null) { winnerWalking = true; cameraRig.StartCoroutine(WinnerToMiddle(won)); }
            ui.SetRivalHealth(false, 0f); // (the end screen: the health bars go)
            ui.HideCapeViews();
            ui.SetHealth(false, health, 0f);
            chUi.HideAll();
            cameraRig.StartCoroutine(ShowChallengeResult(won, draw, score, gold));
        }

        /// <summary>
        /// The result card, with its trophies: a random live match's from the server (sent with its result), a match
        /// against a virtual rival's once the server has taken the result (half a live match's), a friend's: none.
        /// </summary>
        private IEnumerator ShowChallengeResult(bool won, bool draw, string score, int gold)
        {
            int bestBefore = Challenge.BestRank;
            shownChange = 0; shownChangeKnown = false;
            string trophies;
            if (chLive && liveLost) trophies = Loc.T("live.lost");
            else if (chLive && liveRanked)
            {
                if (liveProfile != null) Challenge.Sync(liveProfile);
                trophies = TrophyLine(liveChange); shownChange = liveChange; shownChangeKnown = true;
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
                trophies = result != null ? TrophyLine(result.change) : Loc.T("ch.resultLater"); if (result != null) { shownChange = result.change; shownChangeKnown = true; }
            }
            else trophies = Loc.T("ch.offline");
            // A new rank reached for the first time: 30 core parts a step and a gold chest.
            string rankUp = "";
            if (Challenge.BestRank > bestBefore)
            {
                int steps = Challenge.BestRank - bestBefore;
                CoreParts.Add(30 * steps);
                DuelChests.Add(ChestTier.Advanced);
                coresWon += 30 * steps;
                rankUp = "\n" + Loc.F("ch.rankUpGift", Challenge.RankName(Challenge.BestRank));
            }
            string line = score + "  ·  " + Loc.F(won ? "ch.wonLine" : draw ? "ch.drawLine" : "ch.lostLine", rival.name)
                          + (chLive && liveLeft ? "\n" + Loc.T("live.rivalLeftWin") : "")
                          + "\n" + trophies + (gold > 0 ? " · +" + gold + " " + Loc.T("ch.gold") : "") + (coresWon > 0 ? " · " + Loc.F("login.cores", "+" + coresWon) : "")
                          + (firstWinToday ? "\n" + Loc.T("ch.firstWin") : "") + rankUp
                          + (won && chestWon.HasValue ? "\n" + Loc.F("chests.won", DuelChestScreen.TierName(chestWon.Value)) : "")
                          + "\n" + Challenge.RankName(Challenge.Rank) + " · " + Loc.F("ch.trophies", Challenge.Trophies);
            // The winner walks to the middle and finishes its dance first, the duel's camera on it (then the card).
            float walkUntil = Time.realtimeSinceStartup + 2.2f;
            while (winnerWalking && challengeRun && Time.realtimeSinceStartup < walkUntil) yield return null;
            var dancer = won ? (robot.Visual != null ? robot.Visual.GetComponentInChildren<SkinnedHero>() : null)
                : !draw && hunt.ChallengeFoeModel != null ? hunt.ChallengeFoeModel.GetComponentInChildren<SkinnedHero>() : null;
            float danceUntil = Time.realtimeSinceStartup + 4.5f; // (a long dance never holds the rewards back)
            yield return null;
            while (dancer != null && dancer.PlayingMove && Time.realtimeSinceStartup < danceUntil && challengeRun) yield return null;
            // The rewards one by one (rank points, XP, mastery, gold, cores, chest), then the card.
            bool rewardsShown = false, revengeTaken = false;
            if (lastRewards != null && challengeRun)
            {
                lastRewards.cores = coresWon;
                lastRewards.trophies = shownChange;
                lastRewards.trophiesKnown = shownChangeKnown;
                if (rankUp.Length > 0) lastRewards.eventLine = (string.IsNullOrEmpty(lastRewards.eventLine) ? "" : lastRewards.eventLine + "\n") + rankUp.Trim(); // (a new rank's gift: shown here, the old card is gone)
                bool shut = false;
                bool friendlyLoss = !won && !draw && rival != null && ((chLive && !liveRanked) || copyMatch);
                string revengeOn = friendlyLoss ? rival.name : null;
                string shareLine = Loc.F("share.line", Loc.T(won ? "ch.victory" : draw ? "ch.draw" : "ch.defeat"), score, Challenge.RankName(Challenge.Rank))
                                   + (Share.MyCode.Length > 0 ? " " + Loc.F("share.code", Share.MyCode) : "") + " " + Share.Link;
                string liveRival = chLive && liveRanked && rival != null ? rival.name : null;
                ui.DuelRewardsView.SetExtras(shareLine, liveRival, n => cameraRig.StartCoroutine(FoiNet.AddFriend(n, (e, f) => { })));
                rewardsShown = true;
                int choice = 0;
                var actions = EndActions(won, draw, gold, c => { choice = c; shut = true; });
                ui.DuelRewardsView.Show(lastRewards, won, draw, actions, () => shut = true, revengeOn, () => { shut = true; revengeTaken = true; Revenge(revengeOn); });
                while (!shut && challengeRun) yield return null;
                if (revengeTaken) yield break; // (revenge chosen: the new match is under way)
                if (challengeRun) { EndChoice(choice); yield break; } // (play again, a rematch, or the menu)
            }
            // The rewards were the duel's result: straight back to the menu (an interstitial may come first). Only a live
            // match with a rematch offered still shows the old card (its REMATCH button).
            if (rewardsShown && !(chLive && rematchMatch != 0))
            {
                if (revengeTaken) yield break; // (revenge chosen: the new match is under way)
                Time.timeScale = 1f;
                Monetization.Ads.AfterDuel(ShowMenu);
                yield break;
            }
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

        /// <summary>A duel stage's own models and pictures (loaded for it) let go a frame after the stage went.</summary>
        private IEnumerator FreeStage()
        {
            yield return null;
            Resources.UnloadUnusedAssets();
        }

        /// <summary>Leaving a challenge (the menu): the board and its screens go; a match the server knows of is lost.</summary>
        private void EndChallenge()
        {
            if (ui != null && ui.DuelRewardsView != null) ui.DuelRewardsView.Hide(); // (left another way: the end screen goes too)
            EndTutorial(false);
            EndTraining();
            FacesOff();
            hunt.LaneX = null;
            copyMatch = false;
            ClearDuelRule();
            // Walked out of a duel under way: the leaver's penalty (rank points); before it began, a plain loss.
            if (chMatchId != 0 && chPhase != ChPhase.Done) SendChallengeResult(false, left: chPhase == ChPhase.Duel || chPhase == ChPhase.Break);
            chLobbyToken++; // a waiting room still searching stops there
            if (levelTps) { levelTps = false; ExitTps(); }
            EndLive();
            if (!challengeRun && chPhase == ChPhase.Off) return;
            challengeRun = false;
            chPhase = ChPhase.Off;
            ClearDuel();
            cameraRig.StartCoroutine(FreeStage()); // (the stage's models and pictures out of memory once it is gone)
            if (robot != null && robot.Visual != null) robot.Visual.gameObject.SetActive(true);
            if (capesOff && robot != null) { capesOff = false; robot.ApplyOutfit(Cosmetics.Outfit()); } // (its cape back on: it was hung up for the duel)
            if (chUi != null) chUi.HideAll();
            if (rivalLook != null) Destroy(rivalLook.gameObject);
            rivalLook = null;
            if (colosseum != null) Destroy(colosseum.gameObject);
            if (duelSky) { duelSky = false; cameraRig.SetBackdrop(null); }
            colosseum = null;
            Visual.CapeShowcase.ClearAll(); // (the capes on the screen go too)
            ui.HideCapeViews();
            ui.SetRivalHealth(false, 0f);
            hunt.ClearChallengeFoe();
        }

        /// <summary>
        /// The duel filmed like a football match: the camera sits high to the east at the halfway line (the player's side
        /// on the left, the rival's on the right), never turning; it keeps both foi in view (their middle, further back the
        /// further apart they are), with a still zone round the middle of the view so small steps do not shake it.
        /// </summary>
        /// <summary>The duel camera's side: straight from the stage's open side, east (where its old seat stood); it never turns.</summary>
        private static readonly Vector3 sideDir = Vector3.right;
        private bool sideKnown;
        private float sideDist = 14f;

        /// <summary>
        /// The duel's camera: fixed, straight from the stage's open side (east), close and a little above, following the
        /// middle of the two foi and backing off as they move apart (the partners in view in 2v2). It never turns.
        /// </summary>
        /// <summary>How far the duel camera may slide sideways on a city stage (its landmark kept in the frame).</summary>
        private const float CityPan = 1.5f;
        /// <summary>A city stage's first shot: how much lower than the others and how far its head is tilted up.</summary>
        private const float CityStartLower = 5f, CityStartTilt = 13f;

        private Vector3 frontMid;
        /// <summary>How fast the duel's view follows the two (per second; a place's jump glides over about half a second).</summary>
        private const float FrontFollow = 5f;

        private void FrontPose(out Vector3 pos, out Quaternion rot)
        {
            var me = robot.transform.position;
            var foe = hunt.ChallengeFoeAt ?? me;
            bool fresh = !sideKnown;
            if (!sideKnown) { sideKnown = true; sideDist = 22f; }
            // everyone to keep in view: the two foi, and the partners in 2v2
            var points = new System.Collections.Generic.List<Vector3> { me, foe };
            var mid = Vector3.zero;
            foreach (var p in points) mid += p;
            mid /= points.Count;
            // followed smoothly: a leap over the other (or any jump of a place) glides the view there, never a cut
            frontMid = fresh ? mid : Vector3.Lerp(frontMid, mid, 1f - Mathf.Exp(-FrontFollow * Time.unscaledDeltaTime));
            mid = frontMid;
            var right = Vector3.Cross(Vector3.up, -sideDir);
            // a stage with a wide landmark: the camera slides with the fight, but its view never past the landmark's ends;
            // a city's tower: the view held on it (the camera barely slides sideways, only nearer and further)
            if (colosseum != null && colosseum.HasLandmark)
            {
                float hf = 2f * Mathf.Atan(Mathf.Tan(Combat.DuelCameraSettings.Current.fov * Mathf.Deg2Rad * 0.5f) * Mathf.Max(0.3f, cameraRig.Cam.aspect));
                float w = Mathf.Max(5f, mid.x + sideDist - colosseum.LandmarkX) * Mathf.Tan(hf * 0.5f);
                float lo = colosseum.LandmarkLeft + w, hi = colosseum.LandmarkRight - w;
                float z = lo <= hi ? Mathf.Clamp(mid.z, lo, hi) : (colosseum.LandmarkLeft + colosseum.LandmarkRight) * 0.5f;
                mid.z = Mathf.Clamp(z, boardCentre.z - CityPan * 3f, boardCentre.z + CityPan * 3f); // (never so far the fight leaves the view)
            }
            else if (Stage.theme == DuelStands.Theme.Istanbul || Stage.theme == DuelStands.Theme.Bosphorus)
            {
                float side = Vector3.Dot(mid - boardCentre, right);
                mid -= right * (side - Mathf.Clamp(side, -CityPan, CityPan));
            }
            CityFar();
            float half = 0f, depth = 0f;
            foreach (var p in points) { half = Mathf.Max(half, Mathf.Abs(Vector3.Dot(p - mid, right))); depth = Mathf.Max(depth, Vector3.Dot(p - mid, sideDir)); }
            var cs = Combat.DuelCameraSettings.Current;
            half += cs.sideRoom; // (room for an arm, a weapon, a fall, and the floor round them)
            var cam = cameraRig.Cam;
            float vfov = cs.fov * Mathf.Deg2Rad;
            float hfov = 2f * Mathf.Atan(Mathf.Tan(vfov * 0.5f) * Mathf.Max(0.3f, cam.aspect));
            float want = Mathf.Clamp(half / Mathf.Tan(hfov * 0.5f) + depth, cs.minDistance, cs.maxDistance);
            if (overtime) want *= cs.overtimeCloser; // (the golden knock-down: a touch nearer)
            sideDist = Mathf.Lerp(sideDist, want, Mathf.Min(1f, Time.unscaledDeltaTime * 2f));
            // looked at above their feet: the two stand in the band from about 30% to 60% of the screen's height
            var focus = mid + Vector3.up * (sideDist * cs.focusPerDistance);
            pos = mid + sideDir * sideDist + Vector3.up * (cs.heightBase + sideDist * cs.heightPerDistance);
            rot = Quaternion.LookRotation(focus - pos);
            PhysCamera(ref pos, ref rot, focus); // (the body attacks' moments: a tilt, a rise after a launched one, a zoom)
        }

        /// <summary>A city stage: the camera sees far (Galata's tower far up its street, the palace far across the Bosphorus),
        /// from the first shot of the stage on.</summary>
        private void CityFar()
        {
            if ((Stage.theme == DuelStands.Theme.Istanbul || Stage.theme == DuelStands.Theme.Bosphorus) && cameraRig.Cam.farClipPlane < 560f) cameraRig.Cam.farClipPlane = 560f;
        }

        private void BoardPose(out Vector3 pos, out Quaternion rot)
        {
            CityFar();
            if (chPhase == ChPhase.Duel) { FrontPose(out pos, out rot); DancePose(ref pos, ref rot); return; }
            sideKnown = false;
            var seat = boardCentre + new Vector3(BoardW * 0.5f + 11f, 12.5f, 0f);
            var look = (boardCentre - seat).normalized;
            var me = robot.transform.position;
            var foe = hunt.ChallengeFoeAt ?? me;
            var aim = (me + foe) * 0.5f - boardCentre;
            aim.y = 0f;
            var gap = aim - duelPan;
            if (gap.magnitude > 1f) duelPan += gap.normalized * (gap.magnitude - 1f) * Mathf.Min(1f, Time.deltaTime * 2.5f);
            float apart = new Vector2(me.x - foe.x, me.z - foe.z).magnitude;
            duelBack = Mathf.Lerp(duelBack, 26f + Mathf.Clamp(apart - 3f, 0f, 14f) * 1.2f, Time.unscaledDeltaTime * 2f); // (no closing in, even for the ultimate)
            pos = boardCentre + duelPan - look * (duelBack + 11f); // (every stage further back: all of the floor, its stands and the land in view)
            // The grove: the view a little further in (the platform lower on the screen, its stands and trees above it in view)
            if (DuelStands.IsMade(Stage.theme)) pos += new Vector3(-4.5f, 0f, 0f) - look * 5f; // (and a little further back: all of it in view)
            rot = Quaternion.LookRotation(look) * Quaternion.Euler(-3.5f, 0f, 0f); // (its head up a little: less floor under the platform, still some room under it)
            // a city stage: lower and its head well up, the landmark in the first shot (Galata's tower, the Maiden's Tower, Big Ben)
            if (Stage.theme == DuelStands.Theme.Istanbul || Stage.theme == DuelStands.Theme.Bosphorus || Stage.theme == DuelStands.Theme.London)
            {
                pos += Vector3.down * CityStartLower;
                rot *= Quaternion.Euler(-CityStartTilt, 0f, 0f);
            }
            OrbitPose(ref pos, ref rot, me, foe);
            DancePose(ref pos, ref rot);
        }

        /// <summary>The winner's dance: the camera comes in on it (about halfway), looking at it.</summary>
        private void DancePose(ref Vector3 pos, ref Quaternion rot)
        {
            danceCam = Mathf.MoveTowards(danceCam, danceAt != null && State == GameState.Result ? 1f : 0f, Time.unscaledDeltaTime * 0.8f);
            if (danceCam > 0f && danceAt != null)
            {
                var target = danceAt.position + Vector3.up * 1.6f;
                var near = target + (pos - target) * 0.5f;
                float w = Mathf.SmoothStep(0f, 1f, danceCam);
                pos = Vector3.Lerp(pos, near, w);
                rot = Quaternion.Slerp(rot, Quaternion.LookRotation(target - near), w);
            }
        }

        // ---------- The slow-motion camera ----------

        /// <summary>Until when (unscaled time) a big moment's slow motion is filmed close, and round what.</summary>
        private float slowCamUntil, slowCamWeight, slowCamAngle;
        private Vector3 slowCamCentre;

        /// <summary>The camera goes in close and circles the two foi while the duel is in slow motion (a big blow, the ultimate).</summary>
        private bool SlowCamOn => false; // (the duel's camera stays on the board in a big moment: it shakes instead)

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
            if (!SlowCamOn) return; // no close-up circling any more (the user asked): the board's view with a shake
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
            slowCamAngle += dt * 22f; // a slow turn
            float a = slowCamAngle * Mathf.Deg2Rad;
            float apart = Mathf.Clamp(new Vector2(me.x - foe.x, me.z - foe.z).magnitude, 0f, 6f);
            float radius = 10.5f + apart * 0.6f; // only a touch closer than the board's view
            var orbit = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * (7.5f + apart * 0.3f);
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
