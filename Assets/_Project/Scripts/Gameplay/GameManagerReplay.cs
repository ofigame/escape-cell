using System.Collections;
using SquashBot.Audio;
using SquashBot.Core;
using SquashBot.Data;
using SquashBot.UI;
using UnityEngine;

namespace SquashBot.Gameplay
{
    /// <summary>
    /// "One more duel" (the design's §8): as soon as the duel's rewards show, the next rival is got ready in the
    /// background (the army goes up, a rival is found on the server) so PLAY AGAIN goes straight to its card and the
    /// stage wheel; MENU drops a rival found ahead (no result, so it is never counted lost). A computer rival asks for a
    /// rematch now and then (30%); a win's gold can be doubled for an ad.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The background search: 0 looking, 1 ready (a rival found, or none to find: offline).</summary>
        private int preSearch;
        private FoiNet.Match preMatch;
        private int preToken;
        /// <summary>The computer rival that asked for a rematch (the next lobby takes it).</summary>
        private Challenge.Opponent rematchBot;
        private const float BotRematchChance = 0.3f, DoubleGoldADay = 5f;

        /// <summary>The next ordinary duel's rival got ready while the rewards show (nothing for a friend's or an event's).</summary>
        private IEnumerator PreSearch(int token)
        {
            preSearch = 0;
            preMatch = null;
            bool ok = false;
            yield return FoiNet.EnsurePlayer(r => ok = r);
            if (token != preToken) yield break;
            if (ok)
            {
                yield return FoiNet.SendPending(r => { if (r != null) Challenge.Sync(r.profile); });
                yield return FoiNet.CancelAhead();
                int world = LevelCatalog.WorldOf(Mathf.Max(0, SaveData.UnlockedLevel - 1));
                var points = new int[Army.Groups];
                for (int g = 0; g < Army.Groups; g++) points[g] = Army.GroupPoints(g);
                yield return FoiNet.PutArmy(points, Army.EarnedPoints, world, Challenge.FoiMaxHp, Challenge.FoiHitFor(world), 0.5f,
                    Armory.Equipped.id, RobotParts.Level(RobotPart.Arms), RobotParts.Armor, Shields.All.IndexOf(Shields.Equipped) + 1, r => ok = r);
                if (token != preToken) yield break;
                if (ok)
                {
                    FoiNet.Match m = null;
                    yield return FoiNet.Find(x => m = x);
                    if (token != preToken) { if (m != null) { FoiNet.NoteAhead(m.matchId); cameraRig.StartCoroutine(FoiNet.CancelAhead()); } yield break; }
                    preMatch = m;
                    if (m != null) FoiNet.NoteAhead(m.matchId);
                }
            }
            preSearch = 1;
        }

        /// <summary>What the end screen offers this duel: PLAY AGAIN, the rematch, MENU, the doubled gold, the tip.</summary>
        private DuelEndActions EndActions(bool won, bool draw, int gold, System.Action<int> choose)
        {
            bool ordinary = chMode == 1 && chFriend == 0 && !copyMatch && !(chLive && !liveRanked);
            var a = new DuelEndActions
            {
                score = ScoreText(),
                replay = () => choose(1),
                menu = () => choose(0),
                searchState = () => preSearch,
            };
            if (chLive && rematchMatch != 0) a.rematch = () => choose(2);
            else if (!chLive && ordinary && rival != null && Random.value < BotRematchChance) { a.rematch = () => choose(3); a.botAsks = true; }
            if (won && gold > 0 && Monetization.Ads.Rewarded.IsReady && DoubleGoldToday < DoubleGoldADay)
                a.doubleGold = done =>
                {
                    done(false); // (the button comes back if the card is turned down)
                    ui.AdOffer.Show(Loc.T("rw.doubleTitle"), Loc.F("rw.doubleBody", gold), () =>
                        Monetization.Ads.Rewarded.Show(ok =>
                        {
                            if (!ok) return;
                            Monetization.Ads.MarkFullScreenShown();
                            SaveData.Coins += gold;
                            DoubleGoldToday++;
                            AudioManager.PlaySfx(Sfx.Coin, 1f, 1.2f);
                            done(true);
                        }));
                };
            if (!won && !draw) { a.tip = DefeatTip(out a.tipHow); }
            preToken++;
            if (ordinary && !chLive) cameraRig.StartCoroutine(PreSearch(preToken));
            else preSearch = 1;
            return a;
        }

        /// <summary>Gold doubled for an ad today (five a day at most).</summary>
        private static int DoubleGoldToday
        {
            get => Prefs.GetString("sb_dbl_day", "") == System.DateTime.Now.ToString("yyyyMMdd") ? Prefs.GetInt("sb_dbl_n", 0) : 0;
            set { Prefs.SetString("sb_dbl_day", System.DateTime.Now.ToString("yyyyMMdd")); Prefs.SetInt("sb_dbl_n", value); Prefs.Save(); }
        }

        /// <summary>
        /// The end screen's choice: 0 the menu (a rival found ahead is dropped), 1 play again (straight to the rival found
        /// ahead, else the usual search), 2 a live rival's rematch, 3 the computer rival's rematch.
        /// </summary>
        private void EndChoice(int choice)
        {
            int mode = chMode;
            Time.timeScale = 1f;
            switch (choice)
            {
                case 1:
                {
                    var ahead = preSearch >= 1 ? preMatch : null;
                    preToken++;
                    preMatch = null;
                    EndChallenge();
                    if (mode >= 2 && mode <= 3) { cameraRig.StartCoroutine(EventDuel(mode)); return; }
                    if (ahead != null) { StartAhead(ahead); return; }
                    StartChallenge(false);
                    return;
                }
                case 2:
                {
                    long id = rematchMatch;
                    preToken++;
                    EndChallenge();
                    StartRematch(id);
                    return;
                }
                case 3:
                {
                    var again = rival;
                    var ahead = preSearch >= 1 ? preMatch : null;
                    preToken++;
                    preMatch = null;
                    EndChallenge();
                    rematchBot = again;
                    if (ahead != null) { StartAhead(ahead); return; }
                    StartChallenge(false);
                    return;
                }
                default:
                    preToken++;
                    preMatch = null;
                    cameraRig.StartCoroutine(FoiNet.CancelAhead());
                    Monetization.Ads.AfterDuel(ShowMenu);
                    return;
            }
        }

        /// <summary>PLAY AGAIN with a rival found ahead: the room only shows its card a moment, then the stage wheel.</summary>
        private void StartAhead(FoiNet.Match ahead)
        {
            if (chUi == null) InitChallenge();
            if (State != GameState.Menu) ShowMenu();
            Armory.RevokeLostRanks();
            chCancelled = false;
            chFriend = 0;
            chPhase = ChPhase.Lobby;
            cameraRig.StartCoroutine(Lobby(ahead));
        }

        /// <summary>The waiting room with a rival found ahead (the army already went up): its card, then the board.</summary>
        private IEnumerator LobbyAhead(FoiNet.Match ahead, int token, float started)
        {
            chUi.SetLobbyStatus(Loc.T("ch.found"));
            yield return LobbyFound(ahead, token, started, 0.3f);
        }
    }
}
